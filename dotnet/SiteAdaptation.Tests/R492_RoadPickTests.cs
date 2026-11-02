#if NET48
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.CheckTable;
using SiteAdaptation.AddIn.NewProject;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R492. Road surfaces picked in Set up: the window Road surfaces on its view model, the row Road levels of New project and the row Road
    // surface of the pane. Made up names, no click, no Revit
    public sealed class R492_RoadPickTests : IDisposable
    {
        private const string Config = "cfg";
        private readonly string folder = Path.Combine(Path.GetTempPath(), "r492-" + Guid.NewGuid().ToString("N"));

        public R492_RoadPickTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static string Text(bool rev6, params string[] listed)
        {
            return new JObject { ["designBasis"] = new JObject { ["mode"] = rev6 ? "rev6" : "past", ["roadLevel"] = new JObject { ["surfaces"] = new JArray(listed) } } }.ToString();
        }

        private static RoadSurfacesViewModel Window(IReadOnlyList<string> inFolder, IReadOnlyList<string> effective)
        {
            return new RoadSurfacesViewModel(inFolder, effective);
        }

        private static string[] Names(RoadSurfacesViewModel model)
        {
            return model.Rows.Select(row => row.Stem).ToArray();
        }

        private static readonly string[] Four = { "J1__Access", "J1__Collector", "J1__Service", "J1__Spine" };

        [Fact]
        public void R492_ticked_rows_come_first_in_their_order_and_the_rest_in_sorted_order()
        {
            RoadSurfacesViewModel model = Window(Four, new[] { "J1__Spine", "J1__Access" });
            Assert.Equal(new[] { "J1__Spine", "J1__Access", "J1__Collector", "J1__Service" }, Names(model));
            Assert.Equal(new[] { "J1__Spine", "J1__Access" }, model.Ticked);
        }

        [Fact]
        public void R492_a_tick_goes_to_the_end_of_the_ticked_rows_and_an_untick_back_to_its_sorted_place()
        {
            RoadSurfacesViewModel model = Window(Four, new[] { "J1__Spine", "J1__Access" });
            model.Rows.Single(row => row.Stem == "J1__Service").IsTicked = true;
            Assert.Equal(new[] { "J1__Spine", "J1__Access", "J1__Service", "J1__Collector" }, Names(model));
            model.Rows.Single(row => row.Stem == "J1__Spine").IsTicked = false;
            Assert.Equal(new[] { "J1__Access", "J1__Service", "J1__Collector", "J1__Spine" }, Names(model));
            Assert.Equal(new[] { "J1__Access", "J1__Service" }, model.Ticked);
        }

        [Fact]
        public void R492_up_and_down_move_the_selected_ticked_row_and_are_grey_at_the_ends()
        {
            RoadSurfacesViewModel model = Window(Four, new[] { "J1__Spine", "J1__Access", "J1__Collector" });
            Assert.False(model.CanMoveUp);
            Assert.True(model.CanMoveDown);
            model.MoveUp();
            Assert.Equal(new[] { "J1__Spine", "J1__Access", "J1__Collector", "J1__Service" }, Names(model));
            model.MoveDown();
            Assert.Equal(new[] { "J1__Access", "J1__Spine", "J1__Collector", "J1__Service" }, Names(model));
            Assert.True(model.CanMoveUp);
            model.MoveDown();
            Assert.Equal(new[] { "J1__Access", "J1__Collector", "J1__Spine", "J1__Service" }, Names(model));
            Assert.False(model.CanMoveDown);
            model.MoveDown();
            Assert.Equal(new[] { "J1__Access", "J1__Collector", "J1__Spine", "J1__Service" }, Names(model));
            Assert.Equal(new[] { "J1__Access", "J1__Collector", "J1__Spine" }, model.Ticked);
            model.Select(model.Rows[0]);
            Assert.False(model.CanMoveUp);
            model.MoveUp();
            Assert.Equal("J1__Access", Names(model)[0]);
        }

        [Fact]
        public void R492_an_unticked_row_does_not_move_and_its_buttons_are_grey()
        {
            RoadSurfacesViewModel model = Window(Four, new[] { "J1__Spine", "J1__Access" });
            model.Select(model.Rows.Single(row => row.Stem == "J1__Collector"));
            Assert.False(model.CanMoveUp);
            Assert.False(model.CanMoveDown);
            string[] before = Names(model);
            model.MoveUp();
            model.MoveDown();
            Assert.Equal(before, Names(model));
            model.Select(model.Rows.Single(row => row.Stem == "J1__Access"));
            Assert.True(model.CanMoveUp);
            Assert.False(model.CanMoveDown);
        }

        [Fact]
        public void R492_with_no_tick_use_is_refused_and_a_line_says_so()
        {
            RoadSurfacesViewModel model = Window(Four, new string[0]);
            int finished = 0;
            model.Finished += () => finished++;
            Assert.False(model.CanUse);
            Assert.True(model.NeedTickShown);
            model.Use();
            Assert.Equal(0, finished);
            Assert.Null(model.Result);
            model.Rows[0].IsTicked = true;
            Assert.False(model.NeedTickShown);
            model.Use();
            Assert.Equal(1, finished);
            Assert.Equal(new[] { "J1__Access" }, model.Result);
        }

        [Fact]
        public void R492_cancel_gives_no_result()
        {
            RoadSurfacesViewModel model = Window(Four, new[] { "J1__Spine" });
            model.Cancel();
            Assert.Null(model.Result);
        }

        [Fact]
        public void R492_a_saved_name_the_folder_no_longer_holds_is_shown_missing_and_unticked()
        {
            RoadSurfacesViewModel model = Window(Four, new[] { "J1__Spine", "J1__Gone" });
            RoadSurfaceRowViewModel gone = model.Rows.Single(row => row.Stem == "J1__Gone");
            Assert.True(gone.Missing);
            Assert.False(gone.IsTicked);
            Assert.False(gone.CanTick);
            gone.IsTicked = true;
            Assert.False(gone.IsTicked);
            Assert.Equal("Not in the folder any more: Gone. It stays unticked.", model.MissingLine);
            Assert.True(model.MissingShown);
            Assert.Equal(new[] { "J1__Spine" }, model.Ticked);
            Assert.Equal("J1__Gone", Names(model).Last());
        }

        [Fact]
        public void R492_a_folder_with_no_surface_files_says_so()
        {
            RoadSurfacesViewModel model = Window(new string[0], new string[0]);
            Assert.True(model.NoFiles);
            Assert.False(model.CanUse);
            Assert.False(Window(Four, new string[0]).NoFiles);
        }

        [Fact]
        public void R492_the_window_opens_on_the_effective_list_of_the_state()
        {
            string road = Path.Combine(folder, "roads");
            Directory.CreateDirectory(road);
            foreach (string name in Four)
            {
                File.WriteAllText(Path.Combine(road, name + ".xml"), "x");
            }
            var settings = new ProjectSettings { ConfigFile = Config };
            settings.Inputs[InputNames.RoadSurface] = new SettingsInput { Path = road };
            Func<string, string> read = path => Text(true, "J1__Spine", "J1__Collector");

            RoadPickState listed = RoadPickState.Of(settings, read);
            Assert.True(listed.Enabled);
            Assert.Equal(new[] { "J1__Spine", "J1__Collector" }, listed.Effective);
            Assert.Equal(Four, listed.InFolder());
            Assert.Equal(new[] { "J1__Spine", "J1__Collector", "J1__Access", "J1__Service" }, Names(Window(listed.InFolder(), listed.Effective)));

            settings.RoadSurfaces = new List<string> { "J1__Service", "J1__Access" };
            RoadPickState picked = RoadPickState.Of(settings, read);
            Assert.Equal(new[] { "J1__Service", "J1__Access" }, picked.Effective);
            Assert.Equal(new[] { "J1__Service", "J1__Access", "J1__Collector", "J1__Spine" }, Names(Window(picked.InFolder(), picked.Effective)));
        }

        [Fact]
        public void R492_the_short_name_is_the_part_after_the_double_underscore()
        {
            Assert.Equal("Spine", RoadPickState.Short("J1__Spine"));
            Assert.Equal("Spine", RoadPickState.Short("J1__Spine.xml"));
            Assert.Equal("Plain", RoadPickState.Short("Plain"));
        }

        [Fact]
        public void R492_under_the_past_job_the_button_is_grey_with_its_tip_in_New_project_and_the_pane()
        {
            var settings = new ProjectSettings { ConfigFile = Config };
            RoadPickState past = RoadPickState.Of(settings, path => Text(false, "J1__Spine"));
            Assert.Equal(RoadPickWhy.PastJob, past.Why);
            Assert.False(past.Enabled);

            NewProjectViewModel model = NewProject(settings, path => Text(false, "J1__Spine"), null);
            SetupRowViewModel row = model.Rows.Single(item => item.Row.Id == SetupRows.RoadLevels);
            Assert.True(row.HasSurfaces);
            Assert.False(row.SurfacesEnabled);
            Assert.Equal(RoadSurfacesText.PastJob, row.SurfacesTip);
            Assert.Equal(string.Empty, row.SurfacesLine);
            Assert.All(model.Rows.Where(item => item != row), item => Assert.False(item.HasSurfaces));

            PaneViewModel pane = Pane(settings, path => Text(false, "J1__Spine"), (a, b) => throw new InvalidOperationException("the window must not open"));
            FileRowViewModel paneRow = pane.InputRows.Single(item => item.HasSurfaces);
            Assert.False(paneRow.SurfacesEnabled);
            Assert.Equal(PaneText.SurfacesPastJob, paneRow.SurfacesTip);
            pane.PickSurfaces();
            Assert.Null(settings.RoadSurfaces);
        }

        [Fact]
        public void R492_with_no_saved_project_the_button_is_grey_and_says_to_pick_one()
        {
            NewProjectViewModel model = NewProject(new ProjectSettings(), path => null, null);
            SetupRowViewModel row = model.Rows.Single(item => item.Row.Id == SetupRows.RoadLevels);
            Assert.False(row.SurfacesEnabled);
            Assert.Equal(RoadSurfacesText.NoSetup, row.SurfacesTip);
        }

        [Fact]
        public void R492_save_setup_in_New_project_carries_the_order_and_the_line_names_it()
        {
            var settings = new ProjectSettings { ConfigFile = Config };
            IReadOnlyList<string> asked = null;
            NewProjectViewModel model = NewProject(settings, path => Text(true, "J1__Spine"), (inFolder, effective) =>
            {
                asked = effective;
                return new[] { "J1__Service", "J1__Access", "J1__Spine" };
            });
            SetupRowViewModel row = model.Rows.Single(item => item.Row.Id == SetupRows.RoadLevels);
            Assert.True(row.SurfacesEnabled);
            Assert.Equal("Road surfaces in order (1): Spine", row.SurfacesLine);

            model.PickSurfaces();
            Assert.Equal(new[] { "J1__Spine" }, asked.ToArray());
            Assert.Equal("Road surfaces in order (3): Service, Access, Spine", row.SurfacesLine);
            Assert.Null(settings.RoadSurfaces);

            model.Save();
            Assert.Equal(new[] { "J1__Service", "J1__Access", "J1__Spine" }, model.Result.RoadSurfaces);
            Assert.Null(settings.RoadSurfaces);
        }

        [Fact]
        public void R492_cancel_of_the_window_or_of_New_project_carries_nothing()
        {
            var settings = new ProjectSettings { ConfigFile = Config, RoadSurfaces = new List<string> { "J1__Access" } };
            NewProjectViewModel model = NewProject(settings, path => Text(true, "J1__Spine"), (inFolder, effective) => null);
            model.PickSurfaces();
            model.Save();
            Assert.Equal(new[] { "J1__Access" }, model.Result.RoadSurfaces);

            NewProjectViewModel other = NewProject(new ProjectSettings { ConfigFile = Config }, path => Text(true, "J1__Spine"), (inFolder, effective) => new[] { "J1__Service" });
            other.PickSurfaces();
            other.Cancel();
            Assert.Null(other.Result);
        }

        [Fact]
        public void R492_a_pick_in_the_pane_is_stored_and_a_change_of_folder_does_not_clear_it()
        {
            string road = Path.Combine(folder, "roads");
            Directory.CreateDirectory(road);
            File.WriteAllText(Path.Combine(road, "J1__Spine.xml"), "x");
            File.WriteAllText(Path.Combine(road, "J1__Access.xml"), "x");
            var settings = new ProjectSettings { ConfigFile = Config };
            settings.Inputs[InputNames.RoadSurface] = new SettingsInput { Path = road };
            IReadOnlyList<string> seen = null;
            PaneViewModel pane = Pane(settings, path => Text(true, "J1__Spine"), (inFolder, effective) =>
            {
                seen = inFolder;
                return new[] { "J1__Access", "J1__Spine" };
            });
            FileRowViewModel row = pane.InputRows.Single(item => item.HasSurfaces);
            Assert.True(row.SurfacesEnabled);
            Assert.Equal("Road surfaces in order (1): Spine", row.SurfacesLine);

            pane.PickSurfaces();
            Assert.Equal(Four.Where(name => name != "J1__Collector" && name != "J1__Service").ToArray(), seen.ToArray());
            Assert.Equal(new[] { "J1__Access", "J1__Spine" }, settings.RoadSurfaces);
            Assert.Equal("Road surfaces in order (2): Access, Spine", row.SurfacesLine);

            pane.Settings.Pick(InputKeys.RoadSurface, Path.Combine(folder, "other"));
            Assert.Equal(new[] { "J1__Access", "J1__Spine" }, pane.Settings.Project.RoadSurfaces);

            var cancelled = Pane(settings, path => Text(true, "J1__Spine"), (inFolder, effective) => null);
            cancelled.PickSurfaces();
            Assert.Equal(new[] { "J1__Access", "J1__Spine" }, settings.RoadSurfaces);
        }

        [Fact]
        public void R492_the_check_and_the_read_take_the_pick_from_the_pane_settings()
        {
            var settings = new PaneSettings { };
            settings.Project.RoadSurfaces = new List<string> { "J1__Access" };
            CheckRun run = CheckTableBody.DoorWired(new CheckRun(CheckTableBody.Roles, null, null, null, null, null, null, null), settings);
            Assert.Equal(new[] { "J1__Access" }, run.RoadPick());
            settings.Project.RoadSurfaces = null;
            Assert.Null(run.RoadPick());
            string read = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Commands", "ReadDataBody.cs"));
            Assert.Contains("RoadPick = () => session.Settings.Project.RoadSurfaces", read);
        }

        [Fact]
        public void R492_no_text_of_the_new_window_or_of_its_buttons_names_a_json_file()
        {
            var json = new Regex(@"\.json\b", RegexOptions.IgnoreCase);
            foreach (Type type in new[] { typeof(RoadSurfacesText), typeof(PaneText) })
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(item => item.IsLiteral && item.FieldType == typeof(string)))
                {
                    Assert.False(json.IsMatch((string)field.GetRawConstantValue()), type.Name + "." + field.Name);
                }
            }
            string xaml = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "NewProject", "RoadSurfacesWindow.xaml"));
            Assert.False(json.IsMatch(xaml));
        }

        private static NewProjectViewModel NewProject(ProjectSettings settings, Func<string, string> read, SurfacePicker picker)
        {
            return new NewProjectViewModel(settings, "Site", new SavedStart[0], null, (path, isFolder) => true, null, read, null, true, picker);
        }

        private static PaneViewModel Pane(ProjectSettings settings, Func<string, string> read, SurfacePicker picker)
        {
            var pane = new PaneViewModel(new NoProbe()) { ReadText = read, SurfacePicker = picker };
            pane.Load(new DocumentSession("s", Path.Combine(Path.GetTempPath(), "x.rvt"), "x"));
            pane.LoadSettings(new PaneSettings(settings));
            return pane;
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
