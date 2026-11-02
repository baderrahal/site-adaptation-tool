using System;
using System.Collections.Generic;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;

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

        // R446. Called by Buttons.Run before the flow, for the ribbon and the pane. A step that still waits runs nothing, so it unticks nothing
        public void BeforeRun(int button)
        {
            if (MissingStep(button) == 0)
            {
                model.UntickDryRunFor(button);
            }
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
            return InputsOf(model.Settings ?? new PaneSettings(), button);
        }

        // The inputs of a button by the settings of the pane, the ones the flow hashes for its run and the pane for the step state
        public static IReadOnlyList<RunInput> InputsOf(PaneSettings settings, int button)
        {
            var inputs = new List<RunInput> { new RunInput(PaneText.Settings, settings.ConfigFile, false) };
            if (button == 2)
            {
                inputs.AddRange(ReadDataBody.Inputs(settings));
            }
            if (button == 4)
            {
                inputs.AddRange(CheckTable.CheckTableBody.Inputs(settings));
            }
            return inputs;
        }

        // R457. The Neighbourhood list of Scope from the latest read of the active document. A work folder refused or a table that does
        // not read leaves the list empty, step 2 names the fault, so the pane still opens
        public void RefreshNbhCodes()
        {
            IReadOnlyList<string> codes = new string[0];
            DocumentSession document = model.Document;
            if (document != null && document.HasPath)
            {
                try
                {
                    codes = Common.Review.ReviewLoad.LatestNbhCodes(WorkFolder(document.Path));
                }
                catch (Exception failure) when (failure is InvalidOperationException || failure is System.IO.IOException || failure is UnauthorizedAccessException || failure is Newtonsoft.Json.JsonException)
                {
                    codes = new string[0];
                }
            }
            model.SetNbhCodes(codes);
        }

        // A run whose output move failed leaves its step not Done (03a Steps)
        public void Committed(int button, RunRecord record)
        {
            if (button == 2)
            {
                RefreshNbhCodes();
            }
            StepRun run = model.Document == null ? null : RunOf(model.Document, record);
            if (run != null)
            {
                model.Committed(button, run);
            }
        }

        // R375. The end of a worker run on a document that is not the active one is kept with its session, which the pane shows
        // when that document is active again
        public static void Record(DocumentSession session, int button, RunRecord record)
        {
            StepRun run = session == null ? null : RunOf(session, record);
            if (run != null)
            {
                session.Runs[button] = run;
            }
        }

        // R366. The run as StepStates reads it: now, the one hash over the inputs the flow hashed, the scope of the pane at the end
        // and every output the record lists with its SHA-256
        private static StepRun RunOf(DocumentSession session, RunRecord record)
        {
            if (!record.StepDone)
            {
                return null;
            }
            SettingsScope scope = session.Settings.Scope;
            var copy = new SettingsScope { Kind = scope.Kind, Nbh = scope.Nbh, Plots = scope.Plots };
            var hashes = record.InputHashes ?? new Dictionary<string, string>();
            return new StepRun(DateTime.UtcNow, StepHash.Combine(hashes), copy, record.Issues.Count, record.Outputs);
        }
    }
}
