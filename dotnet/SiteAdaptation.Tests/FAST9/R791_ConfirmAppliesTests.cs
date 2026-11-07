#if NETFRAMEWORK
using System;
using System.Collections.Generic;
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
    // R790 to R792, F22 of FAST-9. One read of a new project, one Confirm with the proposed columns, and the Scope list names the areas of the
    // confirmed area column and the plots carry the villa types of the confirmed column, with no second worker run. The project is the made up
    // one of samples/new-project, every name and number there is invented
    // The windows of ConfirmProfileStep are one per process, so this class runs with the R771 classes, alone
    [Collection(QuietReadCollection.Name)]
    public sealed class R791_ConfirmAppliesTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa_r791_" + Guid.NewGuid().ToString("N"));
        private readonly Action<IntPtr, DataCheckResult> checkBefore = ConfirmProfileStep.ShowCheck;
        private readonly Action<IntPtr, ConfirmProfileSession> confirmBefore = ConfirmProfileStep.ShowConfirm;
        private readonly Func<bool> quietBefore = QuietRead.Now;
        private int runs;

        public R791_ConfirmAppliesTests()
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
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            config["inputRoot"] = Folder;
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["siteBuild"]["minFreeGB"] = 0.1;
            string path = Path.Combine(root, "project.json");
            File.WriteAllText(path, config.ToString());
            return path;
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

        private sealed class NoModel : IModel
        {
            public IModelTransaction Start(string name)
            {
                throw new InvalidOperationException("button 2 opens no transaction");
            }
        }

        private sealed class Yes : IConfirm
        {
            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                return true;
            }
        }

        // The worker command itself, every run counted
        private sealed class Worker : IWorkerStart
        {
            public readonly List<WorkerRequest> Requests = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                WorkerRunner.Run(requestPath, new Dictionary<string, WorkerCommand> { ["dataCheck"] = CadReader.DataCheckCommand.Run }, () => 1e6, () => 0.0);
                ended(WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId)));
            }
        }

        private const string Document = @"C:\Models\site.rvt";

        // One press of 2 Read data through the flow, the Confirm step wrapped as ReadDataBody wraps it. apply false is the press before R791
        private RunOutcome Press(string configPath, string work, Worker worker, bool apply = true)
        {
            var roles = new Dictionary<string, string> { ["Settings"] = ReadInputs.Config };
            var run = new ReadDataRun(roles, worker, new WorkerDocument { Path = Document, SessionId = "s1" }, () => new SettingsScope()) { ModePick = () => DesignBasis.Rev6 };
            var settings = new PaneSettings(new ProjectSettings { ConfigFile = configPath });
            ButtonBody body = ConfirmProfileStep.Wrap(run.Body(), settings, IntPtr.Zero, apply ? run.ApplyConfirmed : (Func<RunContext, RunLater, RunLater>)null);
            var flow = new ButtonFlow(new State(configPath, work), new NoModel(), new Yes(), InputHash.Of, () => RunIds.New(2, DateTime.Now) + "-" + (++runs).ToString("D3"));
            RunOutcome last = null;
            RunOutcome started = flow.Run(2, "2 Read data", body, Document, outcome => last = outcome);
            return started.End == RunEnd.Started ? last : started;
        }

        // The columns of the sheet picked as a person would in the window, the proposed area column where the window proposes one
        private static void ConfirmWithColumns(ConfirmProfileSession session)
        {
            session.Model.Typology = session.Model.TypologyColumns.First(choice => choice.Column == "House type");
            session.Model.Area = session.Model.Area ?? session.Model.AreaColumns.First(choice => choice.Column == "Phase");
            session.Confirm();
        }

        // The sheet of the made up project with plot 103 given another type than its block in the drawing, so a villa type that comes from the
        // confirmed column can be told from one that comes from the drawing. Its areas part the plots unlike the distance does, as in S1 of F22
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

        [Fact]
        public void R791_one_read_and_one_confirm_give_the_areas_and_villa_types_of_the_confirmed_columns_with_no_second_worker_run()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker();
            ConfirmProfileStep.ShowConfirm = (owner, session) => ConfirmWithColumns(session);

            RunOutcome first = Press(configPath, work, worker);
            Assert.True(first.End == RunEnd.Committed, first.Message);
            Assert.Single(worker.Requests);
            Assert.Contains(ConfirmProfileStep.AppliedLine, first.Message);

            // The Scope list is the neighbourhoods of the plot table of the latest read
            Assert.Equal(new[] { "N1", "N2" }, Areas(work));
            string read = ReviewLoad.LatestReadFolder(work);
            Assert.Equal("column", (string)JObject.Parse(File.ReadAllText(Path.Combine(read, "plots.json")))["areaFrom"]);
            Assert.Equal("VA1", (string)Plot(read, "103")["villaType"]);
            Assert.Equal("VB2", (string)Plot(read, "104")["villaType"]);
            Assert.Equal("N2", (string)Plot(read, "201")["area"]);
            Assert.Equal(ConfirmedProfile.PersonEntered, (string)Plot(read, "201")["source"]["area"]);

            // The record lists the confirmed profile as an input and every output with its hash, so the next press reuses it and reads nothing
            JObject record = JObject.Parse(File.ReadAllText(Path.Combine(read, ReadDataRun.RecordFile)));
            Assert.Contains(((JArray)record["inputs"]).Select(input => (string)input["role"]), role => role == ConfirmedProfile.Role);
            foreach (JObject output in ((JArray)record["outputs"]).OfType<JObject>())
            {
                Assert.Equal((string)output["sha256"], FileHash.Sha256((string)output["path"]), StringComparer.OrdinalIgnoreCase);
                Assert.StartsWith(read, (string)output["path"], StringComparison.OrdinalIgnoreCase);
            }
            Assert.Single(Directory.GetDirectories(work, ReadDataRun.FolderPrefix + "*"));

            RunOutcome again = Press(configPath, work, worker);
            Assert.True(again.End == RunEnd.Committed, again.Message);
            Assert.Single(worker.Requests);
            Assert.Contains("is reused, nothing was read again", again.Message);
            Assert.Equal(new[] { "N1", "N2" }, Areas(work));
        }

        // The same press with no apply, the press before R791: the Scope list and the plots keep the read before the Confirm, F22
        [Fact]
        public void R791_with_no_apply_the_scope_list_lacks_the_areas_of_the_column_so_the_test_above_sees_F22()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker();
            ConfirmProfileStep.ShowConfirm = (owner, session) => ConfirmWithColumns(session);

            RunOutcome first = Press(configPath, work, worker, false);
            Assert.True(first.End == RunEnd.Committed, first.Message);
            Assert.DoesNotContain(ConfirmProfileStep.AppliedLine, first.Message);
            Assert.NotEqual(new[] { "N1", "N2" }, Areas(work));
            Assert.Equal("VB2", (string)Plot(ReviewLoad.LatestReadFolder(work), "103")["villaType"]);
        }

        // The second check, by another way: a full worker read of the same folder with the same confirmed profile gives the same outputs as
        // the check run again on the read before the Confirm, but for the run stamp
        [Fact]
        public void R791_the_applied_read_equals_a_full_worker_read_with_the_same_confirmed_profile()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker();
            ConfirmProfileStep.ShowConfirm = (owner, session) => ConfirmWithColumns(session);
            Assert.True(Press(configPath, work, worker).End == RunEnd.Committed);
            string applied = ReviewLoad.LatestReadFolder(work);

            string other = Work("other");
            File.Copy(Path.Combine(work, ConfirmedProfile.FileName), Path.Combine(other, ConfirmedProfile.FileName));
            ConfirmProfileStep.ShowConfirm = (owner, session) => session.Cancel();
            var full = new Worker();
            Assert.True(Press(configPath, other, full).End == RunEnd.Committed);
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

        // A file added to the folder while the read ran is no part of the read: the applied read keeps the inputs of its start, so the next press
        // reads the folder again and never reuses a read that did not see the file
        [Fact]
        public void R791_a_folder_changed_during_the_read_is_read_again_at_the_next_press()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker();
            ConfirmProfileStep.ShowConfirm = (owner, session) =>
            {
                File.WriteAllText(Path.Combine(Folder, "added.txt"), "made up");
                ConfirmWithColumns(session);
            };

            RunOutcome first = Press(configPath, work, worker);
            Assert.True(first.End == RunEnd.Committed, first.Message);
            Assert.Contains(ConfirmProfileStep.AppliedLine, first.Message);
            Assert.Equal(new[] { "N1", "N2" }, Areas(work));

            ConfirmProfileStep.ShowConfirm = (owner, session) => session.Cancel();
            RunOutcome again = Press(configPath, work, worker);
            Assert.True(again.End == RunEnd.Committed, again.Message);
            Assert.Equal(2, worker.Requests.Count);
            Assert.DoesNotContain("is reused", again.Message);
        }

        // The check runs on the settings of the read: a settings file changed since the read started refuses the apply with its reason
        [Fact]
        public void R792_a_settings_file_changed_since_the_read_refuses_the_apply()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker();
            ConfirmProfileStep.ShowConfirm = (owner, session) =>
            {
                File.AppendAllText(configPath, "\n");
                ConfirmWithColumns(session);
            };

            RunOutcome first = Press(configPath, work, worker);
            Assert.True(first.End == RunEnd.Committed, first.Message);
            Assert.Contains(ConfirmProfileStep.NotAppliedLine.Replace("<why>", ReadDataRun.ApplyConfigChanged), first.Message);
            Assert.NotEqual(new[] { "N1", "N2" }, Areas(work));
        }

        // A read from before R790 holds no gathered files: the Confirm saves, the read stays done as it was and the line says the values wait
        [Fact]
        public void R792_a_read_with_no_gathered_files_keeps_the_read_and_says_the_values_count_from_the_next_read()
        {
            string configPath = Project();
            Sheet();
            string work = Work();
            var worker = new Worker();
            ConfirmProfileStep.ShowConfirm = (owner, session) =>
            {
                File.Delete(Path.Combine(ReviewLoad.LatestReadFolder(work), DataCheckApply.InputFile));
                ConfirmWithColumns(session);
            };

            RunOutcome first = Press(configPath, work, worker);
            Assert.True(first.End == RunEnd.Committed, first.Message);
            Assert.Contains(ConfirmProfileStep.NotAppliedLine.Replace("<why>", ReadDataRun.ApplyNoInput), first.Message);
            Assert.NotEqual(new[] { "N1", "N2" }, Areas(work));
            Assert.True(File.Exists(Path.Combine(work, ConfirmedProfile.FileName)));
        }
    }
}
#endif
