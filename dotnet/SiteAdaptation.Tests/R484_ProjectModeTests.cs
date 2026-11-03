using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;
using SiteAdaptation.Common.Update;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R484. The project mode of the settings, laid once over designBasis.mode where a run loads its config. Made up names, the shipped config for its modes.
    // python/tests/test_r484_project_mode.py holds the same lines
    public sealed class R484_ProjectModeTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "r484-" + Guid.NewGuid().ToString("N"));

        public R484_ProjectModeTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private static string PastMode()
        {
            return ((JArray)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["designBasis"]["modes"]).Select(mode => (string)mode).Single(mode => mode != DesignBasis.Rev6);
        }

        private static JObject Made(string mode, string newProject = DesignBasis.Rev6)
        {
            return new JObject
            {
                ["designBasis"] = new JObject
                {
                    ["mode"] = mode, ["modes"] = new JArray(DesignBasis.Rev6, "pastMade"), ["newProjectMode"] = newProject,
                    ["roadLevel"] = new JObject { ["surfaces"] = new JArray("Made Up Job__Road A", "Made Up Job__Road B") },
                },
            };
        }

        private string ConfigFile(JObject config)
        {
            string path = Path.Combine(root, "project.json");
            File.WriteAllText(path, config.ToString());
            return path;
        }

        // The shipped config

        [Fact]
        public void R484_the_shipped_config_is_the_past_job_and_a_new_project_starts_on_rev6()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            Assert.Equal(PastMode(), ProjectModes.OfConfig(config));
            Assert.Equal(DesignBasis.Rev6, ProjectModes.NewProject(config));
        }

        // Apply

        [Fact]
        public void R484_no_mode_leaves_the_config_and_a_mode_replaces_the_mode_for_every_reader()
        {
            JObject config = Made("pastMade");
            Assert.Same(config, ProjectModes.Apply(config, null));
            Assert.Equal("pastMade", ProjectModes.OfConfig(config));
            Assert.False(RoadSurfaceChoices.IsRev6(config));
            Assert.Null(RoadSurfaceChoices.OrderText(config, null));

            ProjectModes.Apply(config, DesignBasis.Rev6);
            Assert.Equal(DesignBasis.Rev6, DesignBasis.ModeOf(config));
            Assert.True(RoadSurfaceChoices.IsRev6(config));
            Assert.Equal("Made Up Job__Road A\nMade Up Job__Road B", RoadSurfaceChoices.OrderText(config, null));
        }

        [Fact]
        public void R484_a_mode_outside_design_basis_modes_is_refused_by_its_name_with_the_line_of_python()
        {
            Assert.Equal("the project mode manual7 is no mode of designBasis.modes rev6, pastMade (R484)",
                Assert.Throws<InvalidDataException>(() => ProjectModes.Apply(Made("pastMade"), "manual7")).Message);
            Assert.Equal("the project mode rev6 is no mode of designBasis.modes none (R484)",
                Assert.Throws<InvalidDataException>(() => ProjectModes.Apply(JObject.Parse("{ 'designBasis': { 'mode': 'rev6' } }"), DesignBasis.Rev6)).Message);
            string python = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "python", "design_basis.py"));
            Assert.Contains("the project mode %s is no mode of designBasis.modes %s (R484)", python);
        }

        [Fact]
        public void R484_read_settings_under_the_mode_of_the_request_gives_the_read_its_order_on_a_past_job_config()
        {
            string path = ConfigFile(Made("pastMade"));
            Assert.False(ReadSettings.Load(path).IsRev6);
            ReadSettings read = ReadSettings.Load(path, DesignBasis.Rev6);
            Assert.True(read.IsRev6);
            Assert.Equal(new[] { "Made Up Job__Road A", "Made Up Job__Road B" }, read.ReadRoads(null));
        }

        // The settings JSON

        [Fact]
        public void R484_old_settings_with_no_mode_read_null_and_write_no_key_at_format_version_1()
        {
            ProjectSettings read = SettingsJson.Read("{ \"formatVersion\": 1 }");
            Assert.Null(read.ProjectMode);
            string written = SettingsJson.Write(read);
            Assert.DoesNotContain("projectMode", written);
            Assert.Equal(1, (int)JObject.Parse(written)["formatVersion"]);
        }

        [Fact]
        public void R484_the_settings_read_and_write_the_mode()
        {
            string written = SettingsJson.Write(new ProjectSettings { ProjectMode = DesignBasis.Rev6 });
            Assert.Equal(DesignBasis.Rev6, (string)JObject.Parse(written)["projectMode"]);
            Assert.Equal(DesignBasis.Rev6, SettingsJson.Read(written).ProjectMode);
        }

        [Fact]
        public void R484_settings_with_a_mode_outside_the_modes_of_their_config_are_refused_by_name()
        {
            string path = ConfigFile(Made("pastMade"));
            string json = SettingsJson.Write(new ProjectSettings { ConfigFile = path, ProjectMode = "manual7" });
            Assert.Equal("the settings JSON names projectMode manual7, not one of rev6, pastMade",
                Assert.Throws<InvalidDataException>(() => SettingsJson.Read(json)).Message);
            Assert.Equal("pastMade", SettingsJson.Read(SettingsJson.Write(new ProjectSettings { ConfigFile = path, ProjectMode = "pastMade" })).ProjectMode);
        }

        [Theory]
        [InlineData("7")]
        [InlineData("true")]
        [InlineData("\"\"")]
        public void R484_settings_with_a_mode_that_is_no_text_are_refused_though_no_config_is_on_disk(string value)
        {
            Assert.Equal("the settings JSON names a projectMode that is no text",
                Assert.Throws<InvalidDataException>(() => SettingsJson.Read("{ \"formatVersion\": 1, \"projectMode\": " + value + " }")).Message);
        }

        [Fact]
        public void R484_a_pre_setting_carries_the_mode_and_one_with_none_clears_it()
        {
            string export = PreSetting.Export(new ProjectSettings { ProjectMode = DesignBasis.Rev6 });
            var into = new ProjectSettings();
            PreSetting.Import(export, into);
            Assert.Equal(DesignBasis.Rev6, into.ProjectMode);
            PreSetting.Import(PreSetting.Export(new ProjectSettings()), into);
            Assert.Null(into.ProjectMode);
        }

        // Save setup

        private static Func<string, string> Reader(JObject config)
        {
            return path => path == "cfg" ? config.ToString() : null;
        }

        // A settings JSON of a Save or a Use saved of before the project mode, as the model holds it
        private const string OlderSave = "{ \"formatVersion\": 1, \"configFile\": \"cfg\", \"rulesFile\": \"rules\", \"inputs\": { \"baseplan\": { \"path\": \"b.dwg\" } } }";

        [Fact]
        public void R484_save_setup_on_empty_of_a_project_that_names_a_config_and_holds_no_mode_stays_on_the_mode_of_its_config()
        {
            ProjectSettings saved = SetupSave.Apply(new SetupDraft(), SettingsJson.Read(OlderSave), Reader(Made("pastMade")));
            Assert.Null(saved.ProjectMode);
            Assert.Equal("cfg", saved.ConfigFile);
            Assert.DoesNotContain("projectMode", SettingsJson.Write(saved));
        }

        [Fact]
        public void R484_use_saved_of_an_older_save_then_a_plain_save_setup_stays_on_the_mode_of_its_config()
        {
            var job = new ProjectSettings();
            PreSetting.Import(PreSetting.Export(new ProjectSettings { ConfigFile = "cfg" }), job);
            Assert.Null(job.ProjectMode);
            Assert.Null(SetupSave.Apply(new SetupDraft(), job, Reader(Made("pastMade"))).ProjectMode);
        }

        [Fact]
        public void R484_a_project_takes_the_new_project_mode_only_when_the_settings_before_the_save_name_no_config()
        {
            var fresh = new ProjectSettings { ConfigFile = "cfg" };
            ProjectModes.TakeNewProject(null, fresh, Reader(Made("pastMade")));
            Assert.Equal(DesignBasis.Rev6, fresh.ProjectMode);

            var named = new ProjectSettings { ConfigFile = "cfg" };
            ProjectModes.TakeNewProject("cfg", named, Reader(Made("pastMade")));
            Assert.Null(named.ProjectMode);

            var holds = new ProjectSettings { ConfigFile = "cfg", ProjectMode = "pastMade" };
            ProjectModes.TakeNewProject(null, holds, Reader(Made("pastMade")));
            Assert.Equal("pastMade", holds.ProjectMode);

            var bad = new ProjectSettings { ConfigFile = "cfg" };
            ProjectModes.TakeNewProject(null, bad, Reader(Made("pastMade", "manual7")));
            Assert.Null(bad.ProjectMode);
        }

        [Fact]
        public void R484_empty_project_with_no_config_known_holds_no_mode()
        {
            Assert.Null(SetupSave.Apply(new SetupDraft(), new ProjectSettings(), Reader(Made("pastMade"))).ProjectMode);
        }

        [Fact]
        public void R484_a_saved_project_keeps_its_mode_on_any_config()
        {
            string start = PreSetting.Export(new ProjectSettings { ConfigFile = "cfg", ProjectMode = "pastMade" });
            Assert.Equal("pastMade", SetupSave.Apply(new SetupDraft { StartText = start }, new ProjectSettings(), Reader(Made(DesignBasis.Rev6))).ProjectMode);
            string rev6 = PreSetting.Export(new ProjectSettings { ConfigFile = "cfg", ProjectMode = DesignBasis.Rev6 });
            Assert.Equal(DesignBasis.Rev6, SetupSave.Apply(new SetupDraft { StartText = rev6 }, new ProjectSettings(), Reader(Made("pastMade"))).ProjectMode);
        }

        [Fact]
        public void R484_a_saved_project_with_none_reads_the_mode_of_config()
        {
            string start = PreSetting.Export(new ProjectSettings { ConfigFile = "cfg" });
            Assert.Equal("pastMade", SetupSave.Apply(new SetupDraft { StartText = start }, new ProjectSettings(), Reader(Made("pastMade"))).ProjectMode);
        }

        [Fact]
        public void R484_save_setup_on_empty_keeps_the_mode_a_project_holds()
        {
            var job = new ProjectSettings { ConfigFile = "cfg", ProjectMode = "pastMade" };
            Assert.Equal("pastMade", SetupSave.Apply(new SetupDraft(), job, Reader(Made("pastMade"))).ProjectMode);
        }

        [Fact]
        public void R484_a_new_project_mode_outside_the_modes_falls_back_on_the_mode_of_config_and_never_stops_the_window()
        {
            Assert.Null(ProjectModes.NewProject(Made("pastMade", "manual7")));
        }

        [Fact]
        public void R484_a_config_that_does_not_read_is_no_config()
        {
            Assert.Null(ProjectModes.ReadConfig("cfg", path => "{ not json"));
            Assert.Null(ProjectModes.ReadConfig(null, path => "{}"));
            Assert.Null(ProjectModes.ReadConfig("gone", path => null));
            Assert.Null(ProjectModes.ReadConfig("cfg", null));
        }

        // The request of every button

        [Fact]
        public void R484_a_request_with_no_mode_leaves_the_key_out_of_its_json_and_with_one_holds_it()
        {
            var request = new WorkerRequest { FormatVersion = 1, RunId = "r1", Command = "build" };
            Assert.DoesNotContain("projectMode", request.ToJson());
            request.ProjectMode = DesignBasis.Rev6;
            Assert.Contains("\"projectMode\": \"rev6\"", request.ToJson());
        }

        [Fact]
        public void R484_the_mode_reaches_the_build_through_the_request_on_a_shipped_past_job_config()
        {
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest request = steps.Request("m1", "levels");
                string floors = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "floor_outlines.json");
                request.Inputs.Add(new WorkerFile { Role = BuildSteps.BandsRole, Path = floors, Sha256 = FileHash.Sha256(floors) });
                // No edit of the config: its mode stays the past job, the request alone holds rev6
                Assert.Equal(PastMode(), (string)JObject.Parse(File.ReadAllText(request.Inputs.Single(input => input.Role == "config").Path))["designBasis"]["mode"]);
                request.ProjectMode = DesignBasis.Rev6;
                int exit = WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0);
                Assert.True(exit == 0, WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, "m1")).Message);
                JObject levels = JObject.Parse(File.ReadAllText(Path.Combine(request.StepsFolder, "step_levels_csharp.json")));
                Assert.Equal(DesignBasis.Rev6, (string)levels[RunStamp.Key]["designBasis"]);
            }
        }

        // The same made up site three ways: the mode by the request, by a config copy, and the past job. The first two equal plot by plot, the third is another build
        [Fact]
        public void R484_a_build_by_the_mode_of_the_request_equals_a_build_of_a_config_copy_under_rev6()
        {
            using (var steps = new LevelFloorStepTests())
            {
                string Build(string runId, string mode, bool copy)
                {
                    WorkerRequest request = steps.Request(runId, "fronts", "levels", "floors");
                    string floors = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "floor_outlines.json");
                    request.Inputs.Add(new WorkerFile { Role = BuildSteps.BandsRole, Path = floors, Sha256 = FileHash.Sha256(floors) });
                    if (copy)
                    {
                        WorkerFile settings = request.Inputs.Single(input => input.Role == "config");
                        JObject config = JObject.Parse(File.ReadAllText(settings.Path));
                        config["designBasis"]["mode"] = DesignBasis.Rev6;
                        File.WriteAllText(settings.Path, config.ToString());
                        settings.Sha256 = FileHash.Sha256(settings.Path);
                    }
                    request.ProjectMode = mode;
                    int exit = WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0);
                    Assert.True(exit == 0, WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, runId)).Message);
                    return string.Join("\n", new[] { "fronts", "levels", "floors" }.Select(step => JObject.Parse(File.ReadAllText(Path.Combine(request.StepsFolder, "step_" + step + "_csharp.json")))["plots"].ToString()));
                }

                string past = Build("e1", null, false);
                string byRequest = Build("e2", DesignBasis.Rev6, false);
                string byCopy = Build("e3", null, true);
                Assert.Equal(byCopy, byRequest);
                Assert.NotEqual(past, byRequest);
            }
        }

        [Fact]
        public void R484_a_request_with_no_mode_builds_the_past_job_of_the_shipped_config()
        {
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest request = steps.Request("m2", "levels");
                Assert.Equal(0, WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0));
                JObject levels = JObject.Parse(File.ReadAllText(Path.Combine(request.StepsFolder, "step_levels_csharp.json")));
                Assert.Equal(PastMode(), (string)levels[RunStamp.Key]["designBasis"]);
            }
        }

        [Fact]
        public void R484_a_build_refuses_a_request_with_a_mode_outside_the_modes_by_name()
        {
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest request = steps.Request("m3", "levels");
                request.ProjectMode = "manual7";
                Assert.NotEqual(0, WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0));
                Assert.StartsWith("the project mode manual7 is no mode of designBasis.modes", WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, "m3")).Message);
            }
        }

        // The request CheckRun really makes, run by the real worker build: the runner the add-in starts, on made up files in the folders of a project

        internal sealed class RealWorker : IWorkerStart
        {
            public WorkerRequest Request { get; private set; }
            public int Exit { get; private set; }

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Request = request;
                Exit = WorkerRunner.Run(requestPath, WorkerCommands.Default(), () => 9000.0);
                ended(WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId)));
            }
        }

        private sealed class NoTable : ICheckTableDialog
        {
            public CheckTableAnswer Show(CheckTableView view, CheckTableCallbacks callbacks)
            {
                return new CheckTableAnswer();
            }
        }

        private string CopyTo(string path, params string[] parts)
        {
            return CopyInto(root, path, parts);
        }

        private static string CopyInto(string root, string path, params string[] parts)
        {
            string target = Path.Combine(new[] { root }.Concat(parts).ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(path, target, true);
            return target;
        }

        private string WriteTo(string text, params string[] parts)
        {
            return WriteInto(root, text, parts);
        }

        private static string WriteInto(string root, string text, params string[] parts)
        {
            string target = Path.Combine(new[] { root }.Concat(parts).ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, text);
            return target;
        }

        // The project of button 4 on the made up site of LevelFloorStepTests, the shipped config with no edit of its mode, and 4 Check run on it
        private RealWorker RunCheck(LevelFloorStepTests steps, string runId, string mode, out RunLater end)
        {
            WorkerRequest seed = steps.Request("seed", BuildPlan.Steps);
            string shipped = Path.GetDirectoryName(WorkerRunTests.RepoConfig());
            string config = CopyTo(seed.Inputs.Single(input => input.Role == "config").Path, "repo", "config", "project.json");
            CopyTo(Path.Combine(shipped, "floor_outlines.json"), "repo", "config", "floor_outlines.json");
            CopyTo(Path.Combine(shipped, "prototype_offsets.json"), "repo", "config", "prototype_offsets.json");
            string rules = CopyTo(seed.Inputs.Single(input => input.Role == "rules").Path, "repo", "rules", "made-up.json");
            WriteTo(new JObject { ["overrides"] = new JArray(new JObject { ["plot"] = "3", ["field"] = "heightM", ["value"] = JObject.Parse("{\"gapToRuleM\": 0.1}"), ["source"] = "parsons", ["model"] = "a.rvt", ["key"] = "imc" }) }.ToString(), "repo", "config", "overrides", "made-up.json");
            return CheckOn(root, seed, config, rules, null, runId, mode, out end);
        }

        // 4 Check of a project at this config and rules file on the read files of a seed request of the made up site, by the real worker
        internal static RealWorker CheckOn(string root, WorkerRequest seed, string config, string rules, string levelProfile, string runId, string mode, out RunLater end)
        {
            string Seeded(string role) => seed.Inputs.Single(input => input.Role == role).Path;
            string prototypes = Path.GetDirectoryName(seed.Inputs.First(input => input.Role.StartsWith(BuildSteps.PrototypeRole, StringComparison.Ordinal)).Path);
            string work = Path.Combine(root, "work");
            JObject values = JObject.Parse(File.ReadAllText(config));
            var levels = new JObject();
            foreach (string kind in new[] { "generic", "yard", "ramp" })
            {
                levels[(string)values["floors"]["levels"][kind]] = 0.0;
            }
            WriteInto(root, new JObject { ["levels"] = levels, ["surveyPoint"] = JObject.Parse("{ 'position': [0, 0, 0], 'sharedPosition': [0, 0, 0] }"), ["angleToTrueNorthDeg"] = 0.0 }.ToString(), "work", Common.SetUp.SetUpRun.SiteFile);
            string read = ReadDataRun.FolderPrefix + "0123";
            foreach (KeyValuePair<string, string> file in CheckRun.ReadFiles)
            {
                CopyInto(root, Seeded(file.Key), "work", read, file.Value);
            }
            WriteInto(root, "{ \"plots\": [] }", "work", read, ReadDataRun.PlotTableFile);
            var listed = new List<Tuple<string, string>> { Tuple.Create("config", config), Tuple.Create("plotsCsv", Seeded("plotsCsv")), Tuple.Create("labels", Seeded("labels")) };
            listed.AddRange(seed.Inputs.Where(input => input.Role.StartsWith(WorkerRunner.RoadSurfaceRole, StringComparison.Ordinal)).Select(input => Tuple.Create(input.Role, input.Path)));
            WriteInto(root, new JObject
            {
                ["format"] = ReadDataRun.RecordFormat, ["runId"] = "R2-1",
                ["inputs"] = new JArray(listed.Select(item => new JObject { ["role"] = item.Item1, ["path"] = item.Item2, ["sha256"] = FileHash.Sha256(item.Item2) })),
            }.ToString(), "work", read, ReadDataRun.RecordFile);

            var worker = new RealWorker();
            var roles = new Dictionary<string, string> { ["Settings"] = CheckRun.Config, ["Rules"] = CheckRun.Rules, ["Prototypes"] = CheckRun.Prototypes };
            var run = new CheckRun(roles, worker, null, new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" }, () => new SettingsScope(), new NoTable(), null, levelProfile) { ModePick = () => mode };
            var pane = new[] { new RunInput("Settings", config, false), new RunInput("Rules", rules, false), new RunInput("Prototypes", prototypes, true) };
            RunLater ended = null;
            run.Body().Start(new RunContext(4, "4 Check", runId, work, null, true, pane, new Dictionary<string, string>()), later => ended = later);
            end = ended;
            return worker;
        }

        private static string AnswerOf(RealWorker worker)
        {
            return WorkerAnswer.Read(WorkerFiles.AnswerPath(worker.Request.OutputFolder, worker.Request.RunId)).Message;
        }

        [Fact]
        public void R484_R408_button_4_under_the_project_mode_rev6_builds_through_the_real_worker_with_the_floor_file_of_the_request()
        {
            using (var steps = new LevelFloorStepTests())
            {
                RunLater end;
                RealWorker worker = RunCheck(steps, "4_real-rev6", DesignBasis.Rev6, out end);
                Assert.True(worker.Exit == 0, "exit " + worker.Exit + ": " + AnswerOf(worker));
                Assert.Null(end.Failure);
                WorkerFile floors = Assert.Single(worker.Request.Inputs, input => input.Role == BuildSteps.BandsRole);
                Assert.Equal(Path.Combine(root, "repo", "config", "floor_outlines.json"), floors.Path);
                Assert.Equal(FileHash.Sha256(floors.Path), floors.Sha256);
                Assert.True(File.Exists(Path.Combine(worker.Request.OutputFolder, BuildPlan.FileName(null))));
                JObject levels = JObject.Parse(File.ReadAllText(Path.Combine(worker.Request.StepsFolder, "step_levels_csharp.json")));
                Assert.Equal(DesignBasis.Rev6, (string)levels[RunStamp.Key]["designBasis"]);
                Assert.Contains(levels[RunStamp.Key]["inputs"], input => (string)input["role"] == BuildSteps.BandsRole);
            }
        }

        [Fact]
        public void R484_R408_button_4_of_the_past_job_names_no_floor_file_in_its_request_or_its_stamps()
        {
            using (var steps = new LevelFloorStepTests())
            {
                RunLater end;
                RealWorker worker = RunCheck(steps, "4_real-past", null, out end);
                Assert.True(worker.Exit == 0, "exit " + worker.Exit + ": " + AnswerOf(worker));
                Assert.Null(end.Failure);
                Assert.DoesNotContain(worker.Request.Inputs, input => input.Role == BuildSteps.BandsRole);
                Assert.DoesNotContain("projectMode", worker.Request.ToJson());
                JObject levels = JObject.Parse(File.ReadAllText(Path.Combine(worker.Request.StepsFolder, "step_levels_csharp.json")));
                Assert.Equal(PastMode(), (string)levels[RunStamp.Key]["designBasis"]);
                Assert.DoesNotContain(levels[RunStamp.Key]["inputs"], input => (string)input["role"] == BuildSteps.BandsRole);
            }
        }

        [Fact]
        public void R484_update_from_report_and_the_position_read_carry_the_mode_in_their_requests()
        {
            string config = Path.Combine(root, "config.json");
            File.WriteAllText(config, "{ \"addin\": { \"workerTimeLimitS\": { \"updateFromReport\": 60, \"crs\": 60 } } }");
            WorkerRequest update = UpdateWorkerRequest.Make("u1", new WorkerDocument(), config, config, config, config, "p", "012", new string[0], root, "pastMade");
            Assert.Equal("pastMade", update.ProjectMode);
            Assert.Null(UpdateWorkerRequest.Make("u2", new WorkerDocument(), config, config, config, config, "p", "012", new string[0], root).ProjectMode);
            WorkerRequest position = Common.SetUp.CoordinateRequest.Make(new RunInput[0], new Dictionary<string, string>(), config, "c1", root, new WorkerDocument(), 60, DesignBasis.Rev6);
            Assert.Equal(DesignBasis.Rev6, position.ProjectMode);
        }
    }
}

#if NET48
namespace SiteAdaptation.Tests
{
    using SiteAdaptation.AddIn.NewProject;
    using SiteAdaptation.AddIn.Pane;

    // R484, the windows and the pane, net48 alone as the add-in is
    public sealed class R484_ProjectModeWindowTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "r484w-" + Guid.NewGuid().ToString("N"));

        public R484_ProjectModeWindowTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static string Config(string mode)
        {
            return new JObject
            {
                ["designBasis"] = new JObject
                {
                    ["mode"] = mode, ["modes"] = new JArray(DesignBasis.Rev6, "pastMade"), ["newProjectMode"] = DesignBasis.Rev6,
                    ["roadLevel"] = new JObject { ["surfaces"] = new JArray("Made Up Job__Road A") },
                },
            }.ToString();
        }

        private static NewProjectViewModel Window(ProjectSettings settings, IReadOnlyList<SavedStart> starts, Func<string, string> read)
        {
            return new NewProjectViewModel(settings, "Site", starts, null, (path, isFolder) => true, null, read, null, true, (inFolder, effective) => null);
        }

        private static SetupRowViewModel RoadRow(NewProjectViewModel model)
        {
            return model.Rows.Single(item => item.Row.Id == SetupRows.RoadLevels);
        }

        [Fact]
        public void R484_road_pick_state_reads_the_mode_of_the_settings_over_the_mode_of_config()
        {
            Func<string, string> past = path => Config("pastMade"), rev6 = path => Config(DesignBasis.Rev6);
            Assert.Equal(RoadPickWhy.PastJob, RoadPickState.Of(new ProjectSettings { ConfigFile = "cfg" }, past).Why);
            Assert.Equal(RoadPickWhy.Ready, RoadPickState.Of(new ProjectSettings { ConfigFile = "cfg", ProjectMode = DesignBasis.Rev6 }, past).Why);
            Assert.Equal(RoadPickWhy.PastJob, RoadPickState.Of(new ProjectSettings { ConfigFile = "cfg", ProjectMode = "pastMade" }, rev6).Why);
            Assert.Equal(RoadPickWhy.NoSetup, RoadPickState.Of(new ProjectSettings { ConfigFile = "cfg", ProjectMode = "manual7" }, past).Why);
        }

        [Fact]
        public void R484_a_project_with_a_config_and_no_mode_shows_its_config_mode_and_a_save_leaves_it_there()
        {
            NewProjectViewModel past = Window(new ProjectSettings { ConfigFile = "cfg" }, new SavedStart[0], path => Config("pastMade"));
            Assert.Equal("ROSHN past job", past.ModeLine);
            Assert.False(RoadRow(past).SurfacesEnabled);
            past.Save();
            Assert.Null(past.Result.ProjectMode);

            NewProjectViewModel rev6 = Window(new ProjectSettings { ConfigFile = "cfg" }, new SavedStart[0], path => Config(DesignBasis.Rev6));
            Assert.Equal("New project rules, Rev 06", rev6.ModeLine);
            Assert.True(RoadRow(rev6).SurfacesEnabled);
        }

        [Fact]
        public void R484_R486_start_from_empty_with_no_config_and_no_shipped_set_shows_no_mode_line_and_the_stop()
        {
            NewProjectViewModel model = Window(new ProjectSettings(), new SavedStart[0], path => Config("pastMade"));
            Assert.Equal(string.Empty, model.ModeLine);
            Assert.True(model.StopShown);
            Assert.Equal(NewProjectText.NoStart, model.StopLine);
            Assert.False(RoadRow(model).SurfacesEnabled);
            model.Save();
            Assert.Null(model.Result.ProjectMode);
        }

        [Fact]
        public void R484_start_from_changes_the_line_a_saved_project_with_none_reads_the_past_job_of_its_config()
        {
            string saved = PreSetting.Export(new ProjectSettings { ConfigFile = "cfg" });
            NewProjectViewModel model = Window(new ProjectSettings(), new[] { new SavedStart("Past", "p.json", saved) }, path => Config("pastMade"));
            Assert.Equal(string.Empty, model.ModeLine);
            model.Start = model.Starts[1];
            Assert.Equal("ROSHN past job", model.ModeLine);
            Assert.False(RoadRow(model).SurfacesEnabled);
            model.Save();
            Assert.Equal("pastMade", model.Result.ProjectMode);
            model.Start = model.Starts[0];
            Assert.Equal(string.Empty, model.ModeLine);
        }

        [Fact]
        public void R484_reopened_on_empty_a_saved_past_job_stays_the_past_job()
        {
            NewProjectViewModel model = Window(new ProjectSettings { ConfigFile = "cfg", ProjectMode = "pastMade" }, new SavedStart[0], path => Config("pastMade"));
            Assert.Equal("ROSHN past job", model.ModeLine);
            Assert.False(RoadRow(model).SurfacesEnabled);
            model.Save();
            Assert.Equal("pastMade", model.Result.ProjectMode);
        }

        [Fact]
        public void R484_a_config_picked_in_the_pane_sets_no_mode_only_save_setup_writes_it()
        {
            var document = new DocumentSession("s", Path.Combine(Path.GetTempPath(), "x.rvt"), "x");
            var model = new PaneViewModel(new NoProbe()) { ReadText = path => Config("pastMade") };
            model.Load(document);
            model.PickConfig("cfg");
            Assert.Null(document.Settings.Project.ProjectMode);
            Assert.Equal(RoadPickWhy.PastJob, RoadPickState.Of(document.Settings.Project, model.ReadText).Why);
        }

        [Fact]
        public void R484_a_change_of_mode_reads_as_a_change_of_step_2_as_a_change_of_road_pick_does()
        {
            string config = Path.Combine(folder, "project.json");
            File.WriteAllText(config, Config("pastMade"));
            var settings = new PaneSettings(new ProjectSettings { ConfigFile = config });
            Assert.DoesNotContain(PaneRunState.InputsOf(settings, 2), input => input.Name == PaneRunState.RoadOrderInput);
            settings.Project.ProjectMode = DesignBasis.Rev6;
            Assert.Contains(PaneRunState.InputsOf(settings, 2), input => input.Name == PaneRunState.RoadOrderInput && input.Text == "Made Up Job__Road A");
            settings.Project.ProjectMode = "pastMade";
            Assert.DoesNotContain(PaneRunState.InputsOf(settings, 2), input => input.Name == PaneRunState.RoadOrderInput);
        }

        private sealed class NoProbe : IInputProbe
        {
            public InputFacts Look(string path, bool isFolder)
            {
                return new InputFacts();
            }

            public void Hash(string path, bool isFolder, Action<string> done)
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

            public string ConfigSheet(string configFile)
            {
                return null;
            }

            public SiteAdaptation.AddIn.Pane.SheetRead ReadSheet(string path)
            {
                return null;
            }

            public string VillaModeDefault(string configFile)
            {
                return null;
            }
        }
    }
}
#endif
