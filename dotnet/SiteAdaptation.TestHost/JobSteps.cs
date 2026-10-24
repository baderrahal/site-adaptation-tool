using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using RevitPrototypeReader;
using SiteAdaptation.Common;
using SiteAdaptation.Placer;

namespace SiteAdaptation.TestHost
{
    // The Revit work of each step of R102. Each returns one line of what it found and throws when the step fails
    internal sealed class JobSteps
    {
        private readonly Application app;
        private readonly TestHostJob job;
        private readonly string documents;

        public JobSteps(Application app, TestHostJob job, string documents)
        {
            this.app = app;
            this.job = job;
            this.documents = documents;
        }

        private bool IsPrototype(string fileName)
        {
            return string.Equals(fileName, job.PrototypeFile, StringComparison.OrdinalIgnoreCase);
        }

        // Before the copy is opened, every Revit link but the prototype under test is marked to stay unloaded at the next open
        public string UnloadOtherLinks()
        {
            ModelPath path = ModelPathUtils.ConvertUserVisiblePathToModelPath(job.Model);
            TransmissionData data = TransmissionData.ReadTransmissionData(path);
            if (data == null)
            {
                throw new InvalidOperationException("the copy holds no transmission data");
            }
            int links = 0;
            int unloaded = 0;
            int kept = 0;
            int other = 0;
            foreach (ElementId id in data.GetAllExternalFileReferenceIds())
            {
                ExternalFileReference reference = data.GetLastSavedReferenceData(id);
                if (reference.ExternalFileReferenceType != ExternalFileReferenceType.RevitLink)
                {
                    other++;
                    continue;
                }
                links++;
                bool keep = IsPrototype(Path.GetFileName(ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetPath())));
                data.SetDesiredReferenceData(id, reference.GetPath(), reference.PathType, keep);
                if (keep)
                {
                    kept++;
                }
                else
                {
                    unloaded++;
                }
            }
            data.IsTransmitted = true;
            TransmissionData.WriteTransmissionData(path, data);
            return "Revit links " + links + ", set to unloaded " + unloaded + ", left to load " + kept + " (the prototype under test), other references left as they were " + other;
        }

        public string Open(out Document document)
        {
            string original = TestHostGuard.OriginalOf(job.Model);
            if (original == null || string.Equals(Path.GetFullPath(original), Path.GetFullPath(job.Model), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("the model is not a test copy, it is not opened");
            }
            var options = new OpenOptions { Audit = false };
            bool workshared = BasicFileInfo.Extract(job.Model).IsWorkshared;
            if (workshared)
            {
                // The copy is a central model, so it is opened detached and saved as a plain model over itself
                options.DetachFromCentralOption = DetachFromCentralOption.DetachAndDiscardWorksets;
            }
            document = app.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(job.Model), options);
            return "opened " + document.Title + (workshared ? ", detached with worksets discarded" : "") + ". " + LinkState(document);
        }

        public string RemovePrototypeType(Document document)
        {
            List<RevitLinkType> types = Types(document).Where(type => !type.IsNestedLink && IsPrototype(FileOf(type))).ToList();
            int instances = new FilteredElementCollector(document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
                .Count(instance => types.Any(type => type.Id == instance.GetTypeId()));
            if (types.Count > 0)
            {
                using (var transaction = new Transaction(document, "Remove the prototype under test"))
                {
                    transaction.Start();
                    document.Delete(types.Select(type => type.Id).ToList());
                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                    {
                        throw new InvalidOperationException("removing the link type did not commit: " + status);
                    }
                }
            }
            var save = new SaveAsOptions { OverwriteExistingFile = true, MaximumBackups = 1 };
            document.SaveAs(job.Model, save);
            return "removed link types " + types.Count + " with " + instances + " instances, saved over the copy. " + LinkState(document);
        }

        public string Place(Document document, bool real)
        {
            PlanFile plan = PlanFile.Read(job.Plan);
            PlacerReport report = PlaceFromPlan.Run(document, plan, job.Plan, real, job.PrototypeFile);
            string part = real ? "placer_report" : "placer_dryrun";
            JsonOut.Write(TestHostGuard.OutputPath(documents, job.Code, part), report);
            PlacerCounts counts = report.Counts;
            string line = "in plan " + counts.PlotsInPlan + ", left out by the filter " + counts.FilteredOut + ", " + (real ? "placed " : "would place ") + counts.Placed
                + ", skipped " + counts.Skipped + ", parameters written " + counts.ParametersWritten + ", issues " + counts.Issues;
            if (report.Stopped)
            {
                throw new InvalidOperationException("the placer stopped: " + report.Failure + " (" + line + ")");
            }
            if (report.RolledBack)
            {
                throw new InvalidOperationException("the placer rolled back: " + report.Failure + " (" + line + ")");
            }
            return line + (real ? ". " + LinkState(document) : "");
        }

        public string Save(Document document)
        {
            document.Save();
            return "saved " + document.PathName;
        }

        // Only the prototype under test stays loaded before the read. Links nested in it come with it and are counted
        public string KeepOnlyPrototype(Document document)
        {
            int unloaded = 0;
            foreach (RevitLinkType type in Types(document).Where(type => !type.IsNestedLink && type.GetLinkedFileStatus() == LinkedFileStatus.Loaded && !IsPrototype(FileOf(type))).ToList())
            {
                type.Unload(null);
                unloaded++;
            }
            List<RevitLinkType> prototype = Types(document).Where(type => !type.IsNestedLink && IsPrototype(FileOf(type))).ToList();
            if (prototype.Count != 1 || prototype[0].GetLinkedFileStatus() != LinkedFileStatus.Loaded)
            {
                throw new InvalidOperationException("the prototype under test is not one loaded link type: " + string.Join(", ", prototype.Select(type => type.GetLinkedFileStatus().ToString())));
            }
            return "unloaded now " + unloaded + ". " + LinkState(document);
        }

        public string Read(Document document)
        {
            PrototypesFile file = PrototypeReading.ReadOpenDocument(app, document);
            string path = TestHostGuard.OutputPath(documents, job.Code, "prototypes");
            JsonOut.Write(path, file);
            DocumentEntry entry = file.Documents[0];
            List<LinkEntry> links = entry.Links ?? new List<LinkEntry>();
            List<SpotRow> spots = entry.SpotElevations ?? new List<SpotRow>();
            return "links " + links.Count + ", loaded " + links.Count(link => link.Loaded == true)
                + ", loaded with a box " + links.Count(link => link.Loaded == true && link.BoundingBoxCorners != null)
                + ", spots " + spots.Count + ", spots with a host " + spots.Count(spot => spot.HostSource != null)
                + ", errors " + (entry.Errors == null ? 0 : entry.Errors.Count)
                + ", file " + (new FileInfo(path).Length / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }

        public string Close(Document document)
        {
            if (document == null || !document.IsValidObject)
            {
                return "no document to close";
            }
            bool closed = document.Close(false);
            return closed ? "closed without saving again" : "Revit did not close the document";
        }

        private static IEnumerable<RevitLinkType> Types(Document document)
        {
            return new FilteredElementCollector(document).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>();
        }

        private static string FileOf(RevitLinkType type)
        {
            try
            {
                ExternalFileReference reference = type.GetExternalFileReference();
                if (reference != null)
                {
                    return Path.GetFileName(ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath()));
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // A nested link has no reference of its own, its type name stands in
            }
            return type.Name;
        }

        // Counts only, names of the loaded types besides the prototype under test
        private string LinkState(Document document)
        {
            List<RevitLinkType> types = Types(document).ToList();
            List<RevitLinkType> loaded = types.Where(type => type.GetLinkedFileStatus() == LinkedFileStatus.Loaded).ToList();
            List<string> others = loaded.Where(type => !type.IsNestedLink && !IsPrototype(FileOf(type))).Select(FileOf).ToList();
            return "link types " + types.Count + ", loaded " + loaded.Count + ", loaded nested " + loaded.Count(type => type.IsNestedLink)
                + ", loaded besides the prototype " + others.Count + (others.Count > 0 ? " (" + string.Join(", ", others) + ")" : "");
        }
    }
}
