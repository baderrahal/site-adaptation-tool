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
    // The Revit work of each step of R104. Each returns one line of what it found and throws when the step fails
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
            string saved = FloorSteps.SaveOver(document, job.Model);
            return "removed link types " + types.Count + " with " + instances + " instances, " + saved + ". " + LinkState(document);
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

        // R117. Saved over the copy and the backup the save leaves is deleted
        public string Save(Document document)
        {
            return FloorSteps.SaveOver(document, job.Model) + ", " + document.PathName;
        }

        // Only the prototype under test stays loaded before the read. Links nested in it come with it and are counted
        public string KeepOnlyPrototype(Document document)
        {
            int unloaded = 0;
            foreach (RevitLinkType type in Types(document).Where(type => !type.IsNestedLink && type.GetLinkedFileStatus() == LinkedFileStatus.Loaded && !IsPrototype(FileOf(type))).ToList())
            {
                type.Unload(new KeepLinkPositions());
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

        // R109. The saved version and the worksharing of a file, read without opening it
        public string FileLine(string path)
        {
            BasicFileInfo info = BasicFileInfo.Extract(path);
            return "format " + info.Format + ", workshared " + info.IsWorkshared + ", central " + info.IsCentral
                + ", size " + new FileInfo(path).Length + " bytes";
        }

        // The source opens detached when it is workshared, so nothing is written to it or to its central.
        // The links in it stay as they are, they are counted and not loaded or changed here
        public string OpenSource(out Document document)
        {
            var options = new OpenOptions { Audit = false };
            bool workshared = BasicFileInfo.Extract(job.Source).IsWorkshared;
            if (workshared)
            {
                options.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;
            }
            document = app.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(job.Source), options);
            List<RevitLinkType> links = Types(document).ToList();
            return "opened " + document.Title + (workshared ? ", detached with worksets kept" : "")
                + ". Links in it " + links.Count + ", loaded " + links.Count(type => type.GetLinkedFileStatus() == LinkedFileStatus.Loaded);
        }

        // A workshared file becomes a new central in the target folder, any other file a copy. Nothing is ever overwritten
        public string SaveTarget(Document document)
        {
            if (File.Exists(job.Target))
            {
                throw new InvalidOperationException("the target exists, it is never overwritten: " + job.Target);
            }
            var save = new SaveAsOptions { OverwriteExistingFile = false, MaximumBackups = 1 };
            bool workshared = document.IsWorkshared;
            if (workshared)
            {
                save.SetWorksharingOptions(new WorksharingSaveAsOptions { SaveAsCentral = true });
            }
            document.SaveAs(job.Target, save);
            return "saved " + (workshared ? "as a new central " : "as a copy ") + job.Target;
        }

        public string CheckTarget()
        {
            if (!File.Exists(job.Target))
            {
                throw new InvalidOperationException("the target was not written: " + job.Target);
            }
            BasicFileInfo info = BasicFileInfo.Extract(job.Target);
            string line = FileLine(job.Target);
            if (info.Format != app.VersionNumber)
            {
                throw new InvalidOperationException("the target is saved in " + info.Format + ", not " + app.VersionNumber + ": " + line);
            }
            if (Path.GetFileName(job.Target) != Path.GetFileName(job.Source))
            {
                throw new InvalidOperationException("the target name is not the source name");
            }
            return line;
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

        // Positions of a link that changed are never saved back into the link file on unload
        private sealed class KeepLinkPositions : ISaveSharedCoordinatesCallback
        {
            public SaveModifiedLinksOptions GetSaveModifiedLinksOption(RevitLinkType link)
            {
                return SaveModifiedLinksOptions.DoNotSaveLinks;
            }
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
