using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R630 to R633. What a project's own workbooks say about its villa type codes and what a person confirmed of it. Every search goes by what a
    // sheet holds, no name of a sheet or column is looked for, and a proposal changes no plot
    public static partial class DataCheckLogic
    {
        private sealed class CodeTable
        {
            public string FoundIn;
            public string Workbook;
            public string Sheet;
            public List<KeyValuePair<string, string>> Pairs;
            public int Renames;
            public int Prototypes;
            public string Direction;
            public bool Unsure;
            public bool TooLarge;
        }

        private sealed class TypologyCandidate
        {
            public string Workbook;
            public string Sheet;
            public string Column;
            public int Plots;
            public int Whole;
            public int Rows;
            public double WholeShare;
            public int Order;
            public int ListedTwice;
            public bool TooLarge;
            public int Codes;
            public Dictionary<Plot, string> Cells;
        }

        private sealed class HandCount
        {
            public int Mirrored;
            public int NotMirrored;
        }

        // A column of a sheet a person named, with the cell of each plot, or why it cannot be used
        private sealed class ColumnFound
        {
            public string Why;
            public Dictionary<Plot, string> Cells;
            public int ListedTwice;
            public HashSet<Plot> TwoValues = new HashSet<Plot>();
        }

        // The column of a sheet that lists the plots by their number, -1 when no column holds usableShare of them. A sheet that lists the
        // plots is no table of codes, and it is the only sheet a typology column can lie on
        private static int KeyColumn(DataCheckSettings settings, ScanSheet sheet, Dictionary<string, Plot> byNumber, int plots)
        {
            int key = -1, hits = 0;
            for (int column = 0; column < sheet.Headers.Count; column++)
            {
                int count = sheet.Rows.Where(row => column < row.Count && byNumber.ContainsKey(row[column].Trim())).Select(row => row[column].Trim()).Distinct(StringComparer.Ordinal).Count();
                if (count > hits)
                {
                    hits = count;
                    key = column;
                }
            }
            return key < 0 || hits < settings.UsableShare * plots ? -1 : key;
        }

        // R632. A name two columns of one sheet carry names neither, so such a column is never proposed and never taken
        private static bool NameTwice(ScanSheet sheet, int column)
        {
            return sheet.Headers.Count(header => header == sheet.Headers[column]) > 1;
        }

        // The part of a code before its first character of the split, so the base of a code with a qualifier can be held against the words
        // of a drawing. Direction only, the table keeps each code as it is written
        private static string BaseOf(DataCheckSettings settings, string code)
        {
            return settings.Scan.TokenSplit.Split(code).FirstOrDefault(part => part.Length > 0) ?? "";
        }

        // Every word the drawings use in a text or a block name, to tell which side of a table the drawings still speak
        private static HashSet<string> DrawingWords(DataCheckSettings settings, DataCheckInput input)
        {
            var words = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject read in input.CadReads)
            {
                IEnumerable<string> texts = ((JArray)read["texts"]).Select(item => Str(item["text"])).Concat(((JArray)read["inserts"]).Select(item => Str(item["block"])));
                foreach (string text in texts)
                {
                    words.UnionWith(settings.Scan.TokenSplit.Split(text).Where(part => part.Length > 0));
                }
            }
            return words;
        }

        // R630. The tables of old code to new code in the workbooks: two columns side by side on a sheet that lists no plots, whose cells both
        // have the shape of a code, at least two pairs and one that renames, each pair written once, and one side holding usableShare of the
        // prototype codes. A table past codeTableMaxPairs is found and said too large, never proposed. Best first: a table whose old side the
        // drawings hold, then the most prototype codes covered, then the most renames, then the most pairs, then the order of the files
        private static List<CodeTable> CodeTables(DataCheckSettings settings, DataCheckInput input, HashSet<string> prototypes, List<Plot> plots, List<CodeTable> tooLarge)
        {
            var found = new List<CodeTable>();
            if (prototypes.Count == 0)
            {
                return found;
            }
            var byNumber = new Dictionary<string, Plot>(StringComparer.Ordinal);
            foreach (Plot plot in plots)
            {
                byNumber[plot.Number] = plot;
            }
            HashSet<string> words = DrawingWords(settings, input);
            foreach (ScanWorkbook book in input.Excels)
            {
                foreach (ScanSheet sheet in book.Sheets)
                {
                    if (plots.Count > 0 && KeyColumn(settings, sheet, byNumber, plots.Count) >= 0)
                    {
                        continue;
                    }
                    for (int left = 0; left + 1 < sheet.Headers.Count; left++)
                    {
                        CodeTable table = TableAt(settings, book, sheet, left, prototypes, words);
                        if (table != null)
                        {
                            (table.TooLarge ? tooLarge : found).Add(table);
                        }
                    }
                }
            }
            return found.OrderBy(table => table.Unsure).ThenByDescending(table => table.Prototypes).ThenByDescending(table => table.Renames).ThenByDescending(table => table.Pairs.Count).ToList();
        }

        private static CodeTable TableAt(DataCheckSettings settings, ScanWorkbook book, ScanSheet sheet, int left, HashSet<string> prototypes, HashSet<string> words)
        {
            var rows = new List<KeyValuePair<string, string>>();
            // A table with no header row has its first pair in the row the sheet took for its header
            IEnumerable<IList<string>> cells = new IList<string>[] { sheet.Headers }.Concat(sheet.Rows);
            foreach (IList<string> row in cells)
            {
                string a = left < row.Count ? row[left].Trim() : "";
                string b = left + 1 < row.Count ? row[left + 1].Trim() : "";
                if (settings.CodeShape.IsMatch(a) && settings.CodeShape.IsMatch(b))
                {
                    rows.Add(new KeyValuePair<string, string>(a, b));
                }
            }
            List<KeyValuePair<string, string>> pairs = rows.Distinct().ToList();
            if (pairs.Count < 2 || pairs.Count < settings.StrongShare * rows.Count || pairs.All(pair => pair.Key == pair.Value))
            {
                return null;
            }
            int leftSide = pairs.Select(pair => pair.Key).Distinct(StringComparer.Ordinal).Count(prototypes.Contains);
            int rightSide = pairs.Select(pair => pair.Value).Distinct(StringComparer.Ordinal).Count(prototypes.Contains);
            int covered = Math.Max(leftSide, rightSide);
            if (covered < settings.UsableShare * prototypes.Count)
            {
                return null;
            }
            // A pair that keeps its name says nothing of which side is old, only the renames do
            List<KeyValuePair<string, string>> renames = pairs.Where(pair => pair.Key != pair.Value).ToList();
            int leftUsed = renames.Count(pair => words.Contains(BaseOf(settings, pair.Key)));
            int rightUsed = renames.Count(pair => words.Contains(BaseOf(settings, pair.Value)));
            bool turned;
            string direction;
            if (leftUsed != rightUsed)
            {
                turned = rightUsed > leftUsed;
                direction = "the drawings use the old side";
            }
            else if (leftSide != rightSide)
            {
                turned = leftSide > rightSide;
                direction = "the prototype files name the new side";
            }
            else
            {
                turned = false;
                direction = Guess;
            }
            int oldHeld = Math.Max(leftUsed, rightUsed);
            return new CodeTable
            {
                Workbook = book.Name, Sheet = sheet.Name,
                FoundIn = book.Name + "|" + sheet.Name + "|" + sheet.Headers[turned ? left + 1 : left] + "|" + sheet.Headers[turned ? left : left + 1],
                Pairs = turned ? pairs.Select(pair => new KeyValuePair<string, string>(pair.Value, pair.Key)).ToList() : pairs,
                Renames = renames.Count,
                Prototypes = covered,
                Direction = direction,
                Unsure = oldHeld == 0,
                TooLarge = pairs.Count > settings.CodeTableMaxPairs,
            };
        }

        private const string Guess = "a guess, read left to right";

        // R631. The columns that give each plot a villa type, on a sheet that lists the plots: a cell with the shape of a code for usableShare
        // of the plots, two codes or more, as one value on every row says nothing of a plot, codes that each run across the groups as a type does
        // and a block or area name does not, and that say something the drawing does not, since a column that repeats the drawing's codes
        // settles nothing. A column past codeTableMaxPairs is found and said too large. Best first: the most plots a prototype file can be found
        // for, then the column most of whose rows hold one. A column that keeps its place by coming later wins nothing, see TypologyProposal
        private static List<TypologyCandidate> TypologyColumns(DataCheckSettings settings, DataCheckInput input, List<Plot> plots, HashSet<string> prototypes, List<TypologyCandidate> tooLarge)
        {
            var found = new List<TypologyCandidate>();
            if (plots.Count == 0 || plots.Any(plot => plot.Group == null))
            {
                return found;
            }
            var byNumber = new Dictionary<string, Plot>(StringComparer.Ordinal);
            foreach (Plot plot in plots.Where(plot => plot.NumberFlag == null))
            {
                byNumber[plot.Number] = plot;
            }
            int order = 0;
            foreach (ScanWorkbook book in input.Excels)
            {
                foreach (ScanSheet sheet in book.Sheets)
                {
                    int key = KeyColumn(settings, sheet, byNumber, plots.Count);
                    if (key < 0)
                    {
                        continue;
                    }
                    for (int column = 0; column < sheet.Headers.Count; column++)
                    {
                        order++;
                        if (column == key || NameTwice(sheet, column))
                        {
                            continue;
                        }
                        Dictionary<Plot, string> cells = ColumnCells(sheet, key, column, byNumber, out int listedTwice, out HashSet<Plot> _);
                        TypologyCandidate candidate = Typology(settings, plots.Count, cells, prototypes);
                        if (candidate != null)
                        {
                            candidate.Workbook = book.Name;
                            candidate.Sheet = sheet.Name;
                            candidate.Column = sheet.Headers[column];
                            candidate.Order = order;
                            candidate.ListedTwice = listedTwice;
                            (candidate.TooLarge ? tooLarge : found).Add(candidate);
                        }
                    }
                }
            }
            return found.OrderByDescending(item => item.Whole).ThenByDescending(item => item.WholeShare).ThenBy(item => item.Order).ToList();
        }

        // R631. The first column, unless another of the same rank gives some plot another code, then the person picks and tie says it. Columns of one
        // rank that hold the same code on every plot are one answer under two names, so the first in reading order stands for them and nothing
        // rides on which
        private static TypologyCandidate TypologyProposal(List<TypologyCandidate> columns, out string tie)
        {
            tie = null;
            if (columns.Count == 0)
            {
                return null;
            }
            TypologyCandidate first = columns[0];
            List<TypologyCandidate> level = columns.Where(item => item.Whole == first.Whole && item.Rows == first.Rows).ToList();
            if (level.Count == 1)
            {
                return first;
            }
            if (level.Any(item => !SameCells(item.Cells, first.Cells)))
            {
                tie = Count(level.Count, "column", "columns") + " tie and give some plots other codes, none is proposed, pick one";
                return null;
            }
            tie = Count(level.Count, "column", "columns") + " tie and hold the same code on every plot, the first is proposed";
            return first;
        }

        private static bool SameCells(Dictionary<Plot, string> one, Dictionary<Plot, string> other)
        {
            return one.Count == other.Count && one.All(item => other.TryGetValue(item.Key, out string cell) && cell == item.Value);
        }

        // The cell of each plot the column gives, the empty text for a row with no value, the first row of a plot if it is listed twice. listedTwice
        // counts the plots with more than one row, twoValues those whose rows give two values, which the first row cannot speak for
        private static Dictionary<Plot, string> ColumnCells(ScanSheet sheet, int key, int column, Dictionary<string, Plot> byNumber, out int listedTwice, out HashSet<Plot> twoValues)
        {
            var cells = new Dictionary<Plot, string>();
            var again = new HashSet<Plot>();
            twoValues = new HashSet<Plot>();
            foreach (List<string> row in sheet.Rows)
            {
                if (key < row.Count && byNumber.TryGetValue(row[key].Trim(), out Plot plot))
                {
                    string cell = column < row.Count ? row[column].Trim() : "";
                    if (!cells.TryGetValue(plot, out string seen))
                    {
                        cells[plot] = cell;
                        continue;
                    }
                    again.Add(plot);
                    if (seen != cell)
                    {
                        twoValues.Add(plot);
                    }
                }
            }
            listedTwice = again.Count;
            return cells;
        }

        private static Dictionary<Plot, string> ColumnCells(ScanSheet sheet, int key, int column, Dictionary<string, Plot> byNumber)
        {
            return ColumnCells(sheet, key, column, byNumber, out int _, out HashSet<Plot> _);
        }

        private static TypologyCandidate Typology(DataCheckSettings settings, int plotCount, Dictionary<Plot, string> cells, HashSet<string> prototypes)
        {
            List<KeyValuePair<Plot, string>> coded = cells.Where(item => settings.CodeShape.IsMatch(item.Value)).ToList();
            List<IGrouping<string, KeyValuePair<Plot, string>>> codes = coded.GroupBy(item => item.Value, StringComparer.Ordinal).ToList();
            if (coded.Count < settings.UsableShare * plotCount || codes.Count < 2)
            {
                return null;
            }
            int finest = settings.GroupGapsM.Count - 1;
            int acrossGroups = codes.Where(code => code.Select(item => item.Key.Group[finest]).Distinct().Count() >= 2).Sum(code => code.Count());
            if (acrossGroups < settings.StrongShare * coded.Count)
            {
                return null;
            }
            List<KeyValuePair<Plot, string>> withDrawing = coded.Where(item => item.Key.DrawingCode != null).ToList();
            if (withDrawing.Count > 0 && withDrawing.Count(item => item.Key.DrawingCode == item.Value) >= settings.StrongShare * withDrawing.Count)
            {
                return null;
            }
            int whole = coded.Count(item => prototypes.Contains(item.Value));
            return new TypologyCandidate
            {
                Plots = coded.Count, Whole = whole, Rows = cells.Count, WholeShare = cells.Count == 0 ? 0 : (double)whole / cells.Count, Cells = cells, Codes = codes.Count,
                TooLarge = codes.Count > settings.CodeTableMaxPairs,
            };
        }

        // The column a person named: why when the sheet or the name is gone, two columns carry the name or it does not join to the plots, else the
        // cell of each plot
        private static ColumnFound FindColumn(DataCheckSettings settings, DataCheckInput input, List<Plot> plots, ConfirmedColumn column)
        {
            var result = new ColumnFound();
            ScanSheet sheet = input.Excels.Where(book => column.Workbook == null || book.Name == column.Workbook).SelectMany(book => book.Sheets).FirstOrDefault(item => item.Name == column.Sheet && item.Headers.Contains(column.Column));
            if (sheet == null)
            {
                result.Why = "it is no longer in the folder";
                return result;
            }
            int at = sheet.Headers.IndexOf(column.Column);
            if (NameTwice(sheet, at))
            {
                result.Why = "two columns of that sheet carry that name";
                return result;
            }
            var byNumber = new Dictionary<string, Plot>(StringComparer.Ordinal);
            foreach (Plot plot in plots.Where(plot => plot.NumberFlag == null))
            {
                byNumber[plot.Number] = plot;
            }
            int key = KeyColumn(settings, sheet, byNumber, plots.Count);
            if (key < 0)
            {
                result.Why = "it does not join to the plots of this folder";
                return result;
            }
            result.Cells = ColumnCells(sheet, key, at, byNumber, out int listedTwice, out HashSet<Plot> twoValues);
            result.ListedTwice = listedTwice;
            result.TwoValues = twoValues;
            return result;
        }

        // R631, R632. The confirmed column gives each plot its code, what the drawing said no longer decides but is kept and counted. A plot the
        // column does not list or lists with no code has no villa type and says why, a stale code of the drawing is never kept for it. A plot whose
        // label holds two numbers has no number to join by, so it keeps what the drawing gives. Gives the reason when it cannot be used, then no plot
        // changes
        private static string ApplyColumn(DataCheckSettings settings, DataCheckInput input, List<Plot> plots, ConfirmedColumn column, out int listedTwice)
        {
            listedTwice = 0;
            ColumnFound found = FindColumn(settings, input, plots, column);
            if (found.Why != null)
            {
                return found.Why;
            }
            if (found.Cells.Values.Where(cell => cell.Length > 0).Distinct(StringComparer.Ordinal).Count() < 2)
            {
                return "it holds one value on every row, so it says nothing of a plot";
            }
            listedTwice = found.ListedTwice;
            foreach (Plot plot in plots.Where(plot => plot.NumberFlag == null))
            {
                plot.VillaDispute = null;
                plot.OtherCodes.Clear();
                plot.FromColumn = true;
                if (found.TwoValues.Contains(plot))
                {
                    plot.ListedTwoWays = "the sheet lists this plot twice with two values, the first is taken";
                }
                if (!found.Cells.TryGetValue(plot, out string cell) || cell.Length == 0)
                {
                    plot.Villa = null;
                    plot.VillaWhy = "the confirmed typology column has no code for this plot";
                }
                else if (settings.CodeShape.IsMatch(cell))
                {
                    plot.Villa = cell;
                    plot.VillaWhy = null;
                }
                else
                {
                    plot.Villa = null;
                    plot.VillaWhy = "the confirmed typology column gives this plot no villa type code: " + cell;
                }
            }
            return null;
        }

        // R632. The codes of a map that are both old and new: a code that takes the place of another and is renamed itself, as a chain or a swap. A
        // plot of such a code cannot be told to be the old or the new one
        private static List<string> BothOldAndNew(List<KeyValuePair<string, string>> map)
        {
            var taken = new HashSet<string>(map.Where(pair => pair.Key != pair.Value).Select(pair => pair.Value), StringComparer.Ordinal);
            return map.Where(pair => pair.Key != pair.Value && taken.Contains(pair.Key)).Select(pair => pair.Key).Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToList();
        }

        // R632. The confirmed map turns the code of each plot once. A code the map does not rename stands. A code of a chain or a swap, or one that
        // would land on such a code, is no answer and neither is an old code with two new codes: the plot keeps what it read and says so, never a
        // pick. Gives the code of each plot's old name for the disagreement count of the column
        private static void ApplyMap(List<Plot> plots, List<KeyValuePair<string, string>> map)
        {
            var both = new HashSet<string>(BothOldAndNew(map), StringComparer.Ordinal);
            Func<string, List<string>> outs = code => map.Where(pair => pair.Key == code).Select(pair => pair.Value).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
            Func<string, string> turned = code =>
            {
                List<string> now = outs(code);
                return now.Count == 1 && !both.Contains(code) && !both.Contains(now[0]) ? now[0] : code;
            };
            foreach (Plot plot in plots.Where(plot => plot.Villa != null))
            {
                string read = plot.Villa;
                plot.CodeRead = read;
                List<string> now = outs(read);
                if (both.Contains(read) || now.Any(both.Contains))
                {
                    string code = both.Contains(read) ? read : now.First(both.Contains);
                    plot.MapDoubt = "the code map holds " + code + " as both an old and a new code, so this plot keeps " + read;
                }
                else if (now.Count > 1)
                {
                    plot.MapDoubt = "the code map gives " + read + " " + now.Count + " new codes: " + string.Join(", ", now);
                }
                else
                {
                    plot.Villa = turned(read);
                }
                // Another source that only gave the other name of this code does not dispute it
                plot.OtherCodes.RemoveAll(code => code == plot.Villa || turned(code) == plot.Villa);
                if (plot.OtherCodes.Count == 0)
                {
                    plot.VillaDispute = null;
                }
                plot.BlockCodes = new HashSet<string>(plot.BlockCodes.Select(turned), StringComparer.Ordinal);
                if (plot.DrawingCode != null)
                {
                    plot.DrawingCode = turned(plot.DrawingCode);
                }
            }
        }

        // R633. The guess of a prototype's hand: the hand its blocks are drawn in on at least handGuessShare of the plots that hold one, unmirrored
        // read as right, none when no such share or no block gives the code. It is a guess the window shows, no prototype file is opened here
        private static Dictionary<string, string> HandGuesses(DataCheckSettings settings, HashSet<string> prototypes, Dictionary<string, HandCount> counts)
        {
            var guesses = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string code in prototypes.OrderBy(item => item, StringComparer.Ordinal))
            {
                string guess = null;
                if (counts.TryGetValue(code, out HandCount count))
                {
                    double all = count.Mirrored + count.NotMirrored;
                    guess = count.NotMirrored / all >= settings.HandGuessShare ? ConfirmedProfile.Right : count.Mirrored / all >= settings.HandGuessShare ? ConfirmedProfile.Left : null;
                }
                guesses[code] = guess;
            }
            return guesses;
        }

        // R633. The mirror of a plot is the sign of its block turned over when the prototype is drawn in the left hand. A hand only a person gave
        // lifts the mirror from weak, and only when the block in the plot is of the plot's own type, the hand of another type's block says
        // little about the prototype this plot links
        private static void ApplyHands(List<Plot> plots, Dictionary<string, string> hands)
        {
            foreach (Plot plot in plots.Where(plot => plot.Mirror != null))
            {
                string hand = null;
                if (plot.Villa != null && !hands.TryGetValue(plot.Villa, out hand) && plot.CodeRead != null)
                {
                    hands.TryGetValue(plot.CodeRead, out hand);
                }
                if (hand == null)
                {
                    continue;
                }
                plot.HandKnown = true;
                plot.Mirror = hand == ConfirmedProfile.Left ? !plot.Mirror.Value : plot.Mirror.Value;
                plot.BlockOfOtherType = !plot.BlockCodes.Contains(plot.Villa);
            }
        }

        // R632. A confirmed code no plot holds did nothing, which a person is told, a typing slip reads as a map that worked otherwise
        private static List<string> NotHeld(IEnumerable<string> codes, List<Plot> plots)
        {
            var held = new HashSet<string>(plots.Select(plot => plot.Villa).Concat(plots.Select(plot => plot.CodeRead)).Where(code => code != null), StringComparer.Ordinal);
            return codes.Distinct(StringComparer.Ordinal).Where(code => !held.Contains(code)).OrderBy(code => code, StringComparer.Ordinal).ToList();
        }

        // R635. The plot table of button 2 from plots.json, in the fields the later buttons read. Every numbered plot is a row, a plot with no type
        // too, so nothing read is hidden from Review data. The area is the confirmed column's, else the group of level 1. A plot whose label holds two
        // numbers has no number to be known by and gets no row, plots.json says why
        public static JObject PlotTable(JObject plots)
        {
            bool fromColumn = Str(plots["areaFrom"]) == AreaFromColumn;
            var rows = new JArray();
            foreach (JObject plot in ((JArray)plots["plots"]).OfType<JObject>().Where(row => Str(row["number"]).Length > 0))
            {
                var groups = plot["groups"] as JArray;
                JToken area = fromColumn ? plot["area"] : groups != null && groups.Count > 0 ? groups[0] : null;
                rows.Add(new JObject
                {
                    ["baseplanLabel"] = plot["number"], ["officialNumber"] = plot["number"], ["locationPlanNumber"] = null,
                    ["nbhId"] = area, ["typologyRev9"] = plot["villaType"], ["footprintMirrored"] = plot["mirror"], ["faceAreaM2"] = plot["areaM2"],
                    ["source"] = plot["source"], ["missing"] = plot["missing"], ["disputed"] = plot["disputed"], ["weak"] = plot["weak"],
                });
            }
            return new JObject { ["plots"] = rows };
        }

        public const string AreaFromColumn = "column";
        public const string AreaFromDistance = "distance";

        private static JArray PairRows(IEnumerable<KeyValuePair<string, string>> pairs, string source)
        {
            return new JArray(pairs.Select(pair => new JObject { ["old"] = pair.Key, ["new"] = pair.Value, ["source"] = source }));
        }

        private static JObject ColumnRow(TypologyCandidate item)
        {
            return new JObject { ["workbook"] = item.Workbook, ["sheet"] = item.Sheet, ["column"] = item.Column, ["plots"] = item.Plots, ["prototypePlots"] = item.Whole, ["listedTwice"] = item.ListedTwice, ["source"] = Computed };
        }

        private static JObject LargeTable(CodeTable table)
        {
            return new JObject { ["foundIn"] = table.FoundIn, ["pairs"] = table.Pairs.Count };
        }

        // R630 to R635. The blocks of project_profile.json beside the levels: what the tool proposes and what a person confirmed, each with its
        // source. Empty when nothing was read, so the window has one shape to read
        private static void AddProposals(JObject profile, Findings f)
        {
            ConfirmedProfile confirmed = f.Input.Confirmed;
            List<CodeTable> tables = f.Tables ?? new List<CodeTable>();
            bool mapGiven = confirmed != null && confirmed.CodeMap.Count > 0;
            profile["codeMap"] = new JObject
            {
                ["proposed"] = tables.Count == 0 ? new JArray() : PairRows(tables[0].Pairs, Computed),
                ["confirmed"] = mapGiven ? PairRows(confirmed.CodeMap, ConfirmedProfile.PersonEntered) : new JArray(),
                ["source"] = mapGiven ? ConfirmedProfile.PersonEntered : tables.Count > 0 ? Computed : Missing,
                ["foundIn"] = tables.Count == 0 ? null : tables[0].FoundIn,
                ["direction"] = tables.Count == 0 ? null : tables[0].Direction,
                ["unsure"] = tables.Count > 0 && tables[0].Unsure,
                ["bothOldAndNew"] = new JArray(BothOldAndNew(mapGiven ? confirmed.CodeMap : tables.Count > 0 ? tables[0].Pairs : new List<KeyValuePair<string, string>>())),
                ["notHeld"] = new JArray(f.MapNotHeld ?? new List<string>()),
                ["tooLarge"] = new JArray((f.TablesTooLarge ?? new List<CodeTable>()).Select(LargeTable)),
                ["candidates"] = new JArray(tables.Select(table => new JObject { ["foundIn"] = table.FoundIn, ["pairs"] = table.Pairs.Count, ["renames"] = table.Renames, ["prototypes"] = table.Prototypes, ["unsure"] = table.Unsure })),
            };
            List<TypologyCandidate> columns = f.Columns ?? new List<TypologyCandidate>();
            ConfirmedColumn column = confirmed?.Column;
            profile["typologyColumn"] = new JObject
            {
                ["proposed"] = f.ColumnProposed == null ? null : ColumnRow(f.ColumnProposed),
                ["confirmed"] = column == null ? null : new JObject { ["workbook"] = column.Workbook, ["sheet"] = column.Sheet, ["column"] = column.Column, ["source"] = ConfirmedProfile.PersonEntered, ["found"] = f.ColumnApplied, ["why"] = f.ColumnGone, ["listedTwice"] = f.ColumnListedTwice },
                ["source"] = column != null ? ConfirmedProfile.PersonEntered : f.ColumnProposed != null ? Computed : Missing,
                ["tie"] = f.ColumnTie,
                ["tooLarge"] = new JArray((f.ColumnsTooLarge ?? new List<TypologyCandidate>()).Select(item => new JObject { ["workbook"] = item.Workbook, ["sheet"] = item.Sheet, ["column"] = item.Column, ["codes"] = item.Codes })),
                ["candidates"] = new JArray(columns.Select(ColumnRow)),
            };
            profile["areaColumn"] = AreaBlock(f);
            var evidence = new JObject();
            foreach (KeyValuePair<string, HandCount> count in (f.HandCounts ?? new Dictionary<string, HandCount>()).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                evidence[count.Key] = new JObject { ["mirrored"] = count.Value.Mirrored, ["notMirrored"] = count.Value.NotMirrored };
            }
            var proposed = new JObject();
            foreach (KeyValuePair<string, string> guess in f.HandGuess ?? new Dictionary<string, string>())
            {
                proposed[guess.Key] = guess.Value == null ? JValue.CreateNull() : new JValue(guess.Value);
            }
            var given = new JObject();
            foreach (KeyValuePair<string, string> hand in (confirmed?.Hands ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                given[hand.Key] = hand.Value;
            }
            profile["hands"] = new JObject
            {
                ["proposed"] = proposed,
                ["proposedAs"] = "a guess from how the blocks are drawn, no prototype file was opened",
                ["evidence"] = evidence,
                ["confirmed"] = given,
                ["notHeld"] = new JArray(f.HandsNotHeld ?? new List<string>()),
                ["source"] = given.Count > 0 ? ConfirmedProfile.PersonEntered : (f.HandGuess ?? new Dictionary<string, string>()).Values.Any(guess => guess != null) ? Computed : Missing,
            };
        }

        // R635. Each sheet a proposal was taken from, so a person sees the source of it. The tool cannot tell a past workbook by its name, the person can
        private static JArray ProposalSources(Findings f)
        {
            var rows = new JArray();
            if (f.Tables != null && f.Tables.Count > 0)
            {
                rows.Add(new JObject { ["use"] = "codeMap", ["workbook"] = f.Tables[0].Workbook, ["sheet"] = f.Tables[0].Sheet });
            }
            if (f.ColumnProposed != null)
            {
                rows.Add(new JObject { ["use"] = "typologyColumn", ["workbook"] = f.ColumnProposed.Workbook, ["sheet"] = f.ColumnProposed.Sheet, ["column"] = f.ColumnProposed.Column });
            }
            if (f.AreaProposed != null)
            {
                rows.Add(new JObject { ["use"] = "areaColumn", ["workbook"] = f.AreaProposed.Workbook, ["sheet"] = f.AreaProposed.Sheet, ["column"] = f.AreaProposed.Column });
            }
            return rows;
        }

        // R630 to R635. The rows of values of data_check.json. Each says what happened on this run: what a person confirmed and what it did, or what
        // was found and is waiting for a person, or that nothing was
        private static IEnumerable<JObject> ProposalRows(Findings f)
        {
            ConfirmedProfile confirmed = f.Input.Confirmed;
            List<Plot> plots = f.Plots;

            // The code map
            List<KeyValuePair<string, string>> given = confirmed?.CodeMap ?? new List<KeyValuePair<string, string>>();
            CodeTable table = f.Tables != null && f.Tables.Count > 0 ? f.Tables[0] : null;
            List<string> both = BothOldAndNew(given.Count > 0 ? given : table != null ? table.Pairs : new List<KeyValuePair<string, string>>());
            string bothLine = both.Count == 0 ? "" : " " + string.Join(", ", both.Select(Plain)) + " is both an old and a new code, the plots holding it keep what they read.";
            if (given.Count > 0)
            {
                int turned = plots.Count(plot => plot.CodeRead != null && plot.CodeRead != plot.Villa);
                string held = f.MapNotHeld == null || f.MapNotHeld.Count == 0 ? "" : " " + Count(f.MapNotHeld.Count, "entry names", "entries name") + " a code no plot holds.";
                yield return Need("codeMap", both.Count > 0 || plots.Any(plot => plot.MapDoubt != null) ? "weak" : "usable", "A code map of " + Count(given.Count, "row", "rows") + " is confirmed, it changed " + Count(turned, "plot", "plots") + "." + bothLine + held);
            }
            else if (table != null)
            {
                string how = (table.Direction == Guess ? " Which side is old is a guess." : "") + (table.Unsure ? " Unsure, the drawings hold none of its old codes." : "");
                yield return Need("codeMap", both.Count > 0 || table.Unsure || table.Direction == Guess ? "weak" : "usable", "A table of " + Count(table.Pairs.Count, "old and new code", "old and new codes") + " was found and waits for a person." + how + bothLine);
            }
            else if (f.TablesTooLarge != null && f.TablesTooLarge.Count > 0)
            {
                yield return Need("codeMap", "weak", "A table of " + f.TablesTooLarge[0].Pairs.Count + " old and new codes was found, too large for a code table, the limit is " + f.Settings.CodeTableMaxPairs + ". Type the map if the project has one.");
            }
            else
            {
                yield return Need("codeMap", "missing", "No table of old and new codes was found, type it if the project has one.");
            }

            // The typology column
            if (confirmed?.Column != null)
            {
                string twice = f.ColumnListedTwice > 0 ? " " + Count(f.ColumnListedTwice, "plot is", "plots are") + " listed twice on that sheet." : "";
                yield return f.ColumnApplied
                    ? Need("typologyColumn", "usable", "The confirmed typology column gave " + Count(plots.Count(plot => plot.FromColumn && plot.Villa != null), "plot", "plots") + " a code, the drawing gives another on " + plots.Count(plot => plot.ColumnDisagrees != null) + "." + twice)
                    : Need("typologyColumn", "weak", "The confirmed typology column was not used, " + f.ColumnGone + ". No plot took a code from it.");
            }
            else if (f.ColumnProposed != null)
            {
                yield return Need("typologyColumn", "usable", Count(f.Columns.Count, "column gives", "columns give") + " each plot a villa type, the best waits for a person." + (f.ColumnTie == null ? "" : " " + f.ColumnTie + "."));
            }
            else if (f.ColumnTie != null)
            {
                yield return Need("typologyColumn", "weak", f.ColumnTie + ".");
            }
            else if (f.ColumnsTooLarge != null && f.ColumnsTooLarge.Count > 0)
            {
                yield return Need("typologyColumn", "weak", "A column of " + f.ColumnsTooLarge[0].Codes + " codes was found, too large for a typology column, the limit is " + f.Settings.CodeTableMaxPairs + ".");
            }
            else
            {
                yield return Need("typologyColumn", "missing", "No sheet column gives each plot a villa type the drawing does not already give.");
            }

            // The area column
            int areas = plots.Where(plot => plot.AreaName != null).Select(plot => plot.AreaName).Distinct(StringComparer.Ordinal).Count();
            if (confirmed?.AreaColumn != null)
            {
                yield return f.AreaGone == null
                    ? Need("areaColumn", "usable", "The confirmed area column gives " + Count(areas, "area", "areas") + " on " + plots.Count(plot => plot.AreaName != null) + " of " + plots.Count + " plots.")
                    : Need("areaColumn", "weak", "The confirmed area column was not used, " + f.AreaGone + ". The areas stay those found by distance.");
            }
            else if (f.AreaProposed != null)
            {
                yield return Need("areaColumn", "usable", "A column parts the plots into " + Count(f.AreaProposed.Values, "area", "areas") + " and waits for a person." + (f.AreaTie == null ? "" : " " + f.AreaTie + "."));
            }
            else if (f.AreaTie != null)
            {
                yield return Need("areaColumn", "weak", f.AreaTie + ".");
            }
            else
            {
                yield return Need("areaColumn", "missing", "No sheet column parts the plots into areas, the areas are those found by distance.");
            }

            // The hands
            int guesses = f.HandGuess == null ? 0 : f.HandGuess.Values.Count(guess => guess != null);
            int total = f.HandGuess == null ? 0 : f.HandGuess.Count;
            Dictionary<string, string> hands = confirmed?.Hands ?? new Dictionary<string, string>();
            if (hands.Count > 0)
            {
                string notHeld = f.HandsNotHeld == null || f.HandsNotHeld.Count == 0 ? "" : " " + Count(f.HandsNotHeld.Count, "names", "name") + " a code no plot holds.";
                yield return Need("hands", "usable", Count(hands.Count, "hand is", "hands are") + " confirmed, " + plots.Count(plot => plot.HandKnown) + " plots take their mirror from one." + notHeld);
            }
            else if (guesses > 0)
            {
                yield return Need("hands", "weak", guesses + " of " + Count(total, "prototype has", "prototypes have") + " a guess from how its blocks are drawn, confirm it. No prototype file was opened.");
            }
            else
            {
                yield return Need("hands", "missing", "No prototype has a guess, its blocks are drawn both ways or there are none. Give each hand, no prototype file was opened.");
            }
        }
    }
}
