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
    // first vertex of each ring, so the coordinates of a site keep their digits (docs/design/05-plan-steps.md)
    public static class FaceStep
    {
        public const string Name = "faces";

        // R348. The baseplan a build reads: plots.json of a CadReader read, baseplanPlots, or the read of the baseplan of the worker,
        // baseplanRead, read_baseplan.json of the command read, one of the two
        public const string BaseplanRoles = "baseplanPlots|baseplanRead";

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

        // Per plot of numbers: face false with the other fields null, or face true with the area, the centroid and the bounds of every
        // ring of its label
        // A ring that is not simple or a label whose rings overlap is refused first, as plan_from_book.py refuses it (FaceRings)
        public static Dictionary<string, Dictionary<string, object>> Build(IEnumerable<string> numbers, JArray plots, IEnumerable<Dictionary<string, string>> plotsCsv, double overlapM2)
        {
            List<string> refused = FaceRings.Refusals(plots, overlapM2);
            if (refused.Count > 0)
            {
                throw new InvalidDataException(refused.Count + " baseplan faces refused: " + string.Join(", ", refused));
            }
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
                found[number] = labelOfNumber.TryGetValue(number, out label) ? Face(rings[label]) : NoFace();
            }
            return found;
        }

        // A plot with no face holds every field of the step with no value, so a field missing on a side is a failure of the parity
        private static Dictionary<string, object> NoFace()
        {
            return new Dictionary<string, object> { ["face"] = false, ["area"] = null, ["centroid"] = null, ["boundsMin"] = null, ["boundsMax"] = null };
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

    // R345. The plots of a scope (docs/design/03a-buttons-shared-and-1.md): the whole project is every client code label of the location
    // plan, a neighbourhood those of its NBH, a plot list its numbers, each once, in the order of link_summary.sort_key
    public static class PlotScope
    {
        private static readonly Regex Number = new Regex(@"^-?\d+(\.\d+)?$");

        public const string LabelsRole = "labels";

        // R345. A plot list needs no labels.csv, as plan_from_book.py never reads --labels for a cover file and needs it for all and
        // nbh=<NBH> (H1 of the fifth review of PR 55)
        public static bool NeedsLabels(WorkerScope scope)
        {
            return scope?.Kind != "plots";
        }

        // R345. The plots of the scope of a request, labels.csv read with the columns of its scope when the scope needs it
        public static List<string> Of(WorkerContext run)
        {
            WorkerScope scope = run.Request.Scope;
            return Numbers(scope, NeedsLabels(scope) ? CsvRows.Read(run.Input(LabelsRole), CsvRows.LabelsOf(scope)) : new List<Dictionary<string, string>>());
        }

        // A scope that numbers no plot is refused, as plan_from_book.scope_plots refuses it, so a build or a read of no plot never reads as
        // a run done (H1 of the fifth review of PR 55)
        public static List<string> Numbers(WorkerScope scope, IEnumerable<Dictionary<string, string>> labels)
        {
            List<string> numbers = Ordered(scope, labels);
            if (numbers.Count == 0)
            {
                throw new InvalidDataException("the scope " + StepFile.ScopeText(scope) + " numbers no plot, a build or a read of no plot is refused (R345)");
            }
            return numbers;
        }

        private static List<string> Ordered(WorkerScope scope, IEnumerable<Dictionary<string, string>> labels)
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

        // A client code row with no number is refused with its place among the rows read, as plan_from_book.nbh_plots refuses it, every
        // row before the scope is taken (H1 of the fifth review of PR 55)
        private static List<string> ClientCodes(IEnumerable<Dictionary<string, string>> labels, string nbh)
        {
            var found = new List<string>();
            int index = 0;
            foreach (Dictionary<string, string> row in labels)
            {
                index++;
                string kind, number, area;
                row.TryGetValue("kind", out kind);
                row.TryGetValue("number", out number);
                row.TryGetValue("nbh", out area);
                if (kind == "clientCode" && string.IsNullOrEmpty(number))
                {
                    throw new InvalidDataException("a row of kind clientCode of the labels holds no number, row " + index + " of the rows read (R345)");
                }
                if (kind == "clientCode" && (nbh == null || area == nbh))
                {
                    found.Add(number);
                }
            }
            return found;
        }
    }
}
