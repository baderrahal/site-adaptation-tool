using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using SiteAdaptation.Common;
using SiteAdaptation.Common.PlaceVillas;

namespace SiteAdaptation.AddIn.Revit
{
    // R404. The base boundary of a prototype by the source config names. D19: the prototype is copied into the work folder and the copy
    // opened and closed with no save, the source held by its SHA-256 before and after. The probe of 5c (5c_base_boundary) met a modal
    // error box on the open of a Revit 2021 file that froze Revit, so warnings are deleted and dialogs answered while the copy opens
    internal sealed class RevitBaseShapeReader : IBaseShapeReader
    {
        private static readonly Regex SignIn = new Regex("sign|licen|account|log ?in", RegexOptions.IgnoreCase);
        private readonly UIApplication application;
        private readonly string copyFolder;

        public RevitBaseShapeReader(UIApplication application, string workFolder)
        {
            this.application = application;
            copyFolder = Path.Combine(workFolder, ShapeCache.Folder, "copies");
        }

        public IReadOnlyList<double[]> Read(string prototypePath, LightModeConfig config)
        {
            return Opened(application, copyFolder, prototypePath, opened => Outer(opened, config));
        }

        // R404, R451. The prototype read from a copy opened detached and closed with no save, the source held by its SHA-256
        internal static T Opened<T>(UIApplication application, string copyFolder, string prototypePath, Func<Document, T> read)
        {
            string before = FileHash.Sha256(prototypePath);
            Directory.CreateDirectory(copyFolder);
            string copy = Path.Combine(copyFolder, Guid.NewGuid().ToString("N") + Path.GetExtension(prototypePath));
            File.Copy(prototypePath, copy);
            application.Application.FailuresProcessing += OnFailures;
            application.DialogBoxShowing += OnDialog;
            Document opened = null;
            try
            {
                opened = application.Application.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(copy), new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets });
                return read(opened);
            }
            finally
            {
                opened?.Close(false);
                application.DialogBoxShowing -= OnDialog;
                application.Application.FailuresProcessing -= OnFailures;
                File.Delete(copy);
                if (FileHash.Sha256(prototypePath) != before)
                {
                    throw new IOException(Path.GetFileName(prototypePath) + " changed while its shape was read (D19)");
                }
            }
        }

        // The floor of the lowest level, or every floor of the named type, the outer loop of the sketch of the largest of them
        internal static IReadOnlyList<double[]> Outer(Document prototype, LightModeConfig config)
        {
            List<List<double[]>> loops = GroundFloors(prototype, config).Select(floor => prototype.GetElement(floor.SketchId) as Sketch).Where(sketch => sketch != null)
                .SelectMany(sketch => sketch.Profile.Cast<CurveArray>().Select(Points)).ToList();
            if (loops.Count == 0)
            {
                throw new InvalidDataException(prototype.Title + " holds no floor for the base boundary source " + config.BaseBoundary);
            }
            return loops.OrderByDescending(loop => Math.Abs(RingMath.Area(loop))).First();
        }

        // R404, R451. The floors of the base boundary source, the floors of the lowest level that holds one or every floor of the named
        // type, so the slab base and the ground level of the walls come from the same floors
        private static List<Floor> GroundFloors(Document prototype, LightModeConfig config)
        {
            if (config.BaseBoundary == BaseBoundarySources.AreaBoundary)
            {
                throw new NotSupportedException("the base boundary source areaBoundary is not read yet, Q164 holds lowestFloor or floorType");
            }
            List<Floor> floors = new FilteredElementCollector(prototype).OfClass(typeof(Floor)).Cast<Floor>().ToList();
            if (config.BaseBoundary == BaseBoundarySources.LowestFloor)
            {
                Level lowest = new FilteredElementCollector(prototype).OfClass(typeof(Level)).Cast<Level>().Where(level => floors.Any(floor => floor.LevelId == level.Id))
                    .OrderBy(level => level.Elevation).FirstOrDefault();
                return lowest == null ? new List<Floor>() : floors.Where(floor => floor.LevelId == lowest.Id).ToList();
            }
            return floors.Where(floor => string.Equals(prototype.GetElement(floor.GetTypeId())?.Name, config.BaseBoundaryFloorType, StringComparison.Ordinal)).ToList();
        }

        // R451. The exterior walls, WallType.Function Exterior, that cross the plane wallCutHeightM above the top of the ground floors of
        // GroundFloors, each drawn by WallStrips from its location line, width and Orientation, the side Revit gives as the exterior
        // with Flipped already in it, so no door or window opens the ring. The outlines are extruded and joined, and the loops of the
        // bottom face of the union are the plan of the walls. Where a union fails the solids stay apart and their loops are given as
        // they are, so WallRing.Choose finds no ring and takes the slab
        internal static IReadOnlyList<IReadOnlyList<double[]>> WallLoops(Document prototype, LightModeConfig config)
        {
            List<BoundingBoxXYZ> boxes = GroundFloors(prototype, config).Select(floor => floor.get_BoundingBox(null)).Where(box => box != null).ToList();
            if (boxes.Count == 0)
            {
                return new List<IReadOnlyList<double[]>>();
            }
            double cut = boxes.Max(box => box.Max.Z) + UnitMath.MetresToFeet(config.MassFiles.WallCutHeightM);
            var solids = new List<Solid>();
            foreach (Wall wall in new FilteredElementCollector(prototype).OfClass(typeof(Wall)).Cast<Wall>())
            {
                BoundingBoxXYZ box = wall.get_BoundingBox(null);
                if (wall.WallType?.Function != WallFunction.Exterior || box == null || box.Min.Z > cut || box.Max.Z < cut || !(wall.Location is LocationCurve location))
                {
                    continue;
                }
                Solid outline = WallOutline(wall, location.Curve);
                if (outline != null)
                {
                    solids.Add(outline);
                }
            }
            Solid union = null;
            var joined = new List<Solid>();
            foreach (Solid solid in solids)
            {
                if (union == null)
                {
                    union = solid;
                    continue;
                }
                try
                {
                    union = BooleanOperationsUtils.ExecuteBooleanOperation(union, solid, BooleanOperationsType.Union);
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    joined.Add(solid);
                }
            }
            if (union != null)
            {
                joined.Insert(0, union);
            }
            return joined.SelectMany(solid => solid.Faces.OfType<PlanarFace>()).Where(face => face.FaceNormal.Z < -0.999)
                .SelectMany(face => face.GetEdgesAsCurveLoops()).Select(loop => (IReadOnlyList<double[]>)Points(loop)).ToList();
        }

        // The outline of WallStrips extruded up from Z 0, any height as only its bottom face is read. Null where Revit refuses the loop
        private static Solid WallOutline(Wall wall, Curve curve)
        {
            IList<XYZ> along = curve is Line ? new List<XYZ> { curve.GetEndPoint(0), curve.GetEndPoint(1) } : curve.Tessellate();
            List<double[]> path = along.Select(point => new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y) }).ToList();
            double width = UnitMath.FeetToMetres(wall.Width);
            CompoundStructure structure = wall.WallType.GetCompoundStructure();
            List<double> layers = structure?.GetLayers().Select(layer => UnitMath.FeetToMetres(layer.Width)).ToList();
            int locationLine = wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM)?.AsInteger() ?? WallStrips.WallCenterline;
            double offset = WallStrips.LocationOffset(locationLine, width, layers, structure?.GetFirstCoreLayerIndex() ?? -1, structure?.GetLastCoreLayerIndex() ?? -1);
            double[][] ring = WallStrips.Outline(path, width, offset, new[] { wall.Orientation.X, wall.Orientation.Y });
            if (ring == null)
            {
                return null;
            }
            var loop = new CurveLoop();
            try
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    double[] a = ring[i], b = ring[(i + 1) % ring.Length];
                    loop.Append(Line.CreateBound(new XYZ(UnitMath.MetresToFeet(a[0]), UnitMath.MetresToFeet(a[1]), 0), new XYZ(UnitMath.MetresToFeet(b[0]), UnitMath.MetresToFeet(b[1]), 0)));
                }
                return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, 1.0);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return null;
            }
        }

        private static List<double[]> Points(IEnumerable<Curve> curves)
        {
            var points = new List<double[]>();
            foreach (Curve curve in curves)
            {
                IList<XYZ> along = curve is Line ? new List<XYZ> { curve.GetEndPoint(0), curve.GetEndPoint(1) } : curve.Tessellate();
                points.AddRange(along.Take(along.Count - 1).Select(point => new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y) }));
            }
            return points;
        }

        // Every corner: the start of each line, the tessellated points of an arc, in the order of the sketch
        private static List<double[]> Points(CurveArray curves)
        {
            var points = new List<double[]>();
            foreach (Curve curve in curves)
            {
                IList<XYZ> along = curve is Line ? new List<XYZ> { curve.GetEndPoint(0), curve.GetEndPoint(1) } : curve.Tessellate();
                points.AddRange(along.Take(along.Count - 1).Select(point => new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y) }));
            }
            return points;
        }

        internal static void OnFailures(object sender, FailuresProcessingEventArgs args)
        {
            args.GetFailuresAccessor().DeleteAllWarnings();
            args.SetProcessingResult(FailureProcessingResult.Continue);
        }

        // A sign in or licence box is left to the user, any other box of the open is closed with its first button
        private static void OnDialog(object sender, DialogBoxShowingEventArgs args)
        {
            string message = args is TaskDialogShowingEventArgs task ? task.Message : args is MessageBoxShowingEventArgs box ? box.Message : args.DialogId;
            if (!SignIn.IsMatch(message ?? ""))
            {
                args.OverrideResult(1);
            }
        }
    }
}
