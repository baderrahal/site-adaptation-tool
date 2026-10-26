using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R354 to R357. The build steps of the worker command build, the C# of plan_from_book.py --steps on the reads of button 2: the inputs
    // the worker read wrote, read_baseplan.json and read_villaBlocks.json, each held to what it claims to be by its role, its drawing in
    // Meters and its run stamp, plots.csv and labels.csv of the data book, the rules file with keyRules of the level profile of the request
    // and the analysis of the clusters (Q089). Every number of a JSON input is the nearest double of its text, as Python reads it
    public static class BuildSteps
    {
        public static readonly string[] Names = { SiteClusters.StepName };

        public static readonly string[] ClusterRoles = { "rules", "plotsCsv", "labels", "baseplanRead", "villaBlocksRead", "analysis" };

        // A read of the worker of the role it names, its drawing in Meters and every input of its run stamp on disk with its SHA-256, each
        // input of the stamps of one run hashed once
        private static JObject Read(WorkerContext run, string role, string readRole, Dictionary<string, string> hashes)
        {
            string path = run.Input(role);
            JObject found = PyMath.ReadJson(path, role) as JObject ?? throw run.Refusal("the input " + role + " is no JSON object: " + path);
            JToken named = found["role"];
            if (named == null || named.Type != JTokenType.String || (string)named != readRole)
            {
                throw run.Refusal("the input " + role + " is the read of " + SiteClusters.PyText(named) + ", the build takes the read of " + readRole + " (R354)");
            }
            string refusal = CadUnits.Refusal(path, found) ?? StampRefusal(found, hashes);
            if (refusal != null)
            {
                throw run.Refusal("the input " + role + ": " + refusal);
            }
            return found;
        }

        // RunStamp.Refusal with each input of the stamp hashed once per run
        private static string StampRefusal(JObject output, Dictionary<string, string> hashes)
        {
            JArray inputs = (output[RunStamp.Key] as JObject)?["inputs"] as JArray;
            if (inputs == null)
            {
                return "the output holds no run stamp with its inputs (R347)";
            }
            if (inputs.Count == 0)
            {
                return "the run stamp of the output names no input, so a change of its inputs could not be seen (R347)";
            }
            foreach (JToken input in inputs)
            {
                string role = (string)input["role"], path = (string)input["path"], sha = (string)input["sha256"];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return "the input " + role + " of the output is not on disk: " + path;
                }
                string full = Path.GetFullPath(path);
                if (!hashes.TryGetValue(full, out string found))
                {
                    found = hashes[full] = FileHash.Sha256(full);
                }
                if (!string.Equals(found, sha, StringComparison.OrdinalIgnoreCase))
                {
                    return "the input " + role + " of the output changed since the output was written, its SHA-256 is not the one of the stamp: " + path;
                }
            }
            return null;
        }

        // The build steps the request names, on the inputs read once
        public static void Run(WorkerContext run, AddinConfig config, IEnumerable<string> steps)
        {
            WorkerRequest request = run.Request;
            List<string> asked = steps.ToList();
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            JObject settings = PyMath.ReadJson(run.Input("config"), "config") as JObject ?? throw run.Refusal("the config is no JSON object");
            JObject rules = PyMath.ReadJson(run.Input("rules"), "rules") as JObject ?? throw run.Refusal("the rules file is no JSON object");
            List<SiteCluster> clusters;
            List<string> numbers;
            try
            {
                BuildRules.KeyRules(rules, request.LevelProfile, string.Join(", ", Names));
                List<Dictionary<string, string>> csv = CsvRows.Read(run.Input("plotsCsv"));
                var rows = new Dictionary<string, Dictionary<string, string>>();
                foreach (Dictionary<string, string> row in csv)
                {
                    string number;
                    if (row.TryGetValue("locationPlanNumber", out number) && !string.IsNullOrEmpty(number))
                    {
                        rows[number] = row;
                    }
                }
                Dictionary<string, string> numberOf = FaceStep.NumberOfLabel(csv);
                numbers = PlotScope.Numbers(request.Scope, CsvRows.Read(run.Input("labels")));
                JObject baseplan = Read(run, "baseplanRead", "baseplan", hashes);
                double overlap = config.ParityTolerance("areas") ?? throw new InvalidDataException("addin.parity.kinds.areas is null, the tolerance of the baseplan faces (R345)");
                Dictionary<string, BuildFace> faces = BuildFace.Faces(baseplan["plots"] as JArray ?? throw new InvalidDataException("the baseplan read holds no plots list"), numberOf, overlap);
                JObject footprint = Read(run, "villaBlocksRead", "villaBlocks", hashes);
                // R300. The villa blocks of a drawing kept for reference only are never read
                string source = (string)footprint["source"] ?? "";
                if (ReadSettings.Load(run.Input("config")).IsReferenceOnly(source))
                {
                    throw new InvalidDataException(source + " is kept for reference only (drawings.referenceOnly) and is never read for villaBlocks (R300)");
                }
                Dictionary<string, List<double[][]>> blocks = SiteClusters.BlocksByName(footprint["blocks"] as JArray ?? throw new InvalidDataException("the footprint read holds no blocks list"));
                string analysisPath = run.Input("analysis");
                List<SiteCluster> analysis = SiteClusters.AnalysisOf(PyMath.ReadJson(analysisPath, "analysis") as JObject ?? new JObject(), Path.GetFileName(analysisPath),
                    (string)settings["drawings"]?["baseplan"]);
                List<string> units = rows.Where(item => item.Value.TryGetValue("typologyRev9", out string unit) && !string.IsNullOrEmpty(unit) && faces.ContainsKey(item.Key)).Select(item => item.Key).ToList();
                clusters = SiteClusters.Site(units, rows, faces, blocks, analysis, numberOf);
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            run.Counts["clusters of the build"] = clusters.Count;
            run.Counts["clusters of the build from the analysis"] = clusters.Count(cluster => cluster.Source == SiteClusters.AnalysisSource);
            if (asked.Contains(SiteClusters.StepName))
            {
                Write(run, config, SiteClusters.StepName, SiteClusters.Step(numbers, clusters));
            }
        }

        private static void Write(WorkerContext run, AddinConfig config, string step, Dictionary<string, Dictionary<string, object>> made)
        {
            WorkerRequest request = run.Request;
            run.Counts[step + " items"] = made.Count;
            run.WriteJson(request.StepsFolder, StepFile.FileName(step, "csharp"), StepFile.Build(step, "csharp", StepFile.ScopeText(request.Scope), made, config.StepFields(step)));
        }
    }
}
