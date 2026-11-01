using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R438, R439. The scan step of button 2 on made up files in a temporary folder, the worker run in process with a made up command
    // scan that lists its inputs as CadScan does, so no real input is read and no Revit is needed
    public sealed class R438_ScanStepTests : IDisposable
    {
        private const string Label = "2 Read data";
        private const string RunId = "2_20261031-120000-000";
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa-scanstep-" + Guid.NewGuid().ToString("N"));
        private readonly string work;
        private readonly string input;
        private readonly string prototypes;
        private readonly string configPath;
        private readonly List<RunInput> pane = new List<RunInput>();
        private readonly Worker worker = new Worker();
        private readonly List<RunLater> ended = new List<RunLater>();
        private readonly List<string> gated = new List<string>();
        private int readStarts;

        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string>
        {
            ["Settings"] = "config", ["Baseplan"] = "baseplan", ["Villa blocks"] = "villaBlocks", ["Curbs"] = "curbs", ["Road surface"] = "roadSurface",
            ["DLUS schedule"] = "dlus", ["Sales schedule"] = "sales", ["Plot numbers"] = "plotNumbers",
        };

        public R438_ScanStepTests()
        {
            work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
            input = Path.Combine(root, "input");
            prototypes = Path.Combine(root, "prototypes");
            configPath = Write(Path.Combine(root, "config", "project.json"), File.ReadAllText(WorkerRunTests.RepoConfig()));
            pane.Add(new RunInput("Settings", configPath, false));
            pane.Add(new RunInput("Baseplan", Write(Path.Combine(input, "baseplan.dwg"), "a made up baseplan"), false));
            pane.Add(new RunInput("Villa blocks", Write(Path.Combine(input, "blocks.dxf"), "made up blocks"), false));
            pane.Add(new RunInput("Curbs", Write(Path.Combine(input, "curbs.dwg"), "made up curbs"), false));
            string roads = Path.Combine(input, "roads");
            Write(Path.Combine(roads, "road.xml"), "<LandXML/>");
            pane.Add(new RunInput("Road surface", roads, true));
            pane.Add(new RunInput("DLUS schedule", Write(Path.Combine(input, "dlus.xlsx"), "a made up DLUS"), false));
            pane.Add(new RunInput("Sales schedule", Write(Path.Combine(input, "sales.xlsx"), "a made up sales"), false));
            pane.Add(new RunInput("Plot numbers", Write(Path.Combine(input, "plan.pdf"), "a made up page"), false));
            Write(Path.Combine(prototypes, "P-B.rvt"), "a made up prototype B");
            Write(Path.Combine(prototypes, "P-A.rvt"), "a made up prototype A");
            Write(Path.Combine(prototypes, "notes.txt"), "no prototype");
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void R439_latest_takes_the_newest_scan_folder_that_holds_a_scan()
        {
            Assert.Null(ScanStep.Latest(work));
            Write(Path.Combine(work, "scan_2_20261031-100000-000", ScanStep.ResultFile), "{}");
            string newest = Write(Path.Combine(work, "scan_2_20261031-110000-000", ScanStep.ResultFile), "{}");
            Directory.CreateDirectory(Path.Combine(work, "scan_2_20261031-115900-000"));
            Write(Path.Combine(work, "read_ffff", ScanStep.ResultFile), "{}");
            Assert.Equal(newest, ScanStep.Latest(work));
        }

        [Fact]
        public void R439_reusable_matches_name_size_and_hash_and_refuses_a_changed_file()
        {
            IReadOnlyList<WorkerFile> inputs = Inputs();
            Assert.Null(ScanStep.Reusable(work, inputs));
            string scan = Scanned("scan_2_20261031-100000-000", inputs);
            Assert.Equal(scan, ScanStep.Reusable(work, Inputs()));

            // Same name and size, other content: only the hash parts them. The hash cache keys by write time and two writes can share a
            // tick, so each rewrite gets a write time of its own
            string dlus = Path.Combine(input, "dlus.xlsx");
            File.WriteAllText(dlus, "a made up DLUX");
            File.SetLastWriteTimeUtc(dlus, File.GetLastWriteTimeUtc(dlus).AddSeconds(1));
            Assert.Null(ScanStep.Reusable(work, Inputs()));
            File.WriteAllText(dlus, "a made up DLUS");
            File.SetLastWriteTimeUtc(dlus, File.GetLastWriteTimeUtc(dlus).AddSeconds(2));
            Assert.Equal(scan, ScanStep.Reusable(work, Inputs()));

            // Same content under another name
            File.Move(Path.Combine(prototypes, "P-B.rvt"), Path.Combine(prototypes, "P-C.rvt"));
            Assert.Null(ScanStep.Reusable(work, Inputs()));
        }

        [Fact]
        public void R438_the_request_names_cad_excel_pdf_prototypes_and_config_and_leaves_curbs_and_road_surface_out()
        {
            IReadOnlyList<WorkerFile> inputs = Inputs();
            Assert.Equal(new[] { "scanCad:1", "scanCad:2", "scanExcel:1", "scanExcel:2", "scanPdf:1", "prototype:P-A.rvt", "prototype:P-B.rvt", "config" }, inputs.Select(item => item.Role).ToArray());
            Assert.Equal(new[] { "baseplan.dwg", "blocks.dxf", "dlus.xlsx", "sales.xlsx", "plan.pdf", "P-A.rvt", "P-B.rvt", "project.json" }, inputs.Select(item => Path.GetFileName(item.Path)).ToArray());
            Assert.All(inputs, item => Assert.Equal(FileHash.Sha256(item.Path), item.Sha256));
            // The roles and the result file are those the worker command scan reads and writes
            Assert.Equal(CadReader.CadScan.ResultFile, ScanStep.ResultFile);
            Assert.Equal(CadReader.CadScan.CadRole + "1", inputs[0].Role);
            Assert.Equal(CadReader.CadScan.ExcelRole + "1", inputs[2].Role);
            Assert.Equal(CadReader.CadScan.PdfRole + "1", inputs[4].Role);
            Assert.Equal(BuildSteps.PrototypeRole + "P-A.rvt", inputs[5].Role);
            Assert.Equal(ScanStep.Command, WorkerRunner.StepCommand[WorkerRunner.ScanStep]);

            // Plot numbers given as a drawing is the third CAD file and no PDF is scanned
            pane[7] = new RunInput("Plot numbers", Write(Path.Combine(input, "numbers.dwg"), "made up numbers"), false);
            Assert.Equal(new[] { "scanCad:1", "scanCad:2", "scanCad:3", "scanExcel:1", "scanExcel:2", "prototype:P-A.rvt", "prototype:P-B.rvt", "config" }, Inputs().Select(item => item.Role).ToArray());
            Assert.Equal("numbers.dwg", Path.GetFileName(Inputs()[2].Path));
        }

        [Fact]
        public void R438_a_scan_of_the_same_inputs_is_reused_and_no_worker_starts()
        {
            string scan = Scanned("scan_2_20261031-100000-000", Inputs());
            Start(Wrapped(true, "Read 8 files, 2 plots, every value clear."));
            Assert.Empty(worker.Requests);
            Assert.Equal(new[] { scan }, gated.ToArray());
            Assert.Equal(1, readStarts);
            Assert.Null(ended.Single().Failure);
            Assert.Equal("Read 8 files, 2 plots, every value clear." + Environment.NewLine + "the read ended", ended.Single().Message);
        }

        [Fact]
        public void R438_a_fresh_scan_runs_the_worker_command_scan_then_the_gate_then_the_read()
        {
            Start(Wrapped(true, string.Format(ScanStep.ClearLine, 8, 2)));
            WorkerRequest request = worker.Requests.Single();
            Assert.Equal(ScanStep.Command, request.Command);
            Assert.Equal(RunId + "-scan", request.RunId);
            Assert.Equal(Path.Combine(work, "scan_" + RunId), request.OutputFolder);
            Assert.Equal(Inputs().Select(item => item.Role), request.Inputs.Select(item => item.Role));
            Assert.Equal(Path.Combine(work, "scan_" + RunId, ScanStep.ResultFile), gated.Single());
            Assert.Equal(1, readStarts);
            Assert.Null(ended.Single().Failure);
            Assert.StartsWith("Read 8 files, 2 plots, every value clear.", ended.Single().Message);

            // The scan.json the worker wrote is reused by the next press with the same inputs
            Start(Wrapped(true, "clear"), "2_20261031-130000-000");
            Assert.Single(worker.Requests);
            Assert.Equal(2, readStarts);
            Assert.Equal(gated[0], gated[1]);
        }

        [Fact]
        public void R438_gate_go_on_false_stops_the_run_and_the_read_never_starts()
        {
            Start(Wrapped(false, "Cancelled in the scan window, nothing was read."));
            Assert.Single(gated);
            Assert.Equal(0, readStarts);
            Assert.Equal("Cancelled in the scan window, nothing was read.", ended.Single().Failure);
            Assert.Empty(Directory.GetDirectories(work, "read_*"));
        }

        [Fact]
        public void R438_a_scan_that_did_not_finish_still_starts_the_read_with_its_line()
        {
            worker.Status = "failed";
            Start(Wrapped(true, "never"));
            Assert.Single(worker.Requests);
            Assert.Empty(gated);
            Assert.Equal(1, readStarts);
            Assert.Null(ended.Single().Failure);
            string expected = string.Format(ScanStep.NotFinishedLine, "failed, the worker ran past its time limit of 600 s and was ended");
            Assert.Equal(expected + Environment.NewLine + "the read ended", ended.Single().Message);
        }

        [Fact]
        public void R438_a_second_press_while_the_scan_runs_starts_nothing()
        {
            worker.Answer = false;
            ButtonBody body = Wrapped(true, "clear");
            Start(body);
            Assert.Throws<InvalidOperationException>(() => body.Start(Context("2_20261031-120001-000"), later => ended.Add(later)));
            Assert.Throws<InvalidOperationException>(() => body.Preview(Context("2_20261031-120002-000")));
            Assert.Single(worker.Requests);
            Assert.Equal(0, readStarts);
        }

        private IReadOnlyList<WorkerFile> Inputs()
        {
            return ScanStep.Inputs(pane, Roles, prototypes);
        }

        // A scan folder whose scan.json lists the inputs as the worker command scan does, by role, name, size and SHA-256
        private string Scanned(string folder, IReadOnlyList<WorkerFile> inputs)
        {
            return Write(Path.Combine(work, folder, ScanStep.ResultFile), Worker.Result(inputs).ToString());
        }

        private ButtonBody Wrapped(bool goOn, string line)
        {
            var read = new ButtonBody
            {
                Preview = context => new RunSummary("the read", null),
                Start = (context, done) =>
                {
                    readStarts++;
                    done(RunLater.Done("the read ended", null, null));
                },
            };
            return ScanStep.Wrap(read, worker, new WorkerDocument { Path = @"C:\Models\site.rvt", SessionId = "s1" }, Inputs, path =>
            {
                gated.Add(path);
                return new ScanGateAnswer { GoOn = goOn, Line = line };
            });
        }

        private void Start(ButtonBody body, string runId = RunId)
        {
            body.Start(Context(runId), later => ended.Add(later));
        }

        private RunContext Context(string runId)
        {
            return new RunContext(2, Label, runId, work, null, true, pane, new Dictionary<string, string>());
        }

        private static string Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        private sealed class Worker : IWorkerStart
        {
            public List<WorkerRequest> Requests { get; } = new List<WorkerRequest>();
            public bool Answer { get; set; } = true;
            public string Status { get; set; }

            public static JObject Result(IEnumerable<WorkerFile> inputs)
            {
                return new JObject
                {
                    ["inputs"] = new JArray(inputs.Select(item =>
                    {
                        InputMark mark = InputGuard.Mark(item.Path);
                        return new JObject { ["role"] = item.Role, ["name"] = mark.Name, ["size"] = mark.Size, ["sha256"] = mark.Sha256 };
                    })),
                    ["rows"] = new JArray(), ["conflicts"] = new JArray(), ["checks"] = new JArray(), ["plots"] = new JArray(),
                };
            }

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                if (!Answer)
                {
                    return;
                }
                var commands = new Dictionary<string, WorkerCommand>
                {
                    ["scan"] = context => context.WriteJson(request.OutputFolder, ScanStep.ResultFile, Result(request.Inputs)),
                };
                WorkerRunner.Run(requestPath, commands, () => 1e6, () => 0.0);
                WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId));
                if (Status != null)
                {
                    answer = new WorkerAnswer { RunId = answer.RunId, Document = answer.Document, Status = Status, Message = "the worker ran past its time limit of 600 s and was ended" };
                }
                ended(answer);
            }
        }

        [Fact]
        public void R439_a_scan_older_than_the_rules_profile_or_the_worker_is_not_reused()
        {
            string work = Path.Combine(Path.GetTempPath(), "sa_r439_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(work);
                string input = Path.Combine(work, "site.dwg");
                File.WriteAllText(input, "x");
                string sha = FileHash.Sha256(input);
                string scan = Path.Combine(work, "scan_2_20261101-010000-000", "scan.json");
                Directory.CreateDirectory(Path.GetDirectoryName(scan));
                File.WriteAllText(scan, new JObject { ["inputs"] = new JArray { new JObject { ["role"] = "scanCad:1", ["name"] = "site.dwg", ["size"] = 1, ["sha256"] = sha } } }.ToString());
                File.SetLastWriteTimeUtc(scan, new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc));
                string rules = Path.Combine(work, "rules.json");
                File.WriteAllText(rules, "{}");
                var inputs = new[] { new WorkerFile { Role = "scanCad:1", Path = input, Sha256 = sha } };

                File.SetLastWriteTimeUtc(rules, new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc));
                Assert.Equal(scan, ScanStep.Reusable(work, inputs, new[] { rules }));
                File.SetLastWriteTimeUtc(rules, new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc));
                Assert.Null(ScanStep.Reusable(work, inputs, new[] { rules }));
                Assert.Equal(scan, ScanStep.Reusable(work, inputs));
            }
            finally
            {
                Directory.Delete(work, true);
            }
        }
    }
}
