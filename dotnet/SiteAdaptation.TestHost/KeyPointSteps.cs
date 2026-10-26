using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace SiteAdaptation.TestHost
{
    // R353. After a read job has read its model, the key points of it at full precision: the project position of the internal origin,
    // the survey point, the project base point and the corners of the outer loop of the sketch of every floor, the start of each edge, in
    // internal coordinates and in shared coordinates by the transform of the active project location, every double kept as it reads back.
    // KeyPointsFile of Common makes the file, nothing is changed or saved
    internal sealed class KeyPointSteps
    {
        private readonly TestHostJob job;
        private readonly string documents;

        public KeyPointSteps(TestHostJob job, string documents)
        {
            this.job = job;
            this.documents = documents;
        }

        private static double[] Metres(XYZ point)
        {
            return new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y), UnitMath.FeetToMetres(point.Z) };
        }

        private static XYZ Feet(double[] point)
        {
            return new XYZ(point[0] / SiteCoordinates.MetresPerFoot, point[1] / SiteCoordinates.MetresPerFoot, point[2] / SiteCoordinates.MetresPerFoot);
        }

        // A line gives its ends, an arc its ends with its centre and radius, any other curve its ends and the points between them, so the
        // outer loop is the one FloorMath.TryOuter chooses by area as the reader chooses it
        private static BoundaryEdge EdgeOf(Curve curve)
        {
            var edge = new BoundaryEdge { Start = Metres(curve.GetEndPoint(0)), End = Metres(curve.GetEndPoint(1)) };
            var arc = curve as Arc;
            if (arc != null && arc.IsBound)
            {
                edge.Center = Metres(arc.Center);
                edge.Radius = UnitMath.FeetToMetres(arc.Radius);
                edge.Ccw = arc.Normal.Z >= 0;
            }
            else if (!(curve is Line))
            {
                IList<XYZ> points = curve.Tessellate();
                edge.Via = points.Skip(1).Take(System.Math.Max(0, points.Count - 2)).Select(Metres).ToList();
            }
            return edge;
        }

        public string KeyPoints(Document document)
        {
            ProjectLocation location = document.ActiveProjectLocation;
            ProjectPosition at = location.GetProjectPosition(XYZ.Zero);
            // GetTotalTransform takes a shared point to its internal point (SiteSteps), so its inverse takes an internal point to its shared one
            Transform toShared = location.GetTotalTransform().Inverse;
            BasePoint survey = BasePoint.GetSurveyPoint(document);
            BasePoint projectBase = BasePoint.GetProjectBasePoint(document);
            var floors = new List<KeyPointFloor>();
            int noLoop = 0;
            foreach (Floor floor in new FilteredElementCollector(document).OfClass(typeof(Floor)).Cast<Floor>())
            {
                var loops = new List<IList<BoundaryEdge>>();
                var sketch = document.GetElement(floor.SketchId) as Sketch;
                if (sketch != null)
                {
                    foreach (CurveArray array in sketch.Profile)
                    {
                        loops.Add(array.Cast<Curve>().Select(EdgeOf).ToList());
                    }
                }
                List<double[]> vertices, ends;
                List<FloorArc> arcs;
                if (!FloorMath.TryOuter(loops, out vertices, out arcs, out ends))
                {
                    noLoop++;
                    continue;
                }
                Parameter plot = floor.LookupParameter(PlotParts.ParameterName);
                floors.Add(new KeyPointFloor
                {
                    ElementId = floor.Id.Value, TypeName = document.GetElement(floor.GetTypeId())?.Name, PlotText = plot == null ? null : plot.AsString(),
                    Corners = ends, SharedCorners = ends.Select(end => Metres(toShared.OfPoint(Feet(end)))).ToList(),
                });
            }
            var file = KeyPointsFile.Build(Path.GetFileName(job.Model),
                new[] { UnitMath.FeetToMetres(at.EastWest), UnitMath.FeetToMetres(at.NorthSouth), UnitMath.FeetToMetres(at.Elevation) }, at.Angle * 180.0 / System.Math.PI,
                Metres(survey.Position), Metres(survey.SharedPosition), Metres(projectBase.Position), Metres(projectBase.SharedPosition), floors);
            File.WriteAllText(TestHostGuard.OutputPath(documents, job.Code, "points"), KeyPointsFile.Text(file), new UTF8Encoding(false));
            return "floors " + floors.Count + ", with a plot " + file["floors"].Count(item => item["plot"].Type != Newtonsoft.Json.Linq.JTokenType.Null)
                + ", corners " + floors.Sum(floor => floor.Corners.Count) + ", no outer loop " + noLoop;
        }
    }
}
