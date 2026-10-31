using System;
using System.IO;
using System.Windows.Interop;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.ScanResults
{
    // Lane C SCAN-UI. What the tool read in Revit: modal, its owner the Revit main window. Show returns the settings Use these picks gave,
    // null on Cancel or a close of the window. It opens no transaction and writes no file but the workbook the user names in Export
    public static class ScanResultsDialog
    {
        public static ProjectSettings Show(IntPtr owner, string scanJsonPath, ProjectSettings settings, Func<string> pickExportPath)
        {
            ScanViewData data = ScanView.Read(File.ReadAllText(scanJsonPath), settings);
            var session = new ScanResultsSession(data, settings, pickExportPath);
            var window = new ScanResultsWindow(session);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            session.Finished += () => window.Close();
            window.ShowDialog();
            return session.Answer;
        }
    }
}
