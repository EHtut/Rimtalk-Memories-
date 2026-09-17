using System.Collections.Generic;
using Verse;

namespace RimTalkMemories.Util
{
    /// <summary>
    /// Every log line this mod writes goes through here, so they all carry the same prefix
    /// and a player can tell at a glance which mod is complaining.
    /// </summary>
    public static class RTMLog
    {
        private const string Prefix = "[RimTalk Memories] ";

        private static readonly HashSet<int> SeenOnce = new HashSet<int>();

        public static void Message(string text) => Log.Message(Prefix + text);

        public static void Warning(string text) => Log.Warning(Prefix + text);

        public static void Error(string text) => Log.Error(Prefix + text);

        /// <summary>
        /// Warns once per key and stays quiet afterwards.
        ///
        /// Context providers run every time a prompt is built. A provider that throws would
        /// otherwise spam the log hundreds of times a session and bury whatever went wrong
        /// first, so anything on that path warns through here.
        /// </summary>
        public static void WarnOnce(string text, int key)
        {
            if (!SeenOnce.Add(key)) return;
            Log.Warning(Prefix + text);
        }

        /// <summary>Only written when the player has RimWorld's dev mode on.</summary>
        public static void Debug(string text)
        {
            if (Prefs.DevMode) Log.Message(Prefix + text);
        }
    }
}
