using System;
using System.Globalization;

namespace SiteAdaptation.Common.Flow
{
    // R444, Lane C DEMO-FIX. When the loader of a button stands and what its clock reads. It stands only after ShowAfter and only while
    // the main window of Revit, its hang ghost or the loader itself is in front, so the window of a step or a TaskDialog is never covered
    public static class LoaderRule
    {
        public static readonly TimeSpan ShowAfter = TimeSpan.FromSeconds(2);

        // The class Windows gives the frame it draws over a window that stopped answering
        public const string GhostClass = "Ghost";

        public static bool Shown(TimeSpan elapsed, bool mainInFront)
        {
            return elapsed >= ShowAfter && mainInFront;
        }

        public static bool MainInFront(IntPtr front, IntPtr main, IntPtr loader, string frontClass)
        {
            if (front == IntPtr.Zero || main == IntPtr.Zero)
            {
                return false;
            }
            return front == main || (loader != IntPtr.Zero && front == loader) || string.Equals(frontClass, GhostClass, StringComparison.Ordinal);
        }

        // Minutes and seconds, the minutes as many as it takes
        public static string Clock(TimeSpan elapsed)
        {
            long seconds = Math.Max(0, (long)elapsed.TotalSeconds);
            return (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
