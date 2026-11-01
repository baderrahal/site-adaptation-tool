using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R404, Q130. The largest distance from any point of one closed outline to the other outline, taken both ways (Hausdorff). The
    // farthest point can lie inside an edge, so every edge is sampled at the step of config besides its corners
    public static class OutlineDistance
    {
        public static double HausdorffMm(IReadOnlyList<double[]> a, IReadOnlyList<double[]> b, double stepMm)
        {
            return Math.Max(DirectedMm(a, b, stepMm), DirectedMm(b, a, stepMm));
        }

        public static double DirectedMm(IReadOnlyList<double[]> from, IReadOnlyList<double[]> to, double stepMm)
        {
            double step = stepMm / 1000.0, largest = 0;
            for (int edge = 0; edge < from.Count; edge++)
            {
                double[] start = from[edge], end = from[(edge + 1) % from.Count];
                double length = Math.Sqrt(Square(end[0] - start[0]) + Square(end[1] - start[1]));
                int parts = Math.Max(1, (int)Math.Ceiling(length / step));
                for (int part = 0; part < parts; part++)
                {
                    double t = (double)part / parts;
                    largest = Math.Max(largest, ToOutline(start[0] + t * (end[0] - start[0]), start[1] + t * (end[1] - start[1]), to));
                }
            }
            return largest * 1000.0;
        }

        private static double ToOutline(double x, double y, IReadOnlyList<double[]> outline)
        {
            double nearest = double.MaxValue;
            for (int edge = 0; edge < outline.Count; edge++)
            {
                double[] a = outline[edge], b = outline[(edge + 1) % outline.Count];
                double dx = b[0] - a[0], dy = b[1] - a[1], length = dx * dx + dy * dy;
                double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((x - a[0]) * dx + (y - a[1]) * dy) / length));
                nearest = Math.Min(nearest, Math.Sqrt(Square(x - a[0] - t * dx) + Square(y - a[1] - t * dy)));
            }
            return nearest;
        }

        private static double Square(double value)
        {
            return value * value;
        }
    }

    // R404, open point 3. One row of footprint_<NBH>.json, which button 7 writes beside the check rows of button 4. The run never stops
    // on a flag, the run record counts the flagged plots and its issues name each
    public sealed class FootprintRow
    {
        public string Plot { get; set; }

        // R453. The prototype file name of the villa, so the largest gap is told per prototype
        public string Prototype { get; set; }

        // Null when no villa block holds the base, which is flagged too
        public double? DistanceMm { get; set; }
        public double LimitMm { get; set; }
        public bool Flagged { get; set; }
        public string Why { get; set; }
    }

    public sealed class FootprintSummary
    {
        public string Prototype { get; set; }
        public int Plots { get; set; }
        public double? LargestGapMm { get; set; }
        public int OverLimit { get; set; }
    }

    public static class FootprintCheck
    {
        // The footprint of a plot is the villa block of R300 whose outline holds the middle of the placed base, CHOSEN since a plot row
        // names no block and the blocks are listed by block name only. Where two hold it the nearer middle wins
        public static FootprintRow Row(string plot, IReadOnlyList<double[]> placedBase, IEnumerable<double[][]> blocks, LightModeConfig config, string prototype = null)
        {
            double[] middle = Middle(placedBase);
            double[][] block = blocks.Where(outline => outline.Length >= 3 && Holds(outline, middle))
                .OrderBy(outline => Distance(Middle(outline), middle)).FirstOrDefault();
            if (block == null)
            {
                return new FootprintRow { Plot = plot, Prototype = prototype, LimitMm = config.FootprintLimitMm, Flagged = true, Why = "no villa block or house outline of the footprint holds the middle of the base" };
            }
            double distance = JsonOut.Round3(OutlineDistance.HausdorffMm(placedBase, block, config.FootprintSampleStepMm));
            bool over = distance > config.FootprintLimitMm;
            return new FootprintRow
            {
                Plot = plot, Prototype = prototype, DistanceMm = distance, LimitMm = config.FootprintLimitMm, Flagged = over,
                Why = over ? "the base lies up to " + distance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " mm from the footprint, over the limit" : null,
            };
        }

        // Every outline of the footprint read of button 2 (read_villaBlocks.json), by SiteClusters.BlocksByName as the build reads it
        public static List<double[][]> FromRead(JObject villaBlocksRead)
        {
            return SiteClusters.BlocksByName(villaBlocksRead?["blocks"] as JArray ?? new JArray()).SelectMany(group => group.Value).ToList();
        }

        // R453, Q276. The house outlines of each footprint block, the rings the read joined on houseLayer of at least houseMinAreaM2, in
        // place of the block outline, which holds the whole plot envelope. A config with no houseLayer keeps the block outlines of R404
        public static List<double[][]> FromRead(JObject villaBlocksRead, LightModeConfig config)
        {
            if (config?.HouseLayer == null)
            {
                return FromRead(villaBlocksRead);
            }
            var houses = new List<double[][]>();
            foreach (JToken block in villaBlocksRead?["blocks"] as JArray ?? new JArray())
            {
                foreach (JToken ring in block["rings"] as JArray ?? new JArray())
                {
                    if (!string.Equals((string)ring["layer"], config.HouseLayer, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    List<double[]> points = FaceRings.Clean((ring["points"] as JArray ?? new JArray()).Select(point => new[] { (double)point[0], (double)point[1] }));
                    if (points.Count >= 3 && Math.Abs(WallRing.Area(points)) >= config.HouseMinAreaM2.Value)
                    {
                        houses.Add(points.ToArray());
                    }
                }
            }
            return houses;
        }

        // R453. Per prototype the plots measured, the largest gap and the count over the limit, in the order of the file name
        public static List<FootprintSummary> Summary(IEnumerable<FootprintRow> rows)
        {
            return rows.GroupBy(row => row.Prototype ?? "", StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new FootprintSummary
            {
                Prototype = group.Key, Plots = group.Count(), LargestGapMm = group.Max(row => row.DistanceMm), OverLimit = group.Count(row => row.Flagged),
            }).ToList();
        }

        public static IEnumerable<string> SummaryLines(IEnumerable<FootprintRow> rows)
        {
            return Summary(rows).Select(item => "prototype " + item.Prototype + ": largest gap to the house outline "
                + (item.LargestGapMm.HasValue ? item.LargestGapMm.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " mm" : "not measured")
                + ", " + item.OverLimit + " of " + item.Plots + " plots over the limit (R453)");
        }

        // masses holds the base of each mass file by its prototype, so a base taken from the slab is a row of its own (R451, Q279)
        public static JObject Json(IEnumerable<FootprintRow> rows, IEnumerable<KeyValuePair<string, MassBase>> masses = null)
        {
            List<FootprintRow> all = rows.ToList();
            return new JObject
            {
                ["format"] = "site-adaptation-footprint",
                ["rows"] = new JArray(all.Select(row => new JObject
                {
                    ["plot"] = row.Plot, ["prototype"] = row.Prototype, ["distanceMm"] = row.DistanceMm, ["limitMm"] = row.LimitMm, ["flagged"] = row.Flagged, ["why"] = row.Why,
                })),
                ["prototypes"] = new JArray(Summary(all).Select(item => new JObject
                {
                    ["prototype"] = item.Prototype, ["plots"] = item.Plots, ["largestGapMm"] = item.LargestGapMm, ["overLimit"] = item.OverLimit,
                })),
                ["masses"] = new JArray((masses ?? Enumerable.Empty<KeyValuePair<string, MassBase>>()).Where(item => item.Value != null).OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new JObject
                    {
                        ["prototype"] = System.IO.Path.GetFileName(item.Key), ["source"] = item.Value.Source, ["flagged"] = item.Value.Flagged, ["reason"] = item.Value.Reason,
                    })),
            };
        }

        private static double[] Middle(IReadOnlyList<double[]> ring)
        {
            return new[] { ring.Average(point => point[0]), ring.Average(point => point[1]) };
        }

        private static double Distance(double[] a, double[] b)
        {
            return Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));
        }

        private static bool Holds(IReadOnlyList<double[]> ring, double[] point)
        {
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                if ((ring[i][1] > point[1]) != (ring[j][1] > point[1])
                    && point[0] < (ring[j][0] - ring[i][0]) * (point[1] - ring[i][1]) / (ring[j][1] - ring[i][1]) + ring[i][0])
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
