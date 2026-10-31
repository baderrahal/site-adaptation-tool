using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.Floors
{
    // R378. Button 5 Floors and ramps as a body of the flow of R382. Every decision is made here and IFloorDocument only carries it out:
    // per plot the Generic floor on the whole face, the yard floor and the ramp of the plan with its corners by slab shape editing (R113
    // to R116), each with the plot value of R114 and the tag of R368, and the second run of SecondRun.Match
    public sealed class FloorsRun
    {
        public const string RecordFormat = "site-adaptation-floors";

        private readonly IFloorDocument document;
        private readonly Func<RunContext, FloorInputs> load;

        public FloorsRun(IFloorDocument document, Func<RunContext, FloorInputs> load)
        {
            this.document = document;
            this.load = load;
        }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Apply = Apply };
        }

        // floors_<NBH>.json in the work folder, the record of the last real run of 5
        public static string RecordName(string nbh)
        {
            return "floors_" + nbh + ".json";
        }

        public static HashSet<string> Placed(JObject record)
        {
            return new HashSet<string>(((record?["placed"] as JArray) ?? new JArray()).Select(token => (string)token), StringComparer.Ordinal);
        }

        private enum Act
        {
            Place,
            Move,
            Keep,
            Skip,
        }

        private sealed class Step
        {
            public PlannedFloor Floor;
            public Act Act;
            public MatchOutcome Outcome;
        }

        private sealed class Decision
        {
            public FloorInputs Inputs;
            public FloorPlanView View;
            public readonly List<Step> Steps = new List<Step>();
            public readonly List<string> Listed = new List<string>();

            public int Count(Act act) => Steps.Count(step => step.Act == act);
        }

        private RunSummary Preview(RunContext context)
        {
            Decision decision = Decide(context);
            string counts = "Floors: place " + decision.Count(Act.Place) + ", move " + decision.Count(Act.Move) + ", already placed " + decision.Count(Act.Keep) + ", skip " + decision.Count(Act.Skip) + ", listed " + decision.Listed.Count;
            return new RunSummary(counts, decision.Steps.Select(What).Concat(decision.Listed).ToList());
        }

        // R116. Every floor is made with its height offset and plot value, then the ramps are shaped, then all are read back, so the model
        // regenerates twice and not once per floor. Any failure throws and the flow rolls the whole run back
        private IReadOnlyList<string> Apply(RunContext context)
        {
            Decision decision = Decide(context);
            FloorInputs inputs = decision.Inputs;
            var issues = new List<string>(decision.Listed);
            var ids = new List<KeyValuePair<Step, string>>();
            var standing = new List<string>();
            foreach (Step step in decision.Steps)
            {
                FloorPlacer.Candidate candidate = step.Floor.Candidate;
                if (step.Act == Act.Keep)
                {
                    standing.Add(step.Floor.Key);
                    continue;
                }
                if (step.Act == Act.Skip)
                {
                    continue;
                }
                string id;
                if (step.Act == Act.Move)
                {
                    id = step.Outcome.Element;
                    document.SetHeightOffset(id, candidate.HeightOffset);
                }
                else
                {
                    id = document.CreateFloor(candidate.Loops, candidate.Type.Key, candidate.Level.Key);
                    document.SetHeightOffset(id, candidate.HeightOffset);
                    // R114. Without its plot value a later run could not tell the floor is placed and would place it twice
                    ParameterWrite write = document.WriteText(id, decision.View.PlotParameter, candidate.Floor.PlotValue.Value.Trim());
                    if (write != ParameterWrite.Written)
                    {
                        throw new InvalidOperationException(step.Floor.Label + ": " + decision.View.PlotParameter + " was not written, " + write);
                    }
                }
                ids.Add(new KeyValuePair<Step, string>(step, id));
            }
            document.Regenerate();
            foreach (KeyValuePair<Step, string> made in ids.Where(made => made.Key.Floor.IsRamp))
            {
                FloorPlan.Shape(document, made.Value, made.Key.Floor);
            }
            document.Regenerate();
            foreach (KeyValuePair<Step, string> made in ids)
            {
                Step step = made.Key;
                string mismatch = FloorPlan.Mismatch(document, made.Value, step.Floor);
                if (mismatch != null)
                {
                    throw new InvalidOperationException(step.Floor.Label + ": the floor does not match the plan, " + mismatch);
                }
                document.Tag(made.Value, step.Act == Act.Move ? step.Outcome.NextTag(context.RunId, inputs.Profile) : PlacedTag.ForPlacement(step.Floor.Item, context.RunId, inputs.Profile, made.Value));
                standing.Add(step.Floor.Key);
            }
            if (standing.Count == 0)
            {
                throw new InvalidOperationException("nothing of the scope is placed or already placed, so Floors and ramps is not done: " + string.Join(". ", issues.DefaultIfEmpty("the plan gives no floor")));
            }
            var record = new JObject
            {
                ["format"] = RecordFormat, ["runId"] = context.RunId, ["plan"] = inputs.PlanPath,
                ["placed"] = new JArray(standing.OrderBy(key => key, StringComparer.Ordinal)),
                ["counts"] = new JObject(new[] { Act.Place, Act.Move, Act.Keep, Act.Skip }.Select(act => new JProperty(act.ToString().ToLowerInvariant(), decision.Count(act)))),
            };
            context.WriteText(RecordName(inputs.Plan.Neighbourhood ?? "all"), record.ToString(Newtonsoft.Json.Formatting.Indented));
            return issues;
        }

        private Decision Decide(RunContext context)
        {
            FloorInputs inputs = load(context) ?? throw new InvalidOperationException("Floors and ramps has no plan to read");
            var decision = new Decision { Inputs = inputs, View = FloorPlan.Read(document, inputs.Plan, inputs.InScope, inputs.LastPlaced) };
            FloorPlanView view = decision.View;
            decision.Listed.AddRange(view.Listed);
            var found = new List<FoundElement>();
            foreach (FloorElement element in view.Elements)
            {
                string kind = view.KindOf(element);
                if (element.Tag != null && kind != null)
                {
                    found.Add(FoundElement.Tagged(element.UniqueId, element.Tag, FloorPlan.Current(element, kind)));
                }
                else if (element.Tag == null && kind != null && view.PlotOf(element) is string plot)
                {
                    found.Add(FoundElement.Untagged(element.UniqueId, plot, kind));
                }
            }
            MatchResult match = SecondRun.Match(view.Floors.Select(floor => floor.Item), found, inputs.SetToPlan);
            Dictionary<TagKey, PlannedFloor> byKey = view.Floors.ToDictionary(floor => floor.Item.Key);
            foreach (MatchOutcome outcome in match.Outcomes)
            {
                PlannedFloor floor = byKey[outcome.Item.Key];
                if (!floor.InScope)
                {
                    continue;
                }
                var step = new Step { Floor = floor, Outcome = outcome };
                if (outcome.Action == MatchAction.Place)
                {
                    step.Act = Act.Place;
                }
                else if (outcome.Action == MatchAction.Update && !outcome.Differences.Contains(FloorPlan.Outline))
                {
                    step.Act = Act.Move;
                }
                else if (outcome.Action == MatchAction.Update)
                {
                    // SketchEditScope opens a transaction of its own and cannot run inside the one of the flow, and the design gives no
                    // other way, so an outline the plan changed is listed and left until the question of 5d is answered
                    step.Act = Act.Skip;
                    decision.Listed.Add(floor.Label + ": element " + outcome.Element + ", its outline changed in the plan, left as it is, an outline is not changed in place yet (R378)");
                }
                else
                {
                    step.Act = outcome.Case == MatchCase.AlreadyPlaced ? Act.Keep : Act.Skip;
                    if (outcome.Case == MatchCase.ChangedByHand)
                    {
                        decision.Listed.Add(floor.Label + ": changed by hand in " + string.Join(", ", outcome.Differences) + ", left as it is");
                    }
                }
                decision.Listed.AddRange(outcome.Issues);
                decision.Steps.Add(step);
            }
            decision.Listed.AddRange(match.Unplanned.Select(id => "element " + id + " is tagged for no floor of this plan, left as it is"));
            return decision;
        }

        private static string What(Step step)
        {
            FloorPlacer.Candidate candidate = step.Floor.Candidate;
            string place = "place " + candidate.Type.Name + " on " + candidate.Level.Name + ", height offset " + FloorPlan.Text(candidate.HeightOffset) + " m"
                + (step.Floor.IsRamp ? ", " + candidate.CornersInternal.Count + " corners by slab shape editing" : "");
            switch (step.Act)
            {
                case Act.Place:
                    return step.Floor.Label + ": " + place;
                case Act.Move:
                    return step.Floor.Label + ": change element " + step.Outcome.Element + " in place, " + string.Join(", ", step.Outcome.Differences) + " to the plan";
                case Act.Keep:
                    return step.Floor.Label + ": already placed";
                default:
                    return step.Floor.Label + ": skipped, " + step.Outcome.Case;
            }
        }
    }
}
