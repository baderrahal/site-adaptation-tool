using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SiteAdaptation.Common.CheckTable
{
    // R495. The sort of the check table: a click on a header sorts that column up, a second click on it reverses, a click on another
    // column starts up again. Immutable, and a sort only orders the rows it is given, so it drops none
    public sealed class CheckSort
    {
        public const string Up = "up";
        public const string Down = "down";

        public static readonly CheckSort None = new CheckSort(null, false);

        private CheckSort(string column, bool descending)
        {
            Column = column;
            Descending = descending;
        }

        // Null while no header was clicked, the rows then stay in the order of the table
        public string Column { get; }

        public bool Descending { get; }

        // Up or Down for the sorted column, else an empty text, which the header shows as no arrow
        public string MarkOf(string column)
        {
            return column != Column ? "" : Descending ? Down : Up;
        }

        public CheckSort Click(string column)
        {
            return column == Column ? new CheckSort(column, !Descending) : new CheckSort(column, false);
        }

        // Equal keys keep the order they had, up or down, so a second click reverses the sort and no more
        public IReadOnlyList<T> Apply<T>(IEnumerable<T> rows, Func<T, string> key)
        {
            if (Column == null)
            {
                return rows.ToList();
            }
            var comparer = Comparer<string>.Create(Compare);
            return (Descending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer)).ToList();
        }

        // No text first, then numbers by value, then words with the case ignored. A plot 98 sits before 100 as a person counts
        public static int Compare(string left, string right)
        {
            bool leftNumber = TryNumber(left, out double leftValue);
            bool rightNumber = TryNumber(right, out double rightValue);
            int leftRank = string.IsNullOrEmpty(left) ? 0 : leftNumber ? 1 : 2;
            int rightRank = string.IsNullOrEmpty(right) ? 0 : rightNumber ? 1 : 2;
            if (leftRank != rightRank)
            {
                return leftRank.CompareTo(rightRank);
            }
            return leftRank == 1 ? leftValue.CompareTo(rightValue) : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryNumber(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    // R495. The nine columns of the check table by the key a header click and a mark go by. The same keys name the bindings Marks[plot] of the window
    public static class CheckColumn
    {
        public const string Plot = "plot";
        public const string Nbh = "nbh";
        public const string Unit = "unit";
        public const string Check = "check";
        public const string Tool = "tool";
        public const string Past = "past";
        public const string Rule = "rule";
        public const string Manual = "manual";
        public const string Use = "use";

        public static readonly IReadOnlyList<string> All = new[] { Plot, Nbh, Unit, Check, Tool, Past, Rule, Manual, Use };
    }
}
