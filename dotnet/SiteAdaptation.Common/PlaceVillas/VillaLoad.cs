using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Scope;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Settings;

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

        // R366. The scope of the pane held against the plot table of the latest read: an NBH takes every plot of its plan, a plot list takes
        // the plots it names and the one NBH they stand in, a whole project has no one NBH and stops as it did
        public static VillaInputs Load(RunContext context, SettingsScope scope, string configPath, string pickedMode, string profile)
        {
            scope = scope ?? new SettingsScope();
            if (scope.Kind != ScopeKinds.Plots)
            {
                return Load(context, scope.Kind == ScopeKinds.Nbh ? scope.Nbh : null, configPath, pickedMode, profile, null);
            }
            string read = ReviewLoad.LatestReadFolder(context.WorkFolder);
            string tablePath = read == null ? null : Path.Combine(read, ReadDataRun.PlotTableFile);
            if (tablePath == null || !File.Exists(tablePath))
            {
                throw new InvalidDataException("a plot list needs the plot table of 2 Read data, run 2 Read data first");
            }
            List<ScopePlot> table = ReadDataRun.ScopePlots(JObject.Parse(WorkFiles.ReadText(tablePath))).ToList();
            ScopeResult result = ButtonScope.Resolve(context.Button, scope, table);
            if (!result.Ok)
            {
                throw new InvalidDataException("the plot list stops the button: " + string.Join(", ", result.Stops));
            }
            var chosen = new HashSet<string>(result.Plots, StringComparer.Ordinal);
            List<string> nbhs = table.Where(plot => chosen.Contains(ButtonScope.WholeNumber(plot.Number))).Select(plot => (plot.Nbh ?? "").Trim()).Distinct(StringComparer.Ordinal)
                .OrderBy(nbh => nbh, StringComparer.Ordinal).ToList();
            if (nbhs.Count != 1 || nbhs[0].Length == 0)
            {
                throw new InvalidDataException("7 Place villas works on one NBH, the plot list stands in " + (nbhs.Count == 0 ? "none" : string.Join(", ", nbhs)) + ". Name the plots of one NBH");
            }
            return Load(context, nbhs[0], configPath, pickedMode, profile, chosen.Contains);
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
