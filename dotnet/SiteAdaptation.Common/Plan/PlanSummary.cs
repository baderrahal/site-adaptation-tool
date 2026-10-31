using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R365, R401, Q116. Step 15 of the plan builder: the plan summary of plan_from_book.build, the counts and names that fill the top line of the
    // level Excel and the box Rules applied of the report, the status of each value by field and status, and the conflicts of the plan, as
    // plan_from_book.summary_step holds them in the table summary of its step file, which parity.py compares entry by entry exact
    public static class PlanSummary
    {
        public const string StepName = "summary";

        // plan_from_book.TURNED_KEPT and TURNED_OTHER
        public const string TurnedKept = "the ramp on the front of R355";
        public const string TurnedOther = "the open edge the rotationDeg override turned the link to face, which the walls leave open (R362)";

        // plan_rules.STEP_RULES, the floor kinds are SiteCoordinates.FloorKinds, plan_from_past.FLOOR_KINDS
        private static readonly string[] FloorKinds = SiteCoordinates.FloorKinds;
        private static readonly string[] StepRules = { "steps", "typologySteps", "plotCodeStepsAndFacade" };
        private const string ParameterWord = "parameter ";
        private const string Assumed = "assumed";

        private static bool Written(PlotPlan plan)
        {
            return Walls.Written(plan);
        }

        private static List<string> AppliedOf(PlotPlan plan)
        {
            return Walls.AppliedOf(plan);
        }

        // A value of the plan by its field, null where the plan holds none
        private static object Held(PlotPlan plan, string field)
        {
            return plan?.Overrides != null && plan.Overrides.TryGetValue(field, out object value) ? value : null;
        }

        private static void Add(Dictionary<string, int> counts, string name)
        {
            counts[name] = counts.TryGetValue(name, out int count) ? count + 1 : 1;
        }

        // dict(Counter.most_common()) of Python: the count first, a tie in the order first seen
        private static JObject MostCommon(Dictionary<string, int> counts, List<string> seen)
        {
            var found = new JObject();
            foreach (string name in seen.OrderByDescending(name => counts[name]))
            {
                found[name] = counts[name];
            }
            return found;
        }

        private static JObject Sorted(Dictionary<string, int> counts)
        {
            var found = new JObject();
            foreach (KeyValuePair<string, int> item in counts.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                found[item.Key] = item.Value;
            }
            return found;
        }

        // plan_from_book.turned_ramps: each written plot with a ramp whose rotationDeg override turned the edge its walls leave open off the
        // edge the rule leaves open, both by Walls.OpenEdge, but a plot whose ramp moved to the open edge (RampOnOpenEdge, Fronts.OpenRamp)
        public static List<JObject> Conflicts(IEnumerable<string> numbers, IReadOnlyDictionary<string, PlotPlan> plans, IReadOnlyDictionary<string, FrontItem> fronts,
            IReadOnlyDictionary<string, BuildFace> faces)
        {
            var found = new List<JObject>();
            foreach (string number in numbers)
            {
                plans.TryGetValue(number, out PlotPlan plan);
                fronts.TryGetValue(number, out FrontItem item);
                faces.TryGetValue(number, out BuildFace face);
                if (!Written(plan) || plan.RampOnOpenEdge || !AppliedOf(plan).Contains("rotationDeg") || plan.Overrides["rampType"] == null || item?.Frame == null || face == null)
                {
                    continue;
                }
                bool mirrored = (bool)(JToken)plan.Overrides["mirrored"];
                double[][] front = { item.Frame.Left, item.Frame.Right };
                double[][] rule = Walls.OpenEdge(face.Ring, front, LinkFrames.PlanRotation(LinkFrames.RotationOf(item.Frame, mirrored)), mirrored, true);
                double turn = Convert.ToDouble(((StepValue)plan.Overrides["rotationDeg"]).Written, CultureInfo.InvariantCulture);
                double[][] turned = Walls.OpenEdge(face.Ring, front, turn, mirrored, true);
                if (!rule.Zip(turned, (one, other) => one.SequenceEqual(other)).All(same => same))
                {
                    found.Add(new JObject
                    {
                        ["plot"] = number, ["field"] = "floors.ramp", ["kept"] = TurnedKept, ["other"] = TurnedOther, ["otherSource"] = "override rotationDeg", ["question"] = "Q116",
                    });
                }
            }
            return found;
        }

        // plan_from_book.summary_step: per plot its unit and whether the plan writes it, and the table summary in the order of SUMMARY_TABLE. fronts are
        // those of the build with the road levels, status the step status of R401 and table the details its keys name
        public static Tuple<Dictionary<string, Dictionary<string, object>>, JObject> Step(IList<string> numbers, IReadOnlyDictionary<string, PlotPlan> plans,
            IReadOnlyDictionary<string, FrontItem> fronts, IReadOnlyDictionary<string, Dictionary<string, object>> hands, IReadOnlyDictionary<string, Dictionary<string, object>> status,
            ValueStatusTable table, JObject config, JObject keyRules, string profileName, ICollection<string> later, IReadOnlyDictionary<string, BuildFace> faces,
            JArray noDoor = null, JArray outside = null)
        {
            var plots = new Dictionary<string, Dictionary<string, object>>();
            var frontRules = new Dictionary<string, int>();
            var frontSeen = new List<string>();
            var surfaces = new Dictionary<string, int>();
            var surfaceSeen = new List<string>();
            var floors = FloorKinds.ToDictionary(kind => kind, kind => 0);
            var types = new Dictionary<string, int>();
            var handRules = new Dictionary<string, int>();
            var notApplied = new Dictionary<string, int>();
            string generic = (string)config["floors"]?["types"]?["generic"]?[0];
            foreach (string number in numbers)
            {
                plans.TryGetValue(number, out PlotPlan plan);
                fronts.TryGetValue(number, out FrontItem item);
                string unit = Held(plan, "unit") as string;
                bool written = Written(plan);
                plots[number] = new Dictionary<string, object> { ["unit"] = unit, ["written"] = written };
                string rule = item?.FrontRule ?? "nearestCurb";
                if (!frontRules.ContainsKey(rule))
                {
                    frontSeen.Add(rule);
                }
                Add(frontRules, rule);
                if (unit != null && Held(plan, "skip") == null)
                {
                    foreach (string name in (item?.Surfaces ?? new string[0]).Select(name => string.IsNullOrEmpty(name) ? "none" : name))
                    {
                        if (!surfaces.ContainsKey(name))
                        {
                            surfaceSeen.Add(name);
                        }
                        Add(surfaces, name);
                    }
                }
                if (Held(plan, "notApplied") is JObject left)
                {
                    foreach (JProperty field in left.Properties())
                    {
                        Add(notApplied, field.Name);
                    }
                }
                if (!written)
                {
                    continue;
                }
                // The floors as ValueStatus.Sources takes them, the worker reads no site file, so the generic floor stands on every written plot
                floors["generic"] += 1;
                Add(types, "generic: " + generic);
                if (plan.Overrides["yardOutline"] != null)
                {
                    floors["yard"] += 1;
                    Add(types, "yard: " + plan.YardType);
                }
                if (plan.Overrides["rampType"] != null)
                {
                    floors["ramp"] += 1;
                    Add(types, "ramp: " + (string)(JToken)plan.Overrides["rampType"]);
                }
                hands.TryGetValue(number, out Dictionary<string, object> hand);
                object handRule = null;
                hand?.TryGetValue("handRule", out handRule);
                Add(handRules, AppliedOf(plan).Contains("mirrored") ? "override" : handRule as string ?? "None");
            }
            var summary = new JObject
            {
                ["frontRule"] = MostCommon(frontRules, frontSeen), ["roadSurfacesOfFrontCorners"] = MostCommon(surfaces, surfaceSeen),
                ["floorsFound"] = new JObject(FloorKinds.Select(kind => new JProperty(kind, floors[kind]))), ["floorTypes"] = Sorted(types), ["handRules"] = Sorted(handRules),
                ["overridesNotApplied"] = Sorted(notApplied),
                ["steps"] = new JObject
                {
                    ["from"] = "keyRules.steps " + (string)keyRules["steps"]["rule"] + " of " + profileName,
                    ["parameters"] = new JArray(keyRules.Properties().Where(item => item.Name.StartsWith(ParameterWord, StringComparison.Ordinal) && !later.Contains(item.Name)
                        && item.Value is JObject entry && entry["rule"]?.Type == JTokenType.String && StepRules.Contains((string)entry["rule"]))
                        .Select(item => item.Name.Substring(ParameterWord.Length)).OrderBy(name => name, StringComparer.Ordinal)),
                },
            };
            foreach (KeyValuePair<string, JToken> item in Status(numbers, status, table, config))
            {
                summary[item.Key] = item.Value;
            }
            summary["conflicts"] = new JArray(Conflicts(numbers, plans, fronts, faces));
            // R408. The lists of a build under rev6, plan_from_book.SUMMARY_REV6, null on a build of the past job
            summary["noDoorLevel"] = noDoor?.DeepClone();
            summary["slopeOutsideManual"] = outside?.DeepClone();
            return Tuple.Create(plots, new JObject { ["summary"] = summary });
        }

        // value_status.summary: statusByField by ValueStatus.Counts, one source with the counts of the worker, assumedByField the plots of each field
        // whose status valueStatus.assumed names, and statusPlots the plots holding a value of each status, assumed last
        private static JObject Status(IEnumerable<string> numbers, IReadOnlyDictionary<string, Dictionary<string, object>> status, ValueStatusTable table, JObject config)
        {
            var byField = new JObject();
            foreach (KeyValuePair<Tuple<string, string>, int> count in ValueStatus.Counts(status, table))
            {
                if (!(byField[count.Key.Item1] is JObject field))
                {
                    byField[count.Key.Item1] = field = new JObject();
                }
                field[count.Key.Item2] = count.Value;
            }
            var assumed = new HashSet<string>(((config["valueStatus"]?["assumed"] as JArray) ?? new JArray()).Select(item => (string)item));
            var listed = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var holding = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string number in numbers)
            {
                if (!status.TryGetValue(number, out Dictionary<string, object> plot) || !(plot["fields"] is JObject fields))
                {
                    continue;
                }
                foreach (JProperty field in fields.Properties())
                {
                    table.Used.TryGetValue((string)field.Value["statusFrom"], out JObject detail);
                    string name = ValueStatus.Label((string)field.Value["status"], detail);
                    if (!holding.ContainsKey(name))
                    {
                        holding[name] = new HashSet<string>();
                    }
                    holding[name].Add(number);
                    if (assumed.Contains(name))
                    {
                        if (!listed.ContainsKey(field.Name))
                        {
                            listed[field.Name] = new List<string>();
                        }
                        listed[field.Name].Add(number);
                        if (!holding.ContainsKey(Assumed))
                        {
                            holding[Assumed] = new HashSet<string>();
                        }
                        holding[Assumed].Add(number);
                    }
                }
            }
            var assumedByField = new JObject();
            foreach (KeyValuePair<string, List<string>> item in listed.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                assumedByField[item.Key] = new JArray(item.Value.OrderBy(plot => plot, PyMath.SortKey));
            }
            var statusPlots = new JObject();
            foreach (KeyValuePair<string, HashSet<string>> item in holding.OrderBy(item => item.Key == Assumed).ThenBy(item => item.Key, StringComparer.Ordinal))
            {
                statusPlots[item.Key] = item.Value.Count;
            }
            return new JObject { ["statusByField"] = byField, ["assumedByField"] = assumedByField, ["statusPlots"] = statusPlots };
        }
    }
}
