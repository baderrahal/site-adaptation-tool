using System;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Review;

namespace SiteAdaptation.AddIn.ReviewData
{
    // R406. The body of button 3, ReviewDataRun of Common with the window and the zoom of the Revit side. Like SetUpSiteBody it names no
    // Revit type, so the tests of the body load none. App sets Dialog at start, the Revit side sets Zoom, and Load gives the rows from
    // the files of button 2. While one of them is null button 3 is not built (Bodies.Of in Buttons.cs wires it)
    public static class ReviewDataBody
    {
        // The window for its owner, the Revit main window
        public static Func<IntPtr, IReviewDialog> Dialog { get; set; }

        // The rows, the checks kept and the hash of the plot table of the read, from the work folder of the run
        public static Func<RunContext, ReviewInput> Load { get; set; }

        // The NBH of the scope of the document of the run, set by the entry of the button before it makes the body
        public static Func<string> Nbh { get; set; }

        // Zoom to plot in the active view of Revit, set by the Revit side
        public static Action<ReviewRow> Zoom { get; set; }

        public static bool IsSet => Dialog != null && Load != null;

        public static ButtonBody For(IntPtr mainWindow)
        {
            return new ReviewDataRun(Load, Dialog(mainWindow), Zoom).Body();
        }
    }
}
