using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.ProjectRules;

namespace SiteAdaptation.AddIn.RulesWindow
{
    // R682. Where the rules table of a project reads and writes. The rules sit beside the rules profile of the project, where siteBuild
    // looks for them (SiteBuildRun.AskRules). Only a new project's copy under the work root takes rules, never the shipped set or a past job
    public sealed class ProjectRulesPlace
    {
        public string ConfigFile { get; set; }
        public string RulesProfile { get; set; }
        public string WorkRoot { get; set; }
        public string WorkFolder { get; set; }
        public string ProjectMode { get; set; }
        public string TemplateFolder { get; set; }
        public string Who { get; set; }
        public WorkerDocument Document { get; set; }

        public string Folder { get { return string.IsNullOrEmpty(RulesProfile) ? null : Path.GetDirectoryName(Path.GetFullPath(RulesProfile)); } }

        public string RulesFile { get { return Folder == null ? null : Path.Combine(Folder, ProjectRulesFile.FileName); } }
    }

    // R682, R683. The rules table of a project over RulesForBuild.Check of R664, and its changes by the commands rulesSetup and rulesChange of
    // R660 and R661. CHOSEN: the two commands run in the process of the add-in by WorkerRunner.Run, the code the worker runs, as they change one
    // small file and hold no Revit type, so the window has its answer at once and needs no ExternalEvent while it is open
    public static class ProjectRulesTable
    {
        public const string ManualTemplate = "manual.json";
        public const double TimeLimitS = 60;

        // The input roles of rulesSetup and rulesChange, RulesSetup.cs and RulesChange.cs of Common
        private const string TemplateRole = "template";
        private const string ProjectFolderRole = "projectFolder";
        private const string ConfigRole = "config";
        private const string RulesRole = "rules";

        // The templates the install ships beside the add-in, R684
        public static string InstalledTemplates()
        {
            return Path.Combine(Path.GetDirectoryName(typeof(ProjectRulesTable).Assembly.Location) ?? "", Common.Settings.ShippedSet.FolderName, "rules", "templates");
        }

        // Null when the rules of this project can be set and changed, else the plain reason
        public static string Refusal(ProjectRulesPlace place)
        {
            string projects = string.IsNullOrEmpty(place.WorkRoot) ? null : Path.Combine(place.WorkRoot, Common.Settings.ShippedSet.ProjectsFolder);
            if (place.Folder == null || string.IsNullOrEmpty(place.ConfigFile) || projects == null || !Pane.WorkFolder.Inside(place.Folder, projects))
            {
                return ProjectRulesText.OwnCopyOnly;
            }
            return string.IsNullOrEmpty(place.WorkFolder) ? ProjectRulesText.NotSaved : null;
        }

        public static bool HasRules(ProjectRulesPlace place)
        {
            return place.RulesFile != null && File.Exists(place.RulesFile);
        }

        // What a build would take, the rows of the table. Null with no config to read
        public static RulesForBuildAnswer Read(ProjectRulesPlace place)
        {
            JObject config;
            try
            {
                config = JObject.Parse(WorkFiles.ReadText(place.ConfigFile));
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is ArgumentException)
            {
                return null;
            }
            return RulesForBuild.Check(HasRules(place) ? place.RulesFile : null, config, place.ProjectMode);
        }

        // warn unless the file says block. RulesForBuild.Check holds the logged setting, this is what the table shows as chosen
        public static string BeforeBuild(ProjectRulesPlace place)
        {
            try
            {
                return HasRules(place) && (string)JObject.Parse(WorkFiles.ReadText(place.RulesFile))[ProjectRulesFile.BeforeBuild] == ProjectRulesFile.Block ? ProjectRulesFile.Block : ProjectRulesFile.Warn;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is InvalidCastException)
            {
                return ProjectRulesFile.Warn;
            }
        }

        // R683. The one amber line of the pane, null when every rule a build takes is confirmed
        public static string AmberLine(RulesForBuildAnswer answer, bool hasRules)
        {
            if (answer == null)
            {
                return null;
            }
            if (!hasRules)
            {
                return ProjectRulesText.AmberNone;
            }
            List<RuleForBuild> settable = answer.Rules.Where(rule => !ProjectRulesFile.IsFixed(rule.Id)).ToList();
            int open = settable.Count(rule => rule.Status != ProjectRulesFile.Confirmed);
            return open == 0 ? null : TextTemplate.Fill(ProjectRulesText.AmberSome, open, settable.Count);
        }

        // A value as the table shows it: a text as it is, any other value as JSON on one line, nothing for none
        public static string ValueText(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                return string.Empty;
            }
            return value.Type == JTokenType.String ? (string)value : value.ToString(Formatting.None);
        }

        // What a person typed, as the value it replaces. CHOSEN: a number stays a number when the text reads as one, a list or a set must read as
        // JSON, anything else is text. A rule with no value takes a number when the text reads as one, else text. Null and the reason when it does not read
        public static JToken Parse(string text, JToken old, out string why)
        {
            why = null;
            string typed = (text ?? string.Empty).Trim();
            JTokenType kind = old?.Type ?? JTokenType.Null;
            if (kind == JTokenType.Array || kind == JTokenType.Object)
            {
                try
                {
                    JToken read = JToken.Parse(typed);
                    if (read.Type == kind)
                    {
                        return read;
                    }
                    why = typed;
                    return null;
                }
                catch (JsonException failure)
                {
                    why = failure.Message;
                    return null;
                }
            }
            if ((kind == JTokenType.Integer || kind == JTokenType.Null) && long.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole))
            {
                return new JValue(whole);
            }
            if ((kind == JTokenType.Integer || kind == JTokenType.Float || kind == JTokenType.Null) && double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && !double.IsNaN(number) && !double.IsInfinity(number))
            {
                return new JValue(number);
            }
            if (kind == JTokenType.Boolean && bool.TryParse(typed, out bool flag))
            {
                return new JValue(flag);
            }
            return new JValue(typed);
        }

        // R660. The project takes the manual template. Null when done, else the plain reason
        public static string UseManual(ProjectRulesPlace place)
        {
            string refused = Refusal(place);
            if (refused != null)
            {
                return refused;
            }
            string template = Path.Combine(place.TemplateFolder ?? "", ManualTemplate);
            if (!File.Exists(template))
            {
                return ProjectRulesText.NoTemplate;
            }
            return Run(place, "rulesSetup", new List<WorkerFile> { File(TemplateRole, template), File(ProjectFolderRole, place.Folder), File(ConfigRole, place.ConfigFile) }, null, null);
        }

        // R661, R662. One rule changed or confirmed, or the Before Build setting by its id. Null when done, else the plain reason
        public static string Change(ProjectRulesPlace place, string ruleId, JToken value)
        {
            string refused = Refusal(place);
            if (refused != null)
            {
                return refused;
            }
            return Run(place, "rulesChange", new List<WorkerFile> { File(RulesRole, place.RulesFile), File(ConfigRole, place.ConfigFile) }, ruleId, value);
        }

        private static WorkerFile File(string role, string path)
        {
            // The empty SHA-256 is what the tests of R660 and R661 send, the runner measures the file itself
            return new WorkerFile { Role = role, Path = path, Sha256 = string.Empty };
        }

        private static string Run(ProjectRulesPlace place, string command, List<WorkerFile> inputs, string ruleId, JToken value)
        {
            string runId = "rules-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            try
            {
                Directory.CreateDirectory(place.WorkFolder);
                var request = new WorkerRequest
                {
                    FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = runId, Document = place.Document, Command = command, Inputs = inputs,
                    Scope = new WorkerScope { Kind = "project" }, OutputFolder = place.WorkFolder, TimeLimitS = TimeLimitS,
                    Who = place.Who, RuleId = ruleId, NewValue = value, ProjectMode = place.ProjectMode,
                };
                var commands = new Dictionary<string, WorkerCommand>();
                ProjectRulesCommands.Add(commands);
                // The memory of Revit is no measure of this run, so none is sampled
                int code = WorkerRunner.Run(request.Write(place.WorkFolder), commands, () => null, () => null);
                if (code == WorkerExit.Done)
                {
                    return null;
                }
                string answer = WorkerFiles.AnswerPath(place.WorkFolder, runId);
                string message = System.IO.File.Exists(answer) ? WorkerAnswer.Read(answer)?.Message : null;
                return TextTemplate.Fill(ProjectRulesText.Refused, message ?? WorkerExit.Meaning(code));
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException)
            {
                return TextTemplate.Fill(ProjectRulesText.Refused, failure.Message);
            }
        }
    }
}
