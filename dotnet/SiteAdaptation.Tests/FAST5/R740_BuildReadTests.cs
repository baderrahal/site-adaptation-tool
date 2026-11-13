using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;
#if NETFRAMEWORK
using SiteAdaptation.AddIn.Fast5;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Review;
#endif

namespace SiteAdaptation.Tests
{
    // R740 and R741, F16 of run 1 of FAST-3. The Build fills the input rows from the project folder and reads the drawings, road surfaces and workbooks for
    // 4 Check itself before Check. Every file name is made up, every file is a stand in
    public sealed class R740_BuildReadTests : IDisposable
    {
        private const string Road = "Made Up Road A";
        private readonly string root = Path.Combine(Path.GetTempPath(), "r740-" + Guid.NewGuid().ToString("N"));

        public R740_BuildReadTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private string Folder()
        {
            string folder = Path.Combine(root, "project");
            foreach (string file in new[] { "Made Plan.dwg", "Made Houses.dwg", "Made Roads.dwg", "Made Plots.pdf", Path.Combine("books", "Made Book D.xlsx"), "Made Book S.xlsx",
                Path.Combine("surfaces", Road + ".xml"), Path.Combine("protos", "Made-A.rvt"), Path.Combine("protos", "Made-B.rvt"),
                Path.Combine("old", "Made-C.rvt"), Path.Combine("old", "Made-D.rvt"), Path.Combine("old", "Made-E.rvt") })
            {
                string path = Path.Combine(folder, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "stand in");
            }
            return folder;
        }

        private JObject Config(string folder)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["inputRoot"] = folder;
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["designBasis"]["roadLevel"]["surfaces"] = new JArray(Road);
            config["designBasis"]["roadLevel"]["notRoad"] = new JObject();
            config["drawings"] = new JObject { ["baseplan"] = "Made Plan.dwg", ["villaBlocks"] = "Made Houses.dwg", ["roadLayout"] = "Made Roads.dwg", ["referenceOnly"] = new JArray() };
            config["dataBook"]["rev9"]["workbooks"] = new JObject { ["d"] = "any/Made Book D.xlsx", ["s"] = "Made Book S.xlsx" };
            config["addin"]["workbookRoles"] = new JObject { ["d"] = "dlus", ["s"] = "sales" };
            config["dataCheck"]["skipFolders"] = new JArray("old");
            config["addin"]["readData"] = new JObject { ["cadLayers"] = "layers.json" };
            return config;
        }

        private string ConfigFile(JObject config)
        {
            string folder = Path.Combine(root, "config");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "layers.json"), "{}");
            string file = Path.Combine(folder, "project.json");
            File.WriteAllText(file, config.ToString());
            return file;
        }

        [Fact]
        public void R740_each_row_is_found_in_the_folder_by_the_name_config_gives_and_a_skipped_folder_is_not_walked()
        {
            string folder = Folder();
            FolderRoles.Found found = FolderRoles.Of(Config(folder), folder, null);
            Assert.Empty(found.Left);
            Assert.Equal(Path.Combine(folder, "Made Plan.dwg"), found.Paths[InputNames.Baseplan]);
            Assert.Equal(Path.Combine(folder, "Made Houses.dwg"), found.Paths[InputNames.VillaBlocks]);
            Assert.Equal(Path.Combine(folder, "Made Roads.dwg"), found.Paths[InputNames.Curbs]);
            Assert.Equal(Path.Combine(folder, "books", "Made Book D.xlsx"), found.Paths[InputNames.Dlus]);
            Assert.Equal(Path.Combine(folder, "Made Book S.xlsx"), found.Paths[InputNames.Sales]);
            Assert.Equal(Path.Combine(folder, "surfaces"), found.Paths[InputNames.RoadSurface]);
            Assert.Equal(Path.Combine(folder, "protos"), found.Paths[InputNames.Prototypes]);
        }

        [Fact]
        public void R740_a_name_two_files_carry_or_a_name_not_there_is_left_out_with_its_reason()
        {
            string folder = Folder();
            File.WriteAllText(Path.Combine(folder, "books", "Made Plan.dwg"), "a second file of the same name");
            JObject config = Config(folder);
            config["drawings"]["villaBlocks"] = "Not There.dwg";
            // Two folders of as many prototypes each, the tool cannot tell which one is meant
            Directory.CreateDirectory(Path.Combine(folder, "protos2"));
            File.WriteAllText(Path.Combine(folder, "protos2", "Made-D.rvt"), "stand in");
            File.WriteAllText(Path.Combine(folder, "protos2", "Made-E.rvt"), "stand in");
            FolderRoles.Found found = FolderRoles.Of(config, folder, null);
            Assert.False(found.Paths.ContainsKey(InputNames.Baseplan));
            Assert.Contains("baseplan: 2 files are named Made Plan.dwg", found.Left);
            Assert.False(found.Paths.ContainsKey(InputNames.VillaBlocks));
            Assert.Contains("villaBlocks: Not There.dwg is not in the folder", found.Left);
            Assert.False(found.Paths.ContainsKey(InputNames.Prototypes));
            Assert.Contains(found.Left, line => line.StartsWith("prototypes: two folders hold 2 each", StringComparison.Ordinal));
            Assert.Equal(Path.Combine(folder, "Made Roads.dwg"), found.Paths[InputNames.Curbs]);
        }

        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string>
        {
            ["Settings"] = "config", ["Baseplan"] = "baseplan", ["Villa blocks"] = "villaBlocks", ["Curbs"] = "curbs", ["Road surface"] = ReadInputs.RoadSurface,
            ["DLUS"] = "dlus", ["Sales"] = "sales", ["Plot numbers"] = "plotNumbers",
        };

        private List<RunInput> Pane(string configFile, string folder, string plotNumbers)
        {
            return new List<RunInput>
            {
                new RunInput("Settings", configFile, false), new RunInput("Baseplan", Path.Combine(folder, "Made Plan.dwg"), false),
                new RunInput("Villa blocks", Path.Combine(folder, "Made Houses.dwg"), false), new RunInput("Curbs", Path.Combine(folder, "Made Roads.dwg"), false),
                new RunInput("Road surface", Path.Combine(folder, "surfaces"), true), new RunInput("DLUS", Path.Combine(folder, "books", "Made Book D.xlsx"), false),
                new RunInput("Sales", Path.Combine(folder, "Made Book S.xlsx"), false), new RunInput("Plot numbers", plotNumbers, false),
            };
        }

        [Fact]
        public void R740_the_read_for_the_build_needs_no_schedule_and_no_plot_number_file_and_leaves_the_location_plan_out()
        {
            string folder = Folder();
            string configFile = ConfigFile(Config(folder));
            string work = Path.Combine(root, "work");
            // R840. The plots and labels of the road levels come from the read of card 1, here a made up one
            MadeRead(work, "2_20261107-010000-000", MadeTable);
            ReadPlan plan = ReadInputs.ForBuild(Pane(configFile, folder, null), Roles, work);
            Assert.Empty(plan.Stops);
            Assert.Equal(new[] { "baseplan", "cadLayers", "config", "curbs", "dlus", "labels", "plotsCsv", WorkerRunner.RoadSurfaceRole + Road, "sales", "villaBlocks" },
                plan.Inputs.Select(input => input.Role).OrderBy(role => role, StringComparer.Ordinal).ToArray());
            Assert.Empty(plan.Steps.Intersect(ReadInputs.PlotTableSteps));
            Assert.Contains(ReadSteps.RoadLevels, plan.Steps);
            Assert.Contains(ReadSteps.Sales, plan.Steps);
            // A drawing row gone still stops the read for the build, with its row named
            List<RunInput> broken = Pane(configFile, folder, null);
            broken[1] = new RunInput("Baseplan", Path.Combine(folder, "gone.dwg"), false);
            Assert.Contains(ReadInputs.ForBuild(broken, Roles, work).Stops, stop => stop.StartsWith("Baseplan is not on disk", StringComparison.Ordinal));
        }

        // A plot table of stage 1 with one made up plot, and another one
        private const string MadeTable = "{\"plots\":[{\"baseplanLabel\":\"MU-1\",\"officialNumber\":\"9001\",\"nbhId\":\"Z9\"}]}";
        private const string OtherTable = "{\"plots\":[{\"baseplanLabel\":\"MU-2\",\"officialNumber\":\"9002\",\"nbhId\":\"Z9\"}]}";

        // A read of stage 1 by the data check: its record and plot table only, as run 1 found it
        private static string MadeRead(string work, string runId, string table)
        {
            string folder = Path.Combine(work, "read_" + runId.Replace("_", "").Replace("-", ""));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, ReadDataRun.PlotTableFile), table);
            File.WriteAllText(Path.Combine(folder, ReadDataRun.RecordFile), new JObject { ["format"] = ReadDataRun.RecordFormat, ["runId"] = runId, ["inputs"] = new JArray(), ["outputs"] = new JArray() }.ToString());
            return folder;
        }

#if NETFRAMEWORK
        // The worker of a test: writes the four reads Check takes and answers done
        private sealed class MadeWorker : IWorkerStart
        {
            public List<WorkerRequest> Requests { get; } = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                Directory.CreateDirectory(request.OutputFolder);
                var outputs = new List<WorkerFile>();
                foreach (string file in CheckRun.ReadFiles.Values)
                {
                    string path = Path.Combine(request.OutputFolder, file);
                    File.WriteAllText(path, "{}");
                    outputs.Add(new WorkerFile { Role = file, Path = path, Sha256 = FileHash.Sha256(path) });
                }
                ended(new WorkerAnswer { RunId = request.RunId, Status = WorkerExit.Status(WorkerExit.Done), Outputs = outputs, Counts = new Dictionary<string, int>(), Issues = new List<string>() });
            }
        }

        private const string Label = "2 Made up read";

        private PaneSettings Settings(string folder)
        {
            var settings = new PaneSettings { ConfigFile = ConfigFile(Config(folder)) };
            settings.Pick(InputKeys.Prototypes, Path.Combine(folder, "my own prototypes"));
            Directory.CreateDirectory(Path.Combine(folder, "my own prototypes"));
            return settings;
        }

        [Fact]
        public void R740_the_empty_rows_take_the_folder_and_a_row_a_person_set_stays()
        {
            string folder = Folder();
            PaneSettings settings = Settings(folder);
            IReadOnlyList<string> filled = BuildRead.FillRows(settings, settings.Pick);
            Assert.Equal(new[] { InputKeys.Baseplan, InputKeys.Curbs, InputKeys.Dlus, InputKeys.RoadSurface, InputKeys.Sales, InputKeys.VillaBlocks }.OrderBy(key => key, StringComparer.Ordinal), filled);
            Assert.Equal(Path.Combine(folder, "Made Plan.dwg"), settings.InputPath(InputKeys.Baseplan));
            Assert.Equal(Path.Combine(folder, "my own prototypes"), settings.InputPath(InputKeys.Prototypes));
            // A row whose file is gone takes the folder again
            Directory.Delete(Path.Combine(folder, "my own prototypes"));
            Assert.Equal(new[] { InputKeys.Prototypes }, BuildRead.FillRows(settings, settings.Pick));
            Assert.Equal(Path.Combine(folder, "protos"), settings.InputPath(InputKeys.Prototypes));
        }

        [Fact]
        public void R741_the_read_for_the_build_is_the_latest_read_with_what_check_takes_and_the_plot_table_of_stage_1()
        {
            string folder = Folder();
            PaneSettings settings = Settings(folder);
            BuildRead.FillRows(settings, settings.Pick);
            string work = Path.Combine(root, "work");
            string stageOne = MadeRead(work, "2_20261107-010000-000", MadeTable);
            Assert.True(BuildRead.Needed(work));
            var worker = new MadeWorker();
            bool? ok = null;
            string line = null;
            BuildRead.Start(settings, work, new WorkerDocument { Path = Path.Combine(work, "a.rvt"), SessionId = "s1" }, worker, Label, (done, said) => { ok = done; line = said; });
            Assert.True(ok, line);
            WorkerRequest request = worker.Requests.Single();
            Assert.Equal(ReadDataRun.Command, request.Command);
            Assert.DoesNotContain(request.Inputs, input => input.Role.StartsWith("schedule", StringComparison.Ordinal) || input.Role == "plotNumbers" || input.Role == BuildRead.PlotTableRole);
            Assert.Empty(request.Steps.Intersect(ReadInputs.PlotTableSteps));
            string read = ReviewLoad.LatestReadFolder(work);
            Assert.Equal(request.OutputFolder, read);
            Assert.NotEqual(stageOne, read);
            Assert.False(BuildRead.Needed(work));
            Assert.Equal(File.ReadAllText(Path.Combine(stageOne, ReadDataRun.PlotTableFile)), File.ReadAllText(Path.Combine(read, ReadDataRun.PlotTableFile)));
            // The record names what Check holds against the files now: the config of the run, the road surface by its role, the plots and labels of the plot table
            JObject record = JObject.Parse(File.ReadAllText(Path.Combine(read, ReadDataRun.RecordFile)));
            List<string> roles = record["inputs"].Select(input => (string)input["role"]).ToList();
            Assert.Contains(WorkerRunner.RoadSurfaceRole + Road, roles);
            Assert.Contains("plotsCsv", roles);
            Assert.Contains("labels", roles);
            Assert.Equal(Path.GetFullPath(settings.ConfigFile), (string)record["inputs"].Single(input => (string)input["role"] == "config")["path"]);
            Assert.EndsWith(BuildRead.RunSuffix, (string)record["runId"]);

            // A read of stage 1 again with the same plot table makes it latest, the read for the build is then reused with no worker and latest again
            // A read of stage 1 after it is latest again and lacks what Check takes, so the next Build reads for the build again
            MadeRead(work, "2_29991231-000000-000", MadeTable);
            Assert.True(BuildRead.Needed(work));
        }

        [Fact]
        public void R741_a_new_plot_table_of_stage_1_is_a_new_read_for_the_build_and_the_same_one_is_reused_with_no_worker()
        {
            string folder = Folder();
            PaneSettings settings = Settings(folder);
            BuildRead.FillRows(settings, settings.Pick);
            string work = Path.Combine(root, "work");
            MadeRead(work, "2_20261107-010000-000", MadeTable);
            var worker = new MadeWorker();
            var lines = new List<string>();
            var document = new WorkerDocument { Path = Path.Combine(work, "a.rvt"), SessionId = "s1" };
            BuildRead.Start(settings, work, document, worker, Label, (done, said) => lines.Add(done + " " + said));
            string first = ReviewLoad.LatestReadFolder(work);
            System.Threading.Thread.Sleep(5);
            BuildRead.Start(settings, work, document, worker, Label, (done, said) => lines.Add(done + " " + said));
            Assert.Single(worker.Requests);
            Assert.StartsWith("True " + RunTexts.ReadReused(Label, "x").Split('x')[0], lines[1]);
            Assert.StartsWith("True " + RunTexts.ReadDone(Label, 0, 0).Split('0')[0], lines[0]);
            Assert.Equal(first, ReviewLoad.LatestReadFolder(work));
            // Another plot table, so another read
            MadeRead(work, "2_29991231-000000-000", OtherTable);
            Assert.True(BuildRead.Needed(work));
            BuildRead.Start(settings, work, document, worker, Label, (done, said) => lines.Add(done + " " + said));
            Assert.Equal(2, worker.Requests.Count);
            Assert.NotEqual(first, ReviewLoad.LatestReadFolder(work));
        }

        [Fact]
        public void R741_a_worker_that_fails_or_a_missing_row_ends_the_read_for_the_build_with_why_and_writes_no_read()
        {
            string folder = Folder();
            PaneSettings settings = Settings(folder);
            string work = Path.Combine(root, "work");
            MadeRead(work, "2_20261107-010000-000", MadeTable);
            var document = new WorkerDocument { Path = Path.Combine(work, "a.rvt"), SessionId = "s1" };
            // The baseplan row names a file that is gone
            BuildRead.FillRows(settings, settings.Pick);
            settings.Pick(InputKeys.Baseplan, Path.Combine(folder, "gone.dwg"));
            string line = null;
            bool? ok = null;
            BuildRead.Start(settings, work, document, new MadeWorker(), Label, (done, said) => { ok = done; line = said; });
            Assert.False(ok);
            Assert.Contains("Baseplan is not on disk", line);
            // A name config gives that the folder does not hold: the row stays empty and the stop says why the folder gave none
            JObject config = JObject.Parse(File.ReadAllText(settings.ConfigFile));
            config["drawings"]["villaBlocks"] = "Not There.dwg";
            File.WriteAllText(settings.ConfigFile, config.ToString());
            settings.Pick(InputKeys.VillaBlocks, null);
            BuildRead.FillRows(settings, settings.Pick);
            BuildRead.Start(settings, work, document, new MadeWorker(), Label, (done, said) => { ok = done; line = said; });
            Assert.False(ok);
            Assert.Contains("villaBlocks: Not There.dwg is not in the folder", line);
            BuildRead.Start(settings, work, document, null, Label, (done, said) => { ok = done; line = said; });
            Assert.Equal(RunTexts.NotBuilt(Label), line);
            var plan = new ReadPlan(new ReadInput[0], new string[0], new string[0], settings.ConfigFile);
            Assert.False(BuildRead.Answered(Path.Combine(work, "read_x"), "2_x-build", plan, Path.Combine(work, "none.json"), Label, new WorkerAnswer { Status = "failed", Message = "made up stop" }, out line));
            Assert.Contains("made up stop", line);
            Assert.False(File.Exists(Path.Combine(work, "read_x", ReadDataRun.RecordFile)));
        }

        [Fact]
        public void R741_the_chain_runs_the_read_for_the_build_right_before_check_after_the_steps_check_waits_on_and_once()
        {
            var state = new Dictionary<int, StepStatus> { [3] = new StepStatus(StepKind.Ready), [4] = new StepStatus(StepKind.NeedsStep, 3) };
            int asked = 0;
            var chain = new BuildChain(number => "Step" + number, number => state.TryGetValue(number, out StepStatus held) ? held : new StepStatus(StepKind.Done))
            {
                ReadFirst = () => { asked++; return true; },
            };
            Assert.Equal(3, chain.Start());
            Assert.Equal(0, asked);
            state.Remove(3);
            state.Remove(4);
            Assert.True(chain.Ended(3, RunEnd.Committed, null, out int? next, out _));
            Assert.Equal(BuildChain.ReadForCheck, next);
            Assert.Equal(1, asked);
            Assert.True(chain.Ended(BuildChain.ReadForCheck, RunEnd.Committed, null, out next, out _));
            Assert.Equal(4, next);
            var ran = new List<int>();
            while (next.HasValue)
            {
                ran.Add(next.Value);
                Assert.True(chain.Ended(next.Value, RunEnd.Committed, null, out next, out _));
            }
            Assert.Equal(new[] { 4, 5, 6, 7, 8 }, ran.ToArray());
            Assert.Equal(1, asked);

            // A read that stops ends the build at it with why, and no Check runs
            var stopping = new BuildChain(number => number == BuildChain.ReadForCheck ? Label : "Step" + number, number => new StepStatus(StepKind.Done)) { ReadFirst = () => true };
            Assert.Equal(BuildChain.ReadForCheck, stopping.Start());
            Assert.True(stopping.Ended(BuildChain.ReadForCheck, RunEnd.Stopped, "made up stop", out next, out string line));
            Assert.Null(next);
            Assert.Equal("Build stopped at " + Label + ": made up stop", line);
            // Not needed, the chain is as before
            var plain = new BuildChain(number => "Step" + number, number => new StepStatus(StepKind.Done)) { ReadFirst = () => false };
            Assert.Equal(4, plain.Start());
        }
#endif
    }
}
