namespace SiteAdaptation.AddIn.HowItWorks
{
    // Lane C NEWPROJ-UI, docs/design/boards/HowItWorks.dc.html word for word. Every text the window shows, in the files of the window so
    // the text test of R372 finds it there. The button of each step is its ribbon label, RibbonLayout
    public static class HowItWorksText
    {
        public const string Title = "How it works";
        public const string Badge = "Read once";
        public const string Line = "The tool puts each villa on its plot at the right level. It works in nine steps, one button each, always in this order. Each step needs the one before.";
        public const string ApplyLine = "Nothing changes in the model until you press Apply.";
        public const string Count = "<n> steps";
        public const string CountLine = "<n> buttons in the pane";

        public const string ColumnStep = "Step";
        public const string ColumnWhat = "What happens";
        public const string ColumnNeeds = "It needs";
        public const string ColumnGives = "It gives";
        public const string ColumnButton = "Button";

        public static readonly string[] What =
        {
            "Set up the site", "Read the data", "Review the data", "Check the rules", "Make floors and ramps", "Set the levels", "Place the villas",
            "Build the walls", "Make the report",
        };

        public static readonly string[] Needs =
        {
            "Site drawing, parameter sheet", "Your CAD, Excel and PDF files", "The list from step 2", "Confirmed plots, road levels or door levels",
            "The check from step 4", "Floors from step 5", "Levels from step 6", "Placed villas, plot edges", "The run of steps 1 to 8",
        };

        public static readonly string[] Gives =
        {
            "Coordinates, levels, types and parameters in the model",
            "One list of plots with villa type and cluster, and what was read from each file",
            "Plots, villa types and clusters you confirm",
            "Door level, ramp slope and floor split for each plot, and the issues",
            "Generic, yard and ramp floors",
            "Floor level, entry level and steps of each villa",
            "Villas on their plots at their floor level",
            "Boundary and garden walls, corner plots included",
            "Excel report the team can edit and read back",
        };

        public const string StopsTitle = "When the tool stops";
        public const string StopsLine = "It says what is missing, which step needs it, and who to ask. Fix that and press the button again.";
        public const string DryTitle = "Before you apply";
        public const string DryLine = "Use Dry run. It shows every change as a table and writes nothing to the model.";

        public const string Close = "Close";
        public const string OpenNewProject = "Open New project";
    }
}
