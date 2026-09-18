using System;
using System.Collections.Generic;
using Verse;

namespace Arkh.Prompt
{
    /// <summary>One text this section could produce, for the profile view.</summary>
    public struct VariantSample
    {
        public string Label;
        public string Text;

        /// <summary>
        /// True when <see cref="Text"/> is an explanation of why nothing is emitted rather than
        /// something that will actually reach a prompt.
        ///
        /// It matters to the budget, not just the display: measuring a placeholder would make an
        /// unconfigured section reserve characters for text it is never going to send.
        /// </summary>
        public bool IsPlaceholder;

        public VariantSample(string label, string text, bool isPlaceholder = false)
        {
            Label = label;
            Text = text;
            IsPlaceholder = isPlaceholder;
        }
    }

    /// <summary>
    /// One block of text this mod contributes to a prompt, described well enough that the profile
    /// panel can show it with no game loaded.
    ///
    /// Sections are declared once at startup into <see cref="PromptCatalog"/>, and the assembler
    /// walks them in slot order when a prompt is built. Nothing anchors to anyone else's
    /// categories any more — the slot *is* the position.
    /// </summary>
    public sealed class PromptSection
    {
        public string SectionName;

        /// <summary>Which part of the prompt this belongs to.</summary>
        public PromptSlot Slot;

        /// <summary>Ordering within the slot. Lower first. Ties broken by name for stability.</summary>
        public int Order = 100;

        /// <summary>
        /// Order of service when the character budget is divided — **lower is served first**, and
        /// independent of <see cref="Order"/>, which is only about where the text sits. Two
        /// different questions that would be confusing to answer with one number.
        /// </summary>
        public int BudgetPriority = 50;

        /// <summary>
        /// True when this is built once per participant rather than once per prompt. It triples
        /// the real cost of a section in a three-way conversation, so the budget has to know.
        /// </summary>
        public bool PerParticipant;

        /// <summary>
        /// Characters this section would like. Defaults to the longest text it could emit, which
        /// is the honest answer for a section bounded by authored text.
        /// </summary>
        public Func<int> DesiredChars;

        /// <summary>
        /// Exactly one of these is set. The int is the section's character allowance for this
        /// call; a provider must clamp itself to it. Zero is legitimate and means "say nothing" —
        /// the budget squeezed this section out.
        /// </summary>
        public Func<Pawn, int, string> PawnProvider;
        public Func<Map, int, string> MapProvider;

        /// <summary>
        /// Every text this section could emit, enumerable with no pawn and no map.
        ///
        /// This is what makes the panel useful from the main menu: "what will a child actually
        /// get" should be answerable without finding a child.
        /// </summary>
        public Func<List<VariantSample>> Variants;

        /// <summary>Plain-English note shown under the section in the panel.</summary>
        public string Note;

        // --- Live counters, written from the provider path ----------------------------------

        public int Calls;

        /// <summary>
        /// Tracked apart from Calls on purpose: "never built" and "built but returned nothing"
        /// are different outcomes and must not look alike.
        /// </summary>
        public int NonEmptyReturns;

        public string LastEmitted;

        public bool IsPawnSection => PawnProvider != null;

        public void ResetCounters()
        {
            Calls = 0;
            NonEmptyReturns = 0;
            LastEmitted = null;
        }

        public List<VariantSample> SafeVariants()
        {
            if (Variants == null) return new List<VariantSample>();
            try
            {
                return Variants() ?? new List<VariantSample>();
            }
            catch
            {
                return new List<VariantSample>();
            }
        }

        /// <summary>
        /// What this section wants, in characters. Falls back to the longest variant it could
        /// emit, so a section that never declares a size still budgets honestly.
        /// </summary>
        public int SafeDesired()
        {
            if (DesiredChars != null)
            {
                try
                {
                    return Math.Max(0, DesiredChars());
                }
                catch
                {
                    // Fall through to the variant measurement below.
                }
            }

            int longest = 0;
            foreach (var variant in SafeVariants())
            {
                if (variant.IsPlaceholder) continue;
                if (variant.Text != null && variant.Text.Length > longest) longest = variant.Text.Length;
            }
            return longest;
        }
    }
}
