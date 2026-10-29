using System;
using System.Collections.Generic;
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
    // R365. The report Excel of the command reportExcel of the worker on a made up plan with its status, walls and missing list, its override
    // list, its step checkRows, plots.csv, rules and config built in code (R10), read back with DocumentFormat.OpenXml, on net48 and net8.0.
    // python/report_excel_check.py reads the same file a second way with openpyxl
    public sealed class ReportExcelTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r365_" + Guid.NewGuid().ToString("N"));

        public ReportExcelTests()
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

        private const string ProfileFile = "rules/r.json";
        private const string KeyPath = ProfileFile + " levelProfiles.parsons2024Rev6.keyRules.";

        private static JObject Valued(JToken value, string source, string status, string from)
        {
            return new JObject { ["value"] = value, ["source"] = source, ["status"] = status, ["statusFrom"] = from };
        }

        // The details of the status of the plan by key, the table statusFrom of R401
        private static JObject Details()
        {
            Func<JObject, string, JObject> measured = (entry, strength) => new JObject { ["entries"] = new JArray(entry), ["strength"] = strength };
            return new JObject
            {
                ["unit from locationPlan"] = new JObject { ["entries"] = new JArray(new JObject { ["status"] = "DOCUMENT", ["document"] = "the location plan" }) },
                ["mirrored from computed hand villa"] = measured(new JObject { ["entry"] = KeyPath + "hand villa.rule", ["status"] = "MEASURED", ["share"] = 0.96, ["strength"] = "strong" }, "strong"),
                ["levels.entry from estimate"] = measured(new JObject { ["entry"] = KeyPath + "entry.rule", ["status"] = "MEASURED", ["share"] = 0.088, ["strength"] = "weak" }, "weak"),
                ["levels.entry from override by entryM"] = new JObject
                {
                    ["entries"] = new JArray(new JObject { ["status"] = "RECOMMENDED", ["question"] = "Q053", ["where"] = "section 6" }), ["overrides"] = new JArray("entryM"),
                },
                // A strong entry before the UNKNOWN one, so the basis is that of the worst entry by worstFirst and never the first entry
                ["floors type from computed"] = new JObject
                {
                    ["entries"] = new JArray(new JObject { ["entry"] = KeyPath + "generic.rule", ["status"] = "MEASURED", ["share"] = 0.99, ["strength"] = "strong" },
                        new JObject { ["entry"] = "config/project.json floors.types", ["status"] = "UNKNOWN", ["why"] = "no entry" }),
                },
                // Review 1 of PR 96: a height the heightM override touched still reads keyRules.entry, and counts under no keyRules entry
                ["levels.height from estimate by heightM"] = new JObject
                {
                    ["entries"] = new JArray(new JObject { ["status"] = "RECOMMENDED", ["question"] = "Q053", ["where"] = "section 6" },
                        new JObject { ["entry"] = KeyPath + "entry.rule", ["status"] = "MEASURED", ["share"] = 0.088, ["strength"] = "weak" }),
                    ["overrides"] = new JArray("heightM"),
                },
                ["walls.base from keyRules"] = measured(new JObject { ["entry"] = KeyPath + "wallBase.rule", ["status"] = "MEASURED", ["share"] = 0.98, ["strength"] = "strong" }, "strong"),
                ["walls.top from keyRules by heightM"] = new JObject
                {
                    ["entries"] = new JArray(new JObject { ["status"] = "RECOMMENDED", ["question"] = "Q053", ["where"] = "section 6" }), ["overrides"] = new JArray("heightM"),
                },
            };
        }

        private static JObject Plot(string number, string unit, bool mirrored, bool entryMoved, string[] floors, JObject overrides = null)
        {
            var kinds = new JObject();
            foreach (string kind in floors)
            {
                kinds[kind] = new JObject { ["type"] = Valued(kind == "generic" ? "Generic 150mm" : kind == "yard" ? "yard" : "Ramps 2%", "computed", "UNKNOWN", "floors type from computed") };
            }
            var plot = new JObject
            {
                ["plot"] = number, ["unit"] = Valued(unit, "locationPlan", "DOCUMENT", "unit from locationPlan"),
                ["mirrored"] = Valued(mirrored, "computed", "MEASURED", "mirrored from computed hand villa"),
                ["levels"] = new JObject { ["entry"] = entryMoved ? Valued(10.5, "override", "RECOMMENDED", "levels.entry from override by entryM") : Valued(10.25, "estimate", "MEASURED", "levels.entry from estimate") },
                ["floors"] = kinds,
            };
            if (overrides != null)
            {
                plot["overrides"] = overrides;
                ((JObject)plot["levels"])["height"] = Valued(0.2, "estimate", "RECOMMENDED", "levels.height from estimate by heightM");
            }
            return plot;
        }

        // NBH 012: 101 with an entryM, a CASE, a rampFrontM and an RVT-Name override applied and a ramp front fall left, a height the heightM
        // override touched, a ramp, and a wall on 102 whose top heightM moved,
        // 102 mirrored with a yard and no ramp, 103 not written for its unit with a mirror override left. NBH 013: 201. 999 has no row of plots.csv
        internal static JObject MadePlan()
        {
            return new JObject
            {
                ["format"] = "site-adaptation-plan",
                ["formatVersion"] = 1,
                ["run"] = new JObject { ["runId"] = "b7" },
                ["statusFrom"] = Details(),
                ["summary"] = new JObject
                {
                    ["skipped"] = new JArray(),
                    ["rules"] = new JObject { ["levelProfile"] = "parsons2024Rev6", ["overrideSource"] = new JArray("parsons"), ["overrideModel"] = new JArray() },
                },
                ["plots"] = new JArray
                {
                    Plot("101", "VD4", false, true, new[] { "generic", "ramp" },
                        new JObject { ["applied"] = new JArray("entryM", "parameter CASE", "rampFrontM", "parameter RRE_ZZ_SP_RVT-Name"), ["notApplied"] = new JObject { ["rampFrontFallM"] = "no direction" } }),
                    Plot("102", "DP1", true, false, new[] { "generic", "yard" }),
                    Plot("201", "VD1", false, false, new[] { "generic" }),
                    Plot("999", "VD1", false, false, new[] { "generic" }),
                },
                ["walls"] = new JArray(new JObject
                {
                    ["plot"] = "101", ["edge"] = 1, ["plots"] = Valued(new JArray("101", "102"), "locationPlan", "DOCUMENT", "unit from locationPlan"),
                    ["base"] = Valued(10.1, "keyRules", "MEASURED", "walls.base from keyRules"), ["top"] = Valued(10.4, "keyRules", "RECOMMENDED", "walls.top from keyRules by heightM"),
                    ["basePlot"] = "102", ["topPlot"] = "101",
                }),
                ["missing"] = new JArray
                {
                    new JObject { ["plot"] = "102", ["written"] = true, ["missing"] = new JArray(new JObject { ["key"] = "floors.ramp", ["why"] = "no ramp type" }) },
                    new JObject
                    {
                        ["plot"] = "103", ["written"] = false, ["missing"] = new JArray(new JObject { ["key"] = "unit", ["why"] = "no unit code" }),
                        ["overrides"] = new JObject { ["applied"] = new JArray(), ["notApplied"] = new JObject { ["mirrored"] = "plot not written" } },
                    },
                },
                ["conflicts"] = new JArray(),
            };
        }

        private static JObject Entry(string plot, string field, JToken value, string reason, string source = "parsons")
        {
            return new JObject { ["plot"] = plot, ["field"] = field, ["value"] = value, ["source"] = source, ["model"] = "model_" + source + ".rvt", ["reason"] = reason };
        }

        internal static JObject MadeOverrides()
        {
            return new JObject
            {
                ["overrides"] = new JArray
                {
                    Entry("101", "entryM", new JObject { ["gapToRuleM"] = 0.01 }, "breaks roadLevel"),
                    Entry("101", "heightM", new JObject { ["gapToRuleM"] = 0.5 }, "a native entry the build does not take", "native"),
                    Entry("101", "parameter CASE", "D", "breaks typologyMirrored"),
                    Entry("101", "rampFrontFallM", 0.02, "breaks roadAtEachCorner"),
                    Entry("101", "rampFrontM", new JObject { ["gapToRuleM"] = 0.003 }, "breaks roadLevel"),
                    Entry("101", "parameter RRE_ZZ_SP_RVT-Name", "101 | VD4 | 1", "breaks plotCodeStepsAndFacade"),
                    Entry("103", "mirrored", true, "breaks blockMirrorRoadSide"),
                },
            };
        }

        private static JObject Row(string check, string field, string from, string use, string why = null)
        {
            return new JObject { ["check"] = check, ["field"] = field, ["from"] = from, ["use"] = use, ["rule"] = null, ["toolValue"] = null, ["past"] = null, ["why"] = why };
        }

        // The step checkRows of the build, the rows of 7.4 per plot (R363)
        internal static JObject MadeCheckRows()
        {
            Func<JObject[], JObject> plot = rows => new JObject { ["raw"] = new JObject { ["rows"] = new JArray(rows) }, ["written"] = new JObject { ["rows"] = new JArray(rows) } };
            return new JObject
            {
                ["format"] = "siteAdaptationStep", ["step"] = "checkRows",
                ["plots"] = new JObject
                {
                    ["101"] = plot(new[] { Row("Level", "entryM", "override", "Past"), Row("Level", "rampFrontFallM", "override", "Rule", "no direction"), Row("Parameter", "parameter CASE", "override", "Past") }),
                    ["102"] = plot(new[] { Row("Floor", "floors.ramp", "missing", "Rule") }),
                    ["103"] = plot(new[] { Row("Hand", "mirrored", "override", "Rule", "plot not written"), Row("Unit code", "unit", "missing", "Rule") }),
                    ["201"] = plot(new JObject[0]),
                },
            };
        }

        // The step summary of the build, step 15, the unit of each plot of the scope and whether the plan writes it, 103 with no unit. 150 of
        // plots.csv lies in 012 outside the scope of the build, as a key cover holds part of an NBH
        internal static JObject MadeSummary()
        {
            Func<string, bool, JObject> plot = (unit, written) =>
            {
                var item = new JObject { ["unit"] = unit, ["written"] = written };
                return new JObject { ["raw"] = item, ["written"] = item.DeepClone() };
            };
            return new JObject
            {
                ["format"] = "siteAdaptationStep", ["step"] = "summary",
                ["plots"] = new JObject { ["101"] = plot("VD4", true), ["102"] = plot("DP1", true), ["103"] = plot(null, false), ["201"] = plot("VD1", true), ["999"] = plot("VD1", true) },
            };
        }

        internal static JObject MadeRules()
        {
            return JObject.Parse("{\"levelProfiles\": {\"parsons2024Rev6\": {\"keyRules\": {\"note\": \"made up\", "
                + "\"hand villa\": {\"rule\": \"blockMirrorRoadSide\", \"share\": 0.96, \"strong\": true}, \"entry\": {\"rule\": \"roadLevel\", \"share\": 0.088, \"strong\": false}, "
                + "\"wallBase\": {\"rule\": \"yardLevelOfTheHigherPlot\", \"share\": 0.98, \"strong\": true}, "
                + "\"wallTop\": {\"rule\": \"higherFflPlusRev6\", \"share\": 0.35, \"strong\": false}, \"ssl\": {\"rule\": \"fflPlusGroundSsl\", \"notMeasured\": \"no key\"}}}, "
                + "\"native2022\": {}}}");
        }

        internal static JObject MadeConfig()
        {
            Func<int, JToken> step = value => value;
            return new JObject
            {
                ["profile"] = ProfileFile,
                ["siteBuild"] = new JObject { ["minFreeGB"] = 2.0 },
                ["valueStatus"] = new JObject
                {
                    ["assumed"] = new JArray("RECOMMENDED", "CHOSEN by Code", "UNKNOWN", "MEASURED weak"), ["worstFirst"] = new JArray("UNKNOWN", "CHOSEN by Code", "RECOMMENDED", "MEASURED weak"),
                },
                ["addin"] = new JObject
                {
                    ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "Road.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) },
                    ["workerTimeLimitS"] = new JObject { ["reportExcel"] = 600 },
                    ["workerTimeLimitMinS"] = 10,
                    // Rows 5 Floors and ramps and 6 Levels of 7.6, as config/project.json holds them
                    ["report"] = new JObject
                    {
                        ["stepOfField"] = new JObject
                        {
                            ["rampType"] = step(5), ["rampFrontM"] = step(5), ["genericFloor"] = step(5), ["rampOutline"] = step(5), ["yardOutline"] = step(5), ["rampFrontFallM"] = step(5),
                            ["entryM"] = step(6), ["heightM"] = step(6), ["steps"] = step(6), ["parameter RRE_ZZ_SP_Prototype-FFL"] = step(6), ["parameter RRE_ZZ_SP_RVT-Name"] = step(6),
                        },
                        ["defaultStep"] = 4,
                        ["wallDecisions"] = new JArray("wallBase", "wallTop", "wallLine", "frontOpen"),
                    },
                },
            };
        }

        internal const string PlotsCsv = "locationPlanNumber,typologyRev9,zone,nbhId\n101,VD4,Zone 2,012\n102,DP1,Zone 2,012\n103,,Zone 2,012\n150,,Zone 2,012\n201,VD1,Zone 3,013\n";

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

        private WorkerAnswer Run(JObject plan, out int code, out string planPath, string profile = "parsons2024Rev6", string id = "rx1", JObject config = null)
        {
            planPath = Write("plan_all.json", plan.ToString());
            var inputs = new List<WorkerFile>
            {
                Input("config", Write("project.json", (config ?? MadeConfig()).ToString())),
                Input("rules", Write("rules.json", MadeRules().ToString())),
                Input(ReportWorkbook.PlanRole, planPath),
                Input(ReportWorkbook.OverridesRole, Write("overrides.json", MadeOverrides().ToString())),
                Input(ReportWorkbook.CheckRowsRole, Write("step_checkRows_python.json", MadeCheckRows().ToString())),
                Input(ReportWorkbook.SummaryRole, Write("step_summary_python.json", MadeSummary().ToString())),
                Input("plotsCsv", Write("plots.csv", PlotsCsv)),
            };
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = id, Command = "reportExcel", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = inputs, LevelProfile = profile, Scope = new WorkerScope { Kind = "project" }, OutputFolder = Path.Combine(folder, "out"), TimeLimitS = 600,
            };
            code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            return WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, id));
        }

        private LevelExcelTests.Book RunBook(out WorkerAnswer answer, out string planPath, string nbh = "012")
        {
            answer = Run(MadePlan(), out int code, out planPath);
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            string[] paths = { Path.Combine(folder, "out", "012_Report.xlsx"), Path.Combine(folder, "out", "013_Report.xlsx") };
            Assert.Equal(paths, answer.Outputs.Select(item => item.Path).ToArray());
            return LevelExcelTests.Book.Read(Path.Combine(folder, "out", nbh + "_Report.xlsx"));
        }

        // A cell of a row read back as a flag where the sheet wrote one, OpenXml giving a flag as 1 or 0
        private static object Flag(LevelExcelTests.Book book, string sheet, int row, string column)
        {
            Cell cell = book.Sheets[sheet][row].TryGetValue(column, out Cell found) ? found : null;
            return cell?.DataType != null && cell.DataType.Value == CellValues.Boolean ? cell.CellValue.Text == "1" : LevelExcelTests.Book.Value(cell);
        }

        private static bool Bordered(LevelExcelTests.Book book, Cell cell)
        {
            CellFormat format = book.Styles.CellFormats.Elements<CellFormat>().ElementAt((int)(cell.StyleIndex?.Value ?? 0));
            Border border = book.Styles.Borders.Elements<Border>().ElementAt((int)(format.BorderId?.Value ?? 0));
            return border.LeftBorder?.Style?.Value == BorderStyleValues.Thin && border.BottomBorder?.Style?.Value == BorderStyleValues.Thin;
        }

        [Fact]
        public void R365_the_report_excel_holds_the_sheets_of_7_6_with_assumed_after_overrides_and_settings_last_in_a_valid_package()
        {
            LevelExcelTests.Book book = RunBook(out WorkerAnswer answer, out string plan);
            Assert.Equal(new[] { "Summary", "Placed", "Skipped", "Overrides", "Assumed", "Issues", "Rules", "Settings" }, book.Names.ToArray());
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(Path.Combine(folder, "out", "012_Report.xlsx"), false))
            {
                Assert.Empty(new OpenXmlValidator().Validate(document).Select(error => error.Description));
            }
            Assert.Equal(new[] { "Plot", "Kind", "Type", "Mirror", "Element id" }, book.Row("Placed", 1).Cast<string>().ToArray());
            Assert.Equal(new[] { "Plot", "Kind", "Reason" }, book.Row("Skipped", 1).Cast<string>().ToArray());
            Assert.Equal(new[] { "Plot", "Field", "Outcome", "Why", "Reason", "Source", "Model" }, book.Row("Overrides", 1).Cast<string>().ToArray());
            Assert.Equal(new[] { "Plot", "Field", "Value", "Status", "Basis" }, book.Row("Assumed", 1).Cast<string>().ToArray());
            Assert.Equal(new[] { "Step", "Plot", "Check", "Field", "Source", "Use", "Why" }, book.Row("Issues", 1).Cast<string>().ToArray());
            Assert.Equal(new[] { "Rule", "Status", "Basis", "Plots", "Values" }, book.Row("Rules", 1).Cast<string>().ToArray());
            Assert.Equal(new[] { "Setting", "Value" }, book.Row("Settings", 1).Cast<string>().ToArray());
        }

        [Fact]
        public void R365_the_summary_holds_the_top_line_the_table_of_buttons_1_to_7_from_row_3_and_the_box_counts_agree_across_outputs()
        {
            LevelExcelTests.Book book = RunBook(out WorkerAnswer answer, out string plan);
            Assert.Equal(new object[] { "[file name].rvt, NBH 012, 3 plots, parsons2024Rev6, run time [n] min" }, book.Row("Summary", 1).ToArray());
            Assert.True(book.Bold(book.Sheets["Summary"][1]["A"]));
            Assert.Equal(new[] { "A1:E1" }, book.Merged["Summary"].ToArray());
            Assert.Empty(book.Row("Summary", 2));
            Assert.Equal(new[] { "Step", "Done", "Skipped", "Override", "Issues" }, book.Row("Summary", 3).Cast<string>().ToArray());
            // 2 Read data: of the 3 plots of the scope in 012 101 and 102 hold a unit and 103 none, 150 of plots.csv lying outside the scope.
            // 3 Check: the 6 entries of the list on them of the source parsons and the 6 rows of 7.4. 4: 2 links, 103 skipped, CASE applied and
            // the mirror of 103 left. 5: 4 floors, the ramp of 102 and the 3 floors of 103 skipped, rampFrontM applied and rampFrontFallM left as
            // row 5 of 7.6 lists them. 6: entryM and RVT-Name applied as row 6 lists them. 7: the wall heightM moved and the weak wallTop
            Assert.Equal(new object[] { "1 Set up site", "not run yet", "not run yet", "not run yet", "not run yet" }, book.Row("Summary", 4).ToArray());
            Assert.Equal(new object[] { "2 Read data", 2.0, 1.0, 0.0, 0.0 }, book.Row("Summary", 5).ToArray());
            Assert.Equal(new object[] { "3 Check", 3.0, 0.0, 6.0, 6.0 }, book.Row("Summary", 6).ToArray());
            Assert.Equal(new object[] { "4 Place villas", 2.0, 1.0, 1.0, 1.0 }, book.Row("Summary", 7).ToArray());
            Assert.Equal(new object[] { "5 Floors and ramps", 4.0, 4.0, 1.0, 1.0 }, book.Row("Summary", 8).ToArray());
            Assert.Equal(new object[] { "6 Levels", 2.0, 1.0, 2.0, 0.0 }, book.Row("Summary", 9).ToArray());
            Assert.Equal(new object[] { "7 Walls", 1.0, 0.0, 1.0, 1.0 }, book.Row("Summary", 10).ToArray());
            Assert.Empty(book.Row("Summary", 11));
            Assert.Equal(new object[] { "Counts agree across outputs" }, book.Row("Summary", 12).ToArray());
            Assert.True(book.Bold(book.Sheets["Summary"][12]["A"]));
            Assert.Equal(new object[] { "Item", "Revit", "Excel" }, book.Row("Summary", 13).ToArray());
            Assert.Equal(new object[] { "Links", "not placed yet", 2.0 }, book.Row("Summary", 14).ToArray());
            Assert.Equal(new object[] { "Floors", "not placed yet", 4.0 }, book.Row("Summary", 15).ToArray());
            Assert.Equal(new object[] { "Levels", "not placed yet", "no run of button 6 yet" }, book.Row("Summary", 16).ToArray());
            Assert.All(Enumerable.Range(13, 4).SelectMany(row => book.Sheets["Summary"][row].Values), cell => Assert.True(Bordered(book, cell)));
            Assert.False(Bordered(book, book.Sheets["Summary"][3]["A"]));
            Assert.DoesNotContain(answer.Issues, issue => issue.Contains("Links") || issue.Contains("Floors"));
        }

        [Fact]
        public void R365_the_box_rules_applied_names_each_key_rule_with_its_share_strength_and_the_plots_that_took_its_value()
        {
            LevelExcelTests.Book book = RunBook(out WorkerAnswer answer, out string plan);
            Assert.Empty(book.Row("Summary", 17));
            Assert.Equal(new object[] { "Rules applied" }, book.Row("Summary", 18).ToArray());
            Assert.Equal(new object[] { "Decision", "Rule", "Share", "Strength", "Plots" }, book.Row("Summary", 19).ToArray());
            // hand villa on the mirror flags of 101 and 102, entry on 102 alone as the entryM override moved that of 101 and the height of 101 the
            // heightM override touched still names keyRules.entry, wallBase on the wall of 101, wallTop on none as heightM touched the top, ssl
            // measured by no key and read by no value
            Assert.Equal(new object[] { "hand villa", "blockMirrorRoadSide", 0.96, "strong", "2 plots" }, book.Row("Summary", 20).ToArray());
            Assert.Equal(new object[] { "entry", "roadLevel", 0.088, "weak", "1 plots" }, book.Row("Summary", 21).ToArray());
            Assert.Equal(new object[] { "wallBase", "yardLevelOfTheHigherPlot", 0.98, "strong", "1 plots" }, book.Row("Summary", 22).ToArray());
            Assert.Equal(new object[] { "wallTop", "higherFflPlusRev6", 0.35, "weak", "0 plots" }, book.Row("Summary", 23).ToArray());
            Assert.Equal(new object[] { "ssl", "fflPlusGroundSsl", "not measured", "not measured", "0 plots" }, book.Row("Summary", 24).ToArray());
            Assert.Equal(new[] { 1, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16, 18, 19, 20, 21, 22, 23, 24 }, book.Sheets["Summary"].Keys.ToArray());
            Assert.Equal("0.000", book.Format(book.Sheets["Summary"][20]["C"]));
        }

        [Fact]
        public void R365_placed_keys_each_row_by_the_plot_one_row_per_plot_and_kind_with_the_mirror_of_a_link_and_type_and_mirror_marked_editable()
        {
            LevelExcelTests.Book book = RunBook(out WorkerAnswer answer, out string plan);
            Assert.Equal(new object[] { "101", "link", "VD4", false }, Enumerable.Range(0, 4).Select(index => Flag(book, "Placed", 2, "ABCD"[index].ToString())).ToArray());
            Assert.Equal(new object[] { "101", "generic", "Generic 150mm" }, book.Row("Placed", 3).ToArray());
            Assert.Equal(new object[] { "101", "ramp", "Ramps 2%" }, book.Row("Placed", 4).ToArray());
            Assert.Equal(true, Flag(book, "Placed", 5, "D"));
            Assert.Equal(new object[] { "102", "generic", "Generic 150mm" }, book.Row("Placed", 6).ToArray());
            Assert.Equal(new object[] { "102", "yard", "yard" }, book.Row("Placed", 7).ToArray());
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, book.Sheets["Placed"].Keys.ToArray());
            Assert.Empty(book.Merged["Placed"]);
            var filled = book.Sheets["Placed"][1].Where(pair => book.Fill(pair.Value) != null).Select(pair => (string)LevelExcelTests.Book.Value(pair.Value)).ToArray();
            Assert.Equal(new[] { "Type", "Mirror" }, filled);
            Assert.Equal(new[] { LevelExcelText.EditableFill }, book.Sheets["Placed"][1].Values.Select(book.Fill).Where(fill => fill != null).Distinct().ToArray());
            Assert.DoesNotContain(book.Names.Where(name => name != "Placed").SelectMany(name => book.Sheets[name].Values.SelectMany(row => row.Values)), cell => book.Fill(cell) != null);
        }

        [Fact]
        public void R365_skipped_overrides_assumed_issues_and_rules_hold_the_rows_of_the_plan_its_list_its_status_and_its_check_rows()
        {
            LevelExcelTests.Book book = RunBook(out WorkerAnswer answer, out string plan);
            Assert.Equal(new object[] { "102", "ramp", "no ramp type" }, book.Row("Skipped", 2).ToArray());
            Assert.Equal(new[] { "link", "generic", "yard", "ramp" }, Enumerable.Range(3, 4).Select(row => (string)book.Row("Skipped", row)[1]).ToArray());
            Assert.Equal(new object[] { "103", "link", "unit (no unit code)" }, book.Row("Skipped", 3).ToArray());

            Assert.Equal(new object[] { "101", "entryM", "Applied", null, "breaks roadLevel", "parsons", "model_parsons.rvt" }, book.Row("Overrides", 2).ToArray());
            Assert.Equal(new object[] { "101", "parameter CASE", "Applied", null, "breaks typologyMirrored", "parsons", "model_parsons.rvt" }, book.Row("Overrides", 3).ToArray());
            Assert.Equal(new object[] { "101", "rampFrontM", "Applied", null, "breaks roadLevel", "parsons", "model_parsons.rvt" }, book.Row("Overrides", 4).ToArray());
            Assert.Equal(new object[] { "101", "rampFrontFallM", "Left", "no direction", "breaks roadAtEachCorner", "parsons", "model_parsons.rvt" }, book.Row("Overrides", 6).ToArray());
            Assert.Equal(new object[] { "103", "mirrored", "Left", "plot not written", "breaks blockMirrorRoadSide", "parsons", "model_parsons.rvt" }, book.Row("Overrides", 7).ToArray());
            Assert.Equal(7, book.Sheets["Overrides"].Count);

            // A DOCUMENT or strong MEASURED value is not shown, the entryM of 101 reads Q053, the weak entry of 102 its rule and share
            Assert.Equal(new object[] { "101", "levels.entry", 10.5, "RECOMMENDED", "Q053" }, book.Row("Assumed", 2).ToArray());
            Assert.Equal(new object[] { "101", "levels.height", 0.2, "RECOMMENDED", "Q053" }, book.Row("Assumed", 3).ToArray());
            Assert.Equal(new object[] { "101", "floors.generic.type", "Generic 150mm", "UNKNOWN", "config/project.json floors.types" }, book.Row("Assumed", 4).ToArray());
            Assert.Equal(new object[] { "101", "floors.ramp.type", "Ramps 2%", "UNKNOWN", "config/project.json floors.types" }, book.Row("Assumed", 5).ToArray());
            Assert.Equal(new object[] { "102", "levels.entry", 10.25, "MEASURED weak", KeyPath + "entry.rule share 0.088" }, book.Row("Assumed", 6).ToArray());
            Assert.Equal(8, book.Sheets["Assumed"].Count);

            Assert.Equal(new object[] { "3 Check", "101", "Level", "entryM", "override", "Past" }, book.Row("Issues", 2).ToArray());
            Assert.Equal(new object[] { "3 Check", "103", "Hand", "mirrored", "override", "Rule", "plot not written" }, book.Row("Issues", 6).ToArray());
            Assert.Equal(new object[] { "3 Check", "103", "Unit code", "unit", "missing", "Rule" }, book.Row("Issues", 7).ToArray());
            Assert.Equal(7, book.Sheets["Issues"].Count);

            Assert.Equal(new object[] { "Q053", "RECOMMENDED", "Q053", 1.0, 3.0 }, book.Row("Rules", 2).ToArray());
            Assert.Equal(new object[] { "config/project.json floors.types", "UNKNOWN", "config/project.json floors.types", 2.0, 4.0 }, book.Row("Rules", 3).ToArray());
            Assert.Equal(new object[] { KeyPath + "entry.rule", "MEASURED weak", KeyPath + "entry.rule share 0.088", 2.0, 2.0 }, book.Row("Rules", 4).ToArray());
            Assert.Equal(new object[] { KeyPath + "generic.rule", "MEASURED strong", KeyPath + "generic.rule share 0.99", 2.0, 4.0 }, book.Row("Rules", 5).ToArray());
            Assert.Equal(new object[] { KeyPath + "hand villa.rule", "MEASURED strong", KeyPath + "hand villa.rule share 0.96", 2.0, 2.0 }, book.Row("Rules", 6).ToArray());
            Assert.Equal(new object[] { KeyPath + "wallBase.rule", "MEASURED strong", KeyPath + "wallBase.rule share 0.98", 1.0, 1.0 }, book.Row("Rules", 7).ToArray());
            Assert.Equal(new object[] { "the location plan", "DOCUMENT", "the location plan", 2.0, 3.0 }, book.Row("Rules", 8).ToArray());
            Assert.Equal(8, book.Sheets["Rules"].Count);
        }

        [Fact]
        public void R365_settings_hold_the_editable_columns_the_build_id_of_the_plan_and_its_sha256()
        {
            LevelExcelTests.Book book = RunBook(out WorkerAnswer answer, out string plan);
            Assert.Equal(new[] { "Run id", "Profile", "Rules SHA-256", "Override list SHA-256", "Run time", "Editable columns", "Plan build id", "Plan SHA-256" },
                book.Sheets["Settings"].Keys.Skip(1).Select(row => (string)book.Row("Settings", row)[0]).ToArray());
            Assert.Equal(new object[] { "Run id", "rx1" }, book.Row("Settings", 2).ToArray());
            Assert.Equal(new object[] { "Editable columns", "Type, Mirror" }, book.Row("Settings", 7).ToArray());
            Assert.Equal(new object[] { "Plan build id", "b7" }, book.Row("Settings", 8).ToArray());
            Assert.Equal(new object[] { "Plan SHA-256", FileHash.Sha256(plan) }, book.Row("Settings", 9).ToArray());
            Assert.Matches("^[0-9a-f]{64}$", (string)book.Row("Settings", 9)[1]);
            Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$", (string)book.Row("Settings", 6)[1]);
        }

        [Fact]
        public void R365_one_workbook_per_nbh_of_plots_csv_and_a_plot_of_no_nbh_an_issue()
        {
            LevelExcelTests.Book other = RunBook(out WorkerAnswer answer, out string plan, "013");
            Assert.Equal(new object[] { "[file name].rvt, NBH 013, 1 plots, parsons2024Rev6, run time [n] min" }, other.Row("Summary", 1).ToArray());
            Assert.Equal(new object[] { "201", "generic", "Generic 150mm" }, other.Row("Placed", 3).ToArray());
            Assert.Equal(new[] { 1 }, other.Sheets["Issues"].Keys.ToArray());
            Assert.Equal(2, answer.Counts["report workbooks"]);
            Assert.Equal(1, answer.Counts["report plots with no NBH"]);
            Assert.Equal(new[] { "1 plots of the plan have no nbhId in plots.csv, so no report lists them: 999 (R365)" }, answer.Issues.ToArray());
        }

        [Fact]
        public void R365_a_plan_of_another_level_profile_or_with_no_table_statusFrom_or_settings_with_no_block_addin_report_is_refused()
        {
            WorkerAnswer answer = Run(MadePlan(), out int code, out string plan, "native2022", "rx2");
            Assert.Equal(2, code);
            Assert.Contains("the plan was built with the level profile parsons2024Rev6, the request names native2022 (R365)", answer.Message);
            JObject bare = MadePlan();
            bare.Remove("statusFrom");
            answer = Run(bare, out code, out plan, id: "rx3");
            Assert.Equal(2, code);
            Assert.Contains("no table statusFrom", answer.Message);
            JObject bareConfig = MadeConfig();
            ((JObject)bareConfig["addin"]).Remove("report");
            answer = Run(MadePlan(), out code, out plan, id: "rx4", config: bareConfig);
            Assert.Equal(2, code);
            Assert.Contains(ReportSheets.NoReportBlock, answer.Message);
            Assert.False(Directory.Exists(Path.Combine(folder, "out")) && Directory.GetFiles(Path.Combine(folder, "out"), "*.xlsx").Length > 0);
        }

        [Fact]
        public void R365_a_placed_sheet_short_of_one_floor_row_gives_the_mismatch_of_the_box_as_an_issue_row()
        {
            List<JObject> written = ((JArray)MadePlan()["plots"]).OfType<JObject>().Where(plot => (string)plot["plot"] == "101" || (string)plot["plot"] == "102").ToList();
            ReportTable placed = ReportSheets.PlacedTable(written);
            Assert.Equal(6, placed.Rows.Count);
            placed.Rows.RemoveAt(placed.Rows.Count - 1);
            var issues = new ReportTable { Header = ReportExcelText.IssueColumns, Formats = new string[7] };
            var result = new ReportResult();
            ReportTable box = ReportSheets.CountsTable(written, placed, issues, result, "012");
            Assert.Equal(new object[] { "Floors", "not placed yet", 3L }, box.Rows[1]);
            Assert.Equal(new object[] { "8 Report", null, "Counts", "Floors", null, null, "the plan holds 4, Revit not placed yet, Excel 3 (R365)" }, Assert.Single(issues.Rows));
            Assert.Equal(new[] { "NBH 012 Floors: the plan holds 4, Revit not placed yet, Excel 3 (R365)" }, result.Issues.ToArray());
        }
    }
}
