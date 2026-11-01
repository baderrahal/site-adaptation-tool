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
            return Text(wall.Ends.SelectMany(point => point).ToArray());
        }

        private static string Text(params double[] values)
        {
            return string.Join(" ", values.Select(value => Math.Round(value, 6).ToString("R", CultureInfo.InvariantCulture)));
        }

        // keyPlans.wallEdgeM of a made up build, within which two ends of walls of two faces are one point
        private const double Near = 0.05;

        private static List<PlanWall> RingWalls(string plot, double[][] ring, int from)
        {
            return Enumerable.Range(from, ring.Length - from).Select(index => new PlanWall { Plot = plot, Edge = index, Plots = new List<string> { plot },
                Ends = new[] { (double[])ring[index].Clone(), (double[])ring[(index + 1) % ring.Length].Clone() } }).ToList();
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
            Walls.Inside(walls, rings, new HashSet<string> { "1", "2" }, new Dictionary<string, bool> { ["1"] = true, ["2"] = false }, 0.1, 2.0, Near);
            Assert.Equal(new double?[] { 0.1, 0.1, 0.0 }, walls.Select(wall => wall.LineInsideM).ToArray());
            // The two moved walls meet at the vertex 12, 22 again, at the mitre of their lines. The end at 9, 25 has no wall on the next edge, it stays square
            double step = 0.1 / Math.Sqrt(2.0);
            double mitre = 34.0 - 2.0 * step - 11.9;
            Assert.Equal(Text(11.9, 0, 11.9, mitre), Ends(walls[0]));
            Assert.Equal(Text(11.9, mitre, 9.0 - step, 25.0 - step), Ends(walls[1]));
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
                Walls.Inside(walls, rings, written, new Dictionary<string, bool> { ["1"] = one, ["2"] = two }, 0.1, 2.0, Near);
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
                WorkerRequest past = steps.Request("i1", Walls.StepName, KeyLevels.StepName);
                Ran(past, "i1");
                JObject made = RunStamp.Read(Path.Combine(past.StepsFolder, "step_walls_csharp.json"));
                Assert.True(((JObject)made["plots"]).Properties().All(plot => plot.Value["raw"]["irregular"].Type == JTokenType.Null && plot.Value["raw"]["lineInsideM"].Type == JTokenType.Null));
                // Under the past job no plot of the build holds a split test of R414 either
                JObject levels = RunStamp.Read(Path.Combine(past.StepsFolder, "step_levels_csharp.json"));
                Assert.Equal(((JObject)made["plots"]).Count, ((JObject)levels["plots"]).Count);
                Assert.True(((JObject)levels["plots"]).Properties().All(plot => plot.Value["raw"]["splitTest"].Type == JTokenType.Null));
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
            // The wall 3 files on the edge it shares with 4 moves into 4, the walls of 4 into 4, the rest stays on the edge. The back corners of 4
            // close at the mitre of the moved lines, the front ends next to the open front stay square
            Func<PlanWall, string> points = wall => wall.Plot + " " + wall.Edge + " " + string.Join(" ", wall.Ends.Select(end => Text(end[0], end[1]).Replace(' ', ','))
                .OrderBy(text => text, StringComparer.Ordinal));
            Assert.Equal(new[] { "3 2 36.1,0 36.1,24.9", "4 1 36.1,24.9 47.9,24.9", "4 2 47.9,0 47.9,24.9" },
                past.Zip(rev6, (before, after) => Ends(before) == Ends(after) ? null : points(after)).Where(text => text != null).ToArray());
            Assert.Equal("0 0 0 0 0 0 0.1 0.1 0.1 0 0 0 0", string.Join(" ", rev6.Select(wall => wall.LineInsideM.Value.ToString(CultureInfo.InvariantCulture))));
            // The ends of a moved wall take the status of R416, every other value that of the past job byte for byte
            ValueStatusTable table = ValueStatus.Table(built.Config, built.Rules, "parsons2024Rev6");
            foreach (PlanWall wall in past.Concat(rev6))
            {
                Walls.Mark(wall, table, built.Plans);
            }
            Assert.Equal(new[] { "3 2", "4 1", "4 2" }, rev6.Where(wall => wall.Status["ends"][0] == "manualInside").Select(wall => wall.Plot + " " + wall.Edge).ToArray());
            Assert.True(past.Zip(rev6, (before, after) => after.Status["ends"][0] == "manualInside"
                || Walls.Fields.All(field => string.Join("|", before.Status[field]) == string.Join("|", after.Status[field]) && Ends(before) == Ends(after))).All(same => same));
            Assert.Equal("walls.ends from manualInside", rev6.First(wall => wall.LineInsideM > 0).Status["ends"][2]);
        }

        [Fact]
        public void R416_an_l_shaped_clockwise_plot_with_a_wall_on_each_edge_closes_at_every_corner_reflex_and_convex()
        {
            // The L of 10 by 10 less its 4 by 6 back right part, drawn clockwise, its reflex corner at 6, 4
            double[][] ring = Points(0, 0, 0, 10, 6, 10, 6, 4, 10, 4, 10, 0);
            List<PlanWall> walls = RingWalls("1", ring, 0);
            Walls.Inside(walls, new Dictionary<string, double[][]> { ["1"] = ring }, new HashSet<string> { "1" }, new Dictionary<string, bool> { ["1"] = true }, 0.1, 2.0, Near);
            double[][] inner = Points(0.1, 0.1, 0.1, 9.9, 5.9, 9.9, 5.9, 3.9, 9.9, 3.9, 9.9, 0.1);
            Assert.Equal(Enumerable.Range(0, inner.Length).Select(index => Text(inner[index][0], inner[index][1], inner[(index + 1) % inner.Length][0], inner[(index + 1) % inner.Length][1])),
                walls.Select(Ends));
            // Each corner, the reflex one too, holds one end of two walls, so no square is left unbuilt and none built twice
            Assert.True(Enumerable.Range(0, walls.Count).All(index => walls[index].Ends[1].SequenceEqual(walls[(index + 1) % walls.Count].Ends[0])));
        }

        [Fact]
        public void R416_a_chamfered_plot_closes_at_its_chamfer_and_keeps_square_ends_next_to_its_open_front()
        {
            // Chamfer anticlockwise, its front edge 0 open, a wall on each other edge
            List<PlanWall> walls = RingWalls("1", Chamfer, 1);
            Walls.Inside(walls, new Dictionary<string, double[][]> { ["1"] = Chamfer }, new HashSet<string> { "1" }, new Dictionary<string, bool> { ["1"] = true }, 0.1, 2.0, Near);
            // The chamfer line x + y = 34 moved 0.1 inside is x + y = 34 less 0.1 times the root of 2
            double line = 34.0 - 0.1 * Math.Sqrt(2.0);
            Assert.Equal(new[] { Text(11.9, 0, 11.9, line - 11.9), Text(11.9, line - 11.9, line - 24.9, 24.9), Text(line - 24.9, 24.9, 0.1, 24.9), Text(0.1, 24.9, 0.1, 0) },
                walls.Select(Ends));
        }

        [Fact]
        public void R416_an_end_mid_edge_a_collinear_vertex_and_a_wall_that_meets_mid_edge_keep_square_ends()
        {
            // 1 a 12 by 25 plot with a vertex on its back edge that runs on, its side x 12 parted at y 10 where 2 and 3 beside it meet. All three taken
            // irregular, the wall of 2 and 3 filed under 2 meets the side of 1 mid edge
            var rings = new Dictionary<string, double[][]> { ["1"] = Points(0, 0, 12, 0, 12, 25, 6, 25, 0, 25), ["2"] = Points(12, 0, 24, 0, 24, 10, 12, 10),
                ["3"] = Points(12, 10, 24, 10, 24, 25, 12, 25) };
            var walls = new List<PlanWall> { Wall("1", 1, "1|2", 12, 0, 12, 10), Wall("1", 1, "1|3", 12, 10, 12, 25), Wall("1", 2, "1", 12, 25, 6, 25), Wall("1", 3, "1", 6, 25, 0, 25),
                Wall("1", 4, "1", 0, 25, 0, 0), Wall("2", 2, "2|3", 24, 10, 12, 10) };
            Walls.Inside(walls, rings, new HashSet<string> { "1", "2", "3" }, new Dictionary<string, bool> { ["1"] = true, ["2"] = true, ["3"] = true }, 0.1, 2.0, Near);
            Assert.Equal(new[] { "11.9 0 11.9 10", "11.9 10 11.9 24.9", "11.9 24.9 6 24.9", "6 24.9 0.1 24.9", "0.1 24.9 0.1 0", "24 9.9 12 9.9" }, walls.Select(Ends));
        }

        [Fact]
        public void R416_a_moved_wall_names_r416_and_the_manual_in_its_status_an_unmoved_wall_keeps_the_status_of_today()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            ValueStatusTable table = ValueStatus.Table(config, Rules(), "parsons2024Rev6");
            var marked = new Dictionary<string, PlanWall>();
            foreach (KeyValuePair<string, double?> line in new[] { new KeyValuePair<string, double?>("past", null), new KeyValuePair<string, double?>("regular", 0.0), new KeyValuePair<string, double?>("moved", 0.1) })
            {
                PlanWall wall = Wall("1", 1, "1", 11.9, 0, 11.9, 25);
                wall.Base = 9.5;
                wall.Top = 12.4;
                wall.BasePlot = "1";
                wall.TopPlot = "1";
                wall.LineInsideM = line.Value;
                Walls.Mark(wall, table, new Dictionary<string, PlotPlan>());
                marked[line.Key] = wall;
            }
            Func<PlanWall, string, string> status = (wall, field) => string.Join("|", wall.Status[field]);
            Assert.True(Walls.Fields.All(field => status(marked["past"], field) == status(marked["regular"], field)));
            Assert.Equal("keyRules|UNKNOWN|walls.ends from keyRules", status(marked["regular"], "ends"));
            Assert.Equal("manualInside|UNKNOWN|walls.ends from manualInside", status(marked["moved"], "ends"));
            Assert.True(new[] { "plots", "base", "top" }.All(field => status(marked["moved"], field) == status(marked["regular"], field)));
            JArray entries = (JArray)table.Used["walls.ends from manualInside"]["entries"];
            Assert.Contains("R416", (string)entries[0]["document"]);
            Assert.Contains("Rev 06", (string)entries[0]["document"]);
            Assert.Equal("2.8.A", (string)entries[0]["pageOrSheet"]);
            List<string> named = entries.Select(entry => ((string)entry["entry"] ?? "").Split(new[] { ' ' }, 2).Last()).ToList();
            Assert.Contains("rev6.irregular_tolerance_deg", named);
            Assert.Contains("plot.boundary_wall_thickness", named);
            Assert.DoesNotContain(named, name => name.Contains("wallLine"));
            Assert.Contains(((JArray)table.Used["walls.ends from keyRules"]["entries"]).Select(entry => (string)entry["entry"] ?? ""), name => name.Contains("wallLine"));
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
