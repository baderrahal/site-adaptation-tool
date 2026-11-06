namespace SiteAdaptation.AddIn.Pane
{
    // R686, R687. The words of card 2 Build site and of the 2D preview, in docs/design/14-ui-labels.md under Pane
    public static class SiteBuildText
    {
        public const string ScopeProject = "Builds the whole project.";
        public const string ScopeNbh = "Builds the neighbourhood <name>.";
        public const string ScopePlots = "Builds the plots <name>.";
        public const string Preview = "Preview first";
        public const string NoRead = "Read the project data in stage 1 first, Build takes the plots it found.";
        public const string Stopped = "Build stopped: <name>";
        public const string NotRun = "The plan could not be made: <name>";
        public const string Coordinates = "Coordinates from the drawing: east <n> m, north <n> m.";
        public const string CoordinatesFile = "Coordinates from the drawing: east <n> m, north <n> m, <n> mm from the position file.";
        public const string NoCoordinates = "No coordinates from the drawing: <name>";
        public const string Issues = "<n> issues go to the issues list.";
        public const string AskRules = "<name> Use the manual template for this project now?";
        public const string PlanReady = "The plan is made. The steps 4 to 8 still run from Advanced until the one Build runs them.";

        public const string PreviewTitle = "Preview of the build";
        public const string PreviewLine = "<n> plots with an outline, <n> issues. Nothing is written to the model yet.";
        public const string GoOn = "Go on";
        public const string Cancel = "Cancel";
    }
}
