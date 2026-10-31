using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R365, Q116. The plan summary of step 15 and the conflicts of the plan, PlanSummary of Common, the C# side of python/tests/test_plan_summary.py
    // on the made up row of OverrideParameterStepTests with its overrides, on net48 and net8.0
    public sealed class PlanSummaryTests
    {
        private static readonly string[] Numbers = { "1", "2", "3", "4", "5", "6" };

        // test_plan_summary.summary_of: the step summary of the made row built with the overrides of OverrideParameterStepTests.Made
        private static Tuple<Dictionary<string, Dictionary<string, object>>, JObject> Made(out Dictionary<string, Dictionary<string, object>> status,
            out ValueStatusTable table)
        {
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made());
            table = ValueStatus.Table(built.Config, built.Rules, "parsons2024Rev6");
            status = ValueStatus.Step(Numbers, built.Plans, built.Hands, table);
            JObject key = BuildRules.KeyRules(built.Rules, null, PlanSummary.StepName);
            JObject profile = BuildRules.LevelProfile(built.Rules, null);
            return PlanSummary.Step(Numbers, built.Plans, built.Build.Fronts, built.Hands, status, table, built.Config, key, "parsons2024Rev6",
                KeyLevels.LaterParameters(built.Config, profile), built.Faces);
        }

        private static string Text(JToken token)
        {
            return token.ToString(Formatting.None);
        }

        // test_plan_summary.test_r365_r401_parity_summary_the_step_holds_the_counts_and_names_of_the_plan_summary_and_the_conflicts, the values
        // Python gives the made row
        [Fact]
        public void R365_parity_summary_the_step_holds_the_counts_and_names_of_the_plan_summary_and_the_conflicts()
        {
            Tuple<Dictionary<string, Dictionary<string, object>>, JObject> made = Made(out Dictionary<string, Dictionary<string, object>> status, out ValueStatusTable table);
            var summary = (JObject)made.Item2["summary"];
            Assert.Equal("frontRule roadSurfacesOfFrontCorners floorsFound floorTypes handRules overridesNotApplied steps statusByField assumedByField statusPlots conflicts "
                + "noDoorLevel slopeOutsideManual", string.Join(" ", summary.Properties().Select(item => item.Name)));
            // R407. The lists of rev6 are null on a build of the past job
            Assert.Equal(JTokenType.Null, summary["noDoorLevel"].Type);
            Assert.Equal(JTokenType.Null, summary["slopeOutsideManual"].Type);
            Assert.Equal("{\"nearestCurb\":5,\"orientation\":1}", Text(summary["frontRule"]));
            Assert.Equal("{\"road\":12}", Text(summary["roadSurfacesOfFrontCorners"]));
            Assert.Equal("{\"generic\":6,\"yard\":3,\"ramp\":6}", Text(summary["floorsFound"]));
            Assert.Equal("{\"generic: Generic 150mm\":6,\"ramp: Flat Ramps\":1,\"ramp: Ramps 0.5%\":5,\"yard: Yard made up\":3}", Text(summary["floorTypes"]));
            Assert.Equal("{\"blockMirrorRoadSide\":1,\"clusterSide\":2,\"override\":1,\"rowPairSideOrBlockMirrorOpenEnd\":2}", Text(summary["handRules"]));
            Assert.Equal("{\"foo\":1,\"genericFloor\":1,\"insert\":1,\"mirrored\":1,\"rampFrontFallM\":1,\"rampOutline\":1,\"yardOutline\":1}", Text(summary["overridesNotApplied"]));
            Assert.Equal("{\"from\":\"keyRules.steps stepsFromHeight of parsons2024Rev6\",\"parameters\":[\"RRE_ZZ_SP_Number-of-Steps\",\"RRE_ZZ_SP_RVT-Name\"]}", Text(summary["steps"]));
            // The status of R401 by the counts the worker writes, one source, 242 values in 44 fields, 28 of them with an assumed value, since Q120
            // the yard level and plotValue of 4, whose mirror flag override touches only the outline and type of its yard, no longer assumed
            Assert.Equal(string.Join(" ", ValueStatus.Counts(status, table).Select(item => item.Key.Item1 + ":" + item.Key.Item2 + "=" + item.Value)),
                string.Join(" ", ((JObject)summary["statusByField"]).Properties().SelectMany(field => ((JObject)field.Value).Properties().Select(item => field.Name + ":" + item.Name + "=" + (int)item.Value))));
            Assert.Equal(242, ((JObject)summary["statusByField"]).Properties().Sum(field => ((JObject)field.Value).Properties().Sum(item => (int)item.Value)));
            Assert.Equal(44, ((JObject)summary["statusByField"]).Count);
            Assert.Equal(28, ((JObject)summary["assumedByField"]).Count);
            Assert.Null(summary["assumedByField"]["floors.yard.level"]);
            Assert.Null(summary["assumedByField"]["floors.yard.plotValue"]);
            Assert.Equal("[\"2\",\"4\",\"5\"] [\"4\"] [\"1\",\"2\",\"3\",\"4\",\"6\"] [\"2\",\"4\",\"6\"]", string.Join(" ", new[] { "floors.yard.outline", "mirrored",
                "parameters.RRE_ZZ_SP_Number-of-Steps", "rotationDeg" }.Select(field => Text(summary["assumedByField"][field]))));
            Assert.Equal("{\"CHOSEN by Bader\":6,\"DOCUMENT\":6,\"MEASURED strong\":6,\"MEASURED weak\":6,\"RECOMMENDED\":5,\"UNKNOWN\":6,\"assumed\":6}", Text(summary["statusPlots"]));
            Assert.Equal("[{\"plot\":\"2\",\"field\":\"floors.ramp\",\"kept\":\"" + PlanSummary.TurnedKept + "\",\"other\":\"" + PlanSummary.TurnedOther
                + "\",\"otherSource\":\"override rotationDeg\",\"question\":\"Q116\"}]", Text(summary["conflicts"]));
            Assert.Equal("DP2 True", (string)made.Item1["2"]["unit"] + " " + made.Item1["2"]["written"]);
            Assert.Equal(6, made.Item1.Count);
        }

        // test_plan_summary.test_r362_q116_a_plot_a_rotation_override_turned_to_another_open_edge_is_listed_in_the_conflicts_with_its_ramp_on_the_front
        [Fact]
        public void R362_Q116_a_plot_a_rotation_override_turned_to_another_open_edge_is_listed_in_the_conflicts_with_its_ramp_on_the_front()
        {
            Assert.Equal("the ramp on the front of R355", PlanSummary.TurnedKept);
            Assert.Equal("the open edge the rotationDeg override turned the link to face, which the walls leave open (R362)", PlanSummary.TurnedOther);
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made());
            // 2 turned by about 180 degrees, 6 by 1.5 degrees, which keeps the front open
            Assert.Equal("2", string.Join(" ", PlanSummary.Conflicts(Numbers, built.Plans, built.Build.Fronts, built.Faces).Select(item => (string)item["plot"])));
            OverrideParameterStepTests.Built plain = OverrideParameterStepTests.Build(new OverrideList(null));
            Assert.Empty(PlanSummary.Conflicts(Numbers, plain.Plans, plain.Build.Fronts, plain.Faces));
        }

        // Review 1 of PR 88: Written of Walls, one source, so a plan whose written is missing or null is no written plot and never throws
        [Fact]
        public void R365_review1_a_plan_whose_written_is_missing_or_null_does_not_throw_and_is_not_counted()
        {
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made());
            ValueStatusTable table = ValueStatus.Table(built.Config, built.Rules, "parsons2024Rev6");
            Dictionary<string, Dictionary<string, object>> status = ValueStatus.Step(Numbers, built.Plans, built.Hands, table);
            var plans = new Dictionary<string, PlotPlan>(built.Plans)
            {
                ["8"] = new PlotPlan { Overrides = new Dictionary<string, object> { ["unit"] = "DP2" } },
                ["9"] = new PlotPlan { Overrides = new Dictionary<string, object> { ["unit"] = "DP2", ["written"] = null } },
            };
            string[] numbers = Numbers.Concat(new[] { "8", "9" }).ToArray();
            JObject key = BuildRules.KeyRules(built.Rules, null, PlanSummary.StepName);
            Tuple<Dictionary<string, Dictionary<string, object>>, JObject> made = PlanSummary.Step(numbers, plans, built.Build.Fronts, built.Hands, status, table, built.Config, key,
                "parsons2024Rev6", KeyLevels.LaterParameters(built.Config, BuildRules.LevelProfile(built.Rules, null)), built.Faces);
            Assert.Equal("False False", made.Item1["8"]["written"] + " " + made.Item1["9"]["written"]);
            Assert.Equal("{\"generic\":6,\"yard\":3,\"ramp\":6}", Text(made.Item2["summary"]["floorsFound"]));
            Assert.Equal(6, ((JObject)made.Item2["summary"]["handRules"]).Properties().Sum(item => (int)item.Value));
            Assert.Single(PlanSummary.Conflicts(numbers, plans, built.Build.Fronts, built.Faces));
        }

        [Fact]
        public void R365_the_config_names_the_fields_of_the_step_summary_and_the_worker_builds_it_on_the_inputs_of_the_overrides()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("unit:noGeometry written:noGeometry", string.Join(" ", config.StepFields(PlanSummary.StepName).Select(field => field.Name + ":" + field.Kind + field.Decimals)));
            Assert.Equal(PlanSummary.StepName, BuildSteps.Names.Last());
        }
    }
}
