using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R372, R382. The pane side fills IRunState of the flow of a button: the work folder, the dry run tick, the missing step, the
    // inputs and the step line of a run that committed. On net48, with the view model of the pane and no Revit
    public sealed class PaneRunStateTests
    {
        private const string Folder = @"C:\Job\";

        [Fact]
        public void R372_the_run_state_gives_the_work_folder_of_the_document_by_the_root_and_refuses_one_inside_input_root()
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { Folder + "project.json", "{\"inputRoot\": \"D:\\\\Sources\"}" } };
            Func<string, string> read = path => files.TryGetValue(path, out string text) ? text : null;
            string root = @"D:\Work";
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" });
            var state = new PaneRunState(model, () => root, read);

            // The hash is the one PaneTests holds against hashlib of Python
            Assert.Equal(@"D:\Work\Model A_45e7c493", state.WorkFolder(@"C:\Work\Site\Model A.rvt"));
            root = @"D:\Sources\Work";
            Assert.Contains("lies inside inputRoot", Assert.Throws<InvalidOperationException>(() => state.WorkFolder(@"C:\Work\Site\Model A.rvt")).Message);
            files.Remove(Folder + "project.json");
            Assert.Equal(@"D:\Sources\Work\Model A_45e7c493", state.WorkFolder(@"C:\Work\Site\Model A.rvt"));
        }

        [Fact]
        public void R372_the_run_state_gives_the_dry_run_tick_of_the_pane()
        {
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" });
            var state = new PaneRunState(model, () => @"D:\Work", path => null);
            Assert.True(state.DryRunFirst);
            model.DryRun = false;
            Assert.False(state.DryRunFirst);
            model.Load(null);
            Assert.False(state.DryRunFirst);
        }

        [Fact]
        public void R372_the_run_state_gives_the_step_a_button_needs_and_0_when_none()
        {
            PaneViewModel model = Loaded(new PaneSettings());
            var state = new PaneRunState(model, () => @"D:\Work", path => null);
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 4 }, Enumerable.Range(1, 9).Select(state.MissingStep));
            model.PickConfig(Folder + "project.json");
            Assert.Equal(0, state.MissingStep(1));
            Assert.Equal(1, state.MissingStep(2));
        }

        [Fact]
        public void R372_the_run_state_gives_each_button_the_settings_file_in_5a()
        {
            PaneViewModel model = Loaded(new PaneSettings());
            var state = new PaneRunState(model, () => @"D:\Work", path => null);
            RunInput none = state.Inputs(1).Single();
            Assert.Equal("Settings", none.Name);
            Assert.Null(none.Path);
            Assert.False(none.IsFolder);
            model.PickConfig(Folder + "project.json");
            // Button 2 reads the input files of the pane beside it since 5c (R375)
            Assert.All(Enumerable.Range(1, 9).Where(button => button != 2 && button != 4), button => Assert.Equal(Folder + "project.json", state.Inputs(button).Single().Path));
            Assert.Equal(Folder + "project.json", state.Inputs(2).First().Path);
            // Button 4 reads the Rules file and the Prototypes folder of the pane beside it since 5c (R376)
            Assert.Equal(new[] { "Settings", "Rules", "Prototypes" }, state.Inputs(4).Select(input => input.Name));
            Assert.Equal(Folder + "project.json", state.Inputs(4).First().Path);
        }

        // R375. Button 2 gets the Settings file and the seven input files of the pane it reads, each by its row, Road surface as a
        // folder, the two workbooks of the row DLUS and sales by their own names, and each name has its role in the request
        [Fact]
        public void R375_the_run_state_gives_button_2_the_input_files_of_the_pane_each_with_its_role()
        {
            var settings = new PaneSettings { ConfigFile = Folder + "project.json" };
            settings.Pick(InputKeys.Baseplan, Folder + "baseplan.dwg");
            settings.Pick(InputKeys.RoadSurface, Folder + "roads");
            settings.Pick(InputKeys.Sales, Folder + "sales.xlsx");
            settings.Pick(InputKeys.Prototypes, Folder + "prototypes");
            var state = new PaneRunState(Loaded(settings), () => @"D:\Work", path => null);
            List<RunInput> inputs = state.Inputs(2).ToList();
            Assert.Equal(new[] { "Settings", "Baseplan", "Villa blocks", "Curbs", "Road surface", "DLUS schedule", "Sales schedule", "Plot numbers" }, inputs.Select(input => input.Name));
            Assert.Equal(Folder + "baseplan.dwg", inputs[1].Path);
            Assert.Null(inputs[2].Path);
            Assert.True(inputs[4].IsFolder);
            Assert.Equal(Folder + "roads", inputs[4].Path);
            Assert.Equal(Folder + "sales.xlsx", inputs[6].Path);
            Assert.All(inputs, input => Assert.True(SiteAdaptation.AddIn.ReadDataBody.Roles.ContainsKey(input.Name), input.Name));
            Assert.Equal(new[] { "config", "baseplan", "villaBlocks", "curbs", "roadSurface", "dlus", "sales", "plotNumbers" }, inputs.Select(input => SiteAdaptation.AddIn.ReadDataBody.Roles[input.Name]));
        }

        [Fact]
        public void R372_a_run_that_committed_marks_its_step_done_or_with_its_issues_and_a_failed_move_leaves_it()
        {
            // R366: a step is done while its inputs hash now as in its run, so the disk holds the Settings file by its hash
            var disk = new HashDisk();
            disk.Add(Folder + "project.json", HashA);
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" }, disk);
            var state = new PaneRunState(model, () => @"D:\Work", path => null);

            state.Committed(1, Record(new string[0], new[] { @"D:\Work\a.json" }, HashA));
            Assert.Equal("Ready", model.Steps[0].ChipText);
            state.Committed(1, Record(new string[0], new string[0], HashA));
            Assert.Equal(new[] { "Done", "Ready", "Needs step 2" }, model.Steps.Take(3).Select(step => step.ChipText));
            Assert.Equal("Run 2 Read data", model.BoxButton);
            Assert.Equal(0, state.MissingStep(2));
            // Step 2 is done only with every input of button 2 on disk and hashed as in its run (R366)
            foreach (string key in InputKeys.All)
            {
                model.Settings.Pick(key, Folder + key);
                disk.Add(Folder + key, HashA);
            }
            var inputs = PaneRunState.InputsOf(model.Settings, 2).ToDictionary(input => input.Name, input => HashA);
            state.Committed(2, new RunRecord("run2", inputs, new[] { "one", "two" }, new Dictionary<string, string>(), new string[0]));
            Assert.Equal("2 issues", model.Steps[1].ChipText);
            Assert.Equal("Done with 2 issues", model.Steps[1].ChipTip);
            Assert.Equal(MarkKind.Issues, model.Steps[1].Mark);
            Assert.Equal("Ready", model.Steps[2].ChipText);

            DocumentSession first = model.Document;
            model.Load(new DocumentSession("other", Folder + "Other.rvt", "Other"));
            Assert.Equal("Needs setup", model.Steps[0].ChipText);
            model.Load(first);
            Assert.Equal("Done", model.Steps[0].ChipText);
        }

        private const string HashA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string HashB = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
        private const string HashC = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        // W9 of steps/lane-b.md. The step lines come from StepStates of R366 by the one hash over the inputs of each step now, as the
        // pane hashed them, so an input changed since a run makes that step and every one after it run again
        [Fact]
        public void R366_the_pane_step_lines_come_from_the_step_state_by_hash_and_a_changed_input_runs_the_step_again()
        {
            var disk = new HashDisk();
            disk.Add(Folder + "project.json", HashA);
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" }, disk);
            var state = new PaneRunState(model, () => @"D:\Work", path => null);

            state.Committed(1, Record(new string[0], new string[0], HashA));
            Assert.Equal(new[] { "Done", "Ready", "Needs step 2" }, model.Steps.Take(3).Select(step => step.ChipText));

            disk.Add(Folder + "project.json", HashB, 2);
            model.Load(model.Document);
            Assert.Equal(new[] { "Ready", "Needs step 1" }, model.Steps.Take(2).Select(step => step.ChipText));
            Assert.Equal(1, state.MissingStep(2));
        }

        // R366. A step is done only while every output its record lists is on disk with the SHA-256 the record gives
        [Fact]
        public void R366_the_pane_shows_a_step_ready_again_when_an_output_of_its_record_is_gone_or_changed()
        {
            var disk = new HashDisk();
            disk.Add(Folder + "project.json", HashA);
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" }, disk);
            var state = new PaneRunState(model, () => @"D:\Work", path => null);
            var outputs = new Dictionary<string, string> { { @"D:\Work\site.json", HashC } };

            state.Committed(1, new RunRecord("run1", new Dictionary<string, string> { { "Settings", HashA } }, new string[0], outputs, new string[0]));
            Assert.Equal("Ready", model.Steps[0].ChipText);
            disk.Add(@"D:\Work\site.json", HashC);
            model.Load(model.Document);
            Assert.Equal("Done", model.Steps[0].ChipText);
            disk.Add(@"D:\Work\site.json", HashB, 2);
            model.Load(model.Document);
            Assert.Equal("Ready", model.Steps[0].ChipText);
        }

        // R366 and Q144: the pane maps each line of StepStates, 9 Report needs 4 Check only, so 9 is Ready once 4 is done
        [Fact]
        public void R366_the_pane_maps_each_step_state_and_9_report_is_ready_once_4_check_is_done()
        {
            var scope = new SettingsScope();
            var steps = new Dictionary<int, StepNow>();
            for (int step = 1; step <= 4; step++)
            {
                var run = new StepRun(new DateTime(2026, 10, 30, 10, step, 0, DateTimeKind.Utc), HashA, scope, step == 4 ? 3 : 0, null);
                steps[step] = new StepNow(run, HashA, null);
            }
            IReadOnlyList<StepStatus> lines = StepPlan.Of(true, scope, steps);
            Assert.Equal(new[] { StepKind.Done, StepKind.Done, StepKind.Done, StepKind.Issues, StepKind.Ready, StepKind.NeedsStep, StepKind.NeedsStep, StepKind.NeedsStep, StepKind.Ready }, lines.Select(line => line.Kind));
            Assert.Equal(3, lines[3].Value);
            Assert.Equal(5, lines[5].Value);
            Assert.Equal(5, StepPlan.Next(lines));
            Assert.Equal(StepKind.NeedsSettings, StepPlan.Of(false, scope, new Dictionary<int, StepNow>())[0].Kind);
        }

        // J1 and J19. The pane reads the settings of the document by R367, its last runs among them, so a project set up by an earlier
        // run of button 1 shows step 1 Done when it opens again while its inputs hash the same
        [Fact]
        public void R367_the_pane_reads_the_settings_entity_by_r367_and_shows_step_1_done_from_its_last_run()
        {
            var disk = new HashDisk();
            disk.Add(Folder + "project.json", HashA);
            var project = new ProjectSettings { ConfigFile = Folder + "project.json" };
            project.LastRuns["1"] = new ButtonRun { Time = "2026-10-30T10:00:00.000Z", Mode = RunModes.Direct, Scope = new SettingsScope(), InputHashes = new SortedDictionary<string, string> { { "Settings", HashA } } };
            var entity = new MemoryEntity { Json = SettingsJson.Write(project) };

            var session = new DocumentSession("s", Folder + "Site.rvt", "Site");
            Assert.Null(session.LoadFrom(entity));
            var model = new PaneViewModel(disk);
            model.Load(session);
            Assert.Equal(Folder + "project.json", model.Settings.ConfigFile);
            Assert.Equal(new[] { "Done", "Ready" }, model.Steps.Take(2).Select(step => step.ChipText));

            // The 5a reader took any text as a hash, R367 refuses one that is not 64 lower case hex digits and names it
            entity.Json = "{\"formatVersion\": 1, \"inputs\": {\"baseplan\": {\"path\": \"a.dwg\", \"sha256\": \"ab\"}}}";
            var refused = new DocumentSession("t", Folder + "Other.rvt", "Other");
            Assert.Contains("SHA-256", refused.LoadFrom(entity));
            Assert.False(refused.Settings.Chosen);
            entity.Json = null;
            Assert.Null(refused.LoadFrom(entity));
        }

        // Item 6 of the 5c join and R404. With no villaMode in the settings the row shows villaModeDefault of addin.lightMode of config,
        // and only a pick writes villaMode into the settings
        [Fact]
        public void R404_the_villa_mode_row_shows_the_default_of_config_until_a_pick()
        {
            var disk = new HashDisk { VillaDefault = VillaModes.Links };
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" }, disk);
            Assert.True(model.VillaLinks);
            Assert.False(model.VillaMasses);
            Assert.Null(model.Settings.Project.VillaMode);
            model.VillaMasses = true;
            Assert.Equal(VillaModes.Masses, model.Settings.Project.VillaMode);
            Assert.False(model.VillaLinks);

            PaneViewModel none = Loaded(new PaneSettings(), new HashDisk { VillaDefault = null });
            Assert.False(none.VillaLinks);
            Assert.False(none.VillaMasses);
        }

        // R366. The pane hashes a folder input by InputHash of the flow, so the hash of a run and the hash now are one method
        [Fact]
        public void R366_the_pane_hashes_a_folder_as_the_flow_does()
        {
            string folder = Path.Combine(Path.GetTempPath(), "pane-folder-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(folder, "Sub"));
                File.WriteAllText(Path.Combine(folder, "Sub", "A.rvt"), "a");
                File.WriteAllText(Path.Combine(folder, "b.rvt"), "bb");
                Assert.Equal(InputHash.OfFolder(folder), DiskProbe.FolderSha256(folder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        // A second way: the flow of PR 89 run through the run state of the pane, its stops and a real run that commits
        [Fact]
        public void R372_the_flow_of_a_button_runs_on_the_run_state_of_the_pane_from_its_stops_to_the_step_line()
        {
            string root = Path.Combine(Path.GetTempPath(), "pane-flow-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                string config = Path.Combine(root, "project.json");
                string document = Path.Combine(root, "Site.rvt");
                // The disk itself, hashed at once, so the step state of R366 sees the file the flow hashed
                PaneViewModel model = Loaded(new PaneSettings(), new SyncDisk());
                var state = new PaneRunState(model, () => Path.Combine(root, "work"), WorkFolder.ReadIfThere);
                var body = new ButtonBody { Preview = context => new RunSummary("1 plot", null), Apply = context => new string[0] };
                var flow = new ButtonFlow(state, new Model(), new Confirm(), InputHash.Of, () => "run1");

                Assert.Equal("Save the project first, its work folder is named by its file", flow.Run(1, "1 Set up site", body, string.Empty).Message);
                Assert.Equal("1 Set up site needs Settings, the file is not chosen", flow.Run(1, "1 Set up site", body, document).Message);
                Assert.Equal("2 Read data needs step 1", flow.Run(2, "2 Read data", body, document).Message);
                model.PickConfig(config);
                Assert.Equal("1 Set up site needs Settings, the file is not on disk", flow.Run(1, "1 Set up site", body, document).Message);
                File.WriteAllText(config, "{}");

                RunOutcome outcome = flow.Run(1, "1 Set up site", body, document);
                Assert.Equal(RunEnd.Committed, outcome.End);
                Assert.Equal("Done", model.Steps[0].ChipText);
                Assert.Equal("Ready", model.Steps[1].ChipText);
                Assert.Equal("Run 2 Read data", model.BoxButton);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        // R457, found by hand in Task 6: SetNbhCodes had no caller in the add-in, so the Neighbourhood list stayed empty and 3 Review data,
        // which shows one NBH, could not run
        [Fact]
        public void R457_the_neighbourhood_list_fills_from_the_latest_read_and_keeps_the_code_of_the_settings()
        {
            string root = Path.Combine(Path.GetTempPath(), "sa-nbh-" + Guid.NewGuid().ToString("N"));
            try
            {
                PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" });
                var state = new PaneRunState(model, () => root, path => null);
                string work = state.WorkFolder(model.Document.Path);

                state.RefreshNbhCodes();
                Assert.Empty(model.NbhCodes);

                WriteRead(work, "read_old", "2_20260101", "007");
                WriteRead(work, "read_new", "2_20260102", "012", "003", "012", null);
                state.Committed(2, new RunRecord("run2", new Dictionary<string, string>(), new string[0], new Dictionary<string, string>(), new string[0]));
                Assert.Equal(new[] { "003", "012" }, model.NbhCodes);

                model.Settings.Project.Scope.Nbh = "099";
                state.RefreshNbhCodes();
                Assert.Equal(new[] { "003", "012", "099" }, model.NbhCodes);

                File.WriteAllText(Path.Combine(work, "read_new", Common.ReadData.ReadDataRun.PlotTableFile), "{ not json");
                state.RefreshNbhCodes();
                Assert.Equal(new[] { "099" }, model.NbhCodes);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        // Review of R457: a combo given a new ItemsSource may write a null SelectedItem back, which would wipe the scope saved with the model
        [Fact]
        public void R457_a_new_list_keeps_the_saved_neighbourhood_when_the_combo_clears_its_selection()
        {
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" });
            model.Nbh = "012";
            int swaps = 0;
            model.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(PaneViewModel.NbhCodes))
                {
                    swaps++;
                    model.Nbh = null;
                }
            };

            model.SetNbhCodes(new[] { "003", "012" });
            Assert.Equal("012", model.Nbh);
            Assert.Equal(1, swaps);

            model.SetNbhCodes(new[] { "003", "012" });
            Assert.Equal(1, swaps);
            Assert.Equal("012", model.Nbh);
        }

        // A row with no official number is no unit plot, so its neighbourhood offers no choice (R366)
        private static void WriteRead(string work, string name, string runId, params string[] nbhs)
        {
            string folder = Path.Combine(work, name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, Common.ReadData.ReadDataRun.RecordFile), new Newtonsoft.Json.Linq.JObject { ["format"] = Common.ReadData.ReadDataRun.RecordFormat, ["runId"] = runId }.ToString());
            var rows = new Newtonsoft.Json.Linq.JArray(nbhs.Select((nbh, index) => new Newtonsoft.Json.Linq.JObject { ["officialNumber"] = (index + 1).ToString(), ["nbhId"] = nbh }));
            rows.Add(new Newtonsoft.Json.Linq.JObject { ["officialNumber"] = null, ["nbhId"] = "055" });
            File.WriteAllText(Path.Combine(folder, Common.ReadData.ReadDataRun.PlotTableFile), new Newtonsoft.Json.Linq.JObject { ["plots"] = rows }.ToString());
        }

        private static PaneViewModel Loaded(PaneSettings settings, IInputProbe probe = null)
        {
            var model = new PaneViewModel(probe ?? new NoDisk());
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.LoadSettings(settings);
            return model;
        }

        private static RunRecord Record(IReadOnlyList<string> issues, IReadOnlyList<string> failedMoves, string settingsHash = null)
        {
            var hashes = settingsHash == null ? new Dictionary<string, string>() : new Dictionary<string, string> { { "Settings", settingsHash } };
            return new RunRecord("run1", hashes, issues, new Dictionary<string, string>(), failedMoves);
        }

        // A disk of made up files by path, each with its hash and stamp, hashed at once
        private sealed class HashDisk : IInputProbe
        {
            private readonly Dictionary<string, Tuple<string, long>> files = new Dictionary<string, Tuple<string, long>>(StringComparer.OrdinalIgnoreCase);

            public string VillaDefault { get; set; } = VillaModes.Masses;

            public void Add(string path, string sha256, long stamp = 1)
            {
                files[path] = Tuple.Create(sha256, stamp);
            }

            public InputFacts Look(string path, bool folder)
            {
                return path != null && files.TryGetValue(path, out Tuple<string, long> file) ? new InputFacts { Found = true, Size = 10, Stamp = file.Item2 } : new InputFacts();
            }

            public void Hash(string path, bool folder, Action<string> done)
            {
                done(files[path].Item1);
            }

            public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
            {
                fallback = null;
                return new string[0];
            }

            public string RulesName(string rulesFile)
            {
                return null;
            }

            public string VillaModeDefault(string configFile)
            {
                return VillaDefault;
            }
        }

        private sealed class SyncDisk : IInputProbe
        {
            private readonly DiskProbe disk = new DiskProbe(action => action());

            public InputFacts Look(string path, bool folder)
            {
                return disk.Look(path, folder);
            }

            public void Hash(string path, bool folder, Action<string> done)
            {
                done(folder ? DiskProbe.FolderSha256(path) : DiskProbe.FileSha256(path));
            }

            public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
            {
                fallback = null;
                return new string[0];
            }

            public string RulesName(string rulesFile)
            {
                return null;
            }

            public string VillaModeDefault(string configFile)
            {
                return null;
            }
        }

        private sealed class MemoryEntity : ISettingsEntity
        {
            public string Json { get; set; }

            public string Read()
            {
                return Json;
            }

            public void Write(string json)
            {
                Json = json;
            }
        }

        private sealed class NoDisk : IInputProbe
        {
            public InputFacts Look(string path, bool folder)
            {
                return new InputFacts();
            }

            public void Hash(string path, bool folder, Action<string> done)
            {
            }

            public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
            {
                fallback = null;
                return new string[0];
            }

            public string RulesName(string rulesFile)
            {
                return null;
            }

            public string VillaModeDefault(string configFile)
            {
                return null;
            }
        }

        private sealed class Model : IModel
        {
            public IModelTransaction Start(string name)
            {
                return new Transaction();
            }
        }

        private sealed class Transaction : IModelTransaction
        {
            public IReadOnlyList<string> Warnings { get; } = new string[0];
            public IReadOnlyList<string> Errors { get; } = new string[0];

            public bool Commit()
            {
                return true;
            }

            public void RollBack()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class Confirm : IConfirm
        {
            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                return true;
            }
        }
    }
}
