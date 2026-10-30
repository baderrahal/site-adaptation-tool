using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Review;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R402. Button 3 Review data in Common, on net48 and net8.0. Made up numbers only, no coordinate or level of a real model
    public sealed class ReviewDataLogicTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "review-" + Guid.NewGuid().ToString("N"));

        public ReviewDataLogicTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static JObject Table()
        {
            return JObject.Parse(@"{
  '1001': { 'baseplanLabel': '1001', 'neighbourhood': '012', 'locationPlanNumber': '12', 'officialNumber': '1012', 'typologyRev9': 'VL4', 'footprintMirrored': true, 'faceAreaM2': 412.5 },
  '1002': { 'baseplanLabel': '1002', 'neighbourhood': '012', 'locationPlanNumber': '3', 'officialNumber': '1003', 'typologyRev9': 'TH2', 'footprintMirrored': false, 'faceAreaM2': 160.0 },
  '1003': { 'baseplanLabel': '1003', 'neighbourhood': '013', 'locationPlanNumber': '4', 'officialNumber': '2004', 'typologyRev9': 'VL4', 'footprintMirrored': null, 'faceAreaM2': 300.0 },
  '1004': { 'baseplanLabel': '1004', 'neighbourhood': null, 'locationPlanNumber': null, 'officialNumber': null, 'typologyRev9': null, 'footprintMirrored': null, 'faceAreaM2': 90.0 },
  '1005': { 'baseplanLabel': '1005', 'neighbourhood': '012', 'locationPlanNumber': null, 'officialNumber': '1020', 'typologyRev9': null, 'footprintMirrored': null, 'faceAreaM2': 80.0 }
}");
        }

        private static double[][] Square()
        {
            return new[] { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 10.0 }, new[] { 0.0, 10.0 } };
        }

        [Fact]
        public void R402_the_rows_are_the_plots_of_the_nbh_in_the_order_of_their_numbers_and_each_names_its_manual_rule()
        {
            List<ReviewRow> rows = ReviewTable.Rows(Table(), "012", null, null, null);

            Assert.Equal(new[] { "3", "12", "1020" }, rows.Select(row => row.PlotNo));
            Assert.Equal(new[] { "1002", "1001", "1005" }, rows.Select(row => row.CadLabel));
            Assert.Equal(new[] { "TH2", "VL4", null }, rows.Select(row => row.VillaType));
            Assert.Equal(new bool?[] { false, true, null }, rows.Select(row => row.Mirror));
            Assert.Equal(new[] { 160.0, 412.5, 80.0 }, rows.Select(row => row.AreaM2));
            Assert.All(rows, row => Assert.Equal(ReviewRules.RowRule, row.RuleId));
            Assert.Matches("^M(0[1-9]|1[0-5])$", ReviewRules.RowRule);
            // Before button 4 builds the plan nothing gives the cluster, the front edge, the road at the front or the corners
            Assert.All(rows, row =>
            {
                Assert.Null(row.Cluster);
                Assert.Null(row.FrontEdge);
                Assert.Null(row.RoadAtFront);
                Assert.Empty(row.Corners);
            });
        }

        [Fact]
        public void R402_a_plan_fact_gives_the_cluster_the_mirror_and_the_road_and_the_corners_start_at_the_left_front_corner_seen_from_the_road()
        {
            var plan = new Dictionary<string, ReviewPlanFact>
            {
                ["1001"] = new ReviewPlanFact { Cluster = "C3", Mirror = false, FrontEdge = 0, RoadAtFront = "R7" },
            };
            var rings = new Dictionary<string, double[][]> { ["1001"] = Square() };
            ReviewRow row = ReviewTable.Rows(Table(), "012", plan, rings, null).Single(found => found.CadLabel == "1001");

            Assert.Equal("C3", row.Cluster);
            Assert.False(row.Mirror);
            Assert.Equal("R7", row.RoadAtFront);
            Assert.Equal("edge 1", row.FrontEdge);
            Assert.Equal(new[] { 1, 2, 3, 4 }, row.Corners.Select(corner => corner.Number));
            Assert.Equal(new[] { 0.0, 10.0, 10.0, 0.0 }, row.Corners.Select(corner => corner.SharedE));
            Assert.Equal(new[] { 0.0, 0.0, 10.0, 10.0 }, row.Corners.Select(corner => corner.SharedN));
        }

        [Fact]
        public void R402_corner_one_is_the_left_front_corner_for_a_ring_either_way_round_and_for_each_front_edge()
        {
            double[][] ccw = Square();
            double[][] cw = ccw.Reverse().ToArray();
            // Road below, edge from (0,0) to (10,0): standing at the road and facing the plot, left is the west
            Assert.Equal(new[] { 0.0, 0.0 }, ReviewCorners.Order(ccw, 0)[0]);
            // The same ring written the other way round, its front edge is the one from (10,0) to (0,0), index 2
            double[][] fromCw = ReviewCorners.Order(cw, 2);
            Assert.Equal(new[] { 0.0, 0.0 }, fromCw[0]);
            Assert.Equal(new[] { 10.0, 0.0 }, fromCw[1]);
            // Road above, edge from (10,10) to (0,10): facing south, left is the east
            double[][] top = ReviewCorners.Order(ccw, 2);
            Assert.Equal(new[] { 10.0, 10.0 }, top[0]);
            Assert.Equal(new[] { 0.0, 10.0 }, top[1]);
            Assert.Equal(new[] { 0.0, 0.0 }, top[2]);
            Assert.Equal(new[] { 10.0, 0.0 }, top[3]);
            // Road to the east, edge from (10,0) to (10,10): facing west, left is the south
            Assert.Equal(new[] { 10.0, 0.0 }, ReviewCorners.Order(ccw, 1)[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => ReviewCorners.Order(ccw, 4));
        }

        [Fact]
        public void R402_the_shared_coordinates_come_from_the_site_file_and_the_model_coordinates_follow_the_survey_point()
        {
            JObject site = JObject.Parse("{ 'angleToTrueNorthDeg': 0.0, 'surveyPoint': { 'position': [100.0, 200.0, 0.0], 'sharedPosition': [5000.0, 6000.0, 0.0] } }");
            var plan = new Dictionary<string, ReviewPlanFact> { ["1001"] = new ReviewPlanFact { FrontEdge = 0 } };
            var rings = new Dictionary<string, double[][]> { ["1001"] = new[] { new[] { 5010.0, 6020.0 }, new[] { 5030.0, 6020.0 }, new[] { 5030.0, 6040.0 }, new[] { 5010.0, 6040.0 } } };
            ReviewCorner first = ReviewTable.Rows(Table(), "012", plan, rings, site).Single(row => row.CadLabel == "1001").Corners[0];

            Assert.Equal(5010.0, first.SharedE);
            Assert.Equal(6020.0, first.SharedN);
            Assert.Equal(110.0, first.X);
            Assert.Equal(220.0, first.Y);

            // R83 refuses an angle that is not 0, so a site file with one gives no model coordinate and the shared coordinates stay
            JObject turned = JObject.Parse("{ 'angleToTrueNorthDeg': 12.0, 'surveyPoint': { 'position': [100.0, 200.0, 0.0], 'sharedPosition': [5000.0, 6000.0, 0.0] } }");
            ReviewCorner none = ReviewTable.Rows(Table(), "012", plan, rings, turned).Single(row => row.CadLabel == "1001").Corners[0];
            Assert.Null(none.X);
            Assert.Equal(5010.0, none.SharedE);
        }

        private static ReviewState State(string hash, params ReviewCheck[] checks)
        {
            return new ReviewState(ReviewTable.Rows(Table(), "012", null, null, null), checks, "read-1", hash);
        }

        [Fact]
        public void R402_a_plot_is_not_checked_until_the_team_marks_it_and_the_count_follows()
        {
            ReviewState state = State("h1");

            Assert.Equal((0, 3), (state.CheckedCount, state.Total));
            Assert.All(state.Rows, row => Assert.Equal(ReviewStatus.NotChecked, state.StatusOf(row.PlotNo)));
            state.Mark("3", ReviewCheckValue.Ok, "");
            state.Mark("12", ReviewCheckValue.Wrong, "mirror drawn the other way");
            Assert.Equal((2, 3), (state.CheckedCount, state.Total));
            Assert.Equal(ReviewStatus.Ok, state.StatusOf("3"));
            Assert.Equal(ReviewStatus.Wrong, state.StatusOf("12"));
            Assert.Equal("mirror drawn the other way", state.NoteOf("12"));
            Assert.Throws<ArgumentException>(() => state.Mark("999", ReviewCheckValue.Ok, ""));
            Assert.Throws<ArgumentException>(() => state.Mark("3", "Maybe", ""));
            Assert.True(state.Changed);
        }

        [Fact]
        public void R402_the_chips_All_Not_checked_OK_Wrong_and_the_find_box_pick_the_rows()
        {
            ReviewState state = State("h1");
            state.Mark("3", ReviewCheckValue.Ok, "");
            state.Mark("12", ReviewCheckValue.Wrong, "x");

            Assert.Equal(new[] { "3", "12", "1020" }, state.Filter(ReviewChip.All, "").Select(row => row.PlotNo));
            Assert.Equal(new[] { "1020" }, state.Filter(ReviewChip.NotChecked, "").Select(row => row.PlotNo));
            Assert.Equal(new[] { "3" }, state.Filter(ReviewChip.Ok, "").Select(row => row.PlotNo));
            Assert.Equal(new[] { "12" }, state.Filter(ReviewChip.Wrong, "").Select(row => row.PlotNo));
            Assert.Equal((3, 1, 1, 1), (state.CountOf(ReviewChip.All), state.CountOf(ReviewChip.NotChecked), state.CountOf(ReviewChip.Ok), state.CountOf(ReviewChip.Wrong)));
            // Find plot takes a part of the plot number or of the CAD label
            Assert.Equal(new[] { "12" }, state.Filter(ReviewChip.All, "12").Select(row => row.PlotNo));
            Assert.Equal(new[] { "12" }, state.Filter(ReviewChip.All, "1001").Select(row => row.PlotNo));
            Assert.Equal(new[] { "1020" }, state.Filter(ReviewChip.NotChecked, " 102 ").Select(row => row.PlotNo));
            Assert.Empty(state.Filter(ReviewChip.Wrong, "3"));
        }

        [Fact]
        public void R402_save_review_writes_review_nbh_json_by_a_temporary_name_and_a_rename_with_an_entry_per_checked_plot_only()
        {
            ReviewState state = State("h1");
            state.Mark("3", ReviewCheckValue.Ok, "");
            state.Mark("12", ReviewCheckValue.Wrong, "mirror");
            string path = ReviewFile.Write(folder, "012", state.ToChecks(), "run-9");

            Assert.Equal(Path.Combine(folder, "review_012.json"), path);
            Assert.Equal(new[] { "review_012.json" }, Directory.GetFiles(folder).Select(Path.GetFileName));
            JObject saved = JObject.Parse(File.ReadAllText(path));
            Assert.Equal(2, ((JArray)saved["checks"]).Count);
            Assert.Equal(new[] { "12", "3" }, ((JArray)saved["checks"]).Select(entry => (string)entry["plot"]).OrderBy(plot => plot, StringComparer.Ordinal));
            JToken wrong = ((JArray)saved["checks"]).Single(entry => (string)entry["plot"] == "12");
            Assert.Equal("Wrong", (string)wrong["check"]);
            Assert.Equal("mirror", (string)wrong["note"]);
            Assert.Equal("read-1", (string)wrong["runId"]);
            Assert.Equal("h1", (string)wrong["plotTableHash"]);

            List<ReviewCheck> back = ReviewFile.Read(path);
            Assert.Equal(2, back.Count);
            Assert.Equal("Wrong", back.Single(entry => entry.Plot == "12").Check);
            // A file that is not there is no check yet, a file that is no review file is named
            Assert.Empty(ReviewFile.Read(Path.Combine(folder, "review_013.json")));
            File.WriteAllText(Path.Combine(folder, "review_014.json"), "{ \"format\": \"other\" }");
            InvalidDataException error = Assert.Throws<InvalidDataException>(() => ReviewFile.Read(Path.Combine(folder, "review_014.json")));
            Assert.Contains("review_014.json", error.Message);
        }

        [Fact]
        public void R402_a_check_made_on_an_earlier_read_shows_as_not_checked_with_its_note_kept_and_is_replaced_by_a_new_mark()
        {
            var old = new ReviewCheck { Plot = "12", Check = ReviewCheckValue.Wrong, Note = "front edge", RunId = "read-0", PlotTableHash = "h0" };
            var current = new ReviewCheck { Plot = "3", Check = ReviewCheckValue.Ok, Note = "", RunId = "read-1", PlotTableHash = "h1" };
            ReviewState state = State("h1", old, current);

            Assert.Equal(ReviewStatus.NotChecked, state.StatusOf("12"));
            Assert.Equal("front edge", state.EarlierNoteOf("12"));
            Assert.Equal("", state.NoteOf("12"));
            Assert.Equal(ReviewStatus.Ok, state.StatusOf("3"));
            Assert.Equal((1, 3), (state.CheckedCount, state.Total));
            // 4 Check lists only the plots marked Wrong on the read it holds now
            Assert.DoesNotContain(state.ToChecks(), check => check.Check == ReviewCheckValue.Wrong && check.PlotTableHash == "h1");
            // The earlier entry stays in the file until the team marks the plot again
            Assert.Contains(state.ToChecks(), check => check.Plot == "12" && check.PlotTableHash == "h0");
            state.Mark("12", ReviewCheckValue.Ok, "");
            Assert.Single(state.ToChecks(), check => check.Plot == "12");
            Assert.Equal("h1", state.ToChecks().Single(check => check.Plot == "12").PlotTableHash);
            Assert.Equal("", state.EarlierNoteOf("12"));
        }

        [Fact]
        public void R402_check_reads_the_plots_marked_wrong_from_review_nbh_json_for_the_plot_table_it_holds()
        {
            ReviewFile.Write(folder, "012", new[]
            {
                new ReviewCheck { Plot = "12", Check = ReviewCheckValue.Wrong, Note = "mirror", RunId = "r", PlotTableHash = "h1" },
                new ReviewCheck { Plot = "3", Check = ReviewCheckValue.Ok, Note = "", RunId = "r", PlotTableHash = "h1" },
                new ReviewCheck { Plot = "20", Check = ReviewCheckValue.Wrong, Note = "old", RunId = "r0", PlotTableHash = "h0" },
            }, "run-9");

            Assert.Equal(new[] { "12" }, ReviewFile.WrongPlots(folder, "012", "h1").Select(check => check.Plot));
            Assert.Equal(new[] { "12", "20" }, ReviewFile.WrongPlots(folder, "012", null).Select(check => check.Plot).OrderBy(plot => plot, StringComparer.Ordinal));
            Assert.Empty(ReviewFile.WrongPlots(folder, "013", "h1"));
        }

        [Fact]
        public void R402_the_run_shows_the_window_with_the_title_the_band_line_and_saves_what_the_window_asks_to_save()
        {
            var input = new ReviewInput
            {
                Nbh = "012",
                Rows = ReviewTable.Rows(Table(), "012", null, null, null),
                Checks = new List<ReviewCheck>(),
                PlotTableHash = "h1",
                ReadRunId = "read-1",
                Sources = new[] { "Baseplan.dwg", "Plot numbers.pdf" },
            };
            ReviewView shown = null;
            ReviewCallbacks callbacks = null;
            var dialog = new FakeDialog((opened, back) =>
            {
                shown = opened;
                callbacks = back;
                opened.State.Mark("12", ReviewCheckValue.Wrong, "mirror");
                Assert.EndsWith("review_012.json", back.Save(opened.State.ToChecks()));
                return new ReviewAnswer { Saved = true };
            });
            var run = new ReviewDataRun(context => input, dialog, null);

            bool saved = run.Show(input, "run-9", folder);

            Assert.True(saved);
            Assert.Equal("012", shown.Nbh);
            Assert.Equal(3, shown.State.Total);
            Assert.Equal(new[] { "Baseplan.dwg", "Plot numbers.pdf" }, shown.Sources);
            Assert.Equal("12", ReviewFile.WrongPlots(folder, "012", "h1").Single().Plot);
            Assert.Single(Directory.GetFiles(folder));
            // Zoom has no Revit side set, so it says so and throws nothing
            Assert.Contains("Zoom to plot is not built yet", callbacks.ZoomMessage(shown.State.Rows[0]));
            Assert.Contains("Export to Excel is not built yet", callbacks.ExportMessage());
        }

        private sealed class FakeDialog : IReviewDialog
        {
            private readonly Func<ReviewView, ReviewCallbacks, ReviewAnswer> act;

            public FakeDialog(Func<ReviewView, ReviewCallbacks, ReviewAnswer> act)
            {
                this.act = act;
            }

            public ReviewAnswer Show(ReviewView shown, ReviewCallbacks callbacks)
            {
                return act(shown, callbacks);
            }
        }
    }
}
