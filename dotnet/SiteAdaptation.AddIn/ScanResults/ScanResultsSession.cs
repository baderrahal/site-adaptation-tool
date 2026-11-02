using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.ScanResults
{
    // R436. What the buttons of the window What the tool read do, apart from the window, so a test drives them with no click. Use these
    // picks lays the picks over the settings and ends the window, Cancel ends it with no answer. Nothing here writes a model or a settings
    // file, the settings reach the model with the next real run (R367)
    public sealed class ScanResultsSession
    {
        private readonly ProjectSettings settings;
        private readonly Func<string> pickExportPath;

        public ScanResultsSession(ScanViewData data, ProjectSettings settings, Func<string> pickExportPath)
        {
            Model = new ScanResultsViewModel(data);
            this.settings = settings ?? new ProjectSettings();
            this.pickExportPath = pickExportPath;
        }

        public ScanResultsViewModel Model { get; }

        // The new settings after Use these picks, null after Cancel or a close of the window
        public ProjectSettings Answer { get; private set; }

        // Raised when Use these picks or Cancel ends the window
        public event Action Finished;

        public void UseThesePicks()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ScanValueViewModel value in Model.Values)
            {
                if (value.Touched && value.SelectedRaw != null)
                {
                    values[value.Kind] = value.SelectedRaw;
                }
            }
            var conflicts = new Dictionary<string, string>(StringComparer.Ordinal);
            // Review of SCAN-UI: Use these picks takes the choice each conflict shows, the proposed where the user changed nothing, so
            // the next press of button 2 does not open the window again on conflicts the user saw and left
            foreach (ScanConflictViewModel conflict in Model.AllConflicts)
            {
                conflicts[conflict.Key] = conflict.Selected.Source;
            }
            Answer = ScanPicks.Apply(values, conflicts, settings);
            Finished?.Invoke();
        }

        public void Cancel()
        {
            Answer = null;
            Finished?.Invoke();
        }

        public void Export()
        {
            string path = pickExportPath?.Invoke();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            // R446, Q308. The rule of the tool, nothing is written where the inputs lie (R1)
            if (InsideInputs(Path.GetDirectoryName(Path.GetFullPath(path))))
            {
                Model.FootLine = ScanResultsText.NotInInputs;
                return;
            }
            try
            {
                ScanPicks.Export(path, Model.Data);
                Model.FootLine = ScanView.Fill(ScanResultsText.Saved, Path.GetFileName(path));
            }
            catch (Exception error)
            {
                Model.FootLine = error.Message;
            }
        }

        // The input root of the config and the folder of each input picked, which may lie outside that root
        private bool InsideInputs(string folder)
        {
            var places = new List<string> { Pane.WorkFolder.InputRoot(settings?.ConfigFile, Pane.WorkFolder.ReadIfThere) };
            if (settings != null)
            {
                foreach (SettingsInput input in settings.Inputs.Values)
                {
                    if (!string.IsNullOrEmpty(input?.Path))
                    {
                        places.Add(Directory.Exists(input.Path) ? input.Path : Path.GetDirectoryName(input.Path));
                    }
                }
            }
            return places.Any(place => !string.IsNullOrEmpty(place) && Pane.WorkFolder.Inside(folder, place));
        }

        public void UseProposedForAll()
        {
            Model.UseProposedForAll();
        }

        public void ShowAll()
        {
            Model.ShowAll();
        }
    }
}
