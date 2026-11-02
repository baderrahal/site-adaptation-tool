using System;
using System.Collections.Generic;
using System.Windows.Interop;

namespace SiteAdaptation.AddIn.NewProject
{
    // R492. The window Road surfaces: modal, its owner the Revit main window. Show returns the ticked stems in their order, null on Cancel
    // or a close of the window. It opens no transaction and writes nothing
    public static class RoadSurfacesDialog
    {
        public static IReadOnlyList<string> Show(IntPtr owner, IReadOnlyList<string> inFolder, IReadOnlyList<string> effective)
        {
            var model = new RoadSurfacesViewModel(inFolder, effective);
            var window = new RoadSurfacesWindow(model);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            model.Finished += () => window.Close();
            window.ShowDialog();
            return model.Result;
        }
    }
}
