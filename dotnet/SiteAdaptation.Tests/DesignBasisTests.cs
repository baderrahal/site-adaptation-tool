using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R407 to R410. The design basis of round 4i on the made up row of LevelFloorStepTests built in code (R10), the C# side of
    // python/tests/test_r407_design_basis.py, test_r408_door_level.py, test_r409_road_surface.py and test_r410_back_yard.py on the same made up
    // data and the same values by hand, on net48 and net8.0
    public sealed class DesignBasisTests : IDisposable
    {
        private const double Depth = 5.52;
        private const double Road5 = (10.48 + 10.6) / 2.0;
        private const double Road6 = (10.6 + 10.72) / 2.0;
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r407_" + Guid.NewGuid().ToString("N"));

        public DesignBasisTests()
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

        private static JObject Config()
        {
            return JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
        }

        // The mode of the past job as config/project.json names it, read from config so no project name stands in code
        private static string PastJob()
        {
            return ((JArray)Config()["designBasis"]["modes"]).Select(mode => (string)mode).Single(mode => mode != DesignBasis.Rev6);
        }

        // config/project.json with designBasis changed as change does
        private static JObject Settings(Action<JObject> change)
        {
            JObject config = Config();
            change((JObject)config["designBasis"]);
            return config;
        }

        private static JArray Bands()
        {
            return DesignBasis.Bands(JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "floor_outlines.json"))));
        }

        private string Civil(string header, params string[] rows)
        {
            string path = Path.Combine(folder, "civil.csv");
            File.WriteAllText(path, header + "\n" + string.Join("\n", rows) + "\n");
            return path;
        }

        private static DesignBasis Rev6(Action<JObject> change = null, string civil = null)
        {
            return DesignBasis.Of(Settings(block =>
            {
                block["mode"] = DesignBasis.Rev6;
                change?.Invoke(block);
            }), () => civil);
        }

        // designBasis.doorLevel of the source civilFile on the made up file path
        private static Action<JObject> CivilFile(string path)
        {
            return block =>
            {
                block["doorLevel"]["source"] = "civilFile";
                block["doorLevel"]["civilFile"]["path"] = path;
            };
        }

        private static string Refusal(Action action)
        {
            return Assert.Throws<InvalidDataException>(action).Message;
        }

        [Fact]
        public void R407_a_mode_outside_design_basis_modes_is_refused_by_its_name()
        {
            Assert.Equal("designBasis.mode manual7 is no mode of designBasis.modes rev6, " + PastJob() + " (R407)", Refusal(() => DesignBasis.Of(Settings(block => block["mode"] = "manual7"), () => null)));
            Assert.StartsWith("designBasis.mode none is no mode", Refusal(() => DesignBasis.Of(Settings(block => block["mode"] = null), () => null)));
            Assert.Equal("designBasis.modes must hold rev6 and the mode of the past job, it holds " + PastJob() + ", other (R407)",
                Refusal(() => DesignBasis.Of(Settings(block => block["modes"] = new JArray(PastJob(), "other")), () => null)));
        }

        [Fact]
        public void R407_the_past_job_reads_none_of_door_level_road_level_and_back_yard()
        {
            DesignBasis basis = DesignBasis.Of(Settings(block =>
            {
                block["mode"] = PastJob();
                block["doorLevel"] = "nothing";
                block["roadLevel"]["surface"] = null;
                block["backYard"]["floor"] = true;
            }), () => throw new InvalidOperationException("the past job asks no civil file"));
            Assert.Equal(PastJob(), basis.Mode);
            Assert.False(basis.IsRev6);
            Assert.Null(basis.DoorSource);
            Assert.Equal(PastJob(), DesignBasis.ModeOf(Config()));
        }

        [Fact]
        public void R407_the_switch_back_to_the_past_job_gives_the_values_of_before_round_4i_with_the_new_fields_null()
        {
            LevelFloorStepTests.Made past = LevelFloorStepTests.MadeBuild(basis: DesignBasis.Of(Config(), () => null));
            LevelFloorStepTests.Made plain = LevelFloorStepTests.MadeBuild();
            Assert.Equal(Road5 + 0.005 * Depth, (double)past.Levels["5"]["entry"], 12);
            Assert.Null(past.Levels["5"]["doorSource"]);
            Assert.Null(past.Levels["5"]["slopePercent"]);
            Assert.Null(past.Floors["5"]["yardGrade"]);
            Assert.Equal("\"0.5\" \"Ramps 0.5%\" 0.1", ((JToken)past.Floors["5"]["rampClass"]).ToString(Newtonsoft.Json.Formatting.None) + " "
                + ((JToken)past.Floors["5"]["rampType"]).ToString(Newtonsoft.Json.Formatting.None) + " " + past.Floors["5"]["yardDepth"]);
            Assert.Null(past.Build.NoDoor);
            Assert.Equal(Text(plain.Levels), Text(past.Levels));
            Assert.Equal(Text(plain.Floors), Text(past.Floors));
        }

        private static string Text(Dictionary<string, Dictionary<string, object>> step)
        {
            return string.Join("|", step.Select(plot => plot.Key + ":" + string.Join(",", plot.Value.Select(field => field.Key + "=" + Newtonsoft.Json.JsonConvert.SerializeObject(field.Value)))));
        }

        [Fact]
        public void R407_the_run_block_of_a_build_step_names_the_design_basis()
        {
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest request = steps.Request("d1", "levels");
                Assert.Equal(0, WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0));
                JObject levels = JObject.Parse(File.ReadAllText(Path.Combine(request.StepsFolder, "step_levels_csharp.json")));
                Assert.Equal(PastJob(), (string)levels[RunStamp.Key]["designBasis"]);
                Assert.Equal("designBasis", ((JObject)levels[RunStamp.Key]).Properties().Last().Name);
            }
        }

        [Fact]
        public void R408_civil_file_el_at_entry_is_the_level_of_the_file_and_the_slope_the_result()
        {
            string path = Civil("Plot No.,El @ Entry", "1,10.2", "2,10.3", "3,10.35", "5,10.6", "6,10.9");
            DesignBasis basis = Rev6(CivilFile(path), path);
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(basis: basis, bands: Bands());
            Assert.Equal("civilFile", (string)made.Levels["5"]["doorSource"]);
            Assert.Equal(10.6, (double)made.Levels["5"]["entry"]);
            Assert.Equal((10.6 - Road5) / Depth * 100.0, (double)made.Levels["5"]["slopePercent"]);
            Assert.Null(made.Floors["5"]["rampClass"]);
            Assert.Equal("Ramps 0.5%", (string)(JToken)made.Floors["5"]["rampType"]);
            Assert.Equal("Ramps 2%", (string)(JToken)made.Floors["6"]["rampType"]);
            Assert.Equal(10.9 + 0.2, (double)made.Levels["5"]["ffl"]);
            Assert.Equal(10.9 + 0.2, (double)made.Levels["6"]["ffl"]);
        }

        [Fact]
        public void R408_a_plot_the_civil_file_does_not_hold_gets_no_el_at_entry_and_is_listed_with_why()
        {
            string path = Civil("Plot No.,El @ Entry", "1,10.2", "2,10.3", "3,10.35", "5,10.6", "6,10.7");
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(basis: Rev6(CivilFile(path), path), bands: Bands());
            Assert.Equal("civilFile", (string)made.Levels["4"]["doorSource"]);
            Assert.Null(made.Levels["4"]["entry"]);
            Assert.Null(made.Levels["4"]["slopePercent"]);
            Assert.Null(made.Levels["4"]["ffl"]);
            Assert.Null(made.Floors["4"]["rampType"]);
            Assert.Equal("[{\"plot\":\"4\",\"reason\":\"the civil file civil.csv holds no row of plot 4, so no El @ Entry (R408)\"}]", made.Build.NoDoor.ToString(Newtonsoft.Json.Formatting.None));
        }

        [Fact]
        public void R408_road_plus_slope_el_at_entry_is_the_road_at_front_plus_the_slope_of_the_unit_over_the_ramp_depth()
        {
            DesignBasis basis = Rev6(block => block["doorLevel"]["slopePercent"] = new JObject { ["default"] = 0.5, ["VL4"] = 2.0 });
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(units: new Dictionary<string, string> { ["6"] = "VL4" }, basis: basis, bands: Bands());
            Assert.Equal("roadPlusSlope", (string)made.Levels["5"]["doorSource"]);
            Assert.Equal(Road5 + 0.5 / 100.0 * Depth, (double)made.Levels["5"]["entry"]);
            Assert.Equal(0.5, (double)made.Levels["5"]["slopePercent"], 12);
            Assert.Equal("Ramps 0.5%", (string)(JToken)made.Floors["5"]["rampType"]);
            Assert.Equal(Road6 + 2.0 / 100.0 * Depth, (double)made.Levels["6"]["entry"]);
            Assert.Equal("Ramps 2%", (string)(JToken)made.Floors["6"]["rampType"]);
            Assert.Empty(made.Build.NoDoor);
            Assert.Empty(RuleOutside(made));
        }

        [Fact]
        public void R408_a_slope_outside_front_min_to_front_max_is_listed_as_outside_the_manual()
        {
            double road2 = (10.12 + 10.24) / 2.0, road3 = (10.24 + 10.36) / 2.0;
            Func<double, string> text = value => Math.Round(value, 6).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            string path = Civil("Plot No.,El @ Entry", "1,10.2", "2," + text(road2 + 0.01 * Depth), "3," + text(road3 + 0.01 * Depth), "5," + text(Road5 + 0.003 * Depth), "6,10.9");
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(basis: Rev6(CivilFile(path), path), bands: Bands());
            List<JObject> listed = RuleOutside(made).OfType<JObject>().Where(item => (string)item["plot"] != "1").ToList();
            Assert.Equal("5 6", string.Join(" ", listed.Select(item => (string)item["plot"])));
            Assert.Equal(0.3, (double)listed[0]["slopePercent"]);
            Assert.Equal(Rounding.Fixed((10.9 - Road6) / Depth * 100.0, 3), (double)listed[1]["slopePercent"]);
            JObject limits = new JObject { ["front_min"] = 0.5, ["front_max"] = 2.0 };
            Assert.False(DesignBasis.OutsideManual(0.49999999999, limits));
            Assert.False(DesignBasis.OutsideManual(2.0004, limits));
            Assert.True(DesignBasis.OutsideManual(2.0051, limits));
        }

        // The list of a build with no override, whose slopes after the overrides are those of the rule
        private static JArray RuleOutside(LevelFloorStepTests.Made made)
        {
            return DesignBasis.OutsideList(made.Levels.Keys, made.Build.Entries.ToDictionary(item => item.Key, item => item.Value.Slope),
                (JObject)LevelFloorStepTests.MadeRules()["slopes_percent"]);
        }

        // test_r408_door_level.slope_build: the made up row under rev6 with roadPlusSlope at 1.5 percent, in the band Ramps 2% and inside the manual
        private static OverrideParameterStepTests.Built SlopeBuild(string overrides)
        {
            JObject parsed = JObject.Parse(overrides);
            var list = new OverrideList(parsed.Properties().ToDictionary(item => item.Name, item => (JObject)item.Value));
            DesignBasis basis = Rev6(block => block["doorLevel"]["slopePercent"] = new JObject { ["default"] = 1.5 });
            return OverrideParameterStepTests.Build(list, basis: basis, bands: Bands());
        }

        private static JArray Outside(OverrideParameterStepTests.Built built)
        {
            return PlanOverrides.Outside(built.Plans.Keys.ToList(), built.Build, built.Plans, built.Rules);
        }

        // El @ Entry of the rule at 4 decimals moved by its gap, less the road at front moved by its gap, over the ramp depth
        private static double FinalSlope(double road, double entryGap, double frontGap)
        {
            double rule = Rounding.Fixed(road + 1.5 / 100.0 * Depth, 4);
            return (rule + entryGap - (road + frontGap)) / Depth * 100.0;
        }

        [Fact]
        public void R408_an_entry_override_gives_the_slope_ramp_type_and_outside_list_of_the_moved_el_at_entry()
        {
            OverrideParameterStepTests.Built built = SlopeBuild("{\"5\": {\"entryM\": {\"gapToRuleM\": -0.06}}}");
            Dictionary<string, object> five = built.Plans["5"].Overrides;
            Assert.Equal("Ramps 0.5%", (string)(JToken)five["rampType"]);
            Assert.Equal(FinalSlope(Road5, -0.06, 0.0), (double)five["slopePercent"], 9);
            Assert.Equal(0.413, Rounding.Fixed((double)five["slopePercent"], 3));
            Assert.Equal("[{\"plot\":\"5\",\"slopePercent\":0.413}]", Outside(built).ToString(Newtonsoft.Json.Formatting.None));
            // The step levels keeps the slope by the rule, the step before the overrides
            Assert.Equal(1.5, built.Build.Entries["5"].Slope, 12);
        }

        [Fact]
        public void R408_a_ramp_front_override_gives_the_slope_ramp_type_and_outside_list_of_the_moved_front()
        {
            OverrideParameterStepTests.Built built = SlopeBuild("{\"6\": {\"rampFrontM\": {\"gapToRuleM\": 0.07}}}");
            Dictionary<string, object> six = built.Plans["6"].Overrides;
            Assert.Equal("Ramps 0.5%", (string)(JToken)six["rampType"]);
            Assert.Equal(FinalSlope(Road6, 0.0, 0.07), (double)six["slopePercent"], 9);
            Assert.Equal(0.232, Rounding.Fixed((double)six["slopePercent"], 3));
            Assert.Equal("[{\"plot\":\"6\",\"slopePercent\":0.232}]", Outside(built).ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal(1.5, built.Build.Entries["6"].Slope, 12);
        }

        [Fact]
        public void R408_a_plot_with_no_override_keeps_the_slope_of_the_rule()
        {
            OverrideParameterStepTests.Built built = SlopeBuild("{}");
            foreach (string number in new[] { "4", "5", "6" })
            {
                Assert.Equal("Ramps 2%", (string)(JToken)built.Plans[number].Overrides["rampType"]);
                Assert.Equal(built.Build.Entries[number].Slope, (double)built.Plans[number].Overrides["slopePercent"]);
            }
            Assert.Empty(Outside(built));
            // The step overrides of a build of the past job carries no slope and no list
            OverrideParameterStepTests.Built past = OverrideParameterStepTests.Build(new OverrideList(new Dictionary<string, JObject>()));
            Assert.All(past.Plans.Values, plan => Assert.Null(plan.Overrides["slopePercent"]));
            Assert.Null(Outside(past));
        }

        [Theory]
        [InlineData(null, "Plot No.,El @ Entry", "designBasis.doorLevel.civilFile.path {path} is no file, the civil file of the door levels is missing (R408)")]
        [InlineData("5,10.6", "Plot,El @ Entry", "the civil file civil.csv holds no column Plot No., designBasis.doorLevel.civilFile.plotColumn (R408)")]
        [InlineData("5,10.6", "Plot No.,Level", "the civil file civil.csv holds no column El @ Entry, designBasis.doorLevel.civilFile.levelColumn (R408)")]
        [InlineData("5,nan", "Plot No.,El @ Entry", "the civil file civil.csv gives plot 5 the level 'nan', which is no finite number (R408)")]
        [InlineData("5,", "Plot No.,El @ Entry", "the civil file civil.csv gives plot 5 the level '', which is no finite number (R408)")]
        [InlineData("5,1_0.6", "Plot No.,El @ Entry", "the civil file civil.csv gives plot 5 the level '1_0.6', which is no finite number (R408)")]
        [InlineData("5,1e999", "Plot No.,El @ Entry", "the civil file civil.csv gives plot 5 the level '1e999', which is no finite number (R408)")]
        [InlineData("5,10.6|5,10.7", "Plot No.,El @ Entry", "the civil file civil.csv holds plot 5 twice (R408)")]
        public void R408_a_civil_file_missing_without_its_column_with_a_level_no_finite_number_or_a_plot_twice_is_refused(string rows, string header, string why)
        {
            string path = rows == null ? Path.Combine(folder, "missing.csv") : Civil(header, rows.Split('|'));
            Assert.Equal(why.Replace("{path}", path), Refusal(() => Rev6(CivilFile(path), path)));
        }

        [Fact]
        public void R408_a_civil_file_path_null_and_a_source_outside_sources_are_refused()
        {
            Assert.Equal("designBasis.doorLevel.civilFile.path none is no file, the civil file of the door levels is missing (R408)",
                Refusal(() => Rev6(block => block["doorLevel"]["source"] = "civilFile", Civil("Plot No.,El @ Entry", "5,10.6"))));
            Assert.Equal("designBasis.doorLevel.source survey is no source of designBasis.doorLevel.sources, El @ Entry comes from civilFile or roadPlusSlope (R408)",
                Refusal(() => Rev6(block => block["doorLevel"]["source"] = "survey")));
            Assert.StartsWith("designBasis.doorLevel.slopePercent must hold a finite number for default", Refusal(() => Rev6(block => block["doorLevel"]["slopePercent"] = new JObject { ["VL4"] = 2.0 })));
        }

        // The worker request of LevelFloorStepTests on the design basis rev6 by change, the road surfaces of the request the first of config alone
        private static WorkerRequest Rev6Request(LevelFloorStepTests steps, string runId, Action<JObject, List<WorkerFile>> change, params string[] names)
        {
            WorkerRequest request = steps.Request(runId, names);
            WorkerFile settings = request.Inputs.Single(input => input.Role == "config");
            JObject config = JObject.Parse(File.ReadAllText(settings.Path));
            string surface = (string)config["siteBuild"]["roadSurfaces"][0];
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["designBasis"]["roadLevel"]["surface"] = surface;
            change?.Invoke(config, request.Inputs);
            File.WriteAllText(settings.Path, config.ToString());
            settings.Sha256 = FileHash.Sha256(settings.Path);
            request.Inputs.RemoveAll(input => input.Role.StartsWith(WorkerRunner.RoadSurfaceRole, StringComparison.Ordinal) && input.Role != WorkerRunner.RoadSurfaceRole + surface);
            string floors = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "floor_outlines.json");
            request.Inputs.Add(new WorkerFile { Role = BuildSteps.BandsRole, Path = floors, Sha256 = FileHash.Sha256(floors) });
            return request;
        }

        [Fact]
        public void R408_R409_R410_the_worker_build_under_rev6_takes_the_one_surface_the_door_level_and_the_yard_grade()
        {
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest request = Rev6Request(steps, "d2", null, "levels", "floors");
                Assert.Equal(0, WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0));
                JObject levels = JObject.Parse(File.ReadAllText(Path.Combine(request.StepsFolder, "step_levels_csharp.json")));
                JObject floors = JObject.Parse(File.ReadAllText(Path.Combine(request.StepsFolder, "step_floors_csharp.json")));
                Assert.Equal("rev6", (string)levels[RunStamp.Key]["designBasis"]);
                // A flat road at 10 m of the one surface: El @ Entry 10 plus 0.5 percent of the 5.52 m ramp, the slope 0.5 percent
                JToken five = levels["plots"]["5"]["raw"];
                Assert.Equal("roadPlusSlope", (string)five["doorSource"]);
                Assert.Equal(10.0 + 0.005 * Depth, (double)five["entry"], 12);
                Assert.Equal(0.5, (double)five["slopePercent"], 9);
                Assert.Equal("{\"toward\":\"front\",\"minPercent\":0.3,\"maxPercent\":0.5}", floors["plots"]["5"]["raw"]["yardGrade"].ToString(Newtonsoft.Json.Formatting.None));
                Assert.Equal("Ramps 0.5%", (string)floors["plots"]["5"]["raw"]["rampType"]);
                Assert.Equal(JTokenType.Null, floors["plots"]["5"]["raw"]["yardOutline"].Type);
                // A civil file the request does not name is refused
                WorkerRequest civil = Rev6Request(steps, "d3", (config, inputs) =>
                {
                    config["designBasis"]["doorLevel"]["source"] = "civilFile";
                    config["designBasis"]["doorLevel"]["civilFile"]["path"] = "civil.csv";
                }, "levels");
                Assert.Equal(WorkerExit.Refused, WorkerRunner.Run(civil.Write(civil.OutputFolder), WorkerCommands.Default(), () => 9000.0));
            }
        }

        [Fact]
        public void R409_the_road_surfaces_of_a_build_under_rev6_are_the_one_surface_of_design_basis_road_level()
        {
            JObject rules = JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules()));
            JObject profile = BuildRules.LevelProfile(rules, null);
            ReadSettings read = ReadSettings.Load(WorkerRunTests.RepoConfig());
            DesignBasis basis = Rev6(block => block["roadLevel"]["surface"] = "Made Up Road Surface");
            Assert.Equal(new[] { "Made Up Road Surface" }, Fronts.RoadNames(profile, read, basis));
            Assert.Equal(Fronts.RoadNames(profile, read), Fronts.RoadNames(profile, read, DesignBasis.Of(Config(), () => null)));
            Assert.Equal(((JArray)Config()["siteBuild"]["roadSurfaces"]).Select(item => (string)item), Fronts.RoadNames(profile, read));
        }

        [Fact]
        public void R409_no_surface_name_is_refused_under_rev6_and_a_name_the_request_does_not_hold_is_refused()
        {
            Assert.Equal("designBasis.roadLevel.surface names no road surface, a build under rev6 takes the road level from the one surface it names (R409)",
                Refusal(() => Rev6(block => block["roadLevel"]["surface"] = "")));
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest request = Rev6Request(steps, "d4", (config, inputs) => config["designBasis"]["roadLevel"]["surface"] = "Made Up Road Surface", "levels");
                Assert.Equal(WorkerExit.Refused, WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0));
            }
        }

        [Fact]
        public void R409_old_job_offset_m_is_added_to_no_level()
        {
            LevelFloorStepTests.Made plain = LevelFloorStepTests.MadeBuild(basis: Rev6(), bands: Bands());
            LevelFloorStepTests.Made moved = LevelFloorStepTests.MadeBuild(basis: Rev6(block => block["roadLevel"]["oldJobOffsetM"] = 5.0), bands: Bands());
            Assert.Equal(Text(plain.Levels), Text(moved.Levels));
            Assert.Equal(Road5, (double)moved.Levels["5"]["roadAtFront"]);
        }

        [Fact]
        public void R410_the_step_floors_gives_each_unit_plot_its_yard_grade_by_the_manual_and_no_yard_floor()
        {
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(units: new Dictionary<string, string> { ["5"] = "TH1-IM" }, basis: Rev6(), bands: Bands());
            Assert.Equal("{\"toward\":\"front\",\"minPercent\":0.3,\"maxPercent\":0.5}", ((JObject)made.Floors["4"]["yardGrade"]).ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal("{\"toward\":\"back\",\"minPercent\":2.0,\"maxPercent\":2.0}", ((JObject)made.Floors["5"]["yardGrade"]).ToString(Newtonsoft.Json.Formatting.None));
            Assert.Null(made.Floors["7"]["yardGrade"]);
            Assert.All(made.Floors.Values, floors => Assert.All(new[] { "yardDepth", "yardUnit", "yardWidth", "yardType", "yardOutline" }, name => Assert.Null(floors[name])));
        }

        [Fact]
        public void R410_back_yard_floor_true_under_rev6_is_refused_by_name_naming_q225()
        {
            Assert.Equal("designBasis.backYard.floor is true, the manual gives no level for a yard floor under rev6, so none is placed until Q225 is answered (R410)",
                Refusal(() => Rev6(block => block["backYard"]["floor"] = true)));
            Assert.Equal("designBasis.backYard.floor must be true or false, it holds 'no' (R410)", Refusal(() => Rev6(block => block["backYard"]["floor"] = "no")));
        }

        [Fact]
        public void R410_the_past_job_keeps_the_flat_yard_floor_of_key_rules_yard()
        {
            DesignBasis past = DesignBasis.Of(Settings(block => block["backYard"]["floor"] = true), () => null);
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(basis: past);
            Assert.Equal(0.1, (double)made.Floors["5"]["yardDepth"]);
            Assert.Null(made.Floors["5"]["yardGrade"]);
        }
    }
}
