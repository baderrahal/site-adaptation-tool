using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;

namespace SiteAdaptation.Common.SiteBuild
{
    // R640, R641. The values of each plot of the plan. A value read from the plot table keeps the source stage 1 gave it. A number from a rule says the rule and its
    // status. Everything else has no rule and no read behind it, so it is missing with the reason, and the plot is marked. Nothing is copied from a past file
    internal static class PlanPlots
    {
        public static List<PlanPlot> Make(IEnumerable<PlotIn> rows, RuleNumbers numbers, List<PlanIssue> issues)
        {
            var plots = new List<PlanPlot>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlotIn row in rows)
            {
                if (!seen.Add(row.Number))
                {
                    issues.Add(new PlanIssue { Plot = row.Number, Line = PlanTexts.PlotTwice(row.Number) });
                    continue;
                }
                var plot = new PlanPlot { In = row };
                plot.Add("villaType", Read(row, "villaType", row.VillaType == null ? null : new JValue(row.VillaType), "villa type"));
                plot.Add("mirror", Read(row, "mirror", row.Mirror.HasValue ? new JValue(row.Mirror.Value) : null, "mirror"));
                plot.Add("area", Read(row, "area", row.Area == null ? null : new JValue(row.Area), "neighbourhood"));
                plot.Add("areaM2", Read(row, "areaM2", row.AreaM2.HasValue ? new JValue(row.AreaM2.Value) : null, "plot area"));
                plot.Add("outlinePoints", row.Outline == null ? PlanValue.Missing(PlanTexts.NoOutlineWhy) : PlanValue.Of(new JValue(row.Outline.Count), ProjectRulesFile.Computed));
                plot.Add("frontEdge", PlanValue.Missing(PlanTexts.FrontEdgeWhy));
                plot.Add("roadLevelAtFront", PlanValue.Missing(PlanTexts.RoadLevelWhy));
                plot.Add("ffl", PlanValue.Missing(PlanTexts.FflWhy));
                plot.Add("steps", FromRule(numbers, RuleNumbers.StepsRule, Pick(numbers.StepsByType, row.VillaType, null), "the number of steps"));
                plot.Add("frontSetbackM", FromRule(numbers, RuleNumbers.SetbackRule, Pick(numbers.SetbackByType, row.VillaType, numbers.Setback), "the front setback"));
                plot.Add("boundaryWallAboveFflM", FromRule(numbers, RuleNumbers.BoundaryWallRule, numbers.BoundaryWallTopM, "the top of the boundary wall"));
                plot.Add("boundaryWallThicknessM", FromRule(numbers, RuleNumbers.BoundaryWallThicknessRule, numbers.BoundaryWallThicknessM, "the thickness of the boundary wall"));
                plot.Add("gardenWallAboveFflM", FromRule(numbers, RuleNumbers.GardenWallRule, numbers.GardenWallTopM, "the top of the garden wall"));
                MarkDoubts(plot);
                // R705, Q504 answer 5A. A plot with no floor level is marked by its missing value and named in the issues, so the card counts them
                if (plot.Get("ffl").IsMissing)
                {
                    issues.Add(new PlanIssue { Plot = row.Number, Line = PlanTexts.NoRoadLevel(row.Number) });
                }
                plots.Add(plot);
            }
            return plots;
        }

        // A value stage 1 marked disputed or weak is not ready, the plot is marked with the words stage 1 gave, the value itself stays as read
        private static void MarkDoubts(PlanPlot plot)
        {
            foreach (KeyValuePair<string, JObject> kind in new[] { new KeyValuePair<string, JObject>("disputed", plot.In.Disputed), new KeyValuePair<string, JObject>("weak", plot.In.Weak) })
            {
                foreach (JProperty doubt in kind.Value.Properties())
                {
                    if (plot.Get(doubt.Name) != null && doubt.Value.Type == JTokenType.String)
                    {
                        plot.Marked.Add(doubt.Name + ": " + kind.Key + ", " + (string)doubt.Value);
                    }
                }
            }
        }

        // A value of the plot table keeps the source of stage 1. A value a person confirmed there is person entered, every other read is computed
        private static PlanValue Read(PlotIn row, string name, JToken value, string what)
        {
            if (value == null)
            {
                string why = row.MissingWhy[name]?.Type == JTokenType.String ? (string)row.MissingWhy[name] : PlanTexts.NoValueWhy(what);
                return PlanValue.Missing(why);
            }
            string tag = row.Source[name]?.Type == JTokenType.String ? (string)row.Source[name] : null;
            bool person = tag == ProjectRulesFile.PersonEntered || tag == "user pick" || tag == "confirmed";
            return PlanValue.Of(value, person ? ProjectRulesFile.PersonEntered : ProjectRulesFile.Computed);
        }

        private static PlanValue FromRule(RuleNumbers numbers, string rule, double? value, string what)
        {
            if (!value.HasValue)
            {
                return PlanValue.Missing(PlanTexts.NoRuleWhy(what));
            }
            string status, source;
            numbers.Status.TryGetValue(rule, out status);
            numbers.Sources.TryGetValue(rule, out source);
            if (source == null || source == ProjectRulesFile.FromRule)
            {
                return PlanValue.FromRule(new JValue(value.Value), rule, status);
            }
            // A person entered, default or past number keeps its own tag, with the rule it came from
            return new PlanValue { Value = new JValue(value.Value), Source = source, Rule = rule, Status = status };
        }

        private static double? Pick(Dictionary<string, double> byType, string type, double? otherwise)
        {
            double own;
            return type != null && byType.TryGetValue(type, out own) ? own : otherwise;
        }
    }

    // R640. The five steps of Build in order, each with how many plots are ready for it, and the one progress line
    internal static class PlanSteps
    {
        // A plot is ready for every step when none of its values is missing
        public static bool IsReady(PlanPlot plot)
        {
            return plot.Marked.Count == 0;
        }

        public static JArray Make(IReadOnlyList<PlanPlot> plots)
        {
            int total = plots.Count;
            int outlined = plots.Count(plot => plot.In.Outline != null);
            int leveled = plots.Count(plot => !plot.Get("ffl").IsMissing);
            int typed = plots.Count(plot => !plot.Get("ffl").IsMissing && !plot.Get("villaType").IsMissing);
            int walled = plots.Count(plot => !plot.Get("ffl").IsMissing && !plot.Get("boundaryWallAboveFflM").IsMissing && !plot.Get("boundaryWallThicknessM").IsMissing);
            var steps = new JArray();
            Add(steps, 4, "Check", plots.Count(plot => !plot.Get("villaType").IsMissing && plot.In.Outline != null), total);
            Add(steps, 5, "Floors and ramps", outlined, total);
            Add(steps, 6, "Levels", leveled, total);
            Add(steps, 7, "Place villas", typed, total);
            Add(steps, 8, "Walls", walled, total);
            return steps;
        }

        private static void Add(JArray steps, int button, string name, int ready, int total)
        {
            steps.Add(new JObject { ["button"] = button, ["name"] = name, ["ready"] = ready, ["total"] = total, ["line"] = PlanTexts.StepLine(button, name, ready, total) });
        }
    }
}
