using System;
using System.Windows.Interop;
using Microsoft.Win32;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.AddIn.SetUpSite
{
    // R373, 7.3 of docs/design/07-mock.md. The window of button 1 in Revit: modal, its owner the Revit main window. Show returns
    // when Apply or Cancel ends it, or the window is closed, which is Cancel
    public sealed class SetUpSiteDialog : ISetUpSiteDialog
    {
        private readonly IntPtr owner;

        public SetUpSiteDialog(IntPtr owner)
        {
            this.owner = owner;
        }

        public SetUpSiteAnswer Show(SetUpSiteView view, SetUpSiteCallbacks callbacks)
        {
            var session = new SetUpSiteSession(view, callbacks, PickSheet);
            var window = new SetUpSiteWindow(session);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            session.Finished += () => window.Close();
            window.ShowDialog();
            return session.Answer;
        }

        private static string PickSheet()
        {
            var dialog = new OpenFileDialog { CheckFileExists = true };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
