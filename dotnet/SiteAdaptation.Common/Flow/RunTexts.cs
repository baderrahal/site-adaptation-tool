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

        // R498
        public static string RecordNotWritten(string reason)
        {
            return "The step is done, but its record did not reach the model, so a reopen asks for it again: " + reason;
        }

        public static string Refused(IEnumerable<string> errors)
        {
            return "Revit refused the change: " + string.Join(" ", errors);
        }

        public static string HeldOpen(string label, IEnumerable<string> files)
        {
            return label + " changed the model, but " + string.Join(", ", files) + " was not saved: another program holds it open. Close it and run the button again.";
        }

        // R375. The end of a button whose worker did not end done, its reason named
        public static string LaterFailed(string label, string reason)
        {
            return label + " did not finish, nothing was kept: " + (reason ?? "no reason was given");
        }

        // R375. Button 2, its worker started, ended and reused. Each count is a number, each file a file name
        public static string ReadRunning(string label)
        {
            return label + " is running already, its worker has not ended.";
        }

        public static string ReadCounts(int files, double mib)
        {
            return "Reads " + files + " files, " + System.Math.Round(mib, 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " MiB.";
        }

        public static string ReadReuses(int files, string runId)
        {
            return "Reuses the read of run " + runId + ", its " + files + " input files are unchanged.";
        }

        // R639. A raw read has no list of files to count, the worker picks what it opens as it walks. So the count names what was measured, the
        // files the folder holds, and never says that many are read
        public static string ReadFolderCounts(int files, double mib, bool confirmed)
        {
            return (confirmed ? "Reads the folder, the settings file and the values a person confirmed." : "Reads the folder and the settings file.")
                + " Files in the folder: " + files + ", " + System.Math.Round(mib, 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " MiB, those of a kind it knows are read.";
        }

        // R821. when is the time of the read, ReadDataRun.RunTime of its run id, so a person knows which read comes back in seconds
        public static string ReadFolderReuses(string when, bool confirmed)
        {
            return "Reuses the read of " + when + (confirmed ? ", nothing changed: the folder, the settings file and the values a person confirmed are unchanged." : ", nothing changed: the folder and the settings file are unchanged.");
        }

        // R639. R635 sends a project that lacks a file of the old read to the folder on purpose. The plot table then differs, so a person is told which file did it
        public static string ReadFolderInstead(IReadOnlyList<string> lacks)
        {
            return lacks.Count == 0
                ? "This project names no files for the old read, so the folder is read instead."
                : "The old read lacks a file: " + lacks[0] + (lacks.Count > 1 ? ", and " + (lacks.Count - 1) + " more" : "") + ". The folder is read instead.";
        }

        public static string ReadFolderLack(string lack)
        {
            return "The old read lacks: " + lack;
        }

        // R639. Why a read of these same files is done again, and what the end of a read says when it is not whole
        public static string ReadAgain(string why)
        {
            return "Reads again, the last read of these files is not whole: " + why + ".";
        }

        public static string ReadNotWhole(string why)
        {
            return "This read is not whole, the next press reads again: " + why + ".";
        }

        public static string ReadStopped(string reason)
        {
            return "the check stopped, " + reason;
        }

        public static string ReadLeftOut(int count, string name, string reason)
        {
            return "files or folders not read " + count + ", the first is " + name + ", " + reason;
        }

        public const string ReadNoResult = "its check left no result";

        // R639. The band line Read from of 3 Review data on a raw read, which holds no drawing by role
        public static string ReadFromFolder(string path)
        {
            return "the folder " + path;
        }

        public static string ReadFile(string file, long bytes, string sha256, bool unchanged)
        {
            return file + ", " + bytes + " bytes, SHA-256 " + sha256 + (unchanged ? ", unchanged since the last read" : ", new or changed since the last read");
        }

        public static string ReadDone(string label, int outputs, int issues)
        {
            return label + " done: " + outputs + " files written, " + issues + " issues.";
        }

        public static string ReadReused(string label, string runId)
        {
            return label + " done: the read of run " + runId + " is reused, nothing was read again.";
        }

        public static string ReadWorker(string status, string message)
        {
            return "the worker ended " + status + ", " + message;
        }

        public static string ReadScope(string label, IEnumerable<string> stops)
        {
            return label + " read the files, but the scope stops it: " + string.Join(". ", stops) + ". Change the scope and run it again, the read is reused.";
        }

        // R639. The same stop on a read that is not whole, which is never reused. A stopped check holds no plot, so the scope is the symptom and the why the cause
        public static string ReadScopeAgain(string label, IEnumerable<string> stops, string why)
        {
            return label + " read the files, but the scope stops it: " + string.Join(". ", stops) + ". This read is not whole, the next press reads again: " + why + ".";
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
