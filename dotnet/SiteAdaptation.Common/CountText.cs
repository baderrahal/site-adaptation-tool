using System.Globalization;

namespace SiteAdaptation.Common
{
    // R495. A count with its noun in the number it needs, so a window never reads 1 plots. Texts that carry a count call this and the
    // docs/design/14-ui-labels.md templates keep the plural form for the count of zero or many
    public static class CountText
    {
        // thousands parts the count by commas as the Door level row writes it
        public static string Plots(int count, bool thousands = false)
        {
            string number = count.ToString(thousands ? "N0" : "G", CultureInfo.InvariantCulture);
            return number + (count == 1 ? " plot" : " plots");
        }

        // "1 plot follows every rule" and "2 plots follow every rule"
        public static string FollowEveryRule(int count)
        {
            return Plots(count) + (count == 1 ? " follows every rule" : " follow every rule");
        }
    }
}
