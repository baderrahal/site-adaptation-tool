using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

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

    // R664. What a build gets from the rules of its project. A rules file can be edited outside the tool, so nothing in it is trusted on its word: a value whose
    // source does not stand becomes missing, a status of confirmed with no person behind it stays not confirmed, and the fixed rules stay on whatever it says
    public static class RulesForBuild
    {
        private const string ChoiceLine = "No rules are chosen for this project. Use the manual rules template? Its rules stay not confirmed until a person confirms them.";
        private const string NoChoiceMark = "No rules are chosen for this project, so this rule has no value.";
        private static readonly string[] ValueSources = { ProjectRulesFile.Computed, ProjectRulesFile.FromRule, ProjectRulesFile.PersonEntered, ProjectRulesFile.Default };

        public static RulesForBuildAnswer Check(string rulesPath, JObject config)
        {
            var answer = new RulesForBuildAnswer();
            JArray listed = (config?["projectRules"] as JObject)?["ruleIds"] as JArray;
            if (listed == null || listed.Count == 0 || listed.Any(id => id.Type != JTokenType.String))
            {
                throw new InvalidDataException("the config holds no list projectRules.ruleIds of the rules a build needs (R664)");
            }
            List<string> ids = listed.Select(id => (string)id).ToList();
            if (string.IsNullOrEmpty(rulesPath))
            {
                answer.RulesChoiceLine = ChoiceLine;
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
            TakeRules(answer, file, ids, config);
            Fixed(answer, file);
            Before(answer, file);
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

        private static void TakeRules(RulesForBuildAnswer answer, JObject file, List<string> ids, JObject config)
        {
            bool pastAllowed = ProjectRulesFile.PastJobMode(config) != null && ProjectModes.OfConfig(config) == ProjectRulesFile.PastJobMode(config);
            var held = new HashSet<string>();
            foreach (JObject row in ((JArray)file["rules"]).OfType<JObject>())
            {
                string id = (string)row["id"];
                held.Add(id);
                if (!ProjectRulesFile.IsFixed(id))
                {
                    answer.Rules.Add(Take(row, id, pastAllowed, answer.Issues));
                }
            }
            // A rule the build needs that the file lost is missing, it does not fall back on a template
            foreach (string id in ids.Where(id => !held.Contains(id)))
            {
                answer.Rules.Add(MissingRule(id, id, "The rules file of this project does not hold this rule."));
            }
        }

        private static RuleForBuild Take(JObject row, string id, bool pastAllowed, List<string> issues)
        {
            string name = row["name"]?.Type == JTokenType.String ? (string)row["name"] : id;
            string source = row["source"]?.Type == JTokenType.String ? (string)row["source"] : null;
            JToken value = row["value"];
            if (value == null || value.Type == JTokenType.Null || source == ProjectRulesFile.Missing)
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
            bool confirmed = row["status"]?.Type == JTokenType.String && (string)row["status"] == ProjectRulesFile.Confirmed
                && row["confirmedBy"]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)row["confirmedBy"]);
            return new RuleForBuild
            {
                Id = id, Name = name, Value = value.DeepClone(), Source = source, Rule = source == ProjectRulesFile.FromRule ? id : null,
                Status = confirmed ? ProjectRulesFile.Confirmed : ProjectRulesFile.NotConfirmed,
            };
        }

        // R663. The fixed rules hold whatever the file says, and an edit that tried to change one is said
        private static void Fixed(RulesForBuildAnswer answer, JObject file)
        {
            foreach (JObject rule in ProjectRulesFile.FixedRules())
            {
                JObject held = ((JArray)file["rules"]).OfType<JObject>().FirstOrDefault(row => (string)row["id"] == (string)rule["id"]);
                if (held == null || !JToken.DeepEquals(held["value"], rule["value"]) || !JToken.DeepEquals(held["fixed"], rule["fixed"]))
                {
                    answer.Issues.Add("The rules file changed or lost the fixed rule " + rule["name"] + ". The tool keeps it on.");
                }
            }
        }

        // R662. Under warn the build goes on and says so, under block it stops. A missing value is never confirmed, so a rule with no value counts as not confirmed
        private static void Before(RulesForBuildAnswer answer, JObject file)
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
