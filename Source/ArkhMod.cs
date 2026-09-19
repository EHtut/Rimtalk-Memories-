using System;
using System.Collections.Generic;
using Arkh.Budget;
using Arkh.Context;
using Arkh.Model;
using Arkh.Settings;
using Arkh.Util;
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

        /// <summary>
        /// A provider chosen from the dropdown, applied at the start of the next layout pass.
        ///
        /// Not applied immediately, and this is the whole reason the settings page used to break.
        /// A FloatMenuOption's action runs during the float menu's own event handling — after this
        /// window's Layout pass but before its Repaint. Changing the provider there means repaint
        /// draws a different number of controls than layout registered (LM Studio has no API key
        /// field, OpenAI does), and Unity's IMGUI throws a control-count mismatch. Deferring to the
        /// next Layout keeps both passes of any single frame in agreement.
        /// </summary>
        private ModelProvider? _pendingProvider;

        /// <summary>
        /// The connection-test result, settled once per frame.
        ///
        /// A worker thread swaps it at any moment, including between Layout and Repaint, which
        /// would change how many labels get drawn part-way through a frame — the same fault as
        /// above, arriving from a different direction.
        /// </summary>
        private ConnectionTest.Result _testResult = ConnectionTest.Current;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Everything that can change underneath this window is settled here, on Layout only,
            // so the frame's two passes see identical state.
            if (Event.current.type == EventType.Layout)
            {
                if (_pendingProvider.HasValue)
                {
                    Settings.Provider = _pendingProvider.Value;
                    _pendingProvider = null;
                }

                _testResult = ConnectionTest.Current;
            }

            var viewRect = new Rect(0f, 0f, inRect.width - 20f, _contentHeight);
            Widgets.BeginScrollView(inRect, ref _scroll, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            try
            {
                DrawSettings(listing);
            }
            catch (Exception ex)
            {
                // An exception escaping between Begin and End leaves Unity's GUI stack unbalanced,
                // which takes the entire Options window down until the game is restarted — a far
                // worse outcome than one broken page. OnGUI runs every frame, so log once.
                ArkhLog.WarnOnce("The settings page threw while drawing: " + ex, 0xA9C1);
            }
            finally
            {
                _contentHeight = listing.CurHeight + 40f;
                listing.End();
                Widgets.EndScrollView();
            }

            base.DoSettingsWindowContents(inRect);
        }

        private void DrawSettings(Listing_Standard listing)
        {

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

            // --- Model ------------------------------------------------------------------
            Header(listing, "Language model");

            var info = ModelProviders.For(Settings.Provider);

            var providerRow = listing.GetRect(32f);
            providerRow.width = Mathf.Min(340f, providerRow.width);
            if (Widgets.ButtonText(providerRow, "Provider: " + info.DisplayName))
            {
                var options = new List<FloatMenuOption>();
                foreach (var provider in ModelProviders.All)
                {
                    var captured = provider;
                    options.Add(new FloatMenuOption(ModelProviders.For(captured).DisplayName,
                        () => _pendingProvider = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Gap(6f);

            if (!string.IsNullOrEmpty(info.Note)) listing.Label("<i>" + info.Note + "</i>");

            if (Settings.Provider == ModelProvider.Mock)
            {
                listing.Label($"Simulated delay: <b>{Settings.MockDelayMs} ms</b>");
                Settings.MockDelayMs = (int)listing.Slider(Settings.MockDelayMs, 0f, 3000f);

                listing.Label($"Simulated failure rate: <b>{Settings.MockFailureRate:P0}</b>");
                Settings.MockFailureRate = listing.Slider(Settings.MockFailureRate, 0f, 1f);
                listing.Label("<i>Worth turning up for a while. Real providers fail often, and a "
                              + "colony that has only ever seen success hides how that looks.</i>");
            }
            else
            {
                if (info.KeyRequired || Settings.Provider == ModelProvider.Custom)
                {
                    Settings.ApiKey = KeyField(listing, Settings.ApiKey);
                }

                Settings.ModelName = Field(listing, "Model", Settings.ModelName);
                if (string.IsNullOrEmpty(Settings.ModelName) && !string.IsNullOrEmpty(info.DefaultModel))
                {
                    listing.Label("<i>Empty uses " + info.DefaultModel + ".</i>");
                }

                Settings.BaseUrlOverride = Field(listing, "Base URL", Settings.BaseUrlOverride);
                if (string.IsNullOrEmpty(Settings.BaseUrlOverride) && !string.IsNullOrEmpty(info.BaseUrl))
                {
                    listing.Label("<i>Empty uses " + info.BaseUrl + ".</i>");
                }
            }

            // Answers "will colonists actually speak" without a twenty-minute colony load, and
            // works here at the main menu.
            listing.Gap(6f);
            var testRow = listing.GetRect(34f);
            testRow.width = Mathf.Min(220f, testRow.width);

            // Read from the frame's snapshot, not the live value, for the same reason as above.
            if (_testResult.Status == ConnectionTest.State.Running)
            {
                Widgets.ButtonText(testRow, "Testing…", active: false);
            }
            else if (Widgets.ButtonText(testRow, "Test connection"))
            {
                ConnectionTest.Start(Settings);
            }
            TooltipHandler.TipRegion(testRow,
                "Sends one short request using the real instruction and output contract, and reads "
                + "the reply back through the real parser. It answers whether this provider and "
                + "model will actually produce usable speech — not merely whether they are reachable.");

            var test = _testResult;
            if (!string.IsNullOrEmpty(test.Summary))
            {
                string colour = test.Status == ConnectionTest.State.Succeeded ? "#88DD88"
                    : test.Status == ConnectionTest.State.Failed ? "#FF8888"
                    : "#CCCCCC";

                listing.Label("<color=" + colour + ">" + test.Summary + "</color>");
                if (!string.IsNullOrEmpty(test.Detail)) listing.Label("<i>" + test.Detail + "</i>");
            }

            listing.Gap(6f);
            listing.Label($"Reply length cap: <b>{Settings.MaxResponseTokens}</b> tokens");
            Settings.MaxResponseTokens = (int)listing.Slider(Settings.MaxResponseTokens, 50f, 1000f);

            listing.Label($"Temperature: <b>{Settings.Temperature:0.00}</b>");
            Settings.Temperature = listing.Slider(Settings.Temperature, 0f, 2f);

            listing.Label($"Timeout: <b>{Settings.TimeoutSeconds}s</b>");
            Settings.TimeoutSeconds = (int)listing.Slider(Settings.TimeoutSeconds, 5f, 120f);

            listing.GapLine();

            // --- Conversations ----------------------------------------------------------
            Header(listing, "Conversations");

            listing.CheckboxLabeled("Colonists start conversations", ref Settings.TalkEnabled,
                "Turn this off to stop all requests without unloading the mod.");

            listing.Label($"At most one line per colonist every <b>{Settings.TalkIntervalSeconds}s</b>");
            Settings.TalkIntervalSeconds = (int)listing.Slider(Settings.TalkIntervalSeconds, 5f, 600f);

            listing.Label($"Requests in flight at once: <b>{Settings.MaxInFlight}</b>");
            Settings.MaxInFlight = (int)listing.Slider(Settings.MaxInFlight, 1f, 10f);
            listing.Label("<i>A spend control as much as a pacing one — every line is a paid "
                          + "request.</i>");

            listing.Label($"Conversation range: <b>{Settings.TalkRadius:0}</b> tiles");
            Settings.TalkRadius = listing.Slider(Settings.TalkRadius, 2f, 40f);

            listing.CheckboxLabeled("Allow talking to oneself", ref Settings.AllowMonologue,
                "When nobody is close enough, let a colonist speak anyway.");

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

        }

        private static void Header(Listing_Standard listing, string text)
        {
            Text.Font = GameFont.Medium;
            listing.Label(text);
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// The API key field, hidden by default.
        ///
        /// Not security — anyone with the save folder has the key anyway — but people stream and
        /// screenshot this game, and an API key sitting in plain text on a settings page is the
        /// kind of thing that ends up on video.
        /// </summary>
        private static string KeyField(Listing_Standard listing, string value)
        {
            var row = listing.GetRect(28f);
            var labelRect = new Rect(row.x, row.y, row.width * 0.32f, row.height);
            var buttonRect = new Rect(row.xMax - 70f, row.y, 70f, row.height);
            var fieldRect = new Rect(labelRect.xMax + 6f, row.y,
                row.width - labelRect.width - 82f, row.height);

            Widgets.Label(labelRect, "API key");

            string result = value ?? "";
            if (_showKey)
            {
                result = Widgets.TextField(fieldRect, result);
            }
            else
            {
                string shown = string.IsNullOrEmpty(result)
                    ? "(not set)"
                    : new string('•', Math.Min(24, result.Length));
                Widgets.Label(fieldRect, shown);
            }

            if (Widgets.ButtonText(buttonRect, _showKey ? "Hide" : "Show")) _showKey = !_showKey;

            listing.Gap(4f);
            return result;
        }

        private static bool _showKey;

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
