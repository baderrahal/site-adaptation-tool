using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.BoundaryWalls
{
    // R380. Button 8 Walls as a body of the flow of R382. Every decision is made here and IWallDocument only carries it out: one wall of
    // the type BW per wall of the plan (R362) on its line, base and top, with the tag of R368, and the second run of SecondRun.Match.
    // The plan already holds the rules of the keys, the base at the yard level of the higher plot, the centre on the edge, no wall on the
    // front and one wall per shared edge, so the button places what the plan gives and decides no wall of its own
    public sealed class WallsRun
    {
        public const string RecordFormat = "site-adaptation-walls";
        public const string GardenLine = "Garden walls: none placed, they have no measured rule, the 28 of the IMC key are counted only (R362)";

        private readonly IWallDocument document;
        private readonly Func<RunContext, WallInputs> load;

        public WallsRun(IWallDocument document, Func<RunContext, WallInputs> load)
        {
            this.document = document;
            this.load = load;
        }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Apply = Apply };
        }

        // walls_<NBH>.json in the work folder, the record of the last real run of 8
        public static string RecordName(string nbh)
        {
            return "walls_" + nbh + ".json";
        }

        public static HashSet<string> Placed(JObject record)
        {
            return new HashSet<string>(((record?["placed"] as JArray) ?? new JArray()).Select(token => (string)token), StringComparer.Ordinal);
        }

        private enum Act
        {
            Place,
            Change,
            Keep,
            Skip,
        }

        private sealed class Step
        {
            public PlannedWall Wall;
            public Act Act;
            public MatchOutcome Outcome;
        }

        private sealed class Decision
        {
            public WallInputs Inputs;
            public WallTypeEntry Type;
            public LevelEntry Level;
            public readonly List<Step> Steps = new List<Step>();
            public readonly List<string> Listed = new List<string>();

            public int Count(Act act) => Steps.Count(step => step.Act == act);
        }

        private RunSummary Preview(RunContext context)
        {
            Decision decision = Decide(context);
            string counts = "Walls: place " + decision.Count(Act.Place) + ", change " + decision.Count(Act.Change) + ", already placed " + decision.Count(Act.Keep) + ", skip " + decision.Count(Act.Skip) + ", listed " + decision.Listed.Count;
            return new RunSummary(counts, decision.Steps.Select(step => What(decision, step)).Concat(decision.Listed).ToList());
        }

        // Every wall is made or set, the model regenerates once, then each is read back and tagged. Any failure throws and the flow rolls
        // the whole run back
        private IReadOnlyList<string> Apply(RunContext context)
        {
            Decision decision = Decide(context);
            WallInputs inputs = decision.Inputs;
            var made = new List<KeyValuePair<Step, string>>();
            var standing = new List<string>();
            foreach (Step step in decision.Steps)
            {
                PlannedWall wall = step.Wall;
                if (step.Act == Act.Keep)
                {
                    standing.Add(wall.Key);
                    continue;
                }
                if (step.Act == Act.Skip)
                {
                    continue;
                }
                double offset = wall.Base - decision.Level.ElevationM, height = wall.Top - wall.Base;
                string id;
                if (step.Act == Act.Change)
                {
                    id = step.Outcome.Element;
                    document.SetWall(id, wall.Start, wall.End, decision.Level.Key, offset, height);
                }
                else
                {
                    id = document.CreateWall(wall.Start, wall.End, decision.Type.Key, decision.Level.Key, offset, height);
                }
                made.Add(new KeyValuePair<Step, string>(step, id));
            }
            document.Regenerate();
            double tolerance = DesignValues.Current.WallReadBackM;
            foreach (KeyValuePair<Step, string> item in made)
            {
                Step step = item.Key;
                string mismatch = WallPlan.Mismatch(document.ReadBack(item.Value), step.Wall, tolerance);
                if (mismatch != null)
                {
                    throw new InvalidOperationException(step.Wall.Label + ": the wall does not match the plan, " + mismatch + " (R380)");
                }
                document.Tag(item.Value, step.Act == Act.Change ? step.Outcome.NextTag(context.RunId, inputs.Profile) : PlacedTag.ForPlacement(step.Wall.Item, context.RunId, inputs.Profile, item.Value));
                standing.Add(step.Wall.Key);
            }
            if (standing.Count == 0)
            {
                throw new InvalidOperationException("nothing of the scope is placed or already placed, so Walls is not done: " + string.Join(". ", decision.Listed.DefaultIfEmpty("the plan gives no wall")));
            }
            var record = new JObject
            {
                ["format"] = RecordFormat, ["runId"] = context.RunId, ["plan"] = inputs.PlanPath,
                ["placed"] = new JArray(standing.OrderBy(key => key, StringComparer.Ordinal)),
                ["counts"] = new JObject(new[] { Act.Place, Act.Change, Act.Keep, Act.Skip }.Select(act => new JProperty(act.ToString().ToLowerInvariant(), decision.Count(act)))),
            };
            context.WriteText(RecordName(inputs.Plan.Neighbourhood ?? "all"), record.ToString(Newtonsoft.Json.Formatting.Indented));
            return decision.Listed;
        }

        private Decision Decide(RunContext context)
        {
            WallInputs inputs = load(context) ?? throw new InvalidOperationException("Walls has no plan to read");
            var decision = new Decision { Inputs = inputs };
            double[] offset = Check(inputs, decision);
            List<PlannedWall> planned = Plan(inputs, offset);
            double tolerance = DesignValues.Current.WallReadBackM;
            var regex = new Regex(inputs.BoundaryType);
            var found = new List<FoundElement>();
            var byHand = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (WallElement element in document.Walls() ?? new List<WallElement>())
            {
                if (element.Tag != null)
                {
                    if (element.Tag.Key.Kind == PlacedKinds.BoundaryWall)
                    {
                        found.Add(FoundElement.Tagged(element.UniqueId, element.Tag, WallPlan.Current(element.State)));
                    }
                    continue;
                }
                if (element.TypeName == null || !regex.IsMatch(element.TypeName))
                {
                    continue;
                }
                // SecondRun.Match finds an element with no tag by plot and kind alone, which would skip every wall of the plot, so a wall
                // with no tag holds back only the plan wall whose line it lies on
                PlannedWall on = element.State?.Start == null || element.State.End == null ? null : planned.FirstOrDefault(wall => OnLine(element.State, wall, tolerance));
                if (on == null)
                {
                    decision.Listed.Add("element " + element.UniqueId + " of type " + element.TypeName + " has no tag and lies on no wall of the plan, left as it is");
                    continue;
                }
                if (!byHand.TryGetValue(on.Key, out List<string> ids))
                {
                    byHand[on.Key] = ids = new List<string>();
                }
                ids.Add(element.UniqueId);
            }
            MatchResult match = SecondRun.Match(planned.Select(wall => wall.Item), found, inputs.SetToPlan);
            Dictionary<TagKey, PlannedWall> byKey = planned.ToDictionary(wall => wall.Item.Key);
            foreach (MatchOutcome outcome in match.Outcomes)
            {
                PlannedWall wall = byKey[outcome.Item.Key];
                if (!wall.InScope)
                {
                    continue;
                }
                var step = new Step { Wall = wall, Outcome = outcome };
                if (outcome.Action == MatchAction.Place && byHand.TryGetValue(wall.Key, out List<string> hand))
                {
                    step.Act = Act.Skip;
                    decision.Listed.Add(wall.Label + ": already placed, not by this tool, element " + string.Join(", ", hand) + " left and the wall skipped");
                }
                else if (outcome.Action == MatchAction.Place)
                {
                    step.Act = Act.Place;
                }
                else if (outcome.Action == MatchAction.Update)
                {
                    step.Act = Act.Change;
                }
                else
                {
                    step.Act = outcome.Case == MatchCase.AlreadyPlaced ? Act.Keep : Act.Skip;
                    if (outcome.Case == MatchCase.ChangedByHand)
                    {
                        decision.Listed.Add(wall.Label + ": changed by hand in " + string.Join(", ", outcome.Differences) + ", left as it is");
                    }
                }
                decision.Listed.AddRange(outcome.Issues);
                decision.Steps.Add(step);
            }
            decision.Listed.AddRange(match.Unplanned.Select(id => "element " + id + " is tagged for no wall of this plan, left as it is"));
            // 03b 8 Walls: the top rule holds on 0.346 of the key walls, so every wall that stands or will stand names it until Q071
            decision.Listed.AddRange(decision.Steps.Where(step => step.Act != Act.Skip)
                .Select(step => step.Wall.Label + ": its top is the FFL of plot " + step.Wall.Wall.TopPlot + " plus the Rev 6 height, a weak rule, an issue until Q071"));
            decision.Listed.Add(GardenLine);
            return decision;
        }

        // The checks before anything changes, each stop named: the plan format, the coordinates of R83, the wall type of the pattern of
        // the type BW and the yard level of floorSettings, the level the yard floors stand on, as the base level of every wall
        private double[] Check(WallInputs inputs, Decision decision)
        {
            PlanFile plan = inputs.Plan ?? throw new InvalidDataException("Walls has no plan to read");
            string format = Placer.CheckFormat(plan);
            // The prototype folder is the business of 7 Place villas, a wall needs none
            if (format != null && !format.Contains("prototypeFolder"))
            {
                throw new InvalidDataException(format);
            }
            SurveyReading survey = document.Survey();
            CoordinateCheck coordinates = Placer.CheckCoordinates(plan, survey);
            if (!coordinates.Match)
            {
                throw new InvalidDataException(coordinates.Message ?? "the plan or the document gives no survey point, nothing was changed (R83)");
            }
            var stops = new List<string>();
            if (string.IsNullOrEmpty(inputs.BoundaryType))
            {
                stops.Add("the config gives no pattern of the wall type BW");
            }
            else
            {
                var regex = new Regex(inputs.BoundaryType);
                decision.Type = (document.WallTypes() ?? new List<WallTypeEntry>()).Where(type => type?.Name != null && regex.IsMatch(type.Name)).OrderBy(type => type.Name, StringComparer.Ordinal).FirstOrDefault();
                if (decision.Type == null)
                {
                    stops.Add("the document has no wall type of " + inputs.BoundaryType + ", button 1 makes it");
                }
            }
            string yard = null;
            if (plan.FloorSettings?.Levels == null || !plan.FloorSettings.Levels.TryGetValue("yard", out yard) || string.IsNullOrEmpty(yard))
            {
                stops.Add("floorSettings.levels of the plan names no yard level, the base level of a wall");
            }
            else
            {
                decision.Level = (document.Levels() ?? new List<LevelEntry>()).FirstOrDefault(level => level?.Name == yard);
                if (decision.Level == null)
                {
                    stops.Add("the document has no level " + yard);
                }
            }
            if (stops.Count > 0)
            {
                throw new InvalidDataException("Stopped before any wall was changed: " + string.Join(". ", stops) + " (R380).");
            }
            return SharedCoordinates.Offset(survey.Position, survey.SharedPosition);
        }

        // Every wall of the plan with its ends and levels internal, in the scope when any plot it stands on is in the scope
        private static List<PlannedWall> Plan(WallInputs inputs, double[] offset)
        {
            List<PlanWallItem> walls = (inputs.Walls ?? new List<PlanWallItem>()).ToList();
            List<string> parts = WallPlan.Parts(walls);
            var planned = new List<PlannedWall>();
            for (int index = 0; index < walls.Count; index++)
            {
                PlanWallItem wall = walls[index];
                double[] start = { wall.Ends[0][0] - offset[0], wall.Ends[0][1] - offset[1] }, end = { wall.Ends[1][0] - offset[0], wall.Ends[1][1] - offset[1] };
                double bottom = wall.Base - offset[2], top = wall.Top - offset[2];
                bool scoped = inputs.InScope == null || wall.Plots.Any(inputs.InScope);
                var key = new TagKey(wall.Plot, PlacedKinds.BoundaryWall, parts[index]);
                planned.Add(new PlannedWall
                {
                    Wall = wall, Start = start, End = end, Base = bottom, Top = top, InScope = scoped,
                    Item = new PlanItem(key, WallPlan.Values(start, end, bottom, top), scoped, inputs.LastPlaced != null && inputs.LastPlaced.Contains(WallPlan.KeyOf(wall.Plot, parts[index]))),
                });
            }
            return planned;
        }

        // A wall lies on a plan wall when its two ends lie on the two ends of the plan wall in either order
        private static bool OnLine(WallReadBack state, PlannedWall wall, double tolerance)
        {
            return (WallPlan.Flat(state.Start, wall.Start) <= tolerance && WallPlan.Flat(state.End, wall.End) <= tolerance)
                || (WallPlan.Flat(state.Start, wall.End) <= tolerance && WallPlan.Flat(state.End, wall.Start) <= tolerance);
        }

        private static string What(Decision decision, Step step)
        {
            PlannedWall wall = step.Wall;
            switch (step.Act)
            {
                case Act.Place:
                    return wall.Label + ": place " + decision.Type.Name + " on " + decision.Level.Name + ", base offset " + Text(wall.Base - decision.Level.ElevationM) + " m, height " + Text(wall.Top - wall.Base) + " m";
                case Act.Change:
                    return wall.Label + ": change element " + step.Outcome.Element + " in place, " + string.Join(", ", step.Outcome.Differences) + " to the plan";
                case Act.Keep:
                    return wall.Label + ": already placed";
                default:
                    return wall.Label + ": skipped, " + step.Outcome.Case;
            }
        }

        private static string Text(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
