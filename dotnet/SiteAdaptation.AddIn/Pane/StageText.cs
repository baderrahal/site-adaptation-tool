namespace SiteAdaptation.AddIn.Pane
{
    // R680. Every text of the pane in 3 stages, the order of Bader of 6 Nov 2026. Each is under Pane in docs/design/14-ui-labels.md, marked CHOSEN as no
    // sketch in the repo writes them
    public static class StageText
    {
        public const string Stage1 = "Project data";
        public const string Stage2 = "Build site";
        public const string Stage3 = "Review and deliver";
        public const string Stage1Help = "Choose the folder of the project. The tool reads every drawing and sheet in it and says what it found.";
        public const string Stage2Help = "Builds the site model from the data of stage 1: floors and ramps, levels, villas and walls.";
        public const string Stage3Help = "The report of the build, written to Excel by step 9 of version 1.";

        public const string ChooseFolder = "Choose project folder";
        public const string FixItems = "Fix items";
        public const string Build = "Build";
        public const string Report = "Report";
        public const string SettingsButton = "Settings";
        public const string Advanced = "Advanced";
        public const string ProjectSetup = "Project setup";
        public const string Edit = "Edit";

        public const string NoDocument = "Open a project first.";
        public const string SetUpFirst = "Set up this project first, in Settings.";
        public const string Done = "Done";
        public const string DoneIssues = "Done, <n> issues";
        public const string Ready = "Ready";
        public const string NotStarted = "Not started";
        public const string Waits = "Waits for stage <n>";

        public const string NoRead = "No data check yet. Choose the project folder.";
        public const string ReadStopped = "The last read stopped and holds nothing to fix: <name>. Fix the cause and choose the folder again.";

        // Choose project folder, R681
        public const string FolderOwnCopyOnly = "This project uses the shipped settings, so the tool cannot set its folder. Start a new project in Settings, Project setup, then choose the folder.";
        public const string FolderHoldsWork = "The work folder of this model lies inside that folder, and the tool never writes into a project folder. Choose another folder or save the model elsewhere.";
        public const string FolderNotWritten = "The project folder was not saved: <name>";
        public const string FixNotOpened = "Fix items could not open the last read: <name>";
    }
}
