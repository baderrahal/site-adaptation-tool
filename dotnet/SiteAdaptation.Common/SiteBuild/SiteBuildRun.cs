using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;

namespace SiteAdaptation.Common.SiteBuild
{
    // R640. The command siteBuild, the logic behind the one Build button and the 2D preview. It reads the plot table of stage 1, the project profile, the config,
    // the rules of the project and, when given, a position file, placed floors, masses and road edge points. It writes build_plan.json and preview.json and
    // touches no model. Only a blocking line stops the build, and that is a line of the plan and not a failure of the run, so the worker ends well
    internal static class SiteBuildRun
    {
        public const string PlanName = "build_plan.json";
        public const string PreviewName = "preview.json";
        private const double DefaultOverlapMm = 1.0;

        public static void Run(WorkerContext run)
        {
            string tablePath = run.Input("plotTable");
            string profilePath = run.Input("projectProfile");
            JObject config = ProjectRulesFile.Config(run);
            var issues = new List<PlanIssue>();
            var blocking = new List<string>();

            string fault;
            List<PlotIn> rows = PlotTable.Read(tablePath, issues, out fault) ?? new List<PlotIn>();
            if (fault != null)
            {
                blocking.Add(fault);
            }
            else if (rows.Count == 0)
            {
                blocking.Add(PlanTexts.NoPlots);
            }
            else if (rows.All(row => row.Outline == null))
            {
                blocking.Add(PlanTexts.NoOutlines);
            }

            RulesForBuildAnswer rules = AskRules(run, profilePath, config);
            if (rules.BlockingLine != null)
            {
                blocking.Add(rules.BlockingLine);
            }
            issues.AddRange(rules.Issues.Select(line => new PlanIssue { Line = line }));

            RuleNumbers numbers = RuleNumbers.From(rules.Rules, issues);
            List<PlanPlot> plots = PlanPlots.Make(rows, numbers, issues);

            JObject coordinates = SiteCoordinates.Build(config, plots, Optional(run, "positionFile"), issues);
            double[] origin = Origin(plots);
            JObject checks = origin == null ? new JObject() : Checks(run, plots, config, origin, issues);

            var plan = new JObject
            {
                ["blocking"] = new JArray(blocking.Select(line => new JObject { ["line"] = line })),
                ["issues"] = new JArray(issues.Select(issue => issue.ToJson())),
                ["rulesChoice"] = rules.RulesChoiceLine == null ? JValue.CreateNull() : new JObject { ["line"] = rules.RulesChoiceLine },
                ["projectMode"] = run.Request.ProjectMode == null ? JValue.CreateNull() : new JValue(run.Request.ProjectMode),
                ["coordinates"] = coordinates,
                ["steps"] = PlanSteps.Make(plots),
                ["progress"] = blocking.Count > 0 ? PlanTexts.BlockedProgress(blocking[0]) : PlanTexts.Progress(plots.Count, plots.Count(PlanSteps.IsReady), plots.Count(plot => plot.Marked.Count > 0), issues.Count),
                ["floorsToReplace"] = checks["floorsToReplace"] ?? JValue.CreateNull(),
                ["toposolidCut"] = checks["toposolidCut"] ?? JValue.CreateNull(),
                ["overlap"] = checks["overlap"] ?? JValue.CreateNull(),
                ["plots"] = new JArray(plots.Select(plot => plot.ToJson())),
            };
            var preview = new JObject
            {
                ["plots"] = new JArray(plots.Where(plot => plot.In.Outline != null).Select(plot => new JObject
                {
                    ["number"] = plot.Number,
                    ["outline"] = new JArray(Closed(plot.In.Outline).Select(point => new JArray(point[0], point[1]))),
                    ["villaType"] = plot.In.VillaType == null ? JValue.CreateNull() : new JValue(plot.In.VillaType),
                })),
                ["issues"] = issues.Count,
            };
            run.WriteJson(run.Request.OutputFolder, PlanName, plan);
            run.WriteJson(run.Request.OutputFolder, PreviewName, preview);
            run.Counts["plots"] = plots.Count;
            run.Counts["marked"] = plots.Count(plot => plot.Marked.Count > 0);
            run.Counts["blocking"] = blocking.Count;
            run.Counts["issues"] = issues.Count;
        }

        // The rules file of the request, else the one beside the profile. Null asks for a choice even when the project has rules, so it is looked for first
        private static RulesForBuildAnswer AskRules(WorkerContext run, string profilePath, JObject config)
        {
            string path = Optional(run, "rules");
            if (path == null)
            {
                string beside = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(profilePath)) ?? "", ProjectRulesFile.FileName);
                path = File.Exists(beside) ? beside : null;
            }
            if (path != null)
            {
                InputMark before;
                string refusal = run.OpenFile("rules", path, out before);
                if (refusal != null)
                {
                    var answer = new RulesForBuildAnswer { BlockingLine = "The rules file of this project cannot be opened, so Build stops: " + refusal };
                    return answer;
                }
            }
            return RulesForBuild.Check(path, config, run.Request.ProjectMode);
        }

        private static string Optional(WorkerContext run, string role)
        {
            return run.Request.Inputs.Any(item => item.Role == role) ? run.Input(role) : null;
        }

        private static JObject OptionalJson(WorkerContext run, string role, List<PlanIssue> issues)
        {
            string path = Optional(run, role);
            if (path == null)
            {
                return null;
            }
            try
            {
                return JObject.Parse(WorkFiles.ReadText(path));
            }
            catch (Exception failure) when (failure is Newtonsoft.Json.JsonException || failure is IOException)
            {
                issues.Add(new PlanIssue { Line = PlanTexts.FileUnreadable(role, failure.Message) });
                return null;
            }
        }

        // The south west corner of all outlines, the one point every clipping counts its millimetres from
        private static double[] Origin(List<PlanPlot> plots)
        {
            List<double[]> all = plots.Where(plot => plot.In.Outline != null).SelectMany(plot => plot.In.Outline).ToList();
            if (all.Count == 0)
            {
                return null;
            }
            double[] box = Rings.Box(all);
            return new[] { Math.Floor(box[0]), Math.Floor(box[2]) };
        }

        private static JObject Checks(WorkerContext run, List<PlanPlot> plots, JObject config, double[] origin, List<PlanIssue> issues)
        {
            var found = new JObject();
            double? same = Number(config, "designValues", "samePoint", "sameMm");
            double? margin = Number(config, "floors", "roadTopography", "marginM");
            double? edge = Number(config, "floors", "roadTopography", "limitMm");
            double? given = Number(config, "siteBuild", "massOverlapMm");
            // A limit below 0 would grow the outlines and flag every shared edge, so it counts as not given
            bool fromConfig = given.HasValue && given.Value >= 0;
            double overlap = fromConfig ? given.Value : DefaultOverlapMm;
            if (!fromConfig)
            {
                // Source default, said in a line and in the plan, never a quiet guess
                issues.Add(new PlanIssue { Line = given.HasValue ? "The overlap limit of the config is below 0, so the default of " + PlanTexts.Mm(DefaultOverlapMm) + " mm is used." : "The config holds no overlap limit, so the default of " + PlanTexts.Mm(DefaultOverlapMm) + " mm is used." });
            }
            if (same.HasValue)
            {
                found["floorsToReplace"] = FloorsToReplace.Check(plots, OptionalJson(run, "placedFloors", issues), same.Value, issues);
            }
            else
            {
                issues.Add(new PlanIssue { Line = "The config holds no distance for two points to count as the same, so the floors placed earlier are not compared." });
            }
            if (margin.HasValue && edge.HasValue)
            {
                found["toposolidCut"] = NeighbourhoodCut.Build(plots, OptionalJson(run, "roadEdge", issues), margin.Value, edge.Value, origin, issues);
            }
            else
            {
                issues.Add(new PlanIssue { Line = "The config holds no margin or no limit for the toposolid cut, so the cut to the neighbourhood is not made." });
            }
            found["overlap"] = MassOverlap.Check(plots, OptionalJson(run, "masses", issues), overlap, origin, issues);
            found["overlap"]["limitSource"] = fromConfig ? "config" : "default";
            return found;
        }

        private static double? Number(JObject config, params string[] path)
        {
            JToken at = config;
            foreach (string key in path)
            {
                at = (at as JObject)?[key];
            }
            return at != null && (at.Type == JTokenType.Float || at.Type == JTokenType.Integer) ? (double?)at : null;
        }

        private static List<double[]> Closed(List<double[]> ring)
        {
            var points = ring.ToList();
            double[] first = points[0], last = points[points.Count - 1];
            if (first[0] != last[0] || first[1] != last[1])
            {
                points.Add(first);
            }
            return points;
        }
    }

    // R640. Registers the command with the worker
    public static class SiteBuildCommands
    {
        public static void Add(IDictionary<string, WorkerCommand> commands)
        {
            commands["siteBuild"] = SiteBuildRun.Run;
        }
    }
}
