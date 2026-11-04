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
        public string Stopped { get; set; }
    }

    public sealed class DataCheckOutput
    {
        public JObject Check { get; set; }
        public JObject Profile { get; set; }
        public JObject Plots { get; set; }
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
            public string Villa;
            public string VillaWhy;
            public string VillaDispute;
            public bool? Mirror;
            public bool HasBlock;
            public string MirrorWhy;
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
            return token == null || token.Type == JTokenType.Null ? "" : (string)token;
        }

        public static DataCheckOutput Stopped(DataCheckSettings settings, DataCheckInput input, string why)
        {
            var needs = new JArray(Needs.Select(need => Need(need, "missing", why)));
            return new DataCheckOutput
            {
                Check = Wrap(needs, input, null),
                Profile = new JObject { ["levels"] = new JArray(LevelCount(settings).Select(level => Level(level, null, Missing))) },
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
                ["scanChecks"] = checks ?? new JArray(),
            };
        }

        public static DataCheckOutput Run(DataCheckSettings settings, DataCheckInput input)
        {
            var codes = new HashSet<string>(input.PrototypeNames.Select(settings.PrototypeCode).Where(code => code != null), StringComparer.Ordinal);
            JObject scan = null;
            List<KeyValuePair<string, List<double[]>>> rings = new List<KeyValuePair<string, List<double[]>>>();
            if (input.CadReads.Count > 0)
            {
                try
                {
                    scan = ScanLogic.Result(settings.Scan, codes, codes, settings.StrongShare, new HashSet<string>(StringComparer.Ordinal), input.CadReads, input.Excels, input.Pdfs, new JArray(), out rings);
                }
                catch (Exception failure)
                {
                    input.Notes.Add(new DataCheckNote { Name = "(plot read)", Kind = "scan", Read = false, Reason = failure.GetType().Name + ": " + failure.Message });
                }
            }
            List<JObject> rows = scan == null ? new List<JObject>() : ((JArray)scan["rows"]).OfType<JObject>().ToList();
            List<JObject> checks = scan == null ? new List<JObject>() : ((JArray)scan["checks"]).OfType<JObject>().ToList();
            JObject outline = rows.FirstOrDefault(row => Str(row["kind"]) == "plotOutline" && Str(row["proposedUse"]) == "plot outlines");
            string drawingName = outline == null ? null : Str(outline["source"]).Split('|')[0];
            JObject drawing = drawingName == null ? null : input.CadReads.FirstOrDefault(read => Str(read["source"]) == drawingName);
            double? metersPerUnit = drawing == null ? (double?)null : DataCheckSettings.MetersPerUnit(Str(drawing["units"]));
            var plots = new List<Plot>();
            if (scan != null && outline != null)
            {
                var byNumber = new Dictionary<string, Plot>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, List<double[]>> ring in rings)
                {
                    byNumber[ring.Key] = new Plot
                    {
                        Number = ring.Key, Ring = ring.Value,
                        Box = new[] { ring.Value.Min(p => p[0]), ring.Value.Min(p => p[1]), ring.Value.Max(p => p[0]), ring.Value.Max(p => p[1]) },
                    };
                }
                plots = byNumber.Values.OrderBy(plot => plot.Number, StringComparer.Ordinal).ToList();
            }
            string lead;
            HashSet<string> blockSources = BlockSources(rows);
            FindVillaTypes(plots, scan, rows, codes, out lead);
            FindMirror(plots, input, blockSources);
            string groupWhy = null;
            if (plots.Count > 0)
            {
                if (metersPerUnit == null)
                {
                    groupWhy = "the drawing states a unit this check cannot turn into metres: " + Str(drawing["units"]);
                    foreach (Plot plot in plots)
                    {
                        plot.Group = new int[settings.GroupGapsM.Count];
                    }
                }
                else
                {
                    for (int level = 0; level < settings.GroupGapsM.Count; level++)
                    {
                        int[] labels = Cluster(plots, settings.GroupGapsM[level] / metersPerUnit.Value);
                        for (int at = 0; at < plots.Count; at++)
                        {
                            if (level == 0)
                            {
                                plots[at].Group = new int[settings.GroupGapsM.Count];
                            }
                            plots[at].Group[level] = labels[at];
                        }
                    }
                }
            }
            string[] words = new string[settings.GroupGapsM.Count];
            var groupNames = new List<Dictionary<int, string>>();
            for (int level = 0; level < settings.GroupGapsM.Count; level++)
            {
                groupNames.Add(new Dictionary<int, string>());
            }
            if (plots.Count > 0 && groupWhy == null)
            {
                WordsFromCad(settings, plots, drawing, rows, metersPerUnit.Value, words);
                WordsFromExcel(settings, plots, input.Excels, words, groupNames);
            }
            return Build(settings, input, scan, rows, checks, outline, plots, lead, blockSources, drawing, metersPerUnit, words, groupNames, groupWhy);
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
                var every = new SortedSet<string>(((JObject)row["villaType"]).Properties().Select(item => Str(item.Value)), StringComparer.Ordinal);
                for (int kind = 0; kind < KindWords.Length && plot.Villa == null; kind++)
                {
                    List<string> given = ((JObject)row["villaType"]).Properties().Where(item => KindOf(item.Name) == kind).Select(item => Str(item.Value)).Distinct(StringComparer.Ordinal).ToList();
                    if (given.Count == 1)
                    {
                        plot.Villa = given[0];
                        plot.VillaWhy = null;
                        used[kind]++;
                        // R602. Another kind of source that gives another code is said, the plot keeps the code of the first kind and is counted
                        if (every.Count > 1)
                        {
                            plot.VillaDispute = "the " + KindWords[kind] + " give " + given[0] + " and another kind of source gives " + string.Join(", ", every.Where(code => code != given[0]));
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

        // The hand of the villa block inside each plot: the insert is drawn mirrored or not, the plots that hold inserts of both hands none
        private static void FindMirror(List<Plot> plots, DataCheckInput input, HashSet<string> blockSources)
        {
            if (blockSources.Count == 0)
            {
                foreach (Plot plot in plots)
                {
                    plot.MirrorWhy = "no layer of villa blocks was found in the plots";
                }
                return;
            }
            var hands = new Dictionary<Plot, HashSet<bool>>();
            var grid = new BoxGrid(plots.Select(plot => plot.Box).ToList(), 0);
            foreach (JObject read in input.CadReads)
            {
                string file = Str(read["source"]);
                foreach (JObject insert in ((JArray)read["inserts"]).OfType<JObject>().Where(item => item["mirrored"] != null && blockSources.Contains(file + "|" + Str(item["layer"]))))
                {
                    double x = (double)insert["x"], y = (double)insert["y"];
                    foreach (int at in grid.Holding(x, y).Where(at => ScanLogic.Contains(plots[at].Ring, x, y)))
                    {
                        if (!hands.TryGetValue(plots[at], out HashSet<bool> set))
                        {
                            hands[plots[at]] = set = new HashSet<bool>();
                        }
                        set.Add((bool)insert["mirrored"]);
                    }
                }
            }
            foreach (Plot plot in plots)
            {
                if (!hands.TryGetValue(plot, out HashSet<bool> set))
                {
                    plot.MirrorWhy = "the plot holds no villa block insert";
                }
                else if (set.Count > 1)
                {
                    plot.HasBlock = true;
                    plot.MirrorWhy = "the plot holds villa blocks drawn both mirrored and not mirrored";
                }
                else
                {
                    plot.HasBlock = true;
                    plot.Mirror = set.First();
                }
            }
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
        private static int[] Cluster(List<Plot> plots, double gap)
        {
            var parent = Enumerable.Range(0, plots.Count).ToArray();
            Func<int, int> find = null;
            find = at => parent[at] == at ? at : (parent[at] = find(parent[at]));
            int[] order = Enumerable.Range(0, plots.Count).OrderBy(at => plots[at].Box[0]).ToArray();
            for (int first = 0; first < order.Length; first++)
            {
                Plot a = plots[order[first]];
                for (int second = first + 1; second < order.Length; second++)
                {
                    Plot b = plots[order[second]];
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

        // R603. The word of a group level found in the texts of the plot drawing. A text of two or more words that lies outside every plot
        // and within wordReach of a plot is the word before its last token and that token. A word is the word of a level when its texts,
        // each with a token of its own, sit by wordShare of the groups of the level, the largest level taking it first
        private static void WordsFromCad(DataCheckSettings settings, List<Plot> plots, JObject drawing, List<JObject> rows, double metersPerUnit, string[] words)
        {
            if (drawing == null)
            {
                return;
            }
            JObject numberRow = rows.FirstOrDefault(row => Str(row["kind"]) == "plotNumber" && Str(row["proposedUse"]) == "plot numbers");
            string numberLayer = numberRow == null ? null : Str(numberRow["source"]).Split('|').Last();
            double reach = settings.WordReachM / metersPerUnit;
            var grid = new BoxGrid(plots.Select(plot => plot.Box).ToList(), 0);
            var found = new List<Tuple<string, string, int[]>>();
            foreach (JObject text in ((JArray)drawing["texts"]).OfType<JObject>())
            {
                if (numberLayer != null && Str(text["layer"]) == numberLayer)
                {
                    continue;
                }
                string[] tokens = Regex.Split(Str(text["text"]).Trim(), @"\s+").Where(token => token.Length > 0).ToArray();
                if (tokens.Length < 2)
                {
                    continue;
                }
                double x = (double)text["x"], y = (double)text["y"];
                if (grid.Holding(x, y).Any(at => ScanLogic.Contains(plots[at].Ring, x, y)))
                {
                    continue;
                }
                int near = -1;
                double least = reach;
                for (int at = 0; at < plots.Count; at++)
                {
                    double gap = BoxGap(plots[at].Box, new[] { x, y, x, y });
                    if (gap <= least)
                    {
                        least = gap;
                        near = at;
                    }
                }
                if (near >= 0)
                {
                    found.Add(Tuple.Create(string.Join(" ", tokens.Take(tokens.Length - 1)), tokens[tokens.Length - 1], plots[near].Group));
                }
            }
            for (int level = 0; level < words.Length; level++)
            {
                int groups = plots.Select(plot => plot.Group[level]).Distinct().Count();
                string best = null;
                double bestShare = 0;
                foreach (IGrouping<string, Tuple<string, string, int[]>> word in found.GroupBy(item => item.Item1, StringComparer.Ordinal).OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    if (words.Contains(word.Key) || !Regex.IsMatch(word.Key, @"\p{L}"))
                    {
                        continue;
                    }
                    List<IGrouping<int, Tuple<string, string, int[]>>> covered = word.GroupBy(item => item.Item3[level]).ToList();
                    bool eachOwn = covered.All(group => group.Select(item => item.Item2).Distinct(StringComparer.Ordinal).Count() == 1)
                        && covered.Select(group => group.First().Item2).Distinct(StringComparer.Ordinal).Count() == covered.Count;
                    double share = (double)covered.Count / groups;
                    if (eachOwn && covered.Count >= 2 && share >= settings.WordShare && share > bestShare)
                    {
                        best = word.Key;
                        bestShare = share;
                    }
                }
                words[level] = best;
            }
        }

        // R603. The word of a level found in a header of a sheet: a column whose values part the plots of the sheet the way the groups of
        // that level part them. A header that is only the letter of its column is no word
        private static void WordsFromExcel(DataCheckSettings settings, List<Plot> plots, List<ScanWorkbook> excels, string[] words, List<Dictionary<int, string>> groupNames)
        {
            var byNumber = plots.ToDictionary(plot => plot.Number, StringComparer.Ordinal);
            foreach (ScanWorkbook book in excels)
            {
                foreach (ScanSheet sheet in book.Sheets)
                {
                    int key = -1, hits = 0;
                    for (int column = 0; column < sheet.Headers.Count; column++)
                    {
                        int count = sheet.Rows.Where(row => column < row.Count && byNumber.ContainsKey(ScanLogic.CellText(row[column]).Trim())).Select(row => row[column].Trim()).Distinct(StringComparer.Ordinal).Count();
                        if (count > hits)
                        {
                            hits = count;
                            key = column;
                        }
                    }
                    if (key < 0 || hits < settings.UsableShare * plots.Count)
                    {
                        continue;
                    }
                    for (int column = 0; column < sheet.Headers.Count; column++)
                    {
                        string header = sheet.Headers[column];
                        if (column == key || header == ScanLogic.ColumnLetter(column))
                        {
                            continue;
                        }
                        var value = new Dictionary<Plot, string>();
                        foreach (List<string> row in sheet.Rows.Where(row => key < row.Count && column < row.Count && byNumber.ContainsKey(row[key].Trim()) && row[column].Trim().Length > 0))
                        {
                            value[byNumber[row[key].Trim()]] = row[column].Trim();
                        }
                        if (value.Count < settings.UsableShare * plots.Count)
                        {
                            continue;
                        }
                        for (int level = 0; level < words.Length; level++)
                        {
                            if ((words[level] != null && !string.Equals(words[level], header, StringComparison.OrdinalIgnoreCase)) || (words[level] == null && words.Contains(header)) || !SamePartition(value, level))
                            {
                                continue;
                            }
                            words[level] = words[level] ?? header;
                            foreach (IGrouping<int, KeyValuePair<Plot, string>> group in value.GroupBy(item => item.Key.Group[level]))
                            {
                                groupNames[level][group.Key] = group.Select(item => item.Value).First();
                            }
                        }
                    }
                }
            }
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
