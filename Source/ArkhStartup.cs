using Arkh.Display;
using Arkh.Integration;
using Arkh.Prompt;
using Arkh.Util;
using Verse;

namespace Arkh
{
    /// <summary>
    /// Everything that has to happen once, when the game finishes loading mods.
    ///
    /// This exists so that nothing else needs a static constructor. A type that both holds data
    /// and does startup work cannot be read outside a running game, which rules it out of the
    /// headless harness — and the harness is how most of this mod gets verified, because RimWorld
    /// takes twenty minutes to load.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ArkhStartup
    {
        static ArkhStartup()
        {
            PromptCatalog.EnsureDeclared();
            SpeechDisplay.Wire();

            ConflictDetector.WarnIfAnyActive();
            SpeechDisplay.WarnIfMissing();

            ArkhLog.Message("ready — " + PromptCatalog.Sections.Count + " prompt sections declared.");
        }
    }
}
