namespace Arkh.Util
{
    public static class TextUtil
    {
        /// <summary>
        /// Trims text to a character limit at a word boundary, so the model is never handed a
        /// sentence cut mid-word.
        ///
        /// Falls back to a hard cut when the last space sits implausibly early — otherwise a
        /// long unbroken run (a pasted URL, or any CJK text, which has no spaces at all) would
        /// collapse the whole string to almost nothing.
        /// </summary>
        public static string Clamp(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (maxChars <= 0) return "";
            if (text.Length <= maxChars) return text;

            int cut = text.LastIndexOf(' ', maxChars - 1);
            if (cut < maxChars / 2) cut = maxChars - 1;

            // Four characters passed explicitly, on purpose.
            //
            // Two of String's Trim overloads are .NET Core additions that compile against our
            // reference assemblies and then throw MissingMethodException on the runtime the game
            // actually uses: the parameterless TrimEnd(), and the single-char TrimEnd(char).
            // Passing two or more chars binds to params char[], which has always existed.
            //
            // Both have already bitten this project once each. Do not "simplify" this back.
            return text.Substring(0, cut).TrimEnd(' ', '\t', '\n', '\r') + "…";
        }
    }
}
