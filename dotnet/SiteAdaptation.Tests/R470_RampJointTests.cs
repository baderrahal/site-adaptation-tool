using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R470. A front ramp corner two or more plots share holds one level, the road level of the first corner of the joint in plot order, taken
    // before any rampFrontM gap, and the back pair keeps El @ Entry, on made up ramps and the made up row of OverrideParameterStepTests.cs built
    // in code (R10), the C# side of python/tests/test_r470_ramp_joints.py, on net48 and net8.0
    public sealed class R470_RampJointTests
    {
        private const double Near = 0.01;
        private static readonly double[][] Left = { new[] { 0.0, 0.0 }, new[] { 12.0, 0.0 }, new[] { 12.0, 5.52 }, new[] { 0.0, 5.52 } };
        private static readonly double[][] Right = { new[] { 12.0, 0.0 }, new[] { 24.0, 0.0 }, new[] { 24.0, 5.52 }, new[] { 12.0, 5.52 } };

        private static double[][] Moved(double[][] corners, double dx)
        {
            return corners.Select(corner => new[] { corner[0] + dx, corner[1] }).ToArray();
        }

        // Each ramp as plot, corners, levels and the road of its front pair
        private static List<Tuple<string, double[][], double[], double[]>> Ramps(params object[] items)
        {
            var found = new List<Tuple<string, double[][], double[], double[]>>();
            for (int at = 0; at < items.Length; at += 4)
            {
                found.Add(Tuple.Create((string)items[at], (double[][])items[at + 1], (double[])items[at + 2], (double[])items[at + 3]));
            }
            return found;
        }

        private static string Text(Dictionary<string, double[]> joined)
        {
            return string.Join(" ", joined.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Key + ":" + string.Join(",", item.Value.Select(z => z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))));
        }

        [Fact]
        public void R470_an_override_on_either_side_leaves_the_front_joint_at_the_road_of_the_first_plot()
        {
            // The left ramp's front moved up 5 cm by a rampFrontM override, the right one's down 3 cm, the road at x 12 sampled 10.12 by the left
            // plot and 10.125 by the right, so the joint takes 10.12 of the first plot given and neither gap moves it
            double[] leftRoad = { 10.0, 10.12 }, rightRoad = { 10.125, 10.24 };
            double[] left = FloorOutlines.RampHeights(leftRoad.Select(z => z + 0.05).ToArray(), 10.1);
            double[] right = FloorOutlines.RampHeights(rightRoad.Select(z => z - 0.03).ToArray(), 10.2);
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("1", Left, left, leftRoad, "2", Right, right, rightRoad), Near);
            Assert.Equal(new[] { left[0], 10.12, left[2], left[3] }, joined["1"]);
            Assert.Equal(new[] { 10.12, right[1], right[2], right[3] }, joined["2"]);
            // Given the other way round the joint takes the road of 2
            joined = FloorOutlines.JointLevels(Ramps("2", Right, right, rightRoad, "1", Left, left, leftRoad), Near);
            Assert.Equal(10.125, joined["1"][1]);
            Assert.Equal(10.125, joined["2"][0]);
        }

        [Fact]
        public void R470_back_corners_of_different_el_at_entry_are_never_joined()
        {
            double[] left = FloorOutlines.RampHeights(new[] { 10.0, 10.12 }, 10.10);
            double[] right = FloorOutlines.RampHeights(new[] { 10.12, 10.24 }, 10.25);
            // The back right corner of 1 and the back left of 2 lie at (12, 5.52), 10.16 and 10.19 by El @ Entry, and keep them (R358)
            Assert.Equal(10.16, left[2], 12);
            Assert.Equal(10.19, right[3], 12);
            Assert.Empty(FloorOutlines.JointLevels(Ramps("1", Left, left, new[] { 10.0, 10.12 }, "2", Right, right, new[] { 10.12, 10.24 }), Near));
            // A back corner on the front corner of another ramp joins nothing either
            double[][] behind = { new[] { 12.0, 5.52 }, new[] { 24.0, 5.52 }, new[] { 24.0, 11.04 }, new[] { 12.0, 11.04 } };
            Assert.Empty(FloorOutlines.JointLevels(Ramps("1", Left, left, new[] { 10.0, 10.12 }, "3", behind, new[] { 10.5, 10.6, 10.7, 10.8 }, new[] { 10.5, 10.6 }), Near));
        }

        [Fact]
        public void R470_a_front_corner_11_mm_away_is_no_joint_and_one_9_mm_away_is()
        {
            double[] left = { 10.05, 10.17, 10.16, 10.04 }, right = { 10.07, 10.21, 10.25, 10.11 };
            Assert.Empty(FloorOutlines.JointLevels(Ramps("1", Left, left, new[] { 10.0, 10.12 }, "2", Moved(Right, Near * 1.1), right, new[] { 10.1, 10.24 }), Near));
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("1", Left, left, new[] { 10.0, 10.12 }, "2", Moved(Right, Near * 0.9), right, new[] { 10.1, 10.24 }), Near);
            Assert.Equal(10.12, joined["1"][1]);
            Assert.Equal(10.12, joined["2"][0]);
            Assert.Equal(10.16, joined["1"][2]);
            Assert.Equal(10.11, joined["2"][3]);
        }

        [Fact]
        public void R470_three_front_corners_at_one_point_and_a_chain_take_the_road_of_the_first_plot()
        {
            double[][] a = { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 1.0, 1.0 }, new[] { 0.0, 1.0 } };
            double[][] b = { new[] { 0.0, 0.0 }, new[] { -1.0, 0.0 }, new[] { -1.0, 1.0 }, new[] { 0.0, 1.0 } };
            double[][] c = { new[] { 0.0, 0.0 }, new[] { 0.0, -1.0 }, new[] { 1.0, -1.0 }, new[] { 1.0, -2.0 } };
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("a", a, new[] { 1.5, 5.0, 5.0, 2.0 }, new[] { 1.0, 5.0 }, "b", b, new[] { 2.0, 5.0, 5.0, 2.5 }, new[] { 2.0, 5.0 },
                "c", c, new[] { 4.0, 9.0, 9.0, 9.0 }, new[] { 4.0, 9.0 }), Near);
            Assert.Equal("a:1,5,5,2 b:1,5,5,2.5 c:1,9,9,9", Text(joined));
            // A chain, each front corner within 6 mm of the next and 12 mm from the far one, is one joint all the same
            Dictionary<string, double[]> chain = FloorOutlines.JointLevels(Ramps(
                "a", new[] { new[] { 0.0, 0.0 }, new[] { 5.0, 0.0 } }, new[] { 1.5, 7.0 }, new[] { 1.0, 7.0 },
                "b", new[] { new[] { Near * 0.6, 0.0 }, new[] { -5.0, 0.0 } }, new[] { 2.0, 8.0 }, new[] { 2.0, 8.0 },
                "c", new[] { new[] { Near * 1.2, 0.0 }, new[] { 0.0, 5.0 } }, new[] { 6.0, 9.0 }, new[] { 6.0, 9.0 }), Near);
            Assert.Equal("a:1,7 b:1,8 c:1,9", Text(chain));
        }

        [Fact]
        public void R470_a_plot_with_no_joint_or_a_joint_with_no_override_is_left_out()
        {
            double[] left = { 10.0, 10.12, 10.16, 10.04 }, second = { 10.17, 10.29, 10.28, 10.17 }, far = { 11.0, 11.1, 11.2, 11.3 };
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("1", Left, left, new[] { 10.0, 10.12 }, "2", Right, second, new[] { 10.12, 10.24 },
                "9", Moved(Left, 500.0), far, new[] { 11.0, 11.1 }), Near);
            Assert.Equal("2:10.12,10.29,10.28,10.17", Text(joined));
            Assert.Equal(10.17, second[0]);
            // A joint whose front corners lie at the road, as the ramp rules gave them with no gap, changes nothing
            Assert.Empty(FloorOutlines.JointLevels(Ramps("1", Left, left, new[] { 10.0, 10.12 }, "2", Right, new[] { 10.12, 10.24, 10.28, 10.16 }, new[] { 10.12, 10.24 }), Near));
        }

        [Fact]
        public void R470_near_is_read_from_config_floors_ramp_joint_and_a_bad_one_refused()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            Assert.Equal(Near, FloorOutlines.JointNear(config, "project.json"));
            config["floors"]["rampJoint"]["nearM"] = 0.02;
            double wider = FloorOutlines.JointNear(config, "project.json");
            Assert.Equal(0.02, wider);
            // At 2 cm the front corner 11 mm away is a joint
            Dictionary<string, double[]> joined = FloorOutlines.JointLevels(Ramps("1", Left, new[] { 10.0, 10.12, 10.16, 10.04 }, new[] { 10.0, 10.12 }, "2", Moved(Right, Near * 1.1),
                new[] { 10.10, 10.24, 10.28, 10.14 }, new[] { 10.10, 10.24 }), wider);
            Assert.Equal(10.12, joined["2"][0]);
            foreach (JToken bad in new JToken[] { 0, -0.01, "0.01", true, JValue.CreateNull() })
            {
                config["floors"]["rampJoint"]["nearM"] = bad;
                Assert.Contains("floors.rampJoint.nearM", Assert.Throws<InvalidDataException>(() => FloorOutlines.JointNear(config, "project.json")).Message);
            }
        }

        [Fact]
        public void R470_the_step_overrides_keep_the_front_joints_at_the_road_a_ramp_front_override_moved()
        {
            var list = new OverrideList(new Dictionary<string, JObject> { ["2"] = JObject.Parse("{\"rampFrontM\": {\"gapToRuleM\": 0.05}}") });
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(list);
            Dictionary<string, PlotPlan> plans = built.Plans;
            double[] one = (double[])plans["1"].Overrides["rampZ"], two = (double[])plans["2"].Overrides["rampZ"], three = (double[])plans["3"].Overrides["rampZ"];
            // Both front corners of 2 are shared, at x 12 with 1 and at x 24 with 3, so they stay at the road and its 5 cm gap moves neither
            Assert.Equal(one[1], two[0]);
            Assert.Equal(three[0], two[1]);
            Assert.Equal(new[] { 10.12, 10.24 }, two.Take(2).Select(z => Rounding.Fixed(z, 3)).ToArray());
            // Its back pair stays at El @ Entry as the ramp rules gave it
            Assert.Equal(new[] { 10.268, 10.148 }, two.Skip(2).Select(z => Rounding.Fixed(z, 3)).ToArray());
            // A ramp none of whose corners moved holds the levels of the ramp rules
            Assert.Equal(new[] { 10.36, 10.48, 10.508, 10.388 }, ((double[])plans["4"].Overrides["rampZ"]).Select(z => Rounding.Fixed(z, 3)).ToArray());
        }
    }
}
