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
    // R362. The boundary walls of a build, Walls of Common, the C# side of python/tests/test_plan_walls.py on the same made up faces and values,
    // so the two sides give one wall list, on net48 and net8.0
    public sealed class WallTests
    {
        private static readonly WallRules Higher = new WallRules { HigherBase = true, HigherTop = true, Depth = 0.1, Rise = 1.7, Near = 0.3 };

        // test_plan_walls.RINGS, FRAMES and FFLS: plots 1, 2 and 5 written, 3 and 6 faces the plan does not write
        private static readonly Dictionary<string, double[][]> Rings = new Dictionary<string, double[][]>
        {
            ["1"] = Points(0, 0, 10, 0, 10, 20, 0, 20), ["2"] = Points(10, 0, 20, 0, 20, 20, 10, 20), ["3"] = Points(0, 20, 20, 20, 20, 30, 0, 30),
            ["5"] = Points(20, 0, 30, 0, 30, 8, 20, 8), ["6"] = Points(20, 8, 30, 8, 30, 20, 20, 20),
        };

        private static readonly Dictionary<string, double[][]> Frames = new Dictionary<string, double[][]>
        {
            ["1"] = Points(0, 0, 10, 0), ["2"] = Points(20, 0, 10, 0), ["5"] = Points(20, 0, 30, 0),
        };

        private static readonly Dictionary<string, double> Ffls = new Dictionary<string, double> { ["1"] = 100.0, ["2"] = 100.5, ["5"] = 101.0 };

        private static double[][] Points(params double[] values)
        {
            return Enumerable.Range(0, values.Length / 2).Select(index => new[] { values[2 * index], values[2 * index + 1] }).ToArray();
        }

        private static List<PlanWall> Made(Dictionary<string, double[][]> rings = null, Dictionary<string, double[][]> frames = null, Dictionary<string, double> ffls = null,
            string[] order = null, WallRules rules = null)
        {
            ffls = ffls ?? Ffls;
            return Walls.Of(order ?? new[] { "1", "2", "5" }, new HashSet<string>(ffls.Keys), rings ?? Rings, frames ?? Frames, ffls, rules ?? Higher);
        }

        private static string Text(PlanWall wall)
        {
            return wall.Plot + " " + wall.Edge + " " + string.Join("|", wall.Plots) + " " + string.Join(" ", wall.Ends.SelectMany(point => point).Select(value => value.ToString("R", CultureInfo.InvariantCulture)))
                + " " + wall.BasePlot + " " + wall.TopPlot;
        }

        [Fact]
        public void R362_walls_along_every_edge_but_the_front_one_wall_per_shared_edge_from_the_first_plot_that_writes_it()
        {
            List<PlanWall> walls = Made();
            Assert.Equal(new[]
            {
                "1 1 1|2 10 0 10 20 2 2", "1 2 1|3 10 20 0 20 1 1", "1 3 1 0 20 0 0 1 1",
                // The side of 2 parts at the corner of 5 and 6, the part on 5 at the yard of 5, the higher plot
                "2 1 2|5 20 0 20 8 5 5", "2 1 2|6 20 8 20 20 2 2", "2 2 2|3 20 20 10 20 2 2", "5 1 5 30 0 30 8 5 5", "5 2 5|6 30 8 20 8 5 5",
            }, walls.Select(Text).ToArray());
            Assert.Equal(new[] { 100.5 - 0.1, 100.0 - 0.1, 100.0 - 0.1, 101.0 - 0.1, 100.5 - 0.1, 100.5 - 0.1, 101.0 - 0.1, 101.0 - 0.1 }, walls.Select(wall => wall.Base).ToArray());
            Assert.Equal(new[] { 100.5 + 1.7, 100.0 + 1.7, 100.0 + 1.7, 101.0 + 1.7, 100.5 + 1.7, 100.5 + 1.7, 101.0 + 1.7, 101.0 + 1.7 }, walls.Select(wall => wall.Top).ToArray());
        }

        [Fact]
        public void R362_the_lower_plot_rules_take_the_yard_and_the_ffl_of_the_lower_plot()
        {
            PlanWall first = Made(rules: new WallRules { HigherBase = false, HigherTop = false, Depth = 0.1, Rise = 1.7, Near = 0.3 })[0];
            Assert.Equal(new object[] { 100.0 - 0.1, 100.0 + 1.7, "1", "1" }, new object[] { first.Base, first.Top, first.BasePlot, first.TopPlot });
        }

        [Fact]
        public void R362_a_neighbour_end_within_the_edge_hold_of_a_corner_parts_no_wall()
        {
            var rings = new Dictionary<string, double[][]> { ["7"] = Points(0, 40, 10, 40, 10, 60, 0, 60), ["8"] = Points(10, 40.2, 20, 40.2, 20, 60, 10, 60) };
            List<PlanWall> walls = Made(rings, new Dictionary<string, double[][]> { ["7"] = Points(0, 40, 10, 40) }, new Dictionary<string, double> { ["7"] = 50.0 }, new[] { "7" });
            Assert.Equal(new[] { "7 1 7|8 10 40 10 60 7 7", "7 2 7 10 60 0 60 7 7", "7 3 7 0 60 0 40 7 7" }, walls.Select(Text).ToArray());
        }

        [Fact]
        public void R362_an_edge_on_the_front_of_a_written_neighbour_is_open()
        {
            var rings = new Dictionary<string, double[][]> { ["9"] = Points(0, 100, 10, 100, 10, 110, 0, 110), ["10"] = Points(0, 110, 10, 110, 10, 120, 0, 120) };
            var frames = new Dictionary<string, double[][]> { ["9"] = Points(0, 100, 10, 100), ["10"] = Points(10, 110, 0, 110) };
            List<PlanWall> walls = Made(rings, frames, new Dictionary<string, double> { ["9"] = 10.0, ["10"] = 11.0 }, new[] { "9", "10" });
            Assert.Equal("9 1, 9 3, 10 1, 10 2, 10 3", string.Join(", ", walls.Select(wall => wall.Plot + " " + wall.Edge)));
        }

        [Fact]
        public void R362_a_rule_the_build_does_not_place_walls_by_is_refused()
        {
            JObject rules = JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules()));
            JObject level = (JObject)rules["levelProfiles"]["parsons2024Rev6"];
            JObject key = (JObject)level["keyRules"];
            WallRules found = Walls.Rules(key, level, "parsons2024Rev6", rules, "roshn-makkah.json", new JValue(0.3));
            Assert.Equal(new object[] { true, true, 0.1, 1.7, 0.3 }, new object[] { found.HigherBase, found.HigherTop, found.Depth, found.Rise, found.Near });
            foreach (string[] pair in new[] { new[] { "wallLine", "centreOnTheEdge" }, new[] { "frontOpen", "all" }, new[] { "wallBase", "yardLevelOfTheHigherPlot or yardLevelOfTheLowerPlot" } })
            {
                var changed = (JObject)key.DeepClone();
                changed[pair[0]] = new JObject { ["rule"] = "other" };
                InvalidDataException error = Assert.Throws<InvalidDataException>(() => Walls.Rules(changed, level, "parsons2024Rev6", rules, "roshn-makkah.json", new JValue(0.3)));
                Assert.Equal("keyRules." + pair[0] + " of parsons2024Rev6 holds no rule a build places boundary walls by, it takes " + pair[1] + " (R362)", error.Message);
            }
            InvalidDataException text = Assert.Throws<InvalidDataException>(() => Walls.Rules(key, level, "parsons2024Rev6", rules, "roshn-makkah.json", new JValue("0.3")));
            Assert.Equal("keyPlans.wallEdgeM is no number of 0 or more, a build places no boundary wall without it (R362)", text.Message);
            // keyRules of none of the four wall rules places no wall, one of them alone is refused for the others
            var bare = (JObject)key.DeepClone();
            foreach (string name in new[] { "wallBase", "wallTop", "wallLine", "frontOpen" })
            {
                bare.Remove(name);
            }
            Assert.Null(Walls.Rules(bare, level, "parsons2024Rev6", rules, "roshn-makkah.json", new JValue(0.3)));
            bare["frontOpen"] = new JObject { ["rule"] = "all" };
            Assert.Throws<InvalidDataException>(() => Walls.Rules(bare, level, "parsons2024Rev6", rules, "roshn-makkah.json", new JValue(0.3)));
        }

        private static PlotPlan Plan(double ffl, params string[] applied)
        {
            return new PlotPlan { Overrides = new Dictionary<string, object> { ["written"] = true, ["ffl"] = ffl, ["applied"] = applied.ToList() }, AppliedOrder = applied.ToList() };
        }

        [Fact]
        public void R362_R401_each_wall_value_carries_the_status_of_its_entries_and_an_ffl_an_override_moved_names_it()
        {
            string configPath = WorkerRunTests.RepoConfig();
            JObject config = JObject.Parse(File.ReadAllText(configPath));
            JObject rules = JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules()));
            ValueStatusTable table = ValueStatus.Table(config, rules, "parsons2024Rev6");
            PlanWall wall = Made()[0];
            var plans = Ffls.ToDictionary(item => item.Key, item => Plan(item.Value));
            Walls.Mark(wall, table, plans, null, Higher);
            Assert.Equal("plots locationPlan DOCUMENT walls.plots from locationPlan, ends keyRules UNKNOWN walls.ends from keyRules, base keyRules MEASURED walls.base from keyRules, "
                + "top keyRules MEASURED walls.top from keyRules", string.Join(", ", Walls.Fields.Select(field => field + " " + wall.Status[field][0] + " " + wall.Status[field][1] + " " + wall.Status[field][2])));
            Assert.Equal("weak weak UNKNOWN", (string)table.Used["walls.base from keyRules"]["strength"] + " " + (string)table.Used["walls.top from keyRules"]["strength"] + " "
                + (string)table.Used["walls.ends from keyRules"]["entries"].Last["status"]);
            // A heightM override that moved the FFL of 2, the higher plot of the wall, gives its base and top the status of the override list
            plans["2"] = Plan(100.5, "heightM");
            Walls.Mark(wall, table, plans, new Dictionary<string, PlotPlan> { ["2"] = Plan(100.4) }, Higher);
            Assert.Equal("RECOMMENDED walls.base from override by heightM walls.top from override by heightM walls.ends from keyRules",
                wall.Status["base"][1] + " " + wall.Status["base"][2] + " " + wall.Status["top"][2] + " " + wall.Status["ends"][2]);
            // An override that left the FFL at its value by the rule moved no wall
            Walls.Mark(wall, table, plans, new Dictionary<string, PlotPlan> { ["2"] = Plan(100.5) }, Higher);
            Assert.Equal("walls.base from keyRules", wall.Status["base"][2]);
        }

        [Fact]
        public void R362_parity_walls_the_plan_places_on_the_made_up_row_after_the_overrides()
        {
            // test_plan_walls.test_r362_parity_walls_the_plan_places_on_the_made_up_row_after_the_overrides: the row of OverrideParameterStepTests, 3 its FFL
            // 10 cm up by heightM
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made());
            OverrideParameterStepTests.Built plain = OverrideParameterStepTests.Build(new OverrideList(new Dictionary<string, JObject>()));
            ValueStatusTable table = ValueStatus.Table(built.Config, built.Rules, "parsons2024Rev6");
            WallRules rules = Walls.Rules((JObject)built.Rules["levelProfiles"]["parsons2024Rev6"]["keyRules"], (JObject)built.Rules["levelProfiles"]["parsons2024Rev6"],
                "parsons2024Rev6", built.Rules, "roshn-makkah.json", built.Config["keyPlans"]["wallEdgeM"]);
            List<string> scope = built.Plans.Keys.ToList();
            List<PlanWall> walls = Walls.OfPlans(scope, built.Plans, built.Faces, built.Fronts, rules);
            foreach (PlanWall wall in walls)
            {
                Walls.Mark(wall, table, built.Plans, plain.Plans, rules);
            }
            Assert.Equal(new[]
            {
                "1 0 1 10.188 11.988 1", "1 1 1 10.188 11.988 1", "1 2 1|2 10.428 12.228 2", "2 2 2|3 10.528 12.328 3", "2 3 2 10.428 12.228 2", "3 1 3 10.528 12.328 3",
                "3 2 3|4 10.548 12.348 4", "4 1 4 10.548 12.348 4", "4 2 4|5 10.788 12.588 5", "5 1 5 10.788 12.588 5", "5 2 5|6 10.788 12.588 5", "6 1 6 10.788 12.588 6",
                "6 2 6 10.788 12.588 6",
            }, walls.Select(wall => wall.Plot + " " + wall.Edge + " " + string.Join("|", wall.Plots) + " " + Rounding.Fixed(wall.Base, 3).ToString(CultureInfo.InvariantCulture) + " "
                + Rounding.Fixed(wall.Top, 3).ToString(CultureInfo.InvariantCulture) + " " + wall.BasePlot).ToArray());
            Assert.Equal(new[] { "walls.base from override by heightM", "walls.base from override by heightM" }, walls.Where(wall => wall.BasePlot == "3").Select(wall => wall.Status["base"][2]).ToArray());
            // The rotation override of -179.999 turns the link of 2 to face its back edge, so that edge is open and the front of the rule along the road
            // walled, the only wall on the road (breaker F2 of PR 79)
            Assert.Equal("2 3", string.Join(", ", walls.Where(wall => wall.Ends[0][1] == 0.0 && wall.Ends[1][1] == 0.0).Select(wall => wall.Plot + " " + wall.Edge)));
            Dictionary<string, Dictionary<string, object>> step = Walls.Step(scope, built.Plans, walls);
            Assert.Equal("False 0", step["7"]["written"] + " " + ((System.Collections.ICollection)step["7"]["edges"]).Count);
            Assert.Equal("1 2 | 3 3|4 | 3 3 4 4", string.Join(" ", (List<int>)step["3"]["edges"]) + " | " + string.Join(" ", ((List<List<string>>)step["3"]["plots"]).Select(item => string.Join("|", item)))
                + " | " + string.Join(" ", ((List<List<string>>)step["3"]["levelPlots"]).SelectMany(item => item)));
            Assert.Equal("{\"status\":\"RECOMMENDED\",\"statusFrom\":\"walls.base from override by heightM\"}", ((JArray)step["3"]["status"])[0]["base"].ToString(Newtonsoft.Json.Formatting.None));
            JObject tables = Walls.Tables(walls, table);
            Assert.Equal("walls.base from keyRules, walls.base from override by heightM, walls.ends from keyRules, walls.plots from locationPlan, walls.top from keyRules, walls.top from override by heightM",
                string.Join(", ", ((JObject)tables["statusFrom"]).Properties().Select(item => item.Name)));
            Assert.Equal("heightM", (string)tables["statusFrom"]["walls.base from override by heightM"]["override"]);
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("unit:noGeometry written:noGeometry edges:noGeometry plots:noGeometry ends:points3 base:levels3 top:levels3 levelPlots:noGeometry status:noGeometry",
                string.Join(" ", config.StepFields(Walls.StepName).Select(field => field.Name + ":" + field.Kind + field.Decimals)));
            Assert.True(step.Values.All(plot => string.Join(" ", plot.Keys) == string.Join(" ", Walls.StepFields)));
        }

        [Fact]
        public void R362_the_worker_build_writes_the_step_walls_and_the_build_steps_name_it_after_the_floors()
        {
            using (var maker = new LevelFloorStepTests())
            {
                WorkerRequest request = maker.Request("w1", Walls.StepName);
                string list = Path.Combine(request.OutputFolder, "overrides.json");
                File.WriteAllText(list, new JObject { ["overrides"] = new JArray(new JObject { ["plot"] = "3", ["field"] = "heightM", ["value"] = JObject.Parse("{\"gapToRuleM\": 0.1}"),
                    ["source"] = "parsons", ["model"] = "a.rvt", ["key"] = "imc" }) }.ToString());
                request.Inputs.Add(new WorkerFile { Role = "overrides", Path = list, Sha256 = FileHash.Sha256(list) });
                int code = WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0);
                Assert.True(code == 0, code + " " + WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, "w1")).Message);
                JObject step = RunStamp.Read(Path.Combine(request.OutputFolder, "steps", "step_walls_csharp.json"));
                // A flat road at 10 m: the pair 5 and 6 at one FFL of 10.228, so their shared side stands at 10.128 and tops at 11.928, placed from 5
                JToken five = step["plots"]["5"]["written"];
                Assert.Equal("1 2 | 5 5|6 | 10.128 10.128 | 11.928 11.928", string.Join(" ", five["edges"].Select(item => (int)item)) + " | "
                    + string.Join(" ", five["plots"].Select(item => string.Join("|", item.Select(plot => (string)plot)))) + " | " + string.Join(" ", five["base"].Select(item => ((double)item).ToString(CultureInfo.InvariantCulture)))
                    + " | " + string.Join(" ", five["top"].Select(item => ((double)item).ToString(CultureInfo.InvariantCulture))));
                Assert.Equal(6, ((JObject)step["plots"]).Count);
                // The FFL of 3 moved 10 cm up by heightM, so the wall behind it names the override, the worker holding it against the plots with no override
                Assert.Equal("walls.base from override by heightM", (string)step["plots"]["3"]["written"]["status"][0]["base"]["statusFrom"]);
                Assert.NotNull(step["tables"]["statusFrom"]["walls.ends from keyRules"]);
            }
            Assert.Equal("clusters fronts hands linkFrame levels floors walls overrides parameters status checkRows", string.Join(" ", BuildSteps.Names));
        }

        [Fact]
        public void R362_a_plot_a_rotation_override_turned_leaves_open_the_edge_its_link_faces()
        {
            // test_plan_walls.test_r362_a_plot_a_rotation_override_turned_leaves_open_the_edge_its_link_faces, breaker F2 of PR 79
            double[][] ring = Points(0, 0, 10, 0, 10, 20, 0, 20), frame = Points(0, 0, 10, 0);
            Func<double[][], string> text = edge => string.Join(" ", edge.SelectMany(point => point).Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
            Assert.Equal("0 0 10 0", text(Walls.OpenEdge(ring, frame, 0.0, false, true)));
            Assert.Equal("10 0 10 20", text(Walls.OpenEdge(ring, frame, 90.0, false, true)));
            // Review 2 of PR 79: the turned plots of the keys are mirrored at about 180 degrees, whose link faces the road again
            Assert.Equal("0 0 10 0", text(Walls.OpenEdge(ring, frame, 180.0, true, true)));
            Assert.Equal("10 0 10 20", text(Walls.OpenEdge(ring, frame, 270.0, true, true)));
            Assert.Equal("0 20 10 20", text(Walls.OpenEdge(ring.Reverse().ToArray(), frame, 180.0, false, true)));
            Assert.Equal("0 0 10 0", text(Walls.OpenEdge(ring, frame, 90.0, false, false)));
            var rings = new Dictionary<string, double[][]> { ["1"] = Rings["1"], ["2"] = Rings["2"], ["5"] = Rings["5"] };
            var opened = new Dictionary<string, double[][]> { ["1"] = Frames["1"], ["2"] = Walls.OpenEdge(Rings["2"], Frames["2"], 90.0, false, true), ["5"] = Frames["5"] };
            Assert.Equal("1 1, 1 2, 1 3, 2 0, 2 2, 5 1, 5 2", string.Join(", ", Made(rings, opened).Select(wall => wall.Plot + " " + wall.Edge)));
        }

        [Fact]
        public void R362_R401_a_base_whose_pick_an_override_changed_names_the_override()
        {
            // test_plan_walls.test_r362_r401_a_base_whose_pick_an_override_changed_names_the_override, breaker F3 of PR 79
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            JObject rules = JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules()));
            ValueStatusTable table = ValueStatus.Table(config, rules, "parsons2024Rev6");
            var ffls = new Dictionary<string, double>(Ffls) { ["2"] = 99.98 };
            PlanWall wall = Made(ffls: ffls)[0];
            Assert.Equal("1|2 1 1", string.Join("|", wall.Plots) + " " + wall.BasePlot + " " + wall.TopPlot);
            var plans = ffls.ToDictionary(item => item.Key, item => item.Key == "2" ? Plan(item.Value, "heightM") : Plan(item.Value));
            var rulePlans = Ffls.ToDictionary(item => item.Key, item => Plan(item.Value));
            Walls.Mark(wall, table, plans, rulePlans, Higher);
            Assert.Equal("RECOMMENDED walls.base from override by heightM walls.top from override by heightM", wall.Status["base"][1] + " " + wall.Status["base"][2] + " " + wall.Status["top"][2]);
            rulePlans["2"] = Plan(99.9);
            Walls.Mark(wall, table, plans, rulePlans, Higher);
            Assert.Equal("walls.base from keyRules", wall.Status["base"][2]);
            // Review 2 of PR 79, the wall of 543 and 544: heightM lifts 1, the plot of the base, to the FFL of 2, the pick of the rule, so the base
            // equals the base with no override and keeps its own list, though the override sits on its own plot
            var lifted = new Dictionary<string, double>(Ffls) { ["1"] = 100.5 };
            PlanWall even = Made(ffls: lifted)[0];
            Assert.Equal("1|2 1", string.Join("|", even.Plots) + " " + even.BasePlot);
            var liftedPlans = lifted.ToDictionary(item => item.Key, item => item.Key == "1" ? Plan(item.Value, "heightM") : Plan(item.Value));
            Walls.Mark(even, table, liftedPlans, Ffls.ToDictionary(item => item.Key, item => Plan(item.Value)), Higher);
            Assert.Equal("walls.base from keyRules walls.top from keyRules", even.Status["base"][2] + " " + even.Status["top"][2]);
        }
    }
}
