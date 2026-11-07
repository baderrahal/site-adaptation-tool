using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.ReadData;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R790, FAST-9. The worker keeps what it gathered beside the outputs of the read, and the check run on that file gives the plots the worker
    // wrote. On both targets, as the worker runs on both. The project is the made up one of samples/new-project
    public sealed class R790_GatheredFilesTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa_r790_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static string RepoRoot()
        {
            string at = AppContext.BaseDirectory;
            while (at != null && !File.Exists(Path.Combine(at, "CLAUDE.md")))
            {
                at = Path.GetDirectoryName(at);
            }
            return at ?? throw new InvalidOperationException("the repo root was not found above " + AppContext.BaseDirectory);
        }

        // One worker run of dataCheck on the made up folder, the output folder given back
        private string Read(out string configPath)
        {
            string folder = Path.Combine(root, "handed over");
            Directory.CreateDirectory(folder);
            string sample = Path.Combine(RepoRoot(), "samples", "new-project");
            foreach (string file in Directory.GetFiles(sample, "*.dxf").Concat(Directory.GetFiles(sample, "*.xlsx")))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), true);
            }
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "config", "project.json")));
            config["inputRoot"] = folder;
            configPath = Path.Combine(root, "project.json");
            File.WriteAllText(configPath, config.ToString());
            string work = Path.Combine(root, "work");
            string output = Path.Combine(work, "read_x");
            Directory.CreateDirectory(output);
            var request = new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = "2_r790", Command = ReadDataRun.DataCheckCommand,
                Document = new WorkerDocument { Path = @"C:\Models\site.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile> { new WorkerFile { Role = "config", Path = configPath, Sha256 = FileHash.Sha256(configPath) }, new WorkerFile { Role = "scanFolder", Path = folder, Sha256 = "" } },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = output,
            };
            string requestPath = request.Write(work);
            WorkerRunner.Run(requestPath, new Dictionary<string, WorkerCommand> { ["dataCheck"] = CadReader.DataCheckCommand.Run }, () => 1e6, () => 0.0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(work, request.RunId));
            Assert.True(answer.Status == WorkerExit.Status(WorkerExit.Done), answer.Status + " " + answer.Message);
            Assert.Contains(answer.Outputs, item => Path.GetFileName(item.Path) == DataCheckApply.InputFile);
            return output;
        }

        [Fact]
        public void R790_the_check_on_the_gathered_files_gives_the_plots_the_worker_wrote()
        {
            string output = Read(out string configPath);
            DataCheckInput input = DataCheckApply.Load(File.ReadAllText(Path.Combine(output, DataCheckApply.InputFile)));
            Assert.Single(input.CadReads);
            Assert.Single(input.Excels);
            var issues = new List<string>();
            DataCheckOutput again = DataCheckApply.Check(DataCheckSettings.From(JObject.Parse(File.ReadAllText(configPath))), input, issues);
            Assert.Null(input.Stopped);

            JObject written = JObject.Parse(File.ReadAllText(Path.Combine(output, "plots.json")));
            written.Remove(RunStamp.Key);
            Assert.Equal(8, ((JArray)written["plots"]).Count + ((JArray)written["otherPlots"]).Count);
            Assert.True(JToken.DeepEquals(written, JObject.Parse(again.Plots.ToString())), "plots.json differs from the check on the gathered files");
        }

        // A file of another shape is refused with its reason, never half taken
        [Fact]
        public void R790_a_gathered_file_of_another_shape_is_refused()
        {
            Assert.Contains("no gathered files of this version", Assert.Throws<InvalidDataException>(() => DataCheckApply.Load("{\"format\":\"other\"}")).Message);
            Assert.Contains("do not read", Assert.Throws<InvalidDataException>(() => DataCheckApply.Load("{not json")).Message);
            Assert.Contains("no list cadReads", Assert.Throws<InvalidDataException>(() => DataCheckApply.Load(new JObject
            {
                ["format"] = DataCheckApply.Format, ["landXmlFiles"] = 0, ["landXmlWithCoordinateSystem"] = 0, ["ruleFiles"] = 0, ["sharedParameterFiles"] = 0, ["sheetFound"] = false,
            }.ToString())).Message);
        }
    }
}
