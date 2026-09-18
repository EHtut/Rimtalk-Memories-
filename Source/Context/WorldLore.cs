using System.Collections.Generic;
using Arkh.Prompt;
using Arkh.Util;
using Verse;

namespace Arkh.Context
{
    /// <summary>
    /// Background every pawn on the map speaks from: what this world is, who runs it, what
    /// everyone here simply takes for granted.
    ///
    /// This is world context rather than pawn context because it is true of the place, not the
    /// person — which also means it is built once per prompt instead of once per participant. For
    /// a four-pawn conversation that is the difference between paying for the lore once and paying
    /// for it four times.
    /// </summary>
    public static class WorldLore
    {
        public const string Prefix = "What everyone here knows about the world:\n";

        /// <summary>
        /// The map argument is unused: lore is the same everywhere, which is exactly why this is
        /// world context in the first place. Kept in the signature because that is the shape every
        /// world section has, and a special case here would cost more than the unused parameter.
        /// </summary>
        public static string Describe(Map map, int budget) => Text(budget);

        /// <summary>
        /// The lore block as it would be emitted for a given allowance. Needs no map, so the
        /// profile panel can show it from the main menu.
        ///
        /// Two limits apply and the smaller wins: the player's own character cap for this field,
        /// and whatever the budget allocated. Keeping both is deliberate — the cap is a statement
        /// about how much lore is worth sending, the allowance is about what fits alongside
        /// everything else, and they are not the same question.
        /// </summary>
        public static string Text(int budget)
        {
            var settings = ArkhMod.Settings;
            if (settings == null || !settings.Enabled || !settings.EnableWorldLore) return "";

            string lore = settings.WorldLore;
            if (string.IsNullOrEmpty(lore)) return "";

            lore = lore.Trim();
            if (lore.Length == 0) return "";

            int limit = budget < settings.WorldLoreMaxChars ? budget : settings.WorldLoreMaxChars;

            // Clamp the body, then add the label, so a tight allowance eats the lore rather than
            // truncating the sentence that explains what the lore is.
            string body = TextUtil.Clamp(lore, limit - Prefix.Length);
            return string.IsNullOrEmpty(body) ? "" : Prefix + body;
        }

        /// <summary>The one text this section emits, for the profile panel.</summary>
        public static List<VariantSample> Variants()
        {
            var settings = ArkhMod.Settings;
            int cap = settings?.WorldLoreMaxChars ?? 1200;

            string text = Text(cap);
            bool silent = string.IsNullOrEmpty(text);

            return new List<VariantSample>
            {
                new VariantSample(
                    "All pawns",
                    silent ? "(adds nothing — no lore written, or the feature is off)" : text,
                    silent)
            };
        }
    }
}
