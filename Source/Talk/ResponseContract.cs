using System.Collections.Generic;
using Arkh.Util;

namespace Arkh.Talk
{
    public struct SpokenLine
    {
        public string Speaker;
        public string Text;

        public SpokenLine(string speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }

        public override string ToString() => Speaker + ": " + Text;
    }

    /// <summary>
    /// Reads what the model sent back.
    ///
    /// Paired with <c>CoreSections.DefaultContract</c>, which is the prompt half of the same
    /// bargain — they live close together because a contract described in one place and parsed in
    /// another drifts apart silently.
    ///
    /// **Tolerance is the whole design.** Models wrap JSON in code fences, prefix it with "Sure,
    /// here you go", or ignore the format entirely and just write the line. None of that is worth
    /// discarding a paid-for response over, so every one of those degrades to something usable and
    /// only genuinely empty input fails.
    /// </summary>
    public static class ResponseContract
    {
        public static List<SpokenLine> Parse(string reply, string fallbackSpeaker)
        {
            var lines = new List<SpokenLine>();
            if (string.IsNullOrEmpty(reply)) return lines;

            string body = StripFences(reply).Trim();

            var root = Json.Parse(ExtractObject(body));
            var array = root["lines"];

            if (array.IsArray)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    string speaker = array[i]["speaker"].AsString(fallbackSpeaker);
                    string text = Clean(array[i]["text"].AsString(""));

                    if (!string.IsNullOrEmpty(text))
                    {
                        lines.Add(new SpokenLine(string.IsNullOrEmpty(speaker) ? fallbackSpeaker : speaker, text));
                    }
                }

                // Return even when empty. The model answered in the contracted shape and simply had
                // nothing to say — falling through to the prose branch here would hand the raw JSON
                // to a colonist as their line, which is exactly the sort of thing players screenshot.
                return lines;
            }

            // The model answered in prose. That is a formatting miss, not a failure: the text is
            // usually exactly the line we asked for, and throwing it away would waste a request
            // the player paid for and leave a colonist mute for no visible reason.
            string plain = Clean(body);
            if (!string.IsNullOrEmpty(plain)) lines.Add(new SpokenLine(fallbackSpeaker, plain));

            return lines;
        }

        /// <summary>Removes a ``` wrapper, with or without a language tag.</summary>
        private static string StripFences(string text)
        {
            string s = text.Trim();
            if (!s.StartsWith("```")) return s;

            int firstNewline = s.IndexOf('\n');
            if (firstNewline < 0) return s;

            s = s.Substring(firstNewline + 1);

            int closing = s.LastIndexOf("```", System.StringComparison.Ordinal);
            if (closing >= 0) s = s.Substring(0, closing);

            return s.Trim();
        }

        /// <summary>
        /// Takes the outermost {...} from a string that may have prose around it, so a reply like
        /// "Sure! {json}" still parses. Returns the input unchanged when there is no object, which
        /// lets the caller fall through to treating it as plain speech.
        /// </summary>
        private static string ExtractObject(string text)
        {
            int open = text.IndexOf('{');
            int close = text.LastIndexOf('}');

            if (open < 0 || close <= open) return text;
            return text.Substring(open, close - open + 1);
        }

        /// <summary>
        /// Strips the decorations models add to speech. Surrounding quotes are the common one —
        /// we asked for speech, so quoting it is redundant and shows up literally in a bubble.
        /// </summary>
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string s = text.Trim();

            if (s.Length >= 2)
            {
                char first = s[0];
                char last = s[s.Length - 1];
                bool quoted = (first == '"' && last == '"')
                              || (first == '“' && last == '”')
                              || (first == '\'' && last == '\'');

                if (quoted) s = s.Substring(1, s.Length - 2).Trim();
            }

            return s;
        }
    }
}
