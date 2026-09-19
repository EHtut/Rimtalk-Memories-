using Arkh.Util;
using Verse;

namespace Arkh.Talk
{
    /// <summary>
    /// The clock. Drains finished replies every tick and tries to start something on an interval.
    ///
    /// Draining is separate from starting and runs far more often on purpose: a reply that arrives
    /// should reach the player promptly, while deciding *whether* to spend money is a much rarer
    /// and more deliberate act.
    /// </summary>
    public class ArkhGameComponent : GameComponent
    {
        /// <summary>How often to consider starting a conversation. One second of game time.</summary>
        private const int ScheduleEvery = 60;

        /// <summary>Roughly once every eight in-game hours.</summary>
        private const int PruneEvery = 20000;

        private int _ticks;

        public ArkhGameComponent(Game game)
        {
        }

        public override void FinalizeInit()
        {
            // A loaded save is a new session: in-flight requests from the previous game are gone,
            // and carrying their counters over would make the diagnostics lie.
            TalkEngine.ResetSession();
            ArkhLog.Debug("talk engine ready.");
        }

        public override void GameComponentTick()
        {
            TalkEngine.Drain();

            _ticks++;

            if (_ticks % ScheduleEvery == 0)
            {
                TalkEngine.TryStart(Find.CurrentMap);
            }

            if (_ticks % PruneEvery == 0)
            {
                TalkStates.Prune();
            }
        }
    }
}
