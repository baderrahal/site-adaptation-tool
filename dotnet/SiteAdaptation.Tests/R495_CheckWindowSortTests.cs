using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn.CheckTable;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R495. The 4 Check window sorts by a header click and filters by field. WPF view model, so net48 only. The sample table has 8 rows of
    // 5 plots with the fields heightM, steps, mirrored, yardOutline and insert
    public sealed class R495_CheckWindowSortTests
    {
        private static CheckTableSession Session(List<IReadOnlyList<CheckChoice>> saved = null)
        {
            return new CheckTableSession(Shots.SampleCheckView(false), new CheckTableCallbacks { Save = choices => { saved?.Add(choices); return "list.json"; } }, Shots.SampleRuleLine(), id => { });
        }

        [Fact]
        public void R495_a_header_click_sorts_the_rows_by_that_column_and_a_second_click_reverses_them()
        {
            CheckTableViewModel model = Session().Model;
            List<string> start = model.Rows.Select(row => row.Plot).ToList();

            model.SortBy(CheckColumn.Plot);
            Assert.Equal(start.OrderBy(plot => int.Parse(plot)).ToList(), model.Rows.Select(row => row.Plot).ToList());
            Assert.Equal(CheckSort.Up, model.Marks[CheckColumn.Plot]);
            Assert.Equal("", model.Marks[CheckColumn.Nbh]);

            model.SortBy(CheckColumn.Plot);
            Assert.Equal(start.OrderByDescending(plot => int.Parse(plot)).ToList(), model.Rows.Select(row => row.Plot).ToList());
            Assert.Equal(CheckSort.Down, model.Marks[CheckColumn.Plot]);

            model.SortBy(CheckColumn.Check);
            Assert.Equal(model.Rows.Select(row => row.Check).OrderBy(check => check, System.StringComparer.OrdinalIgnoreCase).ToList(), model.Rows.Select(row => row.Check).ToList());
            Assert.Equal("", model.Marks[CheckColumn.Plot]);
            Assert.Equal(8, model.Rows.Count);
        }

        [Fact]
        public void R495_every_column_of_the_table_sorts_and_none_drops_a_row()
        {
            CheckTableViewModel model = Session().Model;
            foreach (string column in CheckColumn.All)
            {
                model.SortBy(column);
                Assert.Equal(8, model.Rows.Count);
                model.SortBy(column);
                Assert.Equal(8, model.Rows.Count);
            }
            Assert.Equal(9, CheckColumn.All.Count);
            Assert.Equal(9, model.Marks.Count);
        }

        [Fact]
        public void R495_the_field_list_has_all_fields_first_then_each_field_with_its_row_count()
        {
            CheckTableViewModel model = Session().Model;

            Assert.Equal("", model.FieldOptions[0].Field);
            Assert.Equal(CheckTableText.AllFields, model.FieldOptions[0].Text);
            Assert.Equal(new[] { "heightM 1", "insert 1", "mirrored 1", "prototypeFile 1", "steps 1", "yardOutline 1" }, model.FieldOptions.Skip(1).Select(option => option.Text));
            Assert.Equal("", model.FieldPick);
            Assert.False(model.ShowingShown);
        }

        [Fact]
        public void R495_picking_a_field_shows_only_its_rows_with_a_showing_line_and_all_fields_brings_them_back()
        {
            CheckTableViewModel model = Session().Model;

            model.FieldPick = "mirrored";
            Assert.Single(model.Rows);
            Assert.All(model.Rows, row => Assert.Equal("mirrored", row.Row.Field));
            Assert.True(model.ShowingShown);
            Assert.Equal("Showing 1 of 8 rows", model.ShowingLine);

            model.SetChip("Level");
            Assert.Empty(model.Rows);
            model.SetChip("All");
            model.FieldPick = "";
            Assert.Equal(8, model.Rows.Count);
            Assert.False(model.ShowingShown);
        }

        [Fact]
        public void R495_the_filter_and_the_sort_work_together_and_the_find_box_finds_a_word_of_the_note()
        {
            CheckTableViewModel model = Session().Model;

            model.SortBy(CheckColumn.Plot);
            model.SortBy(CheckColumn.Plot);
            model.Find = "link";
            Assert.NotEmpty(model.Rows);
            Assert.All(model.Rows, row => Assert.Contains("link", row.Row.Why.ToString()));
            Assert.Equal(model.Rows.Select(row => int.Parse(row.Plot)).OrderByDescending(plot => plot).ToList(), model.Rows.Select(row => int.Parse(row.Plot)).ToList());
            Assert.Equal("Showing " + model.Rows.Count + " of 8 rows", model.ShowingLine);
            model.FieldPick = "steps";
            Assert.Empty(model.Rows);
        }

        [Fact]
        public void R495_a_use_set_on_a_row_the_filter_hides_stays_in_the_save_the_answer_and_the_follow_line()
        {
            var saved = new List<IReadOnlyList<CheckChoice>>();
            CheckTableSession session = Session(saved);
            CheckTableViewModel model = session.Model;
            string follow = model.FollowLine;
            int level = model.Chips.Single(chip => chip.Chip == "Level").Text.Length;
            CheckRowViewModel row = model.Rows.First(each => each.Row.Field == "steps");
            int index = row.Row.Index;

            row.Use = CheckUse.Skip;
            model.FieldPick = "mirrored";
            model.SortBy(CheckColumn.Use);
            Assert.DoesNotContain(model.Rows, each => each.Row.Index == index);
            Assert.True(model.NotSavedShown);
            session.Save();

            Assert.Single(saved);
            Assert.Contains(saved[0], choice => choice.Index == index && choice.Use == CheckUse.Skip);
            Assert.Contains(session.Answer().Uses, choice => choice.Index == index && choice.Use == CheckUse.Skip);
            Assert.Equal(8, session.Answer().Uses.Count);
            Assert.Equal(follow, model.FollowLine);
            Assert.Equal(level, model.Chips.Single(chip => chip.Chip == "Level").Text.Length);
            model.FieldPick = "";
            Assert.Equal(CheckUse.Skip, model.Rows.Single(each => each.Row.Index == index).Use);
        }

        [Fact]
        public void R495_the_window_has_a_sort_button_and_a_mark_for_each_column_a_field_list_and_a_find_box()
        {
            string xaml = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "CheckTable", "CheckTableWindow.xaml"));
            string code = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "CheckTable", "CheckTableWindow.xaml.cs"));

            foreach (string column in CheckColumn.All)
            {
                Assert.Contains("Binding Marks[" + column + "]", xaml);
            }
            Assert.Equal(9, System.Text.RegularExpressions.Regex.Matches(xaml, "Click=\"OnSort\"").Count);
            Assert.Contains("ItemsSource=\"{Binding FieldOptions}\"", xaml);
            Assert.Contains("SelectedValue=\"{Binding FieldPick, Mode=TwoWay}\"", xaml);
            Assert.Contains("Text=\"{Binding Find, UpdateSourceTrigger=PropertyChanged}\"", xaml);
            Assert.Contains("session.Model.SortBy(", code);
        }
    }
}
