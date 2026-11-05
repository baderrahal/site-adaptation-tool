using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.ProjectRules
{
    // R660, R662. The command rulesSetup: one project takes its rules from a template the person chose. Every rule starts not confirmed and Before Build starts on warn
    internal static class RulesSetup
    {
        private static readonly string[] TemplateSources = { ProjectRulesFile.FromRule, ProjectRulesFile.Default, ProjectRulesFile.Past };

        public static void Run(WorkerContext run)
        {
            string who = ProjectRulesFile.Who(run);
            JObject config = ProjectRulesFile.Config(run);
            string templatePath = run.Input("template");
            string folder = run.Input("projectFolder");
            string place = ProjectRulesFile.UnderTemplates(folder) ? "the project folder " + folder + " lies under the shipped templates, which are read only" : ProjectRulesFile.PlaceRefusal(folder, config);
            if (place != null)
            {
                throw run.Refusal(place);
            }
            JObject template = ProjectRulesFile.Read(templatePath);
            JArray rules = FromTemplate(template, Path.GetFileName(templatePath), run, config);
            foreach (JObject rule in ProjectRulesFile.FixedRules())
            {
                rules.Add(rule);
            }
            string name = Path.GetFileName(templatePath);
            var file = new JObject { ["formatVersion"] = 1, ["fromTemplate"] = name, [ProjectRulesFile.BeforeBuild] = ProjectRulesFile.Warn, ["rules"] = rules };
            string path = Path.Combine(folder, ProjectRulesFile.FileName);
            using (ChangeLog log = ChangeLog.Open(folder, run))
            {
                // A second setup must not wipe what a person entered or confirmed. Changes go through rulesChange
                if (File.Exists(path))
                {
                    throw run.Refusal("this project already holds its rules, nothing changed: " + path);
                }
                string when = ChangeLog.Now();
                log.Add(who, when, "rulesSetup", null, name);
                try
                {
                    ProjectRulesFile.Write(path, file, run.Request.RunId);
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                {
                    log.Add(who, when, "rulesSetup", null, name, failure.Message);
                    throw;
                }
            }
            List<JObject> settable = rules.OfType<JObject>().Where(rule => rule["fixed"] == null).ToList();
            int missing = settable.Count(rule => (string)rule["source"] == ProjectRulesFile.Missing);
            run.Counts["rules"] = settable.Count;
            run.Counts["notConfirmed"] = settable.Count;
            run.Counts["missing"] = missing;
            if (missing > 0)
            {
                run.Issues.Add(missing + " of " + settable.Count + " rules have no value, each stays empty and marked until a person enters it");
            }
        }

        private static JArray FromTemplate(JObject template, string name, WorkerContext run, JObject config)
        {
            JArray rows = template["rules"] as JArray;
            if (rows == null || rows.Count == 0)
            {
                throw run.Refusal("the template " + name + " holds no rules");
            }
            var seen = new HashSet<string>();
            var rules = new JArray();
            foreach (JToken row in rows)
            {
                JObject rule = row as JObject;
                string id = rule?["id"]?.Type == JTokenType.String ? (string)rule["id"] : null;
                if (string.IsNullOrEmpty(id) || !seen.Add(id) || ProjectRulesFile.IsFixed(id) || id == ProjectRulesFile.BeforeBuild)
                {
                    throw run.Refusal("the template " + name + " holds a rule with no id, a repeated id or an id the tool keeps: " + (id ?? "none"));
                }
                string source = rule["source"]?.Type == JTokenType.String ? (string)rule["source"] : null;
                bool hasValue = rule["value"] != null && rule["value"].Type != JTokenType.Null;
                // A value with no real source would reach the build as if it were known, and a source with no value would hide that it is missing
                if (hasValue ? !TemplateSources.Contains(source) : source != ProjectRulesFile.Missing)
                {
                    throw run.Refusal("the template " + name + " rule " + id + " has a value and a source that do not fit, value " + (hasValue ? "given" : "none") + ", source " + (source ?? "none"));
                }
                // By what the template holds and not by its name, so a copy of the past measures under another name is caught too
                if (source == ProjectRulesFile.Past && (run.Request.ProjectMode == null || run.Request.ProjectMode != ProjectRulesFile.PastJobMode(config)))
                {
                    throw run.Refusal("the template " + name + " holds past job measures, only a project in past job mode may take them, never a new project");
                }
                var made = (JObject)rule.DeepClone();
                made.Remove("fixed");
                made.Remove("confirmedBy");
                made.Remove("confirmedWhen");
                made["status"] = ProjectRulesFile.NotConfirmed;
                if (!hasValue)
                {
                    made["value"] = JValue.CreateNull();
                    made["marked"] = made["marked"]?.Type == JTokenType.String ? made["marked"] : ProjectRulesFile.MissingMark;
                }
                rules.Add(made);
            }
            return rules;
        }
    }
}
