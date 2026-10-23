using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SiteAdaptation.Common
{
    public static class ErrorLines
    {
        // The most element ids one line names. The count in the line is always the full count
        public const int MaxIds = 5;

        // One line for every element that failed the same way, so a failure on a thousand floors is one line and not a thousand
        public static string Grouped(string label, string detail, IList<long?> ids)
        {
            string shown = string.Join(", ", ids.Take(MaxIds).Select(id => id.HasValue ? id.Value.ToString(CultureInfo.InvariantCulture) : "?"));
            string lead = ids.Count > MaxIds ? "first " + MaxIds + " element ids " : "element ids ";
            return label + ": " + detail + " on " + ids.Count + (ids.Count == 1 ? " element" : " elements") + ", " + lead + shown;
        }
    }
}
