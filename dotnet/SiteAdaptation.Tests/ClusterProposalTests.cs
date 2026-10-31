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
    // R411. The cluster and mirror proposals of round 4i on made up rows built in code (R10), the C# side of
    // python/tests/test_r411_cluster_proposals.py on the same rows and the same values by hand, on net48 and net8.0
    public sealed class ClusterProposalTests
    {
        private const string Dp1File = "A-DP1.rvt";
        private const string Th1File = "A-TH1.rvt";
        private const string Dp2File = "A-DP2.rvt";

        // D02 the same sequence as M02 under another name, as cluster_aliases names it, so the pair splits one way
        private static JObject Profile()
        {
            return JObject.Parse("{\"typologies\":{\"VD2\":{\"attachment\":\"detached\"},\"DP1\":{\"attachment\":\"one_side\"},\"DP2\":{\"attachment\":\"one_side\"},"
                + "\"TH1\":{\"attachment\":\"both_sides\"},\"XX1\":{}},"
                + "\"clusters\":{\"M02\":[\"DP2\",\"DP2\"],\"M07\":[\"DP1\",\"TH1\",\"TH1\",\"DP1\"],\"M01\":[\"DP1\",\"DP1\"],\"D02\":[\"DP2\",\"DP2\"]}}");
        }

        private static readonly JObject Key = JObject.Parse("{\"hand cluster\":{\"rule\":\"clusterSide\",\"byFile\":{\"" + Dp1File + "\":{\"left\":false,\"right\":true},\""
            + Th1File + "\":{\"left\":true,\"right\":false}}}}");

        private static readonly Dictionary<string, string> Files = new Dictionary<string, string> { ["DP1"] = Dp1File, ["TH1"] = Th1File, ["DP2"] = Dp2File };

        private static string F(string text, params object[] values)
        {
            return string.Format(CultureInfo.InvariantCulture, text, values);
        }

        // A row of plots 11, 12 and on, left to right, each the side neighbour of the next, as test_r411_cluster_proposals.row makes it
        private static Dictionary<string, ClusterProposal> Proposed(string[] units, JObject profile = null, Dictionary<string, string> files = null,
            Action<Dictionary<string, Sides>> change = null)
        {
            List<string> plots = units.Select((unit, index) => (11 + index).ToString(CultureInfo.InvariantCulture)).ToList();
            var neighbours = new Dictionary<string, Sides>();
            var rows = new Dictionary<string, Dictionary<string, string>>();
            for (int index = 0; index < plots.Count; index++)
            {
                var sides = new Sides { Framed = true };
                if (index > 0)
                {
                    sides.Left.Add(plots[index - 1]);
                }
                if (index < plots.Count - 1)
                {
                    sides.Right.Add(plots[index + 1]);
                }
                neighbours[plots[index]] = sides;
                rows[plots[index]] = new Dictionary<string, string> { ["typologyRev9"] = units[index] };
            }
            change?.Invoke(neighbours);
            return ClusterProposals.Of(plots, rows, neighbours, profile ?? Profile(), Key, files ?? Files);
        }

        private static string Cluster(ClusterProposal proposal)
        {
            return proposal.Cluster == null ? "null" : string.Join(" ", proposal.Cluster);
        }

        private static string Mirror(ClusterProposal proposal)
        {
            return proposal.Mirrored == null ? "null" : ((bool)proposal.Mirrored ? "true" : "false");
        }

        [Fact]
        public void R411_a_detached_unit_is_proposed_alone_with_no_mirror_flag_naming_q226()
        {
            Dictionary<string, ClusterProposal> found = Proposed(new[] { "VD2", "DP1", "DP1" });
            Assert.Equal("11", Cluster(found["11"]));
            Assert.Null(found["11"].Mirrored);
            Assert.Equal(F(ClusterProposals.DetachedWhy, "VD2"), found["11"].Reason);
            Assert.Contains("Q226", found["11"].Reason);
            Assert.Equal("12 13", Cluster(found["12"]));
            Assert.Equal("12 13", Cluster(found["13"]));
        }

        [Fact]
        public void R411_a_dp_pair_is_one_cluster_whose_code_is_the_first_of_its_sequence()
        {
            Dictionary<string, ClusterProposal> found = Proposed(new[] { "DP2", "DP2" });
            Assert.Equal("11 12", Cluster(found["11"]));
            Assert.Equal("11 12", Cluster(found["12"]));
            Assert.StartsWith("cluster M02 of the rules profile, the one split of the row 11 12 by Rev 06 2.1.E, the left half", found["11"].Reason);
            Assert.StartsWith("cluster M02 of the rules profile, the one split of the row 11 12 by Rev 06 2.1.E, the right half", found["12"].Reason);
        }

        [Fact]
        public void R411_dp_th_th_dp_is_one_cluster_and_its_halves_take_opposite_hands_of_the_hand_table()
        {
            Dictionary<string, ClusterProposal> found = Proposed(new[] { "DP1", "TH1", "TH1", "DP1" });
            Assert.All(found.Values, item => Assert.Equal("11 12 13 14", Cluster(item)));
            Assert.Equal("false true false true", string.Join(" ", new[] { "11", "12", "13", "14" }.Select(plot => Mirror(found[plot]))));
            Assert.Equal(F(ClusterProposals.Proposed, "M07", "11 12 13 14", "left", F(ClusterProposals.Hand, Th1File)), found["12"].Reason);
        }

        [Fact]
        public void R411_four_duplexes_in_a_row_split_into_two_pairs()
        {
            Dictionary<string, ClusterProposal> found = Proposed(new[] { "DP2", "DP2", "DP2", "DP2" });
            Assert.Equal("11 12|11 12|13 14|13 14", string.Join("|", new[] { "11", "12", "13", "14" }.Select(plot => Cluster(found[plot]))));
        }

        [Fact]
        public void R411_a_row_with_no_split_or_several_gets_no_proposal_naming_q229()
        {
            Dictionary<string, ClusterProposal> found = Proposed(new[] { "DP2", "DP2", "DP2" });
            Assert.All(found.Values, item => Assert.True(item.Cluster == null && item.Mirrored == null));
            Assert.Equal(F(ClusterProposals.NoSplit, "11 12 13", "DP2 DP2 DP2"), found["11"].Reason);
            Assert.Contains("Q229", found["11"].Reason);
            JObject several = Profile();
            several["clusters"] = JObject.Parse("{\"M02\":[\"DP2\",\"DP2\"],\"Q04\":[\"DP2\",\"DP2\",\"DP2\",\"DP2\"]}");
            found = Proposed(new[] { "DP2", "DP2", "DP2", "DP2" }, several);
            Assert.Null(found["12"].Cluster);
            Assert.Equal(F(ClusterProposals.Several, "11 12 13 14", "DP2 DP2 DP2 DP2", 2), found["12"].Reason);
        }

        [Fact]
        public void R411_a_plot_with_two_neighbours_on_a_side_leaves_its_row_with_no_proposal_naming_q229()
        {
            Dictionary<string, ClusterProposal> found = Proposed(new[] { "DP2", "DP2" }, change: neighbours => neighbours["12"].Right.AddRange(new[] { "98", "99" }));
            Assert.All(found.Values, item => Assert.True(item.Cluster == null && item.Mirrored == null));
            Assert.Equal(F(ClusterProposals.Crowded, "12", "11 12", 2, "right"), found["11"].Reason);
            Assert.Equal(found["11"].Reason, found["12"].Reason);
        }

        [Fact]
        public void R411_a_file_the_hand_table_does_not_hold_gives_no_mirror_flag_with_the_reason()
        {
            Dictionary<string, ClusterProposal> found = Proposed(new[] { "DP2", "DP2" });
            Assert.Equal("11 12", Cluster(found["11"]));
            Assert.Null(found["11"].Mirrored);
            Assert.EndsWith(F(ClusterProposals.NoHand, "left", Dp2File) + " (R411)", found["11"].Reason);
            Assert.EndsWith(F(ClusterProposals.NoFile, "DP2") + " (R411)", Proposed(new[] { "DP2", "DP2" }, files: new Dictionary<string, string>())["12"].Reason);
        }

        [Fact]
        public void R411_a_unit_with_no_attachment_and_a_plot_with_no_unit()
        {
            Dictionary<string, ClusterProposal> found = Proposed(new[] { "XX1", "DP1", "" });
            Assert.False(found.ContainsKey("13"));
            Assert.Equal(F(ClusterProposals.NoAttachment, "XX1"), found["11"].Reason);
            Assert.Null(found["11"].Cluster);
            Assert.Equal(F(ClusterProposals.NoSplit, "12", "DP1"), found["12"].Reason);
        }

        [Fact]
        public void R411_clusters_propose_must_be_true_or_false()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["designBasis"]["clusters"]["propose"] = "yes";
            Assert.Equal("designBasis.clusters.propose must be true or false, it holds 'yes' (R411)", Assert.Throws<InvalidDataException>(() => DesignBasis.Of(config, () => null)).Message);
            config["designBasis"]["clusters"]["propose"] = true;
            Assert.True(DesignBasis.Of(config, () => null).Proposes);
        }

        private static DesignBasis Basis(string mode, bool propose)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["designBasis"]["mode"] = mode;
            config["designBasis"]["clusters"]["propose"] = propose;
            return DesignBasis.Of(config, () => null);
        }

        // The ramp bands of config/floor_outlines.json, which a build under rev6 reads
        private static JArray Bands()
        {
            return DesignBasis.Bands(JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "floor_outlines.json"))));
        }

        private static string PastJob()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            return ((JArray)config["designBasis"]["modes"]).Select(mode => (string)mode).Single(mode => mode != DesignBasis.Rev6);
        }

        private static string Text(object value)
        {
            return JToken.FromObject(value ?? JValue.CreateNull()).ToString(Newtonsoft.Json.Formatting.None);
        }

        [Fact]
        public void R411_the_step_hands_under_rev6_gives_the_proposals_and_the_plan_takes_them_with_their_count()
        {
            // Plot 6 a villa, so 2 to 5 are four DP2 in a row, the pairs 2 3 and 4 5, the DP2 file in the hand cluster table of the made up rules
            var units = new Dictionary<string, string> { ["6"] = "VD2" };
            Dictionary<string, Dictionary<string, object>> hands = LevelFloorStepTests.MadeBuild(units: units, basis: Basis(DesignBasis.Rev6, true), bands: Bands()).Hands;
            Assert.Equal("[\"1\"] [\"2\",\"3\"] [\"2\",\"3\"] [\"4\",\"5\"] [\"4\",\"5\"] [\"6\"] null",
                string.Join(" ", new[] { "1", "2", "3", "4", "5", "6", "7" }.Select(plot => Text(hands[plot]["proposedCluster"]))));
            Assert.Equal("false true false true", string.Join(" ", new[] { "2", "3", "4", "5" }.Select(plot => Text(hands[plot]["proposedMirror"]))));
            Assert.Null(hands["1"]["proposedMirror"]);
            Assert.Contains("Q226", (string)hands["1"]["proposalReason"]);
            Assert.Null(hands["7"]["proposalReason"]);
            // The plan takes the proposal of the step hands on a written plot and counts them in its summary
            Dictionary<string, JObject> steps = R80_PlanFileTests.Steps();
            foreach (string side in new[] { "raw", "written" })
            {
                JObject one = (JObject)steps["hands"]["plots"]["1"][side];
                one["proposedCluster"] = new JArray("1", "2");
                one["proposedMirror"] = false;
                one["proposalReason"] = "why";
                JObject two = (JObject)steps["hands"]["plots"]["2"][side];
                two["proposedCluster"] = null;
                two["proposedMirror"] = null;
                two["proposalReason"] = "none";
            }
            JObject plan = BuildPlan.Assemble(steps, R80_PlanFileTests.Inputs());
            Assert.Equal("{\"cluster\":[\"1\",\"2\"],\"mirrored\":false,\"reason\":\"why\"}", plan["plots"][0]["proposal"].ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal("proposal", ((JObject)plan["plots"][0]).Properties().Last().Name);
            Assert.Equal("{\"proposed\":1,\"none\":1}", plan["summary"]["proposals"].ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal("proposals", ((JObject)plan["summary"]).Properties().Last().Name);
            JObject plain = BuildPlan.Assemble(R80_PlanFileTests.Steps(), R80_PlanFileTests.Inputs());
            Assert.Null(plain["plots"][0]["proposal"]);
            Assert.Null(plain["summary"]["proposals"]);
        }

        [Fact]
        public void R411_the_switch_back_to_the_past_job_or_propose_false_gives_null_proposals_and_the_values_of_today()
        {
            var units = new Dictionary<string, string> { ["6"] = "VD2" };
            LevelFloorStepTests.Made past = LevelFloorStepTests.MadeBuild(units: units, basis: Basis(PastJob(), true));
            LevelFloorStepTests.Made today = LevelFloorStepTests.MadeBuild(units: units);
            LevelFloorStepTests.Made off = LevelFloorStepTests.MadeBuild(units: units, basis: Basis(DesignBasis.Rev6, false), bands: Bands());
            LevelFloorStepTests.Made on = LevelFloorStepTests.MadeBuild(units: units, basis: Basis(DesignBasis.Rev6, true), bands: Bands());
            foreach (Dictionary<string, object> hand in past.Hands.Values.Concat(off.Hands.Values))
            {
                Assert.Null(hand["proposedCluster"]);
                Assert.Null(hand["proposedMirror"]);
                Assert.Null(hand["proposalReason"]);
            }
            Assert.Equal(Text(today.Hands), Text(past.Hands));
            Assert.Equal(Text(today.Links), Text(past.Links));
            // A proposal sets no other value: the links and every other field of the step hands equal the build with propose false
            Assert.Equal(Text(off.Links), Text(on.Links));
            foreach (string plot in on.Hands.Keys)
            {
                Assert.Equal(Text(off.Hands[plot].Where(item => !item.Key.StartsWith("propos", StringComparison.Ordinal)).ToDictionary(item => item.Key, item => item.Value)),
                    Text(on.Hands[plot].Where(item => !item.Key.StartsWith("propos", StringComparison.Ordinal)).ToDictionary(item => item.Key, item => item.Value)));
            }
        }
    }
}
