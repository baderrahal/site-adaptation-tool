using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.Common.CheckTable
{
    // R801, R802. The counts of the plan the last run of 4 Check wrote, the one plan steps 5 to 8 build from, so card 2 names the plots the plan
    // leaves out with why and card 3 counts the plots of the scope with no road level, not those of the whole read (F29, F30 of short check 2)
    public sealed class CheckPlanCount
    {
        // R801. The plain words of a key a plot is left out for, a key not here is named as the plan names it
        private static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["skip"] = "on the skip list",
            ["unit"] = "no unit",
            ["prototypeFile"] = "no prototype file",
            ["transform"] = "no outline or front",
            ["mirrored"] = "no hand",
            ["insert"] = "no link position",
            ["levels.ffl"] = "no floor level",
        };

        public const string LeftOut = "{0} of {1} plots are not built: {2}.";
        public const string AllBuilt = "All {0} plots of the plan are built.";
        public const string WithMissing = " Built with a value missing: {0} plots, each value tagged missing in the plan.";

        public string Nbh { get; private set; }
        public int Plots { get; private set; }
        public int Written { get; private set; }
        public int NotWritten { get; private set; }
        public int WrittenWithMissing { get; private set; }
        public int NoRoadLevel { get; private set; }
        public List<KeyValuePair<string, int>> NotWrittenBy { get; } = new List<KeyValuePair<string, int>>();

        // The plan of the newest check folder of the work folder, as its record names it, null with none. VillaLoad.LastPlan stops steps 5 to 8 on
        // the same folder, so the card speaks of the plan they build from
        public static string LatestPlan(string workFolder)
        {
            if (string.IsNullOrEmpty(workFolder) || !Directory.Exists(workFolder))
            {
                return null;
            }
            try
            {
                string folder = Directory.GetDirectories(workFolder, CheckRun.FolderPrefix + "*").OrderByDescending(item => Path.GetFileName(item), StringComparer.Ordinal).FirstOrDefault();
                JObject record = folder == null ? null : CheckRun.RecordOf(folder);
                string plan = (record?["outputs"] as JArray ?? new JArray()).OfType<JObject>().Select(output => (string)output["path"])
                    .FirstOrDefault(path => path != null && Path.GetFileName(path).StartsWith("plan_", StringComparison.OrdinalIgnoreCase));
                return plan != null && File.Exists(plan) ? plan : null;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is InvalidCastException || failure is ArgumentException)
            {
                return null;
            }
        }

        // The counts of a plan file, null when it does not read
        public static CheckPlanCount Read(string planFile)
        {
            if (planFile == null)
            {
                return null;
            }
            try
            {
                return Of(JObject.Parse(WorkFiles.ReadText(planFile)));
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is InvalidCastException || failure is ArgumentException)
            {
                return null;
            }
        }

        public static CheckPlanCount Of(JObject plan)
        {
            JObject summary = plan["summary"] as JObject ?? new JObject();
            var found = new CheckPlanCount
            {
                Nbh = (string)plan["neighbourhood"],
                Plots = (int?)summary["plots"] ?? 0,
                Written = (int?)summary["written"] ?? 0,
                NotWritten = (int?)summary["notWritten"] ?? 0,
                WrittenWithMissing = (int?)summary[BuildPlan.WrittenWithMissingKey] ?? 0,
                NoRoadLevel = (summary["plotsWithNoRoadLevel"] as JArray)?.Count ?? 0,
            };
            // The most plots first, so the line leads with the main reason
            found.NotWrittenBy.AddRange((summary[BuildPlan.NotWrittenByKey] as JObject ?? new JObject()).Properties()
                .Select(item => new KeyValuePair<string, int>(item.Name, (int)item.Value)).OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal));
            return found;
        }

        // R801. The line of card 2: the plots left out of the plan and why, then the plots built with a value missing
        public string Line
        {
            get
            {
                string head = NotWritten == 0 ? string.Format(CultureInfo.InvariantCulture, AllBuilt, Plots)
                    : string.Format(CultureInfo.InvariantCulture, LeftOut, NotWritten, Plots, string.Join(", ", NotWrittenBy.Select(item => Reason(item.Key) + " " + item.Value.ToString(CultureInfo.InvariantCulture))));
                return head + (WrittenWithMissing > 0 ? string.Format(CultureInfo.InvariantCulture, WithMissing, WrittenWithMissing) : string.Empty);
            }
        }

        public static string Reason(string key)
        {
            return Reasons.TryGetValue(key, out string words) ? words : key;
        }
    }
}
