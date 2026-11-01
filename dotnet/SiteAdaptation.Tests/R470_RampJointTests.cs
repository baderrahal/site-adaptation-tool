using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R470. A ramp corner two or more plots share holds one level, the mean of the levels the ramp rules give it, on made up ramps and the made
    // up row of OverrideParameterStepTests.cs built in code (R10), the C# side of python/tests/test_r470_ramp_joints.py, on net48 and net8.0
    public sealed class R470_RampJointTests
    {
        private const double Near = 0.01;
        private static readonly double[][] Left = { new[] { 0.0, 0.0 }, new[] { 12.0, 0.0 }, new[] { 12.0, 5.52 }, new[] { 0.0, 5.52 } };
        private static readonly double[][] Right = { new[] { 12.0, 0.0 }, new[] { 24.0, 0.0 }, new[] { 24.0, 5.52 }, new[] { 12.0, 5.52 } };

        private static double[][] Moved(double[][] corners, double dx)
        {
            return corners.Select(corner => new[] { corner[0] + dx, corner[1] }).ToArray();
        }

        private static List<Tuple<string, double[][], double[]>> Ramps(params object[] items)
        {
            var found = new List<Tuple<string, double[][], double[]>>();
            for (int at = 0; at < items.Length; at += 3)
            {
                found.Add(Tuple.Create((string)items[at], (double[][])items[at + 1], (double[])items[at + 2]));
            }
            return found;
        }

        [Fact]
        public void R470_two_ramps_side_by_side_with_front_gaps_meet_at_one_level()
        {
            // The left ramp's front moved up 5 cm by a rampFrontM override, the right one's down 3 cm, so the shared front corner at x 12 differs by 8 cm
            double[] left = FloorOutlines.RampHeights(new[] { 10.0 + 0.05, 10.12 + 0.05 }, 10.1);
            double[] right = FloorOutlines.RampHeights(new[] { 10.12 - 0.03, 10.24 - 0.03 }, 10.2);
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("1", Left, left, "2", Right, right), Near);
            Assert.Equal((left[1] + right[0]) / 2.0, joined["1"][1]);
            Assert.Equal(joined["1"][1], joined["2"][0]);
            Assert.Equal(10.13, joined["1"][1], 12);
            Assert.Equal(left[0], joined["1"][0]);
            Assert.Equal(right[1], joined["2"][1]);
        }

        [Fact]
        public void R470_back_corners_with_different_el_at_entry_meet_at_their_mean()
        {
            double[] left = FloorOutlines.RampHeights(new[] { 10.0, 10.12 }, 10.10);
            double[] right = FloorOutlines.RampHeights(new[] { 10.12, 10.24 }, 10.25);
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("1", Left, left, "2", Right, right), Near);
            Assert.Equal((left[2] + right[3]) / 2.0, joined["1"][2]);
            Assert.Equal(joined["1"][2], joined["2"][3]);
            Assert.Equal(10.175, joined["1"][2], 12);
            // The front corner at x 12 is the road sampled once, 10.12 on both, and stays as it was
            Assert.Equal(10.12, joined["1"][1]);
            Assert.Equal(10.12, joined["2"][0]);
        }

        [Fact]
        public void R470_a_corner_11_mm_away_is_no_joint_and_one_9_mm_away_is()
        {
            double[] left = { 10.0, 10.12, 10.16, 10.04 };
            double[] right = { 10.10, 10.24, 10.28, 10.14 };
            Assert.Empty(FloorOutlines.JointLevels(Ramps("1", Left, left, "2", Moved(Right, 0.011), right), Near));
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("1", Left, left, "2", Moved(Right, Near * 0.9), right), Near);
            Assert.Equal((10.12 + 10.10) / 2.0, joined["1"][1]);
            Assert.Equal(joined["1"][1], joined["2"][0]);
            Assert.Equal((10.16 + 10.14) / 2.0, joined["1"][2]);
            Assert.Equal(joined["1"][2], joined["2"][3]);
        }

        [Fact]
        public void R470_three_ramps_at_one_point_and_a_chain_take_one_mean()
        {
            double[][] a = { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 1.0, 1.0 }, new[] { 0.0, 1.0 } };
            double[][] b = { new[] { 0.0, 0.0 }, new[] { -1.0, 0.0 }, new[] { -1.0, 1.0 }, new[] { 0.0, 1.0 } };
            double[][] c = { new[] { 0.0, 0.0 }, new[] { 0.0, -1.0 }, new[] { 1.0, -1.0 }, new[] { 1.0, -2.0 } };
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("a", a, new[] { 1.0, 5.0, 5.0, 2.0 }, "b", b, new[] { 2.0, 5.0, 5.0, 2.0 },
                "c", c, new[] { 4.0, 9.0, 9.0, 9.0 }), Near);
            Assert.Equal((1.0 + 2.0 + 4.0) / 3.0, joined["a"][0]);
            Assert.Equal(joined["a"][0], joined["b"][0]);
            Assert.Equal(joined["a"][0], joined["c"][0]);
            // A chain, each corner within 6 mm of the next and 12 mm from the far one, is one joint all the same
            Dictionary<string, double[]> chain = FloorOutlines.JointLevels(Ramps("a", new[] { new[] { 0.0, 0.0 } }, new[] { 1.0 }, "b", new[] { new[] { 0.006, 0.0 } }, new[] { 2.0 },
                "c", new[] { new[] { 0.012, 0.0 } }, new[] { 6.0 }), Near);
            Assert.Equal("a:3 b:3 c:3", string.Join(" ", chain.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Key + ":" + string.Join(",", item.Value))));
        }

        [Fact]
        public void R470_a_plot_with_no_joint_is_left_out_and_the_order_of_the_sum_is_the_order_given()
        {
            double[] left = { 10.0, 10.12, 10.16, 10.04 }, second = { 10.13, 10.24, 10.28, 10.17 }, far = { 11.0, 11.1, 11.2, 11.3 };
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("1", Left, left, "2", Right, second, "9", Moved(Left, 500.0), far), Near);
            Assert.Equal(new[] { "1", "2" }, joined.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
            Assert.Equal(new[] { 10.0, 10.12, 10.16, 10.04 }, left);
            Assert.Equal(10.13, second[0]);
            // A joint whose levels already agree changes nothing, so the plan of its plots stays as the ramp rules wrote it
            Assert.Empty(FloorOutlines.JointLevels(Ramps("1", Left, left, "2", Right, new[] { 10.12, 10.24, 10.28, 10.16 }), Near));
            // Three levels summed left to right one by one, as Python sums them in joint_levels
            double total = 0.0;
            foreach (double z in new[] { 0.1, 0.2, 0.3 })
            {
                total += z;
            }
            double[][] point = { new[] { 0.0, 0.0 } };
            Dictionary<string, double[]> tri = FloorOutlines.JointLevels(Ramps("0", point, new[] { 0.1 }, "1", point, new[] { 0.2 }, "2", point, new[] { 0.3 }), Near);
            Assert.Equal(total / 3.0, tri["0"][0]);
            Assert.Equal(0.20000000000000004, tri["0"][0]);
        }

        [Fact]
        public void R470_near_is_read_from_config_floors_ramp_joint_and_a_bad_one_refused()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            Assert.Equal(Near, FloorOutlines.JointNear(config, "project.json"));
            config["floors"]["rampJoint"]["nearM"] = 0.02;
            double wider = FloorOutlines.JointNear(config, "project.json");
            Assert.Equal(0.02, wider);
            // At 2 cm the corner 11 mm away is a joint
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("1", Left, new[] { 10.0, 10.12, 10.16, 10.04 }, "2", Moved(Right, 0.011),
                new[] { 10.10, 10.24, 10.28, 10.14 }), wider);
            Assert.Equal((10.12 + 10.10) / 2.0, joined["1"][1]);
            foreach (JToken bad in new JToken[] { 0, -0.01, "0.01", true, JValue.CreateNull() })
            {
                config["floors"]["rampJoint"]["nearM"] = bad;
                Assert.Contains("floors.rampJoint.nearM", Assert.Throws<InvalidDataException>(() => FloorOutlines.JointNear(config, "project.json")).Message);
            }
        }

        [Fact]
        public void R470_the_step_overrides_join_the_front_corners_a_ramp_front_override_moved()
        {
            var list = new OverrideList(new Dictionary<string, JObject> { ["2"] = JObject.Parse("{\"rampFrontM\": {\"gapToRuleM\": 0.05}}") });
            Dictionary<string, PlotPlan> plans = OverrideParameterStepTests.Build(list).Plans;
            double[] one = (double[])plans["1"].Overrides["rampZ"], two = (double[])plans["2"].Overrides["rampZ"], four = (double[])plans["4"].Overrides["rampZ"];
            // The road at x 12 is 10.12 and plot 2 lifts its front 5 cm, so both plots hold the mean 10.145 there, and 10.265 at x 24
            Assert.Equal(one[1], two[0]);
            Assert.Equal(10.145, one[1], 12);
            Assert.Equal(10.145, Rounding.Fixed(one[1], 3));
            Assert.Equal(10.265, Rounding.Fixed(two[1], 3));
            Assert.Equal(two[1], ((double[])plans["3"].Overrides["rampZ"])[0]);
            // A ramp none of whose corners moved holds the levels of the ramp rules, rounded to those the plan wrote before the joints
            Assert.Equal(new[] { 10.36, 10.48, 10.508, 10.388 }, four.Select(z => Rounding.Fixed(z, 3)).ToArray());
        }
    }
}
