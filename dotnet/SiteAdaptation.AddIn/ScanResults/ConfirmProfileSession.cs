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

        public ConfirmProfileSession(ConfirmProfileModel model, string workFolder, string runId)
        {
            Model = model;
            this.workFolder = workFolder;
            this.runId = runId;
        }

        public ConfirmProfileModel Model { get; }

        // The line the pane tells after the window: what was saved or removed, null while the window is open
        public string Result { get; private set; }

        // True when the file was written
        public bool Saved { get; private set; }

        // True when a file of an earlier Confirm was removed, as the person cleared every block
        public bool Removed { get; private set; }

        // The reason the last Confirm was refused, the window stays open to correct it
        public string Refusal { get; private set; }

        public event Action Finished;

        public void Confirm()
        {
            try
            {
                if (Model.GivesNothing)
                {
                    // A file of an earlier Confirm would stay in force behind a line that says nothing was confirmed
                    string earlier = Path.Combine(workFolder, ConfirmedProfile.FileName);
                    Removed = File.Exists(earlier);
                    if (Removed)
                    {
                        File.Delete(earlier);
                    }
                    Result = Removed ? ConfirmProfileText.Removed : ConfirmProfileText.NothingGiven;
                }
                else
                {
                    Model.Save(workFolder, runId);
                    Saved = true;
                    Result = ConfirmProfileText.Saved;
                }
                Refusal = null;
            }
            catch (Exception failure) when (failure is InvalidDataException || failure is IOException || failure is UnauthorizedAccessException)
            {
                Refusal = ConfirmProfileText.NotSaved.Replace("<why>", failure.Message);
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
