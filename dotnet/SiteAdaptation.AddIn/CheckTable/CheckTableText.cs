namespace SiteAdaptation.AddIn.CheckTable
{
    // R376, docs/design/07-mock.md 7.4 and docs/design/12-ui-spec.md 7.4. Every text the window shows. The text test holds each against
    // docs/design/14-ui-labels.md word for word, a value written <n> as the list writes it and filled by TextTemplate
    public static class CheckTableText
    {
        public const string Title = "4 Check, NBH <n>, <n> plots";
        public const string TitleAll = "4 Check, NBH all, <n> plots";
        public const string ProfileLine = "Level profile <n>, override file <n>, <n> overrides, <n> of them on NBH <n>";
        public const string ZoomToPlot = "Zoom to plot";
        public const string ExportToExcel = "Export to Excel";
        public const string SaveOverrides = "Save overrides";
        public const string HowTo = "Each row is a plot where the tool and the past job differ. Pick a row to see why, then choose Rule, Past or Skip. Save overrides keeps your choices.";
        public const string FollowLine = "<n> plots follow every rule";
        public const string NoPastJob = "No past job was given, so Plot cut shows no rows and no plot is counted as following every rule on Plot cut.";

        public const string All = "All";
        public const string TeamCheck = "Team check";
        public const string PlotCut = "Plot cut";
        public const string Floor = "Floor";
        public const string Hand = "Hand";
        public const string UnitCode = "Unit code";
        public const string Position = "Position";
        public const string Level = "Level";
        public const string Parameter = "Parameter";
        public const string Assumed = "Assumed";

        public const string Plot = "Plot";
        public const string Nbh = "NBH";
        public const string Unit = "Unit";
        public const string Check = "Check";
        public const string ToolValue = "Tool value (rule)";
        public const string PastJob = "Past job";
        public const string Rule = "Rule";
        public const string ManualRule = "Manual rule";
        public const string Use = "Use";
        public const string Past = "Past";
        public const string Skip = "Skip";

        public const string Selected = "Selected";
        public const string SelectedPlot = "Plot <n>, <unit>, <check>";
        public const string Tool = "Tool";
        public const string Why = "Why";
        public const string Choose = "Choose";
        public const string KeepTool = "Keep the tool value.";
        public const string UsePast = "Use the past job. Saved as an override.";
        public const string LeaveOut = "Leave this plot out. It is listed in the report.";
        public const string OpenRules = "Open in Site Adaptation Rules";

        public const string None = "none";
        public const string NotSavedYet = "Not saved yet";
        public const string OverridesSaved = "Overrides saved.";
        public const string PickARow = "Pick a row from the table.";
    }
}
