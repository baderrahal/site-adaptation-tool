using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.CheckTable;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R376. The window of button 4 Check, its view model and the session that drives its buttons with no click. WPF, so net48 only.
    // The statics of the body are set in this class only, so no two tests set them at once
    public sealed class CheckTableTests
    {
        private static string Spec(string name)
        {
            return File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", name)).Replace("\r\n", "\n");
        }

        [Fact]
        public void R376_the_texts_of_the_window_are_those_of_the_mock_the_ui_spec_and_the_workflow_changes_word_for_word()
        {
            string mock = Spec("07-mock.md");
            string ui = Spec("12-ui-spec.md");

            Assert.Contains("\"" + CheckTableText.HowTo + "\"", ui);
            Assert.Contains("\"4 Check, NBH [code], [n] plots\"", mock);
            Assert.Contains("\"4 Check, NBH all, [n] plots\"", mock);
            Assert.Contains("Buttons " + string.Join(", ", CheckTableText.ZoomToPlot, CheckTableText.ExportToExcel, CheckTableText.SaveOverrides) + ".", mock);
            Assert.Contains("Columns: " + string.Join(", ", CheckTableText.Plot, CheckTableText.Nbh, CheckTableText.Unit, CheckTableText.Check, CheckTableText.ToolValue,
                CheckTableText.PastJob, CheckTableText.Rule, CheckTableText.Use) + ".", mock);
            Assert.Contains("Use is a dropdown " + string.Join(", ", CheckTableText.Rule, CheckTableText.Past, CheckTableText.Skip) + ".", mock);
            Assert.Contains("\"" + CheckTableText.Selected + "\", `\"Plot <n>, <unit>, <check>\"`", ui);
            Assert.Equal("Plot <n>, <unit>, <check>", CheckTableText.SelectedPlot);
            Assert.Contains("Rule \"" + CheckTableText.KeepTool + "\", Past \"" + CheckTableText.UsePast + "\", Skip \"" + CheckTableText.LeaveOut + "\"", ui);
            Assert.Contains("\"" + CheckTableText.OpenRules + "\"", ui);
            Assert.Contains("\"Level profile parsons2024Rev6, override file roshn-makkah.json, 12,421 overrides, 1,227 of them on NBH 012\"", mock);
            Assert.Equal("Level profile <n>, override file <n>, <n> overrides, <n> of them on NBH <n>", CheckTableText.ProfileLine);
        }

        [Fact]
        public void R376_the_titles_the_follow_line_and_the_no_past_job_line_of_the_text_equal_those_common_composes()
        {
            CheckTableView view = Shots.SampleCheckView(false);
            string all = CheckTableBuild.Compose(null, new Newtonsoft.Json.Linq.JObject(), new Newtonsoft.Json.Linq.JObject(), null, null, null, true).Title;

            Assert.Equal(TextTemplate.Fill(CheckTableText.Title, "012", 5), view.Title);
            Assert.Equal(TextTemplate.Fill(CheckTableText.TitleAll, 0), all);
            Assert.Equal(TextTemplate.Fill(CheckTableText.FollowLine, view.FollowEveryRule), view.FollowLine);
            Assert.Equal(CheckTableBuild.NoPastJob, CheckTableText.NoPastJob);
        }

        [Fact]
        public void R376_the_view_model_shows_the_title_the_profile_line_the_chips_and_the_manual_rule_id_of_each_row()
        {
            var session = new CheckTableSession(Shots.SampleCheckView(false), new CheckTableCallbacks { Save = choices => "list.json" }, Shots.SampleRuleLine(), id => { });
            CheckTableViewModel model = session.Model;

            Assert.Equal("4 Check, NBH 012, 5 plots", model.Title);
            Assert.Equal("Level profile parsons2024Rev6, override file roshn-makkah.json, 12 overrides, 5 of them on NBH 012", model.ProfileLine);
            Assert.Equal(new[] { "All 8", "Plot cut 1", "Floor 1", "Hand 1", "Unit code 1", "Position 1", "Level 2", "Team check 1", "Assumed 1" }, model.Chips.Select(chip => chip.Text));
            Assert.Equal(new[] { "All" }, model.Chips.Where(chip => chip.Active).Select(chip => chip.Chip));
            Assert.Equal(new[] { "Assumed" }, model.Chips.Where(chip => chip.IsAssumed).Select(chip => chip.Chip));
            Assert.Equal(8, model.Rows.Count);
            Assert.Equal("M01", model.Rows.Single(row => row.Check == "Hand").ManualRule);
            Assert.Equal("M06", model.Rows.Single(row => row.Check == "Floor").ManualRule);
            Assert.Equal("M13", model.Rows.Single(row => row.Check == "Position").ManualRule);
            Assert.Equal("M10", model.Rows.Single(row => row.Check == "Level" && row.Row.Field == "steps").ManualRule);
            Assert.Equal("", model.Rows.Single(row => row.Check == "Level" && row.Row.Field == "heightM").ManualRule);
            Assert.Equal("", model.Rows.Single(row => row.Check == "Plot cut").ManualRule);
            Assert.Equal("M01", model.Rows.Single(row => row.Check == "Team check").ManualRule);
            Assert.StartsWith("M13 Front setback", model.Rows.Single(row => row.Check == "Position").ManualRuleTip);
            Assert.EndsWith("plots follow every rule", model.FollowLine);
            Assert.False(model.NoPastJobShown);

            model.SetChip("Level");
            Assert.Equal(new[] { "Level", "Level" }, model.Rows.Select(row => row.Check));
            model.SetChip("Assumed");
            Assert.Empty(model.Rows);
        }

        [Fact]
        public void R376_Past_is_grey_with_its_reason_as_the_tooltip_where_it_is_not_offered_and_a_use_set_in_the_row_shows_in_the_side_box()
        {
            var session = new CheckTableSession(Shots.SampleCheckView(false), new CheckTableCallbacks { Save = choices => "list.json" }, Shots.SampleRuleLine(), id => { });
            CheckTableViewModel model = session.Model;
            CheckRowViewModel hand = model.Rows.Single(row => row.Check == "Hand");
            CheckRowViewModel level = model.Rows.Single(row => row.Check == "Level" && row.Row.Field == "heightM");

            UseOptionViewModel past = hand.UseOptions.Single(option => option.Name == "Past");
            Assert.False(past.Enabled);
            Assert.Equal("no answer key holds plot 32", past.Tip);
            Assert.True(level.UseOptions.Single(option => option.Name == "Past").Enabled);
            Assert.Equal(new[] { "Rule", "Past", "Skip" }, hand.UseOptions.Select(option => option.Name));
            Assert.Equal("Past", level.Use);

            model.Selected = hand;
            Assert.False(model.PastChoosable);
            Assert.Equal("no answer key holds plot 32", model.PastGreyTip);
            Assert.Equal("Plot 32, DT1, Hand", model.SelectedLine);
            Assert.Throws<ArgumentException>(() => { hand.Use = "Past"; });
            model.Choose("Skip");
            Assert.Equal("Skip", hand.Use);
            Assert.True(model.NotSavedShown);
            Assert.Equal("not mirrored", model.GiveRule);
            Assert.Equal("", model.GiveSkip);
        }

        [Fact]
        public void R376_Save_overrides_gives_only_the_changed_rows_and_a_callback_that_throws_leaves_the_window_open_with_its_reason()
        {
            IReadOnlyList<CheckChoice> given = null;
            var session = new CheckTableSession(Shots.SampleCheckView(false), new CheckTableCallbacks { Save = choices => { given = choices; return "list.json"; } }, Shots.SampleRuleLine(), id => { });
            CheckRowViewModel hand = session.Model.Rows.Single(row => row.Check == "Hand");
            session.Save();
            Assert.Empty(given);

            hand.Use = "Skip";
            session.Save();

            Assert.Equal(new[] { hand.Row.Index + "|Skip" }, given.Select(choice => choice.Index + "|" + choice.Use));
            Assert.True(session.Saved);
            Assert.Equal("Overrides saved.", session.Model.Message);
            Assert.False(session.Model.NotSavedShown);
            Assert.True(session.Answer().Saved);
            Assert.Equal("Skip", session.Answer().Uses.Single(use => use.Index == hand.Row.Index).Use);

            var failing = new CheckTableSession(Shots.SampleCheckView(false), new CheckTableCallbacks { Save = choices => throw new IOException("the override list is read only") }, null, null);
            failing.Save();
            Assert.False(failing.Saved);
            Assert.Equal("the override list is read only", failing.Model.Message);
        }

        [Fact]
        public void R376_in_a_dry_run_Save_overrides_is_grey_and_does_nothing()
        {
            bool saved = false;
            var session = new CheckTableSession(Shots.SampleCheckView(true), new CheckTableCallbacks { Save = choices => { saved = true; return "x"; } }, null, null);

            Assert.False(session.Model.SaveAllowed);
            session.Save();
            Assert.False(saved);
            Assert.False(session.Saved);
            Assert.False(new CheckTableSession(Shots.SampleCheckView(false), null, null, null).Model.SaveAllowed);
        }

        [Fact]
        public void R376_the_side_box_shows_the_manual_rule_and_Open_in_Site_Adaptation_Rules_hands_the_id_of_the_row_on_and_a_row_with_none_has_no_button()
        {
            string opened = null;
            var session = new CheckTableSession(Shots.SampleCheckView(false), new CheckTableCallbacks(), Shots.SampleRuleLine(), id => opened = id);
            session.Model.Selected = session.Model.Rows.Single(row => row.Check == "Position");

            Assert.True(session.Model.ManualRuleShown);
            Assert.StartsWith("M13 Front setback 5.52 m", session.Model.ManualRuleLine);
            session.OpenRule();
            Assert.Equal("M13", opened);

            session.Model.Selected = session.Model.Rows.Single(row => row.Check == "Plot cut");
            Assert.False(session.Model.ManualRuleShown);
            opened = null;
            session.OpenRule();
            Assert.Null(opened);

            var failing = new CheckTableSession(Shots.SampleCheckView(false), new CheckTableCallbacks(), null, id => throw new FileNotFoundException("the rules file was not found"));
            failing.Model.Selected = failing.Model.Rows.Single(row => row.Check == "Position");
            failing.OpenRule();
            Assert.Equal("the rules file was not found", failing.Model.Message);
        }

        [Fact]
        public void R376_Zoom_and_Export_are_grey_until_the_revit_side_gives_them()
        {
            var bare = new CheckTableSession(Shots.SampleCheckView(false), new CheckTableCallbacks(), null, null);
            Assert.False(bare.Model.ZoomAllowed);
            Assert.False(bare.Model.ExportAllowed);
            bare.Zoom();
            bare.Export();
            Assert.Equal("", bare.Model.Message);

            CheckRow zoomed = null;
            var wired = new CheckTableSession(Shots.SampleCheckView(false), new CheckTableCallbacks { Zoom = row => { zoomed = row; return "Zoomed."; }, Export = () => "Exported." }, null, null);
            wired.Model.Selected = wired.Model.Rows[0];
            wired.Zoom();
            Assert.Same(wired.Model.Rows[0].Row, zoomed);
            Assert.Equal("Zoomed.", wired.Model.Message);
            wired.Export();
            Assert.Equal("Exported.", wired.Model.Message);
        }

        [Fact]
        public void R376_the_window_has_the_band_the_guide_line_the_chips_the_table_with_a_use_dropdown_and_the_side_box_340_wide_on_the_panel()
        {
            OnSta(() =>
            {
                CheckTableWindow window = Shots.LaidOutCheck(Shots.SampleCheckView(false));
                List<FrameworkElement> all = Visuals(Shots.Body(window)).OfType<FrameworkElement>().ToList();
                List<string> texts = all.OfType<TextBlock>().Select(block => block.Text).ToList();

                Assert.Equal("4 Check, NBH 012, 5 plots", window.Title);
                foreach (string text in new[] { CheckTableText.HowTo, "Plot", "NBH", "Unit", "Check", "Tool value (rule)", "Past job", "Rule", "Manual rule", "Use", "Selected", "Plot 31, VL4, Level", "Tool",
                    "Choose", "Keep the tool value.", "Use the past job. Saved as an override.", "Leave this plot out. It is listed in the report." })
                {
                    Assert.Contains(text, texts);
                }
                Assert.Contains(texts, text => text.StartsWith("Level profile parsons2024Rev6"));
                Assert.Contains(texts, text => text.EndsWith("plots follow every rule"));
                Assert.Equal(new[] { "Zoom to plot", "Export to Excel", "Save overrides" },
                    all.OfType<Button>().Select(button => button.Content).OfType<string>().Where(content => new[] { "Zoom to plot", "Export to Excel", "Save overrides" }.Contains(content)).ToArray());

                Border band = all.OfType<Border>().First(border => border.Background == window.FindResource("SA.Band"));
                Assert.Contains(Visuals(band).OfType<TextBlock>(), block => block.Text.StartsWith("4 Check") && block.FontSize == 20.0);
                Button save = Visuals(band).OfType<Button>().Single(button => (string)button.Content == "Save overrides");
                Assert.Equal(window.FindResource("SA.Button.Primary"), save.Style);
                Assert.True(save.IsEnabled);
                Assert.False(Visuals(band).OfType<Button>().Single(button => (string)button.Content == "Zoom to plot").IsEnabled);

                Border side = all.OfType<Border>().First(border => border.Background == window.FindResource("SA.Panel"));
                Assert.Equal(340.0, side.ActualWidth);
                Assert.Contains(Visuals(side).OfType<Button>(), button => (string)button.Content == "Open in Site Adaptation Rules");

                List<Button> chips = all.OfType<Button>().Where(button => button.Content is string text && System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z ]+ \d+$")
                    && !new[] { "Zoom to plot", "Export to Excel" }.Contains(text)).ToList();
                Assert.Equal(9, chips.Count);
                Assert.Equal(window.FindResource("SA.Band"), chips[0].Background);
                Assert.NotEqual(window.FindResource("SA.Band"), chips[1].Background);
                Rectangle dashed = all.OfType<Rectangle>().Single(rectangle => rectangle.StrokeDashArray.Count > 0 && rectangle.Visibility == Visibility.Visible);
                Assert.Equal(window.FindResource("SA.Warn"), dashed.Stroke);
                Assert.Equal(Visibility.Visible, dashed.Visibility);
                Assert.Equal(9, all.OfType<Rectangle>().Count(rectangle => rectangle.StrokeDashArray.Count > 0));

                List<ListBoxItem> items = all.OfType<ListBoxItem>().ToList();
                Assert.Equal(8, items.Count);
                Assert.All(items, item => Assert.True(item.ActualHeight >= 32.0, item.ActualHeight.ToString()));
                Assert.Equal(1, items.Count(item => item.IsSelected));
                Border mark = Visuals(items.Single(item => item.IsSelected)).OfType<Border>().Single(border => border.Background == window.FindResource("SA.Accent"));
                Assert.Equal(3.0, mark.Width);
                List<ComboBox> uses = all.OfType<ComboBox>().ToList();
                Assert.Equal(8, uses.Count);
                Assert.Equal(new[] { "Rule", "Past", "Skip" }, uses[0].Items.OfType<UseOptionViewModel>().Select(option => option.Name));
            });
        }

        [Fact]
        public void R376_a_dry_run_window_has_Save_overrides_grey_and_the_no_past_job_line_shows_when_no_past_job_was_given()
        {
            OnSta(() =>
            {
                CheckTableWindow window = Shots.LaidOutCheck(Shots.SampleCheckView(true));
                List<FrameworkElement> all = Visuals(Shots.Body(window)).OfType<FrameworkElement>().ToList();

                Assert.False(all.OfType<Button>().Single(button => (string)button.Content == "Save overrides").IsEnabled);

                CheckTableView none = Shots.SampleCheckView(false);
                none.NoPastJobLine = CheckTableBuild.NoPastJob;
                CheckTableWindow shown = Shots.LaidOutCheck(none);
                Assert.Contains(Visuals(Shots.Body(shown)).OfType<TextBlock>(), block => block.Text == CheckTableBuild.NoPastJob);
            });
        }

        [Fact]
        public void R376_button_4_is_built_once_the_window_is_set_and_the_pane_wires_the_manual_rule_lookup_and_the_inputs()
        {
            SiteAdaptation.Common.Flow.ButtonBody made = new SiteAdaptation.Common.Flow.ButtonBody();
            RibbonButtonSpec four = RibbonLayout.Button(4);
            CheckTableBody.Dialog = null;
            Assert.False(CheckTableBody.IsSet);
            Assert.Equal("4 Check is not built yet.", SiteAdaptation.AddIn.Bodies.Of(four, null, null, null, () => made).NotBuilt);
            CheckTableBody.Dialog = (owner, rules) => new CheckTableDialog(owner, rules);
            try
            {
                Assert.True(CheckTableBody.IsSet);
                Assert.Same(made, SiteAdaptation.AddIn.Bodies.Of(four, null, null, null, () => made));
                Assert.Equal("4 Check is not built yet.", SiteAdaptation.AddIn.Bodies.Of(four, null, null, null, null).NotBuilt);
                Assert.Equal("4 Check is not built yet.", SiteAdaptation.AddIn.Bodies.Of(four).NotBuilt);
            }
            finally
            {
                CheckTableBody.Dialog = null;
            }

            var settings = new PaneSettings { RulesFile = "rules.json" };
            settings.Pick(InputKeys.Prototypes, "protos");
            Assert.Equal(new[] { "Rules=rules.json", "Prototypes=protos" }, CheckTableBody.Inputs(settings).Select(input => input.Name + "=" + input.Path));
            Assert.Equal(new[] { "config", "rules", "prototypes" }, new[] { PaneText.Settings, PaneText.Rules, PaneText.Prototypes }.Select(name => CheckTableBody.Roles[name]));

            string addin = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn");
            Assert.Contains("CheckTable.CheckTableBody.Dialog = (owner, rules) => new CheckTable.CheckTableDialog(owner, rules);", File.ReadAllText(Path.Combine(addin, "App", "App.cs")));
            string host = File.ReadAllText(Path.Combine(addin, "App", "PaneHost.cs"));
            Assert.Contains("CheckTable.CheckTableBody.ManualRule = ", host);
            Assert.Contains("IdOfCheck", host);
            Assert.Contains("CheckTable.CheckTableBody.For(", File.ReadAllText(Path.Combine(addin, "Commands", "Buttons.cs")));
            Assert.Contains("PlaceVillas.PlaceVillasBody.Load = context =>", host);
            Assert.Contains("Common.PlaceVillas.VillaLoad.Load(", host);
            Assert.Contains("PlaceVillas.PlaceVillasBody.For(application)", File.ReadAllText(Path.Combine(addin, "Commands", "Buttons.cs")));
        }

        [Fact]
        public void R376_the_dialog_gives_a_rule_line_from_the_profile_and_the_id_alone_when_the_file_is_not_there()
        {
            Func<string, string> line = CheckTableDialog.RuleLine(Shots.RulesFile());
            Assert.StartsWith("M13 Front setback", line("M13"));
            Assert.Equal("M99", line("M99"));
            Assert.Equal("M13", CheckTableDialog.RuleLine(Path.Combine(Path.GetTempPath(), "no-such-rules.json"))("M13"));
            Assert.Equal("M13", CheckTableDialog.RuleLine(null)("M13"));
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
