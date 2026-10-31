using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SiteAdaptation.AddIn.SetUpSite;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R373, R374, 7.3 of docs/design/12-ui-spec.md. The window of button 1, its view model and the session that drives its four
    // buttons with no click. WPF, so net48 only
    public sealed class SetUpSiteTests
    {
        // The view of a dry run as Common makes it, for the slope the dry run was asked with
        private static SetUpSiteView DryAt(double slope)
        {
            SetUpSiteView view = Shots.SampleSetUpView(true, true);
            view.RampSlope = slope;
            return view;
        }

        [Fact]
        public void R373_the_view_model_shows_the_badge_the_signs_the_count_and_the_pass_line_of_the_view()
        {
            SetUpSiteView view = Shots.SampleSetUpView(true, true);
            var model = new SetUpSiteViewModel(view);

            Assert.Equal("Existing site file", model.Badge);
            Assert.Equal(string.Join(", ", view.Signs), model.SignsLine);
            Assert.True(model.SignsShown);
            Assert.Equal("7 of 7", model.CountLine);
            Assert.Equal("Pass limit 0.1 mm. 7 of 7 pass", model.PassLine);

            var empty = new SetUpSiteViewModel(Shots.SampleSetUpView(false, false));
            Assert.Equal("Empty project found", empty.Badge);
            Assert.False(empty.SignsShown);
            // Breaker pass 1 of 5b: before a dry run nothing is measured, so no count reads as points that failed
            Assert.False(empty.CountShown);
            Assert.Equal("", empty.CountLine);
            Assert.Equal("", empty.PassLine);
        }

        // Breaker pass 1 of 5b, 03a Second run: a view where nothing differs says so and Apply stays off, so no empty run commits
        [Fact]
        public void R374_a_view_where_nothing_differs_says_so_and_keeps_apply_off()
        {
            SetUpSiteView view = Shots.SampleSetUpView(true, true);
            view.NothingDiffers = true;
            var model = new SetUpSiteViewModel(view);

            Assert.True(model.NothingShown);
            Assert.Equal(SetUpSiteText.NothingDiffers, model.NothingLine);
            Assert.Equal(SetUpTexts.NothingDiffers, SetUpSiteText.NothingDiffers);
            Assert.False(model.CanApply);
            Assert.False(new SetUpSiteViewModel(Shots.SampleSetUpView(true, true)).NothingShown);
        }

        // Breaker pass 2 of 5b: what waits, Plot on Q156, shows beside the line, so nothing differs never reads as a model complete
        [Fact]
        public void R374_the_items_that_wait_show_in_the_window_beside_nothing_differs()
        {
            SetUpSiteView view = Shots.SampleSetUpView(true, true);
            view.NothingDiffers = true;
            view.Waiting = new List<string> { "Plot not bound, waiting on Q156" };
            var model = new SetUpSiteViewModel(view);

            Assert.True(model.WaitingShown);
            Assert.Equal("Plot not bound, waiting on Q156", model.WaitingLine);
            Assert.False(new SetUpSiteViewModel(Shots.SampleSetUpView(true, true)).WaitingShown);
        }

        // Breaker pass 2 of 5b: a view holds for the slope it was made with, another slope asks for a dry run before Apply and before
        // the line nothing differs is believed, so no Apply ends with nothing done and nothing said
        [Fact]
        public void R374_a_slope_picked_after_the_view_turns_apply_and_nothing_differs_off_until_a_dry_run()
        {
            SetUpSiteView view = Shots.SampleSetUpView(true, true);
            view.NothingDiffers = true;
            var nothing = new SetUpSiteViewModel(view);
            nothing.RampIndex = 2;
            Assert.False(nothing.NothingShown);
            Assert.False(nothing.CanApply);

            var model = new SetUpSiteViewModel(Shots.SampleSetUpView(true, true));
            Assert.True(model.CanApply);
            model.RampIndex = 2;
            Assert.False(model.CanApply);
            model.RampIndex = 0;
            Assert.True(model.CanApply);
        }

        [Fact]
        public void R373_the_rows_stand_in_the_order_of_the_mock_with_the_three_rows_of_28_oct_2026()
        {
            var model = new SetUpSiteViewModel(Shots.SampleSetUpView(true, true));

            Assert.Equal(new[] { "Survey point", "Project base point", "Levels", "Floor types and wall types", "Shared parameters", "Units" }, model.Now.Select(row => row.Label));
            Assert.Equal(new[] { "Coordinate system", "Survey point", "Angle to true north", "Levels", "Floor types", "Ramp slope", "Wall types", "Shared parameters", "Units" },
                model.WillSet.Select(row => row.Label));
            SetUpRowViewModel floor = model.WillSet.Single(row => row.Label == "Floor types");
            Assert.Equal("Generic 150mm, yard, Flat Ramps, and one Ramps type per slope below", floor.Value);
            SetUpRowViewModel sheet = model.WillSet.Single(row => row.Label == "Shared parameters");
            Assert.True(sheet.IsSheet);
            // 7.3: the sheet name and its count in the one line Common gives, never a number alone beside Browse
            Assert.Equal("From the parameter sheet: Parameter sheet.xlsx, 12 parameters", sheet.Value);
            Assert.True(model.WillSet.Single(row => row.Label == "Ramp slope").IsRamp);
        }

        [Fact]
        public void R373_the_ramp_slope_choices_are_05_1_2_then_3_to_15_percent_and_the_default_is_05()
        {
            var model = new SetUpSiteViewModel(Shots.SampleSetUpView(true, true));

            Assert.Equal(new[] { "0.5%", "1%", "2%", "3%", "4%", "5%", "6%", "7%", "8%", "9%", "10%", "11%", "12%", "13%", "14%", "15%" }, model.RampChoices);
            Assert.Equal(0, model.RampIndex);
            Assert.Equal(0.5, model.SelectedSlope);
            model.RampIndex = 2;
            Assert.Equal(2.0, model.SelectedSlope);
        }

        [Fact]
        public void R373_a_check_point_shows_its_diff_in_two_decimals_and_passes_only_when_the_view_says_so()
        {
            SetUpSiteView view = Shots.SampleSetUpView(true, true);
            view.CheckPoints[1].DiffMm = 0.376;
            view.CheckPoints[1].Pass = false;
            view.CheckPoints[2].DiffMm = null;
            var model = new SetUpSiteViewModel(view);

            Assert.Equal("0.00", model.CheckPoints[0].Diff);
            Assert.True(model.CheckPoints[0].Passes);
            Assert.Equal("0.38", model.CheckPoints[1].Diff);
            Assert.True(model.CheckPoints[1].Fails);
            Assert.False(model.CheckPoints[1].Passes);
            Assert.Equal("", model.CheckPoints[2].Diff);
            Assert.False(model.CheckPoints[2].Passes);
            Assert.False(model.CheckPoints[2].Fails);
        }

        [Fact]
        public void R373_apply_is_off_before_the_dry_run_and_while_the_view_holds_a_stop_and_on_after_the_dry_run()
        {
            SetUpSiteView before = Shots.SampleSetUpView(false, true);
            Assert.False(new SetUpSiteViewModel(before).CanApply);

            SetUpSiteView done = Shots.SampleSetUpView(true, true);
            Assert.True(new SetUpSiteViewModel(done).CanApply);

            done.Stops.Add("The parameter Plot has another GUID in the sheet");
            var stopped = new SetUpSiteViewModel(done);
            Assert.False(stopped.CanApply);
            Assert.True(stopped.StopsShown);
            Assert.Contains("another GUID", stopped.StopsLine);
        }

        [Fact]
        public void R373_dry_run_hands_the_slope_chosen_to_the_callback_and_loads_the_view_it_gives_and_turns_apply_on()
        {
            var seen = new List<double>();
            var callbacks = new SetUpSiteCallbacks
            {
                DryRun = slope =>
                {
                    seen.Add(slope);
                    return DryAt(slope);
                },
            };
            var session = new SetUpSiteSession(Shots.SampleSetUpView(false, true), callbacks, () => null);
            session.Model.RampIndex = 3;

            Assert.False(session.Model.CanApply);
            session.DryRun();

            Assert.Equal(new[] { 3.0 }, seen);
            Assert.Equal("7 of 7", session.Model.CountLine);
            Assert.Equal(3.0, session.Model.SelectedSlope);
            Assert.True(session.Model.CanApply);
        }

        [Fact]
        public void R373_a_slope_changed_after_the_dry_run_turns_apply_off_until_the_next_dry_run()
        {
            var session = new SetUpSiteSession(Shots.SampleSetUpView(false, true),
                new SetUpSiteCallbacks { DryRun = slope => DryAt(slope) }, () => null);

            session.DryRun();
            Assert.True(session.Model.CanApply);
            session.Model.RampIndex = 1;
            Assert.False(session.Model.CanApply);
            session.Apply();
            Assert.False(session.Answer.Apply);
            session.DryRun();
            Assert.True(session.Model.CanApply);
        }

        [Fact]
        public void R373_apply_ends_the_window_with_the_slope_and_the_sheet_and_does_nothing_while_it_is_off()
        {
            int finished = 0;
            var session = new SetUpSiteSession(Shots.SampleSetUpView(false, true),
                new SetUpSiteCallbacks { DryRun = slope => DryAt(slope), SheetChosen = path => Shots.SampleSetUpView(true, true) },
                () => @"C:\Sample\Other sheet.xlsx");
            session.Finished += () => finished++;

            session.Apply();
            Assert.Equal(0, finished);
            Assert.False(session.Answer.Apply);

            session.Model.RampIndex = 2;
            session.Browse();
            session.DryRun();
            session.Apply();

            Assert.Equal(1, finished);
            Assert.True(session.Answer.Apply);
            Assert.Equal(2.0, session.Answer.RampSlope);
            Assert.Equal(@"C:\Sample\Other sheet.xlsx", session.Answer.ParameterSheet);
        }

        [Fact]
        public void R373_cancel_ends_the_window_with_no_apply_even_after_a_dry_run()
        {
            int finished = 0;
            var session = new SetUpSiteSession(Shots.SampleSetUpView(true, true), new SetUpSiteCallbacks(), () => null);
            session.Finished += () => finished++;

            session.Cancel();

            Assert.Equal(1, finished);
            Assert.False(session.Answer.Apply);
            Assert.Null(session.Answer.ParameterSheet);
        }

        [Fact]
        public void R373_browse_hands_the_path_to_the_callback_and_a_cancelled_file_dialog_changes_nothing()
        {
            var paths = new List<string>();
            string pick = null;
            var session = new SetUpSiteSession(Shots.SampleSetUpView(false, true),
                new SetUpSiteCallbacks
                {
                    SheetChosen = path =>
                    {
                        paths.Add(path);
                        SetUpSiteView view = Shots.SampleSetUpView(false, true);
                        view.ParameterSheetName = "Other sheet.xlsx";
                        view.ParameterCount = 14;
                        view.WillSet.Single(row => row.Label == "Shared parameters").Value = SetUpTexts.FromSheet("Other sheet.xlsx, 14 parameters");
                        return view;
                    },
                },
                () => pick);

            session.Browse();
            Assert.Empty(paths);
            Assert.Null(session.Model.ChosenSheet);

            pick = @"C:\Sample\Other sheet.xlsx";
            session.Browse();

            Assert.Equal(new[] { pick }, paths);
            Assert.Equal(pick, session.Model.ChosenSheet);
            SetUpRowViewModel sheet = session.Model.WillSet.Single(row => row.IsSheet);
            Assert.Equal("From the parameter sheet: Other sheet.xlsx, 14 parameters", sheet.Value);
        }

        // Review 1 of 5b: in Revit every Browse fails until the worker reads the sheet (Q155), so the failed path is never kept for Apply
        [Fact]
        public void R373_a_browse_whose_sheet_does_not_read_keeps_no_sheet_for_apply()
        {
            var session = new SetUpSiteSession(Shots.SampleSetUpView(false, true),
                new SetUpSiteCallbacks
                {
                    DryRun = slope => DryAt(slope),
                    SheetChosen = path => throw new InvalidOperationException("The parameter sheet is not read yet"),
                },
                () => @"C:\Sample\Bad sheet.xlsx");

            session.DryRun();
            session.Browse();
            session.Apply();

            Assert.Null(session.Model.ChosenSheet);
            Assert.True(session.Model.StopsShown);
            Assert.True(session.Answer.Apply);
            Assert.Null(session.Answer.ParameterSheet);
        }

        [Fact]
        public void R373_a_callback_that_throws_leaves_the_window_open_with_its_reason_and_no_apply()
        {
            int finished = 0;
            var session = new SetUpSiteSession(Shots.SampleSetUpView(false, true),
                new SetUpSiteCallbacks { DryRun = slope => throw new InvalidOperationException("the dry run could not read the levels") }, () => null);
            session.Finished += () => finished++;

            session.DryRun();

            Assert.Equal(0, finished);
            Assert.False(session.Answer.Apply);
            Assert.True(session.Model.StopsShown);
            Assert.Contains("could not read the levels", session.Model.StopsLine);
            Assert.False(session.Model.CanApply);
        }

        [Fact]
        public void R373_the_window_has_the_band_the_two_tables_the_check_table_the_guide_box_and_the_foot_of_7_3()
        {
            OnSta(() =>
            {
                SetUpSiteWindow window = Shots.LaidOutSetUp(Shots.SampleSetUpView(true, true));
                List<FrameworkElement> all = Visuals(Shots.Body(window)).OfType<FrameworkElement>().ToList();
                List<string> texts = all.OfType<TextBlock>().Select(block => block.Text).ToList();

                Assert.Equal("1 Set up site", window.Title);
                foreach (string text in new[] { "1 Set up site", "Existing site file", "Sets the coordinates, levels and types of this model from the CAD drawings.", "7 of 7",
                    "check points pass", "What the project has now", "What the tool will set", "Check points from CAD", "Point", "CAD", "Revit", "Diff",
                    "Pass limit 0.1 mm. 7 of 7 pass", "On an existing site file", "Nothing changes until Apply. Apply sets only the rows that differ.",
                    "Default 0.5%. Choices 0.5%, 1%, 2%, then 3% to 15%", "Generic 150mm, yard, Flat Ramps, and one Ramps type per slope below" })
                {
                    Assert.Contains(text, texts);
                }
                Assert.Contains(texts, text => text.StartsWith("From the parameter sheet: "));

                Border band = all.OfType<Border>().First(border => border.Background == window.FindResource("SA.Band"));
                Assert.Contains(Visuals(band).OfType<TextBlock>(), block => block.Text == "1 Set up site" && block.FontSize == 20.0);
                Assert.Contains(Visuals(band).OfType<TextBlock>(), block => block.Text == "7 of 7" && block.FontSize == 28.0);
                Assert.Contains(Visuals(band).OfType<Border>(), border => border.Background == window.FindResource("SA.BandAccent")
                    && Visuals(border).OfType<TextBlock>().Any(block => block.Text == "Existing site file"));

                Border guide = all.OfType<Border>().Single(border => border.Background == window.FindResource("SA.Guide")
                    && Visuals(border).OfType<TextBlock>().Any(block => block.Text == "On an existing site file"));
                Assert.Equal(window.FindResource("SA.GuideBorder"), guide.BorderBrush);

                List<Button> foot = all.OfType<Button>().Where(button => button.Content is string text && (text == "Cancel" || text == "Dry run" || text == "Apply")).ToList();
                Assert.Equal(new[] { "Cancel", "Dry run", "Apply" }, foot.Select(button => (string)button.Content).ToArray());
                Assert.All(foot, button => Assert.Equal(button.Content.Equals("Apply") ? 34.0 : 28.0, button.ActualHeight));
                Border panel = Ancestors(foot[0]).OfType<Border>().First(border => border.Background == window.FindResource("SA.Panel"));
                Assert.All(foot, button => Assert.Contains(button, Visuals(panel).OfType<Button>()));
                Assert.Equal(window.FindResource("SA.Button.Primary"), foot[2].Style);
                Assert.Equal(window.FindResource("SA.Button.DryRun"), foot[1].Style);
            });
        }

        [Fact]
        public void R373_the_rows_that_differ_stand_on_warn_ground_with_will_change_and_the_others_do_not()
        {
            OnSta(() =>
            {
                SetUpSiteView view = Shots.SampleSetUpView(true, true);
                int differing = view.WillSet.Count(row => row.Differs);
                Assert.True(differing > 0 && differing < view.WillSet.Count);
                SetUpSiteWindow window = Shots.LaidOutSetUp(view);
                List<FrameworkElement> all = Visuals(Shots.Body(window)).OfType<FrameworkElement>().ToList();

                List<TextBlock> tags = all.OfType<TextBlock>().Where(block => block.Text == "Will change" && block.Visibility == Visibility.Visible).ToList();
                Assert.Equal(differing, tags.Count);
                Assert.All(tags, tag => Assert.Equal(window.FindResource("SA.Warn"), tag.Foreground));
                Assert.All(tags, tag => Assert.Equal(window.FindResource("SA.WarnGround"), Ancestors(tag).OfType<Border>().First().Background));
                List<Border> warnRows = all.OfType<Border>().Where(border => Equals(border.Background, window.FindResource("SA.WarnGround"))
                    && Visuals(border).OfType<TextBlock>().Any(block => block.Text == "Will change")).ToList();
                Assert.Equal(differing, warnRows.Count);
            });
        }

        [Fact]
        public void R373_the_diff_cell_stands_in_ok_when_the_point_passes_and_the_check_table_has_one_row_per_point()
        {
            OnSta(() =>
            {
                SetUpSiteView view = Shots.SampleSetUpView(true, true);
                view.CheckPoints[3].DiffMm = 0.5;
                view.CheckPoints[3].Pass = false;
                view.Passing = 6;
                SetUpSiteWindow window = Shots.LaidOutSetUp(view);
                List<TextBlock> diffs = Visuals(Shots.Body(window)).OfType<TextBlock>().Where(block => block.DataContext is CheckPointViewModel && block.Name == "DiffCell").ToList();

                Assert.Equal(7, diffs.Count);
                Assert.Equal(6, diffs.Count(cell => Equals(cell.Foreground, window.FindResource("SA.Ok"))));
                Assert.Equal(6, diffs.Count(cell => Equals(Ancestors(cell).OfType<Border>().First().Background, window.FindResource("SA.OkGround"))));
                Assert.Equal(1, diffs.Count(cell => Equals(cell.Foreground, window.FindResource("SA.Error"))));
                Assert.Equal("0.50", diffs[3].Text);
            });
        }

        [Fact]
        public void R441_apply_stays_grey_until_the_site_drawing_is_ticked_and_hands_on_the_ticks_and_the_rules()
        {
            var session = new SetUpSiteSession(Shots.SampleSetUpView(true, true), new SetUpSiteCallbacks(), () => null);
            SetUpSiteViewModel model = session.Model;
            Assert.True(model.CurrentShown);
            Assert.True(model.CanApply);
            CurrentFileViewModel site = model.CurrentFiles.Single(row => row.Site);
            site.Current = false;
            Assert.False(model.CanApply);
            session.Apply();
            Assert.False(session.Answer.Apply);
            site.Current = true;
            Assert.True(model.CanApply);
            Assert.Equal(0, model.RulesIndex);
            session.Apply();
            Assert.True(session.Answer.Apply);
            Assert.Equal(new[] { InputNames.Baseplan, InputNames.VillaBlocks }, session.Answer.CurrentFiles);
            Assert.Equal("rules.json", session.Answer.RulesFile);
        }

        [Fact]
        public void R441_a_view_no_current_files_dialog_filled_shows_no_section_and_waits_for_no_tick()
        {
            SetUpSiteView view = Shots.SampleSetUpView(true, true);
            view.CurrentFiles = null;
            var model = new SetUpSiteViewModel(view);
            Assert.False(model.CurrentShown);
            Assert.True(model.CanApply);
        }

        [Fact]
        public void R373_the_ramp_slope_row_holds_a_30_high_drop_down_of_16_choices_and_the_sheet_row_holds_browse()
        {
            OnSta(() =>
            {
                SetUpSiteWindow window = Shots.LaidOutSetUp(Shots.SampleSetUpView(true, true));
                var model = (SetUpSiteViewModel)Shots.Body(window).DataContext;
                List<FrameworkElement> all = Visuals(Shots.Body(window)).OfType<FrameworkElement>().ToList();

                ComboBox ramp = all.OfType<ComboBox>().Single(box => box.ItemsSource == model.RampChoices);
                Assert.Equal(30.0, ramp.ActualHeight);
                Assert.Equal(16, ramp.Items.Count);
                Assert.Equal(0, ramp.SelectedIndex);
                ramp.SelectedIndex = 4;
                Assert.Equal(4, model.RampIndex);
                Assert.Equal(4.0, model.SelectedSlope);

                Button browse = all.OfType<Button>().Single(button => "Browse".Equals(button.Content));
                Assert.Equal(28.0, browse.ActualHeight);
                Assert.Contains(Ancestors(browse).OfType<Grid>(), grid => Visuals(grid).OfType<TextBlock>().Any(block => block.Text.StartsWith("From the parameter sheet: ")));
            });
        }

        [Fact]
        public void R373_the_buttons_of_the_window_drive_the_session_and_apply_is_off_before_the_dry_run()
        {
            OnSta(() =>
            {
                var drawn = new List<double>();
                var session = new SetUpSiteSession(Shots.SampleSetUpView(false, true),
                    new SetUpSiteCallbacks { DryRun = slope => { drawn.Add(slope); return DryAt(slope); } }, () => null);
                var window = new SetUpSiteWindow(session);
                Shots.LayOut(window);
                int finished = 0;
                session.Finished += () => finished++;
                List<Button> buttons = Visuals(Shots.Body(window)).OfType<Button>().ToList();
                Button apply = buttons.Single(button => "Apply".Equals(button.Content));
                Button dry = buttons.Single(button => "Dry run".Equals(button.Content));
                Button cancel = buttons.Single(button => "Cancel".Equals(button.Content));

                Assert.False(apply.IsEnabled);
                Assert.True(dry.IsEnabled);
                Click(dry);
                Shots.LayOut(window);
                Assert.Single(drawn);
                Assert.True(apply.IsEnabled);
                Click(apply);
                Assert.Equal(1, finished);
                Assert.True(session.Answer.Apply);
                Click(cancel);
                Assert.False(session.Answer.Apply);
                Assert.Equal(2, finished);
            });
        }

        [Fact]
        public void R373_no_text_of_the_window_is_below_12_and_every_label_of_the_list_is_shown_by_a_file_of_the_window()
        {
            string root = ThemeGateTests.RepoRoot();
            Dictionary<string, UiLabels.Window> list = UiLabels.List(File.ReadAllText(Path.Combine(root, UiLabels.ListFile)));
            Dictionary<string, List<UiLabels.Found>> texts = UiLabels.Texts(Path.Combine(root, "dotnet", "SiteAdaptation.AddIn"));
            HashSet<string> shown = new HashSet<string>(texts["Set up site"].Select(found => found.Text));

            Assert.Equal(new string[0], list["Set up site"].Entries.Select(entry => entry.Text).Where(text => !shown.Contains(text)).ToArray());

            OnSta(() =>
            {
                SetUpSiteWindow window = Shots.LaidOutSetUp(Shots.SampleSetUpView(true, true));
                Assert.All(Visuals(Shots.Body(window)).OfType<TextBlock>().Where(block => block.Text.Length > 0), block => Assert.True(block.FontSize >= 12.0, block.Text));
            });
        }

        [Fact]
        public void R373_the_window_is_1280_wide_at_most_800_high_and_the_rows_stand_in_the_theme_sizes()
        {
            OnSta(() =>
            {
                SetUpSiteWindow window = Shots.LaidOutSetUp(Shots.SampleSetUpView(true, true));
                FrameworkElement body = Shots.Body(window);
                Assert.Equal((double)window.FindResource("SA.Size.DialogWidth"), body.ActualWidth);
                Assert.True(body.ActualHeight <= (double)window.FindResource("SA.Size.DialogMaxHeight"));
                Assert.Equal(1280.0, body.ActualWidth);
                Assert.Equal(WindowStartupLocation.CenterOwner, window.WindowStartupLocation);
                Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
            });
        }

        private static void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        private static IEnumerable<DependencyObject> Ancestors(DependencyObject start)
        {
            for (DependencyObject up = VisualTreeHelper.GetParent(start); up != null; up = VisualTreeHelper.GetParent(up))
            {
                yield return up;
            }
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
