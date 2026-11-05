using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R600 to R605. The finding half of the data check: what the scan gives, the plots made of it, and the groups, words, hands and villa
    // types of those plots. The marks and lines are written from these findings in DataCheckMarks
    public static partial class DataCheckLogic
    {
        private sealed class WordCandidate
        {
            public string Word;
            public int Groups;
        }

        private sealed class HeaderMatch
        {
            public string Header;
            public Dictionary<int, string> Names;
        }

        private sealed class Findings
        {
            public DataCheckSettings Settings;
            public DataCheckInput Input;
            public JObject Scan;
            public List<JObject> Rows = new List<JObject>();
            public List<JObject> Checks = new List<JObject>();
            public JObject Outline;
            public JObject NumberRow;
            public string PlotSource;
            public JObject Drawing;
            public List<Plot> Plots = new List<Plot>();
            public string Lead;
            public HashSet<string> BlockSources = new HashSet<string>();
            public double? MetersPerUnit;
            public bool UnitAssumed;
            public string GroupWhy;
            public string[] Words;
            public string[] WordSources;
            public string[] WordNotes;
            public List<string>[] WordCandidates;
            public List<Dictionary<int, string>> GroupNames = new List<Dictionary<int, string>>();
            public string SheetPoor;
            public int OutlineFaces;
            public int LabelledFaces;
            public int NumbersTwice;
            public int NumberFlags;
            public int Holes;
            public List<Plot> StandIns = new List<Plot>();
            public List<CodeTable> Tables;
            public List<CodeTable> TablesTooLarge;
            public List<TypologyCandidate> Columns;
            public List<TypologyCandidate> ColumnsTooLarge;
            public TypologyCandidate ColumnProposed;
            public string ColumnTie;
            public string ColumnGone;
            public int ColumnListedTwice;
            public List<AreaCandidate> Areas;
            public AreaCandidate AreaProposed;
            public string AreaTie;
            public string AreaGone;
            public List<string> MapNotHeld;
            public List<string> HandsNotHeld;
            public Dictionary<string, HandCount> HandCounts;
            public Dictionary<string, string> HandGuess;
            public bool ColumnApplied;
            public HashSet<string> Prototypes = new HashSet<string>(StringComparer.Ordinal);
        }

        public static DataCheckOutput Run(DataCheckSettings settings, DataCheckInput input)
        {
            var prototypes = new HashSet<string>(input.PrototypeNames.Select(settings.PrototypeCode).Where(code => code != null), StringComparer.Ordinal);
            // R632. Where the prototype files carry the new codes the drawing speaks the old ones, so a confirmed old code is looked for as well. A
            // prototype file still decides which source scores, no old code does
            var codes = new HashSet<string>(prototypes, StringComparer.Ordinal);
            if (input.Confirmed != null)
            {
                codes.UnionWith(input.Confirmed.CodeMap.Select(pair => pair.Key));
            }
            List<KeyValuePair<string, List<double[]>>> rings;
            string plotSource;
            JObject scan = ReadScan(settings, input, codes, prototypes, out rings, out plotSource);
            BreakTies(settings, input, codes, prototypes, ref scan, ref rings, ref plotSource);
            var f = new Findings { Settings = settings, Input = input, Scan = scan, Prototypes = prototypes };
            f.Rows = scan == null ? new List<JObject>() : ((JArray)scan["rows"]).OfType<JObject>().ToList();
            f.Checks = scan == null ? new List<JObject>() : ((JArray)scan["checks"]).OfType<JObject>().ToList();
            // The scan proposes an outline for each drawing and chose one to give the plots, every line of a need follows that one
            f.PlotSource = plotSource;
            f.Outline = plotSource == null ? null : f.Rows.FirstOrDefault(row => Str(row["kind"]) == "plotOutline" && Str(row["source"]) == plotSource);
            string drawingName = plotSource == null ? null : plotSource.Split('|')[0];
            f.NumberRow = drawingName == null ? null : f.Rows.FirstOrDefault(row => Str(row["kind"]) == "plotNumber" && Str(row["proposedUse"]) == "plot numbers" && Str(row["source"]).StartsWith(drawingName + "|", StringComparison.Ordinal));
            f.Drawing = drawingName == null ? null : input.CadReads.FirstOrDefault(read => Str(read["source"]) == drawingName);
            string units = f.Drawing == null ? "" : Str(f.Drawing["units"]);
            f.MetersPerUnit = f.Drawing == null ? (double?)null : DataCheckSettings.MetersPerUnit(units);
            f.UnitAssumed = f.Drawing != null && DataCheckSettings.UnitAssumed(units);
            Match faces = f.Outline == null ? Match.Empty : Regex.Match(Str(f.Outline["found"]), @"^(\d+) faces");
            f.OutlineFaces = faces.Success ? int.Parse(faces.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            f.LabelledFaces = f.Outline == null ? 0 : (int)f.Outline["score"];
            f.NumbersTwice = ChecksOf(f.Checks, "numberTwice");
            if (scan != null && f.Outline != null)
            {
                f.Plots = PlotsOf(scan, rings);
            }
            f.NumberFlags = f.Plots.Count(plot => plot.NumberFlag != null);
            f.Holes = FindHoles(f.Plots, f.Drawing, f.Outline, settings.StandInMaxArea, f.StandIns);
            f.BlockSources = BlockSources(f.Rows);
            FindVillaTypes(f.Plots, scan, f.Rows, codes, out f.Lead);
            f.HandCounts = new Dictionary<string, HandCount>(StringComparer.Ordinal);
            FindMirror(f.Plots, input, f.BlockSources, settings, codes, f.HandCounts);
            f.HandGuess = HandGuesses(settings, prototypes, f.HandCounts);
            f.SheetPoor = SheetPoor(settings, input, scan, f.Plots, codes);
            int levels = settings.GroupGapsM.Count;
            if (f.Plots.Count > 0)
            {
                if (f.MetersPerUnit == null)
                {
                    f.GroupWhy = "the drawing states a unit this check cannot turn into metres: " + units;
                }
                else
                {
                    for (int level = 0; level < levels; level++)
                    {
                        int[] labels = Cluster(f.Plots, f.StandIns, settings.GroupGapsM[level] / f.MetersPerUnit.Value);
                        for (int at = 0; at < f.Plots.Count; at++)
                        {
                            f.Plots[at].Group = f.Plots[at].Group ?? new int[levels];
                            f.Plots[at].Group[level] = labels[at];
                        }
                    }
                }
            }
            f.TablesTooLarge = new List<CodeTable>();
            f.ColumnsTooLarge = new List<TypologyCandidate>();
            f.Tables = CodeTables(settings, input, prototypes, f.Plots, f.TablesTooLarge);
            f.Columns = TypologyColumns(settings, input, f.Plots, prototypes, f.ColumnsTooLarge);
            f.ColumnProposed = TypologyProposal(f.Columns, out f.ColumnTie);
            f.Areas = AreaColumns(settings, input, f.Plots);
            f.AreaProposed = AreaProposal(f.Areas, out f.AreaTie);
            ApplyConfirmed(f);
            f.Words = new string[levels];
            f.WordSources = new string[levels];
            f.WordNotes = new string[levels];
            f.WordCandidates = new List<string>[levels];
            for (int level = 0; level < levels; level++)
            {
                f.GroupNames.Add(new Dictionary<int, string>());
                f.WordCandidates[level] = new List<string>();
            }
            if (f.Plots.Count > 0 && f.GroupWhy == null)
            {
                List<WordCandidate>[] fromCad = CadWords(settings, f.Plots, f.Drawing, f.NumberRow, f.MetersPerUnit.Value, levels);
                List<HeaderMatch>[] fromSheets = SheetHeaders(settings, f.Plots, input.Excels, levels);
                DecideWords(settings, f, fromCad, fromSheets);
            }
            for (int level = 0; level < levels; level++)
            {
                f.WordSources[level] = f.Words[level] == null ? Missing : Computed;
                // R635. The header of a column a person confirmed names the areas, a word the person typed still goes over it
                if (level == 0 && input.Confirmed?.AreaColumn != null && f.AreaGone == null)
                {
                    f.Words[level] = input.Confirmed.AreaColumn.Column;
                    f.WordSources[level] = ConfirmedProfile.PersonEntered;
                    f.WordNotes[level] = null;
                    f.WordCandidates[level].Clear();
                }
                if (input.Confirmed != null && input.Confirmed.Levels.TryGetValue(level + 1, out string word))
                {
                    f.Words[level] = word;
                    f.WordSources[level] = ConfirmedProfile.PersonEntered;
                    f.WordNotes[level] = null;
                    f.WordCandidates[level].Clear();
                }
            }
            return Build(f);
        }

        // R632. What a person confirmed, in the order that keeps each change on what the one before it left: the column gives the code, the
        // map turns it, the hand then reads the code the plot ended with
        private static void ApplyConfirmed(Findings f)
        {
            ConfirmedProfile confirmed = f.Input.Confirmed;
            if (confirmed == null)
            {
                return;
            }
            if (confirmed.Column != null)
            {
                f.ColumnGone = ApplyColumn(f.Settings, f.Input, f.Plots, confirmed.Column, out f.ColumnListedTwice);
                f.ColumnApplied = f.ColumnGone == null;
                if (!f.ColumnApplied)
                {
                    f.Input.Notes.Add(new DataCheckNote { Name = confirmed.Column.Sheet + "|" + confirmed.Column.Column, Kind = "confirmed column", Read = false, Reason = "the confirmed typology column was not used, " + f.ColumnGone });
                }
            }
            if (confirmed.AreaColumn != null)
            {
                f.AreaGone = ApplyArea(f.Settings, f.Input, f.Plots, confirmed.AreaColumn);
                if (f.AreaGone != null)
                {
                    f.Input.Notes.Add(new DataCheckNote { Name = confirmed.AreaColumn.Sheet + "|" + confirmed.AreaColumn.Column, Kind = "confirmed column", Read = false, Reason = "the confirmed area column was not used, " + f.AreaGone });
                }
            }
            if (confirmed.CodeMap.Count > 0)
            {
                ApplyMap(f.Plots, confirmed.CodeMap);
                f.MapNotHeld = NotHeld(confirmed.CodeMap.Select(pair => pair.Key), f.Plots);
            }
            ApplyHands(f.Plots, confirmed.Hands);
            f.HandsNotHeld = NotHeld(confirmed.Hands.Keys, f.Plots);
            // R631. Confirming the column settles the type and lifts the mark, what the drawing still says is kept and counted, never dropped
            foreach (Plot plot in f.Plots.Where(plot => plot.FromColumn && plot.Villa != null && plot.DrawingCode != null && plot.Villa != plot.DrawingCode))
            {
                plot.ColumnDisagrees = "the drawing gives " + plot.DrawingCode + " and the confirmed column gives " + plot.Villa;
            }
        }

        private static JObject ReadScan(DataCheckSettings settings, DataCheckInput input, HashSet<string> codes, HashSet<string> prototypes, out List<KeyValuePair<string, List<double[]>>> rings, out string plotSource)
        {
            rings = new List<KeyValuePair<string, List<double[]>>>();
            plotSource = null;
            if (input.CadReads.Count == 0)
            {
                return null;
            }
            try
            {
                return ScanLogic.Result(settings.Scan, codes, prototypes, settings.StrongShare, new HashSet<string>(StringComparer.Ordinal), input.CadReads, input.Excels, input.Pdfs, new JArray(), out rings, out plotSource);
            }
            catch (Exception failure)
            {
                input.Notes.Add(new DataCheckNote { Name = "(plot read)", Kind = "scan", Read = false, Reason = failure.GetType().Name + ": " + failure.Message });
                return null;
            }
        }

        private static string[] LayersOf(string layers)
        {
            return layers.Split(new[] { ScanLogic.LayerJoin }, StringSplitOptions.None);
        }

        // R601. Two candidate outlines that tie on the plots they cover are not a conflict when one is the other with more layers, as a layer of
        // blocks added to the plot layer gives the same plots. The candidate with the fewest layers is taken and the tie is said under ties. Any
        // other tie leaves the scan with no proposal and is named in the line of the need, never dropped in silence
        private static void BreakTies(DataCheckSettings settings, DataCheckInput input, HashSet<string> codes, HashSet<string> prototypes, ref JObject scan, ref List<KeyValuePair<string, List<double[]>>> rings, ref string plotSource)
        {
            for (int round = 0; round < 3 && scan != null; round++)
            {
                bool changed = false;
                foreach (JObject tie in ((JArray)scan["checks"]).OfType<JObject>().Where(item => Str(item["what"]) == "tie" && Str(item["source"]) == "plotOutline").ToList())
                {
                    var parsed = Str(tie["value"]).Split(new[] { " or " }, StringSplitOptions.None).Select(source => new { Source = source, Cut = source.IndexOf('|') }).ToList();
                    if (parsed.Count < 2 || parsed.Any(item => item.Cut <= 0))
                    {
                        continue;
                    }
                    var sources = parsed.Select(item => new { item.Source, File = item.Source.Substring(0, item.Cut), Layers = item.Source.Substring(item.Cut + 1) }).ToList();
                    int fewest = sources.Min(item => LayersOf(item.Layers).Length);
                    var winners = sources.Where(item => LayersOf(item.Layers).Length == fewest).ToList();
                    if (winners.Count != 1)
                    {
                        continue;
                    }
                    foreach (var loser in sources.Where(item => item.Source != winners[0].Source))
                    {
                        DropCandidate(input.CadReads, loser.File, loser.Layers);
                    }
                    input.Ties.Add(sources.Count.ToString(CultureInfo.InvariantCulture) + " candidates for the plot outlines gave the same number of plots, the one with the fewest layers was taken: "
                        + winners[0].Source + " over " + string.Join(", ", sources.Where(item => item.Source != winners[0].Source).Select(item => item.Source)));
                    changed = true;
                }
                if (!changed)
                {
                    return;
                }
                scan = ReadScan(settings, input, codes, prototypes, out rings, out plotSource);
            }
        }

        private static void DropCandidate(List<JObject> reads, string file, string layers)
        {
            foreach (JObject read in reads.Where(item => Str(item["source"]) == file))
            {
                var candidates = (JArray)read["candidates"];
                foreach (JToken candidate in candidates.ToList())
                {
                    string joined = string.Join(ScanLogic.LayerJoin, ((JArray)candidate["layers"]).Select(Str).OrderBy(name => name, StringComparer.Ordinal));
                    if (joined == layers)
                    {
                        candidate.Remove();
                    }
                }
            }
        }

        private static double RingArea(List<double[]> ring)
        {
            double sum = 0;
            for (int at = 0; at < ring.Count; at++)
            {
                double[] a = ring[at], b = ring[(at + 1) % ring.Count];
                sum += a[0] * b[1] - b[0] * a[1];
            }
            return Math.Abs(sum) / 2;
        }

        // R602. A plot of the drawing as its ring and the number of its label. A label that holds more than one number takes the last, which the
        // flag says, so the number is no strong read
        private static List<Plot> PlotsOf(JObject scan, List<KeyValuePair<string, List<double[]>>> rings)
        {
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JObject row in ((JArray)scan["plots"]).OfType<JObject>())
            {
                labels[Str(row["plot"])] = Str(row["label"]);
            }
            var byNumber = new Dictionary<string, Plot>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<double[]>> ring in rings)
            {
                var plot = new Plot
                {
                    Number = ring.Key, Ring = ring.Value, Area = RingArea(ring.Value),
                    Box = new[] { ring.Value.Min(p => p[0]), ring.Value.Min(p => p[1]), ring.Value.Max(p => p[0]), ring.Value.Max(p => p[1]) },
                };
                // Only a token of digits alone is a second number, a type code such as VL4 on a line of its own or before the number is not
                if (labels.TryGetValue(ring.Key, out string label) && Regex.Matches(label, @"(?<!\S)\d+(?!\S)").Count > 1)
                {
                    plot.NumberFlag = "the label holds more than one number (" + string.Join(" and ", Regex.Matches(label, @"(?<!\S)\d+(?!\S)").Cast<Match>().Select(found => found.Value)) + "), the plot has no number of its own";
                }
                byNumber[ring.Key] = plot;
            }
            return byNumber.Values.OrderBy(plot => plot.Number, StringComparer.Ordinal).ToList();
        }

        public const string HoleWhy = "the outline has a hole or an island, what lies in it is left out of the mirror and the villa type may still count it";

        private static string RingKey(IList<double[]> ring)
        {
            return ring.Count.ToString(CultureInfo.InvariantCulture) + ":" + ring[0][0].ToString("F3", CultureInfo.InvariantCulture) + "," + ring[0][1].ToString("F3", CultureInfo.InvariantCulture);
        }

        // R602. A closed outline of the proposed layers that is no plot and lies inside a plot is a hole or an island of that plot. A face that
        // holds no number is not a plot, so a courtyard drawn inside a plot is one. The plot is flagged and what lies inside the hole is left
        // out of its mirror. An outline that lies inside no plot and is no larger than standInMaxArea times the median plot is a plot with no
        // number, it gets no row and is given back to stand in the distance test. Gives the count of plots flagged
        private static int FindHoles(List<Plot> plots, JObject drawing, JObject outline, double standInMaxArea, List<Plot> standIns)
        {
            if (plots.Count == 0 || drawing == null || outline == null)
            {
                return 0;
            }
            string layers = LayerOf(Str(outline["source"]));
            JToken candidate = ((JArray)drawing["candidates"]).FirstOrDefault(item => string.Join(ScanLogic.LayerJoin, ((JArray)item["layers"]).Select(Str).OrderBy(name => name, StringComparer.Ordinal)) == layers);
            if (candidate == null)
            {
                return 0;
            }
            var own = new HashSet<string>(plots.Select(plot => RingKey(plot.Ring)), StringComparer.Ordinal);
            List<double> areas = plots.Select(plot => plot.Area).OrderBy(value => value).ToList();
            double limit = standInMaxArea * areas[areas.Count / 2];
            var grid = new BoxGrid(plots.Select(plot => plot.Box).ToList(), 0);
            foreach (JToken face in (JArray)candidate["faces"])
            {
                List<double[]> ring = ((JArray)face["ring"]).Select(point => new[] { (double)point[0], (double)point[1] }).ToList();
                if (ring.Count < 3 || own.Contains(RingKey(ring)))
                {
                    continue;
                }
                double x = (double)face["centroid"][0], y = (double)face["centroid"][1];
                if (!ScanLogic.Contains(ring, x, y))
                {
                    x = ring.Average(point => point[0]);
                    y = ring.Average(point => point[1]);
                }
                double area = RingArea(ring);
                Plot holder = grid.Holding(x, y).Select(at => plots[at]).Where(plot => plot.Area > area && ScanLogic.Contains(plot.Ring, x, y)).OrderBy(plot => plot.Area).FirstOrDefault();
                if (holder != null)
                {
                    holder.Holes.Add(ring);
                }
                else if (area <= limit)
                {
                    standIns.Add(new Plot { Ring = ring, Area = area, Box = new[] { ring.Min(point => point[0]), ring.Min(point => point[1]), ring.Max(point => point[0]), ring.Max(point => point[1]) } });
                }
            }
            return plots.Count(plot => plot.Hole);
        }

        // R602. A sheet that holds villa type codes for many plots but joins few of them to the drawing is weak in itself, a join that falls
        // apart must never leave the need better than a join that holds. Gives the words for the line, null when there is no such sheet
        private static string SheetPoor(DataCheckSettings settings, DataCheckInput input, JObject scan, List<Plot> plots, HashSet<string> codes)
        {
            if (plots.Count == 0 || codes.Count == 0 || input.Excels.Count == 0)
            {
                return null;
            }
            int held = 0;
            foreach (ScanWorkbook book in input.Excels)
            {
                foreach (ScanSheet sheet in book.Sheets)
                {
                    for (int column = 0; column < sheet.Headers.Count; column++)
                    {
                        int cells = sheet.Rows.Count(row => column < row.Count && ScanLogic.ValueCodes(row[column], codes, settings.Scan.TokenSplit).Count == 1);
                        held = Math.Max(held, cells);
                    }
                }
            }
            if (held < settings.UsableShare * plots.Count)
            {
                return null;
            }
            var joined = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject row in ((JArray)scan["plots"]).OfType<JObject>())
            {
                if (((JObject)row["villaType"]).Properties().Any(item => KindOf(item.Name) == 2))
                {
                    joined.Add(Str(row["plot"]));
                }
            }
            int reach = Math.Min(held, plots.Count);
            return joined.Count < settings.UsableShare * reach
                ? "a sheet holds " + held + " villa type codes and joins only " + joined.Count + " plots to the drawing"
                : null;
        }

        // The hand of the villa block inside each plot: the insert is drawn mirrored or not, the plots that hold inserts of both hands none. An
        // insert belongs to the smallest outline that holds it, so what lies in a hole is not given to the plot around it
        private static void FindMirror(List<Plot> plots, DataCheckInput input, HashSet<string> blockSources, DataCheckSettings settings, HashSet<string> codes, Dictionary<string, HandCount> counts)
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
                    Plot own = grid.Holding(x, y).Where(at => ScanLogic.Contains(plots[at].Ring, x, y)).Select(at => plots[at]).OrderBy(plot => plot.Area).FirstOrDefault();
                    if (own == null || own.Holes.Any(hole => ScanLogic.Contains(hole, x, y)))
                    {
                        continue;
                    }
                    if (!hands.TryGetValue(own, out HashSet<bool> set))
                    {
                        hands[own] = set = new HashSet<bool>();
                    }
                    set.Add((bool)insert["mirrored"]);
                    // R633. The code of the block is what says which prototype's hand this insert speaks for
                    List<string> named = ScanLogic.ValueCodes(Str(insert["block"]), codes, settings.Scan.TokenSplit);
                    if (named.Count == 1)
                    {
                        own.BlockCodes.Add(named[0]);
                        if (!counts.TryGetValue(named[0], out HandCount count))
                        {
                            counts[named[0]] = count = new HandCount();
                        }
                        if ((bool)insert["mirrored"])
                        {
                            count.Mirrored++;
                        }
                        else
                        {
                            count.NotMirrored++;
                        }
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

        // R603. The words a group level could carry, found in the texts of the plot drawing. A text of two or more words that lies outside every
        // plot and within wordReach of one is the words before its last token and that token. A word qualifies for a level when its texts sit
        // by wordShare of the groups of the level, each with a last token of its own. Every word that qualifies is given, the choice is made
        // in DecideWords
        private static List<WordCandidate>[] CadWords(DataCheckSettings settings, List<Plot> plots, JObject drawing, JObject numberRow, double metersPerUnit, int levels)
        {
            var result = Enumerable.Range(0, levels).Select(level => new List<WordCandidate>()).ToArray();
            if (drawing == null)
            {
                return result;
            }
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
                // R634. Only a text that names a group can give a word: its last token reads as the name of a group, as a note's last word does not
                if (tokens.Length < 2 || !settings.GroupName.IsMatch(tokens[tokens.Length - 1]))
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
                if (near >= 0 && plots[near].Group != null)
                {
                    found.Add(Tuple.Create(string.Join(" ", tokens.Take(tokens.Length - 1)), tokens[tokens.Length - 1], plots[near].Group));
                }
            }
            for (int level = 0; level < levels; level++)
            {
                int groups = plots.Select(plot => plot.Group[level]).Distinct().Count();
                foreach (IGrouping<string, Tuple<string, string, int[]>> word in found.GroupBy(item => item.Item1, StringComparer.Ordinal).OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    if (!Regex.IsMatch(word.Key, @"\p{L}"))
                    {
                        continue;
                    }
                    List<IGrouping<int, Tuple<string, string, int[]>>> covered = word.GroupBy(item => item.Item3[level]).ToList();
                    bool eachOwn = covered.All(group => group.Select(item => item.Item2).Distinct(StringComparer.Ordinal).Count() == 1)
                        && covered.Select(group => group.First().Item2).Distinct(StringComparer.Ordinal).Count() == covered.Count;
                    if (eachOwn && covered.Count >= 2 && (double)covered.Count / groups >= settings.WordShare)
                    {
                        result[level].Add(new WordCandidate { Word = word.Key, Groups = covered.Count });
                    }
                }
            }
            return result;
        }

        // R603. The headers of the sheets whose column parts the plots of the sheet the way the groups of a level part them, for each level. A
        // header that is only the letter of its column is no word
        private static List<HeaderMatch>[] SheetHeaders(DataCheckSettings settings, List<Plot> plots, List<ScanWorkbook> excels, int levels)
        {
            var result = Enumerable.Range(0, levels).Select(level => new List<HeaderMatch>()).ToArray();
            var byNumber = plots.ToDictionary(plot => plot.Number, StringComparer.Ordinal);
            foreach (ScanWorkbook book in excels)
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
                        for (int level = 0; level < levels; level++)
                        {
                            // R634. One value over one group parts nothing, so a column must name wordShare of the groups and two at least
                            int named = value.Keys.Select(plot => plot.Group[level]).Distinct().Count();
                            int groups = plots.Select(plot => plot.Group[level]).Distinct().Count();
                            if (!SamePartition(value, level) || named < 2 || named < settings.WordShare * groups)
                            {
                                continue;
                            }
                            var names = new Dictionary<int, string>();
                            foreach (IGrouping<int, KeyValuePair<Plot, string>> group in value.GroupBy(item => item.Key.Group[level]))
                            {
                                names[group.Key] = group.Select(item => item.Value).First();
                            }
                            result[level].Add(new HeaderMatch { Header = header, Names = names });
                        }
                    }
                }
            }
            return result;
        }

        // R603. The word of each level, or none with the words that could not be told apart. A header of a sheet that parts the plots as the
        // groups do is enough alone. A word found in the drawing alone needs wordMinGroups groups, and two words that both qualify leave the
        // level with none, so a tie or a weak share never gives a word. A word used for a level is not offered to the next
        private static void DecideWords(DataCheckSettings settings, Findings f, List<WordCandidate>[] fromCad, List<HeaderMatch>[] fromSheets)
        {
            var taken = new List<string>();
            for (int level = 0; level < f.Words.Length; level++)
            {
                int groups = f.Plots.Select(plot => plot.Group[level]).Distinct().Count();
                List<HeaderMatch> headers = fromSheets[level].Where(match => !taken.Contains(match.Header, StringComparer.OrdinalIgnoreCase))
                    .GroupBy(match => match.Header, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
                List<WordCandidate> texts = fromCad[level].Where(candidate => !taken.Contains(candidate.Word, StringComparer.OrdinalIgnoreCase)).ToList();
                HeaderMatch header = null;
                WordCandidate text = null;
                if (headers.Count == 1)
                {
                    header = headers[0];
                }
                else if (headers.Count > 1)
                {
                    List<HeaderMatch> both = headers.Where(match => texts.Any(candidate => string.Equals(candidate.Word, match.Header, StringComparison.OrdinalIgnoreCase))).ToList();
                    if (both.Count == 1)
                    {
                        header = both[0];
                    }
                    else
                    {
                        f.WordCandidates[level].AddRange(headers.Select(match => match.Header));
                        f.WordNotes[level] = "unsure between " + string.Join(" and ", headers.Select(match => Plain(match.Header)));
                    }
                }
                else if (texts.Count == 1)
                {
                    if (texts[0].Groups >= settings.WordMinGroups)
                    {
                        text = texts[0];
                    }
                    else
                    {
                        f.WordCandidates[level].Add(texts[0].Word);
                        f.WordNotes[level] = "a word in the drawing covers only " + texts[0].Groups + " of " + groups + " groups";
                    }
                }
                else if (texts.Count > 1)
                {
                    f.WordCandidates[level].AddRange(texts.Select(candidate => candidate.Word));
                    f.WordNotes[level] = "unsure between " + string.Join(" and ", texts.Select(candidate => Plain(candidate.Word)));
                }
                if (header != null)
                {
                    f.Words[level] = header.Header;
                    f.GroupNames[level] = header.Names;
                }
                else if (text != null)
                {
                    f.Words[level] = text.Word;
                }
                if (f.Words[level] != null)
                {
                    taken.Add(f.Words[level]);
                }
            }
        }
    }
}
