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

        // Null when no villa block holds the base, which is flagged too
        public double? DistanceMm { get; set; }
        public double LimitMm { get; set; }
        public bool Flagged { get; set; }
        public string Why { get; set; }
    }

    public static class FootprintCheck
    {
        // The footprint of a plot is the villa block of R300 whose outline holds the middle of the placed base, CHOSEN since a plot row
        // names no block and the blocks are listed by block name only. Where two hold it the nearer middle wins
        public static FootprintRow Row(string plot, IReadOnlyList<double[]> placedBase, IEnumerable<double[][]> blocks, LightModeConfig config)
        {
            double[] middle = Middle(placedBase);
            double[][] block = blocks.Where(outline => outline.Length >= 3 && Holds(outline, middle))
                .OrderBy(outline => Distance(Middle(outline), middle)).FirstOrDefault();
            if (block == null)
            {
                return new FootprintRow { Plot = plot, LimitMm = config.FootprintLimitMm, Flagged = true, Why = "no villa block of the footprint holds the middle of the base" };
            }
            double distance = JsonOut.Round3(OutlineDistance.HausdorffMm(placedBase, block, config.FootprintSampleStepMm));
            bool over = distance > config.FootprintLimitMm;
            return new FootprintRow
            {
                Plot = plot, DistanceMm = distance, LimitMm = config.FootprintLimitMm, Flagged = over,
                Why = over ? "the base lies up to " + distance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " mm from the footprint, over the limit" : null,
            };
        }

        // Every outline of the footprint read of button 2 (read_villaBlocks.json), by SiteClusters.BlocksByName as the build reads it
        public static List<double[][]> FromRead(JObject villaBlocksRead)
        {
            return SiteClusters.BlocksByName(villaBlocksRead?["blocks"] as JArray ?? new JArray()).SelectMany(group => group.Value).ToList();
        }

        public static JObject Json(IEnumerable<FootprintRow> rows)
        {
            return new JObject
            {
                ["format"] = "site-adaptation-footprint",
                ["rows"] = new JArray(rows.Select(row => new JObject
                {
                    ["plot"] = row.Plot, ["distanceMm"] = row.DistanceMm, ["limitMm"] = row.LimitMm, ["flagged"] = row.Flagged, ["why"] = row.Why,
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
