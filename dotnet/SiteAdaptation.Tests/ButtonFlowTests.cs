using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R382. The flow of a button in Common, on net48 and net8.0, driven by fake bodies, a fake model and a fake confirm, so no Revit is
    // needed. The files are real files in a temporary folder, and a move fails on a file this test holds open as Excel would
    public sealed class ButtonFlowTests : IDisposable
    {
        private const string RunId = "4_20261028-101800-000";
        private const string Document = @"C:\Models\Site.rvt";
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa-flow-" + Guid.NewGuid().ToString("N"));
        private readonly string work;
        private readonly string curbs;
        private readonly FakeState state;
        private readonly FakeModel model = new FakeModel();
        private readonly FakeConfirm confirm = new FakeConfirm();
        private readonly Dictionary<string, string> hashes = new Dictionary<string, string>();

        public ButtonFlowTests()
        {
            work = Path.Combine(root, "work");
            Directory.CreateDirectory(work);
            curbs = Path.Combine(root, "curbs.dwg");
            File.WriteAllText(curbs, "curbs");
            hashes["Curbs"] = "a1";
            state = new FakeState(work) { Inputs = { new RunInput("Curbs", curbs, false) } };
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        // The labels of the ribbon the add-in passes in, RibbonLayout of the add-in, which the net8.0 leg does not load
        private static string Label(int number)
        {
            return new Dictionary<int, string> { { 2, "2 Read data" }, { 4, "4 Place villas" }, { 5, "5 Floors and ramps" }, { 6, "6 Levels" } }[number];
        }

        private ButtonFlow Flow()
        {
            return new ButtonFlow(state, model, confirm, input => hashes[input.Name], () => RunId);
        }

        // A body that counts in its preview, writes plan.json and its dry log in a dry run, and plan.json in the real run
        private static ButtonBody Body(Func<RunContext, IReadOnlyList<string>> apply = null, Func<RunContext, string> readBack = null)
        {
            return new ButtonBody
            {
                Preview = context =>
                {
                    if (context.DryRun)
                    {
                        context.WriteText("plan.json", "dry plan");
                        context.WriteText("dry.log", "dry log");
                    }
                    return new RunSummary("place 2, skip 1", new[] { "2290 place", "2291 place", "2292 skip, already placed" });
                },
                Apply = apply ?? (context =>
                {
                    context.WriteText("plan.json", "real plan");
                    return new[] { "plot 2290: one issue of the body" };
                }),
                ReadBack = readBack ?? (context => null),
            };
        }

        private string[] FilesUnderWork()
        {
            return Directory.GetFiles(work, "*", SearchOption.AllDirectories).Select(path => path.Substring(work.Length + 1)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private void AssertNothingChanged()
        {
            Assert.Empty(model.Started);
            Assert.Empty(state.Records);
        }

        [Fact]
        public void R382_each_stop_comes_before_any_change_with_its_message()
        {
            Assert.Equal("Save the project first, its work folder is named by its file", Flow().Run(4, Label(4), Body(), "").Message);
            Assert.Equal("Save the project first, its work folder is named by its file", Flow().Run(4, Label(4), Body(), null).Message);

            state.Missing = 4;
            RunOutcome step = Flow().Run(5, Label(5), Body(), Document);
            Assert.Equal(RunEnd.Stopped, step.End);
            Assert.Equal("5 Floors and ramps needs step 4", step.Message);
            state.Missing = 0;

            File.Delete(curbs);
            Assert.Equal("2 Read data needs Curbs, the file is not on disk", Flow().Run(2, Label(2), Body(), Document).Message);
            state.Inputs[0] = new RunInput("Curbs", null, false);
            Assert.Equal("2 Read data needs Curbs, the file is not chosen", Flow().Run(2, Label(2), Body(), Document).Message);
            state.Inputs[0] = new RunInput("Road surface", Path.Combine(root, "no-such-folder"), true);
            Assert.Equal("2 Read data needs Road surface, the folder is not on disk", Flow().Run(2, Label(2), Body(), Document).Message);

            Assert.Empty(confirm.Asked);
            Assert.Empty(FilesUnderWork());
            AssertNothingChanged();
        }

        [Fact]
        public void R382_a_dry_run_writes_only_under_its_dry_folder_and_cancel_changes_nothing()
        {
            state.DryRunFirst = true;
            confirm.Answer = false;

            RunOutcome outcome = Flow().Run(4, Label(4), Body(), Document);

            Assert.Equal(RunEnd.Cancelled, outcome.End);
            Assert.Null(outcome.Message);
            Assert.Equal(new[] { Path.Combine("dry", RunId, "dry.log"), Path.Combine("dry", RunId, "plan.json") }, FilesUnderWork());
            Assert.Equal("dry plan", File.ReadAllText(Path.Combine(work, "dry", RunId, "plan.json")));
            FakeConfirm.Call asked = Assert.Single(confirm.Asked);
            Assert.True(asked.DryRun);
            Assert.Equal("4 Place villas", asked.Label);
            Assert.Equal(3, asked.Summary.PerPlot.Count);
            AssertNothingChanged();
        }

        [Fact]
        public void R382_apply_after_a_dry_run_checks_the_input_hashes_again_and_stops_on_a_change()
        {
            state.DryRunFirst = true;
            confirm.OnAsk = () => hashes["Curbs"] = "b2";

            RunOutcome outcome = Flow().Run(4, Label(4), Body(), Document);

            Assert.Equal(RunEnd.Stopped, outcome.End);
            Assert.Equal("4 Place villas: Curbs changed since the dry run, nothing was changed. Run the button again.", outcome.Message);
            Assert.False(File.Exists(Path.Combine(work, "plan.json")));
            AssertNothingChanged();
        }

        [Fact]
        public void R382_apply_after_a_dry_run_on_the_same_inputs_runs_one_transaction_named_by_the_button_and_moves_its_files()
        {
            state.DryRunFirst = true;
            model.WarningsToGive.Add("Highlighted walls overlap");

            RunOutcome outcome = Flow().Run(4, Label(4), Body(), Document);

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Null(outcome.Message);
            Assert.Equal(new[] { "Site Adaptation 4 Place villas" }, model.Started);
            Assert.Equal(1, model.Commits);
            Assert.Equal(0, model.RollBacks);
            Assert.Equal("real plan", File.ReadAllText(Path.Combine(work, "plan.json")));
            Assert.DoesNotContain(FilesUnderWork(), path => WorkFiles.IsTemporary(path));
            RunRecord record = Assert.Single(state.Records).Value;
            Assert.True(record.StepDone);
            Assert.Equal(RunId, record.RunId);
            Assert.Equal("a1", record.InputHashes["Curbs"]);
            Assert.Equal(new[] { "plot 2290: one issue of the body", "Highlighted walls overlap" }, record.Issues);
            Assert.Equal(FileHash.Sha256(Path.Combine(work, "plan.json")), record.Outputs[Path.Combine(work, "plan.json")]);
        }

        [Fact]
        public void R382_unticked_writes_no_dry_folder_but_asks_apply_on_the_counts_before_any_write()
        {
            state.DryRunFirst = false;
            confirm.Answer = false;
            var preview = new List<bool>();
            ButtonBody body = Body();
            Func<RunContext, RunSummary> counts = body.Preview;
            body.Preview = context =>
            {
                preview.Add(context.DryRun);
                Assert.Throws<InvalidOperationException>(() => context.Output("plan.json"));
                return counts(context);
            };

            RunOutcome outcome = Flow().Run(4, Label(4), body, Document);

            Assert.Equal(RunEnd.Cancelled, outcome.End);
            Assert.Equal(new[] { false }, preview);
            Assert.False(Assert.Single(confirm.Asked).DryRun);
            Assert.Empty(FilesUnderWork());
            AssertNothingChanged();

            confirm.Answer = true;
            Assert.Equal(RunEnd.Committed, Flow().Run(4, Label(4), body, Document).End);
            Assert.Equal(new[] { "plan.json" }, FilesUnderWork());
        }

        [Fact]
        public void R382_the_confirm_shows_the_list_per_plot_after_a_dry_run_only()
        {
            var summary = new RunSummary("place 2, skip 1", new[] { "2290 place" });

            Assert.Equal("Dry run done, nothing was changed.\nplace 2, skip 1", RunTexts.ConfirmContent(summary, true));
            Assert.Equal("2290 place", RunTexts.ConfirmDetails(summary, true));
            Assert.Equal("Nothing changes until Apply.\nplace 2, skip 1", RunTexts.ConfirmContent(summary, false));
            Assert.Null(RunTexts.ConfirmDetails(summary, false));
        }

        [Fact]
        public void R382_a_body_that_throws_rolls_back_deletes_its_temporary_files_and_leaves_the_step_line()
        {
            ButtonBody body = Body(context =>
            {
                context.WriteText("plan.json", "half");
                throw new InvalidOperationException("the link type would not load");
            });

            RunOutcome outcome = Flow().Run(4, Label(4), body, Document);

            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.Equal("4 Place villas changed nothing, the model is as it was. The run failed: the link type would not load", outcome.Message);
            Assert.Equal(1, model.RollBacks);
            Assert.Equal(0, model.Commits);
            Assert.Empty(FilesUnderWork());
            Assert.Empty(state.Records);
        }

        [Fact]
        public void R382_a_read_back_that_does_not_match_rolls_back()
        {
            RunOutcome outcome = Flow().Run(4, Label(4), Body(readBack: context => "plot 2290 sits 12 mm off its plan"), Document);

            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.Equal("4 Place villas changed nothing, the model is as it was. The read back does not match: plot 2290 sits 12 mm off its plan", outcome.Message);
            Assert.Equal(1, model.RollBacks);
            Assert.Equal(0, model.Commits);
            Assert.Empty(FilesUnderWork());
            Assert.Empty(state.Records);
        }

        [Fact]
        public void R382_a_commit_that_does_not_end_committed_rolls_back_with_the_errors_revit_gave()
        {
            model.CommitEnds = false;
            model.ErrorsToGive.Add("Can't make extrusion.");

            RunOutcome outcome = Flow().Run(4, Label(4), Body(), Document);

            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.Equal("4 Place villas changed nothing, the model is as it was. Revit refused the change: Can't make extrusion.", outcome.Message);
            Assert.Empty(FilesUnderWork());
            Assert.Empty(state.Records);

            model.ErrorsToGive.Clear();
            Assert.Equal("4 Place villas changed nothing, the model is as it was. Revit did not commit the change.", Flow().Run(4, Label(4), Body(), Document).Message);

            model.CommitThrows = true;
            RunOutcome thrown = Flow().Run(4, Label(4), Body(), Document);
            Assert.Equal(RunEnd.RolledBack, thrown.End);
            Assert.Equal("4 Place villas changed nothing, the model is as it was. The run failed: the document is read only", thrown.Message);
            Assert.Empty(FilesUnderWork());

            model.StartThrows = true;
            RunOutcome unstarted = Flow().Run(4, Label(4), Body(), Document);
            Assert.Equal(RunEnd.RolledBack, unstarted.End);
            Assert.Equal("4 Place villas changed nothing, the model is as it was. The run failed: Revit did not start the transaction", unstarted.Message);
            Assert.Empty(state.Records);
        }

        [WindowsFact]
        public void R382_a_move_onto_a_file_another_program_holds_leaves_the_step_not_done_and_names_the_file()
        {
            string levels = Path.Combine(work, "levels.xlsx");
            File.WriteAllText(levels, "the earlier levels");
            ButtonBody body = Body(context =>
            {
                context.WriteText("plan.json", "real plan");
                context.WriteText("levels.xlsx", "new levels");
                return new string[0];
            });

            RunOutcome outcome;
            using (new FileStream(levels, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                outcome = Flow().Run(6, Label(6), body, Document);
            }

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Equal("6 Levels changed the model, but levels.xlsx was not saved: another program holds it open. Close it and run the button again.", outcome.Message);
            Assert.Equal("the earlier levels", File.ReadAllText(levels));
            Assert.Equal("new levels", File.ReadAllText(WorkFiles.TemporaryName(levels, RunId)));
            Assert.Equal("real plan", File.ReadAllText(Path.Combine(work, "plan.json")));
            RunRecord record = Assert.Single(state.Records).Value;
            Assert.False(record.StepDone);
            Assert.Equal(new[] { levels }, record.FailedMoves);
            Assert.Equal(new[] { Path.Combine(work, "plan.json") }, record.Outputs.Keys);
        }

        [Fact]
        public void R382_a_file_input_is_hashed_by_its_content_and_a_folder_input_by_its_file_list()
        {
            Assert.Equal(FileHash.Sha256(curbs), InputHash.Of(new RunInput("Curbs", curbs, false)));

            string folder = Path.Combine(root, "surfaces");
            Directory.CreateDirectory(folder);
            string surface = Path.Combine(folder, "a.xml");
            File.WriteAllText(surface, "abc");
            DateTime written = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(surface, written);
            string first = InputHash.Of(new RunInput("Road surface", folder, true));

            File.WriteAllText(surface, "xyz");
            File.SetLastWriteTimeUtc(surface, written);
            Assert.Equal(first, InputHash.OfFolder(folder));

            File.SetLastWriteTimeUtc(surface, written.AddSeconds(1));
            Assert.NotEqual(first, InputHash.OfFolder(folder));
            File.SetLastWriteTimeUtc(surface, written);
            File.WriteAllText(Path.Combine(folder, "b.xml"), "b");
            Assert.NotEqual(first, InputHash.OfFolder(folder));
        }

        [Fact]
        public void R382_the_flow_lives_in_common_with_no_revit_reference_and_a_run_id_names_files()
        {
            string id = RunIds.New(4, new DateTime(2026, 10, 28, 10, 18, 0, 123));
            Assert.Equal("4_20261028-101800-123", id);
            Assert.True(WorkerFiles.IsRunId(id));

            System.Reflection.Assembly common = typeof(ButtonFlow).Assembly;
            Assert.Equal("SiteAdaptation.Common", common.GetName().Name);
            Assert.All(new[] { typeof(RunContext), typeof(RunTexts), typeof(InputHash), typeof(IRunState) }, type => Assert.Same(common, type.Assembly));
            Assert.DoesNotContain(common.GetReferencedAssemblies(), name => name.Name.StartsWith("RevitAPI", StringComparison.Ordinal) || name.Name.StartsWith("Autodesk", StringComparison.Ordinal));
            string flow = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.Common", "Flow");
            Assert.All(Directory.GetFiles(flow, "*.cs"), path => Assert.DoesNotMatch(new Regex(@"\bAutodesk\b|\bRibbonLayout\b"), File.ReadAllText(path)));        }

        private sealed class FakeState : IRunState
        {
            private readonly string work;

            public FakeState(string work)
            {
                this.work = work;
            }

            public List<RunInput> Inputs { get; } = new List<RunInput>();
            public List<KeyValuePair<int, RunRecord>> Records { get; } = new List<KeyValuePair<int, RunRecord>>();
            public int Missing { get; set; }
            public bool DryRunFirst { get; set; }

            public string WorkFolder(string documentPath)
            {
                return work;
            }

            public int MissingStep(int button)
            {
                return Missing;
            }

            IReadOnlyList<RunInput> IRunState.Inputs(int button)
            {
                return Inputs;
            }

            public void Committed(int button, RunRecord record)
            {
                Records.Add(new KeyValuePair<int, RunRecord>(button, record));
            }
        }

        private sealed class FakeModel : IModel
        {
            public List<string> Started { get; } = new List<string>();
            public List<string> WarningsToGive { get; } = new List<string>();
            public List<string> ErrorsToGive { get; } = new List<string>();
            public bool CommitEnds { get; set; } = true;
            public bool CommitThrows { get; set; }
            public bool StartThrows { get; set; }
            public int Commits { get; set; }
            public int RollBacks { get; set; }

            public IModelTransaction Start(string name)
            {
                if (StartThrows)
                {
                    throw new InvalidOperationException("Revit did not start the transaction");
                }
                Started.Add(name);
                return new Transaction(this);
            }

            private sealed class Transaction : IModelTransaction
            {
                private readonly FakeModel model;

                public Transaction(FakeModel model)
                {
                    this.model = model;
                }

                public IReadOnlyList<string> Warnings
                {
                    get { return model.WarningsToGive; }
                }

                public IReadOnlyList<string> Errors
                {
                    get { return model.ErrorsToGive; }
                }

                public bool Commit()
                {
                    if (model.CommitThrows)
                    {
                        throw new InvalidOperationException("the document is read only");
                    }
                    if (model.CommitEnds)
                    {
                        model.Commits++;
                    }
                    return model.CommitEnds;
                }

                public void RollBack()
                {
                    model.RollBacks++;
                }

                public void Dispose()
                {
                }
            }
        }

        private sealed class FakeConfirm : IConfirm
        {
            public sealed class Call
            {
                public string Label;
                public RunSummary Summary;
                public bool DryRun;
            }

            public List<Call> Asked { get; } = new List<Call>();
            public bool Answer { get; set; } = true;
            public Action OnAsk { get; set; }

            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                Asked.Add(new Call { Label = label, Summary = summary, DryRun = dryRun });
                OnAsk?.Invoke();
                return Answer;
            }
        }
    }
}
