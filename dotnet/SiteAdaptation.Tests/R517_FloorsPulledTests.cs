using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R517. Every floor of a plot stands inside its outline by floors.plotPull.pullM, so the floors of two plots lap on no 1 mm grid, on made up rings and the made up row of
    // OverrideParameterStepTests.cs built in code (R10), the C# side of python/tests/test_r517_floors_pulled.py, on net48 and net8.0
    public sealed class R517_FloorsPulledTests
    {
        private const double Pull = 0.002;

        private static void Near(double[][] expected, double[][] found)
        {
            Assert.Equal(expected.Length, found.Length);
            for (int at = 0; at < expected.Length; at++)
            {
                Assert.Equal(expected[at][0], found[at][0], 10);
                Assert.Equal(expected[at][1], found[at][1], 10);
            }
        }

        [Fact]
        public void R517_a_rectangle_moves_each_corner_by_the_pull_on_both_axes_in_either_direction()
        {
            double[][] ring = { new[] { 0.0, 0.0 }, new[] { 12.0, 0.0 }, new[] { 12.0, 5.52 }, new[] { 0.0, 5.52 } };
            double[][] inside = { new[] { 0.002, 0.002 }, new[] { 11.998, 0.002 }, new[] { 11.998, 5.518 }, new[] { 0.002, 5.518 } };
            Near(inside, FloorOutlines.PulledRing(ring, Pull));
            Near(inside.Reverse().ToArray(), FloorOutlines.PulledRing(ring.Reverse().ToArray(), Pull));
        }

        [Fact]
        public void R517_a_skew_corner_takes_the_mitre_and_a_notch_moves_into_the_plot_too()
        {
            double[][] moved = FloorOutlines.PulledRing(new[] { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 0.0, 10.0 } }, Pull);
            Assert.Equal(Pull, moved[0][0], 10);
            Assert.Equal(Pull, moved[1][1], 10);
            Assert.Equal(10.0 - Pull * (1 + System.Math.Sqrt(2)), moved[1][0], 10);
            double[][] notch = FloorOutlines.PulledRing(new[] { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 4.0 }, new[] { 6.0, 4.0 }, new[] { 6.0, 2.0 }, new[] { 4.0, 2.0 }, new[] { 4.0, 4.0 }, new[] { 0.0, 4.0 } }, Pull);
            Assert.Equal(6.0 + Pull, notch[4][0], 10);
            Assert.Equal(2.0 - Pull, notch[4][1], 10);
            Assert.Equal(6.0 + Pull, notch[3][0], 10);
            Assert.Equal(4.0 - Pull, notch[3][1], 10);
        }

        [Fact]
        public void R517_no_pull_and_a_ring_of_no_mitre_stay_as_given()
        {
            double[][] ring = { new[] { 0.0, 0.0 }, new[] { 12.0, 0.0 }, new[] { 12.0, 5.52 }, new[] { 0.0, 5.52 } };
            Near(ring, FloorOutlines.PulledRing(ring, 0));
            double[][] repeat = { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 } };
            Near(repeat, FloorOutlines.PulledRing(repeat, Pull));
            double[][] line = { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 } };
            Near(line, FloorOutlines.PulledRing(line, Pull));
        }

        [Fact]
        public void R517_the_pull_is_read_from_config_and_a_bad_one_refused()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            Assert.Equal(Pull, FloorOutlines.PlotPull(config, "project.json"));
            foreach (JToken bad in new JToken[] { -0.001, "0.002", true, JValue.CreateNull() })
            {
                var refused = (JObject)config.DeepClone();
                refused["floors"]["plotPull"]["pullM"] = bad;
                Assert.Contains("floors.plotPull.pullM", Assert.Throws<InvalidDataException>(() => FloorOutlines.PlotPull(refused, "project.json")).Message);
            }
        }

        [Fact]
        public void R517_the_floors_of_a_build_stand_inside_and_the_ramp_corners_move_with_the_outline()
        {
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(new OverrideList(new Dictionary<string, JObject>()), pull: Pull);
            double[][] outline = (double[][])built.Plans["2"].Overrides["rampOutline"];
            Near(new[] { new[] { 12.002, 0.002 }, new[] { 23.998, 0.002 }, new[] { 23.998, 5.518 }, new[] { 12.002, 5.518 } }, outline.Select(point => point.Select(value => Rounding.Fixed(value, 3)).ToArray()).ToArray());
            Assert.Equal(Pull, built.Build.Pull);
        }
    }
}
