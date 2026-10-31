using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteAdaptation.AddIn.RulesWindow;
using SiteAdaptation.Common.ManualRules;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R403. The window Site Adaptation Rules, its view model and the session that drives its buttons with no click. WPF, so net48 only
    public sealed class RulesWindowTests
    {
        private static string Part()
        {
            string text = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "13-workflow-changes.md")).Replace("\r\n", "\n");
            int start = text.IndexOf("### Site Adaptation Rules (5c)", StringComparison.Ordinal);
            return text.Substring(start, text.IndexOf("\n### ", start + 5, StringComparison.Ordinal) - start);
        }

        [Fact]
        public void R403_the_texts_of_the_window_are_those_of_the_workflow_changes_word_for_word()
        {
            string part = Part();

            Assert.Contains("band line with the manual name, revision, number and \"Rev 06 prevails over Rev 01\"", part);
            Assert.Equal("<n> prevails over <n>", RulesWindowText.Prevails);
            Assert.Contains("buttons " + RulesWindowText.OpenManual + ", " + RulesWindowText.ExportToExcel + ".", part);
            Assert.Contains("\"" + RulesWindowText.PastJobDiffers + "\"", part);
            Assert.Contains("Columns " + string.Join(", ", RulesWindowText.Id, RulesWindowText.Rule, RulesWindowText.Manual, RulesWindowText.CheckedIn, RulesWindowText.PastJob) + ".", part);
            Assert.Contains("full text, values, checked in, past job, what the tool does", part);
            Assert.Equal("Site Adaptation Rules", RulesWindowText.Title);
        }

        [Fact]
        public void R403_the_view_model_shows_the_manual_line_the_revisions_and_the_rows_from_the_block()
        {
            var model = new RulesWindowViewModel(Shots.SampleRulesTable());

            Assert.Equal("Site Adaptation Rules", model.Title);
            Assert.Equal("ROSHN Makkah Site Adaptation Manual, Rev 06, document 01101-PSN-ZZZ-XXX-RPT-ZZ-001000", model.ManualLine);
            Assert.Equal("Rev 06 prevails over Rev 01", model.PrevailsLine);
            Assert.Equal(15, model.Rows.Count);
            Assert.Equal("M01", model.Rows[0].Id);
            Assert.Equal("4 Check, 7 Place villas", model.Rows[0].CheckedIn);
            Assert.Equal("2.1", model.Rows[0].ManualRef);
            Assert.True(model.NoneSelected);
        }

        [Fact]
        public void R403_the_past_job_column_says_not_read_yet_for_a_rule_not_measured_and_marks_the_one_that_differs()
        {
            var model = new RulesWindowViewModel(Shots.SampleRulesTable());

            Assert.Equal("not read yet", model.Rows.Single(row => row.Id == "M01").PastJob);
            Assert.Equal("Matches", model.Rows.Single(row => row.Id == "M04").PastJob);
            Assert.StartsWith("Differs", model.Rows.Single(row => row.Id == "M06").PastJob);
            Assert.Equal(new[] { "M06" }, model.Rows.Where(row => row.Differs).Select(row => row.Id));

            var facts = new Dictionary<string, PastJobFact> { { "M02", new PastJobFact(PastJobState.Differs, "3 of 11 differ") } };
            var given = new RulesWindowViewModel(new ManualRuleTable(Shots.SampleRulesTable().Block, id => facts.TryGetValue(id, out PastJobFact fact) ? fact : null));
            Assert.Equal("3 of 11 differ", given.Rows.Single(row => row.Id == "M02").PastJob);
            Assert.Equal(new[] { "M02", "M06" }, given.Rows.Where(row => row.Differs).Select(row => row.Id));
        }

        [Fact]
        public void R403_the_chips_are_all_the_groups_and_past_job_differs_with_counts_and_a_chip_picks_the_rows()
        {
            var model = new RulesWindowViewModel(Shots.SampleRulesTable());
            Assert.Equal(new[] { "All 15", "Plots and clusters 4", "Levels and steps 4", "Slopes 3", "Walls 3", "Structure 1", "Past job differs 1" }, model.Chips.Select(chip => chip.Text));
            Assert.True(model.Chips[0].Active);

            model.Selected = model.Rows.Single(row => row.Id == "M06");
            model.SetChip(model.Chips.Single(chip => chip.Text.StartsWith("Slopes")).Chip);
            Assert.Equal(new[] { "M05", "M06", "M07" }, model.Rows.Select(row => row.Id));
            Assert.Equal("M06", model.SideId);

            model.SetChip(model.Chips.Single(chip => chip.Text.StartsWith("Walls")).Chip);
            Assert.True(model.NoneSelected);

            model.SetChip(model.Chips.Last().Chip);
            Assert.Equal(new[] { "M06" }, model.Rows.Select(row => row.Id));
            Assert.True(model.Chips.Single(chip => chip.Active).Text.StartsWith("Past job differs"));
        }

        [Fact]
        public void R403_a_rule_picked_by_id_fills_the_side_box_with_the_text_values_checked_in_past_job_and_what_the_tool_does()
        {
            var model = new RulesWindowViewModel(Shots.SampleRulesTable(), "M09");

            Assert.True(model.HasSelected);
            Assert.Equal("M09", model.SideId);
            Assert.Equal("Riser at least 100 mm, at most 180 mm", model.SideText);
            Assert.Equal("Rev 06", model.SideManual);
            Assert.Equal("2.4.A, p. 13 (PDF 16)", model.SideSection);
            Assert.Equal(new[] { "100 mm", "180 mm" }, model.SideValues);
            Assert.Equal("6 Levels", model.SideChecked);
            Assert.Equal("not read yet", model.SidePastJob);
            Assert.Equal("not read yet", model.SideToolDoes);
            Assert.Equal(new[] { "none" }, new RulesWindowViewModel(Shots.SampleRulesTable(), "M01").SideValues);
            Assert.True(new RulesWindowViewModel(Shots.SampleRulesTable(), "M99").NoneSelected);
        }

        [Fact]
        public void R403_open_the_manual_pdf_opens_the_file_found_else_says_where_it_looked_and_a_shell_that_throws_is_shown()
        {
            ManualRuleTable table = Shots.SampleRulesTable();
            ManualInfo manual = table.Block.Manual;
            string first = Path.Combine(manual.Folders[0], manual.File);
            var opened = new List<string>();

            var missing = new RulesWindowSession(table, null, path => false, opened.Add);
            missing.OpenManual();
            Assert.Empty(opened);
            Assert.StartsWith("The manual PDF was not found. Looked for " + first, missing.Model.Message);
            Assert.True(missing.Model.MessageShown);

            var found = new RulesWindowSession(table, null, path => path == first, opened.Add);
            found.OpenManual();
            Assert.Equal(new[] { first }, opened);
            Assert.Equal("", found.Model.Message);

            var refused = new RulesWindowSession(table, null, path => true, path => throw new InvalidOperationException("no reader"));
            refused.OpenManual();
            Assert.Equal("The manual PDF did not open: no reader", refused.Model.Message);
        }

        [Fact]
        public void R403_export_to_excel_says_it_is_not_built_yet_as_no_xlsx_is_written_inside_revit()
        {
            var session = new RulesWindowSession(Shots.SampleRulesTable(), null, null, null);

            session.Export();

            Assert.Equal("Export to Excel is not built yet.", session.Model.Message);
        }

        [Fact]
        public void R403_the_window_has_the_band_with_the_manual_line_the_two_buttons_the_chips_the_five_columns_and_the_side_box_340_wide()
        {
            OnSta(() =>
            {
                RulesWindow window = Shots.LaidOutRules(Shots.SampleRulesTable(), "M06");
                List<FrameworkElement> all = Visuals(Shots.Body(window)).OfType<FrameworkElement>().ToList();
                List<string> texts = all.OfType<TextBlock>().Select(block => block.Text).ToList();

                Assert.Equal("Site Adaptation Rules", window.Title);
                foreach (string text in new[] { "Site Adaptation Rules", "Rev 06 prevails over Rev 01", "ID", "Rule", "Manual", "Checked in", "Past job", "Selected rule", "Full text",
                    "Section and page", "Values", "What the tool does", "M06", "Back yard graded to the front at 0.3% to 0.5%" })
                {
                    Assert.Contains(text, texts);
                }
                Assert.Contains(texts, text => text.StartsWith("ROSHN Makkah Site Adaptation Manual, Rev 06, document "));
                Assert.Equal(new[] { "Open the manual PDF", "Export to Excel" },
                    all.OfType<Button>().Select(button => button.Content).OfType<string>().Where(content => content == "Open the manual PDF" || content == "Export to Excel").ToArray());

                Border band = all.OfType<Border>().First(border => border.Background == window.FindResource("SA.Band"));
                Assert.Contains(Visuals(band).OfType<TextBlock>(), block => block.Text == "Site Adaptation Rules" && block.FontSize == 20.0);

                Border side = all.OfType<Border>().First(border => border.Background == window.FindResource("SA.Panel"));
                Assert.Equal(340.0, side.ActualWidth);

                List<Button> chips = all.OfType<Button>().Where(button => button.Content is string text && Regex.IsMatch(text, @" \d+$")).ToList();
                Assert.Equal(7, chips.Count);
                Assert.Equal(window.FindResource("SA.Band"), chips[0].Background);
                Assert.NotEqual(window.FindResource("SA.Band"), chips[1].Background);

                List<ListBoxItem> items = all.OfType<ListBoxItem>().ToList();
                // The list shows the rows that fit its height, the rest is reached by its scroll bar
                Assert.InRange(items.Count, 10, 15);
                Assert.All(items, item => Assert.True(item.ActualHeight >= 32.0, item.ActualHeight.ToString()));
                Assert.Equal("M06", ((RuleRowViewModel)items.Single(item => item.IsSelected).DataContext).Id);
            });
        }

        [Fact]
        public void R403_the_body_factory_refuses_a_missing_rules_file_before_it_opens_a_window()
        {
            Assert.Throws<FileNotFoundException>(() => RulesWindowBody.Show(IntPtr.Zero, Path.Combine(Path.GetTempPath(), "no-such-rules-file.json")));
            Assert.Throws<FileNotFoundException>(() => RulesWindowBody.Show(IntPtr.Zero, null));
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
