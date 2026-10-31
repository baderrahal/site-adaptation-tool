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

        // split_test on a made up cluster of four DP2 units with El @ Entry levels in row order and a slope of 1 percent each, or slopes
        private static Tuple<Dictionary<string, List<string>>, Dictionary<string, string>> MadeSplit(double[] levels, double[] slopes = null)
        {
            JObject rules = LevelFloorStepTests.MadeRules();
            var entries = Cluster.Select((plot, index) => new { plot, index }).ToDictionary(item => item.plot,
                item => new EntryLevel { Unit = "DP2", Raw = levels[item.index], Rule = levels[item.index], Slope = slopes == null ? 1.0 : slopes[item.index] });
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
            Assert.Equal("2:2,3,4,5:noFit 3:2,3,4,5:noFit 4:2,3,4,5:noFit 5:2,3,4,5:noFit", Text(MadeSplit(new[] { Low, Low, Mid, High }, new[] { 1.0, 1.0, 1.0, 2.5 })));
            Assert.Equal("noFit", MadeSplit(new[] { Low, Low, High, Low + 1.02 }).Item2["2"]);
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
            Assert.Equal("none", MadeSplit(new[] { Low, Low, Low, Low }, Enumerable.Repeat(unrounded, 4).ToArray()).Item2["2"]);
            Assert.Equal("noFit", MadeSplit(new[] { Low, Low, Low, Low }, Enumerable.Repeat(roundedFirst, 4).ToArray()).Item2["2"]);
            Assert.Equal("none", MadeSplit(new[] { Low, Low, Low, Low }, new[] { 0.5, 0.5, 2.0, 2.0 }).Item2["2"]);
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

        // The made up civil file: plot 2 at 0.5 percent over its road of 10.18 m, plot 5 near 2 percent over 10.54 m, 0.4424 m apart
        private string Civil()
        {
            string path = Path.Combine(folder, "civil.csv");
            File.WriteAllText(path, "Plot No.,El @ Entry\n1,10.1\n2,10.2076\n3,10.3276\n4,10.4476\n5,10.65\n6,10.7\n");
            return path;
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
            Assert.All(Cluster, plot => Assert.Equal("walls 3", (string)made.Levels[plot]["splitTest"]));
            Assert.Equal("2,3,4", string.Join(",", (List<string>)made.Levels["2"]["piece"]));
            Assert.Equal("5", string.Join(",", (List<string>)made.Levels["5"]["piece"]));
            Assert.Null(made.Levels["1"]["splitTest"]);
            Assert.Null(made.Levels["6"]["splitTest"]);
            Assert.Null(made.Levels["7"]["splitTest"]);
            Assert.Equal(Rounding.Fixed(10.4476 + 0.2, 3), made.Build.Rows["2"].Ffl);
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
