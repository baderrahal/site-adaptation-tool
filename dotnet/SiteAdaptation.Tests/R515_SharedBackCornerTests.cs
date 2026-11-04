using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R515, R516. Under a mode of floors.rampJoint.modes a back ramp corner two or more plots share holds one level, the door level of each such plot is the road at front plus
    // floors.rampJoint.slopePercent over the ramp depth, and the entryM and rampFrontM overrides of such a plot are left with why, on made up ramps and the made up row of
    // OverrideParameterStepTests.cs built in code (R10), the C# side of python/tests/test_r515_shared_back_corner.py, on net48 and net8.0
    public sealed class R515_SharedBackCornerTests
    {
        private const double Near = 0.005;
        private static readonly double[][] Left = { new[] { 0.0, 0.0 }, new[] { 12.0, 0.0 }, new[] { 12.0, 5.52 }, new[] { 0.0, 5.52 } };
        private static readonly double[][] Right = { new[] { 12.0, 0.0 }, new[] { 24.0, 0.0 }, new[] { 24.0, 5.52 }, new[] { 12.0, 5.52 } };
        private static readonly double[][] Far = { new[] { 100.0, 0.0 }, new[] { 112.0, 0.0 }, new[] { 112.0, 5.52 }, new[] { 100.0, 5.52 } };

        private static List<Tuple<string, double[][], double[], double[]>> Ramps(params object[] items)
        {
            var found = new List<Tuple<string, double[][], double[], double[]>>();
            for (int at = 0; at < items.Length; at += 4)
            {
                found.Add(Tuple.Create((string)items[at], (double[][])items[at + 1], (double[])items[at + 2], (double[])items[at + 3]));
            }
            return found;
        }

        private static void Joined(JObject config)
        {
            config["floors"]["rampJoint"]["join"] = true;
            config["floors"]["rampJoint"]["joinBack"] = true;
            config["floors"]["rampJoint"]["modes"] = new JArray("rev6", "roshnPastJob");
            config["floors"]["rampJoint"]["slopePercent"] = 0.5;
        }

        [Fact]
        public void R515_a_back_joint_takes_the_level_of_its_first_corner_and_a_free_corner_stays()
        {
            double[] left = FloorOutlines.RampHeights(new[] { 10.0, 10.12 }, 10.1), right = FloorOutlines.RampHeights(new[] { 10.12, 10.24 }, 10.25);
            Dictionary<string, double[]> found = FloorOutlines.JointLevels(Ramps("1", Left, left, new[] { 10.0, 10.12 }, "2", Right, right, new[] { 10.12, 10.24 }), Near, true);
            Assert.False(found.ContainsKey("1"));
            Assert.Equal(left[2], found["2"][3]);
            Assert.Equal(right.Take(3), found["2"].Take(3));
            // Given the other way round the joint takes the back corner of 2
            found = FloorOutlines.JointLevels(Ramps("2", Right, right, new[] { 10.12, 10.24 }, "1", Left, left, new[] { 10.0, 10.12 }), Near, true);
            Assert.False(found.ContainsKey("2"));
            Assert.Equal(right[3], found["1"][2]);
            Assert.Equal(left[3], found["1"][3]);
        }

        [Fact]
        public void R515_with_join_back_false_the_back_pair_joins_nothing_as_r470()
        {
            double[] left = FloorOutlines.RampHeights(new[] { 10.0, 10.12 }, 10.1), right = FloorOutlines.RampHeights(new[] { 10.12, 10.24 }, 10.25);
            Assert.Empty(FloorOutlines.JointLevels(Ramps("1", Left, left, new[] { 10.0, 10.12 }, "2", Right, right, new[] { 10.12, 10.24 }), Near, false));
        }

        [Theory]
        [InlineData(0.0046, true)]
        [InlineData(0.0054, true)]
        [InlineData(0.0056, false)]
        public void R515_corners_are_grouped_on_the_mm_the_plan_writes(double dx, bool near)
        {
            double[][] right = Right.Select(corner => new[] { corner[0] + dx, corner[1] }).ToArray();
            Dictionary<string, double[]> found = FloorOutlines.JointLevels(Ramps("1", Left, new[] { 10.0, 10.12, 10.15, 10.03 }, new[] { 10.0, 10.12 }, "2", right, new[] { 10.2, 10.3, 10.4, 10.5 }, new[] { 10.2, 10.3 }), Near, true);
            Assert.Equal(near, found.ContainsKey("2"));
        }

        [Fact]
        public void R515_the_rule_of_config_turns_on_under_its_modes_and_refuses_a_bad_block()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            JointRule rule = FloorOutlines.JointRuleOf(config, "project.json", "rev6");
            Assert.Equal(Near, rule.Near);
            Assert.True(rule.Back);
            Assert.Equal(0.5, rule.Slope);
            // The past job proof breaks with the joint on, so config keeps the past job off (R515)
            Assert.Equal("rev6", (string)((JArray)config["floors"]["rampJoint"]["modes"]).Single());
            Assert.Null(FloorOutlines.JointRuleOf(config, "project.json", "roshnPastJob"));
            Assert.Null(FloorOutlines.JointRuleOf(config, "project.json", null));
            var off = (JObject)config.DeepClone();
            off["floors"]["rampJoint"]["join"] = false;
            Assert.Null(FloorOutlines.JointRuleOf(off, "project.json", "rev6"));
            foreach (Tuple<string, JToken, string> bad in new[]
            {
                Tuple.Create("modes", (JToken)"rev6", "floors.rampJoint.modes"), Tuple.Create("modes", (JToken)new JArray("rev5"), "floors.rampJoint.modes"),
                Tuple.Create("joinBack", (JToken)1, "floors.rampJoint.joinBack"), Tuple.Create("slopePercent", (JToken)(-1), "floors.rampJoint.slopePercent"),
                Tuple.Create("slopePercent", (JToken)"0.5", "floors.rampJoint.slopePercent"), Tuple.Create("slopePercent", (JToken)true, "floors.rampJoint.slopePercent"),
            })
            {
                var refused = (JObject)config.DeepClone();
                refused["floors"]["rampJoint"][bad.Item1] = bad.Item2;
                Assert.Contains(bad.Item3, Assert.Throws<InvalidDataException>(() => FloorOutlines.JointRuleOf(refused, "project.json", "rev6")).Message);
            }
        }

        [Fact]
        public void R515_shared_backs_names_the_plots_of_a_shared_back_corner()
        {
            var joint = new JointRule { Near = Near, Back = true, Slope = 0.5 };
            var front = new Dictionary<string, FrontItem>
            {
                ["1"] = new FrontItem { Unit = "A", Front = new[] { 1.0, 1.1 }, Corners = Left }, ["2"] = new FrontItem { Unit = "A", Front = new[] { 1.1, 1.2 }, Corners = Right },
                ["9"] = new FrontItem { Unit = "A", Front = new[] { 1.0, 1.1 }, Corners = Far }, ["8"] = new FrontItem(),
            };
            Assert.Equal(new[] { "1", "2" }, FloorOutlines.SharedBacks(front, joint).OrderBy(item => item, StringComparer.Ordinal));
            // A ramp that shares only its front corner: the back pair apart
            double[][] skew = { new[] { 12.0, 0.0 }, new[] { 24.0, 0.0 }, new[] { 24.0, 5.52 }, new[] { 12.5, 5.52 } };
            Assert.Empty(FloorOutlines.SharedBacks(new Dictionary<string, FrontItem> { ["1"] = front["1"], ["2"] = new FrontItem { Unit = "A", Front = new[] { 1.1, 1.2 }, Corners = skew } }, joint));
            Assert.Empty(FloorOutlines.SharedBacks(front, null));
            Assert.Empty(FloorOutlines.SharedBacks(front, new JointRule { Near = Near, Back = false, Slope = 0.5 }));
        }

        private static double[] Levels(OverrideParameterStepTests.Built built, string number)
        {
            return ((double[])built.Plans[number].Overrides["rampZ"]).Select(z => Rounding.Fixed(z, 3)).ToArray();
        }

        [Fact]
        public void R515_a_row_under_the_mode_holds_one_level_at_every_shared_corner_and_its_ramps_stay_planes()
        {
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(new OverrideList(new Dictionary<string, JObject>()), basis: DesignBasis.Of(JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig())), () => null), changeConfig: Joined);
            double[] one = Levels(built, "1"), two = Levels(built, "2"), three = Levels(built, "3");
            Assert.Equal(one[1], two[0]);
            Assert.Equal(two[1], three[0]);
            Assert.Equal(one[2], two[3]);
            Assert.Equal(two[2], three[3]);
            foreach (double[] z in new[] { one, two, three })
            {
                Assert.InRange(z[2] - z[1], 0.005 * 5.52 - 2e-3, 0.005 * 5.52 + 2e-3);
                Assert.InRange(z[3] - z[0], 0.005 * 5.52 - 2e-3, 0.005 * 5.52 + 2e-3);
            }
        }

        private static DesignBasis Rev6Basis()
        {
            JObject settings = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            settings["designBasis"]["mode"] = "rev6";
            settings["designBasis"]["doorLevel"]["slopePercent"] = JObject.Parse("{\"default\": 0.5, \"VD2\": 2.0}");
            return DesignBasis.Of(settings, () => null);
        }

        private static JArray Bands()
        {
            return JArray.Parse("[{\"type\": \"Ramps 0.5%\", \"belowPercent\": 1.25}, {\"type\": \"Ramps 2%\", \"belowPercent\": null}]");
        }

        [Fact]
        public void R515_under_rev6_a_two_percent_unit_in_a_row_takes_the_slope_of_the_joint_and_its_door_level_type_and_ffl_follow()
        {
            var list = new OverrideList(new Dictionary<string, JObject>());
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(list, basis: Rev6Basis(), bands: Bands(), changeConfig: Joined);
            // 1, a VD2 at 2 percent by the door level block, shares its back corner with 2, so it takes 0.5 percent, the slope of the joint in config
            Assert.Equal(0.5, (double)built.Plans["1"].Overrides["slopePercent"], 9);
            Assert.Equal("Ramps 0.5%", (string)(JToken)built.Plans["1"].Overrides["rampType"]);
            Assert.Equal(Levels(built, "1")[2], Levels(built, "2")[3]);
            // With the joint off the same unit holds its 2 percent and its back corners stand apart
            OverrideParameterStepTests.Built apart = OverrideParameterStepTests.Build(list, basis: Rev6Basis(), bands: Bands());
            Assert.Equal(2.0, (double)apart.Plans["1"].Overrides["slopePercent"], 9);
            Assert.True(Math.Abs(Levels(apart, "1")[2] - Levels(apart, "2")[3]) > 0.05);
        }

        [Fact]
        public void R515_R517_R518_the_joint_with_the_pull_holds_one_level_at_each_pulled_corner_and_the_rows_pass()
        {
            var list = new OverrideList(new Dictionary<string, JObject>());
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(list, basis: Rev6Basis(), bands: Bands(), changeConfig: Joined, pull: 0.002);
            Assert.Equal(Near, built.Build.Joint.Near);
            Dictionary<string, double[][]> corners = CheckRows.RampCorners(built.Plans);
            // The corners stand pulled, 4 mm apart across each shared edge, and hold one level
            Assert.Equal(11.998, corners["1"][1][0]);
            Assert.Equal(12.002, corners["2"][0][0]);
            Assert.Equal(corners["1"][1][2], corners["2"][0][2]);
            Assert.Equal(corners["2"][1][2], corners["3"][0][2]);
            Assert.Equal(corners["1"][2][2], corners["2"][3][2]);
            Assert.Equal(corners["2"][2][2], corners["3"][3][2]);
            Dictionary<string, List<JObject>> rows = CheckRows.JointRows(new[] { "1", "2", "3" }, corners, new Dictionary<string, CheckPlot>(), (JObject)built.Config["checkRows"], CheckRows.JointLimits(built.Config));
            Assert.Equal(new[] { 2, 4, 4 }, new[] { "1", "2", "3" }.Select(number => rows[number].Count));
            Assert.All(rows.Values.SelectMany(made => made), row =>
            {
                Assert.Equal(0.0, (double)row["toolValue"]);
                Assert.Contains(", Pass,", (string)row["why"]);
            });
        }

        [Fact]
        public void R516_an_entry_of_the_list_turns_the_joint_off_for_the_build()
        {
            var list = new OverrideList(new Dictionary<string, JObject> { ["2"] = JObject.Parse("{\"rampFrontM\": {\"gapToRuleM\": 0.05}, \"entryM\": {\"gapToRuleM\": 0.2}}") });
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(list, basis: Rev6Basis(), bands: Bands(), changeConfig: Joined);
            OverrideParameterStepTests.Built off = OverrideParameterStepTests.Build(list, basis: Rev6Basis(), bands: Bands());
            Assert.Null(built.Build.Joint);
            Assert.Empty(built.Build.JointBack);
            foreach (string number in new[] { "1", "2", "3" })
            {
                Assert.Equal(Levels(off, number), Levels(built, number));
            }
            Assert.Equal(off.Plans["2"].Overrides["entry"], built.Plans["2"].Overrides["entry"]);
            Assert.Equal(2.0, (double)built.Plans["1"].Overrides["slopePercent"], 9);
            Assert.Contains("entryM", (List<string>)built.Plans["2"].Overrides["applied"]);
            Assert.Contains("rampFrontM", (List<string>)built.Plans["2"].Overrides["applied"]);
        }
    }
}
