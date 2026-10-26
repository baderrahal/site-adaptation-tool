using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R345. The step faces of the parity harness, the C# of plan_from_book.face_step: the baseplan face of each plot as the build
    // takes it from the plot shapes of plots.json of the baseplan reader (yard_rule.face_shapes) and the number plots.csv gives
    // its label (plan_from_book.book_rows), with its area, centroid and bounds. The area and the centroid are taken about the
    // first vertex of each ring, so the coordinates of a site keep their digits (docs/addin-design.md section 5)
    public static class FaceStep
    {
        public const string Name = "faces";

        // A label is a whole number of ASCII digits after the blanks around it, written with no leading zero, as
        // yard_rule.whole_label reads it, else null
        public static string WholeLabel(string text)
        {
            string trimmed = (text ?? "").Trim();
            if (trimmed.Length == 0 || trimmed.Any(letter => letter < '0' || letter > '9'))
            {
                return null;
            }
            string whole = trimmed.TrimStart('0');
            return whole.Length == 0 ? "0" : whole;
        }

        // The number of each baseplan label by the rows of plots.csv with a location plan number, a later row over an earlier one
        public static Dictionary<string, string> NumberOfLabel(IEnumerable<Dictionary<string, string>> rows)
        {
            var found = new Dictionary<string, string>();
            foreach (Dictionary<string, string> row in rows)
            {
                string number, label;
                if (row.TryGetValue("locationPlanNumber", out number) && !string.IsNullOrEmpty(number) && row.TryGetValue("baseplanLabel", out label) && !string.IsNullOrEmpty(label))
                {
                    found[label] = number;
                }
            }
            return found;
        }

        // Per plot of numbers: face false, or face true with the area, the centroid and the bounds of every ring of its label
        public static Dictionary<string, Dictionary<string, object>> Build(IEnumerable<string> numbers, JArray plots, IEnumerable<Dictionary<string, string>> plotsCsv)
        {
            var order = new List<string>();
            var rings = new Dictionary<string, List<double[][]>>();
            foreach (JToken entry in plots)
            {
                if ((string)entry["kind"] != "plot_shape")
                {
                    continue;
                }
                JToken plot = entry["plot"];
                string label = WholeLabel(plot == null || plot.Type == JTokenType.Null ? null : plot.ToString());
                double[][] ring = (entry["vertices"] as JArray ?? new JArray()).Select(vertex => new[] { (double)vertex[0], (double)vertex[1] }).ToArray();
                // The build drops a label that is no whole number, a ring of under three vertices and one with no area
                if (label == null || ring.Length < 3 || Ring(ring).Area == 0)
                {
                    continue;
                }
                if (!rings.ContainsKey(label))
                {
                    rings[label] = new List<double[][]>();
                    order.Add(label);
                }
                rings[label].Add(ring);
            }
            Dictionary<string, string> numberOf = NumberOfLabel(plotsCsv);
            var labelOfNumber = new Dictionary<string, string>();
            foreach (string label in order)
            {
                string number;
                if (numberOf.TryGetValue(label, out number))
                {
                    labelOfNumber[number] = label;
                }
            }
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                string label;
                found[number] = labelOfNumber.TryGetValue(number, out label) ? Face(rings[label]) : new Dictionary<string, object> { ["face"] = false };
            }
            return found;
        }

        private static Dictionary<string, object> Face(List<double[][]> rings)
        {
            double area = 0, x = 0, y = 0;
            double[] low = { double.MaxValue, double.MaxValue };
            double[] high = { double.MinValue, double.MinValue };
            foreach (double[][] ring in rings)
            {
                RingValues values = Ring(ring);
                area += values.Area;
                x += values.Area * values.X;
                y += values.Area * values.Y;
                foreach (double[] vertex in ring)
                {
                    low[0] = Math.Min(low[0], vertex[0]);
                    low[1] = Math.Min(low[1], vertex[1]);
                    high[0] = Math.Max(high[0], vertex[0]);
                    high[1] = Math.Max(high[1], vertex[1]);
                }
            }
            return new Dictionary<string, object> { ["face"] = true, ["area"] = area, ["centroid"] = new[] { x / area, y / area }, ["boundsMin"] = low, ["boundsMax"] = high };
        }

        public struct RingValues
        {
            public double Area;
            public double X;
            public double Y;
        }

        // The area, never negative, and the centroid of a ring, the shoelace sums taken about its first vertex
        public static RingValues Ring(IReadOnlyList<double[]> ring)
        {
            double originX = ring[0][0], originY = ring[0][1];
            double twice = 0, sumX = 0, sumY = 0;
            for (int index = 0; index < ring.Count; index++)
            {
                double ax = ring[index][0] - originX, ay = ring[index][1] - originY;
                double bx = ring[(index + 1) % ring.Count][0] - originX, by = ring[(index + 1) % ring.Count][1] - originY;
                double cross = ax * by - bx * ay;
                twice += cross;
                sumX += (ax + bx) * cross;
                sumY += (ay + by) * cross;
            }
            if (twice == 0)
            {
                return new RingValues { Area = 0, X = originX, Y = originY };
            }
            return new RingValues { Area = Math.Abs(twice) / 2, X = originX + sumX / (3 * twice), Y = originY + sumY / (3 * twice) };
        }
    }

    // R345. The plots of a scope (docs/addin-design.md section 3): the whole project is every client code label of the location
    // plan, a neighbourhood those of its NBH, a plot list its numbers, each once, in the order of link_summary.sort_key
    public static class PlotScope
    {
        private static readonly Regex Number = new Regex(@"^-?\d+(\.\d+)?$");

        public static List<string> Numbers(WorkerScope scope, IEnumerable<Dictionary<string, string>> labels)
        {
            IEnumerable<string> found;
            switch (scope?.Kind)
            {
                case "project":
                    found = ClientCodes(labels, null);
                    break;
                case "nbh":
                    found = ClientCodes(labels, scope.Nbh ?? "");
                    break;
                case "plots":
                    found = scope.Plots ?? new List<string>();
                    break;
                default:
                    throw new InvalidDataException("the scope " + scope?.Kind + " is not project, nbh or plots");
            }
            return found.Distinct().OrderBy(text => Number.IsMatch(text) ? 0 : 1).ThenBy(text => Number.IsMatch(text) ? double.Parse(text, System.Globalization.CultureInfo.InvariantCulture) : 0)
                .ThenBy(text => text, StringComparer.Ordinal).ToList();
        }

        private static IEnumerable<string> ClientCodes(IEnumerable<Dictionary<string, string>> labels, string nbh)
        {
            foreach (Dictionary<string, string> row in labels)
            {
                string kind, number, area;
                row.TryGetValue("kind", out kind);
                row.TryGetValue("number", out number);
                row.TryGetValue("nbh", out area);
                if (kind == "clientCode" && number != null && (nbh == null || (area ?? "") == nbh))
                {
                    yield return number;
                }
            }
        }
    }
}
