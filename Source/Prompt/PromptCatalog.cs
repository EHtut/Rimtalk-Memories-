using System;
using System.Collections.Generic;
using System.Linq;
using Arkh.Budget;
using Arkh.Context;
using Arkh.Util;
using Verse;

namespace Arkh.Prompt
{
    /// <summary>
    /// Every block of text this mod can put in a prompt, declared once at startup.
    ///
    /// This is the single source of truth the profile panel renders and the assembler walks. It
    /// holds declarations rather than rendered text, which is why the panel can show what will be
    /// sent without a game loaded, and why it cannot be wrong about what we add.
    /// </summary>
    public static class PromptCatalog
    {
        public const string AgeSection = "AgeVoice";
        public const string GenderSection = "GenderVoice";
        public const string WorldLoreSection = "WorldLore";

        private static readonly List<PromptSection> Declared = new List<PromptSection>();

        private static bool _initialized;

        public static IReadOnlyList<PromptSection> Sections
        {
            get
            {
                EnsureDeclared();
                return Declared;
            }
        }

        /// <summary>
        /// Declares the built-in sections, once.
        ///
        /// Deliberately not a <c>StaticConstructorOnStartup</c>: this type is a catalogue, and
        /// nothing in it should need a running game. Startup work — the conflict warning, the log
        /// line — lives in <see cref="ArkhStartup"/> instead. Keeping them apart is what lets the
        /// headless harness read the real declarations rather than a mirror of them that can drift.
        /// </summary>
        public static void EnsureDeclared()
        {
            if (_initialized) return;

            // Set before declaring: DeclareAll invalidates the budget, which reads Sections back,
            // and without this the re-entry would declare everything twice.
            _initialized = true;

            DeclareAll();
            PromptBudget.Invalidate();
        }

        public static void Declare(PromptSection section)
        {
            if (section == null) return;
            Declared.Add(section);
            PromptBudget.Invalidate();
        }

        /// <summary>Sections in the order they will appear in an assembled prompt.</summary>
        public static IEnumerable<PromptSection> InPromptOrder()
        {
            return Sections
                .OrderBy(s => (int)s.Slot)
                .ThenBy(s => s.Order)
                .ThenBy(s => s.SectionName);
        }

        /// <summary>
        /// Runs a pawn section, counting the call and containing any failure.
        ///
        /// A section that throws must cost one block of one prompt, not the whole line. Returning
        /// empty means the block is skipped, which is a far better failure than a pawn falling
        /// silent — and the counters make the difference visible in the panel afterwards.
        /// </summary>
        public static string Build(PromptSection section, Pawn pawn)
        {
            if (section?.PawnProvider == null) return "";

            section.Calls++;
            try
            {
                string text = section.PawnProvider(pawn, PromptBudget.For(section.SectionName)) ?? "";
                Record(section, text);
                return text;
            }
            catch (Exception ex)
            {
                ArkhLog.WarnOnce("Section " + section.SectionName + " threw and was skipped: " + ex,
                    section.SectionName.GetHashCode());
                return "";
            }
        }

        /// <summary>Runs a world section. Same contract as the pawn overload.</summary>
        public static string Build(PromptSection section, Map map)
        {
            if (section?.MapProvider == null) return "";

            section.Calls++;
            try
            {
                string text = section.MapProvider(map, PromptBudget.For(section.SectionName)) ?? "";
                Record(section, text);
                return text;
            }
            catch (Exception ex)
            {
                ArkhLog.WarnOnce("Section " + section.SectionName + " threw and was skipped: " + ex,
                    section.SectionName.GetHashCode());
                return "";
            }
        }

        private static void Record(PromptSection section, string text)
        {
            if (text.Length == 0) return;
            section.NonEmptyReturns++;
            section.LastEmitted = text;
        }

        private static void DeclareAll()
        {
            Declare(new PromptSection
            {
                SectionName = AgeSection,
                Slot = PromptSlot.PawnContext,
                Order = 20,
                BudgetPriority = BudgetOrder.AgeVoice,
                PerParticipant = true,
                PawnProvider = AgeVoice.Describe,
                Variants = AgeVoice.Variants,
                Note = "How someone this old speaks. Adults add nothing by default — they are the "
                       + "model's default register already, so saying so would cost tokens on every prompt."
            });

            Declare(new PromptSection
            {
                SectionName = GenderSection,
                Slot = PromptSlot.PawnContext,
                Order = 30,
                BudgetPriority = BudgetOrder.GenderVoice,
                PerParticipant = true,
                PawnProvider = GenderVoice.Describe,
                Variants = GenderVoice.Variants,
                Note = "Off by default with both texts empty. Only for colonies that want men and "
                       + "women to sound different."
            });

            Declare(new PromptSection
            {
                SectionName = WorldLoreSection,
                Slot = PromptSlot.WorldContext,
                Order = 10,
                BudgetPriority = BudgetOrder.WorldLore,
                PerParticipant = false,
                DesiredChars = () => ArkhMod.Settings?.WorldLoreMaxChars ?? 1200,
                MapProvider = WorldLore.Describe,
                Variants = WorldLore.Variants,
                Note = "Built once per prompt rather than once per participant, because it is true "
                       + "of the place and not the person. Squeezed first when the budget is tight: "
                       + "it is the largest and the least specific to the moment."
            });
        }
    }
}
