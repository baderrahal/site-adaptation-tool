using System;
using System.Collections.Generic;
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
            // J8, Q127. The work root of the machine, not the Documents folder of Windows
            get { return Path.Combine(WorkRoot.OfThisMachine(), "placer_report.json"); }
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

        // R113. The floor step, every plot of the plan or the plots listed
        public static FloorReport RunFloors(Document document, PlanFile plan, string planPath, bool real, ICollection<string> plots)
        {
            var target = new RevitFloorTarget(document);
            try
            {
                return FloorPlacer.Run(plan, planPath, target, real, plots);
            }
            catch (Exception failure)
            {
                target.RollBack();
                FloorReport report = FloorPlacer.StoppedReport(real ? "real" : "dryRun", planPath, document.Title, "The floor step failed, nothing stays: " + failure.Message);
                report.RolledBack = real;
                return report;
            }
        }

        // R116. Links, floors or both. With both the links step runs first in its own transaction, then the floors step in its own
        public static PlacerRunReport RunSteps(Document document, PlanFile plan, string planPath, bool real, bool links, bool floors, string prototypeFile, ICollection<string> plots)
        {
            var report = new PlacerRunReport { Steps = links && floors ? "linksAndFloors" : links ? "links" : "floors" };
            if (links)
            {
                report.Links = Run(document, plan, planPath, real, prototypeFile);
            }
            if (floors)
            {
                report.Floors = RunFloors(document, plan, planPath, real, plots);
            }
            return report;
        }
    }
}
