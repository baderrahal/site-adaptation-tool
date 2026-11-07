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
    // F10 of run 1 of FAST-3, R750 of FAST-6. Card 1 on a project with no past file reads the folder by the data check, and its request carries the parameter
    // sheet of the pane, in the request only, so the read folder and its reuse stay as they were. Every file is a made up stand in
    public sealed class F10_RawReadSheetTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "f10-" + Guid.NewGuid().ToString("N"));

        public F10_RawReadSheetTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private sealed class Catching : IWorkerStart
        {
            public List<WorkerRequest> Requests { get; } = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                ended(new WorkerAnswer { RunId = request.RunId, Status = "failed", Message = "made up stop after the request was caught" });
            }
        }

        private sealed class State : IRunState
        {
            private readonly string configPath;
            private readonly string work;

            public State(string configPath, string work)
            {
                this.configPath = configPath;
                this.work = work;
            }

            public bool DryRunFirst => false;

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
                return new[] { new RunInput("Settings", configPath, false) };
            }

            public void Committed(int button, RunRecord record)
            {
            }
        }

        private sealed class Yes : IConfirm
        {
            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                return true;
            }
        }

        private sealed class NoModel : IModel
        {
            public IModelTransaction Start(string name)
            {
                throw new InvalidOperationException("button 2 opens no transaction");
            }
        }

        private string Config()
        {
            string folder = Path.Combine(root, "handed over");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "made up.dxf"), "stand in");
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["inputRoot"] = folder;
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            string path = Path.Combine(root, "project.json");
            File.WriteAllText(path, config.ToString());
            return path;
        }

        private WorkerRequest Press(string configPath, string work, string sheet)
        {
            var project = new ProjectSettings();
            if (sheet != null)
            {
                project.Inputs[InputNames.ParameterSheet] = new SettingsInput { Path = sheet };
            }
            var worker = new Catching();
            var run = new ReadDataRun(new Dictionary<string, string> { ["Settings"] = ReadInputs.Config }, worker, new WorkerDocument { Path = @"C:\Models\site.rvt", SessionId = "s1" },
                () => new SettingsScope(), () => project) { ModePick = () => DesignBasis.Rev6 };
            var flow = new ButtonFlow(new State(configPath, work), new NoModel(), new Yes(), InputHash.Of, () => RunIds.New(2, DateTime.Now) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            flow.Run(2, "2 Read data", run.Body(), @"C:\Models\site.rvt", outcome => { });
            return Assert.Single(worker.Requests);
        }

        [Fact]
        public void F10_the_raw_read_of_card_1_hands_the_parameter_sheet_of_the_pane_to_the_check_in_the_request_only()
        {
            string configPath = Config();
            string work = Path.Combine(root, "work");
            Directory.CreateDirectory(work);
            Assert.True(ReadInputs.NoPastFiles(configPath, DesignBasis.Rev6, work));
            string sheet = Path.Combine(root, "made up sheet.xlsx");
            File.WriteAllText(sheet, "stand in");
            WorkerRequest with = Press(configPath, work, sheet);
            Assert.Equal(ReadDataRun.DataCheckCommand, with.Command);
            WorkerFile given = Assert.Single(with.Inputs, input => input.Role == InputNames.ParameterSheet);
            Assert.Equal(Path.GetFullPath(sheet), given.Path);
            Assert.Equal(string.Empty, given.Sha256);
            // No sheet picked and none in config: no such input, and the read folder is the same, as the sheet is no input of the read hash
            WorkerRequest without = Press(configPath, work, null);
            Assert.DoesNotContain(without.Inputs, input => input.Role == InputNames.ParameterSheet);
            Assert.Equal(with.OutputFolder, without.OutputFolder);
            // A sheet picked but gone is left out, as the runner refuses an input that is not on disk
            Assert.DoesNotContain(Press(configPath, work, Path.Combine(root, "gone.xlsx")).Inputs, input => input.Role == InputNames.ParameterSheet);
        }
    }
}
