using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Scope;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.Floors
{
    // R378, R379. The inputs of buttons 5 and 6 from the work folder: the plan of NBH the last real run of 4 Check wrote, held against the
    // SHA-256 its check.json names as button 7 takes it (Q190), the level profile of that build and, for 6, every input of the build the
    // worker command levelExcel takes, each held against the SHA-256 the build read or wrote it with
    public static class FloorLoad
    {
        public static readonly string[] BuildRoles = { "config", "rules", CheckRun.OverridesRole, "plotsCsv" };
        public static readonly string[] StepRoles = { SiteClusters.StepName, KeyLevels.StepName, Hands.StepName };

        public static FloorInputs Floors(RunContext context, SettingsScope scope)
        {
            Tuple<string, Func<string, bool>> chosen = Scope(context, scope);
            string nbh = chosen.Item1;
            Tuple<string, string> named = VillaLoad.LastPlan(context.WorkFolder, nbh);
            PlanFile plan = PlanSource.Open(named.Item1, named.Item2);
            string lastPath = Path.Combine(context.WorkFolder, FloorsRun.RecordName(nbh));
            JObject last = File.Exists(lastPath) ? JObject.Parse(WorkFiles.ReadText(lastPath)) : null;
            return new FloorInputs { Plan = plan, PlanPath = named.Item1, Profile = Request(named.Item1).LevelProfile, InScope = chosen.Item2, LastPlaced = FloorsRun.Placed(last) };
        }

        public static LevelRunInputs Levels(RunContext context, SettingsScope scope, WorkerDocument document)
        {
            Tuple<string, Func<string, bool>> chosen = Scope(context, scope);
            Tuple<string, string> named = VillaLoad.LastPlan(context.WorkFolder, chosen.Item1);
            string planPath = named.Item1;
            PlanFile plan = PlanSource.Open(planPath, named.Item2);
            WorkerRequest request = Request(planPath);
            JObject record = CheckRun.RecordOf(Path.GetDirectoryName(Path.GetFullPath(planPath)));
            var files = new List<WorkerFile> { new WorkerFile { Role = LevelWorkbookRoles.Plan, Path = planPath, Sha256 = named.Item2 } };
            foreach (string role in BuildRoles)
            {
                WorkerFile input = (request.Inputs ?? new List<WorkerFile>()).FirstOrDefault(item => item.Role == role)
                    ?? throw new InvalidDataException("the run of 4 Check that built " + Path.GetFileName(planPath) + " names no input " + role + ", run 4 Check again (R379)");
                files.Add(Held(role, input.Path, input.Sha256, planPath));
            }
            Dictionary<string, string> outputs = ((record?["outputs"] as JArray) ?? new JArray()).OfType<JObject>().Where(item => item["path"] != null)
                .GroupBy(item => Path.GetFullPath((string)item["path"]), StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => (string)group.First()["sha256"], StringComparer.OrdinalIgnoreCase);
            foreach (string step in StepRoles)
            {
                string path = Path.GetFullPath(Path.Combine(request.StepsFolder ?? "", StepFile.FileName(step, "csharp")));
                if (!outputs.TryGetValue(path, out string sha))
                {
                    throw new InvalidDataException("the check.json of the run of 4 Check that built " + Path.GetFileName(planPath) + " names no " + Path.GetFileName(path) + ", run 4 Check again (R379)");
                }
                files.Add(Held(step, path, sha, planPath));
            }
            string configPath = files.First(file => file.Role == "config").Path;
            return new LevelRunInputs
            {
                Plan = plan, PlanPath = planPath, NoFfl = NoFfl(JObject.Parse(WorkFiles.ReadText(planPath))), Profile = request.LevelProfile, InScope = chosen.Item2,
                ExcelInputs = files, TimeLimitS = AddinConfig.Load(configPath).WorkerTimeLimitS(LevelsRun.Command), Document = document,
            };
        }

        // Plot number to why the plan gives it no FFL, from the missing list of the plan, each key with its why
        public static Dictionary<string, string> NoFfl(JObject plan)
        {
            var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JObject item in ((plan?["missing"] as JArray) ?? new JArray()).OfType<JObject>())
            {
                string plot = (string)item["plot"];
                List<string> why = ((item["missing"] as JArray) ?? new JArray()).OfType<JObject>().Select(lack => (string)lack["key"] + ": " + (string)lack["why"]).ToList();
                if (!string.IsNullOrEmpty(plot) && why.Count > 0)
                {
                    reasons[plot.Trim()] = string.Join(", ", why);
                }
            }
            return reasons;
        }

        // The request of the run of 4 Check that built the plan, in its check folder by the run id of its check.json
        private static WorkerRequest Request(string planPath)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(planPath));
            string runId = (string)CheckRun.RecordOf(folder)?["runId"];
            string path = runId == null ? null : WorkerFiles.RequestPath(folder, runId);
            if (path == null || !File.Exists(path))
            {
                throw new InvalidDataException("the run of 4 Check that built " + Path.GetFileName(planPath) + " left no request in " + Path.GetFileName(folder) + ", run 4 Check again (R379)");
            }
            WorkerRequest request = WorkerRequest.Read(path);
            if (string.IsNullOrEmpty(request.LevelProfile))
            {
                throw new InvalidDataException("the run of 4 Check that built " + Path.GetFileName(planPath) + " names no level profile, run 4 Check again (R379)");
            }
            return request;
        }

        private static WorkerFile Held(string role, string path, string sha, string planPath)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new InvalidDataException("the " + role + " " + Path.GetFileName(path ?? "") + " the run of 4 Check built " + Path.GetFileName(planPath) + " on is not on disk, run 4 Check again (R379)");
            }
            string now = FileHash.Sha256(path);
            if (!string.Equals(now, sha, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("the " + role + " " + Path.GetFileName(path) + " changed since the run of 4 Check that built " + Path.GetFileName(planPath) + ", its SHA-256 differs, run 4 Check again (R379)");
            }
            return new WorkerFile { Role = role, Path = path, Sha256 = now };
        }

        // R366. An NBH takes every plot of its plan, a plot list the plots it names in the one NBH they stand in, a whole project has no
        // one NBH and stops, as 7 Place villas takes its scope
        public static Tuple<string, Func<string, bool>> Scope(RunContext context, SettingsScope scope)
        {
            scope = scope ?? new SettingsScope();
            if (scope.Kind == ScopeKinds.Nbh && !string.IsNullOrWhiteSpace(scope.Nbh))
            {
                return Tuple.Create(scope.Nbh.Trim(), (Func<string, bool>)null);
            }
            if (scope.Kind != ScopeKinds.Plots)
            {
                throw new InvalidDataException(context.Label + " works on one NBH, the scope names none. Choose an NBH in Scope of the pane");
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
            var plots = new HashSet<string>(result.Plots, StringComparer.Ordinal);
            List<string> nbhs = table.Where(plot => plots.Contains(ButtonScope.WholeNumber(plot.Number))).Select(plot => (plot.Nbh ?? "").Trim()).Distinct(StringComparer.Ordinal)
                .OrderBy(nbh => nbh, StringComparer.Ordinal).ToList();
            if (nbhs.Count != 1 || nbhs[0].Length == 0)
            {
                throw new InvalidDataException(context.Label + " works on one NBH, the plot list stands in " + (nbhs.Count == 0 ? "none" : string.Join(", ", nbhs)) + ". Name the plots of one NBH");
            }
            return Tuple.Create(nbhs[0], (Func<string, bool>)plots.Contains);
        }
    }

    // R364. The roles of LevelWorkbook of CadReader, which the add-in loads no type of
    public static class LevelWorkbookRoles
    {
        public const string Plan = "plan";
    }
}
