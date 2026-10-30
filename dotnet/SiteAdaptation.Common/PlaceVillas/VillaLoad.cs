using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Review;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R377. The inputs of one run of button 7 from the work folder: the plan of the latest real run of 4 Check by its SHA-256, the villa
    // blocks of the latest read of button 2 and the record of the last run of 7 for the NBH. A dry run of 4 writes under dry, so no
    // folder of it is taken
    public static class VillaLoad
    {
        public static VillaInputs Load(RunContext context, string nbh, string configPath, string pickedMode, string profile, Func<string, bool> inScope)
        {
            if (string.IsNullOrWhiteSpace(nbh))
            {
                throw new InvalidDataException("7 Place villas works on one NBH, the scope names none. Choose an NBH in Scope of the pane");
            }
            string plan = LatestPlan(context.WorkFolder, nbh);
            if (plan == null)
            {
                throw new InvalidDataException("the work folder holds no plan of NBH " + nbh + ", run 4 Check on it first");
            }
            string read = ReviewLoad.LatestReadFolder(context.WorkFolder);
            string blocksPath = read == null ? null : Path.Combine(read, CheckRun.ReadFiles["villaBlocksRead"]);
            JObject blocks = blocksPath != null && File.Exists(blocksPath) ? JObject.Parse(WorkFiles.ReadText(blocksPath)) : null;
            string lastPath = Path.Combine(context.WorkFolder, "place_" + nbh + ".json");
            JObject last = File.Exists(lastPath) ? JObject.Parse(WorkFiles.ReadText(lastPath)) : null;
            return VillaInputs.Of(plan, FileHash.Sha256(plan), configPath, pickedMode, profile, inScope, blocks, last);
        }

        // The plan file of the check folder with the largest run id that holds one for the NBH, the folder of a build again after Save
        // overrides ending in -again, which sorts after its first
        public static string LatestPlan(string workFolder, string nbh)
        {
            if (!Directory.Exists(workFolder))
            {
                return null;
            }
            return Directory.GetDirectories(workFolder, CheckRun.FolderPrefix + "*").OrderByDescending(folder => Path.GetFileName(folder), StringComparer.Ordinal)
                .Select(folder => Path.Combine(folder, CheckRun.PlanName(nbh))).FirstOrDefault(File.Exists);
        }
    }
}
