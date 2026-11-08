#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.ConfirmProfile;
using SiteAdaptation.AddIn.Fast7;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R820 to R823, F22 of FAST-15. One read of a new project, one Confirm whose check runs in the worker on the gathered files of that read, and
    // the Scope list names the areas of the confirmed column with no second read and no gathered file loaded in Revit. A second press with
    // nothing changed reuses the read in seconds. The project is the made up one of samples/new-project, every name and number is invented
    // The windows of ConfirmProfileStep are one per process, so this class runs with the R771 classes, alone
    [Collection(QuietReadCollection.Name)]
    public sealed class R820_ConfirmInWorkerTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa_r820_" + Guid.NewGuid().ToString("N"));
        private readonly Action<IntPtr, DataCheckResult> checkBefore = ConfirmProfileStep.ShowCheck;
        private readonly Action<IntPtr, ConfirmProfileSession> confirmBefore = ConfirmProfileStep.ShowConfirm;
        private readonly Func<bool> quietBefore = QuietRead.Now;
        private int runs;

        public R820_ConfirmInWorkerTests()
        {
            QuietRead.Now = () => false;
            ConfirmProfileStep.ShowCheck = (owner, result) => { };
        }

        public void Dispose()
        {
            ConfirmProfileStep.ShowCheck = checkBefore;
            ConfirmProfileStep.ShowConfirm = confirmBefore;
            QuietRead.Now = quietBefore;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private string Folder => Path.Combine(root, "handed over");

        private string Project()
        {
            Directory.CreateDirectory(Folder);
            string sample = Path.Combine(ThemeGateTests.RepoRoot(), "samples", "new-project");
            foreach (string file in Directory.GetFiles(sample, "*.dxf").Concat(Directory.GetFiles(sample, "*.xlsx")))
            {
                File.Copy(file, Path.Combine(Folder, Path.GetFileName(file)), true);
            }
            Directory.CreateDirectory(Path.Combine(Folder, "Prototypes"));
            foreach (string file in Directory.GetFiles(Path.Combine(sample, "Prototypes")))
            {
                File.Copy(file, Path.Combine(Folder, "Prototypes", Path.GetFileName(file)), true);
            }
            JObject config = Config();
            config["inputRoot"] = Folder;
            string path = Path.Combine(root, "project.json");
            File.WriteAllText(path, config.ToString());
            return path;
        }

        private static JObject Config()
        {
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["siteBuild"]["minFreeGB"] = 0.1;
            return config;
        }

        private string Work(string name = "model")
        {
            string work = Path.Combine(root, "work", name);
            Directory.CreateDirectory(work);
            return work;
        }

        private sealed class State : IRunState
        {
            private readonly string configPath;
            private readonly string work;

            public State(string configPath, string work, bool dryRun)
            {
                this.configPath = configPath;
                this.work = work;
                DryRunFirst = dryRun;
            }

            public bool DryRunFirst { get; }

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

        private sealed class NoModel : IModel
        {
            public IModelTransaction Start(string name)
            {
                throw new InvalidOperationException("button 2 opens no transaction");
            }
        }

        // Says yes to every prompt and keeps the dry run line it was shown
        private sealed class Yes : IConfirm
        {
            public RunSummary Asked;

            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                Asked = summary;
                return true;
            }
        }

        // The worker command itself, every request kept. Held keeps the Confirm run back until Release, as the worker runs while Revit is free
        private sealed class Worker : IWorkerStart
        {
            public readonly List<WorkerRequest> Requests = new List<WorkerRequest>();
            public bool HoldConfirm;
            public bool FailConfirm;
            private Action held;

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                Action run = () =>
                {
                    if (FailConfirm && request.RunId.EndsWith(ReadDataRun.ConfirmRunSuffix, StringComparison.Ordinal))
                    {
                        ended(null);
                        return;
                    }
                    WorkerRunner.Run(requestPath, new Dictionary<string, WorkerCommand> { ["dataCheck"] = CadReader.DataCheckCommand.Run }, () => 1e6, () => 0.0);
                    ended(WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId)));
                };
                if (HoldConfirm && request.RunId.EndsWith(ReadDataRun.ConfirmRunSuffix, StringComparison.Ordinal))
                {
                    held = run;
                    return;
                }
                run();
            }

            public void Release()
            {
                Action run = held;
                held = null;
                run();
            }
        }

        private const string Document = @"C:\Models\site.rvt";

        private sealed class Pressed
        {
            public RunOutcome Started;
            public RunOutcome Last;
            public Yes Confirm;
        }

        // One press of 2 Read data through the flow, the Confirm step wrapped as ReadDataBody wraps it since R820
        private Pressed Press(string configPath, string work, Worker worker, bool dryRun = false)
        {
            var roles = new Dictionary<string, string> { ["Settings"] = ReadInputs.Config };
            var run = new ReadDataRun(roles, worker, new WorkerDocument { Path = Document, SessionId = "s1" }, () => new SettingsScope()) { ModePick = () => DesignBasis.Rev6 };
            var settings = new PaneSettings(new ProjectSettings { ConfigFile = configPath });
            ButtonBody body = ConfirmProfileStep.WrapLater(run.Body(), settings, IntPtr.Zero, run.ApplyConfirmedLater);
            var pressed = new Pressed { Confirm = new Yes() };
            var flow = new ButtonFlow(new State(configPath, work, dryRun), new NoModel(), pressed.Confirm, InputHash.Of, () => RunIds.New(2, DateTime.Now) + "-" + (++runs).ToString("D3"));
            pressed.Started = flow.Run(2, "2 Read data", body, Document, outcome => pressed.Last = outcome);
            return pressed;
        }

        private static RunOutcome End(Pressed pressed)
        {
            return pressed.Started.End == RunEnd.Started ? pressed.Last : pressed.Started;
        }

        private static void ConfirmWithColumns(ConfirmProfileSession session)
        {
            session.Model.Typology = session.Model.TypologyColumns.First(choice => choice.Column == "House type");
            session.Model.Area = session.Model.Area ?? session.Model.AreaColumns.First(choice => choice.Column == "Phase");
            session.Confirm();
        }

        // The sheet of the made up project, plot 103 another type than its block in the drawing and its areas parted unlike the distance does
        private void Sheet()
        {
            var rows = new List<string[]>
            {
                new[] { "Plot", "Phase", "Block", "House type" },
                new[] { "101", "N1", "A1", "VA1" }, new[] { "102", "N1", "A1", "VA1" }, new[] { "103", "N1", "A1", "VA1" }, new[] { "104", "N2", "A2", "VB2" },
                new[] { "105", "N2", "A2", "VA1" }, new[] { "201", "N2", "B1", "VB2" }, new[] { "202", "N2", "B1", "VA1" }, new[] { "203", "N2", "B1", "VA1" },
            };
            string path = Path.Combine(Folder, "plots.xlsx");
            File.Delete(path);
            using (var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart book = document.AddWorkbookPart();
                book.Workbook = new Workbook();
                var data = new SheetData();
                for (int index = 0; index < rows.Count; index++)
                {
                    var row = new Row { RowIndex = (uint)(index + 1) };
                    for (int column = 0; column < rows[index].Length; column++)
                    {
                        string reference = ((char)('A' + column)).ToString() + (index + 1).ToString();
                        row.Append(new Cell { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(rows[index][column])) });
                    }
                    data.Append(row);
                }
                var part = book.AddNewPart<WorksheetPart>();
                part.Worksheet = new Worksheet(data);
                book.Workbook.AppendChild(new Sheets()).Append(new Sheet { Id = book.GetIdOfPart(part), SheetId = 1, Name = "Plots" });
            }
        }

        private static JObject Plot(string readFolder, string number)
        {
            JObject plots = JObject.Parse(File.ReadAllText(Path.Combine(readFolder, "plots.json")));
            return ((JArray)plots["plots"]).OfType<JObject>().Single(row => (string)row["number"] == number);
        }

        private static string[] Areas(string work)
        {
            return ReviewLoad.LatestNbhCodes(work).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        }

        private static string[] Roles(WorkerRequest request)
        {
            return request.Inputs.Select(input => input.Role).OrderBy(role => role, StringComparer.Ordinal).ToArray();
        }

        [Fact]
        public void R820_one_read_and_one_confirm_through_the_worker_name_the_confirmed_areas_and_a_second_press_reuses_the_read()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker();
            ConfirmProfileStep.ShowConfirm = (owner, session) => ConfirmWithColumns(session);

            RunOutcome first = End(Press(configPath, work, worker));
            Assert.True(first.End == RunEnd.Committed, first.Message);
            Assert.Contains(ConfirmProfileStep.AppliedLine, first.Message);

            // One read of the folder, then the check of the Confirm in the worker on the gathered files, no folder walked again
            Assert.Equal(2, worker.Requests.Count);
            Assert.Contains(ReadInputs.ScanFolder, Roles(worker.Requests[0]));
            Assert.Equal(new[] { DataCheckApply.GatheredRole, ReadInputs.Config, ConfirmedProfile.Role }.OrderBy(role => role, StringComparer.Ordinal).ToArray(), Roles(worker.Requests[1]));
            Assert.EndsWith(ReadDataRun.ConfirmRunSuffix, worker.Requests[1].RunId);
            string read = ReviewLoad.LatestReadFolder(work);
            Assert.Equal(Path.GetFullPath(read), Path.GetFullPath(worker.Requests[1].OutputFolder));
            Assert.Equal(Path.Combine(Path.GetFullPath(read), DataCheckApply.InputFile), worker.Requests[1].Inputs.Single(input => input.Role == DataCheckApply.GatheredRole).Path);

            // The Scope list names the areas of the confirmed column, the plots carry the confirmed types
            Assert.Equal(new[] { "N1", "N2" }, Areas(work));
            Assert.Equal("column", (string)JObject.Parse(File.ReadAllText(Path.Combine(read, "plots.json")))["areaFrom"]);
            Assert.Equal("VA1", (string)Plot(read, "103")["villaType"]);
            Assert.Equal("N2", (string)Plot(read, "201")["area"]);
            JObject record = JObject.Parse(File.ReadAllText(Path.Combine(read, ReadDataRun.RecordFile)));
            Assert.Contains(((JArray)record["inputs"]).Select(input => (string)input["role"]), role => role == ConfirmedProfile.Role);
            foreach (JObject output in ((JArray)record["outputs"]).OfType<JObject>())
            {
                Assert.Equal((string)output["sha256"], FileHash.Sha256((string)output["path"]), StringComparer.OrdinalIgnoreCase);
            }
            Assert.Single(Directory.GetDirectories(work, ReadDataRun.FolderPrefix + "*"));

            // R821. A second press with nothing changed reads nothing: its dry run names the time of the read and the press takes seconds
            var clock = Stopwatch.StartNew();
            Pressed again = Press(configPath, work, worker, true);
            clock.Stop();
            Assert.True(End(again).End == RunEnd.Committed, End(again).Message);
            Assert.True(worker.Requests.Count == 2, again.Confirm.Asked?.Counts + " | " + End(again).Message);
            Assert.StartsWith("Reuses the read of ", again.Confirm.Asked.Counts);
            Assert.Contains(", nothing changed: the folder, the settings file and the values a person confirmed are unchanged.", again.Confirm.Asked.Counts);
            Assert.Contains("is reused, nothing was read again", End(again).Message);
            Assert.True(clock.Elapsed.TotalSeconds < 10, clock.Elapsed.TotalSeconds + " s");
            Assert.Equal(new[] { "N1", "N2" }, Areas(work));
        }

        // The press hands Revit back while the worker checks: no end until the worker answered, then the end with the applied line
        [Fact]
        public void R820_the_press_waits_on_the_worker_of_the_confirm_and_ends_on_its_answer()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker { HoldConfirm = true };
            ConfirmProfileStep.ShowConfirm = (owner, session) => ConfirmWithColumns(session);

            Pressed pressed = Press(configPath, work, worker);
            Assert.Equal(RunEnd.Started, pressed.Started.End);
            Assert.Null(pressed.Last);
            Assert.Equal(2, worker.Requests.Count);
            // While it runs the read folder holds no record, so no other press takes it half made
            Assert.False(File.Exists(Path.Combine(worker.Requests[1].OutputFolder, ReadDataRun.RecordFile)));

            worker.Release();
            Assert.NotNull(pressed.Last);
            Assert.True(pressed.Last.End == RunEnd.Committed, pressed.Last.Message);
            Assert.Contains(ConfirmProfileStep.AppliedLine, pressed.Last.Message);
            Assert.Equal(new[] { "N1", "N2" }, Areas(work));
        }

        // R792 kept: a worker that gives no answer leaves the read done with the reason, and the next press reads the folder again
        [Fact]
        public void R820_a_confirm_whose_worker_fails_keeps_the_read_done_and_the_next_press_reads_again()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker { FailConfirm = true };
            ConfirmProfileStep.ShowConfirm = (owner, session) => ConfirmWithColumns(session);

            RunOutcome first = End(Press(configPath, work, worker));
            Assert.True(first.End == RunEnd.Committed, first.Message);
            Assert.Contains("The confirmed values count from the next read, ", first.Message);
            Assert.DoesNotContain(ConfirmProfileStep.AppliedLine, first.Message);

            worker.FailConfirm = false;
            ConfirmProfileStep.ShowConfirm = (owner, session) => session.Cancel();
            RunOutcome again = End(Press(configPath, work, worker));
            Assert.True(again.End == RunEnd.Committed, again.Message);
            Assert.Equal(3, worker.Requests.Count);
            Assert.Contains(ReadInputs.ScanFolder, Roles(worker.Requests[2]));
            Assert.Equal(new[] { "N1", "N2" }, Areas(work));
        }

        // R821, R639. A read with a file not read is no read to reuse, and the dry run of the next press names the file that kept it from being whole
        [Fact]
        public void R821_a_read_that_is_not_whole_names_the_file_that_kept_it_from_being_whole()
        {
            string configPath = Project();
            Sheet();
            File.WriteAllText(Path.Combine(Folder, "broken.dwg"), "made up, no drawing");
            string work = Work();
            var worker = new Worker();
            ConfirmProfileStep.ShowConfirm = (owner, session) => session.Cancel();

            RunOutcome first = End(Press(configPath, work, worker));
            Assert.True(first.End == RunEnd.Committed, first.Message);
            Assert.Contains("broken.dwg", first.Message);

            Pressed again = Press(configPath, work, worker, true);
            Assert.Equal(2, worker.Requests.Count);
            Assert.Contains("Reads again, the last read of these files is not whole: files or folders not read 1, the first is broken.dwg", again.Confirm.Asked.Counts);
        }

        [Fact]
        public void R821_the_reuse_line_names_the_time_of_the_read_by_its_run_id()
        {
            Assert.Equal("Reuses the read of 04:13 on 8 Nov, nothing changed: the folder and the settings file are unchanged.", RunTexts.ReadFolderReuses("2_20261108-041329-266", false));
            Assert.Equal("09:05 on 1 Dec", RunTexts.RunTime("2_20261201-090501-001-build"));
            Assert.Equal("run odd", RunTexts.RunTime("odd"));
        }

        // R822. The line of one part of the read in the worker log, names and seconds only
        [Fact]
        public void R822_a_part_of_the_read_is_one_line_of_kind_name_and_seconds()
        {
            Assert.Equal("part | drawing | made up.dxf | 2.5 s", CadReader.DataCheckCommand.PartLine("made up.dxf", "drawing", 2.5));
        }

        // R823. Of the outline candidates of a drawing the gathered file keeps those of the top score, a tie whole, and drops the rest. Made up squares
        private static JObject Square(double x)
        {
            return new JObject { ["ring"] = new JArray(new JArray(x, 0.0), new JArray(x + 10, 0.0), new JArray(x + 10, 10.0), new JArray(x, 10.0)), ["centroid"] = new JArray(x + 5, 5.0), ["area"] = 100.0 };
        }

        private static JObject Read()
        {
            return new JObject
            {
                ["source"] = "made up.dxf", ["path"] = @"C:\made up.dxf", ["units"] = "Meters",
                ["layers"] = new JArray(),
                ["texts"] = new JArray(new JObject { ["text"] = "101", ["layer"] = "NUM", ["x"] = 5.0, ["y"] = 5.0 }, new JObject { ["text"] = "102", ["layer"] = "NUM", ["x"] = 15.0, ["y"] = 5.0 }),
                ["inserts"] = new JArray(),
                ["candidates"] = new JArray(
                    new JObject { ["layers"] = new JArray("PLOT"), ["faces"] = new JArray(Square(0), Square(10)) },
                    new JObject { ["layers"] = new JArray("ROAD"), ["faces"] = new JArray(Square(0)) },
                    new JObject { ["layers"] = new JArray("PLOT", "EDGE"), ["faces"] = new JArray(Square(0), Square(10)) }),
                ["lines"] = new JObject(), ["arcs"] = new JObject(), ["notRead"] = new JArray(), ["notTraced"] = new JArray(),
            };
        }

        [Fact]
        public void R823_the_gathered_file_keeps_the_outline_candidates_of_the_top_score_and_a_tie_whole()
        {
            DataCheckSettings settings = DataCheckSettings.From(Config());
            HashSet<string> kept = DataCheckApply.TopCandidates(settings, Read());
            Assert.NotNull(kept);
            Assert.Equal(new[] { "made up.dxf|EDGE + PLOT", "made up.dxf|PLOT" }, kept.OrderBy(key => key, StringComparer.Ordinal).ToArray());

            // Taken out of the read, the losing candidate is gone and the two of the tie stay in their order, the rest of the read as it was
            JObject read = Read();
            Assert.Equal(1, DataCheckApply.Prune(settings, read));
            Assert.Equal(new[] { "PLOT", "PLOT|EDGE" }, ((JArray)read["candidates"]).Select(candidate => string.Join("|", ((JArray)candidate["layers"]).Select(layer => (string)layer))).ToArray());
            Assert.Equal(2, ((JArray)read["texts"]).Count);
        }

        // A drawing whose best outline holds no plot number proposes none, so it keeps no candidate. With no settings nothing is cut
        [Fact]
        public void R823_a_drawing_with_no_scored_outline_keeps_no_candidate_and_no_settings_cuts_nothing()
        {
            JObject read = Read();
            read["texts"] = new JArray();
            Assert.Empty(DataCheckApply.TopCandidates(DataCheckSettings.From(Config()), read));
            Assert.Equal(3, DataCheckApply.Prune(DataCheckSettings.From(Config()), read));
            Assert.Empty((JArray)read["candidates"]);
            Assert.Null(DataCheckApply.TopCandidates(null, Read()));
            Assert.Equal(0, DataCheckApply.Prune(null, Read()));
        }

        // The second check by another way: a full worker read of the folder with the same confirmed profile gives the outputs of the check the
        // Confirm ran in the worker on the cut gathered file, but for the run stamp
        [Fact]
        public void R820_the_check_of_the_confirm_equals_a_full_worker_read_with_the_same_confirmed_profile()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker();
            ConfirmProfileStep.ShowConfirm = (owner, session) => ConfirmWithColumns(session);
            Assert.True(End(Press(configPath, work, worker)).End == RunEnd.Committed);
            string applied = ReviewLoad.LatestReadFolder(work);

            string other = Work("other");
            File.Copy(Path.Combine(work, ConfirmedProfile.FileName), Path.Combine(other, ConfirmedProfile.FileName));
            ConfirmProfileStep.ShowConfirm = (owner, session) => session.Cancel();
            var full = new Worker();
            Assert.True(End(Press(configPath, other, full)).End == RunEnd.Committed);
            Assert.Single(full.Requests);
            string read = ReviewLoad.LatestReadFolder(other);
            Assert.Equal(Path.GetFileName(applied), Path.GetFileName(read));
            foreach (string name in new[] { "data_check.json", "project_profile.json", "plots.json", ReadDataRun.PlotTableFile })
            {
                JObject left = JObject.Parse(File.ReadAllText(Path.Combine(applied, name)));
                JObject right = JObject.Parse(File.ReadAllText(Path.Combine(read, name)));
                left.Remove(RunStamp.Key);
                right.Remove(RunStamp.Key);
                Assert.True(JToken.DeepEquals(left, right), name + " differs");
            }
        }
    }
}
#endif
