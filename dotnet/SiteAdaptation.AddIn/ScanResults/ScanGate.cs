using System;
using System.IO;
using Microsoft.Win32;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.ScanResults
{
    // R446, Q308. Export to Excel of the window What the tool read. R847: the gate of the scan before the old read of step 2 went with
    // that read, the export stays for the window New project opens
    public static class ScanGate
    {
        // Export to Excel asks where, opening in the work folder of the scan (R446, Q308), never over a file (ScanPicks.Export refuses
        // one). scan.json lies in scan_<n>_<time> of the work folder
        public static Func<string> PickExportFor(string scanJsonPath)
        {
            return () => PickExport(WorkFolderOf(scanJsonPath));
        }

        public static string WorkFolderOf(string scanJsonPath)
        {
            return Path.GetDirectoryName(Path.GetDirectoryName(scanJsonPath));
        }

        // The open window of What the tool read, so its Save As blocks it and a second Export opens no second dialog. Revit has no
        // WPF application to give the dialog an owner by itself
        internal static System.Windows.Window ExportOwner { get; set; }

        private static string PickExport(string workFolder)
        {
            var dialog = new SaveFileDialog { InitialDirectory = workFolder, DefaultExt = TemplateBook.Extension, AddExtension = true, OverwritePrompt = false, FileName = ScanResultsText.Title + TemplateBook.Extension };
            return dialog.ShowDialog(ExportOwner) == true ? dialog.FileName : null;
        }
    }
}
