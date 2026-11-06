using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.Pane
{
    // R689. How many values of the last plan came from each source, the tags of R703 that FAST-2 writes per value: computed, rule, person entered and
    // missing, every other tag counted as other
    public sealed class SourceMix
    {
        public const string Computed = "computed";
        public const string Rule = "rule";
        public const string Person = "person entered";
        public const string Missing = "missing";

        public int ComputedCount { get; set; }
        public int RuleCount { get; set; }
        public int PersonCount { get; set; }
        public int MissingCount { get; set; }
        public int OtherCount { get; set; }
        public int Total { get { return ComputedCount + RuleCount + PersonCount + MissingCount + OtherCount; } }

        public string Line
        {
            get
            {
                return TextTemplate.Fill(StageResultsText.Mix, Text(ComputedCount), Text(RuleCount), Text(PersonCount), Text(MissingCount), Text(OtherCount));
            }
        }

        private static string Text(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture);
        }
    }

    // R689. What card 3 shows of the last build: the issues and the source mix of the values of its plan, from build_plan.json of the newest build folder
    // of the work folder, so a model opened again shows them too. The closing messages of the steps are no issues, card 2 says them
    public sealed class StageResults
    {
        public SourceMix Mix { get; set; } = new SourceMix();
        public List<string> Issues { get; } = new List<string>();
        public bool HasPlan { get; set; }

        // Q504, Bader's answer 5A of 6 Nov: the plan lists each plot whose floor level is missing as an issue with the words no road level, and the steps
        // 6 to 8 of version 1 leave such a plot out (R344). The words are those of FAST-2's plan
        public const string NoRoadLevel = "no road level";

        public int NoRoadLevelCount { get { return Issues.Count(issue => issue.IndexOf(NoRoadLevel, StringComparison.OrdinalIgnoreCase) >= 0); } }

        // An issue that names a rule, so card 3 offers the rules table from it, point 1 of R2-C3
        public bool RuleIssue { get { return Issues.Any(issue => issue.IndexOf("rule", StringComparison.OrdinalIgnoreCase) >= 0); } }

        // The newest plan of a build in the work folder, null with none
        public static string LatestPlan(string workFolder)
        {
            if (string.IsNullOrEmpty(workFolder) || !Directory.Exists(workFolder))
            {
                return null;
            }
            try
            {
                return Directory.GetDirectories(workFolder, SiteBuildPlan.FolderPrefix + "*")
                    .Select(folder => Path.Combine(folder, SiteBuildResult.PlanName))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static StageResults Read(string planFile)
        {
            var results = new StageResults();
            if (planFile != null)
            {
                try
                {
                    JObject plan = JObject.Parse(WorkFiles.ReadText(planFile));
                    var read = new SiteBuildResult();
                    SiteBuildPlan.Read(plan, read);
                    results.Issues.AddRange(read.Issues);
                    results.Mix = Count(plan);
                    results.HasPlan = true;
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is InvalidCastException || failure is ArgumentException)
                {
                    results.HasPlan = false;
                }
            }
            return results;
        }

        // Each value of each plot once, by its source
        public static SourceMix Count(JObject plan)
        {
            var mix = new SourceMix();
            foreach (JObject plot in (plan["plots"] as JArray ?? new JArray()).OfType<JObject>())
            {
                foreach (JProperty value in (plot["values"] as JObject ?? new JObject()).Properties())
                {
                    string source = value.Value is JObject held && held["source"]?.Type == JTokenType.String ? (string)held["source"] : null;
                    switch (source)
                    {
                        case SourceMix.Computed: mix.ComputedCount++; break;
                        case SourceMix.Rule: mix.RuleCount++; break;
                        case SourceMix.Person: mix.PersonCount++; break;
                        case SourceMix.Missing: mix.MissingCount++; break;
                        default: mix.OtherCount++; break;
                    }
                }
            }
            return mix;
        }
    }
}
