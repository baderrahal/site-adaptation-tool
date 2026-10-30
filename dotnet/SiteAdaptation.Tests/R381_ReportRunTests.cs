using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CadReader;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Report;
using SiteAdaptation.Common.Tags;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R381, R365. The body of button 9 Report through the flow of R382 on a made up repo and work folder, with a stand in of the model and of the
    // worker, or the worker command reportExcel run in process, on net48 and net8.0. The run log of 4 Check is made here, as 4 is not built yet
    public sealed class R381_ReportRunTests : IDisposable
    {
        private const string RunId = "9_20261030-120000-000";
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa_r381_" + Guid.NewGuid().ToString("N"));
        private readonly string work;
        private readonly string config;

        public R381_ReportRunTests()
        {
            work = Path.Combine(root, "work");
            config = Path.Combine(root, "config", "project.json");
            Directory.CreateDirectory(work);
            Directory.CreateDirectory(Path.GetDirectoryName(config));
            Directory.CreateDirectory(Path.Combine(root, "rules"));
            File.WriteAllText(config, ReportExcelTests.MadeConfig().ToString());
            JObject rules = ReportExcelTests.MadeRules();
            rules["levelProfiles"]["default"] = "parsons2024Rev6";
            File.WriteAllText(Path.Combine(root, "rules", "r.json"), rules.ToString());
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void R381_report_stops_with_needs_step_4_while_no_real_run_of_4_check_lies_in_the_work_folder_or_its_plan_changed()
        {
            var asked = new List<WorkerRequest>();
            Model model;
            State state;
            RunOutcome outcome = Run(false, true, Stand(asked), out model, out state);
            Assert.Equal(RunEnd.Stopped, outcome.End);
            Assert.Contains("9 Report needs step 4: the work folder holds no run log of a real run of 4 Check", outcome.Message);
            Assert.Equal(0, model.Started);
            Assert.Empty(asked);

            CheckRunLog(committed: false);
            outcome = Run(false, true, Stand(asked), out model, out state);
            Assert.Contains("9 Report needs step 4", outcome.Message);

            CheckRunLog();
            File.AppendAllText(Path.Combine(work, "plan_012.json"), " ");
            outcome = Run(false, true, Stand(asked), out model, out state);
            Assert.Equal(RunEnd.Stopped, outcome.End);
            Assert.Contains("9 Report needs step 4: plan_012.json on disk is not the one the run 4_20261030-110000-000 wrote or read", outcome.Message);
            Assert.Empty(asked);
            Assert.False(File.Exists(Path.Combine(work, "012_Report.xlsx")));
        }

        [Fact]
        public void R381_the_dry_run_lists_the_file_it_would_write_and_the_move_of_the_earlier_one_and_writes_nothing()
        {
            CheckRunLog();
            var asked = new List<WorkerRequest>();
            Model model;
            State state;
            var confirm = new Confirm { Answer = false };
            RunOutcome outcome = Run(true, confirm, Stand(asked), out model, out state);
            Assert.Equal(RunEnd.Cancelled, outcome.End);
            Assert.True(confirm.DryRun);
            Assert.Equal("Report Excel files: 1", confirm.Summary.Counts);
            Assert.Equal(new[] { "Writes " + Path.Combine(work, "012_Report.xlsx") }, confirm.Summary.PerPlot.ToArray());
            Assert.Empty(asked);
            Assert.Equal(0, model.Started);
            Assert.False(File.Exists(Path.Combine(work, "012_Report.xlsx")));

            string earlier = Path.Combine(work, "012_Report.xlsx");
            File.WriteAllBytes(earlier, new byte[] { 1, 2, 3 });
            var time = new DateTime(2026, 10, 29, 8, 30, 15, 250, DateTimeKind.Local);
            File.SetLastWriteTime(earlier, time);
            confirm = new Confirm { Answer = false };
            Run(true, confirm, Stand(asked), out model, out state);
            Assert.Equal("Moves the earlier 012_Report.xlsx to " + Path.Combine(work, "earlier", "20261029-083015-250", "012_Report.xlsx"), confirm.Summary.PerPlot[1]);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(earlier));
            Assert.False(Directory.Exists(Path.Combine(work, "earlier")));
            Assert.Empty(asked);
        }

        // The request goes to the worker command itself, so a role it does not take or a count file it refuses fails here. The Revit column of the
        // workbook read back, the first after the item as ReportExcelTests reads it, holds the counts the stand in of the model gave for the plots the
        // run log of 6 lists
        [Fact]
        public void R381_the_request_holds_the_plan_the_step_files_of_4_the_run_logs_and_the_counts_and_the_worker_writes_the_report_of_the_nbh()
        {
            CheckRunLog();
            string log1 = RunLog(1, "1_20261030-090000-000", null);
            RunLog(6, "6_20261030-080000-000", new JArray("101"));
            string log6 = RunLog(6, "6_20261030-115000-000", new JArray("101", "102"));
            RunLog(6, "6_20261030-115900-000", new JArray("101", "102", "103"), committed: false);
            var asked = new List<WorkerRequest>();
            var levels = new List<string>();
            Model model;
            State state;
            Func<WorkerRequest, string, WorkerAnswer> worker = (request, folder) =>
            {
                asked.Add(request);
                int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
                WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, request.RunId));
                Assert.True(code == 0, "exit " + code + ": " + answer.Message);
                return answer;
            };
            RunOutcome outcome = Run(false, true, worker, out model, out state, (plots, run) =>
            {
                levels.AddRange(plots);
                levels.Add(run);
                return RevitCounts.FromTags(new PlacedTag[0], plots, run);
            });
            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            Assert.Null(outcome.Message);
            Assert.Equal(new[] { "101", "102", "6_20261030-115000-000" }, levels.ToArray());

            WorkerRequest asked1 = Assert.Single(asked);
            Assert.Equal("reportExcel", asked1.Command);
            Assert.Equal(RunId + "-012", asked1.RunId);
            Assert.Equal("parsons2024Rev6", asked1.LevelProfile);
            Assert.Equal("012", asked1.Scope.Nbh);
            Assert.Equal(new[] { "config", "rules", "plan", "checkRows", "summary", "plotsCsv", "overrides", "revitCounts", "runLog1", "runLog4", "runLog6" },
                asked1.Inputs.Select(input => input.Role).ToArray());
            Assert.Equal(Path.Combine(work, "plan_012.json"), asked1.Inputs.Single(input => input.Role == "plan").Path);
            Assert.Equal(log1, asked1.Inputs.Single(input => input.Role == "runLog1").Path);
            Assert.Equal(log6, asked1.Inputs.Single(input => input.Role == "runLog6").Path);
            Assert.Equal(Path.Combine(work, "revit_counts_" + RunId + ".json"), asked1.Inputs.Single(input => input.Role == "revitCounts").Path);
            Assert.All(asked1.Inputs, input => Assert.Equal(FileHash.Sha256(input.Path), input.Sha256));

            string report = Path.Combine(work, "012_Report.xlsx");
            Assert.Equal(FileHash.Sha256(report), outcome.Record.Outputs[report]);
            Assert.Equal(new[] { report }, outcome.Record.Outputs.Keys.ToArray());
            Assert.False(Directory.Exists(Path.Combine(work, "report_" + RunId)), "the folder the worker wrote to is gone, with the 013 book of the plan");
            Assert.Equal(new[] { 9 }, state.Told.ToArray());
            Assert.Equal(1, model.Started);
            LevelExcelTests.Book book = LevelExcelTests.Book.Read(report);
            Assert.Equal(new object[] { "Links", 0.0, 2.0 }, book.Row("Summary", 15).Take(3).ToArray());
            Assert.Equal(new object[] { "Levels", 2.0, "no run of button 6 yet" }, book.Row("Summary", 17).Take(3).ToArray());
        }

        [Fact]
        public void R381_a_second_run_moves_the_earlier_report_to_an_earlier_folder_with_its_time_and_writes_the_new_one()
        {
            CheckRunLog();
            string report = Path.Combine(work, "012_Report.xlsx");
            File.WriteAllBytes(report, new byte[] { 1, 2, 3 });
            File.SetLastWriteTime(report, new DateTime(2026, 10, 29, 8, 30, 15, 250, DateTimeKind.Local));
            var asked = new List<WorkerRequest>();
            Model model;
            State state;
            RunOutcome outcome = Run(false, true, Stand(asked), out model, out state);
            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(work, "earlier", "20261029-083015-250", "012_Report.xlsx")));
            Assert.Equal(new byte[] { 7, 7 }, File.ReadAllBytes(report));
            Assert.Empty(Directory.GetFiles(work, "*.tmp-*", SearchOption.AllDirectories));
            Assert.True(outcome.Record.StepDone);
        }

        // Linux locks no file another program holds open, so this half runs on Windows, where the add-in runs
        [WindowsFact]
        public void R381_a_report_another_program_holds_open_leaves_step_9_not_done_with_the_file_named()
        {
            CheckRunLog();
            string report = Path.Combine(work, "012_Report.xlsx");
            File.WriteAllBytes(report, new byte[] { 1, 2, 3 });
            var asked = new List<WorkerRequest>();
            Model model;
            State state;
            RunOutcome outcome;
            using (new FileStream(report, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                outcome = Run(false, true, Stand(asked), out model, out state);
            }
            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Contains("012_Report.xlsx was not saved: another program holds it open", outcome.Message);
            Assert.False(outcome.Record.StepDone);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(report));
            Assert.Equal(new byte[] { 7, 7 }, File.ReadAllBytes(report + WorkFiles.Marker + RunId));
        }

        [Fact]
        public void R381_a_worker_that_fails_leaves_step_9_not_done_with_the_file_named()
        {
            CheckRunLog();
            File.WriteAllBytes(Path.Combine(work, "012_Report.xlsx"), new byte[] { 1, 2, 3 });
            Model model;
            State state;
            RunOutcome outcome = Run(false, true, (request, folder) => { throw new InvalidOperationException("the worker ended timedOut, time limit"); }, out model, out state);
            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.Contains("012_Report.xlsx was not written: the worker ended timedOut, time limit", outcome.Message);
            Assert.Empty(state.Told);
            Assert.Equal(0, model.Started);
            Assert.Empty(Directory.GetFiles(work, "*.tmp-*", SearchOption.AllDirectories));
        }

        [Fact]
        public void R381_the_entry_of_button_9_hands_the_application_to_its_body_in_a_few_lines_of_buttons_cs()
        {
            string addin = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn");
            string buttons = File.ReadAllText(Path.Combine(addin, "Commands", "Buttons.cs"));
            Assert.Matches(@"\bReportBody\.For\(application\)", buttons);
            string body = File.ReadAllText(Path.Combine(addin, "Commands", "ReportBody.cs"));
            Assert.Contains("new ReportRun(", body);
            Assert.Contains("RevitCountsReader.Read(document, levelPlots, levelRun)", body);
        }

        // The run log of a real run of 4 Check as ReportRun reads it, the plan, the step files and the inputs it read in the work folder
        private void CheckRunLog(bool committed = true)
        {
            string plan = Write("plan_012.json", ReportExcelTests.MadePlan().ToString());
            string rows = Write(Path.Combine("steps", "step_checkRows_csharp_012.json"), ReportExcelTests.MadeCheckRows().ToString());
            string summary = Write(Path.Combine("steps", "step_summary_csharp_012.json"), ReportExcelTests.MadeSummary().ToString());
            string plots = Write("plots.csv", ReportExcelTests.PlotsCsv);
            string overrides = Write("overrides.json", ReportExcelTests.MadeOverrides().ToString());
            Func<string, string, string, JObject> item = (role, nbh, path) =>
            {
                var made = new JObject { ["role"] = role, ["path"] = path.Substring(work.Length + 1), ["sha256"] = FileHash.Sha256(path) };
                if (nbh != null)
                {
                    made["nbh"] = nbh;
                }
                return made;
            };
            var log = new JObject
            {
                ["runId"] = "4_20261030-110000-000", ["committed"] = committed,
                ["inputs"] = new JArray(item("plotsCsv", null, plots), item("overrides", null, overrides)),
                ["outputs"] = new JArray(item("plan", "012", plan), item("checkRows", "012", rows), item("summary", "012", summary)),
            };
            Write("run_4_20261030-110000-000.json", log.ToString());
        }

        private string RunLog(int button, string runId, JArray levelPlots, bool committed = true)
        {
            var log = new JObject { ["runId"] = runId, ["committed"] = committed };
            if (levelPlots != null)
            {
                log["levelPlots"] = levelPlots;
            }
            return Write("run_" + runId + ".json", log.ToString());
        }

        private string Write(string name, string text)
        {
            string path = Path.Combine(work, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        // A worker that writes a made up book of the NBH into its output folder, as the runner leaves it on its name
        private static Func<WorkerRequest, string, WorkerAnswer> Stand(List<WorkerRequest> asked)
        {
            return (request, folder) =>
            {
                asked.Add(request);
                Directory.CreateDirectory(request.OutputFolder);
                string book = Path.Combine(request.OutputFolder, request.Scope.Nbh + "_Report.xlsx");
                File.WriteAllBytes(book, new byte[] { 7, 7 });
                return new WorkerAnswer { RunId = request.RunId, Status = "done", Outputs = new List<WorkerFile> { new WorkerFile { Role = "report", Path = book } }, Issues = new List<string>() };
            };
        }

        private RunOutcome Run(bool dryFirst, bool apply, Func<WorkerRequest, string, WorkerAnswer> worker, out Model model, out State state,
            Func<ICollection<string>, string, JObject> counts = null)
        {
            return Run(dryFirst, new Confirm { Answer = apply }, worker, out model, out state, counts);
        }

        private RunOutcome Run(bool dryFirst, Confirm confirm, Func<WorkerRequest, string, WorkerAnswer> worker, out Model model, out State state,
            Func<ICollection<string>, string, JObject> counts = null)
        {
            var run = new ReportRun("Settings", WorkFiles.ReadText, counts ?? ((plots, levelRun) => RevitCounts.FromTags(new PlacedTag[0], plots, levelRun)), worker,
                new WorkerDocument { Path = @"C:\Models\Site.rvt", SessionId = "s" });
            model = new Model();
            state = new State { Work = work, Config = config, DryFirst = dryFirst };
            var flow = new ButtonFlow(state, model, confirm, InputHash.Of, () => RunId);
            return flow.Run(9, "9 Report", run.Body(), @"C:\Models\Site.rvt");
        }

        private sealed class Confirm : IConfirm
        {
            public bool Answer;
            public RunSummary Summary;
            public bool DryRun;

            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                Summary = summary;
                DryRun = dryRun;
                return Answer;
            }
        }

        private sealed class State : IRunState
        {
            public string Work;
            public string Config;
            public bool DryFirst;
            public readonly List<int> Told = new List<int>();

            public string WorkFolder(string documentPath) { return Work; }
            public bool DryRunFirst { get { return DryFirst; } }
            public int MissingStep(int button) { return 0; }
            public IReadOnlyList<RunInput> Inputs(int button) { return new[] { new RunInput("Settings", Config, false) }; }
            public void Committed(int button, RunRecord record) { if (record.StepDone) Told.Add(button); }
        }

        private sealed class Transaction : IModelTransaction
        {
            public bool Commit() { return true; }
            public void RollBack() { }
            public void Dispose() { }
            public IReadOnlyList<string> Warnings { get { return new string[0]; } }
            public IReadOnlyList<string> Errors { get { return new string[0]; } }
        }

        private sealed class Model : IModel
        {
            public int Started;
            public IModelTransaction Start(string name) { Started++; return new Transaction(); }
        }
    }
}
