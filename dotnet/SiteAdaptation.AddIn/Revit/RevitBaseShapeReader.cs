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
            if (config.BaseBoundary == BaseBoundarySources.AreaBoundary)
            {
                throw new NotSupportedException("the base boundary source areaBoundary is not read yet, Q164 holds lowestFloor or floorType");
            }
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
                return Outer(opened, config);
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
        private static IReadOnlyList<double[]> Outer(Document prototype, LightModeConfig config)
        {
            List<Floor> floors = new FilteredElementCollector(prototype).OfClass(typeof(Floor)).Cast<Floor>().ToList();
            if (config.BaseBoundary == BaseBoundarySources.LowestFloor)
            {
                Level lowest = new FilteredElementCollector(prototype).OfClass(typeof(Level)).Cast<Level>().Where(level => floors.Any(floor => floor.LevelId == level.Id))
                    .OrderBy(level => level.Elevation).FirstOrDefault();
                floors = lowest == null ? new List<Floor>() : floors.Where(floor => floor.LevelId == lowest.Id).ToList();
            }
            else
            {
                floors = floors.Where(floor => string.Equals(prototype.GetElement(floor.GetTypeId())?.Name, config.BaseBoundaryFloorType, StringComparison.Ordinal)).ToList();
            }
            List<List<double[]>> loops = floors.Select(floor => prototype.GetElement(floor.SketchId) as Sketch).Where(sketch => sketch != null)
                .SelectMany(sketch => sketch.Profile.Cast<CurveArray>().Select(Points)).ToList();
            if (loops.Count == 0)
            {
                throw new InvalidDataException(prototype.Title + " holds no floor for the base boundary source " + config.BaseBoundary);
            }
            return loops.OrderByDescending(loop => Math.Abs(Area(loop))).First();
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

        private static double Area(List<double[]> loop)
        {
            double area = 0;
            for (int i = 0; i < loop.Count; i++)
            {
                area += loop[i][0] * loop[(i + 1) % loop.Count][1] - loop[(i + 1) % loop.Count][0] * loop[i][1];
            }
            return area / 2;
        }

        private static void OnFailures(object sender, FailuresProcessingEventArgs args)
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
