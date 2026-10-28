using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common.Flow
{
    // R382. Every text the flow shows a user, in one place for the text test of docs/design/12-ui-spec.md
    public static class RunTexts
    {
        public const string SaveFirst = "Save the project first, its work folder is named by its file";
        public const string Apply = "Apply";
        public const string DryRunDone = "Dry run done, nothing was changed.";
        public const string NothingChangedYet = "Nothing changes until Apply.";
        public const string NotCommitted = "Revit did not commit the change.";

        public static string NotBuilt(string label)
        {
            return label + " is not built yet.";
        }

        public static string TransactionName(string label)
        {
            return "Site Adaptation " + label;
        }

        public static string NeedsStep(string label, int step)
        {
            return label + " needs step " + step;
        }

        public static string NotChosen(string label, RunInput input)
        {
            return label + " needs " + input.Name + ", the " + Kind(input) + " is not chosen";
        }

        public static string NotOnDisk(string label, RunInput input)
        {
            return label + " needs " + input.Name + ", the " + Kind(input) + " is not on disk";
        }

        public static string ChangedSinceDryRun(string label, IEnumerable<string> inputs)
        {
            return label + ": " + string.Join(", ", inputs) + " changed since the dry run, nothing was changed. Run the button again.";
        }

        public static string ChangedNothing(string label, string reason)
        {
            return label + " changed nothing, the model is as it was. " + reason;
        }

        public static string Failed(string message)
        {
            return "The run failed: " + message;
        }

        public static string ReadBackDiffers(string difference)
        {
            return "The read back does not match: " + difference;
        }

        public static string Refused(IEnumerable<string> errors)
        {
            return "Revit refused the change: " + string.Join(" ", errors);
        }

        public static string HeldOpen(string label, IEnumerable<string> files)
        {
            return label + " changed the model, but " + string.Join(", ", files) + " was not saved: another program holds it open. Close it and run the button again.";
        }

        public static string DryHeldOpen(string file)
        {
            return file + " was not saved: another program holds it open.";
        }

        // R382. The content of the confirm: unticked it shows the counts only, with no list per plot
        public static string ConfirmContent(RunSummary summary, bool dryRun)
        {
            return (dryRun ? DryRunDone : NothingChangedYet) + (summary.Counts.Length == 0 ? string.Empty : "\n" + summary.Counts);
        }

        public static string ConfirmDetails(RunSummary summary, bool dryRun)
        {
            return dryRun && summary.PerPlot.Count > 0 ? string.Join("\n", summary.PerPlot) : null;
        }

        private static string Kind(RunInput input)
        {
            return input.IsFolder ? "folder" : "file";
        }

        internal static IReadOnlyList<string> Names(IEnumerable<string> paths)
        {
            return paths.Select(System.IO.Path.GetFileName).ToList();
        }
    }
}
