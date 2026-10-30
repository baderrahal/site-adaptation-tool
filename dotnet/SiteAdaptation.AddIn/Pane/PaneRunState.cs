using System;
using System.Collections.Generic;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, R382. What the flow of a button asks of the pane side, read from the pane of the active document, the one a button runs
    // on. No Revit type, so a test fills it without Revit
    public sealed class PaneRunState : IRunState
    {
        private readonly PaneViewModel model;
        private readonly Func<string> root;
        private readonly Func<string, string> readText;

        // root gives the work root of 06-settings when asked, so a machine file written while Revit runs is read
        public PaneRunState(PaneViewModel model, Func<string> root, Func<string, string> readText)
        {
            this.model = model;
            this.root = root;
            this.readText = readText;
        }

        // The flow stops on a document with no path before it asks. A work folder inside inputRoot is refused (R1), and the flow of
        // PR 89 has no stop for that, so the refusal is thrown with its text
        public string WorkFolder(string documentPath)
        {
            string folder = Pane.WorkFolder.For(root(), documentPath);
            string configFile = model.Settings == null ? null : model.Settings.ConfigFile;
            string refused = folder == null ? null : Pane.WorkFolder.Refusal(folder, Pane.WorkFolder.InputRoot(configFile, readText));
            if (refused != null)
            {
                throw new InvalidOperationException(refused);
            }
            return folder;
        }

        public bool DryRunFirst
        {
            get { return model.DryRun; }
        }

        public int MissingStep(int button)
        {
            StepStatus status = model.Statuses[button - 1];
            return status.Kind == StepKind.NeedsStep ? status.Value : 0;
        }

        // CHOSEN for 5a: every button reads the Settings file. 5c gives button 2 the input files of the pane it reads (R375), the
        // other buttons get theirs as they are built
        public IReadOnlyList<RunInput> Inputs(int button)
        {
            string configFile = model.Settings == null ? null : model.Settings.ConfigFile;
            var inputs = new List<RunInput> { new RunInput(PaneText.Settings, configFile, false) };
            if (button == 2)
            {
                inputs.AddRange(ReadDataBody.Inputs(model.Settings ?? new PaneSettings()));
            }
            return inputs;
        }

        // A run whose output move failed leaves its step not Done (03a Steps)
        public void Committed(int button, RunRecord record)
        {
            StepStatus status = StatusOf(record);
            if (status != null)
            {
                model.Committed(button, status);
            }
        }

        // R375. The end of a worker run on a document that is not the active one is kept with its session, which the pane shows
        // when that document is active again
        public static void Record(DocumentSession session, int button, RunRecord record)
        {
            StepStatus status = StatusOf(record);
            if (session != null && status != null)
            {
                session.Done[button] = status;
            }
        }

        private static StepStatus StatusOf(RunRecord record)
        {
            if (!record.StepDone)
            {
                return null;
            }
            return record.Issues.Count == 0 ? new StepStatus(StepKind.Done) : new StepStatus(StepKind.Issues, record.Issues.Count);
        }
    }
}
