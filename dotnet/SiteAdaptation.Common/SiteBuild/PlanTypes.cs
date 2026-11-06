using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;

namespace SiteAdaptation.Common.SiteBuild
{
    // R640. One value of the plan and where it comes from. Source is computed, rule with its id, person entered, default or missing. A missing value is
    // null and says why, the tool never fills a value that no rule and no read gives
    internal sealed class PlanValue
    {
        public JToken Value { get; set; }
        public string Source { get; set; }
        public string Rule { get; set; }
        public string Why { get; set; }
        public string Status { get; set; }

        public static PlanValue Of(JToken value, string source)
        {
            return new PlanValue { Value = value, Source = source };
        }

        public static PlanValue FromRule(JToken value, string rule, string status)
        {
            return new PlanValue { Value = value, Source = ProjectRulesFile.FromRule, Rule = rule, Status = status };
        }

        public static PlanValue Missing(string why)
        {
            return new PlanValue { Value = null, Source = ProjectRulesFile.Missing, Why = why };
        }

        public bool IsMissing
        {
            get { return Source == ProjectRulesFile.Missing; }
        }

        public JObject ToJson()
        {
            var json = new JObject { ["value"] = Value ?? JValue.CreateNull(), ["source"] = Source };
            if (Rule != null)
            {
                json["rule"] = Rule;
            }
            if (Status != null)
            {
                json["status"] = Status;
            }
            if (Why != null)
            {
                json["why"] = Why;
            }
            return json;
        }
    }

    // R640. An issue of the plan, with the plot it is about or null for the site. Only a blocking line stops a build, an issue never does
    internal sealed class PlanIssue
    {
        public string Plot { get; set; }
        public string Line { get; set; }

        public JObject ToJson()
        {
            return new JObject { ["plot"] = Plot == null ? JValue.CreateNull() : new JValue(Plot), ["line"] = Line };
        }
    }

    // R640. One plot as stage 1 gave it, outline in drawing metres. Outline is null when the table holds none for the plot
    internal sealed class PlotIn
    {
        public string Number { get; set; }
        public string VillaType { get; set; }
        public bool? Mirror { get; set; }
        public string Area { get; set; }
        public double? AreaM2 { get; set; }
        public List<string> Groups { get; set; } = new List<string>();
        public List<double[]> Outline { get; set; }
        public JObject Source { get; set; } = new JObject();
        public JObject MissingWhy { get; set; } = new JObject();
        public JObject Disputed { get; set; } = new JObject();
        public JObject Weak { get; set; } = new JObject();
    }

    // R640. One plot of the plan: its values in a fixed order, each with its source, and why the plot is marked
    internal sealed class PlanPlot
    {
        public PlotIn In { get; set; }
        public List<KeyValuePair<string, PlanValue>> Values { get; } = new List<KeyValuePair<string, PlanValue>>();
        public List<string> Marked { get; } = new List<string>();

        public string Number
        {
            get { return In.Number; }
        }

        public void Add(string name, PlanValue value)
        {
            Values.Add(new KeyValuePair<string, PlanValue>(name, value));
            if (value.IsMissing)
            {
                Marked.Add(name + ": " + value.Why);
            }
        }

        public PlanValue Get(string name)
        {
            foreach (KeyValuePair<string, PlanValue> value in Values)
            {
                if (value.Key == name)
                {
                    return value.Value;
                }
            }
            return null;
        }

        public JObject ToJson()
        {
            var values = new JObject();
            foreach (KeyValuePair<string, PlanValue> value in Values)
            {
                values[value.Key] = value.Value.ToJson();
            }
            return new JObject { ["number"] = Number, ["area"] = In.Area == null ? JValue.CreateNull() : new JValue(In.Area), ["values"] = values, ["marked"] = new JArray(Marked) };
        }
    }
}
