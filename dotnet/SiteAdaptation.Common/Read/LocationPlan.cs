using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R351. A character of the page as pdfplumber gives it: its text, its box in page points from the top left, its size and whether it
    // is upright. The worker makes it from the letters of PdfPig (PdfPage of CadReader)
    public sealed class PageChar
    {
        public string Text { get; set; }
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public double Size { get; set; }
        public bool Upright { get; set; }
    }

    // R351, R387. A curve of the page as pdfminer gives it, its box and the number of its points: the start, the end of each segment and
    // the start again for a close
    public sealed class PageCurve
    {
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public int Points { get; set; }
    }

    // A word of pdfplumber extract_words with the size of its first character
    public sealed class PageWord
    {
        public string Text { get; set; }
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public double Size { get; set; }
    }

    // R179. A text line of the label size, its text, box, centre and kind
    public sealed class TextLine
    {
        public string Text { get; set; }
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Size { get; set; }
        public string Kind { get; set; }
    }

    // R180. A label, a code line paired with its number line, at the middle of the code line top and the number line bottom
    public sealed class PlanLabel
    {
        public string Code { get; set; }
        public string Kind { get; set; }
        public string Number { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    // R181, R387. A map from page points to site metres, east = m00 x + m10 y + m20 and north = m01 x + m11 y + m21 as location_plan.apply
    // gives it, held about the means of the points it was fitted on so the coordinates of a site keep their digits
    public sealed class PageFit
    {
        public double M00 { get; set; }
        public double M01 { get; set; }
        public double M10 { get; set; }
        public double M11 { get; set; }
        public double PageX { get; set; }
        public double PageY { get; set; }
        public double East { get; set; }
        public double North { get; set; }

        public double[] Apply(double x, double y)
        {
            double dx = x - PageX, dy = y - PageY;
            return new[] { East + M00 * dx + M10 * dy, North + M01 * dx + M11 * dy };
        }

        // The metres per page point, the square root of the absolute determinant of the linear part
        public double Scale => Math.Sqrt(Math.Abs(M00 * M11 - M10 * M01));
    }

    // R181, R182. The baseplan face of a label as yard_rule.face_shapes gives it: its rings, the area and the centroid of their union
    public sealed class PlanFace
    {
        public string Label { get; set; }
        public List<double[][]> Rings { get; } = new List<double[][]>();
        public double CentroidX { get; set; }
        public double CentroidY { get; set; }
    }

    // R351. The settings of the location plan, dataBook.locationPlan and connect of config/project.json
    // R351, R352. face_index.FaceIndex: the faces of a point by LocationPlan.Holds, found by a grid of the cells of their bounding boxes,
    // the cell the median of the larger side of the boxes and at least MinCellM, so a point is held against the faces whose box holds it
    // and never against every face, and the result is the one a scan of every face gives
    public sealed class FaceIndex
    {
        // The least side of a cell, so faces of no extent never give a cell of none. The cell only sets how many faces a point is held against
        public const double MinCellM = 1.0;

        private readonly List<PlanFace> faces;
        private readonly List<double[]> boxes;
        private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
        private readonly double originX;
        private readonly double originY;
        private readonly double cell;

        public FaceIndex(IEnumerable<PlanFace> faces)
        {
            this.faces = faces.OrderBy(face => long.Parse(face.Label, CultureInfo.InvariantCulture)).ToList();
            boxes = this.faces.Select(face =>
            {
                List<double[]> points = face.Rings.SelectMany(ring => ring).ToList();
                return new[] { points.Min(point => point[0]), points.Min(point => point[1]), points.Max(point => point[0]), points.Max(point => point[1]) };
            }).ToList();
            if (boxes.Count == 0)
            {
                return;
            }
            originX = boxes.Min(box => box[0]);
            originY = boxes.Min(box => box[1]);
            List<double> sides = boxes.Select(box => Math.Max(box[2] - box[0], box[3] - box[1])).OrderBy(side => side).ToList();
            int middle = sides.Count / 2;
            double median = sides.Count % 2 == 1 ? sides[middle] : (sides[middle - 1] + sides[middle]) / 2;
            cell = Math.Max(median, MinCellM);
            for (int item = 0; item < boxes.Count; item++)
            {
                double[] box = boxes[item];
                for (long column = Column(box[0]); column <= Column(box[2]); column++)
                {
                    for (long row = Row(box[1]); row <= Row(box[3]); row++)
                    {
                        long key = Key(column, row);
                        if (!cells.TryGetValue(key, out List<int> held))
                        {
                            held = new List<int>();
                            cells[key] = held;
                        }
                        held.Add(item);
                    }
                }
            }
        }

        private long Column(double x)
        {
            return (long)Math.Floor((x - originX) / cell);
        }

        private long Row(double y)
        {
            return (long)Math.Floor((y - originY) / cell);
        }

        private static long Key(long column, long row)
        {
            return (column << 32) ^ (row & 0xFFFFFFFFL);
        }

        // The labels of every face that holds the point, in the order of their numbers
        public List<string> Holding(double x, double y)
        {
            var found = new List<string>();
            if (cells.Count == 0 || !cells.TryGetValue(Key(Column(x), Row(y)), out List<int> held))
            {
                return found;
            }
            foreach (int item in held)
            {
                double[] box = boxes[item];
                if (box[0] <= x && x <= box[2] && box[1] <= y && y <= box[3] && LocationPlan.Holds(faces[item], x, y))
                {
                    found.Add(faces[item].Label);
                }
            }
            return found;
        }

        // The one face that holds the point, null when no face or two hold it
        public string FaceOf(double x, double y)
        {
            List<string> found = Holding(x, y);
            return found.Count == 1 ? found[0] : null;
        }
    }

    public sealed class LocationPlanSettings
    {
        public double SizeLow { get; set; }
        public double SizeHigh { get; set; }
        public double JoinGapPt { get; set; }
        public double LineTolerancePt { get; set; }
        public List<KeyValuePair<string, string>> Patterns { get; set; }
        public List<string> CodeKinds { get; set; }
        public double PairDyLow { get; set; }
        public double PairDyHigh { get; set; }
        public double PairDxPt { get; set; }
        public string FitNeighbourhood { get; set; }
        public double FitDropFactor { get; set; }
        public int FitRounds { get; set; }
        public double FitFloorM { get; set; }
        public Dictionary<string, string> FormOfSuffix { get; set; }
        public double ControlSearchPt { get; set; }
        public double MarkWidthPt { get; set; }
        public double MarkHeightPt { get; set; }
        public double MarkTolerancePt { get; set; }
        public int MarkPoints { get; set; }
        public TableSetting Sales { get; set; }
        public TableSetting CodeTable { get; set; }
        public List<KeyValuePair<string, ScheduleEntry>> Neighbourhoods { get; set; }
        public string ScheduleSheet { get; set; }

        public sealed class TableSetting
        {
            public string Workbook { get; set; }
            public string Sheet { get; set; }
            public int HeaderRow { get; set; }
            public List<KeyValuePair<string, string>> Columns { get; set; }
        }

        public sealed class ScheduleEntry
        {
            public string Schedule { get; set; }
            public int Shift { get; set; }
        }

        public static LocationPlanSettings Load(string path)
        {
            return From(JObject.Parse(WorkFiles.ReadText(path)));
        }

        public static LocationPlanSettings From(JObject root)
        {
            JObject plan = (root["dataBook"] as JObject)?["locationPlan"] as JObject ?? throw new InvalidDataException("the config holds no dataBook.locationPlan (R351)");
            JObject connect = root["connect"] as JObject ?? throw new InvalidDataException("the config holds no connect with the neighbourhood schedules (R351)");
            JObject mark = plan["controlMark"] as JObject ?? throw new InvalidDataException("the config holds no dataBook.locationPlan.controlMark (R387)");
            var hoods = new List<KeyValuePair<string, ScheduleEntry>>();
            foreach (JProperty hood in ((JObject)connect["neighbourhoods"]).Properties())
            {
                if (hood.Value.Type == JTokenType.Null)
                {
                    continue;
                }
                hoods.Add(new KeyValuePair<string, ScheduleEntry>(hood.Name, new ScheduleEntry { Schedule = (string)hood.Value["schedule"], Shift = (int)hood.Value["shift"] }));
            }
            return new LocationPlanSettings
            {
                SizeLow = (double)plan["labelSize"][0],
                SizeHigh = (double)plan["labelSize"][1],
                JoinGapPt = (double)plan["joinGapPt"],
                LineTolerancePt = (double)plan["lineTolerancePt"],
                Patterns = ((JObject)plan["patterns"]).Properties().Select(item => new KeyValuePair<string, string>(item.Name, (string)item.Value)).ToList(),
                CodeKinds = plan["codeKinds"].Select(item => (string)item).ToList(),
                PairDyLow = (double)plan["pairDyPt"][0],
                PairDyHigh = (double)plan["pairDyPt"][1],
                PairDxPt = (double)plan["pairDxPt"],
                FitNeighbourhood = (string)plan["fitNeighbourhood"],
                FitDropFactor = (double)plan["fitDropFactor"],
                FitRounds = (int)plan["fitRounds"],
                FitFloorM = (double)plan["fitFloorM"],
                FormOfSuffix = ((JObject)plan["formOfSuffix"]).Properties().ToDictionary(item => item.Name, item => (string)item.Value),
                ControlSearchPt = (double)plan["controlSearchPt"],
                MarkWidthPt = (double)mark["widthPt"],
                MarkHeightPt = (double)mark["heightPt"],
                MarkTolerancePt = (double)mark["tolerancePt"],
                MarkPoints = (int)mark["points"],
                Sales = Table(plan["sales"], "dataBook.locationPlan.sales"),
                CodeTable = Table(plan["codeTable"], "dataBook.locationPlan.codeTable"),
                Neighbourhoods = hoods,
                ScheduleSheet = (string)connect["scheduleSheet"] ?? throw new InvalidDataException("the config holds no connect.scheduleSheet, the sheet of the schedules (R351)"),
            };
        }

        private static TableSetting Table(JToken value, string where)
        {
            if (!(value is JObject block))
            {
                throw new InvalidDataException("the config holds no " + where + " (R351)");
            }
            return new TableSetting
            {
                Workbook = (string)block["workbook"],
                Sheet = (string)block["sheet"],
                HeaderRow = (int)block["headerRow"],
                Columns = ((JObject)block["columns"]).Properties().Select(item => new KeyValuePair<string, string>(item.Name, (string)item.Value)).ToList(),
            };
        }
    }

    // R351. The location plan of button 2 in C#, python/location_plan.py step for step: the text lines of the label size and their kind
    // (R179), the code and number pairs (R180), the page fitted to the site on the schedule of fitNeighbourhood (R181), which numbers every
    // build, each label on the baseplan face that holds its point with its codes (R182), and the control point fit as a check of that
    // numbering (R387), which numbers nothing and lists apart every face it numbers otherwise with why. The fits are taken about the means
    // of their points, so the coordinates of a site keep their digits (docs/addin-design.md section 5)
    public static class LocationPlan
    {
        public const double WordTolerance = 3;

        // R351. The input of the location plan PDF, Plot numbers of the pane, and of the schedule of each neighbourhood of
        // connect.neighbourhoods, schedule:<NBH>
        public const string PlotNumbersRole = "plotNumbers";
        public const string ScheduleRole = "schedule:";
        public static readonly string[] Roles = { PlotNumbersRole, "baseplan", ScheduleRole + "*", "sales" };

        // R179. location_plan.text_lines: the characters of the size band in the order of their top and their start, a row while the top
        // stays within the line tolerance of the first of the row, each row by its start, cut where a character starts more than the gap
        // after the end of the one before
        public static List<TextLine> TextLines(IEnumerable<PageChar> chars, double low, double high, double gap, double lineTolerance)
        {
            List<PageChar> picked = chars.Where(c => low <= c.Size && c.Size <= high).OrderBy(c => c.Top).ThenBy(c => c.X0).ToList();
            var rows = new List<List<PageChar>>();
            foreach (PageChar c in picked)
            {
                if (rows.Count > 0 && Math.Abs(c.Top - rows[rows.Count - 1][0].Top) <= lineTolerance)
                {
                    rows[rows.Count - 1].Add(c);
                }
                else
                {
                    rows.Add(new List<PageChar> { c });
                }
            }
            var found = new List<TextLine>();
            foreach (List<PageChar> row in rows)
            {
                List<PageChar> sorted = row.OrderBy(c => c.X0).ToList();
                var group = new List<PageChar> { sorted[0] };
                foreach (PageChar c in sorted.Skip(1))
                {
                    if (c.X0 - group[group.Count - 1].X1 <= gap)
                    {
                        group.Add(c);
                    }
                    else
                    {
                        found.Add(Line(group));
                        group = new List<PageChar> { c };
                    }
                }
                found.Add(Line(group));
            }
            return found.Where(line => line.Text.Length > 0).ToList();
        }

        private static TextLine Line(List<PageChar> group)
        {
            double x0 = group.Min(c => c.X0), x1 = group.Max(c => c.X1), top = group.Min(c => c.Top), bottom = group.Max(c => c.Bottom);
            return new TextLine
            {
                Text = SheetRows.PyStrip(string.Concat(group.Select(c => c.Text))),
                X0 = x0,
                X1 = x1,
                Top = top,
                Bottom = bottom,
                X = (x0 + x1) / 2,
                Y = (top + bottom) / 2,
                Size = group[0].Size,
            };
        }

        // A pattern of the config, written for Python re, as a .NET regex matched in full: (?P<name> becomes (?<name>
        public static Regex FullMatch(string pattern)
        {
            return new Regex("^(?:" + pattern.Replace("(?P<", "(?<") + ")\\z", RegexOptions.CultureInvariant);
        }

        // R179. The kind of a text line, the first pattern of the config it matches in full, else other
        public static string Classify(string text, IEnumerable<KeyValuePair<string, string>> patterns)
        {
            foreach (KeyValuePair<string, string> pattern in patterns)
            {
                if (FullMatch(pattern.Value).IsMatch(text))
                {
                    return pattern.Key;
                }
            }
            return "other";
        }

        // R180. location_plan.pair_labels: each code line with the number line just under it, nearest first and one to one, the pairs in
        // the order of the score, the code and the number, and the codes and numbers left alone
        public static List<int[]> PairLabels(IReadOnlyList<TextLine> codes, IReadOnlyList<TextLine> numbers, double dyLow, double dyHigh, double dxMax, out List<int> loneCodes, out List<int> loneNumbers)
        {
            var candidates = new List<Tuple<double, int, int>>();
            for (int i = 0; i < codes.Count; i++)
            {
                for (int k = 0; k < numbers.Count; k++)
                {
                    double dy = numbers[k].Top - codes[i].Top;
                    double dx = Math.Abs(numbers[k].X - codes[i].X);
                    if (dyLow <= dy && dy <= dyHigh && dx <= dxMax)
                    {
                        candidates.Add(Tuple.Create(dx + Math.Abs(dy - (dyLow + dyHigh) / 2), i, k));
                    }
                }
            }
            var usedCodes = new HashSet<int>();
            var usedNumbers = new HashSet<int>();
            var pairs = new List<int[]>();
            foreach (Tuple<double, int, int> candidate in candidates.OrderBy(item => item.Item1).ThenBy(item => item.Item2).ThenBy(item => item.Item3))
            {
                if (usedCodes.Contains(candidate.Item2) || usedNumbers.Contains(candidate.Item3))
                {
                    continue;
                }
                usedCodes.Add(candidate.Item2);
                usedNumbers.Add(candidate.Item3);
                pairs.Add(new[] { candidate.Item2, candidate.Item3 });
            }
            loneCodes = Enumerable.Range(0, codes.Count).Where(i => !usedCodes.Contains(i)).ToList();
            loneNumbers = Enumerable.Range(0, numbers.Count).Where(k => !usedNumbers.Contains(k)).ToList();
            return pairs;
        }

        // R179, R180. location_plan.label_pairs: the lines with their kind and the labels in the order of the pairs
        public static List<PlanLabel> Labels(IEnumerable<PageChar> chars, LocationPlanSettings settings, out List<TextLine> lines)
        {
            lines = TextLines(chars, settings.SizeLow, settings.SizeHigh, settings.JoinGapPt, settings.LineTolerancePt);
            var patterns = settings.Patterns.Select(item => new KeyValuePair<string, Regex>(item.Key, FullMatch(item.Value))).ToList();
            foreach (TextLine line in lines)
            {
                line.Kind = patterns.FirstOrDefault(item => item.Value.IsMatch(line.Text)).Key ?? "other";
            }
            List<TextLine> codes = lines.Where(line => settings.CodeKinds.Contains(line.Kind)).ToList();
            List<TextLine> numbers = lines.Where(line => line.Kind == "number").ToList();
            List<int> loneCodes, loneNumbers;
            return PairLabels(codes, numbers, settings.PairDyLow, settings.PairDyHigh, settings.PairDxPt, out loneCodes, out loneNumbers)
                .Select(pair => new PlanLabel
                {
                    Code = codes[pair[0]].Text,
                    Kind = codes[pair[0]].Kind,
                    Number = numbers[pair[1]].Text,
                    X = (codes[pair[0]].X + numbers[pair[1]].X) / 2,
                    Y = (codes[pair[0]].Top + numbers[pair[1]].Bottom) / 2,
                }).ToList();
        }

        // R387. pdfplumber extract_words with extra_attrs size and its defaults: runs of characters next to each other of one upright
        // flag and one size, each run cut into lines by the top within 3 pt for upright characters, else by the start, each line sorted by
        // the start, else by the top, and cut into words at a blank or where a character does not follow the one before
        public static List<PageWord> Words(IReadOnlyList<PageChar> chars)
        {
            var words = new List<PageWord>();
            int start = 0;
            while (start < chars.Count)
            {
                int end = start + 1;
                while (end < chars.Count && chars[end].Upright == chars[start].Upright && chars[end].Size.Equals(chars[start].Size))
                {
                    end++;
                }
                List<PageChar> run = chars.Skip(start).Take(end - start).ToList();
                bool upright = run[0].Upright;
                Func<PageChar, double> lineKey = upright ? (Func<PageChar, double>)(c => c.Top) : c => c.X0;
                foreach (List<PageChar> line in Cluster(run, lineKey, WordTolerance))
                {
                    List<PageChar> sorted = upright ? line.OrderBy(c => c.X0).ToList() : line.OrderBy(c => c.Top).ThenBy(c => c.Bottom).ToList();
                    var current = new List<PageChar>();
                    foreach (PageChar c in sorted)
                    {
                        if (IsSpace(c.Text))
                        {
                            Flush(current, words);
                        }
                        else if (current.Count > 0 && BeginsNewWord(current[current.Count - 1], c, upright))
                        {
                            Flush(current, words);
                            current.Add(c);
                        }
                        else
                        {
                            current.Add(c);
                        }
                    }
                    Flush(current, words);
                }
                start = end;
            }
            return words;
        }

        // pdfplumber cluster_objects: the values, each once and sorted, joined while each lies within the tolerance of the one before,
        // the objects in the order of their cluster and else as given
        private static List<List<PageChar>> Cluster(List<PageChar> items, Func<PageChar, double> key, double tolerance)
        {
            List<double> values = items.Select(key).Distinct().OrderBy(value => value).ToList();
            var clusterOf = new Dictionary<double, int>();
            int cluster = 0;
            for (int index = 0; index < values.Count; index++)
            {
                if (index > 0 && !(values[index] <= values[index - 1] + tolerance))
                {
                    cluster++;
                }
                clusterOf[values[index]] = cluster;
            }
            return items.Select((item, order) => new { item, order, cluster = clusterOf[key(item)] }).OrderBy(entry => entry.cluster).ThenBy(entry => entry.order)
                .GroupBy(entry => entry.cluster).Select(group => group.Select(entry => entry.item).ToList()).ToList();
        }

        private static bool BeginsNewWord(PageChar before, PageChar now, bool upright)
        {
            if (upright)
            {
                return now.X0 < before.X0 || now.X0 > before.X1 + WordTolerance || Math.Abs(now.Top - before.Top) > WordTolerance;
            }
            return now.Top < before.Top || now.Top > before.Bottom + WordTolerance || Math.Abs(now.X0 - before.X0) > WordTolerance;
        }

        private static void Flush(List<PageChar> current, List<PageWord> words)
        {
            if (current.Count == 0)
            {
                return;
            }
            words.Add(new PageWord
            {
                Text = string.Concat(current.Select(c => c.Text)),
                X0 = current.Min(c => c.X0),
                X1 = current.Max(c => c.X1),
                Top = current.Min(c => c.Top),
                Bottom = current.Max(c => c.Bottom),
                Size = current[0].Size,
            });
            current.Clear();
        }

        // Python str.isspace: a text of one character or more, every one white space
        private static bool IsSpace(string text)
        {
            return !string.IsNullOrEmpty(text) && SheetRows.PyStrip(text).Length == 0;
        }

        public sealed class ControlText
        {
            public PageWord Word { get; set; }
            public double East { get; set; }
            public double North { get; set; }
        }

        // R181, R387. location_plan.control_texts: each control point text the sheet prints, E = <east> N = <north>, its E word and the
        // stated site point, a text whose numbers do not read as Python float reads them left out
        public static List<ControlText> ControlTexts(IReadOnlyList<PageWord> words)
        {
            var found = new List<ControlText>();
            for (int k = 0; k < words.Count; k++)
            {
                if (words[k].Text == "E" && k + 5 < words.Count && words[k + 1].Text == "=" && words[k + 3].Text == "N" && words[k + 4].Text == "=")
                {
                    double east, north;
                    if (PyFloat(words[k + 2].Text, out east) && PyFloat(words[k + 5].Text, out north))
                    {
                        found.Add(new ControlText { Word = words[k], East = east, North = north });
                    }
                }
            }
            return found;
        }

        private static readonly Regex FloatText = new Regex(@"^\s*[+-]?((\d(_?\d)*)?\.?\d(_?\d)*([eE][+-]?\d(_?\d)*)?|\d(_?\d)*\.)\s*$", RegexOptions.CultureInvariant);

        // Python float() of a text of digits, a point and an exponent, false where Python raises ValueError. The words of a control text
        // hold no nan or inf
        private static bool PyFloat(string text, out double value)
        {
            value = 0;
            if (text == null || !FloatText.IsMatch(text))
            {
                return false;
            }
            value = ExactDouble.Parse(SheetRows.PyStrip(text).Replace("_", ""));
            return true;
        }

        public sealed class ControlMark
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double East { get; set; }
            public double North { get; set; }
        }

        // R387. location_plan.control_marks: the mark of each control text, the one curve of the mark shape within the search of the start
        // of its E = text, refused when there is none or two, never a mark by its nearness to the text
        public static List<ControlMark> ControlMarks(IEnumerable<PageCurve> curves, IEnumerable<ControlText> texts, LocationPlanSettings settings)
        {
            List<double[]> shaped = curves.Where(curve => Math.Abs(curve.X1 - curve.X0 - settings.MarkWidthPt) <= settings.MarkTolerancePt
                && Math.Abs(curve.Bottom - curve.Top - settings.MarkHeightPt) <= settings.MarkTolerancePt && curve.Points == settings.MarkPoints)
                .Select(curve => new[] { (curve.X0 + curve.X1) / 2, (curve.Top + curve.Bottom) / 2 }).ToList();
            var found = new List<ControlMark>();
            foreach (ControlText text in texts)
            {
                List<double[]> near = shaped.Where(point => Math.Abs(point[0] - text.Word.X0) <= settings.ControlSearchPt && Math.Abs(point[1] - text.Word.Top) <= settings.ControlSearchPt).ToList();
                if (near.Count != 1)
                {
                    throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture, "the control point text E = {0} N = {1} has {2} curves of the mark shape within {3} pt, one is needed (R387)",
                        SheetRows.PyRepr(text.East), SheetRows.PyRepr(text.North), near.Count, SheetRows.PyRepr(settings.ControlSearchPt)));
                }
                found.Add(new ControlMark { X = near[0][0], Y = near[0][1], East = text.East, North = text.North });
            }
            return found;
        }

        // R181. The affine map from page points to site metres by least squares, by the normal equations about the means
        public static PageFit AffineFit(IReadOnlyList<double[]> page, IReadOnlyList<double[]> site)
        {
            int count = page.Count;
            if (count < 3)
            {
                throw new InvalidDataException("an affine fit of the page needs three points, it has " + count + " (R181)");
            }
            double mx = page.Average(p => p[0]), my = page.Average(p => p[1]), me = site.Average(p => p[0]), mn = site.Average(p => p[1]);
            double sxx = 0, sxy = 0, syy = 0, sxe = 0, sye = 0, sxn = 0, syn = 0;
            for (int index = 0; index < count; index++)
            {
                double x = page[index][0] - mx, y = page[index][1] - my, e = site[index][0] - me, n = site[index][1] - mn;
                sxx += x * x;
                sxy += x * y;
                syy += y * y;
                sxe += x * e;
                sye += y * e;
                sxn += x * n;
                syn += y * n;
            }
            double det = sxx * syy - sxy * sxy;
            if (det == 0)
            {
                throw new InvalidDataException("the page points of an affine fit lie on one line (R181)");
            }
            return new PageFit
            {
                M00 = (sxe * syy - sye * sxy) / det,
                M10 = (sye * sxx - sxe * sxy) / det,
                M01 = (sxn * syy - syn * sxy) / det,
                M11 = (syn * sxx - sxn * sxy) / det,
                PageX = mx,
                PageY = my,
                East = me,
                North = mn,
            };
        }

        // R387. location_plan.similarity_fit: a scale, a turn, a mirror or none and a shift by least squares about the means, of the two
        // mirror states the one whose largest residual is smaller, the mirrored one on a tie
        public static PageFit SimilarityFit(IReadOnlyList<double[]> page, IReadOnlyList<double[]> site)
        {
            if (page.Count < 2)
            {
                throw new InvalidDataException("the similarity of the control marks needs two marks, it has " + page.Count + " (R387)");
            }
            double mx = page.Average(p => p[0]), my = page.Average(p => p[1]), me = site.Average(p => p[0]), mn = site.Average(p => p[1]);
            double s = 0, plainA = 0, plainB = 0, mirrorA = 0, mirrorB = 0;
            for (int index = 0; index < page.Count; index++)
            {
                double x = page[index][0] - mx, y = page[index][1] - my, e = site[index][0] - me, n = site[index][1] - mn;
                s += x * x + y * y;
                plainA += x * e + y * n;
                plainB += x * n - y * e;
                mirrorA += x * e - y * n;
                mirrorB += y * e + x * n;
            }
            if (s == 0)
            {
                throw new InvalidDataException("the control marks lie on one point (R387)");
            }
            var mirrored = new PageFit { M00 = mirrorA / s, M10 = mirrorB / s, M01 = mirrorB / s, M11 = -mirrorA / s, PageX = mx, PageY = my, East = me, North = mn };
            var plain = new PageFit { M00 = plainA / s, M10 = -plainB / s, M01 = plainB / s, M11 = plainA / s, PageX = mx, PageY = my, East = me, North = mn };
            return Largest(mirrored, page, site) <= Largest(plain, page, site) ? mirrored : plain;
        }

        private static double Largest(PageFit fit, IReadOnlyList<double[]> page, IReadOnlyList<double[]> site)
        {
            return Residuals(fit, page, site).Max();
        }

        public static double[] Residuals(PageFit fit, IReadOnlyList<double[]> page, IReadOnlyList<double[]> site)
        {
            var found = new double[page.Count];
            for (int index = 0; index < page.Count; index++)
            {
                double[] at = fit.Apply(page[index][0], page[index][1]);
                found[index] = Hypot(at[0] - site[index][0], at[1] - site[index][1]);
            }
            return found;
        }

        public static double Hypot(double x, double y)
        {
            x = Math.Abs(x);
            y = Math.Abs(y);
            double large = Math.Max(x, y), small = Math.Min(x, y);
            if (large == 0)
            {
                return 0;
            }
            double ratio = small / large;
            return large * Math.Sqrt(1 + ratio * ratio);
        }

        public sealed class RobustResult
        {
            public PageFit Fit { get; set; }
            public double[] Residual { get; set; }
            public List<int> Keep { get; set; }
        }

        // R181. location_plan.robust_fit: fit, drop the points whose residual is over the drop factor times the median of those kept, never
        // under the floor, fit again, for the rounds, then fit on those kept
        public static RobustResult RobustFit(IReadOnlyList<double[]> page, IReadOnlyList<double[]> site, double dropFactor, int rounds, double floor)
        {
            List<int> keep = Enumerable.Range(0, page.Count).ToList();
            PageFit fit;
            double[] residual;
            for (int round = 0; round < rounds; round++)
            {
                fit = AffineFit(keep.Select(i => page[i]).ToList(), keep.Select(i => site[i]).ToList());
                residual = Residuals(fit, page, site);
                double limit = Math.Max(dropFactor * Median(keep.Select(i => residual[i])), floor);
                keep = Enumerable.Range(0, page.Count).Where(i => residual[i] <= limit).ToList();
            }
            fit = AffineFit(keep.Select(i => page[i]).ToList(), keep.Select(i => site[i]).ToList());
            residual = Residuals(fit, page, site);
            return new RobustResult { Fit = fit, Residual = residual, Keep = keep };
        }

        // numpy median and statistics.median: the middle value, or the mean of the two middle values
        public static double Median(IEnumerable<double> values)
        {
            List<double> sorted = values.OrderBy(value => value).ToList();
            if (sorted.Count == 0)
            {
                return double.NaN;
            }
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }

        // R351. The median, the 95th percentile and the largest as location_plan.stats takes them, unrounded, null for none
        public static double[] Stats(IEnumerable<double> values)
        {
            List<double> sorted = values.OrderBy(value => value).ToList();
            if (sorted.Count == 0)
            {
                return null;
            }
            return new[] { Median(sorted), sorted[(int)(0.95 * (sorted.Count - 1))], sorted[sorted.Count - 1] };
        }

        // R182. yard_rule.face_shapes on the plots of the baseplan read: per whole number label its rings of three vertices or more with
        // an area, the centroid of their union the area weighted centroid of the rings, each taken about its first vertex (FaceStep). A
        // ring that is not simple and rings of one label that overlap are refused first, as the build refuses them (FaceRings)
        public static Dictionary<string, PlanFace> Faces(JArray plots, double overlapM2)
        {
            List<string> refused = FaceRings.Refusals(plots, overlapM2);
            if (refused.Count > 0)
            {
                throw new InvalidDataException(refused.Count + " baseplan faces refused: " + string.Join(", ", refused));
            }
            var faces = new Dictionary<string, PlanFace>();
            foreach (JToken entry in plots)
            {
                if ((string)entry["kind"] != "plot_shape")
                {
                    continue;
                }
                JToken plot = entry["plot"];
                string label = FaceStep.WholeLabel(plot == null || plot.Type == JTokenType.Null ? null : plot.ToString());
                double[][] ring = (entry["vertices"] as JArray ?? new JArray()).Select(vertex => new[] { (double)vertex[0], (double)vertex[1] }).ToArray();
                if (label == null || ring.Length < 3 || FaceStep.Ring(ring).Area == 0)
                {
                    continue;
                }
                PlanFace face;
                if (!faces.TryGetValue(label, out face))
                {
                    face = new PlanFace { Label = label };
                    faces[label] = face;
                }
                face.Rings.Add(ring);
            }
            foreach (PlanFace face in faces.Values)
            {
                double area = 0, x = 0, y = 0;
                foreach (double[][] ring in face.Rings)
                {
                    FaceStep.RingValues values = FaceStep.Ring(ring);
                    area += values.Area;
                    x += values.Area * values.X;
                    y += values.Area * values.Y;
                }
                face.CentroidX = x / area;
                face.CentroidY = y / area;
            }
            return faces;
        }

        // A point strictly inside a ring, by the crossings of a ray to the east
        public static bool Inside(double[][] ring, double x, double y)
        {
            bool inside = false;
            for (int index = 0, before = ring.Length - 1; index < ring.Length; before = index++)
            {
                double xi = ring[index][0], yi = ring[index][1], xj = ring[before][0], yj = ring[before][1];
                if (OnSegment(xi, yi, xj, yj, x, y))
                {
                    return false;
                }
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static bool OnSegment(double ax, double ay, double bx, double by, double x, double y)
        {
            double cross = (bx - ax) * (y - ay) - (by - ay) * (x - ax);
            return cross == 0 && Math.Min(ax, bx) <= x && x <= Math.Max(ax, bx) && Math.Min(ay, by) <= y && y <= Math.Max(ay, by);
        }

        // The point lies on an edge of the ring, each edge taken from a vertex to the one before it as Inside takes it
        public static bool OnOutline(double[][] ring, double x, double y)
        {
            for (int index = 0, before = ring.Length - 1; index < ring.Length; before = index++)
            {
                if (OnSegment(ring[index][0], ring[index][1], ring[before][0], ring[before][1], x, y))
                {
                    return true;
                }
            }
            return false;
        }

        // R351. face_index.holds: a face holds a point inside one of its rings by the crossings of a ray to the east, or on the outline of
        // two of its rings or more, such as the edge two rings of one label share, which the union of the rings holds within it
        public static bool Holds(PlanFace face, double x, double y)
        {
            return face.Rings.Any(ring => Inside(ring, x, y)) || face.Rings.Count(ring => OnOutline(ring, x, y)) >= 2;
        }

        // R182, R387, R351. location_plan.face_of_points by a scan of every face: the label of the one face that holds the point by Holds,
        // null where no face or more than one holds it. FaceIndex gives the same by its grid
        public static string FaceOf(IEnumerable<PlanFace> faces, double x, double y)
        {
            string found = null;
            foreach (PlanFace face in faces)
            {
                if (Holds(face, x, y))
                {
                    if (found != null)
                    {
                        return null;
                    }
                    found = face.Label;
                }
            }
            return found;
        }

        // R387. The distance of a point from the outline of a face, the edges of its rings that no other ring of the face shares, as
        // shapely gives the exterior of the union of rings that share an edge
        public static double EdgeDistance(PlanFace face, double x, double y)
        {
            var edges = new List<double[]>();
            foreach (double[][] ring in face.Rings)
            {
                for (int index = 0; index < ring.Length; index++)
                {
                    double[] a = ring[index], b = ring[(index + 1) % ring.Length];
                    edges.Add(new[] { a[0], a[1], b[0], b[1] });
                }
            }
            double best = double.PositiveInfinity;
            foreach (double[] edge in edges)
            {
                bool shared = face.Rings.Count > 1 && edges.Count(other => (other[0] == edge[2] && other[1] == edge[3] && other[2] == edge[0] && other[3] == edge[1])
                    || (other != edge && other[0] == edge[0] && other[1] == edge[1] && other[2] == edge[2] && other[3] == edge[3])) > 0;
                if (!shared)
                {
                    best = Math.Min(best, SegmentDistance(edge, x, y));
                }
            }
            return best;
        }

        private static double SegmentDistance(double[] edge, double x, double y)
        {
            double dx = edge[2] - edge[0], dy = edge[3] - edge[1];
            double length = dx * dx + dy * dy;
            double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((x - edge[0]) * dx + (y - edge[1]) * dy) / length));
            return Hypot(x - (edge[0] + t * dx), y - (edge[1] + t * dy));
        }

        public sealed class Row
        {
            public string Number { get; set; }
            public string Code { get; set; }
            public string Kind { get; set; }
            public string Face { get; set; }
            public long? Offset { get; set; }
            public string Group { get; set; }
            public double PageX { get; set; }
            public double PageY { get; set; }
            public double East { get; set; }
            public double North { get; set; }
            public string NewCode { get; set; }
            public string Rev9 { get; set; }
            public string Nbh { get; set; }
            public string Zone { get; set; }
        }

        public sealed class FitPart
        {
            public string Neighbourhood { get; set; }
            public string Role { get; set; }
            public List<string> Used { get; set; }
            public List<string> Kept { get; set; }
            public double[] ResidualKept { get; set; }
            public double[] ResidualAll { get; set; }
            public double? MetresPerPoint { get; set; }
        }

        public sealed class Why
        {
            public string Number { get; set; }
            public string ScheduleFace { get; set; }
            public string ControlFace { get; set; }
            public double? EdgeM { get; set; }
            public double GapM { get; set; }
            public string Text { get; set; }
        }

        public sealed class ListedFace
        {
            public string Face { get; set; }
            public List<string> Schedule { get; set; }
            public List<string> Control { get; set; }
            public List<Why> Why { get; set; }
        }

        public sealed class ControlCheck
        {
            // R387. Why the check could not be made, a control text with no mark or two, marks that give no similarity or a refine of
            // under three faces, null when it was made. A failed check is an issue of the read and numbers nothing, as every check
            public string Failed { get; set; }
            public int ControlTexts { get; set; }
            public int Marks { get; set; }
            public double[] MarkGapStartM { get; set; }
            public double[] MarkGapRefinedM { get; set; }
            public int RefineFaces { get; set; }
            public int RefineKept { get; set; }
            public double[] RefineResidualKeptM { get; set; }
            public double[] AgainstScheduleM { get; set; }
            public int LabelsOnAFaceSchedule { get; set; }
            public int LabelsOnAFaceControl { get; set; }
            public int UnitFacesNumberedOtherwise { get; set; }
            public List<ListedFace> Listed { get; set; }
        }

        public sealed class Result
        {
            public int Lines { get; set; }
            public List<PlanLabel> Labels { get; set; }
            public List<Row> Rows { get; set; }
            public PageFit Fit { get; set; }
            public List<FitPart> Fits { get; set; }
            public ControlCheck Control { get; set; }
        }

        // R181. location_plan.known_points: the page point of each label whose number is an official number of a schedule, given once on
        // the page, and the centroid of the face of that number less the shift, in the order of the official numbers
        private static void Known(IReadOnlyList<PlanLabel> labels, IReadOnlyDictionary<string, PlanFace> faces, IEnumerable<string> officials, int shift,
            List<double[]> page, List<double[]> site, List<string> used)
        {
            ILookup<string, PlanLabel> byNumber = labels.ToLookup(label => label.Number);
            foreach (string official in officials.OrderBy(PyInt))
            {
                List<PlanLabel> found = byNumber[official].ToList();
                PlanFace face;
                if (found.Count == 1 && faces.TryGetValue((PyInt(official) - shift).ToString(CultureInfo.InvariantCulture), out face))
                {
                    page.Add(new[] { found[0].X, found[0].Y });
                    site.Add(new[] { face.CentroidX, face.CentroidY });
                    used.Add(official);
                }
            }
        }

        // Python int() of a text of digits with blanks around them, as the numbers of the page and the schedules are read
        private static long PyInt(string text)
        {
            return long.Parse(SheetRows.PyStrip(text), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        // R179 to R182, R387. The whole location plan: the labels, the schedule fit, every label on its face with its codes, and the control
        // point fit as its check. officials holds the official numbers of the schedule of each neighbourhood, sales and table the rows of
        // the sales sheet and of the code table as location_plan.read_table gives them
        public static Result Number(IReadOnlyList<PageChar> chars, IReadOnlyList<PageCurve> curves, IReadOnlyDictionary<string, PlanFace> faces,
            IReadOnlyDictionary<string, HashSet<string>> officials, IReadOnlyList<Dictionary<string, string>> sales, IReadOnlyList<Dictionary<string, string>> table,
            LocationPlanSettings settings)
        {
            List<TextLine> lines;
            List<PlanLabel> labels = Labels(chars, settings, out lines);
            Dictionary<string, int> shifts = settings.Neighbourhoods.ToDictionary(item => item.Key, item => item.Value.Shift);
            if (!shifts.ContainsKey(settings.FitNeighbourhood))
            {
                throw new InvalidDataException("dataBook.locationPlan.fitNeighbourhood " + settings.FitNeighbourhood + " has no schedule in connect.neighbourhoods (R181)");
            }
            Func<string, HashSet<string>> officialOf = name => officials.TryGetValue(name, out HashSet<string> found) ? found : new HashSet<string>();
            var page = new List<double[]>();
            var site = new List<double[]>();
            var used = new List<string>();
            Known(labels, faces, officialOf(settings.FitNeighbourhood), shifts[settings.FitNeighbourhood], page, site, used);
            RobustResult robust = RobustFit(page, site, settings.FitDropFactor, settings.FitRounds, settings.FitFloorM);
            var fits = new List<FitPart>
            {
                new FitPart
                {
                    Neighbourhood = settings.FitNeighbourhood, Role = "fit", Used = used, Kept = robust.Keep.Select(i => used[i]).ToList(),
                    ResidualKept = Stats(robust.Keep.Select(i => robust.Residual[i])), ResidualAll = Stats(robust.Residual), MetresPerPoint = robust.Fit.Scale,
                },
            };
            foreach (KeyValuePair<string, int> hood in shifts.Where(item => item.Key != settings.FitNeighbourhood))
            {
                var otherPage = new List<double[]>();
                var otherSite = new List<double[]>();
                var otherUsed = new List<string>();
                Known(labels, faces, officialOf(hood.Key), hood.Value, otherPage, otherSite, otherUsed);
                if (otherPage.Count > 0)
                {
                    fits.Add(new FitPart { Neighbourhood = hood.Key, Role = "check", Used = otherUsed, ResidualAll = Stats(Residuals(robust.Fit, otherPage, otherSite)) });
                }
            }
            var home = new Dictionary<string, string>();
            foreach (KeyValuePair<string, int> hood in shifts)
            {
                foreach (string official in officialOf(hood.Key))
                {
                    home[official] = hood.Key;
                }
            }
            var index = new FaceIndex(faces.Values);
            var rows = new List<Row>();
            foreach (PlanLabel label in labels)
            {
                double[] at = robust.Fit.Apply(label.X, label.Y);
                string face = index.FaceOf(at[0], at[1]);
                string group;
                rows.Add(new Row
                {
                    Number = label.Number, Code = label.Code, Kind = label.Kind, Face = face, Offset = face == null ? (long?)null : PyInt(label.Number) - PyInt(face),
                    Group = home.TryGetValue(label.Number, out group) ? group : "rest", PageX = label.X, PageY = label.Y, East = at[0], North = at[1],
                });
            }
            JoinCodes(rows, sales, table, settings);
            return new Result
            {
                Lines = lines.Count,
                Labels = labels,
                Rows = rows,
                Fit = robust.Fit,
                Fits = fits,
                Control = Check(labels, rows, Words(chars), curves, faces, index, settings),
            };
        }

        // R182. location_plan.join_codes: the new code of each row by the code table, and Typology_Rev9, the NBH and the zone of the first
        // sales row of its number
        public static void JoinCodes(IEnumerable<Row> rows, IEnumerable<Dictionary<string, string>> sales, IReadOnlyList<Dictionary<string, string>> table, LocationPlanSettings settings)
        {
            var saleOf = new Dictionary<string, Dictionary<string, string>>();
            foreach (Dictionary<string, string> row in sales)
            {
                string plot = row["plot"];
                if (!string.IsNullOrEmpty(plot))
                {
                    string key = ((long)Math.Truncate(ParseFloat(plot))).ToString(CultureInfo.InvariantCulture);
                    if (!saleOf.ContainsKey(key))
                    {
                        saleOf[key] = row;
                    }
                }
            }
            string pattern = settings.Patterns.First(item => item.Key == "clientCode").Value;
            foreach (Row row in rows)
            {
                Dictionary<string, string> sale;
                saleOf.TryGetValue(row.Number, out sale);
                row.NewCode = NewCode(row.Code, table, settings.FormOfSuffix, pattern);
                row.Rev9 = sale?["typology"];
                row.Nbh = sale?["nbh"];
                row.Zone = sale?["zone"];
            }
        }

        private static double ParseFloat(string text)
        {
            double value;
            if (!PyFloat(text, out value))
            {
                throw new InvalidDataException("the plot " + text + " of a sales row is no number (R182)");
            }
            return value;
        }

        // R182. location_plan.new_code: the new code of an old client code, an exact row of the table, else the rows of its base code,
        // picked by the form its suffix names when the base has more than one, the base itself when it is a new code already, else null
        public static string NewCode(string code, IReadOnlyList<Dictionary<string, string>> table, IReadOnlyDictionary<string, string> formOfSuffix, string pattern)
        {
            List<Dictionary<string, string>> rows = table.Where(row => !string.IsNullOrEmpty(row["new"])).ToList();
            List<Dictionary<string, string>> exact = rows.Where(row => row["old"] == code).ToList();
            if (exact.Select(row => row["new"]).Distinct().Count() == 1)
            {
                return exact[0]["new"];
            }
            Match found = FullMatch(pattern).Match(code);
            if (!found.Success)
            {
                return null;
            }
            string baseCode = found.Groups["base"].Success ? found.Groups["base"].Value : null;
            string suffix = found.Groups["suffix"].Success ? found.Groups["suffix"].Value : null;
            List<Dictionary<string, string>> ofBase = rows.Where(row => row["old"] == baseCode).ToList();
            if (ofBase.Count == 0)
            {
                ofBase = rows.Where(row => !string.IsNullOrEmpty(row["old"]) && row["old"].Split(' ')[0] == baseCode).ToList();
            }
            List<string> news = ofBase.Select(row => row["new"]).Distinct().ToList();
            if (ofBase.Count == 0 && rows.Any(row => row["new"] == baseCode))
            {
                return baseCode;
            }
            if (news.Count == 1)
            {
                return news[0];
            }
            string form;
            formOfSuffix.TryGetValue(suffix ?? "", out form);
            List<string> picked = ofBase.Where(row => row["form"] == form).Select(row => row["new"]).Distinct().ToList();
            return picked.Count == 1 ? picked[0] : null;
        }

        // R182. location_plan.read_table: the rows of a sheet under its header row, each column of the config by its header text, a cell
        // as str() of Python stripped and null for no value, a row of no value left out
        public static List<Dictionary<string, string>> Table(IReadOnlyList<object[]> rows, int headerRow, IReadOnlyList<KeyValuePair<string, string>> columns, string what)
        {
            if (rows.Count < headerRow)
            {
                throw new InvalidDataException(what + " holds no header row " + headerRow + " (R182)");
            }
            List<string> header = rows[headerRow - 1].Select(cell => cell == null ? null : SheetRows.PyStrip(SheetRows.PyText(cell))).ToList();
            var index = new List<KeyValuePair<string, int>>();
            foreach (KeyValuePair<string, string> column in columns)
            {
                int at = header.IndexOf(column.Value);
                if (at < 0)
                {
                    throw new InvalidDataException(what + " holds no column " + column.Value + " in its header row " + headerRow + " (R182)");
                }
                index.Add(new KeyValuePair<string, int>(column.Key, at));
            }
            var found = new List<Dictionary<string, string>>();
            foreach (object[] row in rows.Skip(headerRow))
            {
                var item = new Dictionary<string, string>();
                foreach (KeyValuePair<string, int> column in index)
                {
                    item[column.Key] = column.Value < row.Length && row[column.Value] != null ? SheetRows.PyStrip(SheetRows.PyText(row[column.Value])) : null;
                }
                if (item.Values.Any(value => !string.IsNullOrEmpty(value)))
                {
                    found.Add(item);
                }
            }
            return found;
        }

        // R387. location_plan.control_fit_parts and numbered_otherwise: the marks by their shape, the similarity start, the refine of R181 on
        // the faces with one label each, every label by the control fit and the faces it numbers otherwise than the schedule fit, with why
        private static ControlCheck Check(IReadOnlyList<PlanLabel> labels, IReadOnlyList<Row> rows, IReadOnlyList<PageWord> words, IEnumerable<PageCurve> curves,
            IReadOnlyDictionary<string, PlanFace> faces, FaceIndex index, LocationPlanSettings settings)
        {
            List<ControlText> texts;
            List<ControlMark> marks;
            RobustResult refine;
            PageFit start;
            int refineFaces;
            try
            {
                texts = ControlTexts(words);
                marks = ControlMarks(curves, texts, settings);
                start = SimilarityFit(marks.Select(mark => new[] { mark.X, mark.Y }).ToList(), marks.Select(mark => new[] { mark.East, mark.North }).ToList());
                List<string> homes = labels.Select(label => { double[] at = start.Apply(label.X, label.Y); return index.FaceOf(at[0], at[1]); }).ToList();
                var held = homes.Where(item => item != null).GroupBy(item => item).ToDictionary(group => group.Key, group => group.Count());
                var taken = homes.Select((face, place) => new { face, place }).Where(item => item.face != null && held[item.face] == 1).OrderBy(item => PyInt(item.face)).ToList();
                if (taken.Count < 3)
                {
                    throw new InvalidDataException("the control point fit puts a single label on " + taken.Count + " faces, the refine needs three (R387)");
                }
                refineFaces = taken.Count;
                refine = RobustFit(taken.Select(item => new[] { labels[item.place].X, labels[item.place].Y }).ToList(),
                    taken.Select(item => new[] { faces[item.face].CentroidX, faces[item.face].CentroidY }).ToList(), settings.FitDropFactor, settings.FitRounds, settings.FitFloorM);
            }
            catch (InvalidDataException failure)
            {
                // R387. A check that cannot be made is an issue with why, the numbering of the schedule fit goes on as it is
                return new ControlCheck { Failed = failure.Message, Listed = new List<ListedFace>() };
            }
            var controlSite = labels.Select(label => refine.Fit.Apply(label.X, label.Y)).ToList();
            List<string> controlFaces = controlSite.Select(at => index.FaceOf(at[0], at[1])).ToList();
            List<ListedFace> listed = NumberedOtherwise(rows, controlFaces, controlSite, faces);
            var unitNumbers = new HashSet<string>(rows.Where(row => row.Kind == "clientCode").Select(row => row.Number));
            Func<PageFit, double[]> gaps = fit => marks.Select(mark => { double[] at = fit.Apply(mark.X, mark.Y); return Hypot(at[0] - mark.East, at[1] - mark.North); }).ToArray();
            return new ControlCheck
            {
                ControlTexts = texts.Count,
                Marks = marks.Count,
                MarkGapStartM = gaps(start),
                MarkGapRefinedM = gaps(refine.Fit),
                RefineFaces = refineFaces,
                RefineKept = refine.Keep.Count,
                RefineResidualKeptM = Stats(refine.Keep.Select(i => refine.Residual[i])),
                AgainstScheduleM = Stats(rows.Select((row, index) => Hypot(controlSite[index][0] - row.East, controlSite[index][1] - row.North))),
                LabelsOnAFaceSchedule = rows.Count(row => row.Face != null),
                LabelsOnAFaceControl = controlFaces.Count(face => face != null),
                UnitFacesNumberedOtherwise = listed.Count(item => item.Schedule.Concat(item.Control).Any(unitNumbers.Contains)),
                Listed = listed,
            };
        }

        // R387. location_plan.numbered_otherwise: every face the control fit numbers otherwise than the schedule fit, with the numbers of each
        // fit and why, every label the two fits put apart, its face by each, its distance from the edge of the face it is in and how far
        // apart the two fits put it. It numbers nothing
        public static List<ListedFace> NumberedOtherwise(IReadOnlyList<Row> rows, IReadOnlyList<string> controlFaces, IReadOnlyList<double[]> controlSite, IReadOnlyDictionary<string, PlanFace> faces)
        {
            var bySchedule = new Dictionary<string, List<string>>();
            var byControl = new Dictionary<string, List<string>>();
            var moved = new Dictionary<string, List<Why>>();
            for (int index = 0; index < rows.Count; index++)
            {
                Row row = rows[index];
                string face = controlFaces[index];
                if (row.Face != null)
                {
                    Add(bySchedule, row.Face, row.Number);
                }
                if (face != null)
                {
                    Add(byControl, face, row.Number);
                }
                if (row.Face != face)
                {
                    string edgeFace = row.Face ?? face;
                    double[] point = row.Face != null ? new[] { row.East, row.North } : controlSite[index];
                    double? edge = edgeFace != null ? Rounding.Fixed(EdgeDistance(faces[edgeFace], point[0], point[1]), 3) : (double?)null;
                    double gap = Rounding.Fixed(Hypot(row.East - controlSite[index][0], row.North - controlSite[index][1]), 3);
                    var why = new Why
                    {
                        Number = row.Number,
                        ScheduleFace = row.Face,
                        ControlFace = face,
                        EdgeM = edge,
                        GapM = gap,
                        Text = string.Format(CultureInfo.InvariantCulture, "label {0} on face {1} by the schedule fit and on face {2} by the control fit, {3} m from the edge of face {4}, the fits {5} m apart at it",
                            row.Number, row.Face ?? "none", face ?? "none", edge.HasValue ? SheetRows.PyRepr(edge.Value) : "None", edgeFace ?? "none", SheetRows.PyRepr(gap)),
                    };
                    foreach (string at in new[] { row.Face, face }.Where(item => item != null).Distinct())
                    {
                        if (!moved.ContainsKey(at))
                        {
                            moved[at] = new List<Why>();
                        }
                        moved[at].Add(why);
                    }
                }
            }
            var found = new List<ListedFace>();
            foreach (string face in bySchedule.Keys.Union(byControl.Keys).OrderBy(PyInt))
            {
                List<string> mine = (bySchedule.TryGetValue(face, out List<string> a) ? a : new List<string>()).OrderBy(PyInt).ToList();
                List<string> theirs = (byControl.TryGetValue(face, out List<string> b) ? b : new List<string>()).OrderBy(PyInt).ToList();
                if (!mine.SequenceEqual(theirs))
                {
                    found.Add(new ListedFace
                    {
                        Face = face,
                        Schedule = mine,
                        Control = theirs,
                        Why = (moved.TryGetValue(face, out List<Why> why) ? why : new List<Why>()).OrderBy(item => PyInt(item.Number)).ToList(),
                    });
                }
            }
            return found;
        }

        private static void Add(Dictionary<string, List<string>> map, string key, string value)
        {
            if (!map.ContainsKey(key))
            {
                map[key] = new List<string>();
            }
            map[key].Add(value);
        }

        // R351. The steps of the parity harness: locationLabels per label by its number, a number met again number#n, locationFit per
        // neighbourhood schedule, controlCheck the one control check and controlFaces per face numbered otherwise
        public const string LabelStepName = "locationLabels";
        public const string FitStepName = "locationFit";
        public const string CheckStepName = "controlCheck";
        public const string FacesStepName = "controlFaces";

        public static Dictionary<string, Dictionary<string, object>> LabelStep(Result result)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            var counts = new Dictionary<string, int>();
            foreach (Row row in result.Rows)
            {
                int count;
                counts[row.Number] = counts.TryGetValue(row.Number, out count) ? count + 1 : 1;
                string key = counts[row.Number] == 1 ? row.Number : row.Number + "#" + counts[row.Number].ToString(CultureInfo.InvariantCulture);
                found[key] = new Dictionary<string, object>
                {
                    ["number"] = row.Number, ["code"] = row.Code, ["kind"] = row.Kind, ["face"] = row.Face, ["offset"] = row.Offset, ["group"] = row.Group,
                    ["page"] = new[] { row.PageX, row.PageY }, ["site"] = new[] { row.East, row.North }, ["newCode"] = row.NewCode, ["rev9"] = row.Rev9,
                    ["nbh"] = row.Nbh, ["zone"] = row.Zone,
                };
            }
            return found;
        }

        public static Dictionary<string, Dictionary<string, object>> FitStep(Result result)
        {
            return result.Fits.ToDictionary(part => part.Neighbourhood, part => new Dictionary<string, object>
            {
                ["role"] = part.Role, ["points"] = part.Used.Count, ["used"] = new JArray(part.Used), ["kept"] = part.Kept == null ? null : new JArray(part.Kept),
                ["residualKeptM"] = part.ResidualKept, ["residualAllM"] = part.ResidualAll, ["metresPerPoint"] = part.MetresPerPoint,
            });
        }

        public static Dictionary<string, Dictionary<string, object>> CheckStep(Result result)
        {
            ControlCheck check = result.Control;
            bool made = check.Failed == null;
            return new Dictionary<string, Dictionary<string, object>>
            {
                ["control"] = new Dictionary<string, object>
                {
                    ["failed"] = check.Failed,
                    ["controlTexts"] = made ? (object)check.ControlTexts : null, ["marks"] = made ? (object)check.Marks : null, ["markGapStartM"] = check.MarkGapStartM,
                    ["markGapRefinedM"] = check.MarkGapRefinedM, ["refineFaces"] = made ? (object)check.RefineFaces : null, ["refineKept"] = made ? (object)check.RefineKept : null,
                    ["refineResidualKeptM"] = check.RefineResidualKeptM, ["againstScheduleM"] = check.AgainstScheduleM,
                    ["labelsOnAFaceSchedule"] = made ? (object)check.LabelsOnAFaceSchedule : null, ["labelsOnAFaceControl"] = made ? (object)check.LabelsOnAFaceControl : null,
                    ["facesNumberedOtherwise"] = made ? (object)check.Listed.Count : null, ["unitFacesNumberedOtherwise"] = made ? (object)check.UnitFacesNumberedOtherwise : null,
                },
            };
        }

        public static Dictionary<string, Dictionary<string, object>> FacesStep(Result result)
        {
            return result.Control.Listed.ToDictionary(item => item.Face, item => new Dictionary<string, object>
            {
                ["schedule"] = new JArray(item.Schedule), ["control"] = new JArray(item.Control), ["why"] = WhyToken(item.Why),
            });
        }

        public static JArray WhyToken(IEnumerable<Why> why)
        {
            return new JArray(why.Select(item => new JObject
            {
                ["number"] = item.Number, ["scheduleFace"] = item.ScheduleFace, ["controlFace"] = item.ControlFace,
                ["edgeM"] = item.EdgeM.HasValue ? new JValue(item.EdgeM.Value) : JValue.CreateNull(), ["gapM"] = item.GapM, ["text"] = item.Text,
            }));
        }
    }

    // R351, R9. The official numbers of a neighbourhood schedule as data_book.schedule_rows reads them with excel_reader.read_sheet and
    // levels_proof.read_schedule: the header row is the first row with a cell holding Plot No., any case, the rows under it up to the first
    // blank row or total row, each column by its header stripped of blanks and stars in lower case, a row whose FFL, SSL or El @ Entry is
    // no number refused, and the number of a plot cell its first digits
    public static class ScheduleRows
    {
        private const string Marker = "plot no.";
        private static readonly string[] Wanted = { "plot no.", "unit", "ffl", "ssl", "el @ entry", "steps needed" };
        private static readonly Regex Leading = new Regex(@"^\s*([0-9]+)", RegexOptions.CultureInvariant);

        public static HashSet<string> Officials(IReadOnlyList<object[]> rows, string file)
        {
            int headerIndex = -1;
            for (int index = 0; index < rows.Count && headerIndex < 0; index++)
            {
                if (rows[index].Any(cell => !Blank(cell) && SheetRows.PyText(cell).ToLowerInvariant().Contains(Marker)))
                {
                    headerIndex = index;
                }
            }
            if (headerIndex < 0)
            {
                throw new InvalidDataException(file + ": no row containing 'Plot No.' (R9)");
            }
            object[] header = rows[headerIndex];
            List<string> headers = Unique(header);
            int plotColumn = Enumerable.Range(0, header.Length).First(index => !Blank(header[index]) && SheetRows.PyText(header[index]).ToLowerInvariant().Contains(Marker));
            var names = new Dictionary<string, int>();
            for (int index = 0; index < headers.Count; index++)
            {
                names[SheetRows.PyStrip(SheetRows.PyStrip(headers[index]).TrimEnd('*')).ToLowerInvariant()] = index;
            }
            List<string> missing = Wanted.Where(name => !names.ContainsKey(name)).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidDataException(file + " has no column " + string.Join(", ", missing) + " (R9)");
            }
            var found = new HashSet<string>();
            for (int index = headerIndex + 1; index < rows.Count; index++)
            {
                object[] row = rows[index];
                if (row.All(Blank) || IsTotal(row, plotColumn))
                {
                    break;
                }
                foreach (string key in new[] { "ffl", "ssl", "el @ entry" })
                {
                    int at = names[key];
                    object value = at < row.Length ? row[at] : null;
                    if (!(value is double) && !(value is long))
                    {
                        throw new InvalidDataException(file + " row " + (index + 1) + " has no number in column " + key + " (R9)");
                    }
                }
                int plotAt = names[Marker];
                Match number = Leading.Match(SheetRows.PyText(plotAt < row.Length ? row[plotAt] : null));
                if (number.Success)
                {
                    found.Add(long.Parse(number.Groups[1].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                }
            }
            return found;
        }

        private static bool Blank(object cell)
        {
            return cell == null || SheetRows.PyStrip(SheetRows.PyText(cell)).Length == 0;
        }

        private static bool IsTotal(object[] row, int plotColumn)
        {
            if (row.Any(cell => cell is string text && SheetRows.PyStrip(text).ToLowerInvariant().StartsWith("total", StringComparison.Ordinal)))
            {
                return true;
            }
            return plotColumn < row.Length ? Blank(row[plotColumn]) : true;
        }

        // excel_reader.unique_headers: each header stripped, col_<letter> for an empty one, a header met again <header>_<n>
        private static List<string> Unique(object[] header)
        {
            var seen = new Dictionary<string, int>();
            var found = new List<string>();
            for (int index = 0; index < header.Length; index++)
            {
                string text = header[index] == null ? "" : SheetRows.PyStrip(SheetRows.PyText(header[index]));
                if (text.Length == 0)
                {
                    text = "col_" + Letter(index + 1);
                }
                int count;
                seen[text] = seen.TryGetValue(text, out count) ? count + 1 : 1;
                found.Add(seen[text] == 1 ? text : text + "_" + seen[text].ToString(CultureInfo.InvariantCulture));
            }
            return found;
        }

        private static string Letter(int column)
        {
            string text = "";
            while (column > 0)
            {
                int rest = (column - 1) % 26;
                text = (char)('A' + rest) + text;
                column = (column - 1) / 26;
            }
            return text;
        }
    }
}
