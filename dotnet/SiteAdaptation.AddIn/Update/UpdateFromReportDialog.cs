using System;
using System.Windows.Interop;
using Microsoft.Win32;

namespace SiteAdaptation.AddIn.Update
{
    // R402. The window of Update from report in Revit: modal, its owner the Revit main window. Show returns when Apply or Cancel ends it, or the
    // window is closed, which is Cancel
    public sealed class UpdateFromReportDialog : IUpdateFromReportDialog
    {
        private readonly IntPtr owner;

        public UpdateFromReportDialog(IntPtr owner)
        {
            this.owner = owner;
        }

        public UpdateFromReportAnswer Show(UpdateFromReportCallbacks callbacks)
        {
            var session = new UpdateFromReportSession(callbacks, PickExcel);
            var window = new UpdateFromReportWindow(session);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            session.Finished += () => window.Close();
            window.ShowDialog();
            return session.Answer;
        }

        private static string PickExcel()
        {
            var dialog = new OpenFileDialog { CheckFileExists = true, Filter = "*.xlsx|*.xlsx" };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
