using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimTalkMemories.Util;
using Verse;

namespace RimTalkMemories.Integration
{
    /// <summary>
    /// RimTalk Memories takes over work that several separate companion mods each did on
    /// their own. Running one of those alongside it is not a crash, but it is a bad time:
    /// both inject their own version of the same context and the pawn ends up describing
    /// their age twice, or two mods fight over the same distance check.
    ///
    /// So say so once, clearly, at startup, and leave the choice to the player. Disabling
    /// another author's mod on their behalf would be worse than the duplication.
    /// </summary>
    public static class CompanionConflicts
    {
        /// <summary>
        /// Package id, and which part of this mod supersedes it. Keep this in step with the
        /// "Mods this replaces" table in README.md.
        /// </summary>
        private static readonly Dictionary<string, string> Superseded = new Dictionary<string, string>
        {
            { "wuren.rimtalkcontextupgrade", "context and prompt settings" },
            { "saltgin.rimtalkeventmemory", "event memory" },
            { "alus.rimtalk.lucidchronicle", "long-term memory and world lore" },
            { "youyu.rimtalk.distancecontrol", "conversation distance" },
        };

        public static void WarnIfAnyActive()
        {
            var active = Superseded
                .Where(pair => ModsConfig.IsActive(pair.Key))
                .ToList();

            if (active.Count == 0) return;

            var message = new StringBuilder();
            message.AppendLine(active.Count == 1
                ? "A mod is active that RimTalk Memories already replaces:"
                : "Mods are active that RimTalk Memories already replaces:");

            foreach (var pair in active)
            {
                message.AppendLine("  - " + pair.Key + " (superseded by: " + pair.Value + ")");
            }

            message.Append("Both will write into the same prompts, so pawns may repeat themselves. "
                           + "Disabling the listed mod(s) is recommended.");

            RTMLog.Warning(message.ToString());
        }
    }
}
