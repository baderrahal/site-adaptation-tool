using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R680 and R681 are tested in FAST1/R680_StagePaneTests.cs of FAST-1-build-1, which merges after its rule lines. This names them for test_r12 until then
    // What the lanes of R2-C2 and R2-C3 share, set by Lead before either starts, so neither lane edits an existing file of Common
    public sealed class R640_WorkerSeamTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "R640_" + Guid.NewGuid().ToString("N"));

        public R640_WorkerSeamTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private WorkerRequest Request(string command, string role, string path, string sha = "")
        {
            string config = Path.Combine(folder, "project.json");
            File.WriteAllText(config, new JObject { ["siteBuild"] = new JObject { ["minFreeGB"] = 0.1 }, ["addin"] = new JObject
            {
                ["workerTimeLimitMinS"] = 0.5,
                ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "a.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) },
            } }.ToString());
            return new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = "r1", Document = new WorkerDocument { Path = @"C:\Projects\site\a.rvt", SessionId = "s1" }, Command = command,
                Inputs = new List<WorkerFile> { new WorkerFile { Role = "config", Path = config, Sha256 = "" }, new WorkerFile { Role = role, Path = path, Sha256 = sha } },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, TimeLimitS = 600,
            };
        }

        private string Made(string name)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, "one");
            return path;
        }

        private string Message()
        {
            return WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Message;
        }

        [Theory]
        [InlineData("siteBuild", "plotTable")]
        [InlineData("rulesChange", "rules")]
        public void R640_a_command_of_this_round_with_no_handler_is_refused_as_not_built_yet(string command, string role)
        {
            int code = WorkerRunner.Run(Request(command, role, Made("a.json")).Write(folder), new Dictionary<string, WorkerCommand>(), () => 9000.0);
            Assert.Equal(2, code);
            Assert.Contains("not built yet", Message());
        }

        [Fact]
        public void R640_the_commands_of_this_round_take_an_empty_hash_and_an_older_command_still_refuses_one()
        {
            bool ran = false;
            var commands = new Dictionary<string, WorkerCommand> { ["siteBuild"] = run => ran = run.Input("plotTable") != null, ["build"] = run => ran = true };
            Assert.Equal(0, WorkerRunner.Run(Request("siteBuild", "plotTable", Made("plots.json")).Write(folder), commands, () => 9000.0));
            Assert.True(ran);
            ran = false;
            Assert.Equal(4, WorkerRunner.Run(Request("build", "plotTable", Made("plots.json")).Write(folder), commands, () => 9000.0));
            Assert.False(ran);
        }

        [Fact]
        public void R640_a_hash_the_request_does_give_is_still_compared()
        {
            var commands = new Dictionary<string, WorkerCommand> { ["siteBuild"] = run => { } };
            Assert.Equal(4, WorkerRunner.Run(Request("siteBuild", "plotTable", Made("plots.json"), "not the hash").Write(folder), commands, () => 9000.0));
        }

        [Fact]
        public void R660_only_rulesSetup_takes_a_project_folder_and_it_must_be_on_disk()
        {
            string project = Path.Combine(folder, "project");
            Directory.CreateDirectory(project);
            var commands = new Dictionary<string, WorkerCommand> { ["rulesSetup"] = run => { }, ["rulesChange"] = run => { } };
            Assert.Equal(0, WorkerRunner.Run(Request("rulesSetup", "projectFolder", project).Write(folder), commands, () => 9000.0));
            Assert.Equal(2, WorkerRunner.Run(Request("rulesSetup", "projectFolder", Path.Combine(folder, "gone")).Write(folder), commands, () => 9000.0));
            Assert.Contains("not on disk", Message());
            Assert.NotEqual(0, WorkerRunner.Run(Request("rulesChange", "projectFolder", project).Write(folder), commands, () => 9000.0));
        }

        [Fact]
        public void R661_rulesChange_may_change_its_rules_file_and_no_other_input_or_command_may()
        {
            string rules = Made("project_rules.json");
            var writes = new Dictionary<string, WorkerCommand> { ["rulesChange"] = run => File.WriteAllText(run.Input("rules"), "two"), ["rulesSetup"] = run => File.WriteAllText(run.Input("rules"), "three") };
            Assert.Equal(0, WorkerRunner.Run(Request("rulesChange", "rules", rules).Write(folder), writes, () => 9000.0));
            Assert.Equal("two", File.ReadAllText(rules));
            Assert.Equal(4, WorkerRunner.Run(Request("rulesSetup", "rules", rules).Write(folder), writes, () => 9000.0));

            var other = new Dictionary<string, WorkerCommand> { ["rulesChange"] = run => File.WriteAllText(run.Input("template"), "two") };
            Assert.Equal(4, WorkerRunner.Run(Request("rulesChange", "template", Made("manual.json")).Write(folder), other, () => 9000.0));
        }

        [Fact]
        public void R661_a_new_value_of_a_date_shape_comes_through_with_the_same_text()
        {
            WorkerRequest request = Request("rulesChange", "rules", Made("a.json"));
            request.NewValue = "2020-01-02T03:04:05.10Z";
            WorkerRequest back = WorkerRequest.Read(request.Write(folder));
            Assert.Equal(JTokenType.String, back.NewValue.Type);
            Assert.Equal("2020-01-02T03:04:05.10Z", (string)back.NewValue);
        }

        [Fact]
        public void R660_who_and_the_new_value_come_through_and_a_request_with_none_leaves_them_out()
        {
            WorkerRequest request = Request("rulesChange", "rules", Made("a.json"));
            string plain = request.ToJson();
            Assert.DoesNotContain("\"who\"", plain, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("newValue", plain, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ruleId", plain, StringComparison.OrdinalIgnoreCase);
            request.Who = "made up person";
            request.RuleId = "R1";
            request.NewValue = 123.0;
            WorkerRequest back = WorkerRequest.Read(request.Write(folder));
            Assert.Equal("made up person", back.Who);
            Assert.Equal("R1", back.RuleId);
            Assert.Equal(123.0, (double)back.NewValue);
        }
    }
}
