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
        // The pattern of a file dialog, no text of the window (NEWPROJ, no window names a kind of file)
        private const string PositionFilter = "*|*.json";

        private readonly IntPtr owner;

        public SetUpSiteDialog(IntPtr owner)
        {
            this.owner = owner;
        }

        public SetUpSiteAnswer Show(SetUpSiteView view, SetUpSiteCallbacks callbacks)
        {
            var session = new SetUpSiteSession(view, callbacks, PickSheet, PickPosition);
            var window = new SetUpSiteWindow(session);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            session.Finished += () => window.Close();
            window.ShowDialog();
            return session.Answer;
        }

        // R513. A dialog of its own, so the user sees which file it asks for
        private static string PickPosition()
        {
            var dialog = new OpenFileDialog { CheckFileExists = true, Title = SetUpSiteText.PickPositionTitle, Filter = PositionFilter };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static string PickSheet()
        {
            var dialog = new OpenFileDialog { CheckFileExists = true };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
