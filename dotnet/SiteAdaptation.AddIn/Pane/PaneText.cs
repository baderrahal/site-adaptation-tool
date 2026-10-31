namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/12-ui-spec.md 7.2. Every text the view model of the pane shows. The text test holds each against
    // docs/design/14-ui-labels.md word for word, a value written <n> as the list writes it and filled by TextTemplate
    public static class PaneText
    {
        public const string Title = "Site Adaptation";
        public const string NotSaved = "not saved";

        public const string NextLabel = "Next step";
        public const string RunButton = "Run <n> <name>";
        public const string DryRunOn = "Dry run is on.";
        public const string DryRunOff = "Dry run is off.";

        // docs/design/boards/PaneFirst.dc.html, the first open box of Lane C NEWPROJ-UI
        public const string StartLabel = "Start here";
        public const string StartName = "Set up this project";
        public const string StartLine = "Tell the tool where your files are. It reads them, shows what it found, and saves your setup. No file editing.";
        public const string StartButton = "Open New project";
        public const string StartSaved = "Or start from a saved project such as <name>:";
        public const string StartSavedNone = "Or start from a saved project:";
        public const string UseSaved = "Use a saved project";
        public const string NewHere = ". New here? Read";
        public const string FirstSuffix = "first.";

        public const string InputHelpAll = "The drawings and schedules the tool reads. All <n> found.";
        public const string InputHelpSome = "The drawings and schedules the tool reads. <m> of <n> found.";
        public const string InputHelpFirst = "Filled from your setup. You can change any of them after.";

        // The name of the config file among the inputs of a run, which the step hashes of R366 hold, never shown since NEWPROJ-UI
        public const string Settings = "Settings";
        public const string Setup = "Setup";
        public const string SetupSaved = "Saved for this project";
        public const string NotDone = "Not done";
        public const string FromSetup = "From setup";
        public const string Edit = "Edit";
        public const string Open = "Open";
        public const string Change = "Change";
        public const string Help = "Help";
        public const string HowItWorks = "How it works";
        public const string NewProject = "New project";
        public const string Rules = "Rules";
        public const string Baseplan = "Baseplan";
        public const string VillaBlocks = "Villa blocks";
        public const string Curbs = "Curbs";
        public const string RoadSurface = "Road surface";
        public const string DlusAndSales = "DLUS and sales";
        public const string PlotNumbers = "Plot numbers";
        public const string DlusTitle = "DLUS schedule";
        public const string SalesTitle = "Sales schedule";
        public const string Prototypes = "Prototypes";
        public const string SharedParameters = "Shared parameters";
        public const string Export = "Export";

        public const string NotChosen = "Not chosen";
        public const string Size = "<n> MiB";
        public const string SizeAndHash = "<n> MiB <n>";

        public const string StepLine1 = "Coordinates, levels and types from CAD";
        public const string StepLine2 = "Reads the drawings and schedules";
        public const string StepLine3 = "The team checks what was read";
        public const string StepLine4 = "Compares the rules with the past job";
        public const string StepLine5 = "Generic, yard and ramp floors";
        public const string StepLine6 = "Sets FFL and entry levels";
        public const string StepLine7 = "Places a villa on each plot at its FFL";
        public const string StepLine8 = "Boundary and garden walls";
        public const string StepLine9 = "Excel report of the run";

        public const string Done = "Done";
        public const string Issues = "<n> issues";
        public const string DoneWithIssues = "Done with <n> issues";
        public const string Ready = "Ready";
        public const string NeedsStep = "Needs step <n>";
        public const string NeedsSettings = "Needs setup";

        // The steps after 4 folded into one row on first open, as "Floors and ramps to 9 Report"
        public const string FoldedSteps = "<name> to <n> <name>";

        public static readonly string[] StepLines = { StepLine1, StepLine2, StepLine3, StepLine4, StepLine5, StepLine6, StepLine7, StepLine8, StepLine9 };
    }
}
