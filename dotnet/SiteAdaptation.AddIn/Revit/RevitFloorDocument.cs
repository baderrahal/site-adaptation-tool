using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.AddIn.Revit
{
    // R378, R379. The open document seen through IFloorDocument, the Revit side of buttons 5 and 6. It carries out what FloorsRun and
    // LevelsRun decide and decides nothing, as RevitFloorTarget of the placer does for R113 to R115, whose steps it takes over: Floor.Create
    // with the loops, the type and the level, an arc kept an arc, and a ramp made flat at its lowest corner with each corner vertex of
    // SlabShapeEditor lifted by ModifySubElement. The transaction is the one of the flow (R382), so nothing here starts one
    internal sealed class RevitFloorDocument : IFloorDocument
    {
        private readonly Document document;
        private readonly PlacedTagStore tags;

        // The plan position of each corner vertex ShapeCorners gave, so a vertex is found again by where it is and no vertex object is kept
        private readonly Dictionary<string, List<XYZ>> corners = new Dictionary<string, List<XYZ>>(StringComparer.Ordinal);

        public RevitFloorDocument(Document document)
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

        public IReadOnlyList<FloorTypeEntry> FloorTypes()
        {
            return new FilteredElementCollector(document).OfClass(typeof(FloorType)).Cast<FloorType>().Select(type => new FloorTypeEntry { Name = type.Name, Key = Key(type.Id) }).ToList();
        }

        public IReadOnlyList<LevelEntry> Levels()
        {
            return new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>()
                .Select(level => new LevelEntry { Name = level.Name, Key = Key(level.Id), ElevationM = UnitMath.FeetToMetres(level.Elevation) }).ToList();
        }

        // R114. The plot parameter is the one floorSettings names, read by name as the placer reads it
        public IReadOnlyList<FloorElement> Floors(string plotParameter)
        {
            Dictionary<string, PlacedTag> tagged = tags.Collect().GroupBy(item => item.Key.UniqueId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Value.Version).First().Value, StringComparer.Ordinal);
            var found = new List<FloorElement>();
            foreach (Floor floor in new FilteredElementCollector(document).OfClass(typeof(Floor)).Cast<Floor>())
            {
                Element type = document.GetElement(floor.GetTypeId());
                Parameter plot = plotParameter == null ? null : floor.LookupParameter(plotParameter);
                tagged.TryGetValue(floor.UniqueId, out PlacedTag tag);
                found.Add(new FloorElement
                {
                    UniqueId = floor.UniqueId, TypeName = type?.Name, Tag = tag,
                    PlotValue = plot == null || !plot.HasValue || plot.StorageType != StorageType.String ? null : plot.AsString(),
                    Vertices = SketchVertices(floor), State = Read(floor),
                });
            }
            return found;
        }

        public string CreateFloor(IList<FloorLoop> loops, string typeKey, string levelKey)
        {
            var profile = new List<CurveLoop>();
            foreach (FloorLoop loop in loops)
            {
                var curves = new CurveLoop();
                foreach (FloorSegment segment in loop.Segments)
                {
                    curves.Append(segment.Mid == null ? (Curve)Line.CreateBound(Feet(segment.Start), Feet(segment.End)) : Arc.Create(Feet(segment.Start), Feet(segment.End), Feet(segment.Mid)));
                }
                profile.Add(curves);
            }
            return Floor.Create(document, profile, Id(typeKey), Id(levelKey)).UniqueId;
        }

        public void SetHeightOffset(string uniqueId, double metres)
        {
            Parameter offset = FloorOf(uniqueId).get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            if (offset == null || offset.IsReadOnly || !offset.Set(UnitMath.MetresToFeet(metres)))
            {
                throw new InvalidOperationException("the height offset of floor " + uniqueId + " could not be set");
            }
        }

        public void SetType(string uniqueId, string typeKey)
        {
            Floor floor = FloorOf(uniqueId);
            ElementId type = Id(typeKey);
            if (floor.GetTypeId() != type)
            {
                floor.ChangeTypeId(type);
            }
        }

        public void Regenerate()
        {
            document.Regenerate();
        }

        public IList<double[]> ShapeCorners(string uniqueId)
        {
            Floor floor = FloorOf(uniqueId);
            SlabShapeEditor editor = floor.GetSlabShapeEditor() ?? throw new InvalidOperationException("floor " + uniqueId + " has no slab shape editor");
            // A ramp set again by button 5 or 6 starts from flat, so no lift of an earlier run stays under the new one
            if (editor.IsEnabled)
            {
                editor.ResetSlabShape();
            }
            editor.Enable();
            List<SlabShapeVertex> found = CornerVertices(editor);
            if (found.Count == 0)
            {
                // The vertices of a floor just made can wait for a regeneration
                document.Regenerate();
                found = CornerVertices(floor.GetSlabShapeEditor());
            }
            corners[uniqueId] = found.Select(vertex => vertex.Position).ToList();
            return found.Select(vertex => Metres(vertex.Position)).ToList();
        }

        public void SetCornerOffset(string uniqueId, int corner, double metres)
        {
            SlabShapeEditor editor = FloorOf(uniqueId).GetSlabShapeEditor();
            XYZ at = corners[uniqueId][corner];
            SlabShapeVertex vertex = null;
            double best = double.MaxValue;
            foreach (SlabShapeVertex candidate in editor.SlabShapeVertices)
            {
                double gap = Math.Sqrt(Math.Pow(candidate.Position.X - at.X, 2) + Math.Pow(candidate.Position.Y - at.Y, 2));
                if (candidate.VertexType == SlabShapeVertexType.Corner && gap < best)
                {
                    best = gap;
                    vertex = candidate;
                }
            }
            if (vertex == null || UnitMath.FeetToMetres(best) > FloorPlacer.CornerOnVertex)
            {
                throw new InvalidOperationException("corner " + corner + " of floor " + uniqueId + " is no longer a shape vertex");
            }
            editor.ModifySubElement(vertex, UnitMath.MetresToFeet(metres));
        }

        public FloorReadBack ReadBack(string uniqueId)
        {
            return Read(FloorOf(uniqueId));
        }

        public ParameterWrite WriteText(string uniqueId, string name, string value)
        {
            Parameter parameter = FloorOf(uniqueId).LookupParameter(name);
            if (parameter == null)
            {
                return ParameterWrite.Missing;
            }
            if (parameter.IsReadOnly)
            {
                return ParameterWrite.ReadOnly;
            }
            if (parameter.StorageType != StorageType.String)
            {
                return ParameterWrite.NotText;
            }
            return parameter.Set(value) ? ParameterWrite.Written : ParameterWrite.Refused;
        }

        public void Tag(string uniqueId, PlacedTag tag)
        {
            tags.Write(FloorOf(uniqueId), tag);
        }

        private static FloorReadBack Read(Floor floor)
        {
            BoundingBoxXYZ box = floor.get_BoundingBox(null);
            var read = new FloorReadBack { TopMax = box == null ? (double?)null : UnitMath.FeetToMetres(box.Max.Z) };
            SlabShapeEditor editor = floor.GetSlabShapeEditor();
            if (editor != null && editor.IsEnabled)
            {
                read.Corners = CornerVertices(editor).Select(vertex => Metres(vertex.Position)).ToList();
            }
            return read;
        }

        // The start of every curve of every loop of the sketch, so the outline is held by its corners as the plan gives them
        private IReadOnlyList<double[]> SketchVertices(Floor floor)
        {
            var points = new List<double[]>();
            if (!(document.GetElement(floor.SketchId) is Sketch sketch))
            {
                return points;
            }
            foreach (CurveArray loop in sketch.Profile)
            {
                foreach (Curve curve in loop)
                {
                    XYZ start = curve.GetEndPoint(0);
                    points.Add(new[] { UnitMath.FeetToMetres(start.X), UnitMath.FeetToMetres(start.Y) });
                }
            }
            return points;
        }

        private static List<SlabShapeVertex> CornerVertices(SlabShapeEditor editor)
        {
            var found = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
            {
                if (vertex.VertexType == SlabShapeVertexType.Corner)
                {
                    found.Add(vertex);
                }
            }
            return found;
        }

        private Floor FloorOf(string uniqueId)
        {
            return document.GetElement(uniqueId) as Floor ?? throw new InvalidOperationException("floor " + uniqueId + " is not in the document");
        }

        private static ElementId Id(string key) => new ElementId(long.Parse(key, CultureInfo.InvariantCulture));

        private static string Key(ElementId id) => id.Value.ToString(CultureInfo.InvariantCulture);

        private static double[] Metres(XYZ point) => new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y), UnitMath.FeetToMetres(point.Z) };

        private static XYZ Feet(double[] metres) => new XYZ(UnitMath.MetresToFeet(metres[0]), UnitMath.MetresToFeet(metres[1]), UnitMath.MetresToFeet(metres[2]));
    }
}
