namespace SiteAdaptation.AddIn.Update
{
    // R402, 5f, docs/design/13-workflow-changes.md part Update from report. Every text the view model and the window show, in the files of the
    // window so the text test of R372 finds it there. The tiles, chips and columns are held equal to UpdateText of Common by a test, so the
    // words of the logic and of the window never differ
    public static class UpdateFromReportText
    {
        public const string Title = "Update from report";
        public const string PickLine = "Pick the edited report Excel";
        public const string Browse = "Browse";

        public const string ChangesFound = "Changes found";
        public const string FollowTheRules = "Follow the rules";
        public const string BreakAManualRule = "Break a manual rule";
        public const string NotEditableIgnored = "Not editable ignored";

        public const string All = "All";
        public const string Ffl = "FFL";
        public const string Entry = "El @ Entry";
        public const string Steps = "Steps";
        public const string VillaType = "Villa type";
        public const string Mirror = "Mirror";
        public const string BreaksARule = "Breaks a rule";

        public const string KeepModel = "Keep the model";
        public const string ApplyAnyway = "Apply anyway";
        public const string Note = "Note";

        public const string Foot = "Updates the levels, floors, links and parameters of the changed plots only.";
        public const string Cancel = "Cancel";
        public const string DryRun = "Dry run";
        public const string ApplyN = "Apply <n> changes";
        public const string DryDone = "Dry run: <n> changes would be saved, <n> replaced. Nothing was changed.";
        public const string Saved = "Saved <n> changes to the override list, plots changed: <n>.";
        public const string NotSaved = "<n> ticked changes were not saved:";
        // A chip with the count of its changes
        public const string ChipCount = "<name> <n>";

        // Why the picked files are not read, in the problem line
        public const string NoPlan = "the work folder holds no <n>, the plan of NBH <n> that 4 Check writes, so the Excel can not be compared (R402)";
        public const string WorkerMissing = "the worker CadReader.exe is not beside the add-in: <n>";
        public const string WorkerFailed = "the worker did not read the Excel: <n>";
        public const string WorkerNoAnswer = "no answer";
        public const string WorkerNoJson = "the list the worker wrote is no JSON object";

        public static readonly string[] Tiles = { ChangesFound, FollowTheRules, BreakAManualRule, NotEditableIgnored };
        public static readonly string[] Chips = { All, Ffl, Entry, Steps, VillaType, Mirror, BreaksARule };
        public const string ColumnApply = "Apply";
        public const string ColumnPlot = "Plot";
        public const string ColumnField = "Field";
        public const string ColumnInModel = "In model";
        public const string ColumnInExcel = "In Excel";
        public const string ColumnRuleCheck = "Rule check";
        public static readonly string[] Columns = { ColumnApply, ColumnPlot, ColumnField, ColumnInModel, ColumnInExcel, ColumnRuleCheck };
    }
}
