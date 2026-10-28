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
                ["totalMinutes"] = 27,
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
        public void R104_a_failed_phase_goes_straight_to_the_last_phase_of_the_steps_that_always_run()
        {
            var phases = new AddinRunPhases(4);
            Assert.Equal(0, phases.Next(true));
            Assert.Equal(1, phases.Next(true));
            Assert.Equal(3, phases.Next(false));
            Assert.True(phases.Failed);
            Assert.Equal(-1, phases.Next(true));

            var clean = new AddinRunPhases(2);
            Assert.Equal(0, clean.Next(true));
            Assert.Equal(1, clean.Next(true));
            Assert.Equal(-1, clean.Next(true));
            Assert.False(clean.Failed);
        }

        [Fact]
        public void R372_the_wait_for_the_answer_event_ends_when_the_relay_is_empty_or_at_its_limit()
        {
            Assert.Equal(WaitState.Wait, AddinRunWait.Look(Now, Now.AddSeconds(1), 0, 2, 60));
            Assert.Equal(WaitState.Ready, AddinRunWait.Look(Now, Now.AddSeconds(2), 0, 2, 60));
            Assert.Equal(WaitState.Wait, AddinRunWait.Look(Now, Now.AddSeconds(30), 1, 2, 60));
            Assert.Equal(WaitState.TimedOut, AddinRunWait.Look(Now, Now.AddSeconds(60), 1, 2, 60));
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
            record.Steps.Add(new RunStepState { Step = "openFirst", Active = "first", ActiveSession = "s3", PaneSession = "s3", GenerationBefore = 4, GenerationAfter = 6,
                Events = new List<string> { RunEvent.Opened, RunEvent.ViewActivated } });
            record.Steps.Add(new RunStepState { Step = "switchToFirst", Active = "first", ActiveSession = "s3", PaneSession = "s3", GenerationBefore = 8, GenerationAfter = 9,
                Events = new List<string> { RunEvent.ViewActivated } });
            record.Steps.Add(new RunStepState { Step = "closeSecond", Active = "first", ActiveSession = "s3", PaneSession = "s3", GenerationBefore = 9, GenerationAfter = 10,
                Events = new List<string> { RunEvent.Closed } });
            record.Steps.Add(new RunStepState { Step = "secondNew", GenerationBefore = 1, GenerationAfter = 3, Events = new List<string> { RunEvent.Created, RunEvent.Closed } });
            record.Answers.Add(new RunAnswer { RunId = "sa5a-1", PostedFor = "second", PostedSession = "s4", ActiveWhenPosted = "first", Routed = true, RoutedSession = "s4", RoutedPath = second });
            record.Answers.Add(new RunAnswer { RunId = "sa5a-2", PostedFor = "second", PostedSession = "s4", ActiveWhenPosted = "first", Routed = true,
                Issue = "run sa5a-2 ended after its document closed, nothing applied" });
            record.Shots.Add(new RunShot { Name = "pane", Colours = 40, Blank = false });
            record.Shots.Add(new RunShot { Name = "ribbon", Colours = 90, Blank = false, SameHandle = true });
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

            Assert.All(record.Checks, check => Assert.True(check.Held == true, check.Name + ": " + check.Detail));
            Assert.Equal(record.Checks.Count, record.Held);
            Assert.Equal(0, record.NotHeld);
            Assert.Equal(0, record.NotMeasured);
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
            record.Steps.Single(step => step.Step == "closeSecond").GenerationAfter = 9;
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

        [WindowsFact]
        public void R372_a_value_the_run_could_not_read_is_not_measured_never_held()
        {
            AddinRunRecord record = Expected();
            record.Documents.RemoveAll(document => document.Label == "detached");
            record.Answers.Clear();
            record.Steps.ForEach(step => step.GenerationAfter = null);
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
            Assert.Contains("DetachFromCentralOption.DetachAndPreserveWorksets", run);
            Assert.Contains("SaveAsCentral = true", run);
            Assert.Contains("OpenAndActivateDocument(", run);
            Assert.Contains("AddinRunJudge.Judge(record)", run);
            // The work folder comes from the pane side of the add-in, the save stop from the flow of R382 on the path Revit gives
            Assert.Contains("state.WorkFolder(entry.PathName)", run);
            Assert.Contains("new ButtonFlow(state, null, null, null, null).Run(1, \"1\", new ButtonBody(), entry.PathName).Message", run);
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
