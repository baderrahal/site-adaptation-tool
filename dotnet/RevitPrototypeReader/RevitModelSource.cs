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

        public RevitModelSource(Document document)
        {
            this.document = document;
            Errors = new List<string>();
        }

        public IList<string> Errors { get; }

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
            var link = new SourceLink { File = LinkFileName(instance), Problems = problems, Open = () => OpenLink(instance) };
            try
            {
                Transform transform = instance.GetTotalTransform();
                link.Transform = LinkMath.Describe(RevitExtractor.Metres(transform.Origin), Axis(transform.BasisX), Axis(transform.BasisY), Axis(transform.BasisZ));
            }
            catch (Exception failure)
            {
                problems.Add("transform: " + ModelReader.Describe(failure));
            }
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
            return link;
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
            return rows.OrderBy(row => row.TypeName, StringComparer.Ordinal).ThenBy(row => row.ElementId).ToList();
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
            if (FloorMath.TryOuter(BoundaryLoops(floor), out boundary, out arcs))
            {
                row.Boundary = boundary;
                row.Arcs = arcs.Count > 0 ? arcs : null;
            }
            else
            {
                Errors.Add("floor " + floor.Id.Value + ": no boundary found");
            }
            return row;
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
