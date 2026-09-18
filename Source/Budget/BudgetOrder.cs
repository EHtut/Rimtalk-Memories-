namespace Arkh.Budget
{
    /// <summary>
    /// Who gets served first when the character budget is divided up. **Lower is served first.**
    ///
    /// This is a design statement, not a tuning constant. The order says what this mod believes
    /// matters most in a conversation, and it is what decides whose text survives when a pawn has
    /// a great deal going on:
    ///
    /// 1. **What a pawn remembers** outranks everything. A recalled memory is usually the reason
    ///    the line is worth generating at all.
    /// 2. **How they speak** comes next — age, then gender. Both are short and change the output
    ///    out of all proportion to their length, so they are cheap to serve early.
    /// 3. **What everyone knows** goes last. Colony lore, then world lore. Both are background,
    ///    both are large, and neither is specific to this moment — so they are what should give
    ///    way, and world lore gives way first because it is the least specific of all.
    ///
    /// Gaps are deliberate: features arrive later and should slot in without renumbering.
    /// See docs/DESIGN.md §12.
    /// </summary>
    public static class BudgetOrder
    {
        /// <summary>Reserved for P5 recall. Nothing registers here yet.</summary>
        public const int RecalledMemory = 10;

        public const int AgeVoice = 20;

        public const int GenderVoice = 30;

        /// <summary>Reserved for P2 colony lore.</summary>
        public const int ColonyLore = 80;

        public const int WorldLore = 90;
    }
}
