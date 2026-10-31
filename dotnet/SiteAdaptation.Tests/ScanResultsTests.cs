using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Lane C SCAN-UI, rules R435 and R436. The session of the window What the tool read with no click: what each button does, that Cancel
    // changes nothing, and that 2808 conflicts reach the screen through a virtualizing list. net48 only, as the window is WPF
    public sealed class ScanResultsTests : IDisposable
    {
        private const string Excel = "list.xlsx|Sheet 1|Type|shift 0";
        private const string Block = "feet.dwg|block|BLOCKS";
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_scanwin_" + Guid.NewGuid().ToString("N"));

        public ScanResultsTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static ScanResultsSession Session(bool villaClear, int conflicts, ProjectSettings settings = null, Func<string> export = null)
        {
            settings = settings ?? new ProjectSettings();
            return new ScanResultsSession(ScanView.Read(R435_ScanViewTests.Scan(villaClear, conflicts).ToString(), settings), settings, export);
        }

        [Fact]
        public void R435_the_window_counts_the_files_plots_and_conflicts_and_shows_a_box_for_each_value_to_pick()
        {
            ScanResultsViewModel model = Session(false, 3).Model;

            Assert.Equal("5", model.FilesRead);
            Assert.Equal("5", model.PlotsFound);
            Assert.Equal("3", model.ConflictCount);
            Assert.Equal(new[] { "Plot outline", "Plot number", "Villa block", "Villa type" }, model.Values.Select(value => value.Label));
            Assert.Equal(new[] { "Clear", "Clear", "Clear", "Pick" }, model.Values.Select(value => value.Status));
            Assert.True(model.HasUnclear);
            Assert.Equal("Villa type is not clear", model.Unclear.Single().Title);
            Assert.True(model.HasConflicts);

            ScanResultsViewModel none = Session(true, 0).Model;
            Assert.False(none.HasUnclear);
            Assert.False(none.HasConflicts);
        }

        [Fact]
        public void R435_a_changed_dropdown_counts_as_picked_so_the_status_turns_clear_and_the_box_goes()
        {
            ScanResultsViewModel model = Session(false, 0).Model;
            ScanValueViewModel villa = model.Values.Single(value => value.Kind == "villaType");
            var raised = new List<string>();
            villa.PropertyChanged += (sender, args) => raised.Add(args.PropertyName);
            var modelRaised = new List<string>();
            model.PropertyChanged += (sender, args) => modelRaised.Add(args.PropertyName);
            Assert.Null(villa.Selected);
            Assert.True(villa.IsPick);

            villa.Selected = villa.Sources.Single(source => source.Raw == Excel);

            Assert.Equal("Clear", villa.Status);
            Assert.True(villa.IsClear);
            Assert.Equal("3 of 5", villa.Covers);
            Assert.True(villa.Touched);
            Assert.Contains("Status", raised);
            Assert.False(model.HasUnclear);
            Assert.Empty(model.Unclear);
            Assert.Contains("HasUnclear", modelRaised);
            Assert.False(model.Data.NeedsWindow);

            // A value not changed by the user is not Touched
            Assert.False(model.Values[0].Touched);
        }

        [Fact]
        public void R436_use_these_picks_returns_the_settings_with_the_picks_and_ends_the_window()
        {
            var current = new ProjectSettings { VillaMode = VillaModes.Links };
            ScanResultsSession session = Session(false, 3, current);
            int ended = 0;
            session.Finished += () => ended++;
            ScanValueViewModel villa = session.Model.Values.Single(value => value.Kind == "villaType");
            villa.Selected = villa.Sources.Single(source => source.Raw == Excel);
            session.Model.AllConflicts[1].Selected = session.Model.AllConflicts[1].Choices[1];

            session.UseThesePicks();

            Assert.Equal(1, ended);
            Assert.NotNull(session.Answer);
            Assert.Equal(new[] { "villaType" }, session.Answer.ScanPicks.Keys);
            Assert.Equal(Excel, session.Answer.ScanPicks["villaType"]);
            Assert.Equal(new[] { "2|villa type" }, session.Answer.ConflictPicks.Keys);
            Assert.Equal(Excel, session.Answer.ConflictPicks["2|villa type"]);
            Assert.Equal(VillaModes.Links, session.Answer.VillaMode);
            Assert.Null(current.ScanPicks);
            Assert.Null(current.ConflictPicks);
        }

        [Fact]
        public void R436_cancel_returns_null_and_a_proposal_the_user_never_touched_is_not_saved()
        {
            ScanResultsSession session = Session(false, 2);
            int ended = 0;
            session.Finished += () => ended++;
            session.Model.Values.Single(value => value.Kind == "villaType").Selected = session.Model.Values.Single(value => value.Kind == "villaType").Sources[0];

            session.Cancel();

            Assert.Null(session.Answer);
            Assert.Equal(1, ended);

            ScanResultsSession untouched = Session(true, 0);
            untouched.UseThesePicks();
            Assert.Null(untouched.Answer.ScanPicks);
            Assert.Null(untouched.Answer.ConflictPicks);
        }

        [Fact]
        public void R436_use_the_proposed_for_all_picks_the_first_side_of_every_conflict_and_saves_them_all()
        {
            ScanResultsSession session = Session(true, 40);
            session.Model.AllConflicts[5].Selected = session.Model.AllConflicts[5].Choices[1];
            Assert.True(session.Model.Data.NeedsWindow);

            session.UseProposedForAll();

            Assert.All(session.Model.AllConflicts, conflict => Assert.True(conflict.Picked));
            Assert.All(session.Model.AllConflicts, conflict => Assert.Same(conflict.Choices[0], conflict.Selected));
            Assert.False(session.Model.Data.NeedsWindow);
            session.UseThesePicks();
            Assert.Equal(40, session.Answer.ConflictPicks.Count);
            Assert.All(session.Answer.ConflictPicks.Values, pick => Assert.Equal(Block, pick));
        }

        [Fact]
        public void R435_the_list_shows_the_first_20_conflicts_until_show_all_is_pressed()
        {
            ScanResultsSession session = Session(true, 45);
            ScanResultsViewModel model = session.Model;
            var raised = new List<string>();
            model.PropertyChanged += (sender, args) => raised.Add(args.PropertyName);

            Assert.Equal(20, model.Conflicts.Count);
            Assert.True(model.CanShowAll);
            Assert.Equal("Show all 45", model.ShowAllText);

            session.ShowAll();

            Assert.Equal(45, model.Conflicts.Count);
            Assert.False(model.CanShowAll);
            Assert.Contains("Conflicts", raised);

            Assert.False(Session(true, 20).Model.CanShowAll);
        }

        [Fact]
        public void R436_export_writes_the_workbook_the_user_names_says_so_in_the_foot_and_never_overwrites()
        {
            string path = Path.Combine(folder, "read.xlsx");
            ScanResultsSession session = Session(false, 2, null, () => path);

            session.Export();

            Assert.True(File.Exists(path));
            Assert.Equal("Saved read.xlsx.", session.Model.FootLine);
            byte[] before = File.ReadAllBytes(path);
            session.Export();
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Contains("read.xlsx", session.Model.FootLine);
            Assert.NotEqual("Saved read.xlsx.", session.Model.FootLine);

            // A pick of no path changes nothing
            ScanResultsSession none = Session(false, 2, null, () => null);
            string foot = none.Model.FootLine;
            none.Export();
            Assert.Equal(foot, none.Model.FootLine);
        }

        [Fact]
        public void R435_a_window_of_2808_conflicts_realises_only_the_rows_on_screen()
        {
            int realised = 0;
            int shown = 0;
            OnSta(() =>
            {
                var session = Session(true, 2808);
                session.ShowAll();
                var window = new ScanResultsWindow(session);
                Shots.LayOut(window);
                List<ListBox> lists = Visuals(window.Content as DependencyObject).OfType<ListBox>().ToList();
                ListBox list = Assert.Single(lists);
                shown = list.Items.Count;
                realised = Visuals(list).OfType<ListBoxItem>().Count();
            });
            Assert.Equal(2808, shown);
            Assert.InRange(realised, 1, 60);
        }

        [Fact]
        public void R435_the_window_renders_the_chips_amber_for_pick_and_green_for_clear()
        {
            OnSta(() =>
            {
                ScanResultsWindow window = Shots.LaidOutScanResults();
                List<TextBlock> chips = Visuals(window.Content as DependencyObject).OfType<TextBlock>().Where(text => text.Text == "Clear" || text.Text == "Pick").ToList();
                Assert.Equal(new[] { "Clear", "Clear", "Clear", "Pick" }, chips.Select(text => text.Text));
                var ok = (Brush)window.FindResource("SA.Ok");
                var warn = (Brush)window.FindResource("SA.Warn");
                Assert.Equal(((SolidColorBrush)ok).Color, ((SolidColorBrush)chips[0].Foreground).Color);
                Assert.Equal(((SolidColorBrush)warn).Color, ((SolidColorBrush)chips[3].Foreground).Color);
            });
        }

        private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            if (root == null)
            {
                yield break;
            }
            yield return root;
            int count = root is Visual || root is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(root) : 0;
            for (int at = 0; at < count; at++)
            {
                foreach (DependencyObject child in Visuals(VisualTreeHelper.GetChild(root, at)))
                {
                    yield return child;
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
