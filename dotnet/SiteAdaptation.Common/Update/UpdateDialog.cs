using System;
using System.Collections.Generic;

namespace SiteAdaptation.Common.Update
{
    // R402. What the window asks of the run, apart from the window: Read gives the list of the picked files through the worker, Preview the
    // entries Apply would save with no write. A callback that throws leaves the window open with its reason shown. In Common since 6a, as
    // the dialogs of 1, 3 and 4, so the test host answers the window with no reference to the add-in (R384)
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
}
