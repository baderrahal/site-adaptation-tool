using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteAdaptation.AddIn.CheckTable;
using SiteAdaptation.AddIn.ReviewData;
using SiteAdaptation.AddIn.RulesWindow;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // The window fixes of the UI review of Lane B 5c: one table column set for header and rows, the shared gap and mark sizes of Theme.xaml,
    // the corner table of Review data, the tooltip of the Rule column, the selected look of OK and Wrong, the line of a Rules command with no file
    public sealed class R406_UiFixTests
    {
        private static string Source(params string[] parts)
        {
            return File.ReadAllText(Path.Combine(new[] { ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn" }.Concat(parts).ToArray()));
        }

        private static double Left(FrameworkElement element, FrameworkElement body)
        {
            return element.TransformToAncestor(body).Transform(new Point(0, 0)).X;
        }

        private static double Top(FrameworkElement element, FrameworkElement body)
        {
            return element.TransformToAncestor(body).Transform(new Point(0, 0)).Y;
        }

        // The left edge and the width of each column of the header and of the first row, in the coordinates of the window body
        private static void AssertHeaderLinesUpWithRows(Window window)
        {
            FrameworkElement body = Shots.Body(window);
            Grid header = Visuals(body).OfType<Border>().Select(border => border.Child).OfType<Grid>()
                .First(grid => grid.ColumnDefinitions.Count >= 5 && grid.Children.OfType<TextBlock>().Count() >= 5);
            ListBoxItem item = Visuals(body).OfType<ListBoxItem>().First();
            Grid row = Visuals(item).OfType<Grid>().First(grid => grid.ColumnDefinitions.Count >= 5);

            for (int at = 0; at < row.ColumnDefinitions.Count; at++)
            {
                double headerLeft = Left(header, body) + header.ColumnDefinitions[at].Offset;
                double rowLeft = Left(row, body) + row.ColumnDefinitions[at].Offset;
                Assert.True(Math.Abs(headerLeft - rowLeft) < 0.5, "column " + at + " header at " + headerLeft + " rows at " + rowLeft);
                Assert.True(Math.Abs(header.ColumnDefinitions[at].ActualWidth - row.ColumnDefinitions[at].ActualWidth) < 0.5, "column " + at + " width");
            }
        }

        [Fact]
        public void R406_the_corner_table_has_the_heading_shared_coordinates_as_a_column_over_its_values()
        {
            OnSta(() =>
            {
                ReviewDataWindow window = Shots.LaidOutReview(Shots.SampleReviewView(true));
                FrameworkElement body = Shots.Body(window);
                List<TextBlock> blocks = Visuals(body).OfType<TextBlock>().ToList();
                TextBlock x = blocks.First(block => block.Text == "X");
                TextBlock y = blocks.First(block => block.Text == "Y");
                TextBlock shared = blocks.First(block => block.Text == "Shared coordinates");
                TextBlock corner1 = blocks.First(block => block.Text == "Corner 1");
                TextBlock value = blocks.First(block => block.DataContext is CornerViewModel corner && block.Text == corner.Shared);

                Assert.True(Top(shared, body) <= Top(x, body) + 0.5 && Top(shared, body) + shared.ActualHeight >= Top(x, body) + x.ActualHeight - 0.5, "the heading is on the line of X and Y");
                Assert.True(Top(shared, body) + shared.ActualHeight <= Top(corner1, body), "the heading is over the first corner, not between its lines");
                Assert.True(Math.Abs(Left(shared, body) - Left(value, body)) < 0.5, "the heading is over its values");
                Assert.True(Left(shared, body) > Left(y, body), "the column is after Y");
                Assert.True(Top(value, body) - Top(corner1, body) < 1.0, "the values are on the line of their corner, not under it");
            });
        }

        [Fact]
        public void R406_the_review_table_header_lines_up_with_its_rows()
        {
            OnSta(() => AssertHeaderLinesUpWithRows(Shots.LaidOutReview(Shots.SampleReviewView(true))));
        }

        [Fact]
        public void R403_the_rules_table_header_lines_up_with_its_rows_with_the_scroll_bar_showing()
        {
            OnSta(() =>
            {
                RulesWindow window = Shots.LaidOutRules(Shots.SampleRulesTable(), "M06");
                Assert.Contains(Visuals(Shots.Body(window)).OfType<System.Windows.Controls.Primitives.ScrollBar>(), bar => bar.Visibility == Visibility.Visible && bar.Orientation == Orientation.Vertical);
                AssertHeaderLinesUpWithRows(window);
            });
        }

        [Fact]
        public void R376_the_check_table_header_lines_up_with_its_rows()
        {
            OnSta(() => AssertHeaderLinesUpWithRows(Shots.LaidOutCheck(Shots.SampleCheckView(false))));
        }

        [Fact]
        public void R406_the_cell_gap_the_selected_mark_and_the_assumed_ring_are_theme_keys_used_by_the_three_windows()
        {
            string theme = Source("Theme", "Theme.xaml");
            foreach (string key in new[] { "SA.Gap.Cell", "SA.Margin.SelectedMark", "SA.Size.ChipRadius", "SA.Dash.Assumed" })
            {
                Assert.Contains("x:Key=\"" + key + "\"", theme);
            }
            foreach (string file in new[] { Source("ReviewData", "ReviewDataWindow.xaml"), Source("CheckTable", "CheckTableWindow.xaml"), Source("RulesWindow", "RulesWindow.xaml") })
            {
                Assert.DoesNotContain("-8,-4,0,-4", file);
                Assert.DoesNotContain("Value=\"0,0,8,0\"", file);
                Assert.DoesNotContain("RadiusX=\"12\"", file);
                Assert.DoesNotContain("StrokeDashArray=\"3 2\"", file);
                Assert.Contains("SA.Margin.SelectedMark", file);
            }
            OnSta(() =>
            {
                foreach (Window window in new Window[] { Shots.LaidOutReview(Shots.SampleReviewView(true)), Shots.LaidOutCheck(Shots.SampleCheckView(false)),
                    Shots.LaidOutRules(Shots.SampleRulesTable(), "M06") })
                {
                    var cell = (Thickness)window.FindResource("SA.Gap.Cell");
                    Assert.True(cell.Right >= 8.0);
                    ListBoxItem item = Visuals(Shots.Body(window)).OfType<ListBoxItem>().First();
                    List<TextBlock> cells = Visuals(item).OfType<TextBlock>().Where(block => block.Style == window.Resources["RowText"]).ToList();
                    Assert.NotEmpty(cells);
                    Assert.All(cells, block => Assert.Equal(cell, block.Margin));
                }
            });
        }

        [Fact]
        public void R376_the_rule_cell_tooltip_is_the_rule_id_and_the_manual_rule_cell_keeps_the_manual_rule_tooltip()
        {
            OnSta(() =>
            {
                CheckTableWindow window = Shots.LaidOutCheck(Shots.SampleCheckView(false));
                var model = (CheckTableViewModel)window.DataContext;
                CheckRowViewModel row = model.Rows.First(candidate => candidate.ManualRuleTip != "");
                Assert.Equal(CheckTableState.Text(row.Row.Rule), row.RuleTip);
                Assert.NotEqual(row.ManualRuleTip, row.RuleTip);

                List<TextBlock> blocks = Visuals(Shots.Body(window)).OfType<TextBlock>().ToList();
                TextBlock ruleCell = blocks.First(block => block.DataContext == row && block.Text == row.RuleText);
                TextBlock manualCell = blocks.First(block => block.DataContext == row && block.Text == row.ManualRule);
                Assert.Equal(row.RuleTip, ruleCell.ToolTip);
                Assert.Equal(row.ManualRuleTip, manualCell.ToolTip);
            });
        }

        [Fact]
        public void R406_the_ok_and_wrong_buttons_of_the_side_box_show_the_status_of_the_selected_plot()
        {
            OnSta(() =>
            {
                ReviewDataWindow window = Shots.LaidOutReview(Shots.SampleReviewView(true));
                var model = (ReviewDataViewModel)window.DataContext;
                FrameworkElement body = Shots.Body(window);
                Func<string, Button> button = text => Visuals(body).OfType<Button>().First(candidate => (candidate.Content as string) == text);

                ReviewRowViewModel ok = model.Rows.First(row => row.Check == ReviewDataText.Ok);
                ReviewRowViewModel wrong = model.Rows.First(row => row.Check == ReviewDataText.Wrong);
                ReviewRowViewModel open = model.Rows.First(row => row.Check == ReviewDataText.NotChecked);

                model.Selected = ok;
                Shots.LayOut(window);
                Assert.Equal(window.FindResource("SA.OkGround"), button("OK").Background);
                Assert.Equal(window.FindResource("SA.Ok"), button("OK").BorderBrush);
                Assert.NotEqual(window.FindResource("SA.ErrorGround"), button("Wrong").Background);

                model.Selected = wrong;
                Shots.LayOut(window);
                Assert.Equal(window.FindResource("SA.ErrorGround"), button("Wrong").Background);
                Assert.Equal(window.FindResource("SA.Error"), button("Wrong").BorderBrush);
                Assert.NotEqual(window.FindResource("SA.OkGround"), button("OK").Background);

                model.Selected = open;
                Shots.LayOut(window);
                Assert.Equal(window.FindResource("SA.Surface"), button("OK").Background);
                Assert.Equal(window.FindResource("SA.Surface"), button("Wrong").Background);
            });
        }

        [Fact]
        public void R403_a_rules_command_with_no_rules_file_says_to_choose_one_in_set_up_site_first()
        {
            Assert.Equal(RulesWindowText.NoRulesFile, RulesWindowBody.Problem(null));
            Assert.Equal(RulesWindowText.NoRulesFile, RulesWindowBody.Problem(""));
            Assert.StartsWith("The rules file was not found", RulesWindowBody.Problem(Path.Combine(Path.GetTempPath(), "no-such-rules-file.json")));
            Assert.Contains("no-such-rules-file.json", RulesWindowBody.Problem(Path.Combine(Path.GetTempPath(), "no-such-rules-file.json")));
            Assert.Null(RulesWindowBody.Problem(Path.Combine(ThemeGateTests.RepoRoot(), "rules", "roshn-makkah.json")));
            Assert.DoesNotContain(":", RulesWindowText.NoRulesFile.TrimEnd());
        }

        [Fact]
        public void R376_the_team_check_chip_text_of_the_window_list_is_the_check_value_the_build_gives()
        {
            Assert.Equal(CheckTableBuild.TeamCheck, CheckTableText.TeamCheck);
            string list = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "14-ui-labels.md"));
            string part = list.Substring(list.IndexOf("### Check table", StringComparison.Ordinal));
            part = part.Substring(0, part.IndexOf("### Update from report", StringComparison.Ordinal));
            Assert.Contains("- `Team check`", part);
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
