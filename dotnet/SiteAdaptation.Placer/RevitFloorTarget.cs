using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace SiteAdaptation.Placer
{
    // R113 to R115. The open document seen through IFloorTarget. It carries out the steps FloorPlacer asks for and decides nothing.
    // Floor.Create takes the loops, the floor type and the level. A ramp is made flat at its lowest corner and then each corner vertex
    // of SlabShapeEditor is lifted with ModifySubElement, which is how the past model holds its ramps (cornersSource shapeEditor, R35)
    internal sealed class RevitFloorTarget : IFloorTarget
    {
        private readonly Document document;
        private readonly WarningCollector collector = new WarningCollector();
        // The plan position of each corner vertex ShapeCorners gave, so a vertex is found again by where it is and no vertex object is kept
        private readonly Dictionary<long, List<XYZ>> corners = new Dictionary<long, List<XYZ>>();
        private Transaction transaction;

        public RevitFloorTarget(Document document)
        {
            this.document = document;
        }

        public string DocumentTitle
        {
            get { return document.Title; }
        }

        public IList<string> Warnings
        {
            get { return collector.Lines; }
        }

        public SurveyReading Survey()
        {
            BasePoint survey = BasePoint.GetSurveyPoint(document);
            ProjectPosition position = document.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            return new SurveyReading
            {
                Position = Metres(survey.Position),
                SharedPosition = Metres(survey.SharedPosition),
                AngleToTrueNorthDeg = UnitMath.RadiansToDegrees(position.Angle),
            };
        }

        public IList<FloorTypeEntry> FloorTypes()
        {
            return new FilteredElementCollector(document).OfClass(typeof(FloorType)).Cast<FloorType>()
                .Select(type => new FloorTypeEntry { Name = type.Name, Key = Key(type.Id) }).ToList();
        }

        public IList<LevelEntry> Levels()
        {
            return new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>()
                .Select(level => new LevelEntry { Name = level.Name, Key = Key(level.Id), ElevationM = UnitMath.FeetToMetres(level.Elevation) }).ToList();
        }

        public IList<ExistingFloor> Floors(string plotParameter)
        {
            var found = new List<ExistingFloor>();
            foreach (Floor floor in new FilteredElementCollector(document).OfClass(typeof(Floor)).Cast<Floor>())
            {
                Element type = document.GetElement(floor.GetTypeId());
                Parameter plot = floor.LookupParameter(plotParameter);
                found.Add(new ExistingFloor
                {
                    Id = floor.Id.Value,
                    TypeName = type == null ? null : type.Name,
                    PlotValue = plot == null || !plot.HasValue || plot.StorageType != StorageType.String ? null : plot.AsString(),
                });
            }
            return found;
        }

        public void Begin()
        {
            transaction = new Transaction(document, "Place floors from plan");
            FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(collector);
            // A roll back then shows no error box, which would stop the test host of R103 until someone clicks
            options.SetClearAfterRollback(true);
            transaction.SetFailureHandlingOptions(options);
            transaction.Start();
        }

        // R115. A segment with a middle point is an Arc through its three points, so the arc is kept and never cut into lines
        public long CreateFloor(IList<FloorLoop> loops, string typeKey, string levelKey)
        {
            var profile = new List<CurveLoop>();
            foreach (FloorLoop loop in loops)
            {
                var curves = new CurveLoop();
                foreach (FloorSegment segment in loop.Segments)
                {
                    Curve curve = segment.Mid == null
                        ? (Curve)Line.CreateBound(Feet(segment.Start), Feet(segment.End))
                        : Arc.Create(Feet(segment.Start), Feet(segment.End), Feet(segment.Mid));
                    curves.Append(curve);
                }
                profile.Add(curves);
            }
            Floor floor = Floor.Create(document, profile, Id(typeKey), Id(levelKey));
            return floor.Id.Value;
        }

        public void SetHeightOffset(long floorId, double metres)
        {
            Parameter offset = FloorOf(floorId).get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            if (offset == null || offset.IsReadOnly || !offset.Set(UnitMath.MetresToFeet(metres)))
            {
                throw new InvalidOperationException("the height offset of floor " + floorId + " could not be set");
            }
        }

        public IList<double[]> ShapeCorners(long floorId)
        {
            Floor floor = FloorOf(floorId);
            SlabShapeEditor editor = floor.GetSlabShapeEditor();
            if (editor == null)
            {
                throw new InvalidOperationException("floor " + floorId + " has no slab shape editor");
            }
            editor.Enable();
            List<SlabShapeVertex> found = CornerVertices(editor);
            if (found.Count == 0)
            {
                // The vertices of a floor just made can wait for a regeneration
                document.Regenerate();
                found = CornerVertices(floor.GetSlabShapeEditor());
            }
            corners[floorId] = found.Select(vertex => vertex.Position).ToList();
            return found.Select(vertex => Metres(vertex.Position)).ToList();
        }

        public void SetCornerOffset(long floorId, int corner, double metres)
        {
            SlabShapeEditor editor = FloorOf(floorId).GetSlabShapeEditor();
            XYZ at = corners[floorId][corner];
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
                throw new InvalidOperationException("corner " + corner + " of floor " + floorId + " is no longer a shape vertex");
            }
            editor.ModifySubElement(vertex, UnitMath.MetresToFeet(metres));
        }

        public void Regenerate()
        {
            document.Regenerate();
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

        public FloorReadBack ReadBack(long floorId)
        {
            Floor floor = FloorOf(floorId);
            BoundingBoxXYZ box = floor.get_BoundingBox(null);
            var read = new FloorReadBack { TopMax = box == null ? (double?)null : UnitMath.FeetToMetres(box.Max.Z) };
            SlabShapeEditor editor = floor.GetSlabShapeEditor();
            if (editor != null && editor.IsEnabled)
            {
                read.Corners = new List<double[]>();
                foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
                {
                    if (vertex.VertexType == SlabShapeVertexType.Corner)
                    {
                        read.Corners.Add(Metres(vertex.Position));
                    }
                }
            }
            return read;
        }

        public ParameterWrite WriteText(long floorId, string name, string value)
        {
            Parameter parameter = FloorOf(floorId).LookupParameter(name);
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

        public CommitResult Commit()
        {
            TransactionStatus status = transaction.Commit();
            string errors = string.Join(". ", collector.Errors);
            return new CommitResult { Committed = status == TransactionStatus.Committed, Reason = status + (errors.Length > 0 ? ", " + errors : "") };
        }

        public void RollBack()
        {
            if (transaction != null && transaction.IsValidObject && transaction.GetStatus() == TransactionStatus.Started)
            {
                transaction.RollBack();
            }
        }

        private Floor FloorOf(long floorId)
        {
            var floor = document.GetElement(new ElementId(floorId)) as Floor;
            if (floor == null)
            {
                throw new InvalidOperationException("floor " + floorId + " is not in the document");
            }
            return floor;
        }

        private static ElementId Id(string key)
        {
            return new ElementId(long.Parse(key, CultureInfo.InvariantCulture));
        }

        private static string Key(ElementId id)
        {
            return id.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static double[] Metres(XYZ point)
        {
            return new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y), UnitMath.FeetToMetres(point.Z) };
        }

        private static XYZ Feet(double[] metres)
        {
            return new XYZ(UnitMath.MetresToFeet(metres[0]), UnitMath.MetresToFeet(metres[1]), UnitMath.MetresToFeet(metres[2]));
        }
    }
}
