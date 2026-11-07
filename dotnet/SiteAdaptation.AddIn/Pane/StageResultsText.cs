namespace SiteAdaptation.AddIn.Pane
{
    // R689, R690, R691. The words of card 3 Review and deliver, in docs/design/14-ui-labels.md under Pane
    public static class StageResultsText
    {
        public const string NoBuild = "No build yet. Build site in stage 2 first.";
        public const string IssuesCount = "<n> issues from the last build.";
        public const string Mix = "Values: computed <n>, rule <n>, person entered <n>, missing <n>, other <n>.";
        public const string OpenRules = "Open rules";
        public const string UpdateFromReport = "Update from report";
        public const string NoRoadLevel = "<n> plots have no road level, so no floor level, and steps 6 to 8 leave them out.";
        // R802. The plots of the plan of 4 Check with no road level, the count of that plan and its NBH, F30 of short check 2
        public const string NoRoadLevelOfPlan = "<n> of the <n> plots of the plan of <name> have no road level, so no floor level, and steps 5 to 8 leave them out.";
        public const string ReportLine = "Report runs step 9 of version 1, Update from report reads the Excel back.";
    }
}
