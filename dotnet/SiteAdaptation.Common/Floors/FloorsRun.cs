using System;
using System.Collections.Generic;
using System.IO;
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

        // R459. Set by Apply when the transaction took the list, for the file only the commit can fill
        private string acceptedNbh;
        private HashSet<string> standingBefore;

        // R572. The surface of the last read of this run, so the preview and the transaction read the file once
        private RoadSurfaceRead surfaceRead;

        // R572. Set by Apply when this press makes or keeps the toposolid, for the step after the commit
        private Decision toposolidDecision;

        public FloorsRun(IFloorDocument document, Func<RunContext, FloorInputs> load)
        {
            this.document = document;
            this.load = load;
        }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Apply = Apply, AfterCommit = AfterCommit };
        }

        // floors_<NBH>.json in the work folder, the record of the last real run of 5
        public static string RecordName(string nbh)
        {
            return "floors_" + nbh + ".json";
        }

        // R459. floors_<NBH>_accepted.json in the work folder, what step 5 accepted so far
        public static string AcceptedName(string nbh)
        {
            return "floors_" + nbh + "_accepted.json";
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
            List<string> lines = decision.Steps.Select(What).Concat(decision.Listed).ToList();
            lines.AddRange(PreviewToposolid(context, decision));
            return new RunSummary(counts, lines);
        }

        // R116. Every floor is made with its height offset and plot value, then the ramps are shaped, then all are read back, so the model
        // regenerates twice and not once per floor. Any failure throws and the flow rolls the whole run back
        private IReadOnlyList<string> Apply(RunContext context)
        {
            Decision decision = Decide(context);
            FloorInputs inputs = decision.Inputs;
            FloorPlanView plan = decision.View;
            KnownWarningList known = inputs.KnownWarnings?.WithPlotNumbers((role, value) => plan.PlotOfValue.TryGetValue(role + "|" + value, out string number) ? number : null);
            if (context.AcceptKnownWarnings(known))
            {
                acceptedNbh = inputs.Plan.Neighbourhood ?? "all";
                standingBefore = new HashSet<string>(decision.View.Elements.Select(element => element.UniqueId), StringComparer.Ordinal);
            }
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
                    // R459. If the floor still overlaps where it now stands, Revit raises it and it is accepted again
                    standingBefore?.Remove(id);
                    // The plan may type a ramp anew by its slope or give the plot another unit code, so a move takes both of the plan
                    document.SetType(id, candidate.Type.Key);
                    document.SetHeightOffset(id, candidate.HeightOffset);
                }
                else
                {
                    id = document.CreateFloor(candidate.Loops, candidate.Type.Key, candidate.Level.Key);
                    document.SetHeightOffset(id, candidate.HeightOffset);
                }
                // R114. Without its plot value a later run could not tell the floor is placed and would place it twice
                ParameterWrite write = document.WriteText(id, decision.View.PlotParameter, candidate.Floor.PlotValue.Value.Trim());
                if (write != ParameterWrite.Written)
                {
                    throw new InvalidOperationException(step.Floor.Label + ": " + decision.View.PlotParameter + " was not written, " + write);
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
            // R572. Not here: an error Revit posts at the commit would roll the floors back with the toposolid
            if (!LeavesToposolid(decision, issues))
            {
                toposolidDecision = decision;
            }
            return issues;
        }

        private sealed class ToposolidStep
        {
            public RoadSurfaceRead Read;
            public ToposolidPlan Plan;
            public string Problem;
            public string Element;
            public bool ReadBack;
            public string Key;
            public string Action;
            public string TypeDuplicatedFrom;
            public List<ToposolidElement> Own = new List<ToposolidElement>();
        }

        // R572. Only a press that covers the NBH makes or replaces its toposolid, a plot list would put a small one in its place. A tick off
        // or a plot list leaves it, with one plain line, no read of the surface and no report
        private bool LeavesToposolid(Decision decision, List<string> lines)
        {
            FloorInputs inputs = decision.Inputs;
            if (inputs.RoadTopography == null)
            {
                string nbh = inputs.Plan.Neighbourhood ?? "all";
                bool stands = false;
                Attempt(() => stands = document.Toposolids().Any(element => RoadTopography.IsOwn(element, nbh)), "the toposolids were not read, ", lines);
                if (stands)
                {
                    lines.Add(RoadTopography.TickOffLine);
                }
                return true;
            }
            bool covers = inputs.InScope == null || (inputs.Plan.Plots ?? new List<PlanPlot>()).All(plot => string.IsNullOrWhiteSpace(plot?.Plot) || inputs.InScope(plot.Plot.Trim()));
            if (!covers)
            {
                lines.Add(RoadTopography.PlotListLine);
                return true;
            }
            return false;
        }

        // R571. From the plots of the NBH and what stands in the document. No failure here reaches the floors of the run
        private ToposolidStep PlanToposolid(Decision decision)
        {
            var step = new ToposolidStep();
            try
            {
                surfaceRead = surfaceRead ?? decision.Inputs.RoadTopography();
                step.Read = surfaceRead;
                List<PlannedFloor> scoped = decision.View.Floors.Where(floor => floor.InScope).ToList();
                IEnumerable<double[]> outlines = scoped.SelectMany(floor => floor.Candidate.Floor.Outline.Value.Loops).SelectMany(loop => loop.Points);
                // R573. A ramp this press leaves as it stands does not hold the corners of the plan, so its rows say so
                var skipped = new HashSet<string>(decision.Steps.Where(item => item.Act == Act.Skip && item.Floor.IsRamp).Select(item => item.Floor.Plot), StringComparer.Ordinal);
                // R340. The plan writes the two front corners of a ramp first
                IEnumerable<RampEdge> edges = scoped.Where(floor => floor.IsRamp && floor.Candidate.Floor.Corners?.Value?.Count >= 2)
                    .Select(floor => new RampEdge { Plot = floor.Plot, Start = floor.Candidate.Floor.Corners.Value[0], End = floor.Candidate.Floor.Corners.Value[1], Skipped = skipped.Contains(floor.Plot) });
                step.Plan = RoadTopography.Plan(step.Read, outlines, edges);
                step.Problem = step.Plan.Refused;
                if (step.Problem == null)
                {
                    string nbh = decision.Inputs.Plan.Neighbourhood ?? "all";
                    step.Key = RoadTopography.KeyOf(step.Read, step.Plan);
                    step.Own = document.Toposolids().Where(element => RoadTopography.IsOwn(element, nbh)).ToList();
                    step.Action = RoadTopography.ActionOf(step.Own, step.Key);
                }
            }
            catch (Exception failure) when (!(failure is OutOfMemoryException))
            {
                step.Problem = failure.Message;
            }
            return step;
        }

        // R572. A dry run writes the report to its own folder and nothing into the model
        private IEnumerable<string> PreviewToposolid(RunContext context, Decision decision)
        {
            var lines = new List<string>();
            if (LeavesToposolid(decision, lines))
            {
                return lines;
            }
            ToposolidStep step = PlanToposolid(decision);
            if (context.DryRun)
            {
                WriteToposolid(context, decision, step);
            }
            lines.Add(step.Problem != null ? RoadTopography.NoToposolid(step.Problem) : RoadTopography.PreviewLine(step.Plan, step.Read.Settings, step.Action, step.Own));
            return lines;
        }

        // R459, R572. The file of the accepted warnings, then the toposolid, whichever of them fails
        private void AfterCommit(RunContext context)
        {
            try
            {
                RecordAccepted(context);
            }
            finally
            {
                ToposolidAfterCommit(context);
            }
        }

        // R572. After the floors committed, in a transaction of its own inside the group of the flow, so one undo entry takes both back and an
        // error Revit posts at this commit rolls back the toposolid alone. The floors have committed, so the line that they stand holds
        // whatever fails here. The issues reach the step record by the settling of R498
        private void ToposolidAfterCommit(RunContext context)
        {
            Decision decision = toposolidDecision;
            if (decision == null)
            {
                return;
            }
            var lines = new List<string>();
            ToposolidStep step = PlanToposolid(decision);
            string nbh = decision.Inputs.Plan.Neighbourhood ?? "all";
            if (step.Problem == null)
            {
                if (step.Action == RoadTopography.Keep)
                {
                    step.Element = RoadTopography.Matching(step.Own, step.Key).UniqueId;
                }
                else
                {
                    MakeInOwnTransaction(context, decision, step, nbh, lines);
                }
            }
            if (step.Problem != null)
            {
                lines.Add(RoadTopography.NoToposolid(step.Problem));
            }
            else
            {
                // Only now: the commit regenerated the model and no failed transaction is open
                Attempt(() =>
                {
                    RoadTopography.ReadBack(step.Plan.Edges, document.ToposolidTriangles(step.Element), decision.View.Offset);
                    step.ReadBack = true;
                }, "the toposolid was not read back, ", lines);
                lines.AddRange(new[] { RoadTopography.DoneLine(step.Plan, step.Read.Settings, step.Action, step.Own), RoadTopography.LimitLine(step.Plan, step.Read.Settings, nbh) }.Where(line => line != null));
            }
            try
            {
                WriteToposolid(context, decision, step);
            }
            finally
            {
                context.IssuesAfterCommit.AddRange(lines);
            }
        }

        // The new one before the old one goes, and both in this transaction, so any failure leaves the toposolid of the last run standing
        private void MakeInOwnTransaction(RunContext context, Decision decision, ToposolidStep step, string nbh, List<string> lines)
        {
            try
            {
                using (IModelTransaction transaction = context.Model.Start(RunTexts.TransactionName(context.Label)))
                {
                    try
                    {
                        ToposolidMade made = MakeToposolid(context, decision, step, nbh);
                        foreach (ToposolidElement old in step.Own)
                        {
                            document.DeleteToposolid(old.UniqueId);
                        }
                        if (transaction.Commit())
                        {
                            step.Element = made.UniqueId;
                            step.TypeDuplicatedFrom = made.TypeDuplicatedFrom;
                            lines.AddRange(transaction.Warnings.Select(warning => RoadTopography.Line + "Revit warns, " + warning));
                            return;
                        }
                        step.Problem = transaction.Errors.Count > 0 ? RunTexts.Refused(transaction.Errors) : RunTexts.NotCommitted;
                    }
                    catch (Exception failure) when (!(failure is OutOfMemoryException))
                    {
                        step.Problem = failure.Message;
                    }
                    transaction.RollBack();
                }
            }
            catch (Exception failure) when (!(failure is OutOfMemoryException))
            {
                step.Problem = step.Problem ?? failure.Message;
            }
        }

        private ToposolidMade MakeToposolid(RunContext context, Decision decision, ToposolidStep step, string nbh)
        {
            string levelName = decision.View.Settings.Levels[FloorPlacer.Ramp];
            LevelEntry level = document.Levels().FirstOrDefault(item => item.Name == levelName)
                ?? throw new InvalidDataException("the document has no level " + levelName);
            double[] offset = decision.View.Offset;
            List<double[]> boundary = step.Plan.Boundary.Select(corner => new[] { corner[0] - offset[0], corner[1] - offset[1], level.ElevationM }).ToList();
            List<double[]> points = step.Plan.Points.Select(point => PlacementMath.InternalPoint(point, offset)).ToList();
            ToposolidMade made = document.CreateToposolid(boundary, points, step.Read.Settings.TypeName, level.Key);
            document.Tag(made.UniqueId, RoadTopography.Tag(nbh, context.RunId, decision.Inputs.Profile, made.UniqueId, step.Plan, step.Key));
            return made;
        }

        private static void Attempt(Action action, string what, List<string> lines)
        {
            try
            {
                action();
            }
            catch (Exception failure) when (!(failure is OutOfMemoryException))
            {
                lines.Add(RoadTopography.Line + what + failure.Message);
            }
        }

        private static void WriteToposolid(RunContext context, Decision decision, ToposolidStep step)
        {
            string nbh = decision.Inputs.Plan.Neighbourhood ?? "all";
            JObject report = RoadTopography.Report(context.RunId, nbh, step.Read, step.Plan, step.Element, step.ReadBack, step.Problem, step.Action, step.TypeDuplicatedFrom);
            context.WriteText(RoadTopography.RecordName(nbh), report.ToString(Newtonsoft.Json.Formatting.Indented));
        }

        // R459. A floor deleted by hand since an earlier run takes its pairs off the list
        private void RecordAccepted(RunContext context)
        {
            if (acceptedNbh == null)
            {
                return;
            }
            string name = AcceptedName(acceptedNbh);
            List<AcceptedWarning> earlier = AcceptedWarningFile.Load(Path.Combine(context.WorkFolder, name)) ?? new List<AcceptedWarning>();
            List<AcceptedWarning> list = AcceptedWarningFile.Merge(earlier, context.AcceptedWarnings, standingBefore);
            string written = context.Output(name);
            File.WriteAllText(written, AcceptedWarningFile.Text(context.RunId, acceptedNbh, list), new System.Text.UTF8Encoding(false));
            // Q324, Q350. The count in the log, in the shape of R381, as an accepted warning is no issue and a message would be a box
            int now = context.AcceptedWarnings.Count;
            var log = new JObject
            {
                ["runId"] = context.RunId, ["button"] = context.Button, ["committed"] = true, ["acceptedWarnings"] = now,
                ["line"] = now + " known warnings accepted by this run, " + list.Count + " on the list of the NBH",
                ["inputs"] = new JArray(),
                ["outputs"] = new JArray(new JObject { ["role"] = "acceptedWarnings", ["path"] = name, ["sha256"] = FileHash.Sha256(written) }),
            };
            context.WriteText(LevelsRun.RunLogName(context.RunId), log.ToString(Newtonsoft.Json.Formatting.Indented));
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
                    PlacedValues current = FloorPlan.Current(element, kind);
                    found.Add(FoundElement.Tagged(element.UniqueId, FloorPlan.Named(element.Tag, current), current));
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
