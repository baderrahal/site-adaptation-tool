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
    // R415 and R416, round 4j-2, Manual Rev 06 2.8.A. The irregular plot by the tolerance of the rules profile and the boundary wall of an
    // irregular plot with its outer face on the plot line under rev6, the C# side of python/tests/test_r415_r416_irregular_walls.py on the same
    // made up faces and values
    public sealed class R415_IrregularWallTests
    {
        private static double[][] Points(params double[] values)
        {
            return Enumerable.Range(0, values.Length / 2).Select(index => new[] { values[2 * index], values[2 * index + 1] }).ToArray();
        }

        private static readonly double[][] Rectangle = Points(0, 0, 12, 0, 12, 25, 0, 25);

        // A corner plot cut by a chamfer of 3 m at 45 degrees, and a plot notched 4 m by 5 m at its back right corner
        private static readonly double[][] Chamfer = Points(0, 0, 12, 0, 12, 22, 9, 25, 0, 25);

        private static readonly double[][] Notch = Points(0, 0, 12, 0, 12, 20, 8, 20, 8, 25, 0, 25);

        // A rectangle with a vertex on its side that runs on within half a degree and a repeated vertex, both merged
        private static readonly double[][] Bent = Points(0, 0, 6, 0.02, 12, 0, 12, 25, 12, 25, 0, 25);

        // The back edge off by degrees, its corners 90 less and 90 plus that
        private static double[][] Off(double degrees)
        {
            return Points(0, 0, 12, 0, 12, 25, 0, 25 + 12 * Math.Tan(degrees * (Math.PI / 180.0)));
        }

        private static JObject Rules()
        {
            return JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules()));
        }

        private static PlanWall Wall(string plot, int edge, string plots, params double[] ends)
        {
            return new PlanWall { Plot = plot, Edge = edge, Plots = plots.Split('|').ToList(), Ends = Points(ends) };
        }

        private static string Ends(PlanWall wall)
        {
            return string.Join(" ", wall.Ends.SelectMany(point => point).Select(value => Math.Round(value, 6).ToString("R", CultureInfo.InvariantCulture)));
        }

        [Fact]
        public void R415_a_rectangle_and_one_off_by_1_degree_are_regular_a_chamfer_and_a_notch_irregular()
        {
            Assert.Equal(new[] { false, false, false, true, true, true }, new[] { Rectangle, Off(1.0), Bent, Chamfer, Notch, Off(3.0) }.Select(ring => Walls.Irregular(ring, 2.0)).ToArray());
            Assert.Equal(new[] { 4, 4, 4, 5, 6 }, new[] { Rectangle, Off(1.0), Bent, Chamfer, Notch }.Select(ring => Walls.Corners(ring, 2.0).Count).ToArray());
            // The ring drawn the other way round gives the same
            Assert.Equal(new[] { false, false, true, true }, new[] { Rectangle, Off(1.0), Chamfer, Notch }.Select(ring => Walls.Irregular(ring.Reverse().ToArray(), 2.0)).ToArray());
        }

        [Fact]
        public void R415_the_tolerance_is_read_from_the_rules_profile_and_a_value_that_is_no_angle_refused()
        {
            JObject rules = Rules();
            Assert.Equal(2.0, (double)rules["rev6"]["irregular_tolerance_deg"]);
            WallLine line = Walls.LineRules(rules, "roshn-makkah.json");
            Assert.Equal("2 0.1", line.Tolerance.ToString(CultureInfo.InvariantCulture) + " " + line.Half.ToString(CultureInfo.InvariantCulture));
            Assert.True(Walls.Irregular(Off(3.0), line.Tolerance));
            rules["rev6"]["irregular_tolerance_deg"] = 4.0;
            WallLine wider = Walls.LineRules(rules, "roshn-makkah.json");
            Assert.False(Walls.Irregular(Off(3.0), wider.Tolerance));
            foreach (JToken wrong in new JToken[] { "2", 0, 45, JValue.CreateNull() })
            {
                rules["rev6"]["irregular_tolerance_deg"] = wrong;
                InvalidDataException error = Assert.Throws<InvalidDataException>(() => Walls.LineRules(rules, "roshn-makkah.json"));
                Assert.Equal("rev6.irregular_tolerance_deg is no number over 0 and under 45, a build under the Rev 6 mode tells no irregular plot without it (R415)", error.Message);
            }
        }

        [Fact]
        public void R415_the_plots_of_the_scope_with_a_face_each_regular_or_irregular()
        {
            var rings = new Dictionary<string, double[][]> { ["1"] = Rectangle, ["2"] = Chamfer, ["9"] = Notch };
            Assert.Equal("1 False, 2 True", string.Join(", ", Walls.IrregularOf(new[] { "1", "2", "3" }, rings, 2.0).Select(item => item.Key + " " + item.Value)));
        }

        [Fact]
        public void R416_the_wall_of_an_irregular_plot_runs_half_the_thickness_inside_it_a_regular_plot_keeps_the_centre_on_the_edge()
        {
            var rings = new Dictionary<string, double[][]> { ["1"] = Chamfer, ["2"] = Points(12, 0, 12, 25, 24, 25, 24, 0) };
            var walls = new List<PlanWall> { Wall("1", 1, "1|2", 12, 0, 12, 22), Wall("1", 2, "1", 12, 22, 9, 25), Wall("2", 2, "2", 24, 25, 24, 0) };
            Walls.Inside(walls, rings, new HashSet<string> { "1", "2" }, new Dictionary<string, bool> { ["1"] = true, ["2"] = false }, 0.1);
            Assert.Equal(new double?[] { 0.1, 0.1, 0.0 }, walls.Select(wall => wall.LineInsideM).ToArray());
            Assert.Equal("11.9 0 11.9 22", Ends(walls[0]));
            double step = 0.1 / Math.Sqrt(2.0);
            Assert.Equal(string.Join(" ", new[] { 12.0 - step, 22.0 - step, 9.0 - step, 25.0 - step }.Select(value => Math.Round(value, 6).ToString("R", CultureInfo.InvariantCulture))), Ends(walls[1]));
            Assert.Equal("24 25 24 0", Ends(walls[2]));
        }

        [Fact]
        public void R416_a_shared_edge_takes_the_inside_of_the_one_irregular_plot_and_of_the_plot_it_is_filed_under_when_both_are()
        {
            var rings = new Dictionary<string, double[][]> { ["1"] = Rectangle, ["2"] = Points(12, 0, 24, 0, 24, 20, 20, 20, 20, 25, 12, 25) };
            var both = new HashSet<string> { "1", "2" };
            Func<HashSet<string>, bool, bool, string> moved = (written, one, two) =>
            {
                var walls = new List<PlanWall> { Wall("1", 1, "1|2", 12, 0, 12, 25) };
                Walls.Inside(walls, rings, written, new Dictionary<string, bool> { ["1"] = one, ["2"] = two }, 0.1);
                return Ends(walls[0]) + " " + walls[0].LineInsideM.Value.ToString(CultureInfo.InvariantCulture);
            };
            // 1 regular, 2 irregular: the wall filed under 1 moves into 2. Both irregular: written once as today, inside 1 it is filed under
            Assert.Equal("12.1 0 12.1 25 0.1", moved(both, false, true));
            Assert.Equal("11.9 0 11.9 25 0.1", moved(both, true, true));
            // An irregular neighbour the build does not write pulls no wall
            Assert.Equal("12 0 12 25 0", moved(new HashSet<string> { "1" }, false, true));
        }

        [Fact]
        public void R416_the_step_walls_gives_irregular_and_line_inside_under_rev6_and_null_under_the_past_job()
        {
            var plans = new Dictionary<string, PlotPlan>();
            var walls = new List<PlanWall> { Wall("3", 1, "3", 0, 0, 1, 0), Wall("3", 2, "3|4", 1, 0, 1, 1), Wall("4", 1, "4", 2, 0, 2, 1) };
            walls[0].LineInsideM = 0.0;
            walls[1].LineInsideM = 0.1;
            walls[2].LineInsideM = 0.1;
            foreach (PlanWall wall in walls)
            {
                foreach (string field in Walls.Fields)
                {
                    wall.Status[field] = new[] { "keyRules", "UNKNOWN", "walls." + field + " from keyRules" };
                }
            }
            var irregular = new Dictionary<string, bool> { ["3"] = false, ["4"] = true };
            Dictionary<string, Dictionary<string, object>> step = Walls.Step(new[] { "3", "4", "7" }, plans, walls, irregular);
            Assert.Equal("False True null", string.Join(" ", new[] { "3", "4", "7" }.Select(number => step[number]["irregular"]?.ToString() ?? "null")));
            Assert.Equal("0 0.1 | 0.1 | ", string.Join(" | ", new[] { "3", "4", "7" }.Select(number => string.Join(" ", ((List<double>)step[number]["lineInsideM"]).Select(value => value.ToString(CultureInfo.InvariantCulture))))));
            Dictionary<string, Dictionary<string, object>> past = Walls.Step(new[] { "3", "4", "7" }, plans, walls);
            Assert.True(past.Values.All(plot => plot["irregular"] == null && plot["lineInsideM"] == null));
            Assert.True(step.Values.All(plot => string.Join(" ", plot.Keys) == string.Join(" ", Walls.StepFields)));
        }

        // DesignBasisTests.Rev6Request: the worker request of LevelFloorStepTests on the design basis rev6 by change, the one road surface of config
        private static WorkerRequest Rev6Request(LevelFloorStepTests steps, string runId, params string[] names)
        {
            WorkerRequest request = steps.Request(runId, names);
            WorkerFile settings = request.Inputs.Single(input => input.Role == "config");
            JObject config = JObject.Parse(File.ReadAllText(settings.Path));
            string surface = (string)config["siteBuild"]["roadSurfaces"][0];
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["designBasis"]["roadLevel"]["surface"] = surface;
            File.WriteAllText(settings.Path, config.ToString());
            settings.Sha256 = FileHash.Sha256(settings.Path);
            request.Inputs.RemoveAll(input => input.Role.StartsWith(WorkerRunner.RoadSurfaceRole, StringComparison.Ordinal) && input.Role != WorkerRunner.RoadSurfaceRole + surface);
            string floors = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "floor_outlines.json");
            request.Inputs.Add(new WorkerFile { Role = BuildSteps.BandsRole, Path = floors, Sha256 = FileHash.Sha256(floors) });
            return request;
        }

        // The override list a walls step reads, the one of WallTests, which must hold an override of its source
        private static void Overrides(WorkerRequest request)
        {
            string list = Path.Combine(request.OutputFolder, "overrides.json");
            File.WriteAllText(list, new JObject { ["overrides"] = new JArray(new JObject { ["plot"] = "3", ["field"] = "heightM", ["value"] = JObject.Parse("{\"gapToRuleM\": 0.1}"),
                ["source"] = "parsons", ["model"] = "a.rvt", ["key"] = "imc" }) }.ToString());
            request.Inputs.Add(new WorkerFile { Role = "overrides", Path = list, Sha256 = FileHash.Sha256(list) });
        }

        private static void Ran(WorkerRequest request, string runId)
        {
            Overrides(request);
            int code = WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0);
            Assert.True(code == 0, code + " " + WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, runId)).Message);
        }

        [Fact]
        public void R416_the_worker_build_writes_irregular_and_line_inside_under_rev6_and_null_under_the_past_job()
        {
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest past = steps.Request("i1", Walls.StepName);
                Ran(past, "i1");
                JObject made = RunStamp.Read(Path.Combine(past.StepsFolder, "step_walls_csharp.json"));
                Assert.True(((JObject)made["plots"]).Properties().All(plot => plot.Value["raw"]["irregular"].Type == JTokenType.Null && plot.Value["raw"]["lineInsideM"].Type == JTokenType.Null));
                WorkerRequest request = Rev6Request(steps, "i2", Walls.StepName);
                Ran(request, "i2");
                JObject rev6 = RunStamp.Read(Path.Combine(request.StepsFolder, "step_walls_csharp.json"));
                // The made up row holds rectangles alone, so each plot is regular and each wall keeps its centre on the edge, its ends those of the past job
                Assert.True(((JObject)rev6["plots"]).Properties().All(plot => (bool)plot.Value["raw"]["irregular"] == false
                    && plot.Value["raw"]["lineInsideM"].All(value => (double)value == 0.0)));
                Assert.Equal(made["plots"]["5"]["raw"]["ends"].ToString(), rev6["plots"]["5"]["raw"]["ends"].ToString());
                Assert.Equal(((JArray)rev6["plots"]["5"]["raw"]["ends"]).Count, ((JArray)rev6["plots"]["5"]["raw"]["lineInsideM"]).Count);
            }
        }

        [Fact]
        public void R416_the_walls_of_a_build_move_inside_an_irregular_plot_and_stay_on_the_edge_without_the_rev6_line()
        {
            // The row of OverrideParameterStepTests, plot 4 on x 36 to 48 taken as irregular, as test_r415_r416_irregular_walls takes it in Python
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made());
            WallRules rules = Walls.Rules((JObject)built.Rules["levelProfiles"]["parsons2024Rev6"]["keyRules"], (JObject)built.Rules["levelProfiles"]["parsons2024Rev6"],
                "parsons2024Rev6", built.Rules, "roshn-makkah.json", built.Config["keyPlans"]["wallEdgeM"]);
            List<string> scope = built.Plans.Keys.ToList();
            List<PlanWall> past = Walls.OfPlans(scope, built.Plans, built.Faces, built.Fronts, rules);
            WallLine line = Walls.LineRules(built.Rules, "roshn-makkah.json");
            Dictionary<string, bool> irregular = Walls.IrregularOf(scope, Walls.RingsOf(built.Faces), line.Tolerance);
            Assert.True(irregular.Values.All(odd => !odd));
            irregular["4"] = true;
            List<PlanWall> rev6 = Walls.OfPlans(scope, built.Plans, built.Faces, built.Fronts, rules, null, line, irregular);
            Assert.Null(past[0].LineInsideM);
            Assert.Equal(past.Select(wall => wall.Plot + " " + wall.Edge), rev6.Select(wall => wall.Plot + " " + wall.Edge));
            Func<PlanWall, PlanWall, string> moved = (before, after) => before.Plot + " " + before.Edge + " "
                + string.Join(" ", before.Ends.Zip(after.Ends, (one, two) => Math.Round(two[0] - one[0], 6).ToString(CultureInfo.InvariantCulture) + "," + Math.Round(two[1] - one[1], 6).ToString(CultureInfo.InvariantCulture)));
            // The wall 3 files on the edge it shares with 4 moves into 4, the walls of 4 into 4, the rest stays on the edge
            Assert.Equal(new[] { "3 2 0.1,0 0.1,0", "4 1 0,-0.1 0,-0.1", "4 2 -0.1,0 -0.1,0" },
                past.Zip(rev6, moved).Where(text => text.Contains("0.1")).ToArray());
            Assert.Equal("0 0 0 0 0 0 0.1 0.1 0.1 0 0 0 0",string.Join(" ", rev6.Select(wall => wall.LineInsideM.Value.ToString(CultureInfo.InvariantCulture))));
        }

        [Fact]
        public void R416_the_parity_fields_of_the_step_walls_hold_irregular_and_line_inside()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal(string.Join(" ", Walls.StepFields), string.Join(" ", config.StepFields(Walls.StepName).Select(field => field.Name)));
            Assert.Equal("irregular:noGeometry lineInsideM:levels3", string.Join(" ", config.StepFields(Walls.StepName).Where(field => field.Name == "irregular" || field.Name == "lineInsideM")
                .Select(field => field.Name + ":" + field.Kind + field.Decimals)));
        }
    }
}
