using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Lane B 5a PR 6. The addinRun job of the test host, which measures open items 1 and 2 of docs/design/09-risks.md in Revit. The
    // logic that decides and records is plain, so it is tested here with no Revit, on net48 and net8.0
    public class AddinRunTests
    {
        private const string Token = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
        private static readonly DateTime Now = new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);
        private const string Documents = @"C:\Users\x\Documents";
        private const string Folder = Documents + @"\SiteAdaptation\addin_run\SA5A_20261028-090000";
        private const string Model = Folder + @"\SA5A-TEST-NEW.rvt";
        private const string Template = @"C:\ProgramData\Autodesk\RVT 2024\Templates\English\DefaultMetric.rte";
        private const string SaveFirst = "Save the project first, its work folder is named by its file";

        private static string RepoText(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules", "index.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return File.ReadAllText(Path.Combine(new[] { folder }.Concat(parts).ToArray()));
        }

        private static string Between(string text, string from, string to)
        {
            int start = text.IndexOf(from, StringComparison.Ordinal);
            int end = text.IndexOf(to, start + 1, StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, from + " to " + to);
            return text.Substring(start, end - start);
        }

        private static JObject Job()
        {
            return new JObject
            {
                ["kind"] = "addinRun",
                ["token"] = Token,
                ["code"] = "SA5A",
                ["model"] = Model,
                ["template"] = Template,
                ["createdUtc"] = Now.ToString("o"),
                ["totalMinutes"] = 13,
            };
        }

        private static GuardResult Check(JObject job, Func<string, bool> fileExists = null, Func<string, bool> folderExists = null)
        {
            return TestHostGuard.Check(job.ToString(), Token, Now, fileExists ?? (path => path == Template), folderExists ?? (path => path == Folder));
        }

        [WindowsFact]
        public void R213_the_add_in_run_names_its_three_files_by_the_new_site_tag()
        {
            Assert.Equal(new[] { Model, Folder + @"\SA5A-SECOND-TEST-NEW.rvt", Folder + @"\SA5A-CENTRAL-TEST-NEW.rvt" }, AddinRunFiles.All(Model));
            Assert.All(AddinRunFiles.All(Model), file => Assert.True(TestHostGuard.IsNewSite(file), file));
        }

        [WindowsFact]
        public void R103_the_guard_runs_an_add_in_run_job_that_makes_only_new_files_from_the_template()
        {
            GuardResult good = Check(Job());
            Assert.True(good.Act, good.Reason);
            Assert.Equal(TestHostGuard.AddinRunKind, good.Job.Kind);

            var refused = new List<Tuple<JObject, Func<string, bool>, Func<string, bool>, string>>();
            JObject plan = Job();
            plan["plan"] = @"C:\a\plan.json";
            refused.Add(Tuple.Create(plan, (Func<string, bool>)null, (Func<string, bool>)null, "names no plan"));
            JObject prototype = Job();
            prototype["prototypeFile"] = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-3100.rvt";
            refused.Add(Tuple.Create(prototype, (Func<string, bool>)null, (Func<string, bool>)null, "names no prototype file"));
            JObject site = Job();
            site["site"] = @"C:\a\site.json";
            refused.Add(Tuple.Create(site, (Func<string, bool>)null, (Func<string, bool>)null, "site goes with a newSite job only"));
            JObject name = Job();
            name["model"] = Folder + @"\SA5B-TEST-NEW.rvt";
            refused.Add(Tuple.Create(name, (Func<string, bool>)null, (Func<string, bool>)null, "makes SA5A-TEST-NEW.rvt"));
            JObject copy = Job();
            copy["model"] = Folder + @"\SA5A_TEST-SA5A.rvt";
            refused.Add(Tuple.Create(copy, (Func<string, bool>)null, (Func<string, bool>)null, "-TEST-NEW.rvt"));
            refused.Add(Tuple.Create(Job(), (Func<string, bool>)(path => path == Template || path == Folder + @"\SA5A-CENTRAL-TEST-NEW.rvt"), (Func<string, bool>)null, "never overwritten"));
            refused.Add(Tuple.Create(Job(), (Func<string, bool>)(path => false), (Func<string, bool>)null, "the template does not exist"));
            refused.Add(Tuple.Create(Job(), (Func<string, bool>)null, (Func<string, bool>)(path => false), "the run folder does not exist"));
            JObject rte = Job();
            rte["template"] = @"C:\a\DefaultMetric.rvt";
            refused.Add(Tuple.Create(rte, (Func<string, bool>)null, (Func<string, bool>)null, ".rte"));

            foreach (var item in refused)
            {
                GuardResult result = Check(item.Item1, item.Item2, item.Item3);
                Assert.False(result.Act, item.Item4);
                Assert.Contains(item.Item4, result.Reason);
            }
        }

        [WindowsFact]
        public void R110_an_add_in_run_sits_in_a_run_folder_under_the_work_root_never_in_it_or_beside_it()
        {
            var job = new TestHostJob { Code = "SA5A", Kind = TestHostGuard.AddinRunKind, Model = Model };
            string jobPath = TestHostGuard.JobPath(Documents, "SA5A", false);

            Assert.Null(TestHostGuard.CheckJobPath(jobPath, job, Documents));
            job.Model = Documents + @"\SiteAdaptation\SA5A-TEST-NEW.rvt";
            Assert.Contains("under the work root", TestHostGuard.CheckJobPath(jobPath, job, Documents));
            job.Model = @"C:\02-Site Adaptation\SA5A-TEST-NEW.rvt";
            Assert.Contains("under the work root", TestHostGuard.CheckJobPath(jobPath, job, Documents));
            job.Model = Documents + @"\SiteAdaptationOther\x\SA5A-TEST-NEW.rvt";
            Assert.Contains("under the work root", TestHostGuard.CheckJobPath(jobPath, job, Documents));
        }

        [Fact]
        public void R104_a_failed_phase_goes_on_at_the_phases_that_always_run()
        {
            // The documents, the routing, item 2, then from 3 the pane and the pictures, which always run
            var phases = new AddinRunPhases(5, 3);
            Assert.Equal(0, phases.Next(true));
            Assert.Equal(3, phases.Next(false));
            Assert.True(phases.Failed);
            Assert.Equal(4, phases.Next(true));
            Assert.Equal(-1, phases.Next(true));

            // A failed phase among those that always run does not skip the next one
            var late = new AddinRunPhases(5, 3);
            Assert.Equal(0, late.Next(true));
            Assert.Equal(1, late.Next(true));
            Assert.Equal(2, late.Next(true));
            Assert.Equal(3, late.Next(true));
            Assert.Equal(4, late.Next(false));
            Assert.True(late.Failed);
            Assert.Equal(-1, late.Next(true));

            var clean = new AddinRunPhases(2, 1);
            Assert.Equal(0, clean.Next(true));
            Assert.Equal(1, clean.Next(true));
            Assert.Equal(-1, clean.Next(true));
            Assert.False(clean.Failed);
        }

        // Run 2 of 5a: OpenAndActivateDocument from Idling threw "Switching active documents is not allowed during API event handling".
        // The phases now run in an ExternalEvent, and the Idling clock only ends waits, raises and gives up
        [Fact]
        public void R372_the_idle_clock_ends_a_wait_raises_the_next_phase_and_gives_up_on_a_phase_revit_never_runs()
        {
            var schedule = new AddinRunSchedule();
            schedule.Raise(Now);
            Assert.True(schedule.Raised);
            Assert.Equal(TickAction.None, schedule.Tick(Now.AddSeconds(5), 0, out WaitState? ended, out double seconds));
            Assert.Null(ended);
            schedule.Ran();
            Assert.False(schedule.Raised);

            schedule.Wait(Now, 2, 60);
            Assert.Equal(TickAction.None, schedule.Tick(Now.AddSeconds(1), 0, out ended, out seconds));
            Assert.Equal(TickAction.None, schedule.Tick(Now.AddSeconds(10), 1, out ended, out seconds));
            Assert.Equal(TickAction.Raise, schedule.Tick(Now.AddSeconds(12), 0, out ended, out seconds));
            Assert.Equal(WaitState.Ready, ended);
            Assert.Equal(12, seconds);
            Assert.False(schedule.Waiting);
            Assert.True(schedule.Raised);

            schedule.Ran();
            schedule.Wait(Now, 3, 60);
            Assert.Equal(TickAction.Raise, schedule.Tick(Now.AddSeconds(60), 2, out ended, out seconds));
            Assert.Equal(WaitState.TimedOut, ended);

            Assert.Equal(TickAction.None, schedule.Tick(Now.AddSeconds(60 + AddinRunSchedule.RaiseMaxSeconds - 1), 0, out ended, out seconds));
            Assert.Equal(TickAction.GiveUp, schedule.Tick(Now.AddSeconds(60 + AddinRunSchedule.RaiseMaxSeconds), 0, out ended, out seconds));
            Assert.False(schedule.Raised);
        }

        // QL20. Run 2 of 5a: Revit refused "application.Idling += OnTick" inside an Idling handler. Every handler is added once at the
        // start of Revit, and a flag tells each one whether the job runs, so no handler is ever added or removed in an event
        [Fact]
        public void R372_the_run_gate_starts_once_and_ends_by_its_flag_never_by_a_handler_removed()
        {
            var gate = new RunGate();
            Assert.False(gate.Started);
            Assert.False(gate.Live);
            Assert.True(gate.Begin());
            Assert.True(gate.Live);
            Assert.False(gate.Begin());
            gate.End();
            Assert.True(gate.Finished);
            Assert.False(gate.Live);
            Assert.False(gate.Begin());

            // A job that ends before it started never starts after
            var early = new RunGate();
            early.End();
            Assert.False(early.Begin());
            Assert.False(early.Live);
        }

        // QL20 point 4. The first thing run 3 measures: whether a raised ExternalEvent runs on the Home screen of Revit, no document open
        [Fact]
        public void R372_the_home_screen_event_records_its_raise_and_its_first_run_only()
        {
            var home = new RunHomeEvent();
            home.Raise(Now, 0, true);
            Assert.True(home.Raised);
            Assert.Equal(Now, home.RaisedUtc);
            Assert.Equal(0, home.DocumentsAtRaise);
            Assert.Null(home.Ran);
            Assert.True(home.Run(Now.AddSeconds(1.26), true, 0));
            Assert.True(home.Ran);
            Assert.Equal(1.3, home.Seconds);
            Assert.True(home.NoDocument);
            Assert.Equal(0, home.DocumentsAtRun);
            // Later phases run from the same event, and the first run stays the one recorded
            Assert.False(home.Run(Now.AddSeconds(50), false, 2));
            Assert.Equal(1.3, home.Seconds);
            home.NotRun();
            Assert.True(home.Ran);

            var never = new RunHomeEvent();
            never.Raise(Now, 0, true);
            never.NotRun();
            Assert.False(never.Ran);
            Assert.False(never.Run(Now.AddSeconds(200), true, 0));

            var refused = new RunHomeEvent();
            refused.Raise(Now, 0, false);
            Assert.False(refused.Raised);
            refused.NotRun();
            Assert.Null(refused.Ran);
        }

        [WindowsFact]
        public void R372_the_home_screen_event_is_held_not_held_or_not_measured()
        {
            AddinRunRecord held = Expected();
            AddinRunJudge.Judge(held);
            Assert.True(CheckOf(held, AddinRunJudge.HomeScreenEvent).Held == true, CheckOf(held, AddinRunJudge.HomeScreenEvent).Detail);
            Assert.Equal("home", CheckOf(held, AddinRunJudge.HomeScreenEvent).Item);

            AddinRunRecord never = Expected();
            never.HomeEvent = new RunHomeEvent();
            never.HomeEvent.Raise(Now, 0, true);
            never.HomeEvent.NotRun();
            AddinRunJudge.Judge(never);
            Assert.True(CheckOf(never, AddinRunJudge.HomeScreenEvent).Held == false);
            Assert.Contains("never ran", CheckOf(never, AddinRunJudge.HomeScreenEvent).Detail);

            var unread = new Action<AddinRunRecord>[]
            {
                run => run.HomeEvent = new RunHomeEvent(),
                run => run.HomeEvent.Ran = null,
                run => run.HomeEvent.NoDocument = false,
                run => run.HomeEvent.DocumentsAtRun = 1,
                run => run.HomeEvent.DocumentsAtRaise = 1,
            };
            foreach (Action<AddinRunRecord> change in unread)
            {
                AddinRunRecord record = Expected();
                change(record);
                AddinRunJudge.Judge(record);
                Assert.Null(CheckOf(record, AddinRunJudge.HomeScreenEvent).Held);
                Assert.False(string.IsNullOrEmpty(CheckOf(record, AddinRunJudge.HomeScreenEvent).Detail));
            }
        }

        // QL20 point 5. The whole addinRun job ends by 15 minutes, a tool timing
        [WindowsFact]
        public void R372_an_add_in_run_job_is_capped_at_15_minutes()
        {
            Assert.Equal(15, TestHostGuard.AddinRunMaxTotalMinutes);
            foreach (double minutes in new[] { 13, 15 })
            {
                JObject job = Job();
                job["totalMinutes"] = minutes;
                GuardResult result = Check(job);
                Assert.True(result.Act, result.Reason);
                Assert.Equal(minutes, TestHostGuard.TotalMinutesOf(result.Job));
            }
            foreach (double minutes in new[] { 15.5, 16, 27, 29 })
            {
                JObject job = Job();
                job["totalMinutes"] = minutes;
                GuardResult result = Check(job);
                Assert.False(result.Act, minutes.ToString());
                Assert.Contains("at most 15", result.Reason);
            }
            JObject none = Job();
            none.Remove("totalMinutes");
            Assert.Equal(15, TestHostGuard.TotalMinutesOf(Check(none).Job));
            Assert.Equal(15, TestHostGuard.TotalMinutesOf(new TestHostJob { Kind = TestHostGuard.AddinRunKind, TotalMinutes = 27 }));
            // The other kinds keep their limits
            Assert.Equal(TestHostGuard.DefaultTotalMinutes, TestHostGuard.TotalMinutesOf(new TestHostJob { Kind = TestHostGuard.ReadKind }));
        }

        [Fact]
        public void R372_the_wait_for_the_answer_event_ends_when_the_relay_is_empty_or_at_its_limit()
        {
            Assert.Equal(WaitState.Wait, AddinRunWait.Look(Now, Now.AddSeconds(1), 0, 2, 60));
            Assert.Equal(WaitState.Ready, AddinRunWait.Look(Now, Now.AddSeconds(2), 0, 2, 60));
            Assert.Equal(WaitState.Wait, AddinRunWait.Look(Now, Now.AddSeconds(30), 1, 2, 60));
            Assert.Equal(WaitState.TimedOut, AddinRunWait.Look(Now, Now.AddSeconds(60), 1, 2, 60));
        }

        // The ribbon of the add-in as a run should see it: its tab active, each panel of RibbonLayout drawn inside the cut and each
        // button on the tab. The names stand for those RibbonLayout gives at run time, whatever they are
        private static RunShot Ribbon()
        {
            return new RunShot
            {
                Name = "ribbon", Colours = 90, Blank = false, SameHandle = true, PartRect = new[] { 0, 32, 1922, 183 }, Crop = new[] { 9, 41, 1922, 151 },
                TabFound = true, TabVisible = true, ActiveTab = "Site Adaptation",
                ExpectedPanels = new List<string> { "Setup", "Rules", "Build", "Output" },
                Panels = new List<RunPanel>
                {
                    new RunPanel { Title = "Setup", Bounds = new[] { 10, 40, 300, 180 } }, new RunPanel { Title = "Rules", Bounds = new[] { 300, 40, 400, 180 } },
                    new RunPanel { Title = "Build", Bounds = new[] { 400, 40, 700, 180 } }, new RunPanel { Title = "Output", Bounds = new[] { 700, 40, 900, 180 } },
                },
                ExpectedButtons = new List<string> { "1 Set up site", "9 Report" },
                Buttons = new List<string> { "1 Set up\nsite", "9 Report", "Site Adaptation Rules" },
            };
        }

        // A run as docs/design/09-risks.md and section 6 expect it, every value read
        private static AddinRunRecord Expected()
        {
            const string root = Documents + @"\SiteAdaptation";
            string first = Model;
            string second = AddinRunFiles.Second(Model);
            var record = new AddinRunRecord { Code = "SA5A", WorkRoot = root, SaveFirst = SaveFirst, RibbonTab = "Site Adaptation" };
            record.Documents.Add(new RunDocument { Label = "first.new", Made = RunDocument.Created, Title = "Project1", PathName = "", SessionId = "s1", PathAfterSave = first });
            record.Documents.Add(new RunDocument { Label = "second.new", Made = RunDocument.Created, Title = "Project2", PathName = "", SessionId = "s2", PathAfterSave = second });
            record.Documents.Add(new RunDocument { Label = "first", Made = RunDocument.Opened, Title = "SA5A-TEST-NEW", PathName = first, SessionId = "s3",
                WorkFolder = AddinRunJudge.ExpectedFolder(root, first), WorkFolderRead = true });
            record.Documents.Add(new RunDocument { Label = "second", Made = RunDocument.Opened, Title = "SA5A-SECOND-TEST-NEW", PathName = second, SessionId = "s4" });
            record.Documents.Add(new RunDocument { Label = "central.new", Made = RunDocument.Created, Title = "Project3", PathName = "", SessionId = "s5" });
            record.Documents.Add(new RunDocument { Label = "detached", Made = RunDocument.Detached, Title = "", PathName = "", SessionId = "s6", IsDetached = true,
                WorkFolderRead = true, ButtonStop = SaveFirst, ButtonStopRead = true });
            record.Documents.Add(new RunDocument { Label = "unsaved", Made = RunDocument.Created, Title = "Project4", PathName = "", SessionId = "s7",
                WorkFolderRead = true, ButtonStop = SaveFirst, ButtonStopRead = true });
            record.Steps.Add(new RunStepState { Step = "openFirst", Active = "first", ActiveSession = "s3", PaneSession = "s3", LoadsBefore = 4, LoadsAfter = 6,
                Events = new List<string> { RunEvent.Opened, RunEvent.ViewActivated } });
            record.Steps.Add(new RunStepState { Step = "switchToFirst", Active = "first", ActiveSession = "s3", PaneSession = "s3", LoadsBefore = 8, LoadsAfter = 9,
                Events = new List<string> { RunEvent.ViewActivated } });
            record.Steps.Add(new RunStepState { Step = "closeSecond", Active = "first", ActiveSession = "s3", PaneSession = "s3", LoadsBefore = 9, LoadsAfter = 10,
                Events = new List<string> { RunEvent.Closed } });
            record.Steps.Add(new RunStepState { Step = "secondNew", LoadsBefore = 1, LoadsAfter = 4,
                Events = new List<string> { RunEvent.Created, RunEvent.SavedAs, RunEvent.Closed } });
            record.Answers.Add(new RunAnswer { RunId = "sa5a-1", PostedFor = "second", PostedSession = "s4", ActiveWhenPosted = "first", Routed = true, RoutedSession = "s4", RoutedPath = second });
            record.Answers.Add(new RunAnswer { RunId = "sa5a-2", PostedFor = "second", PostedSession = "s4", ActiveWhenPosted = "first", Routed = true,
                Issue = "run sa5a-2 ended after its document closed, nothing applied" });
            record.Shots.Add(new RunShot { Name = "pane", Width = 380, Height = 900, Colours = 40, Blank = false, Shown = true, MinWidth = 380 });
            record.Shots.Add(Ribbon());
            record.HomeEvent.Raise(Now, 0, true);
            record.PaneShownBefore = true;
            record.HomeEvent.Run(Now.AddSeconds(1), true, 0);
            return record;
        }

        private static RunCheck CheckOf(AddinRunRecord record, string name)
        {
            return record.Checks.Single(check => check.Name == name);
        }

        [WindowsFact]
        public void R372_a_run_that_follows_the_design_holds_every_check_of_open_items_1_and_2()
        {
            AddinRunRecord record = Expected();
            AddinRunJudge.Judge(record);

            // The first show of a hidden pane is the one claim no run measures, a shown pane may be one Revit restored (QL18)
            List<RunCheck> measured = record.Checks.Where(check => check.Name != AddinRunJudge.FirstShow).ToList();
            Assert.All(measured, check => Assert.True(check.Held == true, check.Name + ": " + check.Detail));
            Assert.Null(CheckOf(record, AddinRunJudge.FirstShow).Held);
            Assert.Equal(measured.Count, record.Held);
            Assert.Equal(0, record.NotHeld);
            Assert.Equal(1, record.NotMeasured);
            Assert.Contains(record.Checks, check => check.Item == "1");
            Assert.Contains(record.Checks, check => check.Item == "2");
            Assert.Contains(record.Checks, check => check.Item == "shots");
        }

        [WindowsFact]
        public void R372_each_way_a_run_leaves_the_design_is_a_check_not_held_with_its_detail()
        {
            AddinRunRecord record = Expected();
            record.Documents.Single(document => document.Label == "second").SessionId = "s3";
            record.Steps.Single(step => step.Step == "switchToFirst").PaneSession = "s4";
            record.Steps.Single(step => step.Step == "closeSecond").LoadsAfter = 9;
            record.Answers[0].RoutedSession = "s3";
            record.Answers[1].Issue = null;
            record.Documents.Single(document => document.Label == "detached").Title = "SA5A-CENTRAL-TEST-NEW_detached";
            record.Documents.Single(document => document.Label == "detached").ButtonStop = null;
            record.Documents.Single(document => document.Label == "first").WorkFolder = Documents + @"\SiteAdaptation\SA5A-TEST-NEW_00000000";
            record.Shots[1].Blank = true;
            AddinRunJudge.Judge(record);

            foreach (string name in new[] { AddinRunJudge.SessionsDistinct, AddinRunJudge.PaneShowsActive, AddinRunJudge.LoadOn + RunEvent.Closed, AddinRunJudge.AnswerToOwn,
                AddinRunJudge.AnswerAfterClose, AddinRunJudge.DetachedTitleEmpty, AddinRunJudge.DetachedStop, AddinRunJudge.SavedFolder, AddinRunJudge.RibbonShot })
            {
                Assert.True(CheckOf(record, name).Held == false, name);
                Assert.False(string.IsNullOrEmpty(CheckOf(record, name).Detail), name);
            }
            Assert.True(CheckOf(record, AddinRunJudge.LoadOn + RunEvent.ViewActivated).Held == true);
            Assert.Equal(9, record.NotHeld);
            Assert.Equal(record.Checks.Count, record.Held + record.NotHeld + record.NotMeasured);
        }

        // Run 2 of 5a passed a pane PNG of 160 by 160 px from a pane Revit did not show, and a ribbon PNG with Architecture active. Each
        // such picture, and a ribbon whose panels or buttons are not those RibbonLayout gives, is not held
        [WindowsFact]
        public void R384_a_pane_not_shown_or_narrow_and_a_ribbon_without_the_active_tab_panels_and_buttons_are_not_held()
        {
            var pictures = new List<Tuple<string, Action<AddinRunRecord>>>
            {
                Tuple.Create(AddinRunJudge.PaneShot, (Action<AddinRunRecord>)(run => { run.Shots[0].Shown = false; run.Shots[0].Width = 160; run.Shots[0].Height = 160; })),
                Tuple.Create(AddinRunJudge.PaneShot, (Action<AddinRunRecord>)(run => run.Shots[0].Width = 379)),
                Tuple.Create(AddinRunJudge.PaneShot, (Action<AddinRunRecord>)(run => run.Shots[0].Shown = false)),
                Tuple.Create(AddinRunJudge.RibbonShot, (Action<AddinRunRecord>)(run => run.Shots[1].ActiveTab = "Architecture")),
                Tuple.Create(AddinRunJudge.RibbonShot, (Action<AddinRunRecord>)(run => run.Shots[1].Panels[3].Bounds = null)),
                Tuple.Create(AddinRunJudge.RibbonShot, (Action<AddinRunRecord>)(run => run.Shots[1].Panels[3].Bounds = new[] { 1900, 40, 2100, 180 })),
                Tuple.Create(AddinRunJudge.RibbonShot, (Action<AddinRunRecord>)(run => run.Shots[1].Panels.RemoveAt(1))),
                Tuple.Create(AddinRunJudge.RibbonShot, (Action<AddinRunRecord>)(run => run.Shots[1].Buttons.Remove("9 Report"))),
                Tuple.Create(AddinRunJudge.RibbonShot, (Action<AddinRunRecord>)(run => run.Shots[1].Crop = null)),
            };
            foreach (Tuple<string, Action<AddinRunRecord>> picture in pictures)
            {
                AddinRunRecord record = Expected();
                picture.Item2(record);
                AddinRunJudge.Judge(record);
                Assert.True(CheckOf(record, picture.Item1).Held == false, picture.Item1 + ": " + CheckOf(record, picture.Item1).Detail);
            }

            // A pane whose state, least width, or a tab Revit did not read is not measured
            foreach (Action<AddinRunRecord> unread in new Action<AddinRunRecord>[] { run => run.Shots[0].Shown = null, run => run.Shots[0].MinWidth = null })
            {
                AddinRunRecord record = Expected();
                unread(record);
                AddinRunJudge.Judge(record);
                Assert.Null(CheckOf(record, AddinRunJudge.PaneShot).Held);
            }
            AddinRunRecord noTab = Expected();
            noTab.Shots[1].TabFound = null;
            AddinRunJudge.Judge(noTab);
            Assert.Null(CheckOf(noTab, AddinRunJudge.RibbonShot).Held);
        }

        // Review 1 of PR 97: Revit shows a pane it restores from the last session (QL18), so a pane shown when the job came to it does
        // not tell the add-in showed it. Only a pane still hidden after the first project view says something, that the add-in did not
        [WindowsFact]
        public void R372_the_first_show_of_a_hidden_pane_is_not_measured_when_revit_may_have_restored_the_pane()
        {
            AddinRunRecord shown = Expected();
            AddinRunJudge.Judge(shown);
            RunCheck restored = CheckOf(shown, AddinRunJudge.FirstShow);
            Assert.Null(restored.Held);
            Assert.Contains("restored", restored.Detail);
            Assert.Contains("QL18", restored.Detail);

            AddinRunRecord hidden = Expected();
            hidden.PaneShownBefore = false;
            AddinRunJudge.Judge(hidden);
            Assert.True(CheckOf(hidden, AddinRunJudge.FirstShow).Held == false, CheckOf(hidden, AddinRunJudge.FirstShow).Detail);

            AddinRunRecord unread = Expected();
            unread.PaneShownBefore = null;
            AddinRunJudge.Judge(unread);
            Assert.Null(CheckOf(unread, AddinRunJudge.FirstShow).Held);
            Assert.Equal(unread.Checks.Count, unread.Held + unread.NotHeld + unread.NotMeasured);
        }

        [WindowsFact]
        public void R372_a_value_the_run_could_not_read_is_not_measured_never_held()
        {
            AddinRunRecord record = Expected();
            record.Documents.RemoveAll(document => document.Label == "detached");
            record.Answers.Clear();
            record.Steps.ForEach(step => step.LoadsAfter = null);
            record.Shots.Clear();
            AddinRunJudge.Judge(record);

            foreach (string name in new[] { AddinRunJudge.DetachedNoPath, AddinRunJudge.DetachedStop, AddinRunJudge.AnswerToOwn, AddinRunJudge.AnswerAfterClose,
                AddinRunJudge.LoadOn + RunEvent.Opened, AddinRunJudge.PaneShot })
            {
                Assert.Null(CheckOf(record, name).Held);
            }
            Assert.Equal(0, record.NotHeld);
            Assert.True(record.NotMeasured >= 6);
        }

        // Review 1 of PR 99, Revit run 3: the detached document gave the bare PathName below, the gate counted it as saved and never
        // asked the flow, while a button on it would have run. A document there whose stop was not read is not held, never not measured
        [WindowsFact]
        public void R382_a_detached_or_unsaved_document_whose_stop_the_flow_was_never_asked_is_not_held()
        {
            AddinRunRecord record = Expected();
            RunDocument detached = record.Documents.Single(document => document.Label == "detached");
            detached.PathName = "SA5A-CENTRAL-TEST-NEW_detached.rvt";
            detached.Title = "SA5A-CENTRAL-TEST-NEW_detached";
            detached.WorkFolder = Documents + @"\SiteAdaptation\SA5A-CENTRAL-TEST-NEW_detached_00000000";
            detached.ButtonStop = null;
            detached.ButtonStopRead = false;
            RunDocument unsaved = record.Documents.Single(document => document.Label == "unsaved");
            unsaved.ButtonStop = null;
            unsaved.ButtonStopRead = false;
            AddinRunJudge.Judge(record);

            foreach (string name in new[] { AddinRunJudge.DetachedStop, AddinRunJudge.UnsavedStop })
            {
                Assert.True(CheckOf(record, name).Held == false, name + ": " + CheckOf(record, name).Detail);
                Assert.Contains("the flow was not asked", CheckOf(record, name).Detail);
            }

            AddinRunRecord noText = Expected();
            noText.SaveFirst = null;
            AddinRunJudge.Judge(noText);
            Assert.Null(CheckOf(noText, AddinRunJudge.DetachedStop).Held);
            Assert.Null(CheckOf(noText, AddinRunJudge.UnsavedStop).Held);
        }

        // The second method for the work folder: the hash of the lower case full path, here a value worked out by python hashlib
        [WindowsFact]
        public void R372_the_judge_works_out_the_work_folder_by_its_own_hash_of_the_path()
        {
            Assert.Equal(@"C:\Root\SA5A-TEST-NEW_eb90821f", AddinRunJudge.ExpectedFolder(@"C:\Root", @"C:\Runs\SA5A-TEST-NEW.rvt"));
            // Windows paths ignore case, so the hash does too
            Assert.Equal(@"C:\Root\sa5a-test-new_eb90821f", AddinRunJudge.ExpectedFolder(@"C:\Root", @"c:\runs\sa5a-test-new.RVT"));
            Assert.Null(AddinRunJudge.ExpectedFolder(@"C:\Root", ""));
        }

        [Fact]
        public void R384_the_ribbon_crop_is_the_part_of_the_window_the_ribbon_covers()
        {
            // left, top, right, bottom in screen pixels, the crop as x, y, width, height in the window
            Assert.Equal(new[] { 8, 30, 1904, 160 }, ShotCrop.Within(new[] { -8, -8, 1928, 1048 }, new[] { 0, 22, 1904, 182 }));
            Assert.Equal(new[] { 0, 0, 100, 50 }, ShotCrop.Within(new[] { 100, 100, 500, 500 }, new[] { 50, 80, 200, 150 }));
            Assert.Null(ShotCrop.Within(new[] { 0, 0, 100, 100 }, new[] { 200, 200, 300, 300 }));
            Assert.Null(ShotCrop.Within(new[] { 0, 0, 100, 100 }, null));
        }

        [Fact]
        public void R384_a_blank_capture_is_told_from_a_drawn_one()
        {
            byte[] black = new byte[4 * 100];
            ShotPixels blank = ShotPixels.Of(black);
            Assert.True(blank.Blank);
            Assert.Equal(1, blank.Colours);
            Assert.Equal(1.0, blank.BlackShare);

            byte[] drawn = new byte[4 * 100];
            for (int at = 0; at < 100; at++)
            {
                drawn[4 * at] = (byte)(at % 7 * 30);
                drawn[4 * at + 1] = 200;
                drawn[4 * at + 2] = (byte)(at % 3 * 80);
                drawn[4 * at + 3] = 255;
            }
            ShotPixels seen = ShotPixels.Of(drawn);
            Assert.False(seen.Blank);
            Assert.Equal(21, seen.Colours);
            Assert.Equal(0.0, seen.BlackShare);
        }

        [Fact]
        public void R384_the_shots_come_from_our_own_revit_window_and_the_live_pane_never_the_desktop()
        {
            string shots = RepoText("dotnet", "SiteAdaptation.TestHost", "RevitShots.cs");

            Assert.Contains("PrintWindow(handle, context, RenderFullContent)", shots);
            Assert.Contains("Process.GetCurrentProcess().MainWindowHandle", shots);
            Assert.Contains("new RenderTargetBitmap(", shots);
            Assert.Contains("PresentationSource.CurrentSources", shots);
            Assert.DoesNotContain("CopyFromScreen", shots);
            Assert.DoesNotContain("GetDesktopWindow", shots);
            Assert.DoesNotContain("GetDC(IntPtr.Zero)", shots);
        }

        [Fact]
        public void R384_the_run_script_and_the_session_carry_the_add_in_run()
        {
            string script = RepoText("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            string run = RepoText("dotnet", "SiteAdaptation.TestHost", "AddinRunSession.cs");

            Assert.Contains("'addinRun'", script);
            Assert.Contains("$run = New-AddinRunFolder -Docs $docs -Code $Code -Stamp $stamp", script);
            Assert.Contains("$roundJob.template = $build.template", script);
            Assert.Contains("job.Kind == TestHostGuard.AddinRunKind", session);
            Assert.Contains("new AddinRunSession(", session);
            // The answers go through the relay of the add-in, so the ExternalEvent of the add-in routes them
            Assert.Contains("probe.Post(", run);
            // Run 2: the phases run in the Execute of an ExternalEvent of the test host, never in an Idling handler, where Revit 2024
            // refuses OpenAndActivateDocument
            Assert.Contains("phaseEvent = ExternalEvent.Create(new PhaseHandler(this));", run);
            string tick = Between(run, "private void OnTick(", "private int Pending(");
            string start = Between(run, "public void Start(", "private bool Raise(");
            foreach (string part in new[] { tick, start })
            {
                Assert.DoesNotContain("runner.Run(", part);
                Assert.DoesNotContain("OpenAndActivateDocument", part);
            }
            Assert.Contains("lastOk = runner.Run(Phase(phase));", Between(run, "private void Execute(", "private IEnumerable<JobStep> Phase("));
            // R200. A failed log ends the run script with 1, which the lock wrapper hands on
            Assert.Contains("if (-not $DryStart -and ($stopReason -or $logResult -ne 'ok')) {", script);
            Assert.Contains("DetachFromCentralOption.DetachAndPreserveWorksets", run);
            Assert.Contains("SaveAsCentral = true", run);
            Assert.Contains("OpenAndActivateDocument(", run);
            Assert.Contains("AddinRunJudge.Judge(record)", run);
            // The work folder comes from the pane side of the add-in, the save stop from the flow of R382 on the path Revit gives
            Assert.Contains("state.WorkFolder(entry.PathName)", run);
            Assert.Contains("new ButtonFlow(state, null, null, null, null).Run(1, \"1\", new ButtonBody(), entry.PathName).Message", run);
        }

        // QL20 point 2. No handler of a Revit event is removed anywhere, and each is added in the Subscribe of the session, which the
        // test host runs from OnStartup, never in Start, Execute, OnTick or a handler, where Revit refuses it
        [Fact]
        public void R372_every_revit_handler_is_added_from_on_startup_and_none_is_removed()
        {
            string app = RepoText("dotnet", "SiteAdaptation.TestHost", "TestHostApp.cs");
            string session = RepoText("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");
            string run = RepoText("dotnet", "SiteAdaptation.TestHost", "AddinRunSession.cs");
            var added = new System.Text.RegularExpressions.Regex(@"\.\w+\s*\+=\s*On\w+");

            Assert.Contains("Wake(application, token, jobPath);", Between(app, "public Result OnStartup(", "public Result OnShutdown("));
            Assert.Contains("JobSession.Start(application, token, jobPath, addinsFolder);", app);
            Assert.Contains("session.Subscribe();", Between(session, "public static void Start(", "private static void Refused("));
            string subscribe = Between(session, "private void Subscribe(", "private void Kill(");
            Assert.Contains("addinRun.Subscribe();", subscribe);
            string runSubscribe = Between(run, "public void Subscribe(", "public void Start(");
            Assert.Contains("phaseEvent = ExternalEvent.Create(new PhaseHandler(this));", runSubscribe);
            Assert.Contains("application.Idling += OnTick;", runSubscribe);
            Assert.Contains("application.Idling += OnFirstIdling;", subscribe);

            foreach (string text in new[] { session, run })
            {
                Assert.DoesNotContain("-=", text);
            }
            Assert.Equal(added.Matches(session).Count, added.Matches(subscribe).Count);
            Assert.Equal(added.Matches(run).Count, added.Matches(runSubscribe).Count);
            Assert.Equal(7, added.Matches(runSubscribe).Count);
            Assert.Equal(5, added.Matches(subscribe).Count);
            // One Idling handler per job: the add-in run takes its clock, every other kind the first Idling of the job session
            Assert.Contains("if (addinRun != null)", subscribe);
            Assert.DoesNotContain("ExternalEvent.Create(", run.Replace(runSubscribe, ""));
            Assert.Contains("if (!gate.Live)", Between(run, "private void Seen(", "private string LabelOf("));
            Assert.Contains("if (!gate.Begin())", Between(session, "private void OnFirstIdling(", "private void Exit("));
        }

        // Review 1 of PR 97: PaneMinWidth loads Theme.xaml by reflection, and the wait asks for the width on every Idling tick at
        // SetRaiseWithoutDelay speed, so the session reads it once in Start and the wait and the pane shot take the field
        [Fact]
        public void R372_the_least_pane_width_is_read_once_in_start_and_never_on_a_tick()
        {
            string run = RepoText("dotnet", "SiteAdaptation.TestHost", "AddinRunSession.cs");
            var calls = new System.Text.RegularExpressions.Regex(@"probe\.PaneMinWidth\(\)");

            Assert.Single(calls.Matches(run));
            Assert.Single(calls.Matches(Between(run, "public void Start(", "private bool Raise(")));
            Assert.Contains("paneMinWidth", Between(run, "private int Pending(", "private void Finish("));
        }

        // Revit run 3: the detached document gave a bare PathName, which the run measured as saved and never asked the flow about. The
        // test host asks the flow for every path the rule of R382 does not take as saved, and the judge names no folder for it
        [Fact]
        public void R382_the_test_host_asks_the_flow_for_a_path_that_is_not_saved_and_the_judge_names_no_folder()
        {
            string run = RepoText("dotnet", "SiteAdaptation.TestHost", "AddinRunSession.cs");
            string workFolderOf = Between(run, "private void WorkFolderOf(", "private void OnTick(");
            Assert.Contains("if (!SavedPath.Is(entry.PathName))", workFolderOf);
            Assert.DoesNotContain("IsNullOrEmpty(entry.PathName)", workFolderOf);

            Assert.Null(AddinRunJudge.ExpectedFolder(Documents + @"\SiteAdaptation", "SA5A-CENTRAL-TEST-NEW_detached.rvt"));
            Assert.Null(AddinRunJudge.ExpectedFolder(Documents + @"\SiteAdaptation", @"sub\x.rvt"));
            Assert.NotNull(AddinRunJudge.ExpectedFolder(Documents + @"\SiteAdaptation", Model));
        }

        // The session asks the flow only for a document with no path, where the flow stops before it reads the state, the model or a file
        [Fact]
        public void R382_the_flow_stops_a_document_with_no_path_before_it_reads_anything()
        {
            foreach (string path in new[] { null, "" })
            {
                RunOutcome outcome = new ButtonFlow(null, null, null, null, null).Run(1, "1", new ButtonBody(), path);
                Assert.Equal(RunTexts.SaveFirst, outcome.Message);
                Assert.Equal(SaveFirst, outcome.Message);
            }
        }
    }
}
