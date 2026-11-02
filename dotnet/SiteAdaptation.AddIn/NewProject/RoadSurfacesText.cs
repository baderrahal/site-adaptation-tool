namespace SiteAdaptation.AddIn.NewProject
{
    // R492. The words of the window Road surfaces and of the row Road levels that opens it, DRAFT, listed under New project in
    // docs/design/14-ui-labels.md. No board holds this window, so Bader's check of the draft comes first
    public static class RoadSurfacesText
    {
        public const string Title = "Road surfaces";
        public const string Help = "The tool takes each plot corner from the first ticked surface that covers it.";
        public const string Up = "Up";
        public const string Down = "Down";
        public const string Use = "Use these picks";
        public const string NeedTick = "Tick at least one road surface to use the picks.";
        public const string NotInFolder = "Not in the folder any more: <name>. It stays unticked.";
        public const string NoFiles = "The road surface folder holds no road surface files. Pick the folder in the row Road levels, then open this window again.";

        public const string Pick = "Pick surfaces";
        public const string InOrder = "Road surfaces in order (<n>): <name>";
        public const string NonePicked = "No road surface is picked.";
        public const string PastJob = "The saved project takes its road surfaces from its past job, so this pick is not used.";
        public const string NoSetup = "Pick a saved project under Start from first.";
    }
}
