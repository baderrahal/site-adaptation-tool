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

        // R500. Every wall of the prototype with a line or an arc for WallRingBuilder of Common, which decides: its function, curve, width,
        // Orientation, the side Revit gives as the exterior with Flipped already in it, and how it stands to the plane of the top of the
        // largest ground floor. The curve is read as it lies, the Location Line setting never moves it (R501). The member of a stacked wall is not read, the stacked wall itself is
        internal static IReadOnlyList<RingWall> RingWalls(Document prototype, LightModeConfig config)
        {
            Floor largest = GroundFloors(prototype, config).Select(floor => new { floor, sketch = prototype.GetElement(floor.SketchId) as Sketch }).Where(item => item.sketch != null)
                .OrderByDescending(item => item.sketch.Profile.Cast<CurveArray>().Max(loop => Math.Abs(RingMath.Area(Points(loop))))).Select(item => item.floor).FirstOrDefault();
            BoundingBoxXYZ top = largest?.get_BoundingBox(null);
            var walls = new List<RingWall>();
            if (top == null)
            {
                return walls;
            }
            foreach (Wall wall in new FilteredElementCollector(prototype).OfClass(typeof(Wall)).Cast<Wall>().Where(item => !item.IsStackedWallMember))
            {
                BoundingBoxXYZ box = wall.get_BoundingBox(null);
                if (box == null || !(wall.Location is LocationCurve location))
                {
                    continue;
                }
                var read = new RingWall
                {
                    Id = wall.Id.ToString(), Function = wall.WallType?.Function.ToString(), Width = UnitMath.FeetToMetres(wall.Width), Exterior = new[] { wall.Orientation.X, wall.Orientation.Y },
                    BottomM = UnitMath.FeetToMetres(box.Min.Z - top.Max.Z), HeightM = UnitMath.FeetToMetres(box.Max.Z - top.Max.Z),
                };
                if (location.Curve is Line || location.Curve is Arc)
                {
                    read.Start = Plan(location.Curve.GetEndPoint(0));
                    read.End = Plan(location.Curve.GetEndPoint(1));
                }
                if (location.Curve is Arc arc)
                {
                    read.Centre = Plan(arc.Center);
                    read.CounterClockwise = arc.Normal.Z > 0;
                }
                walls.Add(read);
            }
            return walls;
        }

        private static double[] Plan(XYZ point)
        {
            return new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y) };
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
