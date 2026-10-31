using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;
using SiteAdaptation.Common.BoundaryWalls;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.AddIn.Revit
{
    // R380. The open document seen through IWallDocument, the Revit side of button 8. It carries out what WallsRun decides and decides
    // nothing: Wall.Create on a bound line at the level with its base offset and unconnected height, a wall moved in place by its location
    // curve and its base and height parameters, and the tag of R368. The transaction is the one of the flow (R382), so nothing here starts one
    internal sealed class RevitWallDocument : IWallDocument
    {
        private readonly Document document;
        private readonly PlacedTagStore tags;

        public RevitWallDocument(Document document)
        {
            this.document = document;
            tags = new PlacedTagStore(document);
        }

        public string Title => document.Title;

        public SurveyReading Survey()
        {
            BasePoint survey = BasePoint.GetSurveyPoint(document);
            ProjectPosition position = document.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            return new SurveyReading { Position = Metres(survey.Position), SharedPosition = Metres(survey.SharedPosition), AngleToTrueNorthDeg = UnitMath.RadiansToDegrees(position.Angle) };
        }

        public IReadOnlyList<WallTypeEntry> WallTypes()
        {
            return new FilteredElementCollector(document).OfClass(typeof(WallType)).Cast<WallType>().Where(type => type.Kind == WallKind.Basic)
                .Select(type => new WallTypeEntry { Name = type.Name, Key = Key(type.Id) }).ToList();
        }

        public IReadOnlyList<LevelEntry> Levels()
        {
            return new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>()
                .Select(level => new LevelEntry { Name = level.Name, Key = Key(level.Id), ElevationM = UnitMath.FeetToMetres(level.Elevation) }).ToList();
        }

        public IReadOnlyList<WallElement> Walls()
        {
            Dictionary<string, PlacedTag> tagged = tags.Collect().GroupBy(item => item.Key.UniqueId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Value.Version).First().Value, StringComparer.Ordinal);
            var found = new List<WallElement>();
            foreach (Wall wall in new FilteredElementCollector(document).OfClass(typeof(Wall)).Cast<Wall>())
            {
                tagged.TryGetValue(wall.UniqueId, out PlacedTag tag);
                found.Add(new WallElement { UniqueId = wall.UniqueId, TypeName = wall.WallType?.Name, Tag = tag, State = Read(wall) });
            }
            return found;
        }

        public string CreateWall(double[] start, double[] end, string typeKey, string levelKey, double baseOffset, double height)
        {
            Level level = LevelOf(levelKey);
            return Wall.Create(document, LineOf(start, end, level), Id(typeKey), level.Id, UnitMath.MetresToFeet(height), UnitMath.MetresToFeet(baseOffset), false, false).UniqueId;
        }

        public void SetWall(string uniqueId, double[] start, double[] end, string levelKey, double baseOffset, double height)
        {
            Wall wall = WallOf(uniqueId);
            Level level = LevelOf(levelKey);
            if (!(wall.Location is LocationCurve location))
            {
                throw new InvalidOperationException("wall " + uniqueId + " has no location line");
            }
            location.Curve = LineOf(start, end, level);
            Set(wall, BuiltInParameter.WALL_BASE_CONSTRAINT, parameter => parameter.Set(level.Id));
            // A top constraint would hold the top to a level, so the height is set unconnected as Wall.Create makes it
            Set(wall, BuiltInParameter.WALL_HEIGHT_TYPE, parameter => parameter.Set(ElementId.InvalidElementId));
            Set(wall, BuiltInParameter.WALL_BASE_OFFSET, parameter => parameter.Set(UnitMath.MetresToFeet(baseOffset)));
            Set(wall, BuiltInParameter.WALL_USER_HEIGHT_PARAM, parameter => parameter.Set(UnitMath.MetresToFeet(height)));
        }

        public void Regenerate()
        {
            document.Regenerate();
        }

        public WallReadBack ReadBack(string uniqueId)
        {
            return Read(WallOf(uniqueId));
        }

        public void Tag(string uniqueId, PlacedTag tag)
        {
            tags.Write(WallOf(uniqueId), tag);
        }

        // The base is the elevation of the base level plus the base offset, the top the base plus the unconnected height, or the top level
        // plus its offset when the wall is held to one
        private WallReadBack Read(Wall wall)
        {
            var read = new WallReadBack();
            if (wall.Location is LocationCurve location && location.Curve != null && location.Curve.IsBound)
            {
                XYZ start = location.Curve.GetEndPoint(0), end = location.Curve.GetEndPoint(1);
                read.Start = new[] { UnitMath.FeetToMetres(start.X), UnitMath.FeetToMetres(start.Y) };
                read.End = new[] { UnitMath.FeetToMetres(end.X), UnitMath.FeetToMetres(end.Y) };
            }
            Level bottom = document.GetElement(wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)?.AsElementId() ?? ElementId.InvalidElementId) as Level;
            Level top = document.GetElement(wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.AsElementId() ?? ElementId.InvalidElementId) as Level;
            double baseFeet = (bottom?.Elevation ?? 0.0) + (wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0.0);
            double topFeet = top != null ? top.Elevation + (wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.AsDouble() ?? 0.0)
                : baseFeet + (wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0.0);
            read.Base = UnitMath.FeetToMetres(baseFeet);
            read.Top = UnitMath.FeetToMetres(topFeet);
            read.Centred = wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM)?.AsInteger() == (int)WallLocationLine.WallCenterline;
            return read;
        }

        private static void Set(Wall wall, BuiltInParameter name, Func<Parameter, bool> set)
        {
            Parameter parameter = wall.get_Parameter(name);
            if (parameter == null || parameter.IsReadOnly || !set(parameter))
            {
                throw new InvalidOperationException(name + " of wall " + wall.UniqueId + " could not be set");
            }
        }

        // The location line lies at the elevation of its level, as Wall.Create takes it
        private static Line LineOf(double[] start, double[] end, Level level)
        {
            return Line.CreateBound(new XYZ(UnitMath.MetresToFeet(start[0]), UnitMath.MetresToFeet(start[1]), level.Elevation), new XYZ(UnitMath.MetresToFeet(end[0]), UnitMath.MetresToFeet(end[1]), level.Elevation));
        }

        private Wall WallOf(string uniqueId)
        {
            return document.GetElement(uniqueId) as Wall ?? throw new InvalidOperationException("wall " + uniqueId + " is not in the document");
        }

        private Level LevelOf(string key)
        {
            return document.GetElement(Id(key)) as Level ?? throw new InvalidOperationException("level " + key + " is not in the document");
        }

        private static ElementId Id(string key) => new ElementId(long.Parse(key, CultureInfo.InvariantCulture));

        private static string Key(ElementId id) => id.Value.ToString(CultureInfo.InvariantCulture);

        private static double[] Metres(XYZ point) => new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y), UnitMath.FeetToMetres(point.Z) };
    }
}
