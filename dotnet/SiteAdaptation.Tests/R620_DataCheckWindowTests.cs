#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R620. The section Data check of the window What the tool read and button 2 running the data check first through the worker launch
    // of the scan. net48 only, it loads the add-in
    public sealed class R620_DataCheckWindowTests : IDisposable
    {
        private const string Json = "{\"needs\":[{\"need\":\"plotOutlines\",\"mark\":\"strong\",\"line\":\"Found 12 plot outlines.\"},"
            + "{\"need\":\"roadLevels\",\"mark\":\"missing\",\"line\":\"No road surface file was found.\"}],"
            + "\"files\":[{\"name\":\"a.dwg\",\"kind\":\"drawing\",\"status\":\"read\",\"reason\":\"\"},{\"name\":\"b.dwg\",\"kind\":\"drawing\",\"status\":\"not read\",\"reason\":\"free memory 1 GiB, b.dwg needs 9 GiB\"},"
            + "{\"name\":\"c.xlsx\",\"kind\":\"workbook\",\"status\":\"read\",\"reason\":\"\"}],\"stopped\":\"\"}";
        private const string Plots = "{\"plots\":[{\"number\":\"1\"},{\"number\":\"2\"},{\"number\":\"3\"}]}";

        private readonly Action<IntPtr, DataCheckResult> aloneBefore = DataCheckStep.ShowAlone;
        private readonly Func<IntPtr, string, ProjectSettings, ProjectSettings> windowBefore = ScanGate.Window;
        private readonly IWorkerStart workerBefore = ReadDataBody.Worker;
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r620_" + Guid.NewGuid().ToString("N"));

        public R620_DataCheckWindowTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            DataCheckStep.ShowAlone = aloneBefore;
            ScanGate.Window = windowBefore;
            ReadDataBody.Worker = workerBefore;
            ScanGate.PendingCheck = null;
            Directory.Delete(folder, true);
        }

        // The worker of the tests: it answers at once with what Reply gives, or holds the answer when Defer is set
        private sealed class FakeWorker : IWorkerStart
        {
            public readonly List<WorkerRequest> Requests = new List<WorkerRequest>();
            public Func<WorkerRequest, WorkerAnswer> Reply = request => new WorkerAnswer { Status = "done" };
            public Exception Throws;
            public bool Defer;
            public Action Release;

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                if (Throws != null)
                {
                    throw Throws;
                }
                Requests.Add(request);
                Action answer = () => ended(Reply(request));
                if (Defer)
                {
                    Release = answer;
                    return;
                }
                answer();
            }
        }

        private static WorkerAnswer Good(WorkerRequest request, string dataCheck = Json)
        {
            Directory.CreateDirectory(request.OutputFolder);
            File.WriteAllText(Path.Combine(request.OutputFolder, DataCheckRows.FileName), dataCheck);
            File.WriteAllText(Path.Combine(request.OutputFolder, DataCheckRows.PlotsFileName), Plots);
            return new WorkerAnswer { Status = "done" };
        }

        // A copy of the config of the repo whose inputRoot is a folder of the test, so addin.workerTimeLimitS.dataCheck is the real one
        private PaneSettings Settings(bool inputFolderThere = true, bool named = true)
        {
            string input = Path.Combine(folder, "input");
            if (inputFolderThere)
            {
                Directory.CreateDirectory(input);
            }
            else if (Directory.Exists(input))
            {
                Directory.Delete(input, true);
            }
            string config = Path.Combine(folder, "config.json");
            JObject root = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            if (named)
            {
                root["inputRoot"] = input;
            }
            else
            {
                root.Remove("inputRoot");
            }
            File.WriteAllText(config, root.ToString());
            return new PaneSettings(new ProjectSettings { ConfigFile = config });
        }

        private RunContext Context(string runId = "r2")
        {
            string work = Path.Combine(folder, "work");
            Directory.CreateDirectory(work);
            return new RunContext(2, "2 Read data", runId, work, null, true, new RunInput[0], new Dictionary<string, string>());
        }

        // The read behind the check: it records its start and ends done at once
        private sealed class Read
        {
            public int Started;
            public Func<RunContext, RunSummary> Preview = context => new RunSummary(null, null);

            public ButtonBody Body()
            {
                return new ButtonBody { Preview = context => Preview(context), Start = (context, done) => { Started++; done(RunLater.Done("read done", null, null)); } };
            }
        }

        private static RunLater Press(ButtonBody body, RunContext context)
        {
            RunLater end = null;
            body.Start(context, later => end = later);
            return end;
        }

        [Fact]
        public void The_rows_keep_the_order_the_worker_wrote_with_plain_names_and_marks()
        {
            DataCheckResult result = DataCheckRows.ReadResult(Json, Plots);
            Assert.Equal(new[] { "Plot outlines", "Road levels" }, result.Rows.Select(row => row.Name));
            Assert.Equal(new[] { "Strong", "Missing" }, result.Rows.Select(row => row.Mark));
            Assert.True(result.Rows[0].IsGood);
            Assert.False(result.Rows[1].IsGood);
            Assert.Equal("No road surface file was found.", result.Rows[1].Line);
        }

        [Fact]
        public void The_counts_come_from_the_files_of_the_check_and_the_plots_of_the_run()
        {
            DataCheckResult result = DataCheckRows.ReadResult(Json, Plots);
            Assert.Equal(2, result.FilesRead);
            Assert.Equal(3, result.PlotsFound);
            var model = new ScanResultsViewModel(new ScanViewData(), result, true);
            Assert.Equal("2", model.FilesRead);
            Assert.Equal("3", model.PlotsFound);
        }

        [Fact]
        public void A_file_not_read_is_shown_with_its_exact_reason_and_a_stopped_check_with_its_note()
        {
            DataCheckResult result = DataCheckRows.ReadResult(Json.Replace("\"stopped\":\"\"", "\"stopped\":\"the check stopped at the villa step\""), Plots);
            Assert.Equal("b.dwg", Assert.Single(result.Unread).Name);
            Assert.Equal("free memory 1 GiB, b.dwg needs 9 GiB", result.Unread[0].Reason);
            var model = new ScanResultsViewModel(new ScanViewData(), result, true);
            Assert.True(model.HasUnread);
            Assert.True(model.HasStoppedNote);
            Assert.Equal("the check stopped at the villa step", model.StoppedNote);
            Assert.False(new ScanResultsViewModel(new ScanViewData(), DataCheckRows.ReadResult(Json, Plots), true).HasStoppedNote);
        }

        [Fact]
        public void A_result_with_no_needs_says_so()
        {
            Assert.Equal(DataCheckRows.NoNeeds, Assert.Throws<FormatException>(() => DataCheckRows.ReadResult("{}", null)).Message);
            Assert.Equal(DataCheckRows.NoNeeds, Assert.Throws<FormatException>(() => DataCheckRows.ReadResult("{\"needs\":[]}", null)).Message);
        }

        [Fact]
        public void The_words_stand_at_the_place_of_the_ids_of_the_worker()
        {
            Assert.Equal(DataCheckLogic.Needs.Length, DataCheckText.Needs.Length);
            Assert.Equal(DataCheckRows.MarkIds.Length, DataCheckText.Marks.Length);
            Assert.Equal("Shared parameters", DataCheckText.Needs[Array.IndexOf(DataCheckLogic.Needs, "sharedParameters")]);
            Assert.Equal("Missing", DataCheckText.Marks[Array.IndexOf(DataCheckRows.MarkIds, "missing")]);
        }

        [Fact]
        public void With_only_the_check_the_window_hides_what_it_cannot_say_truly()
        {
            DataCheckResult result = DataCheckRows.ReadResult(Json, Plots);
            var alone = new ScanResultsViewModel(new ScanViewData(), result, true);
            Assert.True(alone.CheckOnly);
            Assert.False(alone.ShowScan);
            Assert.Equal(DataCheckText.CheckLine, alone.BandLine);
            Assert.Equal(DataCheckText.CheckFoot, alone.FootLine);
            var together = new ScanResultsViewModel(new ScanViewData(), result, false);
            Assert.True(together.ShowScan);
            Assert.Equal(ScanResultsText.Line, together.BandLine);
            Assert.Equal(ScanResultsText.Foot, together.FootLine);
            Assert.False(new ScanResultsViewModel(new ScanViewData()).HasDataCheck);
        }

        [Fact]
        public void The_check_goes_by_the_launch_of_the_worker_with_the_limit_of_the_config_and_the_read_goes_on()
        {
            var worker = new FakeWorker { Reply = request => Good(request) };
            var read = new Read();
            DataCheckResult shown = null;
            DataCheckStep.ShowAlone = (owner, result) => shown = result;
            ScanGate.PendingCheck = null;
            ButtonBody body = DataCheckStep.Wrap(read.Body(), worker, new WorkerDocument(), Settings(), IntPtr.Zero);
            RunLater end = Press(body, Context());

            WorkerRequest request = Assert.Single(worker.Requests);
            Assert.Equal("dataCheck", request.Command);
            Assert.Equal(new[] { "scanFolder", "config" }, request.Inputs.Select(input => input.Role));
            Assert.Equal(3600, request.TimeLimitS);
            Assert.Equal(1, read.Started);
            Assert.Null(end.Failure);
            Assert.Equal("read done", end.Message);
            // The scan never reached its gate here, so the rows were shown alone when the read ended
            Assert.NotNull(shown);
            Assert.Equal(2, shown.Rows.Count);
            Assert.Null(ScanGate.PendingCheck);
        }

        [Fact]
        public void The_gate_takes_the_check_and_shows_it_in_the_one_window_with_the_values_and_cancel_stops()
        {
            int shown = 0;
            DataCheckResult seen = null;
            ScanGate.Window = (owner, path, settings) => { shown++; seen = ScanGate.CheckShown; return null; };
            ScanGate.PendingCheck = DataCheckRows.ReadResult(Json, Plots);
            // A scan with every value clear and no conflict opens no window of itself, the check opens it
            ScanGateAnswer answer = ScanGate.Ask("scan.json", new PaneSettings(), IntPtr.Zero, path => R435_ScanViewTests.Scan(true, 0).ToString());
            Assert.Equal(1, shown);
            Assert.Equal(2, seen.Rows.Count);
            Assert.False(answer.GoOn);
            Assert.Null(ScanGate.PendingCheck);

            ScanGate.PendingCheck = DataCheckRows.ReadResult(Json, Plots);
            ScanGate.Window = (owner, path, settings) => settings;
            ScanGateAnswer used = ScanGate.Ask("scan.json", new PaneSettings(), IntPtr.Zero, path => R435_ScanViewTests.Scan(true, 0).ToString());
            Assert.True(used.GoOn);
            Assert.Equal(ScanResultsText.PicksSaved, used.Line);

            // With no check pending the clear scan opens nothing, as before
            shown = 0;
            ScanGate.Window = (owner, path, settings) => { shown++; return settings; };
            Assert.True(ScanGate.Ask("scan.json", new PaneSettings(), IntPtr.Zero, path => R435_ScanViewTests.Scan(true, 0).ToString()).GoOn);
            Assert.Equal(0, shown);
        }

        [Fact]
        public void A_pick_made_in_the_window_the_check_opened_on_a_clear_scan_is_saved()
        {
            ScanGate.PendingCheck = DataCheckRows.ReadResult(Json, Plots);
            // The window of the test changes one pick, as a person does from the list of a row
            ScanGate.Window = (owner, path, settings) =>
            {
                return new ProjectSettings
                {
                    ScanPicks = new SortedDictionary<string, string> { ["villaType"] = "a.dwg|LAYER" },
                    ConflictPicks = new SortedDictionary<string, string> { ["12"] = "a.dwg|LAYER" },
                };
            };
            var pane = new PaneSettings();
            ScanGateAnswer answer = ScanGate.Ask("scan.json", pane, IntPtr.Zero, path => R435_ScanViewTests.Scan(true, 0).ToString());
            Assert.True(answer.GoOn);
            Assert.Equal(ScanResultsText.PicksSaved, answer.Line);
            Assert.Equal("a.dwg|LAYER", pane.Project.ScanPicks["villaType"]);
            Assert.Equal("a.dwg|LAYER", pane.Project.ConflictPicks["12"]);
        }

        [Fact]
        public void A_window_that_fails_when_the_read_ends_still_ends_the_run_and_frees_the_folder()
        {
            var worker = new FakeWorker { Reply = request => Good(request) };
            DataCheckStep.ShowAlone = (owner, result) => throw new InvalidOperationException("the window could not open");
            RunContext context = Context();
            RunLater end = Press(DataCheckStep.Wrap(new Read().Body(), worker, new WorkerDocument(), Settings(), IntPtr.Zero), context);
            Assert.NotNull(end);
            Assert.Contains("the window could not open", end.Failure);
            Assert.False(DataCheckStep.IsRunning(context.WorkFolder));
        }

        [Fact]
        public void When_the_read_cannot_go_on_the_check_is_shown_alone_and_the_stop_of_the_read_is_told_after_it()
        {
            var worker = new FakeWorker { Reply = request => Good(request) };
            var read = new Read { Preview = context => throw new InvalidOperationException("the schedule of one is not on disk") };
            DataCheckResult shown = null;
            DataCheckStep.ShowAlone = (owner, result) => shown = result;
            ButtonBody body = DataCheckStep.Wrap(read.Body(), worker, new WorkerDocument(), Settings(), IntPtr.Zero);
            RunContext context = Context();

            // The preview of the flow does not stop, it cannot wait for a worker
            Assert.NotNull(body.Preview(context));
            RunLater end = Press(body, context);

            Assert.NotNull(shown);
            Assert.Equal(0, read.Started);
            Assert.Contains("the schedule of one is not on disk", end.Failure);
            Assert.Contains("changed nothing", end.Failure);
        }

        [Fact]
        public void A_press_while_a_check_of_the_folder_runs_says_so_and_starts_nothing()
        {
            var worker = new FakeWorker { Defer = true, Reply = request => Good(request) };
            DataCheckStep.ShowAlone = (owner, result) => { };
            ButtonBody body = DataCheckStep.Wrap(new Read().Body(), worker, new WorkerDocument(), Settings(), IntPtr.Zero);
            RunContext first = Context("r1");
            Press(body, first);
            Assert.True(DataCheckStep.IsRunning(first.WorkFolder));
            var again = Assert.Throws<InvalidOperationException>(() => Press(body, Context("r2")));
            Assert.Equal(ScanView.Fill(DataCheckText.RunningLine, "2 Read data"), again.Message);
            Assert.Single(worker.Requests);
            // The answer frees the folder
            worker.Release();
            Assert.False(DataCheckStep.IsRunning(first.WorkFolder));
        }

        private void FailureSays(Func<FakeWorker> make, Func<PaneSettings> settings, string text)
        {
            FakeWorker worker = make();
            var read = new Read();
            int shown = 0;
            DataCheckStep.ShowAlone = (owner, result) => shown++;
            ButtonBody body = DataCheckStep.Wrap(read.Body(), worker, new WorkerDocument(), settings(), IntPtr.Zero);
            RunLater end = Press(body, Context());
            Assert.NotNull(end);
            Assert.Contains(text, end.Failure);
            Assert.Contains("othing was read", end.Failure);
            // The read never goes on as if the check had read, and no window shows rows that were not read
            Assert.Equal(0, read.Started);
            Assert.Equal(0, shown);
            Assert.False(DataCheckStep.IsRunning(Path.Combine(folder, "work")));
        }

        [Fact]
        public void A_worker_that_did_not_finish_says_so_with_its_message()
        {
            FailureSays(() => new FakeWorker { Reply = request => new WorkerAnswer { Status = "timeLimit", Message = "the run took longer than 3600 s" } }, () => Settings(), "did not finish, timeLimit, the run took longer than 3600 s");
        }

        [Fact]
        public void A_worker_that_cannot_start_says_so()
        {
            FailureSays(() => new FakeWorker { Throws = new IOException("CadReader.exe is not there") }, () => Settings(), "could not start, CadReader.exe is not there");
        }

        [Fact]
        public void A_worker_that_wrote_no_result_says_so()
        {
            FailureSays(() => new FakeWorker { Reply = request => new WorkerAnswer { Status = "done" } }, () => Settings(), "wrote no result file");
        }

        [Fact]
        public void A_result_that_cannot_be_read_says_so()
        {
            FailureSays(() => new FakeWorker { Reply = request => Good(request, "{ not json") }, () => Settings(), "result could not be read");
            FailureSays(() => new FakeWorker { Reply = request => Good(request, "{\"needs\":[]}") }, () => Settings(), DataCheckRows.NoNeeds);
        }

        [Fact]
        public void Settings_with_no_input_folder_or_a_folder_that_is_gone_say_so_and_start_no_worker()
        {
            var none = new FakeWorker();
            FailureSays(() => none, () => Settings(true, false), "name no input folder");
            FailureSays(() => none, () => Settings(false, true), "input folder is not there");
            Assert.Empty(none.Requests);
        }

        [Fact]
        public void R847_button_2_of_the_pane_goes_to_the_read_of_the_folder_and_stops_with_no_settings_file()
        {
            // R847. The press goes to the one read, of the folder: with no settings file it stops by the line of that read and starts no worker
            var worker = new FakeWorker();
            ReadDataBody.Worker = worker;
            var session = new DocumentSession("s1", Path.Combine(folder, "model.rvt"), "model");
            ButtonBody body = ReadDataBody.For(session, IntPtr.Zero);
            InvalidDataException stop = Assert.Throws<InvalidDataException>(() => Press(body, Context()));
            Assert.Contains(ReadInputs.NoSettingsFile, stop.Message);
            Assert.Empty(worker.Requests);
        }
    }
}
#endif
