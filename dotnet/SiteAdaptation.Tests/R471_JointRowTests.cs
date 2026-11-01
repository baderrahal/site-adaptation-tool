using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R471. The check row of a ramp joint, CheckRows.JointRows of Common, the C# side of python/tests/test_r471_joint_rows.py on the same made up
    // corners, so the two sides give one check table, on net48 and net8.0
    public sealed class R471_JointRowTests
    {
        private static JObject Config()
        {
            return JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
        }

        private static readonly JObject Settings = Config();
        private static readonly JObject Block = (JObject)Settings["checkRows"];
        private static readonly Tuple<double, double> Limits = CheckRows.JointLimits(Settings);

        // test_r471_joint_rows.SHARED: plot 1 and 2 share their front corners 1 and 0, the back corners of the two lie 50 mm apart, plot 3 stands alone
        private static Dictionary<string, double[][]> Shared()
        {
            return new Dictionary<string, double[][]>
            {
                ["1"] = new[] { new[] { 0.0, 0.0, 10.0 }, new[] { 10.0, 0.0, 10.2 }, new[] { 10.0, 6.0, 10.5 }, new[] { 0.0, 6.0, 10.3 } },
                ["2"] = new[] { new[] { 10.0, 0.0, 10.2 }, new[] { 20.0, 0.0, 10.4 }, new[] { 20.0, 6.0, 10.7 }, new[] { 10.05, 6.0, 10.6 } },
                ["3"] = new[] { new[] { 50.0, 0.0, 11.0 }, new[] { 60.0, 0.0, 11.0 }, new[] { 60.0, 6.0, 11.3 }, new[] { 50.0, 6.0, 11.3 } },
            };
        }

        private static JObject Row(int corner, string others, double gap, string verdict, string use = "Rule", double limit = 0.001)
        {
            return new JObject
            {
                ["check"] = "Floor", ["field"] = "rampJoint", ["from"] = "joint", ["use"] = use, ["rule"] = "R471", ["toolValue"] = gap,
                ["past"] = new JObject { ["writes"] = JValue.CreateNull(), ["value"] = JValue.CreateNull(), ["why"] = Block["fields"]["rampJoint"]["why"] },
                ["why"] = string.Format(CultureInfo.InvariantCulture, "corner {0} shared with plots {1}, gap {2:0.000} m, {3}, passes at most {4:0.000} m", corner, others, gap, verdict, limit),
            };
        }

        private static Dictionary<string, List<JObject>> RowsOf(Dictionary<string, double[][]> corners, string[] numbers = null, Dictionary<string, CheckPlot> plots = null,
            Tuple<double, double> limits = null)
        {
            return CheckRows.JointRows(numbers ?? corners.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(), corners, plots ?? new Dictionary<string, CheckPlot>(), Block,
                limits ?? Limits);
        }

        private static void Same(IEnumerable<JObject> expected, IEnumerable<JObject> found)
        {
            Assert.Equal(new JArray(expected).ToString(Formatting.None), new JArray(found).ToString(Formatting.None));
        }

        [Fact]
        public void R471_limits_read_from_config()
        {
            Assert.Equal(Tuple.Create((double)Settings["floors"]["rampJoint"]["nearM"], (double)Settings["floors"]["rampJoint"]["passGapM"]), Limits);
            foreach (string broken in new[] { "{}", "{\"nearM\": 0, \"passGapM\": 0.001}", "{\"nearM\": 0.01, \"passGapM\": -1}", "{\"nearM\": \"0.01\", \"passGapM\": 0.001}" })
            {
                JObject settings = (JObject)Settings.DeepClone();
                settings["floors"]["rampJoint"] = JObject.Parse(broken);
                Assert.Contains("R471", Assert.Throws<InvalidDataException>(() => CheckRows.JointLimits(settings)).Message);
            }
        }

        [Fact]
        public void R471_joint_at_zero_gap_passes()
        {
            Dictionary<string, List<JObject>> found = RowsOf(Shared());
            Same(new[] { Row(2, "2", 0.0, "Pass") }, found["1"]);
            Same(new[] { Row(1, "1", 0.0, "Pass") }, found["2"]);
            Assert.Empty(found["3"]);
            Assert.Equal(JTokenType.Float, found["1"][0]["toolValue"].Type);
        }

        [Fact]
        public void R471_joint_at_two_mm_fails()
        {
            Dictionary<string, double[][]> corners = Shared();
            corners["2"][0] = new[] { 10.0, 0.0, 10.202 };
            Dictionary<string, List<JObject>> found = RowsOf(corners);
            Same(new[] { Row(2, "2", 0.002, "Fail") }, found["1"]);
            Same(new[] { Row(1, "1", 0.002, "Fail") }, found["2"]);
            Same(new[] { Row(2, "2", 0.002, "Pass", limit: 0.002) }, RowsOf(corners, limits: Tuple.Create(Limits.Item1, 0.002))["1"]);
        }

        [Fact]
        public void R471_corner_eleven_mm_away_has_no_row()
        {
            Dictionary<string, double[][]> corners = Shared();
            corners["2"][0] = new[] { 10.011, 0.0, 10.2 };
            Assert.All(RowsOf(corners).Values, Assert.Empty);
            Same(new[] { Row(1, "1", 0.0, "Pass") }, RowsOf(corners, limits: Tuple.Create(0.02, Limits.Item2))["2"]);
        }

        [Fact]
        public void R471_chain_of_three_plots_is_one_joint()
        {
            // 1 and 3 lie 12 mm apart, each 6 mm from 2, so the chain through 2 joins all three
            var corners = new Dictionary<string, double[][]>
            {
                ["1"] = new[] { new[] { 0.0, 0.0, 5.0 }, new[] { 10.0, 0.0, 5.001 }, new[] { 10.0, 6.0, 5.3 }, new[] { 0.0, 6.0, 5.3 } },
                ["2"] = new[] { new[] { 10.006, 0.0, 5.0 }, new[] { 20.0, 0.0, 5.1 }, new[] { 20.0, 6.0, 5.4 }, new[] { 10.05, 6.0, 5.4 } },
                ["3"] = new[] { new[] { 10.012, 0.0, 5.0005 }, new[] { 10.012, -6.0, 5.2 }, new[] { 30.0, -6.0, 5.2 }, new[] { 30.0, 0.0, 5.0 } },
            };
            Dictionary<string, List<JObject>> found = RowsOf(corners);
            Same(new[] { Row(2, "2, 3", 0.001, "Pass") }, found["1"]);
            Same(new[] { Row(1, "1, 3", 0.001, "Pass") }, found["2"]);
            Same(new[] { Row(1, "1, 2", 0.001, "Pass") }, found["3"]);
        }

        [Fact]
        public void R471_skip_plot_reads_skip_and_scope_holds_only_its_plots()
        {
            var plots = new Dictionary<string, CheckPlot> { ["2"] = new CheckPlot { Skip = "a made up reason" }, ["3"] = new CheckPlot() };
            Dictionary<string, List<JObject>> found = RowsOf(Shared(), new[] { "2", "3" }, plots);
            Assert.Equal(new[] { "2", "3" }, found.Keys.ToArray());
            Same(new[] { Row(1, "1", 0.0, "Pass", use: "Skip") }, found["2"]);
            Assert.Empty(found["3"]);
        }

        [Fact]
        public void R471_joint_rows_sort_after_the_floor_rows_of_a_plot()
        {
            Dictionary<string, List<JObject>> joints = RowsOf(Shared());
            Dictionary<string, CheckPlot> plots = Shared().Keys.ToDictionary(number => number, number => new CheckPlot { Unit = "VD2", Nbh = "N1", Lacks = new List<string> { "floors.yard" } });
            Dictionary<string, List<JObject>> made = CheckRows.Rows(new[] { "1", "2", "3" }, plots, new List<JObject>(), new List<JObject>(), new JObject(), Block, joints);
            Assert.Equal(new[] { "Floor floors.yard missing", "Floor rampJoint joint" }, made["1"].Select(item => item["check"] + " " + item["field"] + " " + item["from"]).ToArray());
        }

        [Fact]
        public void R471_ramp_corners_of_the_step_overrides_at_three_decimals()
        {
            // A plot with rampZ, rampOutline and a ramp type gives its corners as BuildPlan writes them, a plot with no ramp type gives none
            var plans = new Dictionary<string, PlotPlan>
            {
                ["1"] = new PlotPlan { Overrides = new Dictionary<string, object>
                {
                    ["rampZ"] = new[] { 10.00049, 10.2, 10.5, 10.3 }, ["rampType"] = new JValue("Ramps 2%"),
                    ["rampOutline"] = new[] { new[] { 0.0001, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 6.0 }, new[] { 0.0, 6.0 } },
                } },
                ["2"] = new PlotPlan { Overrides = new Dictionary<string, object> { ["rampZ"] = null, ["rampType"] = null, ["rampOutline"] = null } },
            };
            Dictionary<string, double[][]> corners = CheckRows.RampCorners(plans);
            Assert.Equal(new[] { "1" }, corners.Keys.ToArray());
            Assert.Equal(new[] { 0.0, 0.0, 10.0 }, corners["1"][0]);
            Assert.Equal(new[] { 0.0, 6.0, 10.3 }, corners["1"][3]);
        }
    }
}
