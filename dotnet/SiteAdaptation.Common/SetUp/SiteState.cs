using System.Collections.Generic;

namespace SiteAdaptation.Common.SetUp
{
    // R373, R374. What the document holds now for button 1, in metres and degrees, read by the Revit side and judged here: the units,
    // the project position, the survey point and the project base point, the levels, the floor and wall type names, the parameters and
    // whether the Settings entity of SiteAdaptation_Settings is there
    public sealed class SiteState
    {
        public UnitState Units { get; set; } = new UnitState();

        // The shared position of the internal origin and the angle to true north
        public SitePosition Position { get; set; } = new SitePosition { EastM = 0, NorthM = 0, ElevationM = 0, AngleDeg = 0 };

        public BasePointState Survey { get; set; } = new BasePointState();

        public BasePointState ProjectBase { get; set; } = new BasePointState();

        public List<LevelState> Levels { get; set; } = new List<LevelState>();

        public List<string> FloorTypes { get; set; } = new List<string>();

        public List<string> WallTypes { get; set; } = new List<string>();

        public List<ParameterState> SharedParameters { get; set; } = new List<ParameterState>();

        // The bindings whose definition is no shared parameter, which hold no GUID
        public List<string> ProjectParameters { get; set; } = new List<string>();

        public bool SettingsEntity { get; set; }

        // R455. The name of the active named position and the site location, latitude and longitude in degrees, null where not read
        public string PositionName { get; set; }
        public double? LatitudeDeg { get; set; }
        public double? LongitudeDeg { get; set; }
        public string PlaceName { get; set; }
    }

    // The unit names as SiteFacts.UnitName gives them from the type id, such as meters, squareMeters, degrees
    public sealed class UnitState
    {
        public string System { get; set; }
        public string Length { get; set; }
        public double? LengthAccuracy { get; set; }
        public string Area { get; set; }
        public string Angle { get; set; }
        public string Slope { get; set; }
    }

    public sealed class BasePointState
    {
        public double[] InternalM { get; set; } = { 0, 0, 0 };
        public double[] SharedM { get; set; } = { 0, 0, 0 };
        public bool Clipped { get; set; }
    }

    // Elevation is measured from the Elevation Base of the level type, ProjectElevation from the internal origin (03a step 3)
    public sealed class LevelState
    {
        public string Name { get; set; }
        public double ElevationM { get; set; }
        public double ProjectElevationM { get; set; }
        public string Base { get; set; }
    }

    // A SharedParameterElement with its binding, Categories empty when it is not bound
    public sealed class ParameterState
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public List<string> Categories { get; set; } = new List<string>();

        public bool Bound
        {
            get { return Categories.Count > 0; }
        }
    }
}
