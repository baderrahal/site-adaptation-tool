using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using Xunit;

namespace SiteAdaptation.Tests.FAST11
{
    // R800, R801, F29 of short check 2. Under rev6 a plot no rule gives a hand is written not mirrored with its hand tagged missing, a plot
    // that lacks a parameter is written with the parameter left out and listed, and the plan counts the plots it leaves out by their first
    // key. On the made up row of OverrideParameterStepTests and the made up step files of R80_PlanFileTests only
    public sealed class R800_PlanWritesPlotsWithMissingTests : IDisposable
    {
        private const string Dp2File = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt";
        private readonly string work = Path.Combine(Path.GetTempPath(), "r800-" + Guid.NewGuid().ToString("N"));

        public R800_PlanWritesPlotsWithMissingTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private static DesignBasis Rev6()
        {
            JObject settings = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            settings["designBasis"]["mode"] = DesignBasis.Rev6;
            return DesignBasis.Of(settings, () => null);
        }

        private static JArray Bands()
        {
            return JArray.Parse("[{\"type\": \"Ramps 0.5%\", \"belowPercent\": 1.25}, {\"type\": \"Ramps 2%\", \"belowPercent\": null}]");
        }

        // Plot 6 of the made up row, its hand alone rule given no flag for its side, as the old test of R360 makes a plot with no hand
        private static OverrideParameterStepTests.Built NoHand(DesignBasis basis)
        {
            return OverrideParameterStepTests.Build(new OverrideList(new Dictionary<string, JObject>()), key => ((JObject)key["hand alone"]["byFile"][Dp2File]).Remove("pair right"),
                basis: basis, bands: basis == null ? null : Bands());
        }

        [Fact]
        public void R800_under_rev6_a_plot_with_no_hand_is_written_not_mirrored_and_its_hand_is_missing()
        {
            OverrideParameterStepTests.Built built = NoHand(Rev6());
            Assert.True(PlanOverrides.HandMissing(built.Hands["6"]));
            Dictionary<string, object> six = built.Plans["6"].Overrides;
            Assert.True((bool)six["written"]);
            Assert.False((bool)(JToken)six["mirrored"]);
            Assert.NotNull(six["insert"]);
            Assert.True((bool)six[PlanOverrides.HandMissingKey]);
            Assert.Equal("mirrored", built.Plans["6"].Lacks.First());
            // A plot the rule gives a hand holds no such key
            Assert.False(built.Plans["5"].Overrides.ContainsKey(PlanOverrides.HandMissingKey));
            // The status step tags the hand of plot 6 missing and that of plot 5 computed
            ValueStatusTable table = ValueStatus.Table(built.Config, built.Rules, "parsons2024Rev6");
            Dictionary<string, Dictionary<string, object>> status = ValueStatus.Step(built.Plans.Keys.ToList(), built.Plans, built.Hands, table);
            Assert.Equal(ValueStatus.MissingSource, (string)((JObject)status["6"]["fields"])["mirrored"]["source"]);
            Assert.Equal("computed", (string)((JObject)status["5"]["fields"])["mirrored"]["source"]);
        }

        [Fact]
        public void R800_a_build_of_the_past_job_still_leaves_a_plot_with_no_hand_out()
        {
            OverrideParameterStepTests.Built built = NoHand(null);
            Assert.False((bool)built.Plans["6"].Overrides["written"]);
            Assert.Null(built.Plans["6"].Overrides["mirrored"]);
            Assert.False(built.Plans["6"].Overrides.ContainsKey(PlanOverrides.HandMissingKey));
        }

        // The made up step files of R80, plot 1 written with no hand of the rule and one parameter of no source
        private static JObject Plan()
        {
            Dictionary<string, JObject> steps = R80_PlanFileTests.Steps();
            foreach (string side in new[] { "raw", "written" })
            {
                JObject one = (JObject)steps["overrides"]["plots"]["1"][side];
                one["mirrored"] = false;
                one["applied"] = new JArray();
                one[PlanOverrides.HandMissingKey] = true;
                steps["linkFrame"]["plots"]["1"][side]["lacks"] = JArray.Parse("[{\"key\":\"mirrored\",\"why\":\"no rule gives the mirror flag of DP2 in this cluster\"}]");
                steps["parameters"]["plots"]["1"][side]["lacks"] = new JArray("CASE");
            }
            // The yard outline of the made up rules fits DP2 mirrored, the plot is now built not mirrored
            PlanInputs inputs = R80_PlanFileTests.Inputs();
            inputs.KeyRules["yardOutline"]["groups"][0]["mirrored"] = false;
            return BuildPlan.Assemble(steps, inputs);
        }

        [Fact]
        public void R800_R801_the_plan_writes_the_plot_lists_what_it_lacks_and_counts_the_plots_left_out_by_their_first_key()
        {
            JObject plan = Plan();
            JObject one = ((JArray)plan["plots"]).OfType<JObject>().Single(plot => (string)plot["plot"] == "1");
            Assert.False((bool)one["mirrored"]["value"]);
            Assert.Null(one["parameters"]["CASE"]);
            JObject listed = ((JArray)plan["missing"]).OfType<JObject>().Single(item => (string)item["plot"] == "1");
            Assert.True((bool)listed["written"]);
            Assert.Equal("mirrored parameters.CASE", string.Join(" ", ((JArray)listed["missing"]).Select(lack => (string)lack["key"])));
            Assert.EndsWith(BuildPlan.HandMissingWhy, (string)listed["missing"][0]["why"]);
            JObject summary = (JObject)plan["summary"];
            Assert.Equal(1, (int)summary["written"]);
            Assert.Equal("{\"levels.ffl\":1,\"skip\":1}", summary[BuildPlan.NotWrittenByKey].ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal(1, (int)summary[BuildPlan.WrittenWithMissingKey]);
            CheckPlanCount count = CheckPlanCount.Of(plan);
            Assert.Equal("2 of 3 plots are not built: no floor level 1, on the skip list 1. Built with a value missing: 1 plots, each value tagged missing in the plan.", count.Line);
            Assert.Equal(1, count.NoRoadLevel);
            Assert.Equal("021", count.Nbh);
        }

        [Fact]
        public void R800_a_plot_with_no_hand_and_no_link_fit_is_left_out_for_the_hand_and_lacks_the_insert()
        {
            Dictionary<string, JObject> steps = R80_PlanFileTests.Steps();
            foreach (string side in new[] { "raw", "written" })
            {
                JObject two = (JObject)steps["overrides"]["plots"]["2"][side];
                two[PlanOverrides.HandMissingKey] = true;
                two["mirrored"] = null;
                steps["linkFrame"]["plots"]["2"][side]["lacks"] = JArray.Parse("[{\"key\":\"mirrored\",\"why\":\"no rule gives the mirror flag of DT1 in no cluster\"}]");
            }
            JObject listed = ((JArray)BuildPlan.Assemble(steps, R80_PlanFileTests.Inputs())["missing"]).OfType<JObject>().Single(item => (string)item["plot"] == "2");
            Assert.False((bool)listed["written"]);
            Assert.Equal("mirrored insert levels.ffl parameters.Steps", string.Join(" ", ((JArray)listed["missing"]).Select(lack => (string)lack["key"])));
            Assert.Equal("no rule gives the mirror flag of DT1 in no cluster", (string)listed["missing"][0]["why"]);
        }

        [Fact]
        public void R801_the_line_says_all_plots_are_built_when_none_is_left_out()
        {
            var plan = JObject.Parse("{\"neighbourhood\":\"N1\",\"summary\":{\"plots\":5,\"written\":5,\"notWritten\":0,\"notWrittenByFirstKey\":{},\"writtenWithMissing\":0,\"plotsWithNoRoadLevel\":[]}}");
            Assert.Equal("All 5 plots of the plan are built.", CheckPlanCount.Of(plan).Line);
            Assert.Equal("no hand", CheckPlanCount.Reason("mirrored"));
            Assert.Equal("parameters.CASE", CheckPlanCount.Reason("parameters.CASE"));
        }

        // A check folder of the work folder and its record naming the plan, as CheckRun writes them
        private string Check(string name, string nbh, bool record = true)
        {
            string folder = Path.Combine(work, CheckRun.FolderPrefix + name);
            Directory.CreateDirectory(folder);
            string plan = Path.Combine(folder, CheckRun.PlanName(nbh));
            File.WriteAllText(plan, "{\"neighbourhood\":\"" + nbh + "\",\"summary\":{\"plots\":2,\"written\":1,\"notWritten\":1,\"notWrittenByFirstKey\":{\"unit\":1},\"plotsWithNoRoadLevel\":[\"7\"]}}");
            if (record)
            {
                File.WriteAllText(Path.Combine(folder, CheckRun.RecordFile), CheckRun.Record("R4-" + name, new Dictionary<string, string> { [plan] = "made up" }).ToString());
            }
            return plan;
        }

        [Fact]
        public void R801_R802_the_counts_are_those_of_the_plan_the_newest_run_of_check_names()
        {
            Assert.Null(CheckPlanCount.LatestPlan(work));
            Assert.Null(CheckPlanCount.LatestPlan(null));
            Check("20261107-1", "01A");
            string newest = Check("20261108-1", "01B");
            Assert.Equal(newest, CheckPlanCount.LatestPlan(work));
            CheckPlanCount count = CheckPlanCount.Read(newest);
            Assert.Equal("1 of 2 plots are not built: no unit 1.", count.Line);
            Assert.Equal("01B", count.Nbh);
            // Broken on purpose: a newest run with no record names no plan, as steps 5 to 8 then stop, never an older plan
            Check("20261108-2", "01C", false);
            Assert.Null(CheckPlanCount.LatestPlan(work));
            Assert.Null(CheckPlanCount.Read(Path.Combine(work, "none.json")));
        }
    }
}
