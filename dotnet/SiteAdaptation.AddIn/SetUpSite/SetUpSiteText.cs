using System.Collections.Generic;
using SiteAdaptation.Common.Settings;

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
        public const string IntroFile = "Sets the coordinates, levels and types of this model from the position file.";
        public const string IntroTyped = "Sets the coordinates of this model from the values typed here, and its levels and types.";
        public const string IntroNone = "Sets the coordinates, levels and types of this model once a source is chosen.";
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
        public const string NothingDiffers = "Nothing differs, the model stays as it is. Apply marks this step done.";
        public const string NothingDiffersPlain = "Nothing differs, the model stays as it is.";

        // R441, the section Current files above the two tables, DRAFT of SCAN-UI with no board
        public const string CurrentTitle = "Current files";
        public const string CurrentHelp = "Tick each file that is the current issue. Apply waits for the site drawing.";
        public const string CurrentTick = "Current";
        public const string NoSiteDrawing = "No site drawing in the setup. CAD needs the Baseplan from the pane, or take a position file or typed values below.";
        public const string SiteDrawing = "Site drawing";
        public const string Rules = "Rules";

        // R510 to R513, the section Position source and the section CAD and position file
        public const string PositionFrom = "Position from";
        public const string Place = "Place";
        public const string TypedToPlots = "Typed origin to the plots";
        public const string SourceTitle = "Position source";
        public const string SourceHelp = "Use CAD, pick a position file or type Easting, Northing and Angle. Apply stays off until a source works.";
        public const string CurrentHelpOther = "Tick each file that is the current issue.";

        // R766, F2 of run 1. The line beside a grey Apply
        public const string DryRunFirst = "Press Dry run first. Apply turns on after it.";
        public const string TickSiteFirst = "Tick the site drawing Current under Current files. Apply turns on then.";
        public const string CheckTitleCad = "Check points from CAD";
        public const string CheckTitleFile = "Check points from the position file";
        public const string CheckTitleTyped = "Check points from the typed values";
        public const string CheckTitleNone = "Check points";
        public const string CheckColumnTyped = "Typed";
        public const string PickPositionTitle = "Pick the position file";
        public const string UseCad = "Use CAD";
        public const string PickPosition = "Pick position file";
        public const string Easting = "Easting";
        public const string Northing = "Northing";
        public const string Angle = "Angle";
        public const string UseTyped = "Use typed values";
        public const string CompareTitle = "CAD and position file";
        public const string CompareHelp = "Only the source above is set. Easting and Northing differ by the choice of origin. File origin to the plots tells if both are in one frame.";
        public const string CompareValue = "Value";
        public const string CompareCad = "CAD";
        public const string CompareFile = "Position file";
        public const string CompareDifference = "Difference";

        // Each file of the setup by its input, the names of the pane rows
        public static readonly IReadOnlyDictionary<string, string> InputLabels = new Dictionary<string, string>
        {
            [InputNames.Baseplan] = "Baseplan", [InputNames.VillaBlocks] = "Villa blocks", [InputNames.Curbs] = "Plot fronts (curb lines)", [InputNames.RoadSurface] = "Road surface", [InputNames.Dlus] = "DLUS schedule",
            [InputNames.Sales] = "Sales schedule", [InputNames.PlotNumbers] = "Plot numbers", [InputNames.ParameterSheet] = "Parameter sheet",
        };

        // The rows of the two tables in the order of the mock, a row of another label stands after them
        public static readonly string[] NowOrder = { SurveyPoint, ProjectBasePoint, Levels, FloorAndWallTypes, SharedParameters, Units };
        public static readonly string[] WillOrder = { PositionFrom, Place, TypedToPlots, CoordinateSystem, SurveyPoint, AngleToTrueNorth, Levels, FloorTypes, RampSlope, WallTypes, SharedParameters, Units };
    }
}
