using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R347. The worker run on made up files: the request and the answer, the memory check, the exit codes, the work files, the kill
    // at the time limit and the answer routed to the document of its request, on net48 and net8.0
    public sealed class WorkerRunTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r347_" + Guid.NewGuid().ToString("N"));

        public WorkerRunTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
        }

        private string Write(string name, string text)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text);
            return path;
        }

        private string Config(double peakMiB = 100, double floorGiB = 2.0)
        {
            var config = new JObject
            {
                ["siteBuild"] = new JObject { ["minFreeGB"] = floorGiB },
                ["addin"] = new JObject
                {
                    ["workerPeakMiB"] = new JObject { ["plotsCsv"] = peakMiB },
                    ["workerTimeLimitS"] = new JObject { ["build"] = 600 },
                },
            };
            return Write("project.json", config.ToString());
        }

        private WorkerRequest Request(string runId = "r1", string config = null)
        {
            string data = Write("plots.csv", "locationPlanNumber\n101\n");
            config = config ?? Config();
            return new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion,
                RunId = runId,
                Document = new WorkerDocument { Path = @"C:\Projects\site\IMC site.rvt", SessionId = "s1" },
                Command = "build",
                Inputs = new List<WorkerFile>
                {
                    new WorkerFile { Role = "config", Path = config, Sha256 = FileHash.Sha256(config) },
                    new WorkerFile { Role = "plotsCsv", Path = data, Sha256 = FileHash.Sha256(data) },
                },
                LevelProfile = "parsons2024Rev6",
                Scope = new WorkerScope { Kind = "nbh", Nbh = "012" },
                OutputFolder = folder,
                TimeLimitS = 600,
            };
        }

        private string Save(WorkerRequest request)
        {
            return request.Write(folder);
        }

        // A command that writes one output by its temporary name and checks that no answer is on disk while it runs
        private static Dictionary<string, WorkerCommand> Writing(Action<WorkerRequest> during = null)
        {
            return new Dictionary<string, WorkerCommand>
            {
                ["build"] = (request, issues, counts) =>
                {
                    Assert.False(File.Exists(WorkerFiles.AnswerPath(request.OutputFolder, request.RunId)));
                    during?.Invoke(request);
                    counts["plots"] = 1;
                    issues.Add("a made up issue");
                    return new List<string> { WorkFiles.WriteText(Path.Combine(request.OutputFolder, "plan_012.json"), request.RunId, "{}") };
                },
            };
        }

        private static WorkerAnswer Answer(string path)
        {
            return WorkerAnswer.Read(path);
        }

        [Fact]
        public void R347_the_request_and_the_answer_read_back_equal()
        {
            WorkerRequest request = Request();
            request.Steps = new List<string> { "faces" };
            request.StepsFolder = Path.Combine(folder, "steps");
            request.Scope.Plots = new List<string> { "2290", "2291" };
            string path = Save(request);
            Assert.Equal(WorkerFiles.RequestPath(folder, "r1"), path);
            Assert.Equal(request.ToJson(), WorkerRequest.Read(path).ToJson());
            var answer = new WorkerAnswer
            {
                RunId = "r1", Document = request.Document, Status = "done", ExitCode = 0, Message = "done",
                Outputs = new List<WorkerFile> { new WorkerFile { Role = "output", Path = "plan_012.json", Sha256 = "ab" } },
                Counts = new Dictionary<string, int> { ["plots"] = 3 }, Issues = new List<string> { "one" },
                Opened = new List<WorkerOpened> { new WorkerOpened { Role = "config", Path = "project.json", Sha256Before = "ab", Sha256After = "ab" } },
                PeakPrivateMiB = 80.5, FreeMiBBefore = 6000, FreeMiBAfter = 5900, Seconds = 1.25, Library = "SiteAdaptation.Common 1.0.0.0",
            };
            string written = WorkFiles.WriteText(WorkerFiles.AnswerPath(folder, "r1"), "r1", answer.ToJson());
            Assert.Equal(answer.ToJson(), Answer(written).ToJson());
            Assert.Contains("\"sessionId\": \"s1\"", request.ToJson());
            Assert.Contains("\"exitCode\": 0", answer.ToJson());
        }

        [Fact]
        public void R347_the_memory_check_needs_the_measured_peak_plus_the_floor()
        {
            Assert.Equal("free memory 1.42 GiB, the Road Layout read needs 2.55 GiB and the floor 2.00 GiB",
                WorkerMemory.Refusal("Road Layout", 1.42 * 1024, 2612, 2.0));
            Assert.Null(WorkerMemory.Refusal("Road Layout", 2612 + 2048, 2612, 2.0));
            Assert.NotNull(WorkerMemory.Refusal("Road Layout", 2612 + 2047.9, 2612, 2.0));
            Assert.Equal("free memory could not be read, the baseplan read needs 0.10 GiB and the floor 2.00 GiB", WorkerMemory.Refusal("baseplan", null, 100, 2.0));
            double? free = WorkerMemory.FreeMiB();
            Assert.True(free.HasValue && free.Value > 0);
        }

        [Fact]
        public void R347_every_exit_code_has_its_status_and_its_meaning_and_no_answer_names_the_code()
        {
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, new[] { WorkerExit.Done, WorkerExit.Failed, WorkerExit.Refused, WorkerExit.Memory, WorkerExit.InputChanged });
            Assert.Equal(new[] { "done", "failed", "refused", "refused", "failed" }, Enumerable.Range(0, 5).Select(WorkerExit.Status).ToArray());
            Assert.StartsWith("refused before any read", WorkerExit.Meaning(2));
            Assert.StartsWith("refused for memory", WorkerExit.Meaning(3));
            Assert.Contains("SHA-256 before and after differ", WorkerExit.Meaning(4));
            Assert.Equal("an exit code no rule names", WorkerExit.Meaning(9));
            Assert.Equal("failed", WorkerExit.Status(9));
            Assert.Equal("the worker ended with code 3, refused for memory: free memory under the measured peak of an input plus the floor, and wrote no answer",
                WorkerExit.NoAnswer(3));
        }

        [Fact]
        public void R347_a_run_reads_its_inputs_writes_its_outputs_and_then_its_answer_by_temporary_names()
        {
            WorkerRequest request = Request();
            string old = Write("plan_old.json.tmp-r0", "partial");
            string mine = Write("other.json.tmp-r1", "kept, a file of this run");
            int code = WorkerRunner.Run(Save(request), Writing(), () => 9000.0);
            WorkerAnswer answer = Answer(WorkerFiles.AnswerPath(folder, "r1"));

            Assert.Equal(0, code);
            Assert.Equal("done", answer.Status);
            Assert.Equal(0, answer.ExitCode);
            Assert.Equal("s1", answer.Document.SessionId);
            Assert.Equal(new[] { "plan_012.json" }, answer.Outputs.Select(item => Path.GetFileName(item.Path)).ToArray());
            Assert.Equal(FileHash.Sha256(Path.Combine(folder, "plan_012.json")), answer.Outputs[0].Sha256);
            Assert.Equal(2, answer.Opened.Count);
            Assert.All(answer.Opened, item => Assert.Equal(item.Sha256Before, item.Sha256After));
            Assert.Equal(1, answer.Counts["plots"]);
            Assert.Contains("a made up issue", answer.Issues);
            Assert.Equal(9000.0, answer.FreeMiBBefore);
            Assert.Contains("SiteAdaptation.Common", answer.Library);
            Assert.Contains("Newtonsoft.Json 13.0.1", answer.Library);
            // A run deletes the temporary files of earlier runs, and nothing of its own is left under a temporary name
            Assert.False(File.Exists(old));
            Assert.True(File.Exists(mine));
            Assert.DoesNotContain(Directory.GetFiles(folder), path => WorkFiles.IsTemporary(path) && path != mine);
        }

        [Fact]
        public void R347_a_later_format_a_missing_or_temporary_input_an_unknown_command_or_a_bad_run_id_is_refused_with_2_before_any_read()
        {
            var cases = new List<(Action<WorkerRequest> change, string why)>
            {
                (request => request.FormatVersion = WorkerRequest.CurrentFormatVersion + 1, "formatVersion 2"),
                (request => request.Inputs[1].Path = Path.Combine(folder, "missing.csv"), "plotsCsv is not on disk"),
                (request => request.Inputs[1].Path = null, "plotsCsv is not chosen"),
                (request => request.Inputs[1].Path = Write("plots.csv.tmp-r9", "x"), "temporary name"),
                (request => request.Command = "draw", "the command draw"),
                (request => request.Command = "levelExcel", "the command levelExcel is not built yet"),
                (request => request.Steps = new List<string> { "walls" }, "the step walls"),
                (request => request.Inputs.RemoveAt(0), "names no config"),
            };
            int index = 0;
            foreach (var (change, why) in cases)
            {
                WorkerRequest request = Request("r" + (++index));
                change(request);
                bool ran = false;
                int code = WorkerRunner.Run(Save(request), Writing(_ => ran = true), () => 9000.0);
                WorkerAnswer answer = Answer(WorkerFiles.AnswerPath(folder, request.RunId));
                Assert.Equal(2, code);
                Assert.Equal("refused", answer.Status);
                Assert.Contains(why, answer.Message);
                Assert.False(ran, why);
            }
            Assert.Equal(2, WorkerRunner.Run(Write("request_bad id.json", "{\"runId\": \"../x\"}"), Writing(), () => 9000.0));
        }

        [Fact]
        public void R347_an_input_off_its_request_hash_or_changed_while_it_was_read_ends_with_4()
        {
            WorkerRequest before = Request("r1");
            before.Inputs[1].Sha256 = new string('0', 64);
            bool ran = false;
            Assert.Equal(4, WorkerRunner.Run(Save(before), Writing(_ => ran = true), () => 9000.0));
            Assert.False(ran);
            Assert.Contains("plotsCsv changed since the request", Answer(WorkerFiles.AnswerPath(folder, "r1")).Message);

            WorkerRequest during = Request("r2");
            Assert.Equal(4, WorkerRunner.Run(Save(during), Writing(request => File.AppendAllText(request.Inputs[1].Path, "102\n")), () => 9000.0));
            WorkerAnswer answer = Answer(WorkerFiles.AnswerPath(folder, "r2"));
            Assert.Equal("failed", answer.Status);
            Assert.Contains("plotsCsv changed while it was read", answer.Message);
            Assert.Contains(answer.Opened, item => item.Role == "plotsCsv" && item.Sha256Before != item.Sha256After);
        }

        [Fact]
        public void R347_short_of_free_memory_the_worker_reads_nothing_and_refuses_with_3_naming_both_numbers()
        {
            WorkerRequest request = Request("r1", Config(peakMiB: 2612));
            bool ran = false;
            Assert.Equal(3, WorkerRunner.Run(Save(request), Writing(_ => ran = true), () => 1.42 * 1024));
            WorkerAnswer answer = Answer(WorkerFiles.AnswerPath(folder, "r1"));
            Assert.False(ran);
            Assert.Equal("refused", answer.Status);
            Assert.Equal("free memory 1.42 GiB, the plots.csv read needs 2.55 GiB and the floor 2.00 GiB", answer.Message);
        }

        [Fact]
        public void R347_an_exception_of_the_command_ends_with_1_and_its_text()
        {
            WorkerRequest request = Request();
            var failing = new Dictionary<string, WorkerCommand> { ["build"] = (r, issues, counts) => throw new InvalidDataException("the plot table holds no row") };
            Assert.Equal(1, WorkerRunner.Run(Save(request), failing, () => 9000.0));
            WorkerAnswer answer = Answer(WorkerFiles.AnswerPath(folder, "r1"));
            Assert.Equal("failed", answer.Status);
            Assert.Equal("InvalidDataException: the plot table holds no row", answer.Message);
        }

        [Fact]
        public void R347_work_files_go_by_a_temporary_name_and_a_rename_and_a_temporary_name_is_never_read()
        {
            string path = Path.Combine(folder, "plan_012.json");
            Assert.Equal(path + ".tmp-r1", WorkFiles.TemporaryName(path, "r1"));
            Assert.True(WorkFiles.IsTemporary(path + ".tmp-r1"));
            Assert.False(WorkFiles.IsTemporary(path));
            WorkFiles.WriteText(path, "r1", "one");
            WorkFiles.WriteText(path, "r2", "two");
            Assert.Equal("two", File.ReadAllText(path));
            Assert.Throws<InvalidOperationException>(() => WorkFiles.ReadText(path + ".tmp-r1"));
            Write("a.json.tmp-r1", "x");
            Write("b.json.tmp-r2", "x");
            Directory.CreateDirectory(Path.Combine(folder, "dry"));
            File.WriteAllText(Path.Combine(folder, "dry", "c.json.tmp-r1"), "x");
            Assert.Equal(new[] { "b.json.tmp-r2" }, WorkFiles.DeleteEarlier(folder, "r1").Select(Path.GetFileName).ToArray());
            Assert.Equal(new[] { "a.json.tmp-r1", "c.json.tmp-r1" }, WorkFiles.DeleteRun(folder, "r1").Select(Path.GetFileName).OrderBy(name => name).ToArray());
            Assert.DoesNotContain(Directory.GetFiles(folder, "*", SearchOption.AllDirectories), path => WorkFiles.IsTemporary(path));
        }

        private static ProcessStartInfo Sleeping(int seconds)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new ProcessStartInfo("ping", "-n " + (seconds + 1) + " 127.0.0.1")
                : new ProcessStartInfo("sleep", seconds.ToString());
        }

        private static ProcessStartInfo Exiting(int code)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new ProcessStartInfo("cmd.exe", "/c exit " + code)
                : new ProcessStartInfo("sh", "-c \"exit " + code + "\"");
        }

        private WorkerAnswer Launch(ProcessStartInfo start, WorkerRequest request, out TimeSpan returned)
        {
            WorkerAnswer ended = null;
            using (var done = new ManualResetEventSlim(false))
            {
                var watch = Stopwatch.StartNew();
                using (WorkerLaunch launch = WorkerLaunch.Start(start, folder, request, answer =>
                {
                    ended = answer;
                    done.Set();
                }))
                {
                    returned = watch.Elapsed;
                    Assert.True(done.Wait(TimeSpan.FromSeconds(60)), "the worker never ended");
                    Assert.True(launch.Ended);
                }
            }
            return ended;
        }

        [Fact]
        public void R347_the_launch_returns_at_once_and_a_worker_past_its_time_limit_is_killed_with_its_temporary_files()
        {
            WorkerRequest request = Request("r7");
            request.TimeLimitS = 1;
            string own = Write("plan_012.json.tmp-r7", "partial");
            string other = Write("plan_013.json.tmp-r6", "partial of another run");
            TimeSpan returned;
            WorkerAnswer answer = Launch(Sleeping(30), request, out returned);

            Assert.True(returned < TimeSpan.FromSeconds(1), "Start waited " + returned);
            Assert.Equal("timedOut", answer.Status);
            Assert.Null(answer.ExitCode);
            Assert.Contains("time limit of 1 s", answer.Message);
            Assert.False(File.Exists(own));
            Assert.True(File.Exists(other));
            Assert.Equal("timedOut", Answer(WorkerFiles.AnswerPath(folder, "r7")).Status);
            Assert.True(File.Exists(WorkerFiles.LogPath(folder, "r7")));
        }

        [Fact]
        public void R347_a_worker_that_exits_with_no_answer_is_a_failure_that_names_its_code()
        {
            WorkerRequest request = Request("r8");
            TimeSpan returned;
            WorkerAnswer answer = Launch(Exiting(3), request, out returned);
            Assert.Equal("failed", answer.Status);
            Assert.Equal(3, answer.ExitCode);
            Assert.Equal(WorkerExit.NoAnswer(3), answer.Message);
            Assert.Equal("s1", answer.Document.SessionId);
        }

        [Fact]
        public void R347_the_answer_a_worker_wrote_is_the_one_the_launch_hands_on()
        {
            WorkerRequest request = Request("r9");
            var written = new WorkerAnswer { RunId = "r9", Document = request.Document, Status = "done", ExitCode = 0, Message = "done" };
            WorkFiles.WriteText(WorkerFiles.AnswerPath(folder, "r9"), "r9", written.ToJson());
            TimeSpan returned;
            WorkerAnswer answer = Launch(Exiting(0), request, out returned);
            Assert.Equal(written.ToJson(), answer.ToJson());
        }

        [Fact]
        public void R347_an_answer_goes_to_the_document_of_its_request_whatever_document_is_active_and_is_dropped_once_it_closed()
        {
            var answer = new WorkerAnswer { RunId = "r1", Document = new WorkerDocument { Path = @"C:\Projects\site\IMC site.rvt", SessionId = "s1" } };
            var imc = new OpenDocument { Path = @"c:\projects\SITE\imc site.rvt", SessionId = "s1" };
            var imd = new OpenDocument { Path = @"C:\Projects\site\IMD site.rvt", SessionId = "s2", Active = true };
            string issue;
            Assert.Same(imc, AnswerRouting.Find(answer, new[] { imd, imc }, out issue));
            Assert.Null(issue);
            Assert.Null(AnswerRouting.Find(answer, new[] { imd }, out issue));
            Assert.Equal("run r1 ended after its document closed, nothing applied", issue);
            // Closed and opened again gives the document a new session id, so nothing of the answer is applied to it
            var again = new OpenDocument { Path = imc.Path, SessionId = "s3" };
            Assert.Null(AnswerRouting.Find(answer, new[] { imd, again }, out issue));
            Assert.Equal("run r1 ended after its document closed, nothing applied", issue);
        }

        [Fact]
        public void R347_the_config_names_the_measured_peak_of_each_large_input_and_the_time_limit_of_each_command()
        {
            AddinConfig config = AddinConfig.Load(RepoConfig());
            Assert.Equal(2612, config.WorkerPeakMiB("curbs"));
            Assert.Equal(100, config.WorkerPeakMiB("baseplan"));
            Assert.Equal(80, config.WorkerPeakMiB("villaBlocks"));
            Assert.Null(config.WorkerPeakMiB("plotsCsv"));
            Assert.Equal(2.0, config.MinFreeGiB);
            foreach (string command in WorkerRequest.Commands)
            {
                Assert.True(config.WorkerTimeLimitS(command) > 0, command);
            }
            Assert.Equal(600, config.WorkerTimeLimitS("read"));
            Assert.Equal(60, config.WorkerTimeLimitS("levelExcel"));
            Assert.Throws<InvalidDataException>(() => AddinConfig.Load(Write("empty.json", "{}")));
        }

        // The config file of the repo, found from the test folder up, as the C# tests read the settings of R345 and R347
        internal static string RepoConfig()
        {
            for (string step = AppContext.BaseDirectory; step != null; step = Path.GetDirectoryName(step))
            {
                string path = Path.Combine(step, "config", "project.json");
                if (File.Exists(path))
                {
                    return path;
                }
            }
            throw new FileNotFoundException("config/project.json above " + AppContext.BaseDirectory);
        }
    }
}
