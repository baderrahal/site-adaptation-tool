using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace SiteAdaptation.Placer
{
    // The open document seen through IPlacerTarget. It carries out the steps Placer asks for and decides nothing
    internal sealed class RevitPlacerTarget : IPlacerTarget
    {
        private readonly Document document;
        private readonly WarningCollector collector = new WarningCollector();
        private Transaction transaction;

        public RevitPlacerTarget(Document document)
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

        public IList<string> LinkPlotNumbers()
        {
            var numbers = new List<string>();
            foreach (RevitLinkInstance instance in new FilteredElementCollector(document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                Parameter parameter = instance.LookupParameter(LinkFields.PlotNumber);
                numbers.Add(parameter == null || !parameter.HasValue ? null : parameter.AsString());
            }
            return numbers;
        }

        public IList<LinkTypeEntry> LinkTypes()
        {
            var types = new List<LinkTypeEntry>();
            foreach (RevitLinkType type in new FilteredElementCollector(document).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>())
            {
                string name = null;
                try
                {
                    ExternalFileReference reference = type.GetExternalFileReference();
                    string path = reference == null ? null : ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath());
                    name = string.IsNullOrEmpty(path) ? null : Path.GetFileName(path);
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    // A type with no usable file reference cannot stand in for a prototype file
                }
                if (name != null)
                {
                    types.Add(new LinkTypeEntry { FileName = name, Key = Key(type.Id) });
                }
            }
            return types;
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public void Begin()
        {
            transaction = new Transaction(document, "Place from plan");
            FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(collector);
            transaction.SetFailureHandlingOptions(options);
            transaction.Start();
        }

        public string LoadLinkType(string fullPath)
        {
            ModelPath path = ModelPathUtils.ConvertUserVisiblePathToModelPath(fullPath);
            LinkLoadResult result = RevitLinkType.Create(document, path, new RevitLinkOptions(false));
            if (result == null || !LinkLoadResult.IsCodeSuccess(result.LoadResult))
            {
                throw new InvalidOperationException("the link " + fullPath + " did not load: " + (result == null ? "no result" : result.LoadResult.ToString()));
            }
            return Key(result.ElementId);
        }

        public long Place(string linkTypeKey, PlacementSteps steps)
        {
            RevitLinkInstance instance = RevitLinkInstance.Create(document, new ElementId(long.Parse(linkTypeKey, CultureInfo.InvariantCulture)), ImportPlacement.Origin);
            ElementId id = instance.Id;
            XYZ point = Feet(steps.Move);
            ElementTransformUtils.MoveElement(document, id, point);
            ElementTransformUtils.RotateElement(document, id, Line.CreateBound(point, point + XYZ.BasisZ), steps.RotationRad);
            if (steps.MirrorNormal != null)
            {
                var ids = new List<ElementId> { id };
                if (!ElementTransformUtils.CanMirrorElements(document, ids))
                {
                    throw new InvalidOperationException("Revit says link instance " + id.Value + " cannot be mirrored");
                }
                // mirrorCopies false mirrors the instance itself, so no copy is left behind
                ElementTransformUtils.MirrorElements(document, ids, Plane.CreateByNormalAndOrigin(new XYZ(steps.MirrorNormal[0], steps.MirrorNormal[1], steps.MirrorNormal[2]), point), false);
            }
            return id.Value;
        }

        public PlacedTransform ReadBack(long instanceId)
        {
            document.Regenerate();
            var instance = document.GetElement(new ElementId(instanceId)) as RevitLinkInstance;
            if (instance == null)
            {
                return null;
            }
            Transform transform = instance.GetTotalTransform();
            return new PlacedTransform
            {
                Origin = Metres(transform.Origin),
                AxisX = Axis(transform.BasisX),
                AxisY = Axis(transform.BasisY),
                AxisZ = Axis(transform.BasisZ),
            };
        }

        public ParameterWrite Write(long instanceId, string name, string value)
        {
            Element element = document.GetElement(new ElementId(instanceId));
            Parameter parameter = element == null ? null : element.LookupParameter(name);
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
            if (transaction != null && transaction.GetStatus() == TransactionStatus.Started)
            {
                transaction.RollBack();
            }
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

        private static double[] Axis(XYZ vector)
        {
            return new[] { vector.X, vector.Y, vector.Z };
        }

        // Warnings are kept for the report and stay in the model. An error makes Revit roll the transaction back
        private sealed class WarningCollector : IFailuresPreprocessor
        {
            public readonly List<string> Lines = new List<string>();
            public readonly List<string> Errors = new List<string>();

            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                bool error = false;
                foreach (FailureMessageAccessor message in accessor.GetFailureMessages())
                {
                    if (message.GetSeverity() == FailureSeverity.Warning)
                    {
                        Lines.Add(message.GetDescriptionText());
                    }
                    else
                    {
                        Errors.Add(message.GetDescriptionText());
                        error = true;
                    }
                }
                return error ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
            }
        }
    }
}
