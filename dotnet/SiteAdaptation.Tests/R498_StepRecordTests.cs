using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R498, Q384. Buttons 2 to 9 record their run in the Settings entity, so a reopen shows each step as it was saved, on net48 and net8.0.
    // The entity and the model are fakes that take a roll back as Revit does, the Revit side of the group is what a run in Revit proves
    public sealed class R498_StepRecordTests : IDisposable
    {
        private const string Document = @"C:\Models\Site.rvt";
        private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private readonly string root = Path.Combine(Path.GetTempPath(), "r498_" + Guid.NewGuid().ToString("N"));
        private readonly string input;
        private readonly Entity entity = new Entity();
        private readonly ProjectSettings session = new ProjectSettings { Scope = new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "012" } };
        private readonly FakeState state;
        private readonly FakeModel model;
        private DateTime now = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc);

        public R498_StepRecordTests()
        {
            Directory.CreateDirectory(root);
            input = Path.Combine(root, "in.dwg");
            File.WriteAllText(input, "in");
            state = new FakeState(root, input);
            model = new FakeModel(entity);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private RunOutcome Press(int button, ButtonBody body, Func<IModel, IModel> modelOf = null)
        {
            now = now.AddMinutes(1);
            var recorder = new StepRecorder(() => session, entity, () => now);
            IModel used = modelOf == null ? model : modelOf(model);
            return new ButtonFlow(state, used, new Yes(), run => Hash, () => button + "_run", null, recorder).Run(button, button + " Step", body, Document);
        }

        private static ButtonBody InTransaction(params string[] issues)
        {
            return new ButtonBody { Preview = context => new RunSummary("c", null), Apply = context => issues };
        }

        private static ButtonBody Worker(params string[] issues)
        {
            return new ButtonBody
            {
                Preview = context => new RunSummary("c", null),
                Start = (context, end) => end(RunLater.Done("done", issues, new Dictionary<string, string>())),
            };
        }

        private RunOutcome PressWorker(int button, params string[] issues)
        {
            now = now.AddMinutes(1);
            var recorder = new StepRecorder(() => session, entity, () => now);
            RunOutcome ended = null;
            new ButtonFlow(state, model, new Yes(), run => Hash, () => button + "_run", null, recorder).Run(button, button + " Step", Worker(issues), Document, later => ended = later);
            return ended;
        }

        private ProjectSettings Held()
        {
            return new SettingsStore(entity).Load();
        }

        [Fact]
        public void R498_a_step_in_a_transaction_writes_its_run_before_that_transaction_commits()
        {
            RunOutcome outcome = Press(5, new ButtonBody
            {
                Preview = context => new RunSummary("c", null),
                Apply = context => new[] { "one issue" },
                ReadBack = context =>
                {
                    Assert.Equal(1, model.Open);
                    return null;
                },
            });

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Equal(1, model.Started);
            ButtonRun run = Held().LastRuns["5"];
            Assert.Equal(new[] { "one issue" }, run.Issues);
            Assert.Equal(Hash, run.InputHashes["Curbs"]);
            Assert.Equal("012", run.Scope.Nbh);
            Assert.Equal(RunModes.AfterDryRun, run.Mode);
        }

        [Fact]
        public void R498_a_commit_that_fails_leaves_no_record()
        {
            model.CommitAnswers.Enqueue(false);

            RunOutcome outcome = Press(5, InTransaction());

            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.Null(entity.Read());
        }

        [Fact]
        public void R498_a_read_back_that_differs_writes_no_record()
        {
            RunOutcome outcome = Press(5, new ButtonBody { Preview = context => new RunSummary("c", null), Apply = context => new string[0], ReadBack = context => "differs" });

            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.Null(entity.Read());
        }

        [Fact]
        public void R498_a_worker_step_records_in_a_small_transaction_of_its_own()
        {
            RunOutcome outcome = PressWorker(2, "one issue");

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Equal("done", outcome.Message);
            Assert.Equal(1, model.Started);
            Assert.Equal(new[] { "one issue" }, Held().LastRuns["2"].Issues);
        }

        [Fact]
        public void R498_a_worker_step_whose_record_fails_stays_done_and_says_so()
        {
            model.CommitAnswers.Enqueue(false);

            RunOutcome outcome = PressWorker(4);

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.NotNull(outcome.Record);
            Assert.StartsWith("done The step is done", outcome.Message);
            Assert.Null(entity.Read());
        }

        // The trap of this rule: the settings of a session keep only the runs of when they loaded
        [Fact]
        public void R498_each_record_keeps_the_runs_a_stale_session_does_not_hold()
        {
            PressWorker(2);
            Press(3, InTransaction());
            PressWorker(4);
            Press(5, InTransaction());

            Assert.Empty(session.LastRuns);
            Assert.Equal(new[] { "2", "3", "4", "5" }, Held().LastRuns.Keys);
        }

        [Fact]
        public void R498_save_setup_keeps_the_runs_the_model_holds()
        {
            PressWorker(2);
            Press(3, InTransaction());

            SetupWrite.Write(new ProjectSettings { ConfigFile = @"C:\c.json" }, model, entity);

            Assert.Equal(@"C:\c.json", Held().ConfigFile);
            Assert.Equal(new[] { "2", "3" }, Held().LastRuns.Keys);
        }

        [Fact]
        public void R498_settings_the_reader_refuses_do_not_stop_a_record()
        {
            entity.Write("{ not json");

            RunOutcome outcome = Press(5, InTransaction());

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Equal(new[] { "5" }, Held().LastRuns.Keys);
        }

        [Fact]
        public void R498_the_warnings_of_the_commit_come_back_on_a_reopen_in_one_undo_entry()
        {
            model.WarningsOfNext = new[] { "w1", "w2", "w3" };

            RunOutcome outcome = Press(5, InTransaction("one issue"));

            Assert.Equal(4, outcome.Record.Issues.Count);
            Assert.Equal(2, model.Started);
            Assert.Equal(1, model.GroupsKept);
            Assert.Equal(0, model.GroupsRolledBack);
            Assert.Equal(outcome.Record.Issues.Count, StepRun.FromSettings(Held(), 5).Issues);
            Assert.Equal(new[] { "one issue" }, Held().LastRuns["5"].Issues);
        }

        [Fact]
        public void R498_no_warning_asks_for_no_second_transaction()
        {
            Press(5, InTransaction());

            Assert.Equal(1, model.Started);
            Assert.Equal(1, model.GroupsKept);
        }

        [Fact]
        public void R498_a_rolled_back_run_rolls_back_its_group()
        {
            model.CommitAnswers.Enqueue(false);

            Press(5, InTransaction());

            Assert.Equal(0, model.GroupsKept);
            Assert.Equal(1, model.GroupsRolledBack);
        }

        // 03a Steps: a run whose output did not move leaves its step not done, also after a reopen, and the run before it stands
        [WindowsFact]
        public void R498_a_failed_move_puts_back_the_run_before()
        {
            Press(5, InTransaction("first"));
            string first = Held().LastRuns["5"].Time;
            string output = Path.Combine(root, "floors.json");
            File.WriteAllText(output, "held");

            RunOutcome outcome;
            using (new FileStream(output, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                outcome = Press(5, new ButtonBody
                {
                    Preview = context => new RunSummary("c", null),
                    Apply = context =>
                    {
                        context.WriteText("floors.json", "new");
                        return new string[0];
                    },
                });
            }

            Assert.False(outcome.Record.StepDone);
            Assert.Equal(first, Held().LastRuns["5"].Time);
        }

        [WindowsFact]
        public void R498_a_failed_first_run_leaves_no_record()
        {
            string output = Path.Combine(root, "floors.json");
            File.WriteAllText(output, "held");

            using (new FileStream(output, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Press(5, new ButtonBody
                {
                    Preview = context => new RunSummary("c", null),
                    Apply = context =>
                    {
                        context.WriteText("floors.json", "new");
                        return new string[0];
                    },
                });
            }

            Assert.False(Held().LastRuns.ContainsKey("5"));
        }

        [Fact]
        public void R498_button_1_and_a_button_with_no_number_are_left_to_their_own()
        {
            Press(1, InTransaction());
            Press(0, InTransaction());

            Assert.Null(entity.Read());
            Assert.Equal(0, model.GroupsKept);
        }

        // Q384. The whole point: every step after a reopen, read from the entity alone, as StepStates reads it in the pane
        [Fact]
        public void R498_a_reopen_shows_all_nine_steps_as_they_were_saved()
        {
            Press(1, new ButtonBody
            {
                Preview = context => new RunSummary("c", null),
                Apply = context =>
                {
                    new SettingsStore(entity).Record(session, 1, new ButtonRun { Time = "2026-11-03T07:00:00.000Z", Scope = new SettingsScope(), InputHashes = new SortedDictionary<string, string> { { "Curbs", Hash } } }, RunKind.Real, context.Transaction);
                    return new string[0];
                },
            });
            PressWorker(2);
            Press(3, InTransaction());
            PressWorker(4, "row one", "row two");
            model.WarningsOfNext = new[] { "w1" };
            Press(5, InTransaction());
            Press(6, InTransaction());
            Press(7, InTransaction());
            Press(8, InTransaction());
            Press(9, InTransaction());

            ProjectSettings reopened = SettingsJson.Read(entity.Read());
            string hashNow = StepHash.Combine(new Dictionary<string, string> { { "Curbs", Hash } });
            var steps = Enumerable.Range(1, StepStates.Count).ToDictionary(step => step, step => new StepNow(StepRun.FromSettings(reopened, step), hashNow, null));
            IReadOnlyList<StepLine> lines = StepStates.Of(true, reopened.Scope, steps);

            Assert.Equal(new[] { StepStateKind.Done, StepStateKind.Done, StepStateKind.Done, StepStateKind.DoneWithIssues, StepStateKind.DoneWithIssues, StepStateKind.Done, StepStateKind.Done, StepStateKind.Done, StepStateKind.Done }, lines.Select(line => line.Kind));
            Assert.Equal(2, lines[3].Value);
            Assert.Equal(1, lines[4].Value);
        }

        private sealed class Yes : IConfirm
        {
            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                return true;
            }
        }

        private sealed class FakeState : IRunState
        {
            private readonly string work;
            private readonly string input;

            public FakeState(string work, string input)
            {
                this.work = work;
                this.input = input;
            }

            public bool DryRunFirst
            {
                get { return false; }
            }

            public string WorkFolder(string documentPath)
            {
                return work;
            }

            public int MissingStep(int button)
            {
                return 0;
            }

            public IReadOnlyList<RunInput> Inputs(int button)
            {
                return new[] { new RunInput("Curbs", input, false) };
            }

            public void Committed(int button, RunRecord record)
            {
            }
        }

        private sealed class Entity : ISettingsEntity
        {
            public string Json { get; set; }

            public string Read()
            {
                return Json;
            }

            public void Write(string json)
            {
                Json = json;
            }
        }

        // A transaction takes back the entity on a roll back or a dispose while open, as Revit does
        private sealed class FakeModel : IModel, IModelGroups
        {
            private readonly Entity entity;

            public FakeModel(Entity entity)
            {
                this.entity = entity;
            }

            public int Started { get; private set; }
            public int Open { get; set; }
            public int GroupsKept { get; private set; }
            public int GroupsRolledBack { get; private set; }
            public Queue<bool> CommitAnswers { get; } = new Queue<bool>();
            public IReadOnlyList<string> WarningsOfNext { get; set; } = new string[0];

            public IModelTransaction Start(string name)
            {
                Started++;
                IReadOnlyList<string> warnings = WarningsOfNext;
                WarningsOfNext = new string[0];
                return new FakeTransaction(this, entity, warnings);
            }

            public IModelGroup StartGroup(string name)
            {
                return new FakeGroup(this);
            }

            private sealed class FakeGroup : IModelGroup
            {
                private readonly FakeModel model;
                private bool ended;

                public FakeGroup(FakeModel model)
                {
                    this.model = model;
                }

                public void Keep()
                {
                    ended = true;
                    model.GroupsKept++;
                }

                public void RollBack()
                {
                    ended = true;
                    model.GroupsRolledBack++;
                }

                public void Dispose()
                {
                    if (!ended)
                    {
                        RollBack();
                    }
                }
            }

            private sealed class FakeTransaction : IModelTransaction
            {
                private readonly FakeModel model;
                private readonly Entity entity;
                private readonly string before;
                private bool open = true;

                public FakeTransaction(FakeModel model, Entity entity, IReadOnlyList<string> warnings)
                {
                    this.model = model;
                    this.entity = entity;
                    before = entity.Json;
                    Warnings = warnings;
                    model.Open++;
                }

                public IReadOnlyList<string> Warnings { get; }
                public IReadOnlyList<string> Errors { get; } = new string[0];

                public bool Commit()
                {
                    if (model.CommitAnswers.Count > 0 && !model.CommitAnswers.Dequeue())
                    {
                        return false;
                    }
                    open = false;
                    model.Open--;
                    return true;
                }

                public void RollBack()
                {
                    if (open)
                    {
                        entity.Json = before;
                        open = false;
                        model.Open--;
                    }
                }

                public void Dispose()
                {
                    RollBack();
                }
            }
        }
    }
}
