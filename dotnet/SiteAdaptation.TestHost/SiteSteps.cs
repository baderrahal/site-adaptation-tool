using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace SiteAdaptation.TestHost
{
    // R212 and R214 in Revit. The new site file from a template, and a plan view image. The math and the checks are in SiteCoordinates
    internal sealed class SiteSteps
    {
        private const double CutAboveLevelM = 2000;
        private const int ImageScale = 500;
        private const int ImagePixels = 3000;

        private readonly Application app;
        private readonly TestHostJob job;
        private readonly string documents;
        private SiteSpec spec;
        private readonly SiteReport report = new SiteReport();

        public SiteSteps(Application app, TestHostJob job, string documents)
        {
            this.app = app;
            this.job = job;
            this.documents = documents;
        }

        private SiteSpec Spec()
        {
            if (spec == null)
            {
                spec = SiteSpec.Read(job.Site);
                string problem = SiteCoordinates.Check(spec);
                if (problem != null)
                {
                    throw new InvalidOperationException(problem);
                }
                if (File.Exists(spec.SharedParameterFile))
                {
                    throw new InvalidOperationException("the shared parameter file is there already, it is never overwritten: " + spec.SharedParameterFile);
                }
            }
            return spec;
        }

        public string Create(out Document document)
        {
            Spec();
            document = app.NewProjectDocument(job.Template);
            report.Template = job.Template;
            return "made a new project from " + Path.GetFileName(job.Template) + ", levels " + Levels(document).Count + ", floor types " + FloorTypes(document).Count;
        }

        // R211. The internal origin sits at the shared position of the spec, then Revit's own transform is checked against the spec
        public string Coordinates(Document document)
        {
            double[] feet = SiteCoordinates.ProjectPositionFeet(Spec().ProjectPosition);
            Commit(document, "Set the shared coordinates", () =>
                document.ActiveProjectLocation.SetProjectPosition(XYZ.Zero, new ProjectPosition(feet[0], feet[1], feet[2], feet[3])));
            BasePoint survey = BasePoint.GetSurveyPoint(document);
            // GetTotalTransform takes a shared point to its internal point, the check points and the survey point test it both ways
            Transform total = document.ActiveProjectLocation.GetTotalTransform();
            report.SurveyPoint = new SurveyPointOut { Position = Metres(survey.Position), SharedPosition = Metres(survey.SharedPosition) };
            ProjectPosition read = document.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            report.AngleToTrueNorthDeg = Math.Round(read.Angle * 180.0 / Math.PI, 6);
            report.ProjectPositionM = new[] { read.EastWest * SiteCoordinates.MetresPerFoot, read.NorthSouth * SiteCoordinates.MetresPerFoot, read.Elevation * SiteCoordinates.MetresPerFoot };
            report.CheckPoints = SiteCoordinates.CompareCheckPoints(Spec(), shared => Metres(total.OfPoint(Feet(shared))));
            double[] expected = SiteCoordinates.SurveyPointInternal(Spec().ProjectPosition);
            report.SurveyGapMm = SiteCoordinates.Gap(expected, report.SurveyPoint.Position) * 1000.0;
            double worst = report.CheckPoints.Select(point => point.GapMm).DefaultIfEmpty(0).Max();
            if (worst > SiteCoordinates.ToleranceMm || report.SurveyGapMm > SiteCoordinates.ToleranceMm)
            {
                throw new InvalidOperationException("Revit puts a point " + Text(Math.Max(worst, report.SurveyGapMm)) + " mm from where the spec says");
            }
            return "project position set, angle " + Text(report.AngleToTrueNorthDeg) + " deg, check points " + report.CheckPoints.Count + " within " + Text(worst) + " mm, survey point within " + Text(report.SurveyGapMm) + " mm";
        }

        // The levels of the spec by name. One the template has is kept as it is, one it lacks is made at 0 with a plan view
        public string LevelsByName(Document document)
        {
            var made = new List<string>();
            Commit(document, "Levels of the plan", () =>
            {
                ElementId planType = new FilteredElementCollector(document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                    .First(type => type.ViewFamily == ViewFamily.FloorPlan).Id;
                foreach (string name in Spec().Levels.Where(name => Levels(document).All(level => level.Name != name)))
                {
                    Level level = Level.Create(document, 0);
                    level.Name = name;
                    ViewPlan.Create(document, planType, level.Id);
                    made.Add(name);
                }
            });
            report.Levels = Spec().Levels.Select(name => Levels(document).First(level => level.Name == name))
                .Select(level => new LevelOut { Name = level.Name, ElevationM = Math.Round(level.Elevation * SiteCoordinates.MetresPerFoot, 4), Made = made.Contains(level.Name) }).ToList();
            return "levels " + string.Join(", ", report.Levels.Select(level => level.Name + (level.Made ? " made" : " kept"))) + ", elevations in metres " + string.Join(", ", report.Levels.Select(level => Text(level.ElevationM)));
        }

        // The floor types of the spec by name, each a copy of the first generic type the template has, else of its first floor type
        public string FloorTypesByName(Document document)
        {
            var made = new List<string>();
            Commit(document, "Floor types of the plan", () =>
            {
                List<FloorType> types = FloorTypes(document);
                FloorType source = types.FirstOrDefault(type => Spec().FloorTypes["generic"].Contains(type.Name)) ?? types.First();
                foreach (string name in Spec().FloorTypeNames().Where(name => types.All(type => type.Name != name)))
                {
                    source.Duplicate(name);
                    made.Add(name);
                }
            });
            report.FloorTypes = Spec().FloorTypeNames().ToDictionary(name => name, name => made.Contains(name) ? "made" : "kept");
            return "floor types made " + made.Count + " (" + string.Join(", ", made) + "), kept " + (report.FloorTypes.Count - made.Count);
        }

        // The Plot parameter on floors and the link parameters on Revit links, text instance parameters from a new shared parameter file.
        // The shared parameter file setting of Revit is put back as it was, also when a step fails
        public string Parameters(Document document)
        {
            string before = app.SharedParametersFilename;
            File.WriteAllText(Spec().SharedParameterFile, "");
            try
            {
                app.SharedParametersFilename = Spec().SharedParameterFile;
                DefinitionFile file = app.OpenSharedParameterFile();
                DefinitionGroup group = file.Groups.Create(Spec().ParameterGroup);
                var bound = new List<string>();
                Commit(document, "Parameters of the plan", () =>
                {
                    Bind(document, group, Spec().FloorParameter, BuiltInCategory.OST_Floors);
                    bound.Add(Spec().FloorParameter);
                    foreach (string name in Spec().LinkParameters)
                    {
                        Bind(document, group, name, BuiltInCategory.OST_RvtLinks);
                        bound.Add(name);
                    }
                });
                report.Parameters = bound;
                return "parameters bound " + bound.Count + ", " + Spec().FloorParameter + " on floors, " + Spec().LinkParameters.Count + " on Revit links, all text";
            }
            finally
            {
                app.SharedParametersFilename = before;
            }
        }

        private void Bind(Document document, DefinitionGroup group, string name, BuiltInCategory category)
        {
            Definition definition = group.Definitions.Create(new ExternalDefinitionCreationOptions(name, SpecTypeId.String.Text));
            CategorySet categories = app.Create.NewCategorySet();
            categories.Insert(Category.GetCategory(document, category));
            if (!document.ParameterBindings.Insert(definition, app.Create.NewInstanceBinding(categories), GroupTypeId.Data))
            {
                throw new InvalidOperationException("Revit did not bind the parameter " + name);
            }
        }

        // R213. The new file is written once and never over a file that is there
        public string SaveNew(Document document)
        {
            if (File.Exists(job.Model))
            {
                throw new InvalidOperationException("the new site file is there already, it is never overwritten: " + job.Model);
            }
            document.SaveAs(job.Model, new SaveAsOptions { OverwriteExistingFile = false, MaximumBackups = 1 });
            report.Model = job.Model;
            report.Format = BasicFileInfo.Extract(job.Model).Format;
            JsonOut.Write(TestHostGuard.OutputPath(documents, job.Code, "site"), report);
            return "saved " + job.Model + ", format " + report.Format + ", " + TestHostFiles.DeleteBackups(job.Model);
        }

        // R214. A floor plan of the area, cut high above the level so floors and links are seen from above, exported as one PNG
        public string Image(Document document)
        {
            Level level = Levels(document).FirstOrDefault(item => item.Name == job.ImageLevel);
            if (level == null)
            {
                throw new InvalidOperationException("the model has no level " + job.ImageLevel);
            }
            ViewPlan view = null;
            Commit(document, "Plan view of the area", () =>
            {
                ElementId planType = new FilteredElementCollector(document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                    .First(type => type.ViewFamily == ViewFamily.FloorPlan).Id;
                view = ViewPlan.Create(document, planType, level.Id);
                view.Name = "testhost " + job.Code + " area";
                view.Scale = ImageScale;
                view.DetailLevel = ViewDetailLevel.Medium;
                PlanViewRange range = view.GetViewRange();
                range.SetLevelId(PlanViewPlane.TopClipPlane, PlanViewRange.Unlimited);
                range.SetLevelId(PlanViewPlane.CutPlane, level.Id);
                range.SetOffset(PlanViewPlane.CutPlane, CutAboveLevelM / SiteCoordinates.MetresPerFoot);
                range.SetLevelId(PlanViewPlane.BottomClipPlane, level.Id);
                range.SetOffset(PlanViewPlane.BottomClipPlane, -CutAboveLevelM / SiteCoordinates.MetresPerFoot);
                range.SetLevelId(PlanViewPlane.ViewDepthPlane, PlanViewRange.Unlimited);
                view.SetViewRange(range);
                Transform toInternal = document.ActiveProjectLocation.GetTotalTransform();
                XYZ a = toInternal.OfPoint(Feet(new[] { job.Area[0], job.Area[1], 0.0 }));
                XYZ b = toInternal.OfPoint(Feet(new[] { job.Area[2], job.Area[3], 0.0 }));
                BoundingBoxXYZ box = view.CropBox;
                Transform toView = box.Transform.Inverse;
                XYZ p = toView.OfPoint(a);
                XYZ q = toView.OfPoint(b);
                box.Min = new XYZ(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y), box.Min.Z);
                box.Max = new XYZ(Math.Max(p.X, q.X), Math.Max(p.Y, q.Y), box.Max.Z);
                view.CropBox = box;
                view.CropBoxActive = true;
                view.CropBoxVisible = false;
            });
            DateTime start = DateTime.Now.AddSeconds(-2);
            var options = new ImageExportOptions
            {
                ExportRange = ExportRange.SetOfViews,
                FilePath = Path.Combine(job.ImageFolder, "testhost_" + job.Code + "_plan"),
                HLRandWFViewsFileType = ImageFileType.PNG,
                ShadowViewsFileType = ImageFileType.PNG,
                ImageResolution = ImageResolution.DPI_150,
                ZoomType = ZoomFitType.FitToPage,
                FitDirection = FitDirectionType.Horizontal,
                PixelSize = ImagePixels,
            };
            options.SetViewsAndSheets(new List<ElementId> { view.Id });
            document.ExportImage(options);
            List<string> written = Directory.GetFiles(job.ImageFolder, "testhost_" + job.Code + "_plan*.png").Where(path => File.GetLastWriteTime(path) >= start).ToList();
            if (written.Count == 0)
            {
                throw new InvalidOperationException("Revit wrote no image");
            }
            return "image " + string.Join(", ", written.Select(Path.GetFileName)) + ", " + new FileInfo(written[0]).Length + " bytes, links in the view loaded "
                + new FilteredElementCollector(document, view.Id).OfClass(typeof(RevitLinkInstance)).GetElementCount()
                + ", floors in the view " + new FilteredElementCollector(document, view.Id).OfClass(typeof(Floor)).GetElementCount();
        }

        // R234. Before the open every Revit link is set to load, from its saved path when that file is there, else from the link
        // folder by file name. Only the transmission data of the copy changes, the link files are only read
        public string LoadAllLinks()
        {
            ModelPath path = ModelPathUtils.ConvertUserVisiblePathToModelPath(job.Model);
            TransmissionData data = TransmissionData.ReadTransmissionData(path);
            if (data == null)
            {
                return "no transmission data, no link to load";
            }
            int links = 0;
            int there = 0;
            int found = 0;
            int missing = 0;
            foreach (ElementId id in data.GetAllExternalFileReferenceIds())
            {
                ExternalFileReference reference = data.GetLastSavedReferenceData(id);
                if (reference.ExternalFileReferenceType != ExternalFileReferenceType.RevitLink)
                {
                    continue;
                }
                links++;
                string saved = ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath());
                string other = job.LinkFolder == null ? null : Path.Combine(job.LinkFolder, Path.GetFileName(saved));
                if (File.Exists(saved))
                {
                    data.SetDesiredReferenceData(id, reference.GetPath(), reference.PathType, true);
                    there++;
                }
                else if (other != null && File.Exists(other))
                {
                    data.SetDesiredReferenceData(id, ModelPathUtils.ConvertUserVisiblePathToModelPath(other), PathType.Absolute, true);
                    found++;
                }
                else
                {
                    missing++;
                }
            }
            data.IsTransmitted = true;
            TransmissionData.WriteTransmissionData(path, data);
            return "Revit links " + links + ", set to load from their path " + there + ", from the link folder " + found + ", not found " + missing;
        }

        private static void Commit(Document document, string name, Action action)
        {
            using (var transaction = new Transaction(document, name))
            {
                transaction.Start();
                action();
                TransactionStatus status = transaction.Commit();
                if (status != TransactionStatus.Committed)
                {
                    throw new InvalidOperationException(name + " did not commit: " + status);
                }
            }
        }

        private static List<Level> Levels(Document document)
        {
            return new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().ToList();
        }

        private static List<FloorType> FloorTypes(Document document)
        {
            return new FilteredElementCollector(document).OfClass(typeof(FloorType)).Cast<FloorType>().Where(type => !type.IsFoundationSlab).ToList();
        }

        private static XYZ Feet(double[] metres)
        {
            return new XYZ(metres[0] / SiteCoordinates.MetresPerFoot, metres[1] / SiteCoordinates.MetresPerFoot, metres[2] / SiteCoordinates.MetresPerFoot);
        }

        private static double[] Metres(XYZ point)
        {
            return new[] { point.X * SiteCoordinates.MetresPerFoot, point.Y * SiteCoordinates.MetresPerFoot, point.Z * SiteCoordinates.MetresPerFoot };
        }

        private static string Text(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    // testhost_<code>_site.json, what python/plan_from_book.py takes the survey point and the level elevations from
    internal sealed class SiteReport
    {
        public string Template { get; set; }
        public string Model { get; set; }
        public string Format { get; set; }
        public SurveyPointOut SurveyPoint { get; set; }
        public double[] ProjectPositionM { get; set; }
        public double AngleToTrueNorthDeg { get; set; }
        public double SurveyGapMm { get; set; }
        public List<CheckPointResult> CheckPoints { get; set; }
        public List<LevelOut> Levels { get; set; }
        public Dictionary<string, string> FloorTypes { get; set; }
        public List<string> Parameters { get; set; }
    }

    internal sealed class SurveyPointOut
    {
        public double[] Position { get; set; }
        public double[] SharedPosition { get; set; }
    }

    internal sealed class LevelOut
    {
        public string Name { get; set; }
        public double ElevationM { get; set; }
        public bool Made { get; set; }
    }
}
