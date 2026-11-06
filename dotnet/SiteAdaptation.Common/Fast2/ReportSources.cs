using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;

namespace SiteAdaptation.Common
{
    public static partial class ReportSheets
    {
        // R703. The tags a row names first, in this order, any other tag after them by name. pastJob, keyRules, user pick and confirmed are
        // the plan's own words for past, rule and person entered
        private static readonly string[] SourceOrder =
        {
            ProjectRulesFile.Computed, ProjectRulesFile.FromRule, ProjectRulesFile.PersonEntered, ProjectRulesFile.Default, "override", ProjectRulesFile.Past, ProjectRulesFile.Missing,
        };

        private static readonly Dictionary<string, string> SourceWords = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pastJob"] = ProjectRulesFile.Past, ["keyRules"] = ProjectRulesFile.FromRule, ["user pick"] = ProjectRulesFile.PersonEntered, ["confirmed"] = ProjectRulesFile.PersonEntered,
        };

        // R702. A run of a new project: the config names the past job mode and the mode of the run is another. Its report prints no share of
        // the past models, a past share is a past measure and says nothing of this project. A config that names no mode keeps the shares
        public static bool NewProject(JObject config)
        {
            string past = ProjectRulesFile.PastJobMode(config);
            JToken mode = config?["designBasis"]?["mode"];
            return past != null && mode?.Type == JTokenType.String && (string)mode != past;
        }

        // R703. The sources of the plan values of one row of a sheet, each once, so every value on the sheet says where it came from. A value
        // with no source and no value is missing. Null when the row holds no plan value
        public static string SourcesOf(params JToken[] values)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject value in values.OfType<JObject>())
            {
                JToken source = value["source"];
                if (source?.Type == JTokenType.String && ((string)source).Trim().Length > 0)
                {
                    string tag = ((string)source).Trim();
                    found.Add(SourceWords.TryGetValue(tag, out string word) ? word : tag);
                }
                else if (value["value"] == null || value["value"].Type == JTokenType.Null)
                {
                    found.Add(ProjectRulesFile.Missing);
                }
            }
            if (found.Count == 0)
            {
                return null;
            }
            return string.Join(ReportExcelText.ListJoin, found.OrderBy(tag => Array.IndexOf(SourceOrder, tag) < 0 ? SourceOrder.Length : Array.IndexOf(SourceOrder, tag))
                .ThenBy(tag => tag, StringComparer.Ordinal));
        }
    }
}
