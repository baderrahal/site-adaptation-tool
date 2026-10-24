using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;
using SiteAdaptation.Placer;

namespace SiteAdaptation.TestHost
{
    // R117. The Revit work of the master and floor jobs. Each step returns one line of what it found and throws when the step fails
    internal sealed class FloorSteps
    {
        private readonly TestHostJob job;
        private readonly string documents;
        private PlanFile plan;

        public FloorSteps(TestHostJob job, string documents)
        {
            this.job = job;
            this.documents = documents;
        }

        private PlanFile Plan()
        {
            if (plan == null)
            {
                plan = PlanFile.Read(job.Plan);
                string settings = FloorPlacer.CheckSettings(plan);
                if (settings != null)
                {
                    throw new InvalidOperationException(settings);
                }
            }
            return plan;
        }

        // The floors of the types of floorSettings, all of them or those whose plot value belongs to a listed plot
        private List<Floor> Targets(Document document, bool all)
        {
            PlanFloorSettings settings = Plan().FloorSettings;
            var values = new HashSet<string>(StringComparer.Ordinal);
            if (!all)
            {
                var wanted = new HashSet<string>(job.DeletePlots, StringComparer.Ordinal);
                foreach (PlanPlot plot in Plan().Plots.Where(plot => plot != null && plot.Plot != null && wanted.Contains(plot.Plot.Trim()) && plot.Floors != null))
                {
                    foreach (PlanFloor floor in new[] { plot.Floors.Generic, plot.Floors.Yard, plot.Floors.Ramp })
                    {
                        if (floor != null && floor.PlotValue != null && !string.IsNullOrWhiteSpace(floor.PlotValue.Value))
                        {
                            values.Add(floor.PlotValue.Value.Trim());
                        }
                    }
                }
            }
            var found = new List<Floor>();
            foreach (Floor floor in new FilteredElementCollector(document).OfClass(typeof(Floor)).Cast<Floor>())
            {
                Element type = document.GetElement(floor.GetTypeId());
                if (type == null || FloorPlacer.KindOf(settings, type.Name) == null)
                {
                    continue;
                }
                Parameter value = floor.LookupParameter(settings.PlotParameter);
                string text = value == null || !value.HasValue || value.StorageType != StorageType.String ? null : value.AsString();
                if (all || (text != null && values.Contains(text.Trim())))
                {
                    found.Add(floor);
                }
            }
            return found;
        }

        // R117. Deletes the floors the job names. A floor inside a model group cannot go alone, so its groups are ungrouped first
        public string DeleteFloors(Document document)
        {
            bool all = job.DeleteFloors == "all";
            List<Floor> floors = Targets(document, all);
            var groups = new HashSet<long>(floors.Where(floor => floor.GroupId != ElementId.InvalidElementId).Select(floor => floor.GroupId.Value));
            var byType = floors.GroupBy(floor => document.GetElement(floor.GetTypeId()).Name).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key + " " + group.Count()).ToList();
            int members = 0;
            using (var transaction = new Transaction(document, "Delete the floors of the test"))
            {
                transaction.Start();
                foreach (long id in groups)
                {
                    var group = document.GetElement(new ElementId(id)) as Group;
                    if (group != null)
                    {
                        members += group.UngroupMembers().Count;
                    }
                }
                if (floors.Count > 0)
                {
                    document.Delete(floors.Select(floor => floor.Id).ToList());
                }
                TransactionStatus status = transaction.Commit();
                if (status != TransactionStatus.Committed)
                {
                    throw new InvalidOperationException("deleting the floors did not commit: " + status);
                }
            }
            int left = Targets(document, all).Count;
            if (left != 0)
            {
                throw new InvalidOperationException(left + " floors are still there after the delete");
            }
            return "deleted " + floors.Count + " floors (" + string.Join(", ", byType) + "), groups ungrouped " + groups.Count + " with " + members + " members, left 0. "
                + "Floors of the types now " + Targets(document, true).Count;
        }

        // R113 to R116 in Revit. links and floors, or floors alone, written to testhost_<code>_<part>.json
        public string Place(Document document, bool real, string part)
        {
            bool links = job.Kind == TestHostGuard.LinksAndFloorsKind;
            PlacerRunReport report = PlaceFromPlan.RunSteps(document, Plan(), job.Plan, real, links, true, links ? job.PrototypeFile : null, job.Plots);
            JsonOut.Write(TestHostGuard.OutputPath(documents, job.Code, part), report);
            FloorReport floors = report.Floors;
            string line = "floors " + (real ? "placed " : "would place ") + floors.Counts.Placed + " (" + Kinds(floors.Counts.PlacedByKind) + "), skipped " + floors.Counts.Skipped
                + ", plot values written " + floors.Counts.PlotValuesWritten + ", issues " + floors.Counts.Issues + ", floors before " + floors.FloorsBefore + " after " + floors.FloorsAfter;
            if (report.Links != null)
            {
                line = "links " + (real ? "placed " : "would place ") + report.Links.Counts.Placed + ", skipped " + report.Links.Counts.Skipped + ". " + line;
                if (report.Links.Stopped || report.Links.RolledBack)
                {
                    throw new InvalidOperationException("the link step " + (report.Links.Stopped ? "stopped: " : "rolled back: ") + report.Links.Failure + " (" + line + ")");
                }
            }
            string ended = floors.Stopped ? "stop" : floors.RolledBack ? "rollBack" : null;
            // A scenario that wants a stop or a roll back passes only on that, and a clean run passes only when nothing stopped
            if (ended != job.Expect && !(job.Expect != null && !real && ended == null))
            {
                throw new InvalidOperationException("the floor step ended " + (ended ?? "clean") + ", the job expects " + (job.Expect ?? "clean") + ": " + floors.Failure + " (" + line + ")");
            }
            return line + (ended == null ? "" : ", ended " + ended + " as expected: " + floors.Failure);
        }

        private static string Kinds(Dictionary<string, int> counts)
        {
            return counts == null ? "" : string.Join(", ", counts.Select(pair => pair.Key + " " + pair.Value.ToString(CultureInfo.InvariantCulture)));
        }

        // R117. Saved over the copy with one backup at most, and the backup the save leaves is deleted at once
        public static string SaveOver(Document document, string model)
        {
            if (!TestHostFiles.IsTestCopy(model))
            {
                throw new InvalidOperationException("only a test copy is saved: " + model);
            }
            document.SaveAs(model, new SaveAsOptions { OverwriteExistingFile = true, MaximumBackups = 1 });
            return "saved over the copy, " + TestHostFiles.DeleteBackups(model);
        }
    }
}
