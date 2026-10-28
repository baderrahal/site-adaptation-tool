using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Pane;

namespace SiteAdaptation.AddIn
{
    // R371, docs/design/08-install.md. The one entry of a button, the ribbon command, the pane and later the test host call it, so
    // no run logic is copied. The transaction wrapper of R382 comes behind it in a later PR of 5a
    public static class Buttons
    {
        public static string NotBuiltText(int number)
        {
            return RibbonLayout.Button(number).Label + " is not built yet.";
        }

        public static Result Run(int number, UIApplication application)
        {
            string stop = Stop(application);
            TaskDialog.Show(RibbonLayout.Tab, stop ?? NotBuiltText(number));
            return Result.Succeeded;
        }

        // R372. A document with no path has no work folder, and a work folder inside inputRoot is refused
        private static string Stop(UIApplication application)
        {
            Document document = application.ActiveUIDocument == null ? null : application.ActiveUIDocument.Document;
            if (document == null)
            {
                return null;
            }
            DocumentSession session = PaneHost.Current == null ? null : PaneHost.Current.Sessions.Find(document);
            try
            {
                string root = WorkFolder.Root(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    WorkFolder.ReadIfThere);
                string inputRoot = session == null ? null : WorkFolder.InputRoot(session.Settings.ConfigFile, WorkFolder.ReadIfThere);
                return WorkFolder.Stop(root, document.PathName, inputRoot);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException
                || error is Newtonsoft.Json.JsonException || error is ArgumentException || error is NotSupportedException)
            {
                return error.Message;
            }
        }
    }
}
