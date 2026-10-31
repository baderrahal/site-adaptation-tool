using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Report;
using SiteAdaptation.Common.Tags;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R379. Button 6 Levels on a fake document after button 5 placed the floors, with a fake worker of levelExcel: the floors set to the
    // plan and read back, the dry run, the stops, and open item 10 of docs/design/09-risks.md, the wait for the worker inside the
    // transaction, the roll back when it fails or runs past its limit and the move onto an Excel held open. No Revit type
    public class R379_LevelsTests : IDisposable
    {
        private const string Profile = "parsons2024Rev6";
        private const string Document = @"C:\Models\Site.rvt";
        private readonly string work = Path.Combine(Path.GetTempPath(), "r379_" + Guid.NewGuid().ToString("N"));
        private readonly string planPath;
        private readonly List<WorkerRequest> asked = new List<WorkerRequest>();

        // What the fake worker does: null writes the Excel and ends done, else the status it ends with
        private string workerEnds;

        public R379_LevelsTests()
        {
            Directory.CreateDirectory(work);
            planPath = Path.Combine(work, "plan_012.json");
            File.WriteAllText(planPath, "{}");
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private FakeFloors Placed(PlanFile plan)
        {
            var document = new FakeFloors();
            var floors = new FloorsRun(document, context => new FloorInputs { Plan = plan, PlanPath = planPath, Profile = Profile });
            var context = new RunContext(5, "5 Floors and ramps", "5_1", work, null, true, new RunInput[0], new Dictionary<string, string>());
            floors.Body().Preview(context);
            floors.Body().Apply(context);
            foreach (KeyValuePair<string, string> output in context.Outputs)
            {
                WorkFiles.Commit(output.Key, output.Value);
            }
            document.Calls.Clear();
            return document;
        }

        private LevelsRun Make(FakeFloors document, PlanFile plan, IReadOnlyDictionary<string, string> noFfl = null)
        {
            return new LevelsRun(document, context => new LevelRunInputs
            {
                Plan = plan, PlanPath = planPath, Profile = Profile, NoFfl = noFfl ?? new Dictionary<string, string>(), TimeLimitS = 60,
                ExcelInputs = new List<WorkerFile> { new WorkerFile { Role = "plan", Path = planPath, Sha256 = FileHash.Sha256(planPath) } },
                Document = new WorkerDocument { Path = Document, SessionId = "levels" },
            }, Worker);
        }

        private WorkerAnswer Worker(WorkerRequest request, string workFolder)
        {
            asked.Add(request);
            if (workerEnds != null)
            {
                return new WorkerAnswer { RunId = request.RunId, Status = workerEnds, Message = "the worker ended " + workerEnds, Seconds = 61 };
            }
            Directory.CreateDirectory(request.OutputFolder);
            string temporary = WorkFiles.TemporaryName(Path.Combine(request.OutputFolder, LevelsRun.ExcelName("012", Profile)), request.RunId);
            File.WriteAllText(temporary, "xlsx of " + request.RunId);
            string book = Path.Combine(request.OutputFolder, LevelsRun.ExcelName("012", Profile));
            WorkFiles.Commit(temporary, book);
            return new WorkerAnswer
            {
                RunId = request.RunId, Status = WorkerExit.Status(WorkerExit.Done), Seconds = 2, Issues = new List<string>(),
                Outputs = new List<WorkerFile> { new WorkerFile { Role = "levelExcel", Path = book, Sha256 = FileHash.Sha256(book) } },
            };
        }

        private RunContext Context(string runId = "6_1", bool dry = false)
        {
            return new RunContext(6, "6 Levels", runId, work, dry ? Path.Combine(work, "dry", runId) : null, true, new RunInput[0], new Dictionary<string, string>());
        }

        private static FakeFloors.Floor Find(FakeFloors document, string plot, string kind)
        {
            return document.Items.Single(floor => floor.Tag != null && floor.Tag.Key.Plot == plot && floor.Tag.Key.Kind == kind);
        }

        private RunOutcome Flow(LevelsRun run, FlowState state, FlowModel model)
        {
            var flow = new ButtonFlow(state, model, new Always(), input => "hash", () => "6_20261031-120000-000");
            return flow.Run(6, "6 Levels", run.Body(), Document);
        }

        [Fact]
        public void R379_after_5_placed_the_same_plan_levels_moves_nothing_and_writes_the_excel_again()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102"));
            FakeFloors document = Placed(plan);
            LevelsRun run = Make(document, plan);

            RunSummary summary = run.Body().Preview(Context());
            IReadOnlyList<string> issues = run.Body().Apply(Context());

            Assert.Contains("move 0, at the plan 6", summary.Counts);
            Assert.DoesNotContain(document.Calls, call => call.StartsWith("offset", StringComparison.Ordinal));
            Assert.Single(asked);
            Assert.Equal(LevelsRun.Command, asked[0].Command);
            Assert.Equal(60, asked[0].TimeLimitS);
            Assert.Equal(Profile, asked[0].LevelProfile);
            Assert.True(File.Exists(WorkFiles.TemporaryName(Path.Combine(work, LevelsRun.ExcelName("012", Profile)), "6_1")));
            Assert.Empty(issues);
        }

        // The Revit run of 5d: levels_<run id>.json, named in the request under its temporary name, made the worker refuse the request
        [Fact]
        public void R379_the_request_of_levelExcel_names_no_input_of_a_temporary_name()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));
            FakeFloors document = Placed(plan);

            Make(document, plan).Body().Apply(Context());

            Assert.Single(asked);
            Assert.DoesNotContain(asked[0].Inputs, input => WorkFiles.IsTemporary(input.Path));
            Assert.True(File.Exists(WorkFiles.TemporaryName(Path.Combine(work, LevelsRun.LevelsName("6_1")), "6_1")));
        }

        // The review of 5d: 6 moved a floor and left the tag of the old plan, so 5 on the next plan found it at neither and left it as
        // changed by hand
        [Fact]
        public void R379_R368_a_floor_6_moved_to_plan_2_is_moved_by_5_on_plan_3_not_changed_by_hand()
        {
            FakeFloors document = Placed(FloorPlans.Plan(FloorPlans.Plot("101")));
            Make(document, FloorPlans.Plan(FloorPlans.Plot("101", 10.8))).Body().Apply(Context());
            FakeFloors.Floor generic = Find(document, "101", PlacedKinds.Generic);
            Assert.Equal(10.8, generic.Top, 9);

            var floors = new FloorsRun(document, context => new FloorInputs { Plan = FloorPlans.Plan(FloorPlans.Plot("101", 11.0)), PlanPath = planPath, Profile = Profile });
            var third = new RunContext(5, "5 Floors and ramps", "5_3", work, null, true, new RunInput[0], new Dictionary<string, string>());
            RunSummary summary = floors.Body().Preview(third);
            floors.Body().Apply(third);

            Assert.DoesNotContain(summary.PerPlot, line => line.Contains(MatchCase.ChangedByHand.ToString()));
            Assert.Contains(summary.PerPlot, line => line.StartsWith("plot 101 generic: change element " + generic.UniqueId + " in place", StringComparison.Ordinal));
            Assert.Equal(11.0, generic.Top, 9);
            Assert.Equal("5_3", generic.Tag.Run);
        }

        // The breaker of 5d, Q225: a plot of the scope the plan does not write has no entry in plan.Plots and got no line
        [Fact]
        public void R379_a_plot_of_the_scope_the_plan_does_not_write_is_listed_with_the_reason_of_the_missing_list()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));
            FakeFloors document = Placed(plan);

            IReadOnlyList<string> issues = Make(document, plan, new Dictionary<string, string> { ["102"] = "the sales book gives no FFL" }).Body().Apply(Context());

            Assert.Contains(issues, issue => issue.StartsWith("plot 102", StringComparison.Ordinal) && issue.Contains("the sales book gives no FFL"));
        }

        // The breaker of 5d: with no tagged floor of the scope standing 6 ended done, where 5 and 8 fail
        [Fact]
        public void R379_levels_fails_when_no_floor_of_the_scope_stands()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));

            var failure = Assert.Throws<InvalidOperationException>(() => Make(new FakeFloors(), plan).Body().Apply(Context()));

            Assert.Contains("run 5 Floors and ramps", failure.Message);
            Assert.Empty(asked);
        }

        [Fact]
        public void R379_a_floor_moved_by_hand_is_set_to_the_plan_and_read_back_and_tagged_with_the_plan_by_this_run()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));
            FakeFloors document = Placed(plan);
            FakeFloors.Floor generic = Find(document, "101", PlacedKinds.Generic);
            FakeFloors.Floor ramp = Find(document, "101", PlacedKinds.Ramp);
            generic.HeightOffset += 0.05;
            ramp.CornerOffsets[1] += 0.02;
            LevelsRun run = Make(document, plan);

            RunSummary summary = run.Body().Preview(Context());
            run.Body().Apply(Context());

            Assert.Contains("move 2, at the plan 1", summary.Counts);
            Assert.Contains("plot 101 generic, element " + generic.UniqueId + ": top 10.55 m to 10.5 m, by -0.05 m", summary.PerPlot);
            Assert.Equal(10.5, generic.Top, 9);
            Assert.Equal(0.1, ramp.CornerOffsets[1], 9);
            Assert.Equal("6_1", generic.Tag.Run);
            Assert.Equal(generic.UniqueId, generic.Tag.PlacedId);
            Assert.Equal("5_1", Find(document, "101", PlacedKinds.Yard).Tag.Run);
        }

        private string[] LevelPlots(string runId)
        {
            JObject levels = JObject.Parse(File.ReadAllText(WorkFiles.TemporaryName(Path.Combine(work, LevelsRun.LevelsName(runId)), runId)));
            return levels["levelPlots"].Select(plot => (string)plot).ToArray();
        }

        [Fact]
        public void R379_a_tagged_floor_of_a_kind_the_plan_no_longer_gives_is_listed_and_its_plot_is_not_set()
        {
            FakeFloors document = Placed(FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102")));
            FakeFloors.Floor yard = Find(document, "101", PlacedKinds.Yard);
            PlanFile changed = FloorPlans.Plan(FloorPlans.Plot("101", yard: false), FloorPlans.Plot("102"));

            IReadOnlyList<string> issues = Make(document, changed).Body().Apply(Context());

            Assert.Contains(issues, issue => issue.Contains(yard.UniqueId) && issue.Contains("plot 101 yard") && issue.Contains("no longer"));
            Assert.Equal(new[] { "102" }, LevelPlots("6_1"));
        }

        [Fact]
        public void R379_a_floor_whose_outline_the_plan_changed_is_listed_not_set_and_its_plot_is_not_set()
        {
            FakeFloors document = Placed(FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102")));
            FakeFloors.Floor yard = Find(document, "101", PlacedKinds.Yard);
            PlanFile changed = FloorPlans.Plan(FloorPlans.Plot("101", ffl: 10.7), FloorPlans.Plot("102"));
            changed.Plots[0].Floors.Yard.Outline.Value.Loops[0].Points = FloorPlans.Square(1100, 2020, 10, 12);

            IReadOnlyList<string> issues = Make(document, changed).Body().Apply(Context());

            Assert.Equal(10.4, yard.Top, 9);
            Assert.Equal("5_1", yard.Tag.Run);
            Assert.Equal(10.7, Find(document, "101", PlacedKinds.Generic).Top, 9);
            Assert.Contains(issues, issue => issue.Contains(yard.UniqueId) && issue.Contains("outline"));
            Assert.Equal(new[] { "102" }, LevelPlots("6_1"));
        }

        [Fact]
        public void R379_a_floor_whose_type_or_plot_value_is_not_the_plan_is_left_to_5_and_its_plot_is_not_set()
        {
            FakeFloors document = Placed(FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102")));
            document.Types.Add(new FloorTypeEntry { Name = "Ramp 2", Key = "4" });
            FakeFloors.Floor ramp = Find(document, "101", PlacedKinds.Ramp);
            FakeFloors.Floor generic = Find(document, "102", PlacedKinds.Generic);
            PlanFile changed = FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102"));
            changed.FloorSettings.Types["ramp"].Add("Ramp 2");
            changed.Plots[0].Floors.Ramp.Type.Value = "Ramp 2";
            changed.Plots[1].Floors.Generic.PlotValue.Value = "VD5-102";
            ramp.CornerOffsets[1] += 0.02;

            IReadOnlyList<string> issues = Make(document, changed).Body().Apply(Context());

            Assert.Equal("3", ramp.TypeKey);
            Assert.Equal(0.12, ramp.CornerOffsets[1], 9);
            Assert.Equal("VD4-102", generic.Parameters["Plot"]);
            Assert.Contains(issues, issue => issue.Contains(ramp.UniqueId) && issue.Contains("type") && issue.Contains("5 Floors and ramps"));
            Assert.Contains(issues, issue => issue.Contains(generic.UniqueId) && issue.Contains("Plot value") && issue.Contains("5 Floors and ramps"));
            Assert.Empty(LevelPlots("6_1"));
        }

        [Fact]
        public void R379_the_dry_run_lists_every_move_and_the_excel_it_would_write_and_changes_nothing()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));
            FakeFloors document = Placed(plan);
            Find(document, "101", PlacedKinds.Yard).HeightOffset = 10.3;

            RunSummary summary = Make(document, plan).Body().Preview(Context(dry: true));

            Assert.Contains("plot 101 yard, element " + Find(document, "101", PlacedKinds.Yard).UniqueId + ": top 10.3 m to 10.4 m, by 0.1 m", summary.PerPlot);
            Assert.Contains("Writes " + Path.Combine(work, LevelsRun.ExcelName("012", Profile)), summary.PerPlot);
            Assert.Equal(10.3, Find(document, "101", PlacedKinds.Yard).Top, 9);
            Assert.Empty(asked);
            Assert.Empty(document.Calls);
        }

        [Fact]
        public void R379_a_plot_of_the_scope_with_no_ffl_in_the_plan_stops_with_its_reason()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102"));
            FakeFloors document = Placed(plan);
            plan.Plots[1].Levels.Ffl = null;
            var why = new Dictionary<string, string> { ["102"] = "ffl: no road level at a front corner" };

            var stop = Assert.Throws<InvalidDataException>(() => Make(document, plan, why).Body().Preview(Context()));

            Assert.Equal("6 Levels stops, a plot of the scope has no FFL in the plan: plot 102, ffl: no road level at a front corner (R379)", stop.Message);
            Assert.Empty(document.Calls);
        }

        [Fact]
        public void R379_a_floor_with_no_tag_is_listed_and_never_changed()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));
            FakeFloors document = Placed(plan);
            FakeFloors.Floor hand = document.ByHand("2", "VD4-101", 9.0, new[] { 1100.0, 2020.0 }, new[] { 1110.0, 2020.0 }, new[] { 1110.0, 2030.0 });

            IReadOnlyList<string> issues = Make(document, plan).Body().Apply(Context());

            Assert.Equal(9.0, hand.Top, 9);
            Assert.Contains(issues, issue => issue.Contains(hand.UniqueId) && issue.Contains("has no tag") && issue.Contains("not changed"));
        }

        [Fact]
        public void R379_the_run_log_names_the_plots_set_and_read_back_and_9_report_finds_it()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102", yard: false));
            FakeFloors document = Placed(plan);
            var state = new FlowState(work);

            RunOutcome outcome = Flow(Make(document, plan), state, new FlowModel());

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.True(outcome.Record.StepDone);
            ReportRun.RunLog log = ReportRun.RunLog.Last(work, 6, WorkFiles.ReadText);
            Assert.Equal("6_20261031-120000-000", log.RunId);
            Assert.Equal(new[] { "101", "102" }, log.LevelPlots);
            JObject levels = JObject.Parse(File.ReadAllText(Path.Combine(work, LevelsRun.LevelsName("6_20261031-120000-000"))));
            Assert.Equal(5, ((JArray)levels["floors"]).Count);
            Assert.True(File.Exists(Path.Combine(work, LevelsRun.ExcelName("012", Profile))));
        }

        // Open item 10. A worker that fails or ends past its limit makes Apply fail inside the transaction, so the flow rolls it back and
        // deletes every temporary output, the Excel, levels_<run id>.json and the run log among them
        [Theory]
        [InlineData("failed")]
        [InlineData("timedOut")]
        public void R379_a_worker_that_fails_or_runs_past_its_limit_rolls_the_run_back(string ends)
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));
            FakeFloors document = Placed(plan);
            Find(document, "101", PlacedKinds.Generic).HeightOffset = 11.0;
            workerEnds = ends;
            var model = new FlowModel();
            var state = new FlowState(work);

            RunOutcome outcome = Flow(Make(document, plan), state, model);

            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.Contains("levelExcel", outcome.Message);
            Assert.Contains(ends, outcome.Message);
            Assert.Equal(1, model.RollBacks);
            Assert.Equal(0, model.Commits);
            Assert.Empty(state.Records);
            Assert.Empty(Directory.GetFiles(work, "*.tmp-*"));
            Assert.Null(ReportRun.RunLog.Last(work, 6, WorkFiles.ReadText));
        }

        [Fact]
        public void R379_an_answer_done_past_the_time_limit_is_refused()
        {
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));
            FakeFloors document = Placed(plan);
            var run = new LevelsRun(document, context => new LevelRunInputs
            {
                Plan = plan, PlanPath = planPath, Profile = Profile, TimeLimitS = 1, Document = new WorkerDocument { Path = Document },
            }, (request, folder) => { WorkerAnswer answer = Worker(request, folder); answer.Seconds = 1.5; return answer; });

            var late = Assert.Throws<InvalidOperationException>(() => run.Body().Apply(Context()));

            Assert.Contains("past its limit of 1 s", late.Message);
        }

        // Open item 10. The Excel held open by the user cannot be moved onto its name after the commit, so the step stays not Done and the
        // message names the file. Only Windows refuses a replace of a file held open, so the held half runs there
        [Fact]
        public void R379_an_excel_held_open_after_the_commit_leaves_step_6_not_done_naming_the_file()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }
            PlanFile plan = FloorPlans.Plan(FloorPlans.Plot("101"));
            FakeFloors document = Placed(plan);
            string excel = Path.Combine(work, LevelsRun.ExcelName("012", Profile));
            File.WriteAllText(excel, "the Excel of an earlier run");
            var state = new FlowState(work);
            RunOutcome outcome;
            using (new FileStream(excel, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                outcome = Flow(Make(document, plan), state, new FlowModel());
            }

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.False(outcome.Record.StepDone);
            Assert.Contains(excel, outcome.Record.FailedMoves);
            Assert.Contains(LevelsRun.ExcelName("012", Profile), outcome.Message);
            Assert.Equal("the Excel of an earlier run", File.ReadAllText(excel));
        }

        private sealed class Always : IConfirm
        {
            public bool Apply(string label, RunSummary summary, bool dryRun) => true;
        }

        private sealed class FlowState : IRunState
        {
            private readonly string work;

            public FlowState(string work)
            {
                this.work = work;
            }

            public List<RunRecord> Records { get; } = new List<RunRecord>();

            public bool DryRunFirst => false;

            public string WorkFolder(string documentPath) => work;

            public int MissingStep(int button) => 0;

            public IReadOnlyList<RunInput> Inputs(int button) => new RunInput[0];

            public void Committed(int button, RunRecord record)
            {
                Records.Add(record);
            }
        }

        private sealed class FlowModel : IModel, IModelTransaction
        {
            public int Commits;
            public int RollBacks;

            public IModelTransaction Start(string name) => this;

            public bool Commit()
            {
                Commits++;
                return true;
            }

            public void RollBack()
            {
                RollBacks++;
            }

            public IReadOnlyList<string> Warnings => new string[0];

            public IReadOnlyList<string> Errors => new string[0];

            public void Dispose()
            {
            }
        }
    }
}
