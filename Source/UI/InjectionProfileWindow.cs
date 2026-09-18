using System;
using System.Collections.Generic;
using RimTalkMemories.Budget;
using RimTalkMemories.Context;
using RimTalkMemories.Integration;
using RimTalkMemories.Util;
using UnityEngine;
using Verse;

namespace RimTalkMemories.UI
{
    /// <summary>
    /// Shows what this mod contributes to RimTalk's prompts, and what RimTalk was already sending.
    ///
    /// Two things shape this window.
    ///
    /// A merged prompt cannot tell you who wrote what: RimTalk ships its own prompts and ours
    /// layer on top, so hunting for our text in the assembled output is guesswork that gets worse
    /// as the layer grows. So the Profile tab renders <see cref="RimTalkApi.Registrations"/> —
    /// the declarations that produce the text — rather than trying to find the text afterwards.
    /// It reads the source, not the result, and therefore cannot be wrong about what we added.
    ///
    /// And RimWorld takes about twenty minutes to load, so the Profile tab is built to work with
    /// no save open. Mod settings load at startup, ours and RimTalk's both, so the whole static
    /// picture — every age band's exact wording, the lore text, what it costs — can be reviewed
    /// and corrected from the main menu. Only the Live tab needs a running game.
    /// </summary>
    public class InjectionProfileWindow : Window
    {
        private enum Tab
        {
            Profile,
            Live
        }

        private Tab _tab = Tab.Profile;
        private Vector2 _scroll;
        private float _contentHeight = 1200f;

        public override Vector2 InitialSize => new Vector2(920f, 740f);

        public InjectionProfileWindow()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            preventCameraMotion = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "RimTalk Memories — injection profile");
            Text.Font = GameFont.Small;

            var body = new Rect(inRect.x, inRect.y + 70f, inRect.width, inRect.height - 70f);
            Widgets.DrawMenuSection(body);

            TabDrawer.DrawTabs(body, new List<TabRecord>
            {
                new TabRecord("Profile", () => _tab = Tab.Profile, _tab == Tab.Profile),
                new TabRecord("Live", () => _tab = Tab.Live, _tab == Tab.Live)
            });

            var content = body.ContractedBy(12f);
            var view = new Rect(0f, 0f, content.width - 20f, _contentHeight);

            Widgets.BeginScrollView(content, ref _scroll, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            if (_tab == Tab.Profile) DrawProfile(listing);
            else DrawLive(listing);

            _contentHeight = listing.CurHeight + 40f;
            listing.End();
            Widgets.EndScrollView();
        }

        // --- Profile -------------------------------------------------------------------------

        private void DrawProfile(Listing_Standard listing)
        {
            DrawNativePreset(listing);
            listing.GapLine(12f);
            DrawOurLayer(listing);
            listing.GapLine(12f);
            DrawVariables(listing);
            listing.GapLine(12f);
            DrawCost(listing);
        }

        private static void DrawNativePreset(Listing_Standard listing)
        {
            Header(listing, "1. What RimTalk already sends");

            var preset = RimTalkApi.ActivePreset();
            if (preset == null)
            {
                Note(listing, "RimTalk's active preset could not be read. It normally loads with mod "
                              + "settings at startup, so this is unexpected — check the log.");
                return;
            }

            listing.Label("Active preset: <b>" + preset.Name + "</b>   ("
                          + (preset.Entries?.Count ?? 0) + " entries)");

            if (preset.Entries == null) return;

            foreach (var entry in preset.Entries)
            {
                if (entry == null) continue;

                string enabled = entry.Enabled ? "" : "  <i>(disabled)</i>";
                string mine = entry.SourceModId == RimTalkApi.ModId ? "  <b>[ours]</b>" : "";
                int chars = entry.Content?.Length ?? 0;

                listing.Label("   • " + entry.Name + "   <i>" + entry.Role + " / " + entry.Position
                              + "</i>   " + chars + " chars" + enabled + mine);
            }
        }

        private void DrawOurLayer(Listing_Standard listing)
        {
            Header(listing, "2. What RimTalk Memories adds on top");

            if (RimTalkApi.Registrations.Count == 0)
            {
                Note(listing, "Nothing registered. That is a bug — check the log for registration errors.");
                return;
            }

            foreach (var declaration in RimTalkApi.Registrations)
            {
                DrawDeclaration(listing, declaration);
            }
        }

        private void DrawDeclaration(Listing_Standard listing, InjectionDeclaration d)
        {
            listing.Gap(8f);

            var row = listing.GetRect(28f);
            var labelRect = new Rect(row.x, row.y, row.width - 260f, row.height);
            var modeRect = new Rect(labelRect.xMax + 10f, row.y, 250f, row.height);

            Text.Font = GameFont.Small;
            Widgets.Label(labelRect, "<b>" + d.SectionName + "</b>   on <i>" + d.Anchor + "</i>");

            if (Widgets.ButtonText(modeRect, DescribeMode(d.Mode)))
            {
                OpenModeMenu(d);
            }
            TooltipHandler.TipRegion(modeRect, ModeTooltip(d));

            if (!string.IsNullOrEmpty(d.Note))
            {
                Note(listing, d.Note);
            }

            int allowance = PromptBudget.For(d.SectionName);

            // Nothing allocated has two very different causes, and conflating them would send
            // someone hunting a budget problem that does not exist.
            if (allowance <= 0)
            {
                if (d.SafeDesired() <= 0)
                {
                    Note(listing, "Nothing to send — this section has no text written, so it asks "
                                  + "for no budget and emits nothing. Not a problem.");
                }
                else
                {
                    Note(listing, "The budget squeezed this section out entirely — it will emit "
                                  + "nothing. Raise the total, or lower what something earlier in "
                                  + "the order wants.");
                }
                return;
            }

            // Show what will really be emitted, not the untrimmed authored text. The whole point
            // of this panel is that it does not lie about what reaches the prompt.
            foreach (var variant in d.SafeVariants())
            {
                string emitted = TextUtil.Clamp(variant.Text, allowance);
                bool trimmed = emitted.Length < variant.Text.Length;

                string header = "      <b>" + variant.Label + "</b>  (" + emitted.Length + " chars"
                                + (trimmed ? ", <color=#FFCC66>trimmed from " + variant.Text.Length + " by budget</color>" : "")
                                + ")";

                var text = header + "\n      " + emitted.Replace("\n", "\n      ");

                float height = Text.CalcHeight(text, listing.ColumnWidth);
                Widgets.Label(listing.GetRect(height), text);
            }
        }

        private void OpenModeMenu(InjectionDeclaration d)
        {
            var options = new List<FloatMenuOption>();

            foreach (InjectionMode mode in Enum.GetValues(typeof(InjectionMode)))
            {
                var captured = mode;
                string suffix = captured == ContextRegistrar.DefaultModeFor(d.SectionName) ? "   (default)" : "";

                options.Add(new FloatMenuOption(DescribeMode(captured) + suffix, () => SetMode(d, captured)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void SetMode(InjectionDeclaration d, InjectionMode mode)
        {
            d.Mode = mode;

            var settings = RimTalkMemoriesMod.Settings;
            if (settings != null)
            {
                settings.SetMode(d.SectionName, mode);
                settings.Write();
            }

            // Re-push everything. Registering without clearing first would leave the old
            // registration in place alongside the new one.
            RimTalkApi.ApplyAll();
        }

        private static string DescribeMode(InjectionMode mode)
        {
            switch (mode)
            {
                case InjectionMode.InjectAfter: return "Separate block, after";
                case InjectionMode.InjectBefore: return "Separate block, before";
                case InjectionMode.HookAppend: return "Folded into RimTalk's text";
                case InjectionMode.HookOverride: return "Replaces RimTalk's text";
                default: return mode.ToString();
            }
        }

        private static string ModeTooltip(InjectionDeclaration d)
        {
            return "How this section attaches to " + d.Anchor + ".\n\n"
                   + "Separate block: adds its own lines near RimTalk's text. Only appears where "
                   + "RimTalk assembles prose — not in prompt templates that reference this category.\n\n"
                   + "Folded in: becomes part of RimTalk's own value for this category. Also applies "
                   + "inside templates, so it reaches further.\n\n"
                   + "Replaces: RimTalk's text for this category is discarded in favour of ours. "
                   + "Powerful, but we then own that text forever and stop receiving RimTalk's "
                   + "improvements to it.";
        }

        private static void DrawVariables(Listing_Standard listing)
        {
            Header(listing, "3. Template variables this mod registers");

            if (RimTalkApi.RegisteredVariables.Count == 0)
            {
                Note(listing, "None.");
                return;
            }

            foreach (var variable in RimTalkApi.RegisteredVariables)
            {
                listing.Label("   • <b>" + variable.Name + "</b> — " + variable.Description);
            }

            Note(listing, "Usable in any RimTalk prompt preset. Placing one by hand puts the text "
                          + "exactly where you want it, instead of where the anchor happens to sit.");
        }

        private static void DrawCost(Listing_Standard listing)
        {
            Header(listing, "4. The character budget");

            listing.Label("Ceiling: <b>" + PromptBudget.TotalChars + " characters</b> per prompt"
                          + "   (~" + Mathf.CeilToInt(PromptBudget.TotalChars / 4f) + " tokens in English)"
                          + ", assuming " + PromptBudget.Participants + "-pawn conversations.");

            listing.Gap(4f);

            foreach (var row in PromptBudget.Table())
            {
                string cost = row.Squeezed
                    ? "<color=#FFCC66>" + row.Allowance + " chars — wanted " + row.Desired + "</color>"
                    : row.Allowance + " chars";

                listing.Label("   • <b>" + row.Section + "</b>: " + cost);
            }

            listing.Gap(4f);
            listing.Label("Allocated: <b>" + PromptBudget.Committed() + "</b> of "
                          + PromptBudget.TotalChars + " characters.");

            Note(listing, "Served in priority order, most important first: what a pawn remembers, "
                          + "then how they speak, then what everyone knows. So a tight budget eats "
                          + "the background before it touches anything specific to the moment.");

            Note(listing, "Characters, not tokens — real tokenisation is not affordable on the tick "
                          + "path. Roughly 4 characters per token in English; Chinese, Japanese and "
                          + "Korean run far denser, so this under-counts badly for those languages.");
        }

        // --- Live ----------------------------------------------------------------------------

        private static void DrawLive(Listing_Standard listing)
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                Header(listing, "No colony loaded");
                Note(listing, "Call counts and the assembled prompt need a running game. Everything "
                              + "on the Profile tab works here at the main menu, so check the wording "
                              + "and the cost there first — this tab only answers whether RimTalk "
                              + "actually reaches each anchor.");
                return;
            }

            Header(listing, "Did RimTalk call us?");

            foreach (var d in RimTalkApi.Registrations)
            {
                string verdict;
                if (d.Calls == 0)
                {
                    verdict = "<color=#FF6666>never called — this anchor is not reached</color>";
                }
                else if (d.NonEmptyReturns == 0)
                {
                    verdict = "<color=#FFCC66>called " + d.Calls + "x, always returned nothing</color>";
                }
                else
                {
                    verdict = "<color=#88DD88>called " + d.Calls + "x, emitted " + d.NonEmptyReturns + "x</color>";
                }

                listing.Label("   • <b>" + d.SectionName + "</b> (" + DescribeMode(d.Mode) + ") — " + verdict);

                if (!string.IsNullOrEmpty(d.LastEmitted))
                {
                    listing.Label("        last: <i>" + d.LastEmitted.Replace("\n", " ") + "</i>");
                }
            }

            Note(listing, "Never called and called-but-empty are different faults. Empty is often "
                          + "correct — adults add no age line by design, and gender voice is off "
                          + "until you write it. Never called means the anchor is dead: try folding "
                          + "into RimTalk's text instead, which travels a different code path.");

            listing.GapLine(12f);
            Header(listing, "Last assembled prompt");

            string prompt = LastAssembledPrompt();
            if (string.IsNullOrEmpty(prompt))
            {
                Note(listing, "No prompt recorded yet. Let two pawns hold a conversation, or send one "
                              + "through RimTalk's own talk window, then reopen this.");
                return;
            }

            float height = Text.CalcHeight(prompt, listing.ColumnWidth);
            Widgets.Label(listing.GetRect(height), prompt);
        }

        /// <summary>
        /// The most recent prompt RimTalk actually built, read from its own log rather than
        /// reconstructed. A reconstruction would drift from the real thing.
        /// </summary>
        private static string LastAssembledPrompt()
        {
            try
            {
                RimTalk.Data.ApiLog newest = null;

                foreach (var log in RimTalk.Data.ApiHistory.GetAll())
                {
                    if (log?.TalkRequest == null) continue;
                    if (string.IsNullOrEmpty(log.TalkRequest.Prompt)) continue;
                    if (newest == null || log.Timestamp > newest.Timestamp) newest = log;
                }

                return newest?.TalkRequest.Prompt;
            }
            catch (Exception ex)
            {
                return "Could not read RimTalk's API history: " + ex.Message;
            }
        }

        // --- Small helpers -------------------------------------------------------------------

        private static void Header(Listing_Standard listing, string text)
        {
            Text.Font = GameFont.Medium;
            listing.Label(text);
            Text.Font = GameFont.Small;
        }

        private static void Note(Listing_Standard listing, string text)
        {
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            listing.Label("<i>" + text + "</i>");
            GUI.color = Color.white;
        }
    }
}
