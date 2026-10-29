namespace SiteAdaptation.AddIn.SetUpSite
{
    // R373, 7.3 of docs/design/12-ui-spec.md. Every text the view model of the window shows. The text test holds each against
    // docs/design/14-ui-labels.md word for word, a value written <n> as the list writes it and filled by TextTemplate
    public static class SetUpSiteText
    {
        public const string Title = "1 Set up site";
        public const string BadgeEmpty = "Empty project found";
        public const string BadgeExisting = "Existing site file";
        public const string CountOf = "<n> of <n>";
        public const string Intro = "Sets the coordinates, levels and types of this model from the CAD drawings.";
        public const string WillChange = "Will change";

        public const string NowTitle = "What the project has now";
        public const string WillTitle = "What the tool will set";

        public const string SurveyPoint = "Survey point";
        public const string ProjectBasePoint = "Project base point";
        public const string Levels = "Levels";
        public const string FloorAndWallTypes = "Floor types and wall types";
        public const string SharedParameters = "Shared parameters";
        public const string Units = "Units";
        public const string CoordinateSystem = "Coordinate system";
        public const string AngleToTrueNorth = "Angle to true north";
        public const string FloorTypes = "Floor types";
        public const string RampSlope = "Ramp slope";
        public const string WallTypes = "Wall types";

        // The Floor types row always reads this, Bader 28 Oct 2026, the types come from the slopes of the dropdown below it
        public const string FloorTypesValue = "Generic 150mm, yard, Flat Ramps, and one Ramps type per slope below";
        public const string RampHelp = "Default 0.5%. Choices 0.5%, 1%, 2%, then 3% to 15%";
        public const string SheetLine = "From the parameter sheet: <n>";

        public const string PassLimit = "Pass limit <n> mm. <n> of <n> pass";
        public const string Browse = "Browse";

        // The rows of the two tables in the order of the mock, a row of another label stands after them
        public static readonly string[] NowOrder = { SurveyPoint, ProjectBasePoint, Levels, FloorAndWallTypes, SharedParameters, Units };
        public static readonly string[] WillOrder = { CoordinateSystem, SurveyPoint, AngleToTrueNorth, Levels, FloorTypes, RampSlope, WallTypes, SharedParameters, Units };
    }
}
