using System;
using System.Windows.Interop;

namespace SiteAdaptation.AddIn.HowItWorks
{
    // Lane C NEWPROJ-UI. How it works in Revit: modal, its owner the Revit main window. Show returns true when the user pressed Open New project
    public static class HowItWorksDialog
    {
        public static bool Show(IntPtr owner, bool canOpenNew)
        {
            var model = new HowItWorksViewModel(canOpenNew);
            var window = new HowItWorksWindow(model);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            model.Finished += () => window.Close();
            window.ShowDialog();
            return model.OpenNewProject;
        }
    }
}
