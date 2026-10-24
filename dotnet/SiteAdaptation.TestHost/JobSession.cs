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
        private static readonly HashSet<string> RollBackSteps = new HashSet<string>(StringComparer.Ordinal) { "removePrototypeType", "realRun" };

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
            string logPath = TestHostGuard.OutputPath(documents, job.Code, job.Kind == TestHostGuard.UpgradeKind ? "upgrade_log" : "log");
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
            if (!TestHostGuard.ShouldWake(token, File.Exists(jobPath)) || jobPath != TestHostGuard.JobPath(documents))
            {
                return;
            }
            // The claim comes first. The job file moves to a name of this process and only the moved file is read and checked,
            // so a job written in between is never run unchecked, and no job runs twice
            string claimed = Path.Combine(TestHostGuard.SiteAdaptationFolder(documents), "testhost_job.claimed-" + Process.GetCurrentProcess().Id + "-" + DateTime.UtcNow.Ticks + ".json");
            string text;
            try
            {
                File.Move(jobPath, claimed);
                text = File.ReadAllText(claimed);
            }
            catch (Exception failure)
            {
                Refused(documents, "the job file could not be claimed: " + failure.Message);
                return;
            }
            GuardResult guard = TestHostGuard.Check(text, token, DateTime.UtcNow, File.Exists);
            if (guard.Act)
            {
                string copy = guard.Job.Kind == TestHostGuard.UpgradeKind
                    ? FileIdentity.CheckTargetFolder(Path.GetDirectoryName(guard.Job.Target))
                    : FileIdentity.CheckCopy(guard.Job.Model, TestHostGuard.OriginalOf(guard.Job.Model));
                if (copy != null)
                {
                    guard = new GuardResult { Act = false, Reason = copy };
                }
            }
            if (!guard.Act)
            {
                Refused(documents, guard.Reason + ". The job stays in " + Path.GetFileName(claimed) + " and never runs");
                return;
            }
            File.Delete(claimed);
            var session = new JobSession(application, guard.Job, jobPath, documents, addinsFolder);
            session.Subscribe();
        }

        // A refused job leaves its job file alone and writes one line of why, and nothing else happens
        private static void Refused(string documents, string reason)
        {
            try
            {
                JsonOut.Write(Path.Combine(TestHostGuard.SiteAdaptationFolder(documents), "testhost_refused.json"),
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
            if (job.Kind == TestHostGuard.UpgradeKind)
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
