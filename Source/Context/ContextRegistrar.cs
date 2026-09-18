using RimTalk.API;
using RimTalkMemories.Budget;
using RimTalkMemories.Integration;
using RimTalkMemories.Util;
using Verse;

namespace RimTalkMemories.Context
{
    /// <summary>
    /// Declares this mod's context sections and pushes them into RimTalk at startup.
    ///
    /// Sections, hooks and variables all land in static registries, so they can be registered here
    /// without a game loaded. Prompt *entries* are different — those edit the active preset, which
    /// does not exist until a save is open — and so they do not belong in this class.
    /// See docs/DESIGN.md §3.2.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ContextRegistrar
    {
        public const string AgeSection = "AgeVoice";
        public const string GenderSection = "GenderVoice";
        public const string WorldLoreSection = "WorldLore";

        /// <summary>
        /// Age and gender fold into RimTalk's own value for their category rather than appearing
        /// beside it. RimTalk already writes the pawn's age; appending a separate block would say
        /// the age twice. Folding also reaches player-authored templates that use {{pawn.age}},
        /// which an injection never does — see docs/RIMTALK-API.md §1.1.
        ///
        /// World lore is a block in its own right rather than a modification of the time, so it
        /// injects. It also registers as {{worldlore}} for template authors who want to place it.
        /// </summary>
        public static InjectionMode DefaultModeFor(string sectionName)
        {
            switch (sectionName)
            {
                case AgeSection: return InjectionMode.HookAppend;
                case GenderSection: return InjectionMode.HookAppend;
                case WorldLoreSection: return InjectionMode.InjectBefore;
                default: return InjectionMode.InjectAfter;
            }
        }

        static ContextRegistrar()
        {
            DeclareAll();
            RimTalkApi.ApplyAll();
            CompanionConflicts.WarnIfAnyActive();
            RTMLog.Message("registered " + RimTalkApi.Registrations.Count + " context sections.");
        }

        private static void DeclareAll()
        {
            var settings = RimTalkMemoriesMod.Settings;

            RimTalkApi.Declare(new InjectionDeclaration
            {
                SectionName = AgeSection,
                Anchor = ContextCategories.Pawn.Age,
                Mode = ModeFrom(settings, AgeSection),
                BudgetPriority = BudgetOrder.AgeVoice,
                PerParticipant = true,
                PawnProvider = AgeVoice.Describe,
                Variants = AgeVoice.Variants,
                Note = "How someone this old speaks. Adults add nothing by default — they are the "
                       + "model's default register already, so saying so would cost tokens on every prompt."
            });

            RimTalkApi.Declare(new InjectionDeclaration
            {
                SectionName = GenderSection,
                Anchor = ContextCategories.Pawn.Gender,
                Mode = ModeFrom(settings, GenderSection),
                BudgetPriority = BudgetOrder.GenderVoice,
                PerParticipant = true,
                PawnProvider = GenderVoice.Describe,
                Variants = GenderVoice.Variants,
                Note = "Off by default with both texts empty. RimTalk already states each pawn's "
                       + "gender; this is only for colonies that want men and women to sound different."
            });

            RimTalkApi.Declare(new InjectionDeclaration
            {
                SectionName = WorldLoreSection,
                Anchor = ContextCategories.Environment.Time,
                Mode = ModeFrom(settings, WorldLoreSection),
                BudgetPriority = BudgetOrder.WorldLore,
                PerParticipant = false,
                DesiredChars = () => RimTalkMemoriesMod.Settings?.WorldLoreMaxChars ?? 1200,
                MapProvider = WorldLore.Describe,
                Variants = WorldLore.Variants,
                Note = "Built once per prompt rather than once per participant, because it is true "
                       + "of the place and not the person. Squeezed first when the budget is tight: "
                       + "it is the largest and the least specific to the moment."
            });

            RimTalkApi.RegisterPawnVariable(
                "ageband",
                AgeVoice.BandLabel,
                "baby, child, teenager, adult or elder — empty for non-humanlike pawns");

            RimTalkApi.RegisterEnvironmentVariable(
                "worldlore",
                WorldLore.VariableText,
                "the shared world lore block, for placing by hand in a prompt preset");
        }

        private static InjectionMode ModeFrom(Settings.MemoriesSettings settings, string sectionName)
        {
            var fallback = DefaultModeFor(sectionName);
            return settings == null ? fallback : settings.ModeFor(sectionName, fallback);
        }
    }
}
