using System;
using System.Collections.Generic;
using RimTalk.API;
using Verse;

namespace RimTalkMemories.Integration
{
    /// <summary>
    /// How a section attaches to its anchor.
    ///
    /// Inject and Hook are genuinely different mechanisms with different reach, not two spellings
    /// of the same thing. An injection adds a new block near the category and is only consumed
    /// when RimTalk assembles prose. A hook transforms the category's own value and also runs
    /// during template variable resolution — so a hook reaches player-authored templates that
    /// reference {{pawn.age}}, and an injection never does.
    /// See docs/RIMTALK-API.md §1.1.
    ///
    /// The mode is a per-section setting rather than a compile-time choice, because RimWorld takes
    /// twenty minutes to load and this way one run can try all four.
    /// </summary>
    public enum InjectionMode
    {
        /// <summary>A separate block, after RimTalk's own text for this category.</summary>
        InjectAfter,

        /// <summary>A separate block, before RimTalk's own text for this category.</summary>
        InjectBefore,

        /// <summary>Folded into RimTalk's own value for this category, after it.</summary>
        HookAppend,

        /// <summary>Replaces RimTalk's value for this category outright.</summary>
        HookOverride
    }

    /// <summary>One text this section could produce, for the profile view.</summary>
    public struct VariantSample
    {
        public string Label;
        public string Text;

        public VariantSample(string label, string text)
        {
            Label = label;
            Text = text;
        }
    }

    /// <summary>
    /// One thing this mod contributes to a prompt, described well enough that the profile panel
    /// can show it without a game loaded.
    ///
    /// This is the point of the whole type: a merged prompt cannot tell you which parts are ours,
    /// so the panel renders these declarations rather than trying to find our text in RimTalk's
    /// output. It reads the source instead of guessing at the result, and so cannot be wrong
    /// about what we added.
    /// </summary>
    public sealed class InjectionDeclaration
    {
        public string SectionName;
        public ContextCategory Anchor;
        public InjectionMode Mode;
        public int Priority = 100;

        /// <summary>Exactly one of these two is set.</summary>
        public Func<Pawn, string> PawnProvider;
        public Func<Map, string> MapProvider;

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

        /// <summary>Times RimTalk called us. Zero means the anchor is never reached.</summary>
        public int Calls;

        /// <summary>
        /// Times we returned something. Tracked apart from Calls on purpose: "never called" and
        /// "called but returned nothing" are different failures and must not look alike.
        /// </summary>
        public int NonEmptyReturns;

        public string LastEmitted;
        public int LastEmittedTick;

        public bool IsPawnSection => PawnProvider != null;

        public bool UsesHook => Mode == InjectionMode.HookAppend || Mode == InjectionMode.HookOverride;

        public void ResetCounters()
        {
            Calls = 0;
            NonEmptyReturns = 0;
            LastEmitted = null;
            LastEmittedTick = 0;
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
    }
}
