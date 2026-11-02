using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ReviewData;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R446, Lane C FIX-2NOV-C, the window findings of steps/demo-check.md. Closing 3 Review data, no button that does nothing, Run complete
    // in the pane, the export of What the tool read outside the input folder, Dry run before 4 Check
    public class R446_WindowFixTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r446_" + Guid.NewGuid().ToString("N"));

        public R446_WindowFixTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static ReviewDataSession Marked(Action<IReadOnlyList<ReviewCheck>> save)
        {
            var session = new ReviewDataSession(Shots.SampleReviewView(true), new ReviewCallbacks { Save = save == null ? (Func<IReadOnlyList<ReviewCheck>, string>)null : checks =>
            {
                save(checks);
                return "review.json";
            } });
            session.Model.Selected = session.Model.Rows[0];
            session.Mark(ReviewCheckValue.Wrong);
            Assert.True(session.Model.NotSavedShown);
            return session;
        }

        [Fact]
        public void R446_review_data_with_nothing_to_save_closes_and_asks_nothing()
        {
            var session = new ReviewDataSession(Shots.SampleReviewView(true), new ReviewCallbacks());
            Assert.False(session.Model.NotSavedShown);
            Assert.True(session.CanClose(() => throw new InvalidOperationException("asked")));
        }

        [Fact]
        public void R446_review_data_with_marks_not_saved_asks_and_cancel_stays_and_dont_save_closes_with_no_save()
        {
            int saved = 0;
            ReviewDataSession session = Marked(checks => saved++);

            Assert.False(session.CanClose(() => ReviewCloseChoice.Cancel));
            Assert.True(session.Model.NotSavedShown);
            Assert.True(session.CanClose(() => ReviewCloseChoice.DontSave));
            Assert.Equal(0, saved);
            Assert.False(session.Saved);
        }

        [Fact]
        public void R446_save_of_the_ask_saves_then_closes_and_a_save_that_fails_stays_with_its_reason()
        {
            int saved = 0;
            ReviewDataSession session = Marked(checks => saved++);
            Assert.True(session.CanClose(() => ReviewCloseChoice.Save));
            Assert.Equal(1, saved);
            Assert.True(session.Saved);

            ReviewDataSession failing = Marked(checks => throw new IOException("the file is held"));
            Assert.False(failing.CanClose(() => ReviewCloseChoice.Save));
            Assert.False(failing.Saved);
            Assert.Equal("the file is held", failing.Model.Message);
        }

        [Fact]
        public void R446_the_ask_says_save_your_review_with_its_three_buttons_and_a_close_by_the_x_is_cancel()
        {
            OnSta(() =>
            {
                var ask = new ReviewCloseWindow();
                List<DependencyObject> all = Logical((DependencyObject)ask.Content).ToList();
                Assert.Contains(all.OfType<TextBlock>(), text => text.Text == "Save your review?");
                Assert.Equal(new[] { "Cancel", "Don't save", "Save" }, all.OfType<Button>().Select(button => (string)button.Content));
                Assert.Equal(ReviewCloseChoice.Cancel, ask.Choice);
                ask.Close();
            });
        }

        [Fact]
        public void R446_review_data_has_a_close_in_its_foot_and_a_window_never_shown_closes_with_no_ask()
        {
            OnSta(() =>
            {
                ReviewDataWindow window = Shots.LaidOutReview(Shots.SampleReviewView(true));
                Button close = Tree(Shots.Body(window)).OfType<Button>().Single(button => (string)button.Content == "Close");
                Assert.Equal(Visibility.Visible, close.Visibility);
                Assert.True(ClickHandlers(close) > 0);
                Border side = Tree(Shots.Body(window)).OfType<Border>().First(border => border.Background == window.FindResource("SA.Panel") && DockPanel.GetDock(border) == Dock.Right);
                Assert.IsType<ScrollViewer>(side.Child);
                window.Close();
            });
        }

        // Every button of every window: one shown has a handler or a command, so no click does nothing
        [Fact]
        public void R446_no_window_shows_a_button_that_does_nothing()
        {
            OnSta(() =>
            {
                var bodies = new Dictionary<string, DependencyObject>
                {
                    ["Set up site"] = Shots.Body(Shots.LaidOutSetUp(Shots.SampleSetUpView(true, true))),
                    ["Review data"] = Shots.Body(Shots.LaidOutReview(Shots.SampleReviewView(true))),
                    ["Rules"] = Shots.Body(Shots.LaidOutRules(Shots.SampleRulesTable(), "M06")),
                    ["Check"] = Shots.Body(Shots.LaidOutCheck(Shots.SampleCheckView(false))),
                    ["Update"] = Shots.Body(Shots.LaidOutUpdate()),
                    ["New project"] = Shots.Body(Shots.LaidOutNewProject()),
                    ["How it works"] = Shots.Body(Shots.LaidOutHowItWorks()),
                    ["What the tool read"] = Shots.Body(Shots.LaidOutScanResults()),
                    ["Pane"] = Shots.LaidOutPane(true),
                };
                var idle = new List<string>();
                int shown = 0;
                foreach (KeyValuePair<string, DependencyObject> body in bodies)
                {
                    foreach (ButtonBase button in Tree(body.Value).OfType<ButtonBase>().Where(item => item is Button).Distinct())
                    {
                        if (button.Visibility != Visibility.Visible)
                        {
                            continue;
                        }
                        shown++;
                        if (button.Command == null && ClickHandlers(button) == 0)
                        {
                            idle.Add(body.Key + ": " + button.Content);
                        }
                    }
                }
                Assert.Empty(idle);
                Assert.True(shown > 30, shown.ToString());
            });
        }

        private PaneViewModel Done(IReadOnlyDictionary<string, string> outputs, out DocumentSession session)
        {
            session = new DocumentSession("s", Path.Combine(folder, "Site.rvt"), "Site");
            var model = new PaneViewModel(new NoDisk());
            model.Load(session);
            model.LoadSettings(new PaneSettings { ConfigFile = Path.Combine(folder, "project.json") });
            if (outputs != null)
            {
                session.Runs[9] = new StepRun(DateTime.UtcNow, "hash", new SettingsScope { Kind = ScopeKinds.Project }, 0, outputs);
            }
            model.SetSteps(Enumerable.Range(0, StepPlan.Count).Select(step => new StepStatus(StepKind.Done)).ToList());
            return model;
        }

        [Fact]
        public void R446_when_9_report_ended_the_box_says_run_complete_with_the_file_and_opens_its_folder()
        {
            string report = Path.Combine(folder, "012_Report.xlsx");
            PaneViewModel model = Done(new Dictionary<string, string> { [report] = "hash" }, out DocumentSession session);
            string opened = null;
            int runs = 0;
            model.OpenFolderRequested += path => opened = path;
            model.RunRequested += number => runs++;

            Assert.Equal("Run complete", model.BoxLabel);
            Assert.Equal("012_Report.xlsx", model.BoxName);
            Assert.Equal("The report is in the work folder.", model.BoxLine);
            Assert.Equal("Open folder", model.BoxButton);
            Assert.True(model.BoxButtonShown);

            model.BoxCommand.Execute(null);
            Assert.Equal(folder, opened);
            Assert.Equal(0, runs);
        }

        [Fact]
        public void R446_a_folder_that_is_gone_is_told_and_a_reopened_project_shows_no_open_folder()
        {
            string report = Path.Combine(folder, "gone", "012_Report.xlsx");
            PaneViewModel model = Done(new Dictionary<string, string> { [report] = "hash" }, out DocumentSession session);
            string told = null;
            string opened = null;
            model.Told += text => told = text;
            model.OpenFolderRequested += path => opened = path;
            model.BoxCommand.Execute(null);
            Assert.Null(opened);
            Assert.Contains("not there any more", told);

            PaneViewModel reopened = Done(null, out DocumentSession other);
            Assert.Equal("Run complete", reopened.BoxLabel);
            Assert.False(reopened.BoxButtonShown);
        }

        [Fact]
        public void R446_a_step_left_to_run_keeps_the_next_step_box()
        {
            PaneViewModel model = Done(new Dictionary<string, string> { [Path.Combine(folder, "012_Report.xlsx")] = "hash" }, out DocumentSession session);
            List<StepStatus> steps = Enumerable.Range(0, StepPlan.Count).Select(step => new StepStatus(StepKind.Done)).ToList();
            steps[8] = new StepStatus(StepKind.Ready);
            model.SetSteps(steps);
            Assert.Equal("Next step", model.BoxLabel);
            Assert.Equal("Run 9 " + "Report", model.BoxButton);
        }

        [Fact]
        public void R446_export_refuses_the_input_root_and_a_folder_of_an_input_with_one_line_and_writes_elsewhere()
        {
            string inputs = Path.Combine(folder, "inputs");
            string other = Path.Combine(folder, "other");
            string work = Path.Combine(folder, "work");
            Directory.CreateDirectory(Path.Combine(inputs, "deep"));
            Directory.CreateDirectory(other);
            Directory.CreateDirectory(work);
            string config = Path.Combine(folder, "project.json");
            File.WriteAllText(config, "{\"inputRoot\":" + Newtonsoft.Json.JsonConvert.ToString(inputs) + "}");
            var settings = new ProjectSettings { ConfigFile = config };
            settings.Inputs[InputNames.Baseplan] = new SettingsInput { Path = Path.Combine(other, "site.dwg") };

            foreach (string refused in new[] { Path.Combine(inputs, "a.xlsx"), Path.Combine(inputs, "deep", "a.xlsx"), Path.Combine(other, "a.xlsx") })
            {
                ScanResultsSession session = Session(settings, refused);
                session.Export();
                Assert.False(File.Exists(refused), refused);
                Assert.Equal("The tool never writes in the input folder. Press Export to Excel again, it opens in the work folder.", session.Model.FootLine);
            }

            string good = Path.Combine(work, "a.xlsx");
            ScanResultsSession allowed = Session(settings, good);
            allowed.Export();
            Assert.True(File.Exists(good));
            Assert.Equal("Saved a.xlsx.", allowed.Model.FootLine);
        }

        private static ScanResultsSession Session(ProjectSettings settings, string path)
        {
            return new ScanResultsSession(ScanView.Read(R435_ScanViewTests.Scan(false, 2).ToString(), settings), settings, () => path);
        }

        [Fact]
        public void R446_the_export_dialog_opens_in_the_work_folder_of_the_scan()
        {
            Assert.Equal(@"D:\Work\Model_1", ScanGate.WorkFolderOf(@"D:\Work\Model_1\scan_2_20261101-100000-000\scan.json"));
        }

        [Fact]
        public void R446_a_run_of_4_check_unticks_dry_run_with_one_line_and_a_tick_again_clears_it()
        {
            PaneViewModel model = Loaded();
            Assert.True(model.DryRun);
            Assert.False(model.DryRunNoteShown);

            var state = new PaneRunState(model, () => folder, path => null);
            model.SetSteps(Steps(4));
            Assert.Equal("4 Check has no dry run yet, so the tool unticks Dry run when it runs.", model.BoxLine.Split(new[] { ". " }, StringSplitOptions.None).Last());

            state.BeforeRun(3);
            Assert.True(model.DryRun);

            state.BeforeRun(4);
            Assert.False(model.DryRun);
            Assert.False(state.DryRunFirst);
            Assert.True(model.DryRunNoteShown);
            Assert.Equal("Dry run is off, as 4 Check has no dry run yet.", model.DryRunNote);

            model.DryRun = true;
            Assert.False(model.DryRunNoteShown);
        }

        // Review of FIX-2NOV-C: Update from report has number 0, Statuses[-1] would throw before its flow starts
        [Fact]
        public void R446_a_button_with_no_step_passes_and_leaves_the_tick()
        {
            PaneViewModel model = Loaded();
            var state = new PaneRunState(model, () => folder, path => null);

            state.BeforeRun(0);

            Assert.True(model.DryRun);
            Assert.False(model.DryRunNoteShown);
        }

        [Fact]
        public void R446_a_step_4_that_still_waits_unticks_nothing()
        {
            PaneViewModel model = Loaded();
            var state = new PaneRunState(model, () => folder, path => null);
            List<StepStatus> steps = Steps(4).ToList();
            steps[3] = new StepStatus(StepKind.NeedsStep, 3);
            model.SetSteps(steps);

            state.BeforeRun(4);

            Assert.True(model.DryRun);
            Assert.False(model.DryRunNoteShown);
        }

        [Fact]
        public void R446_a_note_of_one_document_does_not_follow_to_the_next()
        {
            PaneViewModel model = Loaded();
            model.UntickDryRunFor(4);
            Assert.True(model.DryRunNoteShown);
            model.Load(new DocumentSession("other", Path.Combine(folder, "Other.rvt"), "Other"));
            Assert.False(model.DryRunNoteShown);
        }

        private PaneViewModel Loaded()
        {
            var model = new PaneViewModel(new NoDisk());
            model.Load(new DocumentSession("s", Path.Combine(folder, "Site.rvt"), "Site"));
            model.LoadSettings(new PaneSettings { ConfigFile = Path.Combine(folder, "project.json") });
            return model;
        }

        // Done up to the step before next, next Ready, the rest waiting on it
        private static IReadOnlyList<StepStatus> Steps(int next)
        {
            return Enumerable.Range(1, StepPlan.Count).Select(step => step < next ? new StepStatus(StepKind.Done)
                : step == next ? new StepStatus(StepKind.Ready) : new StepStatus(StepKind.NeedsStep, next)).ToList();
        }

        private sealed class NoDisk : IInputProbe
        {
            public InputFacts Look(string path, bool folder)
            {
                return new InputFacts();
            }

            public void Hash(string path, bool folder, Action<string> done)
            {
            }

            public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
            {
                fallback = null;
                return new string[0];
            }

            public string RulesName(string rulesFile)
            {
                return null;
            }

            public string VillaModeDefault(string configFile)
            {
                return null;
            }
        }

        private static int ClickHandlers(ButtonBase button)
        {
            object store = typeof(UIElement).GetProperty("EventHandlersStore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);
            if (store == null)
            {
                return 0;
            }
            var handlers = (RoutedEventHandlerInfo[])store.GetType().GetMethod("GetRoutedEventHandlers").Invoke(store, new object[] { ButtonBase.ClickEvent });
            return handlers == null ? 0 : handlers.Length;
        }

        // The logical and the visual tree, a row of a template is in the second only
        private static IEnumerable<DependencyObject> Tree(DependencyObject root)
        {
            var seen = new HashSet<DependencyObject>();
            var open = new Stack<DependencyObject>();
            open.Push(root);
            while (open.Count > 0)
            {
                DependencyObject item = open.Pop();
                if (!seen.Add(item))
                {
                    continue;
                }
                yield return item;
                foreach (DependencyObject child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>())
                {
                    open.Push(child);
                }
                if (item is Visual)
                {
                    for (int at = 0; at < VisualTreeHelper.GetChildrenCount(item); at++)
                    {
                        open.Push(VisualTreeHelper.GetChild(item, at));
                    }
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
