using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R430 to R432 and R434, docs/design/18-new-project.md. The rows of the New project window, Check files, Save setup and the saved
    // projects of Start from, with no Revit and no window
    public class R430_SetupTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r420_" + Guid.NewGuid().ToString("N"));

        public R430_SetupTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        [Fact]
        public void R430_the_rows_follow_the_board_and_each_input_is_one_of_the_settings_json()
        {
            Assert.Equal(new[] { SetupRows.Site, SetupRows.PlotList, SetupRows.VillaLevels, SetupRows.RoadLevels, SetupRows.ParameterSheet }, SetupRows.All.Select(row => row.Id).ToArray());
            Assert.All(SetupRows.All.SelectMany(row => row.Inputs), name => Assert.Contains(name, InputNames.All));
            Assert.Equal(new[] { SetupRows.VillaLevels }, SetupRows.All.Where(row => row.Later).Select(row => row.Id).ToArray());
            Assert.Equal(new[] { SetupRows.RoadLevels }, SetupRows.All.Where(row => row.Optional).Select(row => row.Id).ToArray());
            Assert.Equal(new[] { InputNames.Sales, InputNames.ParameterSheet }, SetupRows.All.Where(row => row.Template != null).Select(row => row.Template).ToArray());
        }

        [Fact]
        public void R431_check_files_reads_the_disk_only_and_stops_on_a_missing_file_a_button_needs()
        {
            ProjectSettings settings = Settings();
            string before = SettingsJson.Write(settings);
            var asked = new List<string>();
            SetupCheckResult result = SetupCheck.Run(settings, (path, isFolder) =>
            {
                asked.Add(path + (isFolder ? "/" : ""));
                return path != @"X:\in\parameters.xlsx";
            });

            Assert.Equal(before, SettingsJson.Write(settings));
            Assert.Equal(new[] { @"X:\in\base.dwg", @"X:\in\sales.xlsx", @"X:\in\roads/", @"X:\in\parameters.xlsx", @"X:\job\project.json" }, asked.ToArray());
            Assert.Equal(new[] { SetupState.Found, SetupState.Found, SetupState.Later, SetupState.Found, SetupState.Missing }, result.Rows.Select(row => row.State).ToArray());
            Assert.Equal(3, result.Ready);
            Assert.Equal(4, result.Total);
            Assert.Equal(new[] { SetupRows.ParameterSheet }, result.Stops.Select(row => row.Row.Id).ToArray());
            Assert.True(result.HasStart);
            Assert.False(result.Ok);
        }

        [Fact]
        public void R431_a_missing_optional_file_shows_missing_and_does_not_stop()
        {
            ProjectSettings settings = Settings();
            settings.Inputs.Remove(InputNames.RoadSurface);
            SetupCheckResult result = SetupCheck.Run(settings, (path, isFolder) => true);

            Assert.Equal(SetupState.Missing, result.Rows.Single(row => row.Row.Id == SetupRows.RoadLevels).State);
            Assert.Null(result.Rows.Single(row => row.Row.Id == SetupRows.RoadLevels).Paths[0]);
            Assert.Empty(result.Stops);
            Assert.True(result.Ok);
        }

        [Fact]
        public void R431_settings_with_no_config_file_are_never_ok()
        {
            ProjectSettings settings = Settings();
            settings.ConfigFile = null;
            SetupCheckResult result = SetupCheck.Run(settings, (path, isFolder) => true);

            Assert.False(result.HasStart);
            Assert.False(result.StartNamed);
            Assert.False(result.Ok);
            Assert.Empty(result.Stops);
        }

        // Review of NEWPROJ-UI: a config file the settings name but the disk does not hold stops every button
        [Fact]
        public void R431_settings_whose_config_file_is_not_on_disk_are_never_ok()
        {
            SetupCheckResult result = SetupCheck.Run(Settings(), (path, isFolder) => path != @"X:\job\project.json");

            Assert.False(result.HasStart);
            Assert.True(result.StartNamed);
            Assert.False(result.Ok);
        }

        [Fact]
        public void R432_save_keeps_the_field_names_and_every_value_not_picked()
        {
            ProjectSettings current = Settings();
            current.Scope = new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "012" };
            current.DryRunFirst = false;
            current.Inputs[InputNames.Baseplan].Size = 10;
            current.Inputs[InputNames.Baseplan].Sha256 = new string('a', 64);
            current.Inputs[InputNames.Sales].Size = 20;
            current.LastRuns["2"] = new ButtonRun { Time = "2026-10-31T10:00:00Z", Mode = RunModes.Direct, Scope = new SettingsScope() };
            string before = SettingsJson.Write(current);
            var draft = new SetupDraft();
            draft.Picks[InputNames.Baseplan] = @"X:\in\base.dwg";
            draft.Picks[InputNames.Sales] = @"X:\in\sales2.xlsx";

            ProjectSettings saved = SetupSave.Apply(draft, current);

            Assert.Equal(before, SettingsJson.Write(current));
            Assert.Equal(Keys(SettingsJson.Write(current)), Keys(SettingsJson.Write(saved)));
            Assert.Equal(10, saved.Inputs[InputNames.Baseplan].Size);
            Assert.Equal(new string('a', 64), saved.Inputs[InputNames.Baseplan].Sha256);
            Assert.Equal(@"X:\in\sales2.xlsx", saved.Inputs[InputNames.Sales].Path);
            Assert.Null(saved.Inputs[InputNames.Sales].Size);
            Assert.Equal("012", saved.Scope.Nbh);
            Assert.False(saved.DryRunFirst);
            Assert.Equal("2026-10-31T10:00:00Z", saved.LastRuns["2"].Time);
            Assert.Equal(current.ConfigFile, saved.ConfigFile);
        }

        [Fact]
        public void R432_a_saved_project_lays_its_settings_and_keeps_the_drawings()
        {
            ProjectSettings current = Settings();
            var start = new ProjectSettings { ConfigFile = @"Y:\job\project.json", RulesFile = @"Y:\job\rules.json", LevelProfile = "profileB", VillaMode = VillaModes.Links };
            start.Inputs[InputNames.ParameterSheet] = new SettingsInput { Path = @"Y:\job\parameters.xlsx" };
            var draft = new SetupDraft { StartText = PreSetting.Export(start) };

            ProjectSettings saved = SetupSave.Apply(draft, current);

            Assert.Equal(@"Y:\job\project.json", saved.ConfigFile);
            Assert.Equal(@"Y:\job\rules.json", saved.RulesFile);
            Assert.Equal("profileB", saved.LevelProfile);
            Assert.Equal(VillaModes.Links, saved.VillaMode);
            Assert.Equal(@"Y:\job\parameters.xlsx", saved.Inputs[InputNames.ParameterSheet].Path);
            Assert.Equal(@"X:\in\base.dwg", saved.Inputs[InputNames.Baseplan].Path);
        }

        // Review of NEWPROJ-UI: the dry run tick of the project stays, and a parameter sheet the saved project does not name stays
        [Fact]
        public void R432_a_saved_project_keeps_the_dry_run_tick_and_a_parameter_sheet_it_does_not_name()
        {
            ProjectSettings current = Settings();
            current.DryRunFirst = false;
            var start = new ProjectSettings { ConfigFile = @"Y:\job\project.json", DryRunFirst = true };

            ProjectSettings saved = SetupSave.Apply(new SetupDraft { StartText = PreSetting.Export(start) }, current);

            Assert.False(saved.DryRunFirst);
            Assert.Equal(@"X:\in\parameters.xlsx", saved.Inputs[InputNames.ParameterSheet].Path);
            Assert.Equal(@"Y:\job\project.json", saved.ConfigFile);
        }

        [Fact]
        public void R432_save_refuses_an_input_the_settings_json_does_not_know()
        {
            var draft = new SetupDraft();
            draft.Picks["villaLevels"] = @"X:\in\levels.xlsx";

            Assert.Throws<ArgumentException>(() => SetupSave.Apply(draft, Settings()));
        }

        [Fact]
        public void R434_saved_projects_are_the_pre_settings_of_the_folder_by_name_and_a_refused_file_is_skipped()
        {
            string saved = SavedStarts.Folder(folder);
            Directory.CreateDirectory(saved);
            File.WriteAllText(Path.Combine(saved, "B job.json"), PreSetting.Export(new ProjectSettings { ConfigFile = @"Y:\b\project.json" }));
            File.WriteAllText(Path.Combine(saved, "A job.json"), PreSetting.Export(new ProjectSettings { ConfigFile = @"Y:\a\project.json" }));
            File.WriteAllText(Path.Combine(saved, "A project.json"), SettingsJson.Write(Settings()));
            File.WriteAllText(Path.Combine(saved, "notes.txt"), "not a start");
            var skipped = new List<string>();

            IReadOnlyList<SavedStart> starts = SavedStarts.List(saved, skipped);

            Assert.Equal(Path.Combine(folder, "Saved projects"), saved);
            Assert.Equal(new[] { "A job", "B job" }, starts.Select(start => start.Name).ToArray());
            Assert.Equal(@"Y:\a\project.json", SettingsJson.Read(starts[0].Text).ConfigFile);
            Assert.Equal(new[] { "A project.json" }, skipped.ToArray());
            Assert.Empty(SavedStarts.List(Path.Combine(folder, "none")));
        }

        private static ProjectSettings Settings()
        {
            var settings = new ProjectSettings { ConfigFile = @"X:\job\project.json", RulesFile = @"X:\job\rules.json" };
            settings.Inputs[InputNames.Baseplan] = new SettingsInput { Path = @"X:\in\base.dwg" };
            settings.Inputs[InputNames.Sales] = new SettingsInput { Path = @"X:\in\sales.xlsx" };
            settings.Inputs[InputNames.RoadSurface] = new SettingsInput { Path = @"X:\in\roads" };
            settings.Inputs[InputNames.ParameterSheet] = new SettingsInput { Path = @"X:\in\parameters.xlsx" };
            return settings;
        }

        private static string[] Keys(string json)
        {
            return JObject.Parse(json).Properties().Select(property => property.Name).ToArray();
        }
    }
}
