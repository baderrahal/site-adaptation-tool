using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R360 and R361. The build steps overrides and parameters of the worker on the made up row of LevelFloorStepTests.cs built in code (R10),
    // the C# side of python/tests/test_override_param_steps.py on the same made up data and overrides, on net48 and net8.0
    public sealed class OverrideParameterStepTests : IDisposable
    {
        private const string Dp2File = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt";
        private const string Vd2File = "RRE-WP-NSP-KEO-ARC-VD2-ZZ-MOD-2200.rvt";
        private const string Th1File = "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-2200.rvt";
        private const string Unwritten = "the plot is not written, it lacks ";
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r360_" + Guid.NewGuid().ToString("N"));

        public OverrideParameterStepTests()
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

        // test_override_param_steps.MADE, one override of each path a build takes, applied or left
        internal static OverrideList Made()
        {
            return new OverrideList(new Dictionary<string, JObject>
            {
                ["2"] = JObject.Parse("{\"entryM\": {\"gapToRuleM\": 0.05}, \"parameter CASE\": \"Z\", \"rotationDeg\": {\"gapToRuleDeg\": -179.999}}"),
                ["3"] = JObject.Parse("{\"heightM\": {\"gapToRuleM\": 0.1}, \"parameter RRE_ZZ_SP_Prototype-FFL\": {\"gapToRuleM\": 0.02}}"),
                ["4"] = JObject.Parse("{\"mirrored\": false, \"rampType\": \"flat\", \"insert\": {\"gapToRuleM\": null}, \"parameter RRE_ZZ_SP_RVT-Name\": \"a made up name\"}"),
                ["5"] = JObject.Parse("{\"insert\": {\"gapToRuleM\": [0.3, -0.2]}, \"steps\": \"4\", \"genericFloor\": \"a made up gap\", \"rampFrontFallM\": {\"gapToRuleM\": 0.1}}"),
                ["6"] = JObject.Parse("{\"rotationDeg\": {\"gapToRuleDeg\": 1.5}, \"rampFrontM\": {\"gapToRuleM\": -0.05}, \"yardOutline\": {\"gapToRuleM\": 0.2}, "
                    + "\"rampOutline\": {\"gapToRuleM\": 0.1}, \"foo\": 1, \"mirrored\": \"yes\"}"),
            });
        }

        internal sealed class Built
        {
            public Dictionary<string, PlotPlan> Plans;
            public Dictionary<string, Dictionary<string, object>> Hands;
            public Dictionary<string, Dictionary<string, object>> Links;
            public LevelBuild Build;
            public JObject Config;
            public JObject Rules;
        }

        // test_level_floor_steps.made_build and test_override_param_steps.row_build: the row of six 12 m faces, the plots of faceless DP2 units with no
        // face, the plots of dry with no road level past their left corner, the step scope the plots and 7, a number with no row
        internal static Built Build(OverrideList list, Action<JObject> change = null, string[] faceless = null, string[] dry = null, Action<JObject> changeRules = null)
        {
            faceless = faceless ?? new string[0];
            dry = dry ?? new string[0];
            JObject rules = LevelFloorStepTests.MadeRules();
            changeRules?.Invoke(rules);
            change?.Invoke((JObject)rules["levelProfiles"]["parsons2024Rev6"]["keyRules"]);
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            List<string> numbers = new[] { "1", "2", "3", "4", "5", "6" }.Concat(faceless).ToList();
            var faces = numbers.Where(number => !faceless.Contains(number)).ToDictionary(number => number,
                number => BuildFace.Of(number, LevelFloorStepTests.Ring((int.Parse(number) - 1) * 12.0, 0.0, int.Parse(number) * 12.0, 25.0)));
            var rows = numbers.ToDictionary(number => number, number => new Dictionary<string, string>
            {
                ["locationPlanNumber"] = number, ["typologyRev9"] = number == "1" ? "VD2" : "DP2", ["footprintMirrored"] = "no", ["zone"] = "Zone 2",
            });
            var roads = new RoadLines(new[] { new[] { new[] { -50.0, -3.0 }, new[] { 100.0, -3.0 } }, new[] { new[] { -3.0, -10.0 }, new[] { -3.0, 40.0 } } });
            var sales = new JObject { ["1"] = new JObject { ["Orientation"] = "WS" } };
            var clusters = new List<SiteCluster> { new SiteCluster { Code = "Duplex_DP2", Plots = new List<string> { "2", "3" }, Source = "footprint" } };
            double[] limits = Fronts.Limits(rules, config);
            double roadEdgeM = (double)config["siteBuild"]["corner"]["roadEdgeM"];
            List<string> scope = numbers.Concat(new[] { "7" }).ToList();
            Dictionary<string, FrontItem> fronts = Fronts.Of(scope, rows, faces, roads, limits, (x, y) => new KeyValuePair<double?, string>(null, null), rules, roadEdgeM, sales);
            HandContext context = Hands.Context(numbers, rows, faces, clusters, fronts.ToDictionary(item => item.Key, item => item.Value.Frame), roads, rules, config);
            JObject key = BuildRules.KeyRules(rules, null, "overrides");
            JObject profile = BuildRules.LevelProfile(rules, null);
            var files = new Dictionary<string, string> { ["DP2"] = Dp2File, ["VD2"] = Vd2File, ["TH1-IM"] = Th1File };
            Dictionary<string, Dictionary<string, object>> hands = Hands.Step(scope, rows, context, key, files, rules, config);
            LevelBuild build = KeyLevels.Of(scope, rows, clusters, context, members => Fronts.Of(members, rows, faces, roads, limits,
                (x, y) => dry.Any(plot => x > 12.0 * (int.Parse(plot) - 1) + 1e-9 && x <= 12.0 * int.Parse(plot) + 1e-9) ? new KeyValuePair<double?, string>(null, null)
                    : new KeyValuePair<double?, string>(10.0 + x / 100.0, "road"), rules, roadEdgeM, sales), rules, key, profile, "parsons2024Rev6");
            return new Built
            {
                Plans = PlanOverrides.Of(scope, rows, build, fronts, context, hands, key, profile, rules, config, files, list, "parsons2024Rev6", 0.5), Hands = hands,
                Links = LinkFrames.Step(scope, rows, fronts, context.Roles, hands, key, files, rules), Build = build, Config = config, Rules = rules,
            };
        }

        private static string Text(object value)
        {
            return value == null ? "null" : value is IEnumerable<string> texts ? string.Join(" ", texts) : value is JToken token ? token.ToString(Newtonsoft.Json.Formatting.None)
                : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static JToken Written(Built built, string number)
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            List<string> numbers = built.Plans.Keys.ToList();
            return StepFile.Build(PlanOverrides.StepName, "csharp", "project", PlanOverrides.Step(numbers, built.Plans), config.StepFields(PlanOverrides.StepName))["plots"][number]["written"];
        }

        [Fact]
        public void R360_parity_overrides_what_the_build_did_with_each_override_applied_or_left_with_why()
        {
            Dictionary<string, PlotPlan> plans = Build(Made()).Plans;
            Assert.Equal("entryM parameter CASE rotationDeg | {}", Text(plans["2"].Overrides["applied"]) + " | " + Text(plans["2"].Overrides["notApplied"]));
            Assert.Equal("heightM parameter RRE_ZZ_SP_Prototype-FFL | {}", Text(plans["3"].Overrides["applied"]) + " | " + Text(plans["3"].Overrides["notApplied"]));
            Assert.Equal("mirrored parameter RRE_ZZ_SP_RVT-Name rampType | {\"insert\":\"the override holds no gap to the rule\"}",
                Text(plans["4"].Overrides["applied"]) + " | " + Text(plans["4"].Overrides["notApplied"]));
            Assert.Equal("insert steps | {\"genericFloor\":\"an outline gap, a build places the whole face\",\"rampFrontFallM\":\"a ramp front fall is not applied (Q057)\"}",
                Text(plans["5"].Overrides["applied"]) + " | " + Text(plans["5"].Overrides["notApplied"]));
            Assert.Equal("rampFrontM rotationDeg | {\"foo\":\"a build of parsons2024Rev6 does not apply the field\",\"mirrored\":\"the override holds no mirror flag\","
                + "\"rampOutline\":\"an outline gap, a build places the ramp outline of R127\",\"yardOutline\":\"an outline gap, a build places the fitted yard outline\"}",
                Text(plans["6"].Overrides["applied"]) + " | " + Text(plans["6"].Overrides["notApplied"]));
            // A plot with no override holds none, and plot 7, outside the build with no row, nothing
            Assert.Equal("null null True", Text(plans["1"].Overrides["applied"]) + " " + Text(plans["1"].Overrides["notApplied"]) + " " + Text(plans["1"].Overrides["written"]));
            Assert.True(plans["7"].Overrides.All(item => item.Key == "written" ? (bool)item.Value == false : item.Value == null));
        }

        [Fact]
        public void R360_the_values_after_the_overrides_are_those_the_plan_writes_by_their_kind()
        {
            Built built = Build(Made());
            Dictionary<string, PlotPlan> plans = built.Plans;
            JToken two = Written(built, "2"), three = Written(built, "3"), four = Written(built, "4"), six = Written(built, "6");
            // By hand as test_override_param_steps.py: 2 El @ Entry moved by 5 cm on the FFL of its piece, its rotation turned past 0
            Assert.Equal(new[] { Rounding.Fixed(10.2076 + 0.05, 4), 10.528, Rounding.Fixed(10.528 - 10.2576, 4) }, new[] { (double)two["entry"], (double)two["ffl"], (double)two["height"] });
            Assert.Equal(0.0 - 179.999, (double)((StepValue)plans["2"].Overrides["rotationDeg"]).Raw);
            Assert.Equal(180.001, (double)two["rotationDeg"]);
            Assert.Equal(new[] { 10.628, 10.553, 0.3004 }, new[] { (double)three["ffl"], (double)three["ssl"], (double)three["height"] });
            // 4 not mirrored takes the link of 2 two faces to its left, 24 m along X, the flat ramp of its override and the yard outline of DP2 on it
            double[] rule = ((double[])built.Links["2"]["insert"]).Select(value => Rounding.Fixed(value, 3)).ToArray();
            Assert.Equal("false", Text(plans["4"].Overrides["mirrored"]));
            Assert.Equal(new[] { Rounding.Fixed(rule[0] + 24.0, 3), rule[1] }, four["insert"].Select(value => (double)value).ToArray());
            Assert.Equal(0.0, (double)four["rotationDeg"]);
            Assert.Equal("Flat Ramps", (string)four["rampType"]);
            double[][] yard = { new[] { 1.0, 2.0 }, new[] { 5.0, 2.0 }, new[] { 5.0, 6.0 }, new[] { 1.0, 6.0 } };
            Assert.Equal(string.Join(" ", yard.Select(point => Rounding.Fixed(rule[0] + 24.0 + point[0], 3) + "," + Rounding.Fixed(rule[1] + point[1], 3))),
                string.Join(" ", four["yardOutline"][0].Select(point => (double)point[0] + "," + (double)point[1])));
            double[] five = ((double[])built.Links["5"]["insert"]).Select(value => Rounding.Fixed(value, 3)).ToArray();
            Assert.Equal(new[] { five[0] + 0.3, five[1] - 0.2 }, (double[])plans["5"].Overrides["insert"]);
            Assert.Equal(4, (int)Written(built, "5")["steps"]);
            Assert.Equal(180.0 + 1.5, (double)((StepValue)plans["6"].Overrides["rotationDeg"]).Raw);
            Assert.Equal(new[] { Rounding.Fixed(10.6 - 0.05, 3), Rounding.Fixed(10.72 - 0.05, 3) }, six["rampZ"].Take(2).Select(value => (double)value).ToArray());
            // The raw values: no gap the raw El @ Entry and the FFL of the piece unrounded, a gap the value as written by the rule plus the gap
            Assert.Equal(built.Build.Entries["5"].Raw, (double)plans["5"].Overrides["entry"]);
            Assert.Equal(built.Build.Entries["2"].Rule + 0.05, (double)plans["2"].Overrides["entry"]);
            Assert.Equal(built.Build.Rows["3"].Ffl + 0.1, (double)plans["3"].Overrides["ffl"]);
            Assert.Equal(built.Build.Rows["5"].Top, (double)plans["5"].Overrides["ffl"]);
            Assert.Equal(10.888 - 0.075, (double)plans["5"].Overrides["ssl"]);
        }

        [Fact]
        public void R360_a_plot_the_plan_does_not_write_leaves_its_overrides_with_the_keys_it_lacks()
        {
            var list = new OverrideList(new Dictionary<string, JObject>
            {
                ["6"] = JObject.Parse("{\"entryM\": {\"gapToRuleM\": 0.1}, \"heightM\": {\"gapToRuleM\": 0.1}, \"steps\": \"2\", \"mirrored\": false, \"insert\": {\"gapToRuleM\": [0.1, 0.0]}, "
                    + "\"parameter CASE\": \"H\", \"parameter RRE_ZZ_SP_Prototype-Foundation-Type\": \"DP2-T1\"}"),
                ["8"] = JObject.Parse("{\"mirrored\": true, \"parameter CASE\": \"H\", \"insert\": {\"gapToRuleM\": [0.1, 0.0]}}"),
            });
            Built built = Build(list, null, new[] { "8" }, new[] { "6" });
            Dictionary<string, object> six = built.Plans["6"].Overrides;
            string lacks = Unwritten + "levels.ffl, parameters.RRE_ZZ_SP_Number-of-Steps, parameters.RRE_ZZ_SP_Prototype-FFL";
            Assert.Equal("False ", Text(six["written"]) + " " + Text(six["applied"]));
            Assert.Equal(new JObject
            {
                ["entryM"] = "no road level at a front corner, so no El @ Entry to move", ["heightM"] = "no FFL, no road level at a front corner", ["insert"] = lacks, ["mirrored"] = lacks,
                ["parameter CASE"] = lacks, ["parameter RRE_ZZ_SP_Prototype-Foundation-Type"] = lacks, ["steps"] = "no FFL, no road level at a front corner",
            }.ToString(), ((JObject)six["notApplied"]).ToString());
            Assert.Equal("false", Text(six["mirrored"]));
            Assert.NotNull(six["insert"]);
            Assert.True(new[] { "entry", "ffl", "rampZ", "yardOutline" }.All(name => six[name] == null));
            string noFace = Unwritten + "transform, levels.ffl, parameters.CASE, parameters.RRE_ZZ_SP_Number-of-Steps, parameters.RRE_ZZ_SP_Prototype-FFL, parameters.RRE_ZZ_SP_Prototype-Foundation-Type";
            Dictionary<string, object> eight = built.Plans["8"].Overrides;
            Assert.Equal(new JObject { ["insert"] = "the face has no front frame", ["mirrored"] = noFace, ["parameter CASE"] = noFace }.ToString(), ((JObject)eight["notApplied"]).ToString());
            Assert.True(new[] { "mirrored", "insert", "rotationDeg" }.All(name => eight[name] == null));
        }

        [Fact]
        public void R360_R386_a_skip_plot_and_an_entry_marked_use_rule_are_left_with_why()
        {
            var list = new OverrideList(new Dictionary<string, JObject> { ["1"] = JObject.Parse("{\"mirrored\": false}"), ["5"] = JObject.Parse("{\"entryM\": {\"gapToRuleM\": 0.1}}") },
                new Dictionary<string, string> { ["1"] = "a made up reason" }, new Dictionary<string, Dictionary<string, string>> { ["5"] = new Dictionary<string, string> { ["heightM"] = PlanOverrides.UseRule } });
            Built built = Build(list);
            Dictionary<string, object> one = built.Plans["1"].Overrides, five = built.Plans["5"].Overrides;
            Assert.Equal("a made up reason False", Text(one["skip"]) + " " + Text(one["written"]));
            Assert.Equal("{\"mirrored\":\"" + Unwritten + "skip\"}", Text(one["notApplied"]));
            Assert.True(new[] { "insert", "entry", "mirrored" }.All(name => one[name] == null));
            Assert.Null(built.Plans["1"].Parameters["parameters"]);
            Assert.Equal("entryM {\"heightM\":\"use rule, the override list marks the entry to follow the rule (R386)\"}", Text(five["applied"]) + " " + Text(five["notApplied"]));
            Assert.Equal(built.Build.Rows["5"].Top, (double)five["ffl"]);
        }

        [Fact]
        public void R360_R361_a_plot_its_hand_rule_gives_no_mirror_flag_holds_its_levels_and_lacks_the_parameters_of_the_hand()
        {
            var list = new OverrideList(new Dictionary<string, JObject> { ["6"] = JObject.Parse("{\"heightM\": {\"gapToRuleM\": 0.1}}") });
            Built built = Build(list, key => ((JObject)key["hand alone"]["byFile"][Dp2File]).Remove("pair right"));
            Dictionary<string, object> six = built.Plans["6"].Overrides;
            Assert.Null(built.Hands["6"]["mirrored"]);
            Assert.Equal("False {\"heightM\":\"" + Unwritten + "mirrored, parameters.CASE, parameters.RRE_ZZ_SP_Prototype-Foundation-Type\"}", Text(six["written"]) + " " + Text(six["notApplied"]));
            Assert.Equal(Rounding.Fixed(built.Build.Rows["6"].Ffl + 0.1, 3), Rounding.Fixed((double)six["ffl"], 3));
            Assert.True(six["mirrored"] == null && six["insert"] == null);
        }

        private static JObject Entry(string plot, string field, JToken value, string key = "imc", string model = "a.rvt")
        {
            return new JObject { ["plot"] = plot, ["field"] = field, ["value"] = value, ["source"] = key == "u11" ? "native" : "parsons", ["model"] = model, ["key"] = key };
        }

        private OverrideList Load(JObject data)
        {
            string path = Path.Combine(folder, "overrides.json");
            File.WriteAllText(path, data.ToString());
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            JObject profile = BuildRules.LevelProfile(LevelFloorStepTests.MadeRules(), null);
            return PlanOverrides.Load(path, config, profile, "parsons2024Rev6", "project.json");
        }

        [Fact]
        public void R360_R386_the_override_list_a_build_loads_with_the_lines_of_python()
        {
            JObject ruled = Entry("6", "heightM", JObject.Parse("{\"gapToRuleM\": 0.2}"));
            ruled["use"] = "rule";
            ruled["byHand"] = true;
            OverrideList found = Load(new JObject
            {
                ["overrides"] = new JArray(Entry("5", "entryM", JObject.Parse("{\"gapToRuleM\": 0.1}")), ruled, Entry("5", "mirrored", true, "u11")),
                ["skip"] = new JArray(new JObject { ["plot"] = "1", ["reason"] = "a made up reason", ["byHand"] = true }),
            });
            Assert.Equal("5:{\"entryM\":{\"gapToRuleM\":0.1}}", string.Join(" ", found.Found.Select(item => item.Key + ":" + item.Value.ToString(Newtonsoft.Json.Formatting.None))));
            Assert.Equal(PlanOverrides.UseRule, found.Ruled["6"]["heightM"]);
            Assert.Equal("1:a made up reason", string.Join(" ", found.Skip.Select(item => item.Key + ":" + item.Value)));
            JObject noHand = Entry("3", "heightM", JObject.Parse("{\"gapToRuleM\": 0.1}"));
            noHand["use"] = "rule";
            string path = Path.Combine(folder, "overrides.json") + ": ";
            var lines = new List<Tuple<JObject, string>>
            {
                Tuple.Create(new JObject { ["overrides"] = new JArray(Entry("2", "entryM", JObject.Parse("{\"gapToRuleM\": 0.1}"), "zz"), noHand) },
                    path + "2 overrides refused: override of plot 2 field entryM names key zz, which no group of majority.groups holds (R330) | override of plot 3 field heightM has "
                    + "use rule with no \"byHand\": true, a use is a hand edit a majority run must keep (R386)"),
                Tuple.Create(new JObject { ["overrides"] = new JArray(Entry("5", "entryM", JObject.Parse("{\"gapToRuleM\": 0.1}"))), ["skip"] = new JArray(new JObject { ["plot"] = "1", ["reason"] = "" }) },
                    path + "1 skip entries refused: {\"plot\": \"1\", \"reason\": \"\"} is not {\"plot\": text, \"reason\": text, \"byHand\": true} (R386)"),
                Tuple.Create(new JObject { ["overrides"] = new JArray(Entry("5", "entryM", JObject.Parse("{\"gapToRuleM\": 0.1}")), Entry("5", "entryM", JObject.Parse("{\"gapToRuleM\": 0.2}"), "imd", "b.rvt")) },
                    "1 plots hold two overrides of one field: plot 5 field entryM of imc a.rvt and imd b.rvt. Narrow the list with --override-model or edit it by hand (R280)"),
                Tuple.Create(new JObject { ["overrides"] = new JArray(Entry("5", "steps", "2.5")) },
                    "1 steps overrides hold no whole number: plot 5 value \"2.5\" of a.rvt. Give the step count as a whole number (R280, R340)"),
                Tuple.Create(new JObject { ["overrides"] = new JArray(Entry("5", "mirrored", true, "u11")) }, "the override list holds no override of parsons among the sources parsons (R280, R330)"),
            };
            foreach (Tuple<JObject, string> line in lines)
            {
                Assert.Equal(line.Item2, Assert.Throws<InvalidDataException>(() => Load(line.Item1)).Message);
            }
            var rows = new Dictionary<string, Dictionary<string, string>> { ["2"] = new Dictionary<string, string>() };
            Assert.Equal("the skip list of the override list: 1 skip plots are no plot of plots.csv, a skip names the plot as plots.csv spells it: \"02\" (R386)",
                Assert.Throws<InvalidDataException>(() => PlanOverrides.SkipPlotsKnown(new Dictionary<string, string> { ["02"] = "a made up reason" }, rows, "the skip list of the override list: ")).Message);
            Assert.Equal(new long?[] { 2, 2, 1, null, null, null }, new[] { "2", " 2.0 ", "true", "2.5", "two", "[2]" }.Select(text => PlanOverrides.WholeNumber(text == "true" ? new JValue(true) : text == "[2]" ? (JToken)new JArray(2) : text)).ToArray());
        }

        [Fact]
        public void R360_the_worker_build_writes_the_step_overrides_from_the_reads_and_the_override_list_and_refuses_a_request_with_no_list()
        {
            using (var maker = new LevelFloorStepTests())
            {
                WorkerRequest request = maker.Request("o1", PlanOverrides.StepName);
                string list = Path.Combine(request.OutputFolder, "overrides.json");
                File.WriteAllText(list, new JObject { ["overrides"] = new JArray(Entry("5", "entryM", JObject.Parse("{\"gapToRuleM\": 0.1}"))) }.ToString());
                request.Inputs.Add(new WorkerFile { Role = "overrides", Path = list, Sha256 = FileHash.Sha256(list) });
                Assert.Equal(0, WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0));
                JObject step = RunStamp.Read(Path.Combine(request.OutputFolder, "steps", "step_overrides_csharp.json"));
                // A flat road at 10 m: El @ Entry 10.0276 by the rule, 10.1276 with the gap of 10 cm, the FFL of the pair 5 and 6 as the rule gives it
                JToken five = step["plots"]["5"]["written"];
                Assert.Equal("entryM True", string.Join(" ", five["applied"].Select(item => (string)item)) + " " + (bool)five["written"]);
                Assert.Equal(new[] { 10.1276, 10.228, 0.1004 }, new[] { "entry", "ffl", "height" }.Select(name => (double)five[name]).ToArray());
                Assert.Equal(6, ((JObject)step["plots"]).Count);

                WorkerRequest none = maker.Request("o2", PlanOverrides.StepName);
                Assert.Equal(2, WorkerRunner.Run(none.Write(none.OutputFolder), WorkerCommands.Default(), () => 9000.0));
                Assert.Contains("overrides", WorkerAnswer.Read(WorkerFiles.AnswerPath(none.OutputFolder, "o2")).Message);
            }
        }

        [Fact]
        public void R360_the_config_names_the_fields_of_the_step_overrides_by_their_kind()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("unit:noGeometry skip:noGeometry written:noGeometry applied:noGeometry notApplied:noGeometry mirrored:noGeometry insert:points3 rotationDeg:angles3 entry:levels4 "
                + "ffl:levels3 ssl:levels3 height:levels4 steps:noGeometry rampZ:levels3 rampType:noGeometry yardZ:levels3 yardOutline:points3",
                string.Join(" ", config.StepFields(PlanOverrides.StepName).Select(field => field.Name + ":" + field.Kind + field.Decimals)));
            Assert.Equal(string.Join(" ", config.StepFields(PlanOverrides.StepName).Select(field => field.Name)), string.Join(" ", Build(Made()).Plans["5"].Overrides.Keys));
        }

        [Fact]
        public void R361_parity_parameters_the_text_of_each_link_parameter_the_plan_writes_with_its_source()
        {
            Built built = Build(Made());
            Dictionary<string, PlotPlan> plans = built.Plans;
            Dictionary<string, Dictionary<string, object>> step = LinkParameters.Step(plans.Keys, plans);
            // Plot 1 with no override as the Python build writes it, the tables first and then the formulas, the link Name of R107 never written
            Assert.Equal("{\"RRE_ZZ_SP_Prototype\":\"VD2\",\"RRE_ZZ_SP_Number-of-Steps\":\"1\",\"RRE_ZZ_SP_Fa\\u00e7ade-Color\":\"XX\",\"RRE_ZZ_SP_Fa\\u00e7ade-Design-Option\":\"XX\","
                + "\"RRE_ZZ_SP_Prototype-Foundation-Type\":\"VD2-T2\",\"RRE_ZZ_SP_Prototype-HVAC-Schedule-Type\":\"SCHEDULES OF VRF INDOOR & OUTDOOR UNITS-DP2\",\"CASE\":\"E\","
                + "\"RRE_ZZ_SP_Plot-Number\":\"1\",\"RRE_ZZ_SP_RVT-Name\":\"1 | VD2 | 1 | XX | XX\",\"RRE_ZZ_SP_Prototype-FFL\":\"10.280\"}", PyJson.Dumps((JObject)step["1"]["parameters"]));
            Assert.True(((JObject)step["1"]["sources"]).Properties().All(item => (string)item.Value == "keyRules"));
            Assert.Equal("True  1 | VD2 | 1 | XX | XX", Text(step["1"]["written"]) + " " + Text(step["1"]["lacks"]) + " " + Text(step["1"]["linkName"]));
            // 2 its CASE by the text of its override, 3 its Prototype-FFL from the FFL moved by 10 cm, down to the cm, then its gap of 2 cm
            Assert.Equal("Z override", (string)((JObject)step["2"]["parameters"])["CASE"] + " " + (string)((JObject)step["2"]["sources"])["CASE"]);
            Assert.Equal("10.640 override", (string)((JObject)step["3"]["parameters"])["RRE_ZZ_SP_Prototype-FFL"] + " " + (string)((JObject)step["3"]["sources"])["RRE_ZZ_SP_Prototype-FFL"]);
            // 4 not mirrored by its override takes the CASE of DP2 not mirrored, its link Name the text of its override
            JObject four = (JObject)step["4"]["parameters"], five = (JObject)step["5"]["parameters"];
            Assert.Equal((string)five["CASE"] + " keyRules", (string)four["CASE"] + " " + (string)((JObject)step["4"]["sources"])["CASE"]);
            Assert.Equal("a made up name override", (string)four["RRE_ZZ_SP_RVT-Name"] + " " + (string)((JObject)step["4"]["sources"])["RRE_ZZ_SP_RVT-Name"]);
            // 5 its steps of the override in the link Name, keyRules.steps of Number-of-Steps holds no 4, so the plot lacks it and is written all the same
            Assert.Equal("5 | DP2 | 4 | XX | XX", (string)five["RRE_ZZ_SP_RVT-Name"]);
            Assert.Null(five["RRE_ZZ_SP_Number-of-Steps"]);
            Assert.Equal("RRE_ZZ_SP_Number-of-Steps True", Text(step["5"]["lacks"]) + " " + Text(step["5"]["written"]));
            Assert.True(step["7"].All(item => item.Key == "written" ? (bool)item.Value == false : item.Value == null));
        }

        // test_override_param_steps.test_r361_r107_q099_...: the link Name of R107 is the text of RRE_ZZ_SP_RVT-Name after its overrides
        [Fact]
        public void R361_R107_Q099_a_build_writes_the_link_name_as_the_rvt_name_parameter_after_its_overrides()
        {
            Dictionary<string, PlotPlan> plans = Build(Made()).Plans;
            Dictionary<string, Dictionary<string, object>> step = LinkParameters.Step(plans.Keys, plans);
            Assert.Equal("1 | VD2 | 1 | XX | XX|a made up name|5 | DP2 | 4 | XX | XX", Text(step["1"]["linkName"]) + "|" + Text(step["4"]["linkName"]) + "|" + Text(step["5"]["linkName"]));
            foreach (string number in new[] { "1", "2", "3", "4", "5", "6" })
            {
                Assert.Equal((string)((JObject)step[number]["parameters"])[LinkFields.RvtName], (string)step[number]["linkName"]);
            }
            Assert.Null(step["7"]["linkName"]);
            // A plot the plan does not write holds the name of its parameter all the same
            plans = Build(new OverrideList(new Dictionary<string, JObject>()), null, new[] { "8" }).Plans;
            step = LinkParameters.Step(plans.Keys, plans);
            Assert.Equal("False 8 | DP2 | None | XX | XX", Text(step["8"]["written"]) + " " + Text(step["8"]["linkName"]));
        }

        [Fact]
        public void R361_R107_Q099_a_rule_that_gives_no_rvt_name_leaves_the_link_name_null()
        {
            Dictionary<string, PlotPlan> plans = Build(new OverrideList(new Dictionary<string, JObject>()), key => key.Remove("parameter " + LinkFields.RvtName)).Plans;
            Dictionary<string, Dictionary<string, object>> step = LinkParameters.Step(plans.Keys, plans);
            Assert.Equal("True null", Text(step["1"]["written"]) + " " + Text(step["1"]["linkName"]));
        }

        // Q098: a cluster unit whose file holds no table of hand cluster takes that of hand alone, so the plan writes plots 2 and 3 with its hand
        [Fact]
        public void R357_R360_Q098_the_plan_writes_a_cluster_unit_whose_file_has_no_table_of_hand_cluster_by_the_table_of_hand_alone()
        {
            Dictionary<string, PlotPlan> plans = Build(new OverrideList(new Dictionary<string, JObject>()), key =>
            {
                ((JObject)key["hand cluster"]["byFile"]).Remove(Dp2File);
                key["hand alone"]["byFile"][Dp2File]["alone no none"] = true;
            }).Plans;
            Assert.Equal("True true|True true", string.Join("|", new[] { "2", "3" }.Select(number => Text(plans[number].Overrides["written"]) + " " + Text(plans[number].Overrides["mirrored"]))));
        }

        [Fact]
        public void R361_a_plot_the_plan_does_not_write_holds_the_parameters_the_build_gave_it_with_the_names_it_lacks()
        {
            var list = new OverrideList(new Dictionary<string, JObject> { ["6"] = JObject.Parse("{\"mirrored\": false, \"parameter CASE\": \"H\"}") });
            Dictionary<string, PlotPlan> plans = Build(list, null, new[] { "8" }, new[] { "6" }).Plans;
            Dictionary<string, Dictionary<string, object>> step = LinkParameters.Step(plans.Keys, plans);
            Assert.Equal("False H override RRE_ZZ_SP_Number-of-Steps RRE_ZZ_SP_Prototype-FFL", Text(step["6"]["written"]) + " " + (string)((JObject)step["6"]["parameters"])["CASE"] + " "
                + (string)((JObject)step["6"]["sources"])["CASE"] + " " + Text(step["6"]["lacks"]));
            Assert.Equal("CASE RRE_ZZ_SP_Number-of-Steps RRE_ZZ_SP_Prototype-FFL RRE_ZZ_SP_Prototype-Foundation-Type", Text(step["8"]["lacks"]));
            Assert.Equal("8 | DP2 | None | XX | XX", (string)((JObject)step["8"]["parameters"])["RRE_ZZ_SP_RVT-Name"]);
        }

        [Fact]
        public void R361_the_worker_build_writes_the_step_parameters_and_the_config_names_its_fields()
        {
            using (var maker = new LevelFloorStepTests())
            {
                WorkerRequest request = maker.Request("p1", LinkParameters.StepName, PlanOverrides.StepName);
                string list = Path.Combine(request.OutputFolder, "overrides.json");
                File.WriteAllText(list, new JObject { ["overrides"] = new JArray(Entry("5", "parameter CASE", "H")) }.ToString());
                request.Inputs.Add(new WorkerFile { Role = "overrides", Path = list, Sha256 = FileHash.Sha256(list) });
                Assert.Equal(0, WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0));
                JObject step = RunStamp.Read(Path.Combine(request.OutputFolder, "steps", "step_parameters_csharp.json"));
                // A flat road at 10 m: one step and the FFL 10.228 of the pair 5 and 6, down to the cm in the Prototype-FFL
                JToken five = step["plots"]["5"]["written"];
                Assert.Equal("5 | DP2 | 1 | XX | XX 10.220 H override", (string)five["parameters"]["RRE_ZZ_SP_RVT-Name"] + " " + (string)five["parameters"]["RRE_ZZ_SP_Prototype-FFL"] + " "
                    + (string)five["parameters"]["CASE"] + " " + (string)five["sources"]["CASE"]);
                Assert.True(File.Exists(Path.Combine(request.OutputFolder, "steps", "step_overrides_csharp.json")));
            }
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("unit:noGeometry written:noGeometry parameters:noGeometry sources:noGeometry lacks:noGeometry linkName:noGeometry",
                string.Join(" ", config.StepFields(LinkParameters.StepName).Select(field => field.Name + ":" + field.Kind + field.Decimals)));
            Assert.Equal("clusters fronts hands linkFrame levels floors overrides parameters status", string.Join(" ", BuildSteps.Names));
        }
    }
}
