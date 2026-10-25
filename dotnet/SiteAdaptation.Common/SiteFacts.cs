using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SiteAdaptation.Common
{
    // R311. What the reader takes from a project for its place, as the Revit API gives it in feet and radians.
    // RevitPrototypeReader fills it and only reads, SiteFacts.Build turns it into metres and degrees
    public class RawSite
    {
        public string ActiveSite { get; set; }
        public IList<string> Sites { get; set; }
        public string PlaceName { get; set; }

        // East west, north south and elevation of the internal origin in shared coordinates, and the angle to true north
        public double[] ProjectPositionFeet { get; set; }
        public double? AngleRadians { get; set; }
        public IList<RawLevel> Levels { get; set; }

        // Document.DisplayUnitSystem is the system the project was made in and does not follow a later change of the units.
        // The unit type id of the format options of the project units now, for each spec: length, area, angle and slope
        public string UnitSystem { get; set; }
        public IDictionary<string, string> UnitTypeIds { get; set; }
        public double? LengthAccuracy { get; set; }

        // One entry per element of the document that is no element type
        public IList<RawCategory> Categories { get; set; }

        // The parts that did not read with their error. Their values stay null, so a part not read never looks empty
        public IDictionary<string, string> NotRead { get; set; }
    }

    // R311. The parts of the site read, each by the name of the site field it stands for, which is its key in notRead. Sites stands
    // for activeSite and placeName too
    public static class SitePart
    {
        public const string Sites = "sites";
        public const string ProjectPosition = "projectPosition";
        public const string Levels = "levels";
        public const string Units = "units";
        public const string Categories = "categories";

        public static readonly string[] All = { Sites, ProjectPosition, Levels, Units, Categories };
    }

    public class RawLevel
    {
        public string Name { get; set; }
        public long ElementId { get; set; }
        public double ElevationFeet { get; set; }
        public double ProjectElevationFeet { get; set; }
        public string ElevationBase { get; set; }
    }

    public class RawCategory
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class SiteFacts
    {
        public const string NoCategory = "(no category)";
        public const string NoType = "(none)";
        public const string Metric = "METRIC";
        public const string Imperial = "IMPERIAL";

        // R311. 1 mm across 5 km is 0.0000115 degrees, so 6 decimals hold the angle to true north within 0.05 mm at 5 km.
        // The 3 decimals of R2 are for lengths and levels
        public const int AngleDecimals = 6;

        // R311. The system of each unit Revit 2024 offers for the length spec, by the name UnitName gives
        private static readonly Dictionary<string, string> LengthSystems = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["meters"] = Metric,
            ["centimeters"] = Metric,
            ["millimeters"] = Metric,
            ["decimeters"] = Metric,
            ["metersCentimeters"] = Metric,
            ["feet"] = Imperial,
            ["inches"] = Imperial,
            ["feetFractionalInches"] = Imperial,
            ["fractionalInches"] = Imperial,
            ["usSurveyFeet"] = Imperial,
        };

        // The sites part stands for activeSite, sites and placeName, so each is written as null when it did not read
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string ActiveSite { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public List<string> Sites { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string PlaceName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public ProjectPositionInfo ProjectPosition { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public List<LevelFact> Levels { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public UnitFacts Units { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public List<CategoryCount> Categories { get; set; }

        // Each part that did not read and its error, by the name of its site field in SitePart. Absent when every part read
        public SortedDictionary<string, string> NotRead { get; set; }

        public static SiteFacts Build(RawSite raw)
        {
            return new SiteFacts
            {
                ActiveSite = raw.ActiveSite,
                Sites = raw.Sites?.OrderBy(name => name, StringComparer.Ordinal).ToList(),
                PlaceName = raw.PlaceName,
                ProjectPosition = Position(raw),
                Levels = raw.Levels?
                    .Select(level => new LevelFact
                    {
                        Name = level.Name,
                        ElementId = level.ElementId,
                        Elevation = UnitMath.FeetToMetres(level.ElevationFeet),
                        ProjectElevation = UnitMath.FeetToMetres(level.ProjectElevationFeet),
                        ElevationBase = level.ElevationBase,
                    })
                    .OrderBy(level => level.ProjectElevation).ThenBy(level => level.Name, StringComparer.Ordinal).ToList(),
                Units = UnitsOf(raw),
                Categories = raw.Categories?
                    .GroupBy(item => (item.Type ?? NoType) + "\u0001" + (item.Name ?? NoCategory), StringComparer.Ordinal)
                    .Select(group => new CategoryCount { Type = group.First().Type ?? NoType, Name = group.First().Name ?? NoCategory, Count = group.Count() })
                    .OrderBy(item => item.Type == NoType ? 1 : 0).ThenBy(item => item.Type, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal)
                    .ToList(),
                NotRead = raw.NotRead == null || raw.NotRead.Count == 0 ? null : new SortedDictionary<string, string>(raw.NotRead, StringComparer.Ordinal),
            };
        }

        private static ProjectPositionInfo Position(RawSite raw)
        {
            if (raw.ProjectPositionFeet == null || raw.ProjectPositionFeet.Length != 3 || !raw.AngleRadians.HasValue)
            {
                return null;
            }
            return new ProjectPositionInfo
            {
                EastWest = UnitMath.FeetToMetres(raw.ProjectPositionFeet[0]),
                NorthSouth = UnitMath.FeetToMetres(raw.ProjectPositionFeet[1]),
                Elevation = UnitMath.FeetToMetres(raw.ProjectPositionFeet[2]),
                AngleDeg = AngleDegrees(raw.AngleRadians.Value),
            };
        }

        // R311. Degrees at AngleDecimals from 0 to under 360. Rounded first, so a tiny negative angle or one within the rounding of a
        // full turn gives 0 and not 360, and never negative zero
        public static double AngleDegrees(double radians)
        {
            double degrees = Math.Round(UnitMath.RadiansToDegrees(radians), AngleDecimals, MidpointRounding.AwayFromZero) % 360.0;
            if (degrees < 0)
            {
                degrees = Math.Round(degrees + 360.0, AngleDecimals, MidpointRounding.AwayFromZero);
            }
            return degrees >= 360.0 || degrees == 0 ? 0 : degrees;
        }

        private static UnitFacts UnitsOf(RawSite raw)
        {
            if (raw.UnitSystem == null && raw.UnitTypeIds == null && !raw.LengthAccuracy.HasValue)
            {
                return null;
            }
            string length = UnitName(Id(raw, "length"));
            return new UnitFacts
            {
                System = raw.UnitSystem,
                Length = length,
                LengthSystem = LengthSystemOf(length),
                LengthAccuracy = raw.LengthAccuracy,
                Area = UnitName(Id(raw, "area")),
                Angle = UnitName(Id(raw, "angle")),
                Slope = UnitName(Id(raw, "slope")),
            };
        }

        // METRIC or IMPERIAL for a length unit name, null for a name that is none of the length units of Revit 2024
        public static string LengthSystemOf(string unitName)
        {
            string system;
            return unitName != null && LengthSystems.TryGetValue(unitName, out system) ? system : null;
        }

        private static string Id(RawSite raw, string spec)
        {
            string id;
            return raw.UnitTypeIds != null && raw.UnitTypeIds.TryGetValue(spec, out id) ? id : null;
        }

        // The unit of a type id such as autodesk.unit.unit:millimeters-1.0.1, which is millimeters. Any other text stays as it is
        public static string UnitName(string typeId)
        {
            if (typeId == null)
            {
                return null;
            }
            int colon = typeId.LastIndexOf(':');
            string name = colon >= 0 ? typeId.Substring(colon + 1) : typeId;
            int version = name.LastIndexOf('-');
            return colon >= 0 && version > 0 ? name.Substring(0, version) : name;
        }
    }

    public class ProjectPositionInfo
    {
        public double EastWest { get; set; }
        public double NorthSouth { get; set; }
        public double Elevation { get; set; }

        // R311. Written at SiteFacts.AngleDecimals, not at the 3 decimals of R2
        [JsonConverter(typeof(JsonOut.DecimalsConverter), SiteFacts.AngleDecimals)]
        public double AngleDeg { get; set; }
    }

    public class LevelFact
    {
        public string Name { get; set; }
        public long ElementId { get; set; }

        // Level.Elevation and Level.ProjectElevation in metres, as the API gives them, with the Elevation Base of the level type
        public double Elevation { get; set; }
        public double ProjectElevation { get; set; }
        public string ElevationBase { get; set; }
    }

    public class UnitFacts
    {
        // Document.DisplayUnitSystem, the system the project was made in
        public string System { get; set; }

        // The length unit of the project units now and its system, which follow a change of the units
        public string Length { get; set; }
        public string LengthSystem { get; set; }

        // R311. As Revit gives it, so a step of 0.0001 m is not rounded to 0 by R2
        [JsonConverter(typeof(JsonOut.UnroundedConverter))]
        public double? LengthAccuracy { get; set; }

        public string Area { get; set; }
        public string Angle { get; set; }
        public string Slope { get; set; }
    }

    public class CategoryCount
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
    }
}
