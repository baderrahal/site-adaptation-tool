namespace SiteAdaptation.AddIn.ReviewData
{
    // R406, docs/design/13-workflow-changes.md Review data (5c). Every text the window shows. The text test holds each against
    // docs/design/14-ui-labels.md word for word, a value written <n> as the list writes it and filled by TextTemplate
    public static class ReviewDataText
    {
        public const string Title = "3 Review data, NBH <n>, <n> plots";
        public const string ReadFrom = "Read from <n>";
        public const string CheckedLine = "<n> of <n> checked by the team";
        public const string ZoomToPlot = "Zoom to plot";
        public const string ExportToExcel = "Export to Excel";
        public const string SaveReview = "Save review";
        public const string HowTo = "This is what the tool read from CAD and Civil 3D for each plot. Pick a plot to see its corners and sources, then mark it OK or Wrong. A plot marked Wrong is listed in 4 Check and in the report.";

        public const string All = "All";
        public const string NotChecked = "Not checked";
        public const string Ok = "OK";
        public const string Wrong = "Wrong";
        public const string FindPlot = "Find plot";

        public const string PlotNo = "Plot No";
        public const string CadLabel = "CAD label";
        public const string VillaType = "Villa type";
        public const string Mirror = "Mirror";
        public const string Cluster = "Cluster";
        public const string AreaM2 = "Area m2";
        public const string RoadAtFront = "Road at front";
        public const string TeamCheck = "Team check";

        public const string SelectedPlot = "Selected plot";
        public const string Nbh = "NBH";
        public const string FrontEdge = "Front edge";
        public const string PlotCorners = "Plot corners";
        public const string Corner = "Corner <n>";
        public const string CornerHead = "Corner";
        public const string X = "X";
        public const string Y = "Y";
        public const string SharedCoordinates = "Shared coordinates";
        public const string WhatIsWrong = "What is wrong";

        public const string ManualRule = "Manual rule <n>";
        public const string Edge = "Edge <n>";
        public const string NotReadYet = "not read yet";
        public const string Yes = "Yes";
        public const string No = "No";
        public const string PickAPlot = "Pick a plot from the table.";
        public const string EarlierNote = "Earlier note: <n>";
        public const string NotSavedYet = "Not saved yet";
        public const string ReviewSaved = "Review saved.";

        // R442, Bader's prompt of 1 Nov 2026, a DRAFT with no board
        public const string Confirmed = "Confirmed";
        public const string ConfirmAllProposed = "Confirm all proposed";

        // R444, Bader's demo check of 1 Nov 2026, a DRAFT with no board
        public const string Close = "Close";
        public const string CloseTitle = "3 Review data";
        public const string CloseAsk = "Save your review?";
        public const string CloseSave = "Save";
        public const string CloseDontSave = "Don't save";
        public const string CloseCancel = "Cancel";
        public const string ConfirmAll = "Confirm all";
        public const string ConfirmAllShown = "Confirm all shown";
        public const string ClusterHowTo = "Tick Confirmed for each cluster and mirror that 4 Check proposed, or click Confirm all proposed, then Save review. A plot whose proposed cluster is not confirmed is marked for buttons 5 to 8.";
    }
}
