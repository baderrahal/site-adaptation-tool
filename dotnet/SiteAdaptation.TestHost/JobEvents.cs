using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI.Events;
using SiteAdaptation.Common;

namespace SiteAdaptation.TestHost
{
    // R104. What Revit asks while a job runs, answered and logged for the runner of that job. JobSession holds one for its one job, the listener of R448 one per job
    internal sealed class JobEvents
    {
        private static readonly HashSet<string> RollBackSteps = new HashSet<string>(StringComparer.Ordinal) { "removePrototypeType", "realRun", "deleteFloors", "realRun2", "coordinates", "levels", "floorTypes", "parameters", "image" };

        private readonly TestHostRunner runner;
        private readonly Dictionary<string, LinkOpening> openings = new Dictionary<string, LinkOpening>(StringComparer.OrdinalIgnoreCase);

        public JobEvents(TestHostRunner runner)
        {
            this.runner = runner;
        }

        public void OnDialog(object sender, DialogBoxShowingEventArgs e)
        {
            var task = e as TaskDialogShowingEventArgs;
            var box = e as MessageBoxShowingEventArgs;
            string kind = task != null ? "taskDialog" : box != null ? "messageBox" : "dialogBox";
            string message = task != null ? task.Message : box != null ? box.Message : null;
            int? answer = DialogAnswers.Answer(kind, e.DialogId, message, code => e.OverrideResult(code));
            var entry = new DialogEntry
            {
                Utc = DateTime.UtcNow,
                Kind = kind,
                DialogId = e.DialogId,
                Message = message,
                Answer = answer,
                Accepted = answer.HasValue,
                Step = runner.CurrentStep,
            };
            runner.Record(value => value.Dialogs.Add(entry));
        }

        // Warnings are logged and dismissed, an error is logged and rolls its transaction back with no box
        public void OnFailures(object sender, FailuresProcessingEventArgs e)
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

        public void OnLinkOpening(object sender, LinkedResourceOpeningEventArgs e)
        {
            var entry = new LinkOpening { Path = e.LinkedResourcePathName, OpeningUtc = DateTime.UtcNow, Step = runner.CurrentStep };
            runner.Record(value =>
            {
                value.LinkOpenings.Add(entry);
                openings[entry.Path ?? ""] = entry;
            });
        }

        public void OnLinkOpened(object sender, LinkedResourceOpenedEventArgs e)
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
