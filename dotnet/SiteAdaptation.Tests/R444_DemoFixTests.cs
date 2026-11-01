using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteAdaptation.AddIn.CheckTable;
using SiteAdaptation.AddIn.Loader;
using SiteAdaptation.AddIn.ReviewData;
using SiteAdaptation.AddIn.RulesWindow;
using SiteAdaptation.Common.Review;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R444, Lane C DEMO-FIX of 1 Nov 2026. Confirm all in 3 Review data, the loader of every button and no button that does nothing
    public class R444_DemoFixTests
    {
        [Fact]
        public void R444_confirm_all_marks_every_plot_not_checked_ok_keeps_wrong_and_a_plot_can_still_be_marked_again()
        {
            ReviewView view = Shots.SampleReviewView(true);
            var session = new ReviewDataSession(view, new ReviewCallbacks());
            ReviewDataViewModel model = session.Model;
            string wrong = model.Rows[0].PlotNo;
            model.Selected = model.Rows[0];
            session.Mark(ReviewCheckValue.Wrong);
            Dictionary<string, ReviewStatus> before = view.State.Rows.ToDictionary(row => row.PlotNo, row => view.State.StatusOf(row.PlotNo));

            Assert.Equal("Confirm all", model.ConfirmRowsLabel);
            Assert.True(model.CanConfirmRows);
            session.ConfirmRows();

            Assert.Equal(ReviewStatus.Wrong, view.State.StatusOf(wrong));
            Assert.All(view.State.Rows, row => Assert.Equal(before[row.PlotNo] == ReviewStatus.NotChecked ? ReviewStatus.Ok : before[row.PlotNo], view.State.StatusOf(row.PlotNo)));
            Assert.False(model.CanConfirmRows);
            Assert.True(model.NotSavedShown);

            model.Selected = model.Rows[1];
            session.Mark(ReviewCheckValue.Wrong);
            Assert.Equal(ReviewStatus.Wrong, view.State.StatusOf(model.Rows[1].PlotNo));
        }

        [Fact]
        public void R444_confirm_all_shown_marks_only_the_rows_the_find_box_shows()
        {
            ReviewView view = Shots.SampleReviewView(true);
            var session = new ReviewDataSession(view, new ReviewCallbacks());
            ReviewDataViewModel model = session.Model;
            Dictionary<string, ReviewStatus> before = view.State.Rows.ToDictionary(row => row.PlotNo, row => view.State.StatusOf(row.PlotNo));
            string plot = view.State.Rows.First(row => before[row.PlotNo] == ReviewStatus.NotChecked).PlotNo;
            model.Find = plot;
            List<string> shown = model.Rows.Select(row => row.PlotNo).ToList();
            Assert.Contains(plot, shown);
            Assert.Equal("Confirm all shown", model.ConfirmRowsLabel);

            session.ConfirmRows();

            Assert.All(view.State.Rows, row => Assert.Equal(shown.Contains(row.PlotNo) && before[row.PlotNo] == ReviewStatus.NotChecked ? ReviewStatus.Ok : before[row.PlotNo],
                view.State.StatusOf(row.PlotNo)));
            model.Find = "";
            Assert.Equal("Confirm all", model.ConfirmRowsLabel);
            Assert.Equal(view.State.Rows.Any(row => view.State.StatusOf(row.PlotNo) == ReviewStatus.NotChecked), model.CanConfirmRows);
        }

        [Fact]
        public void R444_review_data_hides_zoom_and_export_while_the_revit_side_gives_them_nothing_and_shows_confirm_all_in_the_foot()
        {
            OnSta(() =>
            {
                ReviewDataWindow window = Shots.LaidOutReview(Shots.SampleReviewView(true));
                List<Button> buttons = Visuals(Shots.Body(window)).OfType<Button>().ToList();
                Assert.Equal(Visibility.Collapsed, buttons.Single(button => (string)button.Content == "Zoom to plot").Visibility);
                Assert.Equal(Visibility.Collapsed, buttons.Single(button => (string)button.Content == "Export to Excel").Visibility);
                Button confirm = buttons.Single(button => (string)button.Content == "Confirm all");
                Assert.Equal(Visibility.Visible, confirm.Visibility);
                Assert.True(confirm.IsEnabled);

                var wired = new ReviewDataSession(Shots.SampleReviewView(true), new ReviewCallbacks { Zoom = row => { }, Export = () => "Exported." });
                Assert.True(wired.Model.ZoomShown);
                Assert.True(wired.Model.ExportShown);
            });
        }

        [Fact]
        public void R444_check_table_hides_zoom_and_export_and_the_rules_window_hides_export_to_excel()
        {
            OnSta(() =>
            {
                CheckTableWindow check = Shots.LaidOutCheck(Shots.SampleCheckView(false));
                List<Button> buttons = Visuals(Shots.Body(check)).OfType<Button>().ToList();
                Assert.Equal(Visibility.Collapsed, buttons.Single(button => (string)button.Content == "Zoom to plot").Visibility);
                Assert.Equal(Visibility.Collapsed, buttons.Single(button => (string)button.Content == "Export to Excel").Visibility);

                RulesWindow rules = Shots.LaidOutRules(Shots.SampleRulesTable(), "M06");
                Assert.Equal(Visibility.Collapsed, Visuals(Shots.Body(rules)).OfType<Button>().Single(button => (string)button.Content == "Export to Excel").Visibility);
            });
        }

        [Fact]
        public void R444_the_loader_starts_on_its_own_thread_never_shows_with_no_revit_window_and_ends_on_dispose()
        {
            RunLoader loader = RunLoader.Start("4 Check", IntPtr.Zero);
            Assert.True(loader.Running);
            loader.Outside();
            loader.Dispose();
            loader.Outside();
            Stopwatch wait = Stopwatch.StartNew();
            while (loader.Running && wait.Elapsed < TimeSpan.FromSeconds(10))
            {
                Thread.Sleep(20);
            }
            Assert.False(loader.Running);
        }

        [Fact]
        public void R444_the_loader_window_shows_the_step_the_clock_and_hide_only_outside_revit()
        {
            OnSta(() =>
            {
                var window = new LoaderWindow("4 Check");
                List<TextBlock> texts = Logical((DependencyObject)window.Content).OfType<TextBlock>().ToList();
                Assert.Contains(texts, text => text.Text == "4 Check");
                Assert.Contains(texts, text => text.Text == LoaderText.Working);
                Assert.Contains(texts, text => text.Text == "Elapsed 0:00");
                Assert.False(window.IsOutside);
                Assert.True(Logical((DependencyObject)window.Content).OfType<ProgressBar>().Single().IsIndeterminate);

                window.Tick(TimeSpan.FromSeconds(75));
                window.Outside();
                texts = Logical((DependencyObject)window.Content).OfType<TextBlock>().ToList();
                Assert.Contains(texts, text => text.Text == "Elapsed 1:15");
                Assert.Contains(texts, text => text.Text == LoaderText.Outside);
                Assert.True(window.IsOutside);
                window.Close();
            });
        }

        private static Window LaidOut(Window window)
        {
            window.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            window.Arrange(new Rect(window.DesiredSize));
            window.ApplyTemplate();
            (window.Content as FrameworkElement)?.ApplyTemplate();
            window.UpdateLayout();
            return window;
        }

        private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            if (root is Window window && window.Content is DependencyObject content && VisualTreeHelper.GetChildrenCount(root) == 0)
            {
                foreach (DependencyObject logical in LogicalTreeHelper.GetChildren(content).OfType<DependencyObject>().SelectMany(Logical).Prepend(content))
                {
                    yield return logical;
                }
                yield break;
            }
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

        private static IEnumerable<DependencyObject> Logical(DependencyObject root)
        {
            yield return root;
            foreach (DependencyObject child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().SelectMany(Logical))
            {
                yield return child;
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
                throw new Exception(failure.Message, failure);
            }
        }
    }
}
