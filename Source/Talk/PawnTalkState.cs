using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Arkh.Talk
{
    /// <summary>What Arkh remembers about a colonist between lines, within a session.</summary>
    public sealed class PawnTalkState
    {
        public Pawn Pawn;

        /// <summary>Zero means "never", which is why a fresh colonist is due immediately.</summary>
        public int LastSpokenTick;

        /// <summary>True while a request for this pawn is in flight.</summary>
        public bool Generating;

        /// <summary>
        /// Scales how often this colonist starts something. One is ordinary; the intent is that a
        /// future persona feature moves it, so a taciturn pawn is genuinely taciturn rather than
        /// merely unlucky.
        /// </summary>
        public float Chattiness = 1f;

        public PawnTalkState(Pawn pawn)
        {
            Pawn = pawn;
        }
    }

    /// <summary>
    /// Per-pawn talk state, created on demand.
    ///
    /// Session state, not save state: how long ago someone last spoke does not deserve to be
    /// written into a save, and rebuilding it on load costs nothing. What a colonist *remembers*
    /// is a different question and belongs in a GameComponent — see DESIGN.md §3.1.
    /// </summary>
    public static class TalkStates
    {
        private static readonly Dictionary<Pawn, PawnTalkState> States = new Dictionary<Pawn, PawnTalkState>();

        public static PawnTalkState For(Pawn pawn)
        {
            if (pawn == null) return null;

            if (!States.TryGetValue(pawn, out var state))
            {
                state = new PawnTalkState(pawn);
                States[pawn] = state;
            }

            return state;
        }

        public static int Count => States.Count;

        /// <summary>
        /// Drops entries for pawns that are gone.
        ///
        /// Keying a long-lived dictionary by Pawn keeps dead colonists alive in memory for as long
        /// as the session lasts. Cheap to prevent, tedious to diagnose later as a slow leak in a
        /// hundred-hour colony.
        /// </summary>
        public static void Prune()
        {
            var stale = States.Keys.Where(p => p == null || p.Destroyed || p.Dead).ToList();
            foreach (var pawn in stale) States.Remove(pawn);
        }

        public static void Clear() => States.Clear();
    }
}
