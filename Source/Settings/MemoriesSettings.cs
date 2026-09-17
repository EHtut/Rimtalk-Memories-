using Verse;

namespace RimTalkMemories.Settings
{
    /// <summary>
    /// Player-facing settings. These live in RimWorld's mod config, not in the save, so they
    /// carry across colonies — which is what you want for authored text like world lore.
    /// Anything that describes a particular colony's history belongs in the save instead;
    /// see docs/DESIGN.md for where that line sits.
    /// </summary>
    public class MemoriesSettings : ModSettings
    {
        // --- Master switches -------------------------------------------------------------

        /// <summary>Turns off every context injection without needing to unload the mod.</summary>
        public bool Enabled = true;

        // --- Age and gender voice --------------------------------------------------------

        public bool EnableAgeVoice = true;
        public bool EnableGenderVoice;

        /// <summary>
        /// Per-band guidance. Empty means "use the built-in default for that band"; the
        /// defaults live in AgeVoice so an empty settings file still does something sensible.
        /// </summary>
        public string BabyVoice = "";
        public string ChildVoice = "";
        public string TeenagerVoice = "";
        public string AdultVoice = "";
        public string ElderVoice = "";

        public string MaleVoice = "";
        public string FemaleVoice = "";

        // --- World lore ------------------------------------------------------------------

        public bool EnableWorldLore = true;

        /// <summary>
        /// Free text every pawn on the map speaks from: what this world is, who rules it,
        /// what everyone takes for granted. Kept short on purpose — it rides along on every
        /// single prompt, so length here is a running token cost.
        /// </summary>
        public string WorldLore = "";

        /// <summary>Guard rail against pasting a novel into a field that bills per token.</summary>
        public int WorldLoreMaxChars = 1200;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);

            Scribe_Values.Look(ref EnableAgeVoice, "enableAgeVoice", true);
            Scribe_Values.Look(ref EnableGenderVoice, "enableGenderVoice", false);
            Scribe_Values.Look(ref BabyVoice, "babyVoice", "");
            Scribe_Values.Look(ref ChildVoice, "childVoice", "");
            Scribe_Values.Look(ref TeenagerVoice, "teenagerVoice", "");
            Scribe_Values.Look(ref AdultVoice, "adultVoice", "");
            Scribe_Values.Look(ref ElderVoice, "elderVoice", "");
            Scribe_Values.Look(ref MaleVoice, "maleVoice", "");
            Scribe_Values.Look(ref FemaleVoice, "femaleVoice", "");

            Scribe_Values.Look(ref EnableWorldLore, "enableWorldLore", true);
            Scribe_Values.Look(ref WorldLore, "worldLore", "");
            Scribe_Values.Look(ref WorldLoreMaxChars, "worldLoreMaxChars", 1200);

            base.ExposeData();
        }
    }
}
