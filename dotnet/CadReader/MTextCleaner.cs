using System.Text.RegularExpressions;

namespace CadReader
{
    // ACadSharp PlainText leaves the underline and overline toggles in as stray letters, so labels like LOT 17 came out as LLOT 17l
    public static class MTextCleaner
    {
        private static readonly Regex StackedFraction = new Regex(@"\\S([^;]*);", RegexOptions.Compiled);
        private static readonly Regex ValueCode = new Regex(@"\\[fFHWQTACcp][^;]*;", RegexOptions.Compiled);
        private static readonly Regex Toggle = new Regex(@"\\[LlOoKk]", RegexOptions.Compiled);

        public static string Plain(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }
            string text = raw.Replace("\\\\", "\u0001").Replace("\\{", "\u0002").Replace("\\}", "\u0003");
            text = StackedFraction.Replace(text, match => match.Groups[1].Value.Replace('^', '/').Replace('#', '/'));
            text = ValueCode.Replace(text, "");
            text = Toggle.Replace(text, "");
            text = text.Replace("\\P", "\n").Replace("\\~", " ");
            text = text.Replace("{", "").Replace("}", "");
            text = text.Replace("\u0001", "\\").Replace("\u0002", "{").Replace("\u0003", "}");
            return text.Replace("\r\n", "\n").Trim();
        }
    }
}
