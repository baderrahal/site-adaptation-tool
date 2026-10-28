using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Pane;
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
                ""inputs"": { ""baseplan"": { ""path"": ""C:\\Job\\a.dwg"", ""size"": 12, ""sha256"": ""ab"" }, ""prototypes"": { ""path"": ""C:\\Job\\p"" } },
                ""scope"": { ""kind"": ""plots"", ""plots"": ""2290-2299, 2311"" }, ""dryRunFirst"": false, ""villaMode"": ""links"" }");
            Assert.Equal(1, full.FormatVersion);
            Assert.Equal(@"C:\Job\project.json", full.ConfigFile);
            Assert.Equal(@"C:\Job\rules.json", full.RulesFile);
            Assert.Equal("native2022", full.LevelProfile);
            Assert.Equal(12L, full.Inputs[InputKeys.Baseplan].Size);
            Assert.Equal("ab", full.Inputs[InputKeys.Baseplan].Sha256);
            Assert.Null(full.Inputs[InputKeys.Prototypes].Size);
            Assert.Equal(ScopeKinds.Plots, full.Scope.Kind);
            Assert.Equal("2290-2299, 2311", full.Scope.Plots);
            Assert.False(full.DryRunFirst);
            Assert.Equal(VillaModes.Links, full.VillaMode);
            Assert.True(full.Chosen);

            PaneSettings small = SettingsFile.Read("{\"formatVersion\": 1}");
            Assert.False(small.Chosen);
            Assert.True(small.DryRunFirst);
            Assert.Equal(VillaModes.Masses, small.VillaMode);
            Assert.Equal(ScopeKinds.Project, small.Scope.Kind);
            Assert.Empty(small.Inputs);
            Assert.True(new PaneSettings().DryRunFirst);
            Assert.Equal(VillaModes.Masses, new PaneSettings().VillaMode);
        }

        [Fact]
        public void R372_the_settings_reader_refuses_a_later_version_with_both_named_and_a_value_it_does_not_know()
        {
            Assert.Equal("the settings file is formatVersion 2, this add-in reads up to 1", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{\"formatVersion\": 2}")).Message);
            Assert.Contains("no whole formatVersion", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{}")).Message);
            Assert.Contains("villaMode boxes", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{\"formatVersion\": 1, \"villaMode\": \"boxes\"}")).Message);
            Assert.Contains("an input roads", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{\"formatVersion\": 1, \"inputs\": {\"roads\": {}}}")).Message);
            Assert.Contains("scope zone", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("{\"formatVersion\": 1, \"scope\": {\"kind\": \"zone\"}}")).Message);
            Assert.Contains("no JSON object", Assert.Throws<InvalidDataException>(() => SettingsFile.Read("not json")).Message);
        }

        [Fact]
        public void R372_with_no_settings_file_the_pane_shows_the_first_open_state()
        {
            PaneViewModel model = Model(new FakeProbe());
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));

            Assert.True(model.FirstOpen);
            Assert.Equal(new[] { "Start here", "Choose the settings file", PaneText.StartLine, "Choose settings file" }, new[] { model.BoxLabel, model.BoxName, model.BoxLine, model.BoxButton });
            Assert.True(model.BoxButtonShown);
            Assert.True(model.BoxCommand.CanExecute(null));
            Assert.Equal("Not chosen", model.SettingsRow.FileName);
            Assert.True(model.SettingsRow.Shown);
            Assert.False(model.SettingsRow.Found);
            Assert.Equal("The drawings and schedules the tool reads. 0 of 7 found.", model.InputHelp);
            Assert.Equal(new[] { "Needs settings", "Needs step 1", "Needs step 2", "Needs step 3", "Needs step 4", "Needs step 5", "Needs step 6", "Needs step 3" }, model.Steps.Select(step => step.ChipText));
            Assert.All(model.Steps, step => Assert.Equal(MarkKind.Waiting, step.Mark));
            int asked = 0;
            model.ChooseSettingsRequested += () => asked++;
            model.BoxCommand.Execute(null);
            Assert.Equal(1, asked);
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
            Assert.Equal(new[] { "Ready", "Needs step 1", "Needs step 2", "Needs step 3", "Needs step 4", "Needs step 5", "Needs step 6", "Needs step 3" }, model.Steps.Select(step => step.ChipText));
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
        public void R372_the_step_lines_of_5a_follow_the_order_of_03a_and_8_needs_3()
        {
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 3 }, Enumerable.Range(1, 8).Select(StepPlan.Needs));
            Assert.Throws<ArgumentOutOfRangeException>(() => StepPlan.Needs(9));
            Assert.Equal(StepKind.NeedsSettings, StepPlan.Before5c(false)[0].Kind);
            Assert.Equal(StepKind.Ready, StepPlan.Before5c(true)[0].Kind);
            Assert.Equal(1, StepPlan.Next(StepPlan.Before5c(true)));
            Assert.Equal(0, StepPlan.Next(StepPlan.Before5c(false)));
        }

        [Fact]
        public void R372_the_steps_of_the_mock_mark_done_issues_the_next_step_and_waiting_with_the_short_chip_and_its_tooltip()
        {
            PaneViewModel model = Model(new FakeProbe());
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.LoadSettings(new PaneSettings { ConfigFile = Folder + "project.json" });
            model.SetSteps(new[]
            {
                new StepStatus(StepKind.Done), new StepStatus(StepKind.Done), new StepStatus(StepKind.Issues, 1247), new StepStatus(StepKind.Ready),
                new StepStatus(StepKind.NeedsStep, 4), new StepStatus(StepKind.NeedsStep, 5), new StepStatus(StepKind.NeedsStep, 6), new StepStatus(StepKind.Ready),
            });

            Assert.Equal(new[] { "Done", "Done", "1,247 issues", "Ready", "Needs step 4", "Needs step 5", "Needs step 6", "Ready" }, model.Steps.Select(step => step.ChipText));
            Assert.Equal("Done with 1,247 issues", model.Steps[2].ChipTip);
            Assert.Null(model.Steps[3].ChipTip);
            Assert.Equal(new[] { MarkKind.Done, MarkKind.Done, MarkKind.Issues, MarkKind.Next, MarkKind.Waiting, MarkKind.Waiting, MarkKind.Waiting, MarkKind.Waiting }, model.Steps.Select(step => step.Mark));
            Assert.Equal("Run 4 Place villas", model.BoxButton);
            Assert.Equal("Places a villa on each plot. Dry run is on.", model.BoxLine);
            Assert.Equal(new[] { "Set up site", "Read data", "Check", "Place villas", "Floors and ramps", "Levels", "Walls", "Report" }, model.Steps.Select(step => step.Name));
            Assert.Equal(PaneText.StepLines, model.Steps.Select(step => step.Line));
            Assert.Throws<ArgumentException>(() => model.SetSteps(new StepStatus[7]));
        }

        [Fact]
        public void R372_each_row_shows_its_mark_the_file_name_the_size_the_short_hash_and_the_full_path_as_tooltip()
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

            model.PickInput(plots, new[] { Folder + "plots.pdf" });
            Assert.True(plots.Found);
            Assert.Equal("plots.pdf", plots.FileName);
            Assert.Equal("2.91 MiB 7061ac0d", plots.Detail);
            Assert.Equal(Folder + "plots.pdf", plots.ToolTip);
            Assert.Equal(3056293L, model.Settings.Inputs[InputKeys.PlotNumbers].Size);
            Assert.StartsWith("7061ac0d", model.Settings.Inputs[InputKeys.PlotNumbers].Sha256);
            Assert.True(model.RulesRow.Found);
            Assert.Equal("0.00 MiB 205000fc", model.RulesRow.Detail);
            Assert.False(model.SettingsRow.Found);
            Assert.Equal("project.json", model.SettingsRow.FileName);
            Assert.Equal(string.Empty, model.SettingsRow.Detail);
            Assert.Equal("The drawings and schedules the tool reads. 1 of 7 found.", model.InputHelp);

            model.PickInput(dlus, new[] { Folder + "dlus.xlsx" });
            Assert.False(dlus.Found);
            Assert.Equal("dlus.xlsx", dlus.FileName);
            model.PickInput(dlus, new[] { Folder + "dlus.xlsx", Folder + "sales.xlsx" });
            Assert.False(dlus.Found);
            Assert.Equal("dlus.xlsx, sales.xlsx", dlus.FileName);
            probe.Add(Folder + "sales.xlsx", 2097152, "bbbbbbbb" + new string('3', 56));
            model.PickInput(dlus, new[] { Folder + "dlus.xlsx", Folder + "sales.xlsx" });
            Assert.True(dlus.Found);
            Assert.Equal("1.00 MiB aaaaaaaa, 2.00 MiB bbbbbbbb", dlus.Detail);
            Assert.Equal(Folder + "dlus.xlsx" + Environment.NewLine + Folder + "sales.xlsx", dlus.ToolTip);
            Assert.Throws<ArgumentException>(() => model.PickInput(plots, new[] { "a", "b" }));
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
            Assert.Equal("The drawings and schedules the tool reads. All 7 found.", model.InputHelp);
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
            model.PickInput(baseplan, new[] { Folder + "a.dwg" });
            Assert.Equal("1.00 MiB", baseplan.Detail);
            Assert.Single(probe.Waiting);

            model.Load(new DocumentSession("two", Folder + "Two.rvt", "Two"));
            probe.Finish();
            Assert.Equal("Not chosen", model.InputRows[0].FileName);
            model.Load(first);
            Assert.Equal("1.00 MiB 12345678", model.InputRows[0].Detail);
            Assert.Empty(probe.Waiting);
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
            model.PickInput(model.InputRows[3], new[] { Folder + "landxml" });

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

            Assert.Equal("Save the project first, its work folder is named by its file", WorkFolder.Stop(@"D:\Root", string.Empty, null));
            Assert.Null(WorkFolder.Stop(@"D:\Root", @"C:\Work\Site\Model A.rvt", @"C:\02-Site Adaptation"));
            Assert.Contains("lies inside inputRoot", WorkFolder.Stop(@"C:\02-Site Adaptation\Work", @"C:\Work\Site\Model A.rvt", @"C:\02-site adaptation\"));
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

                var probe = new DiskProbe();
                InputFacts facts = probe.Look(folder, true);
                Assert.True(facts.Found);
                Assert.Equal(6L, facts.Size);
                Assert.False(probe.Look(Path.Combine(root, "none.txt"), false).Found);
                Assert.Equal(3L, probe.Look(file, false).Size);

                int caller = Thread.CurrentThread.ManagedThreadId;
                int worker = 0;
                var context = new RecordingContext();
                SynchronizationContext previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(context);
                string hash = null;
                try
                {
                    probe.Hash(file, false, done => hash = done);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }
                Assert.True(context.Posted.Wait(TimeSpan.FromSeconds(10)));
                worker = context.PostedFrom;
                Assert.NotEqual(caller, worker);
                Assert.Null(hash);
                context.RunPosted();
                Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
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

        private static PaneViewModel Model(IInputProbe probe)
        {
            return new PaneViewModel(probe);
        }

        private sealed class FakeProbe : IInputProbe
        {
            private readonly Dictionary<string, Tuple<long, string>> files = new Dictionary<string, Tuple<long, string>>(StringComparer.OrdinalIgnoreCase);

            public bool Later { get; set; }
            public List<Action> Waiting { get; } = new List<Action>();
            public IReadOnlyList<string> Profiles { get; set; } = new string[0];

            public void Add(string path, long size, string sha256)
            {
                files[path] = Tuple.Create(size, sha256);
            }

            public InputFacts Look(string path, bool folder)
            {
                return files.TryGetValue(path, out Tuple<long, string> file) ? new InputFacts { Found = true, Size = file.Item1, Stamp = 1 } : new InputFacts();
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

            public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
            {
                fallback = string.IsNullOrEmpty(rulesFile) ? null : Profiles.FirstOrDefault();
                return string.IsNullOrEmpty(rulesFile) ? new string[0] : Profiles;
            }
        }

        private sealed class RecordingContext : SynchronizationContext
        {
            private SendOrPostCallback callback;
            private object state;

            public ManualResetEventSlim Posted { get; } = new ManualResetEventSlim();
            public int PostedFrom { get; private set; }

            public override void Post(SendOrPostCallback d, object s)
            {
                callback = d;
                state = s;
                PostedFrom = Thread.CurrentThread.ManagedThreadId;
                Posted.Set();
            }

            public void RunPosted()
            {
                callback(state);
            }
        }
    }
}
