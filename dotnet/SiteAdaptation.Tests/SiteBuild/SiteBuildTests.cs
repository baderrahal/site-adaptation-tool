using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;
using SiteAdaptation.Common.SiteBuild;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Made up values only, no project value. Each class is named by the rule it proves and breaks the thing it checks as well
    internal static class SiteBuildSamples
    {
        public static List<double[]> Square(double east, double north, double side)
        {
            return new List<double[]> { new[] { east, north }, new[] { east + side, north }, new[] { east + side, north + side }, new[] { east, north + side } };
        }

        public static PlanPlot Plot(string number, List<double[]> outline, string area = "N1", string type = "T1")
        {
            return new PlanPlot { In = new PlotIn { Number = number, Outline = outline, Area = area, VillaType = type } };
        }

        public static RuleForBuild Rule(string id, string text, string source = "rule", string status = "not confirmed")
        {
            return new RuleForBuild { Id = id, Name = id, Value = source == "missing" ? null : new JValue(text), Source = source, Rule = id, Status = status };
        }

        public static JObject Config()
        {
            return new JObject
            {
                ["coordinates"] = new JObject
                {
                    ["source"] = "cad", ["systems"] = new JObject { ["a"] = 32637 }, ["controlPointCount"] = 4, ["expectedEpsg"] = 32637, ["originRoundM"] = 1.0, ["angleToTrueNorthDeg"] = 0.0,
                    ["originElevationM"] = 0.0, ["positionName"] = "p", ["controlPointLimitMm"] = 1.0, ["utmEastM"] = new JArray(166000, 834000), ["utmNorthM"] = new JArray(0, 9300000), ["utmSouthNorthM"] = new JArray(1100000, 10000000),
                },
            };
        }
    }

    public sealed class R640_PlanTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "R640p_" + Guid.NewGuid().ToString("N"));

        public R640_PlanTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private string Table(string json)
        {
            string path = Path.Combine(folder, "plots.json");
            File.WriteAllText(path, json);
            return path;
        }

        [Fact]
        public void A_value_with_no_rule_is_missing_and_marks_the_plot()
        {
            var plots = PlanPlots.Make(new[] { new PlotIn { Number = "1", VillaType = "T1" } }, RuleNumbers.From(new RuleForBuild[0], new List<PlanIssue>()), new List<PlanIssue>());
            PlanValue ffl = plots[0].Get("ffl");
            Assert.True(ffl.IsMissing);
            Assert.Null(ffl.Value);
            Assert.Contains(plots[0].Marked, line => line.StartsWith("ffl:"));
        }

        [Fact]
        public void A_value_read_from_the_table_is_computed_and_a_second_row_of_the_same_plot_is_left_out()
        {
            var issues = new List<PlanIssue>();
            var plots = PlanPlots.Make(new[] { new PlotIn { Number = "1", VillaType = "T1" }, new PlotIn { Number = "1", VillaType = "T2" } }, RuleNumbers.From(new RuleForBuild[0], issues), issues);
            Assert.Single(plots);
            Assert.Equal("computed", plots[0].Get("villaType").Source);
            Assert.Equal("T1", (string)plots[0].Get("villaType").Value);
            Assert.Contains(issues, issue => issue.Plot == "1");
        }

        [Fact]
        public void A_table_that_cannot_be_read_gives_a_fault_in_plain_words_and_no_plots()
        {
            var issues = new List<PlanIssue>();
            string fault;
            Assert.Null(PlotTable.Read(Table("{ not json"), issues, out fault));
            Assert.Contains("plot table", fault);
            Assert.DoesNotContain("{", fault);
        }

        [Fact]
        public void A_row_with_no_number_is_left_out_with_an_issue_and_a_ring_of_two_points_is_no_outline()
        {
            var issues = new List<PlanIssue>();
            string fault;
            List<PlotIn> rows = PlotTable.Read(Table("{\"plots\":[{\"number\":null},{\"number\":\"7\",\"outline\":[[0,0],[1,1]]},{\"number\":\"8\",\"outline\":[[0,0],[1,0],[1,1]]}]}"), issues, out fault);
            Assert.Null(fault);
            Assert.Equal(new[] { "7", "8" }, rows.Select(row => row.Number).ToArray());
            Assert.Null(rows[0].Outline);
            Assert.Equal(3, rows[1].Outline.Count);
            Assert.Single(issues);
        }
    }

    public sealed class R641_RuleNumbersTests
    {
        private static RuleNumbers From(List<PlanIssue> issues, params RuleForBuild[] rules)
        {
            return RuleNumbers.From(rules, issues);
        }

        [Fact]
        public void A_number_in_the_plain_form_comes_with_the_rule_id_and_its_status()
        {
            var issues = new List<PlanIssue>();
            RuleNumbers numbers = From(issues, SiteBuildSamples.Rule("M10", "T1, T2 always have 3 steps"), SiteBuildSamples.Rule("M13", "Front setback 5.5 m, T1 4 m"), SiteBuildSamples.Rule("M11", "FFL + 1.7 m"));
            Assert.Equal(3, numbers.StepsByType["T2"]);
            Assert.Equal(5.5, numbers.Setback);
            Assert.Equal(4, numbers.SetbackByType["T1"]);
            Assert.Equal(1.7, numbers.BoundaryWallTopM);
            Assert.Empty(issues);
            var plots = PlanPlots.Make(new[] { new PlotIn { Number = "1", VillaType = "T1" } }, numbers, issues);
            PlanValue steps = plots[0].Get("steps");
            Assert.Equal("rule", steps.Source);
            Assert.Equal("M10", steps.Rule);
            Assert.Equal("not confirmed", steps.Status);
            Assert.Equal(4.0, (double)plots[0].Get("frontSetbackM").Value);
        }

        [Fact]
        public void A_text_in_another_form_gives_no_number_and_an_issue()
        {
            var issues = new List<PlanIssue>();
            RuleNumbers numbers = From(issues, SiteBuildSamples.Rule("M10", "about three steps"), SiteBuildSamples.Rule("M12", "high"));
            Assert.Empty(numbers.StepsByType);
            Assert.Null(numbers.GardenWallTopM);
            Assert.Equal(2, issues.Count);
        }

        [Fact]
        public void A_missing_rule_gives_no_number_and_no_issue()
        {
            var issues = new List<PlanIssue>();
            RuleNumbers numbers = From(issues, SiteBuildSamples.Rule("M10", null, "missing"));
            Assert.Empty(numbers.StepsByType);
            Assert.Empty(issues);
        }
    }

    public sealed class R642_CoordinatesTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "R642_" + Guid.NewGuid().ToString("N"));

        public R642_CoordinatesTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        [Fact]
        public void The_origin_is_the_centre_of_the_box_of_the_outlines_rounded_and_the_position_file_is_only_a_difference()
        {
            var plots = new[] { SiteBuildSamples.Plot("1", SiteBuildSamples.Square(500000, 2700000, 20)), SiteBuildSamples.Plot("2", SiteBuildSamples.Square(500040, 2700000, 20)) };
            string file = Path.Combine(folder, "position.json");
            File.WriteAllText(file, "{\"eastM\":500030.0,\"northM\":2700010.0,\"elevationM\":0,\"angleDeg\":0}");
            var issues = new List<PlanIssue>();
            JObject json = SiteCoordinates.Build(SiteBuildSamples.Config(), plots, file, issues);
            Assert.Equal("cad", (string)json["source"]);
            Assert.Equal(500030.0, (double)json["eastM"]);
            Assert.Equal(2700010.0, (double)json["northM"]);
            Assert.Equal(0.0, (double)json["differenceMm"]);
            File.WriteAllText(file, "{\"eastM\":500031.0,\"northM\":2700010.0}");
            JObject moved = SiteCoordinates.Build(SiteBuildSamples.Config(), plots, file, issues);
            Assert.Equal(-1000.0, (double)moved["differences"]["eastMm"]);
            Assert.Equal(1000.0, (double)moved["differenceMm"]);
            Assert.Equal(500030.0, (double)moved["eastM"]);
        }

        [Fact]
        public void No_outline_gives_a_missing_source_and_an_issue_and_a_broken_position_file_is_said()
        {
            var issues = new List<PlanIssue>();
            JObject none = SiteCoordinates.Build(SiteBuildSamples.Config(), new[] { SiteBuildSamples.Plot("1", null) }, null, issues);
            Assert.Equal("missing", (string)none["source"]);
            Assert.Single(issues);
            var more = new List<PlanIssue>();
            string file = Path.Combine(folder, "bad.json");
            File.WriteAllText(file, "{ nope");
            JObject json = SiteCoordinates.Build(SiteBuildSamples.Config(), new[] { SiteBuildSamples.Plot("1", SiteBuildSamples.Square(500000, 2700000, 20)) }, file, more);
            Assert.Equal(JTokenType.Null, json["differenceMm"].Type);
            Assert.Contains(more, issue => issue.Line.Contains("position file"));
        }
    }

    public sealed class R643_BlockingTests
    {
        [Fact]
        public void The_texts_a_person_reads_hold_no_brace_and_no_null()
        {
            string[] lines =
            {
                PlanTexts.NoPlots, PlanTexts.NoOutlines, PlanTexts.TableUnreadable("{\"a\": null}"), PlanTexts.PlotWithoutNumber("{x}"), PlanTexts.RuleTextUnreadable("M10", "null {"),
                PlanTexts.CoordinatesNoSettings("null"), PlanTexts.PositionFileUnreadable("{"),
            };
            foreach (string line in lines)
            {
                Assert.DoesNotContain("{", line);
                Assert.DoesNotContain("}", line);
                Assert.DoesNotContain("null", line);
            }
            Assert.Contains("plot", PlanTexts.NoPlots.ToLowerInvariant());
        }
    }

    public sealed class R644_StepsTests
    {
        [Fact]
        public void The_steps_come_in_the_order_of_the_buttons_and_count_the_plots_ready_for_each()
        {
            PlanPlot withType = SiteBuildSamples.Plot("1", SiteBuildSamples.Square(0, 0, 10));
            withType.Add("villaType", PlanValue.Of(new JValue("T1"), "computed"));
            withType.Add("ffl", PlanValue.Missing("none"));
            JArray steps = PlanSteps.Make(new[] { withType });
            Assert.Equal(new[] { 4, 5, 6, 7, 8 }, steps.Select(step => (int)step["button"]).ToArray());
            Assert.Equal(1, (int)steps[0]["ready"]);
            Assert.Equal(1, (int)steps[1]["ready"]);
            Assert.Equal(0, (int)steps[2]["ready"]);
            Assert.False(PlanSteps.IsReady(withType));
            Assert.Equal("6 Levels: 0 of 1 plots are ready.", (string)steps[2]["line"] == null ? "" : PlanTexts.StepLine(6, "Levels", 0, 1));
            Assert.Equal(PlanTexts.StepLine(6, "Levels", 0, 1), (string)steps[2]["line"]);
        }
    }

    public sealed class R645_PlotOutlinesTests
    {
        [Fact]
        public void A_table_with_no_plots_gets_no_note()
        {
            Assert.Empty(PlotOutlines.Add(new JObject(), null, null));
            Assert.Empty(PlotOutlines.Add(new JObject { ["plots"] = new JArray() }, null, null));
        }
    }

    public sealed class R646_OverlapTests
    {
        private static List<PlanIssue> Run(out JObject result, params PlanPlot[] plots)
        {
            var issues = new List<PlanIssue>();
            result = MassOverlap.Check(plots, null, 1.0, new[] { 0.0, 0.0 }, issues);
            return issues;
        }

        [Fact]
        public void Outlines_that_share_an_edge_or_overlap_by_a_millimetre_do_not_count()
        {
            JObject result;
            var issues = Run(out result, SiteBuildSamples.Plot("1", SiteBuildSamples.Square(0, 0, 10)), SiteBuildSamples.Plot("2", SiteBuildSamples.Square(10, 0, 10)), SiteBuildSamples.Plot("3", SiteBuildSamples.Square(19.9995, 0, 10)));
            Assert.Equal(0, (int)result["plots"]["count"]);
            Assert.Empty(issues);
        }

        [Fact]
        public void Outlines_that_overlap_by_more_are_named_and_a_far_pair_is_not()
        {
            JObject result;
            var issues = Run(out result, SiteBuildSamples.Plot("1", SiteBuildSamples.Square(0, 0, 10)), SiteBuildSamples.Plot("2", SiteBuildSamples.Square(9.99, 0, 10)), SiteBuildSamples.Plot("3", SiteBuildSamples.Square(100, 0, 10)));
            Assert.Equal(1, (int)result["plots"]["count"]);
            Assert.Equal("1", (string)result["plots"]["listed"][0]["a"]);
            Assert.Single(issues);
            Assert.Contains("plot 1 and plot 2", issues[0].Line);
        }

        [Fact]
        public void Masses_are_checked_when_they_are_given_and_say_so_when_not()
        {
            var issues = new List<PlanIssue>();
            var masses = new JObject { ["masses"] = new JArray(
                new JObject { ["plot"] = "1", ["ring"] = JArray.FromObject(SiteBuildSamples.Square(0, 0, 5)) },
                new JObject { ["plot"] = "2", ["ring"] = JArray.FromObject(SiteBuildSamples.Square(4, 0, 5)) }) };
            JObject result = MassOverlap.Check(new PlanPlot[0], masses, 1.0, new[] { 0.0, 0.0 }, issues);
            Assert.Equal(1, (int)result["masses"]["count"]);
            Assert.Equal(PlanTexts.NoMassesGiven, (string)MassOverlap.Check(new PlanPlot[0], null, 1.0, new[] { 0.0, 0.0 }, issues)["masses"]["line"]);
        }
    }

    public sealed class R647_CutTests
    {
        [Fact]
        public void The_cut_follows_the_plots_of_a_neighbourhood_and_is_smaller_than_the_hull_of_an_L()
        {
            var plots = new[]
            {
                SiteBuildSamples.Plot("1", SiteBuildSamples.Square(0, 0, 10)), SiteBuildSamples.Plot("2", SiteBuildSamples.Square(10, 0, 10)), SiteBuildSamples.Plot("3", SiteBuildSamples.Square(0, 10, 10)),
            };
            JObject cut = NeighbourhoodCut.Build(plots, null, 0.0, 5.0, new[] { 0.0, 0.0 }, new List<PlanIssue>());
            JObject area = (JObject)cut["boundaries"][0];
            Assert.Equal(300.0, (double)area["cutM2"], 1);
            Assert.Equal(350.0, (double)area["hullM2"], 1);
        }

        [Fact]
        public void A_road_edge_point_over_the_limit_is_listed_and_one_on_the_cut_is_not()
        {
            var plots = new[] { SiteBuildSamples.Plot("1", SiteBuildSamples.Square(0, 0, 10)) };
            var edge = new JObject { ["points"] = new JArray(
                new JObject { ["area"] = "N1", ["east"] = 10.0, ["north"] = 5.0 },
                new JObject { ["area"] = "N1", ["east"] = 10.02, ["north"] = 5.0 }) };
            var issues = new List<PlanIssue>();
            JObject cut = NeighbourhoodCut.Build(plots, edge, 0.0, 5.0, new[] { 0.0, 0.0 }, issues);
            Assert.Equal(2, (int)cut["roadEdge"]["checked"]);
            Assert.Equal(1, ((JArray)cut["roadEdge"]["over"]).Count);
            Assert.Equal(20.0, (double)cut["roadEdge"]["over"][0]["distanceMm"], 1);
        }

        [Fact]
        public void A_plot_with_no_neighbourhood_is_left_out_with_an_issue()
        {
            var issues = new List<PlanIssue>();
            PlanPlot lost = SiteBuildSamples.Plot("9", SiteBuildSamples.Square(0, 0, 10), null);
            NeighbourhoodCut.Build(new[] { lost }, null, 5.0, 5.0, new[] { 0.0, 0.0 }, issues);
            Assert.Contains(issues, issue => issue.Plot == "9");
        }
    }

    public sealed class R648_FloorsToReplaceTests
    {
        private static JObject Placed(string plot, List<double[]> outline)
        {
            return new JObject { ["floors"] = new JArray(new JObject { ["plot"] = plot, ["outline"] = JArray.FromObject(outline) }) };
        }

        [Fact]
        public void A_floor_with_the_outline_of_the_plot_in_any_order_is_kept()
        {
            List<double[]> ring = SiteBuildSamples.Square(0, 0, 10);
            var turned = new List<double[]> { ring[2], ring[1], ring[0], ring[3], ring[2] };
            JObject result = FloorsToReplace.Check(new[] { SiteBuildSamples.Plot("1", ring) }, Placed("1", turned), 0.5, new List<PlanIssue>());
            Assert.Equal(1, (int)result["checked"]);
            Assert.Empty((JArray)result["replace"]);
        }

        [Fact]
        public void A_floor_with_a_changed_outline_is_to_be_replaced_and_a_floor_of_no_plot_is_said()
        {
            var issues = new List<PlanIssue>();
            JObject result = FloorsToReplace.Check(new[] { SiteBuildSamples.Plot("1", SiteBuildSamples.Square(0, 0, 10)) }, Placed("1", SiteBuildSamples.Square(0, 0, 10.1)), 0.5, issues);
            Assert.Equal("1", (string)result["replace"][0]);
            Assert.Single(issues);
            FloorsToReplace.Check(new[] { SiteBuildSamples.Plot("1", SiteBuildSamples.Square(0, 0, 10)) }, Placed("77", SiteBuildSamples.Square(0, 0, 10)), 0.5, issues);
            Assert.Equal(2, issues.Count);
        }

        [Fact]
        public void An_empty_model_gives_an_empty_list_and_says_so()
        {
            JObject result = FloorsToReplace.Check(new[] { SiteBuildSamples.Plot("1", SiteBuildSamples.Square(0, 0, 10)) }, null, 0.5, new List<PlanIssue>());
            Assert.Empty((JArray)result["replace"]);
            Assert.Equal(PlanTexts.NoPlacedFloors, (string)result["line"]);
        }
    }
}
