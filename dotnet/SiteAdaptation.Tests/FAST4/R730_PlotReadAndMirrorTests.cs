using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests.FAST4
{
    // R730 to R732 on made up reads: the plot table holds the plots a villa goes on and lists the rest with the reason, a block of two houses
    // gives both plots it stands on a hand, opposite ones, from the scale sign of its insert, and a pair of plots with no block is traced and
    // said, its hand left missing
    public sealed class R730_PlotReadAndMirrorTests
    {
        private static DataCheckSettings Settings()
        {
            return DataCheckSettings.From(new JObject
            {
                [DataCheckSettings.Block] = new JObject
                {
                    ["scan"] = new JObject
                    {
                        ["labelPattern"] = @"^(?:(.*?)\s+)?(\S*\d\S*)$", ["tokenSplit"] = "[^A-Za-z0-9]+", ["plotNumberShifts"] = new JArray(0), ["pairMinSegments"] = 100,
                        ["pairThreads"] = 1, ["pairTimeCapS"] = 60, ["pdfJoinGapPt"] = 0.3, ["pdfLineTolerancePt"] = 0.05, ["pdfPairDyPt"] = new JArray(1.3, 1.8), ["pdfPairDxPt"] = 1.5,
                    },
                    ["groupGapsM"] = new JArray(100.0, 1.0), ["wordReachM"] = 30, ["wordShare"] = 0.9, ["wordMinGroups"] = 3, ["standInMaxArea"] = 4, ["roadReachM"] = 20, ["roadMinSegments"] = 2, ["roadOutsideShare"] = 0.5,
                    ["drawingPeak"] = new JObject { ["dwg"] = 252, ["dxf"] = 106 },
                    ["strongShare"] = 0.9, ["usableShare"] = 0.5, ["prototypeFilePattern"] = "(?:^|-)ARC-([A-Za-z0-9]+)(?:-[A-Za-z0-9]{2})?-MOD",
                    ["codePattern"] = @"^[A-Za-z]+\d+[A-Za-z0-9.]*(?:-[A-Za-z0-9]+)*(?:\s*\([^()]*\))?$", ["codeTableMaxPairs"] = 100, ["handGuessShare"] = 0.6, ["areaNearPlots"] = 8, ["areaAgreement"] = 0.7, ["skipFolders"] = new JArray(), ["groupNamePattern"] = @"^(?:[A-Z0-9][A-Za-z0-9-]{0,3}|\S*\d\S*)$",
                },
            });
        }

        private static JArray Pt(double x, double y)
        {
            return new JArray(x, y);
        }

        private static JArray Box(double x, double y, double width, double height)
        {
            return new JArray(Pt(x, y), Pt(x + width, y), Pt(x + width, y + height), Pt(x, y + height));
        }

        private static JObject Face(double x, double y)
        {
            return new JObject { ["ring"] = Box(x, y, 12, 20), ["centroid"] = Pt(x + 6, y + 10), ["area"] = 240.0 };
        }

        // Plots of 12 by 20 m, each (number, label code or null, left corner x, y). A plot with no code gets a label of its number alone
        private static JObject Drawing(IEnumerable<(string Number, string Code, double X, double Y)> plots, IEnumerable<JObject> inserts)
        {
            var faces = new JArray();
            var texts = new JArray();
            int count = 0;
            foreach (var plot in plots)
            {
                faces.Add(Face(plot.X, plot.Y));
                texts.Add(new JObject { ["layer"] = "LOTS-TEXT", ["text"] = plot.Code == null ? plot.Number : plot.Code + "\n" + plot.Number, ["x"] = plot.X + 3, ["y"] = plot.Y + 2 });
                count++;
            }
            return new JObject
            {
                ["source"] = "site.dwg", ["path"] = "site.dwg", ["size"] = 1, ["sha256"] = "x", ["units"] = "Meters", ["notRead"] = new JArray(), ["notTraced"] = new JArray(),
                ["layers"] = new JArray(new JObject { ["layer"] = "LOTS", ["closedOutlines"] = count, ["texts"] = 0, ["blocks"] = 0, ["segments"] = count * 4 }),
                ["candidates"] = new JArray(new JObject { ["layers"] = new JArray("LOTS"), ["faces"] = faces }),
                ["texts"] = texts, ["inserts"] = new JArray(inserts), ["lines"] = new JObject(), ["arcs"] = new JObject(),
            };
        }

        // A villa block drawn over the plots it stands on, its outline a little inside them, its centre on the shared wall of a duplex
        private static JObject Block(string name, double x, double y, double width, bool mirrored, double[] axisX, double[] axisY)
        {
            var insert = new JObject
            {
                ["layer"] = "HOUSES", ["block"] = name, ["x"] = x + width / 2, ["y"] = y + 10, ["mirrored"] = mirrored, ["outline"] = Box(x + 1, y + 4, width - 2, 12),
            };
            if (axisX != null)
            {
                insert["axisX"] = new JArray(axisX[0], axisX[1]);
                insert["axisY"] = new JArray(axisY[0], axisY[1]);
            }
            return insert;
        }

        private static DataCheckOutput Run(JObject drawing)
        {
            var input = new DataCheckInput();
            input.CadReads.Add(drawing);
            input.PrototypeNames.AddRange(new[] { "X-ARC-NEW1-ZZ-MOD-1.rvt", "X-ARC-NEW2-ZZ-MOD-1.rvt" });
            return DataCheckLogic.Run(Settings(), input);
        }

        private static Dictionary<string, JObject> Rows(DataCheckOutput output, string key = "plots")
        {
            return ((JArray)output.Plots[key]).OfType<JObject>().ToDictionary(row => (string)row["number"]);
        }

        // Five duplex pairs in a row, each pair under one block, the last pair drawn flipped in X, and a park plot with no code
        private static JObject Street(bool withAxes)
        {
            var plots = new List<(string, string, double, double)>();
            var inserts = new List<JObject>();
            for (int pair = 0; pair < 5; pair++)
            {
                double x = pair * 30.0;
                plots.Add(((201 + 2 * pair).ToString(), "NEW1", x, 0));
                plots.Add(((202 + 2 * pair).ToString(), "NEW1", x + 12, 0));
                bool flipped = pair == 4;
                inserts.Add(Block("Duplex_NEW1", x, 0, 24, flipped, withAxes ? (flipped ? new[] { -1.0, 0 } : new[] { 1.0, 0 }) : null, withAxes ? new[] { 0, 1.0 } : null));
            }
            plots.Add(("300", null, 200, 0));
            return Drawing(plots, inserts);
        }

        [Fact]
        public void R731_a_duplex_block_gives_both_plots_it_stands_on_opposite_hands_from_its_scale_sign()
        {
            Dictionary<string, JObject> rows = Rows(Run(Street(true)));
            for (int pair = 0; pair < 4; pair++)
            {
                Assert.False((bool)rows[(201 + 2 * pair).ToString()]["mirror"]);
                Assert.True((bool)rows[(202 + 2 * pair).ToString()]["mirror"]);
                Assert.Contains("one of the 2 houses of one block", (string)rows[(202 + 2 * pair).ToString()]["weak"]["mirror"]);
            }
            // Flipped in X the first house of the block is the one on the right, and it is the mirrored one
            Assert.True((bool)rows["210"]["mirror"]);
            Assert.False((bool)rows["209"]["mirror"]);
        }

        [Fact]
        public void R731_with_no_axes_the_houses_are_ranked_along_the_line_through_them()
        {
            Dictionary<string, JObject> rows = Rows(Run(Street(false)));
            Assert.Equal(10, rows.Values.Count(row => row["mirror"].Type == JTokenType.Boolean));
            Assert.NotEqual((bool)rows["201"]["mirror"], (bool)rows["202"]["mirror"]);
        }

        [Fact]
        public void R731_an_insert_with_no_outline_keeps_the_plot_that_holds_its_point()
        {
            var inserts = new[] { new JObject { ["layer"] = "HOUSES", ["block"] = "Villa_NEW2", ["x"] = 6.0, ["y"] = 10.0, ["mirrored"] = true } };
            var plots = new List<(string, string, double, double)> { ("101", "NEW2", 0, 0), ("102", "NEW2", 40, 0), ("103", "NEW2", 80, 0) };
            Dictionary<string, JObject> rows = Rows(Run(Drawing(plots, inserts)));
            Assert.True((bool)rows["101"]["mirror"]);
            Assert.Equal(JTokenType.Null, rows["102"]["mirror"].Type);
        }

        [Fact]
        public void R730_a_plot_no_villa_goes_on_leaves_the_table_and_is_listed_with_its_reason()
        {
            DataCheckOutput output = Run(Street(true));
            Assert.Equal(10, Rows(output).Count);
            Assert.False(Rows(output).ContainsKey("300"));
            JObject other = Rows(output, "otherPlots")["300"];
            Assert.StartsWith("no villa goes on it: ", (string)other["why"]);
            Assert.Equal("computed", (string)other["source"]["number"]);
            Assert.Equal(10, ((JArray)DataCheckLogic.PlotTable(output.Plots)["plots"]).Count);
            string line = (string)((JArray)output.Check["needs"]).OfType<JObject>().First(need => (string)need["need"] == "plotOutlines")["line"];
            Assert.Contains("1 plot with no villa sign listed apart with the reason", line);
        }

        [Fact]
        public void R730_with_no_villa_sign_on_any_plot_every_plot_stays_in_the_table()
        {
            var plots = new List<(string, string, double, double)> { ("101", null, 0, 0), ("102", null, 40, 0) };
            DataCheckOutput output = Run(Drawing(plots, new JObject[0]));
            Assert.Equal(2, Rows(output).Count);
            Assert.Empty((JArray)output.Plots["otherPlots"]);
        }

        [Fact]
        public void R732_two_plots_of_one_type_that_share_an_edge_and_no_block_are_a_pair_and_their_hand_stays_missing()
        {
            var plots = new List<(string, string, double, double)> { ("401", "NEW1", 0, 0), ("402", "NEW1", 12, 0), ("403", "NEW2", 60, 0), ("404", "NEW1", 100, 0), ("405", "NEW1", 112, 0), ("406", "NEW1", 124, 0) };
            Dictionary<string, JObject> rows = Rows(Run(Drawing(plots, new JObject[0])));
            Assert.Equal(JTokenType.Null, rows["401"]["mirror"].Type);
            Assert.Contains("pairs with plot 402", (string)rows["401"]["missing"]["mirror"]);
            Assert.Contains("pairs with plot 401", (string)rows["402"]["missing"]["mirror"]);
            Assert.DoesNotContain("pairs with", (string)rows["403"]["missing"]["mirror"]);
            // A row of three is no pair, the middle plot shares an edge with two
            Assert.DoesNotContain("pairs with", (string)rows["405"]["missing"]["mirror"]);
            Assert.DoesNotContain("pairs with", (string)rows["404"]["missing"]["mirror"]);
        }

        [Fact]
        public void R731_the_cover_keeps_the_plots_the_block_stands_on_and_drops_a_sliver()
        {
            var shapes = new List<KeyValuePair<int, List<List<double[]>>>>
            {
                Shape(0, 0, 0, 12, 20), Shape(1, 12, 0, 12, 20), Shape(2, 24, 0, 12, 20),
            };
            // Over plots 0 and 1, reaching 0.5 m into plot 2
            var outline = new List<double[]> { new[] { 1.0, 4 }, new[] { 24.5, 4 }, new[] { 24.5, 16 }, new[] { 1.0, 16 } };
            List<BlockCover.Covered> covered = BlockCover.Of(shapes, outline, new[] { 1.0, 0 }, new[] { 0, 1.0 });
            Assert.Equal(new[] { 0, 1 }, covered.Select(item => item.Plot).ToArray());
            Assert.Equal(new[] { 0, 1 }, covered.Select(item => item.Rank).ToArray());
            Assert.False(BlockCover.Hand(false, 0));
            Assert.True(BlockCover.Hand(false, 1));
            Assert.True(BlockCover.Hand(true, 2));
        }

        private static KeyValuePair<int, List<List<double[]>>> Shape(int index, double x, double y, double width, double height)
        {
            var ring = new List<double[]> { new[] { x, y }, new[] { x + width, y }, new[] { x + width, y + height }, new[] { x, y + height } };
            return new KeyValuePair<int, List<List<double[]>>>(index, new List<List<double[]>> { ring });
        }
    }
}
