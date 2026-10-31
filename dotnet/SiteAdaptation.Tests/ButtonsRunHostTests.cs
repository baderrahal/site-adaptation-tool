using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Lane B 5c, the Revit run after 5c. The buttons mode of the addinRun job runs buttons 1 to 7 of the add-in on one NBH as
    // the ribbon runs them, with windows that answer by themselves, and measures each solid and link it placed by its own reads. What
    // runs outside Revit is tested here, the job, the steps, the corner and link compares, the dialog answers and the script by text
    public class ButtonsRunHostTests
    {
        private const string Job = "{\"kind\": \"addinRun\", \"model\": \"m.rvt\", \"buttons\": [1, 2, 3, 4, 7], \"nbh\": \"012\", \"villaMode\": \"masses\","
            + " \"linksAfterMasses\": true, \"dryRunFirst\": true, \"paneSettings\": \"pane_settings.json\"}";

        private static string Text(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules", "index.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return File.ReadAllText(Path.Combine(new[] { folder }.Concat(parts).ToArray())).Replace("\r\n", "\n");
        }

        private static TestHostJob Read(string json)
        {
            return JsonConvert.DeserializeObject<TestHostJob>(json);
        }

        [Fact]
        public void R384_a_job_carries_the_buttons_their_nbh_villa_mode_links_after_masses_and_the_dry_run()
        {
            TestHostJob job = Read(Job);
            Assert.Equal(new[] { 1, 2, 3, 4, 7 }, job.Buttons);
            Assert.Equal("012", job.Nbh);
            Assert.Equal(VillaModes.Masses, job.VillaMode);
            Assert.True(job.LinksAfterMasses);
            Assert.True(job.DryRunFirst);
            Assert.Equal("pane_settings.json", job.PaneSettings);
            Assert.Null(ButtonsRun.Refusal(job));
            Assert.Null(Read("{\"kind\": \"addinRun\"}").Buttons);
        }

        [Theory]
        [InlineData("[]", "masses", false, "names no button")]
        [InlineData("[1, 10]", "masses", false, "only 1, 2, 3, 4, 5, 6, 7, 8 and 9")]
        [InlineData("[2, 2]", "masses", false, "twice")]
        [InlineData("[4, 2]", "masses", false, "order of the pane")]
        [InlineData("[7]", "solid", false, "villaMode")]
        [InlineData("[4]", "masses", true, "linksAfterMasses")]
        [InlineData("[7]", "links", true, "linksAfterMasses")]
        public void R384_a_buttons_job_the_run_cannot_carry_out_is_refused_with_its_reason(string buttons, string mode, bool linksAfter, string reason)
        {
            TestHostJob job = Read("{\"kind\": \"addinRun\", \"buttons\": " + buttons + ", \"nbh\": \"012\", \"villaMode\": \"" + mode + "\", \"linksAfterMasses\": "
                + (linksAfter ? "true" : "false") + ", \"paneSettings\": \"p.json\"}");
            Assert.Contains(reason, ButtonsRun.Refusal(job));
        }

        [Fact]
        public void R384_a_buttons_job_needs_its_nbh_and_pane_settings_and_never_runs_with_setup()
        {
            Assert.Contains("NBH", ButtonsRun.Refusal(Read("{\"buttons\": [2], \"paneSettings\": \"p.json\"}")));
            Assert.Contains("pane settings", ButtonsRun.Refusal(Read("{\"buttons\": [2], \"nbh\": \"012\"}")));
            Assert.Contains("setUp", ButtonsRun.Refusal(Read("{\"buttons\": [2], \"nbh\": \"012\", \"paneSettings\": \"p.json\", \"setUp\": true}")));
            // The guard refuses it before Revit acts
            TestHostJob job = Read("{\"kind\": \"addinRun\", \"buttons\": [2, 2], \"nbh\": \"012\", \"paneSettings\": \"p.json\"}");
            Assert.Contains("twice", TestHostGuard.CheckButtons(job));
            Assert.Null(TestHostGuard.CheckButtons(Read(Job)));
        }

        [Fact]
        public void R384_the_steps_run_in_the_job_order_and_links_follow_masses_as_a_second_run_of_7()
        {
            List<ButtonsStep> steps = ButtonsRun.Steps(Read(Job));
            Assert.Equal(new[] { "button1", "button2", "button3", "button4", "button7_masses", "button7_links" }, steps.Select(step => step.Name));
            Assert.Equal(new[] { null, null, null, null, VillaModes.Masses, VillaModes.Links }, steps.Select(step => step.VillaMode));
            Assert.Single(ButtonsRun.Steps(Read("{\"buttons\": [7], \"villaMode\": \"links\"}")));
        }

        // R378, R379. Buttons 5 and 6 of 5d run in a job by Buttons.Run as the ribbon runs them, in the order of the pane, and neither waits
        // for a worker after it returns, as 6 waits for levelExcel inside its transaction
        [Fact]
        public void R378_R379_a_job_may_run_buttons_5_and_6_in_the_order_of_the_pane()
        {
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, ButtonsRun.Known);
            Assert.False(ButtonsRun.Waits(5));
            Assert.False(ButtonsRun.Waits(6));
        }

        // R380. Button 8 of 5d runs in a job after 7 and waits for no worker
        [Fact]
        public void R380_a_job_may_run_button_8_after_7()
        {
            Assert.True(Array.IndexOf(ButtonsRun.Known, 8) == Array.IndexOf(ButtonsRun.Known, 7) + 1);
            Assert.False(ButtonsRun.Waits(8));
        }

        // R381. Button 9 of 5e runs in a job after 8 and waits for reportExcel inside its body, as 6 waits for levelExcel, so the job
        // waits for no worker after it returns, and Update from report after it needs its commit
        [Fact]
        public void R381_R384_a_job_may_run_button_9_after_8_and_it_waits_inside_its_body()
        {
            Assert.Equal(9, ButtonsRun.Known.Last());
            Assert.False(ButtonsRun.Waits(9));
            Assert.Null(ButtonsRun.WorkerCommand(9));
            Assert.True(ButtonsRun.GoesOn(9, ButtonOutcomes.Committed));
            Assert.False(ButtonsRun.GoesOn(9, ButtonOutcomes.Stopped));
            Assert.Null(ButtonsRun.Refusal(Read("{\"buttons\": [4, 5, 6, 7, 8, 9], \"nbh\": \"012\", \"villaMode\": \"masses\", \"paneSettings\": \"p.json\"}")));
        }

        private const string NamedJob = "{\"kind\": \"addinRun\", \"buttons\": [6, 9], \"nbh\": \"012\", \"paneSettings\": \"p.json\","
            + " \"named\": [\"SiteAdaptationRulesCommand\", \"UpdateFromReportCommand\"]}";

        // R384, R402, R403. The commands with no number run after the numbered buttons by Buttons.Run of their command name, in the order
        // of the job, each step named by its command
        [Fact]
        public void R384_the_named_commands_run_after_the_numbered_buttons_by_their_command_name()
        {
            TestHostJob job = Read(NamedJob);
            Assert.Equal(new[] { ButtonsRun.RulesCommand, ButtonsRun.UpdateCommand }, job.Named);
            Assert.Equal(new[] { "SiteAdaptationRulesCommand", "UpdateFromReportCommand" }, ButtonsRun.Named);
            Assert.Null(ButtonsRun.Refusal(job));
            List<ButtonsStep> steps = ButtonsRun.Steps(job);
            Assert.Equal(new[] { "button6", "button9", "SiteAdaptationRulesCommand", "UpdateFromReportCommand" }, steps.Select(step => step.Name));
            Assert.Equal(new string[] { null, null, ButtonsRun.RulesCommand, ButtonsRun.UpdateCommand }, steps.Select(step => step.Command));
            Assert.Equal(new[] { 6, 9, 0, 0 }, steps.Select(step => step.Button));
            Assert.Null(Read(Job).Named);
            Assert.DoesNotContain(ButtonsRun.Steps(Read(Job)), step => step.Command != null);
        }

        [Theory]
        [InlineData("[6, 9]", "[]", "named names no command")]
        [InlineData("[6, 9]", "[\"PlaceVillasCommand\"]", "named may name only SiteAdaptationRulesCommand and UpdateFromReportCommand, not PlaceVillasCommand")]
        [InlineData("[6, 9]", "[\"SiteAdaptationRulesCommand\", \"SiteAdaptationRulesCommand\"]", "named names SiteAdaptationRulesCommand twice")]
        [InlineData("[6]", "[\"UpdateFromReportCommand\"]", "UpdateFromReportCommand needs buttons 6 and 9 in the job")]
        [InlineData("[9]", "[\"UpdateFromReportCommand\"]", "UpdateFromReportCommand needs buttons 6 and 9 in the job")]
        [InlineData("null", "[\"SiteAdaptationRulesCommand\"]", "named runs after the buttons of a buttons job")]
        public void R384_a_named_command_the_run_cannot_carry_out_is_refused_with_its_reason(string buttons, string named, string reason)
        {
            TestHostJob job = Read("{\"kind\": \"addinRun\", \"buttons\": " + buttons + ", \"nbh\": \"012\", \"paneSettings\": \"p.json\", \"named\": " + named + "}");
            Assert.Contains(reason, ButtonsRun.Refusal(job));
            Assert.Null(ButtonsRun.Refusal(Read("{\"kind\": \"addinRun\", \"buttons\": [1], \"nbh\": \"012\", \"paneSettings\": \"p.json\", \"named\": [\"SiteAdaptationRulesCommand\"]}")));
        }

        // A named command commits nothing in the job: its window is answered and closed with no change. A window the host did not answer
        // is a stop, written with the step, and the run goes on, as no named command needs another
        [Fact]
        public void R384_a_named_command_ends_closed_when_its_window_was_answered_and_never_stops_the_run()
        {
            Assert.Equal(ButtonOutcomes.Closed, ButtonsRun.NamedOutcome(true, false));
            Assert.Equal(ButtonOutcomes.Stopped, ButtonsRun.NamedOutcome(false, false));
            Assert.Equal(ButtonOutcomes.Committed, ButtonsRun.NamedOutcome(true, true));
            var rules = new ButtonsStep(ButtonsRun.RulesCommand);
            Assert.True(ButtonsRun.GoesOn(rules, ButtonOutcomes.Stopped));
            Assert.True(ButtonsRun.GoesOn(rules, ButtonOutcomes.Closed));
            Assert.False(ButtonsRun.GoesOn(new ButtonsStep(9, null), ButtonOutcomes.Stopped));
            Assert.True(ButtonsRun.GoesOn(new ButtonsStep(3, null), ButtonOutcomes.Cancelled));
        }

        // R402. Update from report reads the level Excel of 6 and the report Excel of 9 of the NBH in the work folder of the run, in that
        // order, and with either not there once it reads nothing and says which
        [Fact]
        public void R402_R384_update_from_report_reads_the_level_and_report_excel_of_the_nbh_in_the_work_folder()
        {
            string work = Path.Combine(Path.GetTempPath(), "r384_named_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                string level = Path.Combine(work, SiteAdaptation.Common.Floors.LevelsRun.ExcelName("012", "parsons2024Rev6"));
                string report = Path.Combine(work, SiteAdaptation.Common.Report.ReportRun.ReportName("012"));
                File.WriteAllText(level, "x");
                File.WriteAllText(Path.Combine(work, SiteAdaptation.Common.Floors.LevelsRun.ExcelName("013", "parsons2024Rev6")), "x");
                string missing;
                Assert.Null(ButtonsRun.UpdateFiles(work, "012", out missing));
                Assert.Contains("012_Report.xlsx", missing);
                File.WriteAllText(report, "x");
                Assert.Equal(new[] { level, report }, ButtonsRun.UpdateFiles(work, "012", out missing));
                Assert.Null(missing);
                File.WriteAllText(Path.Combine(work, SiteAdaptation.Common.Floors.LevelsRun.ExcelName("012", "native2022")), "x");
                Assert.Null(ButtonsRun.UpdateFiles(work, "012", out missing));
                Assert.Contains("native2022", missing);
                Assert.Contains("parsons2024Rev6", missing);
            }
            finally
            {
                Directory.Delete(work, true);
            }
        }

        [Fact]
        public void R384_the_host_runs_a_named_command_by_buttons_run_of_its_name_and_answers_its_window_with_no_click()
        {
            string host = Text("dotnet", "SiteAdaptation.TestHost", "ButtonsRunHost.cs");
            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            Assert.Contains("buttons.GetMethod(AddinRunNames.Run, new[] { typeof(string), typeof(UIApplication) })", host);
            Assert.Contains("Put(AddinRunNames.UpdateFromReportBody, AddinRunNames.Dialog, (Func<IntPtr, IUpdateFromReportDialog>)(owner => new ReadNoApply(this)));", host);
            Assert.Contains("Put(AddinRunNames.RulesWindowBody, AddinRunNames.Dialog, (Action<IntPtr, ManualRuleTable, string>)((owner, rules, selected) => Opened(rules, selected)));", host);
            Assert.Contains("Put(AddinRunNames.UpdateFromReportBody, AddinRunNames.Dialog, updateBefore);", host);
            Assert.Contains("Put(AddinRunNames.RulesWindowBody, AddinRunNames.Dialog, rulesBefore);", host);
            Assert.Contains("return new UpdateFromReportAnswer { Apply = false };", host);
            Assert.Contains("if ($round.named) {", script);
            Assert.Contains("$roundJob.named = [string[]]@($round.named)", script);
        }

        [Fact]
        public void R384_buttons_2_and_4_wait_for_the_worker_and_a_step_the_next_needs_must_commit()
        {
            Assert.Equal(new[] { 2, 4 }, ButtonsRun.Known.Where(ButtonsRun.Waits));
            Assert.Equal("read", ButtonsRun.WorkerCommand(2));
            Assert.Equal("build", ButtonsRun.WorkerCommand(4));
            Assert.Null(ButtonsRun.WorkerCommand(7));
            Assert.True(ButtonsRun.GoesOn(2, ButtonOutcomes.Committed));
            Assert.False(ButtonsRun.GoesOn(2, ButtonOutcomes.Stopped));
            Assert.False(ButtonsRun.GoesOn(4, ButtonOutcomes.TimedOut));
            // 3 Review data saves nothing in the job, so its window ends Cancelled and the run goes on
            Assert.True(ButtonsRun.GoesOn(3, ButtonOutcomes.Cancelled));
            Assert.False(ButtonsRun.GoesOn(7, ButtonOutcomes.Cancelled));
        }

        [Fact]
        public void R404_a_prototype_loop_is_carried_as_the_plan_places_its_link_mirrored_turned_and_moved()
        {
            var loop = new List<double[]> { new[] { 0.0, 0.0 }, new[] { 2.0, 0.0 }, new[] { 2.0, 1.0 } };
            List<double[]> plain = BaseCorners.Carry(loop, new[] { 10.0, 20.0 }, 90, false);
            Assert.Equal(new[] { 10.0, 20.0 }, Round(plain[0]));
            Assert.Equal(new[] { 10.0, 22.0 }, Round(plain[1]));
            Assert.Equal(new[] { 9.0, 22.0 }, Round(plain[2]));
            // Mirrored flips the local Y before the turn, as the mirror plane of PlacementMath holds the turned X axis
            List<double[]> mirrored = BaseCorners.Carry(loop, new[] { 10.0, 20.0 }, 90, true);
            Assert.Equal(new[] { 11.0, 22.0 }, Round(mirrored[2]));
        }

        [Fact]
        public void R404_the_base_of_a_solid_is_held_corner_to_corner_in_mm_whatever_its_start_and_order()
        {
            var expected = new List<double[]> { new[] { 0.0, 0.0 }, new[] { 4.0, 0.0 }, new[] { 4.0, 3.0 }, new[] { 0.0, 3.0 } };
            var placed = new List<double[]> { new[] { 4.0, 3.0005 }, new[] { 0.0, 3.0 }, new[] { 0.0, 0.0 }, new[] { 4.0, 0.0 } };
            CornerMatch held = BaseCorners.Compare(placed, expected, 1.0);
            Assert.True(held.Held);
            Assert.Equal(4, held.Placed);
            Assert.Equal(4, held.Expected);
            Assert.Equal(0.5, held.MaxMm, 6);
            Assert.Equal(4, held.Within);

            var off = new List<double[]> { new[] { 0.0, 0.0 }, new[] { 4.0, 0.0 }, new[] { 4.0, 3.002 }, new[] { 0.0, 3.0 } };
            CornerMatch notHeld = BaseCorners.Compare(off, expected, 1.0);
            Assert.False(notHeld.Held);
            Assert.Equal(2.0, notHeld.MaxMm, 6);
            Assert.Equal(3, notHeld.Within);

            // A corner the prototype has and the solid lacks is not held, though every corner of the solid sits on one
            CornerMatch lacking = BaseCorners.Compare(expected.Take(3).ToList(), expected, 1.0);
            Assert.False(lacking.Held);
            Assert.Equal(3, lacking.Placed);
            Assert.False(BaseCorners.Compare(new List<double[]>(), expected, 1.0).Held);
        }

        [Fact]
        public void R377_a_link_is_held_against_the_plan_within_its_mm_and_degrees_with_its_mirror()
        {
            double turn = 30 * Math.PI / 180;
            double[] x = { Math.Cos(turn), Math.Sin(turn), 0 };
            double[] y = { -Math.Sin(turn), Math.Cos(turn), 0 };
            double[] z = { 0, 0, 1 };
            LinkMatch held = LinkCheck.Compare(new[] { 10.0005, 20.0, 0.0 }, x, y, z, new[] { 10.0, 20.0 }, 30, false, 1.0, 0.01);
            Assert.True(held.Held);
            Assert.Equal(0.5, held.OriginOffMm, 6);
            Assert.Equal(30, held.RotationDeg, 6);
            Assert.False(held.Mirrored);

            Assert.False(LinkCheck.Compare(new[] { 10.002, 20.0, 0.0 }, x, y, z, new[] { 10.0, 20.0 }, 30, false, 1.0, 0.01).Held);
            Assert.False(LinkCheck.Compare(new[] { 10.0, 20.0, 0.0 }, x, y, z, new[] { 10.0, 20.0 }, 30.02, false, 1.0, 0.01).Held);
            Assert.True(LinkCheck.Compare(new[] { 10.0, 20.0, 0.0 }, x, y, z, new[] { 10.0, 20.0 }, 390, false, 1.0, 0.01).Held);
            // A mirrored link keeps its turned X axis and flips its Y axis, so its basis is left handed
            double[] flipped = y.Select(value => -value).ToArray();
            LinkMatch mirrored = LinkCheck.Compare(new[] { 10.0, 20.0, 0.0 }, x, flipped, z, new[] { 10.0, 20.0 }, 30, true, 1.0, 0.01);
            Assert.True(mirrored.Mirrored);
            Assert.True(mirrored.Held);
            Assert.False(LinkCheck.Compare(new[] { 10.0, 20.0, 0.0 }, x, flipped, z, new[] { 10.0, 20.0 }, 30, false, 1.0, 0.01).Held);
        }

        [Fact]
        public void R384_an_armed_add_in_dialog_takes_its_first_link_and_every_other_keeps_its_usual_answers()
        {
            var armed = new[] { "7 Place villas", "Nothing changes until Apply." };
            Assert.Equal(new[] { DialogAnswers.FirstLink, DialogAnswers.Close, DialogAnswers.Cancel, DialogAnswers.Ok },
                DialogAnswers.For("taskDialog", "Custom", "7 Place villas: 12 plots. Nothing changes until Apply.", armed));
            Assert.Equal(DialogAnswers.For("taskDialog", "Custom"), DialogAnswers.For("taskDialog", "Custom", "a link of Revit was not found", armed));
            Assert.Equal(DialogAnswers.For("taskDialog", "Custom"), DialogAnswers.For("taskDialog", "Custom", "7 Place villas", null));
            Assert.Equal(DialogAnswers.For("messageBox", null), DialogAnswers.For("messageBox", null, "7 Place villas", armed));
            // A dialog of Revit the list knows keeps its own answer even when its text holds an armed one
            Assert.Equal(DialogAnswers.For("taskDialog", "TaskDialog_Save_File"), DialogAnswers.For("taskDialog", "TaskDialog_Save_File", "7 Place villas", armed));
        }

        [Fact]
        public void R384_the_session_runs_the_buttons_by_the_ribbon_entry_and_waits_for_the_worker_end_between_ticks()
        {
            string session = Text("dotnet", "SiteAdaptation.TestHost", "AddinRunSession.cs");
            string host = Text("dotnet", "SiteAdaptation.TestHost", "ButtonsRunHost.cs");
            string jobSession = Text("dotnet", "SiteAdaptation.TestHost", "JobSession.cs");

            Assert.Contains("buttons = job.Buttons != null ? new ButtonsRunHost(job, runner) : null;", session);
            Assert.Contains("lastOk = runner.Run(buttons.Phase(app, probe)) && lastOk;", session);
            Assert.Contains("schedule.Wait(DateTime.UtcNow, AnswerMinSeconds, buttons.WaitSeconds.Value);", session);
            Assert.Contains("return buttons.Pending(probe);", session);
            Assert.Contains("buttons.WaitEnded(ended.Value, seconds);", session);
            Assert.Contains("buttons.GetMethod(AddinRunNames.Run, new[] { typeof(int), typeof(UIApplication) })", host);
            Assert.Contains("DialogAnswers.Armed = Armed(step);", host);
            Assert.Contains("DialogAnswers.Armed = null;", host);
            Assert.Contains("JsonOut.Write(Path.Combine(folder, ButtonsRun.ResultFile), result);", host);
            // The windows and the run state of the add-in are put back at the end of the run
            Assert.Contains("Put(AddinRunNames.Buttons, AddinRunNames.State, stateBefore);", host);
            Assert.Contains("DialogAnswers.Answer(kind, e.DialogId, message, code => e.OverrideResult(code));", jobSession);
        }

        [Fact]
        public void R384_the_run_script_writes_the_settings_and_pane_settings_and_hashes_every_real_input_before_and_after()
        {
            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            int branch = script.IndexOf("        'addinRun' {", StringComparison.Ordinal);
            int buttons = script.IndexOf("            if ($round.buttons) {", branch, StringComparison.Ordinal);
            Assert.True(branch > 0 && buttons > branch);
            Assert.Contains("$roundJob.buttons = [int[]]@($round.buttons)", script);
            Assert.Contains("$roundJob.paneSettings = $paneSettingsPath", script);
            Assert.Contains("$paneSettings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $paneSettingsPath -Encoding utf8", script);
            Assert.Contains("$buttonsInputs = Get-ButtonsInputs", script);
            Assert.Contains("$buttonsBefore = Get-InputHashes $buttonsInputs", script);
            Assert.Contains("$buttonsAfter = Get-InputHashes $buttonsInputs", script);
            Assert.Contains("$stopReason = 'a real input of the buttons run changed (D19): ' + ($buttonsChanged -join ', ')", script);
            Assert.True(script.IndexOf("$stopReason = 'a real input of the buttons run changed (D19)", StringComparison.Ordinal)
                < script.IndexOf("if (-not $DryStart -and ($stopReason -or $logResult -ne 'ok')) {", StringComparison.Ordinal));
        }

        // R375. Button 2 stops when the layer list of addin.readData.cadLayers is not beside the config of the run, which the Revit run
        // after 5c met on its first input check
        [Fact]
        public void R375_the_run_script_copies_the_layer_list_beside_the_config_of_a_buttons_run()
        {
            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            int buttons = script.IndexOf("            if ($round.buttons) {", StringComparison.Ordinal);
            int copy = script.IndexOf("$layersName = [string]$settingsCopy.addin.readData.cadLayers", buttons, StringComparison.Ordinal);
            int pane = script.IndexOf("$paneSettingsPath = Join-Path $runFolder 'pane_settings.json'", buttons, StringComparison.Ordinal);
            Assert.True(buttons > 0 && copy > buttons && pane > copy);
            Assert.Contains("Copy-Item -LiteralPath (Join-Path $repo (Join-Path 'config' $layersName)) -Destination (Join-Path (Split-Path $settingsPath) $layersName)", script);
        }

        // R376. 4 Check stops when floor_outlines.json and prototype_offsets.json are not beside the settings file, and with no
        // addin.overrideFile it takes the override list from overrides beside it, where Save overrides writes. The copy takes the
        // config folder along, so a Save writes the copy of the run and never the repo
        [Fact]
        public void R376_the_run_script_copies_the_config_folder_and_its_override_list_beside_the_config_of_a_buttons_run()
        {
            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            int buttons = script.IndexOf("            if ($round.buttons) {", StringComparison.Ordinal);
            int written = script.IndexOf("$settingsCopy | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $settingsPath", buttons, StringComparison.Ordinal);
            int files = script.IndexOf("Get-ChildItem -LiteralPath (Join-Path $repo 'config') -File -Filter '*.json' | Where-Object { $_.Name -ne 'project.json' -and $_.Name -ne $layersName } | Copy-Item -Destination (Split-Path $settingsPath)", buttons, StringComparison.Ordinal);
            int overrides = script.IndexOf("Copy-Item -LiteralPath (Join-Path $repo 'config\\overrides') -Destination (Split-Path $settingsPath) -Recurse", buttons, StringComparison.Ordinal);
            int pane = script.IndexOf("$paneSettingsPath = Join-Path $runFolder 'pane_settings.json'", buttons, StringComparison.Ordinal);
            Assert.True(buttons > 0 && files > written && overrides > written && pane > files && pane > overrides);
        }

        // R375 and D19. The work folder of a buttons run lies in its run folder, so a data book folder of config with no full path
        // would point under it. The copy names the data book of the work root in full and hashes its files as real inputs
        [Fact]
        public void R375_the_run_script_names_the_data_book_of_the_work_root_in_full_and_hashes_its_files()
        {
            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            int buttons = script.IndexOf("            if ($round.buttons) {", StringComparison.Ordinal);
            int full = script.IndexOf("$settingsCopy.addin.readData.dataBookFolder = $bookFolder", buttons, StringComparison.Ordinal);
            int sources = script.IndexOf("$buttonsSources += Join-Path $bookFolder ([string]$bookFile.Value)", buttons, StringComparison.Ordinal);
            int hashed = script.IndexOf("$buttonsBefore = Get-InputHashes $buttonsInputs", buttons, StringComparison.Ordinal);
            int written = script.IndexOf("$settingsCopy | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $settingsPath", buttons, StringComparison.Ordinal);
            Assert.True(buttons > 0 && full > buttons && sources > full && hashed > sources && written > full);
            Assert.Contains("$bookFolder = Join-Path $docs $bookFolder", script);
        }

        // R357 and D19. The build of 4 Check reads pastJob.upgradedPrototypeFolder under the input root, which a buttons run never
        // writes. With a prototypes input the copy of config names that folder in full, so the build reads the folder of the spec
        [Fact]
        public void R357_the_run_script_names_the_prototypes_input_as_the_prototype_folder_of_the_build()
        {
            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            int buttons = script.IndexOf("            if ($round.buttons) {", StringComparison.Ordinal);
            int named = script.IndexOf("$settingsCopy.pastJob.upgradedPrototypeFolder = [string]$round.inputs.prototypes", buttons, StringComparison.Ordinal);
            int written = script.IndexOf("$settingsCopy | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $settingsPath", buttons, StringComparison.Ordinal);
            Assert.True(buttons > 0 && named > buttons && written > named);
            Assert.Contains("if ($round.inputs.prototypes) {", script);
        }

        // R377. Light mode reads the base of each prototype once into base_shapes of the work folder, keyed by the SHA-256 of the
        // file, and a 2021 prototype takes about 100 s to open in Revit 2024. A buttons run may start from the shapes an earlier run
        // read, copied in before Revit starts, so its work folder holds them as a second run of the add-in would
        [Fact]
        public void R377_the_run_script_copies_the_base_shapes_of_the_spec_into_the_work_folder_before_revit_starts()
        {
            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            int buttons = script.IndexOf("            if ($round.buttons) {", StringComparison.Ordinal);
            int shapes = script.IndexOf("if ($round.baseShapes) {", buttons, StringComparison.Ordinal);
            int copy = script.IndexOf("Get-ChildItem -LiteralPath ([string]$round.baseShapes) -File -Filter '*.json' | Copy-Item -Destination $shapesFolder", buttons, StringComparison.Ordinal);
            int job = script.IndexOf("$roundJob.paneSettings = $paneSettingsPath", buttons, StringComparison.Ordinal);
            Assert.True(buttons > 0 && shapes > buttons && copy > shapes && job > copy);
            Assert.Contains("$shapesFolder = Join-Path $runFolder 'work\\buttons\\base_shapes'", script);
        }

        // D3 and D19. The Revit run after 5c opened each original prototype under the input root for the corner compare after 7, and
        // each 2021 prototype took about 100 s to upgrade, so 13 of them ran the step past its 20 minutes twice. The host opens a copy
        [Fact]
        public void D19_the_host_opens_a_copy_of_each_prototype_detached_and_never_the_path_it_is_given()
        {
            string host = Text("dotnet", "SiteAdaptation.TestHost", "ButtonsRunHost.cs");
            Assert.DoesNotContain("OpenDocumentFile(path", host);
            Assert.DoesNotContain("ui.Application.Documents", host);
            string open = "ui.Application.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(copy), new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets });";
            Assert.Equal(1, Count(host, "OpenDocumentFile("));
            Assert.Equal(1, Count(host, open));
            int copies = host.IndexOf("string copies = Path.Combine(work, \"copies\");", StringComparison.Ordinal);
            int copied = host.IndexOf("File.Copy(path, copy);", StringComparison.Ordinal);
            int opened = host.IndexOf(open, StringComparison.Ordinal);
            int final = host.IndexOf("finally", opened, StringComparison.Ordinal);
            int closed = host.IndexOf("prototype?.Close(false);", opened, StringComparison.Ordinal);
            int deleted = host.IndexOf("File.Delete(copy);", opened, StringComparison.Ordinal);
            Assert.True(copies > 0 && copied > copies && opened > copied && final > opened && closed > final && deleted > closed);
        }

        // R404. The loops of the host stay its own read, the sketch of the lowest floor, apart from ShapeCache of the add-in, and come
        // through the loop file of the job, loaded at the start of the corner compare
        [Fact]
        public void R404_the_corner_compare_loads_the_loop_file_at_its_start_and_reads_each_loop_through_it()
        {
            string host = Text("dotnet", "SiteAdaptation.TestHost", "ButtonsRunHost.cs");
            int masses = host.IndexOf("if (step.VillaMode == VillaModes.Masses)", StringComparison.Ordinal);
            int loaded = host.IndexOf("hostLoops = HostLoopCache.Load(LoopsFile, FileHash.Sha256);", masses, StringComparison.Ordinal);
            int solids = host.IndexOf("foreach (string id in solids)", masses, StringComparison.Ordinal);
            Assert.True(masses > 0 && loaded > masses && solids > loaded);
            Assert.Contains("return hostLoops.Get(path, ReadCopy);", host);
            Assert.Contains("get { return job.PrototypeLoops ?? Path.Combine(work, HostLoopCache.DefaultFile); }", host);
            Assert.DoesNotContain("ShapeCache", host);
        }

        [Fact]
        public void R404_the_host_reads_a_prototype_loop_once_per_file_hash_and_keeps_it_across_runs()
        {
            string folder = TempFolder();
            try
            {
                string a = Put(folder, "A.rvt", "alpha");
                string twin = Put(folder, "A twin.rvt", "alpha");
                string b = Put(folder, "B.rvt", "beta");
                string file = Path.Combine(folder, "loops", "prototype_loops.json");
                var reads = new List<string>();
                Func<string, List<double[]>> read = path =>
                {
                    reads.Add(Path.GetFileName(path));
                    return Square(path == b ? 2 : 1);
                };
                HostLoopCache first = HostLoopCache.Load(file, FileHash.Sha256);
                Assert.Equal(0, first.AtStart);
                List<double[]> loop = first.Get(a, read);
                first.Get(a, read);
                first.Get(twin, read);
                Assert.Equal(new[] { "A.rvt" }, reads);
                Assert.Equal(1, first.Read);

                HostLoopCache second = HostLoopCache.Load(file, FileHash.Sha256);
                Assert.Equal(1, second.AtStart);
                Assert.Equal(loop, second.Get(a, read));
                Assert.Equal(Square(2), second.Get(b, read));
                Assert.Equal(new[] { "A.rvt", "B.rvt" }, reads);

                JObject json = JObject.Parse(File.ReadAllText(file));
                Assert.Equal(HostLoopCache.Format, (string)json["format"]);
                Assert.Equal(HostLoopCache.ReaderVersion, (int)json["readerVersion"]);
                Assert.Equal(new[] { FileHash.Sha256(a), FileHash.Sha256(b) }, json["loops"].Select(entry => (string)entry["sha256"]));
                Assert.Equal(4, json["loops"][1]["points"].Count());
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void R404_the_host_writes_its_loop_file_after_each_new_loop_so_a_step_cut_short_keeps_what_it_read()
        {
            string folder = TempFolder();
            try
            {
                string a = Put(folder, "A.rvt", "alpha");
                string b = Put(folder, "B.rvt", "beta");
                string c = Put(folder, "C.rvt", "gamma");
                string file = Path.Combine(folder, "prototype_loops.json");
                HostLoopCache cache = HostLoopCache.Load(file, FileHash.Sha256);
                cache.Get(a, path => Square(1));
                cache.Get(b, path =>
                {
                    Assert.Equal(1, HostLoopCache.Load(file, FileHash.Sha256).AtStart);
                    return Square(2);
                });
                Assert.Equal(2, HostLoopCache.Load(file, FileHash.Sha256).AtStart);
                // A read that fails writes nothing and keeps the loops before it
                Assert.Throws<InvalidDataException>(() => cache.Get(c, path => throw new InvalidDataException("no floor")));
                Assert.Equal(2, HostLoopCache.Load(file, FileHash.Sha256).AtStart);
                Assert.Equal(new[] { "prototype_loops.json" }, Directory.GetFiles(folder, "prototype_loops*").Select(Path.GetFileName));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Theory]
        [InlineData("{\"format\": \"another\", \"readerVersion\": 1, \"loops\": [{\"sha256\": \"SHA\", \"points\": [[0, 0], [1, 0], [1, 1]]}]}")]
        [InlineData("{\"format\": \"site-adaptation-host-loops\", \"readerVersion\": 0, \"loops\": [{\"sha256\": \"SHA\", \"points\": [[0, 0], [1, 0], [1, 1]]}]}")]
        [InlineData("{\"format\": \"site-adaptation-host-loops\", \"readerVersion\": 1, \"loops\": [{\"sha256\": \"SHA\", \"points\": [[0, 0], [1, 0]]}]}")]
        [InlineData("{\"format\": \"site-adaptation-host-loops\", \"readerVersion\": 1, \"loops\": [{\"sha256\": \"SHA\", \"points\": \"none\"}]}")]
        [InlineData("not json")]
        public void R404_a_loop_file_of_another_format_or_reader_version_or_that_does_not_read_is_read_again(string text)
        {
            string folder = TempFolder();
            try
            {
                string a = Put(folder, "A.rvt", "alpha");
                string file = Path.Combine(folder, "prototype_loops.json");
                File.WriteAllText(file, text.Replace("SHA", FileHash.Sha256(a)));
                HostLoopCache cache = HostLoopCache.Load(file, FileHash.Sha256);
                Assert.Equal(0, cache.AtStart);
                int reads = 0;
                cache.Get(a, path =>
                {
                    reads++;
                    return Square(1);
                });
                Assert.Equal(1, reads);
                Assert.Equal(1, HostLoopCache.Load(file, FileHash.Sha256).AtStart);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void D19_a_prototype_that_changes_while_its_loop_is_read_stops_the_read_and_is_not_kept()
        {
            string folder = TempFolder();
            try
            {
                string a = Put(folder, "A.rvt", "alpha");
                string file = Path.Combine(folder, "prototype_loops.json");
                HostLoopCache cache = HostLoopCache.Load(file, FileHash.Sha256);
                IOException changed = Assert.Throws<IOException>(() => cache.Get(a, path =>
                {
                    File.WriteAllText(path, "changed");
                    return Square(1);
                }));
                Assert.Contains("D19", changed.Message);
                Assert.False(File.Exists(file));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        // R384. The loop file is a spec field the run script checks under the work root and passes to the job like the other fields
        // of buttons
        [Fact]
        public void R384_the_prototype_loops_field_of_the_spec_reaches_the_job_by_its_full_json_path()
        {
            string full = Path.Combine(Path.GetTempPath(), "prototype_loops.json");
            Assert.Equal(full, Read(WithLoops(full)).PrototypeLoops);
            Assert.Null(ButtonsRun.Refusal(Read(WithLoops(full))));
            Assert.Null(Read(Job).PrototypeLoops);
            Assert.Contains("prototypeLoops", ButtonsRun.Refusal(Read(WithLoops("prototype_loops.json"))));
            Assert.Contains("prototypeLoops", ButtonsRun.Refusal(Read(WithLoops(Path.Combine(Path.GetTempPath(), "prototype_loops.txt")))));

            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            int buttons = script.IndexOf("            if ($round.buttons) {", StringComparison.Ordinal);
            int field = script.IndexOf("if ($round.prototypeLoops) {", buttons, StringComparison.Ordinal);
            int under = script.IndexOf("-not $loopsPath.StartsWith($docs + '\\')", field, StringComparison.Ordinal);
            int job = script.IndexOf("$roundJob.prototypeLoops = $loopsPath", field, StringComparison.Ordinal);
            int window = script.IndexOf("$addinRunWindowMinutes = 31", buttons, StringComparison.Ordinal);
            Assert.True(buttons > 0 && field > buttons && under > field && job > under && window > job);
        }

        private static string WithLoops(string path)
        {
            JObject job = JObject.Parse(Job);
            job["prototypeLoops"] = path;
            return job.ToString();
        }

        private static List<double[]> Square(double side)
        {
            return new List<double[]> { new[] { 0.0, 0.0 }, new[] { side, 0.0 }, new[] { side, side }, new[] { 0.0, side } };
        }

        private static string TempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "host-loops-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string Put(string folder, string name, string text)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text);
            return path;
        }

        private static int Count(string text, string part)
        {
            int count = 0;
            for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
            {
                count++;
            }
            return count;
        }

        private static double[] Round(double[] point)
        {
            return point.Select(value => Math.Round(value, 9)).ToArray();
        }

        // 6a. shots of a buttons job: a PNG of each window the host answers, of the pane and of the ribbon, under shots of the run folder
        [Fact]
        public void R384_a_shots_job_carries_the_field_and_shots_with_no_buttons_is_refused()
        {
            TestHostJob job = Read(Job.Replace("\"paneSettings\"", "\"shots\": true, \"paneSettings\""));
            Assert.True(job.Shots);
            Assert.Null(ButtonsRun.Refusal(job));
            Assert.Null(Read(Job).Shots);
            Assert.Contains("shots", ButtonsRun.Refusal(Read("{\"kind\": \"addinRun\", \"shots\": true}")));
        }

        [Fact]
        public void R384_each_shot_is_a_png_under_shots_of_the_run_folder_named_by_its_step_and_window()
        {
            string run = Path.Combine(Path.GetTempPath(), "run");
            Assert.Equal(Path.Combine(run, "shots", "button1-setup-site.png"), ButtonShots.File(run, "button1", ButtonShots.SetUpSite));
            Assert.Equal(Path.Combine(run, "shots", "end-ribbon.png"), ButtonShots.File(run, ButtonShots.End, ButtonShots.Ribbon));
            Assert.Equal(Path.Combine(run, "shots", "SiteAdaptationRulesCommand-rules.png"), ButtonShots.File(run, ButtonsRun.RulesCommand, ButtonShots.Rules));
        }

        [Fact]
        public void R384_the_pane_is_taken_after_button_1_only_and_once_more_at_the_end()
        {
            List<ButtonsStep> steps = ButtonsRun.Steps(Read(Job.Replace("\"paneSettings\"", "\"named\": [\"SiteAdaptationRulesCommand\"], \"paneSettings\"")));
            Assert.Equal(new[] { "button1" }, steps.Where(ButtonShots.PaneAfter).Select(step => step.Name));
            Assert.DoesNotContain(ButtonsRun.Steps(Read(Job.Replace("[1, 2, 3, 4, 7]", "[2, 3, 4, 7]"))), ButtonShots.PaneAfter);
        }

        // The host side by text, as Revit runs it: each window is drawn before the host answers it, the pane and the ribbon by RevitShots,
        // New project and How it works at the end, and nothing of the screen or of another process
        [Fact]
        public void R384_the_host_draws_each_window_before_its_answer_and_grabs_no_screen()
        {
            string host = Text("dotnet", "SiteAdaptation.TestHost", "ButtonsRunHost.cs");
            foreach (string window in new[] { "SetUpSite", "ReviewData", "CheckTable", "Rules", "UpdateFromReport", "NewProject", "HowItWorks", "Pane", "Ribbon" })
            {
                Assert.Contains("ButtonShots." + window, host);
            }
            Assert.Contains("RevitShots.Pane(", host);
            Assert.Contains("RevitShots.Window(", host);
            Assert.Contains("job.Shots == true", host);
            int setUp = host.IndexOf("ButtonShots.SetUpSite", StringComparison.Ordinal);
            Assert.True(setUp < host.IndexOf("new SetUpSiteRun.AnswerWindow()", StringComparison.Ordinal));
            Assert.DoesNotContain("CopyFromScreen", host);
            Assert.DoesNotContain("GetDesktopWindow", host);
            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            Assert.Contains("if ($round.shots -eq $true) {\n                    $roundJob.shots = $true", script);
        }
    }
}
