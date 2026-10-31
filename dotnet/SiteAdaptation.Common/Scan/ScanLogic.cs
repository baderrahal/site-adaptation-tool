using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R420 to R427. The values of the scan block of the rules profile, scan.ruleOf naming the rule of each, so no file, layer or sheet
    // name of a project is in the code
    public sealed class ScanSettings
    {
        public Regex LabelPattern { get; private set; }
        public Regex TokenSplit { get; private set; }
        public List<int> PlotNumberShifts { get; private set; }
        public int PairMinSegments { get; private set; }
        public int PairThreads { get; private set; }
        public double PdfJoinGapPt { get; private set; }
        public double PdfLineTolerancePt { get; private set; }
        public double PdfPairDyLow { get; private set; }
        public double PdfPairDyHigh { get; private set; }
        public double PdfPairDxPt { get; private set; }

        public static ScanSettings From(JObject profile)
        {
            JObject block = profile?["scan"] as JObject ?? throw new InvalidDataException("the rules profile holds no scan block (R420)");
            JArray shifts = block["plotNumberShifts"] as JArray;
            if (shifts == null || shifts.Count == 0 || shifts.Any(item => item.Type != JTokenType.Integer))
            {
                throw new InvalidDataException("scan.plotNumberShifts of the rules profile is no list of whole numbers (R426)");
            }
            JArray dy = block["pdfPairDyPt"] as JArray;
            if (dy == null || dy.Count != 2 || dy.Any(item => item.Type != JTokenType.Float && item.Type != JTokenType.Integer) || (double)dy[0] > (double)dy[1])
            {
                throw new InvalidDataException("scan.pdfPairDyPt of the rules profile is no band of two numbers, the low first (R426)");
            }
            return new ScanSettings
            {
                LabelPattern = new Regex(Text(block, "labelPattern", "R421"), RegexOptions.CultureInvariant),
                TokenSplit = new Regex(Text(block, "tokenSplit", "R425"), RegexOptions.CultureInvariant),
                PlotNumberShifts = shifts.Select(item => (int)item).ToList(),
                PairMinSegments = block["pairMinSegments"]?.Type == JTokenType.Integer && (int)block["pairMinSegments"] >= 1 ? (int)block["pairMinSegments"]
                    : throw new InvalidDataException("scan.pairMinSegments of the rules profile is no whole number of 1 or more (R421)"),
                PairThreads = block["pairThreads"]?.Type == JTokenType.Integer && (int)block["pairThreads"] >= 1 ? (int)block["pairThreads"]
                    : throw new InvalidDataException("scan.pairThreads of the rules profile is no whole number of 1 or more (R428)"),
                PdfJoinGapPt = Number(block, "pdfJoinGapPt"),
                PdfLineTolerancePt = Number(block, "pdfLineTolerancePt"),
                PdfPairDyLow = (double)dy[0],
                PdfPairDyHigh = (double)dy[1],
                PdfPairDxPt = Number(block, "pdfPairDxPt"),
            };
        }

        private static string Text(JObject block, string key, string rule)
        {
            return block[key]?.Type == JTokenType.String && ((string)block[key]).Length > 0 ? (string)block[key]
                : throw new InvalidDataException("scan." + key + " of the rules profile is no text (" + rule + ")");
        }

        private static double Number(JObject block, string key)
        {
            JToken value = block[key];
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer) || (double)value < 0)
            {
                throw new InvalidDataException("scan." + key + " of the rules profile is no number of 0 or more (R426)");
            }
            return (double)value;
        }
    }

    // R426. One sheet as the scanner reads it: the header of each column from the first row with any text, the column letter when that
    // cell is empty, and the rows under that row, every cell as its text
    public sealed class ScanSheet
    {
        public string Name { get; set; }
        public List<string> Headers { get; set; }
        public List<List<string>> Rows { get; set; }

        public static ScanSheet Of(string name, IEnumerable<IList<string>> cells)
        {
            List<List<string>> rows = cells.Select(row => row.Select(text => text ?? "").ToList()).ToList();
            int width = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
            int head = rows.FindIndex(row => row.Any(text => SheetRows.PyStrip(text).Length > 0));
            List<string> header = head < 0 ? new List<string>() : rows[head];
            var headers = new List<string>();
            for (int column = 0; column < width; column++)
            {
                string text = column < header.Count ? SheetRows.PyStrip(header[column]) : "";
                headers.Add(text.Length > 0 ? text : ScanLogic.ColumnLetter(column));
            }
            return new ScanSheet { Name = name, Headers = headers, Rows = head < 0 ? new List<List<string>>() : rows.Skip(head + 1).ToList() };
        }
    }

    public sealed class ScanWorkbook
    {
        public string Name { get; set; }
        public List<ScanSheet> Sheets { get; set; }
    }

    // R426. The text lines of every page of a PDF file, page 1 first, each with its box in page points from the top left (R179)
    public sealed class ScanPdf
    {
        public string Name { get; set; }
        public List<List<TextLine>> Pages { get; set; }
    }

    // R421 to R427. The scan result of the files of a run from neutral inputs: the scan_cad files of R420, the sheets of each Excel file,
    // the lines of each PDF file and the codes of the profile and the prototype names. Every list in the order of
    // docs/design/17-scanner.md, every order ordinal, as python/scan.py gives scan_python.json
    public static class ScanLogic
    {
        public const string LayerJoin = " + ";
        private const string TieJoin = " or ";
        private static readonly string[] Kinds = { "cadLayer", "plotOutline", "plotNumber", "villaBlock", "villaType" };
        private static readonly Dictionary<string, string> Use = new Dictionary<string, string>
        {
            ["plotOutline"] = "plot outlines", ["plotNumber"] = "plot numbers", ["villaBlock"] = "villa blocks", ["villaType"] = "villa type",
        };

        // The text of a cell as python/scan.py cell_text gives it: a number with no fraction as a whole number, else Python str of the value
        public static string CellText(object value)
        {
            switch (value)
            {
                case null:
                    return "";
                case double number when !double.IsNaN(number) && !double.IsInfinity(number) && Math.Floor(number) == number:
                    return new BigInteger(number).ToString(CultureInfo.InvariantCulture);
                default:
                    return SheetRows.PyText(value);
            }
        }

        // openpyxl get_column_letter of the column numbered from 0
        public static string ColumnLetter(int column)
        {
            string letters = "";
            for (int number = column + 1; number > 0; number = (number - 1) / 26)
            {
                letters = (char)('A' + (number - 1) % 26) + letters;
            }
            return letters;
        }

        // R426, R179. The text lines of a page as the location plan reads them, rows of the top within the line tolerance cut where a
        // character starts more than the gap after the end of the one before, with no size band. Turned characters join no line (Q252)
        public static List<TextLine> PdfLines(IEnumerable<PageChar> chars, double gap, double tolerance)
        {
            return LocationPlan.TextLines(chars.Where(found => found.Upright), double.NegativeInfinity, double.PositiveInfinity, gap, tolerance);
        }

        // R425. The code of a prototype file name by prototypeNames of config, the part after the prefix, the discipline and a dash up to the
        // suffix, as the build reads it, else null
        public static string PrototypeCode(string name, JObject prototypeNames)
        {
            string prefix = (string)prototypeNames["prefix"], discipline = (string)prototypeNames["discipline"], suffix = (string)prototypeNames["suffix"];
            string head = prefix + discipline + "-";
            if (!name.StartsWith(head, StringComparison.Ordinal) || string.IsNullOrEmpty(suffix) || name.IndexOf(suffix, head.Length, StringComparison.Ordinal) < 0)
            {
                return null;
            }
            string code = name.Substring(head.Length, name.IndexOf(suffix, head.Length, StringComparison.Ordinal) - head.Length);
            return code.Length > 0 ? code : null;
        }

        // R425. The codes a value is matched with: the keys of typologies and their keo_file, the old and new codes of typology_codes and
        // the code of each prototype file name
        public static HashSet<string> KnownCodes(JObject profile, JObject prototypeNames, IEnumerable<string> names)
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);
            JObject typologies = profile["typologies"] as JObject ?? throw new InvalidDataException("the rules profile holds no typologies (R425)");
            foreach (JProperty typology in typologies.Properties())
            {
                codes.Add(typology.Name);
                string keo = Plain((typology.Value as JObject)?["keo_file"]);
                if (keo.Length > 0)
                {
                    codes.Add(keo);
                }
            }
            foreach (JObject row in CodeRows(profile))
            {
                foreach (string code in new[] { Plain(row["old"]), Plain(row["new"]) }.Where(code => code.Length > 0))
                {
                    codes.Add(code);
                }
            }
            List<string> files = names.ToList();
            if (files.Count > 0 && prototypeNames == null)
            {
                throw new InvalidDataException("config names no prototypeNames, the prefix, discipline and suffix a prototype file name is read by (R425)");
            }
            foreach (string code in files.Select(name => PrototypeCode(name, prototypeNames)).Where(code => code != null))
            {
                codes.Add(code);
            }
            return codes;
        }

        // R425, R426. The codes of the prototype file names given, by prototypeNames of config, the codes that score a villaType source
        public static HashSet<string> PrototypeCodes(JObject prototypeNames, IEnumerable<string> names)
        {
            List<string> files = names.ToList();
            if (files.Count > 0 && prototypeNames == null)
            {
                throw new InvalidDataException("config names no prototypeNames, the prefix, discipline and suffix a prototype file name is read by (R425)");
            }
            return new HashSet<string>(files.Select(name => PrototypeCode(name, prototypeNames)).Where(code => code != null), StringComparer.Ordinal);
        }

        // R426, Q245. The old and the new code of each row of typology_codes that differ, each pair as one key
        public static HashSet<string> OldNewPairs(JObject profile)
        {
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject row in CodeRows(profile))
            {
                string old = Plain(row["old"]), now = Plain(row["new"]);
                if (old.Length > 0 && now.Length > 0 && old != now)
                {
                    pairs.Add(PairKey(old, now));
                }
            }
            return pairs;
        }

        private static string PairKey(string one, string two)
        {
            return string.CompareOrdinal(one, two) < 0 ? one + "\u0000" + two : two + "\u0000" + one;
        }

        private static IEnumerable<JObject> CodeRows(JObject profile)
        {
            JArray rows = profile["typology_codes"]?["rows"] as JArray ?? throw new InvalidDataException("the rules profile holds no typology_codes.rows (R425)");
            return rows.OfType<JObject>();
        }

        private static string Plain(JToken token)
        {
            return token != null && token.Type == JTokenType.String ? (string)token : "";
        }

        // R425. The codes a value holds, matched with case: the whole value when it is a code, else each code that lies in it as a run bounded
        // by the start, the end or a character of the split, the longest code first and a matched run not used again, so a code that holds a
        // split character is kept whole. In ordinal order, two or more make the value a check
        public static List<string> ValueCodes(string value, HashSet<string> codes, Regex split)
        {
            string text = SheetRows.PyStrip(value ?? "");
            if (text.Length == 0)
            {
                return new List<string>();
            }
            if (codes.Contains(text))
            {
                return new List<string> { text };
            }
            var used = new bool[text.Length];
            var found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string code in codes.Where(code => code.Length > 0).OrderByDescending(code => code.Length).ThenBy(code => code, StringComparer.Ordinal))
            {
                for (int at = text.IndexOf(code, StringComparison.Ordinal); at >= 0; at = at + 1 < text.Length ? text.IndexOf(code, at + 1, StringComparison.Ordinal) : -1)
                {
                    int end = at + code.Length;
                    bool bounded = (at == 0 || split.IsMatch(text[at - 1].ToString())) && (end == text.Length || split.IsMatch(text[end].ToString()));
                    if (!bounded || Enumerable.Range(at, code.Length).Any(index => used[index]))
                    {
                        continue;
                    }
                    for (int index = at; index < end; index++)
                    {
                        used[index] = true;
                    }
                    found.Add(code);
                }
            }
            return found.ToList();
        }

        // R421. The number of a label as PlotLabels.Parse takes it: the last line with text, matched whole by labelPattern, its group 2
        public static string LabelNumber(string text, Regex pattern)
        {
            List<string> lines = (text ?? "").Split('\n').Select(SheetRows.PyStrip).Where(line => line.Length > 0).ToList();
            if (lines.Count == 0)
            {
                return null;
            }
            Match match = pattern.Match(lines[lines.Count - 1]);
            return match.Success && match.Groups[2].Success ? match.Groups[2].Value : null;
        }

        // The even odd ray test of PlotLabels.Contains of the CAD reader, the same sums in the same order, so a point on an edge counts as
        // the reader counts it
        public static bool Contains(IReadOnlyList<double[]> ring, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                double[] a = ring[i];
                double[] b = ring[j];
                if ((a[1] > y) != (b[1] > y) && x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0])
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // R426. The CAD label a number text gives under a shift, the number minus the shift, when a plot holds it
        public static string PlotOf(string text, int shift, ICollection<string> plots)
        {
            text = SheetRows.PyStrip(text ?? "");
            if (text.Length == 0 || text.Any(letter => letter < '0' || letter > '9'))
            {
                return null;
            }
            string number = (BigInteger.Parse(text, CultureInfo.InvariantCulture) - shift).ToString(CultureInfo.InvariantCulture);
            return plots.Contains(number) ? number : null;
        }

        // R426. The number tokens of a line, the tokens of the split of only the digits 0 to 9
        private static List<string> NumberTokens(string line, Regex split)
        {
            return split.Split(line).Where(token => token.Length > 0 && token.All(letter => letter >= '0' && letter <= '9')).ToList();
        }

        public static string PointText(double[] point)
        {
            return Rounding.Fixed(point[0], 3).ToString("F3", CultureInfo.InvariantCulture) + ", " + Rounding.Fixed(point[1], 3).ToString("F3", CultureInfo.InvariantCulture);
        }

        private sealed class Face
        {
            public List<double[]> Ring;
            public double[] Centroid;
            public double[] Box;
            public string Number;
            public string Label;
            public string Layer;

            public Face Plot(Label label)
            {
                return new Face { Ring = Ring, Centroid = Centroid, Box = Box, Number = label.Number, Label = label.Text, Layer = label.Layer };
            }

            public bool Holds(double x, double y)
            {
                return Box[0] <= x && x <= Box[2] && Box[1] <= y && y <= Box[3] && Contains(Ring, x, y);
            }
        }

        private sealed class Label
        {
            public string Number;
            public string Text;
            public string Layer;
            public double X;
            public double Y;
        }

        // The faces bucketed on a grid of their boxes, so a point is tested only against the faces whose box may hold it, in the order
        // given, which python/scan.py gives by a tree of the boxes
        private sealed class FaceIndex
        {
            private readonly List<Face> faces;
            private readonly double x0, y0, x1, y1, width, height;
            private readonly int cells;
            private readonly List<int>[] buckets;

            public FaceIndex(List<Face> faces)
            {
                this.faces = faces;
                if (faces.Count == 0)
                {
                    return;
                }
                x0 = faces.Min(face => face.Box[0]);
                y0 = faces.Min(face => face.Box[1]);
                x1 = faces.Max(face => face.Box[2]);
                y1 = faces.Max(face => face.Box[3]);
                cells = Math.Max(1, Math.Min(512, (int)Math.Ceiling(Math.Sqrt(faces.Count))));
                width = (x1 - x0) / cells;
                height = (y1 - y0) / cells;
                buckets = new List<int>[cells * cells];
                for (int index = 0; index < faces.Count; index++)
                {
                    double[] box = faces[index].Box;
                    for (int column = Cell(box[0], x0, width); column <= Cell(box[2], x0, width); column++)
                    {
                        for (int row = Cell(box[1], y0, height); row <= Cell(box[3], y0, height); row++)
                        {
                            List<int> bucket = buckets[row * cells + column] ?? (buckets[row * cells + column] = new List<int>());
                            bucket.Add(index);
                        }
                    }
                }
            }

            private int Cell(double value, double start, double size)
            {
                if (!(size > 0))
                {
                    return 0;
                }
                return Math.Max(0, Math.Min(cells - 1, (int)Math.Floor((value - start) / size)));
            }

            public IEnumerable<Face> Holding(double x, double y)
            {
                if (faces.Count == 0 || x < x0 || x > x1 || y < y0 || y > y1)
                {
                    return Enumerable.Empty<Face>();
                }
                List<int> bucket = buckets[Cell(y, y0, height) * cells + Cell(x, x0, width)];
                return bucket == null ? Enumerable.Empty<Face>() : bucket.Select(index => faces[index]).Where(face => face.Holds(x, y)).ToList();
            }
        }

        private sealed class Cad
        {
            public string Source;
            public JObject Read;
            public List<Label> Texts = new List<Label>();
            public List<Label> Inserts = new List<Label>();
        }

        private sealed class Proposal
        {
            public string Source;
            public int Score;
            public List<Face> Plots;
            public Cad Cad;
        }

        private static JObject Row(string kind, string source, string found, int score, bool proposed)
        {
            return new JObject
            {
                ["kind"] = kind, ["source"] = source, ["found"] = found, ["proposedUse"] = proposed ? Use[kind] : kind == "cadLayer" ? "" : "check", ["score"] = score, ["conflicts"] = 0,
            };
        }

        private static JObject Check(string what, string source, string plot, string value)
        {
            return new JObject { ["what"] = what, ["source"] = source, ["plot"] = plot, ["value"] = value };
        }

        private static JObject Conflict(string plot, string field, string valueA, string sourceA, string valueB, string sourceB)
        {
            return new JObject { ["plot"] = plot, ["field"] = field, ["valueA"] = valueA, ["sourceA"] = sourceA, ["valueB"] = valueB, ["sourceB"] = sourceB };
        }

        // The source of the highest score above 0, null on a tie or when none scores, and the tied sources in ordinal order
        private static string Pick(IDictionary<string, int> scores, out List<string> tied)
        {
            tied = new List<string>();
            int best = scores.Count == 0 ? 0 : scores.Values.Max();
            if (best <= 0)
            {
                return null;
            }
            List<string> top = scores.Where(item => item.Value == best).Select(item => item.Key).OrderBy(source => source, StringComparer.Ordinal).ToList();
            if (top.Count == 1)
            {
                return top[0];
            }
            tied = top;
            return null;
        }

        private static string Pick(IDictionary<string, int> scores, string kind, List<JObject> checks)
        {
            List<string> tied;
            string chosen = Pick(scores, out tied);
            if (tied.Count > 0)
            {
                checks.Add(Check("tie", kind, "", string.Join(TieJoin, tied)));
            }
            return chosen;
        }

        private static string Str(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? "" : (string)token;
        }

        private static Face FaceOf(JToken face)
        {
            List<double[]> ring = ((JArray)face["ring"]).Select(point => new[] { (double)point[0], (double)point[1] }).ToList();
            return new Face
            {
                Ring = ring,
                Centroid = new[] { (double)face["centroid"][0], (double)face["centroid"][1] },
                Box = new[] { ring.Min(point => point[0]), ring.Min(point => point[1]), ring.Max(point => point[0]), ring.Max(point => point[1]) },
            };
        }

        private static Cad CadOf(JObject read, Regex pattern)
        {
            var cad = new Cad { Source = Str(read["source"]), Read = read };
            foreach (JToken text in read["texts"] as JArray ?? new JArray())
            {
                string number = LabelNumber(Str(text["text"]), pattern);
                cad.Texts.Add(new Label { Number = number, Text = Str(text["text"]), Layer = Str(text["layer"]), X = (double)text["x"], Y = (double)text["y"] });
            }
            foreach (JToken insert in read["inserts"] as JArray ?? new JArray())
            {
                cad.Inserts.Add(new Label { Text = Str(insert["block"]), Layer = Str(insert["layer"]), X = (double)insert["x"], Y = (double)insert["y"] });
            }
            return cad;
        }

        // R421. The labels each face holds, in the order of the faces, and the labels inside no face
        private static List<List<Label>> Held(List<Face> faces, List<Label> labels, List<Label> outside)
        {
            List<List<Label>> held = faces.Select(face => new List<Label>()).ToList();
            var position = new Dictionary<Face, int>();
            for (int index = 0; index < faces.Count; index++)
            {
                position[faces[index]] = index;
            }
            var faceIndex = new FaceIndex(faces);
            foreach (Label label in labels)
            {
                bool any = false;
                foreach (Face face in faceIndex.Holding(label.X, label.Y))
                {
                    held[position[face]].Add(label);
                    any = true;
                }
                if (!any)
                {
                    outside.Add(label);
                }
            }
            return held;
        }

        // R420. One cadLayer row per layer of a file
        private static IEnumerable<JObject> CadRows(Cad cad)
        {
            return (cad.Read["layers"] as JArray ?? new JArray()).Select(layer => Row("cadLayer", cad.Source + "|" + Str(layer["layer"]),
                string.Format(CultureInfo.InvariantCulture, "{0} closed outlines, {1} texts, {2} blocks", (int)layer["closedOutlines"], (int)layer["texts"], (int)layer["blocks"]), 0, false));
        }

        // R421. The plotOutline rows of a file, its proposal and the plots of it, null when none or a tie
        private static Proposal OutlineProposal(Cad cad, List<JObject> rows, List<JObject> checks)
        {
            List<Label> labels = cad.Texts.Where(text => text.Number != null).ToList();
            var plotsOf = new Dictionary<string, List<Face>>(StringComparer.Ordinal);
            var heldOf = new Dictionary<string, List<List<Label>>>(StringComparer.Ordinal);
            var outsideOf = new Dictionary<string, List<Label>>(StringComparer.Ordinal);
            var scores = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (JToken candidate in cad.Read["candidates"] as JArray ?? new JArray())
            {
                List<string> layers = ((JArray)candidate["layers"]).Select(Str).OrderBy(name => name, StringComparer.Ordinal).ToList();
                string source = cad.Source + "|" + string.Join(LayerJoin, layers);
                List<Face> faces = ((JArray)candidate["faces"]).Select(FaceOf).ToList();
                outsideOf[source] = new List<Label>();
                heldOf[source] = Held(faces, labels, outsideOf[source]);
                plotsOf[source] = Enumerable.Range(0, faces.Count).Where(index => heldOf[source][index].Count == 1).Select(index => faces[index].Plot(heldOf[source][index][0])).ToList();
                scores[source] = plotsOf[source].Count;
                rows.Add(Row("plotOutline", source, faces.Count.ToString(CultureInfo.InvariantCulture) + " faces", scores[source], false));
            }
            string proposed = Pick(scores, "plotOutline", checks);
            if (proposed == null)
            {
                return null;
            }
            foreach (JObject found in rows.Where(found => (string)found["kind"] == "plotOutline" && (string)found["source"] == proposed))
            {
                found["proposedUse"] = Use["plotOutline"];
            }
            // R421. No plot is left out in silence: a face of the proposal of two or more labels, and a label in no face of it
            foreach (List<Label> held in heldOf[proposed].Where(held => held.Count > 1))
            {
                checks.Add(Check("manyLabels", proposed, "", string.Join(", ", held.Select(label => label.Number).OrderBy(number => number, StringComparer.Ordinal))));
            }
            foreach (Label label in outsideOf[proposed])
            {
                checks.Add(Check("labelOutside", cad.Source + "|" + label.Layer, label.Number, label.Text));
            }
            return new Proposal { Source = proposed, Score = scores[proposed], Plots = plotsOf[proposed], Cad = cad };
        }

        // R422. A plotNumber row per text layer of the file of a proposed outline, scored by the plots whose label lies on it
        private static void NumberRows(Proposal proposal, List<JObject> rows, List<JObject> checks)
        {
            Cad cad = proposal.Cad;
            var scores = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string layer in cad.Texts.Select(text => text.Layer).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
            {
                scores[cad.Source + "|" + layer] = 0;
            }
            foreach (Face plot in proposal.Plots)
            {
                scores[cad.Source + "|" + plot.Layer]++;
            }
            string proposed = Pick(scores, "plotNumber", checks);
            foreach (KeyValuePair<string, int> score in scores.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                rows.Add(Row("plotNumber", score.Key, score.Value.ToString(CultureInfo.InvariantCulture) + " labels in plots", score.Value, score.Key == proposed));
            }
        }

        // R423. A villaBlock row per insert layer of each file, scored by the plots of the plot drawing that hold an insert point of it
        private static void BlockRows(List<Cad> cads, FaceIndex index, List<JObject> rows, List<JObject> checks)
        {
            var held = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Cad cad in cads)
            {
                foreach (Label insert in cad.Inserts)
                {
                    string source = cad.Source + "|" + insert.Layer;
                    if (!counts.ContainsKey(source))
                    {
                        counts[source] = 0;
                        held[source] = new HashSet<string>(StringComparer.Ordinal);
                    }
                    counts[source]++;
                    held[source].UnionWith(index.Holding(insert.X, insert.Y).Select(face => face.Number));
                }
            }
            Dictionary<string, int> scores = counts.Keys.ToDictionary(source => source, source => held[source].Count, StringComparer.Ordinal);
            string proposed = Pick(scores, "villaBlock", checks);
            foreach (string source in counts.Keys.OrderBy(source => source, StringComparer.Ordinal))
            {
                rows.Add(Row("villaBlock", source, counts[source].ToString(CultureInfo.InvariantCulture) + " inserts", scores[source], source == proposed));
            }
        }

        // R424. The plots of another file held against the plots of the plot drawing. Two of one number link when the label text is the same
        // and the centroid of each lies inside the other, else a conflict row of each field that fails. A plot whose centroid lies inside a
        // plot of the drawing with another number is a conflict of the number. Never a silent pick
        private static void LinkConflicts(string drawing, Dictionary<string, Face> plots, FaceIndex index, Proposal other, List<JObject> conflicts)
        {
            foreach (Face theirs in other.Plots)
            {
                if (plots.TryGetValue(theirs.Number, out Face ours))
                {
                    bool located = theirs.Holds(ours.Centroid[0], ours.Centroid[1]) && ours.Holds(theirs.Centroid[0], theirs.Centroid[1]);
                    if (!located)
                    {
                        conflicts.Add(Conflict(ours.Number, "location", PointText(ours.Centroid), drawing, PointText(theirs.Centroid), other.Source));
                    }
                    if (ours.Label != theirs.Label)
                    {
                        conflicts.Add(Conflict(ours.Number, "label", ours.Label, drawing, theirs.Label, other.Source));
                    }
                }
                foreach (Face face in index.Holding(theirs.Centroid[0], theirs.Centroid[1]))
                {
                    if (face.Number != theirs.Number)
                    {
                        conflicts.Add(Conflict(face.Number, "number", face.Number, drawing, theirs.Number, other.Source));
                    }
                }
            }
        }

        // R426. The kinds of the villaType sources in the order the lead is sought in
        private const int BlockKind = 0, TextKind = 1, ExcelKind = 2, PdfKind = 3;

        private sealed class Sources
        {
            public readonly Dictionary<string, Dictionary<string, List<string>>> Values = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> Kinds = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static void AddValue(Sources all, int kind, string source, string plot, string value)
        {
            Dictionary<string, Dictionary<string, List<string>>> sources = all.Values;
            all.Kinds[source] = kind;
            if (SheetRows.PyStrip(value ?? "").Length == 0)
            {
                return;
            }
            if (!sources.TryGetValue(source, out Dictionary<string, List<string>> byPlot))
            {
                sources[source] = byPlot = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            }
            if (!byPlot.TryGetValue(plot, out List<string> values))
            {
                byPlot[plot] = values = new List<string>();
            }
            values.Add(value);
        }

        // R426. The block names and the texts inside each plot of the plot drawing, by file and by insert or text layer
        private static void CadSources(List<Cad> cads, FaceIndex index, Sources sources)
        {
            foreach (Cad cad in cads)
            {
                foreach (Label insert in cad.Inserts)
                {
                    foreach (Face face in index.Holding(insert.X, insert.Y))
                    {
                        AddValue(sources, BlockKind, cad.Source + "|block|" + insert.Layer, face.Number, insert.Text);
                    }
                }
                foreach (Label text in cad.Texts)
                {
                    foreach (Face face in index.Holding(text.X, text.Y))
                    {
                        AddValue(sources, TextKind, cad.Source + "|text|" + text.Layer, face.Number, text.Text);
                    }
                }
            }
        }

        // R426. Each sheet joined to the plots by the column and the shift that match the most plot numbers, a tie joins none, then every
        // other column of the sheet one source. Two columns of one header text are one source, the later column taking the join score
        private static void ExcelSources(ScanWorkbook excel, ICollection<string> plots, List<int> shifts, Sources sources, List<JObject> checks)
        {
            foreach (ScanSheet sheet in excel.Sheets)
            {
                string prefix = excel.Name + "|" + sheet.Name + "|";
                var scores = new Dictionary<string, int>(StringComparer.Ordinal);
                var byText = new Dictionary<string, int[]>(StringComparer.Ordinal);
                for (int column = 0; column < sheet.Headers.Count; column++)
                {
                    foreach (int shift in shifts)
                    {
                        int hits = sheet.Rows.Where(row => column < row.Count).Select(row => PlotOf(row[column], shift, plots)).Where(found => found != null).Distinct(StringComparer.Ordinal).Count();
                        string text = prefix + sheet.Headers[column] + "|shift " + shift.ToString(CultureInfo.InvariantCulture);
                        scores[text] = hits;
                        byText[text] = new[] { column, shift };
                    }
                }
                string joined = Pick(scores, "excelJoin", checks);
                if (joined == null)
                {
                    continue;
                }
                int keyColumn = byText[joined][0], keyShift = byText[joined][1];
                foreach (List<string> row in sheet.Rows)
                {
                    string plot = keyColumn < row.Count ? PlotOf(row[keyColumn], keyShift, plots) : null;
                    if (plot == null)
                    {
                        continue;
                    }
                    for (int other = 0; other < sheet.Headers.Count; other++)
                    {
                        if (other != keyColumn && other < row.Count)
                        {
                            AddValue(sources, ExcelKind, prefix + sheet.Headers[other] + "|shift " + keyShift.ToString(CultureInfo.InvariantCulture), plot, row[other]);
                        }
                    }
                }
            }
        }

        // R426, R180. Each code line of a page paired with the number line under it by the label pairs of the location plan, a number line
        // one that holds no code and one number token, joined under the shift that matches the most plots of the file. Text on one line joins
        // nothing (Q252)
        private static void PdfSources(ScanPdf pdf, ICollection<string> plots, ScanSettings settings, HashSet<string> codes, Sources sources,
            List<JObject> checks)
        {
            var pairs = new List<Tuple<int, string, string>>();
            for (int page = 0; page < pdf.Pages.Count; page++)
            {
                List<TextLine> codeLines = pdf.Pages[page].Where(line => ValueCodes(line.Text, codes, settings.TokenSplit).Count > 0).ToList();
                List<TextLine> numberLines = pdf.Pages[page].Where(line => ValueCodes(line.Text, codes, settings.TokenSplit).Count == 0 && NumberTokens(line.Text, settings.TokenSplit).Count == 1).ToList();
                List<int> loneCodes, loneNumbers;
                foreach (int[] pair in LocationPlan.PairLabels(codeLines, numberLines, settings.PdfPairDyLow, settings.PdfPairDyHigh, settings.PdfPairDxPt, out loneCodes, out loneNumbers))
                {
                    pairs.Add(Tuple.Create(page + 1, codeLines[pair[0]].Text, NumberTokens(numberLines[pair[1]].Text, settings.TokenSplit)[0]));
                }
            }
            var scores = new Dictionary<string, int>(StringComparer.Ordinal);
            var byText = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (int shift in settings.PlotNumberShifts)
            {
                string text = pdf.Name + "|shift " + shift.ToString(CultureInfo.InvariantCulture);
                scores[text] = pairs.Select(pair => PlotOf(pair.Item3, shift, plots)).Where(found => found != null).Distinct(StringComparer.Ordinal).Count();
                byText[text] = shift;
            }
            string chosen = Pick(scores, "pdfShift", checks);
            if (chosen == null)
            {
                return;
            }
            int keyShift = byText[chosen];
            foreach (Tuple<int, string, string> pair in pairs)
            {
                string plot = PlotOf(pair.Item3, keyShift, plots);
                if (plot != null)
                {
                    AddValue(sources, PdfKind, string.Format(CultureInfo.InvariantCulture, "{0}|page {1}|shift {2}", pdf.Name, pair.Item1, keyShift), plot, pair.Item2);
                }
            }
        }

        // R425. The one code each plot of a source gives. A value or a plot with two codes is a check row, and a plot with such a value or
        // two codes gives none
        private static Dictionary<string, string> SourceCodes(string source, Dictionary<string, List<string>> valuesByPlot, HashSet<string> codes, Regex split, List<JObject> checks)
        {
            var given = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<string>> plot in valuesByPlot)
            {
                var found = new SortedSet<string>(StringComparer.Ordinal);
                bool twice = false;
                foreach (string value in plot.Value)
                {
                    List<string> matched = ValueCodes(value, codes, split);
                    if (matched.Count > 1)
                    {
                        checks.Add(Check("twoCodes", source, plot.Key, SheetRows.PyStrip(value)));
                        twice = true;
                    }
                    else if (matched.Count == 1)
                    {
                        found.Add(matched[0]);
                    }
                }
                if (found.Count > 1)
                {
                    checks.Add(Check("twoCodes", source, plot.Key, string.Join(", ", found)));
                }
                else if (found.Count == 1 && !twice)
                {
                    given[plot.Key] = found.Min;
                }
            }
            return given;
        }

        // R426. A villaType row per source whose score is above 0, the score the plots it gives a prototype code. The lead is the highest
        // score of the first kind that scores, blocks, texts, Excel then PDF, a tie there no lead. The lead is proposed only when its share of
        // the plots any source gives a prototype code reaches majority.strongShare, else a check row names the best source of each kind. The
        // plots where another source gives another code than the lead are conflicts, or a check when the two are the old and the new code of
        // one row of typology_codes (Q245)
        private static void TypeRows(Dictionary<string, Dictionary<string, string>> given, Dictionary<string, int> kinds, HashSet<string> prototypeCodes, double strongShare,
            HashSet<string> pairs, List<JObject> rows, List<JObject> conflicts, List<JObject> checks)
        {
            Dictionary<string, int> scores = given.ToDictionary(item => item.Key, item => item.Value.Values.Count(code => prototypeCodes.Contains(code)), StringComparer.Ordinal);
            scores = scores.Where(item => item.Value > 0).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            int covered = given.Values.SelectMany(plots => plots.Where(plot => prototypeCodes.Contains(plot.Value)).Select(plot => plot.Key)).Distinct(StringComparer.Ordinal).Count();
            List<int> order = scores.Keys.Select(source => kinds[source]).Distinct().OrderBy(kind => kind).ToList();
            string lead = null;
            bool proposed = false;
            if (order.Count > 0)
            {
                lead = Pick(scores.Where(item => kinds[item.Key] == order[0]).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), "villaType", checks);
            }
            if (lead != null)
            {
                Func<string, string> share = source => Rounding.Fixed((double)scores[source] / covered, 3).ToString("F3", CultureInfo.InvariantCulture);
                proposed = (double)scores[lead] / covered >= strongShare;
                if (!proposed)
                {
                    var parts = new List<string> { lead + " " + share(lead) };
                    foreach (int kind in order.Skip(1))
                    {
                        string best = scores.Where(item => kinds[item.Key] == kind).OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal).First().Key;
                        parts.Add(best + " " + share(best));
                    }
                    checks.Add(Check("noStrongSource", "villaType", "", string.Join(" | ", parts)));
                }
            }
            List<string> ordered = scores.Keys.OrderBy(source => source, StringComparer.Ordinal).ToList();
            foreach (string source in ordered)
            {
                string found = string.Join(", ", given[source].Values.Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal));
                rows.Add(Row("villaType", source, found, scores[source], proposed && source == lead));
            }
            if (lead == null)
            {
                return;
            }
            foreach (string source in given.Keys.Where(source => source != lead).OrderBy(source => source, StringComparer.Ordinal))
            {
                foreach (KeyValuePair<string, string> plot in given[source].OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    if (!given[lead].TryGetValue(plot.Key, out string ours) || ours == plot.Value)
                    {
                        continue;
                    }
                    if (pairs.Contains(PairKey(ours, plot.Value)))
                    {
                        checks.Add(Check("oldNewPair", source, plot.Key, ours + ", " + plot.Value));
                    }
                    else
                    {
                        conflicts.Add(Conflict(plot.Key, "villa type", ours, lead, plot.Value, source));
                    }
                }
            }
        }

        // R421 to R427. The scan result of the files of a run, every list in the order of docs/design/17-scanner.md
        public static JObject Result(ScanSettings settings, HashSet<string> codes, HashSet<string> prototypeCodes, double strongShare, HashSet<string> pairs, IEnumerable<JObject> cadReads,
            IEnumerable<ScanWorkbook> excels, IEnumerable<ScanPdf> pdfs, JArray inputs)
        {
            var rows = new List<JObject>();
            var conflicts = new List<JObject>();
            var checks = new List<JObject>();
            var proposals = new List<Proposal>();
            List<Cad> cads = cadReads.Select(read => CadOf(read, settings.LabelPattern)).ToList();
            foreach (Cad cad in cads)
            {
                // R420. What the explode did not read, so no content is left out in silence
                foreach (JToken missed in cad.Read["notRead"] as JArray ?? new JArray())
                {
                    checks.Add(Check("notRead", Str(missed["source"]), "", Str(missed["value"])));
                }
                rows.AddRange(CadRows(cad));
                Proposal proposal = OutlineProposal(cad, rows, checks);
                if (proposal != null)
                {
                    proposals.Add(proposal);
                    NumberRows(proposal, rows, checks);
                }
            }
            var fileScores = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Proposal proposal in proposals)
            {
                fileScores[proposal.Cad.Source] = proposal.Score;
            }
            string chosen = Pick(fileScores, "plotDrawing", checks);
            Proposal drawing = proposals.FirstOrDefault(proposal => chosen != null && proposal.Cad.Source == chosen);
            var plots = new Dictionary<string, Face>(StringComparer.Ordinal);
            if (drawing == null)
            {
                checks.Add(Check("noPlotDrawing", "", "", ""));
            }
            else
            {
                foreach (IGrouping<string, Face> number in drawing.Plots.GroupBy(plot => plot.Number, StringComparer.Ordinal))
                {
                    if (number.Count() > 1)
                    {
                        checks.Add(Check("numberTwice", drawing.Source, number.Key, number.Count().ToString(CultureInfo.InvariantCulture)));
                    }
                    else
                    {
                        plots[number.Key] = number.First();
                    }
                }
            }
            List<string> numbers = plots.Keys.OrderBy(number => number, StringComparer.Ordinal).ToList();
            var index = new FaceIndex(numbers.Select(number => plots[number]).ToList());
            BlockRows(cads, index, rows, checks);
            if (drawing != null)
            {
                foreach (Proposal proposal in proposals.Where(proposal => proposal != drawing))
                {
                    LinkConflicts(drawing.Source, plots, index, proposal, conflicts);
                }
            }
            var sources = new Sources();
            CadSources(cads, index, sources);
            var known = new HashSet<string>(numbers, StringComparer.Ordinal);
            foreach (ScanWorkbook excel in excels)
            {
                ExcelSources(excel, known, settings.PlotNumberShifts, sources, checks);
            }
            foreach (ScanPdf pdf in pdfs)
            {
                PdfSources(pdf, known, settings, codes, sources, checks);
            }
            var given = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (string source in sources.Values.Keys.OrderBy(source => source, StringComparer.Ordinal))
            {
                given[source] = SourceCodes(source, sources.Values[source], codes, settings.TokenSplit, checks);
            }
            TypeRows(given, sources.Kinds, prototypeCodes, strongShare, pairs, rows, conflicts, checks);
            foreach (JObject found in rows)
            {
                string source = (string)found["source"];
                string kind = (string)found["kind"];
                string[] fields = kind == "villaType" ? new[] { "villa type" } : kind == "plotOutline" ? new[] { "number", "location", "label" } : new string[0];
                found["conflicts"] = conflicts.Count(item => fields.Contains((string)item["field"]) && ((string)item["sourceA"] == source || (string)item["sourceB"] == source));
            }
            List<JObject> orderedRows = rows.OrderBy(found => Array.IndexOf(Kinds, (string)found["kind"])).ThenBy(found => (string)found["source"], StringComparer.Ordinal).ToList();
            List<JObject> orderedConflicts = conflicts.OrderBy(item => (string)item["plot"], StringComparer.Ordinal).ThenBy(item => (string)item["field"], StringComparer.Ordinal)
                .ThenBy(item => (string)item["sourceA"], StringComparer.Ordinal).ThenBy(item => (string)item["sourceB"], StringComparer.Ordinal)
                .ThenBy(item => (string)item["valueA"], StringComparer.Ordinal).ThenBy(item => (string)item["valueB"], StringComparer.Ordinal).ToList();
            List<JObject> orderedChecks = checks.GroupBy(item => string.Join("\u0000", (string)item["what"], (string)item["source"], (string)item["plot"], (string)item["value"]), StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => (string)item["what"], StringComparer.Ordinal).ThenBy(item => (string)item["source"], StringComparer.Ordinal)
                .ThenBy(item => (string)item["plot"], StringComparer.Ordinal).ThenBy(item => (string)item["value"], StringComparer.Ordinal).ToList();
            var plotRows = new JArray();
            foreach (string number in numbers)
            {
                var types = new JObject();
                foreach (KeyValuePair<string, Dictionary<string, string>> source in given.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    if (source.Value.TryGetValue(number, out string code))
                    {
                        types[source.Key] = code;
                    }
                }
                plotRows.Add(new JObject { ["plot"] = number, ["label"] = plots[number].Label, ["centroid"] = PointText(plots[number].Centroid), ["villaType"] = types });
            }
            return new JObject
            {
                ["inputs"] = inputs ?? new JArray(),
                ["rows"] = new JArray(orderedRows),
                ["conflicts"] = new JArray(orderedConflicts),
                ["checks"] = new JArray(orderedChecks),
                ["plots"] = plotRows,
            };
        }
    }
}
