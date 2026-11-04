namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/12-ui-spec.md 7.2. Every text the view model of the pane shows. The text test holds each against
    // docs/design/14-ui-labels.md word for word, a value written <n> as the list writes it and filled by TextTemplate
    public static class PaneText
    {
        public const string Title = "Site Adaptation";
        public const string NotSaved = "not saved";
        public const string ShowRefused = "Revit would not show the pane now. Open a project and try again.";

        public const string NextLabel = "Next step";
        public const string RunButton = "Run <n> <name>";
        public const string DryRunOn = "Dry run is on.";
        public const string DryRunOff = "Dry run is off.";
        public const string DryRunNoStep4 = "4 Check has no dry run yet, so the tool unticks Dry run when it runs.";
        public const string DryRunUnticked = "Dry run is off, as 4 Check has no dry run yet.";
        public const string RunComplete = "Run complete";
        public const string RunCompleteLine = "The report is in the work folder.";
        public const string OpenFolder = "Open folder";
        public const string FolderGone = "The folder is not there any more: <name>";

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
        public const string CurbsRow = "Plot fronts (curb lines)";
        public const string CurbsHint = "Read only. Finds the front of each plot. No curb is built.";
        public const string RoadSurface = "Road surface";
        public const string DlusAndSales = "DLUS and sales";
        public const string PlotNumbers = "Plot numbers";
        public const string DlusTitle = "DLUS schedule";
        public const string SalesTitle = "Sales schedule";
        public const string Prototypes = "Prototypes";
        public const string SharedParameters = "Shared parameters";
        public const string Export = "Export";

        public const string PickSurfaces = "Pick surfaces";
        public const string SurfacesInOrder = "Road surfaces in order (<n>): <name>";
        public const string SurfacesNone = "No road surface is picked.";
        public const string SurfacesPastJob = "The saved project takes its road surfaces from its past job, so this pick is not used.";
        public const string SurfacesNoSetup = "Pick a saved project under Start from first.";
        // R496. The year read and the Revit the add-in runs on fill the two slots
        public const string OldPrototypes = "These files are Revit <n> and upgrade on each use, use the <n> copies. A remake from them takes 20 to 41 minutes.";

        public const string NotChosen = "Not chosen";
        public const string Size = "<n> MiB";
        public const string SizeAndHash = "<n> MiB <n>";

        public const string StepLine1 = "Coordinates, levels and types, set in step 1";
        public const string StepLine1Cad = "Coordinates from the CAD baseplan, levels and units from the rules, types from config";
        public const string StepLine1File = "Coordinates, levels and units from the position file, types from config";
        public const string StepLine1Typed = "Coordinates typed by the user, levels and units from the rules, types from config";
        public const string StepLine2 = "Reads the drawings and schedules";
        public const string StepLine3 = "The team checks what was read";
        public const string StepLine4 = "Compares the rules with the past job";
        public const string StepLine5 = "Generic, yard and ramp floors";
        public const string RoadTopography = "Road topography";
        public const string RoadTopographyTip = "Makes one toposolid for the NBH from the road surface and reports the level at each ramp road edge. Off leaves one that stands.";
        public const string StepLine6 = "Sets FFL and entry levels";
        public const string StepLine7 = "Places a villa on each plot at its FFL";
        public const string StepLine8 = "Boundary walls";
        public const string StepLine9 = "Excel report of the run";

        public const string Done = "Done";
        public const string Issues = "<n> issues";
        public const string DoneWithIssues = "Done with <n> issues";
        public const string Ready = "Ready";
        public const string NeedsStep = "Needs step <n>";
        public const string NeedsSettings = "Needs setup";

        // The steps after 4 folded into one row on first open, as "Floors and ramps to 9 Report"
        public const string FoldedSteps = "<name> to <n> <name>";

        // R543. What site.json says the last step 1 did, the plain line until it ran
        public static string StepOneLine(string siteText)
        {
            string source = Common.SetUp.SiteSources.Written(siteText);
            return source == Common.SetUp.SiteSources.Cad ? StepLine1Cad
                : source == Common.SetUp.SiteSources.PositionFile ? StepLine1File
                : source == Common.SetUp.SiteSources.Typed ? StepLine1Typed
                : StepLine1;
        }

        public static readonly string[] StepLines = { StepLine1, StepLine2, StepLine3, StepLine4, StepLine5, StepLine6, StepLine7, StepLine8, StepLine9 };
    }
}
