using System.Collections.Generic;
using Arkh.Budget;
using Verse;

namespace Arkh.Prompt
{
    /// <summary>
    /// The blocks every prompt needs: who the model is, what shape to answer in, and what it is
    /// being asked about.
    ///
    /// These are separate from the authored context (age, lore) because they are not optional and
    /// not really player content — the output contract in particular is a contract with our own
    /// parser, so it lives in code where breaking it is visible rather than in a text box.
    ///
    /// They are written to be editable later (P5 owns that), but shipping defaults now is what
    /// lets the talk engine produce a complete prompt today.
    /// </summary>
    public static class CoreSections
    {
        public const string InstructionSection = "SystemInstruction";
        public const string ContractSection = "OutputContract";
        public const string TriggerSection = "Trigger";

        /// <summary>
        /// Deliberately short. Long behavioural preambles are mostly wasted tokens: what actually
        /// shapes a colonist's voice is the context about *them*, which the other sections supply.
        /// </summary>
        public const string DefaultInstruction =
            "You write dialogue for colonists in RimWorld, a harsh sci-fi frontier colony.\n"
            + "Write what the named colonist says out loud, in their own voice, in one or two short "
            + "sentences. Speech only — no narration, no stage directions, no quotation marks.\n"
            + "They are a person having an ordinary moment, not a narrator describing one.";

        /// <summary>
        /// The parser's half of the bargain. <see cref="ResponseContract"/> keeps the wording and
        /// the parsing in the same file so they cannot drift apart.
        /// </summary>
        public const string DefaultContract =
            "Reply with JSON and nothing else, in exactly this shape:\n"
            + "{\"lines\":[{\"speaker\":\"NAME\",\"text\":\"WHAT THEY SAY\"}]}\n"
            + "Use the colonist's name exactly as given. One entry per line of speech.";

        internal static void Declare()
        {
            PromptCatalog.Declare(new PromptSection
            {
                SectionName = InstructionSection,
                Slot = PromptSlot.SystemInstruction,
                Order = 0,
                BudgetPriority = BudgetOrder.CoreInstruction,
                PerParticipant = false,
                Essential = true,
                MapProvider = (map, budget) => DefaultInstruction,
                DesiredChars = () => DefaultInstruction.Length,
                Variants = () => new List<VariantSample> { new VariantSample("Always", DefaultInstruction) },
                Note = "Who the model is being asked to be. Short on purpose — voice comes from the "
                       + "context blocks below, not from a long preamble."
            });

            PromptCatalog.Declare(new PromptSection
            {
                SectionName = ContractSection,
                Slot = PromptSlot.OutputContract,
                Order = 0,
                BudgetPriority = BudgetOrder.CoreInstruction,
                PerParticipant = false,
                Essential = true,
                MapProvider = (map, budget) => DefaultContract,
                DesiredChars = () => DefaultContract.Length,
                Variants = () => new List<VariantSample> { new VariantSample("Always", DefaultContract) },
                Note = "A contract with our own parser, not a matter of taste. Changing the shape "
                       + "here without changing ResponseContract breaks every reply."
            });
        }
    }
}
