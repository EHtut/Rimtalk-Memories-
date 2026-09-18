using System.Collections.Generic;
using RimTalkMemories.Integration;
using Verse;

namespace RimTalkMemories.Settings
{
    /// <summary>
    /// Every player-facing setting this mod owns, and the only place they live.
    ///
    /// These are <see cref="ModSettings"/>, saved to RimWorld's config folder — never scribed into
    /// the save game. That is a deliberate rule, not an accident of where the first field landed:
    ///
    /// - They carry across colonies. Authored text is work the player did once; tying it to a save
    ///   would mean redoing it every new colony.
    /// - They are not colony history. Anything describing a *particular* colony's past belongs in
    ///   the save via a GameComponent instead — memories, when they arrive. The line is: authored
    ///   by the player goes here, produced by play goes in the save.
    /// - They are readable at the main menu, because RimWorld loads mod settings at startup. So is
    ///   RimTalk's prompt system. That is what lets the injection profile panel work without
    ///   loading a save, which matters a great deal at twenty minutes a load.
    ///
    /// See docs/DESIGN.md §3.1.
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
        /// Per-band guidance. Empty means "use the built-in default for that band"; the defaults
        /// live in AgeVoice so an empty settings file still does something sensible.
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
        /// Free text every pawn on the map speaks from: what this world is, who rules it, what
        /// everyone takes for granted. Kept short on purpose — it rides along on every single
        /// prompt, so length here is a running token cost.
        /// </summary>
        public string WorldLore = "";

        /// <summary>Guard rail against pasting a novel into a field that bills per token.</summary>
        public int WorldLoreMaxChars = 1200;

        // --- Budget -----------------------------------------------------------------------

        /// <summary>
        /// Total characters this mod may add to a single prompt, across every feature.
        ///
        /// Characters rather than tokens because real tokenisation is not affordable on the tick
        /// path and varies by provider. Roughly four characters per token in English; Chinese,
        /// Japanese and Korean run far denser, so a player using those should set this lower than
        /// the English figure would suggest. Calling it a character budget and letting the player
        /// choose is the honest option.
        /// </summary>
        public int TotalBudgetChars = 2000;

        /// <summary>
        /// How many pawns a typical conversation is assumed to involve.
        ///
        /// Pawn sections are built once per participant, environment sections once per prompt, so
        /// without this the budget would badly under-count a four-way conversation.
        /// </summary>
        public int AssumedParticipants = 3;

        // --- Delivery ---------------------------------------------------------------------

        /// <summary>
        /// Section name to <see cref="InjectionMode"/>, as an int so Scribe can round-trip it.
        /// A section with no entry uses the default its registrar declared.
        ///
        /// Stored rather than compiled in because RimWorld takes twenty minutes to load: making
        /// this a runtime setting means one session can try every mode instead of one.
        /// </summary>
        public Dictionary<string, int> SectionModes = new Dictionary<string, int>();

        public InjectionMode ModeFor(string sectionName, InjectionMode fallback)
        {
            if (SectionModes != null && SectionModes.TryGetValue(sectionName, out int stored))
            {
                return (InjectionMode)stored;
            }
            return fallback;
        }

        public void SetMode(string sectionName, InjectionMode mode)
        {
            if (SectionModes == null) SectionModes = new Dictionary<string, int>();
            SectionModes[sectionName] = (int)mode;
        }

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

            Scribe_Values.Look(ref TotalBudgetChars, "totalBudgetChars", 2000);
            Scribe_Values.Look(ref AssumedParticipants, "assumedParticipants", 3);

            Scribe_Collections.Look(ref SectionModes, "sectionModes", LookMode.Value, LookMode.Value);

            // Scribe hands back null for a collection that was empty or absent when saved, and
            // every read of this runs on the prompt path. Fix it here rather than null-checking
            // at each use site.
            if (Scribe.mode == LoadSaveMode.PostLoadInit && SectionModes == null)
            {
                SectionModes = new Dictionary<string, int>();
            }

            base.ExposeData();
        }
    }
}
