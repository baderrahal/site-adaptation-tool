using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;

namespace SiteAdaptation.Common.SiteBuild
{
    // R641. The numbers a build takes from the text of a rule of the manual template. Only a text that states the number in the plain form below gives one,
    // and then the source is rule with the id of that rule and the status of the rule goes with it, so a rule a person has not confirmed shows as such. A text
    // in any other form gives no number and is said in an issue, the build never reads a number out of words it does not know. Forms:
    //   M10  "<unit types> always have <n> step(s)"
    //   M13  "Front setback <n> m" and zero or more ", <unit type> <n> m" for a unit type with a setback of its own
    //   M11, M12  "FFL + <n> m"
    internal sealed class RuleNumbers
    {
        public const string StepsRule = "M10";
        public const string SetbackRule = "M13";
        public const string BoundaryWallRule = "M11";
        public const string GardenWallRule = "M12";

        private static readonly Regex StepsForm = new Regex(@"^(?<types>.+?)\s+always have\s+(?<n>\d+)\s+steps?\b", RegexOptions.CultureInvariant);
        private static readonly Regex SetbackDefault = new Regex(@"Front setback\s+(?<n>\d+(?:\.\d+)?)\s*m\b", RegexOptions.CultureInvariant);
        private static readonly Regex SetbackOwn = new Regex(@",\s*(?<type>[A-Z]{1,3}\d{1,2})\s+(?<n>\d+(?:\.\d+)?)\s*m\b", RegexOptions.CultureInvariant);
        private static readonly Regex AboveFfl = new Regex(@"FFL\s*\+\s*(?<n>\d+(?:\.\d+)?)\s*m\b", RegexOptions.CultureInvariant);

        public Dictionary<string, double> StepsByType { get; } = new Dictionary<string, double>(StringComparer.Ordinal);
        public double? Setback { get; private set; }
        public Dictionary<string, double> SetbackByType { get; } = new Dictionary<string, double>(StringComparer.Ordinal);
        public double? BoundaryWallTopM { get; private set; }
        public double? GardenWallTopM { get; private set; }

        // Rule id to the status of the rule, for the value that takes a number from it
        public Dictionary<string, string> Status { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        // Rules the template holds that the build reads no number from, said once in an issue so a person knows which rules are not used yet
        public List<string> NotUsed { get; } = new List<string>();

        public static RuleNumbers From(IEnumerable<RuleForBuild> rules, List<PlanIssue> issues)
        {
            var numbers = new RuleNumbers();
            foreach (RuleForBuild rule in rules)
            {
                numbers.Status[rule.Id] = rule.Status;
                string text = rule.Value?.Type == JTokenType.String ? (string)rule.Value : null;
                bool given = rule.Source != ProjectRulesFile.Missing && text != null;
                switch (rule.Id)
                {
                    case StepsRule:
                        if (given && !numbers.ReadSteps(text))
                        {
                            issues.Add(new PlanIssue { Line = PlanTexts.RuleTextUnreadable(rule.Id, text) });
                        }
                        break;
                    case SetbackRule:
                        if (given && !numbers.ReadSetback(text))
                        {
                            issues.Add(new PlanIssue { Line = PlanTexts.RuleTextUnreadable(rule.Id, text) });
                        }
                        break;
                    case BoundaryWallRule:
                        numbers.BoundaryWallTopM = given ? Above(text) : null;
                        if (given && !numbers.BoundaryWallTopM.HasValue)
                        {
                            issues.Add(new PlanIssue { Line = PlanTexts.RuleTextUnreadable(rule.Id, text) });
                        }
                        break;
                    case GardenWallRule:
                        numbers.GardenWallTopM = given ? Above(text) : null;
                        if (given && !numbers.GardenWallTopM.HasValue)
                        {
                            issues.Add(new PlanIssue { Line = PlanTexts.RuleTextUnreadable(rule.Id, text) });
                        }
                        break;
                    default:
                        numbers.NotUsed.Add(rule.Id);
                        break;
                }
            }
            return numbers;
        }

        private bool ReadSteps(string text)
        {
            Match found = StepsForm.Match(text);
            if (!found.Success)
            {
                return false;
            }
            double steps = double.Parse(found.Groups["n"].Value, CultureInfo.InvariantCulture);
            string[] types = Regex.Split(found.Groups["types"].Value, @",|\band\b").Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
            if (types.Length == 0)
            {
                return false;
            }
            foreach (string type in types)
            {
                StepsByType[type] = steps;
            }
            return true;
        }

        private bool ReadSetback(string text)
        {
            Match found = SetbackDefault.Match(text);
            if (!found.Success)
            {
                return false;
            }
            Setback = double.Parse(found.Groups["n"].Value, CultureInfo.InvariantCulture);
            foreach (Match own in SetbackOwn.Matches(text))
            {
                SetbackByType[own.Groups["type"].Value] = double.Parse(own.Groups["n"].Value, CultureInfo.InvariantCulture);
            }
            return true;
        }

        private static double? Above(string text)
        {
            Match found = AboveFfl.Match(text);
            return found.Success ? double.Parse(found.Groups["n"].Value, CultureInfo.InvariantCulture) : (double?)null;
        }
    }
}
