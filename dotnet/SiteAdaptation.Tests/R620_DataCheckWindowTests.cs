#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R620. The section Data check of the window What the tool read and button 2 running the data check first. net48 only, it loads the add-in
    public sealed class R620_DataCheckWindowTests : IDisposable
    {
        private const string Json = "{\"needs\":[{\"need\":\"plotOutlines\",\"mark\":\"strong\",\"line\":\"Found 12 plot outlines.\"},"
            + "{\"need\":\"roadLevels\",\"mark\":\"missing\",\"line\":\"No road surface file was found.\"}]}";

        private readonly Func<IntPtr, IReadOnlyList<DataCheckRow>, bool> windowBefore = DataCheckStep.Window;
        private readonly Func<string, TimeSpan, int> workerBefore = DataCheckStep.RunWorker;
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r620_" + Guid.NewGuid().ToString("N"));

        public R620_DataCheckWindowTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            DataCheckStep.Window = windowBefore;
            DataCheckStep.RunWorker = workerBefore;
            Directory.Delete(folder, true);
        }

        [Fact]
        public void The_rows_keep_the_order_the_worker_wrote_with_plain_names_and_marks()
        {
            IReadOnlyList<DataCheckRow> rows = DataCheckRows.Read(Json);
            Assert.Equal(new[] { "Plot outlines", "Road levels" }, rows.Select(row => row.Name));
            Assert.Equal(new[] { "Strong", "Missing" }, rows.Select(row => row.Mark));
            Assert.True(rows[0].IsGood);
            Assert.False(rows[1].IsGood);
            Assert.Equal("No road surface file was found.", rows[1].Line);
        }

        [Fact]
        public void A_file_with_no_needs_is_refused()
        {
            Assert.Throws<FormatException>(() => DataCheckRows.Read("{}"));
        }

        [Fact]
        public void The_window_model_shows_the_section_only_when_rows_are_given()
        {
            Assert.False(new ScanResultsViewModel(new ScanViewData()).HasDataCheck);
            var model = new ScanResultsViewModel(new ScanViewData(), DataCheckRows.Read(Json));
            Assert.True(model.HasDataCheck);
            Assert.Equal(2, model.DataCheck.Count);
        }

        private string Config()
        {
            string input = Path.Combine(folder, "input");
            Directory.CreateDirectory(input);
            string config = Path.Combine(folder, "config.json");
            File.WriteAllText(config, "{\"inputRoot\":" + Newtonsoft.Json.JsonConvert.ToString(input) + "}");
            return config;
        }

        private RunContext Context()
        {
            string work = Path.Combine(folder, "work");
            Directory.CreateDirectory(work);
            return new RunContext(2, "2 Read data", "r2", work, null, true, new RunInput[0], new Dictionary<string, string>());
        }

        [Fact]
        public void Check_reads_data_check_json_the_worker_wrote_and_not_the_exit_code()
        {
            string config = Config();
            DataCheckStep.RunWorker = (request, limit) =>
            {
                string output = Path.Combine(folder, "work", DataCheckStep.FolderPrefix + "r2");
                File.WriteAllText(Path.Combine(output, DataCheckRows.FileName), Json);
                return 4;
            };
            IReadOnlyList<DataCheckRow> rows = DataCheckStep.Check(Context(), new PaneSettings(new ProjectSettings { ConfigFile = config }), null, out string why);
            Assert.Equal(2, rows.Count);
            Assert.Null(why);
        }

        [Fact]
        public void Check_says_why_when_the_worker_wrote_no_result()
        {
            string config = Config();
            DataCheckStep.RunWorker = (request, limit) => 3;
            IReadOnlyList<DataCheckRow> rows = DataCheckStep.Check(Context(), new PaneSettings(new ProjectSettings { ConfigFile = config }), null, out string why);
            Assert.Null(rows);
            Assert.Contains("code 3", why);
        }

        [Fact]
        public void Check_runs_nothing_when_the_settings_name_no_input_folder()
        {
            bool ran = false;
            DataCheckStep.RunWorker = (request, limit) => { ran = true; return 0; };
            Assert.Null(DataCheckStep.Check(Context(), new PaneSettings(), null, out string why));
            Assert.Null(why);
            Assert.False(ran);
        }
    }
}
#endif
