using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.Revit
{
    // R381, Q124. The counts button 9 gives the worker as the input revitCounts: the tags of R368 read by PlacedTagStore, counted by RevitCounts of
    // Common. It opens no transaction and writes nothing to the model. levelPlots are the plots whose levels the last run of button 6 that
    // committed set and read back, which that run gives once 5d builds it, so none until then
    internal static class RevitCountsReader
    {
        public static void Write(Document document, string path, ICollection<string> levelPlots, string levelRun)
        {
            var tags = new PlacedTagStore(document).Collect().Select(pair => pair.Value);
            File.WriteAllText(path, RevitCounts.FromTags(tags, levelPlots, levelRun).ToString(Formatting.Indented));
        }
    }
}
