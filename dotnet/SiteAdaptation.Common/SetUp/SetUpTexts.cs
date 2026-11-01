using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // R373. Every text of the Set up site window Common fills, the labels of 7.3 as docs/design/14-ui-labels.md part Set up site gives
    // them. The signs, the stops and the values of the rows are worded here (CHOSEN in 5b), the list has no words for them yet
    public static class SetUpTexts
    {
        public const string Title = "1 Set up site";
        public const string SurveyPoint = "Survey point";
        public const string ProjectBasePoint = "Project base point";
        public const string Levels = "Levels";
        public const string FloorAndWallTypes = "Floor types and wall types";
        public const string SharedParameters = "Shared parameters";
        public const string Units = "Units";
        public const string CoordinateSystem = "Coordinate system";
        public const string AngleToTrueNorth = "Angle to true north";
        public const string FloorTypes = "Floor types";
        public const string FloorTypesRow = "Generic 150mm, yard, Flat Ramps, and one Ramps type per slope below";
        public const string RampSlope = "Ramp slope";
        public const string RampSlopeLine = "Default 0.5%. Choices 0.5%, 1%, 2%, then 3% to 15%";
        public const string WallTypes = "Wall types";

        public const string SignSettings = "the Settings entity SiteAdaptation_Settings";
        public const string SignPosition = "a project position other than zero";
        public const string SignSurvey = "the survey point off the internal origin";
        public const string SignBase = "the project base point off the internal origin";
        public const string NoFloorType = "The project holds no floor type to copy the floor types from";
        public const string NoWallType = "The project holds no basic wall type to copy the wall types from";
        public const string NothingDiffers = "Nothing differs, Apply changes nothing.";
        public const string AtOrigin = "at the internal origin";
        public const string SettingsNotWritten = "The Settings entity is not written yet, it joins at 5c with R367";

        // R456. A control point by its order and the plot whose corner it is
        public static string ControlPoint(int number, string plot)
        {
            return "Control point " + number.ToString(CultureInfo.InvariantCulture) + ", plot " + (plot ?? "no number");
        }

        public static string NotOnDisk(string what, string path)
        {
            return "1 Set up site needs " + what + ", the file is not on disk: " + (path ?? "not named");
        }

        public static string WillChange(int changing, int rows)
        {
            return "Will change " + changing.ToString(CultureInfo.InvariantCulture) + " of " + rows.ToString(CultureInfo.InvariantCulture) + " rows";
        }

        public static string SignParameters(IReadOnlyList<string> names)
        {
            return "shared parameters of the tool: " + string.Join(", ", names);
        }

        public static string OtherGuid(string name, string found, string sheet)
        {
            return name + ": GUID found " + found + ", GUID of the sheet " + sheet;
        }

        public static string NoGuid(string name)
        {
            return name + " has no GUID in the parameter sheet";
        }

        public static string OtherSheet(string chosen, string config)
        {
            return chosen + " is not the parameter sheet of config, " + config + ", the one every button reads. Edit " + config + " instead (R461)";
        }

        public static string NoGroup(string name)
        {
            return name + " has no Group in the parameter sheet";
        }

        public static string FloorsOwn(string name, int button)
        {
            return name + " is filled by Floors, give it Filled by " + button.ToString(CultureInfo.InvariantCulture) + " in the parameter sheet";
        }

        public static string PlotWaits(string name)
        {
            return name + " not bound, waiting on Q156";
        }

        public static string FromSheet(string source)
        {
            return "From the parameter sheet: " + source;
        }

        // The line under the check points at addin.position.revitCheckPointLimitMm, the key of its own of Q142 (W2)
        public static string PassLine(double limitMm, int passing, int count)
        {
            return "Pass limit " + limitMm.ToString("0.###", CultureInfo.InvariantCulture) + " mm. " + passing.ToString(CultureInfo.InvariantCulture) + " of " + count.ToString(CultureInfo.InvariantCulture) + " pass";
        }

        // East, north and elevation in metres to the mm
        public static string Coordinate(double[] metres)
        {
            return string.Join(", ", metres.Select(value => SetUpPlanner.Invariant(value, 3)));
        }

        public static string Number(double value, int decimals)
        {
            return Rounding.Fixed(value, decimals).ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
        }

        public static string BasePoint(BasePointState point)
        {
            bool atOrigin = SiteCoordinates.Gap(point.InternalM, new[] { 0.0, 0.0, 0.0 }) * 1000.0 <= SetUpPlanner.SameMm;
            return (atOrigin ? AtOrigin : "internal " + Coordinate(point.InternalM)) + ", shared " + Coordinate(point.SharedM);
        }

        public static string LevelsNow(IReadOnlyList<LevelState> levels)
        {
            return levels.Count == 0 ? "none" : string.Join(", ", levels.Select(level => level.Name + " " + Number(level.ElevationM, 3) + " m on " + (level.Base ?? "no base")));
        }

        public static string LevelsTarget(SetUpTarget target)
        {
            PositionLevel lowest = target.Levels[0];
            IEnumerable<string> steps = target.Levels.Skip(1).Select(level => Number(level.ProjectElevationM - lowest.ProjectElevationM, 3) + " m over " + lowest.Name);
            return string.Join(", ", target.Levels.Select(level => level.Name + " " + Number(level.ElevationM, 3) + " m").Concat(steps)) + ", on " + target.LevelBase;
        }

        // The count of each kind and which of the tool's names the project holds
        public static string TypesNow(IReadOnlyList<string> floors, IReadOnlyList<string> toolFloors, IReadOnlyList<string> walls, IReadOnlyList<string> toolWalls)
        {
            return "floor types " + Of(floors, floors.Where(toolFloors.Contains).ToList()) + ". wall types " + Of(walls, walls.Where(toolWalls.Contains).ToList());
        }

        public static string Of(IReadOnlyList<string> all, IReadOnlyList<string> ofTool)
        {
            if (all.Count == 0)
            {
                return "none";
            }
            return all.Count.ToString(CultureInfo.InvariantCulture) + ", of the tool " + (ofTool.Count == 0 ? "none" : string.Join(", ", ofTool));
        }

        public static string UnitsText(UnitState units)
        {
            return (units.Length ?? "unknown") + ", accuracy " + (units.LengthAccuracy.HasValue ? units.LengthAccuracy.Value.ToString("0.######", CultureInfo.InvariantCulture) : "unknown");
        }

        public static string CoordinateSystemText(SetUpTarget target)
        {
            if (target.Cad != null)
            {
                CadSite cad = target.Cad;
                string system = cad.Systems.Outcome == SystemAgreement.NoneStated ? "no input states a system, EPSG " + cad.Epsg.ToString(CultureInfo.InvariantCulture) + " of config"
                    : "EPSG " + cad.Epsg.ToString(CultureInfo.InvariantCulture);
                return system + ", named position " + cad.PositionName + ", origin " + Coordinate(new[] { target.Position.EastM.Value, target.Position.NorthM.Value, target.Position.ElevationM.Value })
                    + ", latitude " + Number(cad.LatitudeDeg, 7) + ", longitude " + Number(cad.LongitudeDeg, 7) + ", survey point and project base point " + Coordinate(target.BaseShared);
            }
            return "shared coordinates of CAD, site " + (target.ActiveSite ?? "Internal") + ", project position " + Coordinate(new[] { target.Position.EastM.Value, target.Position.NorthM.Value, target.Position.ElevationM.Value })
                + ", project base point " + Coordinate(target.BaseShared);
        }
    }

    // The Elevation Base of a level type, LEVEL_RELATIVE_BASE_TYPE, as the keys and the position file name it (RAN, probe of 5b)
    public static class LevelBases
    {
        public const string ProjectBasePoint = "Project Base Point";
        public const string SurveyPoint = "Survey Point";

        public static string Of(int value)
        {
            return value == 0 ? ProjectBasePoint : value == 1 ? SurveyPoint : "base " + value.ToString(CultureInfo.InvariantCulture);
        }

        // Null for a base of no known value, which Apply refuses
        public static int? Value(string name)
        {
            return name == ProjectBasePoint ? 0 : name == SurveyPoint ? 1 : (int?)null;
        }
    }
}
