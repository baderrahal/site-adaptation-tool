using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R372. The pane of docs/design/12-ui-spec.md 7.2 and docs/design/06-settings.md on net48, its logic in plain classes: the view
    // model, the settings reader, the step lines of 5a, the sessions of the documents, the work folder and the routing of a worker
    // answer. No Revit type is touched
    public sealed class PaneTests
    {
        private const string Folder = @"C:\Job\";

        [Fact]
        public void R372_the_settings_reader_reads_every_field_of_06_settings_and_keeps_the_defaults_of_a_short_file()
        {
            PaneSettings full = SettingsFile.Read(@"{
                ""formatVersion"": 1, ""configFile"": ""C:\\Job\\project.json"", ""rulesFile"": ""C:\\Job\\rules.json"", ""levelProfile"": ""native2022"",
                ""inputs"": { ""baseplan"": { ""path"": ""C:\\Job\\a.dwg"", ""size"": 12, ""sha256"": ""0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"" }, ""prototypes"": { ""path"": ""C:\\Job\\p"" } },
                ""scope"": { ""kind"": ""plots"", ""plots"": ""2290-2299, 2311"" }, ""dryRunFirst"": false, ""villaMode"": ""links"" }");
            Assert.Equal(1, full.FormatVersion);
            Assert.Equal(@"C:\Job\project.json", full.ConfigFile);
            Assert.Equal(@"C:\Job\rules.json", full.RulesFile);
            Assert.Equal("native2022", full.LevelProfile);
            Assert.Equal(12L, full.Inputs[InputKeys.Baseplan].Size);
            Assert.Equal("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", full.Inputs[InputKeys.Baseplan].Sha256);
            Assert.Null(full.Inputs[InputKeys.Prototypes].Size);
            Assert.Equal(ScopeKinds.Plots, full.Scope.Kind);
            Assert.Equal("2290-2299, 2311", full.Scope.Plots);
            Assert.False(full.DryRunFirst);
            Assert.Equal(VillaModes.Links, full.VillaMode);
            Assert.True(full.Chosen);

            PaneSettings small = SettingsFile.Read("{\"formatVersion\": 1}");
            Assert.False(small.Chosen);
            Assert.True(small.DryRunFirst);
            // Item 6 of the 5c join: no villaMode in the settings leaves the default to config, which the pane reads (R404)
            Assert.Null(small.VillaMode);
            Assert.Equal(ScopeKinds.Project, small.Scope.Kind);
            Assert.Empty(small.Inputs);
            Assert.True(new PaneSettings().DryRunFirst);
            Assert.Null(new PaneSettings().VillaMode);
        }

        [Fact]
        public void R372_the_settings_reader_refuses_a_later_version_with_both_named_and_a_value_it_does_not_know()
        {
            // J1: the reader of R367 since 5c, its words
            Assert.Equal("the settings JSON is formatVersion 2, this add-in reads formatVersion 1 and earlier", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{\"formatVersion\": 2}")).Message);
            Assert.Contains("no whole formatVersion", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{}")).Message);
            Assert.Contains("villaMode boxes", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{\"formatVersion\": 1, \"villaMode\": \"boxes\"}")).Message);
            Assert.Contains("an input roads", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{\"formatVersion\": 1, \"inputs\": {\"roads\": {}}}")).Message);
            Assert.Contains("scope zone", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{\"formatVersion\": 1, \"scope\": {\"kind\": \"zone\"}}")).Message);
            Assert.Contains("no JSON object", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("not json")).Message);
        }

        // Lane C NEWPROJ-UI, docs/design/boards/PaneFirst.dc.html: the box sets up the project in New project, the Setup row says Not done,
        // the inputs are filled from the setup and the steps after 4 stand in one row
        [Fact]
        public void R372_with_no_settings_file_the_pane_shows_the_first_open_state()
        {
            PaneViewModel model = Model(new FakeProbe());
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));

            Assert.True(model.FirstOpen);
            Assert.False(model.InUse);
            Assert.Equal(new[] { "Start here", "Set up this project", PaneText.StartLine, "Open New project" }, new[] { model.BoxLabel, model.BoxName, model.BoxLine, model.BoxButton });
            Assert.True(model.BoxButtonShown);
            Assert.True(model.BoxCommand.CanExecute(null));
            Assert.Equal("Setup", model.SettingsRow.Label);
            Assert.Equal("Not done", model.SettingsRow.FileName);
            Assert.True(model.SettingsRow.Shown);
            Assert.False(model.SettingsRow.Found);
            Assert.Null(model.SettingsRow.ToolTip);
            Assert.Equal("Filled from your setup. You can change any of them after.", model.InputHelp);
            Assert.Equal(new[] { "Needs setup", "Needs step 1", "Needs step 2", "Needs step 3", "Needs step 4", "Needs step 5", "Needs step 6", "Needs step 7", "Needs step 4" }, model.Steps.Select(step => step.ChipText));
            Assert.All(model.Steps, step => Assert.Equal(MarkKind.Waiting, step.Mark));
            Assert.Equal(new[] { "Set up site", "Read data", "Review data", "Check", "Floors and ramps to 9 Report" }, model.ShownSteps.Select(step => step.Name));
            Assert.Equal("Needs step 4", model.ShownSteps.Last().ChipText);
            Assert.Equal("Or start from a saved project:", model.StartSavedLine);
            model.SetSavedStarts(new[] { "Job A", "Job B" });
            Assert.Equal("Or start from a saved project such as Job A:", model.StartSavedLine);
        }

        // Bader, 28 Oct 2026 (docs/design/13-workflow-changes.md, Pane): the row Shared parameters for the parameter sheet Excel,
        // after Prototypes, a file of a settings key of its own, counted in the help line
        [Fact]
        public void R372_input_files_gain_the_shared_parameters_row_after_prototypes_for_the_parameter_sheet_file()
        {
            Assert.Equal(new[] { "Baseplan", "Villa blocks", "Curbs", "Road surface", "DLUS and sales", "Plot numbers", "Prototypes", "Shared parameters" },
                Model(new FakeProbe()).InputRows.Select(row => row.Label));
            var probe = new FakeProbe();
            probe.Add(Folder + "parameters.xlsx", 4096, "fedcba98" + new string('0', 56));
            var picker = new FakePicker(Folder + "parameters.xlsx");
            var model = new PaneViewModel(probe, picker);
            var session = new DocumentSession("s", Folder + "Site.rvt", "Site");
            model.Load(session);
            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json" });
            FileRowViewModel row = model.InputRows.Last();
            Assert.False(row.Folder);
            Assert.Equal(new[] { InputKeys.ParameterSheet }, row.Keys);
            Assert.Equal("The drawings and schedules the tool reads. 0 of 8 found.", model.InputHelp);

            model.Browse(row);
            Assert.Equal(new string[] { null }, picker.Titles);
            Assert.Equal(Folder + "parameters.xlsx", session.Settings.InputPath(InputKeys.ParameterSheet));
            Assert.Equal("parameters.xlsx", row.FileName);
            Assert.True(row.Found);
            Assert.Equal("The drawings and schedules the tool reads. 1 of 8 found.", model.InputHelp);

            Assert.Equal("parameterSheet", InputKeys.ParameterSheet);
            Assert.Contains(InputKeys.ParameterSheet, InputKeys.All);
            Assert.DoesNotContain(InputKeys.ParameterSheet, InputKeys.Folders);
            Assert.Equal(@"C:\Job\p.xlsx", SettingsFile.Read(@"{ ""formatVersion"": 1, ""inputs"": { ""parameterSheet"": { ""path"": ""C:\\Job\\p.xlsx"" } } }").InputPath(InputKeys.ParameterSheet));
        }

        // Bader, 28 Oct 2026: Import from past job and Export of the row Pre-setting, and Import pre-setting of the first open box,
        // which runs Import from past job. W4 of steps/lane-b.md joins them to R367 at 5c: Export writes the pre-setting of the
        // document to a file the user names, Import reads one into the settings in memory, a refusal is told and changes nothing
        [Fact]
        public void R367_import_from_past_job_and_export_carry_the_pre_setting_through_a_file_and_wait_for_a_document()
        {
            string folder = Path.Combine(Path.GetTempPath(), "pane-preset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string file = Path.Combine(folder, "past job.json");
                var picker = new FakePicker(file, file, null, file);
                var model = new PaneViewModel(new FakeProbe(), picker);
                var told = new List<string>();
                model.Told += told.Add;
                Assert.False(model.ImportCommand.CanExecute(null));
                Assert.False(model.ExportCommand.CanExecute(null));

                model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
                model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json", RulesFile = Folder + "rules.json", LevelProfile = "native2022" });
                model.PickInput(InputKeys.Baseplan, Folder + "baseplan.dwg");
                model.ExportCommand.Execute(null);
                Assert.DoesNotContain("baseplan", File.ReadAllText(file));

                model.Load(new DocumentSession("t", Folder + "New.rvt", "New"));
                Assert.True(model.FirstOpen);
                model.ImportCommand.Execute(null);
                Assert.False(model.FirstOpen);
                Assert.Equal(Folder + "rules.json", model.Settings.RulesFile);
                Assert.Equal("native2022", model.Settings.LevelProfile);
                Assert.Null(model.Settings.InputPath(InputKeys.Baseplan));

                // A Cancel changes nothing, a file R367 refuses is told and changes nothing
                model.ExportCommand.Execute(null);
                File.WriteAllText(file, "not json");
                model.Load(new DocumentSession("u", Folder + "Other.rvt", "Other"));
                model.ImportCommand.Execute(null);
                Assert.Single(told);
                Assert.Contains("no JSON object", told[0]);
                Assert.True(model.FirstOpen);
                Assert.Equal(new[] { PaneText.Export, PaneText.UseSaved, PaneText.Export, PaneText.UseSaved }, picker.Titles);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        // Lane C NEWPROJ-UI: the box on first open, Edit and Open of the Setup row and New project of the row Help each ask the Revit side
        // for New project, How it works asks for its page with no document too. No settings file is picked by its path in the pane
        [Fact]
        public void NEWPROJ_the_box_the_setup_row_and_the_help_row_ask_for_new_project_and_how_it_works()
        {
            var probe = new FakeProbe();
            probe.Add(Folder + "project.json", 2048, "a1b2c3d4" + new string('0', 56));
            var picker = new FakePicker(Folder + "project.json");
            PaneViewModel model = new PaneViewModel(probe, picker);
            int asked = 0;
            int how = 0;
            model.NewProjectRequested += () => asked++;
            model.HowItWorksRequested += () => how++;
            Assert.False(model.NewProjectCommand.CanExecute(null));
            Assert.True(model.HowItWorksCommand.CanExecute(null));
            model.HowItWorksCommand.Execute(null);
            Assert.Equal(1, how);

            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.BoxCommand.Execute(null);
            model.Browse(model.SettingsRow);
            model.NewProjectCommand.Execute(null);
            Assert.Equal(3, asked);
            Assert.Empty(picker.Titles);
            Assert.True(model.FirstOpen);

            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json" });
            Assert.False(model.FirstOpen);
            Assert.Equal("Saved for this project", model.SettingsRow.FileName);
            Assert.True(model.SettingsRow.Found);
            Assert.Null(model.SettingsRow.ToolTip);
            Assert.Equal("Run 1 Set up site", model.BoxButton);
            Assert.Equal(9, model.ShownSteps.Count);

            probe.Projects[Folder + "rules.json"] = "Job rules";
            model.PickRules(Folder + "rules.json");
            Assert.Equal("Job rules", model.RulesRow.FileName);
        }

        // Review 1 of PR 90: each workbook of DLUS and sales by its role in a dialog of its own, so the two never swap and a Cancel
        // keeps the one the row had
        [Fact]
        public void R372_dlus_and_sales_are_picked_each_by_its_role_and_a_cancel_keeps_the_workbook_the_row_had()
        {
            var probe = new FakeProbe();
            PaneViewModel model = new PaneViewModel(probe, new FakePicker(Folder + "dlus.xlsx", Folder + "sales.xlsx"));
            var session = new DocumentSession("s", Folder + "Site.rvt", "Site");
            model.Load(session);
            FileRowViewModel row = model.InputRows.Single(input => input.Label == "DLUS and sales");
            model.Browse(row);
            Assert.Equal(Folder + "dlus.xlsx", session.Settings.InputPath(InputKeys.Dlus));
            Assert.Equal(Folder + "sales.xlsx", session.Settings.InputPath(InputKeys.Sales));

            var sales = new FakePicker(null, Folder + "sales2.xlsx");
            model = new PaneViewModel(probe, sales);
            model.Load(session);
            model.Browse(model.InputRows.Single(input => input.Label == "DLUS and sales"));
            Assert.Equal(new[] { "DLUS schedule", "Sales schedule" }, sales.Titles);
            Assert.Equal(Folder + "dlus.xlsx", session.Settings.InputPath(InputKeys.Dlus));
            Assert.Equal(Folder + "sales2.xlsx", session.Settings.InputPath(InputKeys.Sales));

            model = new PaneViewModel(probe, new FakePicker(Folder + "dlus2.xlsx", null));
            model.Load(session);
            model.Browse(model.InputRows.Single(input => input.Label == "DLUS and sales"));
            Assert.Equal(Folder + "dlus2.xlsx", session.Settings.InputPath(InputKeys.Dlus));
            Assert.Equal(Folder + "sales2.xlsx", session.Settings.InputPath(InputKeys.Sales));
            Assert.Equal("dlus2.xlsx, sales2.xlsx", model.InputRows.Single(input => input.Label == "DLUS and sales").FileName);

            var folder = new FakePicker(Folder + "landxml");
            model = new PaneViewModel(probe, folder);
            model.Load(session);
            model.Browse(model.InputRows.Single(input => input.Label == "Road surface"));
            Assert.Equal(Folder + "landxml", session.Settings.InputPath(InputKeys.RoadSurface));
            Assert.Equal(new[] { "folder" }, folder.Titles);
        }

        [Fact]
        public void R372_a_chosen_settings_file_makes_button_1_ready_and_the_next_step_box_runs_it_with_the_dry_run_line()
        {
            PaneViewModel model = Model(new FakeProbe());
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json" });

            Assert.False(model.FirstOpen);
            Assert.Equal("Next step", model.BoxLabel);
            Assert.Equal("Set up site", model.BoxName);
            Assert.Equal("Coordinates, levels and types from CAD. Dry run is on.", model.BoxLine);
            Assert.Equal("Run 1 Set up site", model.BoxButton);
            Assert.Equal(new[] { "Ready", "Needs step 1", "Needs step 2", "Needs step 3", "Needs step 4", "Needs step 5", "Needs step 6", "Needs step 7", "Needs step 4" }, model.Steps.Select(step => step.ChipText));
            Assert.Equal(MarkKind.Next, model.Steps[0].Mark);
            Assert.True(model.DryRun);

            model.DryRun = false;
            Assert.Equal("Coordinates, levels and types from CAD. Dry run is off.", model.BoxLine);
            Assert.False(model.Settings.DryRunFirst);
            var runs = new List<int>();
            model.RunRequested += runs.Add;
            model.BoxCommand.Execute(null);
            Assert.Equal(new[] { 1 }, runs);
        }

        [Fact]
        public void R444_a_click_on_a_step_row_runs_a_step_that_is_ready_or_done_through_the_box_path_and_a_step_that_waits_does_nothing()
        {
            PaneViewModel model = Model(new FakeProbe());
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json" });
            var runs = new List<int>();
            model.RunRequested += runs.Add;

            Assert.True(model.Steps[0].Runnable);
            Assert.Equal("Run 1 Set up site", model.Steps[0].RowTip);
            model.Steps[0].RunCommand.Execute(null);
            Assert.False(model.Steps[1].Runnable);
            Assert.Equal("Needs step 1", model.Steps[1].RowTip);
            model.Steps[1].RunCommand.Execute(null);
            model.Steps[8].RunCommand.Execute(null);
            Assert.Equal(new[] { 1 }, runs);

            PaneViewModel none = Model(new FakeProbe());
            var noRuns = new List<int>();
            none.RunRequested += noRuns.Add;
            Assert.All(none.Steps, step => Assert.False(step.Runnable));
            none.Steps[0].RunCommand.Execute(null);
            Assert.Empty(noRuns);
        }

        [Fact]
        public void R372_the_step_lines_of_5a_follow_the_order_of_the_workflow_changes_each_needs_the_one_before_and_9_needs_4()
        {
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 4 }, Enumerable.Range(1, 9).Select(StepPlan.Needs));
            Assert.Throws<ArgumentOutOfRangeException>(() => StepPlan.Needs(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => StepPlan.Needs(10));
            Assert.Equal(9, StepPlan.None(true).Count);
            string changes = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "13-workflow-changes.md"));
            Match odd = Regex.Match(changes, @"Each step needs the one before, and (\d) [\w ]+ needs (\d) \w+\.");
            Assert.True(odd.Success);
            Assert.Equal(int.Parse(odd.Groups[2].Value), StepPlan.Needs(int.Parse(odd.Groups[1].Value)));
            Assert.Equal(StepKind.NeedsSettings, StepPlan.None(false)[0].Kind);
            Assert.Equal(StepKind.Ready, StepPlan.None(true)[0].Kind);
            Assert.Equal(1, StepPlan.Next(StepPlan.None(true)));
            Assert.Equal(0, StepPlan.Next(StepPlan.None(false)));
        }

        [Fact]
        public void R372_the_steps_of_the_mock_mark_done_issues_the_next_step_and_waiting_with_the_short_chip_and_its_tooltip()
        {
            PaneViewModel model = Model(new FakeProbe());
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json" });
            model.SetSteps(new[]
            {
                new StepStatus(StepKind.Done), new StepStatus(StepKind.Done), new StepStatus(StepKind.Done), new StepStatus(StepKind.Issues, 1247),
                new StepStatus(StepKind.Ready), new StepStatus(StepKind.NeedsStep, 5), new StepStatus(StepKind.NeedsStep, 6), new StepStatus(StepKind.NeedsStep, 7),
                new StepStatus(StepKind.Ready),
            });

            Assert.Equal(new[] { "Done", "Done", "Done", "1,247 issues", "Ready", "Needs step 5", "Needs step 6", "Needs step 7", "Ready" }, model.Steps.Select(step => step.ChipText));
            Assert.Equal("Done with 1,247 issues", model.Steps[3].ChipTip);
            Assert.Null(model.Steps[4].ChipTip);
            Assert.Equal(new[] { MarkKind.Done, MarkKind.Done, MarkKind.Done, MarkKind.Issues, MarkKind.Next, MarkKind.Waiting, MarkKind.Waiting, MarkKind.Waiting, MarkKind.Waiting },
                model.Steps.Select(step => step.Mark));
            Assert.Equal("Run 5 Floors and ramps", model.BoxButton);
            Assert.Equal("Generic, yard and ramp floors. Dry run is on.", model.BoxLine);
            Assert.Equal(new[] { "Set up site", "Read data", "Review data", "Check", "Floors and ramps", "Levels", "Place villas", "Walls", "Report" }, model.Steps.Select(step => step.Name));
            Assert.Equal(PaneText.StepLines, model.Steps.Select(step => step.Line));
            Assert.Throws<ArgumentException>(() => model.SetSteps(new StepStatus[8]));
        }

        // The lines of the steps word for word from 7.2 of docs/design/12-ui-spec.md, a second reading of PaneText
        [Fact]
        public void R372_each_step_line_is_the_one_7_2_of_the_ui_spec_gives_its_number()
        {
            string spec = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "docs", "design", "12-ui-spec.md"));
            Match lines = Regex.Match(spec, @"NEW one plain line under it at 12: (.*?)\. The 9 steps");
            Assert.True(lines.Success);
            List<string> given = Regex.Matches(lines.Groups[1].Value, @"(\d) ""([^""]+)""").Cast<Match>().Select(match => match.Groups[2].Value).ToList();
            Assert.Equal(Enumerable.Range(1, 9).Select(number => number.ToString()), Regex.Matches(lines.Groups[1].Value, @"(\d) """).Cast<Match>().Select(match => match.Groups[1].Value));

            Assert.Equal(given, PaneText.StepLines);
            Assert.Equal("The team checks what was read", PaneText.StepLines[2]);
            Assert.Equal("Places a villa on each plot at its FFL", PaneText.StepLines[6]);
        }

        // Review 1 of PR 97: QL15 moved the size and hash into the tooltip, so a member no page binds would only feed the tests
        [Fact]
        public void R372_a_file_row_has_no_detail_member_since_the_tooltip_holds_the_size_and_hash()
        {
            Assert.Null(typeof(FileRowViewModel).GetProperty("Detail"));
        }

        [Fact]
        public void R372_each_row_shows_its_mark_and_the_file_name_with_the_full_path_and_under_it_the_size_and_short_hash_as_tooltip()
        {
            var probe = new FakeProbe();
            probe.Add(Folder + "plots.pdf", 3056293, "7061ac0d" + new string('0', 56));
            probe.Add(Folder + "rules.json", 2048, "205000fc" + new string('1', 56));
            probe.Add(Folder + "dlus.xlsx", 1048576, "aaaaaaaa" + new string('2', 56));
            PaneViewModel model = Model(probe);
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json", RulesFile = Folder + "rules.json" });
            FileRowViewModel plots = model.InputRows.Single(row => row.Label == "Plot numbers");
            FileRowViewModel dlus = model.InputRows.Single(row => row.Label == "DLUS and sales");

            model.PickInput(InputKeys.PlotNumbers, Folder + "plots.pdf");
            Assert.True(plots.Found);
            Assert.Equal("plots.pdf", plots.FileName);
            Assert.Equal(Folder + "plots.pdf" + Environment.NewLine + "2.91 MiB 7061ac0d", plots.ToolTip);
            Assert.Equal(3056293L, model.Settings.Inputs[InputKeys.PlotNumbers].Size);
            Assert.StartsWith("7061ac0d", model.Settings.Inputs[InputKeys.PlotNumbers].Sha256);
            // NEWPROJ-UI: the Setup and Rules rows show no file name ending in .json and no path, the Rules row the name its file gives,
            // else the file name with no extension
            Assert.True(model.RulesRow.Found);
            Assert.Equal("rules", model.RulesRow.FileName);
            Assert.Null(model.RulesRow.ToolTip);
            Assert.False(model.SettingsRow.Found);
            Assert.Equal("Saved for this project", model.SettingsRow.FileName);
            Assert.Null(model.SettingsRow.ToolTip);
            Assert.Equal("The drawings and schedules the tool reads. 1 of 8 found.", model.InputHelp);

            model.PickInput(InputKeys.Dlus, Folder + "dlus.xlsx");
            Assert.False(dlus.Found);
            Assert.Equal("dlus.xlsx", dlus.FileName);
            model.PickInput(InputKeys.Sales, Folder + "sales.xlsx");
            Assert.False(dlus.Found);
            Assert.Equal("dlus.xlsx, sales.xlsx", dlus.FileName);
            probe.Add(Folder + "sales.xlsx", 2097152, "bbbbbbbb" + new string('3', 56));
            model.PickInput(InputKeys.Sales, Folder + "sales.xlsx");
            Assert.True(dlus.Found);
            // QL15: each full path with its own size and short hash right under it
            Assert.Equal(string.Join(Environment.NewLine, Folder + "dlus.xlsx", "1.00 MiB aaaaaaaa", Folder + "sales.xlsx", "2.00 MiB bbbbbbbb"), dlus.ToolTip);
            Assert.Throws<ArgumentException>(() => model.PickInput("roads", "a"));
        }

        // Review 1 of PR 90: a file written again drops the size and hash the settings held until it is hashed again
        [Fact]
        public void R372_an_input_whose_file_changed_drops_its_old_size_and_hash_until_it_is_hashed_again()
        {
            var probe = new FakeProbe { Later = true };
            probe.Add(Folder + "a.dwg", 1048576, "12345678" + new string('0', 56));
            PaneViewModel model = Model(probe);
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json" });
            model.PickInput(InputKeys.Baseplan, Folder + "a.dwg");
            probe.Finish();
            SettingsInput input = model.Settings.Inputs[InputKeys.Baseplan];
            Assert.Equal(1048576L, input.Size);
            Assert.StartsWith("12345678", input.Sha256);

            probe.Add(Folder + "a.dwg", 2097152, "87654321" + new string('0', 56), 2);
            model.PickRules(Folder + "rules.json");
            Assert.Null(input.Size);
            Assert.Null(input.Sha256);
            Assert.Equal(Folder + "a.dwg" + Environment.NewLine + "2.00 MiB", model.InputRows[0].ToolTip);
            probe.Finish();
            Assert.Equal(2097152L, input.Size);
            Assert.StartsWith("87654321", input.Sha256);
            Assert.Equal(Folder + "a.dwg" + Environment.NewLine + "2.00 MiB 87654321", model.InputRows[0].ToolTip);
        }

        [Fact]
        public void R372_the_input_help_reads_all_found_when_every_row_is_found()
        {
            var probe = new FakeProbe();
            var settings = new PaneSettings { ConfigFile = Folder + "project.json" };
            foreach (string key in InputKeys.All)
            {
                probe.Add(Folder + key, 1, key.PadRight(64, '0'));
                settings.Pick(key, Folder + key);
            }
            PaneViewModel model = Model(probe);
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.LoadSettings(settings);
            Assert.Equal("The drawings and schedules the tool reads. All 8 found.", model.InputHelp);
            Assert.All(model.InputRows, row => Assert.True(row.Found, row.Label));
        }

        [Fact]
        public void R372_a_hash_that_comes_later_fills_the_row_and_one_of_an_earlier_document_is_kept_for_its_path_only()
        {
            var probe = new FakeProbe { Later = true };
            probe.Add(Folder + "a.dwg", 1048576, "12345678" + new string('0', 56));
            PaneViewModel model = Model(probe);
            var first = new DocumentSession("one", Folder + "One.rvt", "One");
            model.Load(first);
            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json" });
            FileRowViewModel baseplan = model.InputRows[0];
            model.PickInput(InputKeys.Baseplan, Folder + "a.dwg");
            Assert.Equal(Folder + "a.dwg" + Environment.NewLine + "1.00 MiB", baseplan.ToolTip);
            Assert.Single(probe.Waiting);

            model.Load(new DocumentSession("two", Folder + "Two.rvt", "Two"));
            probe.Finish();
            Assert.Equal("Not chosen", model.InputRows[0].FileName);
            model.Load(first);
            Assert.Equal(Folder + "a.dwg" + Environment.NewLine + "1.00 MiB 12345678", model.InputRows[0].ToolTip);
            Assert.Empty(probe.Waiting);
        }

        // Review 1 of PR 90: the user went to another document and came back before the hash ended, which shows when it ends
        [Fact]
        public void R372_a_hash_that_ends_after_the_user_came_back_to_its_document_shows_on_its_row()
        {
            var probe = new FakeProbe { Later = true };
            probe.Add(Folder + "a.dwg", 1048576, "12345678" + new string('0', 56));
            PaneViewModel model = Model(probe);
            var first = new DocumentSession("one", Folder + "One.rvt", "One");
            model.Load(first);
            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json" });
            model.PickInput(InputKeys.Baseplan, Folder + "a.dwg");
            model.Load(new DocumentSession("two", Folder + "Two.rvt", "Two"));
            model.Load(first);
            Assert.Equal(Folder + "a.dwg" + Environment.NewLine + "1.00 MiB", model.InputRows[0].ToolTip);
            Assert.Single(probe.Waiting);

            probe.Finish();
            Assert.Equal(Folder + "a.dwg" + Environment.NewLine + "1.00 MiB 12345678", model.InputRows[0].ToolTip);
            Assert.StartsWith("12345678", first.Settings.Inputs[InputKeys.Baseplan].Sha256);
        }

        [Fact]
        public void R372_with_no_document_the_pane_shows_its_labels_with_no_values_and_every_button_grey()
        {
            PaneViewModel model = Model(new FakeProbe());
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.Load(null);

            Assert.False(model.HasDocument);
            Assert.Equal(string.Empty, model.ProjectName);
            Assert.False(model.BoxCommand.CanExecute(null));
            Assert.Equal(string.Empty, model.InputHelp);
            Assert.All(model.InputRows.Concat(new[] { model.SettingsRow, model.RulesRow }), row => Assert.False(row.Shown));
            Assert.All(model.Steps, step => Assert.Equal(StepKind.None, step.Chip));
            Assert.False(model.DryRun);
            Assert.False(model.VillaMasses);
            Assert.Null(model.Settings);
            Assert.Empty(model.LevelProfiles);
        }

        [Fact]
        public void R372_a_document_with_an_empty_title_reads_not_saved_and_one_with_no_path_shows_its_steps_with_no_status()
        {
            PaneViewModel model = Model(new FakeProbe());
            model.Load(new DocumentSession("s", string.Empty, string.Empty));
            Assert.Equal("not saved", model.ProjectName);
            Assert.All(model.Steps, step => Assert.Equal(StepKind.None, step.Chip));
            Assert.True(model.BoxCommand.CanExecute(null));
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            Assert.Equal("Site", model.ProjectName);
            Assert.Equal(StepKind.NeedsSettings, model.Steps[0].Chip);
        }

        // R382, Revit run 3 of Lane B 5a: the detached document gave a bare PathName and a Title, so it has no work folder and its
        // steps show no status, as a document with no path. A file on a share is saved
        [Fact]
        public void R382_a_bare_file_name_gets_no_work_folder_and_its_steps_show_no_status()
        {
            foreach (string path in new[] { "SA5A-CENTRAL-TEST-NEW_detached.rvt", @"sub\x.rvt", "BIM 360://Project/Site.rvt" })
            {
                Assert.Null(WorkFolder.For(@"D:\Root", path));
                Assert.False(new DocumentSession("s", path, "SA5A-CENTRAL-TEST-NEW_detached").HasPath);
            }
            Assert.Equal(@"D:\Root\Site_", WorkFolder.For(@"D:\Root", @"\\server\share\Site.rvt").Substring(0, 13));
            Assert.True(new DocumentSession("s", @"\\server\share\Site.rvt", "Site").HasPath);

            PaneViewModel model = Model(new FakeProbe());
            model.Load(new DocumentSession("s", "SA5A-CENTRAL-TEST-NEW_detached.rvt", "SA5A-CENTRAL-TEST-NEW_detached"));
            Assert.All(model.Steps, step => Assert.Equal(StepKind.None, step.Chip));
        }

        // Review 1 of PR 99, Revit run 3: the detached document showed its Title as the project while its steps showed no status. A
        // document with no saved path reads not saved whatever its Title
        [Fact]
        public void R382_a_document_with_no_saved_path_reads_not_saved_whatever_its_title()
        {
            PaneViewModel model = Model(new FakeProbe());
            foreach (string path in new[] { "SA5A-CENTRAL-TEST-NEW_detached.rvt", string.Empty, "BIM 360://Project/Site.rvt" })
            {
                model.Load(new DocumentSession("s", path, "SA5A-CENTRAL-TEST-NEW_detached"));
                Assert.Equal(PaneText.NotSaved, model.ProjectName);
            }
            model.Load(new DocumentSession("s", @"\\server\share\Site.rvt", "Site"));
            Assert.Equal("Site", model.ProjectName);
        }

        [Fact]
        public void R372_a_pick_changes_the_settings_in_memory_and_villa_mode_starts_on_masses()
        {
            var probe = new FakeProbe();
            probe.Profiles = new[] { "parsons2024Rev6", "native2022" };
            PaneViewModel model = Model(probe);
            var session = new DocumentSession("s", Folder + "Site.rvt", "Site");
            model.Load(session);
            Assert.True(model.VillaMasses);
            Assert.False(model.VillaLinks);
            Assert.True(model.ScopeWhole);

            model.PickConfig(Folder + "project.json");
            model.PickRules(Folder + "rules.json");
            Assert.Equal(new[] { "parsons2024Rev6", "native2022" }, model.LevelProfiles);
            Assert.Equal("parsons2024Rev6", model.LevelProfile);
            Assert.Null(session.Settings.LevelProfile);
            model.LevelProfile = "native2022";
            model.VillaLinks = true;
            model.ScopeNbh = true;
            model.Nbh = "012";
            model.PickInput(InputKeys.RoadSurface, Folder + "landxml");

            Assert.Equal(Folder + "project.json", session.Settings.ConfigFile);
            Assert.Equal(Folder + "rules.json", session.Settings.RulesFile);
            Assert.Equal("native2022", session.Settings.LevelProfile);
            Assert.Equal(VillaModes.Links, session.Settings.VillaMode);
            Assert.False(model.VillaMasses);
            Assert.Equal(ScopeKinds.Nbh, session.Settings.Scope.Kind);
            Assert.False(model.ScopeWhole);
            Assert.Equal("012", session.Settings.Scope.Nbh);
            Assert.Equal(Folder + "landxml", session.Settings.InputPath(InputKeys.RoadSurface));
            Assert.True(model.InputRows[3].Folder);
        }

        [Fact]
        public void R372_each_document_gets_a_new_session_id_on_open_keeps_its_settings_while_open_and_loses_them_on_close()
        {
            var sessions = new PaneSessions();
            object one = new object();
            object two = new object();
            DocumentSession first = sessions.Opened(one, Folder + "One.rvt", "One");
            DocumentSession second = sessions.Opened(two, Folder + "Two.rvt", "Two");
            Assert.NotEqual(first.SessionId, second.SessionId);
            Assert.True(Guid.TryParse(first.SessionId, out _));
            first.Settings.ConfigFile = Folder + "project.json";

            Assert.Same(first, sessions.Find(one));
            Assert.Same(first, sessions.Seen(one, Folder + "One.rvt", "One"));
            Assert.Equal(Folder + "project.json", sessions.Find(one).Settings.ConfigFile);

            sessions.Closing(7, one);
            sessions.Closed(7, false);
            Assert.Same(first, sessions.Find(one));
            sessions.Closing(7, one);
            sessions.Closed(7, true);
            Assert.Null(sessions.Find(one));
            Assert.Equal(1, sessions.Count);

            DocumentSession again = sessions.Opened(one, Folder + "One.rvt", "One");
            Assert.NotEqual(first.SessionId, again.SessionId);
            Assert.False(again.Settings.Chosen);
            List<OpenDocument> open = sessions.Documents(two);
            Assert.Equal(2, open.Count);
            Assert.True(open.Single(document => document.SessionId == second.SessionId).Active);
            Assert.False(open.Single(document => document.SessionId == again.SessionId).Active);
            Assert.Same(two, sessions.KeyOf(second.SessionId));
            Assert.NotNull(sessions.Seen(new object(), Folder + "Three.rvt", "Three").SessionId);
        }

        // Review 1 of PR 90: after the close a Revit Document may give a false Equals or throw, so the session leaves on
        // DocumentClosing while it is valid, and nothing compares the closed one after
        [Fact]
        public void R372_a_closed_document_leaves_its_session_on_closing_and_is_never_compared_after_its_close()
        {
            var sessions = new PaneSessions();
            var closing = new Fragile();
            var other = new Fragile();
            DocumentSession gone = sessions.Opened(closing, Folder + "One.rvt", "One");
            DocumentSession kept = sessions.Opened(other, Folder + "Two.rvt", "Two");

            sessions.Closing(3, closing);
            closing.Closed = true;
            sessions.Closed(3, true);
            Assert.Equal(1, sessions.Count);
            List<OpenDocument> open = sessions.Documents(other);
            Assert.Equal(new[] { kept.SessionId }, open.Select(document => document.SessionId));
            Assert.Same(kept, sessions.Find(other));
            var relay = new AnswerRelay(() => { });
            relay.Post(new WorkerAnswer { RunId = "r3", Document = new WorkerDocument { Path = Folder + "One.rvt", SessionId = gone.SessionId } });
            Assert.Equal("run r3 ended after its document closed, nothing applied", relay.Route(open).Single().Issue);

            var cancelled = new Fragile();
            DocumentSession back = sessions.Opened(cancelled, Folder + "Four.rvt", "Four");
            sessions.Closing(4, cancelled);
            Assert.Null(sessions.Find(cancelled));
            sessions.Closed(4, false);
            Assert.Same(back, sessions.Find(cancelled));
            sessions.Closed(9, true);
            Assert.Equal(2, sessions.Count);
        }

        // Review 1 of PR 90: a new project saved under a name keeps its session and shows its title and its steps
        [Fact]
        public void R372_a_new_project_saved_under_a_name_shows_its_title_and_its_steps_with_the_same_session()
        {
            var sessions = new PaneSessions();
            object key = new object();
            DocumentSession session = sessions.Opened(key, string.Empty, string.Empty);
            PaneViewModel model = Model(new FakeProbe());
            model.Load(session);
            Assert.Equal("not saved", model.ProjectName);
            Assert.All(model.Steps, step => Assert.Equal(StepKind.None, step.Chip));

            session.Settings.ConfigFile = Folder + "project.json";
            DocumentSession saved = sessions.SavedAs(key, Folder + "New Site.rvt", "New Site");
            Assert.Same(session, saved);
            Assert.Equal(Folder + "New Site.rvt", saved.Path);
            Assert.Equal(Folder + "project.json", saved.Settings.ConfigFile);
            model.Load(saved);
            Assert.Equal("New Site", model.ProjectName);
            Assert.Equal("Ready", model.Steps[0].ChipText);

            // Review 2 of PR 90: a document the add-in does not know, a family or a linked one, gets no session on a save as
            object family = new object();
            Assert.Null(sessions.SavedAs(family, Folder + "Door.rfa", "Door"));
            Assert.Null(sessions.Find(family));
            Assert.Equal(1, sessions.Count);
        }

        [Fact]
        public void R372_a_worker_answer_goes_to_the_document_of_its_request_while_another_is_active_and_is_dropped_once_that_closed()
        {
            var sessions = new PaneSessions();
            object imc = new object();
            object imd = new object();
            DocumentSession imcSession = sessions.Opened(imc, Folder + "IMC.rvt", "IMC");
            sessions.Opened(imd, Folder + "IMD.rvt", "IMD");
            int raised = 0;
            var relay = new AnswerRelay(() => raised++);

            relay.Post(new WorkerAnswer { RunId = "r1", Document = new WorkerDocument { Path = Folder.ToLowerInvariant() + "imc.rvt", SessionId = imcSession.SessionId } });
            Assert.Equal(1, raised);
            Assert.Equal(1, relay.Waiting);
            List<RoutedAnswer> routed = relay.Route(sessions.Documents(imd));
            Assert.Single(routed);
            Assert.Equal(imcSession.SessionId, routed[0].Document.SessionId);
            Assert.False(routed[0].Document.Active);
            Assert.Null(routed[0].Issue);
            Assert.Equal(0, relay.Waiting);

            sessions.Closing(1, imc);
            sessions.Closed(1, true);
            sessions.Opened(imc, Folder + "IMC.rvt", "IMC");
            relay.Post(new WorkerAnswer { RunId = "r2", Document = new WorkerDocument { Path = Folder + "IMC.rvt", SessionId = imcSession.SessionId } });
            routed = relay.Route(sessions.Documents(imd));
            Assert.Null(routed[0].Document);
            Assert.Equal("run r2 ended after its document closed, nothing applied", routed[0].Issue);
        }

        [Fact]
        public void R372_the_work_folder_comes_from_the_machine_file_or_documents_and_is_named_by_the_file_and_the_hash_of_its_path()
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Func<string, string> read = path => files.TryGetValue(path, out string text) ? text : null;
            Assert.Equal(@"C:\Users\u\Documents\SiteAdaptation", WorkFolder.Root(@"C:\Users\u\AppData\Roaming", @"C:\Users\u\Documents", read));
            files[@"C:\Users\u\AppData\Roaming\SiteAdaptation\machine.json"] = "{\"workRoot\": \"D:\\\\Work\\\\SiteAdaptation\"}";
            Assert.Equal(@"D:\Work\SiteAdaptation", WorkFolder.Root(@"C:\Users\u\AppData\Roaming", @"C:\Users\u\Documents", read));
            files[@"C:\Users\u\AppData\Roaming\SiteAdaptation\machine.json"] = "{\"root\": \"D:\\\\Work\"}";
            Assert.Contains("gives no workRoot", Assert.Throws<InvalidDataException>(() => WorkFolder.Root(@"C:\Users\u\AppData\Roaming", @"C:\Users\u\Documents", read)).Message);
            files[@"C:\Users\u\AppData\Roaming\SiteAdaptation\machine.json"] = "{";
            Assert.Contains("is no JSON", Assert.Throws<InvalidDataException>(() => WorkFolder.Root(@"C:\Users\u\AppData\Roaming", @"C:\Users\u\Documents", read)).Message);

            // The hashes were worked out a second way, by hashlib of Python over the lower case path in UTF-8
            Assert.Equal(@"D:\Root\Model A_45e7c493", WorkFolder.For(@"D:\Root", @"C:\Work\Site\Model A.rvt"));
            Assert.Equal(@"D:\Root\Model A_45e7c493", WorkFolder.For(@"D:\Root", @"c:\WORK\site\model a.RVT").Replace("model a", "Model A"));
            Assert.Equal(@"D:\Root\Model A_690e2e36", WorkFolder.For(@"D:\Root", @"C:\Other\Model A.rvt"));
            Assert.Equal(@"D:\Root\Model A_45e7c493", WorkFolder.For(@"D:\Root", @"C:\Work\Site\..\Site\Model A.rvt"));
            Assert.Null(WorkFolder.For(@"D:\Root", string.Empty));
            Assert.Null(WorkFolder.For(@"D:\Root", null));

            Assert.Null(WorkFolder.Refusal(@"D:\Root\Model A_45e7c493", @"C:\02-Site Adaptation"));
            Assert.Null(WorkFolder.Refusal(@"D:\Root\Model A_45e7c493", null));
            Assert.Contains("lies inside inputRoot", WorkFolder.Refusal(@"C:\02-Site Adaptation\Work\Model A_45e7c493", @"C:\02-site adaptation\"));
            Assert.False(WorkFolder.Inside(@"C:\02-Site Adaptation 2\Work", @"C:\02-Site Adaptation"));
            Assert.True(WorkFolder.Inside(@"C:\02-Site Adaptation", @"C:\02-Site Adaptation"));

            files[@"C:\Job\project.json"] = "{\"inputRoot\": \"C:\\\\02-Site Adaptation\"}";
            Assert.Equal(@"C:\02-Site Adaptation", WorkFolder.InputRoot(@"C:\Job\project.json", read));
            Assert.Null(WorkFolder.InputRoot(@"C:\Job\none.json", read));
            Assert.Null(WorkFolder.InputRoot(null, read));
        }

        [Fact]
        public void R372_a_file_is_hashed_by_its_content_and_a_folder_by_the_names_sizes_and_write_times_of_its_files_off_the_ui_thread()
        {
            string root = Path.Combine(Path.GetTempPath(), "pane-hash-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "folder", "sub"));
                string file = Path.Combine(root, "a.txt");
                File.WriteAllText(file, "abc");
                // hashlib of Python gives the same for b"abc"
                Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", DiskProbe.FileSha256(file));

                string folder = Path.Combine(root, "folder");
                File.WriteAllText(Path.Combine(folder, "one.xml"), "1111");
                File.WriteAllText(Path.Combine(folder, "sub", "two.xml"), "22");
                var time = new DateTime(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(Path.Combine(folder, "one.xml"), time);
                File.SetLastWriteTimeUtc(Path.Combine(folder, "sub", "two.xml"), time);
                string before = DiskProbe.FolderSha256(folder);
                File.WriteAllText(Path.Combine(folder, "one.xml"), "9999");
                File.SetLastWriteTimeUtc(Path.Combine(folder, "one.xml"), time);
                Assert.Equal(before, DiskProbe.FolderSha256(folder));
                File.SetLastWriteTimeUtc(Path.Combine(folder, "one.xml"), time.AddSeconds(1));
                Assert.NotEqual(before, DiskProbe.FolderSha256(folder));

                var looked = new DiskProbe(action => action());
                InputFacts facts = looked.Look(folder, true);
                Assert.True(facts.Found);
                Assert.Equal(6L, facts.Size);
                Assert.False(looked.Look(Path.Combine(root, "none.txt"), false).Found);
                Assert.Equal(3L, looked.Look(file, false).Size);

                // Review 1 of PR 90: the answer goes to the Dispatcher of the page, here one of a thread of its own, with no
                // SynchronizationContext on the caller as in a Revit event
                Dispatcher page = null;
                var ready = new ManualResetEventSlim();
                var ui = new Thread(() =>
                {
                    page = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                });
                ui.SetApartmentState(ApartmentState.STA);
                ui.Start();
                Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));
                // CI of PR 90 failed here twice: the hash queued on the thread pool did not run within 10 s while other tests held
                // the pool. Every pool thread is held here on purpose, so the hash must take a thread of its own to end
                var gate = new ManualResetEventSlim();
                ThreadPool.GetMinThreads(out int poolThreads, out int ioThreads);
                for (int at = 0; at < poolThreads + 64; at++)
                {
                    ThreadPool.QueueUserWorkItem(state => gate.Wait());
                }
                try
                {
                    int postedFrom = 0;
                    bool postedFromPool = true;
                    int doneOn = 0;
                    string hash = null;
                    var finished = new ManualResetEventSlim();
                    var probe = new DiskProbe(action =>
                    {
                        postedFrom = Thread.CurrentThread.ManagedThreadId;
                        postedFromPool = Thread.CurrentThread.IsThreadPoolThread;
                        page.BeginInvoke(action);
                    });
                    SynchronizationContext previous = SynchronizationContext.Current;
                    SynchronizationContext.SetSynchronizationContext(null);
                    try
                    {
                        probe.Hash(file, false, done =>
                        {
                            hash = done;
                            doneOn = Thread.CurrentThread.ManagedThreadId;
                            finished.Set();
                        });
                    }
                    finally
                    {
                        SynchronizationContext.SetSynchronizationContext(previous);
                    }
                    Assert.True(finished.Wait(TimeSpan.FromSeconds(10)), "the hash did not end, posted from thread " + postedFrom);
                    Assert.Equal(ui.ManagedThreadId, doneOn);
                    Assert.False(postedFromPool);
                    Assert.NotEqual(ui.ManagedThreadId, postedFrom);
                    Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, postedFrom);
                    Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
                }
                finally
                {
                    gate.Set();
                    page.InvokeShutdown();
                    ui.Join();
                }

                // Review 1 of PR 90: a workbook open in Excel, which holds it for writing, is hashed all the same
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", DiskProbe.FileSha256(file));
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void R372_the_text_template_fills_each_value_in_order_and_refuses_a_count_that_differs()
        {
            Assert.Equal("Run 4 Place villas", TextTemplate.Fill(PaneText.RunButton, 4, "Place villas"));
            Assert.Equal("1,247 issues", TextTemplate.Fill(PaneText.Issues, TextTemplate.Count(1247)));
            Assert.Throws<ArgumentException>(() => TextTemplate.Fill(PaneText.RunButton, 4));
            Assert.Throws<ArgumentException>(() => TextTemplate.Fill(PaneText.Ready, 4));
            Assert.Equal("2.91", PaneViewModel.SizeText(3056293));
        }

        // Lane B 5a PR 6. The addinRun job of the test host counts the loads of the pane around each step by this counter
        [Fact]
        public void R372_the_pane_counts_each_load_of_a_document_or_of_none()
        {
            PaneViewModel model = Model(new FakeProbe());
            int start = model.Loads;
            var sessions = new PaneSessions();

            model.Load(sessions.Opened(new object(), Folder + "a.rvt", "a"));
            model.Load(null);
            model.Load(sessions.Opened(new object(), "", ""));

            Assert.Equal(start + 3, model.Loads);
        }

        private static PaneViewModel Model(IInputProbe probe)
        {
            return new PaneViewModel(probe);
        }

        private sealed class FakeProbe : IInputProbe
        {
            private readonly Dictionary<string, Tuple<long, string, long>> files = new Dictionary<string, Tuple<long, string, long>>(StringComparer.OrdinalIgnoreCase);

            public bool Later { get; set; }
            public List<Action> Waiting { get; } = new List<Action>();
            public IReadOnlyList<string> Profiles { get; set; } = new string[0];

            public void Add(string path, long size, string sha256, long stamp = 1)
            {
                files[path] = Tuple.Create(size, sha256, stamp);
            }

            public InputFacts Look(string path, bool folder)
            {
                return files.TryGetValue(path, out Tuple<long, string, long> file) ? new InputFacts { Found = true, Size = file.Item1, Stamp = file.Item3 } : new InputFacts();
            }

            public void Hash(string path, bool folder, Action<string> done)
            {
                Action answer = () => done(files[path].Item2);
                if (Later)
                {
                    Waiting.Add(answer);
                }
                else
                {
                    answer();
                }
            }

            public void Finish()
            {
                List<Action> now = Waiting.ToList();
                Waiting.Clear();
                now.ForEach(answer => answer());
            }

            // The villaModeDefault of config/project.json
            public string RulesName(string rulesFile)
            {
                return Projects.TryGetValue(rulesFile ?? "", out string project) ? project : null;
            }

            public Dictionary<string, string> Projects { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public string VillaModeDefault(string configFile)
            {
                return VillaModes.Masses;
            }

            public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
            {
                fallback = string.IsNullOrEmpty(rulesFile) ? null : Profiles.FirstOrDefault();
                return string.IsNullOrEmpty(rulesFile) ? new string[0] : Profiles;
            }
        }

        // The dialogs of Browse, each answer in turn, null a Cancel. Titles keeps the title of each file dialog and "folder" for a
        // folder dialog
        private sealed class FakePicker : IPathPicker
        {
            private readonly Queue<string> answers;

            public FakePicker(params string[] answers)
            {
                this.answers = new Queue<string>(answers);
            }

            public List<string> Titles { get; } = new List<string>();

            public string File(string title)
            {
                Titles.Add(title);
                return answers.Dequeue();
            }

            public string Folder()
            {
                Titles.Add("folder");
                return answers.Dequeue();
            }

            public string Save(string title)
            {
                Titles.Add(title);
                return answers.Dequeue();
            }
        }

        // A key as a Revit Document may act after its close: Equals and GetHashCode throw
        private sealed class Fragile
        {
            public bool Closed { get; set; }

            public override bool Equals(object other)
            {
                if (Closed)
                {
                    throw new InvalidOperationException("the document is closed");
                }
                return ReferenceEquals(this, other);
            }

            public override int GetHashCode()
            {
                if (Closed)
                {
                    throw new InvalidOperationException("the document is closed");
                }
                return 7;
            }
        }
    }
}
