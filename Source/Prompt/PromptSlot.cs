namespace Arkh.Prompt
{
    /// <summary>
    /// The skeleton of a prompt. Every section belongs to exactly one slot, and slots are emitted
    /// in declaration order.
    ///
    /// This is ours to define, which is the whole point of not building on somebody else's prompt.
    /// It differs from RimTalk's five-entry shape in one way that matters: **memory is a slot**,
    /// not a transcript of recent lines wedged in as chat history. What a pawn remembers is a
    /// first-class part of who is speaking, and it gets its own place in the prompt and its own
    /// share of the budget.
    /// </summary>
    public enum PromptSlot
    {
        /// <summary>Who the model is and how it should behave. One per prompt.</summary>
        SystemInstruction,

        /// <summary>
        /// The response format we parse. Separate from the instruction on purpose: it is the one
        /// block whose wording is a contract with our own parser rather than a matter of taste,
        /// so it should be obvious when someone is editing it.
        /// </summary>
        OutputContract,

        /// <summary>True of the place, not the person. Built once per prompt.</summary>
        WorldContext,

        /// <summary>True of a participant. Built once per participant.</summary>
        PawnContext,

        /// <summary>What this pawn remembers, scored and retrieved rather than replayed.</summary>
        Memory,

        /// <summary>Recent lines, for continuity within a single exchange.</summary>
        Conversation,

        /// <summary>What is prompting this line right now.</summary>
        Trigger
    }
}
