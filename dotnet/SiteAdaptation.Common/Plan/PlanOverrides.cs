using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R360, R280, R330, R386. The override list a build applies, as plan_rules.load_overrides gives it: the entries of the sources of
    // majority.overrideDefaultSources by plot and field in the order of the list, the plots of the skip list with their reason and the
    // entries marked use rule with why, which a build leaves
    public sealed class OverrideList
    {
        public OverrideList(Dictionary<string, JObject> found, Dictionary<string, string> skip = null, Dictionary<string, Dictionary<string, string>> ruled = null)
        {
            Found = found ?? new Dictionary<string, JObject>();
            Skip = skip ?? new Dictionary<string, string>();
            Ruled = ruled ?? new Dictionary<string, Dictionary<string, string>>();
        }

        public Dictionary<string, JObject> Found { get; }

        public Dictionary<string, string> Skip { get; }

        public Dictionary<string, Dictionary<string, string>> Ruled { get; }

        // The overrides of one plot by field, none for a plot the list does not hold
        public JObject Of(string plot)
        {
            return Found.TryGetValue(plot, out JObject fields) ? fields : new JObject();
        }
    }

    // R360. A plot as a build by keyRules gives it after the overrides of the list: the values of the step overrides and of the step
    // parameters (R361)
    public sealed class PlotPlan
    {
        public Dictionary<string, object> Overrides { get; set; }

        public Dictionary<string, object> Parameters { get; set; }

        // Q107. The applied overrides of the plot in the order the build applied them, so a status names the one that moved a value
        public List<string> AppliedOrder { get; set; }

        // R363. The keys of the missing list of the plan for the plot in the order plan_from_book.build lists them, for the check rows of step 14.
        // The worker reads no site file, so a floor never lacks its level here
        public List<string> Lacks { get; set; } = new List<string>();

        // R365. The type of the yard outline the plan places, which floorTypes of the plan summary counts
        public string YardType { get; set; }

        // R408. Under rev6 the slope of El @ Entry and the front corners after the overrides of a plot with El @ Entry, a skip plot as well,
        // for the list of the slopes outside the manual
        public double? Slope { get; set; }
        // Q116. The ramp moved to the open edge a rotation override turned the plot to face, so the plot is no conflict of the plan
        public bool RampOnOpenEdge { get; set; }
    }

    // R360, R337, R340, R280, R386. Step 12 of the plan builder: what a build does with each override of each plot, applied or left with why, as
    // the Outcomes of plan_from_book.py give it, and the values after the overrides: the mirror flag, the link of R356 on that hand with the insert
    // moved and the rotation turned by their gaps (moved_link), El @ Entry, the FFL, SSL, the height and the steps of R359 moved by entryM, heightM
    // and steps, and for a plot the plan writes the ramp corners of Step 8 with the rampFrontM gap, the ramp type of a rampType override and the
    // yard of R358 on the link after the overrides. The worker reads no site file, so every level of floors.levels is taken as one the new site
    // file holds
    public static class PlanOverrides
    {
        public const string StepName = "overrides";

        // plan_from_book.NO_GAP, LEFT and plan_rules.USE_RULE, the lines a build leaves an override with
        public const string NoGap = "the override holds no gap to the rule";
        // plan_from_book.OPEN_EDGE_GAP, Q116. The rampFront gap holds the road of the front of R355, not of the open edge of a turned plot
        public const string OpenEdgeGap = "measured on the front of R355, the ramp moved to the open edge (Q116)";
        public const string UseRule = "use rule, the override list marks the entry to follow the rule (R386)";
        private const string ParameterWord = "parameter ";
        private const string KeyRulesSource = "keyRules";
        private const string OverrideSource = "override";

        private static readonly Dictionary<string, string> Left = new Dictionary<string, string>
        {
            ["rampOutline"] = "an outline gap, a build places the ramp outline of R127", ["yardOutline"] = "an outline gap, a build places the fitted yard outline",
            ["genericFloor"] = "an outline gap, a build places the whole face", ["rampFrontFallM"] = "a ramp front fall is not applied (Q057)",
        };

        private static readonly string[] Uses = { "rule", "past" };
        private static readonly string[] SkipFields = { "plot", "reason", "byHand" };

        // json.dumps of Python with its default separators, ", " and ": ", as the lines of override_source write an entry
        public static string Dumps(JToken value)
        {
            if (value is JObject item)
            {
                return "{" + string.Join(", ", item.Properties().Select(property => PyJson.Dumps(new JValue(property.Name)) + ": " + Dumps(property.Value))) + "}";
            }
            if (value is JArray list)
            {
                return "[" + string.Join(", ", list.Select(Dumps)) + "]";
            }
            return PyJson.Dumps(value);
        }

        private static string Text(JToken value)
        {
            return SiteClusters.PyText(value);
        }

        // re.sub("[^a-z]", "", str(name).lower()), the letters of a field name as override_source compares a spelling
        private static string Letters(string name)
        {
            return new string(name.ToLowerInvariant().Where(letter => letter >= 'a' && letter <= 'z').ToArray());
        }

        private static bool IsTrue(JToken value)
        {
            return value != null && value.Type == JTokenType.Boolean && (bool)value;
        }

        // override_source.by_hand: true for "byHand": true, false for no mark, a mark of another value or spelling refused
        private static bool ByHand(JObject item)
        {
            foreach (JProperty name in item.Properties())
            {
                if (name.Name != "byHand" && Letters(name.Name) == "byhand")
                {
                    throw new InvalidDataException("override of plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " marks a hand edit as " + PyJson.Dumps(new JValue(name.Name))
                        + ", spell it \"byHand\": true (R330)");
                }
            }
            if (item.Property("byHand") == null)
            {
                return false;
            }
            if (!IsTrue(item["byHand"]))
            {
                throw new InvalidDataException("override of plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " has byHand " + Dumps(item["byHand"])
                    + ", mark an entry edited by hand with \"byHand\": true or leave it out (R330)");
            }
            return true;
        }

        // override_source.use_of: rule or past, null for an entry with none, another value, spelling or a use with no byHand refused
        public static string UseOf(JObject item)
        {
            foreach (JProperty name in item.Properties())
            {
                if (name.Name != "use" && Letters(name.Name) == "use")
                {
                    throw new InvalidDataException("override of plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " names its Use as " + PyJson.Dumps(new JValue(name.Name)) + ", spell it \"use\" (R386)");
                }
            }
            if (item.Property("use") == null)
            {
                return null;
            }
            JToken use = item["use"];
            if (use.Type != JTokenType.String || !Uses.Contains((string)use))
            {
                throw new InvalidDataException("override of plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " has use " + Dumps(use) + ", a use is rule or past (R386)");
            }
            if (!ByHand(item))
            {
                throw new InvalidDataException("override of plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " has use " + (string)use
                    + " with no \"byHand\": true, a use is a hand edit a majority run must keep (R386)");
            }
            return (string)use;
        }

        // override_source.migrate_entry as check_entries holds it: the job of its key as source and its past model in model, then the mark and the use
        private static void CheckEntry(JObject item, JObject groups)
        {
            var jobs = new Dictionary<string, string>();
            foreach (JProperty group in groups.Properties())
            {
                foreach (JToken key in group.Value as JArray ?? new JArray())
                {
                    jobs[Text(key)] = group.Name;
                }
            }
            JToken named = item["key"];
            string job = named != null && named.Type == JTokenType.String && jobs.TryGetValue((string)named, out string found) ? found : null;
            if (job == null)
            {
                throw new InvalidDataException("override of plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " names key " + Text(named) + ", which no group of majority.groups holds (R330)");
            }
            JToken source = item["source"];
            if (item.Property("model") != null)
            {
                if (source == null || source.Type != JTokenType.String || (string)source != job)
                {
                    throw new InvalidDataException("override of plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " has source " + Text(source) + ", the job of its key "
                        + Text(named) + " is " + job + " (R330)");
                }
            }
            else
            {
                if (source == null || source.Type == JTokenType.Null || (source.Type == JTokenType.String && groups[(string)source] != null))
                {
                    throw new InvalidDataException("override of plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " names no past model, an entry with a job as source needs model (R330)");
                }
                throw new InvalidDataException("override of plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " is not in the form of R330, run override_source.py migrate");
            }
            ByHand(item);
            UseOf(item);
        }

        // override_source.check_entries: every fault named in one line, five of them
        public static void CheckEntries(JArray entries, JObject groups)
        {
            var faults = new List<string>();
            foreach (JToken entry in entries)
            {
                try
                {
                    CheckEntry(entry as JObject ?? throw new InvalidDataException("override " + Dumps(entry) + " is no object of R330"), groups);
                }
                catch (InvalidDataException refused)
                {
                    faults.Add(refused.Message);
                }
            }
            if (faults.Count > 0)
            {
                throw new InvalidDataException(faults.Count + " overrides refused: " + string.Join(" | ", faults.Take(5)) + (faults.Count > 5 ? " and more" : ""));
            }
        }

        // override_source.check_skip: each entry {"plot", "reason", "byHand": true}, the plot and the reason texts that are not empty, one per plot
        public static Dictionary<string, string> CheckSkip(JToken skip, string where)
        {
            if (skip == null || skip.Type == JTokenType.Null)
            {
                return null;
            }
            if (!(skip is JArray items))
            {
                throw new InvalidDataException(where + "skip of the override list is no list of entries (R386)");
            }
            var found = new Dictionary<string, string>();
            var faults = new List<string>();
            foreach (JToken entry in items)
            {
                JObject item = entry as JObject;
                bool whole = item != null && item.Count == SkipFields.Length && SkipFields.All(name => item.Property(name) != null) && IsTrue(item["byHand"]);
                bool texts = whole && new[] { "plot", "reason" }.All(name => item[name].Type == JTokenType.String && ((string)item[name]).Trim().Length > 0);
                if (!texts)
                {
                    faults.Add(Dumps(entry) + " is not {\"plot\": text, \"reason\": text, \"byHand\": true}");
                }
                else if (found.ContainsKey((string)item["plot"]))
                {
                    faults.Add("plot " + (string)item["plot"] + " is skipped twice");
                }
                else
                {
                    found[(string)item["plot"]] = (string)item["reason"];
                }
            }
            if (faults.Count > 0)
            {
                throw new InvalidDataException(where + faults.Count + " skip entries refused: " + string.Join(" | ", faults.Take(5)) + " (R386)");
            }
            return found;
        }

        // override_source.skip_plots_known: every plot a skip names a plot of plots.csv as plots.csv spells it
        public static void SkipPlotsKnown(IReadOnlyDictionary<string, string> skip, IReadOnlyDictionary<string, Dictionary<string, string>> rows, string where)
        {
            List<string> unknown = skip.Keys.Where(plot => !rows.ContainsKey(plot)).OrderBy(plot => plot, StringComparer.Ordinal).ToList();
            if (unknown.Count > 0)
            {
                throw new InvalidDataException(where + unknown.Count + " skip plots are no plot of plots.csv, a skip names the plot as plots.csv spells it: "
                    + string.Join(", ", unknown.Take(10).Select(plot => PyJson.Dumps(new JValue(plot)))) + " (R386)");
            }
        }

        // override_source.default_sources: majority.overrideDefaultSources, jobs of majority.groups, each once
        // R408. Under rev6 the plots of the scope whose slope after the overrides lies outside slopes_percent front_min to front_max of the rules
        // profile, as plan_from_book.build lists them, null under the past job
        public static JArray Outside(IEnumerable<string> numbers, LevelBuild build, Dictionary<string, PlotPlan> plans, JObject rules)
        {
            if (!build.Rev6)
            {
                return null;
            }
            var slopes = plans.Where(plan => plan.Value.Slope.HasValue).ToDictionary(plan => plan.Key, plan => plan.Value.Slope.Value);
            return DesignBasis.OutsideList(numbers, slopes, (JObject)rules["slopes_percent"]);
        }

        public static List<string> DefaultSources(JObject config, string configName)
        {
            JToken found = config["majority"]?["overrideDefaultSources"];
            JObject groups = config["majority"]?["groups"] as JObject;
            List<string> names = (found as JArray)?.Select(item => item.Type == JTokenType.String ? (string)item : null).ToList();
            if (names == null || names.Count == 0 || names.Contains(null) || names.Distinct().Count() != names.Count || groups == null || names.Any(name => groups[name] == null))
            {
                throw new InvalidDataException(configName + " majority.overrideDefaultSources must list jobs of majority.groups, each once (R330)");
            }
            return names;
        }

        // plan_rules.whole_number: the whole number of a step count as a number or its text, null for any other value such as 2.6 or two
        public static long? WholeNumber(JToken value)
        {
            double found;
            if (value == null)
            {
                return null;
            }
            switch (value.Type)
            {
                case JTokenType.Boolean:
                    return (bool)value ? 1 : 0;
                case JTokenType.Integer:
                case JTokenType.Float:
                    found = BuildFace.Number(value);
                    break;
                case JTokenType.String:
                    if (!double.TryParse(((string)value).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out found))
                    {
                        return null;
                    }
                    break;
                default:
                    return null;
            }
            return !double.IsInfinity(found) && !double.IsNaN(found) && Math.Floor(found) == found ? (long?)found : null;
        }

        // plan_rules.gap_of: the gapToRuleM of a level override, null for one the rule gave no gap to
        public static JToken GapOf(JToken value, string word = "gapToRuleM")
        {
            JToken gap = (value as JObject)?[word];
            return gap == null || gap.Type == JTokenType.Null ? null : gap;
        }

        // plan_rules.load_overrides with the sources of majority.overrideDefaultSources, every field of the job: the checks of R330 and R386 and the
        // job of the level profile first, then a source with no override, an override with no value, a step count that is no whole number and two
        // entries of one plot and field refused, an entry marked use rule left in Ruled
        public static OverrideList Load(string path, JObject config, JObject levelProfile, string profileName, string configName)
        {
            List<string> sources = DefaultSources(config, configName);
            JObject data = PyMath.ReadJson(path, "overrides") as JObject ?? throw new InvalidDataException("the input overrides is no JSON object: " + path);
            JToken listed = data["overrides"];
            JArray entries = listed == null || listed.Type == JTokenType.Null || (listed is JObject none && none.Count == 0) ? new JArray() : listed as JArray
                ?? throw new InvalidDataException(path + ": overrides of the override list is no list of entries (R330)");
            JObject groups = config["majority"]?["groups"] as JObject ?? throw new InvalidDataException(configName + " holds no majority.groups (R330)");
            try
            {
                CheckEntries(entries, groups);
            }
            catch (InvalidDataException refused)
            {
                throw new InvalidDataException(path + ": " + refused.Message);
            }
            Dictionary<string, string> skip = CheckSkip(data["skip"], path + ": ") ?? new Dictionary<string, string>();
            JToken job = levelProfile["job"];
            if (job == null || job.Type == JTokenType.Null || (job.Type == JTokenType.String && ((string)job).Length == 0))
            {
                throw new InvalidDataException("the level profile " + profileName + " names no job, its overrides cannot be matched");
            }
            List<string> wrong = sources.Where(source => Text(job) != source).OrderBy(source => source, StringComparer.Ordinal).ToList();
            if (wrong.Count > 0)
            {
                throw new InvalidDataException("the overrides of source " + string.Join(", ", wrong) + " are gaps to the rule of that job, the level profile " + profileName + " is of the " + Text(job)
                    + " job: name --override-source " + Text(job) + " on a build of " + profileName + ", native overrides are never applied by default (R280, R330)");
            }
            List<JObject> chosen = entries.OfType<JObject>().Where(item => sources.Contains((string)item["source"])).ToList();
            List<string> missing = sources.Except(chosen.Select(item => (string)item["source"])).OrderBy(source => source, StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidDataException("the override list holds no override of " + string.Join(", ", missing) + " among the sources " + string.Join(", ", sources.OrderBy(source => source, StringComparer.Ordinal)) + " (R280, R330)");
            }
            List<string> empty = chosen.Where(item => item["value"] == null || item["value"].Type == JTokenType.Null)
                .Select(item => "plot " + Text(item["plot"]) + " field " + Text(item["field"]) + " of " + Text(item["model"])).ToList();
            if (empty.Count > 0)
            {
                throw new InvalidDataException(empty.Count + " overrides of the build hold no value: " + string.Join(" | ", empty.Take(10))
                    + ". A miss with no value belongs in the cannot list of R272, take it out of overrides (R280)");
            }
            List<string> broken = chosen.Where(item => item["field"]?.Type == JTokenType.String && (string)item["field"] == "steps" && WholeNumber(item["value"]) == null)
                .Select(item => "plot " + Text(item["plot"]) + " value " + Dumps(item["value"]) + " of " + Text(item["model"])).ToList();
            if (broken.Count > 0)
            {
                throw new InvalidDataException(broken.Count + " steps overrides hold no whole number: " + string.Join(" | ", broken.Take(10)) + ". Give the step count as a whole number (R280, R340)");
            }
            var found = new Dictionary<string, JObject>();
            var ruled = new Dictionary<string, Dictionary<string, string>>();
            var origin = new Dictionary<string, string>();
            var twice = new List<string>();
            foreach (JObject item in chosen)
            {
                if (item["field"] == null || item["field"].Type != JTokenType.String)
                {
                    throw new InvalidDataException("override of plot " + Text(item["plot"]) + " names the field " + Text(item["field"]) + ", a field is a text (R330)");
                }
                string plot = Text(item["plot"]), field = (string)item["field"], pair = plot + "\n" + field;
                string named = Text(item["key"]) + " " + Text(item["model"]);
                if (origin.TryGetValue(pair, out string first))
                {
                    twice.Add("plot " + plot + " field " + field + " of " + first + " and " + named);
                    continue;
                }
                origin[pair] = named;
                if (UseOf(item) == "rule")
                {
                    if (!ruled.ContainsKey(plot))
                    {
                        ruled[plot] = new Dictionary<string, string>();
                    }
                    ruled[plot][field] = UseRule;
                }
                else
                {
                    if (!found.ContainsKey(plot))
                    {
                        found[plot] = new JObject();
                    }
                    found[plot][field] = item["value"].DeepClone();
                }
            }
            if (twice.Count > 0)
            {
                throw new InvalidDataException(twice.Count + " plots hold two overrides of one field: " + string.Join(" | ", twice.Take(10)) + ". Narrow the list with --override-model or edit it by hand (R280)");
            }
            return new OverrideList(found, skip, ruled);
        }

        // plan_from_book.Outcomes for the plots of the build: what was applied and what was left with why, the first why of a field kept
        private sealed class Outcomes
        {
            private readonly Dictionary<string, HashSet<string>> done = new Dictionary<string, HashSet<string>>();
            private readonly Dictionary<string, Dictionary<string, string>> why = new Dictionary<string, Dictionary<string, string>>();
            // Q107. The overrides of each plot in the order the build applied them
            private readonly Dictionary<string, List<string>> order = new Dictionary<string, List<string>>();

            public void Applied(string plot, string field)
            {
                if (!done.ContainsKey(plot))
                {
                    done[plot] = new HashSet<string>();
                    order[plot] = new List<string>();
                }
                if (done[plot].Add(field))
                {
                    order[plot].Add(field);
                }
            }

            // Outcomes.in_order: the applied overrides of a plot, those Plot gives, in the order the build applied them
            public List<string> InOrder(string plot, ICollection<string> applied)
            {
                return order.TryGetValue(plot, out List<string> found) ? found.Where(applied.Contains).ToList() : new List<string>();
            }

            public void Leave(string plot, string field, string because)
            {
                if (!why.ContainsKey(plot))
                {
                    why[plot] = new Dictionary<string, string>();
                }
                if (!why[plot].ContainsKey(field))
                {
                    why[plot][field] = because;
                }
            }

            // Outcomes.plot: the applied and the left overrides of a plot, null for a plot with none
            public Tuple<List<string>, JObject> Plot(string plot, bool written, IEnumerable<string> lacks, OverrideList list, ICollection<string> later, string profileName)
            {
                list.Ruled.TryGetValue(plot, out Dictionary<string, string> ruled);
                ruled = ruled ?? new Dictionary<string, string>();
                List<string> fields = list.Of(plot).Properties().Select(item => item.Name).Union(ruled.Keys).OrderBy(field => field, StringComparer.Ordinal).ToList();
                if (fields.Count == 0)
                {
                    return null;
                }
                why.TryGetValue(plot, out Dictionary<string, string> because);
                done.TryGetValue(plot, out HashSet<string> taken);
                string unwritten = "the plot is not written, it lacks " + string.Join(", ", lacks);
                var applied = new List<string>();
                var left = new JObject();
                foreach (string field in fields)
                {
                    if (ruled.ContainsKey(field))
                    {
                        left[field] = ruled[field];
                    }
                    else if (because != null && because.ContainsKey(field))
                    {
                        left[field] = because[field];
                    }
                    else if (taken != null && taken.Contains(field))
                    {
                        if (written)
                        {
                            applied.Add(field);
                        }
                        else
                        {
                            left[field] = unwritten;
                        }
                    }
                    else if (Left.ContainsKey(field))
                    {
                        left[field] = Left[field];
                    }
                    else if (later.Contains(field))
                    {
                        left[field] = "left to 3c, majority.overrideKeep.laterFields";
                    }
                    else if (!written)
                    {
                        left[field] = unwritten;
                    }
                    else
                    {
                        left[field] = "a build of " + profileName + " does not apply the field";
                    }
                }
                return Tuple.Create(applied, left);
            }
        }

        // Python truthiness of a JSON value, as a rampType override or the class of the rule is taken by or
        private static bool Truthy(JToken value)
        {
            switch (value?.Type ?? JTokenType.Null)
            {
                case JTokenType.Boolean:
                    return (bool)value;
                case JTokenType.Integer:
                case JTokenType.Float:
                    return BuildFace.Number(value) != 0;
                case JTokenType.String:
                    return ((string)value).Length > 0;
                case JTokenType.Array:
                case JTokenType.Object:
                    return value.HasValues;
                default:
                    return false;
            }
        }

        private static List<string> LaterFields(JObject config, JObject levelProfile)
        {
            JToken job = levelProfile["job"];
            JArray fields = job != null && job.Type == JTokenType.String ? config["majority"]?["overrideKeep"]?["laterFields"]?[(string)job] as JArray : null;
            return (fields ?? new JArray()).Select(Text).ToList();
        }

        // plan_from_book.build for the plots of the scope after the rule: the overrides of the list applied in the order the build applies them,
        // El @ Entry, then the FFL and the steps, then per plot the mirror flag, the link, its insert and rotation, the parameters and the floors
        public static Dictionary<string, PlotPlan> Of(IReadOnlyList<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows, LevelBuild build,
            IReadOnlyDictionary<string, FrontItem> linkFronts, HandContext context, IReadOnlyDictionary<string, Dictionary<string, object>> hands, JObject keyRules,
            JObject levelProfile, JObject rules, JObject config, IReadOnlyDictionary<string, string> files, OverrideList list, string profileName, double step,
            string rulesName = "the rules profile", Func<double, double, KeyValuePair<double?, string>> roadLevel = null)
        {
            if ((string)keyRules["yardOutline"]["rule"] != "fittedOutlineRefit")
            {
                throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture, FloorOutlines.YardGroups, profileName));
            }
            double ground = Fronts.Metres(rules, rulesName, rules["levels"]?["ground_ssl"], "levels.ground_ssl");
            JObject baseRule = keyRules["baseHeight"] as JObject;
            string stepsRule = (string)keyRules["steps"]["rule"];
            JArray groups = keyRules["yardOutline"]["groups"] as JArray ?? new JArray();
            List<string> later = LaterFields(config, levelProfile);
            HashSet<string> laterParameters = KeyLevels.LaterParameters(config, levelProfile);
            List<string> facade = ((config["majority"]?["linkNameFacade"] as JArray) ?? new JArray()).Select(Text).ToList();
            double near = FloorOutlines.JointNear(config, "config");
            var outcomes = new Outcomes();
            // The hand of each plot after a mirror flag override, the link of R356 on it
            var turned = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                var hand = new Dictionary<string, object>(hands[number]);
                JToken flag = list.Of(number)["mirrored"];
                if (!list.Skip.ContainsKey(number) && flag != null && flag.Type == JTokenType.Boolean)
                {
                    hand["mirrored"] = flag.DeepClone();
                }
                turned[number] = hand;
            }
            Dictionary<string, Dictionary<string, object>> links = LinkFrames.Step(numbers, rows, linkFronts, context.Roles, turned, keyRules, files, rules);
            var found = new Dictionary<string, PlotPlan>();
            foreach (string number in numbers)
            {
                JObject mine = list.Of(number);
                rows.TryGetValue(number, out Dictionary<string, string> row);
                string unit = null, zone = null;
                row?.TryGetValue("typologyRev9", out unit);
                row?.TryGetValue("zone", out zone);
                unit = string.IsNullOrEmpty(unit) ? null : unit;
                list.Skip.TryGetValue(number, out string skipped);
                var made = new Dictionary<string, object>
                {
                    ["unit"] = unit, ["skip"] = skipped, ["written"] = false, ["applied"] = null, ["notApplied"] = null, ["mirrored"] = null, ["insert"] = null, ["rotationDeg"] = null,
                    ["roadAtFront"] = null, ["entry"] = null, ["ffl"] = null, ["ssl"] = null, ["height"] = null, ["steps"] = null, ["rampZ"] = null, ["rampType"] = null, ["rampOutline"] = null, ["yardZ"] = null,
                    ["yardOutline"] = null, ["slopePercent"] = null,
                };
                var parameters = new Dictionary<string, object> { ["unit"] = unit, ["written"] = false, ["parameters"] = null, ["sources"] = null, ["lacks"] = null, ["linkName"] = null };
                found[number] = new PlotPlan { Overrides = made, Parameters = parameters };
                build.Fronts.TryGetValue(number, out FrontItem item);
                build.Entries.TryGetValue(number, out EntryLevel entry);
                build.Rows.TryGetValue(number, out LevelRow level);
                // El @ Entry of the rule at 4 decimals, an entryM gap added and rounded to them again
                double? entryWritten = null;
                if (item?.Front != null && entry != null)
                {
                    // R364. The unrounded mean of the two front corners, which no override moves
                    made["roadAtFront"] = KeyLevels.RoadAtFront(item.Front);
                    JToken gap = GapOf(mine["entryM"]);
                    if (gap != null)
                    {
                        made["entry"] = entry.Rule + BuildFace.Number(gap);
                        entryWritten = Rounding.Fixed(entry.Rule + BuildFace.Number(gap), 4);
                        outcomes.Applied(number, "entryM");
                    }
                    else
                    {
                        made["entry"] = entry.Raw;
                        entryWritten = entry.Rule;
                        if (mine.Property("entryM") != null)
                        {
                            outcomes.Leave(number, "entryM", NoGap);
                        }
                    }
                    if (build.Rev6)
                    {
                        // R408. The slope of El @ Entry after its override, the result the plan writes
                        found[number].Slope = gap == null ? entry.Slope : DesignBasis.SlopeOf((double)made["entry"], item.Front, item.Depth.Value);
                        made["slopePercent"] = found[number].Slope;
                    }
                }
                else if (mine.Property("entryM") != null && item?.Front != null && build.Rev6 && build.NoDoorWhy.TryGetValue(number, out string noDoor))
                {
                    // R408. A plot with a road level and no El @ Entry of the design basis, left with why
                    outcomes.Leave(number, "entryM", noDoor);
                }
                else if (mine.Property("entryM") != null)
                {
                    outcomes.Leave(number, "entryM", "no road level at a front corner, so no El @ Entry to move");
                }
                // plan_from_book.key_levels: the heightM gap moves the FFL onto the key with its SSL, height and steps, a steps override sets the steps
                double? fflWritten = null;
                JToken steps = null;
                if (level != null)
                {
                    JToken gap = GapOf(mine["heightM"]);
                    double fflRaw;
                    if (gap != null)
                    {
                        fflRaw = level.Ffl + BuildFace.Number(gap);
                        outcomes.Applied(number, "heightM");
                    }
                    else
                    {
                        fflRaw = level.Top;
                        if (mine.Property("heightM") != null)
                        {
                            outcomes.Leave(number, "heightM", NoGap);
                        }
                    }
                    fflWritten = Rounding.Fixed(fflRaw, 3);
                    double height = fflWritten.Value - entryWritten.Value;
                    steps = KeyLevels.StepsByRule(stepsRule, Rounding.Fixed(height, 4), entry.Unit, rules, levelProfile, baseRule);
                    long? count = WholeNumber(mine["steps"]);
                    if (count.HasValue)
                    {
                        steps = new JValue(count.Value);
                        outcomes.Applied(number, "steps");
                    }
                    else if (mine.Property("steps") != null)
                    {
                        outcomes.Leave(number, "steps", "the override holds no step count");
                    }
                    made["ffl"] = fflRaw;
                    made["ssl"] = fflWritten.Value + ground;
                    made["height"] = height;
                    made["steps"] = steps;
                }
                else
                {
                    foreach (string field in new[] { "heightM", "steps" })
                    {
                        if (mine.Property(field) != null)
                        {
                            outcomes.Leave(number, field, "no FFL, " + (item?.Front == null ? "no road level at a front corner" : "the rule gives none"));
                        }
                    }
                }
                if (skipped != null || unit == null)
                {
                    Tuple<List<string>, JObject> record = outcomes.Plot(number, false, new[] { skipped != null ? "skip" : "unit" }, list, later, profileName);
                    made["applied"] = record?.Item1;
                    made["notApplied"] = record?.Item2;
                    made["roadAtFront"] = made["entry"] = made["ffl"] = made["ssl"] = made["height"] = made["steps"] = made["slopePercent"] = null;
                    found[number].Lacks = new List<string> { skipped != null ? "skip" : "unit" };
                    continue;
                }
                JToken flagged = mine["mirrored"];
                if (flagged != null && flagged.Type == JTokenType.Boolean)
                {
                    outcomes.Applied(number, "mirrored");
                }
                else if (mine.Property("mirrored") != null)
                {
                    outcomes.Leave(number, "mirrored", "the override holds no mirror flag");
                }
                Dictionary<string, object> link = links[number];
                var lacks = ((JArray)link["lacks"]).Select(lack => (string)lack["key"]).ToList();
                double[] insert = link["insert"] as double[];
                StepValue rotation = link["rotationDeg"] as StepValue;
                JToken mirrored = insert == null ? null : (JToken)link["mirrored"];
                bool turnedLink = false;
                // plan_from_book.moved_link: the insert by its gap along the link axes of the hand, the rotation by its gap in degrees
                foreach (string field in new[] { "insert", "rotationDeg" })
                {
                    if (mine.Property(field) == null)
                    {
                        continue;
                    }
                    JToken gap = GapOf(mine[field], field == "insert" ? "gapToRuleM" : "gapToRuleDeg");
                    if (gap == null || item?.Frame == null || insert == null)
                    {
                        outcomes.Leave(number, field, gap == null ? NoGap : item?.Frame == null ? "the face has no front frame" : "the plot has no link");
                        continue;
                    }
                    if (field == "insert")
                    {
                        double[][] axes = LinkFrames.Axes(item.Frame, (bool)mirrored);
                        double[] point = insert.Select(value => Rounding.Fixed(value, 3)).ToArray();
                        double x = BuildFace.Number(gap[0]), y = BuildFace.Number(gap[1]);
                        insert = new[] { point[0] + x * axes[0][0] + y * axes[1][0], point[1] + x * axes[0][1] + y * axes[1][1] };
                    }
                    else
                    {
                        double turn = (double)rotation.Written + BuildFace.Number(gap);
                        rotation = new StepValue { Raw = turn, Written = LinkFrames.PlanRotation(turn) };
                        turnedLink = true;
                    }
                    outcomes.Applied(number, field);
                }
                if (insert != null)
                {
                    made["mirrored"] = mirrored.DeepClone();
                    made["insert"] = insert;
                    made["rotationDeg"] = rotation;
                }
                if (level == null)
                {
                    lacks.Add("levels.ffl");
                }
                // R361. The link parameters by keyRules on the hand, the steps and the FFL after the overrides, then the parameter overrides
                Tuple<JObject, List<string>> values = LinkParameters.KeyParameters(keyRules, number, unit, mirrored, steps, fflWritten, facade, laterParameters);
                JObject texts = values.Item1;
                var sources = new JObject();
                foreach (JProperty text in texts.Properties())
                {
                    sources[text.Name] = KeyRulesSource;
                }
                lacks.AddRange(values.Item2.Select(name => "parameters." + name));
                foreach (JProperty field in mine.Properties())
                {
                    if (!field.Name.StartsWith(ParameterWord, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (laterParameters.Contains(field.Name))
                    {
                        outcomes.Leave(number, field.Name, "left to 3c, majority.overrideKeep.laterFields, the build writes no such parameter");
                        continue;
                    }
                    string name = field.Name.Substring(ParameterWord.Length);
                    if (field.Value is JObject)
                    {
                        // R340. A level parameter holds its gap to the rule, the text of the rule moved by it with 3 decimals
                        JToken gap = GapOf(field.Value);
                        double? current = LinkParameters.Number(texts[name]);
                        if (gap == null || !current.HasValue)
                        {
                            outcomes.Leave(number, field.Name, gap == null ? NoGap : "the rule gives the parameter no value to move");
                            continue;
                        }
                        texts[name] = PyMath.FixedText(Rounding.Fixed(current.Value + BuildFace.Number(gap), 3), 3);
                    }
                    else
                    {
                        texts[name] = Text(field.Value);
                    }
                    sources[name] = OverrideSource;
                    outcomes.Applied(number, field.Name);
                }
                bool written = link["prototypeFile"] != null && insert != null && level != null;
                // R363. The floors a written plot lacks, the yard then the ramp as plan_from_book.build lists them
                bool noYard = false, noRamp = false;
                if (written)
                {
                    // plan_from_book.build: the ramp of Step 8 with the rampFrontM gap and the ramp type of a rampType override, then the yard
                    var moved = new List<string>();
                    double[] front = item.Front;
                    double[][] outline = item.Corners;
                    FrontItem open = turnedLink && roadLevel != null
                        ? Fronts.OpenRamp(item.Face, item.Frame, Convert.ToDouble(rotation.Written, CultureInfo.InvariantCulture), (bool)mirrored, item.Depth, roadLevel) : null;
                    // Q116. With no road level at an open corner the ramp stays on the front and the plot stays in the conflicts
                    if (open?.Front != null)
                    {
                        front = open.Front;
                        outline = open.Corners;
                        found[number].RampOnOpenEdge = true;
                        if (build.Rev6)
                        {
                            // R408. The slope the plan writes is that of the front of the open edge the ramp moved to
                            found[number].Slope = DesignBasis.SlopeOf((double)made["entry"], front, item.Depth.Value);
                            made["slopePercent"] = found[number].Slope;
                        }
                    }
                    JToken gap = GapOf(mine["rampFrontM"]);
                    if (found[number].RampOnOpenEdge && mine.Property("rampFrontM") != null)
                    {
                        outcomes.Leave(number, "rampFrontM", OpenEdgeGap);
                    }
                    else if (gap != null)
                    {
                        front = front.Select(z => z + BuildFace.Number(gap)).ToArray();
                        moved.Add("rampFrontM");
                        if (build.Rev6)
                        {
                            // R408. The slope of the front corners after the rampFrontM override, which the plan writes
                            found[number].Slope = DesignBasis.SlopeOf((double)made["entry"], front, item.Depth.Value);
                            made["slopePercent"] = found[number].Slope;
                        }
                    }
                    else if (mine.Property("rampFrontM") != null)
                    {
                        outcomes.Leave(number, "rampFrontM", NoGap);
                    }
                    JToken named = mine["rampType"];
                    if (named != null && named.Type != JTokenType.Null)
                    {
                        moved.Add("rampType");
                    }
                    // R408. Under rev6 a ramp with no rampType override takes the type of the band of its slope after the overrides, never keyRules.rampSlope
                    bool byBand = build.Rev6 && !Truthy(named);
                    JToken rampClass = Truthy(named) ? named : byBand ? null : keyRules["rampSlope"]["table"][unit];
                    JToken kind = byBand ? DesignBasis.RampType(found[number].Slope.Value, build.Bands) : rampClass != null && rampClass.Type == JTokenType.String ? levelProfile["rampTypes"][(string)rampClass] : null;
                    bool ramp = kind != null && kind.Type != JTokenType.Null;
                    noRamp = !ramp;
                    if (ramp)
                    {
                        made["rampZ"] = FloorOutlines.RampHeights(front, entryWritten.Value);
                        made["rampType"] = kind.DeepClone();
                        made["rampOutline"] = outline;
                    }
                    foreach (string field in moved)
                    {
                        if (ramp)
                        {
                            outcomes.Applied(number, field);
                        }
                        else
                        {
                            outcomes.Leave(number, field, "no ramp floor, " + (byBand ? string.Format(CultureInfo.InvariantCulture, FloorOutlines.NoBand, SheetRows.PyRepr(found[number].Slope.Value))
                                : "keyRules.rampSlope and rampTypes give no ramp type for " + unit));
                        }
                    }
                    // R410. No yard floor under rev6
                    double? below = build.Rev6 ? null : FloorOutlines.YardDepth(keyRules, zone ?? "");
                    if (below.HasValue)
                    {
                        JToken keo = (rules["typologies"]?[unit] as JObject)?["keo_file"];
                        string yardUnit = groups.OfType<JObject>().Any(group => (string)group["unit"] == unit) ? unit : keo != null && keo.Type == JTokenType.String && ((string)keo).Length > 0 ? (string)keo : unit;
                        JObject group = FloorOutlines.OutlineFor(groups, yardUnit, (bool)mirrored, FloorOutlines.WidthKey(item.Frame.Length, step));
                        if (group != null)
                        {
                            double[] origin = insert.Select(value => Rounding.Fixed(value, 3)).ToArray();
                            made["yardZ"] = fflWritten.Value - below.Value;
                            made["yardOutline"] = ((JArray)group["loops"]).Select(loop => FloorOutlines.ToWorld((JArray)loop, origin, (double)rotation.Written, (bool)mirrored)).ToList();
                            found[number].YardType = group["type"]?.Type == JTokenType.String ? (string)group["type"] : null;
                        }
                        noYard = group == null;
                    }
                }
                Tuple<List<string>, JObject> outcome = outcomes.Plot(number, written, lacks, list, later, profileName);
                found[number].Lacks = lacks.Concat(noYard ? new[] { "floors.yard" } : new string[0]).Concat(noRamp ? new[] { "floors.ramp" } : new string[0]).ToList();
                made["written"] = written;
                made["applied"] = outcome?.Item1;
                made["notApplied"] = outcome?.Item2;
                found[number].AppliedOrder = outcome == null ? null : outcomes.InOrder(number, outcome.Item1);
                parameters["written"] = written;
                parameters["parameters"] = texts;
                parameters["sources"] = sources;
                parameters["lacks"] = values.Item2;
                parameters["linkName"] = LinkParameters.LinkName(texts);
            }
            // R470. A corner ramps of two or more written plots share takes the mean of their levels, which BuildPlan writes through rampZ
            var ramps = numbers.Where(number => found[number].Overrides["rampZ"] != null)
                .Select(number => Tuple.Create(number, (double[][])found[number].Overrides["rampOutline"], (double[])found[number].Overrides["rampZ"])).ToList();
            foreach (KeyValuePair<string, double[]> joined in FloorOutlines.JointLevels(ramps, near))
            {
                found[joined.Key].Overrides["rampZ"] = joined.Value;
            }
            return found;
        }

        // plan_from_book.override_step: per plot of the scope the values of step 12
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IReadOnlyDictionary<string, PlotPlan> plans)
        {
            return numbers.ToDictionary(number => number, number => plans[number].Overrides);
        }
    }
}
