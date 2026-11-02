using System;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/07-mock.md 7.2. Revit takes VisibleByDefault only the first time a pane is ever loaded and after that keeps the
    // state the last session closed with (RevitAPIUI.xml of Revit 2024), so a pane hidden once stayed hidden and the user saw no pane.
    // The add-in shows it once each session, at the first view of a project, when Revit has it hidden. No Revit type, so a test runs it
    public sealed class PaneFirstShow
    {
        private bool done;

        // R494. A document that opens or is created arms the show again. Bader found the pane did not return after Close to Home and
        // a reopen in one session, and the user who hid it and opens another project wants it back
        public void Rearm()
        {
            done = false;
        }

        // R494. The button Show pane. Null when Revit showed the pane, else the line to tell. show is true when Revit showed it and
        // false when it refused, the caller catches the ApplicationException of Revit
        public static string ByHand(Func<bool> show)
        {
            return show() ? null : PaneText.ShowRefused;
        }

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
