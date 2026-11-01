using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.UI;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.Revit;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;

namespace SiteAdaptation.AddIn.PlaceVillas
{
    // R377, R404. The body of button 7 on the active document, PlaceVillasRun of Common with the Revit side, the shape reader of light
    // mode and the TaskDialog of the link types to load. Load gives the inputs of one run, VillaInputs.Of on the plan of the last real
    // run of 4 Check, and is set by the main session once the record of button 4 is built. While it is null button 7 is not built
    public static class PlaceVillasBody
    {
        // Size and write time with the path, so a file that changed is hashed again and one that did not is hashed once a session
        private static readonly ConcurrentDictionary<string, string> Hashed = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static Func<RunContext, VillaInputs> Load { get; set; }

        public static bool IsSet => Load != null;

        public static ButtonBody For(UIApplication application)
        {
            var run = new PlaceVillasRun(new RevitVillaDocument(application.ActiveUIDocument.Document), Load,
                context => new ShapeCache(context.WorkFolder, new RevitBaseShapeReader(application, context.WorkFolder), Hash), Hash, LinkLoadDialog.Ask,
                context => new MassFileSet(new RevitMassFileMaker(application, context.WorkFolder), Hash, Root));
            return run.Body();
        }

        // R450. The work root the {workRoot} mark of massFolder stands for, as the pane takes it
        private static string Root()
        {
            return WorkFolder.Root(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), WorkFolder.ReadIfThere);
        }

        private static string Hash(string path)
        {
            var file = new FileInfo(path);
            return Hashed.GetOrAdd(path + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks, key => FileHash.Sha256(path));
        }
    }

    // 03b 7, Link types to load. Names each type with load or reload and says Undo is cleared, with Load and Cancel. The mock does not
    // show it (7.7), so its texts are not in the label list yet
    internal static class LinkLoadDialog
    {
        public static bool Ask(IReadOnlyList<string> lines)
        {
            var dialog = new TaskDialog("7 Place villas")
            {
                MainInstruction = "Link types to load before Place villas",
                MainContent = string.Join("\n", lines),
                CommonButtons = TaskDialogCommonButtons.Cancel,
            };
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Load");
            return dialog.Show() == TaskDialogResult.CommandLink1;
        }
    }
}
