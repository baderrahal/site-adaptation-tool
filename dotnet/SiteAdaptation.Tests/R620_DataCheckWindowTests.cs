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
    // R620. The section Data check of the window What the tool read. net48 only, it loads the add-in. R847: button 2 running the data check
    // before the scan and the old read went with that read, their tests with it
    public sealed class R620_DataCheckWindowTests : IDisposable
    {
        private const string Json = "{\"needs\":[{\"need\":\"plotOutlines\",\"mark\":\"strong\",\"line\":\"Found 12 plot outlines.\"},"
            + "{\"need\":\"roadLevels\",\"mark\":\"missing\",\"line\":\"No road surface file was found.\"}],"
            + "\"files\":[{\"name\":\"a.dwg\",\"kind\":\"drawing\",\"status\":\"read\",\"reason\":\"\"},{\"name\":\"b.dwg\",\"kind\":\"drawing\",\"status\":\"not read\",\"reason\":\"free memory 1 GiB, b.dwg needs 9 GiB\"},"
            + "{\"name\":\"c.xlsx\",\"kind\":\"workbook\",\"status\":\"read\",\"reason\":\"\"}],\"stopped\":\"\"}";
        private const string Plots = "{\"plots\":[{\"number\":\"1\"},{\"number\":\"2\"},{\"number\":\"3\"}]}";

        private readonly IWorkerStart workerBefore = ReadDataBody.Worker;
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r620_" + Guid.NewGuid().ToString("N"));

        public R620_DataCheckWindowTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            ReadDataBody.Worker = workerBefore;
            Directory.Delete(folder, true);
        }

        // The worker of the tests, which records each request
        private sealed class FakeWorker : IWorkerStart
        {
            public readonly List<WorkerRequest> Requests = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                ended(new WorkerAnswer { Status = "done" });
            }
        }

        private RunContext Context(string runId = "r2")
        {
            string work = Path.Combine(folder, "work");
            Directory.CreateDirectory(work);
            return new RunContext(2, "2 Read data", runId, work, null, true, new RunInput[0], new Dictionary<string, string>());
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
