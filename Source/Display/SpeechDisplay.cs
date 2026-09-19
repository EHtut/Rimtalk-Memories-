using System.Linq;
using Arkh.Prompt;
using Arkh.Talk;
using Arkh.Util;
using RimWorld;
using Verse;

namespace Arkh.Display
{
    /// <summary>
    /// Puts spoken lines where players can see them.
    ///
    /// Arkh takes a hard dependency on Interaction Bubbles rather than drawing its own overhead
    /// text. Bubbles already solves that problem well, is widely installed, and has settings
    /// players have already tuned — reimplementing it would mean a second set of controls doing
    /// the same job, and two bubbles per line for anyone running both.
    ///
    /// The bridge is one call: add a <see cref="PlayLogEntry_ArkhSpeech"/> to the play log.
    /// Bubbles patches vanilla <c>PlayLog.Add</c> and takes it from there, so there is no
    /// assembly reference, no patch of our own, and nothing to break when Bubbles updates.
    /// </summary>
    public static class SpeechDisplay
    {
        private static bool _wired;

        public static void Wire()
        {
            if (_wired) return;
            _wired = true;

            TalkEngine.Spoke += Show;
        }

        private static void Show(TalkScene scene, SpokenLine line)
        {
            if (scene == null || string.IsNullOrEmpty(line.Text)) return;

            var speaker = ResolveSpeaker(scene, line.Speaker);
            if (speaker == null) return;

            // Whoever else is in the scene is the audience. With one participant this is a
            // monologue and the entry has no recipient, which Bubbles handles — it accepts a null
            // recipient and simply renders above the speaker.
            var listener = scene.Participants.FirstOrDefault(p => p != null && p != speaker);

            var log = Find.PlayLog;
            if (log == null) return;

            log.Add(new PlayLogEntry_ArkhSpeech(InteractionDefOf.Chitchat, speaker, listener, line.Text));
        }

        /// <summary>
        /// Matches the name the model used back to a pawn in the scene.
        ///
        /// Models paraphrase, abbreviate and occasionally invent, so an exact match is not
        /// something to rely on. Falling back to the initiator is right: a line attributed to the
        /// wrong colonist is a small oddity, while dropping it entirely wastes a paid request and
        /// looks like the mod is broken.
        /// </summary>
        private static Pawn ResolveSpeaker(TalkScene scene, string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var pawn in scene.Participants)
                {
                    if (pawn == null) continue;
                    if (string.Equals(pawn.LabelShort, name, System.StringComparison.OrdinalIgnoreCase)) return pawn;
                }
            }

            return scene.Initiator;
        }

        /// <summary>
        /// Interaction Bubbles is a declared dependency, so RimWorld warns when it is absent. It
        /// does not warn when it is present but switched off inside its own settings, and the
        /// symptom then is silent colonists — indistinguishable from every other cause of silence.
        /// Saying so once at startup costs nothing.
        /// </summary>
        public static void WarnIfMissing()
        {
            if (!ModsConfig.IsActive("Jaxe.Bubbles"))
            {
                ArkhLog.Warning(
                    "Interaction Bubbles is not active. Arkh depends on it to show speech above "
                    + "colonists — lines will still appear in the social log, but nowhere else.");
            }
        }
    }
}
