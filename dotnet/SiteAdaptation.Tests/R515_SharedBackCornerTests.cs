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
        public void R515_shared_ramps_names_the_plots_of_a_shared_front_and_of_a_shared_back()
        {
            var joint = new JointRule { Near = Near, Back = true, Slope = 0.5 };
            var front = new Dictionary<string, FrontItem>
            {
                ["1"] = new FrontItem { Unit = "A", Front = new[] { 1.0, 1.1 }, Corners = Left }, ["2"] = new FrontItem { Unit = "A", Front = new[] { 1.1, 1.2 }, Corners = Right },
                ["9"] = new FrontItem { Unit = "A", Front = new[] { 1.0, 1.1 }, Corners = Far }, ["8"] = new FrontItem(),
            };
            Tuple<HashSet<string>, HashSet<string>> found = FloorOutlines.SharedRamps(front, joint);
            Assert.Equal(new[] { "1", "2" }, found.Item1.OrderBy(item => item, StringComparer.Ordinal));
            Assert.Equal(new[] { "1", "2" }, found.Item2.OrderBy(item => item, StringComparer.Ordinal));
            // A ramp that shares only its front corner: the back pair apart
            double[][] skew = { new[] { 12.0, 0.0 }, new[] { 24.0, 0.0 }, new[] { 24.0, 5.52 }, new[] { 12.5, 5.52 } };
            found = FloorOutlines.SharedRamps(new Dictionary<string, FrontItem> { ["1"] = front["1"], ["2"] = new FrontItem { Unit = "A", Front = new[] { 1.1, 1.2 }, Corners = skew } }, joint);
            Assert.Equal(2, found.Item1.Count);
            Assert.Empty(found.Item2);
            Assert.Empty(FloorOutlines.SharedRamps(front, null).Item1);
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

        [Fact]
        public void R515_under_rev6_a_two_percent_unit_in_a_row_takes_the_slope_of_the_joint_and_its_door_level_type_and_ffl_follow()
        {
            JObject settings = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            settings["designBasis"]["mode"] = "rev6";
            settings["designBasis"]["doorLevel"]["slopePercent"] = JObject.Parse("{\"default\": 0.5, \"DP2\": 2.0}");
            DesignBasis rev6 = DesignBasis.Of(settings, () => null);
            var list = new OverrideList(new Dictionary<string, JObject>());
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(list, basis: rev6, bands: Bands(), changeConfig: Joined);
            // 5, a DP2 at 2 percent by the door level block, shares its back corners with 4 and 6, so it takes 0.5 percent, the slope of the joint in config
            Assert.Equal(0.5, (double)built.Plans["5"].Overrides["slopePercent"], 9);
            Assert.Equal("Ramps 0.5%", (string)(JToken)built.Plans["5"].Overrides["rampType"]);
            double[] four = Levels(built, "4"), five = Levels(built, "5"), six = Levels(built, "6");
            Assert.Equal(four[2], five[3]);
            Assert.Equal(five[2], six[3]);
            // With the joint off the same unit holds its 2 percent and its back corners keep their own levels
            OverrideParameterStepTests.Built apart = OverrideParameterStepTests.Build(list, basis: rev6, bands: Bands());
            Assert.Equal(2.0, (double)apart.Plans["5"].Overrides["slopePercent"], 9);
        }

        private static JArray Bands()
        {
            return JArray.Parse("[{\"type\": \"Ramps 0.5%\", \"belowPercent\": 1.25}, {\"type\": \"Ramps 2%\", \"belowPercent\": null}]");
        }

        [Fact]
        public void R516_an_entry_and_a_front_override_of_a_shared_ramp_are_left_with_why()
        {
            DesignBasis past = DesignBasis.Of(JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig())), () => null);
            OverrideParameterStepTests.Built plain = OverrideParameterStepTests.Build(new OverrideList(new Dictionary<string, JObject>()), basis: past, changeConfig: Joined);
            var list = new OverrideList(new Dictionary<string, JObject> { ["2"] = JObject.Parse("{\"rampFrontM\": {\"gapToRuleM\": 0.05}, \"entryM\": {\"gapToRuleM\": 0.2}}") });
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(list, basis: past, changeConfig: Joined);
            foreach (string number in new[] { "1", "2", "3" })
            {
                Assert.Equal(Levels(plain, number), Levels(built, number));
            }
            Assert.Equal(plain.Plans["2"].Overrides["entry"], built.Plans["2"].Overrides["entry"]);
            JToken left = JToken.FromObject(built.Plans["2"].Overrides["notApplied"]);
            Assert.Equal(PlanOverrides.JointLeft, (string)left["entryM"]);
            Assert.Equal(PlanOverrides.JointLeft, (string)left["rampFrontM"]);
        }
    }
}
