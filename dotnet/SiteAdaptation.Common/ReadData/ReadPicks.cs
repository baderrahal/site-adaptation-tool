using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.ReadData
{
    // R473, R474. The picks of the window What the tool read laid over the read of button 2. ReadDataRun resolves them against the newest
    // scan of the work folder into picks.json, an input of the read, and the worker and python/read_steps.py take picks.json. A pick of a
    // shape the read can not take keeps the default of config and says why
    public static class ReadPicks
    {
        public const string Role = "picks";
        public const string FileName = "picks.json";
        public const string Format = "site-adaptation-picks";
        public const string FromPick = "pick";
        public const string FromProposal = "proposal";
        public const string FromDefault = "default";
        public const string PlotOutline = "plotOutline";
        public const string PlotNumber = "plotNumber";
        public const string VillaBlock = "villaBlock";
        public const string VillaType = "villaType";
        public const string VillaTypeField = "villa type";

        // The column of the plot table and the column key of dataBook.locationPlan.sales a villa type sets
        public const string TypologyColumn = "typologyRev9";
        public const string TypologyKey = "typology";

        public static readonly string[] Kinds = { PlotOutline, PlotNumber, VillaBlock, VillaType };

        private const string LayerJoin = " + ";
        private const string ShiftMark = "|shift ";

        // The file names a pick is held to, those of the inputs of the read, and the sheet of the sales rows the villa type is read from
        public sealed class PickFiles
        {
            public string Baseplan { get; set; }
            public string VillaBlocks { get; set; }
            public string Sales { get; set; }
            public string SalesSheet { get; set; }
        }

        public sealed class PickedConflict
        {
            public string Plot { get; set; }
            public string Field { get; set; }
            public string Source { get; set; }
            public string Value { get; set; }
        }

        // What the worker reads by picks.json, null where it keeps the default of config, and the lines of each pick it does not take
        public sealed class PicksTaken
        {
            public List<string> PlotLineLayers { get; set; }
            public List<string> PlotLabelLayers { get; set; }
            public string VillaBlockLayer { get; set; }
            public string TypologyHeader { get; set; }
            public List<PickedConflict> Conflicts { get; } = new List<PickedConflict>();
            public List<string> Lines { get; } = new List<string>();
        }

        // Per value kind the saved pick when it names a source of the scan, else the proposal of the scan, else the default, and the
        // conflict picks whose key matches a conflict of the scan with the value of the chosen source. scan is null when the work
        // folder holds none
        public static JObject Resolve(ProjectSettings settings, JObject scan, PickFiles files)
        {
            var lines = new List<string>();
            var kinds = new JObject();
            List<JObject> rows = Objects(scan?["rows"]);
            foreach (string kind in Kinds)
            {
                List<JObject> ofKind = rows.Where(row => Text(row["kind"]) == kind).ToList();
                string picked = null;
                settings?.ScanPicks?.TryGetValue(kind, out picked);
                string proposal = ofKind.Where(row => Text(row["proposedUse"]) != "" && Text(row["proposedUse"]) != "check").Select(row => Text(row["source"])).FirstOrDefault();
                string source = null, from = FromDefault;
                if (!string.IsNullOrEmpty(picked) && ofKind.Any(row => Text(row["source"]) == picked))
                {
                    source = picked;
                    from = FromPick;
                }
                else if (!string.IsNullOrEmpty(proposal))
                {
                    source = proposal;
                    from = FromProposal;
                }
                string refused = source == null ? null : Refusal(kind, source, files);
                if (refused != null)
                {
                    lines.Add(refused);
                    source = null;
                    from = FromDefault;
                }
                kinds[kind] = new JObject { ["source"] = source, ["from"] = from };
            }
            var conflicts = new JArray();
            foreach (JObject item in Objects(scan?["conflicts"]))
            {
                string plot = Text(item["plot"]), field = Text(item["field"]), a = Text(item["sourceA"]), b = Text(item["sourceB"]);
                string chosen = null;
                settings?.ConflictPicks?.TryGetValue(ScanView.ConflictKey(plot, field, b), out chosen);
                if (chosen == null || (chosen != a && chosen != b))
                {
                    continue;
                }
                if (field != VillaTypeField)
                {
                    // The read takes the number, place and label of a plot from the baseplan alone, the plot drawing, so only a pick of
                    // the other file has something the read can not take
                    if (chosen == b)
                    {
                        lines.Add("the conflict pick of plot " + plot + " takes the " + field + " of " + b + ", the read takes the " + field + " from the baseplan only and keeps it (R473)");
                    }
                    continue;
                }
                string value = Text(chosen == a ? item["valueA"] : item["valueB"]);
                if (value == "")
                {
                    lines.Add("the conflict pick of plot " + plot + " takes " + chosen + ", which gives no villa type, the read keeps its own (R473)");
                    continue;
                }
                conflicts.Add(new JObject { ["plot"] = plot, ["field"] = field, ["source"] = chosen, ["value"] = value });
            }
            return new JObject { ["format"] = Format, ["kinds"] = kinds, ["conflicts"] = conflicts, ["lines"] = new JArray(lines) };
        }

        // The files of the read by the roles of its inputs, the sales workbook by the role addin.workbookRoles gives the workbook of
        // dataBook.locationPlan.sales, as the worker reads the sales rows
        public static PickFiles FilesOf(JObject config, IEnumerable<KeyValuePair<string, string>> inputs)
        {
            var byRole = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> input in inputs ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                if (input.Key != null && !byRole.ContainsKey(input.Key))
                {
                    byRole[input.Key] = input.Value;
                }
            }
            Func<string, string> name = role => role != null && byRole.TryGetValue(role, out string path) && !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : null;
            string workbook = Text(config?.SelectToken("dataBook.locationPlan.sales.workbook"));
            string salesRole = workbook == "" ? null : Text((config?.SelectToken("addin.workbookRoles") as JObject)?[workbook]);
            string sheet = Text(config?.SelectToken("dataBook.locationPlan.sales.sheet"));
            return new PickFiles { Baseplan = name("baseplan"), VillaBlocks = name("villaBlocks"), Sales = name(salesRole), SalesSheet = sheet == "" ? null : sheet };
        }

        // Why the read can not take this source of the kind with these files, null when it can
        public static string Refusal(string kind, string source, PickFiles files)
        {
            int bar = source.IndexOf('|');
            string what = Label(kind) + " pick " + source;
            if (bar < 0)
            {
                return "the " + what + " names no file, the read keeps the default of config (R473)";
            }
            string file = source.Substring(0, bar), rest = source.Substring(bar + 1);
            files = files ?? new PickFiles();
            switch (kind)
            {
                case PlotOutline:
                case PlotNumber:
                    return OtherFile(what, file, files.Baseplan, "baseplan");
                case VillaBlock:
                    return OtherFile(what, file, files.VillaBlocks, "villa blocks");
                case VillaType:
                    if (rest.StartsWith("block|", StringComparison.Ordinal) || rest.StartsWith("text|", StringComparison.Ordinal))
                    {
                        return "the " + what + " is of blocks or texts of a drawing, the read takes the villa type from the sales sheet only and keeps the column of config (R473)";
                    }
                    string[] sheet = SheetOf(rest);
                    if (sheet == null)
                    {
                        return "the " + what + " is of a PDF page, the read takes the villa type from the sales sheet only and keeps the column of config (R473)";
                    }
                    string other = OtherFile(what, file, files.Sales, "sales workbook");
                    if (other != null)
                    {
                        return other;
                    }
                    return sheet[0] == files.SalesSheet ? null
                        : "the " + what + " is of the sheet " + sheet[0] + ", the read takes the sales rows of the sheet " + files.SalesSheet + " of config and keeps its column (R473)";
                default:
                    return "the pick " + source + " is of no kind the read takes (R473)";
            }
        }

        // The sheet and the header of a villa type source "<sheet>|<header>|shift <n>" after its file, the shift and the header taken from
        // the end since a sheet may hold a bar, null for a page of a PDF or any other shape
        private static string[] SheetOf(string rest)
        {
            int shift = rest.LastIndexOf(ShiftMark, StringComparison.Ordinal);
            if (shift < 0)
            {
                return null;
            }
            string place = rest.Substring(0, shift);
            int column = place.LastIndexOf('|');
            if (place.StartsWith("page ", StringComparison.Ordinal) || column < 0)
            {
                return null;
            }
            return new[] { place.Substring(0, column), place.Substring(column + 1) };
        }

        private static string OtherFile(string what, string file, string given, string input)
        {
            if (given != null && file == given)
            {
                return null;
            }
            return "the " + what + " is of the file " + file + ", the " + input + " of the read is " + (given ?? "not given") + ", the read keeps the default of config (R473)";
        }

        private static string Label(string kind)
        {
            switch (kind)
            {
                case PlotOutline: return "plot outline";
                case PlotNumber: return "plot number";
                case VillaBlock: return "villa block";
                case VillaType: return "villa type";
                default: return kind;
            }
        }

        // What the read takes by picks.json with the files of its inputs. Each kind is held to the files again, so picks.json given with
        // other files than those it was resolved on keeps the default and says why
        public static PicksTaken Take(JObject picks, PickFiles files)
        {
            if (picks == null || Text(picks["format"]) != Format)
            {
                throw new InvalidDataException("the input picks is no " + FileName + " of the format " + Format + " (R473)");
            }
            var taken = new PicksTaken();
            taken.Lines.AddRange((picks["lines"] as JArray ?? new JArray()).Select(line => Text(line)));
            foreach (string kind in Kinds)
            {
                JObject entry = picks["kinds"]?[kind] as JObject;
                string source = Text(entry?["source"]), from = Text(entry?["from"]);
                if (source == "" || (from != FromPick && from != FromProposal))
                {
                    continue;
                }
                string refused = Refusal(kind, source, files);
                if (refused != null)
                {
                    taken.Lines.Add(refused);
                    continue;
                }
                string rest = source.Substring(source.IndexOf('|') + 1);
                switch (kind)
                {
                    case PlotOutline:
                        taken.PlotLineLayers = rest.Split(new[] { LayerJoin }, StringSplitOptions.None).ToList();
                        break;
                    case PlotNumber:
                        taken.PlotLabelLayers = new List<string> { rest };
                        break;
                    case VillaBlock:
                        taken.VillaBlockLayer = rest;
                        break;
                    case VillaType:
                        taken.TypologyHeader = SheetOf(rest)[1];
                        break;
                }
            }
            foreach (JObject item in Objects(picks["conflicts"]))
            {
                taken.Conflicts.Add(new PickedConflict { Plot = Text(item["plot"]), Field = Text(item["field"]), Source = Text(item["source"]), Value = Text(item["value"]) });
            }
            return taken;
        }

        // The columns of the sales rows with the typology column set to the header of the pick, when the header row of the sheet holds it as
        // LocationPlan.Table reads a header, else the columns as config gives them and a line why
        public static List<KeyValuePair<string, string>> TypologyColumns(IReadOnlyList<KeyValuePair<string, string>> columns, IReadOnlyList<object[]> rows, int headerRow, string header, List<string> lines)
        {
            if (header == null)
            {
                return columns.ToList();
            }
            bool held = headerRow >= 1 && rows.Count >= headerRow && rows[headerRow - 1].Any(cell => cell != null && SheetRows.PyStrip(SheetRows.PyText(cell)) == header);
            if (!held)
            {
                lines.Add("the villa type pick names the column " + header + ", which the header row " + headerRow + " of the sales sheet does not hold, the read keeps the column of config (R473)");
                return columns.ToList();
            }
            return columns.Select(column => column.Key == TypologyKey ? new KeyValuePair<string, string>(column.Key, header) : column).ToList();
        }

        // Each villa type conflict pick sets typologyRev9 of its plot in the plot table, keyed by the baseplan label the scan numbers a plot
        // by. A plot of no row, or a second pick of one plot with another value, sets nothing and says why. Gives the count set
        public static int ApplyConflicts(IDictionary<string, Dictionary<string, object>> table, IEnumerable<PickedConflict> conflicts, List<string> lines)
        {
            var set = new Dictionary<string, string>(StringComparer.Ordinal);
            int count = 0;
            foreach (PickedConflict conflict in conflicts ?? Enumerable.Empty<PickedConflict>())
            {
                if (conflict.Field != VillaTypeField)
                {
                    continue;
                }
                if (!table.TryGetValue(conflict.Plot, out Dictionary<string, object> row))
                {
                    lines.Add("the conflict pick of plot " + conflict.Plot + " names no plot of the plot table, nothing is set (R473)");
                    continue;
                }
                if (set.TryGetValue(conflict.Plot, out string before))
                {
                    if (before != conflict.Value)
                    {
                        lines.Add("two conflict picks give plot " + conflict.Plot + " the villa types " + before + " and " + conflict.Value + ", the first is kept (R473)");
                    }
                    continue;
                }
                row[TypologyColumn] = conflict.Value;
                set[conflict.Plot] = conflict.Value;
                count++;
            }
            return count;
        }

        // R474. What read.json records of the picks: per kind the source read and from where, and each conflict pick applied
        public static JObject Sources(JObject picks)
        {
            var kinds = new JObject();
            foreach (string kind in Kinds)
            {
                JToken entry = picks?["kinds"]?[kind];
                kinds[kind] = new JObject { ["source"] = entry?["source"]?.DeepClone() ?? JValue.CreateNull(), ["from"] = Text(entry?["from"]) == "" ? FromDefault : Text(entry["from"]) };
            }
            var conflicts = new JArray(Objects(picks?["conflicts"]).Select(item => new JObject { ["plot"] = item["plot"], ["field"] = item["field"], ["source"] = item["source"] }));
            return new JObject { ["kinds"] = kinds, ["conflicts"] = conflicts };
        }

        // The newest scan of the work folder (R439), null when it holds none or its scan.json does not parse
        public static JObject LatestScan(string workFolder)
        {
            string path = ScanStep.Latest(workFolder);
            if (path == null)
            {
                return null;
            }
            try
            {
                return JObject.Parse(WorkFiles.ReadText(path));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // The plan of the read with picks.json as its input picks in the folder of the read. The folder is named by the hash, which takes
        // the SHA-256 of picks.json and not its path, so the hash is taken first and the path set after
        public static ReadPlan WithPicks(ReadPlan files, string workFolder, JObject picks)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(Json(picks));
            string sha;
            using (SHA256 hash = SHA256.Create())
            {
                sha = string.Concat(hash.ComputeHash(bytes).Select(item => item.ToString("x2")));
            }
            var draft = new ReadPlan(files.Inputs.Concat(new[] { new ReadInput(Role, "", bytes.Length, sha) }).ToList(), files.Steps, files.Stops, files.ConfigPath);
            string path = Path.GetFullPath(Path.Combine(ReadDataRun.FolderOf(workFolder, draft), FileName));
            return new ReadPlan(files.Inputs.Concat(new[] { new ReadInput(Role, path, bytes.Length, sha) }).ToList(), files.Steps, files.Stops, files.ConfigPath);
        }

        public static string Json(JObject picks)
        {
            return picks.ToString(Formatting.Indented);
        }

        public static void Write(string path, string runId, JObject picks)
        {
            WorkFiles.WriteText(path, runId, Json(picks));
        }

        private static List<JObject> Objects(JToken token)
        {
            return (token as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
        }

        private static string Text(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? "" : token.Type == JTokenType.String ? (string)token : token.ToString();
        }
    }
}
