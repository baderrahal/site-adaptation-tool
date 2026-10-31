using System;
using System.Collections.Generic;
using System.IO;
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
            foreach (ScanConflictViewModel conflict in Model.AllConflicts)
            {
                if (conflict.Picked)
                {
                    conflicts[conflict.Key] = conflict.Selected.Source;
                }
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
