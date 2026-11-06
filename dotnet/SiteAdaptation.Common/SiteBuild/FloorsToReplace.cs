using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SiteBuild
{
    // R648, Q439. Which floors the tool placed earlier have an outline that is no longer the outline of the plot, so button 5 replaces them and does not stop on
    // them. The floors come in the optional file placedFloors of the request: {"floors": [{"plot": "101", "outline": [[x, y], ...]}]}, the floors the tool placed
    // and no others, so a floor a person drew is never in the list. An empty model gives an empty list
    internal static class FloorsToReplace
    {
        public static JObject Check(IReadOnlyList<PlanPlot> plots, JObject placed, double sameMm, List<PlanIssue> issues)
        {
            var replace = new JArray();
            int count = 0;
            if (placed == null)
            {
                return new JObject { ["checked"] = 0, ["replace"] = replace, ["line"] = PlanTexts.NoPlacedFloors };
            }
            if (!(placed["floors"] is JArray))
            {
                issues.Add(new PlanIssue { Line = PlanTexts.FileUnderstood("placedFloors", "list of floors") });
                return new JObject { ["checked"] = 0, ["replace"] = replace, ["line"] = PlanTexts.NoPlacedFloors };
            }
            Dictionary<string, PlanPlot> byNumber = plots.ToDictionary(plot => plot.Number, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject floor in ((JArray)placed["floors"]).OfType<JObject>())
            {
                string number = floor["plot"]?.Type == JTokenType.String ? ((string)floor["plot"]).Trim() : null;
                PlanPlot plot;
                if (number == null || !byNumber.TryGetValue(number, out plot))
                {
                    issues.Add(new PlanIssue { Plot = number, Line = PlanTexts.FloorWithoutPlot(number ?? "?") });
                    continue;
                }
                if (!seen.Add(number))
                {
                    issues.Add(new PlanIssue { Plot = number, Line = PlanTexts.ItemLeftOut("placedFloors", "a second floor of plot " + number) });
                    continue;
                }
                if (plot.In.Outline == null)
                {
                    // Nothing to replace the floor with, the plot is already marked for its missing outline
                    issues.Add(new PlanIssue { Plot = number, Line = PlanTexts.ItemLeftOut("placedFloors", "the floor of plot " + number + ", which has no outline now,") });
                    continue;
                }
                count++;
                List<double[]> outline = Ring(floor["outline"]);
                if (outline == null || !SameOutline(outline, plot.In.Outline, sameMm / 1000.0))
                {
                    replace.Add(number);
                    issues.Add(new PlanIssue { Plot = number, Line = PlanTexts.FloorChanged(number) });
                }
            }
            return new JObject { ["checked"] = count, ["replace"] = replace, ["line"] = PlanTexts.FloorsLine(count, replace.Count) };
        }

        // Same corner points whatever the start point, the direction and a repeated closing point, each within tolM of one in the other
        public static bool SameOutline(IList<double[]> one, IList<double[]> other, double tolM)
        {
            List<double[]> first = Open(one);
            List<double[]> second = Open(other);
            return first.All(point => Near(second, point, tolM)) && second.All(point => Near(first, point, tolM));
        }

        private static List<double[]> Open(IList<double[]> ring)
        {
            var points = ring.ToList();
            if (points.Count > 1 && points[0][0] == points[points.Count - 1][0] && points[0][1] == points[points.Count - 1][1])
            {
                points.RemoveAt(points.Count - 1);
            }
            return points;
        }

        private static bool Near(List<double[]> ring, double[] point, double tolM)
        {
            return ring.Any(other => Math.Abs(other[0] - point[0]) <= tolM && Math.Abs(other[1] - point[1]) <= tolM);
        }

        private static List<double[]> Ring(JToken token)
        {
            var ring = token as JArray;
            if (ring == null)
            {
                return null;
            }
            var points = new List<double[]>();
            foreach (JToken item in ring)
            {
                var pair = item as JArray;
                if (pair == null || pair.Count < 2 || (pair[0].Type != JTokenType.Float && pair[0].Type != JTokenType.Integer) || (pair[1].Type != JTokenType.Float && pair[1].Type != JTokenType.Integer))
                {
                    return null;
                }
                points.Add(new[] { (double)pair[0], (double)pair[1] });
            }
            return points.Count >= 3 ? points : null;
        }
    }
}
