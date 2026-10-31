using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.HowItWorks;
using SiteAdaptation.AddIn.NewProject;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Scan;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Lane C NEWPROJ-UI. The view models of New project and How it works with no window and no Revit: what each button does, that Check files
    // and Cancel change nothing, that Save setup gives the settings of R432, the read block from the latest read, the scanner hook
    public class NewProjectTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_newproj_" + Guid.NewGuid().ToString("N"));

        public NewProjectTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        [Fact]
        public void NEWPROJ_how_it_works_lists_the_steps_of_the_ribbon_and_open_new_project_ends_it_with_the_flag()
        {
            Assert.All(new[] { HowItWorksText.What, HowItWorksText.Needs, HowItWorksText.Gives }, column => Assert.Equal(StepPlan.Count, column.Length));
            var model = new HowItWorksViewModel();
            Assert.Equal(RibbonLayout.Buttons.Select(button => button.Label), model.Rows.Select(row => row.Button));
            Assert.Equal(Enumerable.Range(1, StepPlan.Count), model.Rows.Select(row => row.Number));
            Assert.Equal("9 steps", model.Count);
            Assert.Equal("9 buttons in the pane", model.CountLine);
            int ended = 0;
            model.Finished += () => ended++;

            model.Close();
            Assert.False(model.OpenNewProject);
            model.OpenNew();
            Assert.True(model.OpenNewProject);
            Assert.Equal(2, ended);

            // With no document open Open New project is grey and does nothing
            var none = new HowItWorksViewModel(false);
            none.OpenNew();
            Assert.False(none.CanOpenNew);
            Assert.False(none.OpenNewProject);
        }

        [Fact]
        public void NEWPROJ_the_window_shows_the_rows_their_states_the_stop_box_and_the_saved_starts()
        {
            NewProjectViewModel model = Model(Settings(), new Picker());

            Assert.Equal(new[] { "Empty project", "Job A (saved)" }, model.Starts.Select(start => start.Name));
            Assert.Equal("Job A is kept as a saved start, not built in.", model.SavedHint);
            Assert.Equal(new[] { "Site drawing (CAD)", "Plot list (Excel)", "Villa levels (Excel)", "Road levels (civil file)", "Parameter sheet (Excel)" }, model.Rows.Select(row => row.File));
            Assert.Equal(new[] { "Found", "Found", "Comes later", "Missing", "Missing" }, model.Rows.Select(row => row.StatusText));
            Assert.Equal(new[] { "base.dwg", "sales.xlsx", "Not chosen", "Not chosen", "Not chosen" }, model.Rows.Select(row => row.FileName));
            Assert.Equal("2 of 4", model.Ready);
            Assert.True(model.StopShown);
            Assert.Equal("The parameter sheet is missing, so button 1 cannot add the shared parameters.", model.StopLine);
            Assert.Equal("What to do: ask the BIM team for the sheet, or press Get template and fill it.", model.TodoLine);
            Assert.False(model.Rows[2].CanBrowse);
            Assert.Equal(new[] { false, true, false, false, true }, model.Rows.Select(row => row.HasTemplate));
            Assert.Equal("337 plots on layer MP_PLOT_LINE", model.PlotsFound);
            Assert.Equal("14 types", model.VillaTypes);
            Assert.Equal("Comes with the drawing scan", model.SurveyPoint);
            Assert.False(model.CanChange);
        }

        [Fact]
        public void NEWPROJ_browse_and_check_files_change_nothing_until_save_setup_and_cancel_gives_nothing()
        {
            ProjectSettings settings = Settings();
            string before = SettingsJson.Write(settings);
            string sheet = Path.Combine(folder, "parameters.xlsx");
            File.WriteAllText(sheet, "x");
            var picker = new Picker();
            picker.Files.Enqueue(sheet);
            NewProjectViewModel model = Model(settings, picker);
            int ended = 0;
            model.Finished += () => ended++;

            model.Browse(model.Rows[4]);
            Assert.Equal(new[] { "Parameter sheet (Excel)" }, picker.Titles);
            Assert.Equal("Found", model.Rows[4].StatusText);
            Assert.Equal("parameters.xlsx", model.Rows[4].FileName);
            Assert.False(model.StopShown);
            model.CheckFiles();
            Assert.True(model.CheckedOk);
            Assert.Equal(before, SettingsJson.Write(settings));

            model.Browse(model.Rows[2]);
            Assert.Single(picker.Titles);

            model.Cancel();
            Assert.Null(model.Result);
            Assert.Equal(1, ended);
            Assert.Equal(before, SettingsJson.Write(settings));
        }

        [Fact]
        public void NEWPROJ_save_setup_gives_the_settings_of_the_start_and_the_picks_in_the_settings_json()
        {
            ProjectSettings settings = Settings();
            settings.ConfigFile = null;
            string roads = Path.Combine(folder, "roads");
            Directory.CreateDirectory(roads);
            var picker = new Picker();
            picker.Folders.Enqueue(roads);
            NewProjectViewModel model = Model(settings, picker);
            Assert.Equal("No saved project is chosen, so no button has its rules.", model.StopLine);
            ProjectSettings gone = Settings();
            gone.ConfigFile = Path.Combine(folder, "gone.json");
            Assert.Equal("The saved project names settings this computer does not hold, so no button has its rules.", Model(gone, new Picker()).StopLine);

            model.Start = model.Starts[1];
            model.Browse(model.Rows[3]);
            Assert.Equal(new[] { "folder" }, picker.Titles);
            model.Save();

            Assert.Equal(@"Y:\job\project.json", model.Result.ConfigFile);
            Assert.Equal(@"Y:\job\rules.json", model.Result.RulesFile);
            Assert.Equal(roads, model.Result.Inputs[InputNames.RoadSurface].Path);
            Assert.Equal(Path.Combine(folder, "base.dwg"), model.Result.Inputs[InputNames.Baseplan].Path);
            Assert.Null(settings.ConfigFile);
            Assert.Equal(SettingsJson.Write(model.Result), SettingsJson.Write(SettingsJson.Read(SettingsJson.Write(model.Result))));
        }

        [Fact]
        public void NEWPROJ_get_template_writes_the_workbook_where_the_user_names_and_never_inside_the_input_folder()
        {
            string inputs = Path.Combine(folder, "inputs");
            Directory.CreateDirectory(inputs);
            string config = Path.Combine(folder, "project.json");
            File.WriteAllText(config, new JObject { ["inputRoot"] = inputs }.ToString());
            ProjectSettings settings = Settings();
            settings.ConfigFile = config;
            var picker = new Picker();
            picker.Saves.Enqueue(Path.Combine(folder, "sheet"));
            picker.Saves.Enqueue(Path.Combine(inputs, "sheet.xlsx"));
            NewProjectViewModel model = Model(settings, picker);

            model.GetTemplate(model.Rows[4]);
            Assert.Equal("Template saved: sheet.xlsx", model.TemplateLine);
            Assert.True(File.Exists(Path.Combine(folder, "sheet.xlsx")));
            Assert.Equal(new[] { "Get template" }, picker.Titles);

            model.GetTemplate(model.Rows[4]);
            Assert.Equal("The template was not written: it would lie in the input folder, which the tool never writes", model.TemplateLine);

            // Review of NEWPROJ-UI: a name typed with no extension never replaces the file of that name with the extension
            picker.Saves.Enqueue(Path.Combine(folder, "sheet"));
            File.WriteAllText(Path.Combine(folder, "sheet.xlsx"), "filled");
            model.GetTemplate(model.Rows[4]);
            Assert.Equal("The template was not written: a file of that name is there, pick another name", model.TemplateLine);
            Assert.Equal("filled", File.ReadAllText(Path.Combine(folder, "sheet.xlsx")));
            Assert.False(File.Exists(Path.Combine(inputs, "sheet.xlsx")));

            model.GetTemplate(model.Rows[1]);
            Assert.StartsWith("The template was not written: config holds no addin.workbookRoles", model.TemplateLine);
            model.GetTemplate(model.Rows[0]);
            Assert.Equal(3, picker.Titles.Count);

            // Review of NEWPROJ-UI: with no config there is no input folder to hold the path against, so nothing is written
            ProjectSettings bare = Settings();
            bare.ConfigFile = null;
            var second = new Picker();
            second.Saves.Enqueue(Path.Combine(folder, "bare.xlsx"));
            NewProjectViewModel empty = Model(bare, second);
            empty.GetTemplate(empty.Rows[4]);
            Assert.Equal("The template was not written: pick a saved project first, it names the input folder the tool never writes", empty.TemplateLine);
            Assert.False(File.Exists(Path.Combine(folder, "bare.xlsx")));
        }

        [Fact]
        public void NEWPROJ_the_read_block_counts_the_unit_plots_and_villa_types_of_the_latest_read_and_names_the_plot_layers()
        {
            string work = Path.Combine(folder, "work");
            WriteRead(work, "read_a", "2-20261030T100000Z", new[] { "DP1", "DP1" });
            WriteRead(work, "read_b", "2-20261031T100000Z", new[] { "DP1", "DP2", "", null });
            string config = Path.Combine(folder, "project.json");
            File.WriteAllText(config, new JObject { ["addin"] = new JObject { ["readData"] = new JObject { ["cadLayers"] = "layers.json" } } }.ToString());
            File.WriteAllText(Path.Combine(folder, "layers.json"), new JObject { ["plotLineLayers"] = new JArray("MP_PLOT_LINE", "MP_PLOT_SUBD") }.ToString());

            DrawingFactsResult facts = new LastReadFacts().Read(config, work);

            // Review of NEWPROJ-UI: plot 100 met twice counts once, the row with no number gives no villa type
            Assert.Equal(4, facts.Plots);
            Assert.Equal(3, facts.VillaTypes);
            Assert.Equal("MP_PLOT_LINE, MP_PLOT_SUBD", facts.PlotLayer);
            Assert.Null(facts.Layers);
            Assert.Null(facts.SurveyPoint);
            DrawingFactsResult none = new LastReadFacts().Read(null, Path.Combine(folder, "none"));
            Assert.Null(none.Plots);
            Assert.Null(none.PlotLayer);
            // R440: the dialog reads the latest scan of the work folder first and the latest read when there is none
            Assert.IsType<LatestScanFacts>(NewProjectDialog.Facts);
            Assert.Equal(4, new LatestScanFacts().Read(config, work).Plots);
            string scan = Path.Combine(work, "scan_2-20261031T110000Z");
            Directory.CreateDirectory(scan);
            File.WriteAllText(Path.Combine(scan, "scan.json"), R435_ScanViewTests.Scan(true, 0).ToString());
            DrawingFactsResult scanned = new LatestScanFacts().Read(config, work);
            Assert.Equal(5, scanned.Plots);
            Assert.Equal("OUTLINE", scanned.PlotLayer);
            Assert.Equal(Path.Combine(scan, "scan.json"), scanned.ScanPath);
        }

        [Fact]
        public void NEWPROJ_the_rules_row_name_is_the_project_the_rules_file_of_the_repo_names()
        {
            string rules = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "rules", "roshn-makkah.json"));
            Assert.Equal((string)JObject.Parse(rules)["project"], RulesProfiles.Name(rules));
            Assert.Null(RulesProfiles.Name("{\"project\": \"  \"}"));
            Assert.Null(RulesProfiles.Name("{}"));
        }

        private void WriteRead(string work, string name, string runId, string[] units)
        {
            string read = Path.Combine(work, name);
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, ReadDataRun.RecordFile), new JObject { ["format"] = ReadDataRun.RecordFormat, ["runId"] = runId }.ToString());
            var plots = new JArray(units.Select((unit, at) => new JObject { ["officialNumber"] = (100 + at).ToString(), ["nbhId"] = "012", ["typologyRev9"] = unit }));
            plots.Add(new JObject { ["officialNumber"] = null, ["nbhId"] = "012", ["typologyRev9"] = "ROAD" });
            plots.Add(new JObject { ["officialNumber"] = "100", ["nbhId"] = "012", ["typologyRev9"] = "DP3" });
            File.WriteAllText(Path.Combine(read, ReadDataRun.PlotTableFile), new JObject { ["plots"] = plots }.ToString());
        }

        [Fact]
        public void R440_the_layer_sheet_and_column_dropdowns_come_from_the_scan_and_save_setup_keeps_the_picks()
        {
            var scan = R435_ScanViewTests.Scan(false, 1);
            DrawingFactsResult facts = new DrawingFactsResult { ScanPath = "scan.json", Scan = ScanFacts.Of(scan.ToString()), Plots = 5, PlotLayer = "OUTLINE", VillaTypes = 2 };
            ProjectSettings shownWith = null;
            var model = new NewProjectViewModel(Settings(), "Site", new SavedStart[0], facts, NewProjectDialog.There, new Picker(), path => null, (path, settings) =>
            {
                shownWith = settings;
                ProjectSettings next = SettingsJson.Read(SettingsJson.Write(settings));
                next.ConflictPicks = new SortedDictionary<string, string> { ["1|villa type"] = "feet.dwg|block|BLOCKS" };
                return next;
            });
            Assert.True(model.HasLayers);
            Assert.Equal(0, model.LayerIndex);
            Assert.True(model.HasSheets);
            Assert.Equal(new[] { "Type" }, model.ColumnChoices);
            Assert.False(model.HasSurvey);
            Assert.Equal(new[] { "The scan reads no survey point yet" }, model.SurveyChoices);
            Assert.True(model.CanView);

            model.LayerIndex = 1;
            model.ViewScan();
            Assert.Equal("site.dwg|OUTLINE + EDGE", shownWith.ScanPicks["plotOutline"]);
            model.Save();
            Assert.Equal("site.dwg|OUTLINE + EDGE", model.Result.ScanPicks["plotOutline"]);
            Assert.False(model.Result.ScanPicks.ContainsKey("villaType"));
            Assert.Equal("feet.dwg|block|BLOCKS", model.Result.ConflictPicks["1|villa type"]);
        }

        [Fact]
        public void R440_with_no_scan_the_dropdowns_hide_and_view_stays_grey()
        {
            NewProjectViewModel model = Model(Settings(), new Picker());
            Assert.False(model.HasLayers);
            Assert.False(model.HasSheets);
            Assert.False(model.CanView);
            Assert.Equal(new[] { "Comes with the drawing scan" }, model.SurveyChoices);
        }

        private NewProjectViewModel Model(ProjectSettings settings, Picker picker)
        {
            var start = new ProjectSettings { ConfigFile = @"Y:\job\project.json", RulesFile = @"Y:\job\rules.json" };
            var starts = new[] { new SavedStart("Job A", Path.Combine(folder, "Job A.json"), PreSetting.Export(start)) };
            var facts = new DrawingFactsResult { Plots = 337, PlotLayer = "MP_PLOT_LINE", VillaTypes = 14 };
            return new NewProjectViewModel(settings, "Site", starts, facts, NewProjectDialog.There, picker, path => File.Exists(path) ? File.ReadAllText(path) : null);
        }

        private ProjectSettings Settings()
        {
            File.WriteAllText(Path.Combine(folder, "base.dwg"), "x");
            File.WriteAllText(Path.Combine(folder, "sales.xlsx"), "x");
            string config = Path.Combine(folder, "project.json");
            if (!File.Exists(config))
            {
                File.WriteAllText(config, "{}");
            }
            var settings = new ProjectSettings { ConfigFile = config };
            settings.Inputs[InputNames.Baseplan] = new SettingsInput { Path = Path.Combine(folder, "base.dwg") };
            settings.Inputs[InputNames.Sales] = new SettingsInput { Path = Path.Combine(folder, "sales.xlsx") };
            return settings;
        }

        private sealed class Picker : IPathPicker
        {
            public Queue<string> Files { get; } = new Queue<string>();
            public Queue<string> Folders { get; } = new Queue<string>();
            public Queue<string> Saves { get; } = new Queue<string>();
            public List<string> Titles { get; } = new List<string>();

            public string File(string title)
            {
                Titles.Add(title);
                return Files.Count == 0 ? null : Files.Dequeue();
            }

            public string Folder()
            {
                Titles.Add("folder");
                return Folders.Count == 0 ? null : Folders.Dequeue();
            }

            public string Save(string title)
            {
                Titles.Add(title);
                return Saves.Count == 0 ? null : Saves.Dequeue();
            }
        }
    }
}
