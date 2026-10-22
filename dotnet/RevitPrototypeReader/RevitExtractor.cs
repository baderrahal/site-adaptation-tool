using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace RevitPrototypeReader
{
    internal static class RevitExtractor
    {
        public static DocumentEntry Read(Document doc, string file, IList<string> keyNames, ReaderOptions options)
        {
            var entry = new DocumentEntry
            {
                File = file,
                Kind = doc.IsFamilyDocument ? "family" : "project",
                Prototypes = new List<PrototypeRow>(),
            };
            if (doc.IsFamilyDocument)
            {
                ReadFamily(doc, keyNames, entry);
            }
            else
            {
                ReadProjectPoints(doc, entry);
                ReadProject(doc, keyNames, entry);
                ModelReader.ReadSections(new RevitModelSource(doc), entry, options);
            }
            return entry;
        }

        private static void ReadFamily(Document doc, IList<string> keyNames, DocumentEntry entry)
        {
            FamilyManager manager = doc.FamilyManager;
            string familyName = doc.OwnerFamily != null ? doc.OwnerFamily.Name : Path.GetFileNameWithoutExtension(doc.PathName);
            BoxInfo box = FamilyGeometryBox(doc);
            var keyParameters = manager.Parameters.Cast<FamilyParameter>()
                .Where(parameter => IsKey(parameter.Definition.Name, keyNames))
                .ToList();

            var types = manager.Types.Cast<FamilyType>().ToList();
            if (types.Count == 0)
            {
                entry.Prototypes.Add(FamilyRow(familyName, null, box, new Dictionary<string, object>()));
                return;
            }
            foreach (FamilyType type in types)
            {
                var values = new Dictionary<string, object>();
                foreach (FamilyParameter parameter in keyParameters)
                {
                    object value = FamilyValue(type, parameter);
                    if (value != null)
                    {
                        values[parameter.Definition.Name] = value;
                    }
                }
                entry.Prototypes.Add(FamilyRow(familyName, type.Name, box, values));
            }
        }

        private static PrototypeRow FamilyRow(string familyName, string typeName, BoxInfo box, Dictionary<string, object> values)
        {
            return new PrototypeRow
            {
                FamilyName = familyName,
                TypeName = typeName,
                Placement = "family origin",
                Parameters = values,
                BoundingBox = box,
                InsertionPoint = new double[] { 0, 0, 0 },
                RotationDeg = 0,
            };
        }

        private static object FamilyValue(FamilyType type, FamilyParameter parameter)
        {
            switch (parameter.StorageType)
            {
                case StorageType.Double:
                    double? number = type.AsDouble(parameter);
                    return number.HasValue ? (object)Convert(number.Value, parameter.Definition.GetDataType()) : null;
                case StorageType.Integer:
                    return type.AsInteger(parameter);
                case StorageType.String:
                    return type.AsString(parameter);
                default:
                    return null;
            }
        }

        // Solid forms and nested instances only. Voids, reference planes and annotation do not count as the footprint
        private static BoxInfo FamilyGeometryBox(Document doc)
        {
            var filter = new ElementMulticlassFilter(new List<Type>
            {
                typeof(Extrusion), typeof(Blend), typeof(Sweep), typeof(SweptBlend), typeof(Revolution), typeof(FamilyInstance),
            });
            XYZ min = null;
            XYZ max = null;
            foreach (Element element in new FilteredElementCollector(doc).WherePasses(filter))
            {
                var form = element as GenericForm;
                if (form != null && !form.IsSolid)
                {
                    continue;
                }
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (box == null)
                {
                    continue;
                }
                min = min == null ? box.Min : new XYZ(Math.Min(min.X, box.Min.X), Math.Min(min.Y, box.Min.Y), Math.Min(min.Z, box.Min.Z));
                max = max == null ? box.Max : new XYZ(Math.Max(max.X, box.Max.X), Math.Max(max.Y, box.Max.Y), Math.Max(max.Z, box.Max.Z));
            }
            return min == null ? null : new BoxInfo { Min = Metres(min), Max = Metres(max) };
        }

        private static void ReadProjectPoints(Document doc, DocumentEntry entry)
        {
            BasePoint basePoint = BasePoint.GetProjectBasePoint(doc);
            BasePoint surveyPoint = BasePoint.GetSurveyPoint(doc);
            entry.BasePoint = PointInfo(basePoint);
            entry.SurveyPoint = PointInfo(surveyPoint);
            ProjectPosition position = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            entry.AngleToTrueNorthDeg = UnitMath.RadiansToDegrees(position.Angle);
        }

        private static ProjectPointInfo PointInfo(BasePoint point)
        {
            if (point == null)
            {
                return null;
            }
            return new ProjectPointInfo { Position = Metres(point.Position), SharedPosition = Metres(point.SharedPosition) };
        }

        private static void ReadProject(Document doc, IList<string> keyNames, DocumentEntry entry)
        {
            var placed = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(instance => instance.Symbol != null)
                .ToLookup(instance => instance.Symbol.Id.Value);
            var symbols = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .OrderBy(symbol => symbol.FamilyName)
                .ThenBy(symbol => symbol.Name);

            foreach (FamilySymbol symbol in symbols)
            {
                var instances = placed[symbol.Id.Value].ToList();
                if (instances.Count == 0)
                {
                    entry.Prototypes.Add(new PrototypeRow
                    {
                        FamilyName = symbol.FamilyName,
                        TypeName = symbol.Name,
                        Placement = "type only",
                        ElementId = symbol.Id.Value,
                        Parameters = ParameterValues(null, symbol, keyNames),
                    });
                    continue;
                }
                foreach (FamilyInstance instance in instances)
                {
                    entry.Prototypes.Add(InstanceRow(doc, symbol, instance, keyNames));
                }
            }
        }

        private static PrototypeRow InstanceRow(Document doc, FamilySymbol symbol, FamilyInstance instance, IList<string> keyNames)
        {
            PrototypeRow row = PlaceRow(doc, symbol, instance);
            row.Parameters = ParameterValues(instance, symbol, keyNames);
            return row;
        }

        // Everything about a placed instance except its parameters, shared by the host rows and the link rows
        internal static PrototypeRow PlaceRow(Document doc, FamilySymbol symbol, FamilyInstance instance)
        {
            var row = new PrototypeRow
            {
                FamilyName = symbol.FamilyName,
                TypeName = symbol.Name,
                Placement = "placed",
                ElementId = instance.Id.Value,
                Level = LevelName(doc, instance),
            };

            var location = instance.Location as LocationPoint;
            if (location != null)
            {
                row.InsertionPoint = Metres(location.Point);
                row.RotationDeg = UnitMath.RadiansToDegrees(location.Rotation);
            }
            else
            {
                Transform transform = instance.GetTransform();
                row.InsertionPoint = Metres(transform.Origin);
                row.RotationDeg = UnitMath.RadiansToDegrees(Math.Atan2(transform.BasisX.Y, transform.BasisX.X));
            }

            BoundingBoxXYZ box = instance.get_BoundingBox(null);
            if (box != null)
            {
                row.BoundingBox = new BoxInfo { Min = Metres(box.Min), Max = Metres(box.Max) };
            }
            return row;
        }

        private static string LevelName(Document doc, FamilyInstance instance)
        {
            ElementId levelId = instance.LevelId;
            if (levelId == ElementId.InvalidElementId)
            {
                Parameter reference = instance.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                levelId = reference == null ? ElementId.InvalidElementId : reference.AsElementId();
            }
            if (levelId == ElementId.InvalidElementId)
            {
                return null;
            }
            Element level = doc.GetElement(levelId);
            return level == null ? null : level.Name;
        }

        private static Dictionary<string, object> ParameterValues(FamilyInstance instance, FamilySymbol symbol, IList<string> keyNames)
        {
            var values = new Dictionary<string, object>();
            foreach (string name in keyNames)
            {
                Parameter parameter = (instance == null ? null : instance.LookupParameter(name)) ?? symbol.LookupParameter(name);
                if (parameter == null || !parameter.HasValue)
                {
                    continue;
                }
                object value = ParameterValue(parameter);
                if (value != null)
                {
                    values[name] = value;
                }
            }
            return values;
        }

        internal static object ParameterValue(Parameter parameter)
        {
            switch (parameter.StorageType)
            {
                case StorageType.Double:
                    return Convert(parameter.AsDouble(), parameter.Definition.GetDataType());
                case StorageType.Integer:
                    return parameter.AsInteger();
                case StorageType.String:
                    return parameter.AsString();
                case StorageType.ElementId:
                    return parameter.AsValueString();
                default:
                    return null;
            }
        }

        internal static double Convert(double internalValue, ForgeTypeId spec)
        {
            if (spec == SpecTypeId.Length)
            {
                return UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Meters);
            }
            if (spec == SpecTypeId.Area)
            {
                return UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.SquareMeters);
            }
            if (spec == SpecTypeId.Volume)
            {
                return UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.CubicMeters);
            }
            if (spec == SpecTypeId.Angle)
            {
                return UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Degrees);
            }
            return internalValue;
        }

        private static bool IsKey(string name, IList<string> keyNames)
        {
            return keyNames.Any(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
        }

        internal static double[] Metres(XYZ point)
        {
            return new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y), UnitMath.FeetToMetres(point.Z) };
        }
    }
}
