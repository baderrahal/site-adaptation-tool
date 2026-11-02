using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R475. The C# side of python/tests/test_r475_yard_in_plot.py on the same made up rings (R10) with the same expected values, on net48 and net8.0
    public sealed class R475_YardInPlotTests
    {
        private static readonly double[][] Face = { new[] { 20.0, 0.0 }, new[] { 20.0, 30.0 }, new[] { 0.0, 30.0 }, new[] { 0.0, 0.0 } };
        private static readonly double[][] Ramp = { new[] { 5.0, 0.0 }, new[] { 11.0, 0.0 }, new[] { 11.0, 6.0 }, new[] { 5.0, 6.0 } };

        // Each ring a text of pairs, so the rings of the two languages read the same
        private static List<List<double[]>> Rings(params string[] rings)
        {
            return rings.Select(ring => ring.Split(' ').Select(pair => pair.Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray()).ToList()).ToList();
        }

        private static string Text(IEnumerable<IEnumerable<double[]>> rings)
        {
            return string.Join(" | ", rings.Select(ring => string.Join(" ", ring.Select(point => string.Join(",", point.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))))));
        }

        private static double[][] Array(string ring)
        {
            return Rings(ring)[0].ToArray();
        }

        [Fact]
        public void R475_a_yard_past_the_face_is_cut_to_it()
        {
            Assert.Equal("2,10 20,10 20,25 2,25", Text(FloorOutlines.YardInPlot(Rings("2,10 25,10 25,25 2,25"), Face, Ramp)));
        }

        [Fact]
        public void R475_a_yard_on_the_ramp_is_cut_by_it()
        {
            Assert.Equal("2,4 5,4 5,6 11,6 11,4 18,4 18,25 2,25", Text(FloorOutlines.YardInPlot(Rings("2,4 18,4 18,25 2,25"), Face, Ramp)));
        }

        [Fact]
        public void R475_a_concave_yard_keeps_its_notch()
        {
            Assert.Equal("10,10 20,10 20,14 14,14 14,20 10,20", Text(FloorOutlines.YardInPlot(Rings("10,10 25,10 25,14 14,14 14,20 10,20"), Face, Ramp)));
        }

        [Fact]
        public void R475_a_yard_with_a_hole_is_even_odd_and_the_outer_ring_comes_first()
        {
            Assert.Equal("1,8 20,8 20,28 1,28 | 8,15 8,20 12,20 12,15", Text(FloorOutlines.YardInPlot(Rings("8,15 12,15 12,20 8,20", "1,8 25,8 25,28 1,28"), Face, Ramp)));
        }

        [Fact]
        public void R475_a_yard_of_two_loops_keeps_both_rings_sorted_by_first_vertex()
        {
            Assert.Equal("2,10 6,10 6,12 2,12 | 15,10 20,10 20,12 15,12", Text(FloorOutlines.YardInPlot(Rings("15,10 25,10 25,12 15,12", "2,10 6,10 6,12 2,12"), Face, Ramp)));
        }

        [Fact]
        public void R475_a_yard_inside_is_returned_as_it_was_given_byte_for_byte()
        {
            // Off the grid, wound clockwise, a collinear vertex and not from its smallest corner: nothing of it changes
            List<List<double[]>> loops = Rings("10.00012,10.00049 10.00012,20.1 15,20.1 18.1234,20.1 18.1234,10.00049");
            string given = Text(loops);
            Assert.Same(loops, FloorOutlines.YardInPlot(loops, Face, Ramp));
            Assert.Equal(given, Text(loops));
        }

        [Fact]
        public void R475_a_yard_that_only_touches_the_face_and_the_ramp_is_returned_as_it_was_given()
        {
            List<List<double[]>> loops = Rings("0,6 20,6 20,30 0,30");
            Assert.Same(loops, FloorOutlines.YardInPlot(loops, Face, Ramp));
        }

        [Fact]
        public void R475_a_yard_the_cut_leaves_empty_gives_no_loops()
        {
            Assert.Empty(FloorOutlines.YardInPlot(Rings("30,10 40,10 40,20 30,20"), Face, Ramp));
            Assert.Empty(FloorOutlines.YardInPlot(Rings("6,1 10,1 10,5 6,5"), Face, Ramp));
        }

        [Fact]
        public void R475_a_plot_with_no_face_or_no_ramp_clips_by_what_it_has()
        {
            List<List<double[]>> loops = Rings("2,4 25,4 25,25 2,25");
            Assert.Equal("2,4 20,4 20,25 2,25", Text(FloorOutlines.YardInPlot(loops, Face, null)));
            Assert.Equal("2,4 5,4 5,6 11,6 11,4 25,4 25,25 2,25", Text(FloorOutlines.YardInPlot(loops, null, Ramp)));
            Assert.Same(loops, FloorOutlines.YardInPlot(loops, null, null));
        }

        [Fact]
        public void R475_the_shared_case_gives_the_same_rings_in_the_same_order_in_both_languages()
        {
            // test_r475_yard_in_plot.SHARED: outer rings anticlockwise, then holes clockwise, each from its smallest (x, y), sorted by it
            List<List<double[]>> loops = Rings("3.0004,3.5 3.0004,26 25,26 25,3.5", "14,12 14,18 17,18 17,12", "0.5,28 4,29.5 4,28");
            Assert.Equal("0.5,28 4,28 4,29.5 | 3,3.5 20,3.5 20,8 3,8 | 3,9 20,9 20,26 3,26 | 14,12 14,18 17,18 17,12",
                Text(FloorOutlines.YardInPlot(loops, Face, Array("-1,8 30,8 30,9 -1,9"))));
        }

        // The made up row of LevelFloorStepTests: plot 5, x 48 to 60, with a yard in the link frame past the plot on its right and over the ramp
        private static void Yard(JObject rules, JArray loop)
        {
            JObject key = (JObject)rules["levelProfiles"]["parsons2024Rev6"]["keyRules"];
            key["yardOutline"]["groups"][0]["loops"] = new JArray { loop };
        }

        private static JArray Past()
        {
            return new JArray(new JArray(1.0, 2.0), new JArray(15.0, 2.0), new JArray(15.0, 10.0), new JArray(1.0, 10.0));
        }

        private static JArray Outside()
        {
            return new JArray(new JArray(20.0, 8.0), new JArray(22.0, 8.0), new JArray(22.0, 10.0), new JArray(20.0, 10.0));
        }

        [Fact]
        public void R475_the_steps_floors_and_overrides_write_the_one_cut_yard()
        {
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(rules => Yard(rules, Past()));
            double[] insert = ((double[])made.Links["5"]["insert"]).Select(value => Rounding.Fixed(value, 3)).ToArray();
            double low = Math.Max(insert[1] + 2.0, 5.52);
            string expected = string.Join(" ", new[] { new[] { insert[0] + 1.0, low }, new[] { 60.0, low }, new[] { 60.0, insert[1] + 10.0 }, new[] { insert[0] + 1.0, insert[1] + 10.0 } }
                .Select(point => string.Join(",", point.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))));
            Assert.Equal(expected, Text((IEnumerable<IEnumerable<double[]>>)made.Floors["5"]["yardOutline"]));

            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(new OverrideList(new Dictionary<string, JObject>()), changeRules: rules => Yard(rules, Past()));
            Assert.Equal(expected, Text((List<List<double[]>>)built.Plans["5"].Overrides["yardOutline"]));
        }

        [Fact]
        public void R475_a_ramp_on_the_open_edge_of_q116_cuts_the_yard_where_it_ends()
        {
            var list = new OverrideList(new Dictionary<string, JObject> { ["5"] = JObject.Parse("{\"rotationDeg\": {\"gapToRuleDeg\": 90.0}}") });
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(list, changeRules: rules => Yard(rules, new JArray(new JArray(10.0, -8.0), new JArray(14.0, -8.0), new JArray(14.0, -2.0), new JArray(10.0, -2.0))));
            PlotPlan five = built.Plans["5"];
            Assert.True(five.RampOnOpenEdge);
            // By hand: turned 90 degrees the yard lies x 52.005 to 58.005 and y 10.155 to 14.155, far behind the ramp of the front, y 0 to 5.52. The ramp on the
            // open edge covers x 54.48 to 60, so the cut stops the yard at 54.48
            Assert.Equal("52.005,10.155 54.48,10.155 54.48,14.155 52.005,14.155", Text((List<List<double[]>>)five.Overrides["yardOutline"]));
        }

        [Fact]
        public void R475_a_yard_the_cut_leaves_empty_is_the_no_yard_path_with_its_line()
        {
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(rules => Yard(rules, Outside()));
            Dictionary<string, object> five = made.Floors["5"];
            Assert.Null(five["yardOutline"]);
            Assert.Null(five["yardType"]);
            Assert.Equal("DP2", (string)five["yardUnit"]);
            Assert.Equal(12.0, (double)five["yardWidth"]);
            Assert.Equal("[{\"key\":\"floors.yard\",\"why\":\"" + FloorOutlines.YardCutEmpty + "\"}]", ((JToken)five["lacks"]).ToString(Newtonsoft.Json.Formatting.None));

            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(new OverrideList(new Dictionary<string, JObject>()), changeRules: rules => Yard(rules, Outside()));
            PlotPlan plan = built.Plans["5"];
            Assert.Null(plan.Overrides["yardOutline"]);
            Assert.Null(plan.Overrides["yardZ"]);
            Assert.Null(plan.YardType);
            Assert.Contains("floors.yard", plan.Lacks);
        }
    }
}
