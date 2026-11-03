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

        // R477, Q277. The block round the base when it is of another type than the unit of the plot, which is told apart and not flagged
        public string OtherType { get; set; }
    }

    // R453. The house outlines of a footprint read, with the line that tells why there are none when the read came before R453 and holds
    // no rings, so the run says it once and flags no plot for it
    public sealed class FootprintOutlines : List<double[][]>
    {
        public const string NoRings = "the footprint read has no house outlines, run 2 Read data again";

        // R477. What a row says for a block the read gives no name, the name of no block a unit can match
        public const string NoBlockName = "no block name";

        private readonly Dictionary<double[][], string> blockNames = new Dictionary<double[][], string>();

        public string Missing { get; set; }

        public void Add(double[][] outline, string block)
        {
            Add(outline);
            blockNames[outline] = block;
        }

        // The name of the block an outline was drawn in, each outline being one array of the read, null for an outline added with none
        public string BlockOf(double[][] outline)
        {
            return blockNames.TryGetValue(outline, out string block) ? block : null;
        }
    }

    public sealed class FootprintSummary
    {
        public string Prototype { get; set; }
        public int Plots { get; set; }

        // R477. The kinds of the plots, the gap and the count over the limit being of those of the same type only
        public int SameType { get; set; }
        public int OtherType { get; set; }
        public int NoOutline { get; set; }
        public double? LargestGapMm { get; set; }
        public int OverLimit { get; set; }
    }

    public static class FootprintCheck
    {
        // The footprint of a plot is the villa block of R300 whose outline holds the middle of the placed base, CHOSEN since a plot row
        // names no block and the blocks are listed by block name only. Where two hold it the nearer middle wins. R477, Q277. Only a block
        // of the type of the unit is held to the limit, the master plan drew another house in a block of another type. An outline with no
        // block name, which no read gives, has no type to tell and is held as in R404. R479. The type of the plot is the unit of the file it links, TH1-IM a TH1 file, and
        // not keo_file of the rules, which gives DP4 for a VD4 that links a VD4 file. A plot with no prototype, or a file name of no unit, goes by its own unit
        public static FootprintRow Row(string plot, IReadOnlyList<double[]> placedBase, IEnumerable<double[][]> blocks, LightModeConfig config, string prototype = null, string unit = null)
        {
            // The centre of the area, a mean of the corners moves with their count while the house stays put
            double[] middle = RingMath.Centroid(placedBase);
            double[][] block = blocks.Where(outline => outline.Length >= 3 && RingMath.Holds(outline, middle))
                .OrderBy(outline => Distance(Middle(outline), middle)).FirstOrDefault();
            if (block == null)
            {
                return new FootprintRow { Plot = plot, Prototype = prototype, LimitMm = config.FootprintLimitMm, Flagged = true, Why = "no villa block or house outline of the footprint holds the middle of the base" };
            }
            double distance = JsonOut.Round3(OutlineDistance.HausdorffMm(placedBase, block, config.FootprintSampleStepMm));
            string name = (blocks as FootprintOutlines)?.BlockOf(block);
            if (name != null && !SameType(name, Hands.UnitOfFile(prototype) ?? unit))
            {
                return new FootprintRow
                {
                    Plot = plot, Prototype = prototype, DistanceMm = distance, LimitMm = config.FootprintLimitMm, OtherType = name,
                    Why = "the block " + name + " round the base is another type than the unit " + (unit ?? "(none in the plan)") + ", not held to the limit (Q277)",
                };
            }
            bool over = distance > config.FootprintLimitMm;
            return new FootprintRow
            {
                Plot = plot, Prototype = prototype, DistanceMm = distance, LimitMm = config.FootprintLimitMm, Flagged = over,
                Why = over ? "the base lies up to " + distance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " mm from the footprint, over the limit" : null,
            };
        }

        // R477. The type of a block is the part of its name after the last underscore, so a name with none is of no type
        private static bool SameType(string block, string type)
        {
            int underscore = block.LastIndexOf('_');
            return underscore >= 0 && string.Equals(block.Substring(underscore + 1), type, StringComparison.OrdinalIgnoreCase);
        }

        // Every outline of the footprint read of button 2 (read_villaBlocks.json), by SiteClusters.BlocksByName as the build reads it
        public static List<double[][]> FromRead(JObject villaBlocksRead)
        {
            var outlines = new FootprintOutlines();
            foreach (KeyValuePair<string, List<double[][]>> group in SiteClusters.BlocksByName(villaBlocksRead?["blocks"] as JArray ?? new JArray()))
            {
                foreach (double[][] outline in group.Value)
                {
                    outlines.Add(outline, group.Key == SiteClusters.NoName ? FootprintOutlines.NoBlockName : group.Key);
                }
            }
            return outlines;
        }

        // R453, Q276. The house outlines of each footprint block, the rings the read joined on houseLayer of at least houseMinAreaM2, in
        // place of the block outline, which holds the whole plot envelope. A config with no houseLayer keeps the block outlines of R404
        public static List<double[][]> FromRead(JObject villaBlocksRead, LightModeConfig config)
        {
            if (config?.HouseLayer == null)
            {
                return FromRead(villaBlocksRead);
            }
            var houses = new FootprintOutlines();
            JArray blocks = villaBlocksRead?["blocks"] as JArray ?? new JArray();
            if (blocks.Count > 0 && blocks.All(block => block["rings"] == null))
            {
                houses.Missing = FootprintOutlines.NoRings;
                return houses;
            }
            foreach (JToken block in blocks)
            {
                foreach (JToken ring in block["rings"] as JArray ?? new JArray())
                {
                    if (!string.Equals((string)ring["layer"], config.HouseLayer, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    List<double[]> points = FaceRings.Clean((ring["points"] as JArray ?? new JArray()).Select(point => new[] { (double)point[0], (double)point[1] }));
                    if (points.Count >= 3 && Math.Abs(RingMath.Area(points)) >= config.HouseMinAreaM2.Value)
                    {
                        houses.Add(points.ToArray(), (string)block["name"] ?? FootprintOutlines.NoBlockName);
                    }
                }
            }
            return houses;
        }

        // R477. A row held to the limit, its block of the type of the unit
        private static bool Held(FootprintRow row)
        {
            return row.DistanceMm.HasValue && row.OtherType == null;
        }

        // R453, R477. Per prototype the plots, those of each kind, and of those of the same type the largest gap and the count over the
        // limit, in the order of the file name. The gap to another type says nothing of the base, so it stays in its row only
        public static List<FootprintSummary> Summary(IEnumerable<FootprintRow> rows)
        {
            return rows.GroupBy(row => row.Prototype ?? "", StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new FootprintSummary
            {
                Prototype = group.Key, Plots = group.Count(), SameType = group.Count(Held), OtherType = group.Count(row => row.OtherType != null), NoOutline = group.Count(row => !row.DistanceMm.HasValue),
                LargestGapMm = group.Where(Held).Max(row => row.DistanceMm), OverLimit = group.Count(row => Held(row) && row.Flagged),
            }).ToList();
        }

        public static IEnumerable<string> SummaryLines(IEnumerable<FootprintRow> rows)
        {
            return Summary(rows).Select(item => "prototype " + item.Prototype + ": "
                + (item.LargestGapMm.HasValue ? "largest gap to the house outline " + item.LargestGapMm.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " mm, " + item.OverLimit + " of " + item.SameType + " plots of the same type over the limit" : "no plot of the same type")
                + ", " + item.OtherType + " of another type (Q277), " + item.NoOutline + " with no outline (R453, R477)");
        }

        // R477. The part of the run line for the plots measured
        public static string RunCounts(IReadOnlyCollection<FootprintRow> rows)
        {
            return ", over the footprint limit " + rows.Count(row => Held(row) && row.Flagged) + " of " + rows.Count(Held) + " of the same type, "
                + rows.Count(row => row.OtherType != null) + " of another type (Q277), " + rows.Count(row => !row.DistanceMm.HasValue) + " with no outline";
        }

        // masses holds the base of each mass file by its prototype, so a base taken from the slab is a row of its own (R451, Q279)
        public static JObject Json(IEnumerable<FootprintRow> rows, IEnumerable<KeyValuePair<string, MassBase>> masses = null)
        {
            List<FootprintRow> all = rows.ToList();
            return new JObject
            {
                ["format"] = "site-adaptation-footprint",
                ["counts"] = new JObject
                {
                    ["sameType"] = all.Count(Held), ["overLimit"] = all.Count(row => Held(row) && row.Flagged), ["otherType"] = all.Count(row => row.OtherType != null), ["noOutline"] = all.Count(row => !row.DistanceMm.HasValue),
                },
                ["rows"] = new JArray(all.Select(row => new JObject
                {
                    ["plot"] = row.Plot, ["prototype"] = row.Prototype, ["distanceMm"] = row.DistanceMm, ["limitMm"] = row.LimitMm, ["flagged"] = row.Flagged, ["otherType"] = row.OtherType, ["why"] = row.Why,
                })),
                ["prototypes"] = new JArray(Summary(all).Select(item => new JObject
                {
                    ["prototype"] = item.Prototype, ["plots"] = item.Plots, ["sameType"] = item.SameType, ["otherType"] = item.OtherType, ["noOutline"] = item.NoOutline,
                    ["largestGapMm"] = item.LargestGapMm, ["overLimit"] = item.OverLimit,
                })),
                ["masses"] = new JArray((masses ?? Enumerable.Empty<KeyValuePair<string, MassBase>>()).Where(item => item.Value != null).OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new JObject
                    {
                        ["prototype"] = System.IO.Path.GetFileName(item.Key), ["source"] = item.Value.Source, ["flagged"] = item.Value.Flagged, ["reason"] = item.Value.Reason, ["wallsSkipped"] = new JArray(item.Value.Skipped.Cast<object>().ToArray()), ["wallsBackFace"] = new JArray(item.Value.BackFace.Cast<object>().ToArray()),
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
    }
}
