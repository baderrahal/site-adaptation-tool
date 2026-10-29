using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R365, docs/design/07-mock.md 7.6 and items 8 to 12 of 7.7, item 6 of docs/addin-mock.md. Every label of the report Excel in one place, word
    // for word from the mock where it gives the words, the rest CHOSEN in Round ADDIN 4g3 PR 5. A value is written <name> and filled by ReportSheets
    public static class ReportExcelText
    {
        public const string FileName = "<nbh>_Report.xlsx";

        public const string SummarySheet = "Summary";
        public const string PlacedSheet = "Placed";
        public const string SkippedSheet = "Skipped";
        public const string OverridesSheet = "Overrides";
        public const string AssumedSheet = "Assumed";
        public const string IssuesSheet = "Issues";
        public const string RulesSheet = "Rules";
        public const string SettingsSheet = "Settings";
        // 7.6 in its order, Assumed after Overrides (7.7 item 9) and Settings after them all (7.7 item 12)
        public static readonly string[] Sheets = { SummarySheet, PlacedSheet, SkippedSheet, OverridesSheet, AssumedSheet, IssuesSheet, RulesSheet, SettingsSheet };

        // 7.6: the open values stay as the mock writes them while no model is placed, Phase 6 fills them
        public const string TopLine = "[file name].rvt, NBH <nbh>, <n> plots, <profile>, run time [n] min";

        public const string Step = "Step";
        public const string Done = "Done";
        public const string Skipped = "Skipped";
        public const string Override = "Override";
        public const string Issues = "Issues";
        public static readonly string[] StepColumns = { Step, Done, Skipped, Override, Issues };
        // The buttons 1 to 7 by the ribbon of item 1 of docs/addin-mock.md
        public static readonly string[] Steps = { "1 Set up site", "2 Read data", "3 Check", "4 Place villas", "5 Floors and ramps", "6 Levels", "7 Walls" };
        // CHOSEN: the worker holds no run log of button 1 before 5b, so its counts stay open
        public const string NotRun = "not run yet";

        public const string CountsBox = "Counts agree across outputs";
        public const string Item = "Item";
        public const string Revit = "Revit";
        public const string Excel = "Excel";
        public static readonly string[] CountColumns = { Item, Revit, Excel };
        public const string Links = "Links";
        public const string Floors = "Floors";
        public const string Levels = "Levels";
        public static readonly string[] CountItems = { Links, Floors, Levels };
        public const string NotPlaced = "not placed yet";
        // CHOSEN: Excel of Levels is the level Excel of the run of button 6 that committed, none before button 6 runs in Revit
        public const string NoLevelRun = "no run of button 6 yet";

        public const string RulesBox = "Rules applied";
        public const string Decision = "Decision";
        public const string Rule = "Rule";
        public const string Share = "Share";
        public const string Strength = "Strength";
        public const string Plots = "Plots";
        public static readonly string[] RuleColumns = { Decision, Rule, Share, Strength, Plots };
        // 7.7 item 9, the count of plots that took the value of the rule
        public const string PlotCount = "<n> plots";
        public const string Strong = "strong";
        public const string Weak = "weak";
        public const string NotMeasured = "not measured";
        public const string ShareFormat = "0.000";

        public const string Plot = "Plot";
        public const string Kind = "Kind";
        public const string Type = "Type";
        public const string Mirror = "Mirror";
        public const string ElementId = "Element id";
        public static readonly string[] PlacedColumns = { Plot, Kind, Type, Mirror, ElementId };
        // D4 of steps/decisions-2026-10-28.md, items 10 and 11 of 7.7: the villa type and the mirror flag the team may edit, the fill of
        // LevelExcelText so both Excel files mark an editable header alike
        public static readonly string[] Editable = { Type, Mirror };
        public const string LinkKind = "link";

        public const string Reason = "Reason";
        public static readonly string[] SkippedColumns = { Plot, Kind, Reason };

        public const string Field = "Field";
        public const string Outcome = "Outcome";
        public const string Why = "Why";
        public const string Source = "Source";
        public const string Model = "Model";
        public static readonly string[] OverrideColumns = { Plot, Field, Outcome, Why, Reason, Source, Model };
        public const string Applied = "Applied";
        public const string Left = "Left";

        public const string Value = "Value";
        public const string Status = "Status";
        // 7.7 item 9, the Q ID, share or source that gives the status
        public const string Basis = "Basis";
        public static readonly string[] AssumedColumns = { Plot, Field, Value, Status, Basis };
        public const string ShareOf = "<rule> share <share>";
        public const string BasisJoin = " | ";

        public const string Check = "Check";
        public const string Use = "Use";
        public static readonly string[] IssueColumns = { Step, Plot, Check, Field, Source, Use, Why };
        public const string ReportStep = "8 Report";
        public const string CountsCheck = "Counts";

        public const string Values = "Values";
        public static readonly string[] RuleSheetColumns = { Rule, Status, Basis, Plots, Values };

        public const string Setting = "Setting";
        public static readonly string[] SettingColumns = { Setting, Value };
        public const string RunId = "Run id";
        public const string Profile = "Profile";
        public const string RulesSha = "Rules SHA-256";
        public const string OverridesSha = "Override list SHA-256";
        public const string RunTime = "Run time";
        public const string EditableColumns = "Editable columns";
        public const string PlanBuildId = "Plan build id";
        public const string PlanSha = "Plan SHA-256";
        public const string ListJoin = ", ";
    }

    // R365. One table of a report sheet: a bold title over it or null, its header, the number format of each column, null for a text, the
    // headers that carry the editable fill, whether its header and rows are bordered, and its rows, each cell a string, a double, a long, a
    // bool or null for an empty cell
    public sealed class ReportTable
    {
        public string Title { get; set; }
        public string[] Header { get; set; }
        public string[] Formats { get; set; }
        public string[] Editable { get; set; } = new string[0];
        public bool Bordered { get; set; }
        public List<object[]> Rows { get; } = new List<object[]>();
    }

    // A sheet: the top line in one merged bold cell over its first table or null, and its tables in order, one blank row between two
    public sealed class ReportSheet
    {
        public string Name { get; set; }
        public string TopLine { get; set; }
        public List<ReportTable> Tables { get; } = new List<ReportTable>();
    }

    public sealed class ReportBook
    {
        public string Nbh { get; set; }
        public string FileName { get; set; }
        public List<ReportSheet> Sheets { get; } = new List<ReportSheet>();
    }

    public sealed class ReportResult
    {
        public List<ReportBook> Books { get; } = new List<ReportBook>();
        public List<string> Issues { get; } = new List<string>();
        public Dictionary<string, int> Counts { get; } = new Dictionary<string, int>();
    }

    // What the report Excel is made from: the plan as written with its summary and status, the override list, the step checkRows of the
    // build, the rows of plots.csv, the rules profile, config, and the run that writes it
    public sealed class ReportInputs
    {
        public JObject Plan { get; set; }
        public string PlanSha256 { get; set; }
        public JObject Rules { get; set; }
        public string RulesSha256 { get; set; }
        public JObject Config { get; set; }
        public JObject Overrides { get; set; }
        public string OverridesSha256 { get; set; }
        public JObject CheckRows { get; set; }
        public JObject Summary { get; set; }
        public List<Dictionary<string, string>> PlotRows { get; set; }
        public string RunId { get; set; }
        public string Profile { get; set; }
        public DateTime Now { get; set; }
    }

    // R365. The sheets of the report Excel from one build, the plan as written, nothing measured again: the counts of the summary from the
    // plan and its sheets, the overrides by what the plan did with them, the rows of 7.4 of the step checkRows (R363), the status of each value
    // and its detail of the table statusFrom of the plan (R401). One workbook per NBH of the plan by nbhId of plots.csv, as R364 takes it
    public static class ReportSheets
    {
        private static string Fill(string template, params string[] pairs)
        {
            for (int index = 0; index < pairs.Length; index += 2)
            {
                template = template.Replace("<" + pairs[index] + ">", pairs[index + 1]);
            }
            return template;
        }

        private static string Text(JToken token)
        {
            return token?.Type == JTokenType.String ? (string)token : null;
        }

        private static string Number(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        // A value of the plan as one cell: a text, a number, a flag, or the JSON of a list or an object on one line
        private static object Cell(JToken value)
        {
            switch (value?.Type)
            {
                case null:
                case JTokenType.Null:
                    return null;
                case JTokenType.String:
                    return (string)value;
                case JTokenType.Integer:
                    return (long)value;
                case JTokenType.Float:
                    return (double)value;
                case JTokenType.Boolean:
                    return (bool)value;
                default:
                    return value.ToString(Formatting.None);
            }
        }

        // Review 1 of PR 96: the block addin.report of the settings, the button of each override field and the decisions of the walls
        public const string NoReportBlock = "the settings hold no block addin.report with stepOfField, defaultStep and wallDecisions, the report counts the overrides "
            + "of each button and the weak wall decisions by it (R365)";

        // Null when the plan, the rules and the request agree on the level profile and the settings hold the block addin.report, else why the
        // request is refused
        public static string Refusal(JObject plan, JObject rules, JObject config, string profile)
        {
            JObject report = config?["addin"]?["report"] as JObject;
            if (!(report?["stepOfField"] is JObject fields) || fields.Properties().Any(item => item.Value.Type != JTokenType.Integer)
                || report["defaultStep"]?.Type != JTokenType.Integer || !(report["wallDecisions"] is JArray walls) || walls.Any(item => item.Type != JTokenType.String))
            {
                return NoReportBlock;
            }
            string built = Text((plan?["summary"]?["rules"] as JObject)?["levelProfile"]);
            if (built == null)
            {
                return "the plan names no level profile in summary.rules, a report is written of a build by its level profile (R365)";
            }
            if (built != profile)
            {
                return "the plan was built with the level profile " + built + ", the request names " + profile + " (R365)";
            }
            if (!(rules?["levelProfiles"]?[profile] is JObject))
            {
                return "the rules profile holds no level profile " + profile + " (R365)";
            }
            if (!(plan["statusFrom"] is JObject))
            {
                return "the plan holds no table statusFrom, a report reads the status of each value of R401 (R365)";
            }
            return null;
        }

        // Each sourced value of a plot entry by its dotted path in the order of the entry, value_status.fields_of
        public static List<KeyValuePair<string, JObject>> FieldsOf(JObject entry)
        {
            var found = new List<KeyValuePair<string, JObject>>();
            Walk(entry, "", found);
            return found;
        }

        private static void Walk(JObject item, string path, List<KeyValuePair<string, JObject>> found)
        {
            if (item.ContainsKey("value") && item.ContainsKey("source"))
            {
                found.Add(new KeyValuePair<string, JObject>(path, item));
                return;
            }
            foreach (JProperty child in item.Properties())
            {
                if (child.Value is JObject inner)
                {
                    Walk(inner, path.Length == 0 ? child.Name : path + "." + child.Name, found);
                }
            }
        }

        // The Q ID, the share or the source of one entry of a detail: its question, else the rule with its share, else what CheckRows.RuleName names
        public static string Basis(JObject entry)
        {
            string question = Text(entry["question"]);
            if (!string.IsNullOrEmpty(question))
            {
                return question;
            }
            JToken share = entry["share"];
            if (share != null && (share.Type == JTokenType.Float || share.Type == JTokenType.Integer))
            {
                return Fill(ReportExcelText.ShareOf, "rule", CheckRows.RuleName(entry), "share", Number((double)share));
            }
            return CheckRows.RuleName(entry);
        }

        private sealed class Made
        {
            public JObject Plan;
            public JObject Details;
            public List<JObject> Written;
            public List<JObject> NotWritten;
            public List<JObject> Missing;
            public List<JObject> Walls;
            public List<JObject> Entries;
            public Dictionary<string, Dictionary<string, string>> Rows;
            public JObject StepPlots;
            public Dictionary<string, int> StepOfField;
            public int DefaultStep;
            public List<string> WallDecisions;
            public Dictionary<string, JObject> Scope;
            public List<string> Assumed;
            public List<string> Worst;
            public HashSet<string> Skipped;
        }

        public static ReportResult Build(ReportInputs inputs)
        {
            JObject plan = inputs.Plan;
            JObject rules = (JObject)plan["summary"]["rules"];
            var sources = new HashSet<string>((rules["overrideSource"] as JArray ?? new JArray()).Select(item => (string)item));
            var models = new HashSet<string>((rules["overrideModel"] as JArray ?? new JArray()).Select(item => (string)item));
            var made = new Made
            {
                Plan = plan,
                Details = plan["statusFrom"] as JObject ?? new JObject(),
                Written = (plan["plots"] as JArray ?? new JArray()).OfType<JObject>().ToList(),
                Missing = (plan["missing"] as JArray ?? new JArray()).OfType<JObject>().ToList(),
                Walls = (plan["walls"] as JArray ?? new JArray()).OfType<JObject>().ToList(),
                Entries = CheckRows.ListEntries(inputs.Overrides, sources, models).Item1,
                Rows = LevelSheets.RowsByNumber(inputs.PlotRows),
                StepPlots = inputs.CheckRows?["plots"] as JObject ?? new JObject(),
                StepOfField = ((inputs.Config["addin"]?["report"]?["stepOfField"] as JObject) ?? throw new InvalidDataException(NoReportBlock)).Properties().ToDictionary(item => item.Name, item => (int)item.Value),
                DefaultStep = (int)(inputs.Config["addin"]["report"]["defaultStep"] ?? throw new InvalidDataException(NoReportBlock)),
                WallDecisions = ((inputs.Config["addin"]["report"]["wallDecisions"] as JArray) ?? throw new InvalidDataException(NoReportBlock)).Select(item => (string)item).ToList(),
                Scope = (inputs.Summary?["plots"] as JObject ?? new JObject()).Properties().ToDictionary(item => item.Name, item => (item.Value["written"] ?? item.Value["raw"]) as JObject),
                Assumed = (inputs.Config["valueStatus"]?["assumed"] as JArray ?? new JArray()).Select(item => (string)item).ToList(),
                Worst = (inputs.Config["valueStatus"]?["worstFirst"] as JArray ?? new JArray()).Select(item => (string)item).ToList(),
                Skipped = new HashSet<string>((plan["summary"]["skipped"] as JArray ?? new JArray()).Select(item => (string)item)),
            };
            made.NotWritten = made.Missing.Where(item => item["written"]?.Type == JTokenType.Boolean && !(bool)item["written"]).ToList();
            var result = new ReportResult();
            Func<string, string> nbhOf = number =>
            {
                return made.Rows.TryGetValue(number, out Dictionary<string, string> row) && row.TryGetValue("nbhId", out string nbh) && !string.IsNullOrEmpty(nbh) ? nbh : null;
            };
            List<string> numbers = made.Written.Select(item => (string)item["plot"]).Concat(made.NotWritten.Select(item => (string)item["plot"])).ToList();
            List<string> lacking = numbers.Where(number => nbhOf(number) == null).ToList();
            if (lacking.Count > 0)
            {
                result.Issues.Add(lacking.Count + " plots of the plan have no nbhId in plots.csv, so no report lists them: " + string.Join(", ", lacking) + " (R365)");
            }
            foreach (string nbh in numbers.Select(nbhOf).Where(item => item != null).Distinct().OrderBy(item => item, StringComparer.Ordinal))
            {
                if (nbh != Path.GetFileName(nbh) || nbh.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new InvalidDataException("the nbhId " + nbh + " of plots.csv names no file (R365)");
                }
                var mine = new HashSet<string>(numbers.Where(number => nbhOf(number) == nbh));
                result.Books.Add(Book(inputs, made, nbh, mine, result));
            }
            Func<string, int> rowsOf = name => result.Books.Sum(book => book.Sheets.First(sheet => sheet.Name == name).Tables[0].Rows.Count);
            result.Counts["report workbooks"] = result.Books.Count;
            result.Counts["report plots with no NBH"] = lacking.Count;
            foreach (string name in ReportExcelText.Sheets.Skip(1))
            {
                result.Counts["report rows " + name] = rowsOf(name);
            }
            return result;
        }

        // Review 1 of PR 96: the button of the Summary table an override field counts under, addin.report.stepOfField as rows 5 and 6 of 7.6 list
        // the fields, else addin.report.defaultStep
        private static int StepOf(Made made, string field)
        {
            return made.StepOfField.TryGetValue(field, out int step) ? step : made.DefaultStep;
        }

        private static ReportBook Book(ReportInputs inputs, Made made, string nbh, HashSet<string> mine, ReportResult result)
        {
            var book = new ReportBook { Nbh = nbh, FileName = Fill(ReportExcelText.FileName, "nbh", nbh) };
            List<JObject> written = made.Written.Where(item => mine.Contains((string)item["plot"])).ToList();
            var writtenNumbers = new HashSet<string>(written.Select(item => (string)item["plot"]));
            List<JObject> missing = made.Missing.Where(item => mine.Contains((string)item["plot"])).ToList();
            List<JObject> walls = made.Walls.Where(item => writtenNumbers.Contains((string)item["plot"])).ToList();
            List<string> order = written.Select(item => (string)item["plot"]).Concat(made.NotWritten.Select(item => (string)item["plot"]).Where(mine.Contains)).ToList();

            ReportTable placed = PlacedTable(written);
            ReportTable skipped = SkippedTable(missing);
            ReportTable overrides = OverridesTable(made, written, missing);
            ReportTable assumed = AssumedTable(made, written);
            ReportTable issues = IssuesTable(made, order);
            ReportTable rulesTable = RulesTable(made, written, walls);

            var summary = new ReportSheet
            {
                Name = ReportExcelText.SummarySheet,
                TopLine = Fill(ReportExcelText.TopLine, "nbh", nbh, "n", order.Count.ToString(CultureInfo.InvariantCulture), "profile", inputs.Profile),
            };
            summary.Tables.Add(StepTable(made, inputs.Rules["levelProfiles"][inputs.Profile]["keyRules"] as JObject ?? new JObject(), order, written, walls, placed, skipped, overrides, issues, result));
            summary.Tables.Add(CountsTable(written, placed, issues, result, nbh));
            summary.Tables.Add(RulesBox(inputs, made, written, walls));
            book.Sheets.Add(summary);
            foreach (var item in new[]
            {
                Tuple.Create(ReportExcelText.PlacedSheet, placed), Tuple.Create(ReportExcelText.SkippedSheet, skipped), Tuple.Create(ReportExcelText.OverridesSheet, overrides),
                Tuple.Create(ReportExcelText.AssumedSheet, assumed), Tuple.Create(ReportExcelText.IssuesSheet, issues), Tuple.Create(ReportExcelText.RulesSheet, rulesTable),
                Tuple.Create(ReportExcelText.SettingsSheet, SettingsTable(inputs)),
            })
            {
                var sheet = new ReportSheet { Name = item.Item1 };
                sheet.Tables.Add(item.Item2);
                book.Sheets.Add(sheet);
            }
            return book;
        }

        // D4, items 10 and 11 of 7.7: plot first, one row per written plot and kind, the link with its villa type and mirror flag, then each floor
        // with its type as the plan writes them. CHOSEN: the element id stays empty until the run logs of buttons 4 and 5 are an input
        internal static ReportTable PlacedTable(List<JObject> written)
        {
            var table = new ReportTable { Header = ReportExcelText.PlacedColumns, Formats = new string[5], Editable = ReportExcelText.Editable };
            foreach (JObject plot in written)
            {
                string number = (string)plot["plot"];
                table.Rows.Add(new[] { number, ReportExcelText.LinkKind, Cell(plot["unit"]?["value"]), Cell(plot["mirrored"]?["value"]), null });
                foreach (JProperty floor in (plot["floors"] as JObject ?? new JObject()).Properties())
                {
                    table.Rows.Add(new[] { number, floor.Name, Cell(floor.Value["type"]?["value"]), null, null });
                }
            }
            return table;
        }

        // A plot not written skips its link and every floor kind with the keys it lacks, a written plot each floor key it lacks with why
        private static ReportTable SkippedTable(List<JObject> missing)
        {
            var table = new ReportTable { Header = ReportExcelText.SkippedColumns, Formats = new string[3] };
            foreach (JObject item in missing)
            {
                string number = (string)item["plot"];
                List<JObject> lacks = (item["missing"] as JArray ?? new JArray()).OfType<JObject>().ToList();
                if (item["written"]?.Type == JTokenType.Boolean && !(bool)item["written"])
                {
                    string why = string.Join(ReportExcelText.ListJoin, lacks.Select(found => Fill(LevelExcelText.MissingReason, "key", Text(found["key"]), "why", Text(found["why"]))));
                    foreach (string kind in new[] { ReportExcelText.LinkKind }.Concat(SiteCoordinates.FloorKinds))
                    {
                        table.Rows.Add(new object[] { number, kind, why });
                    }
                    continue;
                }
                foreach (JObject found in lacks)
                {
                    string key = Text(found["key"]) ?? "";
                    string kind = key.StartsWith("floors.", StringComparison.Ordinal) ? key.Substring("floors.".Length) : key;
                    table.Rows.Add(new object[] { number, kind, Text(found["why"]) });
                }
            }
            return table;
        }

        // The overrides the plan applied and left, by plot in the order of the plan and field in the order it lists them, with the reason, source and
        // model of the first entry of the list for the plot and field
        private static ReportTable OverridesTable(Made made, List<JObject> written, List<JObject> missing)
        {
            var table = new ReportTable { Header = ReportExcelText.OverrideColumns, Formats = new string[7] };
            var firstOf = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JObject entry in made.Entries)
            {
                string key = Text(entry["plot"]) + "\n" + Text(entry["field"]);
                if (!firstOf.ContainsKey(key))
                {
                    firstOf[key] = entry;
                }
            }
            IEnumerable<Tuple<string, JObject>> records = written.Select(plot => Tuple.Create((string)plot["plot"], plot["overrides"] as JObject))
                .Concat(missing.Where(item => item["written"]?.Type == JTokenType.Boolean && !(bool)item["written"]).Select(item => Tuple.Create((string)item["plot"], item["overrides"] as JObject)));
            foreach (Tuple<string, JObject> record in records.Where(item => item.Item2 != null))
            {
                Action<string, string, object> add = (field, outcome, why) =>
                {
                    firstOf.TryGetValue(record.Item1 + "\n" + field, out JObject entry);
                    table.Rows.Add(new[] { record.Item1, field, outcome, why, Cell(entry?["reason"]), Cell(entry?["source"]), Cell(entry?["model"]) });
                };
                foreach (string field in (record.Item2["applied"] as JArray ?? new JArray()).Select(item => (string)item))
                {
                    add(field, ReportExcelText.Applied, null);
                }
                foreach (JProperty field in (record.Item2["notApplied"] as JObject ?? new JObject()).Properties())
                {
                    add(field.Name, ReportExcelText.Left, Cell(field.Value));
                }
            }
            return table;
        }

        // 7.7 item 9: one row per written plot and field whose status valueStatus.assumed names, the basis of each entry its worst status ties on
        private static ReportTable AssumedTable(Made made, List<JObject> written)
        {
            var table = new ReportTable { Header = ReportExcelText.AssumedColumns, Formats = new string[5] };
            foreach (JObject plot in written)
            {
                foreach (KeyValuePair<string, JObject> field in FieldsOf(plot))
                {
                    JObject detail = made.Details[Text(field.Value["statusFrom"]) ?? ""] as JObject;
                    string label = ValueStatus.Label(Text(field.Value["status"]), detail);
                    if (!made.Assumed.Contains(label))
                    {
                        continue;
                    }
                    List<JObject> entries = (detail?["entries"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
                    string basis = string.Join(ReportExcelText.BasisJoin, ValueStatus.WorstEntries(entries, made.Worst).Select(Basis).Distinct());
                    table.Rows.Add(new[] { (string)plot["plot"], field.Key, Cell(field.Value["value"]), label, basis });
                }
            }
            return table;
        }

        // The rows of 7.4 of the plots of the NBH as the step checkRows of the build holds them (R363), in the order of the plan
        private static ReportTable IssuesTable(Made made, List<string> order)
        {
            var table = new ReportTable { Header = ReportExcelText.IssueColumns, Formats = new string[7] };
            foreach (string number in order)
            {
                JObject step = made.StepPlots[number] as JObject;
                JArray rows = (step?["written"] ?? step?["raw"])?["rows"] as JArray ?? new JArray();
                foreach (JObject row in rows.OfType<JObject>())
                {
                    table.Rows.Add(new[] { ReportExcelText.Steps[2], number, Cell(row["check"]), Cell(row["field"]), Cell(row["from"]), Cell(row["use"]), Cell(row["why"]) });
                }
            }
            return table;
        }

        // Each value of the written plots of the NBH and of the walls they stand on, with the detail of its status
        private static IEnumerable<Tuple<string, JObject>> Valued(Made made, List<JObject> written, List<JObject> walls)
        {
            IEnumerable<Tuple<string, JObject>> values = written.SelectMany(plot => FieldsOf(plot).Select(pair => Tuple.Create((string)plot["plot"], pair.Value)))
                .Concat(walls.SelectMany(wall => WallValues(wall).Select(value => Tuple.Create((string)wall["plot"], value))));
            foreach (Tuple<string, JObject> value in values)
            {
                yield return Tuple.Create(value.Item1, made.Details[Text(value.Item2["statusFrom"]) ?? ""] as JObject);
            }
        }

        // The sourced values of a wall of the plan, its plots, ends, base and top (R362)
        private static IEnumerable<JObject> WallValues(JObject wall)
        {
            return wall.Properties().Select(child => child.Value).OfType<JObject>().Where(value => value.ContainsKey("value") && value.ContainsKey("source"));
        }

        // Every rule an entry of a detail names, the rules the values of the NBH read, with its status, basis, plots and values (CHOSEN, the rule
        // text of docs/rules is not shipped with the add-in, the block entry the build read names the rule)
        private static ReportTable RulesTable(Made made, List<JObject> written, List<JObject> walls)
        {
            var table = new ReportTable { Header = ReportExcelText.RuleSheetColumns, Formats = new string[5] };
            var grouped = new Dictionary<string, Tuple<string, string, string, HashSet<string>, int[]>>(StringComparer.Ordinal);
            foreach (Tuple<string, JObject> value in Valued(made, written, walls))
            {
                foreach (JObject entry in (value.Item2?["entries"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    string rule = CheckRows.RuleName(entry) ?? "";
                    string label = ValueStatus.Label(Text(entry["status"]), entry);
                    string key = rule + "\n" + label;
                    if (!grouped.TryGetValue(key, out Tuple<string, string, string, HashSet<string>, int[]> item))
                    {
                        grouped[key] = item = Tuple.Create(rule, label, Basis(entry), new HashSet<string>(StringComparer.Ordinal), new int[1]);
                    }
                    item.Item4.Add(value.Item1);
                    item.Item5[0]++;
                }
            }
            foreach (Tuple<string, string, string, HashSet<string>, int[]> item in grouped.Values.OrderBy(pair => pair.Item1, StringComparer.Ordinal).ThenBy(pair => pair.Item2, StringComparer.Ordinal))
            {
                table.Rows.Add(new object[] { item.Item1, item.Item2, item.Item3, (long)item.Item4.Count, (long)item.Item5[0] });
            }
            return table;
        }

        private static ReportTable StepTable(Made made, JObject keyRules, List<string> order, List<JObject> written, List<JObject> walls, ReportTable placed, ReportTable skipped,
            ReportTable overrides, ReportTable issues, ReportResult result)
        {
            var table = new ReportTable { Header = ReportExcelText.StepColumns, Formats = new string[5] };
            Func<int, string, long> outcome = (step, word) => overrides.Rows.Count(row => (string)row[2] == word && StepOf(made, (string)row[1]) == step);
            Func<ReportTable, Func<string, bool>, long> kinds = (sheet, kind) => sheet.Rows.Count(row => kind((string)row[1]));
            Func<string, bool> isFloor = kind => SiteCoordinates.FloorKinds.Contains(kind);
            var orderSet = new HashSet<string>(order);
            // Review 1 of PR 96: Done and Skipped of 2 Read data over the one scope of the build, the plots of the step summary with a unit and with
            // none, never the faces of plots.csv outside the scope
            foreach (string number in order.Where(number => !made.Scope.ContainsKey(number)))
            {
                result.Issues.Add("plot " + number + " is in the plan and not in the step summary of the build, so 2 Read data counts it with no unit (R365)");
            }
            long withUnit = order.Count(number => made.Scope.TryGetValue(number, out JObject plot) && plot?["unit"]?.Type == JTokenType.String && ((string)plot["unit"]).Length > 0);
            long listed = made.Entries.Count(entry => orderSet.Contains(Text(entry["plot"]) ?? ""));
            long movedWalls = walls.Count(wall => WallValues(wall).Any(value => (made.Details[Text(value["statusFrom"]) ?? ""]?["overrides"] as JArray)?.Count > 0));
            // Review 1 of PR 96: the Issues of 7 Walls are the weak keyRules decisions of the walls, as 7.6 counts the weak wallTop
            long weakWalls = made.WallDecisions.Count(name => keyRules[name]?["strong"]?.Type == JTokenType.Boolean && !(bool)keyRules[name]["strong"]);
            long notWritten = order.Count - written.Count;
            table.Rows.Add(new object[] { ReportExcelText.Steps[0], ReportExcelText.NotRun, ReportExcelText.NotRun, ReportExcelText.NotRun, ReportExcelText.NotRun });
            table.Rows.Add(new object[] { ReportExcelText.Steps[1], withUnit, order.Count - withUnit, 0L, 0L });
            table.Rows.Add(new object[] { ReportExcelText.Steps[2], (long)order.Count, (long)order.Count(made.Skipped.Contains), listed, (long)issues.Rows.Count });
            table.Rows.Add(new object[] { ReportExcelText.Steps[3], kinds(placed, kind => kind == ReportExcelText.LinkKind), kinds(skipped, kind => kind == ReportExcelText.LinkKind), outcome(4, ReportExcelText.Applied), outcome(4, ReportExcelText.Left) });
            table.Rows.Add(new object[] { ReportExcelText.Steps[4], kinds(placed, isFloor), kinds(skipped, isFloor), outcome(5, ReportExcelText.Applied), outcome(5, ReportExcelText.Left) });
            table.Rows.Add(new object[] { ReportExcelText.Steps[5], (long)written.Count, notWritten, outcome(6, ReportExcelText.Applied), outcome(6, ReportExcelText.Left) });
            table.Rows.Add(new object[] { ReportExcelText.Steps[6], (long)walls.Count, 0L, movedWalls, weakWalls });
            return table;
        }

        // The box Counts agree across outputs: Revit open until the read of 5e fills it, Excel the rows of the Placed sheet the report built, the plan
        // held against both apart and a mismatch of any two counts an issue of the report and the run
        internal static ReportTable CountsTable(List<JObject> written, ReportTable placed, ReportTable issues, ReportResult result, string nbh)
        {
            var table = new ReportTable { Title = ReportExcelText.CountsBox, Header = ReportExcelText.CountColumns, Formats = new string[3], Bordered = true };
            long links = placed.Rows.Count(row => (string)row[1] == ReportExcelText.LinkKind);
            long floors = placed.Rows.Count(row => SiteCoordinates.FloorKinds.Contains((string)row[1]));
            long planLinks = written.Count;
            long planFloors = written.Sum(plot => (plot["floors"] as JObject ?? new JObject()).Count);
            table.Rows.Add(new object[] { ReportExcelText.Links, ReportExcelText.NotPlaced, links });
            table.Rows.Add(new object[] { ReportExcelText.Floors, ReportExcelText.NotPlaced, floors });
            table.Rows.Add(new object[] { ReportExcelText.Levels, ReportExcelText.NotPlaced, ReportExcelText.NoLevelRun });
            foreach (var item in new[] { Tuple.Create(ReportExcelText.Links, planLinks, links), Tuple.Create(ReportExcelText.Floors, planFloors, floors) })
            {
                if (item.Item2 != item.Item3)
                {
                    string why = "the plan holds " + item.Item2 + ", Revit " + ReportExcelText.NotPlaced + ", Excel " + item.Item3 + " (R365)";
                    result.Issues.Add("NBH " + nbh + " " + item.Item1 + ": " + why);
                    issues.Rows.Add(new object[] { ReportExcelText.ReportStep, null, ReportExcelText.CountsCheck, item.Item1, null, null, why });
                }
            }
            return table;
        }

        // The box Rules applied: one row per decision of keyRules of the level profile with its rule, share, strong or weak, and the plots of the
        // NBH holding a value whose status reads that rule (7.7 item 9), by the walls they stand on as well
        private static ReportTable RulesBox(ReportInputs inputs, Made made, List<JObject> written, List<JObject> walls)
        {
            var table = new ReportTable { Title = ReportExcelText.RulesBox, Header = ReportExcelText.RuleColumns, Formats = new[] { null, null, ReportExcelText.ShareFormat, null, null }, Bordered = true };
            JObject keyRules = inputs.Rules["levelProfiles"][inputs.Profile]["keyRules"] as JObject ?? new JObject();
            string file = Text(inputs.Config["profile"]) ?? "";
            var byEntry = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            // Review 1 of PR 96: a value whose detail names an override took the value of the override, so it counts under no keyRules entry
            foreach (Tuple<string, JObject> value in Valued(made, written, walls).Where(item => !((item.Item2?["overrides"] as JArray)?.Count > 0)))
            {
                foreach (string entry in (value.Item2?["entries"] as JArray ?? new JArray()).OfType<JObject>().Select(item => Text(item["entry"])).Where(item => item != null).Distinct())
                {
                    if (!byEntry.TryGetValue(entry, out HashSet<string> plots))
                    {
                        byEntry[entry] = plots = new HashSet<string>(StringComparer.Ordinal);
                    }
                    plots.Add(value.Item1);
                }
            }
            foreach (JProperty decision in keyRules.Properties().Where(item => item.Value is JObject rule && rule["rule"]?.Type == JTokenType.String))
            {
                JToken share = decision.Value["share"];
                bool measured = share != null && (share.Type == JTokenType.Float || share.Type == JTokenType.Integer);
                JToken strong = decision.Value["strong"];
                string strength = strong?.Type == JTokenType.Boolean ? ((bool)strong ? ReportExcelText.Strong : ReportExcelText.Weak) : ReportExcelText.NotMeasured;
                byEntry.TryGetValue(file + " levelProfiles." + inputs.Profile + ".keyRules." + decision.Name + ".rule", out HashSet<string> took);
                table.Rows.Add(new object[]
                {
                    decision.Name, (string)decision.Value["rule"], measured ? (object)(double)share : ReportExcelText.NotMeasured, strength,
                    Fill(ReportExcelText.PlotCount, "n", (took?.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
                });
            }
            return table;
        }

        private static ReportTable SettingsTable(ReportInputs inputs)
        {
            var table = new ReportTable { Header = ReportExcelText.SettingColumns, Formats = new string[2] };
            var rows = new[]
            {
                new[] { ReportExcelText.RunId, inputs.RunId },
                new[] { ReportExcelText.Profile, inputs.Profile },
                new[] { ReportExcelText.RulesSha, inputs.RulesSha256 },
                new[] { ReportExcelText.OverridesSha, inputs.OverridesSha256 },
                new[] { ReportExcelText.RunTime, inputs.Now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) },
                new[] { ReportExcelText.EditableColumns, string.Join(ReportExcelText.ListJoin, ReportExcelText.PlacedColumns.Where(ReportExcelText.Editable.Contains)) },
                new[] { ReportExcelText.PlanBuildId, Text((inputs.Plan["run"] as JObject)?["runId"]) ?? LevelExcelText.NoBuildId },
                new[] { ReportExcelText.PlanSha, inputs.PlanSha256 },
            };
            foreach (string[] row in rows)
            {
                table.Rows.Add(row.Cast<object>().ToArray());
            }
            return table;
        }
    }
}
