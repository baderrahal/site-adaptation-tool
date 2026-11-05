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
            public List<KeyValuePair<string, string>> Pairs;
            public int Renames;
            public int Prototypes;
        }

        private sealed class TypologyCandidate
        {
            public string Workbook;
            public string Sheet;
            public string Column;
            public int Plots;
            public int Whole;
            public double WholeShare;
            public int Order;
        }

        private sealed class HandCount
        {
            public int Mirrored;
            public int NotMirrored;
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
        // prototype codes. The side the drawings use more is the old one, a tie is read left to right. Best first: the most prototype codes
        // covered, then the most renames, then the most pairs, then the order of the files
        private static List<CodeTable> CodeTables(DataCheckSettings settings, DataCheckInput input, HashSet<string> prototypes, List<Plot> plots)
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
                            found.Add(table);
                        }
                    }
                }
            }
            return found.OrderByDescending(table => table.Prototypes).ThenByDescending(table => table.Renames).ThenByDescending(table => table.Pairs.Count).ToList();
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
            if (pairs.Count < 2 || pairs.Count > settings.CodeTableMaxPairs || pairs.Count < settings.StrongShare * rows.Count || pairs.All(pair => pair.Key == pair.Value))
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
            int leftUsed = pairs.Count(pair => words.Contains(BaseOf(settings, pair.Key)));
            int rightUsed = pairs.Count(pair => words.Contains(BaseOf(settings, pair.Value)));
            bool turned = rightUsed > leftUsed;
            return new CodeTable
            {
                FoundIn = book.Name + "|" + sheet.Name + "|" + sheet.Headers[turned ? left + 1 : left] + "|" + sheet.Headers[turned ? left : left + 1],
                Pairs = turned ? pairs.Select(pair => new KeyValuePair<string, string>(pair.Value, pair.Key)).ToList() : pairs,
                Renames = pairs.Count(pair => pair.Key != pair.Value),
                Prototypes = covered,
            };
        }

        // R631. The columns that give each plot a villa type, on a sheet that lists the plots: a cell with the shape of a code for usableShare
        // of the plots, a few codes that each run across the groups as a type does and a block or area name does not, and that say something
        // the drawing does not, since a column that repeats the drawing's codes settles nothing. Best first: the most plots a prototype file
        // can be found for, then the column most of whose rows hold one, then the later column, as a schedule adds new revisions on the right
        private static List<TypologyCandidate> TypologyColumns(DataCheckSettings settings, DataCheckInput input, List<Plot> plots, HashSet<string> prototypes)
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
                        if (column == key)
                        {
                            continue;
                        }
                        Dictionary<Plot, string> cells = ColumnCells(sheet, key, column, byNumber);
                        TypologyCandidate candidate = Typology(settings, plots.Count, cells, prototypes);
                        if (candidate != null)
                        {
                            candidate.Workbook = book.Name;
                            candidate.Sheet = sheet.Name;
                            candidate.Column = sheet.Headers[column];
                            candidate.Order = order;
                            found.Add(candidate);
                        }
                    }
                }
            }
            return found.OrderByDescending(item => item.Whole).ThenByDescending(item => item.WholeShare).ThenByDescending(item => item.Order).ToList();
        }

        // The cell of each plot the column gives, the empty text for a row with no value, the first row of a plot if it is listed twice
        private static Dictionary<Plot, string> ColumnCells(ScanSheet sheet, int key, int column, Dictionary<string, Plot> byNumber)
        {
            var cells = new Dictionary<Plot, string>();
            foreach (List<string> row in sheet.Rows)
            {
                if (key < row.Count && byNumber.TryGetValue(row[key].Trim(), out Plot plot) && !cells.ContainsKey(plot))
                {
                    cells[plot] = column < row.Count ? row[column].Trim() : "";
                }
            }
            return cells;
        }

        private static TypologyCandidate Typology(DataCheckSettings settings, int plotCount, Dictionary<Plot, string> cells, HashSet<string> prototypes)
        {
            List<KeyValuePair<Plot, string>> coded = cells.Where(item => settings.CodeShape.IsMatch(item.Value)).ToList();
            List<IGrouping<string, KeyValuePair<Plot, string>>> codes = coded.GroupBy(item => item.Value, StringComparer.Ordinal).ToList();
            if (coded.Count < settings.UsableShare * plotCount || codes.Count > settings.CodeTableMaxPairs)
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
            return new TypologyCandidate { Plots = coded.Count, Whole = whole, WholeShare = cells.Count == 0 ? 0 : (double)whole / cells.Count };
        }

        // R631, R632. The confirmed column gives each plot its code, what the drawing said no longer counts. A plot the column does not list or
        // lists with no code has no villa type and says why, a stale code of the drawing is never kept for it. A plot whose label holds two
        // numbers has no number to join by, so it keeps what the drawing gives. Gives false when the column is no longer in the folder
        private static bool ApplyColumn(DataCheckSettings settings, DataCheckInput input, List<Plot> plots, ConfirmedColumn column)
        {
            ScanSheet sheet = input.Excels.Where(book => column.Workbook == null || book.Name == column.Workbook).SelectMany(book => book.Sheets).FirstOrDefault(item => item.Name == column.Sheet && item.Headers.Contains(column.Column));
            if (sheet == null)
            {
                return false;
            }
            var byNumber = new Dictionary<string, Plot>(StringComparer.Ordinal);
            foreach (Plot plot in plots.Where(plot => plot.NumberFlag == null))
            {
                byNumber[plot.Number] = plot;
            }
            int key = KeyColumn(settings, sheet, byNumber, plots.Count);
            if (key < 0)
            {
                return false;
            }
            Dictionary<Plot, string> cells = ColumnCells(sheet, key, sheet.Headers.IndexOf(column.Column), byNumber);
            foreach (Plot plot in byNumber.Values)
            {
                plot.VillaDispute = null;
                plot.OtherCodes.Clear();
                if (!cells.TryGetValue(plot, out string cell) || cell.Length == 0)
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
            return true;
        }

        // R632. The confirmed map turns the code of each plot once. A code that is already a new code stands, as a table that keeps a name lists
        // it on both sides. An old code with two new codes is no answer, the plot keeps what it read and says so, never a pick
        private static void ApplyMap(List<Plot> plots, List<KeyValuePair<string, string>> map)
        {
            var news = new HashSet<string>(map.Select(pair => pair.Value), StringComparer.Ordinal);
            Func<string, List<string>> turned = code => news.Contains(code) ? new List<string> { code }
                : map.Where(pair => pair.Key == code).Select(pair => pair.Value).Distinct(StringComparer.Ordinal).ToList();
            foreach (Plot plot in plots.Where(plot => plot.Villa != null))
            {
                plot.CodeRead = plot.Villa;
                List<string> now = turned(plot.Villa);
                if (now.Count == 1)
                {
                    plot.Villa = now[0];
                }
                else if (now.Count > 1)
                {
                    plot.MapDoubt = "the code map gives " + plot.Villa + " " + now.Count + " new codes: " + string.Join(", ", now.OrderBy(code => code, StringComparer.Ordinal));
                }
                // Another source that only gave the other name of this code does not dispute it
                plot.OtherCodes.RemoveAll(code => code == plot.Villa || (turned(code).Count == 1 && turned(code)[0] == plot.Villa));
                if (plot.OtherCodes.Count == 0)
                {
                    plot.VillaDispute = null;
                }
                plot.BlockCodes = new HashSet<string>(plot.BlockCodes.Select(code => turned(code).Count == 1 ? turned(code)[0] : code), StringComparer.Ordinal);
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

        private static JArray PairRows(IEnumerable<KeyValuePair<string, string>> pairs, string source)
        {
            return new JArray(pairs.Select(pair => new JObject { ["old"] = pair.Key, ["new"] = pair.Value, ["source"] = source }));
        }

        private static JObject ColumnRow(TypologyCandidate item)
        {
            return new JObject { ["workbook"] = item.Workbook, ["sheet"] = item.Sheet, ["column"] = item.Column, ["plots"] = item.Plots, ["prototypePlots"] = item.Whole, ["source"] = Computed };
        }

        // R630 to R633. The three blocks of project_profile.json beside the levels: what the tool proposes and what a person confirmed, each with
        // its source. Empty when nothing was read, so the window has one shape to read
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
                ["candidates"] = new JArray(tables.Select(table => new JObject { ["foundIn"] = table.FoundIn, ["pairs"] = table.Pairs.Count, ["renames"] = table.Renames, ["prototypes"] = table.Prototypes })),
            };
            List<TypologyCandidate> columns = f.Columns ?? new List<TypologyCandidate>();
            ConfirmedColumn column = confirmed?.Column;
            profile["typologyColumn"] = new JObject
            {
                ["proposed"] = columns.Count == 0 ? null : ColumnRow(columns[0]),
                ["confirmed"] = column == null ? null : new JObject { ["workbook"] = column.Workbook, ["sheet"] = column.Sheet, ["column"] = column.Column, ["source"] = ConfirmedProfile.PersonEntered, ["found"] = f.ColumnApplied },
                ["source"] = column != null ? ConfirmedProfile.PersonEntered : columns.Count > 0 ? Computed : Missing,
                ["candidates"] = new JArray(columns.Select(ColumnRow)),
            };
            var evidence = new JObject();
            foreach (KeyValuePair<string, HandCount> count in (f.HandCounts ?? new Dictionary<string, HandCount>()).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                evidence[count.Key] = new JObject { ["mirrored"] = count.Value.Mirrored, ["notMirrored"] = count.Value.NotMirrored };
            }
            var proposed = new JObject();
            foreach (KeyValuePair<string, string> guess in f.HandGuess ?? new Dictionary<string, string>())
            {
                proposed[guess.Key] = guess.Value;
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
                ["source"] = given.Count > 0 ? ConfirmedProfile.PersonEntered : proposed.Properties().Any(item => item.Value.Type != JTokenType.Null) ? Computed : Missing,
            };
        }
    }
}
