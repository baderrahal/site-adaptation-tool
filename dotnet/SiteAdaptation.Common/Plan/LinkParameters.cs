using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R361, R85, R337, R340. The link parameters of a plot by the parameter rules of keyRules, as plan_rules.key_parameters gives them: the tables
    // first, so the link Name takes the facade parameters they give, then the formulas, a parameter left to a later phase not written, a rule
    // that gives no value naming its parameter in the lacks
    public static class LinkParameters
    {
        public const string StepName = "parameters";

        private const string ParameterWord = "parameter ";
        private static readonly string[] TableFactors = { "all", "typology", "mirroredFactor", "typologyMirrored", "steps", "typologySteps" };

        // key_rules.number: a parameter that reads as a number, float() of Python on its text, else null
        public static double? Number(JToken text)
        {
            if (text == null || text.Type == JTokenType.Null)
            {
                return null;
            }
            if (text.Type == JTokenType.Integer || text.Type == JTokenType.Float)
            {
                return BuildFace.Number(text);
            }
            return text.Type == JTokenType.String && double.TryParse(((string)text).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double found) ? found : (double?)null;
        }

        // plan_rules.key_parameters: the text of each parameter of keyRules not in later by its name less the word parameter, and the names
        // a rule gives no value, sorted
        public static Tuple<JObject, List<string>> KeyParameters(JObject keyRules, string plot, string code, JToken mirrored, JToken steps, double? ffl, IList<string> facade,
            ICollection<string> later)
        {
            string step = steps == null || steps.Type == JTokenType.Null ? null : SiteClusters.PyText(steps);
            string flag = SiteClusters.PyText(mirrored);
            var factor = new Dictionary<string, string>
            {
                ["all"] = "all", ["typology"] = code, ["mirroredFactor"] = flag, ["typologyMirrored"] = code + " " + flag, ["steps"] = step,
                ["typologySteps"] = step == null ? null : code + " " + step,
            };
            List<JProperty> words = keyRules.Properties().Where(word => word.Name.StartsWith(ParameterWord, StringComparison.Ordinal) && !later.Contains(word.Name)).ToList();
            var values = new JObject();
            foreach (JProperty word in words)
            {
                string rule = SiteClusters.PyText(word.Value["rule"]);
                if (TableFactors.Contains(rule) && factor[rule] != null)
                {
                    JToken found = (word.Value["table"] as JObject)?[factor[rule]];
                    if (found != null && found.Type != JTokenType.Null)
                    {
                        values[word.Name.Substring(ParameterWord.Length)] = SiteClusters.PyText(found);
                    }
                }
            }
            var formulas = new Dictionary<string, Func<string>>
            {
                ["plot"] = () => plot, ["rev9Code"] = () => code, ["plotAndCode"] = () => plot + " | " + code,
                ["plotCodeStepsAndFacade"] = () => string.Join(" | ", new[] { plot, code, step ?? "None" }.Concat(facade.Select(name => (string)values[name] ?? ""))),
                ["ffl"] = () => ffl.HasValue ? PyMath.FixedText(ffl.Value, 3) : null, ["fflDownToTheCm"] = () => KeyLevels.PrototypeFfl("fflDownToTheCm", ffl),
            };
            foreach (JProperty word in words)
            {
                string rule = SiteClusters.PyText(word.Value["rule"]);
                if (formulas.TryGetValue(rule, out Func<string> formula))
                {
                    string found = formula();
                    if (found != null)
                    {
                        values[word.Name.Substring(ParameterWord.Length)] = found;
                    }
                }
            }
            List<string> lacking = words.Select(word => word.Name.Substring(ParameterWord.Length)).Where(name => values[name] == null).OrderBy(name => name, StringComparer.Ordinal).ToList();
            return Tuple.Create(values, lacking);
        }

        // plan_from_book.parameter_step: per plot of the scope the link parameters of step 13 the build gave it after the overrides (PlanOverrides),
        // their texts and sources, the names a rule gave no value and the link Name, which a build from the data book never writes (R107)
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IReadOnlyDictionary<string, PlotPlan> plans)
        {
            return numbers.ToDictionary(number => number, number => plans[number].Parameters);
        }
    }
}
