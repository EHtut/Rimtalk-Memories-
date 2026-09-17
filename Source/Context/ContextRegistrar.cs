using RimTalk.API;
using RimTalkMemories.Integration;
using RimTalkMemories.Util;
using Verse;

namespace RimTalkMemories.Context
{
    /// <summary>
    /// Wires this mod's context providers into RimTalk, once, at startup.
    ///
    /// Sections, hooks and variables all land in static registries, so they can be registered
    /// here without a game loaded. Prompt *entries* are different — those edit the active
    /// preset, which does not exist until a save is open — and so they do not belong in this
    /// class. See docs/DESIGN.md, "Two registration moments".
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ContextRegistrar
    {
        static ContextRegistrar()
        {
            Register();
            CompanionConflicts.WarnIfAnyActive();
        }

        private static void Register()
        {
            // Registering twice would inject every section twice, and a static constructor is
            // not a promise that this only ever runs once. Clearing first makes it idempotent.
            RimTalkApi.UnregisterAll();

            RimTalkApi.InjectPawnSection(
                "AgeVoice",
                ContextCategories.Pawn.Age,
                ContextHookRegistry.InjectPosition.After,
                AgeVoice.Describe);

            RimTalkApi.InjectPawnSection(
                "GenderVoice",
                ContextCategories.Pawn.Gender,
                ContextHookRegistry.InjectPosition.After,
                GenderVoice.Describe);

            // Before the first environment line, so the setting frames everything that
            // follows it rather than arriving as a footnote after the weather report.
            RimTalkApi.InjectEnvironmentSection(
                "WorldLore",
                ContextCategories.Environment.Time,
                ContextHookRegistry.InjectPosition.Before,
                WorldLore.Describe);

            RimTalkApi.RegisterPawnVariable(
                "ageband",
                AgeVoice.BandLabel,
                "baby, child, teenager, adult or elder — empty for non-humanlike pawns");

            RTMLog.Message("context providers registered.");
        }
    }
}
