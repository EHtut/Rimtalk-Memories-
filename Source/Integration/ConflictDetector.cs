using System.Collections.Generic;
using System.Linq;
using System.Text;
using Arkh.Util;
using Verse;

namespace Arkh.Integration
{
    /// <summary>
    /// Arkh generates colony dialogue itself. Anything else doing the same job at the same time
    /// is not a crash, but it is a bad time: two mods answering for the same pawn, two prompts,
    /// two bills.
    ///
    /// So say so once, clearly, at startup, and leave the choice to the player. Disabling another
    /// author's mod on their behalf would be worse than the overlap, and silently going dormant
    /// would be worse still — a mod that does nothing and says nothing is the hardest kind of
    /// problem to diagnose.
    /// </summary>
    public static class ConflictDetector
    {
        /// <summary>
        /// RimTalk itself. Arkh replaces it rather than extending it, so running both means both
        /// will generate dialogue for the same colonists.
        /// </summary>
        private const string RimTalk = "cj.rimtalk";

        /// <summary>
        /// Package id, and what about it overlaps. Keep this in step with the "Mods this replaces"
        /// table in README.md.
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
            var message = new StringBuilder();

            if (ModsConfig.IsActive(RimTalk))
            {
                message.AppendLine("RimTalk is active alongside Arkh.");
                message.AppendLine("  These are not compatible: both generate dialogue for the same pawns, "
                                   + "so colonists will talk over themselves and every line is paid for twice.");
                message.AppendLine("  Arkh replaces RimTalk rather than extending it. Disable one of them.");
            }

            var overlapping = Superseded.Where(pair => ModsConfig.IsActive(pair.Key)).ToList();

            if (overlapping.Count > 0)
            {
                message.AppendLine(overlapping.Count == 1
                    ? "A RimTalk companion mod is active, which Arkh also covers:"
                    : "RimTalk companion mods are active, which Arkh also covers:");

                foreach (var pair in overlapping)
                {
                    message.AppendLine("  - " + pair.Key + " (overlaps: " + pair.Value + ")");
                }

                message.Append("  These extend RimTalk, so they do nothing for Arkh and will keep driving "
                               + "RimTalk if it is still installed.");
            }

            if (message.Length > 0) ArkhLog.Warning(message.ToString().TrimEnd('\r', '\n'));
        }
    }
}
