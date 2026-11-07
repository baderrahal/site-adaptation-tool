#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests.FAST6
{
    // R750, F10 of run 1 of FAST-3. The request of a data check the pane makes holds the sheet the row Shared parameters shows, and the worker
    // reads it strong. A made up folder and the parameter sheet of the repo, which holds no project data
    // Alone, as R620_DataCheckWindowTests sets the same static DataCheckStep.ShowAlone and ScanGate.PendingCheck
    [Collection(AloneCollection.Name)]
    public sealed class R750_DataCheckSheetTests : IDisposable
    {
        private readonly Action<IntPtr, DataCheckResult> aloneBefore = DataCheckStep.ShowAlone;
        private readonly string root = Path.Combine(Path.GetTempPath(), "R750_" + Guid.NewGuid().ToString("N"));

        public R750_DataCheckSheetTests()
        {
            Directory.CreateDirectory(Path.Combine(root, "input"));
        }

        public void Dispose()
        {
            DataCheckStep.ShowAlone = aloneBefore;
            ScanGate.PendingCheck = null;
            Directory.Delete(root, true);
        }

        private static string RepoSheet()
        {
            return Path.Combine(ThemeGateTests.RepoRoot(), "config", "shared_parameters.xlsx");
        }

        // A copy of the config of the repo on a folder of the test, naming the sheet as given
        private string Config(string sheet)
        {
            JObject config = JObject.Parse(File.ReadAllText(R405_DesignValuesTests.RepoConfigPath()));
            config["inputRoot"] = Path.Combine(root, "input");
            config["addin"]["parameterSheet"] = sheet;
            string file = Path.Combine(root, "config", "project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, config.ToString());
            return file;
        }

        // The worker of the add-in stood in by the worker code itself, in this process, on the request the pane wrote
        private sealed class RealWorker : IWorkerStart
        {
            public readonly List<WorkerRequest> Requests = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                var commands = new Dictionary<string, WorkerCommand> { ["dataCheck"] = CadReader.DataCheckCommand.Run };
                WorkerRunner.Run(requestPath, commands, () => 900000.0);
                ended(WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId)));
            }
        }

        private JObject Press(PaneSettings settings, RealWorker worker)
        {
            DataCheckStep.ShowAlone = (owner, result) => { };
            var read = new ButtonBody { Preview = context => new RunSummary(null, null), Start = (context, done) => done(RunLater.Done("read done", null, null)) };
            string work = Path.Combine(root, "work");
            Directory.CreateDirectory(work);
            RunLater end = null;
            DataCheckStep.Wrap(read, worker, new WorkerDocument { Path = Path.Combine(work, "a.rvt"), SessionId = "s1" }, settings, IntPtr.Zero)
                .Start(new RunContext(2, "2 Read data", "r" + worker.Requests.Count, work, null, true, new RunInput[0], new Dictionary<string, string>()), later => end = later);
            Assert.True(end != null && end.Failure == null, end?.Failure);
            JObject check = JObject.Parse(File.ReadAllText(Path.Combine(worker.Requests.Last().OutputFolder, "data_check.json")));
            return (JObject)((JArray)check["needs"]).Single(row => (string)row["need"] == "sharedParameters");
        }

        [Fact]
        public void R750_the_request_the_pane_builds_holds_the_sheet_it_shows_and_the_worker_reads_it_strong()
        {
            var worker = new RealWorker();
            var settings = new PaneSettings(new ProjectSettings { ConfigFile = Config(RepoSheet()) });
            JObject need = Press(settings, worker);
            Assert.Equal("strong", (string)need["mark"]);
            WorkerFile sheet = worker.Requests.Last().Inputs.Single(input => input.Role == InputNames.ParameterSheet);
            Assert.Equal(Path.GetFullPath(RepoSheet()), sheet.Path);

            // The sheet picked in the settings goes first
            string picked = Path.Combine(root, "picked.xlsx");
            File.Copy(RepoSheet(), picked);
            settings.Pick(InputKeys.ParameterSheet, picked);
            Assert.Equal("strong", (string)Press(settings, worker)["mark"]);
            Assert.Equal(picked, worker.Requests.Last().Inputs.Single(input => input.Role == InputNames.ParameterSheet).Path);

            // Broken on purpose: no sheet on disk anywhere, the request holds none and the check says missing as F10 saw it
            var none = new PaneSettings(new ProjectSettings { ConfigFile = Config(Path.Combine(root, "gone.xlsx")) });
            JObject missing = Press(none, worker);
            Assert.Equal("missing", (string)missing["mark"]);
            Assert.DoesNotContain(worker.Requests.Last().Inputs, input => input.Role == InputNames.ParameterSheet);
        }

        [Fact]
        public void R750_the_sheet_comes_from_the_project_settings_else_config_and_never_from_a_file_that_is_not_there()
        {
            string config = Config(RepoSheet());
            Assert.Equal(Path.GetFullPath(RepoSheet()), DataCheckSheet.Input(null, config).Path);
            var project = new ProjectSettings { ConfigFile = config };
            project.Inputs[InputNames.ParameterSheet] = new SettingsInput { Path = Path.Combine(root, "gone.xlsx") };
            Assert.Equal(Path.GetFullPath(RepoSheet()), DataCheckSheet.Input(project, config).Path);
            Assert.Equal(string.Empty, DataCheckSheet.Input(project, config).Sha256);
            Assert.Null(DataCheckSheet.Input(project, Path.Combine(root, "no_config.json")));
            Assert.Null(DataCheckSheet.Input(null, null));
            File.WriteAllText(config, "not json");
            Assert.Null(DataCheckSheet.Input(null, config));
        }
    }
}
#endif
