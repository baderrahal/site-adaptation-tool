using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R600. One file the check looked at, for the note of the data check: the name, the kind, whether it was read and why not
    public sealed class DataCheckNote
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public bool Read { get; set; }
        public string Reason { get; set; }
    }

    // R600. What the worker found in the folder: the reads of the drawings and workbooks, the names of the prototype files and the counts of
    // the files that are no drawing or workbook. The logic holds no file type of Windows or Revit
    public sealed class DataCheckInput
    {
        public List<JObject> CadReads { get; } = new List<JObject>();
        public List<ScanWorkbook> Excels { get; } = new List<ScanWorkbook>();
        public List<ScanPdf> Pdfs { get; } = new List<ScanPdf>();
        public List<string> PrototypeNames { get; } = new List<string>();
        public List<DataCheckNote> Notes { get; } = new List<DataCheckNote>();
        public int LandXmlFiles { get; set; }
        public int LandXmlWithCoordinateSystem { get; set; }
        public int RuleFiles { get; set; }
        public int SharedParameterFiles { get; set; }

        // R700. The parameter sheet the project settings name, the one source of the shared parameters. Sheet is null when it was not
        // read, SheetProblem then says why in plain words
        public string SheetName { get; set; }
        public bool SheetFound { get; set; }
        public SetUp.ParameterSheet Sheet { get; set; }
        public string SheetProblem { get; set; }
        public string Stopped { get; set; }
        public List<string> Ties { get; } = new List<string>();
        public ConfirmedProfile Confirmed { get; set; }
        public List<string> SkippedFolders { get; } = new List<string>();
    }

    public sealed class DataCheckOutput
    {
        public JObject Check { get; set; }
        public JObject Profile { get; set; }
        public JObject Plots { get; set; }

        // R645. The ring of each plot by its number as stage 1 chose it, in drawing units, and the metres one drawing unit is. Empty and null when no plot was read
        public List<KeyValuePair<string, List<double[]>>> Rings { get; set; } = new List<KeyValuePair<string, List<double[]>>>();

        public double? MetersPerUnit { get; set; }
    }

    // R600 to R605. Stage 1 from the raw files of a folder: the plots with their number, villa type, mirror and groups, the project's own
    // word for each group level, and a mark with one plain line for each need. It reads the scan of R420 to R427 and takes every distance,
    // share and pattern from DataCheckSettings, so no word, layer or file name of a project is here
    public static partial class DataCheckLogic
    {
        public static readonly string[] Needs =
        {
            "plotOutlines", "plotNumbers", "groupLevels", "villaType", "villaBlocks", "roadEdges", "roadLevels", "coordinates", "prototypes", "rules", "sharedParameters",
        };

        public const string Computed = "computed";
        public const string Missing = "missing";

        private sealed class Plot
        {
            public string Number;
            public List<double[]> Ring;
            public double[] Box;
            public double Area;
            public string NumberFlag;
            public List<List<double[]>> Holes = new List<List<double[]>>();
            public bool Hole => Holes.Count > 0;
            public string Villa;
            public string VillaWhy;
            public string VillaDispute;
            public List<string> OtherCodes = new List<string>();
            public string DrawingCode;
            public string CodeRead;
            public string MapDoubt;
            public HashSet<string> BlockCodes = new HashSet<string>(StringComparer.Ordinal);
            public bool HandKnown;
            public bool BlockOfOtherType;
            public bool FromColumn;
            public string ColumnDisagrees;
            public string ListedTwoWays;
            public string AreaName;
            public string AreaWhy;
            public bool? Mirror;
            public bool HasBlock;
            public string MirrorWhy;
            // R731. How many plots the block on this plot stands on, 0 with none
            public int BlockSpan;
            // R730. A villa code any kind of source gave in the plot, also when two of a kind disagree
            public bool CodeSeen;
            public int[] Group;
        }

        // A plain line has no new line, no bracket and no more than 160 characters
        public static string Line(string text)
        {
            text = Regex.Replace(text ?? "", @"[\r\n]+", " ");
            text = Regex.Replace(text, @"[{}\[\]]", "");
            return text.Length <= 160 ? text : text.Substring(0, 157) + "...";
        }

        // A name of a layer as the plain lines write it. A name with a code look is left out, since the lines are CAD and Revit words
        private static string Quote(string name)
        {
            return string.IsNullOrWhiteSpace(name) || Regex.IsMatch(name, @"[a-z][A-Z]|[{}\[\]]") || name.Length > 40 ? "a layer" : "layer " + name;
        }

        private static JObject Need(string need, string mark, string line)
        {
            return new JObject { ["need"] = need, ["mark"] = mark, ["line"] = Line(line) };
        }

        private static string Mark(double share, DataCheckSettings settings)
        {
            return share >= settings.StrongShare ? "strong" : share >= settings.UsableShare ? "usable" : "weak";
        }

        private static string Str(JToken token)
        {
            // A null text set in a JObject stays a token of type String until it is written, so the type alone does not tell
            return token == null || token.Type == JTokenType.Null ? "" : (string)token ?? "";
        }

        public static DataCheckOutput Stopped(DataCheckSettings settings, DataCheckInput input, string why)
        {
            var needs = new JArray(Needs.Select(need => Need(need, "missing", why)));
            var profile = new JObject { ["levels"] = new JArray(LevelCount(settings).Select(level => Level(level, null, Missing))) };
            AddProposals(profile, new Findings { Input = input });
            return new DataCheckOutput
            {
                Check = Wrap(needs, input, null),
                Profile = profile,
                Plots = new JObject { ["plots"] = new JArray() },
            };
        }

        private static IEnumerable<int> LevelCount(DataCheckSettings settings)
        {
            return Enumerable.Range(1, settings == null ? 0 : settings.GroupGapsM.Count);
        }

        private static JObject Level(int level, string word, string source)
        {
            return new JObject { ["level"] = level, ["word"] = word, ["source"] = source };
        }

        private static JObject Wrap(JArray needs, DataCheckInput input, JArray checks, JArray values = null)
        {
            return new JObject
            {
                ["needs"] = needs,
                ["values"] = values ?? new JArray(),
                ["files"] = new JArray(input.Notes.OrderBy(note => note.Name, StringComparer.Ordinal).Select(note => new JObject
                {
                    ["name"] = note.Name, ["kind"] = note.Kind, ["status"] = note.Read ? "read" : "not read", ["reason"] = note.Reason,
                })),
                ["stopped"] = input.Stopped,
                ["ties"] = new JArray(input.Ties),
                ["skippedFolders"] = new JArray(input.SkippedFolders),
                ["scanChecks"] = checks ?? new JArray(),
            };
        }

        // R426. The kinds of villa type source in the order a plot is read by: blocks, texts, a sheet column, a PDF page
        private static int KindOf(string source)
        {
            return source.Contains("|block|") ? 0 : source.Contains("|text|") ? 1 : source.Contains("|page ") ? 3 : 2;
        }

        private static readonly string[] KindWords = { "blocks", "text", "a sheet", "a PDF page" };

        // The insert layers the villa blocks are read from, as "file|layer": every layer a block gave a prototype code to a plot from, else the
        // proposed villa block layer, else none. A project that draws each villa type on a layer of its own gives several layers
        private static HashSet<string> BlockSources(List<JObject> rows)
        {
            var found = new HashSet<string>(rows.Where(row => Str(row["kind"]) == "villaType" && Str(row["source"]).Contains("|block|") && (int)row["score"] > 0)
                .Select(row => Str(row["source"]).Replace("|block|", "|")), StringComparer.Ordinal);
            if (found.Count == 0)
            {
                JObject proposed = rows.FirstOrDefault(row => Str(row["kind"]) == "villaBlock" && Str(row["proposedUse"]) == "villa blocks");
                if (proposed != null)
                {
                    found.Add(Str(proposed["source"]));
                }
            }
            return found;
        }

        // R602. The villa type of a plot is the one code the sources of the first kind that gives it agree on, blocks, then texts, a sheet and
        // a PDF page. Two codes in one kind are a conflict the plot is left without a type for, said in its reason, never a pick. lead is the
        // plain words for the kinds that gave a type, null when none did
        private static void FindVillaTypes(List<Plot> plots, JObject scan, List<JObject> rows, HashSet<string> codes, out string lead)
        {
            var byPlot = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var scored = new HashSet<string>(rows.Where(row => Str(row["kind"]) == "villaType" && (int)row["score"] > 0).Select(row => Str(row["source"])), StringComparer.Ordinal);
            if (scan != null)
            {
                foreach (JObject row in ((JArray)scan["plots"]).OfType<JObject>())
                {
                    byPlot[Str(row["plot"])] = row;
                }
            }
            var used = new int[KindWords.Length];
            foreach (Plot plot in plots)
            {
                if (codes.Count == 0)
                {
                    plot.VillaWhy = "no prototype file name gives a villa type code to look for";
                    continue;
                }
                plot.VillaWhy = scored.Count == 0 ? "no drawing text, block or sheet column gives a prototype code in the plots" : "the plot holds no villa block, text or sheet row with a prototype code";
                if (!byPlot.TryGetValue(plot.Number, out JObject row))
                {
                    continue;
                }
                var every = new SortedSet<string>(((JObject)row["villaType"]).Properties().Where(item => plot.NumberFlag == null || KindOf(item.Name) < 2).Select(item => Str(item.Value)), StringComparer.Ordinal);
                plot.CodeSeen = every.Count > 0;
                // A plot whose label holds two numbers was joined to a sheet or a page by the last of them, so only blocks and texts count for it
                int kinds = plot.NumberFlag != null ? 2 : KindWords.Length;
                for (int kind = 0; kind < kinds && plot.Villa == null; kind++)
                {
                    List<string> given = ((JObject)row["villaType"]).Properties().Where(item => KindOf(item.Name) == kind).Select(item => Str(item.Value)).Distinct(StringComparer.Ordinal).ToList();
                    if (given.Count == 1)
                    {
                        plot.Villa = given[0];
                        plot.VillaWhy = null;
                        // Blocks and texts are what the drawing says, a typology column that only repeats them is no new source (R631)
                        plot.DrawingCode = kind < 2 ? given[0] : null;
                        used[kind]++;
                        // R602. Another kind of source that gives another code is said, the plot keeps the code of the first kind and is counted
                        if (every.Count > 1)
                        {
                            plot.OtherCodes.AddRange(every.Where(code => code != given[0]));
                            plot.VillaDispute = "the " + KindWords[kind] + " give " + given[0] + " and another kind of source gives " + string.Join(", ", plot.OtherCodes);
                        }
                    }
                    else if (given.Count > 1)
                    {
                        plot.VillaWhy = "the " + KindWords[kind] + " in the plot give " + given.Count + " villa types: " + string.Join(", ", given.OrderBy(code => code, StringComparer.Ordinal));
                        break;
                    }
                }
            }
            lead = used.All(count => count == 0) ? null : string.Join(" and ", Enumerable.Range(0, used.Length).Where(kind => used[kind] > 0).Select(kind => KindWords[kind]));
        }

        // A grid of boxes: the boxes that may hold a point, each box added to every cell it covers, grown by the margin first
        private sealed class BoxGrid
        {
            private readonly List<double[]> boxes;
            private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
            private readonly double size;
            private readonly double margin;

            public BoxGrid(List<double[]> boxes, double margin)
            {
                this.boxes = boxes;
                this.margin = margin;
                List<double> spans = boxes.Select(box => Math.Max(box[2] - box[0], box[3] - box[1])).OrderBy(span => span).ToList();
                size = Math.Max(1e-6, (spans.Count == 0 ? 1.0 : spans[spans.Count / 2]) + 2 * margin);
                for (int index = 0; index < boxes.Count; index++)
                {
                    double[] box = boxes[index];
                    for (long column = Cell(box[0] - margin); column <= Cell(box[2] + margin); column++)
                    {
                        for (long row = Cell(box[1] - margin); row <= Cell(box[3] + margin); row++)
                        {
                            long key = column * 4000037L + row;
                            if (!cells.TryGetValue(key, out List<int> list))
                            {
                                cells[key] = list = new List<int>();
                            }
                            list.Add(index);
                        }
                    }
                }
            }

            private long Cell(double value)
            {
                return (long)Math.Floor(value / size);
            }

            public IEnumerable<int> Holding(double x, double y)
            {
                if (!cells.TryGetValue(Cell(x) * 4000037L + Cell(y), out List<int> list))
                {
                    return Enumerable.Empty<int>();
                }
                return list.Where(index => boxes[index][0] - margin <= x && x <= boxes[index][2] + margin && boxes[index][1] - margin <= y && y <= boxes[index][3] + margin).ToList();
            }
        }

        // R601. The plots of one level: two plots whose outlines lie within the gap of each other are one group, and a chain of such plots
        // is one group. Numbered from 0 in the order the plots are listed, so the same plots give the same labels
        private static int[] Cluster(List<Plot> plots, List<Plot> standIns, double gap)
        {
            // The outlines that got no row are in the distance test, so the gap of a plot with no number does not read as a road
            List<Plot> members = plots.Concat(standIns).ToList();
            var parent = Enumerable.Range(0, members.Count).ToArray();
            Func<int, int> find = null;
            find = at => parent[at] == at ? at : (parent[at] = find(parent[at]));
            int[] order = Enumerable.Range(0, members.Count).OrderBy(at => members[at].Box[0]).ToArray();
            for (int first = 0; first < order.Length; first++)
            {
                Plot a = members[order[first]];
                for (int second = first + 1; second < order.Length; second++)
                {
                    Plot b = members[order[second]];
                    if (b.Box[0] > a.Box[2] + gap)
                    {
                        break;
                    }
                    if (find(order[first]) == find(order[second]) || BoxGap(a.Box, b.Box) > gap)
                    {
                        continue;
                    }
                    if (RingGap(a.Ring, b.Ring) <= gap)
                    {
                        parent[find(order[first])] = find(order[second]);
                    }
                }
            }
            var labels = new Dictionary<int, int>();
            var result = new int[plots.Count];
            for (int at = 0; at < plots.Count; at++)
            {
                int root = find(at);
                if (!labels.ContainsKey(root))
                {
                    labels[root] = labels.Count;
                }
                result[at] = labels[root];
            }
            return result;
        }

        private static double BoxGap(double[] a, double[] b)
        {
            double dx = Math.Max(0, Math.Max(a[0] - b[2], b[0] - a[2]));
            double dy = Math.Max(0, Math.Max(a[1] - b[3], b[1] - a[3]));
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // The least distance between two rings: 0 when one holds a point of the other, else the least distance of a side to a side
        private static double RingGap(List<double[]> a, List<double[]> b)
        {
            if (ScanLogic.Contains(b, a[0][0], a[0][1]) || ScanLogic.Contains(a, b[0][0], b[0][1]))
            {
                return 0;
            }
            double least = double.MaxValue;
            for (int i = 0; i < a.Count; i++)
            {
                double[] a1 = a[i], a2 = a[(i + 1) % a.Count];
                for (int j = 0; j < b.Count; j++)
                {
                    double[] b1 = b[j], b2 = b[(j + 1) % b.Count];
                    least = Math.Min(least, SegmentGap(a1, a2, b1, b2));
                    if (least == 0)
                    {
                        return 0;
                    }
                }
            }
            return least;
        }

        private static double SegmentGap(double[] a1, double[] a2, double[] b1, double[] b2)
        {
            if (Crosses(a1, a2, b1, b2))
            {
                return 0;
            }
            return Math.Min(Math.Min(PointSegment(a1, b1, b2), PointSegment(a2, b1, b2)), Math.Min(PointSegment(b1, a1, a2), PointSegment(b2, a1, a2)));
        }

        private static double Cross(double[] o, double[] a, double[] b)
        {
            return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
        }

        private static bool Crosses(double[] a1, double[] a2, double[] b1, double[] b2)
        {
            double d1 = Cross(b1, b2, a1), d2 = Cross(b1, b2, a2), d3 = Cross(a1, a2, b1), d4 = Cross(a1, a2, b2);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double PointSegment(double[] p, double[] a, double[] b)
        {
            double dx = b[0] - a[0], dy = b[1] - a[1];
            double length2 = dx * dx + dy * dy;
            double t = length2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / length2));
            double x = a[0] + t * dx - p[0], y = a[1] + t * dy - p[1];
            return Math.Sqrt(x * x + y * y);
        }

        private static bool SamePartition(Dictionary<Plot, string> value, int level)
        {
            var forward = new Dictionary<int, string>();
            var back = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<Plot, string> item in value)
            {
                int group = item.Key.Group[level];
                if ((forward.TryGetValue(group, out string seen) && seen != item.Value) || (back.TryGetValue(item.Value, out int owner) && owner != group))
                {
                    return false;
                }
                forward[group] = item.Value;
                back[item.Value] = group;
            }
            return true;
        }
    }
}
