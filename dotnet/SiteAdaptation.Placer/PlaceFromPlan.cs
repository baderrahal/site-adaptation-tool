using System;
using System.IO;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace SiteAdaptation.Placer
{
    // The one way into the placer, for the button and for the test host (R103), so neither holds a copy of the run
    public static class PlaceFromPlan
    {
        public static string ReportPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SiteAdaptation", "placer_report.json"); }
        }

        public static PlacerReport Run(Document document, PlanFile plan, string planPath, bool real, string prototypeFile)
        {
            var target = new RevitPlacerTarget(document);
            try
            {
                return Common.Placer.Run(plan, planPath, target, real, prototypeFile);
            }
            catch (Exception failure)
            {
                // Run rolls its own transaction back, so only a failure outside it lands here
                target.RollBack();
                PlacerReport report = Common.Placer.StoppedReport(real ? "real" : "dryRun", planPath, document.Title, "The run failed, nothing stays: " + failure.Message);
                report.PrototypeFilter = prototypeFile;
                report.RolledBack = real;
                return report;
            }
        }
    }
}
