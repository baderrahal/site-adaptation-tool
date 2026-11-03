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

        [Fact]
        public void R484_empty_project_gets_rev6_on_a_past_job_config()
        {
            ProjectSettings saved = SetupSave.Apply(new SetupDraft(), new ProjectSettings { ConfigFile = "cfg" }, Reader(Made("pastMade")));
            Assert.Equal(DesignBasis.Rev6, saved.ProjectMode);
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
        public void R484_save_setup_on_empty_leaves_an_old_project_that_ran_on_the_mode_of_config()
        {
            var job = new ProjectSettings { ConfigFile = "cfg" };
            job.LastRuns["2"] = new ButtonRun { Time = "2026-11-01T00:00:00Z" };
            Assert.Null(SetupSave.Apply(new SetupDraft(), job, Reader(Made("pastMade"))).ProjectMode);
        }

        [Fact]
        public void R484_a_new_project_mode_outside_the_modes_falls_back_on_the_mode_of_config_and_never_stops_the_window()
        {
            Assert.Null(ProjectModes.NewProject(Made("pastMade", "manual7")));
            Assert.Null(SetupSave.Apply(new SetupDraft(), new ProjectSettings { ConfigFile = "cfg" }, Reader(Made("pastMade", "manual7"))).ProjectMode);
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
        public void R484_start_from_empty_shows_the_rev6_line_and_the_pick_active_once_a_config_is_known()
        {
            NewProjectViewModel model = Window(new ProjectSettings { ConfigFile = "cfg" }, new SavedStart[0], path => Config("pastMade"));
            Assert.Equal("New project rules, Rev 06", model.ModeLine);
            Assert.True(RoadRow(model).SurfacesEnabled);
            model.Save();
            Assert.Equal(DesignBasis.Rev6, model.Result.ProjectMode);
        }

        [Fact]
        public void R484_start_from_empty_in_a_window_with_empty_settings_shows_the_rev6_line_and_waits_for_a_config()
        {
            NewProjectViewModel model = Window(new ProjectSettings(), new SavedStart[0], path => Config("pastMade"));
            Assert.Equal("New project rules, Rev 06", model.ModeLine);
            Assert.False(RoadRow(model).SurfacesEnabled);
            model.Save();
            Assert.Null(model.Result.ProjectMode);
        }

        [Fact]
        public void R484_start_from_changes_the_line_a_saved_project_with_none_reads_the_past_job_of_its_config()
        {
            string saved = PreSetting.Export(new ProjectSettings { ConfigFile = "cfg" });
            NewProjectViewModel model = Window(new ProjectSettings(), new[] { new SavedStart("Past", "p.json", saved) }, path => Config("pastMade"));
            Assert.Equal("New project rules, Rev 06", model.ModeLine);
            model.Start = model.Starts[1];
            Assert.Equal("ROSHN past job", model.ModeLine);
            Assert.False(RoadRow(model).SurfacesEnabled);
            model.Save();
            Assert.Equal("pastMade", model.Result.ProjectMode);
            model.Start = model.Starts[0];
            Assert.Equal("New project rules, Rev 06", model.ModeLine);
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
        public void R484_the_first_config_picked_in_the_pane_gives_a_new_project_its_mode_and_the_pick()
        {
            var document = new DocumentSession("s", Path.Combine(Path.GetTempPath(), "x.rvt"), "x");
            var model = new PaneViewModel(new NoProbe()) { ReadText = path => Config("pastMade") };
            model.Load(document);
            model.PickConfig("cfg");
            Assert.Equal(DesignBasis.Rev6, document.Settings.Project.ProjectMode);
            Assert.Equal(RoadPickWhy.Ready, RoadPickState.Of(document.Settings.Project, model.ReadText).Why);
        }

        [Fact]
        public void R484_a_project_that_ran_keeps_the_mode_of_config_when_a_config_is_picked_in_the_pane()
        {
            var project = new ProjectSettings { ConfigFile = "cfg" };
            project.LastRuns["2"] = new ButtonRun { Time = "2026-11-01T00:00:00Z" };
            ProjectModes.TakeNewProject(project, path => Config("pastMade"));
            Assert.Null(project.ProjectMode);
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

            public string VillaModeDefault(string configFile)
            {
                return null;
            }
        }
    }
}
#endif
