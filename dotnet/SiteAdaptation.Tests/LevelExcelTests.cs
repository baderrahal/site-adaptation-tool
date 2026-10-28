using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CadReader;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R364. The level Excel of the command levelExcel of the worker on a made up plan, its override list, its clusters, its plots.csv,
    // rules and config built in code (R10), read back with DocumentFormat.OpenXml, on net48 and net8.0. python/level_excel_check.py reads
    // the same file a second way with openpyxl
    public sealed class LevelExcelTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r364_" + Guid.NewGuid().ToString("N"));

        public LevelExcelTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
        }

        private static readonly string[] Columns = { "Plot No", "Unit", "Cluster", "Road at front", "El @ Entry", "FFL", "SSL", "Height", "Steps", "Source" };

        private const string TopLine = "NBH 012, Zone 2, road surface Road A on 6 of 6 front corners, formulas: El @ Entry = the road at the two front corners + the slope "
            + "of the unit x the ramp depth (R278, R339), FFL = the highest El @ Entry of the piece + the base of its unit, to the mm (R340), SSL = FFL - 0.075 m "
            + "(levels.ground_ssl), Height = FFL - El @ Entry, Steps = one for the base and one per riser over it, half a riser no step (R340)";

        private static JObject Levels(double road, double entry, double ffl, double ssl, double height, int steps)
        {
            Func<object, JObject> value = item => new JObject { ["value"] = JToken.FromObject(item), ["source"] = "estimate", ["status"] = "MEASURED" };
            return new JObject { ["ffl"] = value(ffl), ["ssl"] = value(ssl), ["entry"] = value(entry), ["height"] = value(height), ["steps"] = value(steps), ["roadAtFrontM"] = value(road) };
        }

        private static JObject Plot(string number, string unit, JObject levels, params string[] applied)
        {
            var plot = new JObject { ["plot"] = number, ["unit"] = new JObject { ["value"] = unit, ["source"] = "locationPlan", ["status"] = "DOCUMENT" }, ["levels"] = levels };
            if (applied.Length > 0)
            {
                plot["overrides"] = new JObject { ["applied"] = new JArray(applied), ["notApplied"] = new JObject() };
            }
            return plot;
        }

        // Four plots: 101 a lone villa with a height override and an override of no level field, 102 and 103 a duplex where 102 has two
        // level overrides, and 104 not written for its FFL, in a cluster with 105, which the plan never names
        internal static JObject MadePlan()
        {
            return new JObject
            {
                ["format"] = "site-adaptation-plan",
                ["formatVersion"] = 1,
                ["neighbourhood"] = "012",
                ["summary"] = new JObject
                {
                    ["roadSurfacesOfFrontCorners"] = new JObject { ["Road A"] = 6 },
                    ["rules"] = new JObject { ["levelProfile"] = "parsons2024Rev6", ["overrideSource"] = new JArray("parsons"), ["overrideModel"] = new JArray() },
                },
                ["plots"] = new JArray
                {
                    Plot("101", "VD4", Levels(9.9012, 9.9234, 10.123, 10.048, 0.1996, 1), "heightM", "parameter CASE"),
                    Plot("102", "DP1", Levels(10.3801, 10.4, 10.5, 10.425, 0.1, 1), "entryM", "rampFrontM"),
                    Plot("103", "DP1", Levels(10.2857, 10.3115, 10.5, 10.425, 0.1885, 2)),
                },
                ["missing"] = new JArray
                {
                    new JObject { ["plot"] = "101", ["written"] = true, ["missing"] = new JArray(new JObject { ["key"] = "floors.yard", ["why"] = "no yard" }) },
                    new JObject { ["plot"] = "104", ["written"] = false, ["missing"] = new JArray(new JObject { ["key"] = "levels.ffl", ["why"] = "no road level" }) },
                },
            };
        }

        private static JObject Entry(string plot, string field, JToken value, string reason, string source = "parsons")
        {
            return new JObject { ["plot"] = plot, ["field"] = field, ["value"] = value, ["source"] = source, ["model"] = "model_" + source + ".rvt", ["key"] = "k1", ["reason"] = reason };
        }

        internal static JObject MadeOverrides()
        {
            return new JObject
            {
                ["overrides"] = new JArray
                {
                    Entry("101", "heightM", new JObject { ["gapToRuleM"] = 0.0006 }, "breaks oneFflPerPieceAtTheHighestEntry"),
                    Entry("101", "heightM", new JObject { ["gapToRuleM"] = 0.5 }, "a native entry the build does not take", "native"),
                    Entry("101", "parameter CASE", "D", "no level field"),
                    Entry("102", "entryM", new JObject { ["gapToRuleM"] = 0.0123 }, "breaks roadLevel"),
                    Entry("102", "rampFrontM", new JObject { ["gapToRuleM"] = -0.002 }, "breaks roadLevel"),
                },
            };
        }

        private static JObject MadeClusters()
        {
            Func<string[], JObject> cluster = plots => new JObject { ["written"] = new JObject { ["plots"] = new JArray(plots), ["code"] = "Duplex_DP1", ["source"] = "footprint" } };
            return new JObject { ["format"] = "siteAdaptationStep", ["step"] = "clusters", ["plots"] = new JObject { ["102|103"] = cluster(new[] { "102", "103" }), ["104|105"] = cluster(new[] { "104", "105" }) } };
        }

        private static JObject MadeRules()
        {
            return JObject.Parse("{\"units_mm\": true, \"levels\": {\"ground_ssl\": -75}, \"levelProfiles\": {\"parsons2024Rev6\": {\"frontRoad\": {\"surface\": \"roadLevel\", \"offsetM\": 0.0}, "
                + "\"keyRules\": {\"ffl\": {\"rule\": \"oneFflPerPieceAtTheHighestEntry\"}, \"levelSplit\": {\"rule\": \"units\", \"table\": {\"DP1 DP1\": \"middle\"}}}}}}");
        }

        private static JObject MadeConfig()
        {
            return new JObject
            {
                ["siteBuild"] = new JObject { ["minFreeGB"] = 2.0, ["roadSurfaces"] = new JArray("Road A", "Road B") },
                ["placerCheck"] = new JObject { ["level"] = new JObject { ["passMm"] = 1.0 } },
                ["addin"] = new JObject
                {
                    ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "Road.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) },
                    ["workerTimeLimitS"] = new JObject { ["levelExcel"] = 60 },
                    ["workerTimeLimitMinS"] = 10,
                },
            };
        }

        private string Write(string name, string text)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text);
            return path;
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        private sealed class Made
        {
            public JObject Plan = MadePlan();
            public JObject Overrides = MadeOverrides();
            public JObject Rules = MadeRules();
            public string Profile = "parsons2024Rev6";
        }

        private WorkerAnswer Run(Made made, out int code, out string planPath, string id = "lx1")
        {
            planPath = Write("plan_012.json", made.Plan.ToString());
            var inputs = new List<WorkerFile>
            {
                Input("config", Write("project.json", MadeConfig().ToString())),
                Input("rules", Write("rules.json", made.Rules.ToString())),
                Input(LevelWorkbook.PlanRole, planPath),
                Input(LevelWorkbook.OverridesRole, Write("overrides.json", made.Overrides.ToString())),
                Input(LevelWorkbook.ClustersRole, Write("step_clusters_python.json", MadeClusters().ToString())),
                Input("plotsCsv", Write("plots.csv", "locationPlanNumber,baseplanLabel,zone\n101,A1,Zone 2\n102,A2,Zone 2\n103,A3,Zone 2\n104,A4,Zone 2\n105,A5,Zone 3\n")),
            };
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = id, Command = "levelExcel", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = inputs, LevelProfile = made.Profile, Scope = new WorkerScope { Kind = "nbh", Nbh = "012" }, OutputFolder = Path.Combine(folder, "out"), TimeLimitS = 60,
            };
            code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            return WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, id));
        }

        private Book RunBook(Made made, out WorkerAnswer answer, out string planPath)
        {
            int code;
            answer = Run(made, out code, out planPath);
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            string path = Path.Combine(folder, "out", "012_Levels_parsons2024Rev6.xlsx");
            Assert.Equal(new[] { path }, answer.Outputs.Select(item => item.Path).ToArray());
            return Book.Read(path);
        }

        // A workbook read back: per sheet in order its rows of cells by column letter, the merged ranges and the style of each cell
        internal sealed class Book
        {
            public List<string> Names = new List<string>();
            public Dictionary<string, SortedDictionary<int, Dictionary<string, Cell>>> Sheets = new Dictionary<string, SortedDictionary<int, Dictionary<string, Cell>>>();
            public Dictionary<string, List<string>> Merged = new Dictionary<string, List<string>>();
            public Stylesheet Styles;

            public static Book Read(string path)
            {
                var book = new Book();
                using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
                {
                    WorkbookPart part = document.WorkbookPart;
                    book.Styles = (Stylesheet)part.WorkbookStylesPart.Stylesheet.CloneNode(true);
                    foreach (Sheet sheet in part.Workbook.Sheets.Elements<Sheet>())
                    {
                        string name = sheet.Name.Value;
                        book.Names.Add(name);
                        Worksheet worksheet = ((WorksheetPart)part.GetPartById(sheet.Id.Value)).Worksheet;
                        var rows = new SortedDictionary<int, Dictionary<string, Cell>>();
                        foreach (Row row in worksheet.GetFirstChild<SheetData>().Elements<Row>())
                        {
                            rows[(int)row.RowIndex.Value] = row.Elements<Cell>().ToDictionary(cell => new string(cell.CellReference.Value.TakeWhile(char.IsLetter).ToArray()), cell => (Cell)cell.CloneNode(true));
                        }
                        book.Sheets[name] = rows;
                        MergeCells merges = worksheet.GetFirstChild<MergeCells>();
                        book.Merged[name] = merges == null ? new List<string>() : merges.Elements<MergeCell>().Select(merge => merge.Reference.Value).ToList();
                    }
                }
                return book;
            }

            public static object Value(Cell cell)
            {
                if (cell == null)
                {
                    return null;
                }
                if (cell.DataType != null && cell.DataType.Value == CellValues.InlineString)
                {
                    return cell.InlineString.InnerText;
                }
                return cell.CellValue == null ? null : (object)double.Parse(cell.CellValue.Text, CultureInfo.InvariantCulture);
            }

            public List<object> Row(string sheet, int index)
            {
                Dictionary<string, Cell> cells;
                if (!Sheets[sheet].TryGetValue(index, out cells))
                {
                    return new List<object>();
                }
                int last = cells.Keys.Select(Column).DefaultIfEmpty(0).Max();
                return Enumerable.Range(1, last).Select(column => { Cell cell; return cells.TryGetValue(Letter(column), out cell) ? Value(cell) : null; }).ToList();
            }

            public string Format(Cell cell)
            {
                CellFormat format = Styles.CellFormats.Elements<CellFormat>().ElementAt((int)(cell.StyleIndex?.Value ?? 0));
                uint id = format.NumberFormatId?.Value ?? 0;
                NumberingFormat custom = Styles.NumberingFormats?.Elements<NumberingFormat>().FirstOrDefault(item => item.NumberFormatId.Value == id);
                return custom != null ? custom.FormatCode.Value : id == 1 ? "0" : id == 0 ? "General" : "builtin " + id;
            }

            // The colour of the solid fill of a cell, null for a cell with no fill
            public string Fill(Cell cell)
            {
                CellFormat format = Styles.CellFormats.Elements<CellFormat>().ElementAt((int)(cell.StyleIndex?.Value ?? 0));
                PatternFill pattern = Styles.Fills.Elements<Fill>().ElementAt((int)(format.FillId?.Value ?? 0)).PatternFill;
                return pattern?.PatternType?.Value == PatternValues.Solid ? pattern.ForegroundColor?.Rgb?.Value : null;
            }

            public bool Bold(Cell cell)
            {
                CellFormat format = Styles.CellFormats.Elements<CellFormat>().ElementAt((int)(cell.StyleIndex?.Value ?? 0));
                return Styles.Fonts.Elements<Font>().ElementAt((int)(format.FontId?.Value ?? 0)).Bold != null;
            }

            public static int Column(string letters)
            {
                return letters.Aggregate(0, (sum, letter) => sum * 26 + letter - 'A' + 1);
            }

            public static string Letter(int column)
            {
                string text = "";
                for (; column > 0; column = (column - 1) / 26)
                {
                    text = (char)('A' + (column - 1) % 26) + text;
                }
                return text;
            }
        }

        [Fact]
        public void R364_the_level_excel_holds_the_top_line_the_ten_columns_and_the_four_sheets_of_the_mock_in_their_order()
        {
            WorkerAnswer answer;
            string plan;
            Book book = RunBook(new Made(), out answer, out plan);
            Assert.Equal(new[] { "Levels", "Clusters", "Overrides used", "Settings" }, book.Names.ToArray());
            // The package is one the schema of OpenXml holds valid, a third check beside this read and openpyxl
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(Path.Combine(folder, "out", "012_Levels_parsons2024Rev6.xlsx"), false))
            {
                Assert.Empty(new OpenXmlValidator().Validate(document).Select(error => error.Description));
            }
            Assert.Equal(new object[] { TopLine }, book.Row("Levels", 1).ToArray());
            Assert.True(book.Bold(book.Sheets["Levels"][1]["A"]));
            Assert.Equal(new[] { "A1:J1" }, book.Merged["Levels"].ToArray());
            Assert.Equal(Columns, book.Row("Levels", 2).Cast<string>().ToArray());
            Assert.Equal(new[] { "Cluster", "Plots", "Unit sequence", "Split", "FFL" }, book.Row("Clusters", 1).Cast<string>().ToArray());
            Assert.Equal(new[] { "Plot No", "Field", "Gap", "Reason", "Source", "Model" }, book.Row("Overrides used", 1).Cast<string>().ToArray());
            Assert.Equal(new[] { "Setting", "Value" }, book.Row("Settings", 1).Cast<string>().ToArray());
            Assert.Equal(new[] { "Run id", "Profile", "Rules SHA-256", "Override list SHA-256", "Road surface order", "Pass limit", "Run time", "Editable columns", "Plan build id", "Plan SHA-256" },
                book.Sheets["Settings"].Keys.Skip(1).Select(row => (string)book.Row("Settings", row)[0]).ToArray());
        }

        [Fact]
        public void R364_every_value_comes_from_the_plan_as_written_with_4_decimals_for_road_entry_and_height_and_3_for_ffl_and_ssl()
        {
            WorkerAnswer answer;
            string plan;
            Book book = RunBook(new Made(), out answer, out plan);
            Assert.Equal(new object[] { "101", "VD4", "none", 9.9012, 9.9234, 10.123, 10.048, 0.1996, 1.0, "Override heightM (breaks oneFflPerPieceAtTheHighestEntry)" }, book.Row("Levels", 3).ToArray());
            Assert.Equal(new object[] { "102", "DP1", "102-103", 10.3801, 10.4, 10.5, 10.425, 0.1, 1.0, "Override entryM (breaks roadLevel), rampFrontM (breaks roadLevel)" }, book.Row("Levels", 4).ToArray());
            Assert.Equal(new object[] { "103", "DP1", "102-103", 10.2857, 10.3115, 10.5, 10.425, 0.1885, 2.0, "Rule" }, book.Row("Levels", 5).ToArray());
            // A plot not written comes last with its reason, keyed by its number like every other row
            Assert.Equal(new object[] { "104", null, "104-105", null, null, null, null, null, null, "Not written: levels.ffl (no road level)" }, book.Row("Levels", 6).ToArray());
            Dictionary<string, Cell> row = book.Sheets["Levels"][3];
            Assert.Equal(new[] { "0.0000", "0.0000", "0.000", "0.000", "0.0000", "0" }, new[] { "D", "E", "F", "G", "H", "I" }.Select(column => book.Format(row[column])).ToArray());
            Assert.Equal(3, answer.Counts["level rows written"]);
            Assert.Equal(1, answer.Counts["level rows not written"]);
        }

        [Fact]
        public void R364_the_clusters_the_overrides_used_and_the_settings_come_from_the_plan_its_override_list_and_the_request()
        {
            WorkerAnswer answer;
            string plan;
            Book book = RunBook(new Made(), out answer, out plan);
            // One row per piece of a cluster with a written plot, its FFL as the plan writes it, the split of keyRules.levelSplit by its unit sequence
            Assert.Equal(new[] { 1, 2 }, book.Sheets["Clusters"].Keys.ToArray());
            Assert.Equal(new object[] { "102-103", "102-103", "DP1 DP1", "middle", 10.5 }, book.Row("Clusters", 2).ToArray());
            // The level overrides the plan applied, of the source and models of the plan only, parameter CASE and the native entry left out
            Assert.Equal(new object[] { "101", "heightM", 0.0006, "breaks oneFflPerPieceAtTheHighestEntry", "parsons", "model_parsons.rvt" }, book.Row("Overrides used", 2).ToArray());
            Assert.Equal(new object[] { "102", "entryM", 0.0123, "breaks roadLevel", "parsons", "model_parsons.rvt" }, book.Row("Overrides used", 3).ToArray());
            Assert.Equal(new object[] { "102", "rampFrontM", -0.002, "breaks roadLevel", "parsons", "model_parsons.rvt" }, book.Row("Overrides used", 4).ToArray());
            Assert.Equal(4, book.Sheets["Overrides used"].Count);
            Dictionary<string, object> settings = book.Sheets["Settings"].Keys.Skip(1).ToDictionary(row => (string)book.Row("Settings", row)[0], row => book.Row("Settings", row)[1]);
            Assert.Equal("lx1", settings["Run id"]);
            Assert.Equal("parsons2024Rev6", settings["Profile"]);
            Assert.Equal(FileHash.Sha256(Path.Combine(folder, "rules.json")), settings["Rules SHA-256"]);
            Assert.Equal(FileHash.Sha256(Path.Combine(folder, "overrides.json")), settings["Override list SHA-256"]);
            Assert.Equal("Road A, Road B", settings["Road surface order"]);
            Assert.Equal("1 mm", settings["Pass limit"]);
            Assert.Matches("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$", (string)settings["Run time"]);
            Assert.Empty(answer.Issues);
        }

        [Fact]
        public void R364_plot_no_keys_column_a_one_row_per_plot_with_no_merged_cell_or_blank_row_under_the_top_line()
        {
            WorkerAnswer answer;
            string plan;
            Book book = RunBook(new Made(), out answer, out plan);
            SortedDictionary<int, Dictionary<string, Cell>> levels = book.Sheets["Levels"];
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, levels.Keys.ToArray());
            Assert.Equal("Plot No", Book.Value(levels[2]["A"]));
            List<string> keys = levels.Keys.Skip(2).Select(row => (string)Book.Value(levels[row]["A"])).ToList();
            Assert.Equal(new[] { "101", "102", "103", "104" }, keys.ToArray());
            Assert.Equal(keys.Count, keys.Distinct().Count());
            // The one merged range is the top line, nothing under it is merged, in no sheet
            Assert.All(book.Merged["Levels"], range => Assert.Equal("A1:J1", range));
            Assert.All(book.Names.Where(name => name != "Levels"), name => Assert.Empty(book.Merged[name]));
            // Every row of every sheet under its header holds its key in column A, so no row is blank
            Assert.All(book.Names, name => Assert.All(book.Sheets[name].Values, row => Assert.False(string.IsNullOrEmpty(Book.Value(row.ContainsKey("A") ? row["A"] : null) as string))));
        }

        [Fact]
        public void R364_only_the_headers_of_ffl_el_at_entry_and_steps_carry_the_editable_fill_and_settings_lists_those_columns()
        {
            WorkerAnswer answer;
            string plan;
            Book book = RunBook(new Made(), out answer, out plan);
            Dictionary<string, Cell> header = book.Sheets["Levels"][2];
            Dictionary<string, string> fills = header.ToDictionary(cell => (string)Book.Value(cell.Value), cell => book.Fill(cell.Value));
            Assert.Equal(new[] { "El @ Entry", "FFL", "Steps" }, fills.Where(item => item.Value != null).Select(item => item.Key).ToArray());
            Assert.All(fills.Where(item => item.Value != null), item => Assert.Equal(LevelExcelText.EditableFill, item.Value));
            // No other cell of the workbook carries a fill
            Assert.All(book.Names, name => Assert.All(book.Sheets[name], row => Assert.All(row.Value, cell => Assert.True(row.Key == 2 && name == "Levels" || book.Fill(cell.Value) == null, name + " " + cell.Key + row.Key))));
            Assert.Equal("El @ Entry, FFL, Steps", book.Row("Settings", 9)[1]);
            Assert.Equal("Editable columns", book.Row("Settings", 9)[0]);
        }

        [Fact]
        public void R364_settings_carry_the_build_id_of_the_plan_or_the_run_id_and_the_lowercase_sha256_of_the_plan_bytes()
        {
            WorkerAnswer answer;
            string plan;
            Book book = RunBook(new Made(), out answer, out plan);
            Assert.Equal(new object[] { "Plan build id", "lx1" }, book.Row("Settings", 10).ToArray());
            Assert.Equal(new object[] { "Plan SHA-256", FileHash.Sha256(plan) }, book.Row("Settings", 11).ToArray());
            Assert.Matches("^[0-9a-f]{64}$", (string)book.Row("Settings", 11)[1]);

            // A plan the worker built holds its run stamp, whose run id is the build id
            var stamped = new Made();
            stamped.Plan["run"] = new JObject { ["runId"] = "build_7", ["code"] = "SiteAdaptation.Common", ["inputs"] = new JArray() };
            Directory.Delete(Path.Combine(folder, "out"), true);
            book = RunBook(stamped, out answer, out plan);
            Assert.Equal(new object[] { "Plan build id", "build_7" }, book.Row("Settings", 10).ToArray());
            Assert.Equal(FileHash.Sha256(plan), book.Row("Settings", 11)[1]);
        }

        [Fact]
        public void R364_a_profile_off_the_plan_is_refused_and_an_applied_level_override_with_no_entry_is_an_issue_of_the_run()
        {
            int code;
            string plan;
            var other = new Made { Profile = "native2022" };
            WorkerAnswer answer = Run(other, out code, out plan, "lx2");
            Assert.Equal(2, code);
            Assert.Contains("the plan was built with the level profile parsons2024Rev6, the request names native2022", answer.Message);

            var lacking = new Made();
            ((JArray)lacking.Overrides["overrides"]).RemoveAt(4);
            WorkerAnswer issued;
            Book book = RunBook(lacking, out issued, out plan);
            Assert.Equal("Override entryM (breaks roadLevel), rampFrontM (no entry in the override list)", book.Row("Levels", 4)[9]);
            Assert.Contains("plot 102 field rampFrontM: the plan applied it, the override list holds no entry of its sources (R364)", issued.Issues);
        }
    }
}
