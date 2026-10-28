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

        private const string PlotsText = "locationPlanNumber\n101\n";

        // The measured peak of plots.csv by its name and size, the roles of a large read plotsCsv and curbs, and a curbs drawing
        // measured at another size than the one of a run, so the curbs file of a run takes the largest known peak
        // The shortest time limit of the made up config is under the 1 s the kill tests run to
        private string Config(double peakMiB = 100, double floorGiB = 2.0, double largestMiB = 0, double minimumS = 0.5)
        {
            var files = new JArray { new JObject { ["name"] = "plots.csv", ["bytes"] = PlotsText.Length, ["peakMiB"] = peakMiB } };
            if (largestMiB > 0)
            {
                files.Add(new JObject { ["name"] = "Road.dwg", ["bytes"] = 1, ["peakMiB"] = largestMiB });
            }
            var config = new JObject
            {
                ["siteBuild"] = new JObject { ["minFreeGB"] = floorGiB },
                ["addin"] = new JObject
                {
                    ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("plotsCsv", "curbs"), ["files"] = files },
                    ["workerTimeLimitS"] = new JObject { ["build"] = 600 },
                    ["workerTimeLimitMinS"] = minimumS,
                },
            };
            return Write("project.json", config.ToString());
        }

        private WorkerRequest Request(string runId = "r1", string config = null)
        {
            string data = Write("plots.csv", PlotsText);
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

        // A command that reads plots.csv and writes one output, and checks that no answer is on disk while it runs
        private static Dictionary<string, WorkerCommand> Writing(Action<WorkerRequest> during = null)
        {
            return new Dictionary<string, WorkerCommand>
            {
                ["build"] = run =>
                {
                    Assert.False(File.Exists(WorkerFiles.AnswerPath(run.Request.OutputFolder, run.Request.RunId)));
                    File.ReadAllText(run.Input("plotsCsv"));
                    during?.Invoke(run.Request);
                    run.Counts["plots"] = 1;
                    run.Issues.Add("a made up issue");
                    run.WriteJson(run.Request.OutputFolder, "plan_012.json", new JObject { ["plots"] = 1 });
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
            // A temporary file of another run id may belong to a run going on beside this one, so a run deletes only its own
            string other = Write("plan_old.json.tmp-r0", "partial of another run");
            string mine = Write("other.json.tmp-r1", "left by an earlier attempt of this run id");
            string stale = Write("answer_r1.json", "{\"runId\": \"r1\", \"status\": \"done\"}");
            int code = WorkerRunner.Run(Save(request), Writing(_ => Assert.False(File.Exists(stale), "the answer of an earlier attempt is deleted first")), () => 9000.0);
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
            // A run deletes its own temporary files and leaves those of other run ids, nothing of its own is left under a temporary name
            Assert.True(File.Exists(other));
            Assert.False(File.Exists(mine));
            Assert.DoesNotContain(Directory.GetFiles(folder), path => WorkFiles.IsTemporary(path) && path != other);
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
                (request => request.Steps = new List<string> { "gardenWalls" }, "the step gardenWalls is not one the C# build gives"),
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
            var failing = new Dictionary<string, WorkerCommand> { ["build"] = run => throw new InvalidDataException("the plot table holds no row") };
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
            string below = Path.Combine(folder, "dry", "c.json.tmp-r1");
            File.WriteAllText(below, "x");
            // Only the files of the one run id in the folder itself: a folder such as Documents is never walked down, its
            // junctions such as My Music deny the walk
            Assert.Equal(new[] { "a.json.tmp-r1" }, WorkFiles.DeleteRun(folder, "r1").Select(Path.GetFileName).ToArray());
            Assert.True(File.Exists(Path.Combine(folder, "b.json.tmp-r2")));
            Assert.True(File.Exists(below));
            Assert.Empty(WorkFiles.DeleteRun(Path.Combine(folder, "no such folder"), "r1"));
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

        // A made up worker that writes the answer file it is given as its answer and exits 0, as the worker writes its answer last
        private static ProcessStartInfo Answering(string prepared, string answerPath)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new ProcessStartInfo("cmd.exe", "/c copy /y \"" + prepared + "\" \"" + answerPath + "\" >nul")
                : new ProcessStartInfo("sh", "-c \"cp '" + prepared + "' '" + answerPath + "'\"");
        }

        // An exe that is not on disk: a launch that refuses before Process.Start never reaches it
        private ProcessStartInfo Nowhere()
        {
            return new ProcessStartInfo(Path.Combine(folder, "no such worker.exe"));
        }

        private WorkerAnswer Launch(ProcessStartInfo start, WorkerRequest request, out TimeSpan returned, Action<WorkerAnswer> also = null)
        {
            WorkerLaunch ignored;
            return Launch(start, request, out returned, out ignored, also);
        }

        private WorkerAnswer Launch(ProcessStartInfo start, WorkerRequest request, out TimeSpan returned, out WorkerLaunch ran, Action<WorkerAnswer> also = null)
        {
            WorkerAnswer ended = null;
            using (var done = new ManualResetEventSlim(false))
            {
                var watch = Stopwatch.StartNew();
                using (WorkerLaunch launch = WorkerLaunch.Start(start, folder, request, answer =>
                {
                    ended = answer;
                    done.Set();
                    also?.Invoke(answer);
                }))
                {
                    returned = watch.Elapsed;
                    Assert.True(done.Wait(TimeSpan.FromSeconds(60)), "the worker never ended");
                    Assert.True(launch.Ended);
                    ran = launch;
                }
            }
            return ended;
        }

        [Fact]
        public void R347_the_launch_returns_at_once_and_a_worker_past_its_time_limit_is_killed_with_its_temporary_files()
        {
            WorkerRequest request = Request("r7");
            request.TimeLimitS = 1;
            request.StepsFolder = Path.Combine(folder, "steps");
            Directory.CreateDirectory(request.StepsFolder);
            string good = Write("plan_012.json", "a good output of an earlier run");
            string own = Write("plan_012.json.tmp-r7", "partial");
            string step = Path.Combine(request.StepsFolder, "step_faces_csharp.json.tmp-r7");
            File.WriteAllText(step, "partial");
            string other = Write("plan_013.json.tmp-r6", "partial of another run");
            TimeSpan returned;
            WorkerAnswer answer = Launch(Sleeping(30), request, out returned);

            Assert.True(returned < TimeSpan.FromSeconds(1), "Start waited " + returned);
            Assert.Equal("timedOut", answer.Status);
            Assert.Null(answer.ExitCode);
            Assert.Contains("time limit of 1 s", answer.Message);
            Assert.False(File.Exists(own));
            Assert.False(File.Exists(step));
            Assert.True(File.Exists(other));
            Assert.Equal("a good output of an earlier run", File.ReadAllText(good));
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
        public void R347_an_answer_left_on_disk_under_the_run_id_is_deleted_before_the_start_and_never_handed_on()
        {
            WorkerRequest request = Request("r9");
            var stale = new WorkerAnswer { RunId = "r9", Document = request.Document, Status = "done", ExitCode = 0, Message = "done" };
            string path = WorkFiles.WriteText(WorkerFiles.AnswerPath(folder, "r9"), "r9", stale.ToJson());
            bool seen = true;
            TimeSpan returned;
            WorkerAnswer answer = Launch(Exiting(2), request, out returned);
            seen = File.Exists(path);
            Assert.Equal("failed", answer.Status);
            Assert.Equal(2, answer.ExitCode);
            Assert.Equal(WorkerExit.NoAnswer(2), answer.Message);
            Assert.False(seen, "the old answer is deleted before the start");
        }

        [Fact]
        public void R347_the_answer_a_worker_wrote_is_the_one_the_launch_hands_on()
        {
            WorkerRequest request = Request("r10");
            var written = new WorkerAnswer { RunId = "r10", Document = request.Document, Status = "done", ExitCode = 0, Message = "done" };
            string prepared = Write("prepared_r10.txt", written.ToJson());
            TimeSpan returned;
            WorkerAnswer answer = Launch(Answering(prepared, WorkerFiles.AnswerPath(folder, "r10")), request, out returned);
            Assert.Equal(written.ToJson(), answer.ToJson());
        }

        [Fact]
        public void R347_an_answer_of_another_run_id_document_or_exit_code_is_not_used()
        {
            WorkerRequest request = Request("r11");
            var cases = new List<(WorkerAnswer answer, string why)>
            {
                (new WorkerAnswer { RunId = "r12", Document = request.Document, Status = "done", ExitCode = 0 }, "names run r12"),
                (new WorkerAnswer { RunId = "r11", Document = new WorkerDocument { Path = request.Document.Path, SessionId = "s2" }, Status = "done", ExitCode = 0 }, "session s2"),
                (new WorkerAnswer { RunId = "r11", Document = new WorkerDocument { Path = @"C:\Projects\site\IMD site.rvt", SessionId = "s1" }, Status = "done", ExitCode = 0 }, "IMD site.rvt"),
                (new WorkerAnswer { RunId = "r11", Document = request.Document, Status = "done", ExitCode = 3 }, "exit code 3"),
            };
            int index = 0;
            foreach (var (written, why) in cases)
            {
                request.RunId = "r11x" + (++index);
                written.RunId = written.RunId == "r11" ? request.RunId : written.RunId;
                string prepared = Write("prepared_" + request.RunId + ".txt", written.ToJson());
                TimeSpan returned;
                WorkerAnswer answer = Launch(Answering(prepared, WorkerFiles.AnswerPath(folder, request.RunId)), request, out returned);
                Assert.Equal("failed", answer.Status);
                Assert.Equal(request.RunId, answer.RunId);
                Assert.Equal("s1", answer.Document.SessionId);
                Assert.Contains("is not used", answer.Message);
                Assert.Contains(why, answer.Message);
            }
        }

        [Fact]
        public void R347_an_answer_that_does_not_read_or_a_hand_off_that_throws_never_ends_the_host()
        {
            WorkerRequest request = Request("r13");
            string prepared = Write("prepared_r13.txt", "{ this is not json");
            TimeSpan returned;
            WorkerAnswer answer = Launch(Answering(prepared, WorkerFiles.AnswerPath(folder, "r13")), request, out returned);
            Assert.Equal("failed", answer.Status);
            Assert.Contains("could not be read", answer.Message);

            // The hand off is the add-in's, an exception of it is kept on the launch and goes no further
            WorkerLaunch launch;
            answer = Launch(Exiting(0), Request("r14"), out returned, out launch, _ => throw new InvalidOperationException("the external event is gone"));
            Assert.Equal("failed", answer.Status);
            Assert.True(SpinWait.SpinUntil(() => launch.HandOffFailure != null, TimeSpan.FromSeconds(10)));
            Assert.Equal("the external event is gone", launch.HandOffFailure.Message);
        }

        [WindowsFact]
        public void R347_a_temporary_file_the_kill_cannot_delete_turns_the_answer_failed_with_the_reason()
        {
            WorkerRequest request = Request("r15");
            request.TimeLimitS = 1;
            string own = Write("plan_012.json.tmp-r15", "partial");
            WorkerAnswer answer;
            using (new FileStream(own, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                TimeSpan returned;
                answer = Launch(Sleeping(30), request, out returned);
            }
            Assert.Equal("failed", answer.Status);
            Assert.Contains("time limit of 1 s", answer.Message);
            Assert.Contains("could not be deleted", answer.Message);
            Assert.Contains("plan_012.json.tmp-r15", answer.Message);
        }

        [Fact]
        public void R347_a_run_id_already_used_or_a_time_limit_that_is_no_number_of_seconds_is_refused_before_the_start()
        {
            WorkerRequest used = Request("r16");
            Write("worker_r16.log", "the log of an earlier launch of r16");
            Assert.Contains("r16 was used", Assert.Throws<InvalidOperationException>(() => WorkerLaunch.Start(Nowhere(), folder, used, _ => { })).Message);
            foreach (double limit in new[] { 0, -1, double.NaN, double.PositiveInfinity, 5E+06 })
            {
                WorkerRequest request = Request("r17");
                request.TimeLimitS = limit;
                Assert.Throws<ArgumentOutOfRangeException>(() => WorkerLaunch.Start(Nowhere(), folder, request, _ => { }));
                Assert.False(File.Exists(WorkerFiles.LogPath(folder, "r17")), "refused before the log and the start, limit " + limit);
            }
            // Missing in the request JSON reads as 0
            Assert.Equal(0.0, Newtonsoft.Json.JsonConvert.DeserializeObject<WorkerRequest>("{\"runId\": \"r18\"}").TimeLimitS);
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
        public void R347_an_output_moves_onto_its_name_only_after_every_check_and_a_good_earlier_output_stays()
        {
            string good = Write("plan_012.json", "a good output of an earlier run");
            var throwing = new Dictionary<string, WorkerCommand>
            {
                ["build"] = run =>
                {
                    run.WriteJson(run.Request.OutputFolder, "plan_012.json", new JObject { ["plots"] = 1 });
                    throw new InvalidDataException("the second output failed");
                },
            };
            Assert.Equal(1, WorkerRunner.Run(Save(Request("r1")), throwing, () => 9000.0));
            Assert.Equal("a good output of an earlier run", File.ReadAllText(good));
            Assert.Empty(Answer(WorkerFiles.AnswerPath(folder, "r1")).Outputs);

            Assert.Equal(4, WorkerRunner.Run(Save(Request("r2")), Writing(request => File.AppendAllText(request.Inputs[1].Path, "102\n")), () => 9000.0));
            Assert.Equal("a good output of an earlier run", File.ReadAllText(good));
            Assert.Empty(Answer(WorkerFiles.AnswerPath(folder, "r2")).Outputs);
            Assert.DoesNotContain(Directory.GetFiles(folder), path => WorkFiles.IsTemporary(path));

            // An output goes only into the output or the steps folder of the request, where the kill finds its temporary name
            var astray = new Dictionary<string, WorkerCommand> { ["build"] = run => run.WriteJson(Path.Combine(folder, "elsewhere"), "plan.json", new JObject()) };
            Assert.Equal(1, WorkerRunner.Run(Save(Request("r3")), astray, () => 9000.0));
            Assert.Contains("only into the output or the steps folder", Answer(WorkerFiles.AnswerPath(folder, "r3")).Message);

            Assert.Equal(0, WorkerRunner.Run(Save(Request("r4")), Writing(), () => 9000.0));
            Assert.Equal(1, (int)JObject.Parse(File.ReadAllText(good))["plots"]);
        }

        [Fact]
        public void R347_every_output_holds_its_run_id_and_the_hashes_of_its_inputs_and_is_refused_once_an_input_changed()
        {
            WorkerRequest request = Request("r5");
            Assert.Equal(0, WorkerRunner.Run(Save(request), Writing(), () => 9000.0));
            string plan = Path.Combine(folder, "plan_012.json");
            JObject output = RunStamp.Read(plan);
            Assert.Equal("r5", (string)output["run"]["runId"]);
            Assert.Equal(new[] { "config", "plotsCsv" }, output["run"]["inputs"].Select(item => (string)item["role"]).ToArray());
            Assert.Equal(request.Inputs[1].Sha256, (string)output["run"]["inputs"][1]["sha256"]);
            Assert.Null(RunStamp.Refusal(output));

            File.AppendAllText(request.Inputs[1].Path, "102\n");
            Assert.Contains("plotsCsv", RunStamp.Refusal(output));
            Assert.Contains("changed since", Assert.Throws<InvalidDataException>(() => RunStamp.Read(plan)).Message);
            File.Delete(request.Inputs[1].Path);
            Assert.Contains("not on disk", RunStamp.Refusal(output));
            Assert.Contains("no run stamp", RunStamp.Refusal(new JObject()));
        }

        [Fact]
        public void R347_free_memory_is_checked_again_before_each_read_against_the_peak_of_that_file()
        {
            // The curbs drawing of this run is not the one measured, so it takes the largest known peak, 2,612 MiB
            string road = Write("Road.dwg", "a made up drawing");
            WorkerRequest request = Request("r6", Config(peakMiB: 100, largestMiB: 2612));
            request.Inputs.Add(new WorkerFile { Role = "curbs", Path = road, Sha256 = FileHash.Sha256(road) });
            double free = 9000;
            var reading = new Dictionary<string, WorkerCommand>
            {
                ["build"] = run =>
                {
                    File.ReadAllText(run.Input("plotsCsv"));
                    // Revit took memory while the worker ran, the next read is checked against what is free now
                    free = 4000;
                    File.ReadAllText(run.Input("curbs"));
                    run.WriteJson(run.Request.OutputFolder, "plan_r6.json", new JObject());
                },
            };
            Assert.Equal(3, WorkerRunner.Run(Save(request), reading, () => free));
            WorkerAnswer answer = Answer(WorkerFiles.AnswerPath(folder, "r6"));
            Assert.Equal("refused", answer.Status);
            Assert.Equal("free memory 3.91 GiB, the Road.dwg read needs 2.55 GiB and the floor 2.00 GiB", answer.Message);
            Assert.False(File.Exists(Path.Combine(folder, "plan_r6.json")));

            // Before any read each large input is checked as well, so a run short of memory for one of them reads nothing
            free = 4000;
            bool ran = false;
            Assert.Equal(0, WorkerRunner.Run(Save(Request("r7", Config(peakMiB: 100, largestMiB: 2612))), Writing(_ => ran = true), () => free));
            Assert.True(ran, "plots.csv needs 0.10 GiB and the floor, 4,000 MiB is enough");
            WorkerRequest both = Request("r8", Config(peakMiB: 100, largestMiB: 2612));
            both.Inputs.Add(new WorkerFile { Role = "curbs", Path = road, Sha256 = FileHash.Sha256(road) });
            ran = false;
            Assert.Equal(3, WorkerRunner.Run(Save(both), Writing(_ => ran = true), () => free));
            Assert.False(ran);
        }

        [Fact]
        public void R347_an_output_or_steps_folder_inside_a_git_work_tree_is_refused_with_2_before_any_read()
        {
            string tree = Path.Combine(folder, "repo");
            Directory.CreateDirectory(Path.Combine(tree, ".git"));
            Directory.CreateDirectory(Path.Combine(tree, "python", "out"));
            Assert.Equal(tree, WorkFiles.RepoAbove(Path.Combine(tree, "python", "out")));
            Assert.Null(WorkFiles.RepoAbove(folder));
            foreach (bool steps in new[] { true, false })
            {
                WorkerRequest request = Request(steps ? "r9" : "r10");
                if (steps)
                {
                    request.Steps = new List<string> { "faces" };
                    request.Inputs.Add(new WorkerFile { Role = "baseplanPlots", Path = request.Inputs[1].Path, Sha256 = request.Inputs[1].Sha256 });
                    request.Inputs.Add(new WorkerFile { Role = "labels", Path = request.Inputs[1].Path, Sha256 = request.Inputs[1].Sha256 });
                    request.StepsFolder = Path.Combine(tree, "python", "out");
                }
                else
                {
                    request.OutputFolder = Path.Combine(tree, "python", "out");
                }
                bool ran = false;
                Assert.Equal(2, WorkerRunner.Run(Save(request), Writing(_ => ran = true), () => 9000.0));
                Assert.False(ran);
                Assert.Contains("inside the git work tree " + tree, Answer(WorkerFiles.AnswerPath(folder, request.RunId)).Message);
            }
        }

        [Fact]
        public void R347_a_drawing_with_no_measured_peak_takes_a_peak_scaled_by_its_size_from_the_measured_ones()
        {
            // Two made up drawings measured at 10,000,000 bytes and 500 MiB and at 100,000,000 bytes and 2,000 MiB, and a third of the size of
            // the first at a lower peak, which the rising line by size never takes under the peak of a smaller file
            var files = new JArray
            {
                new JObject { ["name"] = "a.dwg", ["bytes"] = 10000000, ["peakMiB"] = 500 }, new JObject { ["name"] = "b.dwg", ["bytes"] = 100000000, ["peakMiB"] = 2000 },
                new JObject { ["name"] = "c.dwg", ["bytes"] = 20000000, ["peakMiB"] = 400 },
            };
            var config = new JObject { ["siteBuild"] = new JObject { ["minFreeGB"] = 2.0 }, ["addin"] = new JObject { ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = files } } };
            AddinConfig addin = AddinConfig.Load(Write("scaled.json", config.ToString()));
            Assert.Equal(500.0, addin.WorkerPeakMiB("curbs", "a.dwg", 10000000).Value);
            Assert.Equal(400.0, addin.WorkerPeakMiB("curbs", "c.dwg", 20000000).Value);
            // Under the smallest size the smallest peak, between two sizes the line of the rising peaks, over the largest its peak per byte
            Assert.Equal(500.0, addin.WorkerPeakMiB("curbs", "small.dwg", 5000000).Value, 9);
            Assert.Equal(500.0, addin.WorkerPeakMiB("curbs", "c rev 2.dwg", 20000000).Value, 9);
            Assert.Equal(1250.0, addin.WorkerPeakMiB("curbs", "middle.dwg", 60000000).Value, 9);
            Assert.Equal(4000.0, addin.WorkerPeakMiB("curbs", "large.dwg", 200000000).Value, 9);
            Assert.Equal(500.0, addin.WorkerPeakMiB("curbs", "a.dwg", 10000001).Value, 3);
        }

        [Fact]
        public void R347_the_config_names_the_measured_peak_of_each_large_input_and_the_time_limit_of_each_command()
        {
            AddinConfig config = AddinConfig.Load(RepoConfig());
            // By the name and the size of the file, the sizes of cadreader_peaks_pr49.txt
            Assert.Equal(2612, config.WorkerPeakMiB("curbs", "Makkah Phase 1_Road Layout.dwg", 82061457));
            Assert.Equal(100, config.WorkerPeakMiB("baseplan", "ROSHN Makkah Baseplan.dwg", 1279917));
            Assert.Equal(80, config.WorkerPeakMiB("villaBlocks", "makkah building footprint.DWG", 1198438));
            // A drawing of a large read with no measured peak, another file or the same name at another size, takes a peak scaled by its
            // size from the measured ones, never the largest known: one byte over the baseplan about its 100 MiB, the size of the footprint
            // its 80 MiB, twice the Road Layout twice its peak
            Assert.Equal(100.0, config.WorkerPeakMiB("baseplan", "ROSHN Makkah Baseplan.dwg", 1279918).Value, 3);
            Assert.Equal(80.0, config.WorkerPeakMiB("villaBlocks", "Footprint Rev 2.dwg", 1198438).Value, 9);
            Assert.Equal(5224.0, config.WorkerPeakMiB("curbs", "Road Layout Rev 2.dwg", 2 * 82061457L).Value, 6);
            // An input of no large read is not checked
            Assert.Null(config.WorkerPeakMiB("plotsCsv", "plots.csv", 100));
            Assert.Null(config.WorkerPeakMiB("config", "project.json", 100));
            Assert.Equal(2.0, config.MinFreeGiB);
            foreach (string command in WorkerRequest.Commands)
            {
                Assert.True(config.WorkerTimeLimitS(command) > 0, command);
            }
            // R347, R348. The read of button 2 measured in 4c, 21.2 s at most on net48 over three runs (p4c_button2_time.txt), about 14 times it
            Assert.Equal(300, config.WorkerTimeLimitS("read"));
            Assert.Equal(60, config.WorkerTimeLimitS("levelExcel"));
            Assert.Throws<InvalidDataException>(() => AddinConfig.Load(Write("empty.json", "{}")));
        }

        [Fact]
        public void R345_the_launch_deletes_the_step_files_of_a_run_that_did_not_pass_and_keeps_those_of_a_run_done()
        {
            WorkerRequest request = Request("r20");
            request.Steps = new List<string> { "faces" };
            request.StepsFolder = Path.Combine(folder, "steps");
            Directory.CreateDirectory(request.StepsFolder);
            string step = Path.Combine(request.StepsFolder, "step_faces_csharp.json");
            File.WriteAllText(step, "a step file of an earlier run, its inputs still hash the same");
            TimeSpan returned;
            WorkerAnswer answer = Launch(Exiting(3), request, out returned);
            Assert.Equal("failed", answer.Status);
            Assert.False(File.Exists(step), "a run that did not pass leaves no step file, so the parity finds it missing");

            File.WriteAllText(step, "the step file of this run");
            request.RunId = "r21";
            var written = new WorkerAnswer { RunId = "r21", Document = request.Document, Status = "done", ExitCode = 0, Message = "done" };
            string prepared = Write("prepared_r21.txt", written.ToJson());
            answer = Launch(Answering(prepared, WorkerFiles.AnswerPath(folder, "r21")), request, out returned);
            Assert.Equal("done", answer.Status);
            Assert.Equal("the step file of this run", File.ReadAllText(step));
        }

        [Fact]
        public void R345_the_run_stamp_names_the_run_id_the_code_that_wrote_it_and_the_hash_of_its_config()
        {
            WorkerRequest request = Request("r22");
            Assert.Equal(0, WorkerRunner.Run(Save(request), Writing(), () => 9000.0));
            var stamp = (JObject)RunStamp.Read(Path.Combine(folder, "plan_012.json"))["run"];
            Assert.Equal("r22", (string)stamp["runId"]);
            string version = typeof(WorkerRunner).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion;
            Assert.Equal("SiteAdaptation.Common " + version, (string)stamp["code"]);
            Assert.Equal(FileHash.Sha256(request.Inputs[0].Path), (string)stamp["configSha256"]);
        }

        [Fact]
        public void R347_a_run_stamp_that_names_no_input_is_refused()
        {
            Assert.Contains("names no input", RunStamp.Refusal(new JObject { ["run"] = new JObject { ["runId"] = "r1", ["inputs"] = new JArray() } }) ?? "accepted");
        }

        // A junction on Windows, which needs no right to make, a symbolic link elsewhere
        private static void Link(string link, string target)
        {
            ProcessStartInfo start = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
                : new ProcessStartInfo("ln", "-s \"" + target + "\" \"" + link + "\"");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            using (Process process = Process.Start(start))
            {
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }
        }

        [Fact]
        public void R347_a_folder_reached_through_a_junction_into_a_git_work_tree_is_refused()
        {
            string tree = Path.Combine(folder, "repo");
            Directory.CreateDirectory(Path.Combine(tree, ".git"));
            Directory.CreateDirectory(Path.Combine(tree, "python"));
            string link = Path.Combine(folder, "link");
            Link(link, Path.Combine(tree, "python"));
            try
            {
                Assert.True(Directory.Exists(link));
                string found = WorkFiles.RepoAbove(Path.Combine(link, "out"));
                Assert.NotNull(found);
                Assert.Equal("repo", Path.GetFileName(found));
                Assert.True(Directory.Exists(Path.Combine(found, ".git")));
                WorkerRequest request = Request("r23");
                request.OutputFolder = Path.Combine(link, "out");
                bool ran = false;
                Assert.Equal(2, WorkerRunner.Run(Save(request), Writing(_ => ran = true), () => 9000.0));
                Assert.False(ran);
                Assert.Contains("inside the git work tree", Answer(WorkerFiles.AnswerPath(folder, "r23")).Message);
            }
            finally
            {
                // The link itself goes, never what it points to
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Directory.Delete(link);
                }
                else
                {
                    File.Delete(link);
                }
            }
        }

        // R347, Q107. An output streamed to its file is the text of a copy of its content with the run stamp set, each double exact, a run key of
        // the content in its place and none after the last key, and the content of the command is left as it gave it, as the step status of a
        // whole build held its tree four times and the text of the file once, 2,913 MiB in all on net48
        [Fact]
        public void R347_R401_an_output_is_the_text_of_its_content_with_the_run_stamp_and_the_content_is_left_as_given()
        {
            var given = new List<JObject>
            {
                new JObject { ["plots"] = new JObject { ["1"] = new JObject { ["raw"] = new JObject { ["z"] = 0.1 + 0.2, ["list"] = new JArray(1e-7, 12345.678901234567, double.NaN) } } }, ["name"] = "Façade" },
                new JObject { ["run"] = "an old stamp", ["after"] = new JArray(), ["empty"] = new JObject() },
            };
            var texts = new List<string>();
            var commands = new Dictionary<string, WorkerCommand>
            {
                ["build"] = run =>
                {
                    for (int index = 0; index < given.Count; index++)
                    {
                        var copy = (JObject)given[index].DeepClone();
                        copy[RunStamp.Key] = RunStamp.Of(run.Request);
                        texts.Add(copy.ToString(Newtonsoft.Json.Formatting.Indented, new ExactDouble.Converter()));
                        run.WriteJson(run.Request.OutputFolder, "out_" + index + ".json", given[index]);
                    }
                },
            };
            Assert.Equal(0, WorkerRunner.Run(Save(Request("r60")), commands, () => 9000.0));
            for (int index = 0; index < given.Count; index++)
            {
                Assert.Equal(new System.Text.UTF8Encoding(false).GetBytes(texts[index]), File.ReadAllBytes(Path.Combine(folder, "out_" + index + ".json")));
            }
            Assert.Null(given[0][RunStamp.Key]);
            Assert.Equal("an old stamp", (string)given[1][RunStamp.Key]);
        }

        // A command that writes two outputs, a.json and then b.json
        private static Dictionary<string, WorkerCommand> TwoOutputs()
        {
            return new Dictionary<string, WorkerCommand>
            {
                ["build"] = run =>
                {
                    run.WriteJson(run.Request.OutputFolder, "a.json", new JObject { ["plots"] = 1 });
                    run.WriteJson(run.Request.OutputFolder, "b.json", new JObject { ["plots"] = 2 });
                },
            };
        }

        [WindowsFact]
        public void R347_the_outputs_of_a_run_move_onto_their_names_all_or_none_and_a_name_held_open_puts_back_the_first()
        {
            string a = Write("a.json", "a of an earlier run");
            string b = Write("b.json", "b of an earlier run");
            int code;
            using (new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                code = WorkerRunner.Run(Save(Request("r24")), TwoOutputs(), () => 9000.0);
            }
            WorkerAnswer answer = Answer(WorkerFiles.AnswerPath(folder, "r24"));
            Assert.Equal(1, code);
            Assert.Equal("a of an earlier run", File.ReadAllText(a));
            Assert.Equal("b of an earlier run", File.ReadAllText(b));
            Assert.Contains("could not all move onto their names", answer.Message);
            Assert.Empty(answer.Outputs);
            Assert.DoesNotContain(Directory.GetFiles(folder), path => WorkFiles.IsTemporary(path));
        }

        [Fact]
        public void R347_the_outputs_of_a_run_move_onto_their_names_all_or_none_and_a_folder_in_the_way_puts_back_the_first()
        {
            string a = Write("a.json", "a of an earlier run");
            Directory.CreateDirectory(Path.Combine(folder, "b.json"));
            Assert.Equal(1, WorkerRunner.Run(Save(Request("r25")), TwoOutputs(), () => 9000.0));
            Assert.Equal("a of an earlier run", File.ReadAllText(a));
            Assert.True(Directory.Exists(Path.Combine(folder, "b.json")));
            // With no earlier a the a this run moved onto its name goes again
            File.Delete(a);
            Assert.Equal(1, WorkerRunner.Run(Save(Request("r26")), TwoOutputs(), () => 9000.0));
            Assert.False(File.Exists(a));
            Assert.Contains("could not all move onto their names", Answer(WorkerFiles.AnswerPath(folder, "r26")).Message);
            Assert.DoesNotContain(Directory.GetFiles(folder), path => WorkFiles.IsTemporary(path));
        }

        [Fact]
        public void R347_a_kill_in_the_middle_of_the_commit_puts_back_every_earlier_output_and_past_the_commit_point_keeps_the_new_ones()
        {
            foreach (bool committed in new[] { false, true })
            {
                string runId = committed ? "r28" : "r27";
                WorkerRequest request = Request(runId);
                request.TimeLimitS = 1;
                // As a worker killed between two renames leaves them: a of this run on its name with the earlier a as its backup, b new
                string a = Write("a.json", "a of this run");
                Write("a.json.tmp-" + runId + ".backup", "a of an earlier run");
                string b = Write("b.json", "b of this run");
                Write("b.json.tmp-" + runId + ".added", "");
                if (committed)
                {
                    Write("a.json.tmp-" + runId + ".committed", "");
                }
                TimeSpan returned;
                WorkerAnswer answer = Launch(Sleeping(30), request, out returned);
                Assert.Equal("timedOut", answer.Status);
                Assert.Equal(committed ? "a of this run" : "a of an earlier run", File.ReadAllText(a));
                Assert.Equal(committed, File.Exists(b));
                Assert.DoesNotContain(Directory.GetFiles(folder), path => path.Contains(".tmp-" + runId));
                File.Delete(a);
                if (File.Exists(b))
                {
                    File.Delete(b);
                }
            }
        }

        [Fact]
        public void R347_a_time_limit_under_the_minimum_of_config_is_refused_before_the_start()
        {
            WorkerRequest request = Request("r29", Config(minimumS: 10));
            request.TimeLimitS = 5;
            Exception refused = Record.Exception(() => WorkerLaunch.Start(Nowhere(), folder, request, _ => { }));
            Assert.IsType<ArgumentOutOfRangeException>(refused);
            Assert.Contains("under the minimum of 10 s", refused.Message);
            Assert.False(File.Exists(WorkerFiles.LogPath(folder, "r29")), "refused before the log and the start");
            // The config of the repo holds the minimum and every time limit of a command at or above it
            JObject addin = (JObject)JObject.Parse(File.ReadAllText(RepoConfig()))["addin"];
            double minimum = (double)(addin["workerTimeLimitMinS"] ?? throw new InvalidDataException("no addin.workerTimeLimitMinS"));
            Assert.Equal(10, minimum);
            AddinConfig config = AddinConfig.Load(RepoConfig());
            foreach (string command in WorkerRequest.Commands)
            {
                Assert.True(config.WorkerTimeLimitS(command) >= minimum, command);
            }
        }

        [Fact]
        public void R347_a_time_limit_that_fires_after_the_worker_answered_and_exited_hands_on_its_answer_and_leaves_it()
        {
            WorkerRequest request = Request("r30");
            request.TimeLimitS = 1;
            var written = new WorkerAnswer { RunId = "r30", Document = request.Document, Status = "done", ExitCode = 0, Message = "done" };
            string prepared = Write("prepared_r30.txt", written.ToJson());
            string output = Write("plan_012.json", "the output of this run");
            WorkerAnswer ended = null;
            using (var done = new ManualResetEventSlim(false))
            {
                // The end of the process is handled 3 s late, so the time limit of 1 s fires after the worker answered and exited
                using (WorkerLaunch.Start(Answering(prepared, WorkerFiles.AnswerPath(folder, "r30")), folder, request, answer =>
                {
                    ended = answer;
                    done.Set();
                }, () => Thread.Sleep(3000)))
                {
                    Assert.True(done.Wait(TimeSpan.FromSeconds(60)), "the worker never ended");
                }
            }
            Assert.Equal(written.ToJson(), ended.ToJson());
            Assert.Equal("done", Answer(WorkerFiles.AnswerPath(folder, "r30")).Status);
            Assert.Equal("the output of this run", File.ReadAllText(output));
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
