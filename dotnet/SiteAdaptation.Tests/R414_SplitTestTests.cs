using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R414. The split of a cluster by slope and steps, manual Rev 06 2.2.B with 2.3.A and 2.4.A, the C# side of python/tests/test_r414_split_test.py
    // on the same made up data: clusters of four DP2 units and the made up row of LevelFloorStepTests with a cluster of plots 2 to 5
    public sealed class R414_SplitTestTests : IDisposable
    {
        private static readonly string[] Cluster = { "2", "3", "4", "5" };

        // By hand on DP2, base 0.2 m and risers of 0.17 m: 0.51 m under the top of a piece is 4 steps, 0.255 m is 2 steps, steps.front_max is 3
        private const double Low = 10.0;
        private const double Mid = 10.255;
        private const double High = 10.51;

        private const string WallsLine = "designBasis.split.units must be a whole number of 2 or more and designBasis.split.walls a list of the walls it tries in order, each a wall after a unit inside the cluster (R414)";

        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r414_" + Guid.NewGuid().ToString("N"));

        public R414_SplitTestTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
        }

        private static JObject Settings(string mode, Action<JObject> change)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            JObject block = (JObject)config["designBasis"];
            block["mode"] = mode;
            change?.Invoke(block);
            return config;
        }

        private static string PastJob()
        {
            return ((JArray)Settings(DesignBasis.Rev6, null)["designBasis"]["modes"]).Select(mode => (string)mode).Single(mode => mode != DesignBasis.Rev6);
        }

        private static DesignBasis Basis(string mode = DesignBasis.Rev6, bool test = true, string civil = null)
        {
            return DesignBasis.Of(Settings(mode, block =>
            {
                block["split"]["test"] = test;
                if (civil != null)
                {
                    block["doorLevel"]["source"] = "civilFile";
                    block["doorLevel"]["civilFile"]["path"] = civil;
                }
            }), () => civil);
        }

        // split_test on a made up cluster of four DP2 units with El @ Entry levels in row order and the road at front of each, none when left out
        private static Tuple<Dictionary<string, List<string>>, Dictionary<string, string>> MadeSplit(double[] levels, double?[] roads = null)
        {
            JObject rules = LevelFloorStepTests.MadeRules();
            var entries = Cluster.Select((plot, index) => new { plot, index }).ToDictionary(item => item.plot,
                item => new EntryLevel { Unit = "DP2", Raw = levels[item.index], Rule = levels[item.index], Slope = 1.0, Road = roads?[item.index] });
            var roles = Cluster.ToDictionary(plot => plot, plot => new ClusterRole { Plots = Cluster.ToList() });
            return KeyLevels.SplitTest(entries, roles, Basis(), BuildRules.KeyRules(rules, null, "levels"), rules, BuildRules.LevelProfile(rules, null));
        }

        private static string Text(Tuple<Dictionary<string, List<string>>, Dictionary<string, string>> tested)
        {
            return string.Join(" ", tested.Item1.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Key + ":" + string.Join(",", item.Value) + ":" + tested.Item2[item.Key]));
        }

        [Fact]
        public void R414_a_cluster_whose_units_all_fit_stays_whole()
        {
            Assert.Equal("2:2,3,4,5:none 3:2,3,4,5:none 4:2,3,4,5:none 5:2,3,4,5:none", Text(MadeSplit(new[] { Low, Low, Mid, Mid })));
        }

        [Fact]
        public void R414_a_split_after_unit_3_is_tried_first_and_kept_when_both_parts_fit()
        {
            Assert.Equal("2:2,3,4:walls 3 3:2,3,4:walls 3 4:2,3,4:walls 3 5:5:walls 3", Text(MadeSplit(new[] { Low, Low, Mid, High })));
        }

        [Fact]
        public void R414_a_split_after_unit_2_when_after_unit_3_does_not_fit_is_the_middle()
        {
            Assert.Equal("2:2,3:middle 3:2,3:middle 4:4,5:middle 5:4,5:middle", Text(MadeSplit(new[] { Low, Low, High, High })));
        }

        [Fact]
        public void R414_a_cluster_no_split_fits_stays_whole_as_no_fit()
        {
            // Q303 took the slope out of the test, so this case holds steps alone
            Assert.Equal("2:2,3,4,5:noFit 3:2,3,4,5:noFit 4:2,3,4,5:noFit 5:2,3,4,5:noFit", Text(MadeSplit(new[] { Low, Low, High, Low + 1.02 })));
        }

        [Fact]
        public void R414_the_slope_is_taken_on_the_unrounded_el_at_entry_and_a_slope_of_exactly_front_min_fits()
        {
            double road = 10.12344, depth = 5.52;
            double raw = road + 0.5 / 100.0 * depth;
            double[] front = { road, road };
            double unrounded = DesignBasis.SlopeOf(raw, front, depth);
            double roundedFirst = DesignBasis.SlopeOf(Rounding.Fixed(raw, 4), front, depth);
            Assert.Equal(new[] { 0.5, 0.499 }, new[] { Rounding.Fixed(unrounded, 3), Rounding.Fixed(roundedFirst, 3) });
            // Q303. The slope is no longer in the split test, the report by OutsideManual holds it as before
            JObject limits = new JObject { ["front_min"] = 0.5, ["front_max"] = 2.0 };
            Assert.False(DesignBasis.OutsideManual(unrounded, limits));
            Assert.True(DesignBasis.OutsideManual(roundedFirst, limits));
        }

        [Fact]
        public void R414_a_cluster_of_another_size_or_with_a_plot_with_no_el_at_entry_is_not_tested()
        {
            JObject rules = LevelFloorStepTests.MadeRules();
            var entries = Cluster.Take(3).ToDictionary(plot => plot, plot => new EntryLevel { Unit = "DP2", Raw = Low, Rule = Low, Slope = 1.0 });
            var three = Cluster.Take(3).ToDictionary(plot => plot, plot => new ClusterRole { Plots = Cluster.Take(3).ToList() });
            var four = Cluster.Take(3).ToDictionary(plot => plot, plot => new ClusterRole { Plots = Cluster.ToList() });
            foreach (Dictionary<string, ClusterRole> roles in new[] { three, four })
            {
                Tuple<Dictionary<string, List<string>>, Dictionary<string, string>> tested = KeyLevels.SplitTest(entries, roles, Basis(), BuildRules.KeyRules(rules, null, "levels"), rules, BuildRules.LevelProfile(rules, null));
                Assert.Empty(tested.Item1);
                Assert.Empty(tested.Item2);
            }
        }

        [Fact]
        public void R414_Q304_a_row_rising_to_its_last_unit_counts_the_walls_from_there_and_the_lone_unit_stands_at_the_low_end()
        {
            // After unit 3 from the high road end is after unit 1 in row order, named from the first unit as split_pattern names it
            Assert.Equal("2:2:walls 1 3:3,4,5:walls 1 4:3,4,5:walls 1 5:3,4,5:walls 1", Text(MadeSplit(new[] { Low, Mid, Mid, High }, new double?[] { 10.0, 10.1, 10.2, 10.3 })));
        }

        [Fact]
        public void R414_Q304_a_row_falling_from_its_first_unit_counts_the_walls_from_there_and_the_lone_unit_stands_at_the_low_end()
        {
            Assert.Equal("2:2,3,4:walls 3 3:2,3,4:walls 3 4:2,3,4:walls 3 5:5:walls 3", Text(MadeSplit(new[] { High, Mid, Mid, Low }, new double?[] { 10.3, 10.2, 10.1, 10.0 })));
        }

        [Fact]
        public void R414_Q304_the_second_wall_counts_from_the_high_road_end_too_and_the_middle_is_the_middle_either_way()
        {
            Assert.Equal("2:2,3:middle 3:2,3:middle 4:4,5:middle 5:4,5:middle", Text(MadeSplit(new[] { Low, Low, High, High }, new double?[] { 10.0, 10.1, 10.2, 10.3 })));
        }

        [Fact]
        public void R414_Q304_a_tie_or_no_road_counts_the_walls_from_the_first_unit()
        {
            const string three = "2:2,3,4:walls 3 3:2,3,4:walls 3 4:2,3,4:walls 3 5:5:walls 3";
            double[] levels = { Low, Mid, Mid, High };
            Assert.Equal(three, Text(MadeSplit(levels, new double?[] { 10.0, 10.1, 10.2, 10.0 })));
            // A rise under the 4 decimals the plan writes the road at is a tie
            Assert.Equal(three, Text(MadeSplit(levels, new double?[] { 10.0, 10.1, 10.2, 10.00001 })));
            Assert.Equal(three, Text(MadeSplit(levels)));
            Assert.Equal(three, Text(MadeSplit(levels, new double?[] { null, 10.1, 10.2, 10.3 })));
            Assert.Equal(three, Text(MadeSplit(levels, new double?[] { 10.0, 10.1, 10.2, null })));
        }

        // The made up civil file: plot 2 at 0.5 percent over its road of 10.18 m, plot 5 near 2 percent over 10.54 m, 0.4424 m apart
        private string Civil(string levels = "1,10.1\n2,10.2076\n3,10.3276\n4,10.4476\n5,10.65\n6,10.7\n")
        {
            string path = Path.Combine(folder, "civil.csv");
            File.WriteAllText(path, "Plot No.,El @ Entry\n" + levels);
            return path;
        }

        // The plots of the cluster the list of a build with no override holds, each with its slope at 3 decimals
        private static string Outside(LevelFloorStepTests.Made made)
        {
            JArray listed = DesignBasis.OutsideList(made.Levels.Keys, made.Build.Entries.ToDictionary(item => item.Key, item => item.Value.Slope), (JObject)LevelFloorStepTests.MadeRules()["slopes_percent"]);
            return string.Join(" ", listed.Where(item => Cluster.Contains((string)item["plot"])).Select(item => (string)item["plot"] + ":" + ((double)item["slopePercent"]).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Fact]
        public void R414_Q303_a_slope_outside_the_limits_no_longer_stops_a_fit_and_stays_reported_for_each_unit_of_the_cluster()
        {
            // Roads of the row 10.18, 10.30, 10.42, 10.54 over a ramp of 5.52 m: 3.08 percent on plots 2 to 4 and 3.623 percent on plot 5, all over
            // front_max. Plot 2 lies 0.59 m under the FFL of the cluster, 3 steps, so the cluster stays whole
            string civil = Civil("1,10.1\n2,10.35\n3,10.47\n4,10.59\n5,10.74\n6,10.7\n");
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(basis: Basis(civil: civil), bands: Bands(), clusters: Four());
            Assert.All(Cluster, plot => Assert.Equal("none", (string)made.Levels[plot]["splitTest"]));
            Assert.Equal("2,3,4,5", string.Join(",", (List<string>)made.Levels["2"]["piece"]));
            Assert.Equal("3 3 2 1", string.Join(" ", Cluster.Select(plot => ((JToken)made.Levels[plot]["steps"]).ToString())));
            Assert.Equal("2:3.080 3:3.080 4:3.080 5:3.623", Outside(made));
        }

        [Fact]
        public void R414_Q303_the_steps_stop_a_fit_where_every_slope_is_inside_the_limits()
        {
            // Civil holds every slope of the cluster inside the limits and plot 2 at 4 steps when whole, so the steps alone split it
            LevelFloorStepTests.Made split = LevelFloorStepTests.MadeBuild(basis: Basis(civil: Civil()), bands: Bands(), clusters: Four());
            Assert.Equal("walls 1", (string)split.Levels["2"]["splitTest"]);
            Assert.Equal("", Outside(split));
            // At 1 percent each plot 2 lies 0.56 m under the FFL of the cluster, 3 steps, so the same row stays whole
            string civil = Civil("1,10.1\n2,10.2352\n3,10.3552\n4,10.4752\n5,10.5952\n6,10.7\n");
            LevelFloorStepTests.Made whole = LevelFloorStepTests.MadeBuild(basis: Basis(civil: civil), bands: Bands(), clusters: Four());
            Assert.Equal("none", (string)whole.Levels["2"]["splitTest"]);
            Assert.Equal("", Outside(whole));
        }

        private static JArray Bands()
        {
            return DesignBasis.Bands(JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "floor_outlines.json"))));
        }

        private static List<SiteCluster> Four()
        {
            return new List<SiteCluster> { new SiteCluster { Code = "Townhouse_made_up", Plots = Cluster.ToList(), Source = "footprint" } };
        }

        private static string Step(Dictionary<string, Dictionary<string, object>> step)
        {
            return string.Join("|", step.Select(plot => plot.Key + ":" + string.Join(",", plot.Value.Select(field => field.Key + "=" + Newtonsoft.Json.JsonConvert.SerializeObject(field.Value)))));
        }

        [Fact]
        public void R414_the_build_splits_the_cluster_and_the_levels_step_marks_each_plot()
        {
            string civil = Civil();
            LevelFloorStepTests.Made made = LevelFloorStepTests.MadeBuild(basis: Basis(civil: civil), bands: Bands(), clusters: Four());
            // Q304 changes this case: the road of the made up row rises to plot 5, so the first wall cuts after plot 2 and not after plot 4
            Assert.All(Cluster, plot => Assert.Equal("walls 1", (string)made.Levels[plot]["splitTest"]));
            Assert.Equal("2", string.Join(",", (List<string>)made.Levels["2"]["piece"]));
            Assert.Equal("3,4,5", string.Join(",", (List<string>)made.Levels["5"]["piece"]));
            Assert.Null(made.Levels["1"]["splitTest"]);
            Assert.Null(made.Levels["6"]["splitTest"]);
            Assert.Null(made.Levels["7"]["splitTest"]);
            Assert.Equal(Rounding.Fixed(10.2076 + 0.2, 3), made.Build.Rows["2"].Ffl);
            Assert.Equal(Rounding.Fixed(10.65 + 0.2, 3), made.Build.Rows["5"].Ffl);
            Assert.Equal(0.5, Rounding.Fixed((double)made.Levels["2"]["slopePercent"], 3));
        }

        [Fact]
        public void R414_the_switch_off_gives_the_pieces_of_key_pieces_and_a_null_split_test()
        {
            LevelFloorStepTests.Made off = LevelFloorStepTests.MadeBuild(basis: Basis(test: false, civil: Civil()), bands: Bands(), clusters: Four());
            Assert.All(off.Levels.Values, made => Assert.Null(made["splitTest"]));
            Assert.Equal("2,3,4,5", string.Join(",", (List<string>)off.Levels["2"]["piece"]));
            Assert.Equal(Rounding.Fixed(10.65 + 0.2, 3), off.Build.Rows["2"].Ffl);
            Assert.Equal("4", ((JToken)off.Levels["2"]["steps"]).ToString());
        }

        [Fact]
        public void R414_the_past_job_gives_a_null_split_test_and_the_plan_of_the_switch_off()
        {
            LevelFloorStepTests.Made past = LevelFloorStepTests.MadeBuild(basis: Basis(PastJob()), clusters: Four());
            LevelFloorStepTests.Made plain = LevelFloorStepTests.MadeBuild(basis: Basis(PastJob(), false), clusters: Four());
            Assert.All(past.Levels.Values, made => Assert.Null(made["splitTest"]));
            Assert.Equal(Step(plain.Levels), Step(past.Levels));
            Assert.False(Basis(PastJob()).Splits);
        }

        [Fact]
        public void R414_a_test_that_is_no_bool_or_walls_outside_the_cluster_are_refused()
        {
            Assert.Equal("designBasis.split.test must be true or false, it holds 'yes' (R414)",
                Assert.Throws<InvalidDataException>(() => DesignBasis.Of(Settings(DesignBasis.Rev6, block => block["split"]["test"] = "yes"), () => null)).Message);
            foreach (Tuple<JToken, JToken> wrong in new[]
            {
                Tuple.Create((JToken)4, (JToken)new JArray(4)), Tuple.Create((JToken)4, (JToken)new JArray()), Tuple.Create((JToken)true, (JToken)new JArray(1)),
                Tuple.Create((JToken)1, (JToken)new JArray(1)), Tuple.Create((JToken)4, (JToken)"3"),
            })
            {
                Assert.Equal(WallsLine, Assert.Throws<InvalidDataException>(() => DesignBasis.Of(Settings(DesignBasis.Rev6, block =>
                {
                    block["split"]["units"] = wrong.Item1;
                    block["split"]["walls"] = wrong.Item2;
                }), () => null)).Message);
            }
            DesignBasis basis = Basis();
            Assert.Equal("True 4 3,2", basis.SplitTest + " " + basis.SplitUnits + " " + string.Join(",", basis.SplitWalls));
            Assert.False(Basis(test: false).Splits);
            Assert.Equal(DesignBasis.SplitWallsLine, WallsLine);
        }
    }
}
