using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests.FAST2
{
    // R700. The worker reads the parameter sheet only when the request names it, so a folder alone never gets the sheet of the tool
    public sealed class R700_ParameterSheetInputTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "R700_" + Guid.NewGuid().ToString("N"));

        public R700_ParameterSheetInputTests()
        {
            Directory.CreateDirectory(Path.Combine(folder, "handed over"));
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private JObject Run(string sheet)
        {
            string root = R700_SharedParametersNeedTests.RepoRoot();
            var inputs = new List<WorkerFile>
            {
                new WorkerFile { Role = "config", Path = Path.Combine(root, "config", "project.json"), Sha256 = "" },
                new WorkerFile { Role = "scanFolder", Path = Path.Combine(folder, "handed over"), Sha256 = "" },
            };
            if (sheet != null)
            {
                inputs.Add(new WorkerFile { Role = "parameterSheet", Path = sheet, Sha256 = "" });
            }
            var request = new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = "r1", Document = new WorkerDocument { Path = @"C:\Projects\site\a.rvt", SessionId = "s1" }, Command = "dataCheck",
                Inputs = inputs, Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, TimeLimitS = 600,
            };
            var commands = new Dictionary<string, WorkerCommand> { ["dataCheck"] = CadReader.DataCheckCommand.Run };
            int code = WorkerRunner.Run(request.Write(folder), commands, () => 900000.0);
            Assert.True(code == 0, WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Message);
            JObject check = JObject.Parse(File.ReadAllText(Path.Combine(folder, "data_check.json")));
            return (JObject)((JArray)check["needs"]).Single(row => (string)row["need"] == "sharedParameters");
        }

        [Fact]
        public void R700_the_sheet_the_request_names_is_read_and_guarded_and_a_request_with_none_says_missing()
        {
            string sheet = Path.Combine(R700_SharedParametersNeedTests.RepoRoot(), "config", "shared_parameters.xlsx");
            JObject named = Run(sheet);
            Assert.Equal("strong", (string)named["mark"]);
            WorkerOpened opened = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Opened.Single(item => item.Role == "parameterSheet");
            Assert.Equal(opened.Sha256Before, opened.Sha256After);

            JObject none = Run(null);
            Assert.Equal("missing", (string)none["mark"]);
            Assert.Equal("No parameter sheet was given to the data check.", (string)none["line"]);
        }

        [Fact]
        public void R700_a_sheet_that_does_not_read_is_weak_with_its_reason_and_the_run_carries_on()
        {
            string broken = Path.Combine(folder, "broken.xlsx");
            File.WriteAllText(broken, "not a workbook");
            JObject need = Run(broken);
            Assert.Equal("weak", (string)need["mark"]);
            Assert.StartsWith("The parameter sheet broken.xlsx could not be read: ", (string)need["line"]);
        }
    }
}
