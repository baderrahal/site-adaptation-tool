using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Settings;
using Xunit;
#if NET48
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ReviewData;
#endif

namespace SiteAdaptation.Tests
{
    // R639. The faults the review and the breaker of PR 209 found in Common, each reached by a person now button 2 is wired. A made up project only,
    // the codes, the sheet and the column names are invented here
    public sealed class R639_StageOneFixTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa_r639_" + Guid.NewGuid().ToString("N"));
        private int runs;

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

        private string Folder => Path.Combine(root, "handed over");

        private string Project(string mode = DesignBasis.Rev6)
        {
            Directory.CreateDirectory(Folder);
            string sample = Path.Combine(RepoRoot(), "samples", "new-project");
            foreach (string file in Directory.GetFiles(sample, "*.dxf").Concat(Directory.GetFiles(sample, "*.xlsx")))
            {
                File.Copy(file, Path.Combine(Folder, Path.GetFileName(file)), true);
            }
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "config", "project.json")));
            config["inputRoot"] = Folder;
            config["designBasis"]["mode"] = mode;
            config["siteBuild"]["minFreeGB"] = 0.1;
            string path = Path.Combine(root, "project.json");
            File.WriteAllText(path, config.ToString());
            return path;
        }

        private string Work()
        {
            string work = Path.Combine(root, "work", "model");
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
            public RunSummary Asked { get; private set; }

            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                Asked = summary;
                return true;
            }
        }

        // The worker command itself, or one a test gives that ends as a stopped check does
        private sealed class Worker : IWorkerStart
        {
            public readonly List<WorkerRequest> Requests = new List<WorkerRequest>();
            public WorkerCommand Command { get; set; } = CadReader.DataCheckCommand.Run;

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                WorkerRunner.Run(requestPath, new Dictionary<string, WorkerCommand> { ["dataCheck"] = Command }, () => 1e6, () => 0.0);
                ended(WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId)));
            }
        }

        private const string Document = @"C:\Models\site.rvt";

        private RunOutcome Press(string configPath, string work, Worker worker, Yes confirm = null, Func<SettingsScope> scope = null)
        {
            var roles = new Dictionary<string, string> { ["Settings"] = ReadInputs.Config };
            var run = new ReadDataRun(roles, worker, new WorkerDocument { Path = Document, SessionId = "s1" }, scope ?? (() => new SettingsScope())) { ModePick = () => DesignBasis.Rev6 };
            var flow = new ButtonFlow(new State(configPath, work), new NoModel(), confirm ?? new Yes(), InputHash.Of, () => RunIds.New(2, DateTime.Now) + "-" + (++runs).ToString("D3"));
            RunOutcome last = null;
            RunOutcome started = flow.Run(2, "2 Read data", run.Body(), Document, outcome => last = outcome);
            return started.End == RunEnd.Started ? last : started;
        }

        // 1. A read that is not whole

        [Fact]
        public void R639_a_file_left_not_read_is_read_again_once_the_cause_is_gone_and_a_whole_read_stays_reused()
        {
            string configPath = Project();
            string work = Work();
            var worker = new Worker();
            string held = Directory.GetFiles(Folder, "*.xlsx").First();

            RunOutcome first;
            using (new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                first = Press(configPath, work, worker);
            }
            Assert.True(first.End == RunEnd.Committed, first.Message);
            Assert.Single(worker.Requests);
            Assert.Contains("This read is not whole, the next press of 2 Read data reads again", first.Message);
            Assert.Contains(Path.GetFileName(held), first.Message);

            // The file is free, nothing of the folder changed, so the hash is the same and the read must still be done again
            var confirm = new Yes();
            RunOutcome second = Press(configPath, work, worker, confirm);
            Assert.True(second.End == RunEnd.Committed, second.Message);
            Assert.Equal(2, worker.Requests.Count);
            Assert.Equal(worker.Requests[0].OutputFolder, worker.Requests[1].OutputFolder);
            Assert.Contains("Reads again, the last read of these files is not whole", confirm.Asked.Counts);
            Assert.Contains(Path.GetFileName(held), confirm.Asked.Counts);
            Assert.DoesNotContain("not whole", second.Message);

            // Whole now, so as before this reuses
            var third = new Yes();
            RunOutcome reused = Press(configPath, work, worker, third);
            Assert.True(reused.End == RunEnd.Committed, reused.Message);
            Assert.Equal(2, worker.Requests.Count);
            Assert.Contains("is reused, nothing was read again", reused.Message);
            Assert.DoesNotContain("Reads again", third.Asked.Counts);
        }

        private static void StoppedCheck(WorkerContext run)
        {
            WriteCheck(run, "IOException: the invented cause");
        }

        private static void WholeCheck(WorkerContext run)
        {
            WriteCheck(run, null);
        }

        private static void WriteCheck(WorkerContext run, string stopped)
        {
            string folder = run.Request.OutputFolder;
            run.WriteJson(folder, ReadDataRun.DataCheckFile, new JObject { ["stopped"] = stopped, ["files"] = new JArray() });
            // A stopped check holds no plot, a whole one a plot the scope of the whole project takes
            var plots = stopped != null ? new JArray() : new JArray(new JObject { ["baseplanLabel"] = "7", ["officialNumber"] = "7", ["nbhId"] = "A" });
            run.WriteJson(folder, ReadDataRun.PlotTableFile, new JObject { ["plots"] = plots });
        }

        [Fact]
        public void R639_a_check_that_stopped_is_never_reused_and_the_line_says_why()
        {
            string configPath = Project();
            string work = Work();
            var worker = new Worker { Command = StoppedCheck };

            // The whole project scope stops on no plot, and the line names the stop of the check as the cause, no change of scope helps
            RunOutcome first = Press(configPath, work, worker);
            Assert.False(first.End == RunEnd.Committed, first.Message);
            Assert.Contains("This read is not whole, the next press of 2 Read data reads again: the check stopped, IOException: the invented cause.", first.Message);
            Assert.DoesNotContain("the read is reused", first.Message);

            worker.Command = WholeCheck;
            var confirm = new Yes();
            RunOutcome second = Press(configPath, work, worker, confirm);
            Assert.True(second.End == RunEnd.Committed, second.Message);
            Assert.Equal(2, worker.Requests.Count);
            Assert.Contains("Reads again, the last read of these files is not whole: the check stopped, IOException: the invented cause.", confirm.Asked.Counts);

            RunOutcome third = Press(configPath, work, worker);
            Assert.True(third.End == RunEnd.Committed, third.Message);
            Assert.Equal(2, worker.Requests.Count);
        }

        [Fact]
        public void R639_a_scope_stop_on_a_read_that_is_not_whole_does_not_promise_a_reuse()
        {
            string configPath = Project();
            string work = Work();
            var worker = new Worker { Command = StoppedCheck };
            RunOutcome stopped = Press(configPath, work, worker, null, () => new SettingsScope { Kind = "plots", Plots = "9999" });
            Assert.False(stopped.End == RunEnd.Committed, stopped.Message);
            Assert.Contains("the next press of 2 Read data reads again", stopped.Message);
            Assert.DoesNotContain("the read is reused", stopped.Message);
        }

        // 2. The count of a raw read

        [Fact]
        public void R639_the_dry_run_of_a_raw_read_counts_the_files_of_the_folder_and_never_says_it_reads_that_many()
        {
            string configPath = Project();
            string work = Work();
            File.WriteAllText(Path.Combine(Folder, "a note.txt"), "no input of the check");
            var worker = new Worker();
            var confirm = new Yes();
            Assert.Equal(RunEnd.Committed, Press(configPath, work, worker, confirm).End);

            int held = Directory.GetFiles(Folder, "*", SearchOption.AllDirectories).Length;
            Assert.True(held > 2, held.ToString());
            Assert.Contains("Reads the folder and the settings file. Files in the folder: " + held + ",", confirm.Asked.Counts);
            Assert.DoesNotMatch(new Regex(@"Reads \d+ files?"), confirm.Asked.Counts);

            var again = new Yes();
            Assert.Equal(RunEnd.Committed, Press(configPath, work, worker, again).End);
            Assert.Contains("the folder and the settings file are unchanged", again.Asked.Counts);
            Assert.DoesNotMatch(new Regex(@"\d+ input files"), again.Asked.Counts);
        }

        // 3. Read from

        [Fact]
        public void R639_read_from_of_a_raw_read_names_the_folder()
        {
            string configPath = Project();
            string work = Work();
            Assert.Equal(RunEnd.Committed, Press(configPath, work, new Worker()).End);
            string nbh = ReviewLoad.LatestNbhCodes(work).First();
            ReviewInput review = ReviewLoad.Load(work, nbh, null);
            Assert.Equal(new[] { "the folder " + Path.GetFullPath(Folder) }, review.Sources);
        }

        // 4. No type and why

        private static JObject Table(JObject row)
        {
            row["baseplanLabel"] = "7";
            row["officialNumber"] = "7";
            row["nbhId"] = "A";
            row["faceAreaM2"] = 100.0;
            return new JObject { ["7"] = row };
        }

        [Fact]
        public void R639_a_finished_read_says_no_type_and_why_and_not_read_yet_stays_for_what_was_not_read()
        {
            JObject finished = Table(new JObject { ["typologyRev9"] = null, ["footprintMirrored"] = null, ["missing"] = new JObject { ["villaType"] = "the invented reason", ["mirror"] = "another invented reason" } });
            ReviewRow row = Assert.Single(ReviewTable.Rows(finished, "A", null, null, null));
            Assert.Equal("the invented reason", row.VillaWhy);
            Assert.Equal("another invented reason", row.MirrorWhy);
            Assert.Equal("no type: the invented reason", ReviewTable.Shown(ReviewRowText.NoType, row.VillaWhy));
            Assert.Equal("no mirror: another invented reason", ReviewTable.Shown(ReviewRowText.NoMirror, row.MirrorWhy));

            // The key with no text is still a finished read, a plot with its value says nothing, and the old read holds no such entry
            JObject silent = Table(new JObject { ["typologyRev9"] = null, ["missing"] = new JObject { ["villaType"] = null } });
            Assert.Equal(ReviewRowText.NoReason, Assert.Single(ReviewTable.Rows(silent, "A", null, null, null)).VillaWhy);
            JObject found = Table(new JObject { ["typologyRev9"] = "NEW1", ["footprintMirrored"] = true, ["missing"] = new JObject() });
            ReviewRow value = Assert.Single(ReviewTable.Rows(found, "A", null, null, null));
            Assert.Null(value.VillaWhy);
            Assert.Null(value.MirrorWhy);
            JObject old = Table(new JObject { ["typologyRev9"] = null });
            ReviewRow past = Assert.Single(ReviewTable.Rows(old, "A", null, null, null));
            Assert.Null(past.VillaWhy);
            Assert.Null(past.MirrorWhy);
            Assert.Null(ReviewTable.Shown(ReviewRowText.NoType, past.VillaWhy));
        }

#if NET48
        [Fact]
        public void R639_the_row_of_the_window_shows_the_reason_and_a_row_of_the_old_read_stays_not_read_yet()
        {
            var state = new ReviewState(new ReviewRow[0], new ReviewCheck[0], "r", "h");
            var said = new ReviewRowViewModel(new ReviewRow { PlotNo = "7", VillaWhy = "the invented reason", MirrorWhy = "another invented reason", RuleId = "M01" }, state);
            Assert.Equal("no type: the invented reason", said.VillaType);
            Assert.Equal("no mirror: another invented reason", said.Mirror);
            Assert.Equal("the invented reason", said.VillaTip);
            Assert.Equal("another invented reason", said.MirrorTip);
            Assert.Equal(ReviewDataText.NotReadYet, said.Cluster);

            var old = new ReviewRowViewModel(new ReviewRow { PlotNo = "8", RuleId = "M01" }, state);
            Assert.Equal(ReviewDataText.NotReadYet, old.VillaType);
            Assert.Equal(ReviewDataText.NotReadYet, old.Mirror);
            Assert.Null(old.VillaTip);
            Assert.Null(old.MirrorTip);

            var value = new ReviewRowViewModel(new ReviewRow { PlotNo = "9", VillaType = "NEW1", Mirror = true, RuleId = "M01" }, state);
            Assert.Equal("NEW1", value.VillaType);
            Assert.Equal(ReviewDataText.Yes, value.Mirror);
            Assert.Null(value.VillaTip);
            Assert.Null(value.MirrorTip);
        }
#endif

        // 5. R635 stays and says so

        [Fact]
        public void R847_the_read_takes_the_folder_and_no_line_of_it_speaks_of_an_old_read()
        {
            string configPath = Project();
            string work = Work();
            ReadPlan plan = ReadInputs.Raw(new[] { new RunInput("Settings", configPath, false) }, new Dictionary<string, string> { ["Settings"] = ReadInputs.Config }, work, DesignBasis.Rev6);
            Assert.Empty(plan.Stops);
            Assert.Contains(plan.Inputs, input => input.Role == ReadInputs.ScanFolder);

            var worker = new Worker();
            var confirm = new Yes();
            RunOutcome done = Press(configPath, work, worker, confirm);
            Assert.True(done.End == RunEnd.Committed, done.Message);
            Assert.StartsWith("Reads the folder and the settings file.", confirm.Asked.Counts);
            Assert.DoesNotContain("old read", confirm.Asked.Counts + done.Message + string.Join(" ", confirm.Asked.PerPlot));

            // A reused read hands on the same plot table and starts no worker
            var again = new Yes();
            RunOutcome reused = Press(configPath, work, worker, again);
            Assert.Single(worker.Requests);
            Assert.DoesNotContain("old read", again.Asked.Counts + reused.Message);
        }

        [Fact]
        public void R639_the_count_of_files_a_folder_holds_is_no_part_of_its_hash()
        {
            Assert.Equal(new ReadInput("config", "c", 1, "a", 5).Sha256, new ReadInput("config", "c", 1, "a").Sha256);
        }

        // 6. A locked settings file

        [Fact]
        public void R639_a_locked_settings_file_is_not_read_and_said_and_never_raises()
        {
            string configPath = Project();
            string work = Work();
            var roles = new Dictionary<string, string> { ["Settings"] = ReadInputs.Config };
            using (new FileStream(configPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.StartsWith("the settings file does not read: ", Assert.Single(ReadInputs.Raw(new[] { new RunInput("Settings", configPath, false) }, roles, work, DesignBasis.Rev6).Stops));
                RunOutcome press = Press(configPath, work, new Worker());
                Assert.Equal(RunEnd.Stopped, press.End);
                Assert.False(string.IsNullOrEmpty(press.Message));
            }
            Assert.Empty(ReadInputs.Raw(new[] { new RunInput("Settings", configPath, false) }, roles, work, DesignBasis.Rev6).Stops);
        }

#if NET48
        [Fact]
        public void R639_the_pane_refresh_on_a_locked_settings_file_names_no_folder_and_raises_nothing()
        {
            string configPath = Project();
            string work = Work();
            var settings = new PaneSettings { ConfigFile = configPath };
            settings.Project.ProjectMode = DesignBasis.Rev6;
            using (new FileStream(configPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                IReadOnlyList<RunInput> inputs = PaneRunState.InputsInFolder(settings, 2, work);
                Assert.Equal(new[] { PaneText.Settings, PaneText.InputFolder }, inputs.Select(input => input.Name));
                Assert.Null(inputs[1].Path);
            }
        }
#endif

        // 7. A column passed over for its name

        private static DataCheckSettings Settings()
        {
            return DataCheckSettings.From(new JObject
            {
                [DataCheckSettings.Block] = new JObject
                {
                    ["scan"] = new JObject
                    {
                        ["labelPattern"] = @"^(?:(.*?)\s+)?(\S*\d\S*)$", ["tokenSplit"] = "[^A-Za-z0-9]+", ["plotNumberShifts"] = new JArray(0), ["pairMinSegments"] = 100,
                        ["pairThreads"] = 1, ["pairTimeCapS"] = 60, ["pdfJoinGapPt"] = 0.3, ["pdfLineTolerancePt"] = 0.05, ["pdfPairDyPt"] = new JArray(1.3, 1.8), ["pdfPairDxPt"] = 1.5,
                    },
                    ["groupGapsM"] = new JArray(100.0, 1.0), ["wordReachM"] = 30, ["wordShare"] = 0.9, ["wordMinGroups"] = 3, ["standInMaxArea"] = 4, ["roadReachM"] = 20, ["roadMinSegments"] = 2, ["roadOutsideShare"] = 0.5,
                    ["drawingPeak"] = new JObject { ["dwg"] = 252, ["dxf"] = 106 },
                    ["strongShare"] = 0.9, ["usableShare"] = 0.5, ["prototypeFilePattern"] = "(?:^|-)ARC-([A-Za-z0-9]+)(?:-[A-Za-z0-9]{2})?-MOD",
                    ["codePattern"] = @"^[A-Za-z]+\d+[A-Za-z0-9.]*(?:-[A-Za-z0-9]+)*(?:\s*\([^()]*\))?$", ["codeTableMaxPairs"] = 100, ["handGuessShare"] = 0.6, ["areaNearPlots"] = 8, ["areaAgreement"] = 0.7, ["skipFolders"] = new JArray(), ["groupNamePattern"] = @"^(?:[A-Z0-9][A-Za-z0-9-]{0,3}|\S*\d\S*)$",
                },
            });
        }

        // Twenty plots whose second half lies far away, so a column that follows the ground parts them, and whose drawing speaks OLD1 and OLD2 where
        // the prototypes are NEW1 and NEW2, so a column of the new codes says what the drawing does not
        private static DataCheckInput Plots(params (string Header, Func<int, string> Cell)[] columns)
        {
            var faces = new JArray();
            var texts = new JArray();
            for (int at = 0; at < 20; at++)
            {
                double x = (at % 5) * 12.0 + (at < 10 ? 0 : 5000), y = (at / 5) * 35.0;
                faces.Add(new JObject { ["ring"] = new JArray(new JArray(x, y), new JArray(x + 12, y), new JArray(x + 12, y + 20), new JArray(x, y + 20)), ["centroid"] = new JArray(x + 6, y + 10), ["area"] = 240.0 });
                texts.Add(new JObject { ["layer"] = "LOTS-TEXT", ["text"] = (at % 2 == 0 ? "OLD1" : "OLD2") + "\n" + (101 + at), ["x"] = x + 3, ["y"] = y + 2 });
            }
            var input = new DataCheckInput();
            input.CadReads.Add(new JObject
            {
                ["source"] = "site.dwg", ["path"] = "site.dwg", ["size"] = 1, ["sha256"] = "x", ["units"] = "Meters", ["notRead"] = new JArray(), ["notTraced"] = new JArray(),
                ["layers"] = new JArray(new JObject { ["layer"] = "LOTS", ["closedOutlines"] = 20, ["texts"] = 0, ["blocks"] = 0, ["segments"] = 80 }),
                ["candidates"] = new JArray(new JObject { ["layers"] = new JArray("LOTS"), ["faces"] = faces }),
                ["texts"] = texts, ["inserts"] = new JArray(), ["lines"] = new JObject(), ["arcs"] = new JObject(),
            });
            input.PrototypeNames.AddRange(new[] { "X-ARC-NEW1-ZZ-MOD-1.rvt", "X-ARC-NEW2-ZZ-MOD-1.rvt" });
            var rows = new List<IList<string>> { new[] { "Plot" }.Concat(columns.Select(column => column.Header)).ToList() };
            for (int at = 0; at < 20; at++)
            {
                int place = at;
                rows.Add(new[] { (101 + at).ToString() }.Concat(columns.Select(column => column.Cell(place))).ToList());
            }
            input.Excels.Add(new ScanWorkbook { Name = "sheet.xlsx", Sheets = new List<ScanSheet> { ScanSheet.Of("Tab", rows) } });
            return input;
        }

        private static string AreaOf(int at)
        {
            return at < 10 ? "North" : "South";
        }

        private static string CodeOf(int at)
        {
            return at % 2 == 0 ? "NEW1" : "NEW2";
        }

        private static string LineOf(DataCheckOutput output, string need)
        {
            return (string)((JArray)output.Check["values"]).OfType<JObject>().Single(row => (string)row["need"] == need)["line"];
        }

        [Fact]
        public void R639_an_area_column_passed_over_for_its_name_is_said_on_the_area_line_and_is_still_not_taken()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Plots(("Xq7", AreaOf), ("Xq7", AreaOf)));
            Assert.False(output.Profile["areaColumn"]["proposed"] is JObject);
            Assert.Empty((JArray)output.Profile["areaColumn"]["candidates"]);
            Assert.StartsWith("Two columns of sheet Tab carry the name Xq7, so neither was taken. ", LineOf(output, "areaColumn"));
            Assert.DoesNotContain("carry the name", LineOf(output, "typologyColumn"));
        }

        [Fact]
        public void R639_a_typology_column_passed_over_for_its_name_is_said_on_the_typology_line_and_is_still_not_taken()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Plots(("Rev", CodeOf), ("Rev", CodeOf)));
            Assert.False(output.Profile["typologyColumn"]["proposed"] is JObject);
            Assert.Empty((JArray)output.Profile["typologyColumn"]["candidates"]);
            Assert.StartsWith("Two columns of sheet Tab carry the name Rev, so neither was taken. ", LineOf(output, "typologyColumn"));
            Assert.DoesNotContain("carry the name", LineOf(output, "areaColumn"));
        }

        [Fact]
        public void R639_a_column_with_a_name_of_its_own_beside_the_pair_is_still_proposed_and_the_line_says_both()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Plots(("Xq7", AreaOf), ("Xq7", AreaOf), ("Zone9", AreaOf)));
            Assert.Equal("Zone9", (string)output.Profile["areaColumn"]["proposed"]["column"]);
            Assert.Contains("carry the name Xq7", LineOf(output, "areaColumn"));
            Assert.Contains("A column parts the plots into", LineOf(output, "areaColumn"));
        }

        [Fact]
        public void R639_two_columns_of_one_name_that_could_fill_no_need_say_nothing()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Plots(("Note", at => "text"), ("Note", at => "text")));
            Assert.DoesNotContain("carry the name", LineOf(output, "areaColumn"));
            Assert.DoesNotContain("carry the name", LineOf(output, "typologyColumn"));
        }

        [Fact]
        public void R639_more_names_are_counted_and_a_long_name_is_not_lost_to_the_cut_of_the_line()
        {
            DataCheckOutput two = DataCheckLogic.Run(Settings(), Plots(("Xq7", AreaOf), ("Xq7", AreaOf), ("Yq8", AreaOf), ("Yq8", AreaOf), ("Yq8", AreaOf)));
            Assert.Contains("carry the name Xq7 and 1 more name, so neither was taken.", LineOf(two, "areaColumn"));

            string longName = "An invented column name of some length in " + new string('x', 50);
            string line = LineOf(DataCheckLogic.Run(Settings(), Plots((longName, AreaOf), (longName, AreaOf))), "areaColumn");
            Assert.Contains(longName, line);
            Assert.True(line.Length <= 160, line);
        }

        // The latent line is gone with the fault
        [Fact]
        public void R639_the_latent_line_of_the_columns_passed_over_is_taken_out()
        {
            string latent = File.ReadAllText(Path.Combine(RepoRoot(), "steps", "latent.md"));
            Assert.DoesNotContain("pass over a column whose name two columns carry", latent);
        }
    }
}
