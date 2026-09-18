using System;
using System.Collections.Generic;
using RimTalkMemories.Integration;
using RimTalkMemories.Settings;
using Verse;

namespace RimTalkMemories.Context
{
    /// <summary>The age brackets this mod gives a distinct voice to.</summary>
    public enum AgeBand
    {
        Baby,
        Child,
        Teenager,
        Adult,
        Elder
    }

    /// <summary>
    /// Tells the model how someone this old actually sounds.
    ///
    /// RimTalk already puts the pawn's age in the prompt, but a number alone does very little:
    /// a model handed "age 6" still writes a six-year-old with the vocabulary of a diplomat.
    /// What moves the needle is an instruction about register — sentence length, what they
    /// understand, what they care about — so that is what gets injected, immediately after
    /// the age RimTalk already wrote.
    /// </summary>
    public static class AgeVoice
    {
        /// <summary>
        /// Used when the player has not written their own guidance for a band. These are
        /// deliberately about *how* to speak rather than what to say, so they compose with
        /// whatever persona the pawn already has instead of overwriting it.
        /// </summary>
        private static string DefaultFor(AgeBand band)
        {
            switch (band)
            {
                case AgeBand.Baby:
                    return "Speaks in babble and single words at most. Cannot hold a conversation or explain anything.";
                case AgeBand.Child:
                    return "Speaks in short, plain sentences. Blunt and literal, asks a lot of questions, and has no grasp of tact or long-term consequence.";
                case AgeBand.Teenager:
                    return "Speaks with more confidence than experience. Quick to be sarcastic or dramatic, and sensitive to how others see them.";
                case AgeBand.Adult:
                    return "";
                case AgeBand.Elder:
                    return "Speaks from long experience, often by comparison to the past. Unhurried, and less concerned with what others think.";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Biological years, not chronological: a pawn who spent thirty years in cryptosleep
        /// still has the voice of the body doing the talking.
        ///
        /// Non-humanlike pawns get no band at all. The thresholds below are human ones, and
        /// applying them to an animal with a vocal link would label a two-year-old boomalope a
        /// toddler, which is worse than staying quiet.
        /// </summary>
        public static AgeBand? BandFor(Pawn pawn)
        {
            if (pawn?.ageTracker == null) return null;
            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return null;

            int years = pawn.ageTracker.AgeBiologicalYears;

            if (years < 3) return AgeBand.Baby;
            if (years < 13) return AgeBand.Child;
            if (years < 18) return AgeBand.Teenager;
            if (years < 60) return AgeBand.Adult;
            return AgeBand.Elder;
        }

        /// <summary>The player's text for a band, falling back to the built-in default.</summary>
        public static string GuidanceFor(MemoriesSettings settings, AgeBand band)
        {
            string custom;
            switch (band)
            {
                case AgeBand.Baby: custom = settings.BabyVoice; break;
                case AgeBand.Child: custom = settings.ChildVoice; break;
                case AgeBand.Teenager: custom = settings.TeenagerVoice; break;
                case AgeBand.Adult: custom = settings.AdultVoice; break;
                case AgeBand.Elder: custom = settings.ElderVoice; break;
                default: custom = null; break;
            }

            return string.IsNullOrEmpty(custom) ? DefaultFor(band) : custom;
        }

        /// <summary>
        /// The context line itself. Empty means the section is skipped entirely, which is the
        /// right outcome for an ordinary adult: they are the model's default register already,
        /// so spending tokens to say so would be waste on every prompt in the colony.
        /// </summary>
        public static string Describe(Pawn pawn)
        {
            var settings = RimTalkMemoriesMod.Settings;
            if (settings == null || !settings.Enabled || !settings.EnableAgeVoice) return "";

            var band = BandFor(pawn);
            if (band == null) return "";

            string guidance = GuidanceFor(settings, band.Value);
            if (string.IsNullOrEmpty(guidance)) return "";

            return "Speech for their age: " + guidance;
        }

        /// <summary>Exposed as a template variable so players can place it themselves.</summary>
        public static string BandLabel(Pawn pawn)
        {
            var band = BandFor(pawn);
            return band == null ? "" : band.Value.ToString().ToLowerInvariant();
        }

        /// <summary>Where each band starts and stops, for the profile panel.</summary>
        public static string RangeLabel(AgeBand band)
        {
            switch (band)
            {
                case AgeBand.Baby: return "under 3";
                case AgeBand.Child: return "3–12";
                case AgeBand.Teenager: return "13–17";
                case AgeBand.Adult: return "18–59";
                case AgeBand.Elder: return "60+";
                default: return "";
            }
        }

        /// <summary>
        /// Exactly what every band would emit, with no pawn needed.
        ///
        /// This is the answer to "what will a child actually get", available from the main menu
        /// instead of requiring a loaded colony that happens to contain a child.
        /// </summary>
        public static List<VariantSample> Variants()
        {
            var samples = new List<VariantSample>();
            var settings = RimTalkMemoriesMod.Settings;
            if (settings == null) return samples;

            foreach (AgeBand band in Enum.GetValues(typeof(AgeBand)))
            {
                string guidance = GuidanceFor(settings, band);
                string emitted = string.IsNullOrEmpty(guidance)
                    ? "(adds nothing)"
                    : "Speech for their age: " + guidance;

                samples.Add(new VariantSample(band + " (" + RangeLabel(band) + ")", emitted));
            }

            return samples;
        }
    }
}
