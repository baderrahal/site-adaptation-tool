using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Q116, R355, R362. The ramp of a plot a rotationDeg override turned goes on the open edge the walls take, Fronts.OpenRamp and PlanOverrides
    // of Common, the C# side of python/tests/test_turned_ramp.py on the same made up faces, on net48 and net8.0
    public sealed class TurnedRampTests
    {
        private static readonly string[] Numbers = { "1", "2", "3", "4", "5", "6" };

        private static readonly BuildFace Face = BuildFace.Of("2", LevelFloorStepTests.Ring(12.0, 0.0, 24.0, 25.0));

        private static readonly FrontFrame Frame = new FrontFrame
        {
            N = new[] { 0.0, 1.0 }, R = new[] { 1.0, 0.0 }, Left = new[] { 12.0, 0.0 }, Right = new[] { 24.0, 0.0 }, Length = 12.0, Distance = 3.0, Tied = false,
        };

        private static KeyValuePair<double?, string> Everywhere(double x, double y)
        {
            return new KeyValuePair<double?, string>(10.0 + x / 100.0, "road");
        }

        private static string Points(IEnumerable<double[]> points)
        {
            return string.Join(" ", points.Select(point => string.Join(",", point.Select(value => (Rounding.Fixed(value, 3) + 0.0).ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        }

        // test_turned_ramp.test_q116_r355_open_ramp_builds_the_frame_ramp_and_road_levels_on_the_open_edge_a_turn_faces
        [Fact]
        public void Q116_R355_open_ramp_builds_the_frame_ramp_and_road_levels_on_the_open_edge_a_turn_faces()
        {
            FrontItem turned = Fronts.OpenRamp(Face, Frame, 180.001, false, 5.52, Everywhere);
            Assert.Equal("24,25 12,25", Points(new[] { turned.Frame.Left, turned.Frame.Right }));
            Assert.Equal("0,-1 12", Points(new[] { turned.Frame.N }) + " " + turned.Frame.Length);
            Assert.Equal("24,25 12,25 12,19.48 24,19.48", Points(turned.Corners));
            Assert.Equal("10.24 10.12 road road", string.Join(" ", turned.Front.Select(z => Rounding.Fixed(z, 3))) + " " + string.Join(" ", turned.Surfaces));
            Assert.Null(Fronts.OpenRamp(Face, Frame, 0.0, false, 5.52, Everywhere));
            Assert.Null(Fronts.OpenRamp(Face, Frame, 1.5, false, 5.52, Everywhere));
            Assert.Null(Fronts.OpenRamp(Face, Frame, 180.0, true, 5.52, Everywhere));
            Assert.Equal("24,25", Points(new[] { Fronts.OpenRamp(Face, Frame, 0.0, true, 5.52, Everywhere).Frame.Left }));
            Assert.Null(Fronts.OpenRamp(null, Frame, 180.0, false, 5.52, Everywhere));
            Assert.Null(Fronts.OpenRamp(Face, null, 180.0, false, 5.52, Everywhere));
            Assert.Null(Fronts.OpenRamp(Face, Frame, 180.0, false, null, Everywhere));
        }

        // test_turned_ramp.test_q116_r355_open_ramp_with_no_road_level_at_an_open_corner_gives_no_front
        [Fact]
        public void Q116_R355_open_ramp_with_no_road_level_at_an_open_corner_gives_no_front()
        {
            FrontItem turned = Fronts.OpenRamp(Face, Frame, 180.0, false, 5.52, (x, y) => y < 20.0 ? Everywhere(x, y) : new KeyValuePair<double?, string>(null, null));
            Assert.Null(turned.Front);
            Assert.Equal(new string[] { null, null }, turned.Surfaces);
            Assert.Equal("24,25", Points(new[] { turned.Corners[0] }));
        }

        // test_turned_ramp.test_q116_r362_a_turned_plot_takes_its_ramp_on_the_open_edge_and_leaves_the_conflicts
        [Fact]
        public void Q116_R362_a_turned_plot_takes_its_ramp_on_the_open_edge_and_leaves_the_conflicts()
        {
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made());
            PlotPlan plan = built.Plans["2"];
            Assert.True(plan.RampOnOpenEdge);
            Assert.Equal("24,25 12,25 12,19.48 24,19.48", Points((double[][])plan.Overrides["rampOutline"]));
            double[] heights = (double[])plan.Overrides["rampZ"];
            Assert.Equal("10.24 10.12", string.Join(" ", heights.Take(2).Select(z => Rounding.Fixed(z, 3))));
            Assert.Equal(0.12, Rounding.Fixed(heights[3] - heights[2], 3));
            Assert.Empty(PlanSummary.Conflicts(Numbers, built.Plans, built.Build.Fronts, built.Faces));
            Assert.False(built.Plans["6"].RampOnOpenEdge);
            Assert.Equal(Points(built.Build.Fronts["6"].Corners), Points((double[][])built.Plans["6"].Overrides["rampOutline"]));
            Assert.Null(built.Plans["7"].Overrides["rampOutline"]);
        }

        // test_turned_ramp.test_q116_r362_a_turned_plot_with_no_road_level_on_its_open_edge_keeps_its_ramp_on_the_front_and_stays_a_conflict
        [Fact]
        public void Q116_R362_a_turned_plot_with_no_road_level_on_its_open_edge_keeps_its_ramp_on_the_front_and_stays_a_conflict()
        {
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made(), backDry: true);
            Assert.False(built.Plans["2"].RampOnOpenEdge);
            Assert.Equal("12,0 24,0 24,5.52 12,5.52", Points((double[][])built.Plans["2"].Overrides["rampOutline"]));
            Assert.Equal("2", string.Join(" ", PlanSummary.Conflicts(Numbers, built.Plans, built.Build.Fronts, built.Faces).Select(item => (string)item["plot"])));
        }

        private static string From(OverrideParameterStepTests.Built built, string number, string field)
        {
            ValueStatusTable table = ValueStatus.Table(built.Config, built.Rules, "parsons2024Rev6");
            Dictionary<string, Dictionary<string, object>> status = ValueStatus.Step(Numbers, built.Plans, built.Hands, table);
            return (string)((Newtonsoft.Json.Linq.JObject)status[number]["fields"])[field]["statusFrom"];
        }

        // test_turned_ramp.test_q116_r401_the_ramp_a_turn_moved_carries_the_status_of_the_override_and_a_ramp_it_left_does_not
        [Fact]
        public void Q116_R401_the_ramp_a_turn_moved_carries_the_status_of_the_override_and_a_ramp_it_left_does_not()
        {
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made());
            Assert.All(new[] { "floors.ramp.outline", "floors.ramp.corners", "floors.ramp.heightOffset" }, field => Assert.Contains("rotationDeg", From(built, "2", field)));
            Assert.DoesNotContain("rotationDeg", From(built, "6", "floors.ramp.outline"));
            OverrideParameterStepTests.Built dry = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made(), backDry: true);
            Assert.All(new[] { "floors.ramp.outline", "floors.ramp.corners", "floors.ramp.heightOffset" }, field => Assert.DoesNotContain("rotationDeg", From(dry, "2", field)));
        }
    }
}
