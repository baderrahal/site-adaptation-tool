using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CadReader;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Update;
using Xunit;
using XSheet = DocumentFormat.OpenXml.Spreadsheet.Sheet;

namespace SiteAdaptation.Tests
{
    // R402. Update from report on a made up plan: its level Excel and report Excel written by the writers of R364 and R365, a copy of each
    // edited cell by cell with DocumentFormat.OpenXml, read back, compared, checked against the rules of a made up profile with a made up
    // block manualRules, and applied to a made up override list, on net48 and net8.0. python/update_check.py is the second route
    public sealed class R402_UpdateFromReportTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r402_" + Guid.NewGuid().ToString("N"));
        private const string Profile = "parsons2024Rev6";
        private const string PlanSha = "aaaa000000000000000000000000000000000000000000000000000000000001";

        public R402_UpdateFromReportTests()
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

        private static JObject Sourced(object value)
        {
            return new JObject { ["value"] = JToken.FromObject(value), ["source"] = "estimate", ["status"] = "MEASURED" };
        }

        private static JObject Plot(string number, string unit, double road, double entry, double ffl, double height, int steps, bool mirrored, params string[] floors)
        {
            var levels = new JObject
            {
                ["ffl"] = Sourced(ffl), ["ssl"] = Sourced(Rounding.Fixed(ffl - 0.075, 3)), ["entry"] = Sourced(entry), ["height"] = Sourced(height), ["steps"] = Sourced(steps), ["roadAtFrontM"] = Sourced(road),
            };
            var made = new JObject();
            foreach (string pair in floors)
            {
                string[] parts = pair.Split('=');
                made[parts[0]] = new JObject { ["type"] = new JObject { ["value"] = parts[1] } };
            }
            return new JObject
            {
                ["plot"] = number, ["unit"] = new JObject { ["value"] = unit, ["source"] = "locationPlan", ["status"] = "DOCUMENT" }, ["mirrored"] = Sourced(mirrored),
                ["levels"] = levels, ["levelRule"] = new JObject { ["entry"] = entry, ["ffl"] = ffl, ["height"] = height, ["steps"] = steps }, ["floors"] = made,
                ["roadSurfaces"] = new JArray("Road A", "Road A"),
            };
        }

        // NBH 012: 101 a lone VD4, 102 and 103 one DP1 piece at one FFL, 104 not written. The levels hold by the rules of MadeRules
        internal static JObject MadePlan()
        {
            return new JObject
            {
                ["format"] = "site-adaptation-plan", ["formatVersion"] = 1, ["neighbourhood"] = "all",
                ["summary"] = new JObject { ["rules"] = new JObject { ["levelProfile"] = Profile, ["overrideSource"] = new JArray("parsons"), ["overrideModel"] = new JArray() } },
                ["plots"] = new JArray
                {
                    Plot("101", "VD4", 9.98, 10.0, 10.2, 0.2, 1, false, "generic=Generic 300"),
                    Plot("102", "DP1", 10.18, 10.2, 10.3, 0.1, 1, true, "generic=Generic 300", "ramp=Ramps 0.5%"),
                    Plot("103", "DP1", 10.03, 10.05, 10.3, 0.25, 2, false, "generic=Generic 300"),
                },
                ["missing"] = new JArray(new JObject { ["plot"] = "104", ["written"] = false, ["missing"] = new JArray(new JObject { ["key"] = "levels.ffl", ["why"] = "no road level" }) }),
            };
        }

        internal static JObject MadeRules(bool manual = true)
        {
            JObject rules = JObject.Parse("{\"units_mm\": true, \"levels\": {\"ground_ssl\": -75}, \"typologies\": {\"VD4\": {}, \"DP1\": {}, \"C10\": {}}, "
                + "\"steps\": {\"front_min\": 1, \"front_max\": 3, \"riser_max\": 180, \"riser_min\": 100, \"riser_added\": 170, "
                + "\"legend\": [[0.1, 1], [0.2, 1], [0.27, 2], [0.37, 2], [0.44, 3], [0.54, 3]], \"base_height\": {\"default\": 100, \"VD4\": 200}, \"one_step_only\": [\"C10\"]}, "
                + "\"levelProfiles\": {\"parsons2024Rev6\": {\"job\": \"parsons\", \"frontRoad\": {\"surface\": \"roadLevel\", \"offsetM\": 0.0}, \"stepBands\": [[0.205, 1], [0.38, 2], [99.0, 3]], "
                + "\"keyRules\": {\"ffl\": {\"rule\": \"oneFflPerPieceAtTheHighestEntry\"}, \"levelSplit\": {\"rule\": \"none\"}, \"steps\": {\"rule\": \"stepsFromHeight\"}, \"baseHeight\": {\"rule\": \"profile\"}}}}}");
            if (manual)
            {
                rules["manualRules"] = JObject.Parse("{\"rules\": [{\"id\": \"M08\", \"text\": \"At most 3 added steps, made up\", \"updateChecks\": [\"stepsAtMost\", \"stepHeights\"]}, "
                    + "{\"id\": \"M09\", \"text\": \"Riser 100 to 180 mm, made up\", \"updateChecks\": [\"riser\"]}]}");
            }
            return rules;
        }

        private static JObject Entry(string plot, string field, JToken value, string source, string key)
        {
            return new JObject
            {
                ["plot"] = plot, ["field"] = field, ["value"] = value, ["rule"] = null, ["source"] = source, ["model"] = "m_" + key + ".rvt", ["key"] = key,
                ["decision"] = source + " " + field, ["reason"] = "made up",
            };
        }

        internal static JObject MadeOverrides()
        {
            return new JObject
            {
                ["note"] = "made up",
                ["overrides"] = new JArray
                {
                    Entry("101", "heightM", new JObject { ["gapToRuleM"] = 0.01 }, "parsons", "img"),
                    Entry("102", "entryM", new JObject { ["gapToRuleM"] = 0.02 }, "native", "u12"),
                    Entry("102", "rampFrontM", new JObject { ["gapToRuleM"] = -0.002 }, "parsons", "imc"),
                    Entry("103", "parameter CASE", "D", "parsons", "imc"),
                },
                ["cannot"] = new JArray(new JObject { ["plot"] = "9", ["field"] = "rampFrontFallM", ["source"] = "native", ["model"] = "m_u13.rvt", ["key"] = "u13", ["decision"] = "d", ["reason"] = "r" }),
            };
        }

        // The level Excel of 012 as LevelSheets makes it and LevelWorkbook writes it
        private string WriteLevelExcel(JObject plan, string planSha = PlanSha)
        {
            Func<string[], JObject> written = plots => new JObject { ["written"] = new JObject { ["plots"] = new JArray(plots) } };
            Func<string[], JObject> piece = plots => new JObject { ["written"] = new JObject { ["piece"] = new JArray(plots) } };
            Func<string[], JObject> row = plots => new JObject { ["written"] = new JObject { ["cluster"] = new JArray(plots) } };
            var inputs = new LevelInputs
            {
                Plan = plan, PlanSha256 = planSha, Rules = MadeRules(), RulesName = "rules.json", RulesSha256 = "r", Config = new JObject { ["placerCheck"] = new JObject { ["level"] = new JObject { ["passMm"] = 1.0 } } },
                RoadOrder = new List<string> { "Road A" }, Overrides = MadeOverrides(), OverridesSha256 = "o",
                Clusters = new JObject { ["plots"] = new JObject { ["102|103"] = written(new[] { "102", "103" }) } },
                Levels = new JObject { ["plots"] = new JObject { ["101"] = piece(new[] { "101" }), ["102"] = piece(new[] { "102", "103" }), ["103"] = piece(new[] { "102", "103" }) } },
                Hands = new JObject { ["plots"] = new JObject { ["102"] = row(new[] { "102", "103" }), ["103"] = row(new[] { "102", "103" }) } },
                PlotRows = new[] { "101", "102", "103", "104" }.Select(number => new Dictionary<string, string> { ["locationPlanNumber"] = number, ["zone"] = "Zone 2", ["nbhId"] = "012" }).ToList(),
                RunId = "lx1", Profile = Profile, Now = new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc),
            };
            LevelResult result = LevelSheets.Build(inputs);
            string path = Path.Combine(folder, result.Books[0].FileName);
            using (var stream = new FileStream(path, FileMode.Create))
            {
                LevelWorkbook.Write(stream, result.Books[0]);
            }
            return path;
        }

        // The sheets Placed and Settings of the report Excel of 012 as ReportSheets makes Placed, written by ReportWorkbook
        private string WriteReportExcel(JObject plan, string planSha = PlanSha)
        {
            var book = new ReportBook { Nbh = "012", FileName = "012_Report.xlsx" };
            var placed = new ReportSheet { Name = ReportExcelText.PlacedSheet };
            placed.Tables.Add(ReportSheets.PlacedTable(plan["plots"].OfType<JObject>().ToList()));
            var settings = new ReportSheet { Name = ReportExcelText.SettingsSheet };
            var table = new ReportTable { Header = ReportExcelText.SettingColumns, Formats = new string[2] };
            table.Rows.Add(new object[] { ReportExcelText.RunId, "rx1" });
            table.Rows.Add(new object[] { ReportExcelText.EditableColumns, "Type, Mirror" });
            table.Rows.Add(new object[] { ReportExcelText.PlanBuildId, LevelExcelText.NoBuildId });
            table.Rows.Add(new object[] { ReportExcelText.PlanSha, planSha });
            settings.Tables.Add(table);
            book.Sheets.Add(placed);
            book.Sheets.Add(settings);
            string path = Path.Combine(folder, book.FileName);
            using (var stream = new FileStream(path, FileMode.Create))
            {
                ReportWorkbook.Write(stream, book);
            }
            return path;
        }

        // One cell of a copy set to a value as the team types it, after the key in column A of its row is held to the plot named
        private static void Edit(string path, string sheetName, string reference, string plot, object value)
        {
            string letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
            uint index = uint.Parse(reference.Substring(letters.Length));
            Assert.Equal(plot, XlsxSheet.Read(path, sheetName).Rows[(int)index - 1][0]);
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, true))
            {
                WorkbookPart part = document.WorkbookPart;
                XSheet sheet = part.Workbook.Sheets.Elements<XSheet>().First(item => item.Name == sheetName);
                var page = (WorksheetPart)part.GetPartById(sheet.Id);
                Row row = page.Worksheet.GetFirstChild<SheetData>().Elements<Row>().First(item => item.RowIndex == index);
                Cell old = row.Elements<Cell>().FirstOrDefault(item => item.CellReference == reference);
                Cell made = MakeCell(reference, value, old?.StyleIndex?.Value ?? 0);
                Func<string, string> column = text => new string(text.TakeWhile(char.IsLetter).ToArray());
                Func<string, string, int> order = (one, other) => one.Length != other.Length ? one.Length.CompareTo(other.Length) : string.CompareOrdinal(one, other);
                if (old != null)
                {
                    old.InsertAfterSelf(made);
                    old.Remove();
                }
                else
                {
                    Cell after = row.Elements<Cell>().FirstOrDefault(item => order(column(item.CellReference), letters) > 0);
                    if (after != null)
                    {
                        after.InsertBeforeSelf(made);
                    }
                    else
                    {
                        row.Append(made);
                    }
                }
                page.Worksheet.Save();
            }
        }

        // A cell as Excel saves a typed value: a text inline, a flag as a boolean, a number by its shortest text
        private static Cell MakeCell(string reference, object value, uint style)
        {
            if (value is string text)
            {
                return new Cell(new InlineString(new Text(text))) { CellReference = reference, DataType = CellValues.InlineString, StyleIndex = style };
            }
            if (value is bool flag)
            {
                return new Cell(new CellValue(flag ? "1" : "0")) { CellReference = reference, DataType = CellValues.Boolean, StyleIndex = style };
            }
            string number = value is long whole ? whole.ToString(System.Globalization.CultureInfo.InvariantCulture) : ((double)value).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return new Cell(new CellValue(number)) { CellReference = reference, StyleIndex = style };
        }

        // The paths of the level Excel and the report Excel the last EditedBooks wrote
        private string[] LastPaths;

        // Five edits of the level Excel, two not editable, and four of the report Excel, one not editable
        private List<UpdateBook> EditedBooks(JObject plan, string planSha = PlanSha)
        {
            string level = WriteLevelExcel(plan, planSha);
            string report = WriteReportExcel(plan, planSha);
            Edit(level, "Levels", "F3", "101", 10.25);
            Edit(level, "Levels", "E4", "102", 10.15);
            Edit(level, "Levels", "I5", "103", 3L);
            Edit(level, "Levels", "G3", "101", 10.5);
            Edit(level, "Levels", "F6", "104", 11.0);
            Edit(report, "Placed", "C2", "101", "C10");
            Edit(report, "Placed", "D4", "102", false);
            Edit(report, "Placed", "C5", "102", "Generic 200");
            Edit(report, "Placed", "C7", "103", "ZZ9");
            LastPaths = new[] { level, report };
            return new List<UpdateBook> { UpdateWorkbook.Read(level), UpdateWorkbook.Read(report) };
        }

        [Fact]
        public void R402_an_edited_copy_lists_each_change_its_rule_check_the_tiles_and_the_chips()
        {
            JObject plan = MadePlan();
            UpdateList list = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(), Profile);
            Assert.Null(list.Refusal);
            string[] rows = list.Changes.Select(change => change.Plot + " " + change.Field + " " + Text(change.InModel) + " -> " + Text(change.InExcel) + " | " + change.RuleCheck + " | " + change.Ticked).ToArray();
            Assert.Equal(new[]
            {
                "101 FFL 10.2 -> 10.25 | Height in the step legend, M08 | False",
                "102 El @ Entry 10.2 -> 10.15 | follows the rules | True",
                "103 Steps 2 -> 3 | Steps by the steps rule, Riser height within its limits, M09 | False",
                "101 Villa type VD4 -> C10 | follows the rules | True",
                "102 Mirror True -> False | no rule checks this field | True",
                "103 Villa type DP1 -> ZZ9 | Villa type of the profile | True",
            }, rows);
            // Review 5f: the rule column shows plain names, the ids go to the tooltip, the checks run when the change follows them
            Assert.Equal(new[] { "stepHeights, M08", "fflOverEntry, stepsAtMost, stepHeights, riser", "stepsRule, riser, M09", "villaType", "", "villaType" },
                list.Changes.Select(change => change.RuleIds).ToArray());
            Assert.Equal(new[] { "101 SSL", "104 FFL", "102 Type" }, list.Ignored.Select(cell => cell.Plot + " " + cell.Column).ToArray());
            // Breaker 5f: the Mirror change no rule checks is not counted as one that follows the rules
            Assert.Equal(new Dictionary<string, int> { ["Changes found"] = 6, ["Follow the rules"] = 2, ["Break a manual rule"] = 2, ["Not editable ignored"] = 3 }, list.Tiles());
            Assert.Equal(new Dictionary<string, int> { ["All"] = 6, ["FFL"] = 1, ["El @ Entry"] = 1, ["Steps"] = 1, ["Villa type"] = 2, ["Mirror"] = 1, ["Breaks a rule"] = 3 }, list.Chips());
            Assert.Empty(list.Lacks);
        }

        private static string Text(object value)
        {
            return value is double number ? number.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Fact]
        public void R402_apply_saves_each_ticked_change_as_an_override_that_gives_back_its_cell()
        {
            JObject plan = MadePlan();
            UpdateList list = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(), Profile);
            UpdateApplied applied = UpdateFromReport.Apply(list.Changes, plan, MadeOverrides(), MadeRules(), Profile, "u1");
            Assert.Empty(applied.NotApplied);
            Assert.Equal(new[] { "101", "102", "103" }, applied.Plots.ToArray());
            Assert.Equal(6, applied.Entries.Count);
            var plots = plan["plots"].OfType<JObject>().ToDictionary(item => (string)item["plot"]);
            foreach (UpdateChange change in list.Changes)
            {
                JObject entry = applied.Entries.Single(item => (string)item["plot"] == change.Plot && (string)item["field"] == UpdateFromReport.OverrideField(change.Field));
                JObject rule = (JObject)plots[change.Plot]["levelRule"];
                object back;
                switch (change.Field)
                {
                    case UpdateText.Entry:
                        back = Rounding.Fixed((double)rule["entry"] + (double)entry["value"]["gapToRuleM"], 4);
                        break;
                    case UpdateText.Ffl:
                        back = Rounding.Fixed((double)rule["ffl"] + (double)entry["value"]["gapToRuleM"], 3);
                        break;
                    case UpdateText.Steps:
                        back = (long)entry["value"];
                        break;
                    case UpdateText.Mirror:
                        back = (bool)entry["value"];
                        break;
                    default:
                        back = (string)entry["value"];
                        break;
                }
                Assert.Equal(change.InExcel, back);
                Assert.Equal("parsons", (string)entry["source"]);
                Assert.Equal("Update from report u1", (string)entry["reason"]);
                Assert.True((bool)entry["byHand"]);
            }
            string[] order = applied.List["overrides"].Select(item => (string)item["plot"] + " " + (string)item["field"] + " " + (string)item["source"] + " " + (string)item["key"]).ToArray();
            Assert.Equal(new[]
            {
                "101 heightM parsons img", "101 unit parsons img", "102 entryM native u12", "102 rampFrontM parsons imc", "102 entryM parsons imc", "102 mirrored parsons imc",
                "103 parameter CASE parsons imc", "103 steps parsons imc", "103 unit parsons imc",
            }, order);
            Assert.Equal("{\"plot\": \"101\", \"field\": \"heightM\", \"value\": {\"gapToRuleM\": 0.01}, \"rule\": null, \"source\": \"parsons\", \"model\": \"m_img.rvt\", \"key\": \"img\", \"decision\": \"parsons heightM\", \"reason\": \"made up\"}",
                UpdateFromReport.EntryText(applied.Replaced.Single()));
            Assert.Equal("{\"plot\": \"101\", \"field\": \"heightM\", \"value\": {\"gapToRuleM\": 0.05}, \"rule\": null, \"source\": \"parsons\", \"model\": \"m_img.rvt\", \"key\": \"img\", \"decision\": \"Update from report\", \"reason\": \"Update from report u1\", \"byHand\": true}",
                UpdateFromReport.EntryText((JObject)applied.List["overrides"][0]));
            Assert.Equal("{\"gapToRuleM\": -0.05}", UpdateFromReport.EntryText((JObject)applied.List["overrides"][4]["value"]));
            Assert.Equal(MadeOverrides()["cannot"].ToString(), applied.List["cannot"].ToString());
        }

        [Fact]
        public void R402_the_default_ticks_apply_only_the_changes_that_break_no_manual_rule()
        {
            JObject plan = MadePlan();
            UpdateList list = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(), Profile);
            UpdateApplied applied = UpdateFromReport.Apply(list.Changes.Where(change => change.Ticked), plan, MadeOverrides(), MadeRules(), Profile, "u2");
            Assert.Equal(new[] { "102 entryM", "101 unit", "102 mirrored", "103 unit" }, applied.Entries.Select(item => (string)item["plot"] + " " + (string)item["field"]).ToArray());
            Assert.Equal(new[] { "102", "101", "103" }, applied.Plots.ToArray());
            Assert.Empty(applied.Replaced);
        }

        [Fact]
        public void R402_an_excel_of_another_build_is_refused_with_one_line()
        {
            JObject plan = MadePlan();
            List<UpdateBook> books = EditedBooks(plan);
            JObject other = MadePlan();
            other["run"] = new JObject { ["runId"] = "b2" };
            UpdateList refused = UpdateFromReport.Compare(books, other, PlanSha, MadeRules(), Profile);
            Assert.Equal("012_Levels_parsons2024Rev6.xlsx is of another build, its Plan build id is none, the plan holds no run stamp, the plan of the model b2 (R402)", refused.Refusal);
            Assert.Empty(refused.Changes);
            refused = UpdateFromReport.Compare(books, plan, "bbbb", MadeRules(), Profile);
            Assert.Equal("012_Levels_parsons2024Rev6.xlsx is of another build, its Plan SHA-256 is " + PlanSha + ", the plan of the model bbbb (R402)", refused.Refusal);
            Assert.DoesNotContain("\n", refused.Refusal);
            Assert.Null(UpdateFromReport.Refusal(books[1], plan, PlanSha));
            var bare = new UpdateBook { FileName = "x.xlsx" };
            Assert.Equal("x.xlsx holds no sheet Settings with the Plan build id and the Plan SHA-256, so its build is not known (R402)", UpdateFromReport.Refusal(bare, plan, PlanSha));
        }

        [Fact]
        public void R402_with_no_manual_rules_block_no_change_is_unticked_and_the_lack_is_named()
        {
            JObject plan = MadePlan();
            UpdateList list = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(false), Profile);
            Assert.All(list.Changes, change => Assert.True(change.Ticked));
            Assert.Equal(0, list.Tiles()[UpdateText.BreakAManualRule]);
            Assert.Equal(2, list.Tiles()[UpdateText.FollowTheRules]);
            Assert.Equal(new[] { "the rules profile holds no block manualRules, so no change breaks a manual rule (R402)" }, list.Lacks.ToArray());
        }

        [Fact]
        public void R402_a_cell_with_no_number_is_a_change_that_is_never_applied()
        {
            JObject plan = MadePlan();
            string level = WriteLevelExcel(plan);
            Edit(level, "Levels", "E3", "101", "ten");
            Edit(level, "Levels", "I4", "102", 1.5);
            UpdateList list = UpdateFromReport.Compare(new List<UpdateBook> { UpdateWorkbook.Read(level) }, plan, PlanSha, MadeRules(), Profile);
            Assert.Equal(new[] { "101 El @ Entry the cell holds no number False", "102 Steps the cell holds no whole number False" },
                list.Changes.Select(change => change.Plot + " " + change.Field + " " + change.RuleCheck + " " + change.Ticked).ToArray());
            UpdateApplied applied = UpdateFromReport.Apply(list.Changes, plan, MadeOverrides(), MadeRules(), Profile, "u3");
            Assert.Empty(applied.Entries);
            Assert.Equal(2, applied.NotApplied.Count);
            Assert.Equal(MadeOverrides().ToString(), applied.List.ToString());
        }

        // Breaker 5f: a plot whose Steps come from a steps override keeps that count when a build gives its levels again, so the checks after
        // an El @ Entry edit hold the override count and not the count of the steps rule. 103 at 0.30 m holds 2 steps by the legend, the edit
        // takes it to 0.36 m, 2 steps by the legend and 3 by the rule
        [Fact]
        public void R402_after_a_level_edit_a_plot_with_a_steps_override_keeps_its_override_count_in_the_checks()
        {
            JObject plan = MadePlan();
            JObject plot = plan["plots"].OfType<JObject>().Single(item => (string)item["plot"] == "103");
            plot["levels"]["entry"]["value"] = 10.0;
            plot["levels"]["height"]["value"] = 0.3;
            plot["levelRule"]["entry"] = 10.0;
            plot["overrides"] = new JObject { ["applied"] = new JArray("steps"), ["notApplied"] = new JObject() };
            string level = WriteLevelExcel(plan);
            Edit(level, "Levels", "E5", "103", 9.94);
            UpdateList list = UpdateFromReport.Compare(new List<UpdateBook> { UpdateWorkbook.Read(level) }, plan, PlanSha, MadeRules(), Profile);
            UpdateChange change = list.Changes.Single();
            Assert.Equal("103 El @ Entry", change.Plot + " " + change.Field);
            Assert.Empty(change.Breaks);
            Assert.Empty(change.ManualRules);
            Assert.True(change.Ticked);
            plot.Remove("overrides");
            list = UpdateFromReport.Compare(new List<UpdateBook> { UpdateWorkbook.Read(level) }, plan, PlanSha, MadeRules(), Profile);
            Assert.Equal(new[] { "stepHeights" }, list.Changes.Single().Breaks.ToArray());
        }

        // Breaker 5f: Apply takes the key and model of an entry of the job of the plot, so a plot with none has a change Apply can not save. Compare
        // marks it at once, unticked and never applied, so the window never offers it (Q169)
        [Fact]
        public void R402_a_change_of_a_plot_with_no_override_entry_of_its_job_is_not_savable_unticked_and_never_applied()
        {
            JObject plan = MadePlan();
            JObject overrides = MadeOverrides();
            ((JArray)overrides["overrides"]).RemoveAt(2);
            UpdateList list = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(), Profile, overrides);
            string why = "the plot has no override entry of the job parsons, so Apply can not save it (Q169)";
            Assert.Equal(new[] { "101 FFL  False", "102 El @ Entry " + why + " False", "103 Steps  False", "101 Villa type  True", "102 Mirror " + why + " False", "103 Villa type  True" },
                list.Changes.Select(change => change.Plot + " " + change.Field + " " + change.NotSavable + " " + change.Ticked).ToArray());
            // Breaker 2 of 5f: the why is kept apart from a cell that holds no value, Invalid stays null
            Assert.All(list.Changes, change => Assert.Null(change.Invalid));
            UpdateApplied applied = UpdateFromReport.Apply(list.Changes.Where(change => change.Ticked), plan, overrides, MadeRules(), Profile, "u5");
            Assert.Equal(new[] { "101 unit", "103 unit" }, applied.Entries.Select(item => (string)item["plot"] + " " + (string)item["field"]).ToArray());
            Assert.Empty(applied.NotApplied);
            Assert.Equal(new[] { "101", "103" }, applied.Plots.ToArray());
            UpdateList unknown = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(), Profile);
            Assert.All(unknown.Changes, change => Assert.Null(change.NotSavable));
        }

        // Breaker 2 of 5f: the team raises both FFLs of a piece alike, one plot with an entry of the job and one with none, as in the NBH 012
        // pieces 2616-2615 and 2624-2623. Apply saves one, so the rebuild holds two FFLs in the piece. The not savable edit is no edit in the
        // checks, so the savable one reads its break against the unchanged plot
        [Fact]
        public void R402_a_piece_split_across_a_savable_and_a_not_savable_plot_breaks_one_ffl_per_piece_on_the_savable_change()
        {
            JObject plan = MadePlan();
            JObject overrides = MadeOverrides();
            ((JArray)overrides["overrides"]).RemoveAt(3);
            string level = WriteLevelExcel(plan);
            Edit(level, "Levels", "F4", "102", 10.35);
            Edit(level, "Levels", "F5", "103", 10.35);
            UpdateList list = UpdateFromReport.Compare(new List<UpdateBook> { UpdateWorkbook.Read(level) }, plan, PlanSha, MadeRules(), Profile, overrides);
            Assert.Equal(new[] { "102 FFL", "103 FFL" }, list.Changes.Select(change => change.Plot + " " + change.Field).ToArray());
            UpdateChange savable = list.Changes[0], held = list.Changes[1];
            Assert.Contains(UpdateFromReport.OneFflPerPiece, savable.Breaks);
            Assert.Contains(UpdateFromReport.OneFflPerPiece, savable.Checks);
            Assert.Contains("One FFL per piece", savable.RuleCheck);
            Assert.Equal("the plot has no override entry of the job parsons, so Apply can not save it (Q169)", held.RuleCheck);
            Assert.Empty(held.Checks);
            Assert.Empty(held.Breaks);
            Assert.False(held.Ticked);
            UpdateApplied applied = UpdateFromReport.Apply(list.Changes.Where(change => change.Ticked), plan, overrides, MadeRules(), Profile, "u9");
            Assert.Equal(new[] { "102" }, applied.Plots.ToArray());
        }

        // Breaker 2 of 5f: two level Excels of one NBH picked together listed a plot twice and Apply saved one of them silently, so a second
        // book of the same kind is refused with one line naming both files
        [Fact]
        public void R402_a_second_level_excel_or_report_excel_of_one_nbh_is_refused_with_one_line_naming_both()
        {
            JObject plan = MadePlan();
            List<UpdateBook> books = EditedBooks(plan);
            string level2 = Path.Combine(folder, "012_Levels_" + Profile + " again.xlsx");
            string report2 = Path.Combine(folder, "012_Report again.xlsx");
            File.Copy(LastPaths[0], level2);
            File.Copy(LastPaths[1], report2);
            string tail = ", Update from report takes one level Excel and one report Excel of one NBH (R402)";
            UpdateList twice = UpdateFromReport.Compare(new List<UpdateBook> { books[0], books[1], UpdateWorkbook.Read(level2) }, plan, PlanSha, MadeRules(), Profile, MadeOverrides());
            Assert.Equal(books[0].FileName + " and " + Path.GetFileName(level2) + " are both level Excels" + tail, twice.Refusal);
            Assert.Empty(twice.Changes);
            Assert.Empty(twice.Ignored);
            twice = UpdateFromReport.Compare(new List<UpdateBook> { books[1], UpdateWorkbook.Read(report2) }, plan, PlanSha, MadeRules(), Profile, MadeOverrides());
            Assert.Equal(books[1].FileName + " and " + Path.GetFileName(report2) + " are both report Excels" + tail, twice.Refusal);
            Assert.Empty(twice.Changes);
            Assert.Null(UpdateFromReport.Compare(books, plan, PlanSha, MadeRules(), Profile, MadeOverrides()).Refusal);
        }

        // Breaker 2 of 5f: a change Apply can not save breaks no rule, so the chip Breaks a rule counts the rule breaks and the cells that hold
        // no value Apply can take, an empty cell with them (CHOSEN), as the team must fix each of those in the Excel. Real: 2311 in the extra try
        [Fact]
        public void R402_a_not_savable_change_is_kept_apart_from_a_rule_break_and_not_counted_in_breaks_a_rule()
        {
            JObject plan = MadePlan();
            JObject overrides = MadeOverrides();
            ((JArray)overrides["overrides"]).RemoveAt(2);
            UpdateList list = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(), Profile, overrides);
            Assert.Equal(new[] { "101 FFL", "103 Steps", "103 Villa type" }, list.Chip(UpdateText.BreaksARule).Select(change => change.Plot + " " + change.Field).ToArray());
            Assert.Equal(3, list.Chips()[UpdateText.BreaksARule]);
            UpdateChange mirror = list.Changes.Single(change => change.Field == UpdateText.Mirror);
            Assert.False(mirror.BreaksARule);
            Assert.Equal("the plot has no override entry of the job parsons, so Apply can not save it (Q169)", mirror.RuleCheck);
            Assert.Equal("", mirror.RuleIds);
            Assert.True(new UpdateChange { Invalid = UpdateText.Empty }.BreaksARule);
            Assert.True(new UpdateChange { Invalid = UpdateText.NoNumber }.BreaksARule);
        }

        // Breaker 5f: a mirror the hand rules of the profile gave, source computed in the plan, is checked by them, and an edit of it breaks
        // them. A mirror an override gave holds no hand of the rules, so no rule checks it
        [Fact]
        public void R402_a_mirror_change_of_a_plot_whose_hand_the_rules_gave_breaks_the_hand_rule()
        {
            JObject plan = MadePlan();
            plan["plots"][1]["mirrored"]["source"] = "computed";
            UpdateList list = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(), Profile);
            UpdateChange mirror = list.Changes.Single(change => change.Field == UpdateText.Mirror);
            Assert.Equal("Mirror by the hand rule", mirror.RuleCheck);
            Assert.Equal("handRule", mirror.RuleIds);
            Assert.True(mirror.Ticked);
            Assert.Equal(2, list.Tiles()[UpdateText.FollowTheRules]);
            Assert.Equal(4, list.Chips()[UpdateText.BreaksARule]);
            Assert.Equal(UpdateFromReport.Checks.Length, UpdateText.RuleNames.Count);
            Assert.All(UpdateText.RuleNames.Values, name => Assert.InRange(name.Split(' ').Length, 1, 6));
        }

        [Fact]
        public void R402_the_result_json_holds_the_changes_tiles_chips_entries_and_list_text()
        {
            JObject plan = MadePlan();
            UpdateList list = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(), Profile);
            UpdateApplied applied = UpdateFromReport.Apply(list.Changes.Where(change => change.Ticked), plan, MadeOverrides(), MadeRules(), Profile, "u4");
            JObject json = UpdateFromReport.ResultJson(list, applied);
            Assert.Equal(6, ((JArray)json["changes"]).Count);
            Assert.Equal("stepsRule", (string)json["changes"][2]["breaks"][0]);
            Assert.Equal(5, (int)json["changes"][2]["row"]);
            Assert.Equal(3, (int)json["tiles"]["Not editable ignored"]);
            Assert.Equal(3, (int)json["chips"]["Breaks a rule"]);
            Assert.Equal(new[] { "102", "101", "103" }, json["plots"].Select(item => (string)item).ToArray());
            Assert.Equal(UpdateFromReport.ListText(applied.List), (string)json["listText"]);
            Assert.False(UpdateFromReport.ResultJson(list, null).ContainsKey("entries"));
        }

        [Fact]
        public void R402_the_list_text_holds_one_entry_per_line_as_majority_writes_it()
        {
            JObject list = MadeOverrides();
            ((JArray)list["overrides"]).Add(new JObject { ["plot"] = "105", ["field"] = "unit", ["value"] = "Façade \"A\"", ["byHand"] = true });
            string text = UpdateFromReport.ListText(list);
            string[] lines = text.Split('\n');
            Assert.Equal("{", lines[0]);
            Assert.Equal("  \"note\": \"made up\",", lines[1]);
            Assert.Equal("  \"overrides\": [", lines[2]);
            Assert.Equal("    {\"plot\": \"105\", \"field\": \"unit\", \"value\": \"Façade \\\"A\\\"\", \"byHand\": true}", lines[7]);
            Assert.Equal("  ],", lines[8]);
            Assert.Equal("  \"cannot\": [", lines[9]);
            Assert.EndsWith("  ]\n}\n", text);
            Assert.True(JToken.DeepEquals(list, JObject.Parse(text)));
        }

        internal static JObject WorkerConfig()
        {
            return new JObject
            {
                ["siteBuild"] = new JObject { ["minFreeGB"] = 2.0, ["roadSurfaces"] = new JArray("Road A") },
                ["placerCheck"] = new JObject { ["level"] = new JObject { ["passMm"] = 1.0 } },
                ["addin"] = new JObject
                {
                    ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "Road.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) },
                    ["workerTimeLimitS"] = new JObject { ["updateFromReport"] = 60 },
                    ["workerTimeLimitMinS"] = 10,
                },
            };
        }

        private string WriteText(string name, string text)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text);
            return path;
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        // The command updateFromReport on the edited copies of 012. The plan is written first so the Excel files carry its real SHA-256, runPlan
        // then takes its place on disk to make a plan of another build
        // The override list of the worker runs: plot 102 holds no entry of the job parsons, so its changes are not savable
        private static JObject WorkerOverrides()
        {
            JObject overrides = MadeOverrides();
            ((JArray)overrides["overrides"]).RemoveAt(2);
            return overrides;
        }

        private WorkerAnswer RunUpdate(JObject plan, bool withExcel, out int code, string id, JObject runPlan = null)
        {
            string planPath = WriteText("plan_012.json", plan.ToString());
            EditedBooks(plan, FileHash.Sha256(planPath));
            if (runPlan != null)
            {
                WriteText("plan_012.json", runPlan.ToString());
            }
            var inputs = new List<WorkerFile>
            {
                Input("config", WriteText("project.json", WorkerConfig().ToString())),
                Input("rules", WriteText("rules.json", MadeRules().ToString())),
                Input(UpdateFromReportWorker.PlanRole, planPath),
                Input(UpdateFromReportWorker.OverridesRole, WriteText("overrides.json", WorkerOverrides().ToString())),
            };
            if (withExcel)
            {
                inputs.Add(Input(UpdateFromReportWorker.LevelRole, LastPaths[0]));
                inputs.Add(Input(UpdateFromReportWorker.ReportRole, LastPaths[1]));
            }
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = id, Command = "updateFromReport", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = inputs, LevelProfile = Profile, Scope = new WorkerScope { Kind = "nbh", Nbh = "012" }, OutputFolder = Path.Combine(folder, "out"), TimeLimitS = 60,
            };
            code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            return WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, id));
        }

        [Fact]
        public void R402_the_worker_command_gives_back_the_update_list_as_json_that_compare_gives()
        {
            JObject plan = MadePlan();
            int code;
            WorkerAnswer answer = RunUpdate(plan, true, out code, "uw1");
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            string output = Path.Combine(folder, "out", UpdateFromReportWorker.OutputName);
            Assert.Equal(new[] { output }, answer.Outputs.Select(item => item.Path).ToArray());
            JObject json = JObject.Parse(File.ReadAllText(output));
            Assert.NotNull(json[RunStamp.Key]);
            json.Remove(RunStamp.Key);
            string sha = FileHash.Sha256(Path.Combine(folder, "plan_012.json"));
            UpdateList expected = UpdateFromReport.Compare(EditedBooks(plan, sha), plan, sha, MadeRules(), Profile, WorkerOverrides());
            Assert.True(JToken.DeepEquals(UpdateFromReport.ResultJson(expected, null), json), json.ToString());
            Assert.Equal(2, json["changes"].Count(item => ((string)item["notSavable"] ?? "").Contains("no override entry of the job parsons")));
            Assert.Equal(6, ((JArray)json["changes"]).Count);
            Assert.Equal(3, (int)json["tiles"]["Not editable ignored"]);
            Assert.Equal(6, answer.Counts["changes"]);
            Assert.Equal(3, answer.Counts["ignored"]);
        }

        [Fact]
        public void R402_the_worker_command_gives_the_refusal_of_an_excel_of_another_build_in_its_json()
        {
            JObject plan = MadePlan();
            JObject other = MadePlan();
            other["run"] = new JObject { ["runId"] = "b2" };
            int code;
            WorkerAnswer answer = RunUpdate(plan, true, out code, "uw2", other);
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            JObject json = JObject.Parse(File.ReadAllText(Path.Combine(folder, "out", UpdateFromReportWorker.OutputName)));
            Assert.Contains("is of another build", (string)json["refusal"]);
            Assert.Empty((JArray)json["changes"]);
        }

        [Fact]
        public void R402_the_worker_command_refuses_a_request_with_no_excel_file()
        {
            int code;
            WorkerAnswer answer = RunUpdate(MadePlan(), false, out code, "uw3");
            Assert.Equal(WorkerExit.Refused, code);
            Assert.Contains("no level Excel or report Excel", answer.Message);
        }

        [Fact]
        public void R402_the_worker_reads_the_excel_files_in_place_and_changes_none()
        {
            int code;
            WorkerAnswer answer = RunUpdate(MadePlan(), true, out code, "uw4");
            Assert.Equal(0, code);
            foreach (string path in LastPaths)
            {
                WorkerOpened opened = answer.Opened.Single(item => item.Path == path);
                Assert.Equal(opened.Sha256Before, opened.Sha256After);
                Assert.Equal(FileHash.Sha256(path), opened.Sha256After);
            }
        }

        [Fact]
        public void R402_the_request_of_the_add_in_names_the_files_by_role_and_the_worker_runs_it()
        {
            JObject plan = MadePlan();
            string planPath = WriteText("plan_012.json", plan.ToString());
            EditedBooks(plan, FileHash.Sha256(planPath));
            string config = WriteText("project.json", WorkerConfig().ToString());
            string rules = WriteText("rules.json", MadeRules().ToString());
            string refusal;
            string nbh = UpdateWorkerRequest.Nbh(LastPaths, out refusal);
            Assert.Equal("012", nbh);
            Assert.Null(refusal);
            Assert.Equal(planPath, UpdateWorkerRequest.PlanPath(folder, nbh));
            string overrides = WriteText("overrides.json", WorkerOverrides().ToString());
            WorkerRequest request = UpdateWorkerRequest.Make("uw6", new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" }, config, rules, planPath, overrides, Profile, nbh, LastPaths, Path.Combine(folder, "out"));
            Assert.Equal("updateFromReport", request.Command);
            Assert.Equal(new[] { "config", "rules", "plan", "overrides", "excel1", "excel2" }, request.Inputs.Select(item => item.Role).ToArray());
            Assert.Equal(60, request.TimeLimitS);
            Assert.Equal(Profile, request.LevelProfile);
            int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "uw6"));
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            Assert.Equal(6, answer.Counts["changes"]);
        }

        [Fact]
        public void R402_the_files_of_two_nbhs_or_with_no_nbh_in_their_name_are_refused_before_any_request()
        {
            string refusal;
            Assert.Null(UpdateWorkerRequest.Nbh(new[] { "a\\012_Levels_x.xlsx", "a\\013_Report.xlsx" }, out refusal));
            Assert.Equal("the files are of NBH 012 and 013, Update from report takes the files of one NBH (R402)", refusal);
            Assert.Null(UpdateWorkerRequest.Nbh(new[] { "a\\Report.xlsx" }, out refusal));
            Assert.Contains("starts with its NBH", refusal);
            // CI on Linux: a path written with either separator gives the NBH of its file name, as the add-in on Windows reads it
            Assert.Equal("012", UpdateWorkerRequest.NbhOf("C:\\work\\012_Levels_x.xlsx"));
            Assert.Equal("012", UpdateWorkerRequest.NbhOf("/home/work/012_Levels_x.xlsx"));
            Assert.Equal("012", UpdateWorkerRequest.Nbh(new[] { "a\\012_Levels_x.xlsx", "b\\012_Report.xlsx" }, out refusal));
        }

        [Fact]
        public void R402_the_time_limit_and_the_command_name_are_in_the_config_and_the_contract()
        {
            Assert.Contains("updateFromReport", WorkerRequest.Commands);
            string config = Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json");
            Assert.Equal(60, AddinConfig.Load(config).WorkerTimeLimitS("updateFromReport"));
        }

        [Fact]
        public void R402_the_list_read_back_from_the_json_applies_as_the_list_the_worker_made()
        {
            JObject plan = MadePlan();
            UpdateList list = UpdateFromReport.Compare(EditedBooks(plan), plan, PlanSha, MadeRules(), Profile);
            UpdateList back = UpdateListJson.Read(JObject.Parse(UpdateFromReport.ResultJson(list, null).ToString()));
            Assert.Equal(list.Tiles(), back.Tiles());
            Assert.Equal(list.Chips(), back.Chips());
            Assert.Equal(list.Changes.Select(change => change.RuleCheck + change.Ticked), back.Changes.Select(change => change.RuleCheck + change.Ticked));
            UpdateApplied one = UpdateFromReport.Apply(list.Changes, plan, MadeOverrides(), MadeRules(), Profile, "u9");
            UpdateApplied two = UpdateFromReport.Apply(back.Changes, plan, MadeOverrides(), MadeRules(), Profile, "u9");
            Assert.Equal(UpdateFromReport.ListText(one.List), UpdateFromReport.ListText(two.List));
            Assert.Empty(two.NotApplied);
            Assert.Equal(one.Plots, two.Plots);
            UpdateList refused = UpdateListJson.Read(new JObject { ["refusal"] = "one line", ["changes"] = new JArray(), ["ignored"] = new JArray(), ["lacks"] = new JArray("a lack") });
            Assert.Equal("one line", refused.Refusal);
            Assert.Equal(new[] { "a lack" }, refused.Lacks.ToArray());
        }
    }
}
