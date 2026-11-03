using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.PlaceVillas;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R530, Q391. The base of a mass cut at the party lines of its unit
    public sealed class R530_MassCutTests
    {
        private static MassBase Base(params double[][] ring)
        {
            return new MassBase(ring, WallRing.FromWalls, "the ring");
        }

        private static readonly double[][] Box = { new[] { 9.925, 7.6 }, new[] { 18.08, 7.6 }, new[] { 18.08, 17.56 }, new[] { 9.925, 17.56 } };

        [Fact]
        public void R530_a_ring_past_both_party_lines_is_cut_on_each_and_keeps_its_other_sides()
        {
            MassBase cut = MassCut.Apply(Base(Box), new PartyLine { MinX = 10.001, MaxX = 17.999 }, 0.004);
            Assert.Equal(10.001, cut.Ring.Min(point => point[0]), 6);
            Assert.Equal(17.999, cut.Ring.Max(point => point[0]), 6);
            Assert.Equal(7.6, cut.Ring.Min(point => point[1]), 6);
            Assert.Equal(17.56, cut.Ring.Max(point => point[1]), 6);
            Assert.Equal(4, cut.Ring.Count);
            Assert.Equal(cut.Ring.Count, cut.Edges.Count);
            Assert.Contains("cut at the party lines 10.001 to 17.999", cut.Reason);
            Assert.Equal((18.08 - 9.925) * 9.96, cut.UncutAreaM2.Value, 6);
        }

        [Fact]
        public void R530_a_notch_past_the_line_goes_and_only_it()
        {
            double[][] notched = { new[] { 2.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 4.0 }, new[] { 10.3, 4.0 }, new[] { 10.3, 8.0 }, new[] { 10.0, 8.0 }, new[] { 10.0, 12.0 }, new[] { 2.0, 12.0 } };
            MassBase cut = MassCut.Apply(Base(notched), new PartyLine { MaxX = 9.999 }, 0.004);
            Assert.Equal(9.999, cut.Ring.Max(point => point[0]), 6);
            Assert.Equal(2.0, cut.Ring.Min(point => point[0]), 6);
            Assert.True(FaceRingsSimple(cut));
        }

        private static bool FaceRingsSimple(MassBase mass)
        {
            return SiteAdaptation.Common.FaceRings.IsSimple(mass.Ring);
        }

        [Fact]
        public void R530_a_ring_inside_its_lines_a_unit_with_none_and_no_line_stay_as_they_are()
        {
            MassBase inside = Base(new[] { 10.5, 0.0 }, new[] { 17.0, 0.0 }, new[] { 17.0, 5.0 }, new[] { 10.5, 5.0 });
            Assert.Same(inside, MassCut.Apply(inside, new PartyLine { MinX = 10.001, MaxX = 17.999 }, 0.004));
            Assert.Same(inside, MassCut.Apply(inside, null, 0.004));
        }

        [Fact]
        public void R530_mirror_and_rotation_hold_the_cut_on_the_same_plot_side()
        {
            MassBase cut = MassCut.Apply(Base(Box), new PartyLine { MinX = 10.001 }, 0.004);
            foreach (bool mirrored in new[] { false, true })
            {
                List<double[]> placed = ShapeMath.Place(cut.Ring, new[] { 100.0, 200.0 }, 30.0, mirrored);
                double[] back = { (placed[0][0] - 100.0) * 0.8660254037844387 + (placed[0][1] - 200.0) * 0.5 };
                Assert.Equal(cut.Ring[0][0], back[0], 6);
            }
        }

        [Fact]
        public void R530_a_cut_that_leaves_no_edge_under_the_shortest_edge()
        {
            double[][] step = { new[] { 2.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 4.0 }, new[] { 9.998, 4.0 }, new[] { 9.998, 8.0 }, new[] { 2.0, 8.0 } };
            MassBase cut = MassCut.Apply(Base(step), new PartyLine { MaxX = 9.993 }, 0.004);
            for (int at = 0; at < cut.Ring.Count; at++)
            {
                double[] from = cut.Ring[at], to = cut.Ring[(at + 1) % cut.Ring.Count];
                Assert.True(System.Math.Sqrt((from[0] - to[0]) * (from[0] - to[0]) + (from[1] - to[1]) * (from[1] - to[1])) >= 0.004);
            }
        }

        [Fact]
        public void R530_a_ring_with_an_arc_past_the_line_stays_whole_and_says_so()
        {
            var edges = new List<RingEdge>
            {
                new RingEdge(new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }),
                new RingEdge(new[] { 10.0, 0.0 }, new[] { 10.0, 4.0 }, new[] { 10.0, 2.0 }, true),
                new RingEdge(new[] { 10.0, 4.0 }, new[] { 0.0, 4.0 }),
                new RingEdge(new[] { 0.0, 4.0 }, new[] { 0.0, 0.0 }),
            };
            var mass = new MassBase(RingEdges.Points(edges, 0.1), WallRing.FromWalls, "the ring", edges);
            MassBase kept = MassCut.Apply(mass, new PartyLine { MaxX = 9.9 }, 0.004);
            Assert.Same(edges, kept.Edges);
            Assert.Contains("the ring holds an arc", kept.NotCut);
        }

        [Fact]
        public void R530_a_concave_side_the_line_crosses_twice_stays_whole_and_says_so()
        {
            double[][] two = { new[] { 0.0, 0.0 }, new[] { 12.0, 0.0 }, new[] { 12.0, 4.0 }, new[] { 5.0, 4.0 }, new[] { 5.0, 6.0 }, new[] { 12.0, 6.0 }, new[] { 12.0, 10.0 }, new[] { 0.0, 10.0 } };
            MassBase cut = MassCut.Apply(Base(two), new PartyLine { MinX = 8.0 }, 0.004);
            Assert.Contains("does not leave one ring", cut.NotCut);
            Assert.Equal(two.Length, cut.Ring.Count);
        }

        [Fact]
        public void R530_the_config_of_the_repo_names_the_lines_and_a_config_with_none_cuts_nothing()
        {
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            var block = (JObject)config["addin"]["lightMode"];
            MassFileConfig read = LightModeConfig.From(config).MassFiles;
            PartyLine th1 = read.PartyLineOf("RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt");
            Assert.Equal(10.001, th1.MinX);
            Assert.Equal(17.999, th1.MaxX);
            Assert.Null(read.PartyLineOf("RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt"));
            block.Remove("massPartyLines");
            Assert.Null(LightModeConfig.From(config).MassFiles.PartyLineOf("RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt"));
            block["massPartyLines"] = new JObject { ["TH1"] = new JObject { ["minXM"] = "ten" } };
            Assert.Throws<InvalidDataException>(() => LightModeConfig.From(config));
            block["massPartyLines"] = new JObject { ["TH1"] = 10 };
            Assert.Throws<InvalidDataException>(() => LightModeConfig.From(config));
        }

        [Fact]
        public void R530_a_change_of_a_line_makes_every_mass_again()
        {
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            string before = MassFiles.SourceKey(LightModeConfig.From(config));
            config["addin"]["lightMode"]["massPartyLines"]["TH1"]["minXM"] = 10.5;
            Assert.NotEqual(before, MassFiles.SourceKey(LightModeConfig.From(config)));
        }

        [Fact]
        public void R530_the_sidecar_says_what_was_cut()
        {
            MassBase cut = MassCut.Apply(Base(Box), new PartyLine { MinX = 10.001 }, 0.004);
            JObject sidecar = MassFiles.Sidecar("a.rvt", "aa", "key", cut);
            Assert.True((double)sidecar["check"]["uncutAreaM2"] > System.Math.Abs(RingMath.Area(cut.Ring)));
            // The check block of the walls speaks of the ring before the cut, the ring written has its own area and says so
            Assert.Equal(System.Math.Abs(RingMath.Area(cut.Ring)), (double)sidecar["check"]["writtenAreaM2"], 6);
            Assert.True((bool)sidecar["check"]["offFaceIsBeforeCut"]);
            Assert.Null(((JValue)MassFiles.Sidecar("a.rvt", "aa", "key", Base(Box))["check"]["uncutAreaM2"]).Value);
        }
    }
}
