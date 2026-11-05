using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.ProjectRules
{
    // R661, R662. The command rulesChange: one person changes or confirms one rule, or changes the Before Build setting, in the rules file of one project.
    // The request has a rule and a new value and no third field, so giving a rule the value it already holds is how a person confirms it
    internal static class RulesChange
    {
        public static void Run(WorkerContext run)
        {
            string who = ProjectRulesFile.Who(run);
            string id = run.Request.RuleId?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                throw run.Refusal("the request names no rule to change");
            }
            if (ProjectRulesFile.IsFixed(id))
            {
                throw run.Refusal("the rule " + id + " is fixed by the tool and cannot be changed (R663)");
            }
            JToken wanted = run.Request.NewValue;
            string noValue = ProjectRulesFile.NoValueReason(wanted);
            if (noValue != null)
            {
                throw run.Refusal("the new value of " + id + " is " + noValue + ", nothing changed. A value is never cleared, a person enters another one");
            }
            string path = run.Input("rules");
            if (ProjectRulesFile.UnderTemplates(path))
            {
                throw run.Refusal("the shipped templates are read only, a project changes its own " + ProjectRulesFile.FileName + ": " + path);
            }
            if (!string.Equals(Path.GetFileName(path), ProjectRulesFile.FileName, StringComparison.OrdinalIgnoreCase))
            {
                throw run.Refusal("rulesChange changes only a " + ProjectRulesFile.FileName + " of a project, a copy of a template under another name stays as it is: " + path);
            }
            string folder = Path.GetDirectoryName(Path.GetFullPath(path));
            string place = ProjectRulesFile.PlaceRefusal(folder, ProjectRulesFile.Config(run));
            if (place != null)
            {
                throw run.Refusal(place);
            }
            using (ChangeLog log = ChangeLog.Open(folder, run))
            {
                // Read with the log held, so a change that ended a moment ago is in what this one builds on
                JObject rules = ProjectRulesFile.ReadOrRefuse(path, "the rules file", run);
                string when = ChangeLog.Now();
                JToken before = id == ProjectRulesFile.BeforeBuild ? ChangeSetting(rules, wanted, run) : ChangeRule(rules, id, wanted, who, when, run);
                log.Change(who, when, id, before, wanted, () => ProjectRulesFile.Write(path, rules, run.Request.RunId));
            }
            run.Counts["changed"] = 1;
        }

        private static JToken ChangeSetting(JObject rules, JToken wanted, WorkerContext run)
        {
            string setting = wanted.Type == JTokenType.String ? (string)wanted : null;
            if (setting != ProjectRulesFile.Warn && setting != ProjectRulesFile.Block)
            {
                throw run.Refusal("Before Build is " + ProjectRulesFile.Warn + " or " + ProjectRulesFile.Block + ", not " + wanted);
            }
            JToken before = rules[ProjectRulesFile.BeforeBuild]?.DeepClone();
            rules[ProjectRulesFile.BeforeBuild] = setting;
            return before;
        }

        private static JToken ChangeRule(JObject rules, string id, JToken wanted, string who, string when, WorkerContext run)
        {
            JObject rule = (rules["rules"] as JArray)?.OfType<JObject>().FirstOrDefault(row => row["id"]?.Type == JTokenType.String && (string)row["id"] == id);
            if (rule == null)
            {
                throw run.Refusal("the rules of this project hold no rule " + id);
            }
            if (rule["fixed"] != null && rule["fixed"].Type == JTokenType.Boolean && (bool)rule["fixed"])
            {
                throw run.Refusal("the rule " + id + " is fixed by the tool and cannot be changed (R663)");
            }
            JToken before = rule["value"]?.DeepClone();
            if (!ProjectRulesFile.SameValue(before, wanted))
            {
                rule["value"] = wanted.DeepClone();
                rule["source"] = ProjectRulesFile.PersonEntered;
            }
            rule["status"] = ProjectRulesFile.Confirmed;
            rule["confirmedBy"] = who;
            rule["confirmedWhen"] = when;
            rule.Remove("marked");
            return before;
        }
    }
}
