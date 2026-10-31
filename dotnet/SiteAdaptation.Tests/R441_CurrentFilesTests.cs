using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.SetUp;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R441 Current files gate, SCAN-UI of Lane C
    public class R441_CurrentFilesTests
    {
        // The folders of config and the rules by the path rules of the runner, the gate runs on Windows and Linux
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "sa_r441");
        private static readonly string RulesFolder = Path.Combine(Root, "rules");

        private static ProjectSettings Setup(params string[] ticked)
        {
            var settings = new ProjectSettings { ConfigFile = Path.Combine(Root, "config", "project.json") };
            settings.Inputs[InputNames.Baseplan] = new SettingsInput { Path = @"C:\in\site.dwg" };
            settings.Inputs[InputNames.Sales] = new SettingsInput { Path = @"C:\in\plot list.xlsx" };
            settings.Inputs[InputNames.Prototypes] = new SettingsInput { Path = @"C:\in\prototypes" };
            settings.CurrentFiles = ticked.Length == 0 ? null : ticked.Select(name => CurrentFiles.Entry(name, CurrentFiles.FileName(settings.Inputs[name].Path))).ToList();
            return settings;
        }

        [Fact]
        public void R441_rows_list_each_file_of_the_setup_but_the_prototypes_folder_and_mark_the_site_drawing()
        {
            List<CurrentFileRow> rows = CurrentFiles.Rows(Setup(InputNames.Sales));
            Assert.Equal(new[] { InputNames.Baseplan, InputNames.Sales }, rows.Select(row => row.Input));
            Assert.Equal(new[] { "site.dwg", "plot list.xlsx" }, rows.Select(row => row.FileName));
            Assert.True(rows[0].Site);
            Assert.False(rows[1].Site);
            Assert.False(rows[0].Current);
            Assert.True(rows[1].Current);
        }

        [Fact]
        public void R441_apply_waits_for_the_site_drawing_ticked()
        {
            Assert.False(CurrentFiles.CanApply(CurrentFiles.Rows(Setup())));
            Assert.False(CurrentFiles.CanApply(CurrentFiles.Rows(Setup(InputNames.Sales))));
            Assert.True(CurrentFiles.CanApply(CurrentFiles.Rows(Setup(InputNames.Baseplan))));
            Assert.False(CurrentFiles.CanApply(null));
        }

        [Fact]
        public void R441_apply_lays_the_ticks_and_the_rules_file_over_the_settings_and_leaves_the_given_settings_alone()
        {
            ProjectSettings before = Setup();
            ProjectSettings next = CurrentFiles.Apply(before, new[] { InputNames.Sales, InputNames.Baseplan, InputNames.Sales }, Path.Combine(RulesFolder, "other.json"));
            Assert.Equal(new[] { "baseplan|site.dwg", "sales|plot list.xlsx" }, next.CurrentFiles);
            Assert.Equal(Path.Combine(RulesFolder, "other.json"), next.RulesFile);
            Assert.Null(before.CurrentFiles);
            Assert.Null(before.RulesFile);
            Assert.Equal(next.CurrentFiles, SettingsJson.Read(SettingsJson.Write(next)).CurrentFiles);
        }

        [Fact]
        public void R441_null_ticks_of_a_stand_in_keep_the_ticks_the_settings_hold()
        {
            ProjectSettings next = CurrentFiles.Apply(Setup(InputNames.Baseplan), null, null);
            Assert.Equal(new[] { "baseplan|site.dwg" }, next.CurrentFiles);
        }

        [Fact]
        public void R441_the_settings_reader_refuses_a_current_file_of_no_input_name_and_an_empty_pick()
        {
            ProjectSettings bad = Setup();
            bad.CurrentFiles = new List<string> { "site" };
            Assert.Throws<InvalidDataException>(() => SettingsJson.Read(SettingsJson.Write(bad)));
            bad.CurrentFiles = null;
            bad.ScanPicks = new SortedDictionary<string, string> { ["plotOutline"] = "" };
            Assert.Throws<InvalidDataException>(() => SettingsJson.Read(SettingsJson.Write(bad)));
        }

        [Fact]
        public void R441_a_settings_file_written_before_scan_ui_reads_with_the_new_fields_null()
        {
            ProjectSettings read = SettingsJson.Read("{\"formatVersion\":1,\"inputs\":{},\"scope\":{\"kind\":\"project\"},\"dryRunFirst\":true,\"lastRuns\":{}}");
            Assert.Null(read.CurrentFiles);
            Assert.Null(read.ScanPicks);
            Assert.Null(read.ConflictPicks);
            Assert.DoesNotContain("currentFiles", SettingsJson.Write(read));
        }

        [Fact]
        public void R441_a_new_file_picked_for_a_ticked_input_is_not_ticked()
        {
            ProjectSettings settings = Setup(InputNames.Baseplan);
            settings.Inputs[InputNames.Baseplan] = new SettingsInput { Path = @"C:\in\site rev 2.dwg" };
            Assert.False(CurrentFiles.Rows(settings)[0].Current);
            Assert.Equal("Current files: none ticked", CurrentFiles.ReportLine(settings));
        }

        [Fact]
        public void R441_the_report_line_names_the_ticked_files_or_none()
        {
            Assert.Equal("Current files: none ticked", CurrentFiles.ReportLine(Setup()));
            Assert.Equal("Current files: site.dwg, plot list.xlsx", CurrentFiles.ReportLine(Setup(InputNames.Baseplan, InputNames.Sales)));
        }

        [Fact]
        public void R441_button_9_gives_the_line_current_files_after_the_lines_of_its_preview()
        {
            var body = new ButtonBody { Preview = context => new RunSummary("Report Excel files: 1", new[] { "Writes a" }) };
            ProjectSettings settings = Setup(InputNames.Baseplan);
            RunSummary summary = CurrentFiles.WithReportLine(body, () => settings).Preview(null);
            Assert.Equal("Report Excel files: 1", summary.Counts);
            Assert.Equal(new[] { "Writes a", "Current files: site.dwg" }, summary.PerPlot);
        }

        [Fact]
        public void R441_profiles_come_from_the_rules_folder_beside_config_by_the_name_each_gives_itself()
        {
            var texts = new Dictionary<string, string>
            {
                [Path.Combine(RulesFolder, "a.json")] = "{\"project\": \"Project A\"}",
                [Path.Combine(RulesFolder, "b.json")] = "{}",
                [Path.Combine(RulesFolder, "c.json")] = "not json",
            };
            string asked = null;
            List<RulesChoice> choices = CurrentFiles.Profiles(Setup(), folder => { asked = folder; return texts.Keys; }, path => texts[path]);
            Assert.Equal(RulesFolder, asked);
            Assert.Equal(new[] { "Project A", "b" }, choices.Select(choice => choice.Name));
        }

        [Fact]
        public void R441_the_dialog_fills_every_view_and_keeps_the_ticks_and_rules_only_on_apply()
        {
            ProjectSettings settings = Setup();
            ProjectSettings kept = null;
            var inner = new FakeDialog { Answer = new SetUpSiteAnswer { Apply = true, CurrentFiles = new List<string> { InputNames.Baseplan }, RulesFile = Path.Combine(RulesFolder, "a.json") } };
            var dialog = new CurrentFilesDialog(inner, () => settings, next => kept = next, folder => new string[0], path => "{}");
            var callbacks = new SetUpSiteCallbacks { DryRun = slope => new SetUpSiteView() };
            dialog.Show(new SetUpSiteView(), callbacks);
            Assert.Equal(2, inner.Shown.CurrentFiles.Count);
            Assert.Equal(2, inner.Callbacks.DryRun(0.5).CurrentFiles.Count);
            Assert.Equal(new[] { "baseplan|site.dwg" }, kept.CurrentFiles);
            Assert.Equal(Path.Combine(RulesFolder, "a.json"), kept.RulesFile);

            kept = null;
            inner.Answer = new SetUpSiteAnswer { Apply = false, CurrentFiles = new List<string> { InputNames.Baseplan } };
            dialog.Show(new SetUpSiteView(), callbacks);
            Assert.Null(kept);
        }

        [Fact]
        public void R436_a_saved_project_carries_the_value_picks_and_not_the_conflict_picks()
        {
            ProjectSettings job = Setup();
            job.ScanPicks = new SortedDictionary<string, string> { ["villaType"] = "feet.dwg|block|BLOCKS" };
            job.ConflictPicks = new SortedDictionary<string, string> { ["1|villa type"] = "feet.dwg|block|BLOCKS" };
            string saved = PreSetting.Export(job);
            var into = new ProjectSettings();
            PreSetting.Import(saved, into);
            Assert.Equal("feet.dwg|block|BLOCKS", into.ScanPicks["villaType"]);
            Assert.Null(into.ConflictPicks);
            var kept = new ProjectSettings { ScanPicks = new SortedDictionary<string, string> { ["plotOutline"] = "site.dwg|A" } };
            PreSetting.Import(PreSetting.Export(new ProjectSettings()), kept);
            Assert.Equal("site.dwg|A", kept.ScanPicks["plotOutline"]);
        }

        private sealed class FakeDialog : ISetUpSiteDialog
        {
            public SetUpSiteAnswer Answer { get; set; }

            public SetUpSiteView Shown { get; private set; }

            public SetUpSiteCallbacks Callbacks { get; private set; }

            public SetUpSiteAnswer Show(SetUpSiteView view, SetUpSiteCallbacks callbacks)
            {
                Shown = view;
                Callbacks = callbacks;
                return Answer;
            }
        }
    }
}
