using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R375. Button 2 Read data on made up files in a temporary folder, the worker run in process with a made up command read, so no
    // real input is read and no Revit is needed
    public sealed class R375_ReadDataTests : IDisposable
    {
        private const string Label = "2 Read data";
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

        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string>
        {
            ["Settings"] = "config", ["Baseplan"] = "baseplan", ["Villa blocks"] = "villaBlocks", ["Curbs"] = "curbs", ["Road surface"] = "roadSurface",
            ["DLUS schedule"] = "dlus", ["Sales schedule"] = "sales", ["Plot numbers"] = "plotNumbers",
        };

        public R375_ReadDataTests()
        {
            work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            input = Path.Combine(root, "input");
            config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["inputRoot"] = input;
            configPath = Path.Combine(root, "config", "project.json");
            Write(Path.Combine(root, "config", "cad_layers.json"), "{}");
            Write(configPath, config.ToString());
            state = new State(work);
            state.Inputs.Add(new RunInput("Settings", configPath, false));
            state.Inputs.Add(new RunInput("Baseplan", Write(Path.Combine(input, "baseplan.dwg"), "a made up baseplan"), false));
            state.Inputs.Add(new RunInput("Villa blocks", Write(Path.Combine(input, "blocks.dwg"), "made up blocks"), false));
            state.Inputs.Add(new RunInput("Curbs", Write(Path.Combine(input, "curbs.dwg"), "made up curbs"), false));
            string roads = Path.Combine(input, "roads");
            foreach (JToken name in (JArray)config["siteBuild"]["roadSurfaces"])
            {
                Write(Path.Combine(roads, (string)name + ".xml"), "<LandXML/>");
            }
            state.Inputs.Add(new RunInput("Road surface", roads, true));
            state.Inputs.Add(new RunInput("DLUS schedule", Write(Path.Combine(input, "dlus.xlsx"), "a made up DLUS"), false));
            state.Inputs.Add(new RunInput("Sales schedule", Write(Path.Combine(input, "sales.xlsx"), "a made up sales"), false));
            state.Inputs.Add(new RunInput("Plot numbers", Write(Path.Combine(input, "plan.pdf"), "a made up page"), false));
            foreach (JProperty hood in ((JObject)config["connect"]["neighbourhoods"]).Properties().Where(hood => hood.Value.Type == JTokenType.Object))
            {
                Write(Path.Combine(input, "01_Output", "1.11 References", (string)hood.Value["schedule"]), "a made up schedule of " + hood.Name);
            }
            string book = Path.Combine(root, "SiteAdaptation", "data_book");
            Write(Path.Combine(book, "plots.csv"), "label\n1\n");
            Write(Path.Combine(book, "location_plan", "labels.csv"), "number\n2301\n");
            Write(Path.Combine(book, "phase1_border.csv"), "x,y\n0,0\n");
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        // R375. The real run writes one request of the command read with every read step, the location plan and the plot table among
        // them, every input by its full path and SHA-256, into a folder named by the hash of the inputs, and Revit does not wait
        [Fact]
        public void R375_the_request_holds_every_read_step_and_every_input_by_full_path_and_sha256()
        {
            worker.Answer = false;
            RunOutcome outcome = Run();
            Assert.Equal(RunEnd.Started, outcome.End);
            Assert.Empty(ended);
            WorkerRequest request = Assert.Single(worker.Requests);
            Assert.Equal("read", request.Command);
            Assert.Equal(WorkerRunner.StepCommand.Where(step => step.Value == "read").Select(step => step.Key).OrderBy(step => step, StringComparer.Ordinal), request.Steps);
            Assert.Contains("plotTable", request.Steps);
            Assert.Contains("locationLabels", request.Steps);
            Assert.Equal("project", request.Scope.Kind);
            Assert.Equal(300, request.TimeLimitS);
            var roles = request.Inputs.Select(item => item.Role).ToList();
            foreach (string role in new[] { "config", "baseplan", "villaBlocks", "curbs", "dlus", "sales", "plotNumbers", "cadLayers", "schedule:IMC", "schedule:IMD", "plotsCsv", "labels", "border" })
            {
                Assert.Contains(role, roles);
            }
            Assert.Equal(((JArray)config["siteBuild"]["roadSurfaces"]).Count, roles.Count(role => role.StartsWith("roadSurface:", StringComparison.Ordinal)));
            Assert.DoesNotContain("schedule:IME", roles);
            foreach (WorkerFile file in request.Inputs)
            {
                Assert.True(Path.IsPathRooted(file.Path));
                Assert.Equal(FileHash.Sha256(file.Path), file.Sha256);
            }
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

        // R375. A done answer records the step with every output on its name and its SHA-256, a second run on unchanged inputs reuses
        // the read and starts no worker, a changed input reads again into another folder
        [Fact]
        public void R375_a_done_read_is_recorded_and_a_second_run_on_unchanged_inputs_reuses_it_and_starts_no_worker()
        {
            RunOutcome first = Ended(Run());
            Assert.Equal(RunEnd.Committed, first.End);
            Assert.True(first.Record.StepDone);
            Assert.Equal(Label + " done: 1 files written, 0 issues.", first.Message);
            KeyValuePair<string, string> output = Assert.Single(first.Record.Outputs);
            Assert.Equal(FileHash.Sha256(output.Key), output.Value);
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

            File.AppendAllText(state.Inputs.First(item => item.Name == "Curbs").Path, " changed");
            RunOutcome changed = Ended(Run());
            Assert.Equal(RunEnd.Committed, changed.End);
            Assert.Equal(3, worker.Requests.Count);
            Assert.NotEqual(worker.Requests[0].OutputFolder, worker.Requests[2].OutputFolder);
        }

        // R375. The dry run lists every file it would read with its size and SHA-256 and says which the last read holds unchanged
        [Fact]
        public void R375_the_dry_run_lists_each_file_with_its_size_and_sha256_and_those_the_last_read_holds_unchanged()
        {
            state.DryRunFirst = true;
            confirm.Answer = false;
            RunOutcome cancelled = Run();
            Assert.Equal(RunEnd.Cancelled, cancelled.End);
            Assert.Empty(worker.Requests);
            RunSummary summary = confirm.Asked.Last();
            string baseplan = state.Inputs.First(item => item.Name == "Baseplan").Path;
            Assert.Contains("baseplan.dwg, " + new FileInfo(baseplan).Length + " bytes, SHA-256 " + FileHash.Sha256(baseplan) + ", new or changed since the last read", summary.PerPlot);
            Assert.StartsWith("Reads " + worker.Count(state, Roles, work) + " files, ", summary.Counts);

            confirm.Answer = true;
            RunOutcome done = Ended(Run());
            Assert.Equal(RunEnd.Committed, done.End);
            Assert.False(Directory.Exists(Path.Combine(work, "dry")) && Directory.EnumerateFiles(Path.Combine(work, "dry"), "*", SearchOption.AllDirectories).Any());

            File.AppendAllText(state.Inputs.First(item => item.Name == "Curbs").Path, " changed");
            confirm.Answer = false;
            Run();
            summary = confirm.Asked.Last();
            Assert.Contains(summary.PerPlot, line => line.StartsWith("baseplan.dwg, ", StringComparison.Ordinal) && line.EndsWith(", unchanged since the last read", StringComparison.Ordinal));
            Assert.Contains(summary.PerPlot, line => line.StartsWith("curbs.dwg, ", StringComparison.Ordinal) && line.EndsWith(", new or changed since the last read", StringComparison.Ordinal));
        }

        // R375, R300, R351. Each stop is named and starts no worker: an input not chosen or not on disk, the fit schedule not named in
        // config or not on disk, a drawing of drawings.referenceOnly, a road surface of config missing from the folder, a data book file
        [Fact]
        public void R375_each_stop_is_named_and_starts_no_worker()
        {
            string fit = (string)config["dataBook"]["locationPlan"]["fitNeighbourhood"];
            string fitSchedule = Path.Combine(input, "01_Output", "1.11 References", (string)config["connect"]["neighbourhoods"][fit]["schedule"]);
            File.Delete(fitSchedule);
            Assert.Contains("the schedule of " + fit + ", the fit schedule, is not on disk: " + fitSchedule, Run().Message);
            JObject unnamed = (JObject)config.DeepClone();
            unnamed["connect"]["neighbourhoods"][fit] = null;
            File.WriteAllText(configPath, unnamed.ToString());
            Assert.Contains("the fit schedule of " + fit + " is not named in connect.neighbourhoods of config (R351)", Run().Message);
            File.WriteAllText(configPath, config.ToString());
            Write(fitSchedule, "the schedule again");

            string reference = Write(Path.Combine(input, (string)config["drawings"]["referenceOnly"][0]), "a drawing kept for reference");
            RunInput baseplan = state.Inputs.First(item => item.Name == "Baseplan");
            state.Inputs[state.Inputs.IndexOf(baseplan)] = new RunInput("Baseplan", reference, false);
            Assert.Contains("Baseplan: " + Path.GetFileName(reference) + " is kept for reference only (drawings.referenceOnly) and is never read (R300)", Run().Message);
            state.Inputs[state.Inputs.FindIndex(item => item.Name == "Baseplan")] = baseplan;

            string surface = Path.Combine(input, "roads", (string)config["siteBuild"]["roadSurfaces"][0] + ".xml");
            File.Delete(surface);
            Assert.Contains("Road surface: " + Path.GetFileName(surface) + " of siteBuild.roadSurfaces is not in the folder " + Path.Combine(input, "roads"), Run().Message);
            Write(surface, "<LandXML/>");

            string border = Path.Combine(root, "SiteAdaptation", "data_book", "phase1_border.csv");
            File.Delete(border);
            Assert.Contains("border of the data book is not on disk: " + border, Run().Message);
            Write(border, "x,y\n0,0\n");

            state.Inputs[state.Inputs.FindIndex(item => item.Name == "Curbs")] = new RunInput("Curbs", null, false);
            Assert.Equal("2 Read data needs Curbs, the file is not chosen", Run().Message);
            state.Inputs[state.Inputs.FindIndex(item => item.Name == "Curbs")] = new RunInput("Curbs", Path.Combine(input, "gone.dwg"), false);
            Assert.Equal("2 Read data needs Curbs, the file is not on disk", Run().Message);
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
        public void R375_a_unit_plot_of_the_plot_table_is_a_row_with_an_official_number_and_its_nbh_is_the_area_key()
        {
            JObject table = Worker.Table();
            List<Common.Scope.ScopePlot> plots = ReadDataRun.ScopePlots(table).ToList();
            Assert.Equal(((JArray)table["plots"]).Count, plots.Count);
            Assert.Equal(new[] { "2301" }, plots.Where(plot => plot.IsUnit).Select(plot => plot.Number));
            Assert.Equal("013", plots.First(plot => plot.IsUnit).Nbh);
        }

        // R375. A body whose work ends later opens no transaction and its end is taken once, a throw of Start ends it named
        [Fact]
        public void R375_a_body_with_start_opens_no_transaction_and_its_end_is_taken_once()
        {
            var model = new NoModel();
            Action<RunLater> keep = null;
            var body = new ButtonBody { Preview = context => new RunSummary("one", null), Start = (context, end) => keep = end };
            var flow = new ButtonFlow(state, model, confirm, InputHash.Of, () => "2_20261030-000000-000");
            Assert.Equal(RunEnd.Stopped, flow.Run(2, Label, body, Path.Combine(root, "site.rvt")).End);
            Assert.Equal(RunEnd.Started, flow.Run(2, Label, body, Path.Combine(root, "site.rvt"), ended.Add).End);
            keep(RunLater.Done(null, new[] { "an issue" }, null));
            keep(RunLater.Failed("a second end"));
            RunOutcome only = Assert.Single(ended);
            Assert.Equal(RunEnd.Committed, only.End);
            Assert.Equal(new[] { "an issue" }, only.Record.Issues);
            Assert.Equal(0, model.Started);
            body.Start = (context, end) => throw new IOException("the request could not be written");
            Assert.Equal(Label + " changed nothing, the model is as it was. The run failed: the request could not be written", flow.Run(2, Label, body, Path.Combine(root, "site.rvt"), ended.Add).Message);
        }

        private RunOutcome Run()
        {
            var run = new ReadDataRun(Roles, worker, new WorkerDocument { Path = Path.Combine(root, "site.rvt"), SessionId = "s1" }, () => scope);
            var flow = new ButtonFlow(state, new NoModel(), confirm, InputHash.Of, () => RunIds.New(2, DateTime.Now) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            return flow.Run(2, Label, run.Body(), Path.Combine(root, "site.rvt"), ended.Add);
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

            public int Count(State state, IReadOnlyDictionary<string, string> roles, string workFolder)
            {
                return ReadInputs.Make(((IRunState)state).Inputs(2), roles, workFolder).Inputs.Count;
            }

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                Action run = () =>
                {
                    var commands = new Dictionary<string, WorkerCommand>
                    {
                        ["read"] = context =>
                        {
                            if (Refuse != null)
                            {
                                throw context.Refusal(Refuse);
                            }
                            context.WriteJson(request.OutputFolder, ReadDataRun.PlotTableFile, Table());
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
