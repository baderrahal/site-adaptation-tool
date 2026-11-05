using System;
using System.IO;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.ConfirmProfile
{
    // R636. The window Confirm what was found with no window type: Confirm saves, Cancel saves nothing and is no failure
    public sealed class ConfirmProfileSession
    {
        private readonly string workFolder;
        private readonly string runId;
        private readonly int levelCount;

        public ConfirmProfileSession(ConfirmProfileModel model, string workFolder, string runId, int levelCount)
        {
            Model = model;
            this.workFolder = workFolder;
            this.runId = runId;
            this.levelCount = levelCount;
        }

        public ConfirmProfileModel Model { get; }

        // The line the window shows and the pane tells after it: what was saved or why not, null while the window is open
        public string Result { get; private set; }

        // True when the file was written
        public bool Saved { get; private set; }

        // The reason the last Confirm was refused, the window stays open to correct it
        public string Refusal { get; private set; }

        public event Action Finished;

        public void Confirm()
        {
            try
            {
                if (Model.GivesNothing)
                {
                    Result = ConfirmProfileText.NothingGiven;
                }
                else
                {
                    string path = Model.Save(workFolder, runId, levelCount);
                    Saved = true;
                    Result = string.Format(System.Globalization.CultureInfo.InvariantCulture, ConfirmProfileText.Saved, Path.GetFileName(path));
                }
                Refusal = null;
            }
            catch (Exception failure) when (failure is InvalidDataException || failure is IOException || failure is UnauthorizedAccessException)
            {
                Refusal = string.Format(System.Globalization.CultureInfo.InvariantCulture, ConfirmProfileText.NotSaved, failure.Message);
                return;
            }
            Finished?.Invoke();
        }

        public void Cancel()
        {
            Result = ConfirmProfileText.CancelledLine;
            Finished?.Invoke();
        }
    }
}
