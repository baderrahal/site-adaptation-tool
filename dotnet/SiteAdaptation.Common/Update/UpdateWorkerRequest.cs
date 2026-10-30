using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common.Update
{
    // R402, R347. The request the add-in writes for the worker command updateFromReport: the config, the rules profile and the plan_<NBH>.json
    // of the NBH the Excel files are of, and the Excel files as excel1, excel2. The NBH is the part of the file name before its first
    // underscore, as the writers of R364 and R365 name them. The time limit is addin.workerTimeLimitS.updateFromReport of the config
    public static class UpdateWorkerRequest
    {
        public const string Command = "updateFromReport";
        public const string ExcelRolePrefix = "excel";

        public static string NbhOf(string excelPath)
        {
            string name = Path.GetFileNameWithoutExtension(excelPath ?? "");
            int at = name.IndexOf('_');
            return at > 0 ? name.Substring(0, at) : null;
        }

        // The one NBH of the files, or why they have none or two
        public static string Nbh(IReadOnlyList<string> excelPaths, out string refusal)
        {
            refusal = null;
            List<string> found = excelPaths.Select(NbhOf).Distinct().ToList();
            if (found.Count == 0 || found.Contains(null))
            {
                refusal = "the file name of a report Excel starts with its NBH and an underscore, as 012_Levels_<profile>.xlsx does (R402)";
                return null;
            }
            if (found.Count > 1)
            {
                refusal = "the files are of NBH " + string.Join(" and ", found) + ", Update from report takes the files of one NBH (R402)";
                return null;
            }
            return found[0];
        }

        public static string PlanPath(string workFolder, string nbh)
        {
            return Path.Combine(workFolder, "plan_" + nbh + ".json");
        }

        public static WorkerRequest Make(string runId, WorkerDocument document, string configPath, string rulesPath, string planPath, string profile, string nbh, IReadOnlyList<string> excelPaths, string outputFolder)
        {
            var inputs = new List<WorkerFile>
            {
                File("config", configPath),
                File("rules", rulesPath),
                File("plan", planPath),
            };
            for (int at = 0; at < excelPaths.Count; at++)
            {
                inputs.Add(File(ExcelRolePrefix + (at + 1).ToString(CultureInfo.InvariantCulture), excelPaths[at]));
            }
            return new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = runId, Command = Command, Document = document, Inputs = inputs, LevelProfile = profile,
                Scope = new WorkerScope { Kind = "nbh", Nbh = nbh }, OutputFolder = outputFolder, TimeLimitS = AddinConfig.Load(configPath).WorkerTimeLimitS(Command),
            };
        }

        private static WorkerFile File(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }
    }
}
