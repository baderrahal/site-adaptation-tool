using System;
using System.Collections.Generic;
using System.Linq;

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

        // DisplayUnitSystem, and the unit type id of the format options of each spec: length, area, angle and slope
        public string UnitSystem { get; set; }
        public IDictionary<string, string> UnitTypeIds { get; set; }
        public double? LengthAccuracy { get; set; }

        // One entry per element of the document that is no element type
        public IList<RawCategory> Categories { get; set; }
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

        public string ActiveSite { get; set; }
        public List<string> Sites { get; set; }
        public string PlaceName { get; set; }
        public ProjectPositionInfo ProjectPosition { get; set; }
        public List<LevelFact> Levels { get; set; }
        public UnitFacts Units { get; set; }
        public List<CategoryCount> Categories { get; set; }

        public static SiteFacts Build(RawSite raw)
        {
            var facts = new SiteFacts
            {
                ActiveSite = raw.ActiveSite,
                Sites = (raw.Sites ?? new List<string>()).OrderBy(name => name, StringComparer.Ordinal).ToList(),
                PlaceName = raw.PlaceName,
                Levels = (raw.Levels ?? new List<RawLevel>())
                    .Select(level => new LevelFact
                    {
                        Name = level.Name,
                        ElementId = level.ElementId,
                        Elevation = UnitMath.FeetToMetres(level.ElevationFeet),
                        ProjectElevation = UnitMath.FeetToMetres(level.ProjectElevationFeet),
                        ElevationBase = level.ElevationBase,
                    })
                    .OrderBy(level => level.ProjectElevation).ThenBy(level => level.Name, StringComparer.Ordinal).ToList(),
                Units = new UnitFacts
                {
                    System = raw.UnitSystem,
                    Length = UnitName(Id(raw, "length")),
                    LengthAccuracy = raw.LengthAccuracy,
                    Area = UnitName(Id(raw, "area")),
                    Angle = UnitName(Id(raw, "angle")),
                    Slope = UnitName(Id(raw, "slope")),
                },
                Categories = (raw.Categories ?? new List<RawCategory>())
                    .GroupBy(item => (item.Type ?? NoType) + "\u0001" + (item.Name ?? NoCategory), StringComparer.Ordinal)
                    .Select(group => new CategoryCount { Type = group.First().Type ?? NoType, Name = group.First().Name ?? NoCategory, Count = group.Count() })
                    .OrderBy(item => item.Type == NoType ? 1 : 0).ThenBy(item => item.Type, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal)
                    .ToList(),
            };
            if (raw.ProjectPositionFeet != null && raw.ProjectPositionFeet.Length == 3 && raw.AngleRadians.HasValue)
            {
                double degrees = UnitMath.RadiansToDegrees(raw.AngleRadians.Value) % 360.0;
                facts.ProjectPosition = new ProjectPositionInfo
                {
                    EastWest = UnitMath.FeetToMetres(raw.ProjectPositionFeet[0]),
                    NorthSouth = UnitMath.FeetToMetres(raw.ProjectPositionFeet[1]),
                    Elevation = UnitMath.FeetToMetres(raw.ProjectPositionFeet[2]),
                    AngleDeg = degrees < 0 ? degrees + 360.0 : degrees,
                };
            }
            return facts;
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
        public string System { get; set; }
        public string Length { get; set; }
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
