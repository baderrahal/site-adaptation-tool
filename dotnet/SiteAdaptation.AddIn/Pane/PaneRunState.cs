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
            string workRoot = root();
            string folder = Pane.WorkFolder.For(workRoot, documentPath);
            string configFile = model.Settings == null ? null : model.Settings.ConfigFile;
            string refused = folder == null ? null : Pane.WorkFolder.Stop(workRoot, documentPath, Pane.WorkFolder.InputRoot(configFile, readText));
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

        // CHOSEN for 5a: every button reads the Settings file only. 5c gives each button the inputs it reads by 03a and 03b
        public IReadOnlyList<RunInput> Inputs(int button)
        {
            string configFile = model.Settings == null ? null : model.Settings.ConfigFile;
            return new[] { new RunInput(PaneText.Settings, configFile, false) };
        }

        // A run whose output move failed leaves its step not Done (03a Steps)
        public void Committed(int button, RunRecord record)
        {
            if (!record.StepDone)
            {
                return;
            }
            model.Committed(button, record.Issues.Count == 0 ? new StepStatus(StepKind.Done) : new StepStatus(StepKind.Issues, record.Issues.Count));
        }
    }
}
