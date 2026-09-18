using System.Collections.Generic;
using System.Linq;
using Arkh.Prompt;

namespace Arkh.Budget
{
    /// <summary>
    /// The one thing that knows how much prompt text this mod may add, and divides it up.
    ///
    /// Without this, every feature caps itself in isolation — and eight features each capped
    /// "reasonably" still add up to a prompt nobody intended and a bill nobody predicted. So the
    /// total lives here, and sections get an allowance rather than a private limit.
    ///
    /// Allocation is priority-ordered and greedy: the most important section takes what it wants,
    /// the next takes what is left, and so on down. That degrades in the right direction — when
    /// there is a lot to say, it is the background text that gets squeezed and not the thing that
    /// made this conversation worth having. See docs/DESIGN.md §12.
    ///
    /// Recomputed only when something changes, never per prompt: providers run on the tick path
    /// and this must cost nothing there.
    /// </summary>
    public static class PromptBudget
    {
        private static readonly Dictionary<string, int> Allowances = new Dictionary<string, int>();

        private static bool _dirty = true;

        /// <summary>Total characters this mod may contribute to a single prompt.</summary>
        private static int Total
        {
            get
            {
                var settings = ArkhMod.Settings;
                return settings == null ? 2000 : settings.TotalBudgetChars;
            }
        }

        /// <summary>
        /// How many pawns a typical conversation is assumed to involve.
        ///
        /// Pawn sections are built once per participant while environment sections are built once
        /// per prompt, so a pawn section's true cost is its text times the number of people
        /// talking. Without this the budget would badly under-count a four-way conversation.
        /// </summary>
        private static int AssumedParticipants
        {
            get
            {
                var settings = ArkhMod.Settings;
                return settings == null ? 3 : UnityEngine.Mathf.Max(1, settings.AssumedParticipants);
            }
        }

        /// <summary>Call after anything that could change the allocation.</summary>
        public static void Invalidate() => _dirty = true;

        /// <summary>
        /// Characters this section may emit on a single call. Zero means it was squeezed out
        /// entirely, which is a legitimate outcome and not an error.
        /// </summary>
        public static int For(string sectionName)
        {
            if (_dirty) Recompute();
            return Allowances.TryGetValue(sectionName, out int allowance) ? allowance : 0;
        }

        /// <summary>The allocation table, for the profile panel to show.</summary>
        public static List<(string Section, int Allowance, int Desired, bool Squeezed)> Table()
        {
            if (_dirty) Recompute();

            var rows = new List<(string, int, int, bool)>();
            foreach (var declaration in Ordered())
            {
                int desired = declaration.SafeDesired();
                int allowance = Allowances.TryGetValue(declaration.SectionName, out int a) ? a : 0;
                rows.Add((declaration.SectionName, allowance, desired, allowance < desired));
            }
            return rows;
        }

        /// <summary>Total characters the current allocation could actually spend.</summary>
        public static int Committed()
        {
            if (_dirty) Recompute();

            int total = 0;
            foreach (var declaration in Ordered())
            {
                if (!Allowances.TryGetValue(declaration.SectionName, out int allowance)) continue;
                total += allowance * Multiplier(declaration);
            }
            return total;
        }

        public static int TotalChars => Total;

        public static int Participants => AssumedParticipants;

        private static IEnumerable<PromptSection> Ordered()
        {
            // Lower BudgetPriority is served first. Ties broken by name so the order is stable
            // between sessions and the panel does not reshuffle itself.
            return PromptCatalog.Sections
                .OrderBy(d => d.BudgetPriority)
                .ThenBy(d => d.SectionName);
        }

        private static int Multiplier(PromptSection d)
        {
            return d.PerParticipant ? AssumedParticipants : 1;
        }

        private static void Recompute()
        {
            _dirty = false;
            Allowances.Clear();

            int remaining = Total;

            foreach (var declaration in Ordered())
            {
                int desired = declaration.SafeDesired();
                int multiplier = Multiplier(declaration);

                // What one call could afford if this section took everything still unspent.
                int affordablePerCall = remaining / multiplier;

                int granted = desired < affordablePerCall ? desired : affordablePerCall;
                if (granted < 0) granted = 0;

                Allowances[declaration.SectionName] = granted;
                remaining -= granted * multiplier;
            }
        }
    }
}
