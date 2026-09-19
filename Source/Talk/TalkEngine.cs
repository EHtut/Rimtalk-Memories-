using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Arkh.Model;
using Arkh.Prompt;
using Arkh.Util;
using Verse;

namespace Arkh.Talk
{
    /// <summary>
    /// Decides who speaks and when, sends the request, and hands back what came out.
    ///
    /// The thread split is the load-bearing part. Selection and prompt assembly happen on the main
    /// thread, where reading pawn state is safe; the request goes to a worker; the reply comes back
    /// through a queue and is drained on the main thread. **Nothing outside <see cref="Dispatch"/>
    /// runs off the main thread, and Dispatch touches no game state** — it is handed finished
    /// messages and a client and knows nothing about pawns.
    ///
    /// Display is not this class's job. It raises <see cref="Spoke"/> and stops; who draws the line
    /// is P4's problem.
    /// </summary>
    public static class TalkEngine
    {
        private sealed class Delivery
        {
            public TalkScene Scene;
            public Completion Completion;
        }

        private static readonly ConcurrentQueue<Delivery> Inbox = new ConcurrentQueue<Delivery>();

        private static int _inFlight;

        /// <summary>Raised on the main thread, once per line, after a reply is parsed.</summary>
        public static event Action<TalkScene, SpokenLine> Spoke;

        // --- Session diagnostics, for the profile panel ---------------------------------------

        public static int InFlight => _inFlight;
        public static int Requests;
        public static int Failures;
        public static int PromptTokens;
        public static int CompletionTokens;
        public static ModelFailure LastFailure;
        public static string LastBlockReason = "nothing has been tried yet";

        /// <summary>The last few lines, newest last. Small on purpose; this is a diagnostic, not a log.</summary>
        public static readonly List<SpokenLine> Recent = new List<SpokenLine>();

        public static void ResetSession()
        {
            while (Inbox.TryDequeue(out _)) { }
            _inFlight = 0;
            Requests = Failures = PromptTokens = CompletionTokens = 0;
            LastFailure = null;
            LastBlockReason = "nothing has been tried yet";
            Recent.Clear();
            TalkStates.Clear();
        }

        // --- Starting ---------------------------------------------------------------------------

        /// <summary>
        /// Tries to start one conversation. Main thread only. Returns false with
        /// <see cref="LastBlockReason"/> set whenever nothing happened, which is most ticks — and
        /// the reason is recorded because "nobody is talking" is otherwise indistinguishable
        /// between a dozen very different causes.
        /// </summary>
        public static bool TryStart(Map map)
        {
            var settings = ArkhMod.Settings;

            if (settings == null || !settings.Enabled) return Blocked("mod disabled");
            if (!settings.TalkEnabled) return Blocked("conversations switched off in settings");
            if (map == null) return Blocked("no map");

            if (_inFlight >= Math.Max(1, settings.MaxInFlight))
            {
                return Blocked("at the in-flight limit (" + settings.MaxInFlight + ")");
            }

            var client = ModelProviders.Create(settings);
            if (!client.Configured)
            {
                return Blocked("no API key set for " + client.Name);
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            int interval = Math.Max(1, settings.TalkIntervalSeconds) * 60;

            var initiator = ChooseInitiator(map, now, interval);
            if (initiator == null) return Blocked("no colonist is eligible and due");

            var scene = BuildScene(map, initiator, settings);
            if (scene == null) return Blocked("nobody to talk to, and monologues are off");

            var messages = PromptAssembler.Build(scene);
            if (messages.Count == 0) return Blocked("the prompt came out empty");

            foreach (var pawn in scene.Participants)
            {
                var state = TalkStates.For(pawn);
                if (state != null) state.Generating = true;
            }

            var options = ModelProviders.OptionsFrom(settings);

            System.Threading.Interlocked.Increment(ref _inFlight);
            Requests++;

            Task.Run(() => Dispatch(scene, messages, client, options));

            LastBlockReason = null;
            return true;
        }

        private static bool Blocked(string reason)
        {
            LastBlockReason = reason;
            return false;
        }

        /// <summary>
        /// The only code here that runs off the main thread, and it is handed everything it needs
        /// so it never reads game state. Failures are values, so nothing thrown escapes into a
        /// worker where RimWorld would never show it.
        /// </summary>
        private static void Dispatch(TalkScene scene, List<ChatMessage> messages, IModelClient client, ModelOptions options)
        {
            Completion completion;
            try
            {
                completion = client.Complete(messages, options);
            }
            catch (Exception ex)
            {
                completion = Completion.Failed(
                    new ModelFailure(FailureKind.Unknown, "The request failed unexpectedly.", ex.Message),
                    client.Name);
            }

            Inbox.Enqueue(new Delivery { Scene = scene, Completion = completion });
        }

        // --- Receiving ---------------------------------------------------------------------------

        /// <summary>Main thread. Turns finished requests into spoken lines.</summary>
        public static void Drain()
        {
            while (Inbox.TryDequeue(out var delivery))
            {
                System.Threading.Interlocked.Decrement(ref _inFlight);
                Complete(delivery);
            }
        }

        private static void Complete(Delivery delivery)
        {
            var scene = delivery.Scene;
            int now = Find.TickManager?.TicksGame ?? 0;

            foreach (var pawn in scene.Participants)
            {
                var state = TalkStates.For(pawn);
                if (state == null) continue;

                state.Generating = false;

                // Stamp the clock even on failure. Otherwise a provider that is down leaves every
                // colonist permanently "due", and the scheduler hammers it once per tick.
                state.LastSpokenTick = now;
            }

            var completion = delivery.Completion;

            if (!completion.Ok)
            {
                Failures++;
                LastFailure = completion.Failure;
                ArkhLog.WarnOnce(completion.Failure.ToString(), completion.Failure.Kind.GetHashCode());
                return;
            }

            PromptTokens += completion.PromptTokens;
            CompletionTokens += completion.CompletionTokens;

            var lines = ResponseContract.Parse(completion.Text, TalkScene.Describe(scene.Initiator));
            if (lines.Count == 0)
            {
                Failures++;
                LastFailure = new ModelFailure(FailureKind.Malformed,
                    "The reply held no usable speech.", Shorten(completion.Text));
                ArkhLog.WarnOnce(LastFailure.ToString(), 0xEEE1);
                return;
            }

            foreach (var line in lines)
            {
                Recent.Add(line);
                if (Recent.Count > 12) Recent.RemoveAt(0);

                ArkhLog.Debug(line.ToString());

                try
                {
                    Spoke?.Invoke(scene, line);
                }
                catch (Exception ex)
                {
                    // A broken listener must not stop the other lines being delivered, nor take
                    // down the tick that drained them.
                    ArkhLog.WarnOnce("A talk listener threw: " + ex, 0xEEE2);
                }
            }
        }

        // --- Choosing ------------------------------------------------------------------------

        private static Pawn ChooseInitiator(Map map, int now, int interval)
        {
            var candidates = new List<Pawn>();
            var weights = new List<float>();

            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (!TalkEligibility.CanSpeak(pawn, out _)) continue;

                var state = TalkStates.For(pawn);
                if (!TalkEligibility.IsDue(state, now, interval)) continue;

                // Weight by how overdue they are, so the colony does not settle into one
                // colonist doing all the talking while another never gets a turn.
                float overdue = state.LastSpokenTick <= 0 ? interval : now - state.LastSpokenTick;
                candidates.Add(pawn);
                weights.Add(Math.Max(1f, overdue) * Math.Max(0.01f, state.Chattiness));
            }

            if (candidates.Count == 0) return null;

            float total = weights.Sum();
            float roll = Rand.Range(0f, total);

            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        private static TalkScene BuildScene(Map map, Pawn initiator, Settings.ArkhSettings settings)
        {
            var scene = new TalkScene
            {
                Initiator = initiator,
                Map = map
            };
            scene.Participants.Add(initiator);

            // Placeholder proximity rule. P6 replaces this with a real earshot model that knows
            // about walls and volume; until then it is a plain radius, and deliberately obvious.
            Pawn nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var other in map.mapPawns.FreeColonistsSpawned)
            {
                if (other == initiator) continue;
                if (!TalkEligibility.CanSpeak(other, out _)) continue;

                float distance = initiator.Position.DistanceTo(other.Position);
                if (distance > settings.TalkRadius) continue;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = other;
                }
            }

            if (nearest != null)
            {
                scene.Participants.Add(nearest);
                return scene;
            }

            return settings.AllowMonologue ? scene : null;
        }

        private static string Shorten(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            return s.Length <= 200 ? s : s.Substring(0, 200) + "…";
        }
    }
}
