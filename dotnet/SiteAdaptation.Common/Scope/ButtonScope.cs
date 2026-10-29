using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.Scope
{
    // R366. One row of the plot table as the scope needs it, so the scope reads no file: the official number, the area key of R256 and
    // whether it is a unit plot. The caller gives the plots inside the border only
    public sealed class ScopePlot
    {
        public ScopePlot(string number, string nbh, bool isUnit)
        {
            Number = number;
            Nbh = nbh;
            IsUnit = isUnit;
        }

        public string Number { get; }
        public string Nbh { get; }
        public bool IsUnit { get; }
    }

    // The plots of a scope in number order, or the stops that keep the button from any change, each naming its token or neighbourhood
    public sealed class ScopeResult
    {
        public ScopeResult(string kind, IReadOnlyList<string> plots, IReadOnlyList<string> stops)
        {
            Kind = kind;
            Stops = stops;
            Plots = stops.Count == 0 ? plots : new string[0];
        }

        public string Kind { get; }
        public IReadOnlyList<string> Plots { get; }
        public IReadOnlyList<string> Stops { get; }

        public bool Ok
        {
            get { return Stops.Count == 0; }
        }
    }

    // R366, docs/design/03a-buttons-shared-and-1.md Scope. Whole project, Neighbourhood by the area key, never an IM letter, or Plot list.
    // Button 1 always works on the whole project, so its scope never stops it
    public static class ButtonScope
    {
        private const int SetUp = 1;

        // A tab parts tokens too, since a row pasted from Excel arrives with tabs
        private static readonly char[] Parts = { ',', ' ', '\t', '\r', '\n' };

        public static ScopeResult Resolve(int button, SettingsScope scope, IEnumerable<ScopePlot> table)
        {
            var units = new SortedSet<string>(NumberOrder.Instance);
            var any = new SortedSet<string>(NumberOrder.Instance);
            var byNbh = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (ScopePlot plot in table)
            {
                string number = WholeNumber(plot.Number);
                if (number == null)
                {
                    continue;
                }
                any.Add(number);
                if (plot.IsUnit)
                {
                    units.Add(number);
                    string nbh = (plot.Nbh ?? "").Trim();
                    if (!byNbh.TryGetValue(nbh, out SortedSet<string> plots))
                    {
                        byNbh[nbh] = plots = new SortedSet<string>(NumberOrder.Instance);
                    }
                    plots.Add(number);
                }
            }

            string kind = KindOf(button, scope);
            if (kind == ScopeKinds.Project)
            {
                var stops = button != SetUp && units.Count == 0 ? new[] { "Whole project: the plot table holds no unit plot" } : new string[0];
                return new ScopeResult(kind, units.ToList(), stops);
            }
            if (kind == ScopeKinds.Nbh)
            {
                string nbh = (scope.Nbh ?? "").Trim();
                if (nbh.Length == 0)
                {
                    return new ScopeResult(kind, new string[0], new[] { "Neighbourhood: no neighbourhood is chosen" });
                }
                return byNbh.TryGetValue(nbh, out SortedSet<string> plots)
                    ? new ScopeResult(kind, plots.ToList(), new string[0])
                    : new ScopeResult(kind, new string[0], new[] { "Neighbourhood " + nbh + " holds no unit plot in the plot table" });
            }
            return List(scope.Plots, units, any);
        }

        // The scope as one text, the same for the same plots however they are typed, so a step compares the scope it ran on with
        // the scope it has now. Ranges stay ranges, the plot table may not be there yet
        public static string Key(int button, SettingsScope scope)
        {
            string kind = KindOf(button, scope);
            if (kind == ScopeKinds.Project)
            {
                return kind;
            }
            if (kind == ScopeKinds.Nbh)
            {
                return kind + ":" + (scope.Nbh ?? "").Trim();
            }
            var tokens = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string token in Tokens(scope.Plots))
            {
                string number = WholeNumber(token);
                string[] ends = token.Split('-');
                string low = ends.Length == 2 ? WholeNumber(ends[0]) : null;
                string high = ends.Length == 2 ? WholeNumber(ends[1]) : null;
                tokens.Add(number ?? (low != null && high != null ? low + "-" + high : token));
            }
            return kind + ":" + string.Join(",", tokens);
        }

        // A whole number of ASCII digits with leading zeros dropped as R81 drops them, else null
        public static string WholeNumber(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Any(letter => letter < '0' || letter > '9'))
            {
                return null;
            }
            string whole = token.TrimStart('0');
            return whole.Length == 0 ? "0" : whole;
        }

        private static string KindOf(int button, SettingsScope scope)
        {
            if (button == SetUp)
            {
                return ScopeKinds.Project;
            }
            string kind = scope == null || scope.Kind == null ? ScopeKinds.Project : scope.Kind;
            if (!ScopeKinds.All.Contains(kind))
            {
                throw new ArgumentException("the scope kind " + kind + " is not " + string.Join(", ", ScopeKinds.All), nameof(scope));
            }
            return kind;
        }

        private static IEnumerable<string> Tokens(string text)
        {
            return (text ?? "").Split(Parts, StringSplitOptions.RemoveEmptyEntries);
        }

        private static ScopeResult List(string text, SortedSet<string> units, SortedSet<string> any)
        {
            var found = new SortedSet<string>(NumberOrder.Instance);
            var stops = new List<string>();
            string[] tokens = Tokens(text).ToArray();
            if (tokens.Length == 0)
            {
                stops.Add("Plot list: the list is empty");
            }
            foreach (string token in tokens)
            {
                string number = WholeNumber(token);
                string[] ends = token.Split('-');
                string low = number ?? (ends.Length == 2 ? WholeNumber(ends[0]) : null);
                string high = number ?? (ends.Length == 2 ? WholeNumber(ends[1]) : null);
                if (low == null || high == null)
                {
                    stops.Add("Plot list: " + token + " is no number and no range");
                    continue;
                }
                if (NumberOrder.Instance.Compare(low, high) > 0)
                {
                    stops.Add("Plot list: " + token + " is a range written high to low");
                    continue;
                }
                List<string> held = any.GetViewBetween(low, high).ToList();
                if (held.Count == 0)
                {
                    stops.Add("Plot list: the plot table holds no plot " + (number == null ? "of " : "") + token);
                    continue;
                }
                List<string> unit = held.Where(units.Contains).ToList();
                if (unit.Count == 0)
                {
                    stops.Add("Plot list: " + token + " holds no unit plot in the plot table");
                    continue;
                }
                found.UnionWith(unit);
            }
            return new ScopeResult(ScopeKinds.Plots, found.ToList(), stops);
        }

        // Whole numbers with no leading zero by value: the shorter is the smaller, then digit by digit, so no number is too long
        private sealed class NumberOrder : IComparer<string>
        {
            public static readonly NumberOrder Instance = new NumberOrder();

            public int Compare(string x, string y)
            {
                int length = x.Length.CompareTo(y.Length);
                return length != 0 ? length : string.CompareOrdinal(x, y);
            }
        }
    }
}
