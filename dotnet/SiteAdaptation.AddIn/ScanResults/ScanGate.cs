using System;
using System.Globalization;
using Microsoft.Win32;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.ScanResults
{
    // R438, Q246. The gate of button 2 after the scan: every value clear and no conflict open gives one pane line and the read goes on,
    // else the window What the tool read opens first. Use these picks lays the picks into the settings of the pane, which the next real
    // run writes to the model (R367), and the read goes on. Cancel stops the read
    public static class ScanGate
    {
        // The window of the gate, a stand-in in the tests and the test host, so a run is driven with no click
        public static Func<IntPtr, string, ProjectSettings, ProjectSettings> Window { get; set; } = (owner, path, settings) => ScanResultsDialog.Show(owner, path, settings, PickExport);

        public static ScanGateAnswer Ask(string scanJsonPath, PaneSettings settings, IntPtr owner, Func<string, string> readText)
        {
            ScanViewData view = ScanView.Read(readText(scanJsonPath), settings.Project);
            if (!view.NeedsWindow)
            {
                return new ScanGateAnswer { GoOn = true, Line = string.Format(CultureInfo.InvariantCulture, ScanStep.ClearLine, view.FilesRead, view.PlotsFound) };
            }
            ProjectSettings next = Window(owner, scanJsonPath, settings.Project);
            if (next == null)
            {
                return new ScanGateAnswer { GoOn = false };
            }
            settings.Project.ScanPicks = next.ScanPicks;
            settings.Project.ConflictPicks = next.ConflictPicks;
            return new ScanGateAnswer { GoOn = true, Line = ScanResultsText.PicksSaved };
        }

        // Export to Excel asks where, the work folder first, never over a file (ScanPicks.Export refuses one)
        public static string PickExport()
        {
            var dialog = new SaveFileDialog { DefaultExt = TemplateBook.Extension, AddExtension = true, OverwritePrompt = false, FileName = ScanResultsText.Title + TemplateBook.Extension };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
