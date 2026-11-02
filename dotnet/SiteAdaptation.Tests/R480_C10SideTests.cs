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
    // R480. The side edges of a written unit keyRules.wallSideOpen holds leave a span open by the hand of the plot, the C# side of python/tests/test_r480_c10_side.py on
    // the same made up faces and values, so the two sides give one wall list, on net48 and net8.0
    public sealed class R480_C10SideTests
    {
        private static readonly WallSides Sides = new WallSides
        {
            Cos = 0.5, ByUnit = new Dictionary<string, double[][]> { ["U1"] = new[] { new[] { 1.8, 7.21 }, new[] { 5.5, 22.025 } } },
        };

        private static readonly WallRules Rules = new WallRules { HigherBase = true, HigherTop = true, Depth = 0.1, Rise = 1.7, Near = 0.3, Sides = Sides };

        // Seen from its front on y = 0 the plot looks north, so the left side is the west edge and the right side the east edge
        private static readonly double[][] Ring = Points(0, 0, 16, 0, 16, 25, 0, 25);
        private static readonly double[][] Frame = Points(0, 0, 16, 0);

        // Plot 0 of another unit lies east of the plot and files the shared edge, as the first of the two in the order, its own edge from y = -3 to y = 30
        private static readonly Dictionary<string, double[][]> PairRings = new Dictionary<string, double[][]> { ["1"] = Ring, ["0"] = Points(16, -3, 30, -3, 30, 30, 16, 30) };
        private static readonly Dictionary<string, double[][]> PairFrames = new Dictionary<string, double[][]> { ["1"] = Frame, ["0"] = Points(16, -3, 30, -3) };

        private static double[][] Points(params double[] values)
        {
            return Enumerable.Range(0, values.Length / 2).Select(index => new[] { values[2 * index], values[2 * index + 1] }).ToArray();
        }

        private static string[] Kin(string cluster, string unit)
        {
            return new[] { cluster, unit };
        }

        private static List<PlanWall> Built(Dictionary<string, double[][]> rings, Dictionary<string, double[][]> frames, Dictionary<string, string[]> kin, Dictionary<string, bool> mirrored,
            WallRules rules = null, ISet<string> written = null)
        {
            written = written ?? new HashSet<string>(frames.Keys);
            Dictionary<string, double> ffls = written.ToDictionary(plot => plot, plot => 100.0);
            return Walls.Of(rings.Keys.OrderBy(plot => plot, StringComparer.Ordinal).ToList(), written, rings, frames, ffls, rules ?? Rules, frames, kin, mirrored);
        }

        private static List<PlanWall> Alone(bool mirrored = false, double[][] ring = null, double[][] frame = null, string unit = "U1")
        {
            return Built(new Dictionary<string, double[][]> { ["1"] = ring ?? Ring }, new Dictionary<string, double[][]> { ["1"] = frame ?? Frame },
                new Dictionary<string, string[]> { ["1"] = Kin(null, unit) }, new Dictionary<string, bool> { ["1"] = mirrored });
        }

        private static string Text(PlanWall wall)
        {
            return string.Join(" ", wall.Ends.SelectMany(point => point).Select(value => Math.Round(value, 6).ToString("R", CultureInfo.InvariantCulture)));
        }

        private static string[] Ends(List<PlanWall> walls, int edge, string plot = "1")
        {
            return walls.Where(wall => wall.Plot == plot && wall.Edge == edge).Select(Text).ToArray();
        }

        private static string[] Shared(List<PlanWall> walls, params string[] plots)
        {
            return walls.Where(wall => wall.Plots.SequenceEqual(plots)).Select(Text).ToArray();
        }

        [Fact]
        public void R480_a_plot_not_mirrored_leaves_the_left_span_on_its_left_and_the_right_span_on_its_right()
        {
            List<PlanWall> found = Alone();
            Assert.Equal(new[] { "0 25 0 7.21", "0 1.8 0 0" }, Ends(found, 3));
            Assert.Equal(new[] { "16 0 16 5.5", "16 22.025 16 25" }, Ends(found, 1));
        }

        [Fact]
        public void R480_a_mirrored_plot_swaps_the_two_spans()
        {
            List<PlanWall> found = Alone(true);
            Assert.Equal(new[] { "0 25 0 22.025", "0 5.5 0 0" }, Ends(found, 3));
            Assert.Equal(new[] { "16 0 16 1.8", "16 7.21 16 25" }, Ends(found, 1));
        }

        [Fact]
        public void R480_the_span_is_measured_from_the_end_nearer_the_middle_of_the_front_whichever_way_the_front_runs()
        {
            // The front on y = 25 looks south, so the left side is the east edge, the first edge of this ring runs along the front
            List<PlanWall> found = Alone(false, Points(16, 25, 0, 25, 0, 0, 16, 0), Points(16, 25, 0, 25));
            Assert.Equal(new[] { "16 0 16 17.79", "16 23.2 16 25" }, Ends(found, 3));
            Assert.Equal(new[] { "0 25 0 19.5", "0 2.975 0 0" }, Ends(found, 1));
        }

        [Fact]
        public void R480_the_back_edge_stays_whole_and_an_edge_of_28_m_keeps_the_span_from_the_front_end()
        {
            List<PlanWall> found = Alone(false, Points(0, 0, 16, 0, 16, 28, 0, 28));
            Assert.Equal(new[] { "16 28 0 28" }, Ends(found, 2));
            Assert.Equal(new[] { "0 28 0 7.21", "0 1.8 0 0" }, Ends(found, 3));
            Assert.Equal(new[] { "16 0 16 5.5", "16 22.025 16 28" }, Ends(found, 1));
        }

        [Fact]
        public void R480_an_edge_shorter_than_the_end_of_its_span_stays_whole()
        {
            List<PlanWall> found = Alone(false, Points(0, 0, 16, 0, 16, 20, 0, 20));
            Assert.Equal(new[] { "16 0 16 20" }, Ends(found, 1));
            Assert.Equal(new[] { "0 20 0 7.21", "0 1.8 0 0" }, Ends(found, 3));
        }

        [Fact]
        public void R480_a_unit_the_table_lacks_a_plot_with_no_hand_and_a_build_with_no_table_are_walled_whole()
        {
            string[] east = { "16 0 16 25" }, west = { "0 25 0 0" };
            List<PlanWall> other = Alone(unit: "U2");
            Assert.Equal(east, Ends(other, 1));
            Assert.Equal(west, Ends(other, 3));
            var rings = new Dictionary<string, double[][]> { ["1"] = Ring };
            var frames = new Dictionary<string, double[][]> { ["1"] = Frame };
            var kin = new Dictionary<string, string[]> { ["1"] = Kin(null, "U1") };
            List<PlanWall> noHand = Built(rings, frames, kin, new Dictionary<string, bool>());
            Assert.Equal(east, Ends(noHand, 1));
            Assert.Equal(west, Ends(noHand, 3));
            var bare = new WallRules { HigherBase = true, HigherTop = true, Depth = 0.1, Rise = 1.7, Near = 0.3 };
            List<PlanWall> noTable = Built(rings, frames, kin, new Dictionary<string, bool> { ["1"] = false }, bare);
            Assert.Equal(east, Ends(noTable, 1));
            Assert.Equal(west, Ends(noTable, 3));
            // A build that gives no hands at all, as the callers before R480
            List<PlanWall> none = Walls.Of(new[] { "1" }, new HashSet<string> { "1" }, rings, frames, new Dictionary<string, double> { ["1"] = 100.0 }, Rules);
            Assert.Equal(noTable.Select(Text).ToArray(), none.Select(Text).ToArray());
        }

        [Fact]
        public void R480_a_side_edge_another_plot_files_leaves_the_span_of_the_unit_from_the_front_of_the_unit()
        {
            List<PlanWall> found = Built(PairRings, PairFrames, new Dictionary<string, string[]> { ["1"] = Kin(null, "U1"), ["0"] = Kin(null, "U2") }, new Dictionary<string, bool> { ["1"] = false, ["0"] = false });
            Assert.Equal(new[] { "16 25 16 22.025", "16 5.5 16 0" }, Shared(found, "0", "1"));
            // The rest of the long edge of 0 past the unit has no span
            string[] own = Ends(found, 3, "0");
            Assert.Equal("16 30 16 25", own[0]);
            Assert.Equal("16 0 16 -3", own[own.Length - 1]);
        }

        [Fact]
        public void R480_an_edge_the_written_unit_does_not_write_is_left_by_its_neighbour_whole_when_the_unit_is_not_written()
        {
            List<PlanWall> found = Built(PairRings, new Dictionary<string, double[][]> { ["0"] = PairFrames["0"] }, new Dictionary<string, string[]> { ["1"] = Kin(null, "U1"), ["0"] = Kin(null, "U2") },
                new Dictionary<string, bool> { ["0"] = false }, null, new HashSet<string> { "0" });
            Assert.Equal(new[] { "16 25 16 0" }, Shared(found, "0", "1"));
        }

        [Fact]
        public void R480_an_edge_of_a_pair_the_wallopen_table_holds_keeps_that_rule()
        {
            var rules = new WallRules
            {
                HigherBase = true, HigherTop = true, Depth = 0.1, Rise = 1.7, Near = 0.3, Sides = Sides,
                Open = new Dictionary<string, double[]> { ["U1|U2"] = new[] { 10.0, 12.0 } },
            };
            List<PlanWall> found = Built(PairRings, PairFrames, new Dictionary<string, string[]> { ["1"] = Kin("0", "U1"), ["0"] = Kin("0", "U2") }, new Dictionary<string, bool> { ["1"] = false, ["0"] = false }, rules);
            // The span of Q115 runs from the front end of the edge of the plot that files it, y = -3 on this edge, and its parts run on from there
            Assert.Equal(new[] { "16 25 16 9", "16 7 16 0" }, Shared(found, "0", "1"));
        }

        [Fact]
        public void R480_two_units_on_one_edge_leave_open_both_their_spans_as_one()
        {
            var rings = new Dictionary<string, double[][]> { ["1"] = Ring, ["2"] = Points(-16, 0, 0, 0, 0, 25, -16, 25) };
            var frames = new Dictionary<string, double[][]> { ["1"] = Frame, ["2"] = Points(-16, 0, 0, 0) };
            // 1 not mirrored takes the left span on the edge and 2 not mirrored the right span
            List<PlanWall> found = Built(rings, frames, new Dictionary<string, string[]> { ["1"] = Kin(null, "U1"), ["2"] = Kin(null, "U1") }, new Dictionary<string, bool> { ["1"] = false, ["2"] = false });
            Assert.Equal(new[] { "0 25 0 22.025", "0 1.8 0 0" }, Shared(found, "1", "2"));
        }

        [Fact]
        public void R480_keyrules_wallsideopen_is_read_at_the_strong_share_and_a_table_that_is_no_span_table_is_refused()
        {
            JObject rules = JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules()));
            JObject level = (JObject)rules["levelProfiles"]["parsons2024Rev6"];
            var key = (JObject)level["keyRules"];
            WallRules found = Walls.Rules(key, level, "parsons2024Rev6", rules, "roshn-makkah.json", new JValue(0.3));
            Assert.Equal(0.5, found.Sides.Cos);
            Assert.Equal(new[] { 1.8, 7.21 }, found.Sides.ByUnit["C10"][0]);
            Assert.Equal(new[] { 5.5, 22.025 }, found.Sides.ByUnit["C10"][1]);
            Assert.True((bool)key["wallSideOpen"]["strong"]);
            Assert.True((double)key["wallSideOpen"]["share"] >= 0.9);
            JToken span = key["wallSideOpen"]["byUnit"]["C10"];
            foreach (JObject bad in new[]
            {
                new JObject { ["rule"] = "other", ["sideCosMax"] = 0.5, ["byUnit"] = new JObject { ["C10"] = span.DeepClone() } },
                new JObject { ["rule"] = "sideSpanByHand", ["byUnit"] = new JObject { ["C10"] = span.DeepClone() } },
                new JObject { ["rule"] = "sideSpanByHand", ["sideCosMax"] = 0.5, ["byUnit"] = new JObject { ["C10"] = new JObject { ["left"] = new JArray(7.0, 2.0), ["right"] = new JArray(5.5, 22.0) } } },
                new JObject { ["rule"] = "sideSpanByHand", ["sideCosMax"] = 0.5, ["byUnit"] = new JObject { ["C10"] = new JObject { ["left"] = new JArray(1.8, 7.2) } } },
                new JObject { ["rule"] = "sideSpanByHand", ["sideCosMax"] = 0.5 },
            })
            {
                var changed = (JObject)key.DeepClone();
                changed["wallSideOpen"] = bad;
                InvalidDataException error = Assert.Throws<InvalidDataException>(() => Walls.Rules(changed, level, "parsons2024Rev6", rules, "roshn-makkah.json", new JValue(0.3)));
                Assert.Equal("keyRules.wallSideOpen of parsons2024Rev6 holds no span table a build leaves a side edge open by, it takes sideSpanByHand with sideCosMax and for each unit a left and a right span of two numbers, the first under the second (R480)", error.Message);
            }
            var bare = (JObject)key.DeepClone();
            bare.Remove("wallSideOpen");
            Assert.Null(Walls.Rules(bare, level, "parsons2024Rev6", rules, "roshn-makkah.json", new JValue(0.3)).Sides);
        }
    }
}
