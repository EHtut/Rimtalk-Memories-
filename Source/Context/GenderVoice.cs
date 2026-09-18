using System.Collections.Generic;
using RimTalkMemories.Integration;
using RimTalkMemories.Util;
using Verse;

namespace RimTalkMemories.Context
{
    /// <summary>
    /// Optional per-gender speech guidance.
    ///
    /// This one is off by default, and deliberately so. RimTalk already tells the model each
    /// pawn's gender, so anything added here is the player asserting that men and women in
    /// their colony should *sound* different — which is a setting some campaigns want and
    /// many do not. Shipping it on with opinionated defaults would put words in every
    /// colonist's mouth that the player never asked for, so both fields start empty and the
    /// feature stays dark until someone fills them in.
    /// </summary>
    public static class GenderVoice
    {
        public const string Prefix = "Speech for their gender: ";

        public static string Describe(Pawn pawn, int budget)
        {
            var settings = RimTalkMemoriesMod.Settings;
            if (settings == null || !settings.Enabled || !settings.EnableGenderVoice) return "";
            if (pawn?.RaceProps == null || !pawn.RaceProps.Humanlike) return "";

            string guidance;
            switch (pawn.gender)
            {
                case Gender.Male: guidance = settings.MaleVoice; break;
                case Gender.Female: guidance = settings.FemaleVoice; break;
                default: return "";
            }

            if (string.IsNullOrEmpty(guidance)) return "";

            return TextUtil.Clamp(Prefix + guidance, budget);
        }

        /// <summary>Both texts, for the profile panel. Empty by design until the player writes them.</summary>
        public static List<VariantSample> Variants()
        {
            var samples = new List<VariantSample>();
            var settings = RimTalkMemoriesMod.Settings;
            if (settings == null) return samples;

            samples.Add(Sample("Male", settings.MaleVoice));
            samples.Add(Sample("Female", settings.FemaleVoice));
            return samples;
        }

        private static VariantSample Sample(string label, string guidance)
        {
            bool silent = string.IsNullOrEmpty(guidance);
            return new VariantSample(
                label,
                silent ? "(adds nothing — not written yet)" : Prefix + guidance,
                silent);
        }
    }
}
