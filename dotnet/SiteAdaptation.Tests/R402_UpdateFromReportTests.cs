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
                rules["manualRules"] = JArray.Parse("[{\"id\": \"M08\", \"text\": \"At most 3 added steps, made up\", \"updateChecks\": [\"stepsAtMost\", \"stepHeights\"]}, "
                    + "{\"id\": \"M09\", \"text\": \"Riser 100 to 180 mm, made up\", \"updateChecks\": [\"riser\"]}]");
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
        private string WriteLevelExcel(JObject plan)
        {
            Func<string[], JObject> written = plots => new JObject { ["written"] = new JObject { ["plots"] = new JArray(plots) } };
            Func<string[], JObject> piece = plots => new JObject { ["written"] = new JObject { ["piece"] = new JArray(plots) } };
            Func<string[], JObject> row = plots => new JObject { ["written"] = new JObject { ["cluster"] = new JArray(plots) } };
            var inputs = new LevelInputs
            {
                Plan = plan, PlanSha256 = PlanSha, Rules = MadeRules(), RulesName = "rules.json", RulesSha256 = "r", Config = new JObject { ["placerCheck"] = new JObject { ["level"] = new JObject { ["passMm"] = 1.0 } } },
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
        private string WriteReportExcel(JObject plan)
        {
            var book = new ReportBook { Nbh = "012", FileName = "012_Report.xlsx" };
            var placed = new ReportSheet { Name = ReportExcelText.PlacedSheet };
            placed.Tables.Add(ReportSheets.PlacedTable(plan["plots"].OfType<JObject>().ToList()));
            var settings = new ReportSheet { Name = ReportExcelText.SettingsSheet };
            var table = new ReportTable { Header = ReportExcelText.SettingColumns, Formats = new string[2] };
            table.Rows.Add(new object[] { ReportExcelText.RunId, "rx1" });
            table.Rows.Add(new object[] { ReportExcelText.EditableColumns, "Type, Mirror" });
            table.Rows.Add(new object[] { ReportExcelText.PlanBuildId, LevelExcelText.NoBuildId });
            table.Rows.Add(new object[] { ReportExcelText.PlanSha, PlanSha });
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

        // Five edits of the level Excel, two not editable, and four of the report Excel, one not editable
        private List<UpdateBook> EditedBooks(JObject plan)
        {
            string level = WriteLevelExcel(plan);
            string report = WriteReportExcel(plan);
            Edit(level, "Levels", "F3", "101", 10.25);
            Edit(level, "Levels", "E4", "102", 10.15);
            Edit(level, "Levels", "I5", "103", 3L);
            Edit(level, "Levels", "G3", "101", 10.5);
            Edit(level, "Levels", "F6", "104", 11.0);
            Edit(report, "Placed", "C2", "101", "C10");
            Edit(report, "Placed", "D4", "102", false);
            Edit(report, "Placed", "C5", "102", "Generic 200");
            Edit(report, "Placed", "C7", "103", "ZZ9");
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
                "101 FFL 10.2 -> 10.25 | stepHeights, M08 | False",
                "102 El @ Entry 10.2 -> 10.15 | follows the rules | True",
                "103 Steps 2 -> 3 | stepsRule, riser, M09 | False",
                "101 Villa type VD4 -> C10 | follows the rules | True",
                "102 Mirror True -> False | follows the rules | True",
                "103 Villa type DP1 -> ZZ9 | villaType | True",
            }, rows);
            Assert.Equal(new[] { "101 SSL", "104 FFL", "102 Type" }, list.Ignored.Select(cell => cell.Plot + " " + cell.Column).ToArray());
            Assert.Equal(new Dictionary<string, int> { ["Changes found"] = 6, ["Follow the rules"] = 3, ["Break a manual rule"] = 2, ["Not editable ignored"] = 3 }, list.Tiles());
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
            Assert.Equal(3, list.Tiles()[UpdateText.FollowTheRules]);
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
    }
}
