namespace SiteAdaptation.AddIn.ScanResults
{
    // R620. The words of the section Data check of the window What the tool read and of the lines button 2 gives when the check stops.
    // Needs and Marks stand in the order of DataCheckRows.NeedIds and DataCheckRows.MarkIds. Every text is in docs/design/14-ui-labels.md
    // under What the tool read. CHOSEN by Lead: docs/design/12-ui-spec.md holds nothing for this window
    public static class DataCheckText
    {
        public static readonly string[] Needs =
        {
            "Plot outlines", "Plot numbers", "Group levels", "Villa types", "Villa blocks", "Road edges", "Road levels", "Coordinates", "Prototypes", "Rule sets", "Shared parameters",
        };

        public static readonly string[] Marks = { "Strong", "Usable", "Weak", "Missing" };

        // R767, F9 of run 1. The head count of a data check that listed plots apart: the villa plots of all numbered plots
        public const string VillaPlotsOfAll = "villa plots of all plots";
        public const string CountOfAll = "<n> of <n>";

        // The window when only the check is shown, because the read cannot go on or has nothing to pick
        public const string CheckLine = "The tool read the folder you gave and says what it holds for each need. Nothing changes in the model.";
        public const string CheckFoot = "The check wrote nothing in your folder and nothing in the model.";
        public const string Close = "Close";
        public const string UnreadTitle = "Files not read";
        public const string UnreadHelp = "The exact reason the check gives for each file it did not read.";
        public const string StoppedTitle = "The check stopped";

        // The lines button 2 ends with when the check does not give a result. the first slot is the button, the second the reason
        public const string RunningLine = "<n>: a data check of this work folder runs now, wait for its end.";
        public const string NoFolder = "<n>: the settings name no input folder, so the data check did not run and nothing was read.";
        public const string FolderGone = "<n>: the input folder is not there, so the data check did not run and nothing was read.";
        public const string NotStarted = "<n>: the data check could not start, <n>. Nothing was read.";
        public const string NotFinished = "<n>: the data check did not finish, <n>. Nothing was read.";
        public const string NoResult = "<n>: the data check wrote no result file, <n>. Nothing was read.";
        public const string BadResult = "<n>: the data check result could not be read, <n>. Nothing was read.";
    }
}
