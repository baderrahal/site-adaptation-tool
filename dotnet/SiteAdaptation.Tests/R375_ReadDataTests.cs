using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R375. Button 2 Read data on made up files in a temporary folder, the worker run in process with a made up command read, so no
    // real input is read and no Revit is needed
    public sealed class R375_ReadDataTests : IDisposable
    {
        private const string Label = "2 Read data";
        // SavedPath holds the Windows paths Revit gives, so the document is named in that form and runs on the Linux leg of CI too
        private const string Document = @"C:\Models\site.rvt";
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa-read-" + Guid.NewGuid().ToString("N"));
        private readonly string work;
        private readonly string input;
        private readonly string configPath;
        private readonly JObject config;
        private readonly State state;
        private readonly Confirm confirm = new Confirm();
        private readonly Worker worker = new Worker();
        private readonly List<RunOutcome> ended = new List<RunOutcome>();
        private SettingsScope scope = new SettingsScope();
        private int runs;

        // R847. Step 2 reads the project folder, so the settings file is its one row of the pane
        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string> { ["Settings"] = "config" };

        public R375_ReadDataTests()
        {
            work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            input = Path.Combine(root, "input");
            config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["inputRoot"] = input;
            configPath = Path.Combine(root, "config", "project.json");
            Write(configPath, config.ToString());
            state = new State(work);
            state.Inputs.Add(new RunInput("Settings", configPath, false));
            Write(Path.Combine(input, "baseplan.dwg"), "a made up baseplan");
            Write(Path.Combine(input, "curbs.dwg"), "made up curbs");
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        // R375, R847. The real run writes one request of the command dataCheck with no read step, the settings file by its full path and
        // SHA-256 and the project folder by its path, into a folder named by the hash of the inputs, and Revit does not wait
        [Fact]
        public void R375_the_request_holds_the_settings_file_and_the_project_folder_and_no_read_step()
        {
            worker.Answer = false;
            RunOutcome outcome = Run();
            Assert.Equal(RunEnd.Started, outcome.End);
            Assert.Empty(ended);
            WorkerRequest request = Assert.Single(worker.Requests);
            Assert.Equal(ReadDataRun.DataCheckCommand, request.Command);
            Assert.True(request.Steps == null || request.Steps.Count == 0);
            Assert.Equal("project", request.Scope.Kind);
            Assert.True(request.TimeLimitS > 0);
            Assert.Equal(new[] { ReadInputs.Config, ReadInputs.ScanFolder }, request.Inputs.Select(item => item.Role));
            Assert.Equal(configPath, request.Inputs[0].Path);
            Assert.Equal(FileHash.Sha256(configPath), request.Inputs[0].Sha256);
            Assert.Equal(input, request.Inputs[1].Path);
            // No role of the old read: no schedule, no data book file, no file row of the pane
            Assert.DoesNotContain(request.Inputs, item => item.Role.StartsWith("schedule:", StringComparison.Ordinal) || item.Role == "border" || item.Role == "plotsCsv" || item.Role == "labels" || item.Role == "baseplan");
            Assert.StartsWith(Path.Combine(work, "read_"), request.OutputFolder);
            Assert.Equal(16, Path.GetFileName(request.OutputFolder).Length - "read_".Length);
            Assert.True(ReadDataRun.IsRunning(work));
            // A second click while the worker runs says so and starts nothing
            RunOutcome again = Run();
            Assert.Equal(RunEnd.Stopped, again.End);
            Assert.Contains("2 Read data is running already, its worker has not ended.", again.Message);
            Assert.Single(worker.Requests);
            worker.EndAll();
            Assert.False(ReadDataRun.IsRunning(work));
        }

        // R375. A done answer records the step with every output on its name and its SHA-256, a second run on an unchanged folder reuses
        // the read and starts no worker, a changed file of the folder reads again into another folder
        [Fact]
        public void R375_a_done_read_is_recorded_and_a_second_run_on_unchanged_inputs_reuses_it_and_starts_no_worker()
        {
            RunOutcome first = Ended(Run());
            Assert.Equal(RunEnd.Committed, first.End);
            Assert.True(first.Record.StepDone);
            Assert.Equal(Label + " done: 2 files written, 0 issues.", first.Message);
            Assert.Equal(2, first.Record.Outputs.Count);
            Assert.All(first.Record.Outputs, item => Assert.Equal(FileHash.Sha256(item.Key), item.Value));
            KeyValuePair<string, string> output = first.Record.Outputs.Single(item => Path.GetFileName(item.Key) == ReadDataRun.PlotTableFile);
            Assert.Equal(state.Inputs.Select(item => item.Name).OrderBy(name => name), first.Record.InputHashes.Keys.OrderBy(name => name));
            Assert.Single(worker.Requests);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(output.Key), ReadDataRun.RecordFile)));

            RunOutcome second = Ended(Run());
            Assert.Equal(RunEnd.Committed, second.End);
            Assert.Single(worker.Requests);
            Assert.Equal(Label + " done: the read of run " + first.Record.RunId + " is reused, nothing was read again.", second.Message);
            Assert.Equal(first.Record.Outputs, second.Record.Outputs);

            // An output changed on disk is no read to reuse
            File.AppendAllText(output.Key, " ");
            Ended(Run());
            Assert.Equal(2, worker.Requests.Count);
            Assert.Equal(worker.Requests[0].OutputFolder, worker.Requests[1].OutputFolder);

            File.AppendAllText(Path.Combine(input, "curbs.dwg"), " changed");
            RunOutcome changed = Ended(Run());
            Assert.Equal(RunEnd.Committed, changed.End);
            Assert.Equal(3, worker.Requests.Count);
            Assert.NotEqual(worker.Requests[0].OutputFolder, worker.Requests[2].OutputFolder);
        }

        // R375, R639. The dry run says what it reads, lists the settings file and the folder with their size and hash, and says which the
        // last read holds unchanged
        [Fact]
        public void R375_the_dry_run_lists_the_settings_file_and_the_folder_and_those_the_last_read_holds_unchanged()
        {
            state.DryRunFirst = true;
            confirm.Answer = false;
            RunOutcome cancelled = Run();
            Assert.Equal(RunEnd.Cancelled, cancelled.End);
            Assert.Empty(worker.Requests);
            RunSummary summary = confirm.Asked.Last();
            Assert.Contains("project.json, " + new FileInfo(configPath).Length + " bytes, SHA-256 " + FileHash.Sha256(configPath) + ", new or changed since the last read", summary.PerPlot);
            Assert.StartsWith("Reads the folder and the settings file. Files in the folder: 2, ", summary.Counts);

            confirm.Answer = true;
            RunOutcome done = Ended(Run());
            Assert.Equal(RunEnd.Committed, done.End);
            Assert.False(Directory.Exists(Path.Combine(work, "dry")) && Directory.EnumerateFiles(Path.Combine(work, "dry"), "*", SearchOption.AllDirectories).Any());

            File.AppendAllText(Path.Combine(input, "curbs.dwg"), " changed");
            confirm.Answer = false;
            Run();
            summary = confirm.Asked.Last();
            Assert.Contains(summary.PerPlot, line => line.StartsWith("project.json, ", StringComparison.Ordinal) && line.EndsWith(", unchanged since the last read", StringComparison.Ordinal));
            Assert.Contains(summary.PerPlot, line => line.StartsWith(input + ", ", StringComparison.Ordinal) && line.EndsWith(", new or changed since the last read", StringComparison.Ordinal));
        }

        // R375, R847. Each stop is named and starts no worker: a settings file that names no folder, a folder that is not on disk
        [Fact]
        public void R375_each_stop_is_named_and_starts_no_worker()
        {
            JObject unnamed = (JObject)config.DeepClone();
            unnamed.Remove("inputRoot");
            File.WriteAllText(configPath, unnamed.ToString());
            Assert.Contains(ReadInputs.NoFolder + "the settings file names no inputRoot", Run().Message);
            File.WriteAllText(configPath, config.ToString());

            Directory.Delete(input, true);
            Assert.Contains(ReadInputs.NoFolder + input, Run().Message);
            Assert.Empty(worker.Requests);
            Assert.Empty(ended);
        }

        // R375, R347, R351. A worker that ends with an error, a refusal such as a CropBox that is not its MediaBox, or past its time
        // limit ends the button with its reason named, records nothing and leaves no record a second run would reuse
        [Fact]
        public void R375_a_worker_that_fails_is_refused_or_times_out_ends_named_and_records_nothing()
        {
            worker.Refuse = "plan.pdf: the CropBox of the page is not its MediaBox (R351)";
            RunOutcome refused = Ended(Run());
            Assert.Equal(RunEnd.Stopped, refused.End);
            Assert.Null(refused.Record);
            Assert.Equal(Label + " did not finish, nothing was kept: the worker ended refused, plan.pdf: the CropBox of the page is not its MediaBox (R351)", refused.Message);
            worker.Refuse = null;
            worker.Status = "timedOut";
            RunOutcome late = Ended(Run());
            Assert.Equal(RunEnd.Stopped, late.End);
            Assert.StartsWith(Label + " did not finish, nothing was kept: the worker ended timedOut, ", late.Message);
            Assert.Empty(Directory.GetFiles(work, ReadDataRun.RecordFile, SearchOption.AllDirectories));
            worker.Status = null;
            Assert.Equal(RunEnd.Committed, Ended(Run()).End);
            Assert.Equal(3, worker.Requests.Count);
        }

        // R375, R366. The plot table is read whole and the scope held against it: a plot list number it does not hold, an empty list or a
        // neighbourhood with no unit plot stops here with the stop of R366, and the read stays for the run with a sound scope
        [Fact]
        public void R375_the_scope_is_held_against_the_whole_plot_table_and_a_stop_keeps_the_read()
        {
            scope = new SettingsScope { Kind = ScopeKinds.Plots, Plots = "9999" };
            RunOutcome stopped = Ended(Run());
            Assert.Equal(RunEnd.Stopped, stopped.End);
            Assert.Equal(Label + " read the files, but the scope stops it: Plot list: the plot table holds no plot 9999. Change the scope and run it again, the read is reused.", stopped.Message);
            scope = new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "IMC" };
            Assert.Contains("Neighbourhood IMC holds no unit plot in the plot table", Ended(Run()).Message);
            scope = new SettingsScope { Kind = ScopeKinds.Plots, Plots = "" };
            Assert.Contains("Plot list: the list is empty", Ended(Run()).Message);
            Assert.Single(worker.Requests);
            scope = new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "013" };
            Assert.Equal(RunEnd.Committed, Ended(Run()).End);
            scope = new SettingsScope { Kind = ScopeKinds.Plots, Plots = "2301" };
            Assert.Equal(RunEnd.Committed, Ended(Run()).End);
            // The plot with no official number is no unit plot, so the list of it stops
            scope = new SettingsScope { Kind = ScopeKinds.Plots, Plots = "2302" };
            Assert.Equal(RunEnd.Stopped, Ended(Run()).End);
            Assert.Single(worker.Requests);
        }

        // R375. The units of the scope from the rows of the plot table, a second way: counted by hand from the rows written
        [Fact]
        public void R375_a_row_with_an_official_number_is_a_unit_and_its_nbh_is_the_area_key()
        {
            JObject table = Worker.Table();
            List<Common.Scope.ScopePlot> plots = ReadDataRun.ScopePlots(table).ToList();
            Assert.Equal(((JArray)table["plots"]).Count, plots.Count);
            Assert.Equal(new[] { "2301" }, plots.Where(plot => plot.IsUnit).Select(plot => plot.Number));
            Assert.Equal("013", plots.First(plot => plot.IsUnit).Nbh);
        }

        // R557. Each line is one kind of row of the real table, written by hand from what the read holds on NBH 012 and 02
        [Theory]
        [InlineData("{'officialNumber': '2301', 'locationPlanNumber': '2301', 'locationPlanNewCode': 'A1'}", "2301")]
        [InlineData("{'officialNumber': '2312', 'locationPlanNumber': '2312|2313', 'locationPlanNewCode': 'A1'}", "2312")]
        [InlineData("{'officialNumber': null, 'locationPlanNumber': '4100', 'locationPlanNewCode': 'A1'}", "4100")]
        [InlineData("{'officialNumber': null, 'locationPlanNumber': '4100|4101', 'locationPlanNewCode': 'A1'}", null)]
        [InlineData("{'officialNumber': null, 'locationPlanNumber': '4100', 'locationPlanNewCode': null}", null)]
        [InlineData("{'officialNumber': null, 'locationPlanNumber': null, 'locationPlanNewCode': 'A1'}", null)]
        [InlineData("{'officialNumber': null, 'locationPlanNumber': '4100', 'locationPlanNewCode': ' '}", null)]
        public void R557_a_unit_takes_its_official_number_else_the_one_number_of_a_new_code(string row, string expected)
        {
            Assert.Equal(expected, ReadDataRun.UnitNumber(JObject.Parse(row)));
        }

        // R557. An NBH with no official number is offered and resolves to its units, the land beside them stays out
        [Fact]
        public void R557_an_nbh_with_no_official_number_resolves_to_its_units()
        {
            var table = JObject.Parse("{'plots': [{'nbhId': '02', 'officialNumber': null, 'locationPlanNumber': '4100', 'locationPlanNewCode': 'A1'},"
                + "{'nbhId': '02', 'officialNumber': null, 'locationPlanNumber': '4101', 'locationPlanNewCode': null}]}");
            List<Common.Scope.ScopePlot> plots = ReadDataRun.ScopePlots(table).ToList();
            Assert.Equal(new[] { "02" }, Common.Scope.ButtonScope.NbhCodes(plots));
            var result = Common.Scope.ButtonScope.Resolve(2, new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "02" }, plots);
            Assert.True(result.Ok);
            Assert.Equal(new[] { "4100" }, result.Plots);
        }

        // R375. A body whose work ends later opens no transaction and its end is taken once, a throw of Start ends it named
        [Fact]
        public void R375_a_body_with_start_opens_no_transaction_and_its_end_is_taken_once()
        {
            var model = new NoModel();
            Action<RunLater> keep = null;
            var body = new ButtonBody { Preview = context => new RunSummary("one", null), Start = (context, end) => keep = end };
            var flow = new ButtonFlow(state, model, confirm, InputHash.Of, () => "2_20261030-000000-000");
            Assert.Equal(RunEnd.Stopped, flow.Run(2, Label, body, Document).End);
            Assert.Equal(RunEnd.Started, flow.Run(2, Label, body, Document, ended.Add).End);
            keep(RunLater.Done(null, new[] { "an issue" }, null));
            keep(RunLater.Failed("a second end"));
            RunOutcome only = Assert.Single(ended);
            Assert.Equal(RunEnd.Committed, only.End);
            Assert.Equal(new[] { "an issue" }, only.Record.Issues);
            Assert.Equal(0, model.Started);
            body.Start = (context, end) => throw new IOException("the request could not be written");
            Assert.Equal(Label + " changed nothing, the model is as it was. The run failed: the request could not be written", flow.Run(2, Label, body, Document, ended.Add).Message);
        }

        private RunOutcome Run()
        {
            var run = new ReadDataRun(Roles, worker, new WorkerDocument { Path = Document, SessionId = "s1" }, () => scope);
            var flow = new ButtonFlow(state, new NoModel(), confirm, InputHash.Of, () => RunIds.New(2, DateTime.Now) + "-" + (++runs).ToString("D3"));
            return flow.Run(2, Label, run.Body(), Document, ended.Add);
        }

        private RunOutcome Ended(RunOutcome started)
        {
            if (started.End != RunEnd.Started)
            {
                return started;
            }
            Assert.NotEmpty(ended);
            return ended.Last();
        }

        private static string Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        private sealed class Worker : IWorkerStart
        {
            private readonly List<Action> waiting = new List<Action>();

            public List<WorkerRequest> Requests { get; } = new List<WorkerRequest>();
            public bool Answer { get; set; } = true;
            public string Refuse { get; set; }
            public string Status { get; set; }

            // Two plots of the plot table, one with an official number of the schedules in NBH 013, one with none
            public static JObject Table()
            {
                return new JObject
                {
                    ["plots"] = new JArray(
                        new JObject { ["baseplanLabel"] = "2290", ["officialNumber"] = "2301", ["nbhId"] = "013" },
                        new JObject { ["baseplanLabel"] = "2291", ["officialNumber"] = null, ["nbhId"] = "013" }),
                };
            }

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                Action run = () =>
                {
                    var commands = new Dictionary<string, WorkerCommand>
                    {
                        [ReadDataRun.DataCheckCommand] = context =>
                        {
                            if (Refuse != null)
                            {
                                throw context.Refusal(Refuse);
                            }
                            context.WriteJson(request.OutputFolder, ReadDataRun.PlotTableFile, Table());
                            // A whole check: no stop and no file left out, so the read is one to reuse (R639)
                            context.WriteJson(request.OutputFolder, ReadDataRun.DataCheckFile, new JObject { ["files"] = new JArray() });
                        },
                    };
                    WorkerRunner.Run(requestPath, commands, () => 1e6, () => 0.0);
                    WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId));
                    if (Status != null)
                    {
                        answer = new WorkerAnswer { RunId = answer.RunId, Document = answer.Document, Status = Status, Message = "the worker ran past its time limit of 300 s and was ended" };
                    }
                    ended(answer);
                };
                if (Answer)
                {
                    run();
                }
                else
                {
                    waiting.Add(run);
                }
            }

            public void EndAll()
            {
                foreach (Action run in waiting.ToList())
                {
                    run();
                }
                waiting.Clear();
            }
        }

        private sealed class State : IRunState
        {
            private readonly string work;

            public State(string work)
            {
                this.work = work;
            }

            public List<RunInput> Inputs { get; } = new List<RunInput>();
            public bool DryRunFirst { get; set; }

            public string WorkFolder(string documentPath)
            {
                return work;
            }

            public int MissingStep(int button)
            {
                return 0;
            }

            IReadOnlyList<RunInput> IRunState.Inputs(int button)
            {
                return Inputs;
            }

            public void Committed(int button, RunRecord record)
            {
                throw new InvalidOperationException("the flow records no later end on the state, the caller does");
            }
        }

        private sealed class NoModel : IModel
        {
            public int Started { get; private set; }

            public IModelTransaction Start(string name)
            {
                Started++;
                throw new InvalidOperationException("button 2 opens no transaction");
            }
        }

        private sealed class Confirm : IConfirm
        {
            public List<RunSummary> Asked { get; } = new List<RunSummary>();
            public bool Answer { get; set; } = true;

            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                Asked.Add(summary);
                return Answer;
            }
        }
    }
}
