using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.ProjectRules
{
    // R664. One rule as a build takes it. Value is null when Source is missing, and Marked then says why in plain words
    public sealed class RuleForBuild
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public JToken Value { get; set; }
        public string Source { get; set; }
        public string Rule { get; set; }
        public string Status { get; set; }
        public string Marked { get; set; }
    }

    // R664. RulesChoiceLine asks once, when no rules are chosen. BlockingLine stops the build. Issues go to the issues list and the build goes on
    public sealed class RulesForBuildAnswer
    {
        public string RulesChoiceLine { get; set; }
        public string BlockingLine { get; set; }
        public List<string> Issues { get; } = new List<string>();
        public List<RuleForBuild> Rules { get; } = new List<RuleForBuild>();
    }

    // R664. What a build gets from the rules of its project. A rules file can be edited outside the tool, so nothing in it is trusted on its word: a value whose source
    // does not stand becomes missing, a confirmed row the log does not hold counts as not confirmed, and the fixed rules hold whatever it says
    public static class RulesForBuild
    {
        private const string NoChoiceMark = "No rules are chosen for this project, so this rule has no value.";
        private static readonly string[] ValueSources = { ProjectRulesFile.Computed, ProjectRulesFile.FromRule, ProjectRulesFile.PersonEntered, ProjectRulesFile.Default };

        // projectMode is the mode of the request, as the mode in config is the default of every run and says past job mode in the shipped config. Null takes no past row (R660)
        public static RulesForBuildAnswer Check(string rulesPath, JObject config, string projectMode)
        {
            var answer = new RulesForBuildAnswer();
            answer.Issues.Add(ProjectRulesFile.SaudiLine);
            JObject block = config?["projectRules"] as JObject;
            JArray listed = block?["ruleIds"] as JArray;
            string manual = block?["manualName"]?.Type == JTokenType.String ? (string)block["manualName"] : null;
            // A saved project keeps an older copy of the config, that is no fault in the tool to throw on
            if (listed == null || listed.Count == 0 || listed.Any(id => id.Type != JTokenType.String) || string.IsNullOrEmpty(manual))
            {
                answer.BlockingLine = "The config of this project names no rules, its copy may be older than this tool, so Build stops. Take a new copy of the config for this project.";
                return answer;
            }
            List<string> ids = listed.Select(id => (string)id).ToList();
            if (string.IsNullOrEmpty(rulesPath))
            {
                answer.RulesChoiceLine = "No rules are chosen for this project. Use the rules template of " + manual + "? Its rules name the unit types of that one manual, so check they fit this project. They stay not confirmed until a person confirms them.";
                AddAllMissing(answer, ids, NoChoiceMark);
                return answer;
            }
            string fault;
            JObject file = ReadRules(rulesPath, out fault);
            if (file == null)
            {
                answer.BlockingLine = "The rules file of this project cannot be used, so Build stops: " + fault + ". File " + Path.GetFileName(rulesPath) + ".";
                AddAllMissing(answer, ids, "The rules file of this project cannot be used, so this rule has no value.");
                return answer;
            }
            RulesLog log = RulesLog.Beside(rulesPath);
            if (log.Gone)
            {
                answer.Issues.Add("The log of the rules of this project is gone or cannot be read, so every rule counts as not confirmed.");
            }
            TakeRules(answer, file, ids, config, projectMode, log);
            Fixed(answer, file);
            Before(answer, file, log);
            return answer;
        }

        private static void AddAllMissing(RulesForBuildAnswer answer, List<string> ids, string mark)
        {
            foreach (string id in ids)
            {
                answer.Rules.Add(MissingRule(id, id, mark));
            }
        }

        private static RuleForBuild MissingRule(string id, string name, string mark)
        {
            return new RuleForBuild { Id = id, Name = name, Value = null, Source = ProjectRulesFile.Missing, Status = ProjectRulesFile.NotConfirmed, Marked = mark };
        }

        // Null and the reason in plain words for a file not on disk or not a rules file
        private static JObject ReadRules(string path, out string fault)
        {
            fault = null;
            if (!File.Exists(path))
            {
                fault = "the file is not on disk";
                return null;
            }
            JObject file;
            try
            {
                file = ProjectRulesFile.Read(path);
            }
            catch (Exception failure) when (failure is JsonException || failure is IOException || failure is UnauthorizedAccessException)
            {
                fault = "the file cannot be read as a rules file";
                return null;
            }
            JArray rows = file["rules"] as JArray;
            if (rows == null || rows.Count == 0)
            {
                fault = "the file holds no rules";
                return null;
            }
            var seen = new HashSet<string>();
            foreach (JToken row in rows)
            {
                JToken id = (row as JObject)?["id"];
                if (id == null || id.Type != JTokenType.String || !seen.Add((string)id))
                {
                    fault = "a rule in the file has no name to go by or is listed twice";
                    return null;
                }
            }
            return file;
        }

        private static void TakeRules(RulesForBuildAnswer answer, JObject file, List<string> ids, JObject config, string projectMode, RulesLog log)
        {
            bool pastAllowed = projectMode != null && projectMode == ProjectRulesFile.PastJobMode(config);
            var held = new HashSet<string>();
            foreach (JObject row in ((JArray)file["rules"]).OfType<JObject>())
            {
                string id = (string)row["id"];
                held.Add(id);
                if (!ProjectRulesFile.IsFixed(id))
                {
                    answer.Rules.Add(Take(row, id, pastAllowed, log, answer.Issues));
                }
            }
            // A rule the build needs that the file lost is missing, it does not fall back on a template
            foreach (string id in ids.Where(id => !held.Contains(id)))
            {
                answer.Rules.Add(MissingRule(id, id, "The rules file of this project does not hold this rule."));
            }
        }

        private static RuleForBuild Take(JObject row, string id, bool pastAllowed, RulesLog log, List<string> issues)
        {
            string name = row["name"]?.Type == JTokenType.String ? (string)row["name"] : id;
            string source = row["source"]?.Type == JTokenType.String ? (string)row["source"] : null;
            JToken value = row["value"];
            if (ProjectRulesFile.NoValueReason(value) != null || source == ProjectRulesFile.Missing)
            {
                return MissingRule(id, name, row["marked"]?.Type == JTokenType.String ? (string)row["marked"] : ProjectRulesFile.MissingMark);
            }
            if (source == ProjectRulesFile.Past && !pastAllowed)
            {
                issues.Add("The rule " + id + " holds a past job measure, which only past job mode may use, so it is left empty.");
                return MissingRule(id, name, "A past job measure, which only past job mode may use.");
            }
            if (source != ProjectRulesFile.Past && !ValueSources.Contains(source))
            {
                issues.Add("The rule " + id + " has a source the tool does not know, so its value is not used.");
                return MissingRule(id, name, "The source of this value is not one the tool knows.");
            }
            return new RuleForBuild
            {
                Id = id, Name = name, Value = value.DeepClone(), Source = source, Rule = source == ProjectRulesFile.FromRule ? id : null,
                Status = Stands(row, id, value, log, issues) ? ProjectRulesFile.Confirmed : ProjectRulesFile.NotConfirmed,
            };
        }

        // R662. A row is confirmed only when the log says so. A row with a log line must hold the value of that line whatever its status says, so a value edited by hand is seen.
        // A not confirmed row with no line is what rulesSetup wrote, or a hand edit no line can show, and it counts as not confirmed either way
        private static bool Stands(JObject row, string id, JToken value, RulesLog log, List<string> issues)
        {
            string by = row["confirmedBy"]?.Type == JTokenType.String ? ((string)row["confirmedBy"]).Trim() : null;
            bool claimed = row["status"]?.Type == JTokenType.String && (string)row["status"] == ProjectRulesFile.Confirmed && !string.IsNullOrEmpty(by);
            if (log.Gone)
            {
                return false;
            }
            JObject line = log.LastOf(id);
            if (line == null)
            {
                if (claimed)
                {
                    issues.Add(Outside(id, "says confirmed and no log line holds it"));
                }
                return false;
            }
            if (!ProjectRulesFile.SameValue(line["new"], value))
            {
                issues.Add(Outside(id, "does not hold the value of its last log line"));
                return false;
            }
            if (claimed && (string)line["who"] != by)
            {
                issues.Add(Outside(id, "names another person than its last log line"));
                return false;
            }
            return claimed;
        }

        private static string Outside(string id, string what)
        {
            return "The rule " + id + " " + what + ", so it was changed outside the tool or a run stopped before it wrote, and it counts as not confirmed.";
        }

        // R663. The fixed rules hold whatever the file says, and an edit that tried to change one is said
        private static void Fixed(RulesForBuildAnswer answer, JObject file)
        {
            foreach (JObject rule in ProjectRulesFile.FixedRules())
            {
                JObject held = ((JArray)file["rules"]).OfType<JObject>().FirstOrDefault(row => (string)row["id"] == (string)rule["id"]);
                if (held == null || !JToken.DeepEquals(held["value"], rule["value"]) || !JToken.DeepEquals(held["fixed"], rule["fixed"]))
                {
                    answer.Issues.Add("The rules file changed or lost the fixed rule " + rule["name"] + ". The tool goes by its own fixed rules and not by the file.");
                }
            }
        }

        // R662. Under warn the build goes on and says so, under block it stops. A missing value is never confirmed, so a rule with no value counts as not confirmed.
        // The setting is the last one logged, so an edit of the file cannot pass a block by writing warn
        private static void Before(RulesForBuildAnswer answer, JObject file, RulesLog log)
        {
            JToken setting = file[ProjectRulesFile.BeforeBuild];
            bool absent = setting == null || setting.Type == JTokenType.Null;
            string before = absent ? ProjectRulesFile.Warn : setting.Type == JTokenType.String ? (string)setting : null;
            int open = answer.Rules.Count(rule => rule.Status != ProjectRulesFile.Confirmed);
            int missing = answer.Rules.Count(rule => rule.Source == ProjectRulesFile.Missing);
            if (missing > 0)
            {
                answer.Issues.Add(missing + " of " + answer.Rules.Count + " rules have no value, each is left empty and marked.");
            }
            if (before != ProjectRulesFile.Warn && before != ProjectRulesFile.Block)
            {
                answer.BlockingLine = "The Before Build setting of this project is neither warn nor block, so Build stops. Choose one in the rules table.";
                return;
            }
            string logged = log.Setting() ?? ProjectRulesFile.Warn;
            if (!log.Gone && before != logged)
            {
                answer.BlockingLine = "The rules file says Before Build is " + before + " and the log says " + logged + ", so the file was changed outside the tool or a run stopped before it wrote, and Build stops. Choose the setting again in the rules table.";
                return;
            }
            if (open == 0)
            {
                return;
            }
            if (before == ProjectRulesFile.Block)
            {
                answer.BlockingLine = "Before Build is set to block and " + open + " of " + answer.Rules.Count + " rules are not confirmed, so Build stops. Confirm them in the rules table.";
                return;
            }
            answer.Issues.Add(open + " of " + answer.Rules.Count + " rules are not confirmed, Build goes on with them as they stand.");
        }
    }
}
