using Arkh.Budget;
using Arkh.Context;
using Arkh.Settings;
using Arkh.UI;
using UnityEngine;
using Verse;

namespace Arkh
{
    /// <summary>
    /// Mod entry point and settings window.
    ///
    /// Nothing here builds prompts. Sections are declared in PromptCatalog at startup;
    /// this class only owns the settings those providers read.
    /// </summary>
    public class ArkhMod : Mod
    {
        public static ArkhSettings Settings;

        private Vector2 _scroll;

        /// <summary>Grows as sections are added; used to size the scroll view.</summary>
        private float _contentHeight = 800f;

        public ArkhMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ArkhSettings>();
        }

        public override string SettingsCategory() => "Arkh";

        /// <summary>
        /// Settings changes can move the budget — a longer lore cap, a different conversation
        /// size — so the allocation has to be recomputed. Doing it here rather than on every read
        /// keeps the cost off the tick path, where the providers run.
        /// </summary>
        public override void WriteSettings()
        {
            base.WriteSettings();
            PromptBudget.Invalidate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, _contentHeight);
            Widgets.BeginScrollView(inRect, ref _scroll, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.CheckboxLabeled(
                "Enable Arkh",
                ref Settings.Enabled,
                "Turns off every context injection without unloading the mod.");
            listing.Gap(6f);

            // Works from the main menu: RimWorld loads mod settings at startup, so the whole
            // static picture is reviewable without opening a colony.
            var buttonRect = listing.GetRect(34f);
            buttonRect.width = Mathf.Min(340f, buttonRect.width);
            if (Widgets.ButtonText(buttonRect, "Open injection profile…"))
            {
                Find.WindowStack.Add(new PromptProfileWindow());
            }
            TooltipHandler.TipRegion(buttonRect,
                "Shows the prompt Arkh builds, block by block, and what it costs. "
                + "Works here at the main menu — no colony needed.");

            listing.GapLine();

            // --- Age --------------------------------------------------------------------
            Header(listing, "Speech by age");
            listing.CheckboxLabeled(
                "Give each age its own voice",
                ref Settings.EnableAgeVoice,
                "Adds a line telling the model how someone this old actually speaks. " +
                "Leave a band empty to use the built-in wording for it.");

            if (Settings.EnableAgeVoice)
            {
                Settings.BabyVoice = Field(listing, "Baby (under 3)", Settings.BabyVoice);
                Settings.ChildVoice = Field(listing, "Child (3–12)", Settings.ChildVoice);
                Settings.TeenagerVoice = Field(listing, "Teenager (13–17)", Settings.TeenagerVoice);
                Settings.AdultVoice = Field(listing, "Adult (18–59)", Settings.AdultVoice);
                Settings.ElderVoice = Field(listing, "Elder (60+)", Settings.ElderVoice);
            }
            listing.GapLine();

            // --- Gender -----------------------------------------------------------------
            Header(listing, "Speech by gender");
            listing.CheckboxLabeled(
                "Give each gender its own voice",
                ref Settings.EnableGenderVoice,
                "Off by default. The model is already told each pawn's gender; this is " +
                "only for colonies that want men and women to sound different. Both boxes " +
                "start empty, so nothing is added until you write it.");

            if (Settings.EnableGenderVoice)
            {
                Settings.MaleVoice = Field(listing, "Male", Settings.MaleVoice);
                Settings.FemaleVoice = Field(listing, "Female", Settings.FemaleVoice);
            }
            listing.GapLine();

            // --- World lore -------------------------------------------------------------
            Header(listing, "World lore");
            listing.CheckboxLabeled(
                "Everyone speaks from shared world lore",
                ref Settings.EnableWorldLore,
                "Background every pawn on the map knows: what this world is, who runs it, " +
                "what is taken for granted here.");

            if (Settings.EnableWorldLore)
            {
                listing.Label("This rides along on every prompt the colony generates, so " +
                              "length here is a running cost. A few sentences goes a long way.");
                Settings.WorldLore = Widgets.TextArea(listing.GetRect(120f), Settings.WorldLore ?? "");
                listing.Gap(4f);

                int used = (Settings.WorldLore ?? "").Length;
                listing.Label($"Character limit: {Settings.WorldLoreMaxChars}   (using {used})");
                Settings.WorldLoreMaxChars = (int)listing.Slider(Settings.WorldLoreMaxChars, 200f, 4000f);
            }
            listing.GapLine();

            // --- Budget -----------------------------------------------------------------
            Header(listing, "Prompt budget");
            listing.Label("Every feature here adds text to the prompt. This is the ceiling on all "
                          + "of it together — when it runs short, background text gives way before "
                          + "anything specific to the moment does.");

            listing.Label($"Characters this mod may add per prompt: <b>{Settings.TotalBudgetChars}</b>"
                          + $"   (~{Mathf.CeilToInt(Settings.TotalBudgetChars / 4f)} tokens in English)");
            Settings.TotalBudgetChars = (int)listing.Slider(Settings.TotalBudgetChars, 200f, 8000f);

            listing.Label($"Assumed conversation size: <b>{Settings.AssumedParticipants}</b> pawns");
            Settings.AssumedParticipants = (int)listing.Slider(Settings.AssumedParticipants, 1f, 8f);
            listing.Label("<i>Age and gender text is built once per speaker, so conversation size "
                          + "decides what they really cost. World lore is built once either way.</i>");

            listing.Label($"<i>Currently allocated: {PromptBudget.Committed()} of "
                          + $"{Settings.TotalBudgetChars} characters. Open the injection profile for "
                          + "the breakdown.</i>");

            _contentHeight = listing.CurHeight + 40f;
            listing.End();
            Widgets.EndScrollView();

            base.DoSettingsWindowContents(inRect);
        }

        private static void Header(Listing_Standard listing, string text)
        {
            Text.Font = GameFont.Medium;
            listing.Label(text);
            Text.Font = GameFont.Small;
        }

        /// <summary>A labelled one-line text box that keeps the label readable at any width.</summary>
        private static string Field(Listing_Standard listing, string label, string value)
        {
            var row = listing.GetRect(28f);
            var labelRect = new Rect(row.x, row.y, row.width * 0.32f, row.height);
            var fieldRect = new Rect(labelRect.xMax + 6f, row.y, row.width - labelRect.width - 6f, row.height);

            Widgets.Label(labelRect, label);
            string result = Widgets.TextField(fieldRect, value ?? "");
            listing.Gap(4f);
            return result;
        }
    }
}
