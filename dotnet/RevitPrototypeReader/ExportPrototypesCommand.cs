using System;
using System.Collections.Generic;
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

            IList<string> keyNames = KeyParameters.Load(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
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
                file.Documents.Add(RevitExtractor.Read(active, active.Title, keyNames));
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
                    file.Documents.Add(ReadFile(app, path, keyNames));
                }
            }

            string outPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SiteAdaptation", "prototypes.json");
            JsonOut.Write(outPath, file);

            int rows = 0;
            foreach (DocumentEntry document in file.Documents)
            {
                rows += document.Prototypes == null ? 0 : document.Prototypes.Count;
            }
            TaskDialog.Show("Read Prototypes", file.Documents.Count + " document(s), " + rows + " prototype row(s).\n" + outPath);
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
        private static DocumentEntry ReadFile(RevitApp app, string path, IList<string> keyNames)
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
                return RevitExtractor.Read(doc, Path.GetFileName(path), keyNames);
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
