namespace SiteAdaptation.AddIn.ScanResults
{
    // Lane C SCAN-UI, docs/design/boards/ScanResults.dc.html word for word, the texts the board gives no words for marked CHOSEN in
    // docs/design/14-ui-labels.md. The texts of the rows come from ScanViewText of Common. Every text the window shows is in these two files
    public static class ScanResultsText
    {
        public const string Title = "What the tool read";
        public const string Badge = "Check before step 3";
        public const string Line = "The tool read your files and proposes where each value comes from. If a pick is wrong, change it from the list. Nothing changes in the model.";
        public const string FilesRead = "files read";
        public const string PlotsFound = "plots found";

        // R771, F6 of run 1. The line of a step 2 the Build put back, which shows no window
        public const string QuietKept = "The Build kept the picks and values you confirmed before and showed no window.";
        public const string Conflicts = "conflicts";

        public const string DataCheckTitle = "Data check";
        public const string DataCheckHelp = "What the folder you gave holds for each thing the build needs. Nothing is written in your folder.";
        public const string ColumnNeed = "Need";
        public const string ColumnMark = "Mark";
        public const string ColumnLine = "What the tool found";

        public const string ValuesTitle = "Where each value comes from";
        public const string ValuesHelp = "The tool picks the source that covers the most plots. The others stay as checks.";
        public const string ColumnValue = "Value";
        public const string ColumnSource = "Source";
        public const string ColumnCovers = "Covers";
        public const string ColumnChecks = "Checks";
        public const string ColumnStatus = "Status";

        public const string FilesTitle = "Files read";
        public const string FilesHelp = "Every file you gave, and what the tool found inside.";
        public const string ColumnFile = "File";
        public const string ColumnKind = "Kind";
        public const string ColumnFound = "What it found";

        public const string ConflictsTitle = "Conflicts";
        public const string ConflictsHelp = "Two files disagree on these plots. Pick the right one.";
        public const string ColumnPlot = "CAD label";
        public const string ColumnUse = "Use";
        public const string ShowAll = "Show all <n>";
        public const string UseProposed = "Use the proposed for all";

        public const string Foot = "Your picks are saved with the setup. Step 2 Read data uses them next time.";
        public const string Cancel = "Cancel";
        public const string Export = "Export to Excel";
        public const string NotInInputs = "The tool never writes in the input folder. Press Export to Excel again, it opens in the work folder.";
        public const string UsePicks = "Use these picks";
        public const string Saved = "Saved <name>.";
    }
}
