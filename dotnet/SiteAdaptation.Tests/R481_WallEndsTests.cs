using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R481, Q265. A wall a build parts by keyRules.wallOpen carries the source wallOpen on its ends, so walls.ends reads MEASURED weak by that rule, the C# side of
    // python/tests/test_r481_wall_ends.py on the same made up faces and values. Every other wall reads as it did
    public sealed class R481_WallEndsTests
    {
        private const string UnknownKey = "walls.ends from keyRules";
        private const string OpenKey = "walls.ends from wallOpen";

        private static readonly List<string> Order = new List<string> { "1", "2", "5" };

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

        private static readonly Dictionary<string, string[]> Kin = new Dictionary<string, string[]>
        {
            ["1"] = new[] { "0", "DT3" }, ["2"] = new[] { "0", "DT3" }, ["5"] = new[] { null, "VD4" },
        };

        private static readonly Dictionary<string, double[]> Spans = new Dictionary<string, double[]> { ["DT3|DT3"] = new[] { 7.6, 17.56 }, ["DT3|VD4"] = new[] { 6.0, 21.0 } };

        private static double[][] Points(params double[] values)
        {
            return Enumerable.Range(0, values.Length / 2).Select(index => new[] { values[2 * index], values[2 * index + 1] }).ToArray();
        }

        private static WallRules Rules(Dictionary<string, double[]> open = null, WallSides sides = null)
        {
            return new WallRules { HigherBase = true, HigherTop = true, Depth = 0.1, Rise = 1.7, Near = 0.3, Open = open, Sides = sides };
        }

        private static List<PlanWall> Found(WallRules rules, Dictionary<string, string[]> kin = null)
        {
            return Walls.Of(Order, new HashSet<string>(Ffls.Keys), Rings, Frames, Ffls, rules, Frames, kin ?? Kin);
        }

        private static List<PlanWall> Pair(List<PlanWall> walls)
        {
            return walls.Where(wall => wall.Plots.SequenceEqual(new[] { "1", "2" })).ToList();
        }

        private static ValueStatusTable Table()
        {
            return ValueStatus.Table(JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig())), JObject.Parse(File.ReadAllText(BuildStepTests.RepoRules())), "parsons2024Rev6");
        }

        private static string Ends(PlanWall wall, ValueStatusTable table = null, Dictionary<string, PlotPlan> plans = null)
        {
            Walls.Mark(wall, table ?? Table(), plans ?? new Dictionary<string, PlotPlan>());
            return string.Join("|", wall.Status["ends"]);
        }

        [Fact]
        public void R481_both_parts_of_a_wall_the_wallopen_table_parts_read_measured_weak_by_that_rule()
        {
            ValueStatusTable table = Table();
            List<PlanWall> parts = Pair(Found(Rules(Spans)));
            Assert.Equal(2, parts.Count);
            Assert.All(parts, wall => Assert.Equal("wallOpen|MEASURED|" + OpenKey, Ends(wall, table)));
            JObject detail = table.Used[OpenKey];
            Assert.Equal("weak", (string)detail["strength"]);
            JArray entries = (JArray)detail["entries"];
            foreach (string word in new[] { "R481", "Q265", "keyRules.wallOpen", "0.7409" })
            {
                Assert.Contains(word, (string)entries[0]["why"]);
            }
            JToken rule = entries.Single(entry => ((string)entry["entry"] ?? "").EndsWith("levelProfiles.parsons2024Rev6.keyRules.wallOpen.rule", System.StringComparison.Ordinal));
            Assert.Equal(0.7409, (double)rule["share"]);
            Assert.Equal("weak", (string)rule["strength"]);
            Assert.DoesNotContain(entries, entry => (string)entry["status"] == "UNKNOWN");
        }

        [Fact]
        public void R481_a_whole_wall_stays_unknown_and_every_other_field_of_a_parted_wall_reads_as_before()
        {
            ValueStatusTable table = Table();
            List<PlanWall> walls = Found(Rules(Spans));
            List<PlanWall> whole = walls.Where(wall => !wall.Plots.SequenceEqual(new[] { "1", "2" })).ToList();
            Assert.NotEmpty(whole);
            Assert.All(whole, wall => Assert.Equal("keyRules|UNKNOWN|" + UnknownKey, Ends(wall, table)));
            PlanWall parted = Pair(walls)[0], plain = Pair(Found(Rules()))[0];
            Ends(parted, table);
            Assert.Equal("keyRules|UNKNOWN|" + UnknownKey, Ends(plain, table));
            Assert.All(new[] { "plots", "base", "top" }, field => Assert.Equal(string.Join("|", plain.Status[field]), string.Join("|", parted.Status[field])));
        }

        [Fact]
        public void R481_a_pair_the_table_lacks_and_a_profile_with_no_wallopen_leave_the_wall_whole_and_unknown()
        {
            var other = new Dictionary<string, string[]>(Kin) { ["2"] = new[] { "0", "TH1" } };
            foreach (List<PlanWall> walls in new[] { Found(Rules(Spans), other), Found(Rules()), Found(Rules(new Dictionary<string, double[]>())) })
            {
                PlanWall wall = Assert.Single(Pair(walls));
                Assert.False(wall.Parted);
                Assert.Equal("keyRules|UNKNOWN|" + UnknownKey, Ends(wall));
            }
        }

        [Fact]
        public void R481_a_gap_clear_of_the_part_parts_nothing_so_the_part_stays_keyrules()
        {
            // The edge of 1 and 2 runs 20 m, a span from 25 m to 30 m lies past its end, the table holds the pair and the wall stays whole
            PlanWall wall = Assert.Single(Pair(Found(Rules(new Dictionary<string, double[]> { ["DT3|DT3"] = new[] { 25.0, 30.0 }, ["DT3|VD4"] = new[] { 6.0, 21.0 } }))));
            Assert.Equal(new[] { 10.0, 0.0, 10.0, 20.0 }, wall.Ends.SelectMany(point => point).ToArray());
            Assert.False(wall.Parted);
            Assert.Equal("keyRules|UNKNOWN|" + UnknownKey, Ends(wall));
        }

        [Fact]
        public void R481_a_part_of_the_r480_side_span_keeps_the_old_source()
        {
            // The faces of test_r480_c10_side.PAIR: the side span of R480 parts this edge and the wallOpen table holds no pair of it
            var sides = new WallSides { Cos = 0.5, ByUnit = new Dictionary<string, double[][]> { ["U1"] = new[] { new[] { 1.8, 7.21 }, new[] { 5.5, 22.025 } } } };
            var rings = new Dictionary<string, double[][]> { ["0"] = Points(16, -3, 30, -3, 30, 30, 16, 30), ["1"] = Points(0, 0, 16, 0, 16, 25, 0, 25) };
            var frames = new Dictionary<string, double[][]> { ["0"] = Points(16, -3, 30, -3), ["1"] = Points(0, 0, 16, 0) };
            var kin = new Dictionary<string, string[]> { ["1"] = new[] { "0", "U1" }, ["0"] = new[] { "0", "U2" } };
            var written = new HashSet<string>(frames.Keys);
            List<PlanWall> walls = Walls.Of(new[] { "0", "1" }, written, rings, frames, written.ToDictionary(plot => plot, plot => 100.0), Rules(null, sides), frames, kin,
                new Dictionary<string, bool> { ["1"] = false, ["0"] = false });
            List<PlanWall> parts = walls.Where(wall => wall.Plots.SequenceEqual(new[] { "0", "1" })).ToList();
            Assert.Equal(2, parts.Count);
            Assert.All(parts, wall =>
            {
                Assert.False(wall.Parted);
                Assert.Equal("keyRules|UNKNOWN|" + UnknownKey, Ends(wall));
            });
        }

        [Fact]
        public void R481_a_moved_wall_of_r416_keeps_its_source_also_where_wallopen_parted_it()
        {
            PlanWall moved = Pair(Found(Rules(Spans)))[0];
            moved.LineInsideM = 0.1;
            Assert.Equal("manualInside|UNKNOWN|walls.ends from manualInside", Ends(moved));
            PlanWall still = Pair(Found(Rules(Spans)))[0];
            still.LineInsideM = 0.0;
            Assert.Equal("wallOpen|MEASURED|" + OpenKey, Ends(still));
        }

        [Fact]
        public void R481_a_turned_plot_on_a_parted_wall_reads_the_override_as_before()
        {
            // overrideReads rotationDeg walls.ends of the settings holds no source, so the status of a turned wall stays that of before and only the source names wallOpen
            var plan = new PlotPlan { AppliedOrder = new List<string> { "rotationDeg" } };
            var plans = new Dictionary<string, PlotPlan> { ["1"] = plan };
            PlanWall wall = Pair(Found(Rules(Spans)))[0], plain = Pair(Found(Rules()))[0];
            wall.Turned = new List<string> { "1" };
            plain.Turned = new List<string> { "1" };
            Assert.Equal("wallOpen|UNKNOWN|walls.ends from override by rotationDeg", Ends(wall, null, plans));
            Assert.Equal("keyRules|UNKNOWN|walls.ends from override by rotationDeg", Ends(plain, null, plans));
        }

        [Fact]
        public void R481_the_settings_hold_walls_ends_from_wallopen_at_measured_weak_and_the_other_sources_as_they_were()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            JObject ends = (JObject)config["valueStatus"]["fields"]["walls.ends"];
            Assert.Equal(new[] { "keyRules", "manualInside", "wallOpen" }, ends.Properties().Select(item => item.Name).ToArray());
            Assert.Equal("MEASURED weak Q265", (string)ends["wallOpen"][0]["status"] + " " + (string)ends["wallOpen"][0]["strength"] + " " + (string)ends["wallOpen"][0]["question"]);
            Assert.Equal("UNKNOWN", (string)ends["keyRules"].Last["status"]);
            Assert.Equal("UNKNOWN", (string)ends["manualInside"].Last["status"]);
            Assert.Equal("noGeometry", (string)config["addin"]["parity"]["steps"]["walls"]["endsSource"]["kind"]);
        }

        [Fact]
        public void R481_the_step_walls_names_the_source_of_each_ends_as_the_plan_writes_it()
        {
            List<PlanWall> walls = Found(Rules(Spans));
            ValueStatusTable table = Table();
            walls.ForEach(wall => Walls.Mark(wall, table, new Dictionary<string, PlotPlan>()));
            Dictionary<string, Dictionary<string, object>> step = Walls.Step(Order, new Dictionary<string, PlotPlan>(), walls);
            Assert.All(step.Values, plot => Assert.Equal(string.Join(" ", plot.Keys), string.Join(" ", Walls.StepFields)));
            List<string> sources = (List<string>)step["1"]["endsSource"];
            List<PlanWall> mine = walls.Where(wall => wall.Plot == "1").ToList();
            Assert.Equal(mine.Select(wall => wall.Status["ends"][0]), sources);
            Assert.Equal(2, sources.Count(source => source == "wallOpen"));
        }
    }
}
