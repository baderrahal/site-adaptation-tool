using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Tags;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R477, Q277, Q351. The footprint check holds a plot to the limit only when the block round its base is of its unit. A fake document,
    // so these run on net8.0 too
    public sealed class R477_FootprintTypeTests : IDisposable
    {
        private const string FileOfVl4 = "RRE-WP-NSP-KEO-ARC-VL4-ZZ-MOD-2400.rvt";
        private readonly string work = Path.Combine(Path.GetTempPath(), "r477_" + Guid.NewGuid().ToString("N"));
        private readonly string prototypes;

        public R477_FootprintTypeTests()
        {
            prototypes = Path.Combine(work, "prototypes");
            Directory.CreateDirectory(prototypes);
            File.WriteAllText(Path.Combine(prototypes, "VD4.rvt"), "prototype");
            File.WriteAllText(Path.Combine(prototypes, FileOfVl4), "prototype");
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private static readonly double[][] Shape = Strip(0, 0, 8, 15);

        private static double[][] Strip(double west, double south, double east, double north)
        {
            return new[] { new[] { west, south }, new[] { east, south }, new[] { east, north }, new[] { west, north } };
        }

        private static LightModeConfig Config(double limitMm = 1200.0)
        {
            return LightModeConfig.From(new JObject
            {
                ["addin"] = new JObject
                {
                    ["lightMode"] = new JObject
                    {
                        ["villaModeDefault"] = "masses", ["solidHeightM"] = 3.0, ["baseBoundary"] = "lowestFloor", ["baseBoundaryFloorType"] = null,
                        ["footprintLimitMm"] = limitMm, ["footprintSampleStepMm"] = 10.0, ["houseLayer"] = "0", ["houseMinAreaM2"] = 60.0,
                    },
                },
            });
        }

        private static JObject ReadBlock(string name, double[][] house)
        {
            var block = new JObject { ["outline"] = Points(house), ["rings"] = new JArray(new JObject { ["layer"] = "0", ["points"] = Points(house) }) };
            if (name != null)
            {
                block["name"] = name;
            }
            return block;
        }

        private static JArray Points(IEnumerable<double[]> ring)
        {
            return new JArray(ring.Select(point => new JArray(point[0], point[1])));
        }

        // The base lies shifted east of its house by the given metres, so the gap of the check is that shift
        private static FootprintRow RowOf(string block, string unit, double shiftM = 0.0, string prototype = "x.rvt", string keoFile = null)
        {
            var read = new JObject { ["blocks"] = new JArray(ReadBlock(block, Shape)) };
            LightModeConfig config = Config();
            return FootprintCheck.Row("101", Strip(shiftM, 0, 8 + shiftM, 15), FootprintCheck.FromRead(read, config), config, prototype, unit, keoFile);
        }

        [Fact]
        public void R477_a_plot_of_the_same_type_under_the_limit_passes()
        {
            FootprintRow row = RowOf("Villa_VL4", "VL4", 1.1);
            Assert.Equal(1100.0, row.DistanceMm.Value, 3);
            Assert.False(row.Flagged);
            Assert.Null(row.OtherType);
            Assert.Null(row.Why);
        }

        [Fact]
        public void R477_a_plot_of_the_same_type_over_the_limit_is_flagged()
        {
            FootprintRow row = RowOf("Villa_VL4", "VL4", 1.3);
            Assert.Equal(1300.0, row.DistanceMm.Value, 3);
            Assert.True(row.Flagged);
            Assert.Null(row.OtherType);
            Assert.Contains("over the limit", row.Why);
        }

        [Fact]
        public void R477_a_plot_of_another_type_keeps_its_distance_is_not_flagged_and_names_the_block_the_unit_and_Q277()
        {
            FootprintRow row = RowOf("Duplex_DP4", "DT3", 3.2);
            Assert.Equal(3200.0, row.DistanceMm.Value, 3);
            Assert.False(row.Flagged);
            Assert.Equal("Duplex_DP4", row.OtherType);
            Assert.Contains("Duplex_DP4", row.Why);
            Assert.Contains("DT3", row.Why);
            Assert.Contains("Q277", row.Why);
        }

        [Fact]
        public void R477_a_plot_with_no_outline_holding_its_base_stays_flagged()
        {
            var read = new JObject { ["blocks"] = new JArray(ReadBlock("Villa_VL4", Strip(100, 0, 108, 15))) };
            LightModeConfig config = Config();
            FootprintRow row = FootprintCheck.Row("101", Shape, FootprintCheck.FromRead(read, config), config, "x.rvt", "VL4");
            Assert.True(row.Flagged);
            Assert.Null(row.DistanceMm);
            Assert.Null(row.OtherType);
            Assert.Contains("no villa block", row.Why);
        }

        [Fact]
        public void R477_a_block_name_with_no_underscore_is_another_type_with_its_name()
        {
            FootprintRow row = RowOf("VL4", "VL4", 3.0);
            Assert.False(row.Flagged);
            Assert.Equal("VL4", row.OtherType);
            Assert.Contains("VL4", row.Why);
        }

        [Fact]
        public void R477_a_block_of_no_name_is_another_type_and_its_row_reads_as_text()
        {
            FootprintRow row = RowOf(null, "VL4", 3.0);
            Assert.False(row.Flagged);
            Assert.Equal(FootprintOutlines.NoBlockName, row.OtherType);
            Assert.Equal(-1, row.Why.IndexOf('\u0000'));
        }

        [Theory]
        [InlineData("Villa_VL4", "VL4", true)]
        [InlineData("MP_Villa_VL4", "VL4", true)]
        [InlineData("Villa_vl4", "VL4", true)]
        [InlineData("VL4_Villa", "VL4", false)]
        [InlineData("Villa_", "VL4", false)]
        [InlineData("Villa_VL4", "VL41", false)]
        public void R477_the_type_of_a_block_is_the_part_of_its_name_after_the_last_underscore(string block, string unit, bool same)
        {
            Assert.Equal(same, RowOf(block, unit, 3.0).Flagged);
            Assert.Equal(same ? null : block, RowOf(block, unit, 3.0).OtherType);
        }

        [Fact]
        public void R477_a_named_block_with_no_unit_at_hand_is_another_type()
        {
            FootprintRow row = RowOf("Villa_VL4", null, 3.0);
            Assert.False(row.Flagged);
            Assert.Equal("Villa_VL4", row.OtherType);
        }

        // A list of outlines with no read behind it names no block, so there is no type to tell and the limit holds as in R404
        [Fact]
        public void R477_an_outline_of_no_block_name_is_held_to_the_limit()
        {
            FootprintRow row = FootprintCheck.Row("101", Strip(3, 0, 11, 15), new List<double[][]> { Shape }, Config(), "x.rvt", "VL4");
            Assert.True(row.Flagged);
            Assert.Null(row.OtherType);
        }

        [Fact]
        public void R477_a_read_of_no_house_layer_keeps_the_block_name_with_the_block_outline()
        {
            var read = new JObject { ["blocks"] = new JArray(new JObject { ["name"] = "Duplex_DP4", ["outline"] = Points(Shape) }) };
            FootprintOutlines outlines = Assert.IsType<FootprintOutlines>(FootprintCheck.FromRead(read));
            Assert.Equal("Duplex_DP4", outlines.BlockOf(outlines.Single()));
        }

        [Fact]
        public void R477_the_file_and_the_summary_count_other_type_apart_and_the_largest_gap_is_of_the_same_type()
        {
            var rows = new List<FootprintRow>
            {
                RowOf("Villa_VL4", "VL4", 0.6, "A.rvt"),
                RowOf("Villa_VL4", "VL4", 1.3, "A.rvt"),
                RowOf("Duplex_DP4", "VL4", 3.2, "A.rvt"),
                FootprintCheck.Row("104", Strip(100, 0, 108, 15), new FootprintOutlines(), Config(), "A.rvt", "VL4"),
                RowOf("Duplex_DP4", "DT3", 3.5, "B.rvt"),
            };

            JObject json = FootprintCheck.Json(rows);

            JToken a = json["prototypes"].Single(item => (string)item["prototype"] == "A.rvt");
            Assert.Equal(4, (int)a["plots"]);
            Assert.Equal(2, (int)a["sameType"]);
            Assert.Equal(1, (int)a["overLimit"]);
            Assert.Equal(1, (int)a["otherType"]);
            Assert.Equal(1, (int)a["noOutline"]);
            Assert.Equal(1300.0, (double)a["largestGapMm"], 3);
            JToken b = json["prototypes"].Single(item => (string)item["prototype"] == "B.rvt");
            Assert.Equal(1, (int)b["otherType"]);
            Assert.Equal(JTokenType.Null, b["largestGapMm"].Type);
            Assert.Equal(new JObject { ["sameType"] = 2, ["overLimit"] = 1, ["otherType"] = 2, ["noOutline"] = 1 }.ToString(), json["counts"].ToString());
            Assert.Equal("Duplex_DP4", (string)json["rows"][2]["otherType"]);
            Assert.Equal(3200.0, (double)json["rows"][2]["distanceMm"], 3);
            Assert.False((bool)json["rows"][2]["flagged"]);
            Assert.Null((string)json["rows"][0]["otherType"]);

            List<string> lines = FootprintCheck.SummaryLines(rows).ToList();
            Assert.Contains("prototype A.rvt: largest gap to the house outline 1300 mm, 1 of 2 plots of the same type over the limit, 1 of another type (Q277), 1 with no outline (R453, R477)", lines);
            Assert.Contains("prototype B.rvt: no plot of the same type, 1 of another type (Q277), 0 with no outline (R453, R477)", lines);
        }

        private PlanPlot Plot(string number, double east, string unit, string prototype = "VD4.rvt")
        {
            return new PlanPlot
            {
                Plot = number,
                Unit = unit == null ? null : new Sourced<string> { Value = unit },
                PrototypeFile = new Sourced<string> { Value = prototype },
                LinkName = new Sourced<string> { Value = "VD4-" + number },
                Insert = new Sourced<double[]> { Value = new[] { east, 2000.0, 5.0 } },
                RotationDeg = new Sourced<double?> { Value = 0.0 },
                Mirrored = new Sourced<bool?> { Value = false },
                Levels = new PlanLevels { Ffl = new Sourced<double?> { Value = 5.0 } },
                Parameters = new Dictionary<string, Sourced<string>> { [LinkFields.PlotNumber] = new Sourced<string> { Value = number } },
            };
        }

        private sealed class FixedShape : IBaseShapeReader
        {
            public IReadOnlyList<double[]> Read(string prototypePath, LightModeConfig config) => Shape;
        }

        private PlaceVillasRun Make(JObject read, params PlanPlot[] plots)
        {
            return Make(read, new Dictionary<string, string>(), plots);
        }

        private PlaceVillasRun Make(JObject read, IReadOnlyDictionary<string, string> keoFiles, params PlanPlot[] plots)
        {
            var plan = new PlanFile
            {
                Format = PlanFile.FormatName, FormatVersion = PlanFile.Version, Neighbourhood = "012",
                SurveyPoint = new PlanSurveyPoint { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 1000.0, 2000.0, 0.0 } },
                AngleToTrueNorthDeg = 0, PrototypeFolder = prototypes, Plots = plots.ToList(),
            };
            LightModeConfig config = Config();
            Func<RunContext, VillaInputs> load = context => new VillaInputs
            {
                Plan = plan, PlanPath = Path.Combine(work, "plan_012.json"), Config = config, Mode = VillaModes.Masses, Profile = "parsons2024Rev6",
                ParameterGuids = new Dictionary<string, string> { [LinkFields.PlotNumber] = FakeVillas.PlotGuid },
                Footprint = FootprintCheck.FromRead(read, config), KeoFiles = keoFiles,
            };
            return new PlaceVillasRun(new FakeVillas(), load, context => new ShapeCache(context.WorkFolder, new FixedShape(), path => new string('a', 64)), path => new string('a', 64), lines => true);
        }

        private RunContext Context()
        {
            return new RunContext(7, "Place villas", "r7", work, null, true, new RunInput[0], new Dictionary<string, string>());
        }

        // Plot 101 stands in its own type, 102 and 103 in another, 104 in no outline, each by the unit of the plan, every file name is VD4
        private PlaceVillasRun Mixed()
        {
            var read = new JObject
            {
                ["blocks"] = new JArray(
                    ReadBlock("Villa_VL4", Strip(1100, 2000, 1108, 2015)),
                    ReadBlock("Duplex_DP4", Strip(1200, 2000, 1208, 2015)),
                    ReadBlock("Duplex_DP4", Strip(1300, 2000, 1308, 2015))),
            };
            return Make(read, Plot("101", 1100.5, "VL4"), Plot("102", 1203, "DT3"), Plot("103", 1303, "DT3"), Plot("104", 1400, "VL4"));
        }

        [Fact]
        public void R477_the_run_line_tells_over_limit_of_the_same_type_another_type_and_no_outline_by_the_unit_of_the_plan()
        {
            string counts = Mixed().Body().Preview(Context()).Counts;
            Assert.Contains(", over the footprint limit 0 of 1 of the same type, 2 of another type (Q277), 1 with no outline", counts);
        }

        [Fact]
        public void R477_a_real_run_writes_the_counts_and_lists_no_plot_of_another_type_as_an_issue()
        {
            ButtonBody body = Mixed().Body();
            body.Preview(Context());
            body.Outside?.Invoke(Context())?.Dispose();
            IReadOnlyList<string> issues = body.Apply(Context());

            JObject written = JObject.Parse(File.ReadAllText(Directory.GetFiles(work, "footprint_012.json*").Single()));
            Assert.Equal(2, (int)written["counts"]["otherType"]);
            Assert.Equal(1, (int)written["counts"]["noOutline"]);
            Assert.Equal(1, (int)written["counts"]["sameType"]);
            Assert.DoesNotContain(issues, line => line.StartsWith("plot 102", StringComparison.Ordinal));
            Assert.Contains(issues, line => line.StartsWith("plot 104", StringComparison.Ordinal) && line.Contains("flagged"));
        }

        // A plan that leaves the unit out still tells the type, by the unit code the prototype file name holds
        [Fact]
        public void R477_a_plot_whose_plan_gives_no_unit_takes_the_unit_of_its_prototype_file()
        {
            var read = new JObject { ["blocks"] = new JArray(ReadBlock("Villa_VL4", Strip(1100, 2000, 1108, 2015))) };
            string counts = Make(read, Plot("101", 1100.5, null, FileOfVl4)).Body().Preview(Context()).Counts;
            Assert.Contains(", over the footprint limit 0 of 1 of the same type, 0 of another type (Q277), 0 with no outline", counts);
        }

        [Fact]
        public void R479_a_unit_goes_through_its_keo_file_before_the_compare_so_a_th1_im_in_a_th1_block_is_the_same_type()
        {
            FootprintRow row = RowOf("Villa_TH1", "TH1-IM", 0.63, keoFile: "TH1");
            Assert.Null(row.OtherType);
            Assert.False(row.Flagged);
            Assert.Equal(630.0, row.DistanceMm.Value, 3);
            Assert.True(RowOf("Villa_TH1", "TH1-IM", 1.3, keoFile: "TH1").Flagged);
        }

        [Fact]
        public void R479_a_unit_with_no_keo_file_compares_by_its_own_name_as_before()
        {
            Assert.Equal("Villa_TH1", RowOf("Villa_TH1", "TH1-IM", 0.63).OtherType);
            Assert.Null(RowOf("Villa_TH1", "TH1", 0.63).OtherType);
        }

        [Theory]
        [InlineData("Duplex_DP4", true)]
        [InlineData("Villa_VD4", true)]
        [InlineData("Duplex_DP2", false)]
        public void R479_a_unit_with_a_keo_file_is_the_same_type_in_a_block_of_that_file_or_of_its_own_name_and_no_other(string block, bool same)
        {
            Assert.Equal(same ? null : block, RowOf(block, "VD4", 0.63, keoFile: "DP4").OtherType);
        }

        [Fact]
        public void R479_the_keo_files_are_read_from_typologies_of_the_rules_and_a_rules_file_with_none_gives_none()
        {
            Dictionary<string, string> real = FootprintCheck.KeoFiles(JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules())));
            Assert.Equal("TH1", real["TH1-IM"]);
            Assert.Equal("TH1", real["th1-im"]);
            Dictionary<string, string> made = FootprintCheck.KeoFiles(JObject.Parse("{ 'typologies': { 'A1': { 'keo_file': 'B1' }, 'A2': { }, 'A3': { 'keo_file': '' }, 'A4': { 'keo_file': 1 } } }"));
            Assert.Equal(new[] { "A1" }, made.Keys.ToArray());
            Assert.Empty(FootprintCheck.KeoFiles(new JObject()));
            Assert.Empty(FootprintCheck.KeoFiles(null));
        }

        [Fact]
        public void R479_the_run_takes_the_keo_file_of_the_unit_of_the_plan()
        {
            var read = new JObject { ["blocks"] = new JArray(ReadBlock("Villa_TH1", Strip(1100, 2000, 1108, 2015))) };
            string before = Make(read, Plot("101", 1100.63, "TH1-IM")).Body().Preview(Context()).Counts;
            Assert.Contains(", over the footprint limit 0 of 0 of the same type, 1 of another type (Q277), 0 with no outline", before);
            string after = Make(read, new Dictionary<string, string> { ["TH1-IM"] = "TH1" }, Plot("101", 1100.63, "TH1-IM")).Body().Preview(Context()).Counts;
            Assert.Contains(", over the footprint limit 0 of 1 of the same type, 0 of another type (Q277), 0 with no outline", after);
        }

        [Fact]
        public void R479_the_loader_takes_the_keo_files_from_the_rules_file_the_run_of_4_check_built_the_plan_on()
        {
            string runId = "R4-20261102-090000";
            string folder = Path.Combine(work, CheckRun.FolderPrefix + runId);
            Directory.CreateDirectory(folder);
            string planPath = Path.Combine(folder, CheckRun.PlanName("012"));
            File.WriteAllText(planPath, Newtonsoft.Json.JsonConvert.SerializeObject(new PlanFile
            {
                Format = PlanFile.FormatName, FormatVersion = PlanFile.Version, Neighbourhood = "012",
                SurveyPoint = new PlanSurveyPoint { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 1000.0, 2000.0, 0.0 } },
                AngleToTrueNorthDeg = 0, PrototypeFolder = prototypes, Plots = new List<PlanPlot>(),
            }));
            File.WriteAllText(Path.Combine(folder, CheckRun.RecordFile), CheckRun.Record(runId, new Dictionary<string, string> { [planPath] = FileHash.Sha256(planPath) }).ToString());
            string rules = Path.Combine(work, "rules.json");
            File.WriteAllText(rules, new JObject { ["typologies"] = new JObject { ["TH1-IM"] = new JObject { ["keo_file"] = "TH1" } } }.ToString());
            new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = runId, Command = "build", LevelProfile = "parsons2024Rev6", OutputFolder = folder,
                Inputs = new List<WorkerFile> { new WorkerFile { Role = CheckRun.Rules, Path = rules, Sha256 = FileHash.Sha256(rules) } },
            }.Write(folder);
            string config = Path.Combine(work, "project.json");
            JToken addin = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")))["addin"];
            File.WriteAllText(config, new JObject { ["addin"] = new JObject { ["lightMode"] = addin["lightMode"], ["parameterSheet"] = Path.Combine(ThemeGateTests.RepoRoot(), "config", "shared_parameters.xlsx") } }.ToString());

            VillaInputs inputs = VillaLoad.Load(Context(), "012", config, null, "p", null);

            Assert.Equal("TH1", inputs.KeoFiles["TH1-IM"]);
        }

        [Fact]
        public void R477_Q351_the_limit_of_the_project_is_1200_mm()
        {
            JObject root = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            Assert.Equal(1200.0, LightModeConfig.From(root).FootprintLimitMm);
            string note = (string)root["addin"]["lightMode"]["note"];
            Assert.Contains("R477", note);
            Assert.Contains("Q351", note);
        }
    }
}
