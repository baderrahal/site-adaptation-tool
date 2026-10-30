using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common.Update;

namespace SiteAdaptation.AddIn.Update
{
    // R402. What the window asks of the run, apart from the window: Read gives the list of the picked files through the worker, Preview the
    // entries Apply would save with no write. A callback that throws leaves the window open with its reason shown
    public sealed class UpdateFromReportCallbacks
    {
        public Func<IReadOnlyList<string>, UpdateList> Read { get; set; }

        public Func<IReadOnlyList<UpdateChange>, UpdateApplied> Preview { get; set; }
    }

    // What the window hands back: Apply false for Cancel or a closed window, else the ticked changes in the order of the list and the note
    // of each one that has a note
    public sealed class UpdateFromReportAnswer
    {
        public bool Apply { get; set; }

        public List<UpdateChange> Ticked { get; } = new List<UpdateChange>();

        public Dictionary<UpdateChange, string> Notes { get; } = new Dictionary<UpdateChange, string>();
    }

    public interface IUpdateFromReportDialog
    {
        UpdateFromReportAnswer Show(UpdateFromReportCallbacks callbacks);
    }

    // R402. What the buttons of the window do, so a test drives them with no click. The window holds the level Excel and the report Excel of
    // one NBH, so a third pick starts a new pair
    public sealed class UpdateFromReportSession
    {
        public const int MostFiles = 2;

        private readonly UpdateFromReportCallbacks callbacks;
        private readonly Func<string> pickFile;
        private readonly List<string> files = new List<string>();

        public UpdateFromReportSession(UpdateFromReportCallbacks callbacks, Func<string> pickFile)
        {
            this.callbacks = callbacks ?? new UpdateFromReportCallbacks();
            this.pickFile = pickFile;
            Model = new UpdateFromReportViewModel();
        }

        public UpdateFromReportViewModel Model { get; }

        public UpdateFromReportAnswer Answer { get; private set; } = new UpdateFromReportAnswer { Apply = false };

        public event Action Finished;

        public void Browse()
        {
            string path = pickFile?.Invoke();
            if (string.IsNullOrEmpty(path) || callbacks.Read == null)
            {
                return;
            }
            var next = files.Count >= MostFiles ? new List<string>() : files.ToList();
            if (!next.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                next.Add(path);
            }
            Model.ClearProblem();
            try
            {
                UpdateList list = callbacks.Read(next);
                files.Clear();
                files.AddRange(next);
                Model.Load(list, files);
            }
            catch (Exception error)
            {
                Model.Fail(error.Message);
            }
        }

        public void DryRun()
        {
            if (!Model.CanDryRun || callbacks.Preview == null)
            {
                return;
            }
            Model.ClearProblem();
            try
            {
                Model.ShowDry(callbacks.Preview(Model.TickedChanges));
            }
            catch (Exception error)
            {
                Model.Fail(error.Message);
            }
        }

        public void Apply()
        {
            if (!Model.CanApply)
            {
                return;
            }
            var answer = new UpdateFromReportAnswer { Apply = true };
            foreach (UpdateChange change in Model.TickedChanges)
            {
                answer.Ticked.Add(change);
            }
            foreach (UpdateRowViewModel row in Model.AllRows.Where(row => row.Apply && row.BreaksManual && row.Note.Trim().Length > 0))
            {
                answer.Notes[row.Change] = row.Note.Trim();
            }
            Answer = answer;
            Finished?.Invoke();
        }

        public void Cancel()
        {
            Answer = new UpdateFromReportAnswer { Apply = false };
            Finished?.Invoke();
        }
    }
}
