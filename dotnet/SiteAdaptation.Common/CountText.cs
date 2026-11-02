using System.Globalization;

namespace SiteAdaptation.Common
{
    // R495. A count with its noun in the number it needs, so a window never reads 1 plots. Texts that carry a count call this and the
    // docs/design/14-ui-labels.md templates keep the plural form for the count of zero or many
    public static class CountText
    {
        // Parted by commas as the other lines of the window write a count
        public static string Plots(int count)
        {
            return count.ToString("N0", CultureInfo.InvariantCulture) + (count == 1 ? " plot" : " plots");
        }

        public static string Overrides(string text)
        {
            return Singular(text, "override");
        }

        public static string Issues(string text)
        {
            return Singular(text, "issue");
        }

        // A text filled from a template of the plural, so a template stays one label. 11 and 2,001 keep theirs
        public static string Singular(string text, string noun)
        {
            return System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\d,])1 " + noun + "s\\b", "1 " + noun);
        }

        // "1 plot follows every rule" and "2 plots follow every rule"
        public static string FollowEveryRule(int count)
        {
            return Plots(count) + (count == 1 ? " follows every rule" : " follow every rule");
        }
    }
}
