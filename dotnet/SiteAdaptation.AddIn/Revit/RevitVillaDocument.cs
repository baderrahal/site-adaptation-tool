using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.AddIn.Revit
{
    // R377, R404. The open document seen through IVillaDocument. It carries out what PlaceVillasRun decides and decides nothing. The
    // transaction is the one of the flow (R382), so nothing here starts one
    internal sealed class RevitVillaDocument : IVillaDocument
    {
        // R404, open point 6. CHOSEN: Generic Models, since a DirectShape of it shows in every plan and 3D view with no setting, where a
        // Mass shows only with Show Mass on, and it takes instance parameters bound by category like any model element
        private static readonly BuiltInCategory SolidCategory = BuiltInCategory.OST_GenericModel;
        private const string ApplicationId = "SiteAdaptation";

        private readonly Document document;
        private readonly PlacedTagStore tags;

        public RevitVillaDocument(Document document)
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

        public IReadOnlyList<VillaLinkType> LinkTypes()
        {
            var types = new List<VillaLinkType>();
            foreach (RevitLinkType type in new FilteredElementCollector(document).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>())
            {
                string path = null;
                try
                {
                    ExternalFileReference reference = type.GetExternalFileReference();
                    path = reference == null ? null : ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath());
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    // A type with no usable file reference cannot stand in for a prototype file
                }
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }
                types.Add(new VillaLinkType
                {
                    FullPath = path, Key = Key(type.Id), Loaded = type.GetLinkedFileStatus() == LinkedFileStatus.Loaded,
                    Usable = !type.IsNestedLink, Why = type.IsNestedLink ? "it is a nested link" : null,
                });
            }
            return types;
        }

        public IReadOnlyList<VillaElement> Villas()
        {
            Dictionary<string, PlacedTag> tagged = tags.Collect().GroupBy(item => item.Key.UniqueId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Value.Version).First().Value, StringComparer.Ordinal);
            var villas = new List<VillaElement>();
            foreach (RevitLinkInstance instance in new FilteredElementCollector(document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                Parameter plot = instance.LookupParameter(LinkFields.PlotNumber);
                tagged.TryGetValue(instance.UniqueId, out PlacedTag tag);
                villas.Add(new VillaElement { UniqueId = instance.UniqueId, Tag = tag, PlotNumber = plot != null && plot.HasValue ? plot.AsString() : null, Transform = Transform(instance) });
            }
            foreach (DirectShape shape in new FilteredElementCollector(document).OfClass(typeof(DirectShape)).Cast<DirectShape>())
            {
                if (tagged.TryGetValue(shape.UniqueId, out PlacedTag tag) && tag.Key.Part == PlaceVillasRun.MassPart)
                {
                    villas.Add(new VillaElement { UniqueId = shape.UniqueId, IsSolid = true, Tag = tag, Base = BaseOf(shape) });
                }
            }
            return villas;
        }

        public bool FileExists(string path) => File.Exists(path);

        public string LoadType(string key)
        {
            LinkLoadResult result = ((RevitLinkType)document.GetElement(Id(key))).Load();
            return result != null && LinkLoadResult.IsCodeSuccess(result.LoadResult) ? null : result == null ? "no result" : result.LoadResult.ToString();
        }

        public string ReloadType(string key)
        {
            LinkLoadResult result = ((RevitLinkType)document.GetElement(Id(key))).Reload();
            return result != null && LinkLoadResult.IsCodeSuccess(result.LoadResult) ? null : result == null ? "no result" : result.LoadResult.ToString();
        }

        public string CreateType(string fullPath)
        {
            LinkLoadResult result = RevitLinkType.Create(document, ModelPathUtils.ConvertUserVisiblePathToModelPath(fullPath), new RevitLinkOptions(false));
            return result != null && LinkLoadResult.IsCodeSuccess(result.LoadResult) ? Key(result.ElementId) : null;
        }

        public string PlaceLink(string typeKey, PlacementSteps steps)
        {
            RevitLinkInstance instance = RevitLinkInstance.Create(document, Id(typeKey), ImportPlacement.Origin);
            Carry(instance, steps);
            return instance.UniqueId;
        }

        public string PlaceSolid(IReadOnlyList<double[]> shape, double heightM, PlacementSteps steps)
        {
            DirectShape solid = DirectShape.CreateElement(document, new ElementId(SolidCategory));
            solid.ApplicationId = ApplicationId;
            solid.SetShape(new GeometryObject[] { Extrusion(shape, heightM, steps) });
            return solid.UniqueId;
        }

        // The link back to origin to origin, the inverse of its steps in reverse order, then the steps of the plan
        public void MoveLink(string uniqueId, PlacementSteps steps)
        {
            var instance = (RevitLinkInstance)document.GetElement(uniqueId);
            if (instance.Pinned)
            {
                instance.Pinned = false;
            }
            Transform now = instance.GetTotalTransform();
            LinkTransform found = LinkMath.Describe(Metres(now.Origin), Axis(now.BasisX), Axis(now.BasisY), Axis(now.BasisZ));
            ElementTransformUtils.MoveElement(document, instance.Id, -now.Origin);
            ElementTransformUtils.RotateElement(document, instance.Id, Line.CreateBound(XYZ.Zero, XYZ.BasisZ), -UnitMath.DegreesToRadians(found.RotationDeg));
            if (found.Mirrored)
            {
                ElementTransformUtils.MirrorElements(document, new List<ElementId> { instance.Id }, Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero), false);
            }
            Carry(instance, steps);
        }

        public void ReshapeSolid(string uniqueId, IReadOnlyList<double[]> shape, double heightM, PlacementSteps steps)
        {
            ((DirectShape)document.GetElement(uniqueId)).SetShape(new GeometryObject[] { Extrusion(shape, heightM, steps) });
        }

        public void Delete(string uniqueId)
        {
            document.Delete(document.GetElement(uniqueId).Id);
        }

        public PlacedTransform ReadBackLink(string uniqueId)
        {
            document.Regenerate();
            return document.GetElement(uniqueId) is RevitLinkInstance instance ? Transform(instance) : null;
        }

        public IReadOnlyList<double[]> ReadBackBase(string uniqueId)
        {
            document.Regenerate();
            return document.GetElement(uniqueId) is DirectShape shape ? BaseOf(shape) : null;
        }

        public ParameterWrite Write(string uniqueId, string name, string value)
        {
            Parameter parameter = document.GetElement(uniqueId)?.LookupParameter(name);
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

        public ParameterWrite WriteName(string uniqueId, string value)
        {
            Element element = document.GetElement(uniqueId);
            if (element is DirectShape shape)
            {
                shape.SetName(value);
                return ParameterWrite.Written;
            }
            Parameter parameter = element?.get_Parameter(BuiltInParameter.RVT_LINK_INSTANCE_NAME);
            if (parameter == null)
            {
                return ParameterWrite.Missing;
            }
            if (parameter.IsReadOnly)
            {
                return ParameterWrite.ReadOnly;
            }
            return parameter.Set(value) ? ParameterWrite.Written : ParameterWrite.Refused;
        }

        public void Tag(string uniqueId, PlacedTag tag)
        {
            tags.Write(document.GetElement(uniqueId), tag);
        }

        // R84. Moved to the point, turned about the vertical through it, mirrored across the plane whose normal is the turned Y axis,
        // mirrorCopies false so no copy is left
        private void Carry(RevitLinkInstance instance, PlacementSteps steps)
        {
            if (instance.Pinned)
            {
                instance.Pinned = false;
            }
            XYZ point = Feet(steps.Move);
            ElementTransformUtils.MoveElement(document, instance.Id, point);
            ElementTransformUtils.RotateElement(document, instance.Id, Line.CreateBound(point, point + XYZ.BasisZ), steps.RotationRad);
            if (steps.MirrorNormal != null)
            {
                var ids = new List<ElementId> { instance.Id };
                if (!ElementTransformUtils.CanMirrorElements(document, ids))
                {
                    throw new InvalidOperationException("Revit says link instance " + instance.Id.Value + " cannot be mirrored");
                }
                ElementTransformUtils.MirrorElements(document, ids, Plane.CreateByNormalAndOrigin(new XYZ(steps.MirrorNormal[0], steps.MirrorNormal[1], steps.MirrorNormal[2]), point), false);
            }
        }

        // The shape carried by the same transform as the link, its loop turned counter clockwise since a mirror reverses it
        private static Solid Extrusion(IReadOnlyList<double[]> shape, double heightM, PlacementSteps steps)
        {
            List<double[]> placed = ShapeMath.Place(shape, steps.Move, UnitMath.RadiansToDegrees(steps.RotationRad), steps.MirrorNormal != null);
            double area = 0;
            for (int i = 0; i < placed.Count; i++)
            {
                double[] a = placed[i], b = placed[(i + 1) % placed.Count];
                area += a[0] * b[1] - b[0] * a[1];
            }
            if (area < 0)
            {
                placed.Reverse();
            }
            double z = steps.Move.Length > 2 ? steps.Move[2] : 0;
            var loop = new CurveLoop();
            for (int i = 0; i < placed.Count; i++)
            {
                double[] a = placed[i], b = placed[(i + 1) % placed.Count];
                loop.Append(Line.CreateBound(Feet(new[] { a[0], a[1], z }), Feet(new[] { b[0], b[1], z })));
            }
            return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, UnitMath.MetresToFeet(heightM));
        }

        // The outer loop of the lowest downward face of the solid, every corner, from the geometry Revit holds and not from the shape
        private static IReadOnlyList<double[]> BaseOf(DirectShape shape)
        {
            PlanarFace lowest = null;
            foreach (Solid solid in (shape.get_Geometry(new Options()) ?? Enumerable.Empty<GeometryObject>()).OfType<Solid>())
            {
                foreach (PlanarFace face in solid.Faces.OfType<PlanarFace>().Where(face => face.FaceNormal.Z < -0.999))
                {
                    if (lowest == null || face.Origin.Z < lowest.Origin.Z)
                    {
                        lowest = face;
                    }
                }
            }
            if (lowest == null)
            {
                return null;
            }
            CurveLoop outer = lowest.GetEdgesAsCurveLoops().OrderByDescending(loop => Math.Abs(SignedArea(loop))).First();
            return outer.SelectMany(curve => curve.Tessellate().Take(curve.Tessellate().Count - 1)).Select(point => new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y) }).ToList();
        }

        private static double SignedArea(CurveLoop loop)
        {
            List<XYZ> points = loop.Select(curve => curve.GetEndPoint(0)).ToList();
            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                area += points[i].X * points[(i + 1) % points.Count].Y - points[(i + 1) % points.Count].X * points[i].Y;
            }
            return area / 2;
        }

        private static PlacedTransform Transform(RevitLinkInstance instance)
        {
            Transform transform = instance.GetTotalTransform();
            return new PlacedTransform { Origin = Metres(transform.Origin), AxisX = Axis(transform.BasisX), AxisY = Axis(transform.BasisY), AxisZ = Axis(transform.BasisZ) };
        }

        private static ElementId Id(string key) => new ElementId(long.Parse(key, CultureInfo.InvariantCulture));

        private static string Key(ElementId id) => id.Value.ToString(CultureInfo.InvariantCulture);

        private static double[] Metres(XYZ point) => new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y), UnitMath.FeetToMetres(point.Z) };

        private static XYZ Feet(double[] metres) => new XYZ(UnitMath.MetresToFeet(metres[0]), UnitMath.MetresToFeet(metres[1]), UnitMath.MetresToFeet(metres.Length > 2 ? metres[2] : 0));

        private static double[] Axis(XYZ vector) => new[] { vector.X, vector.Y, vector.Z };
    }
}
