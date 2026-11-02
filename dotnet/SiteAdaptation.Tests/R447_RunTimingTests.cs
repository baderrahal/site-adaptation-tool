using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Report;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R447. The time of a button run per step and category, driven through the flow of R382 by fake bodies, a fake model and a clock the
    // test moves, so each number is known before the run. The fake transaction reports as RevitTransaction and WarningCollector do
    public sealed class R447_RunTimingTests : IDisposable
    {
        private const string RunId = "7_20261102-101800-000";
        private const string Document = @"C:\Models\Site.rvt";
        private const string Label = "7 Place villas";
        private static readonly string[] Categories = { "revitStart", "modelOpen", "reading", "transactions", "modelRefresh", "warnings", "saving", "workerWait", "other" };

        private readonly string root = Path.Combine(Path.GetTempPath(), "sa-timing-" + Guid.NewGuid().ToString("N"));
        private readonly string work;
        private double now;

        public R447_RunTimingTests()
        {
            work = Path.Combine(root, "work");
            Directory.CreateDirectory(work);
            RunTiming.TakeBeforeRun(out _, out _);
        }

        public void Dispose()
        {
            RunTiming.TakeBeforeRun(out _, out _);
            Directory.Delete(root, true);
        }

        private RunTiming Timing()
        {
            return new RunTiming(() => now);
        }

        private ButtonFlow Flow(RunTiming timing, State state = null, Model model = null, bool apply = true)
        {
            return new ButtonFlow(state ?? new State(work), model ?? new Model(this), new Confirm(this, apply), input => "hash", () => RunId, timing);
        }

        private ButtonBody Body(Func<RunContext, IReadOnlyList<string>> apply = null)
        {
            return new ButtonBody
            {
                Preview = context =>
                {
                    now += 2;
                    return new RunSummary("place 1", null);
                },
                Apply = apply ?? (context =>
                {
                    now += 10;
                    RunTiming.Measure(RunTiming.ModelRefresh, () => now += 3);
                    RunTiming.Measure(RunTiming.WorkerWait, () => now += 4);
                    return new string[0];
                }),
                ReadBack = context =>
                {
                    now += 1;
                    return null;
                },
            };
        }

        private ButtonBody LaterBody(Action<RunContext, Action<RunLater>> start)
        {
            return new ButtonBody { Preview = context => new RunSummary(null, null), Start = start };
        }

        private JObject Json()
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(work, "run_timing_" + RunId + ".json")));
        }

        private static JObject StepOf(JObject json, string name)
        {
            return ((JArray)json["steps"]).Cast<JObject>().Single(step => (string)step["step"] == name);
        }

        private static long Milliseconds(JToken seconds)
        {
            return (long)Math.Round((double)seconds * 1000);
        }

        // A row adds up to its total, a category that says absent counting for nothing
        private static void AssertAddsUp(JObject row, JToken total = null)
        {
            long sum = Categories.Where(name => row[name].Type != JTokenType.String).Sum(name => Milliseconds(row[name]));
            Assert.Equal(sum, Milliseconds(total ?? row["totalSeconds"]));
        }

        [Fact]
        public void R447_the_categories_of_each_step_add_up_to_its_total_and_the_steps_to_the_run()
        {
            RunOutcome outcome = Flow(Timing()).Run(7, Label, Body(), Document);

            Assert.Equal(RunEnd.Committed, outcome.End);
            JObject json = Json();
            Assert.Equal(1, (int)json["formatVersion"]);
            Assert.Equal(RunId, (string)json["runId"]);
            Assert.Equal(7, (int)json["button"]);
            Assert.Equal(Label, (string)json["label"]);
            Assert.Equal("Committed", (string)json["end"]);

            JObject[] steps = ((JArray)json["steps"]).Cast<JObject>().Where(step => (string)step["step"] != "start").ToArray();
            Assert.Equal(new[] { "prepare", "hashes", "preview", "confirm", "apply", "readBack", "commit", "outputs" }, steps.Select(step => (string)step["step"]));
            foreach (JObject step in steps)
            {
                AssertAddsUp(step);
            }
            Assert.Equal(2, (double)StepOf(json, "preview")["reading"]);
            Assert.Equal(5, (double)StepOf(json, "confirm")["other"]);
            JObject apply = StepOf(json, "apply");
            Assert.Equal(17, (double)apply["totalSeconds"]);
            Assert.Equal(3, (double)apply["modelRefresh"]);
            Assert.Equal(4, (double)apply["workerWait"]);
            Assert.Equal(10, (double)apply["other"]);
            Assert.Equal(1, (double)StepOf(json, "readBack")["reading"]);

            // The preprocessor runs inside the commit, so its time leaves the commit and no second counts twice
            JObject commit = StepOf(json, "commit");
            Assert.Equal(8, (double)commit["totalSeconds"]);
            Assert.Equal(6, (double)commit["transactions"]);
            Assert.Equal(2, (double)commit["warnings"]);
            Assert.Equal(0, (double)commit["other"]);

            Assert.Equal(steps.Sum(step => Milliseconds(step["totalSeconds"])), Milliseconds(json["totalSeconds"]));
            Assert.Equal(33, (double)json["totalSeconds"]);
            AssertAddsUp((JObject)json["categories"], json["totalSeconds"]);
            Assert.Equal(6, (double)json["categories"]["transactions"]);
        }

        [Fact]
        public void R447_what_nothing_measured_says_absent_and_never_0()
        {
            Flow(Timing()).Run(7, Label, Body(), Document);

            JObject json = Json();
            JObject start = StepOf(json, "start");
            Assert.Equal("absent", (string)start["revitStart"]);
            Assert.Equal("absent", (string)start["modelOpen"]);
            Assert.Equal("absent", (string)start["totalSeconds"]);
            Assert.Equal("absent", (string)start["other"]);
            Assert.Equal("absent", (string)json["categories"]["revitStart"]);
            Assert.Equal("absent", (string)json["categories"]["modelOpen"]);
            Assert.Equal("absent", (string)json["categories"]["saving"]);
            Assert.Equal("absent", (string)StepOf(json, "preview")["transactions"]);
            Assert.Equal("absent", (string)StepOf(json, "preview")["saving"]);
        }

        [Fact]
        public void R447_the_model_open_of_the_caller_goes_to_the_next_run_that_writes_and_revit_start_stays_absent()
        {
            RunTiming.BeforeRun(null, 40.5);

            // A stop before the run id writes nothing, so it does not use up the open
            Flow(Timing()).Run(7, Label, new ButtonBody { NotBuilt = "not built" }, Document);
            Assert.Empty(Directory.GetFiles(work));

            Flow(Timing()).Run(7, Label, Body(), Document);
            JObject first = Json();
            JObject start = StepOf(first, "start");
            Assert.Equal(40.5, (double)start["modelOpen"]);
            Assert.Equal(40.5, (double)start["totalSeconds"]);
            Assert.Equal("absent", (string)start["revitStart"]);
            Assert.Equal(0, (double)start["other"]);
            AssertAddsUp(start);
            Assert.Equal(33 + 40.5, (double)first["totalSeconds"]);
            AssertAddsUp((JObject)first["categories"], first["totalSeconds"]);

            // One open is told once, so a sum over the runs of a job counts it once
            Flow(Timing()).Run(7, Label, Body(), Document);
            Assert.Equal("absent", (string)StepOf(Json(), "start")["modelOpen"]);
        }

        [Fact]
        public void R447_a_later_call_of_the_hook_keeps_what_an_earlier_call_gave()
        {
            RunTiming.BeforeRun(null, 40.5);
            RunTiming.BeforeRun(12.25, null);

            Flow(Timing()).Run(7, Label, Body(), Document);

            JObject start = StepOf(Json(), "start");
            Assert.Equal(12.25, (double)start["revitStart"]);
            Assert.Equal(40.5, (double)start["modelOpen"]);
            Assert.Equal(52.75, (double)start["totalSeconds"]);
        }

        [Fact]
        public void R447_a_failed_write_changes_nothing_in_the_outcome_nor_the_record()
        {
            var bare = new State(work);
            RunOutcome without = Flow(null, bare).Run(7, Label, Body(), Document);

            // A folder on each name, so both writes fail
            Directory.CreateDirectory(Path.Combine(work, "run_timing_" + RunId + ".json"));
            Directory.CreateDirectory(Path.Combine(work, "run_" + RunId + ".log"));
            var timed = new State(work);
            RunOutcome with = Flow(Timing(), timed).Run(7, Label, Body(), Document);

            Assert.Equal(without.End, with.End);
            Assert.Equal(without.Message, with.Message);
            Assert.Equal(without.Record.RunId, with.Record.RunId);
            Assert.Equal(without.Record.Issues, with.Record.Issues);
            Assert.Equal(without.Record.Outputs, with.Record.Outputs);
            Assert.Single(bare.Records);
            Assert.Single(timed.Records);
        }

        [Fact]
        public void R447_a_clock_that_throws_changes_nothing_in_the_outcome()
        {
            var timing = new RunTiming(() => throw new InvalidOperationException("the clock failed"));

            RunOutcome outcome = Flow(timing).Run(7, Label, Body(), Document);

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Empty(Directory.GetFiles(work, "run_*"));
        }

        [Fact]
        public void R447_a_worker_run_writes_at_its_start_and_ends_with_the_wait_in_a_step_of_its_own()
        {
            Action<RunLater> end = null;
            var ended = new List<RunOutcome>();
            ButtonBody body = LaterBody((context, taken) =>
            {
                now += 1;
                end = taken;
            });

            RunOutcome outcome = Flow(Timing()).Run(2, "2 Read data", body, Document, ended.Add);

            Assert.Equal(RunEnd.Started, outcome.End);
            // A worker that never ends, Revit closed or killed, still leaves the steps before it
            Assert.Equal("Started", (string)Json()["end"]);
            Assert.Equal(1, (double)StepOf(Json(), "workerStart")["other"]);
            Assert.Empty(ended);

            now += 300;
            end(RunLater.Done(null, null, null));

            RunOutcome later = Assert.Single(ended);
            Assert.Equal(RunEnd.Committed, later.End);
            JObject json = Json();
            Assert.Equal("Committed", (string)json["end"]);
            JObject worker = StepOf(json, "worker");
            Assert.Equal(300, (double)worker["workerWait"]);
            Assert.Equal(300, (double)worker["totalSeconds"]);
            Assert.Equal("absent", (string)worker["transactions"]);
            Assert.Equal(300, (double)json["categories"]["workerWait"]);
            AssertAddsUp(worker);
            AssertAddsUp((JObject)json["categories"], json["totalSeconds"]);
            Assert.Equal(((JArray)json["steps"]).Count, File.ReadAllLines(Path.Combine(work, "run_" + RunId + ".log")).Length);
        }

        // As button 2 with a scan and button 4 with its window: the wait of the user is its own step, and the worker that comes after the
        // window is a second step under the name of the first
        [Fact]
        public void R447_a_window_after_a_worker_is_not_worker_wait_and_a_second_worker_adds_to_the_wait()
        {
            Action<RunLater> end = null;
            RunContext run = null;
            ButtonBody body = LaterBody((context, taken) =>
            {
                run = context;
                end = taken;
            });

            Flow(Timing()).Run(4, "4 Check", body, Document, outcome => { });
            now += 100;
            run.Timing.NextStep(RunTiming.WorkerEndStep, RunTiming.Reading);
            now += 1;
            run.Timing.NextStep(RunTiming.WindowStep);
            now += 60;
            run.Timing.NextStep(RunTiming.WorkerStep, RunTiming.WorkerWait);
            now += 20;
            end(RunLater.Done(null, null, null));

            JObject json = Json();
            Assert.Equal(new[] { "start", "prepare", "hashes", "preview", "confirm", "workerStart", "worker", "workerEnd", "window", "worker" },
                ((JArray)json["steps"]).Select(step => (string)step["step"]));
            Assert.Equal(120, (double)json["categories"]["workerWait"]);
            Assert.Equal(1, (double)json["categories"]["reading"]);
            Assert.Equal(60, (double)StepOf(json, "window")["other"]);
            AssertAddsUp((JObject)json["categories"], json["totalSeconds"]);
        }

        [Fact]
        public void R447_a_step_asked_again_while_it_runs_goes_on_with_no_second_line()
        {
            RunContext run = null;
            ButtonBody body = LaterBody((context, taken) =>
            {
                run = context;
                context.Timing.NextStep(RunTiming.WorkerStep, RunTiming.WorkerWait);
                now += 2;
            });

            Flow(Timing()).Run(2, "2 Read data", body, Document, outcome => { });

            JObject worker = StepOf(Json(), "worker");
            Assert.Equal(2, (double)worker["workerWait"]);
            Assert.NotNull(run);
        }

        [Fact]
        public void R447_a_worker_that_ends_before_its_start_returns_and_a_failed_one_are_written_with_their_end()
        {
            var ended = new List<RunOutcome>();
            ButtonBody body = LaterBody((context, taken) =>
            {
                now += 1;
                taken(RunLater.Failed("the worker failed"));
            });

            RunOutcome outcome = Flow(Timing()).Run(2, "2 Read data", body, Document, ended.Add);

            Assert.Equal(RunEnd.Started, outcome.End);
            Assert.Equal(RunEnd.Stopped, Assert.Single(ended).End);
            JObject json = Json();
            Assert.Equal("Stopped", (string)json["end"]);
            Assert.DoesNotContain(((JArray)json["steps"]).Cast<JObject>(), step => (string)step["step"] == "worker");
        }

        [Fact]
        public void R447_the_run_log_holds_one_line_per_step_in_seconds_with_the_word_absent()
        {
            RunTiming.BeforeRun(null, 40.5);

            Flow(Timing()).Run(7, Label, Body(), Document);

            JObject json = Json();
            string[] lines = File.ReadAllLines(Path.Combine(work, "run_" + RunId + ".log"));
            JArray steps = (JArray)json["steps"];
            Assert.Equal(steps.Count, lines.Length);
            Assert.Equal("timing " + RunId + " start total=40.500 revitStart=absent modelOpen=40.500 reading=absent transactions=absent modelRefresh=absent warnings=absent saving=absent workerWait=absent other=0.000", lines[0]);
            Assert.Equal("timing " + RunId + " apply total=17.000 revitStart=absent modelOpen=absent reading=absent transactions=absent modelRefresh=3.000 warnings=absent saving=absent workerWait=4.000 other=10.000",
                lines.Single(line => line.Contains(" apply ")));
            for (int index = 0; index < lines.Length; index++)
            {
                Assert.StartsWith("timing " + RunId + " " + (string)steps[index]["step"] + " total=", lines[index]);
            }
        }

        [Fact]
        public void R447_a_cancelled_run_and_a_rolled_back_run_write_their_end_in_the_work_folder()
        {
            var dry = new State(work) { DryRunFirst = true };
            Assert.Equal(RunEnd.Cancelled, Flow(Timing(), dry, null, false).Run(7, Label, Body(), Document).End);
            Assert.Equal("Cancelled", (string)Json()["end"]);

            ButtonBody failing = Body(context => throw new InvalidOperationException("no"));
            Assert.Equal(RunEnd.RolledBack, Flow(Timing()).Run(7, Label, failing, Document).End);
            Assert.Equal("RolledBack", (string)Json()["end"]);
        }

        [Fact]
        public void R447_a_body_outside_the_transaction_has_its_open_and_its_close_as_steps()
        {
            ButtonBody body = Body();
            body.Outside = context =>
            {
                now += 7;
                return new Closing(this);
            };

            Flow(Timing()).Run(7, Label, body, Document);

            JObject json = Json();
            Assert.Equal(7, (double)StepOf(json, "outside")["other"]);
            Assert.Equal(9, (double)StepOf(json, "outsideEnd")["other"]);
        }

        [Fact]
        public void R447_a_run_that_throws_past_the_flow_still_throws_and_writes_its_end()
        {
            var state = new State(work) { ThrowOnCommitted = true };

            Assert.Throws<InvalidOperationException>(() => Flow(Timing(), state).Run(7, Label, Body(), Document));

            Assert.Equal("Threw", (string)Json()["end"]);
            RunTiming.Measure(RunTiming.ModelRefresh, () => now += 1);
            Assert.Equal("Threw", (string)Json()["end"]);
        }

        [Fact]
        public void R447_a_measure_with_no_run_only_runs_the_work_and_a_failure_of_the_work_goes_on()
        {
            int ran = 0;
            RunTiming.Measure(RunTiming.Transactions, () => { ran++; });
            Assert.Equal(1, ran);
            Assert.Equal(5, RunTiming.Measure(RunTiming.Transactions, () => 5));
            Assert.Throws<InvalidOperationException>(() => RunTiming.Measure(RunTiming.Transactions, () => { throw new InvalidOperationException("the work failed"); }));

            ButtonBody body = Body(context =>
            {
                Assert.Throws<InvalidOperationException>(() => RunTiming.Measure(RunTiming.ModelRefresh, () =>
                {
                    now += 3;
                    throw new InvalidOperationException("the work failed");
                }));
                now += 1;
                return new string[0];
            });
            Flow(Timing()).Run(7, Label, body, Document);

            JObject apply = StepOf(Json(), "apply");
            Assert.Equal(3, (double)apply["modelRefresh"]);
            Assert.Equal(1, (double)apply["other"]);
        }

        [Fact]
        public void R447_the_flow_with_no_timing_writes_no_timing_file()
        {
            Flow(null).Run(7, Label, Body(), Document);

            Assert.Empty(Directory.GetFiles(work, "run_*"));
        }

        // 9 Report takes the last run_<button>_*.json as a run log of R381, which neither file of a timing may be taken for
        [Fact]
        public void R447_neither_file_is_taken_for_a_run_log_by_9_report()
        {
            Flow(Timing()).Run(7, Label, Body(), Document);

            Assert.True(File.Exists(Path.Combine(work, "run_timing_" + RunId + ".json")));
            Assert.True(File.Exists(Path.Combine(work, "run_" + RunId + ".log")));
            Assert.Null(ReportRun.RunLog.Last(work, 7, WorkFiles.ReadText));
        }

        // The Revit side has no test without Revit, so its source is held to the calls that report: a new Regenerate that skipped the
        // measure would read as other in every later speed fix
        [Fact]
        public void R447_every_regenerate_commit_and_preprocessor_of_the_add_in_reports_and_buttons_run_starts_the_timing()
        {
            string revit = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Revit");
            int measured = 0;
            foreach (string name in new[] { "RevitFloorDocument.cs", "RevitSiteDocument.cs", "RevitVillaDocument.cs", "RevitWallDocument.cs" })
            {
                string source = File.ReadAllText(Path.Combine(revit, name));
                Assert.DoesNotContain("document.Regenerate()", source);
                measured += source.Split(new[] { "RunTiming.Measure(RunTiming.ModelRefresh, document.Regenerate)" }, StringSplitOptions.None).Length - 1;
            }
            Assert.Equal(11, measured);

            string model = File.ReadAllText(Path.Combine(revit, "RevitModel.cs"));
            Assert.Contains("RunTiming.Measure(RunTiming.Transactions, () => transaction.Commit())", model);
            Assert.Contains("RunTiming.Measure(RunTiming.Warnings, () => Collect(accessor))", File.ReadAllText(Path.Combine(revit, "WarningCollector.cs")));
            Assert.Contains("RunTiming.Measure(RunTiming.Saving, ", File.ReadAllText(Path.Combine(revit, "RevitMassFileMaker.cs")));

            // The worker waits of the add-in and the two ends of a worker that show a window
            foreach (string client in new[] { Path.Combine("Report", "ReportWorkerClient.cs"), Path.Combine("Update", "UpdateWorkerClient.cs") })
            {
                Assert.Contains("RunTiming.Measure(RunTiming.WorkerWait, done.Wait)", File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", client)));
            }
            string common = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.Common");
            Assert.Contains("context.Timing?.NextStep(RunTiming.WindowStep);\n                        dialog.Show(view, callbacks);",
                File.ReadAllText(Path.Combine(common, "CheckTable", "CheckRun.cs")).Replace("\r\n", "\n"));
            Assert.Contains("context.Timing?.NextStep(RunTiming.WindowStep);", File.ReadAllText(Path.Combine(common, "ReadData", "ScanStep.cs")));
            Assert.Contains("context.Timing?.NextStep(RunTiming.WorkerEndStep, RunTiming.Reading);", File.ReadAllText(Path.Combine(common, "ReadData", "ReadDataRun.cs")));

            string buttons = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Commands", "Buttons.cs"));
            Assert.Contains("new RunTiming()", buttons);
            string host = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.TestHost", "ButtonsRunHost.cs"));
            Assert.Contains("RunTiming.BeforeRun(null, ", host);
        }

        private sealed class Closing : IDisposable
        {
            private readonly R447_RunTimingTests test;

            public Closing(R447_RunTimingTests test)
            {
                this.test = test;
            }

            public void Dispose()
            {
                test.now += 9;
            }
        }

        private sealed class State : IRunState
        {
            private readonly string work;

            public State(string work)
            {
                this.work = work;
            }

            public List<RunRecord> Records { get; } = new List<RunRecord>();
            public bool DryRunFirst { get; set; }
            public bool ThrowOnCommitted { get; set; }

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
                return new RunInput[0];
            }

            public void Committed(int button, RunRecord record)
            {
                if (ThrowOnCommitted)
                {
                    throw new InvalidOperationException("the state failed");
                }
                Records.Add(record);
            }
        }

        private sealed class Model : IModel
        {
            private readonly R447_RunTimingTests test;

            public Model(R447_RunTimingTests test)
            {
                this.test = test;
            }

            public IModelTransaction Start(string name)
            {
                return new Transaction(test);
            }

            private sealed class Transaction : IModelTransaction
            {
                private readonly R447_RunTimingTests test;

                public Transaction(R447_RunTimingTests test)
                {
                    this.test = test;
                }

                public IReadOnlyList<string> Warnings { get; } = new string[0];
                public IReadOnlyList<string> Errors { get; } = new string[0];

                // 8 s of commit, 2 s of them the preprocessor in it, as Revit calls WarningCollector inside Commit
                public bool Commit()
                {
                    return RunTiming.Measure(RunTiming.Transactions, () =>
                    {
                        test.now += 3;
                        RunTiming.Measure(RunTiming.Warnings, () => test.now += 2);
                        test.now += 3;
                        return true;
                    });
                }

                public void RollBack()
                {
                }

                public void Dispose()
                {
                }
            }
        }

        private sealed class Confirm : IConfirm
        {
            private readonly R447_RunTimingTests test;
            private readonly bool answer;

            public Confirm(R447_RunTimingTests test, bool answer)
            {
                this.test = test;
                this.answer = answer;
            }

            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                test.now += 5;
                return answer;
            }
        }
    }
}
