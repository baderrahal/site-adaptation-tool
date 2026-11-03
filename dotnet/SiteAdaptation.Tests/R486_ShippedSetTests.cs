using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R486, R487. Empty project takes the shipped set: copied once, an override list with no entries, the mode of a new project. Made up names on the shipped config
    public sealed class R486_ShippedSetTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "r486-" + Guid.NewGuid().ToString("N"));
        private readonly string shipped;
        private readonly string work;

        public R486_ShippedSetTests()
        {
            shipped = Path.Combine(root, "addin", ShippedSet.FolderName);
            work = Path.Combine(root, "work root");
            MakeShipped(shipped);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        // A made up set: its config names a made up rules file by the key profile, so no file name of a project stands in the code
        internal static void MakeShipped(string folder)
        {
            Directory.CreateDirectory(Path.Combine(folder, "config"));
            Directory.CreateDirectory(Path.Combine(folder, "rules made up"));
            File.WriteAllText(Path.Combine(folder, "config", "project.json"), new JObject
            {
                ["profile"] = "rules made up/made-up.json",
                ["majority"] = JObject.Parse("{ 'groups': { 'native': ['n1'], 'parsons': ['kx'] }, 'overrideDefaultSources': ['parsons'] }"),
                ["addin"] = JObject.Parse("{ 'parameterSheet': 'config/made_up_sheet.xlsx' }"),
                ["designBasis"] = new JObject { ["mode"] = "pastMade", ["modes"] = new JArray(DesignBasis.Rev6, "pastMade"), ["newProjectMode"] = DesignBasis.Rev6 },
            }.ToString());
            File.WriteAllText(Path.Combine(folder, "config", "floor_outlines.json"), "{}");
            File.WriteAllText(Path.Combine(folder, "config", "made_up_sheet.xlsx"), "made up");
            File.WriteAllText(Path.Combine(folder, "rules made up", "made-up.json"), "{ 'levelProfiles': { 'default': 'madeProfile', 'madeProfile': { 'job': 'parsons' } } }");
        }

        private ShippedSet Set(string name = "Site A", bool saved = true)
        {
            return new ShippedSet(shipped, work, name, saved);
        }

        private string ProjectFile(params string[] parts)
        {
            return Path.Combine(new[] { work, ShippedSet.ProjectsFolder, "Site A" }.Concat(parts).ToArray());
        }

        [Fact]
        public void R486_preview_reads_the_copy_when_there_is_one_and_the_shipped_files_before_it()
        {
            string before = Set().Preview(File.ReadAllText).ParameterSheet;
            Assert.StartsWith(shipped, before);
            Assert.EndsWith("made_up_sheet.xlsx", before);
            Set().Take(File.ReadAllText);
            string copy = ProjectFile("config", "project.json");
            JObject config = JObject.Parse(File.ReadAllText(copy));
            config["addin"]["parameterSheet"] = "config/edited.xlsx";
            config["designBasis"]["newProjectMode"] = "pastMade";
            File.WriteAllText(copy, config.ToString());
            File.WriteAllText(ProjectFile("rules made up", "made-up.json"), "{ \"levelProfiles\": { \"default\": \"editedProfile\" } }");
            ProjectSet preview = Set().Preview(File.ReadAllText), taken = Set().Take(File.ReadAllText);
            Assert.Equal(taken.ParameterSheet, preview.ParameterSheet);
            Assert.EndsWith("edited.xlsx", preview.ParameterSheet);
            Assert.Equal("editedProfile", preview.LevelProfile);
            Assert.Equal(taken.ConfigFile, preview.ConfigFile);
            Assert.Equal(taken.RulesFile, preview.RulesFile);
            Assert.Equal("pastMade", Set().NewMode(File.ReadAllText));
        }

        [Theory]
        [InlineData("{ \"levelProfiles\": [] }")]
        [InlineData("{ \"levelProfiles\": 5 }")]
        [InlineData("{ \"levelProfiles\": { \"default\": 5 } }")]
        public void R486_preview_gives_null_for_files_that_do_not_read_where_take_would_throw(string rules)
        {
            File.WriteAllText(Path.Combine(shipped, "rules made up", "made-up.json"), rules);
            Assert.Null(Set().Preview(File.ReadAllText));
        }

        [Fact]
        public void R486_a_sheet_name_that_is_no_text_gives_null_from_preview()
        {
            string path = Path.Combine(shipped, "config", "project.json");
            JObject config = JObject.Parse(File.ReadAllText(path));
            config["addin"]["parameterSheet"] = 5;
            File.WriteAllText(path, config.ToString());
            Assert.Null(Set().Preview(File.ReadAllText));
        }

        [Fact]
        public void R486_the_set_is_copied_to_the_folder_of_the_project_and_names_config_rules_and_level_profile()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            Assert.Equal(ProjectFile("config", "project.json"), set.ConfigFile);
            Assert.Equal(Path.GetFullPath(ProjectFile("rules made up", "made-up.json")), set.RulesFile);
            Assert.Equal("madeProfile", set.LevelProfile);
            Assert.True(File.Exists(ProjectFile("config", "floor_outlines.json")));
            Assert.Empty(Directory.GetDirectories(Path.Combine(work, ShippedSet.ProjectsFolder)).Where(path => path.Contains(".tmp-")));
        }

        [Fact]
        public void R486_a_second_take_keeps_a_hand_edit_of_the_config_and_what_save_overrides_wrote()
        {
            ProjectSet first = Set().Take(File.ReadAllText);
            JObject edited = JObject.Parse(File.ReadAllText(first.ConfigFile));
            edited["handEdit"] = "stays";
            File.WriteAllText(first.ConfigFile, edited.ToString());
            string list = ProjectFile("config", "overrides", "made-up.json");
            File.WriteAllText(list, "{ \"overrides\": [ { \"plot\": \"3\" } ] }");

            Set().Take(File.ReadAllText);
            Assert.Equal("stays", (string)JObject.Parse(File.ReadAllText(first.ConfigFile))["handEdit"]);
            Assert.Contains("\"plot\": \"3\"", File.ReadAllText(list));
        }

        [Fact]
        public void R487_the_copy_holds_a_list_with_no_entries_at_the_path_of_check_run_that_the_build_accepts()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            JObject config = ProjectModes.Apply(JObject.Parse(File.ReadAllText(set.ConfigFile)), DesignBasis.Rev6);
            string list = Common.CheckTable.CheckRun.OverridesPath(config, set.ConfigFile, set.RulesFile);
            Assert.Equal(ProjectFile("config", "overrides", "made-up.json"), list);
            Assert.Empty((JArray)JObject.Parse(File.ReadAllText(list))["overrides"]);
            JObject level = (JObject)JObject.Parse(File.ReadAllText(set.RulesFile))["levelProfiles"]["madeProfile"];
            OverrideList loaded = PlanOverrides.Load(list, config, level, "madeProfile", "project.json");
            Assert.NotNull(loaded);
        }

        [Fact]
        public void R487_a_list_with_entries_of_another_source_is_still_refused()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            JObject config = JObject.Parse(File.ReadAllText(set.ConfigFile));
            config["majority"]["groups"]["native"] = new JArray("n1");
            string list = ProjectFile("config", "overrides", "made-up.json");
            File.WriteAllText(list, new JObject { ["overrides"] = new JArray(JObject.Parse("{ 'plot': '5', 'field': 'mirrored', 'value': true, 'source': 'native', 'model': 'a.rvt', 'key': 'n1' }")) }.ToString());
            JObject level = (JObject)JObject.Parse(File.ReadAllText(set.RulesFile))["levelProfiles"]["madeProfile"];
            Assert.Contains("holds no override of parsons", Assert.Throws<InvalidDataException>(() => PlanOverrides.Load(list, config, level, "madeProfile", "project.json")).Message);
        }

        [Fact]
        public void R486_with_no_shipped_set_it_is_not_there()
        {
            Assert.False(new ShippedSet(Path.Combine(root, "none"), work, "Site A", true).There);
            Assert.False(new ShippedSet(null, work, "Site A", true).There);
            Assert.True(Set().There);
            Assert.Equal(DesignBasis.Rev6, Set().NewMode(File.ReadAllText));
        }

        [Theory]
        [InlineData("Site A", true)]
        [InlineData("Site.A", true)]
        [InlineData("", false)]
        [InlineData("  ", false)]
        [InlineData("a/b", false)]
        [InlineData("a\\b", false)]
        [InlineData("..", false)]
        [InlineData("name.", false)]
        [InlineData(" name", false)]
        [InlineData("a:b", false)]
        [InlineData("CON", false)]
        [InlineData("nul.txt", false)]
        public void R486_a_project_name_makes_a_safe_folder_name_or_is_refused(string name, bool safe)
        {
            Assert.Equal(safe, ShippedSet.SafeName(name));
            Assert.Equal(safe ? ShippedSetRefusal.None : ShippedSetRefusal.NoSafeName, Set(name).Refusal);
        }

        [Fact]
        public void R486_an_unsaved_model_is_refused_and_a_saved_one_names_its_folder()
        {
            Assert.Equal(ShippedSetRefusal.NotSaved, Set(null, false).Refusal);
            Assert.Equal(ProjectFile(), Set("Site A").ProjectFolder);
        }

        // Breaker 2, 1. The list Take writes, in the shape majority.overrides_text writes, through OverrideEdits itself

        private static JObject Rev6Config(ProjectSet set)
        {
            return ProjectModes.Apply(JObject.Parse(File.ReadAllText(set.ConfigFile)), DesignBasis.Rev6);
        }

        private static JObject Level(ProjectSet set)
        {
            return (JObject)JObject.Parse(File.ReadAllText(set.RulesFile))["levelProfiles"]["madeProfile"];
        }

        [Fact]
        public void R487_the_list_of_take_has_the_two_line_form_of_majority_overrides_text()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            string text = File.ReadAllText(ProjectFile("config", "overrides", "made-up.json"));
            Assert.Equal("{\n  \"note\": " + Newtonsoft.Json.JsonConvert.ToString(JObject.Parse(text)["note"].ToString()) + ",\n  \"overrides\": [\n  ]\n}\n", text);
            Assert.NotNull(set);
        }

        [Fact]
        public void R487_save_overrides_writes_skip_on_a_rule_row_and_past_on_a_key_row_into_the_list_of_take_and_the_build_reads_them()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            string list = ProjectFile("config", "overrides", "made-up.json");
            var rows = new List<Common.CheckTable.CheckRow>
            {
                new Common.CheckTable.CheckRow { Index = 0, Plot = "3", Check = "Team check", From = "team", OpenUse = Common.CheckTable.CheckUse.Rule, PastWhy = "the team marked it" },
                new Common.CheckTable.CheckRow { Index = 1, Plot = "2", Check = "Hand", Field = "mirrored", From = "missing", OpenUse = Common.CheckTable.CheckUse.Rule, PastForm = "trueOrFalse", PastValue = true, PastOffered = true, Key = "kx", Model = "kx-site.rvt" },
            };
            var uses = new[] { new Common.CheckTable.CheckChoice(0, Common.CheckTable.CheckUse.Skip), new Common.CheckTable.CheckChoice(1, Common.CheckTable.CheckUse.Past) };
            Assert.Equal(2, Common.CheckTable.OverrideEdits.Save(list, rows, uses, Rev6Config(set), "R4-1"));
            OverrideList read = PlanOverrides.Load(list, Rev6Config(set), Level(set), "madeProfile", "project.json");
            Assert.Equal("3", read.Skip.Keys.Single());
            Assert.Equal(true, read.Of("2")["mirrored"].Value<bool>());
        }

        // Breaker 2, 3 and 4. What a list holds, the applied mode and the same words as plan_rules.load_overrides

        private string ListOf(string text)
        {
            string path = Path.Combine(root, "list.json");
            File.WriteAllText(path, text);
            return path;
        }

        private static JObject Modes(string mode)
        {
            return JObject.Parse("{ 'majority': { 'groups': { 'native': ['n1'], 'parsons': ['kx'] }, 'overrideDefaultSources': ['parsons'] }, 'designBasis': { 'mode': '" + mode + "' } }");
        }

        private OverrideList Load(string text, string mode)
        {
            return PlanOverrides.Load(ListOf(text), Modes(mode), JObject.Parse("{ 'job': 'parsons' }"), "p", "project.json");
        }

        private const string NoSource = "the override list holds no override of parsons among the sources parsons (R280, R330)";

        [Fact]
        public void R487_an_empty_real_list_passes_under_rev6_and_under_the_past_job_is_refused_with_the_old_line()
        {
            Assert.Empty(Load("{ \"overrides\": [] }", DesignBasis.Rev6).Skip);
            Assert.Equal(NoSource, Assert.Throws<InvalidDataException>(() => Load("{ \"overrides\": [] }", "pastMade")).Message);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{ \"note\": \"x\" }")]
        [InlineData("{ \"overrides\": null }")]
        [InlineData("{ \"overrides\": {} }")]
        public void R487_a_file_with_no_real_list_is_refused_under_both_modes_as_no_override(string text)
        {
            Assert.Equal(NoSource, Assert.Throws<InvalidDataException>(() => Load(text, DesignBasis.Rev6)).Message);
            Assert.Equal(NoSource, Assert.Throws<InvalidDataException>(() => Load(text, "pastMade")).Message);
        }

        [Theory]
        [InlineData("{ \"overrides\": \"\" }")]
        [InlineData("{ \"overrides\": 0 }")]
        [InlineData("{ \"overrides\": false }")]
        public void R487_overrides_that_is_no_list_is_refused_under_both_modes_by_the_line_of_python(string text)
        {
            string line = Path.Combine(root, "list.json") + ": overrides of the override list is no list of entries (R330)";
            Assert.Equal(line, Assert.Throws<InvalidDataException>(() => Load(text, DesignBasis.Rev6)).Message);
            Assert.Equal(line, Assert.Throws<InvalidDataException>(() => Load(text, "pastMade")).Message);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("false")]
        public void R487_a_file_that_is_no_object_is_refused_under_both_modes_by_the_line_of_python(string text)
        {
            string line = "the input overrides is no JSON object: " + Path.Combine(root, "list.json");
            Assert.Equal(line, Assert.Throws<InvalidDataException>(() => Load(text, DesignBasis.Rev6)).Message);
            Assert.Equal(line, Assert.Throws<InvalidDataException>(() => Load(text, "pastMade")).Message);
        }

        private const string NativeEntry = "{ \"overrides\": [ {\"plot\": \"5\", \"field\": \"mirrored\", \"value\": true, \"source\": \"native\", \"model\": \"n1.rvt\", \"key\": \"n1\"} ] }";

        [Fact]
        public void R487_entries_of_another_source_do_not_lock_a_rev6_project_and_apply_nothing_and_the_past_job_still_refuses()
        {
            OverrideList read = Load(NativeEntry, DesignBasis.Rev6);
            Assert.Empty(read.Of("5"));
            Assert.Equal(NoSource, Assert.Throws<InvalidDataException>(() => Load(NativeEntry, "pastMade")).Message);
        }

        [Fact]
        public void R487_a_first_past_on_a_plot_only_a_native_key_holds_leaves_the_list_of_a_new_project_loading()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            string list = ProjectFile("config", "overrides", "made-up.json");
            var rows = new List<Common.CheckTable.CheckRow>
            {
                new Common.CheckTable.CheckRow { Index = 0, Plot = "2", Check = "Hand", Field = "mirrored", From = "missing", OpenUse = Common.CheckTable.CheckUse.Rule, PastForm = "trueOrFalse", PastValue = true, PastOffered = true, Key = "n1", Model = "n1-site.rvt" },
            };
            Common.CheckTable.OverrideEdits.Save(list, rows, new[] { new Common.CheckTable.CheckChoice(0, Common.CheckTable.CheckUse.Past) }, Rev6Config(set), "R4-1");
            Assert.Contains("\"source\": \"native\"", File.ReadAllText(list));
            Assert.NotNull(PlanOverrides.Load(list, Rev6Config(set), Level(set), "madeProfile", "project.json"));
        }

        // Breaker 2, 5. The copied sheet is the input of the row

        [Fact]
        public void R486_the_save_that_takes_the_set_names_the_copied_sheet_as_the_row_and_button_1_read_it()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            ProjectSettings saved = SetupSave.Apply(new SetupDraft { NewSet = set }, new ProjectSettings(), File.ReadAllText);
            string path = Common.SetUp.ParameterSheet.PathOf(saved.ConfigFile, JObject.Parse(File.ReadAllText(saved.ConfigFile)));
            Assert.Equal(path, saved.Inputs[InputNames.ParameterSheet].Path);
            Assert.True(File.Exists(path));
            SetupRowState row = SetupCheck.Run(saved, (name, folder) => File.Exists(name)).Rows.Single(item => item.Row.Id == SetupRows.ParameterSheet);
            Assert.Equal(SetupState.Found, row.State);
        }

        [Fact]
        public void R486_a_sheet_the_user_picked_stays_on_the_save_that_takes_the_set()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            var draft = new SetupDraft { NewSet = set };
            draft.Picks[InputNames.ParameterSheet] = "picked.xlsx";
            Assert.Equal("picked.xlsx", SetupSave.Apply(draft, new ProjectSettings(), File.ReadAllText).Inputs[InputNames.ParameterSheet].Path);

            var held = new ProjectSettings();
            held.Inputs[InputNames.ParameterSheet] = new SettingsInput { Path = "held.xlsx" };
            Assert.Equal("held.xlsx", SetupSave.Apply(new SetupDraft { NewSet = set }, held, File.ReadAllText).Inputs[InputNames.ParameterSheet].Path);
        }

        [Fact]
        public void R486_a_folder_that_holds_files_but_no_settings_stops_and_nothing_is_written()
        {
            Directory.CreateDirectory(ProjectFile());
            File.WriteAllText(ProjectFile("left.txt"), "left");
            Assert.Throws<IOException>(() => Set().Take(File.ReadAllText));
            Assert.Equal(new[] { ProjectFile("left.txt") }, Directory.GetFiles(ProjectFile(), "*", SearchOption.AllDirectories));
            Assert.Equal(new[] { ProjectFile() }, Directory.GetDirectories(Path.GetDirectoryName(ProjectFile())));
        }

        // Save setup

        [Fact]
        public void R486_R484_save_on_empty_with_no_config_takes_the_set_and_the_new_project_mode()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            ProjectSettings saved = SetupSave.Apply(new SetupDraft { NewSet = set }, new ProjectSettings(), File.ReadAllText);
            Assert.Equal(set.ConfigFile, saved.ConfigFile);
            Assert.Equal(set.RulesFile, saved.RulesFile);
            Assert.Equal("madeProfile", saved.LevelProfile);
            Assert.Equal(DesignBasis.Rev6, saved.ProjectMode);
        }

        [Fact]
        public void R486_settings_that_name_a_config_do_not_take_the_set()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            string other = Path.Combine(root, "other.json");
            File.WriteAllText(other, new JObject { ["designBasis"] = new JObject { ["mode"] = "pastMade", ["modes"] = new JArray(DesignBasis.Rev6, "pastMade"), ["newProjectMode"] = DesignBasis.Rev6 } }.ToString());
            ProjectSettings saved = SetupSave.Apply(new SetupDraft { NewSet = set }, new ProjectSettings { ConfigFile = other, LevelProfile = "own" }, File.ReadAllText);
            Assert.Equal(other, saved.ConfigFile);
            Assert.Equal("own", saved.LevelProfile);
            Assert.Null(saved.ProjectMode);
        }

        // Item 5: a project on rev6 by Empty goes back through Start from and does not get rev6 again

        [Fact]
        public void R486_R484_a_project_on_rev6_by_empty_then_a_saved_project_with_no_mode_ends_on_its_config_mode_and_a_second_save_keeps_it()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            ProjectSettings rev6 = SetupSave.Apply(new SetupDraft { NewSet = set }, new ProjectSettings(), File.ReadAllText);
            Assert.Equal(DesignBasis.Rev6, rev6.ProjectMode);

            string past = Path.Combine(root, "past.json");
            File.WriteAllText(past, new JObject { ["designBasis"] = new JObject { ["mode"] = "pastMade", ["modes"] = new JArray(DesignBasis.Rev6, "pastMade"), ["newProjectMode"] = DesignBasis.Rev6 } }.ToString());
            string start = PreSetting.Export(new ProjectSettings { ConfigFile = past });

            ProjectSettings back = SetupSave.Apply(new SetupDraft { StartText = start }, rev6, File.ReadAllText);
            Assert.Equal(past, back.ConfigFile);
            Assert.Equal("pastMade", back.ProjectMode);

            ProjectSettings again = SetupSave.Apply(new SetupDraft(), back, File.ReadAllText);
            Assert.Equal(past, again.ConfigFile);
            Assert.Equal("pastMade", again.ProjectMode);
        }
    }
}

#if NET48
namespace SiteAdaptation.Tests
{
    using SiteAdaptation.AddIn.NewProject;
    using SiteAdaptation.AddIn.Pane;

    // R486, the window, net48 alone as the add-in is
    public sealed class R486_ShippedSetWindowTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "r486w-" + Guid.NewGuid().ToString("N"));
        private readonly string shipped;
        private readonly string work;

        public R486_ShippedSetWindowTests()
        {
            shipped = Path.Combine(root, "addin", ShippedSet.FolderName);
            work = Path.Combine(root, "work");
            R486_ShippedSetTests.MakeShipped(shipped);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private NewProjectViewModel Window(ProjectSettings settings, ShippedSet set, IReadOnlyList<SavedStart> starts = null)
        {
            return new NewProjectViewModel(settings, "Site", starts ?? new SavedStart[0], null, (path, isFolder) => File.Exists(path), null, path => File.Exists(path) ? File.ReadAllText(path) : null, null, true, (inFolder, effective) => null, set);
        }

        private ShippedSet Set(string name = "Site A", bool saved = true)
        {
            return new ShippedSet(shipped, work, name, saved);
        }

        private string ProjectFolder => Path.Combine(work, ShippedSet.ProjectsFolder, "Site A");

        private static SetupRowViewModel RoadRow(NewProjectViewModel model)
        {
            return model.Rows.Single(item => item.Row.Id == SetupRows.RoadLevels);
        }

        [Fact]
        public void R486_empty_with_a_shipped_set_shows_the_rev6_line_no_stop_of_a_saved_project_and_copies_nothing_before_save()
        {
            NewProjectViewModel model = Window(new ProjectSettings(), Set());
            Assert.Equal("New project rules, Rev 06", model.ModeLine);
            Assert.NotEqual(NewProjectText.NoStart, model.StopLine);
            Assert.False(Directory.Exists(ProjectFolder));
        }

        [Fact]
        public void R486_save_takes_the_set_and_the_road_pick_is_active_once_save_has_it()
        {
            NewProjectViewModel model = Window(new ProjectSettings(), Set());
            Assert.True(RoadRow(model).SurfacesEnabled);
            model.Save();
            Assert.Equal(DesignBasis.Rev6, model.Result.ProjectMode);
            Assert.StartsWith(ProjectFolder, model.Result.ConfigFile);
            Assert.Equal(RoadPickWhy.Ready, RoadPickState.Of(model.Result, File.ReadAllText).Why);

            // Reopened on the settings that Save gave: the same set, no second copy, the line by the mode the project holds
            File.WriteAllText(Path.Combine(ProjectFolder, "mark.txt"), "stays");
            NewProjectViewModel again = Window(model.Result, Set());
            Assert.Equal("New project rules, Rev 06", again.ModeLine);
            Assert.True(RoadRow(again).SurfacesEnabled);
            again.Save();
            Assert.Equal(model.Result.ConfigFile, again.Result.ConfigFile);
            Assert.True(File.Exists(Path.Combine(ProjectFolder, "mark.txt")));
        }

        [Fact]
        public void R486_no_shipped_set_on_empty_gives_the_stop_and_no_mode_line()
        {
            NewProjectViewModel model = Window(new ProjectSettings(), new ShippedSet(Path.Combine(root, "none"), work, "Site A", true));
            Assert.Equal(string.Empty, model.ModeLine);
            Assert.Equal(NewProjectText.NoStart, model.StopLine);
            Assert.True(model.StopShown);
        }

        [Theory]
        [InlineData("a:b", true, NewProjectText.SetNoName)]
        [InlineData("Site A", false, NewProjectText.SetNotSaved)]
        public void R486_a_name_with_no_safe_folder_or_an_unsaved_model_gets_one_line_and_nothing_is_written(string name, bool saved, string line)
        {
            NewProjectViewModel model = Window(new ProjectSettings(), Set(name, saved));
            Assert.True(model.StopShown);
            Assert.Equal(line, model.StopLine);
            Assert.Equal(string.Empty, model.TodoLine);
            Assert.Equal(string.Empty, model.ModeLine);
            model.Save();
            Assert.Null(model.Result);
            Assert.False(Directory.Exists(work));
        }

        // Breaker 2, 2. Two models of one name in two folders, the folder of the project by the path as the work folder is

        [Fact]
        public void R486_two_models_of_one_name_in_two_folders_hold_their_own_set_and_an_override_does_not_cross()
        {
            string one = WorkFolder.For(work, Path.Combine(root, "one", "Site.rvt")), two = WorkFolder.For(work, Path.Combine(root, "two", "Site.rvt"));
            ProjectSet first = NewProjectDialog.SetFor(shipped, work, one).Take(File.ReadAllText);
            ProjectSet second = NewProjectDialog.SetFor(shipped, work, two).Take(File.ReadAllText);
            Assert.NotEqual(first.ConfigFile, second.ConfigFile);
            Assert.Equal(Path.Combine(work, ShippedSet.ProjectsFolder, Path.GetFileName(one), "config", "project.json"), first.ConfigFile);

            string listOne = Path.Combine(Path.GetDirectoryName(first.ConfigFile), "overrides", "made-up.json");
            string listTwo = Path.Combine(Path.GetDirectoryName(second.ConfigFile), "overrides", "made-up.json");
            File.WriteAllText(listOne, "{ \"overrides\": [ {\"plot\": \"3\"} ] }");
            Assert.DoesNotContain("\"plot\"", File.ReadAllText(listTwo));

            // The same model at a second Save setup keeps its own copy
            ProjectSet again = NewProjectDialog.SetFor(shipped, work, one).Take(File.ReadAllText);
            Assert.Equal(first.ConfigFile, again.ConfigFile);
            Assert.Contains("\"plot\"", File.ReadAllText(listOne));
        }

        [Fact]
        public void R486_a_model_with_no_work_folder_is_a_model_not_saved()
        {
            Assert.Equal(ShippedSetRefusal.NotSaved, NewProjectDialog.SetFor(shipped, work, null).Refusal);
        }

        [Fact]
        public void R486_the_sheet_row_reads_found_before_the_save_and_after_it()
        {
            NewProjectViewModel model = Window(new ProjectSettings(), Set());
            Assert.Equal(SetupState.Found, model.Rows.Single(row => row.Row.Id == SetupRows.ParameterSheet).State);
            Assert.False(Directory.Exists(ProjectFolder));
            model.Save();
            Assert.Equal(SetupState.Found, SetupCheck.Run(model.Result, (path, folder) => File.Exists(path)).Rows.Single(item => item.Row.Id == SetupRows.ParameterSheet).State);
        }

        // The shipped config of the made up set with a grading file listed as no road, and a road folder of three files
        private string RoadFolderWithNotRoad()
        {
            string path = Path.Combine(shipped, "config", "project.json");
            JObject config = JObject.Parse(File.ReadAllText(path));
            config["designBasis"]["roadLevel"] = new JObject { ["notRoad"] = new JObject { ["grading made up"] = "a grading file" } };
            File.WriteAllText(path, config.ToString());
            string folder = Path.Combine(root, "roads");
            Directory.CreateDirectory(folder);
            foreach (string stem in new[] { "area__road one", "area__road two", "grading made up" })
            {
                File.WriteAllText(Path.Combine(folder, stem + ".xml"), "<x/>");
            }
            return folder;
        }

        private static ProjectSettings WithRoadFolder(string folder)
        {
            var settings = new ProjectSettings();
            settings.Inputs[InputNames.RoadSurface] = new SettingsInput { Path = folder };
            return settings;
        }

        [Fact]
        public void R486_R485_R492_before_the_save_the_road_pick_works_off_the_shipped_config_and_leaves_out_a_not_road_name()
        {
            string folder = RoadFolderWithNotRoad();
            IReadOnlyList<string> offered = null;
            var model = new NewProjectViewModel(WithRoadFolder(folder), "Site", new SavedStart[0], null, (path, isFolder) => File.Exists(path), null,
                path => File.Exists(path) ? File.ReadAllText(path) : null, null, true, (inFolder, effective) => { offered = inFolder; return new[] { "area__road two" }; }, Set());
            SetupRowViewModel row = RoadRow(model);
            Assert.True(row.SurfacesEnabled);
            model.PickSurfaces();
            Assert.Equal(new[] { "area__road one", "area__road two" }, offered);
            Assert.False(Directory.Exists(work));
        }

        [Fact]
        public void R486_before_the_save_the_sheet_row_reads_found_with_the_shipped_sheet_no_stop_and_four_of_four()
        {
            // Every other file is there, so the sheet row alone decides the count and the stop
            var settings = new ProjectSettings();
            foreach (string name in InputNames.All.Where(item => item != InputNames.ParameterSheet))
            {
                settings.Inputs[name] = new SettingsInput { Path = Path.Combine(root, name) };
            }
            var model = new NewProjectViewModel(settings, "Site", new SavedStart[0], null, (path, isFolder) => true, null, path => File.Exists(path) ? File.ReadAllText(path) : null, null, true, null, Set());
            SetupRowViewModel sheet = model.Rows.Single(row => row.Row.Id == SetupRows.ParameterSheet);
            Assert.Equal(SetupState.Found, sheet.State);
            Assert.Equal("made_up_sheet.xlsx", sheet.FileName);
            Assert.False(model.StopShown);
            Assert.Equal(string.Empty, model.StopLine);
            Assert.Equal("4 of 4", model.Ready);
            Assert.False(Directory.Exists(work));
        }

        [Fact]
        public void R486_a_pick_made_before_the_save_is_kept_and_the_saved_config_reads_ready()
        {
            string folder = RoadFolderWithNotRoad();
            var model = new NewProjectViewModel(WithRoadFolder(folder), "Site", new SavedStart[0], null, (path, isFolder) => File.Exists(path), null,
                path => File.Exists(path) ? File.ReadAllText(path) : null, null, true, (inFolder, effective) => new[] { "area__road two" }, Set());
            model.PickSurfaces();
            model.Save();
            Assert.Equal(new[] { "area__road two" }, model.Result.RoadSurfaces);
            Assert.StartsWith(ProjectFolder, model.Result.ConfigFile);
            RoadPickState after = RoadPickState.Of(model.Result, File.ReadAllText);
            Assert.Equal(RoadPickWhy.Ready, after.Why);
            Assert.Equal(new[] { "area__road two" }, after.Effective);
            NewProjectViewModel again = Window(model.Result, Set());
            Assert.True(RoadRow(again).SurfacesEnabled);
        }

        [Fact]
        public void R486_with_no_set_a_refused_name_or_a_past_job_the_road_pick_stays_grey()
        {
            Assert.False(RoadRow(Window(new ProjectSettings(), new ShippedSet(Path.Combine(root, "none"), work, "Site A", true))).SurfacesEnabled);
            Assert.False(RoadRow(Window(new ProjectSettings(), Set("a:b"))).SurfacesEnabled);
            string past = Path.Combine(root, "past.json");
            File.WriteAllText(past, new JObject { ["designBasis"] = new JObject { ["mode"] = "pastMade", ["modes"] = new JArray(DesignBasis.Rev6, "pastMade") } }.ToString());
            NewProjectViewModel pastWindow = Window(new ProjectSettings { ConfigFile = past, ProjectMode = "pastMade" }, Set());
            Assert.False(RoadRow(pastWindow).SurfacesEnabled);
            Assert.Equal(RoadSurfacesText.PastJob, RoadRow(pastWindow).SurfacesTip);
        }

        [Fact]
        public void R486_a_sheet_picked_in_the_window_before_the_save_still_wins_over_the_shipped_one()
        {
            string other = Path.Combine(root, "other sheet.xlsx");
            File.WriteAllText(other, "x");
            var picked = new NewProjectViewModel(new ProjectSettings(), "Site", new SavedStart[0], null, (path, isFolder) => File.Exists(path), new FixedPicker(other),
                path => File.Exists(path) ? File.ReadAllText(path) : null, null, true, null, Set());
            picked.Browse(picked.Rows.Single(row => row.Row.Id == SetupRows.ParameterSheet));
            picked.Save();
            Assert.Equal(other, picked.Result.Inputs[InputNames.ParameterSheet].Path);
        }

        private sealed class FixedPicker : IPathPicker
        {
            private readonly string path;

            public FixedPicker(string path)
            {
                this.path = path;
            }

            public string File(string title) { return path; }
            public string Folder() { return null; }
            public string Save(string title) { return null; }
        }

        [Fact]
        public void R486_a_copy_that_fails_gets_one_line_and_the_window_stays()
        {
            Directory.CreateDirectory(ProjectFolder);
            File.WriteAllText(Path.Combine(ProjectFolder, "left.txt"), "left");
            NewProjectViewModel model = Window(new ProjectSettings(), Set());
            model.Save();
            Assert.Null(model.Result);
            Assert.Equal("The project files were not copied.", model.StopLine);
            Assert.Equal("Move or delete the folder of this project under New projects in the work folder, then press Save setup again.", model.TodoLine);
            Assert.DoesNotContain(ProjectFolder, model.StopLine + model.TodoLine);
            Assert.DoesNotContain("R486", model.StopLine + model.TodoLine);
        }

        [Fact]
        public void R486_a_shipped_config_that_does_not_read_opens_the_window_with_a_stop_not_all_found()
        {
            string path = Path.Combine(shipped, "rules made up", "made-up.json");
            File.WriteAllText(path, "{ 'levelProfiles': [] }".Replace("'", "\""));
            NewProjectViewModel model = Window(new ProjectSettings(), Set());
            Assert.True(model.StopShown);
            Assert.Equal("The shipped project files could not be read.", model.StopLine);
            Assert.Equal("Reinstall the add-in, then open New project again.", model.TodoLine);
            Assert.False(model.CheckedOk);
            model.CheckFiles();
            Assert.False(model.CheckedOk);
        }

        [Fact]
        public void R486_a_sheet_name_that_is_no_text_gives_a_stop_and_no_throw()
        {
            string path = Path.Combine(shipped, "config", "project.json");
            JObject config = JObject.Parse(File.ReadAllText(path));
            config["addin"]["parameterSheet"] = 5;
            File.WriteAllText(path, config.ToString());
            NewProjectViewModel model = Window(new ProjectSettings(), Set());
            Assert.True(model.StopShown);
            Assert.Equal("The shipped project files could not be read.", model.StopLine);
        }

        [Fact]
        public void R486_settings_that_name_a_config_are_untouched_with_no_copy()
        {
            string past = Path.Combine(root, "past.json");
            File.WriteAllText(past, new JObject { ["designBasis"] = new JObject { ["mode"] = "pastMade", ["modes"] = new JArray(DesignBasis.Rev6, "pastMade"), ["newProjectMode"] = DesignBasis.Rev6 } }.ToString());
            NewProjectViewModel model = Window(new ProjectSettings { ConfigFile = past }, Set());
            Assert.Equal("ROSHN past job", model.ModeLine);
            model.Save();
            Assert.Equal(past, model.Result.ConfigFile);
            Assert.Null(model.Result.ProjectMode);
            Assert.False(Directory.Exists(work));
        }

        [Fact]
        public void R486_a_saved_project_chosen_with_a_set_shipped_copies_nothing()
        {
            string past = Path.Combine(root, "past.json");
            File.WriteAllText(past, new JObject { ["designBasis"] = new JObject { ["mode"] = "pastMade", ["modes"] = new JArray(DesignBasis.Rev6, "pastMade") } }.ToString());
            var starts = new[] { new SavedStart("Past", "p.json", PreSetting.Export(new ProjectSettings { ConfigFile = past })) };
            NewProjectViewModel model = Window(new ProjectSettings(), Set(), starts);
            model.Start = model.Starts[1];
            Assert.Equal("ROSHN past job", model.ModeLine);
            model.Save();
            Assert.Equal("pastMade", model.Result.ProjectMode);
            Assert.False(Directory.Exists(work));
        }
    }
}
#endif
