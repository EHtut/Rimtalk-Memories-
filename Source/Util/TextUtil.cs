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

            // The characters are passed explicitly on purpose. Parameterless TrimEnd() is a
            // .NET Core / .NET Standard 2.1 addition: it compiles happily against our reference
            // assemblies and then throws MissingMethodException on the .NET Framework-era runtime
            // the game actually uses. This overload takes params char[] and has always existed.
            // Do not "simplify" this back.
            return text.Substring(0, cut).TrimEnd(' ', '\t', '\n', '\r') + "…";
        }
    }
}
