using RimWorld;
using Verse;

namespace Arkh.Display
{
    /// <summary>
    /// A social log entry whose text is ours rather than generated from a rulepack.
    ///
    /// This is the whole display bridge. Interaction Bubbles postfix-patches vanilla
    /// <c>PlayLog.Add</c>, accepts anything that is a <see cref="PlayLogEntry_Interaction"/>, and
    /// renders <c>ToGameStringFromPOV</c> above the initiator. So putting one of these into the
    /// play log is enough — Arkh never calls into Bubbles, references its assembly, or patches it.
    ///
    /// It lands in RimWorld's own social log at the same time, which is the "scrollable after the
    /// fact" half of the requirement for free.
    /// </summary>
    public class PlayLogEntry_ArkhSpeech : PlayLogEntry_Interaction
    {
        private string _spoken;

        /// <summary>Required by Scribe when a save is loaded.</summary>
        public PlayLogEntry_ArkhSpeech()
        {
        }

        public PlayLogEntry_ArkhSpeech(InteractionDef def, Pawn initiator, Pawn recipient, string spoken)
            : base(def, initiator, recipient, null)
        {
            _spoken = spoken;
        }

        /// <summary>
        /// The line, verbatim, whoever is looking.
        ///
        /// Vanilla varies this by point of view — a pawn who could not hear it sees nothing. That
        /// distinction is real and wanted, but it belongs to the earshot model (P6), which decides
        /// who is even a participant. Deciding it twice, in two places, is how the two come to
        /// disagree.
        /// </summary>
        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
        {
            return string.IsNullOrEmpty(_spoken) ? base.ToGameStringFromPOV_Worker(pov, forceLog) : _spoken;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _spoken, "arkhSpoken");
        }
    }
}
