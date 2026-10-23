using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using RevitApp = Autodesk.Revit.ApplicationServices.Application;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.Common;

namespace RevitPrototypeReader
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportPrototypesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            RevitApp app = uiApp.Application;
            Document active = uiApp.ActiveUIDocument == null ? null : uiApp.ActiveUIDocument.Document;

            var choice = new TaskDialog("Read Prototypes")
            {
                MainInstruction = "Which prototypes should be read?",
                MainContent = "Nothing in the model or the files is changed. The result is written to prototypes.json.",
                CommonButtons = TaskDialogCommonButtons.Cancel,
            };
            if (active != null)
            {
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "The open document", active.Title);
            }
            choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Choose .rfa or .rvt files");
            TaskDialogResult answer = choice.Show();
            if (answer == TaskDialogResult.Cancel)
            {
                return Result.Cancelled;
            }

            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            IList<string> keyNames = KeyParameters.Load(folder);
            ReaderOptions options;
            try
            {
                options = ReaderOptions.Load(folder);
            }
            catch (ArgumentException failure)
            {
                TaskDialog.Show("Read Prototypes", ReaderOptions.FileName + ": " + failure.Message);
                return Result.Failed;
            }
            var file = new PrototypesFile
            {
                Tool = "RevitPrototypeReader",
                RevitVersion = app.VersionNumber,
                LengthUnit = "m",
                AngleUnit = "deg",
                Documents = new List<DocumentEntry>(),
            };

            if (answer == TaskDialogResult.CommandLink1 && active != null)
            {
                file.Documents.Add(RevitExtractor.Read(active, active.Title, keyNames, options));
            }
            else
            {
                string[] paths = PickFiles();
                if (paths.Length == 0)
                {
                    return Result.Cancelled;
                }
                foreach (string path in paths)
                {
                    file.Documents.Add(ReadFile(app, path, keyNames, options));
                }
            }

            string outPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SiteAdaptation", "prototypes.json");
            JsonOut.Write(outPath, file);

            int rows = 0;
            int links = 0;
            int linkedFiles = 0;
            int walls = 0;
            int floors = 0;
            int ramps = 0;
            int spots = 0;
            int errors = 0;
            var tally = new FloorTally();
            foreach (DocumentEntry document in file.Documents)
            {
                rows += document.Prototypes == null ? 0 : document.Prototypes.Count;
                links += document.Links == null ? 0 : document.Links.Count;
                linkedFiles += document.LinkedFiles == null ? 0 : document.LinkedFiles.Count;
                walls += document.Walls == null ? 0 : document.Walls.Count;
                floors += document.Floors == null ? 0 : document.Floors.Count;
                ramps += document.Ramps == null ? 0 : document.Ramps.Count;
                spots += document.SpotElevations == null ? 0 : document.SpotElevations.Count;
                if (document.Floors != null)
                {
                    FloorTally part = FloorMath.Tally(document.Floors);
                    tally.Floors += part.Floors;
                    tally.ShapeEditing += part.ShapeEditing;
                    tally.Slope += part.Slope;
                    tally.Neither += part.Neither;
                }
                errors += document.Errors == null ? 0 : document.Errors.Count;
            }
            string summary = file.Documents.Count + " document(s), " + rows + " prototype row(s), " + links + " link(s), "
                + linkedFiles + " linked file(s) read, " + walls + " wall(s), " + floors + " floor(s), " + ramps + " ramp(s), " + spots + " spot elevation(s).";
            summary += "\nFloors: " + tally.ShapeEditing + " use shape editing, " + tally.Slope + " report a slope, " + tally.Neither + " use neither.";
            double megabytes = new FileInfo(outPath).Length / 1048576.0;
            summary += "\nFile size: " + megabytes.ToString("0.0", CultureInfo.InvariantCulture) + " MB, the target is under 20 MB.";
            if (errors > 0)
            {
                summary += "\n" + errors + " problem(s) are listed under errors in the file.";
            }
            TaskDialog.Show("Read Prototypes", summary + "\n" + outPath);
            return Result.Succeeded;
        }

        private static string[] PickFiles()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Prototype families or projects";
                dialog.Filter = "Revit files (*.rfa;*.rvt)|*.rfa;*.rvt";
                dialog.Multiselect = true;
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileNames : new string[0];
            }
        }

        // A file that cannot be opened is recorded in the output with its error, and the rest still run
        private static DocumentEntry ReadFile(RevitApp app, string path, IList<string> keyNames, ReaderOptions readerOptions)
        {
            Document doc = null;
            try
            {
                var options = new OpenOptions { Audit = false };
                if (BasicFileInfo.Extract(path).IsWorkshared)
                {
                    options.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;
                    options.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets));
                }
                doc = app.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(path), options);
                return RevitExtractor.Read(doc, Path.GetFileName(path), keyNames, readerOptions);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException failure)
            {
                return new DocumentEntry { File = Path.GetFileName(path), Error = failure.GetType().Name + ": " + failure.Message };
            }
            finally
            {
                if (doc != null)
                {
                    doc.Close(false);
                }
            }
        }
    }

    public class AlwaysAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return true;
        }
    }
}
