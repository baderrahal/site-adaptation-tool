using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitPrototypeReader;
using SiteAdaptation.Common;
using SiteAdaptation.Placer;

namespace SiteAdaptation.TestHost
{
    // One job of R103 and R104, from the claim of the job file to the exit of Revit
    internal sealed class JobSession
    {
        public const string PlacerFolderName = "SiteAdaptationPlacer";
        public const string ReaderFolderName = "SiteAdaptation";
        private const double ExitMinutes = 5;
        private static readonly HashSet<string> RollBackSteps = new HashSet<string>(StringComparer.Ordinal) { "removePrototypeType", "realRun", "deleteFloors", "realRun2", "coordinates", "levels", "floorTypes", "parameters", "image" };

        private readonly UIControlledApplication application;
        private readonly TestHostJob job;
        private readonly string jobPath;
        private readonly string documents;
        private readonly TestHostLog log;
        private readonly TestHostRunner runner;
        private readonly DateTime deadlineUtc;
        private readonly Dictionary<string, LinkOpening> openings = new Dictionary<string, LinkOpening>(StringComparer.OrdinalIgnoreCase);
        private TestHostWatchdog watchdog;
        private Document document;

        private JobSession(UIControlledApplication application, TestHostJob job, string jobPath, string documents, string addinsFolder)
        {
            this.application = application;
            this.job = job;
            this.jobPath = jobPath;
            this.documents = documents;
            string logPath = TestHostGuard.OutputPath(documents, job.Code, TestHostGuard.IsUpgrade(job) ? "upgrade_log" : "log");
            log = new TestHostLog
            {
                Code = job.Code,
                Kind = job.Kind ?? TestHostGuard.PlaceKind,
                Model = job.Model,
                Source = job.Source,
                Target = job.Target,
                PrototypeFile = job.PrototypeFile,
                Plan = job.Plan,
                StartedUtc = DateTime.UtcNow,
                Guard = "job accepted, the job file was moved at the claim and deleted after the check",
            };
            log.Assemblies["testHost"] = typeof(JobSession).Assembly.Location;
            log.Assemblies["placer"] = typeof(PlaceFromPlan).Assembly.Location;
            log.Assemblies["reader"] = typeof(PrototypeReading).Assembly.Location;
            log.Assemblies["common"] = typeof(Common.Placer).Assembly.Location;
            log.Assemblies["addinsFolder"] = addinsFolder;
            runner = new TestHostRunner(log, value => JsonOut.Write(logPath, value), () => DateTime.UtcNow);
            deadlineUtc = log.StartedUtc.AddMinutes(job.TotalMinutes ?? TestHostGuard.DefaultTotalMinutes);
        }

        public static void Start(UIControlledApplication application, string token, string jobPath, string addinsFolder)
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // A path that is not a job file of the SiteAdaptation folder is never touched, not even to write a refusal
            if (!TestHostGuard.ShouldWake(token, jobPath, File.Exists(jobPath)) || TestHostGuard.CodeOfJobPath(jobPath, documents) == null)
            {
                return;
            }
            // The claim comes first. The job file moves to a name of this process and only the moved file is read and checked,
            // so a job written in between is never run unchecked, and no job runs twice
            string claimed = Path.ChangeExtension(jobPath, ".claimed-" + Process.GetCurrentProcess().Id + "-" + DateTime.UtcNow.Ticks + ".json");
            string text;
            try
            {
                File.Move(jobPath, claimed);
                text = File.ReadAllText(claimed);
            }
            catch (Exception failure)
            {
                Refused(jobPath, "the job file could not be claimed: " + failure.Message);
                return;
            }
            GuardResult guard = TestHostGuard.Check(text, token, DateTime.UtcNow, File.Exists);
            string place = guard.Act ? TestHostGuard.CheckJobPath(jobPath, guard.Job, documents) : null;
            if (place != null)
            {
                guard = new GuardResult { Act = false, Reason = place };
            }
            if (guard.Act)
            {
                string copy = TestHostGuard.IsUpgrade(guard.Job) ? FileIdentity.CheckTargetFolder(Path.GetDirectoryName(guard.Job.Target))
                    : guard.Job.Kind == TestHostGuard.NewSiteKind ? FileIdentity.CheckTargetFolder(Path.GetDirectoryName(guard.Job.Model))
                    : FileIdentity.CheckCopy(guard.Job.Model, TestHostGuard.OriginalOf(guard.Job.Model));
                // R204. The source of a site upgrade is a plain file of its own, never a link to the site model it was copied from
                if (copy == null && guard.Job.Kind == TestHostGuard.SiteUpgradeKind)
                {
                    copy = FileIdentity.CheckCopy(guard.Job.Source, null);
                }
                // R117. A work file is a copy of the master and never the master itself. R213 a new site file has no master
                if (copy == null && TestHostGuard.PlacesFloors(guard.Job) && !TestHostGuard.IsNewSite(guard.Job.Model))
                {
                    copy = FileIdentity.CheckCopy(guard.Job.Model, TestHostGuard.MasterOf(guard.Job.Model));
                }
                if (copy != null)
                {
                    guard = new GuardResult { Act = false, Reason = copy };
                }
            }
            if (!guard.Act)
            {
                Refused(jobPath, guard.Reason + ". The job stays in " + Path.GetFileName(claimed) + " and never runs");
                return;
            }
            File.Delete(claimed);
            var session = new JobSession(application, guard.Job, jobPath, documents, addinsFolder);
            session.Subscribe();
        }

        // A refused job leaves its job file alone and writes one line of why, and nothing else happens
        private static void Refused(string jobPath, string reason)
        {
            try
            {
                JsonOut.Write(TestHostGuard.RefusedPath(jobPath),
                    new Dictionary<string, string> { { "utc", DateTime.UtcNow.ToString("o") }, { "reason", reason } });
            }
            catch (Exception)
            {
                // Nothing more can be done in a session the test host does not act in
            }
        }

        private void Subscribe()
        {
            runner.Record(value => value.Notes.Add("Revit " + application.ControlledApplication.VersionBuild + ", the test host is waiting for the first Idling event"));
            watchdog = new TestHostWatchdog(runner, deadlineUtc, () => DateTime.UtcNow, Kill, TimeSpan.FromSeconds(10));
            application.DialogBoxShowing += OnDialog;
            application.ControlledApplication.FailuresProcessing += OnFailures;
            application.ControlledApplication.LinkedResourceOpening += OnLinkOpening;
            application.ControlledApplication.LinkedResourceOpened += OnLinkOpened;
            application.Idling += OnFirstIdling;
        }

        private void Kill(string why)
        {
            Trace.WriteLine("test host: " + why);
            Process.GetCurrentProcess().Kill();
        }

        private void OnFirstIdling(object sender, IdlingEventArgs e)
        {
            application.Idling -= OnFirstIdling;
            var ui = (UIApplication)sender;
            runner.Run(Steps(ui.Application));
            try
            {
                ui.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit));
                runner.Record(value => value.Notes.Add("exit posted, Revit has " + ExitMinutes + " minutes to close before the test host stops it"));
            }
            catch (Exception failure)
            {
                runner.Record(value => value.Notes.Add("exit could not be posted, the watchdog stops Revit: " + failure.Message));
            }
            watchdog.MoveDeadline(DateTime.UtcNow.AddMinutes(ExitMinutes));
        }

        private IEnumerable<JobStep> Steps(Application app)
        {
            var steps = new JobSteps(app, job, documents);
            if (TestHostGuard.IsUpgrade(job))
            {
                // R109. One prototype saved as a Revit 2024 file, the source only read
                yield return new JobStep { Name = "sourceInfo", LimitMinutes = 2, Action = () => steps.FileLine(job.Source) };
                yield return new JobStep { Name = "openSource", LimitMinutes = 25, Action = () => steps.OpenSource(out document) };
                yield return new JobStep { Name = "saveTarget", LimitMinutes = 10, Action = () => steps.SaveTarget(document) };
                yield return new JobStep { Name = "close", LimitMinutes = 5, Always = true, Action = () => steps.Close(document) };
                yield return new JobStep { Name = "targetInfo", LimitMinutes = 2, Action = () => steps.CheckTarget() };
                yield return new JobStep { Name = "jobFile", LimitMinutes = 1, Always = true, Action = () => File.Exists(jobPath) ? "a new job file is waiting, it is left for its own session" : "the job file is gone" };
                yield break;
            }
            if (job.Kind == TestHostGuard.MasterKind || job.Kind == TestHostGuard.FloorsKind || job.Kind == TestHostGuard.LinksAndFloorsKind)
            {
                foreach (JobStep step in FloorJobSteps(steps))
                {
                    yield return step;
                }
                yield break;
            }
            if (job.Kind == TestHostGuard.ReadKind || job.Kind == TestHostGuard.NewSiteKind || job.Kind == TestHostGuard.ImageKind)
            {
                foreach (JobStep step in SiteJobSteps(steps, app))
                {
                    yield return step;
                }
                yield break;
            }
            yield return new JobStep { Name = "unloadLinks", LimitMinutes = 3, Action = steps.UnloadOtherLinks };
            yield return new JobStep { Name = "openModel", LimitMinutes = 20, Action = () => steps.Open(out document) };
            yield return new JobStep { Name = "removePrototypeType", LimitMinutes = 15, Action = () => steps.RemovePrototypeType(document) };
            yield return new JobStep { Name = "dryRun", LimitMinutes = 5, Action = () => steps.Place(document, false) };
            yield return new JobStep { Name = "realRun", LimitMinutes = 20, Action = () => steps.Place(document, true) };
            yield return new JobStep { Name = "saveAfterPlace", LimitMinutes = 15, Action = () => steps.Save(document) };
            yield return new JobStep { Name = "keepOnlyPrototype", LimitMinutes = 5, Action = () => steps.KeepOnlyPrototype(document) };
            yield return new JobStep { Name = "read", LimitMinutes = 15, Action = () => steps.Read(document) };
            yield return new JobStep { Name = "close", LimitMinutes = 5, Always = true, Action = () => steps.Close(document) };
            yield return new JobStep { Name = "jobFile", LimitMinutes = 1, Always = true, Action = () => File.Exists(jobPath) ? "a new job file is waiting, it is left for its own session" : "the job file is gone" };
        }

        // R205 a read opens a test copy and reads it, R212 a newSite job makes the new site file, R214 an image job exports one plan view.
        // None of them saves the model it opened, the newSite job saves only the file it made
        private IEnumerable<JobStep> SiteJobSteps(JobSteps steps, Application app)
        {
            var site = new SiteSteps(app, job, documents);
            if (job.Kind == TestHostGuard.NewSiteKind)
            {
                yield return new JobStep { Name = "create", LimitMinutes = 5, Action = () => site.Create(out document) };
                yield return new JobStep { Name = "coordinates", LimitMinutes = 2, Action = () => site.Coordinates(document) };
                yield return new JobStep { Name = "levels", LimitMinutes = 2, Action = () => site.LevelsByName(document) };
                yield return new JobStep { Name = "floorTypes", LimitMinutes = 2, Action = () => site.FloorTypesByName(document) };
                yield return new JobStep { Name = "parameters", LimitMinutes = 2, Action = () => site.Parameters(document) };
                yield return new JobStep { Name = "saveNew", LimitMinutes = 5, Action = () => site.SaveNew(document) };
            }
            else
            {
                if (job.LoadLinks == true)
                {
                    yield return new JobStep { Name = "loadLinks", LimitMinutes = 3, Action = site.LoadAllLinks };
                }
                yield return new JobStep { Name = "openModel", LimitMinutes = 20, Action = () => steps.Open(out document) };
                if (job.Kind == TestHostGuard.ReadKind)
                {
                    yield return new JobStep { Name = "read", LimitMinutes = 15, Action = () => steps.Read(document) };
                    yield return new JobStep { Name = "rooms", LimitMinutes = 5, Action = () => new HandSteps(job, documents).Rooms(document) };
                }
                else
                {
                    yield return new JobStep { Name = "image", LimitMinutes = TestHostGuard.ImageStepMinutes(job), Action = () => site.Image(document) };
                }
            }
            yield return new JobStep { Name = "close", LimitMinutes = 5, Always = true, Action = () => steps.Close(document) };
            yield return new JobStep { Name = "jobFile", LimitMinutes = 1, Always = true, Action = () => File.Exists(jobPath) ? "a new job file is waiting, it is left for its own session" : "the job file is gone" };
        }

        // R117. The master job leaves every link unloaded and no floor of the five types. A floors job runs the floor step on a work file,
        // and linksAndFloors places the links of one prototype first. Every save deletes the backup it leaves
        private IEnumerable<JobStep> FloorJobSteps(JobSteps steps)
        {
            var floors = new FloorSteps(job, documents);
            bool links = job.Kind == TestHostGuard.LinksAndFloorsKind;
            yield return new JobStep { Name = "unloadLinks", LimitMinutes = 3, Action = steps.UnloadOtherLinks };
            yield return new JobStep { Name = "openModel", LimitMinutes = 20, Action = () => steps.Open(out document) };
            if (links)
            {
                yield return new JobStep { Name = "removePrototypeType", LimitMinutes = 15, Action = () => steps.RemovePrototypeType(document) };
            }
            if (job.DeleteFloors != null)
            {
                yield return new JobStep { Name = "deleteFloors", LimitMinutes = 10, Action = () => floors.DeleteFloors(document) };
            }
            if (job.Kind != TestHostGuard.MasterKind)
            {
                yield return new JobStep { Name = "dryRun", LimitMinutes = 5, Action = () => floors.Place(document, false, "placer_dryrun") };
                if (job.DryRunOnly != true)
                {
                    yield return new JobStep { Name = "realRun", LimitMinutes = 20, Action = () => floors.Place(document, true, "placer_report") };
                }
                if (job.RunTwice == true)
                {
                    yield return new JobStep { Name = "realRun2", LimitMinutes = 10, Action = () => floors.Place(document, true, "placer_report2") };
                }
            }
            yield return new JobStep { Name = "save", LimitMinutes = 15, Action = () => FloorSteps.SaveOver(document, job.Model) };
            if (job.Kind != TestHostGuard.MasterKind)
            {
                if (links)
                {
                    yield return new JobStep { Name = "keepOnlyPrototype", LimitMinutes = 5, Action = () => steps.KeepOnlyPrototype(document) };
                }
                yield return new JobStep { Name = "read", LimitMinutes = 15, Action = () => steps.Read(document) };
            }
            yield return new JobStep { Name = "close", LimitMinutes = 5, Always = true, Action = () => steps.Close(document) };
            yield return new JobStep { Name = "jobFile", LimitMinutes = 1, Always = true, Action = () => File.Exists(jobPath) ? "a new job file is waiting, it is left for its own session" : "the job file is gone" };
        }

        private void OnDialog(object sender, DialogBoxShowingEventArgs e)
        {
            var task = e as TaskDialogShowingEventArgs;
            var box = e as MessageBoxShowingEventArgs;
            string kind = task != null ? "taskDialog" : box != null ? "messageBox" : "dialogBox";
            int? answer = DialogAnswers.Answer(kind, e.DialogId, code => e.OverrideResult(code));
            var entry = new DialogEntry
            {
                Utc = DateTime.UtcNow,
                Kind = kind,
                DialogId = e.DialogId,
                Message = task != null ? task.Message : box != null ? box.Message : null,
                Answer = answer,
                Accepted = answer.HasValue,
                Step = runner.CurrentStep,
            };
            runner.Record(value => value.Dialogs.Add(entry));
        }

        // Warnings are logged and dismissed, an error is logged and rolls its transaction back with no box
        private void OnFailures(object sender, FailuresProcessingEventArgs e)
        {
            FailuresAccessor accessor = e.GetFailuresAccessor();
            IList<FailureMessageAccessor> messages = accessor.GetFailureMessages();
            if (messages.Count == 0)
            {
                return;
            }
            bool error = false;
            var lines = new List<string>();
            foreach (FailureMessageAccessor message in messages)
            {
                FailureSeverity severity = message.GetSeverity();
                lines.Add((runner.CurrentStep ?? "none") + ": " + severity + ": " + message.GetDescriptionText());
                if (severity == FailureSeverity.Warning)
                {
                    accessor.DeleteWarning(message);
                }
                else
                {
                    error = true;
                }
            }
            // Only the transactions of the test host roll back here. An error of Revit itself, in an open or a link load, is left to Revit
            if (error && RollBackSteps.Contains(runner.CurrentStep ?? ""))
            {
                FailureHandlingOptions options = accessor.GetFailureHandlingOptions();
                options.SetClearAfterRollback(true);
                accessor.SetFailureHandlingOptions(options);
                e.SetProcessingResult(FailureProcessingResult.ProceedWithRollBack);
                lines.Add("rolled back");
            }
            else if (error)
            {
                lines.Add("left to Revit, not a transaction of the test host");
            }
            else
            {
                e.SetProcessingResult(FailureProcessingResult.Continue);
            }
            runner.Record(value => value.Failures.AddRange(lines));
        }

        private void OnLinkOpening(object sender, LinkedResourceOpeningEventArgs e)
        {
            var entry = new LinkOpening { Path = e.LinkedResourcePathName, OpeningUtc = DateTime.UtcNow, Step = runner.CurrentStep };
            runner.Record(value =>
            {
                value.LinkOpenings.Add(entry);
                openings[entry.Path ?? ""] = entry;
            });
        }

        private void OnLinkOpened(object sender, LinkedResourceOpenedEventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            runner.Record(value =>
            {
                LinkOpening entry;
                if (!openings.TryGetValue(e.LinkedResourcePathName ?? "", out entry))
                {
                    entry = new LinkOpening { Path = e.LinkedResourcePathName, OpeningUtc = now, Step = runner.CurrentStep };
                    value.LinkOpenings.Add(entry);
                }
                entry.OpenedUtc = now;
                entry.Seconds = Math.Round((now - entry.OpeningUtc).TotalSeconds, 1);
                openings.Remove(e.LinkedResourcePathName ?? "");
            });
        }
    }
}
