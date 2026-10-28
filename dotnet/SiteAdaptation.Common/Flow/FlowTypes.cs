using System;
using System.Collections.Generic;

namespace SiteAdaptation.Common.Flow
{
    // R382. An input of a button as the pane names it, a file or a folder. Path is null or empty when the user has not chosen it
    public sealed class RunInput
    {
        public RunInput(string name, string path, bool isFolder)
        {
            Name = name;
            Path = path;
            IsFolder = isFolder;
        }

        public string Name { get; }
        public string Path { get; }
        public bool IsFolder { get; }
    }

    // R382, R372. What the flow asks of the pane side, which fills it: the work folder of 06-settings, the tick "Dry run first, change
    // nothing", the step state of R366 and the inputs the pane holds. The flow tells it of a real run that committed
    public interface IRunState
    {
        string WorkFolder(string documentPath);

        bool DryRunFirst { get; }

        // The first step the button needs that is not done, 0 when none
        int MissingStep(int button);

        IReadOnlyList<RunInput> Inputs(int button);

        void Committed(int button, RunRecord record);
    }

    // R382. One model transaction. The Revit side keeps warnings as issues and an error makes Revit roll it back
    public interface IModelTransaction : IDisposable
    {
        // True only when the transaction ended committed
        bool Commit();

        void RollBack();

        IReadOnlyList<string> Warnings { get; }

        IReadOnlyList<string> Errors { get; }
    }

    public interface IModel
    {
        IModelTransaction Start(string name);
    }

    // R382. Apply or Cancel on the summary of a dry run, or with no dry run on the counts alone. True is Apply
    public interface IConfirm
    {
        bool Apply(string label, RunSummary summary, bool dryRun);
    }

    // R382. What a dry run or a count found: the counts to place, move, skip and leave, and the list per plot
    public sealed class RunSummary
    {
        public RunSummary(string counts, IReadOnlyList<string> perPlot)
        {
            Counts = counts ?? string.Empty;
            PerPlot = perPlot ?? new string[0];
        }

        public string Counts { get; }
        public IReadOnlyList<string> PerPlot { get; }
    }

    // R382. The body of a button as delegates, so the flow is the same for all eight. Preview changes nothing in the model: with the
    // dry run ticked it is the dry run and may write under its dry folder, unticked it only counts. Apply runs inside the transaction
    // and gives its issues. ReadBack gives null when the model holds what Apply set, else what differs
    public sealed class ButtonBody
    {
        public string NotBuilt { get; set; }
        public Func<RunContext, RunSummary> Preview { get; set; }
        public Func<RunContext, IReadOnlyList<string>> Apply { get; set; }
        public Func<RunContext, string> ReadBack { get; set; }
    }

    public enum RunEnd
    {
        Stopped,
        Cancelled,
        RolledBack,
        Committed,
    }

    // R382. A real run that committed, for the step line of the pane. A file whose move failed keeps its temporary name and the
    // step is not Done
    public sealed class RunRecord
    {
        public RunRecord(string runId, IReadOnlyDictionary<string, string> inputHashes, IReadOnlyList<string> issues, IReadOnlyDictionary<string, string> outputs, IReadOnlyList<string> failedMoves)
        {
            RunId = runId;
            InputHashes = inputHashes;
            Issues = issues;
            Outputs = outputs;
            FailedMoves = failedMoves;
        }

        public string RunId { get; }
        public IReadOnlyDictionary<string, string> InputHashes { get; }
        public IReadOnlyList<string> Issues { get; }

        // Each output on its name with its SHA-256
        public IReadOnlyDictionary<string, string> Outputs { get; }
        public IReadOnlyList<string> FailedMoves { get; }

        public bool StepDone
        {
            get { return FailedMoves.Count == 0; }
        }
    }

    public sealed class RunOutcome
    {
        public RunOutcome(RunEnd end, string message, RunRecord record)
        {
            End = end;
            Message = message;
            Record = record;
        }

        public RunEnd End { get; }

        // The text of the TaskDialog, null when there is nothing to tell
        public string Message { get; }
        public RunRecord Record { get; }
    }
}
