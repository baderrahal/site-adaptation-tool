using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common.Update
{
    // R402, R347. The request the add-in writes for the worker command updateFromReport: the config, the rules profile, the plan of the last
    // 4 Check of the NBH the Excel files are of (R472), the override list Apply writes to, and the Excel files as excel1, excel2. The NBH is the part of the file name before its first
    // underscore, as the writers of R364 and R365 name them. The time limit is addin.workerTimeLimitS.updateFromReport of the config
    public static class UpdateWorkerRequest
    {
        public const string Command = "updateFromReport";
        public const string ExcelRolePrefix = "excel";

        public static string NbhOf(string excelPath)
        {
            // The add-in reads Windows paths, and Path of Linux, where the gate runs too, parts a name at a slash only, so both part it here
            string path = excelPath ?? "";
            string name = Path.GetFileNameWithoutExtension(path.Substring(path.LastIndexOfAny(new[] { '\\', '/' }) + 1));
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
                refusal = UpdateText.NoNbh;
                return null;
            }
            if (found.Count > 1)
            {
                refusal = UpdateFromReport.Fill(UpdateText.TwoNbhs, string.Join(" and ", found));
                return null;
            }
            return found[0];
        }

        public static WorkerRequest Make(string runId, WorkerDocument document, string configPath, string rulesPath, string planPath, string overridesPath, string profile, string nbh, IReadOnlyList<string> excelPaths, string outputFolder)
        {
            var inputs = new List<WorkerFile>
            {
                File("config", configPath),
                File("rules", rulesPath),
                File("plan", planPath),
                File("overrides", overridesPath),
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
