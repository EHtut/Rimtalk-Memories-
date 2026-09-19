using RimWorld;
using Verse;

namespace Arkh.Talk
{
    /// <summary>
    /// Whether a colonist can speak right now.
    ///
    /// Ordered cheapest first: this runs over every colonist on a scheduling tick, so the null and
    /// flag checks come before anything that touches a tracker.
    ///
    /// Every rejection carries a reason, because "nobody is talking" is otherwise the hardest
    /// symptom in this whole mod to diagnose — it looks identical whether the cause is a missing
    /// API key, a scheduling bug, or simply everyone being asleep.
    /// </summary>
    public static class TalkEligibility
    {
        public static bool CanSpeak(Pawn pawn, out string reason)
        {
            if (pawn == null) { reason = "no pawn"; return false; }
            if (pawn.Dead) { reason = "dead"; return false; }
            if (pawn.Destroyed) { reason = "destroyed"; return false; }
            if (!pawn.Spawned) { reason = "not on a map"; return false; }
            if (pawn.Map == null) { reason = "no map"; return false; }

            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
            {
                reason = "not humanlike";
                return false;
            }

            if (pawn.Downed) { reason = "downed"; return false; }
            if (pawn.InMentalState) { reason = "in a mental state"; return false; }
            if (pawn.Drafted) { reason = "drafted"; return false; }

            if (!pawn.Awake()) { reason = "asleep"; return false; }

            var state = TalkStates.For(pawn);
            if (state != null && state.Generating) { reason = "already generating"; return false; }

            reason = null;
            return true;
        }

        /// <summary>
        /// True once enough time has passed since this colonist last said anything.
        ///
        /// Chattiness scales the wait rather than acting as a dice roll, so a talkative colonist is
        /// reliably more talkative instead of merely luckier — the difference is visible over a
        /// long colony and invisible over a short test.
        /// </summary>
        public static bool IsDue(PawnTalkState state, int nowTick, int intervalTicks)
        {
            if (state == null) return false;
            if (state.LastSpokenTick <= 0) return true;

            float scale = state.Chattiness <= 0.01f ? 100f : 1f / state.Chattiness;
            return nowTick - state.LastSpokenTick >= intervalTicks * scale;
        }
    }
}
