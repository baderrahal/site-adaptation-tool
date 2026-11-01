using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.Common;
using SiteAdaptation.Common.PlaceVillas;

namespace SiteAdaptation.AddIn.Revit
{
    // R450, R451. The Revit side of the mass files: reads the walls and the slab of a prototype from a copy, as the base shape reader
    // does (D19), and writes one mass as a new Revit 2024 project. MassFileSet of Common decides the base, the name and when to make it
    internal sealed class RevitMassFileMaker : IMassFileMaker
    {
        private readonly UIApplication application;
        private readonly string copyFolder;

        public RevitMassFileMaker(UIApplication application, string workFolder)
        {
            this.application = application;
            copyFolder = Path.Combine(workFolder, ShapeCache.Folder, "copies");
        }

        public MassBaseRead ReadBase(string prototypePath, LightModeConfig config)
        {
            return RevitBaseShapeReader.Opened(application, copyFolder, prototypePath, opened => new MassBaseRead
            {
                WallLoops = RevitBaseShapeReader.WallLoops(opened, config.MassFiles.WallCutHeightM),
                Slab = RevitBaseShapeReader.Outer(opened, config),
            });
        }

        // The mass stands at Z 0 of its file with its plan in the internal coordinates of the prototype, so a link placed origin to origin
        // with the steps of the prototype link stands where the prototype would
        public void Write(string massPath, IReadOnlyList<double[]> ring, double heightM)
        {
            Application revit = application.Application;
            revit.FailuresProcessing += RevitBaseShapeReader.OnFailures;
            Document mass = null;
            try
            {
                mass = revit.NewProjectDocument(UnitSystem.Metric);
                using (var transaction = new Transaction(mass, "Mass of " + Path.GetFileNameWithoutExtension(massPath)))
                {
                    transaction.Start();
                    DirectShape shape = DirectShape.CreateElement(mass, new ElementId(RevitVillaDocument.SolidCategory));
                    shape.ApplicationId = RevitVillaDocument.ApplicationId;
                    shape.SetShape(new GeometryObject[] { RevitVillaDocument.Extrusion(ring, heightM, new PlacementSteps { Move = new[] { 0.0, 0.0, 0.0 } }) });
                    transaction.Commit();
                }
                mass.SaveAs(massPath, new SaveAsOptions { OverwriteExistingFile = true });
            }
            finally
            {
                mass?.Close(false);
                revit.FailuresProcessing -= RevitBaseShapeReader.OnFailures;
            }
        }
    }
}
