using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Floors;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R347, R405. The Revit run of 5d: the installed worker has no config/project.json above it, so a refusal before the config of the
    // request gave the design values died in the write of its answer and the refusal was lost. DesignValues is process wide (L10 of
    // steps/latent.md) and the test process always finds the config of the repo, so the worker build runs here as its own process
    // from a copy outside the repo
    public sealed class WorkerRefusalAnswerTests : IDisposable
    {
        private const string RunId = "6_9";
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa_r347w_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            }
            foreach (string folder in Directory.GetDirectories(from))
            {
                CopyTree(folder, Path.Combine(to, Path.GetFileName(folder)));
            }
        }

        // The worker build of the same configuration and framework as this test build
        private string Worker()
        {
            var tests = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string build = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "CadReader", "bin", tests.Parent.Name, tests.Name);
            Assert.True(File.Exists(Path.Combine(build, "CadReader.dll")) || File.Exists(Path.Combine(build, "CadReader.exe")), "no worker build at " + build);
            string worker = Path.Combine(root, "worker");
            CopyTree(build, worker);
            Assert.Null(DesignValues.RepoConfig(worker));
            return worker;
        }

        [Theory]
        [InlineData("plan")]
        [InlineData("config")]
        public void R347_R405_a_refusal_with_no_config_above_the_worker_still_writes_its_answer(string temporaryRole)
        {
            string worker = Worker();
            string work = Path.Combine(root, "work");
            Directory.CreateDirectory(work);
            var paths = new Dictionary<string, string>
            {
                ["config"] = Path.Combine(work, "settings.json"),
                ["plan"] = Path.Combine(work, "plan_012.json"),
            };
            File.Copy(R405_DesignValuesTests.RepoConfigPath(), paths["config"]);
            File.WriteAllText(paths["plan"], "{}");
            paths[temporaryRole] = WorkFiles.TemporaryName(paths[temporaryRole], RunId);
            File.WriteAllText(paths[temporaryRole], "{}");
            var inputs = new List<WorkerFile>();
            foreach (KeyValuePair<string, string> input in paths)
            {
                inputs.Add(new WorkerFile { Role = input.Key, Path = input.Value, Sha256 = FileHash.Sha256(input.Value) });
            }
            var request = new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = RunId, Command = LevelsRun.Command, Inputs = inputs,
                OutputFolder = Path.Combine(work, "out"), TimeLimitS = 60,
            };
            string requestPath = request.Write(work);

            string exe = Path.Combine(worker, "CadReader.exe");
            ProcessStartInfo start = File.Exists(exe) ? new ProcessStartInfo(exe, "--request \"" + requestPath + "\"")
                : new ProcessStartInfo("dotnet", "\"" + Path.Combine(worker, "CadReader.dll") + "\" --request \"" + requestPath + "\"");
            start.UseShellExecute = false;
            start.RedirectStandardError = true;
            start.WorkingDirectory = worker;
            string error;
            int code;
            using (Process process = Process.Start(start))
            {
                error = process.StandardError.ReadToEnd();
                Assert.True(process.WaitForExit(60000), "the worker did not end in 60 s");
                code = process.ExitCode;
            }

            Assert.True(code == WorkerExit.Refused, "the worker ended " + code + ": " + error);
            string answerPath = WorkerFiles.AnswerPath(work, RunId);
            WorkerAnswer answer = WorkerAnswer.Read(answerPath);
            Assert.Equal(WorkerExit.Status(WorkerExit.Refused), answer.Status);
            Assert.Contains("the input " + temporaryRole + " has a temporary name and is never read", answer.Message);
            if (temporaryRole == "plan")
            {
                // The config was checked and used before the other inputs, so the answer holds the rounding of its design values
                var seconds = (double)JObject.Parse(File.ReadAllText(answerPath))["seconds"];
                Assert.Equal(Math.Round(seconds, DesignValues.Current.Decimals), seconds);
            }
        }
    }
}
