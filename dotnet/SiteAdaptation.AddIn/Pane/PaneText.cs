namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/12-ui-spec.md 7.2. Every text the view model of the pane shows. The text test holds each against
    // docs/design/13-ui-labels.md word for word, a value written <n> as the list writes it and filled by TextTemplate
    public static class PaneText
    {
        public const string Title = "Site Adaptation";
        public const string NotSaved = "not saved";

        public const string NextLabel = "Next step";
        public const string RunButton = "Run <n> <name>";
        public const string DryRunOn = "Dry run is on.";
        public const string DryRunOff = "Dry run is off.";

        public const string StartLabel = "Start here";
        public const string StartName = "Choose the settings file";
        public const string StartLine = "It names the drawings, the rules and the prototypes for this job. The tool fills the rest of this panel from it.";
        public const string StartButton = "Choose settings file";

        public const string InputHelpAll = "The drawings and schedules the tool reads. All <n> found.";
        public const string InputHelpSome = "The drawings and schedules the tool reads. <m> of <n> found.";

        public const string Settings = "Settings";
        public const string Rules = "Rules";
        public const string Baseplan = "Baseplan";
        public const string VillaBlocks = "Villa blocks";
        public const string Curbs = "Curbs";
        public const string RoadSurface = "Road surface";
        public const string DlusAndSales = "DLUS and sales";
        public const string PlotNumbers = "Plot numbers";
        public const string Prototypes = "Prototypes";

        public const string NotChosen = "Not chosen";
        public const string Size = "<n> MiB";
        public const string SizeAndHash = "<n> MiB <n>";

        public const string StepLine1 = "Coordinates, levels and types from CAD";
        public const string StepLine2 = "Reads the drawings and schedules";
        public const string StepLine3 = "Compares the rules with the past job";
        public const string StepLine4 = "Places a villa on each plot";
        public const string StepLine5 = "Generic, yard and ramp floors";
        public const string StepLine6 = "Sets FFL and entry levels";
        public const string StepLine7 = "Boundary and garden walls";
        public const string StepLine8 = "Excel report of the run";

        public const string Done = "Done";
        public const string Issues = "<n> issues";
        public const string DoneWithIssues = "Done with <n> issues";
        public const string Ready = "Ready";
        public const string NeedsStep = "Needs step <n>";
        public const string NeedsSettings = "Needs settings";

        public static readonly string[] StepLines = { StepLine1, StepLine2, StepLine3, StepLine4, StepLine5, StepLine6, StepLine7, StepLine8 };
    }
}
