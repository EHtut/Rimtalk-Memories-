using System.Collections.Generic;
using System.Linq;
using System.Text;
using Arkh.Model;
using Verse;

namespace Arkh.Prompt
{
    /// <summary>The moment a prompt is being built for.</summary>
    public sealed class TalkScene
    {
        public Pawn Initiator;
        public List<Pawn> Participants = new List<Pawn>();
        public Map Map;

        /// <summary>What prompted this line — the occasion, in plain words.</summary>
        public string Trigger = "";

        public bool IsMonologue => Participants.Count <= 1;

        public string ParticipantNames =>
            string.Join(", ", Participants.Where(p => p != null).Select(Describe).ToArray());

        public static string Describe(Pawn pawn) => pawn?.LabelShort ?? "someone";
    }

    /// <summary>
    /// Turns the scene and the section catalogue into the messages we send.
    ///
    /// Runs on the main thread, during a tick, because it reads pawn state — so it does lookups
    /// and string building and nothing else. Anything expensive belongs behind a cache in the
    /// section that needs it.
    ///
    /// Sections are built lazily and only when their slot is reached, which is the one lesson
    /// worth taking from how RimTalk did this: it computed its whole context block whether or not
    /// the prompt referenced it, and paid for that on every tick that produced a line.
    /// </summary>
    public static class PromptAssembler
    {
        public static List<ChatMessage> Build(TalkScene scene)
        {
            var messages = new List<ChatMessage>();
            if (scene == null) return messages;

            var system = new StringBuilder();

            Append(system, Once(scene, PromptSlot.SystemInstruction));
            Append(system, Once(scene, PromptSlot.OutputContract));
            Append(system, Once(scene, PromptSlot.WorldContext));

            // Per-participant blocks are labelled by name. Without the label a two-pawn prompt
            // reads as one contradictory person, and the model writes them as one.
            foreach (var pawn in scene.Participants)
            {
                if (pawn == null) continue;

                var forPawn = new StringBuilder();
                Append(forPawn, PerPawn(pawn, PromptSlot.PawnContext));
                Append(forPawn, PerPawn(pawn, PromptSlot.Memory));

                if (forPawn.Length == 0) continue;

                Append(system, TalkScene.Describe(pawn) + ":\n" + forPawn.ToString().TrimEnd());
            }

            Append(system, Once(scene, PromptSlot.Conversation));

            if (system.Length > 0) messages.Add(ChatMessage.System(system.ToString().TrimEnd()));

            string trigger = Once(scene, PromptSlot.Trigger);
            if (string.IsNullOrEmpty(trigger)) trigger = scene.Trigger;
            if (string.IsNullOrEmpty(trigger)) trigger = DefaultTrigger(scene);

            messages.Add(ChatMessage.User(trigger));
            return messages;
        }

        /// <summary>
        /// What we ask for when nothing more specific prompted the line. Naming who should speak
        /// matters: without it the model tends to answer as a narrator, or as everyone at once.
        /// </summary>
        private static string DefaultTrigger(TalkScene scene)
        {
            string speaker = TalkScene.Describe(scene.Initiator);

            if (scene.IsMonologue)
            {
                return speaker + " is alone. Write what they say out loud to themselves, if anything.";
            }

            return speaker + " speaks to " + scene.ParticipantNames + ". Write what is said.";
        }

        private static string Once(TalkScene scene, PromptSlot slot)
        {
            var parts = new StringBuilder();

            foreach (var section in PromptCatalog.InPromptOrder())
            {
                if (section.Slot != slot) continue;
                if (section.MapProvider == null) continue;
                Append(parts, PromptCatalog.Build(section, scene.Map));
            }

            return parts.ToString().TrimEnd();
        }

        private static string PerPawn(Pawn pawn, PromptSlot slot)
        {
            var parts = new StringBuilder();

            foreach (var section in PromptCatalog.InPromptOrder())
            {
                if (section.Slot != slot) continue;
                if (section.PawnProvider == null) continue;
                Append(parts, PromptCatalog.Build(section, pawn));
            }

            return parts.ToString().TrimEnd();
        }

        private static void Append(StringBuilder sb, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(text);
        }
    }
}
