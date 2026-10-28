using System;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/07-mock.md 7.2. Revit takes VisibleByDefault only the first time a pane is ever loaded and after that keeps the
    // state the last session closed with (RevitAPIUI.xml of Revit 2024), so a pane hidden once stayed hidden and the user saw no pane.
    // The add-in shows it once each session, at the first view of a project, when Revit has it hidden. No Revit type, so a test runs it
    public sealed class PaneFirstShow
    {
        private bool done;

        // True when the pane is to be shown now: the first project view of the session and the pane not shown. isShown is asked only
        // then
        public bool Due(bool projectView, Func<bool> isShown)
        {
            if (done || !projectView)
            {
                return false;
            }
            done = true;
            return !isShown();
        }
    }
}
