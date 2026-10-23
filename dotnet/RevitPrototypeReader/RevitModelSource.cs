using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace RevitPrototypeReader
{
    // One Revit document seen through IModelSource. It only reads. A single bad element is skipped and noted in Errors
    internal sealed class RevitModelSource : IModelSource
    {
        private readonly Document document;

        // Survey point shared position minus position on each axis, in metres. Null when it could not be read
        private readonly double[] sharedOffset;

        // Failures of the new floor measurements, kept by reason so a failure on a thousand floors is one line
        private readonly SortedDictionary<string, List<long?>> failures = new SortedDictionary<string, List<long?>>(StringComparer.Ordinal);

        private const char FailureSplit = '\u0001';

        public RevitModelSource(Document document)
        {
            this.document = document;
            Errors = new List<string>();
            sharedOffset = ReadSharedOffset();
        }

        public IList<string> Errors { get; }

        private double[] ReadSharedOffset()
        {
            try
            {
                BasePoint survey = BasePoint.GetSurveyPoint(document);
                return SharedCoordinates.Offset(RevitExtractor.Metres(survey.Position), RevitExtractor.Metres(survey.SharedPosition));
            }
            catch (Exception failure)
            {
                Errors.Add("shared coordinates: survey point not read, the shared fields are left out: " + ModelReader.Describe(failure));
                return null;
            }
        }

        private double[] Shared(XYZ point)
        {
            return sharedOffset == null ? null : SharedCoordinates.ToShared(new[] { point.X, point.Y, point.Z }, sharedOffset);
        }

        private void Fail(string label, long id, string detail)
        {
            string key = label + FailureSplit + detail;
            List<long?> ids;
            if (!failures.TryGetValue(key, out ids))
            {
                ids = new List<long?>();
                failures[key] = ids;
            }
            ids.Add(id);
        }

        private void FlushFailures()
        {
            foreach (KeyValuePair<string, List<long?>> item in failures)
            {
                string[] parts = item.Key.Split(FailureSplit);
                Errors.Add(ErrorLines.Grouped(parts[0], parts[1], item.Value));
            }
            failures.Clear();
        }

        // A failure of one measurement is noted by reason and leaves the other measurements of the element alone
        private T Guarded<T>(string label, Element element, Func<T> read)
        {
            try
            {
                return read();
            }
            catch (Exception failure)
            {
                Fail(label, element.Id.Value, ModelReader.Describe(failure));
                return default(T);
            }
        }

        public IEnumerable<SourceLink> Links()
        {
            var links = new List<SourceLink>();
            foreach (RevitLinkInstance instance in new FilteredElementCollector(document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                try
                {
                    links.Add(ReadLink(instance));
                }
                catch (Exception failure)
                {
                    Errors.Add("link " + instance.Id.Value + ": " + ModelReader.Describe(failure));
                }
            }
            return links.OrderBy(link => link.File, StringComparer.OrdinalIgnoreCase).ThenBy(link => link.Name, StringComparer.Ordinal).ToList();
        }

        // The name, the parameters and the transform sit on the link instance, so none of them needs the link to be loaded.
        // A failure on one of them is kept as a problem line and the link stays in the list
        private SourceLink ReadLink(RevitLinkInstance instance)
        {
            var problems = new List<string>();
            var link = new SourceLink { File = LinkFileName(instance), Problems = problems, Open = () => OpenLink(instance), ElementId = instance.Id.Value };
            try
            {
                Transform transform = instance.GetTotalTransform();
                link.Transform = LinkMath.Describe(RevitExtractor.Metres(transform.Origin), Axis(transform.BasisX), Axis(transform.BasisY), Axis(transform.BasisZ));
            }
            catch (Exception failure)
            {
                problems.Add("transform: " + ModelReader.Describe(failure));
            }
            ReadLinkState(instance, link, problems);
            try
            {
                link.Name = instance.Name;
            }
            catch (Exception failure)
            {
                problems.Add("name: " + ModelReader.Describe(failure));
            }
            try
            {
                link.Parameters = AllParameters(instance, problems);
            }
            catch (Exception failure)
            {
                problems.Add("parameters: " + ModelReader.Describe(failure));
            }
            try
            {
                link.ParameterDetails = Details(instance, problems);
            }
            catch (Exception failure)
            {
                problems.Add("parameter details: " + ModelReader.Describe(failure));
            }
            return link;
        }

        // Whether the link is loaded and, when it is, where it sits in the host. Only the link instance is asked,
        // nothing inside the linked file is read
        private void ReadLinkState(RevitLinkInstance instance, SourceLink link, IList<string> problems)
        {
            try
            {
                var type = document.GetElement(instance.GetTypeId()) as RevitLinkType;
                if (type == null)
                {
                    problems.Add("status: the link type was not found");
                    return;
                }
                LinkedFileStatus status = type.GetLinkedFileStatus();
                link.Status = status.ToString();
                link.Loaded = status == LinkedFileStatus.Loaded;
            }
            catch (Exception failure)
            {
                problems.Add("status: " + ModelReader.Describe(failure));
                return;
            }
            if (link.Loaded != true || sharedOffset == null)
            {
                return;
            }
            try
            {
                BoundingBoxXYZ box = instance.get_BoundingBox(null);
                if (box == null)
                {
                    problems.Add("bounding box: the API gives none");
                }
                else
                {
                    link.BoxCorners = BoxMath.Corners(Shared(box.Min), Shared(box.Max));
                }
            }
            catch (Exception failure)
            {
                problems.Add("bounding box: " + ModelReader.Describe(failure));
            }
            try
            {
                Transform transform = instance.GetTotalTransform();
                double[] origin = SharedCoordinates.Shift(RevitExtractor.Metres(transform.Origin), sharedOffset);
                link.TransformShared = LinkMath.Describe(origin, Axis(transform.BasisX), Axis(transform.BasisY), Axis(transform.BasisZ));
            }
            catch (Exception failure)
            {
                problems.Add("shared transform: " + ModelReader.Describe(failure));
            }
        }

        public IEnumerable<PrototypeRow> Instances()
        {
            var rows = new List<PrototypeRow>();
            foreach (FamilyInstance instance in new FilteredElementCollector(document).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
            {
                try
                {
                    FamilySymbol symbol = instance.Symbol;
                    if (symbol == null)
                    {
                        continue;
                    }
                    PrototypeRow row = RevitExtractor.PlaceRow(document, symbol, instance);
                    row.Mirrored = instance.Mirrored;
                    row.Parameters = ParameterBag.Merge(AllParameters(instance), AllParameters(symbol));
                    rows.Add(row);
                }
                catch (Exception failure)
                {
                    Errors.Add("instance " + instance.Id.Value + ": " + ModelReader.Describe(failure));
                }
            }
            return rows.OrderBy(row => row.FamilyName, StringComparer.Ordinal).ThenBy(row => row.TypeName, StringComparer.Ordinal).ThenBy(row => row.ElementId).ToList();
        }

        public IEnumerable<WallRow> Walls()
        {
            var rows = new List<WallRow>();
            foreach (Wall wall in new FilteredElementCollector(document).OfClass(typeof(Wall)).Cast<Wall>())
            {
                try
                {
                    var byCurve = wall.Location as LocationCurve;
                    Curve curve = byCurve == null ? null : byCurve.Curve;
                    rows.Add(new WallRow
                    {
                        TypeName = TypeNameOf(wall),
                        ElementId = wall.Id.Value,
                        Location = LocationOf(wall),
                        Length = curve != null ? UnitMath.FeetToMetres(curve.Length) : MetresOf(wall, BuiltInParameter.CURVE_ELEM_LENGTH),
                        Height = MetresOf(wall, BuiltInParameter.WALL_USER_HEIGHT_PARAM),
                        Parameters = AllParameters(wall),
                    });
                }
                catch (Exception failure)
                {
                    Errors.Add("wall " + wall.Id.Value + ": " + ModelReader.Describe(failure));
                }
            }
            return rows.OrderBy(row => row.TypeName, StringComparer.Ordinal).ThenBy(row => row.ElementId).ToList();
        }

        public IEnumerable<FloorRow> Floors()
        {
            var rows = new List<FloorRow>();
            foreach (Floor floor in new FilteredElementCollector(document).OfClass(typeof(Floor)).Cast<Floor>())
            {
                try
                {
                    rows.Add(ReadFloor(floor));
                }
                catch (Exception failure)
                {
                    Errors.Add("floor " + floor.Id.Value + ": " + ModelReader.Describe(failure));
                }
            }
            FlushFailures();
            return rows.OrderBy(row => row.TypeName, StringComparer.Ordinal).ThenBy(row => row.ElementId).ToList();
        }

        // Each floor type used in this document, once, with every type parameter
        public IEnumerable<FloorTypeRow> FloorTypes()
        {
            var rows = new List<FloorTypeRow>();
            var seen = new HashSet<long>();
            foreach (Floor floor in new FilteredElementCollector(document).OfClass(typeof(Floor)).Cast<Floor>())
            {
                long typeId = floor.GetTypeId().Value;
                if (!seen.Add(typeId))
                {
                    continue;
                }
                try
                {
                    Element type = document.GetElement(floor.GetTypeId());
                    if (type == null)
                    {
                        continue;
                    }
                    var problems = new List<string>();
                    rows.Add(new FloorTypeRow { TypeId = typeId, Name = type.Name, ParameterDetails = Details(type, problems) });
                    foreach (string problem in problems)
                    {
                        Errors.Add("floor type " + typeId + ": " + problem);
                    }
                }
                catch (Exception failure)
                {
                    Errors.Add("floor type " + typeId + ": " + ModelReader.Describe(failure));
                }
            }
            return rows.OrderBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.TypeId).ToList();
        }

        private FloorRow ReadFloor(Floor floor)
        {
            var level = document.GetElement(floor.LevelId) as Level;
            double? offset = MetresOf(floor, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            var row = new FloorRow
            {
                TypeName = TypeNameOf(floor),
                ElementId = floor.Id.Value,
                Level = level == null ? null : level.Name,
                HeightOffset = offset,
                // Internal coordinates like every other point in the file. The offset is to the top of the floor
                TopElevation = level != null && offset.HasValue ? UnitMath.FeetToMetres(level.Elevation) + offset.Value : (double?)null,
                Thickness = ThicknessOf(floor),
                Area = AreaOf(floor),
                Parameters = AllParameters(floor),
            };
            List<double[]> boundary;
            List<FloorArc> arcs;
            List<double[]> ends;
            if (FloorMath.TryOuter(BoundaryLoops(floor), out boundary, out arcs, out ends))
            {
                row.SketchBoundary = boundary;
                row.Arcs = arcs.Count > 0 ? arcs : null;
                row.BoundaryEnds = ends;
            }
            else
            {
                Errors.Add("floor " + floor.Id.Value + ": no boundary found");
            }
            row.Shape = ShapeOf(floor);
            row.Slope = SlopeOf(floor);
            Measure(floor, level, row);
            return row;
        }

        // The measurements added for the relation study. Each one is read on its own, so a failure costs that one field
        private void Measure(Floor floor, Level level, FloorRow row)
        {
            row.TypeId = floor.GetTypeId().Value;
            if (level != null && sharedOffset != null)
            {
                row.LevelElevation = UnitMath.FeetToMetres(level.Elevation) + sharedOffset[2];
            }
            row.Workset = Guarded("floor workset", floor, () => WorksetOf(floor));
            row.PhaseCreated = Guarded("floor phase created", floor, () => PhaseOf(floor));
            row.DesignOption = Guarded("floor design option", floor, () => floor.DesignOption == null ? null : floor.DesignOption.Name);
            row.ParameterDetails = Guarded("floor parameter details", floor, () => DetailsOf(floor));
            if (sharedOffset == null)
            {
                return;
            }
            row.BoundingBox = Guarded("floor bounding box", floor, () => SharedBox(floor));
            List<BoundaryEdge> top = Guarded("floor top faces", floor, () => TopEdges(floor));
            if (top != null && top.Count > 0)
            {
                row.TopElevationRange = ElevationRange.Of(PointsOf(top));
                int open;
                List<List<double[]>> loops = OutlineLoops.Build(top, out open);
                row.Outline = loops.Count > 0 ? loops : null;
                if (open > 0)
                {
                    Fail("floor outline", floor.Id.Value, open + " edge chain(s) did not close");
                }
            }
            List<BoundaryEdge> bottom = Guarded("floor solid bottom faces", floor, () => BottomEdges(floor));
            if (bottom != null && bottom.Count > 0)
            {
                row.BottomElevationRange = ElevationRange.Of(PointsOf(bottom));
            }
        }

        private List<ParameterDetail> DetailsOf(Element element)
        {
            var problems = new List<string>();
            List<ParameterDetail> details = Details(element, problems);
            foreach (string problem in problems)
            {
                Fail("floor parameter details", element.Id.Value, problem);
            }
            return details;
        }

        private string WorksetOf(Element element)
        {
            if (!document.IsWorkshared)
            {
                return null;
            }
            Workset workset = document.GetWorksetTable().GetWorkset(element.WorksetId);
            return workset == null ? null : workset.Name;
        }

        private string PhaseOf(Element element)
        {
            if (element.CreatedPhaseId == ElementId.InvalidElementId)
            {
                return null;
            }
            Element phase = document.GetElement(element.CreatedPhaseId);
            return phase == null ? null : phase.Name;
        }

        private BoxInfo SharedBox(Element element)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            return box == null ? null : new BoxInfo { Min = Shared(box.Min), Max = Shared(box.Max) };
        }

        // The edges of the real top faces of the floor. A floor with shape editing has several of them
        private List<BoundaryEdge> TopEdges(Floor floor)
        {
            var edges = new List<BoundaryEdge>();
            foreach (Reference reference in HostObjectUtils.GetTopFaces(floor))
            {
                var face = floor.GetGeometryObjectFromReference(reference) as Face;
                if (face != null)
                {
                    edges.AddRange(FaceEdges(face));
                }
            }
            return edges;
        }

        // The faces of the solid that look down. A side face is vertical and is left out
        private List<BoundaryEdge> BottomEdges(Floor floor)
        {
            var edges = new List<BoundaryEdge>();
            GeometryElement geometry = floor.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine });
            if (geometry == null)
            {
                return edges;
            }
            foreach (GeometryObject item in geometry)
            {
                var solid = item as Solid;
                if (solid == null || solid.Faces.IsEmpty || solid.Volume <= 0)
                {
                    continue;
                }
                foreach (Face face in solid.Faces)
                {
                    if (FloorMath.IsBottomFace(NormalZ(face)))
                    {
                        edges.AddRange(FaceEdges(face));
                    }
                }
            }
            return edges;
        }

        private static double NormalZ(Face face)
        {
            var planar = face as PlanarFace;
            if (planar != null)
            {
                return planar.FaceNormal.Z;
            }
            BoundingBoxUV box = face.GetBoundingBox();
            return face.ComputeNormal(new UV((box.Min.U + box.Max.U) / 2, (box.Min.V + box.Max.V) / 2)).Z;
        }

        private IEnumerable<BoundaryEdge> FaceEdges(Face face)
        {
            var edges = new List<BoundaryEdge>();
            foreach (CurveLoop loop in face.GetEdgesAsCurveLoops())
            {
                foreach (Curve curve in loop)
                {
                    edges.Add(SharedEdge(curve));
                }
            }
            return edges;
        }

        private BoundaryEdge SharedEdge(Curve curve)
        {
            var edge = new BoundaryEdge { Start = Shared(curve.GetEndPoint(0)), End = Shared(curve.GetEndPoint(1)) };
            if (!(curve is Line))
            {
                IList<XYZ> points = curve.Tessellate();
                edge.Via = points.Skip(1).Take(Math.Max(0, points.Count - 2)).Select(Shared).ToList();
            }
            return edge;
        }

        private static IEnumerable<double[]> PointsOf(IEnumerable<BoundaryEdge> edges)
        {
            foreach (BoundaryEdge edge in edges)
            {
                yield return edge.Start;
                if (edge.Via != null)
                {
                    foreach (double[] via in edge.Via)
                    {
                        yield return via;
                    }
                }
                yield return edge.End;
            }
        }

        // Slab shape editing gives the real corner levels. The editor is null for a floor that is not flat and horizontal,
        // and a failure is noted and leaves the floor to the boundary fallback
        private ShapeEditing ShapeOf(Floor floor)
        {
            try
            {
                SlabShapeEditor editor = floor.GetSlabShapeEditor();
                if (editor == null || !editor.IsEnabled)
                {
                    return new ShapeEditing { Enabled = false };
                }
                var shape = new ShapeEditing { Enabled = true, Corners = new List<double[]>(), Others = new List<double[]>() };
                foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
                {
                    double[] point = RevitExtractor.Metres(vertex.Position);
                    (vertex.VertexType == SlabShapeVertexType.Corner ? shape.Corners : shape.Others).Add(point);
                }
                return shape;
            }
            catch (Exception failure)
            {
                Errors.Add("floor " + floor.Id.Value + " shape editing: " + ModelReader.Describe(failure));
                return null;
            }
        }

        // The Slope parameter as Revit holds it, and whether the floor sketch holds a slope arrow
        private SlopeInfo SlopeOf(Floor floor)
        {
            var info = new SlopeInfo();
            Parameter parameter = floor.get_Parameter(BuiltInParameter.ROOF_SLOPE) ?? floor.LookupParameter("Slope");
            if (parameter != null && parameter.HasValue)
            {
                info.Parameter = RevitExtractor.ParameterValue(parameter);
            }
            try
            {
                var sketch = document.GetElement(floor.SketchId) as Sketch;
                info.Arrow = sketch == null ? (bool?)null : HasSlopeArrow(sketch);
            }
            catch (Exception failure)
            {
                Errors.Add("floor " + floor.Id.Value + " slope arrow: " + ModelReader.Describe(failure));
            }
            return info;
        }

        // A slope arrow is the sketch element that carries the tail height or tail level of a slope
        private bool HasSlopeArrow(Sketch sketch)
        {
            foreach (ElementId id in sketch.GetAllElements())
            {
                Element item = document.GetElement(id);
                if (item != null && (item.get_Parameter(BuiltInParameter.SLOPE_START_HEIGHT) != null || item.get_Parameter(BuiltInParameter.SLOPE_ARROW_LEVEL_START) != null))
                {
                    return true;
                }
            }
            return false;
        }

        // Spot dimensions of the style spot elevation. Value and ValueString of the API are empty for spots,
        // so the shown value is read from the top or bottom value parameter of the annotation
        public IEnumerable<SpotRow> SpotElevations()
        {
            var rows = new List<SpotRow>();
            foreach (SpotDimension spot in new FilteredElementCollector(document).OfClass(typeof(SpotDimension)).Cast<SpotDimension>())
            {
                try
                {
                    if (spot.DimensionType == null || spot.DimensionType.StyleType != DimensionStyleType.SpotElevation)
                    {
                        continue;
                    }
                    var row = new SpotRow
                    {
                        ElementId = spot.Id.Value,
                        Origin = RevitExtractor.Metres(spot.Origin),
                        Value = MetresOf(spot, BuiltInParameter.SPOT_ELEV_TOP_VALUE) ?? MetresOf(spot, BuiltInParameter.SPOT_ELEV_BOT_VALUE),
                        View = spot.View == null ? null : spot.View.Name,
                        Target = TargetOf(spot),
                    };
                    row.Host = HostOf(spot, row);
                    rows.Add(row);
                }
                catch (Exception failure)
                {
                    Errors.Add("spot elevation " + spot.Id.Value + ": " + ModelReader.Describe(failure));
                }
            }
            return rows.OrderBy(row => row.ElementId).ToList();
        }

        // The element the spot points to, with its category and type. A reference into a link gives the ids only,
        // because nothing inside a link is read. When the API gives no host the reason goes on the row and the reader writes it once
        private SpotHost HostOf(SpotDimension spot, SpotRow row)
        {
            try
            {
                if (!spot.AreReferencesAvailable)
                {
                    row.HostFailure = "the references are not available";
                    return null;
                }
                foreach (Reference reference in spot.References)
                {
                    if (reference.ElementId == ElementId.InvalidElementId)
                    {
                        continue;
                    }
                    var host = new SpotHost { ElementId = reference.ElementId.Value };
                    if (reference.LinkedElementId != ElementId.InvalidElementId)
                    {
                        host.LinkedElementId = reference.LinkedElementId.Value;
                        return host;
                    }
                    Element element = document.GetElement(reference.ElementId);
                    if (element != null)
                    {
                        host.Category = element.Category == null ? null : element.Category.Name;
                        host.TypeName = TypeNameOf(element);
                    }
                    return host;
                }
                row.HostFailure = "no reference points to an element";
                return null;
            }
            catch (Exception failure)
            {
                row.HostFailure = ModelReader.Describe(failure);
                return null;
            }
        }

        private static long? TargetOf(SpotDimension spot)
        {
            if (!spot.AreReferencesAvailable)
            {
                return null;
            }
            foreach (Reference reference in spot.References)
            {
                if (reference.ElementId != ElementId.InvalidElementId)
                {
                    return reference.ElementId.Value;
                }
            }
            return null;
        }

        private double? ThicknessOf(Floor floor)
        {
            var type = document.GetElement(floor.GetTypeId()) as FloorType;
            if (type == null)
            {
                return null;
            }
            double? fromParameter = MetresOf(type, BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            if (fromParameter.HasValue)
            {
                return fromParameter;
            }
            CompoundStructure structure = type.GetCompoundStructure();
            return structure == null ? (double?)null : UnitMath.FeetToMetres(structure.GetWidth());
        }

        private static double? AreaOf(Element element)
        {
            Parameter parameter = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
            if (parameter == null || !parameter.HasValue || parameter.StorageType != StorageType.Double)
            {
                return null;
            }
            return RevitExtractor.Convert(parameter.AsDouble(), SpecTypeId.Area);
        }

        // Every loop of the floor sketch. The outer one is chosen later by area
        private IEnumerable<IList<BoundaryEdge>> BoundaryLoops(Floor floor)
        {
            var loops = new List<IList<BoundaryEdge>>();
            var sketch = document.GetElement(floor.SketchId) as Sketch;
            if (sketch == null)
            {
                return loops;
            }
            foreach (CurveArray array in sketch.Profile)
            {
                var edges = new List<BoundaryEdge>();
                foreach (Curve curve in array)
                {
                    edges.Add(EdgeOf(curve));
                }
                loops.Add(edges);
            }
            return loops;
        }

        // A line gives its ends and an arc adds centre, radius and sweep. Any other curve is cut into straight pieces
        private static BoundaryEdge EdgeOf(Curve curve)
        {
            var edge = new BoundaryEdge
            {
                Start = RevitExtractor.Metres(curve.GetEndPoint(0)),
                End = RevitExtractor.Metres(curve.GetEndPoint(1)),
            };
            var arc = curve as Arc;
            if (arc != null && arc.IsBound)
            {
                edge.Center = RevitExtractor.Metres(arc.Center);
                edge.Radius = UnitMath.FeetToMetres(arc.Radius);
                edge.Ccw = arc.Normal.Z >= 0;
            }
            else if (!(curve is Line))
            {
                IList<XYZ> points = curve.Tessellate();
                edge.Via = points.Skip(1).Take(Math.Max(0, points.Count - 2)).Select(RevitExtractor.Metres).ToList();
            }
            return edge;
        }

        // The Revit API has no Ramp class, so ramps are found by category
        public IEnumerable<RampRow> Ramps()
        {
            var rows = new List<RampRow>();
            foreach (Element ramp in new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_Ramps).WhereElementIsNotElementType())
            {
                try
                {
                    var row = new RampRow
                    {
                        TypeName = TypeNameOf(ramp),
                        ElementId = ramp.Id.Value,
                        Location = LocationOf(ramp),
                        Parameters = AllParameters(ramp),
                    };
                    BoundingBoxXYZ box = ramp.get_BoundingBox(null);
                    if (box != null)
                    {
                        row.BoundingBox = new BoxInfo { Min = RevitExtractor.Metres(box.Min), Max = RevitExtractor.Metres(box.Max) };
                    }
                    rows.Add(row);
                }
                catch (Exception failure)
                {
                    Errors.Add("ramp " + ramp.Id.Value + ": " + ModelReader.Describe(failure));
                }
            }
            return rows.OrderBy(row => row.TypeName, StringComparer.Ordinal).ThenBy(row => row.ElementId).ToList();
        }

        private static IModelSource OpenLink(RevitLinkInstance instance)
        {
            Document linked = instance.GetLinkDocument();
            return linked == null ? null : new RevitModelSource(linked);
        }

        // The file name comes from the link type's file reference, and the type name is the fallback
        private string LinkFileName(RevitLinkInstance instance)
        {
            var linkType = document.GetElement(instance.GetTypeId()) as RevitLinkType;
            if (linkType == null)
            {
                return instance.Name;
            }
            try
            {
                string path = ModelPathUtils.ConvertModelPathToUserVisiblePath(linkType.GetExternalFileReference().GetAbsolutePath());
                if (!string.IsNullOrEmpty(path))
                {
                    return Path.GetFileName(path);
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // No usable file reference, so the type name below stands in
            }
            return linkType.Name;
        }

        private string TypeNameOf(Element element)
        {
            Element type = document.GetElement(element.GetTypeId());
            return type == null ? null : type.Name;
        }

        // Every parameter that holds a value. Shared parameters are in the set like any other
        // With a problems list, a parameter that cannot be read is noted there and the rest still come back.
        // Without one the failure is thrown, as before
        private static Dictionary<string, object> AllParameters(Element element, IList<string> problems = null)
        {
            var pairs = new List<KeyValuePair<string, object>>();
            foreach (Parameter parameter in element.Parameters)
            {
                try
                {
                    if (parameter.HasValue)
                    {
                        pairs.Add(new KeyValuePair<string, object>(parameter.Definition.Name, RevitExtractor.ParameterValue(parameter)));
                    }
                }
                catch (Exception failure)
                {
                    if (problems == null)
                    {
                        throw;
                    }
                    problems.Add("parameter " + parameter.Definition.Name + ": " + ModelReader.Describe(failure));
                }
            }
            return ParameterBag.Collect(pairs);
        }

        // Every parameter that holds a value with its name, built in id, storage type, shown text and number.
        // A parameter that cannot be read is noted in problems and the rest still come back
        private static List<ParameterDetail> Details(Element element, IList<string> problems)
        {
            var details = new List<ParameterDetail>();
            foreach (Parameter parameter in element.Parameters)
            {
                try
                {
                    if (parameter.HasValue)
                    {
                        details.Add(DetailOf(parameter));
                    }
                }
                catch (Exception failure)
                {
                    problems.Add("parameter " + parameter.Definition.Name + ": " + ModelReader.Describe(failure));
                }
            }
            return ParameterFormat.Collect(details);
        }

        private static ParameterDetail DetailOf(Parameter parameter)
        {
            var definition = parameter.Definition as InternalDefinition;
            string builtIn = definition == null ? null : definition.BuiltInParameter.ToString();
            string text = parameter.StorageType == StorageType.String ? parameter.AsString() : parameter.AsValueString();
            double? number = null;
            ValueUnit unit = ValueUnit.None;
            if (parameter.StorageType == StorageType.Double)
            {
                number = parameter.AsDouble();
                unit = UnitOf(parameter);
            }
            else if (parameter.StorageType == StorageType.Integer)
            {
                number = parameter.AsInteger();
            }
            return ParameterFormat.Make(parameter.Definition.Name, builtIn, parameter.StorageType.ToString(), text, number, unit);
        }

        private static ValueUnit UnitOf(Parameter parameter)
        {
            try
            {
                ForgeTypeId spec = parameter.Definition.GetDataType();
                if (spec == SpecTypeId.Length)
                {
                    return ValueUnit.Length;
                }
                if (spec == SpecTypeId.Area)
                {
                    return ValueUnit.Area;
                }
                if (spec == SpecTypeId.Volume)
                {
                    return ValueUnit.Volume;
                }
                if (spec == SpecTypeId.Angle)
                {
                    return ValueUnit.Angle;
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // A parameter with no readable spec keeps its number as Revit holds it
            }
            return ValueUnit.None;
        }

        private static double? MetresOf(Element element, BuiltInParameter id)
        {
            Parameter parameter = element.get_Parameter(id);
            if (parameter == null || !parameter.HasValue || parameter.StorageType != StorageType.Double)
            {
                return null;
            }
            return UnitMath.FeetToMetres(parameter.AsDouble());
        }

        private static LocationInfo LocationOf(Element element)
        {
            var byCurve = element.Location as LocationCurve;
            if (byCurve != null && byCurve.Curve != null)
            {
                return CurveInfo(byCurve.Curve);
            }
            var byPoint = element.Location as LocationPoint;
            if (byPoint != null)
            {
                return new LocationInfo { Kind = "point", Point = RevitExtractor.Metres(byPoint.Point) };
            }
            return null;
        }

        // Ends only, with centre and radius for an arc. No control points, as R3 says
        private static LocationInfo CurveInfo(Curve curve)
        {
            var info = new LocationInfo { Kind = curve is Line ? "line" : curve is Arc ? "arc" : curve.GetType().Name };
            if (curve.IsBound)
            {
                info.Start = RevitExtractor.Metres(curve.GetEndPoint(0));
                info.End = RevitExtractor.Metres(curve.GetEndPoint(1));
            }
            var arc = curve as Arc;
            if (arc != null)
            {
                info.Center = RevitExtractor.Metres(arc.Center);
                info.Radius = UnitMath.FeetToMetres(arc.Radius);
            }
            return info;
        }

        private static double[] Axis(XYZ vector)
        {
            return new[] { vector.X, vector.Y, vector.Z };
        }
    }
}
