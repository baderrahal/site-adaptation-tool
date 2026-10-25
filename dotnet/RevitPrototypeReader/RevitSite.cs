using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace RevitPrototypeReader
{
    // R311. The place of a project as the Revit API gives it, in feet and radians. It only reads, SiteFacts.Build in Common
    // converts and sorts. A part that does not read is one line in errors and the rest still reads
    internal static class RevitSite
    {
        public static RawSite Read(Document doc, IList<string> errors)
        {
            var raw = new RawSite();
            Guard("sites", errors, () =>
            {
                raw.ActiveSite = doc.ActiveProjectLocation.Name;
                raw.Sites = doc.ProjectLocations.Cast<ProjectLocation>().Select(location => location.Name).ToList();
                raw.PlaceName = doc.SiteLocation.PlaceName;
            });
            Guard("project position", errors, () =>
            {
                ProjectPosition position = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                raw.ProjectPositionFeet = new[] { position.EastWest, position.NorthSouth, position.Elevation };
                raw.AngleRadians = position.Angle;
            });
            Guard("levels", errors, () =>
            {
                raw.Levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().Select(level => new RawLevel
                {
                    Name = level.Name,
                    ElementId = level.Id.Value,
                    ElevationFeet = level.Elevation,
                    ProjectElevationFeet = level.ProjectElevation,
                    ElevationBase = doc.GetElement(level.GetTypeId())?.get_Parameter(BuiltInParameter.LEVEL_RELATIVE_BASE_TYPE)?.AsValueString(),
                }).ToList();
            });
            Guard("units", errors, () =>
            {
                Units units = doc.GetUnits();
                raw.UnitSystem = doc.DisplayUnitSystem.ToString();
                raw.UnitTypeIds = new Dictionary<string, string>
                {
                    ["length"] = units.GetFormatOptions(SpecTypeId.Length).GetUnitTypeId().TypeId,
                    ["area"] = units.GetFormatOptions(SpecTypeId.Area).GetUnitTypeId().TypeId,
                    ["angle"] = units.GetFormatOptions(SpecTypeId.Angle).GetUnitTypeId().TypeId,
                    ["slope"] = units.GetFormatOptions(SpecTypeId.Slope).GetUnitTypeId().TypeId,
                };
                raw.LengthAccuracy = units.GetFormatOptions(SpecTypeId.Length).Accuracy;
            });
            Guard("categories", errors, () =>
            {
                raw.Categories = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements()
                    .Select(element => element.Category)
                    .Select(category => new RawCategory { Name = category?.Name, Type = category?.CategoryType.ToString() })
                    .ToList();
            });
            return raw;
        }

        private static void Guard(string part, IList<string> errors, Action read)
        {
            try
            {
                read();
            }
            catch (Exception failure)
            {
                errors.Add("site " + part + ": " + ModelReader.Describe(failure));
            }
        }
    }
}
