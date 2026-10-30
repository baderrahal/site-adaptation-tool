using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteAdaptation.AddIn.ReviewData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R402. The window of button 3 Review data, its view model and the session that drives its buttons with no click. WPF, so net48 only
    public sealed class ReviewDataTests
    {
        private static string Part()
        {
            string text = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "13-workflow-changes.md")).Replace("\r\n", "\n");
            int start = text.IndexOf("### Review data (5c)", StringComparison.Ordinal);
            return text.Substring(start, text.IndexOf("\n### ", start + 5, StringComparison.Ordinal) - start);
        }

        [Fact]
        public void R402_the_texts_of_the_window_are_those_of_the_workflow_changes_word_for_word()
        {
            string part = Part();

            Assert.Contains("\"" + ReviewDataText.HowTo + "\"", part);
            Assert.Contains("\"3 Review data, NBH [code], [n] plots\"", part);
            Assert.Equal("3 Review data, NBH <n>, <n> plots", ReviewDataText.Title);
            Assert.Contains("\"" + ReviewDataText.ReadFrom.Replace(" <n>", "") + "\"", part);
            Assert.Contains("\"[n] of [n] checked by the team\"", part);
            Assert.Contains("buttons " + ReviewDataText.ZoomToPlot + ", " + ReviewDataText.ExportToExcel + ", " + ReviewDataText.SaveReview, part);
            Assert.Contains("Chips " + string.Join(", ", ReviewDataText.All, ReviewDataText.NotChecked, ReviewDataText.Ok, ReviewDataText.Wrong) + ". A " + ReviewDataText.FindPlot + " box.", part);
            Assert.Contains("Columns " + string.Join(", ", ReviewDataText.PlotNo, ReviewDataText.CadLabel, ReviewDataText.VillaType, ReviewDataText.Mirror, ReviewDataText.Cluster,
                ReviewDataText.AreaM2, ReviewDataText.RoadAtFront, ReviewDataText.TeamCheck) + ".", part);
            Assert.Contains("Side box: " + string.Join(", ", ReviewDataText.SelectedPlot, ReviewDataText.CadLabel, ReviewDataText.Nbh, ReviewDataText.Cluster, ReviewDataText.Mirror,
                ReviewDataText.FrontEdge, ReviewDataText.RoadAtFront) + ", a " + ReviewDataText.PlotCorners + " table (Corner 1 to 4, " + ReviewDataText.X + ", " + ReviewDataText.Y + ", "
                + "shared coordinates", part);
            Assert.Contains("Team check OK or Wrong, a note \"" + ReviewDataText.WhatIsWrong + "\"", part);
        }

        [Fact]
        public void R402_the_view_model_shows_the_title_the_sources_the_count_and_not_read_yet_for_what_only_button_4_gives()
        {
            var model = new ReviewDataViewModel(Shots.SampleReviewView(false));

            Assert.Equal("3 Review data, NBH 012, 5 plots", model.Title);
            Assert.Equal("Read from ROSHN Makkah Baseplan.dwg, Makkah Building Footprint.dwg", model.ReadFromLine);
            Assert.Equal("3 of 5 checked by the team", model.CheckedLine);
            Assert.Equal(5, model.Rows.Count);
            Assert.All(model.Rows, row =>
            {
                Assert.Equal("not read yet", row.Cluster);
                Assert.Equal("not read yet", row.RoadAtFront);
                Assert.Equal("M01", row.Row.RuleId);
            });
            Assert.Equal(new[] { "All 5", "Not checked 2", "OK 2", "Wrong 1" }, model.Chips.Select(chip => chip.Text));
            Assert.Equal("not read yet", model.Rows[4].VillaType);
            Assert.Equal("not read yet", model.Rows[4].Mirror);
            Assert.Equal("Yes", model.Rows[0].Mirror);
            Assert.Equal("No", model.Rows[1].Mirror);
            Assert.Equal("300.00", model.Rows[0].Area);
            Assert.Equal("Manual rule M01", model.Rows[0].RuleTip);
        }

        [Fact]
        public void R402_the_side_box_shows_the_selected_plot_its_sources_and_its_corners_from_corner_1_with_the_site_file_coordinates()
        {
            var model = new ReviewDataViewModel(Shots.SampleReviewView(true));
            Assert.True(model.NoneSelected);
            Assert.Equal("Pick a plot from the table.", model.PickText);

            model.Selected = model.Rows[0];

            Assert.True(model.HasSelected);
            Assert.Equal("31", model.SelectedPlot);
            Assert.Equal("1001", model.SideCadLabel);
            Assert.Equal("012", model.SideNbh);
            Assert.Equal("C1", model.SideCluster);
            Assert.Equal("Yes", model.SideMirror);
            Assert.Equal("Edge 1", model.SideFrontEdge);
            Assert.Equal("R1", model.SideRoad);
            Assert.Equal("Manual rule M01", model.ManualRuleLine);
            Assert.Equal(new[] { "Corner 1", "Corner 2", "Corner 3", "Corner 4" }, model.Corners.Select(corner => corner.Name));
            Assert.Equal("100.000", model.Corners[0].X);
            Assert.Equal("200.000", model.Corners[0].Y);
            Assert.Equal("5000.000, 6000.000", model.Corners[0].Shared);

            // A plot the plan has not reached shows its edge and corners as not read yet
            model.Selected = model.Rows[3];
            Assert.Equal("not read yet", model.SideFrontEdge);
            Assert.Empty(model.Corners);
        }

        [Fact]
        public void R402_the_session_marks_a_plot_ok_or_wrong_with_its_note_counts_it_and_saves_what_the_state_holds()
        {
            ReviewView view = Shots.SampleReviewView(true);
            IReadOnlyList<ReviewCheck> saved = null;
            var session = new ReviewDataSession(view, new ReviewCallbacks { Save = checks => { saved = checks; return "review_012.json"; } });
            session.Model.Selected = session.Model.Rows[4];
            session.Model.Note = "Mirror drawn the other way";
            session.Mark(ReviewCheckValue.Wrong);

            Assert.Equal("4 of 5 checked by the team", session.Model.CheckedLine);
            Assert.True(session.Model.IsWrong);
            Assert.False(session.Model.IsOk);
            Assert.Equal("Wrong", session.Model.Rows[4].Check);
            Assert.True(session.Model.NotSavedShown);
            // A note edited after the mark goes into the same check
            session.Model.Note = "Mirror drawn the other way, front edge too";
            Assert.Equal("Mirror drawn the other way, front edge too", view.State.NoteOf("35"));

            session.Save();

            Assert.True(session.Saved);
            Assert.Equal("Review saved.", session.Model.Message);
            Assert.False(session.Model.NotSavedShown);
            Assert.Equal(4, saved.Count);
            Assert.Equal("Wrong", saved.Single(check => check.Plot == "35").Check);
        }

        [Fact]
        public void R402_a_save_that_throws_leaves_the_window_open_with_its_reason_and_zoom_and_export_say_when_they_are_not_built()
        {
            var failing = new ReviewDataSession(Shots.SampleReviewView(true), new ReviewCallbacks { Save = checks => throw new IOException("the work folder is read only") });
            failing.Save();
            Assert.False(failing.Saved);
            Assert.Contains("the work folder is read only", failing.Model.Message);

            var session = new ReviewDataSession(Shots.SampleReviewView(true), new ReviewCallbacks());
            session.Zoom();
            Assert.Equal("", session.Model.Message);
            session.Model.Selected = session.Model.Rows[0];
            session.Zoom();
            Assert.Equal("Zoom to plot is not built yet.", session.Model.Message);
            session.Export();
            Assert.Equal("Export to Excel is not built yet.", session.Model.Message);

            ReviewRow zoomed = null;
            var wired = new ReviewDataSession(Shots.SampleReviewView(true), new ReviewCallbacks { Zoom = row => zoomed = row, Export = () => "Exported." });
            wired.Model.Selected = wired.Model.Rows[2];
            wired.Zoom();
            Assert.Equal("33", zoomed.PlotNo);
            Assert.Equal("", wired.Model.Message);
            wired.Export();
            Assert.Equal("Exported.", wired.Model.Message);
        }

        [Fact]
        public void R402_the_chips_and_the_find_box_pick_the_rows_and_the_selected_plot_stays_only_while_it_is_among_them()
        {
            var model = new ReviewDataViewModel(Shots.SampleReviewView(true));
            model.Selected = model.Rows.Single(row => row.PlotNo == "34");

            model.SetChip(ReviewChip.Wrong);
            Assert.Equal(new[] { "34" }, model.Rows.Select(row => row.PlotNo));
            Assert.Equal("34", model.SelectedPlot);
            Assert.Equal(new[] { "All 5", "Not checked 2", "OK 2", "Wrong 1" }, model.Chips.Select(chip => chip.Text));
            Assert.True(model.Chips.Single(chip => chip.Active).Chip == ReviewChip.Wrong);

            model.SetChip(ReviewChip.Ok);
            Assert.Equal(new[] { "31", "32" }, model.Rows.Select(row => row.PlotNo));
            Assert.True(model.NoneSelected);

            model.SetChip(ReviewChip.All);
            model.Find = "1004";
            Assert.Equal(new[] { "34" }, model.Rows.Select(row => row.PlotNo));
        }

        [Fact]
        public void R402_a_check_of_an_earlier_read_shows_as_not_checked_with_its_earlier_note_in_the_side_box()
        {
            ReviewView view = Shots.SampleReviewView(true);
            var stale = new List<ReviewCheck> { new ReviewCheck { Plot = "31", Check = ReviewCheckValue.Wrong, Note = "front edge", RunId = "old", PlotTableHash = "old" } };
            var model = new ReviewDataViewModel(new ReviewView { Nbh = view.Nbh, Sources = view.Sources, State = new ReviewState(view.State.Rows, stale, "new", "new") });

            model.Selected = model.Rows[0];

            Assert.Equal("Not checked", model.Rows[0].Check);
            Assert.False(model.IsWrong);
            Assert.True(model.EarlierNoteShown);
            Assert.Equal("Earlier note: front edge", model.EarlierNoteLine);
            Assert.Equal("", model.Note);
        }

        [Fact]
        public void R402_the_window_has_the_band_the_guide_line_the_chips_the_table_and_the_side_box_340_wide_on_the_panel()
        {
            OnSta(() =>
            {
                ReviewDataWindow window = Shots.LaidOutReview(Shots.SampleReviewView(true));
                List<FrameworkElement> all = Visuals(Shots.Body(window)).OfType<FrameworkElement>().ToList();
                List<string> texts = all.OfType<TextBlock>().Select(block => block.Text).ToList();

                Assert.Equal("3 Review data, NBH 012, 5 plots", window.Title);
                foreach (string text in new[] { "3 Review data, NBH 012, 5 plots", "3 of 5 checked by the team", ReviewDataText.HowTo, "Plot No", "CAD label", "Villa type", "Mirror", "Cluster",
                    "Area m2", "Road at front", "Team check", "Selected plot", "Plot corners", "Shared coordinates", "What is wrong", "Find plot", "X", "Y", "Corner 1", "Corner 4" })
                {
                    Assert.Contains(text, texts);
                }
                Assert.Contains(texts, text => text.StartsWith("Read from "));
                Assert.Equal(new[] { "Zoom to plot", "Export to Excel", "Save review", "OK", "Wrong" },
                    all.OfType<Button>().Select(button => button.Content).OfType<string>().Where(content => new[] { "Zoom to plot", "Export to Excel", "Save review", "OK", "Wrong" }.Contains(content)).ToArray());

                Border band = all.OfType<Border>().First(border => border.Background == window.FindResource("SA.Band"));
                Assert.Contains(Visuals(band).OfType<TextBlock>(), block => block.Text.StartsWith("3 Review data") && block.FontSize == 20.0);
                Assert.Equal(window.FindResource("SA.Button.Primary"), Visuals(band).OfType<Button>().Single(button => (string)button.Content == "Save review").Style);

                Border guide = all.OfType<Border>().First(border => border.Background == window.FindResource("SA.Guide"));
                Assert.Equal(window.FindResource("SA.GuideBorder"), guide.BorderBrush);

                Border side = all.OfType<Border>().First(border => border.Background == window.FindResource("SA.Panel"));
                Assert.Equal(340.0, side.ActualWidth);
                Assert.Contains(Visuals(side).OfType<TextBlock>(), block => block.Text == "Selected plot");

                // The four chips, the first filled with the colour of the band
                List<Button> chips = all.OfType<Button>().Where(button => button.Content is string text && Regex.IsMatch(text, @"^(All|Not checked|OK|Wrong) \d+$")).ToList();
                Assert.Equal(new[] { "All 5", "Not checked 2", "OK 2", "Wrong 1" }, chips.Select(chip => (string)chip.Content));
                Assert.Equal(window.FindResource("SA.Band"), chips[0].Background);
                Assert.NotEqual(window.FindResource("SA.Band"), chips[1].Background);

                // The table rows are 32 high and the selected one stands on SA.Selected
                List<ListBoxItem> items = all.OfType<ListBoxItem>().ToList();
                Assert.Equal(5, items.Count);
                Assert.All(items, item => Assert.True(item.ActualHeight >= 32.0, item.ActualHeight.ToString()));
                Assert.Equal(1, items.Count(item => item.IsSelected));
                Border selected = Visuals(items.Single(item => item.IsSelected)).OfType<Border>().First(border => border.Background == window.FindResource("SA.Selected"));
                Assert.NotNull(selected);
                Border mark = Visuals(items.Single(item => item.IsSelected)).OfType<Border>().Single(border => border.Background == window.FindResource("SA.Accent"));
                Assert.Equal(3.0, mark.Width);
            });
        }

        [Fact]
        public void R402_the_body_factory_is_not_set_until_the_app_sets_the_window_and_the_load()
        {
            ReviewDataBody.Dialog = null;
            ReviewDataBody.Load = null;
            Assert.False(ReviewDataBody.IsSet);
            ReviewDataBody.Dialog = owner => new ReviewDataDialog(owner);
            Assert.False(ReviewDataBody.IsSet);
            ReviewDataBody.Load = context => null;
            Assert.True(ReviewDataBody.IsSet);
            AssertBody(ReviewDataBody.For(IntPtr.Zero));
            ReviewDataBody.Dialog = null;
            ReviewDataBody.Load = null;
        }

        // R402. Button 3 is built in Bodies.Of once App sets the window and the pane the load, and the entry hands it the main window
        // of Revit. In this class, as the other test of the statics, so no two tests set them at once
        [Fact]
        public void R402_button_3_is_built_once_the_window_and_the_load_are_set_and_the_pane_sets_the_load()
        {
            var made = new SiteAdaptation.Common.Flow.ButtonBody();
            SiteAdaptation.AddIn.RibbonButtonSpec three = SiteAdaptation.AddIn.RibbonLayout.Button(3);
            ReviewDataBody.Dialog = null;
            ReviewDataBody.Load = null;
            Assert.Equal("3 Review data is not built yet.", SiteAdaptation.AddIn.Bodies.Of(three, null, null, () => made).NotBuilt);
            ReviewDataBody.Dialog = owner => new ReviewDataDialog(owner);
            ReviewDataBody.Load = context => null;
            try
            {
                Assert.Same(made, SiteAdaptation.AddIn.Bodies.Of(three, null, null, () => made));
                Assert.Equal("3 Review data is not built yet.", SiteAdaptation.AddIn.Bodies.Of(three, null, null).NotBuilt);
            }
            finally
            {
                ReviewDataBody.Dialog = null;
                ReviewDataBody.Load = null;
            }
            string addin = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn");
            Assert.Contains("ReviewData.ReviewDataBody.Load = context => Common.Review.ReviewLoad.Load(context.WorkFolder,", File.ReadAllText(Path.Combine(addin, "App", "PaneHost.cs")));
            Assert.Contains("ReviewData.ReviewDataBody.Dialog = owner => new ReviewData.ReviewDataDialog(owner);", File.ReadAllText(Path.Combine(addin, "App", "App.cs")));
            Assert.Contains("ReviewData.ReviewDataBody.For(application.MainWindowHandle)", File.ReadAllText(Path.Combine(addin, "Commands", "Buttons.cs")));
        }

        private static void AssertBody(SiteAdaptation.Common.Flow.ButtonBody body)
        {
            Assert.NotNull(body.Preview);
            Assert.NotNull(body.Confirm);
            Assert.NotNull(body.Apply);
            Assert.Empty(body.Apply(null));
        }

        private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            for (int at = 0; at < VisualTreeHelper.GetChildrenCount(root); at++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, at);
                yield return child;
                foreach (DependencyObject below in Visuals(child))
                {
                    yield return below;
                }
            }
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception caught)
                {
                    failure = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException("on the STA thread: " + failure);
            }
        }
    }
}
