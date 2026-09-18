using System.Collections.Generic;
using RimTalkMemories.Integration;
using Verse;

namespace RimTalkMemories.Context
{
    /// <summary>
    /// Background every pawn on the map speaks from: what this world is, who runs it, what
    /// everyone here simply takes for granted.
    ///
    /// This is environment context rather than pawn context because it is true of the place,
    /// not the person — which also means RimTalk builds it once per prompt instead of once
    /// per participant. For a four-pawn conversation that is the difference between paying
    /// for the lore once and paying for it four times.
    /// </summary>
    public static class WorldLore
    {
        /// <summary>
        /// The map argument is unused: lore is the same everywhere, which is exactly why this is
        /// environment context in the first place. Kept in the signature because that is the shape
        /// RimTalk hands us.
        /// </summary>
        public static string Describe(Map map) => Text();

        /// <summary>
        /// The lore block as it would be emitted. Needs no map, so the profile panel can show it
        /// from the main menu.
        /// </summary>
        public static string Text()
        {
            var settings = RimTalkMemoriesMod.Settings;
            if (settings == null || !settings.Enabled || !settings.EnableWorldLore) return "";

            string lore = settings.WorldLore;
            if (string.IsNullOrEmpty(lore)) return "";

            lore = lore.Trim();
            if (lore.Length == 0) return "";

            return "What everyone here knows about the world:\n" + Clamp(lore, settings.WorldLoreMaxChars);
        }

        /// <summary>The one text this section emits, for the profile panel.</summary>
        public static List<VariantSample> Variants()
        {
            string text = Text();
            return new List<VariantSample>
            {
                new VariantSample("All pawns", string.IsNullOrEmpty(text)
                    ? "(adds nothing — no lore written, or the feature is off)"
                    : text)
            };
        }

        /// <summary>
        /// Trims at a word boundary so the model is never handed a sentence cut mid-word.
        ///
        /// The cap matters because this text rides on every prompt the colony generates. A
        /// player who pastes several pages of setting into the box would otherwise pay for
        /// all of it on every line any pawn ever says.
        /// </summary>
        private static string Clamp(string text, int maxChars)
        {
            if (maxChars <= 0 || text.Length <= maxChars) return text;

            int cut = text.LastIndexOf(' ', maxChars - 1);
            if (cut < maxChars / 2) cut = maxChars - 1;

            return text.Substring(0, cut).TrimEnd() + "…";
        }
    }
}
