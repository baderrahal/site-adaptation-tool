using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace RevitPrototypeReader
{
    // R311. The place of a project as the Revit API gives it, in feet and radians. It only reads, SiteFacts.Build in Common
    // converts and sorts. A part that does not read is one line in errors and in NotRead under the name of its site field in
    // SitePart, its values stay null and the rest still reads. Each part sets its values only once all of them read, so a part is
    // never half filled
    internal static class RevitSite
    {
        public static RawSite Read(Document doc, IList<string> errors)
        {
            var raw = new RawSite();
            Guard(SitePart.Sites, raw, errors, () =>
            {
                string active = doc.ActiveProjectLocation.Name;
                List<string> sites = doc.ProjectLocations.Cast<ProjectLocation>().Select(location => location.Name).ToList();
                string place = doc.SiteLocation.PlaceName;
                raw.ActiveSite = active;
                raw.Sites = sites;
                raw.PlaceName = place;
            });
            Guard(SitePart.ProjectPosition, raw, errors, () =>
            {
                ProjectPosition position = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                raw.ProjectPositionFeet = new[] { position.EastWest, position.NorthSouth, position.Elevation };
                raw.AngleRadians = position.Angle;
            });
            Guard(SitePart.Levels, raw, errors, () =>
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
            Guard(SitePart.Units, raw, errors, () =>
            {
                // DisplayUnitSystem is the system the project was made in. The format options of the project units are what it has now
                Units units = doc.GetUnits();
                string system = doc.DisplayUnitSystem.ToString();
                var ids = new Dictionary<string, string>
                {
                    ["length"] = units.GetFormatOptions(SpecTypeId.Length).GetUnitTypeId().TypeId,
                    ["area"] = units.GetFormatOptions(SpecTypeId.Area).GetUnitTypeId().TypeId,
                    ["angle"] = units.GetFormatOptions(SpecTypeId.Angle).GetUnitTypeId().TypeId,
                    ["slope"] = units.GetFormatOptions(SpecTypeId.Slope).GetUnitTypeId().TypeId,
                };
                double accuracy = units.GetFormatOptions(SpecTypeId.Length).Accuracy;
                raw.UnitSystem = system;
                raw.UnitTypeIds = ids;
                raw.LengthAccuracy = accuracy;
            });
            Guard(SitePart.Categories, raw, errors, () =>
            {
                raw.Categories = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements()
                    .Select(element => element.Category)
                    .Select(category => new RawCategory { Name = category?.Name, Type = category?.CategoryType.ToString() })
                    .ToList();
            });
            return raw;
        }

        private static void Guard(string part, RawSite raw, IList<string> errors, Action read)
        {
            try
            {
                read();
            }
            catch (Exception failure)
            {
                string reason = ModelReader.Describe(failure);
                errors.Add("site " + part + ": " + reason);
                raw.NotRead = raw.NotRead ?? new Dictionary<string, string>();
                raw.NotRead[part] = reason;
            }
        }
    }
}
