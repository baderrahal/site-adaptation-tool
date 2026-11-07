using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.SiteBuild;

namespace SiteAdaptation.AddIn.Pane
{
    // R686. What the plan of a build reads: the latest read of stage 1, the config, the rules of the project and the work folder of the model
    public sealed class SiteBuildPlace
    {
        public string ConfigFile { get; set; }
        public string ReadFolder { get; set; }
        public string RulesFile { get; set; }
        public string PositionFile { get; set; }
        public string WorkFolder { get; set; }
        public string ProjectMode { get; set; }
        public WorkerDocument Document { get; set; }
    }

    // R686. The plan of build_plan.json as card 2 says it
    public sealed class SiteBuildResult
    {
        public string Folder { get; set; }
        public string Failure { get; set; }
        public List<string> Blocking { get; } = new List<string>();
        public List<string> Issues { get; } = new List<string>();
        public string Progress { get; set; }
        public string RulesChoice { get; set; }
        public string Coordinates { get; set; }
        public int PlotsWithOutline { get; set; }

        public string Preview { get { return Folder == null ? null : Path.Combine(Folder, PreviewName); } }

        // The first line that stops the build, null when it may go on
        public string StopLine
        {
            get
            {
                if (Failure != null)
                {
                    return TextTemplate.Fill(SiteBuildText.NotRun, Failure);
                }
                return Blocking.Count > 0 ? TextTemplate.Fill(SiteBuildText.Stopped, Blocking[0]) : null;
            }
        }

        public const string PlanName = "build_plan.json";
        public const string PreviewName = "preview.json";
    }

    // R686. The one Build first makes the plan of siteBuild (R640 to R648) over the plot table of stage 1. CHOSEN: the command runs in the process of the
    // add-in by WorkerRunner.Run, the code the worker runs, as it reads JSON only and holds no Revit type, as the rules table does
    public static class SiteBuildPlan
    {
        public const string FolderPrefix = "build_";
        public const double TimeLimitS = 120;

        // The scope of the settings in the words of the project
        public static string ScopeLine(SettingsScope scope)
        {
            if (scope?.Kind == ScopeKinds.Nbh && !string.IsNullOrWhiteSpace(scope.Nbh))
            {
                return TextTemplate.Fill(SiteBuildText.ScopeNbh, scope.Nbh);
            }
            if (scope?.Kind == ScopeKinds.Plots && !string.IsNullOrWhiteSpace(scope.Plots))
            {
                return TextTemplate.Fill(SiteBuildText.ScopePlots, scope.Plots);
            }
            return SiteBuildText.ScopeProject;
        }

        public static SiteBuildResult Make(SiteBuildPlace place)
        {
            var result = new SiteBuildResult();
            string table = place.ReadFolder == null ? null : Path.Combine(place.ReadFolder, DataCheckRows.PlotsFileName);
            string profile = place.ReadFolder == null ? null : Path.Combine(place.ReadFolder, ConfirmProfile.ConfirmProfileStep.ProfileFile);
            if (table == null || !File.Exists(table) || !File.Exists(profile))
            {
                result.Failure = SiteBuildText.NoRead;
                return result;
            }
            // R742, F4 of run 1. The plan runs in the process of Revit, whose folder has no config of the repo above it, so the design values come from the
            // config of the run before any request is written, as a button takes them from the settings of its session
            string refused = DesignValues.UseConfigFile(place.ConfigFile);
            if (refused != null)
            {
                result.Failure = refused;
                return result;
            }
            string runId = "build-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            string output = Path.Combine(place.WorkFolder, FolderPrefix + runId);
            var inputs = new List<WorkerFile> { Input("plotTable", table), Input("projectProfile", profile), Input("config", place.ConfigFile) };
            // The rules of the project live with its copy under the work root, not beside a read folder a new read replaces, so they are named here
            if (!string.IsNullOrEmpty(place.RulesFile) && File.Exists(place.RulesFile))
            {
                inputs.Add(Input("rules", place.RulesFile));
            }
            // R642. The position file is only a check, its difference to the drawing in mm is said, it sets no coordinate
            if (!string.IsNullOrEmpty(place.PositionFile) && File.Exists(place.PositionFile))
            {
                inputs.Add(Input("positionFile", place.PositionFile));
            }
            try
            {
                Directory.CreateDirectory(place.WorkFolder);
                var request = new WorkerRequest
                {
                    FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = runId, Document = place.Document, Command = "siteBuild", Inputs = inputs,
                    Scope = new WorkerScope { Kind = "project" }, OutputFolder = output, TimeLimitS = TimeLimitS, ProjectMode = place.ProjectMode,
                };
                var commands = new Dictionary<string, WorkerCommand>();
                SiteBuildCommands.Add(commands);
                int code = WorkerRunner.Run(request.Write(place.WorkFolder), commands, () => null, () => null);
                if (code != WorkerExit.Done)
                {
                    string answer = WorkerFiles.AnswerPath(place.WorkFolder, runId);
                    result.Failure = (File.Exists(answer) ? WorkerAnswer.Read(answer)?.Message : null) ?? WorkerExit.Meaning(code);
                    return result;
                }
                result.Folder = output;
                Read(JObject.Parse(WorkFiles.ReadText(Path.Combine(output, SiteBuildResult.PlanName))), result);
                result.PlotsWithOutline = (JObject.Parse(WorkFiles.ReadText(result.Preview))["plots"] as JArray)?.Count ?? 0;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is InvalidDataException)
            {
                result.Failure = failure.Message;
            }
            return result;
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = string.Empty };
        }

        // The plan as card 2 says it, R643: the blocking lines, the issues with their plot, the progress line and the coordinates of R642
        public static void Read(JObject plan, SiteBuildResult result)
        {
            result.Blocking.AddRange((plan["blocking"] as JArray ?? new JArray()).OfType<JObject>().Select(line => (string)line["line"]).Where(line => !string.IsNullOrEmpty(line)));
            result.Issues.AddRange((plan["issues"] as JArray ?? new JArray()).OfType<JObject>().Select(issue => issue["plot"]?.Type == JTokenType.String ? (string)issue["plot"] + ": " + (string)issue["line"] : (string)issue["line"]));
            result.Progress = (string)plan["progress"];
            result.RulesChoice = (string)(plan["rulesChoice"] as JObject)?["line"];
            result.Coordinates = CoordinatesLine(plan["coordinates"] as JObject);
        }

        // The position file the person picked in Set up site, else addin.position.file of config beside the config folder as the parameter sheet is. Null with none
        public static string PositionFileOf(string picked, string configFile)
        {
            if (!string.IsNullOrEmpty(picked))
            {
                return picked;
            }
            try
            {
                JToken named = ((JObject.Parse(WorkFiles.ReadText(configFile))["addin"] as JObject)?["position"] as JObject)?["file"];
                if (named == null || named.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)named))
                {
                    return null;
                }
                string path = (string)named;
                return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(configFile))) ?? "", path));
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is ArgumentException)
            {
                return null;
            }
        }

        // R642. The point of the drawing, and the difference to the position file in millimetres when the plan holds one, or why the file did not read
        public static string CoordinatesLine(JObject coordinates)
        {
            string line = PointLine(coordinates);
            string fileError = (string)(coordinates?["positionFile"] as JObject)?["error"];
            return fileError == null ? line : line + " " + TextTemplate.Fill(SiteBuildText.PositionNotRead, fileError);
        }

        private static string PointLine(JObject coordinates)
        {
            double? east = (double?)coordinates?["eastM"];
            double? north = (double?)coordinates?["northM"];
            if (east == null || north == null)
            {
                return TextTemplate.Fill(SiteBuildText.NoCoordinates, (string)coordinates?["why"] ?? (string)coordinates?["source"] ?? string.Empty);
            }
            double? difference = coordinates["differenceMm"]?.Type == JTokenType.Float || coordinates["differenceMm"]?.Type == JTokenType.Integer ? (double?)coordinates["differenceMm"] : null;
            string e = east.Value.ToString("0.###", CultureInfo.InvariantCulture);
            string n = north.Value.ToString("0.###", CultureInfo.InvariantCulture);
            return difference == null ? TextTemplate.Fill(SiteBuildText.Coordinates, e, n)
                : TextTemplate.Fill(SiteBuildText.CoordinatesFile, e, n, difference.Value.ToString("0.#", CultureInfo.InvariantCulture));
        }
    }
}
