using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.Common
{
    public static partial class DataCheckLogic
    {
        // R700. The need sharedParameters comes from the parameter sheet the project settings name, the one source a new project edits, not
        // from a file of the folder. A Revit shared parameter text file in the folder is only said, the tool binds from the sheet
        public static JObject SharedParametersNeed(DataCheckInput input)
        {
            if (input.Sheet == null)
            {
                string why = input.SheetName == null ? "No parameter sheet was given to the data check."
                    : input.SheetFound ? "The parameter sheet " + input.SheetName + " could not be read: " + input.SheetProblem
                    : "The parameter sheet " + input.SheetName + " is not there, so no shared parameter can go on the villa links.";
                // A Revit shared parameter file of the folder holds the names and GUIDs, a person can copy them into the sheet
                return input.SharedParameterFiles > 0
                    ? Need("sharedParameters", "usable", Count(input.SharedParameterFiles, "shared parameter file", "shared parameter files") + " found in the folder. " + why)
                    : Need("sharedParameters", input.SheetFound ? "weak" : "missing", why);
            }
            List<ParameterSpec> links = input.Sheet.Parameters.Where(item => item.BoundOn.Contains(ParameterSheet.LinkCategory)).ToList();
            if (links.Count == 0)
            {
                return Need("sharedParameters", "missing", "The parameter sheet " + input.SheetName + " holds no row for the villa links, category " + ParameterSheet.LinkCategory + ".");
            }
            List<string> noGuid = links.Where(item => item.Guid == null).Select(item => item.Name).ToList();
            string head = Count(links.Count, "parameter", "parameters") + " for the villa links in " + input.SheetName;
            if (noGuid.Count > 0)
            {
                return Need("sharedParameters", "usable", head + ", " + noGuid.Count + " with no GUID: " + string.Join(", ", noGuid) + ".");
            }
            return Need("sharedParameters", "strong", head + ", each with a GUID.");
        }
    }
}
