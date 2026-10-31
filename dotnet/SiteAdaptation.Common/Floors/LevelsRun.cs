using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.Floors
{
    // R379. Button 6 Levels as a body of the flow of R382. It sets the height offset of every tagged Generic and yard floor and the ramp
    // corners of the scope to the plan, reads every one back within 1 mm and writes no link parameter (W6, 13-workflow-changes.md Order).
    // Inside the transaction it writes levels_<run id>.json and waits for the worker command levelExcel within addin.workerTimeLimitS.levelExcel,
    // the one wait of the design, so a worker that fails or runs past its limit rolls the run back. The flow moves the Excel onto its name
    // after the commit, and a move that fails leaves step 6 not Done with the file named (03b 6, 09-risks.md open item 10)
    public sealed class LevelsRun
    {
        public const string Command = "levelExcel";
        public const string LevelsFormat = "site-adaptation-levels";

        private readonly IFloorDocument document;
        private readonly Func<RunContext, LevelRunInputs> load;
        private readonly Func<WorkerRequest, string, WorkerAnswer> worker;

        // worker takes a request and the work folder and gives the answer of the worker once it ended, done or not
        public LevelsRun(IFloorDocument document, Func<RunContext, LevelRunInputs> load, Func<WorkerRequest, string, WorkerAnswer> worker)
        {
            this.document = document;
            this.load = load;
            this.worker = worker;
        }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Apply = Apply };
        }

        public static string ExcelName(string nbh, string profile)
        {
            return LevelExcelText.FileName.Replace("<nbh>", nbh).Replace("<profile>", profile);
        }

        public static string LevelsName(string runId)
        {
            return "levels_" + runId + ".json";
        }

        // R381. The run log 9 Report reads, run_<run id>.json, which RunIds starts with the button
        public static string RunLogName(string runId)
        {
            return "run_" + runId + ".json";
        }

        private sealed class Held
        {
            public PlannedFloor Floor;
            public FloorElement Element;
            public bool Move;
            public string Change;
        }

        private sealed class Decision
        {
            public LevelRunInputs Inputs;
            public FloorPlanView View;
            public string Nbh;
            public readonly List<Held> Floors = new List<Held>();
            public readonly List<string> Listed = new List<string>();
        }

        private RunSummary Preview(RunContext context)
        {
            Decision decision = Decide(context);
            int moves = decision.Floors.Count(held => held.Move);
            string counts = "Levels: move " + moves + ", at the plan " + (decision.Floors.Count - moves) + ", listed " + decision.Listed.Count;
            var lines = decision.Floors.Where(held => held.Move).Select(held => held.Floor.Label + ", element " + held.Element.UniqueId + ": " + held.Change).Concat(decision.Listed).ToList();
            lines.Add("Writes " + Path.Combine(context.WorkFolder, ExcelName(decision.Nbh, decision.Inputs.Profile)));
            return new RunSummary(counts, lines);
        }

        private IReadOnlyList<string> Apply(RunContext context)
        {
            Decision decision = Decide(context);
            LevelRunInputs inputs = decision.Inputs;
            if (decision.Floors.Count == 0)
            {
                // As 5 and 8 fail when nothing of the scope stands, so an empty run never counts as Done
                throw new InvalidOperationException("no floor of this tool stands in the scope, so Levels is not done: " + string.Join(". ", decision.Listed.DefaultIfEmpty("the plan gives no floor")));
            }
            List<Held> moved = decision.Floors.Where(held => held.Move).ToList();
            foreach (Held held in moved)
            {
                document.SetHeightOffset(held.Element.UniqueId, held.Floor.Candidate.HeightOffset);
            }
            document.Regenerate();
            foreach (Held held in moved.Where(held => held.Floor.IsRamp))
            {
                FloorPlan.Shape(document, held.Element.UniqueId, held.Floor);
            }
            document.Regenerate();
            var floors = new JArray();
            foreach (Held held in decision.Floors)
            {
                FloorReadBack read = document.ReadBack(held.Element.UniqueId);
                string mismatch = FloorPlacer.Mismatch(read, new PlanFloorCheck { Top = held.Floor.Candidate.TopInternal, Corners = held.Floor.Candidate.CornersInternal });
                if (mismatch != null)
                {
                    throw new InvalidOperationException(held.Floor.Label + ", element " + held.Element.UniqueId + ": the level read back is not the plan, " + mismatch);
                }
                if (held.Move)
                {
                    // R368. A floor moved under the tag of an older plan stands at neither on the next plan, and 5 would leave it as
                    // changed by hand. So it takes the tag 5 writes on a move: the latest version, the plan values, its placedId kept
                    document.Tag(held.Element.UniqueId, new PlacedTag(PlacedSchema.Latest.Version, held.Floor.Item.Key, context.RunId, inputs.Profile, held.Element.Tag.PlacedId, held.Floor.Item.Plan));
                }
                floors.Add(Row(held, read, decision.View.Offset[2]));
            }
            // A plot counts as set when every floor the plan gives it stands tagged and read back at the plan
            var set = new HashSet<string>(decision.Floors.Select(held => held.Floor.Key), StringComparer.Ordinal);
            List<string> levelPlots = decision.View.Floors.Where(floor => floor.InScope).GroupBy(floor => floor.Plot, StringComparer.Ordinal)
                .Where(plot => plot.All(floor => set.Contains(floor.Key))).Select(plot => plot.Key).OrderBy(plot => plot, StringComparer.Ordinal).ToList();

            var levels = new JObject
            {
                ["format"] = LevelsFormat, ["runId"] = context.RunId, ["plan"] = inputs.PlanPath, ["profile"] = inputs.Profile,
                ["floors"] = floors, ["levelPlots"] = new JArray(levelPlots),
            };
            string levelsPath = context.Output(LevelsName(context.RunId));
            File.WriteAllText(levelsPath, levels.ToString(Newtonsoft.Json.Formatting.Indented), new System.Text.UTF8Encoding(false));
            var issues = new List<string>(decision.Listed);
            string excel = Excel(context, decision, issues);

            var log = new JObject
            {
                ["runId"] = context.RunId, ["button"] = context.Button, ["committed"] = true, ["levelPlots"] = new JArray(levelPlots),
                ["moved"] = moved.Count, ["atPlan"] = decision.Floors.Count - moved.Count,
                ["inputs"] = new JArray(new JObject { ["role"] = "plan", ["path"] = inputs.PlanPath, ["sha256"] = Sha(inputs.PlanPath) }),
                ["outputs"] = new JArray(
                    new JObject { ["role"] = Command, ["path"] = ExcelName(decision.Nbh, inputs.Profile), ["sha256"] = FileHash.Sha256(excel) },
                    new JObject { ["role"] = "levels", ["path"] = LevelsName(context.RunId), ["sha256"] = FileHash.Sha256(levelsPath) }),
            };
            context.WriteText(RunLogName(context.RunId), log.ToString(Newtonsoft.Json.Formatting.Indented));
            return issues;
        }

        // R364. The worker writes the Excel into a stage folder of the run, which is moved to the temporary name of the run here, so the
        // flow moves it onto its name only after the commit. The stage folder is deleted whatever happens
        private string Excel(RunContext context, Decision decision, List<string> issues)
        {
            LevelRunInputs inputs = decision.Inputs;
            string name = ExcelName(decision.Nbh, inputs.Profile);
            string stage = Path.Combine(context.WorkFolder, "levelexcel_" + context.RunId);
            // levels_<run id>.json is not an input: it stands under its temporary name until the commit, which the worker refuses, and
            // the worker does not read it (Q226)
            var files = new List<WorkerFile>(inputs.ExcelInputs ?? new List<WorkerFile>());
            var request = new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = context.RunId, Command = Command, Document = inputs.Document, Inputs = files,
                LevelProfile = inputs.Profile, Scope = new WorkerScope { Kind = "nbh", Nbh = decision.Nbh }, OutputFolder = stage, TimeLimitS = inputs.TimeLimitS,
            };
            try
            {
                WorkerAnswer answer;
                try
                {
                    answer = worker(request, context.WorkFolder);
                }
                catch (Exception failure) when (!(failure is OutOfMemoryException))
                {
                    throw new InvalidOperationException("the worker command " + Command + " did not end done, " + failure.Message + ", nothing was set", failure);
                }
                if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
                {
                    throw new InvalidOperationException("the worker command " + Command + " ended " + (answer == null ? "with no answer" : answer.Status + ", " + answer.Message) + ", nothing was set");
                }
                if (answer.Seconds > inputs.TimeLimitS)
                {
                    throw new InvalidOperationException("the worker command " + Command + " ended past its limit of " + FloorPlan.Text(inputs.TimeLimitS) + " s, addin.workerTimeLimitS." + Command + ", nothing was set");
                }
                WorkerFile book = (answer.Outputs ?? new List<WorkerFile>()).FirstOrDefault(output => string.Equals(Path.GetFileName(output.Path), name, StringComparison.OrdinalIgnoreCase));
                if (book == null || !File.Exists(book.Path))
                {
                    throw new InvalidOperationException("the worker command " + Command + " wrote no " + name + ", nothing was set");
                }
                string temporary = context.Output(Path.Combine(context.WorkFolder, name));
                File.Move(book.Path, temporary);
                issues.AddRange(answer.Issues ?? new List<string>());
                return temporary;
            }
            finally
            {
                if (Directory.Exists(stage))
                {
                    Directory.Delete(stage, true);
                }
            }
        }

        private Decision Decide(RunContext context)
        {
            LevelRunInputs inputs = load(context) ?? throw new InvalidOperationException("Levels has no plan to read");
            PlanFile plan = inputs.Plan;
            Func<string, bool> inScope = number => inputs.InScope == null || inputs.InScope(number);
            List<string> noFfl = (plan?.Plots ?? new List<PlanPlot>()).Where(plot => plot?.Plot != null && inScope(plot.Plot.Trim()) && !(plot.Levels?.Ffl?.Value).HasValue)
                .Select(plot => "plot " + plot.Plot.Trim() + ", " + (inputs.NoFfl.TryGetValue(plot.Plot.Trim(), out string why) ? why : "the plan gives no reason")).ToList();
            if (noFfl.Count > 0)
            {
                throw new InvalidDataException("6 Levels stops, a plot of the scope has no FFL in the plan: " + string.Join(". ", noFfl) + " (R379)");
            }
            var decision = new Decision { Inputs = inputs, View = FloorPlan.Read(document, plan, inputs.InScope, null), Nbh = plan.Neighbourhood ?? "all" };
            // Q225. A plot of the missing list the plan does not write has no entry in plan.Plots, so it is listed, not stopped on
            var written = new HashSet<string>((plan?.Plots ?? new List<PlanPlot>()).Where(plot => plot?.Plot != null).Select(plot => plot.Plot.Trim()), StringComparer.Ordinal);
            decision.Listed.AddRange(inputs.NoFfl.Where(missing => !written.Contains(missing.Key.Trim()) && inScope(missing.Key.Trim())).OrderBy(missing => missing.Key, StringComparer.Ordinal)
                .Select(missing => "plot " + missing.Key.Trim() + ": the plan does not write it, " + missing.Value + ", no level set (R379)"));
            FloorPlanView view = decision.View;
            Dictionary<string, PlannedFloor> planned = view.Floors.ToDictionary(floor => floor.Key, StringComparer.Ordinal);
            var planPlots = new HashSet<string>(view.Floors.Select(floor => floor.Plot), StringComparer.Ordinal);
            var standing = new HashSet<string>(StringComparer.Ordinal);
            foreach (FloorElement element in view.Elements)
            {
                string kind = view.KindOf(element);
                if (kind == null)
                {
                    continue;
                }
                if (element.Tag == null)
                {
                    string plot = view.PlotOf(element);
                    if (plot != null && inScope(plot))
                    {
                        decision.Listed.Add("plot " + plot + " " + kind + ": element " + element.UniqueId + " has no tag of this tool, listed and not changed (R379)");
                    }
                    continue;
                }
                string key = FloorPlan.KeyOf(element.Tag.Key.Plot, kind);
                if (!planned.TryGetValue(key, out PlannedFloor floor))
                {
                    if (!planPlots.Contains(element.Tag.Key.Plot) && inScope(element.Tag.Key.Plot))
                    {
                        decision.Listed.Add("element " + element.UniqueId + " is tagged for plot " + element.Tag.Key.Plot + " " + kind + ", which the plan holds no floor of, left as it is");
                    }
                    continue;
                }
                if (!floor.InScope)
                {
                    continue;
                }
                if (!string.Equals(element.UniqueId, element.Tag.PlacedId, StringComparison.Ordinal) || !standing.Add(key))
                {
                    decision.Listed.Add(floor.Label + ": element " + element.UniqueId + " is a copy of the floor this tool placed, never changed");
                    continue;
                }
                decision.Floors.Add(Compare(floor, element, view.Offset[2]));
            }
            decision.Listed.AddRange(view.Floors.Where(floor => floor.InScope && !standing.Contains(floor.Key)).Select(floor => floor.Label + ": no floor of this tool stands, run 5 Floors and ramps"));
            return decision;
        }

        // A floor moves when its top, or a corner of a ramp, lies more than the read back tolerance of 1 mm off the plan
        private static Held Compare(PlannedFloor floor, FloorElement element, double elevation)
        {
            var held = new Held { Floor = floor, Element = element };
            FloorPlacer.Candidate candidate = floor.Candidate;
            if (!floor.IsRamp)
            {
                double? now = element.State?.TopMax;
                held.Move = !now.HasValue || Math.Abs(now.Value - candidate.TopInternal) > FloorPlacer.TopTolerance;
                held.Change = "top " + (now.HasValue ? FloorPlan.Text(now.Value + elevation) + " m" : "unread") + " to " + FloorPlan.Text(candidate.TopInternal + elevation) + " m"
                    + (now.HasValue ? ", by " + FloorPlan.Text(candidate.TopInternal - now.Value) + " m" : "");
                return held;
            }
            List<double[]> corners = element.State?.Corners;
            double largest = 0;
            bool unread = corners == null || corners.Count != candidate.CornersInternal.Count;
            foreach (double[] corner in unread ? new List<double[]>() : candidate.CornersInternal)
            {
                double[] near = corners.OrderBy(found => FloorPlan.Flat(found, corner)).First();
                if (FloorPlan.Flat(near, corner) > FloorPlacer.CornerOnVertex + 1e-9)
                {
                    unread = true;
                    break;
                }
                largest = Math.Max(largest, Math.Abs(corner[2] - near[2]));
            }
            held.Move = unread || largest > FloorPlacer.TopTolerance;
            held.Change = unread ? "its shape corners do not sit on the plan corners, shaped again from flat" : "corners to the plan, the largest by " + FloorPlan.Text(largest) + " m";
            return held;
        }

        // The level of each floor as read back, in shared elevation at the 3 decimals of R2. No plan coordinate, the UniqueId finds it
        private static JObject Row(Held held, FloorReadBack read, double elevation)
        {
            var row = new JObject
            {
                ["plot"] = held.Floor.Plot, ["kind"] = held.Floor.Kind, ["uniqueId"] = held.Element.UniqueId, ["moved"] = held.Move,
                ["top"] = read.TopMax.HasValue ? JsonOut.Round3(read.TopMax.Value + elevation) : (double?)null,
            };
            if (held.Floor.IsRamp && read.Corners != null)
            {
                row["cornersZ"] = new JArray(read.Corners.Select(corner => JsonOut.Round3(corner[2] + elevation)));
            }
            return row;
        }

        private static string Sha(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? FileHash.Sha256(path) : null;
        }
    }
}
