using System;
using SiteAdaptation.Common.Review;

namespace SiteAdaptation.AddIn.ReviewData
{
    // R406. What the buttons of the window do, apart from the window, so a test drives them with no click. A callback that throws
    // leaves the window open with its reason shown
    public sealed class ReviewDataSession
    {
        private readonly ReviewView view;
        private readonly ReviewCallbacks callbacks;

        public ReviewDataSession(ReviewView view, ReviewCallbacks callbacks)
        {
            this.view = view;
            this.callbacks = callbacks ?? new ReviewCallbacks();
            Model = new ReviewDataViewModel(view) { ZoomShown = this.callbacks.Zoom != null, ExportShown = this.callbacks.Export != null };
        }

        public ReviewDataViewModel Model { get; }

        // True once Save review ran
        public bool Saved { get; private set; }

        public void Zoom()
        {
            if (Model.Selected == null)
            {
                return;
            }
            Model.Message = callbacks.ZoomMessage(Model.Selected.Row) ?? "";
        }

        public void Export()
        {
            Model.Message = callbacks.ExportMessage() ?? "";
        }

        public void Save()
        {
            if (callbacks.Save == null)
            {
                return;
            }
            try
            {
                callbacks.Save(view.State.ToChecks());
                Saved = true;
                view.State.MarkSaved();
                view.Clusters?.MarkSaved();
                Model.Saved();
            }
            catch (Exception error)
            {
                Model.Message = error.Message;
            }
        }

        // R446. Whether the window may close. With marks not saved it asks. Save closes only once the save ran, its reason stays shown
        // when it did not. Don't save closes, Cancel stays
        public bool CanClose(Func<ReviewCloseChoice> ask)
        {
            if (!Model.NotSavedShown)
            {
                return true;
            }
            switch (ask())
            {
                case ReviewCloseChoice.DontSave:
                    return true;
                case ReviewCloseChoice.Save:
                    Save();
                    return !Model.NotSavedShown;
                default:
                    return false;
            }
        }

        public void ConfirmAll()
        {
            Model.ConfirmAll();
        }

        public void ConfirmRows()
        {
            Model.ConfirmRows();
        }

        public void Mark(string check)
        {
            Model.Mark(check);
        }
    }
}
