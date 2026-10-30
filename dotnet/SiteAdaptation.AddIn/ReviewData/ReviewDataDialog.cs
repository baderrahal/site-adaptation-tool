using System;
using System.Windows.Interop;
using SiteAdaptation.Common.Review;

namespace SiteAdaptation.AddIn.ReviewData
{
    // R406. The window of button 3 in Revit: modal, its owner the Revit main window. Show returns when the window is closed, with
    // Saved true when Save review ran in it
    public sealed class ReviewDataDialog : IReviewDialog
    {
        private readonly IntPtr owner;

        public ReviewDataDialog(IntPtr owner)
        {
            this.owner = owner;
        }

        public ReviewAnswer Show(ReviewView view, ReviewCallbacks callbacks)
        {
            var session = new ReviewDataSession(view, callbacks);
            var window = new ReviewDataWindow(session);
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
            }
            window.ShowDialog();
            return new ReviewAnswer { Saved = session.Saved };
        }
    }
}
