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
            Tuple<string, string> named = LastPlan(context.WorkFolder, nbh);
            string plan = named.Item1;
            string read = ReviewLoad.LatestReadFolder(context.WorkFolder);
            string blocksPath = read == null ? null : Path.Combine(read, CheckRun.ReadFiles["villaBlocksRead"]);
            JObject blocks = blocksPath != null && File.Exists(blocksPath) ? JObject.Parse(WorkFiles.ReadText(blocksPath)) : null;
            string lastPath = Path.Combine(context.WorkFolder, "place_" + nbh + ".json");
            JObject last = File.Exists(lastPath) ? JObject.Parse(WorkFiles.ReadText(lastPath)) : null;
            return VillaInputs.Of(plan, named.Item2, configPath, pickedMode, profile, inScope, blocks, last);
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

        // R377. The plan of NBH the last real run of 4 Check wrote and the SHA-256 its record names. The last run is the check folder with
        // the largest run id, a build again after Save overrides ending in -again sorts after its first. A folder with no check.json, a run
        // that did not end done or one of before check.json was written, or one that built no plan of the NBH, such as plan_all.json of a plot list or the whole project, stops 7, never an older plan
        public static Tuple<string, string> LastPlan(string workFolder, string nbh)
        {
            string folder = Directory.Exists(workFolder)
                ? Directory.GetDirectories(workFolder, CheckRun.FolderPrefix + "*").OrderByDescending(item => Path.GetFileName(item), StringComparer.Ordinal).FirstOrDefault()
                : null;
            if (folder == null)
            {
                throw new InvalidDataException("the work folder holds no plan of NBH " + nbh + ", run 4 Check on it first");
            }
            JObject record = CheckRun.RecordOf(folder);
            if (record == null)
            {
                throw new InvalidDataException("the last run of 4 Check, " + Path.GetFileName(folder) + ", holds no " + CheckRun.RecordFile + ", it did not end done or came before "
                    + CheckRun.RecordFile + " was written, so it names no plan. Run 4 Check on NBH " + nbh + " again");
            }
            List<KeyValuePair<string, string>> plans = ((JArray)record["outputs"] ?? new JArray()).OfType<JObject>()
                .Select(output => new KeyValuePair<string, string>((string)output["path"], (string)output["sha256"]))
                .Where(output => output.Key != null && Path.GetFileName(output.Key).StartsWith("plan_", StringComparison.OrdinalIgnoreCase)).ToList();
            string name = CheckRun.PlanName(nbh);
            foreach (KeyValuePair<string, string> output in plans)
            {
                if (string.Equals(Path.GetFileName(output.Key), name, StringComparison.OrdinalIgnoreCase))
                {
                    return Tuple.Create(output.Key, output.Value);
                }
            }
            throw new InvalidDataException("the last run of 4 Check, " + Path.GetFileName(folder) + ", built no plan of NBH " + nbh + ", it built "
                + (plans.Count == 0 ? "none" : string.Join(", ", plans.Select(output => Path.GetFileName(output.Key)))) + ". Run 4 Check on NBH " + nbh);
        }
    }
}
