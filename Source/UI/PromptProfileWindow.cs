using System;
using System.Collections.Generic;
using System.Linq;
using Arkh.Budget;
using Arkh.Prompt;
using Arkh.Talk;
using Arkh.Util;
using UnityEngine;
using Verse;

namespace Arkh.UI
{
    /// <summary>
    /// Shows the prompt this mod builds: its skeleton, every block that can go in it, and what it
    /// all costs.
    ///
    /// It renders <see cref="PromptCatalog.Sections"/> — the declarations that produce the text —
    /// rather than inspecting an assembled prompt after the fact. Reading the source instead of
    /// the result means it cannot be wrong about what we send.
    ///
    /// And it works with **no colony loaded**. RimWorld loads mod settings at startup, so the
    /// whole static picture — every age band's wording, the lore text, the budget split — can be
    /// reviewed and corrected from the main menu. With a twenty-minute load that is the difference
    /// between a usable tool and a useless one.
    /// </summary>
    public class PromptProfileWindow : Window
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

        public PromptProfileWindow()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            preventCameraMotion = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "Arkh — prompt profile");
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

            try
            {
                if (_tab == Tab.Profile) DrawProfile(listing);
                else DrawLive(listing);
            }
            catch (Exception ex)
            {
                // This panel renders text produced by section providers, any of which could throw.
                // An exception escaping between Begin and End leaves Unity's GUI stack unbalanced
                // and takes the whole window stack down with it, so it stops here. OnGUI runs every
                // frame, hence warn-once.
                ArkhLog.WarnOnce("The prompt profile panel threw while drawing: " + ex, 0xA9C2);
            }
            finally
            {
                _contentHeight = listing.CurHeight + 40f;
                listing.End();
                Widgets.EndScrollView();
            }
        }

        // --- Profile -------------------------------------------------------------------------

        private void DrawProfile(Listing_Standard listing)
        {
            DrawSkeleton(listing);
            listing.GapLine(12f);
            DrawSections(listing);
            listing.GapLine(12f);
            DrawBudget(listing);
        }

        /// <summary>
        /// The prompt's shape, slot by slot. Empty slots are shown rather than hidden — a slot
        /// with nothing in it is information, not clutter, and several are placeholders for
        /// pillars not built yet.
        /// </summary>
        private static void DrawSkeleton(Listing_Standard listing)
        {
            Header(listing, "1. The prompt we build");

            foreach (PromptSlot slot in Enum.GetValues(typeof(PromptSlot)))
            {
                var inSlot = PromptCatalog.Sections.Where(s => s.Slot == slot).ToList();

                if (inSlot.Count == 0)
                {
                    GUI.color = new Color(0.55f, 0.55f, 0.55f);
                    listing.Label("   " + slot + " — <i>nothing yet</i>");
                    GUI.color = Color.white;
                    continue;
                }

                string names = string.Join(", ", inSlot.OrderBy(s => s.Order).Select(s => s.SectionName).ToArray());
                listing.Label("   <b>" + slot + "</b> — " + names);
            }

            Note(listing, "Slots are emitted in this order. Memory is a slot of its own rather than "
                          + "a transcript wedged into the conversation — what a pawn remembers is "
                          + "part of who is speaking.");
        }

        private void DrawSections(Listing_Standard listing)
        {
            Header(listing, "2. What goes in each block");

            if (PromptCatalog.Sections.Count == 0)
            {
                Note(listing, "Nothing declared. That is a bug — check the log.");
                return;
            }

            foreach (var section in PromptCatalog.InPromptOrder())
            {
                DrawSection(listing, section);
            }
        }

        private void DrawSection(Listing_Standard listing, PromptSection s)
        {
            listing.Gap(8f);
            listing.Label("<b>" + s.SectionName + "</b>   <i>" + s.Slot
                          + (s.PerParticipant ? ", per speaker" : ", once per prompt") + "</i>");

            if (!string.IsNullOrEmpty(s.Note)) Note(listing, s.Note);

            int allowance = PromptBudget.For(s.SectionName);

            // Nothing allocated has two very different causes, and conflating them would send
            // someone hunting a budget problem that does not exist.
            if (allowance <= 0)
            {
                Note(listing, s.SafeDesired() <= 0
                    ? "Nothing to send — no text written, so it asks for no budget. Not a problem."
                    : "The budget squeezed this out entirely. Raise the total, or lower what "
                      + "something earlier in the order wants.");
                return;
            }

            // Show what will really be emitted, not the untrimmed authored text. The point of this
            // panel is that it does not lie about what reaches the prompt.
            foreach (var variant in s.SafeVariants())
            {
                string emitted = TextUtil.Clamp(variant.Text, allowance);
                bool trimmed = emitted.Length < variant.Text.Length;

                string header = "      <b>" + variant.Label + "</b>  (" + emitted.Length + " chars"
                                + (trimmed ? ", <color=#FFCC66>trimmed from " + variant.Text.Length + "</color>" : "")
                                + ")";

                var text = header + "\n      " + emitted.Replace("\n", "\n      ");
                Widgets.Label(listing.GetRect(Text.CalcHeight(text, listing.ColumnWidth)), text);
            }
        }

        private static void DrawBudget(Listing_Standard listing)
        {
            Header(listing, "3. The character budget");

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
                          + "then how they speak, then what everyone knows. A tight budget eats the "
                          + "background before it touches anything specific to the moment.");

            Note(listing, "Characters, not tokens — real tokenisation is not affordable on the tick "
                          + "path. Roughly 4 characters per token in English; Chinese, Japanese and "
                          + "Korean run far denser, so this under-counts for those languages.");
        }

        // --- Live ----------------------------------------------------------------------------

        private static void DrawLive(Listing_Standard listing)
        {
            Header(listing, "The talk engine");

            listing.Label($"Requests: <b>{TalkEngine.Requests}</b>   "
                          + $"failures: <b>{TalkEngine.Failures}</b>   "
                          + $"in flight: <b>{TalkEngine.InFlight}</b>");

            listing.Label($"Tokens this session: <b>{TalkEngine.PromptTokens}</b> prompt, "
                          + $"<b>{TalkEngine.CompletionTokens}</b> reply");

            // The reason nothing is happening is the single most useful thing this panel can say.
            // Silence has a dozen causes that look identical from the outside.
            if (!string.IsNullOrEmpty(TalkEngine.LastBlockReason))
            {
                listing.Label("Not starting anything: <color=#FFCC66>" + TalkEngine.LastBlockReason + "</color>");
            }

            if (TalkEngine.LastFailure != null)
            {
                listing.Label("Last failure: <color=#FF8888>" + TalkEngine.LastFailure.Summary + "</color>");
                if (!string.IsNullOrEmpty(TalkEngine.LastFailure.Detail))
                {
                    Note(listing, TalkEngine.LastFailure.Detail);
                }
            }

            listing.Gap(6f);
            Header(listing, "Recent lines");

            if (TalkEngine.Recent.Count == 0)
            {
                Note(listing, "Nothing said yet.");
            }
            else
            {
                foreach (var line in TalkEngine.Recent)
                {
                    listing.Label("   <b>" + line.Speaker + "</b>: " + line.Text);
                }
            }

            listing.GapLine(12f);
            Header(listing, "Built blocks");

            listing.Gap(6f);

            foreach (var s in PromptCatalog.Sections)
            {
                string verdict;
                if (s.Calls == 0)
                {
                    verdict = "<color=#999999>not built yet</color>";
                }
                else if (s.NonEmptyReturns == 0)
                {
                    verdict = "<color=#FFCC66>built " + s.Calls + "x, always empty</color>";
                }
                else
                {
                    verdict = "<color=#88DD88>built " + s.Calls + "x, emitted " + s.NonEmptyReturns + "x</color>";
                }

                listing.Label("   • <b>" + s.SectionName + "</b> — " + verdict);

                if (!string.IsNullOrEmpty(s.LastEmitted))
                {
                    listing.Label("        last: <i>" + s.LastEmitted.Replace("\n", " ") + "</i>");
                }
            }

            Note(listing, "Built-but-empty is often correct: adults add no age line by design, and "
                          + "gender voice stays silent until you write it.");
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
