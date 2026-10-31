using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R376. Button 4 Check as a body on made up files, the worker a stand-in that writes the checkRows step, no real input
    public sealed class R376_CheckRunTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "check-run-" + Guid.NewGuid().ToString("N"));
        private readonly string work, configPath, prototypes;
        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string> { ["Settings"] = CheckRun.Config, ["Prototypes"] = CheckRun.Prototypes };

        private const string Overrides = "{\n  \"note\": \"made up\",\n  \"overrides\": [\n"
            + "    {\"plot\": \"1\", \"field\": \"heightM\", \"value\": {\"gapToRuleM\": 0.05}, \"source\": \"parsons\", \"model\": \"kx-site.rvt\", \"key\": \"kx\"}\n  ]\n}\n";

        public R376_CheckRunTests()
        {
            work = Path.Combine(root, "work");
            string config = Path.Combine(root, "repo", "config");
            prototypes = Path.Combine(root, "prototypes");
            Directory.CreateDirectory(work);
            Directory.CreateDirectory(Path.Combine(config, "overrides"));
            Directory.CreateDirectory(Path.Combine(root, "repo", "rules"));
            Directory.CreateDirectory(prototypes);
            configPath = Path.Combine(config, "project.json");
            var settings = new JObject
            {
                ["profile"] = "rules/made-up.json",
                ["majority"] = JObject.Parse("{ 'groups': { 'native': ['n1'], 'parsons': ['kx'] }, 'overrideDefaultSources': ['parsons'], 'linkPlotParameter': 'Plot' }"),
                ["addin"] = JObject.Parse("{ 'workerTimeLimitS': { 'build': 60 } }"),
                ["checkRows"] = R376_CheckTableTests.Block(),
            };
            File.WriteAllText(configPath, settings.ToString());
            File.WriteAllText(Path.Combine(config, "floor_outlines.json"), "{}");
            File.WriteAllText(Path.Combine(config, "prototype_offsets.json"), "{}");
            File.WriteAllText(Path.Combine(config, "overrides", "made-up.json"), Overrides);
            File.WriteAllText(Path.Combine(root, "repo", "rules", "made-up.json"), "{ 'levelProfiles': { 'default': 'p', 'p': { 'job': 'parsons', 'frontRoad': 1, 'rampSlopePercent': 1, 'rampTypes': {}, 'height': 1 } } }");
            File.WriteAllText(Path.Combine(prototypes, "P-ARC-VL4-A.rvt"), "made up");
            File.WriteAllText(Path.Combine(work, "site.json"), "{}");
            string plots = Path.Combine(root, "plots.csv"), labels = Path.Combine(root, "labels.csv");
            File.WriteAllText(plots, "locationPlanNumber\n1\n");
            File.WriteAllText(labels, "label\n");
            string read = Path.Combine(work, ReadDataRun.FolderPrefix + "0123");
            Directory.CreateDirectory(read);
            foreach (string name in CheckRun.ReadFiles.Values.Concat(new[] { ReadDataRun.PlotTableFile }))
            {
                File.WriteAllText(Path.Combine(read, name), "{ 'plots': [] }");
            }
            var record = new JObject
            {
                ["format"] = ReadDataRun.RecordFormat, ["runId"] = "R2-1",
                ["inputs"] = new JArray(new[] { Tuple.Create("config", configPath), Tuple.Create("plotsCsv", plots), Tuple.Create("labels", labels) }
                    .Select(item => new JObject { ["role"] = item.Item1, ["path"] = item.Item2, ["sha256"] = FileHash.Sha256(item.Item2) })),
            };
            File.WriteAllText(Path.Combine(read, ReadDataRun.RecordFile), record.ToString());
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private RunContext Context(string runId, string dry)
        {
            var inputs = new[] { new RunInput("Settings", configPath, false), new RunInput("Prototypes", prototypes, true) };
            return new RunContext(4, "4 Check", runId, work, dry, true, inputs, new Dictionary<string, string>());
        }

        private static SettingsScope Nbh()
        {
            return new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "012" };
        }

        private sealed class Builder : IWorkerStart
        {
            public List<WorkerRequest> Requests { get; } = new List<WorkerRequest>();

            // When set the stand-in writes plan_<NBH>.json in the output folder and names it with its SHA-256, as the worker does
            public bool WritesPlan { get; set; }

            public WorkerAnswer Run(WorkerRequest request)
            {
                Requests.Add(request);
                Directory.CreateDirectory(request.StepsFolder);
                File.WriteAllText(Path.Combine(request.StepsFolder, StepFile.FileName(CheckRows.StepName, "csharp")), R376_CheckTableTests.Step().ToString());
                File.WriteAllText(Path.Combine(request.StepsFolder, StepFile.FileName(PlanSummary.StepName, "csharp")), "{ 'made': 'up' }");
                var outputs = new List<WorkerFile>();
                if (WritesPlan)
                {
                    string plan = Path.Combine(request.OutputFolder, CheckRun.PlanName(request.Scope.Nbh));
                    var file = new PlanFile
                    {
                        Format = PlanFile.FormatName, FormatVersion = PlanFile.Version, Neighbourhood = request.Scope.Nbh,
                        SurveyPoint = new PlanSurveyPoint { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 0.0, 0.0, 0.0 } },
                        AngleToTrueNorthDeg = 0, PrototypeFolder = "protos", Plots = new List<PlanPlot>(),
                    };
                    File.WriteAllText(plan, Newtonsoft.Json.JsonConvert.SerializeObject(file));
                    outputs.Add(new WorkerFile { Role = "plan", Path = plan, Sha256 = FileHash.Sha256(plan) });
                }
                return new WorkerAnswer { RunId = request.RunId, Status = WorkerExit.Status(WorkerExit.Done), Outputs = outputs };
            }

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                ended(Run(request));
            }
        }

        private sealed class Table : ICheckTableDialog
        {
            public Func<CheckTableView, CheckTableCallbacks, CheckTableAnswer> Answer { get; set; }
            public List<CheckTableView> Shown { get; } = new List<CheckTableView>();

            public CheckTableAnswer Show(CheckTableView view, CheckTableCallbacks callbacks)
            {
                Shown.Add(view);
                return Answer == null ? new CheckTableAnswer() : Answer(view, callbacks);
            }
        }

        [Fact]
        public void R376_the_real_run_builds_the_nbh_writes_its_checks_opens_the_table_and_builds_again_after_save_overrides()
        {
            var builder = new Builder();
            var table = new Table
            {
                Answer = (view, callbacks) =>
                {
                    CheckRow level = view.Rows.Single(row => row.Check == "Level");
                    callbacks.Save(new[] { new CheckChoice(level.Index, CheckUse.Rule) });
                    return new CheckTableAnswer { Saved = true };
                },
            };
            RunLater end = null;
            ButtonBody body = new CheckRun(Roles, builder, null, new WorkerDocument(), Nbh, table, null, null).Body();

            body.Preview(Context("R4-1", null));
            body.Start(Context("R4-1", null), later => end = later);

            Assert.Null(end.Failure);
            Assert.Equal(2, builder.Requests.Count);
            WorkerRequest first = builder.Requests[0];
            Assert.Equal("build", first.Command);
            Assert.Equal("012", first.Scope.Nbh);
            Assert.Equal(new[] { "faces" }.Concat(BuildSteps.Names), first.Steps);
            Assert.Equal(new[] { "baseplanRead", "config", "curbsRead", "labels", "overrides", "plotsCsv", "prototype:P-ARC-VL4-A.rvt", "rules", "site", "villaBlocksRead", "workbooksRead" },
                first.Inputs.Select(input => input.Role).OrderBy(role => role, StringComparer.Ordinal));
            // R80. site.json of button 1 gives the plan its survey point, angle and level elevations, and buttons 5 to 8 read the plan by its name
            Assert.Equal("plan_012.json", CheckRun.PlanName(first.Scope.Nbh));
            Assert.Single(table.Shown);
            Assert.True(table.Shown[0].SaveAllowed);
            Assert.Equal(CheckTableBuild.NoPastJob, table.Shown[0].NoPastJobLine);
            // The line under the title: the profile of the rules file, the list by its file name and its entries
            Assert.Equal("made-up.json", table.Shown[0].Info.OverrideFile);
            Assert.False(string.IsNullOrEmpty(table.Shown[0].Info.LevelProfile));
            Assert.True(table.Shown[0].Info.Overrides >= table.Shown[0].Info.OnNbh);
            Assert.Contains("\"use\": \"rule\", \"byHand\": true}", File.ReadAllText(Path.Combine(root, "repo", "config", "overrides", "made-up.json")));
            // The second build reads the list as saved, and the record names the checks of its run with the SHA-256
            Assert.NotEqual(first.Inputs.Single(input => input.Role == "overrides").Sha256, builder.Requests[1].Inputs.Single(input => input.Role == "overrides").Sha256);
            string checks = end.Outputs.Keys.Single(path => Path.GetFileName(path) == "checks_012.json");
            Assert.Equal(FileHash.Sha256(checks), end.Outputs[checks]);
            Assert.Equal("4 Check, NBH 012, 4 plots", (string)JObject.Parse(File.ReadAllText(checks))["title"]);
        }

        // R381. 9 Report finds the last real run of 4 Check by its run log, so a real run that ends done writes one that names the files
        // the build made and read, and a dry run writes none
        [Fact]
        public void R381_a_real_check_run_that_ends_done_writes_the_run_log_9_report_finds_and_a_dry_run_writes_none()
        {
            const string runId = "4_20261031-090000-000";
            var builder = new Builder { WritesPlan = true };
            string dry = Path.Combine(work, "dry", "4_20261031-085900-000");
            new CheckRun(Roles, builder, (path, folder, request) => builder.Run(request), new WorkerDocument(), Nbh, new Table(), null, null).Body().Preview(Context("4_20261031-085900-000", dry));

            Assert.Empty(Directory.GetFiles(work, "run_*.json", SearchOption.AllDirectories));
            Assert.Throws<InvalidOperationException>(() => SiteAdaptation.Common.Report.ReportRun.CheckRun.Find(work, File.ReadAllText));

            RunLater end = null;
            new CheckRun(Roles, builder, null, new WorkerDocument(), Nbh, new Table(), null, null).Body().Start(Context(runId, null), later => end = later);

            Assert.Null(end.Failure);
            Assert.True(File.Exists(Path.Combine(work, "run_" + runId + ".json")));
            SiteAdaptation.Common.Report.ReportRun.CheckRun found = SiteAdaptation.Common.Report.ReportRun.CheckRun.Find(work, File.ReadAllText);
            Assert.Equal(new[] { "012" }, found.Nbhs);
            string made = Path.Combine(work, CheckRun.FolderPrefix + runId);
            var expected = new Dictionary<string, string>
            {
                ["plan"] = Path.Combine(made, "plan_012.json"),
                ["checkRows"] = Path.Combine(made, "steps", StepFile.FileName(CheckRows.StepName, "csharp")),
                ["summary"] = Path.Combine(made, "steps", StepFile.FileName(PlanSummary.StepName, "csharp")),
                ["plotsCsv"] = Path.Combine(root, "plots.csv"),
                ["overrides"] = Path.Combine(root, "repo", "config", "overrides", "made-up.json"),
            };
            Dictionary<string, WorkerFile> files = found.Files("012").ToDictionary(file => file.Role);
            Assert.Equal(expected.Keys.OrderBy(role => role, StringComparer.Ordinal), files.Keys.OrderBy(role => role, StringComparer.Ordinal));
            foreach (KeyValuePair<string, string> file in expected)
            {
                Assert.Equal(Path.GetFullPath(file.Value), Path.GetFullPath(files[file.Key].Path));
                Assert.Equal(FileHash.Sha256(file.Value), files[file.Key].Sha256);
            }
        }

        // Q214. The request of 4 Check names the Prototypes folder of the pane, the folder its prototype files are listed from
        [Fact]
        public void R357_Q214_the_request_of_4_check_names_the_prototypes_folder_of_the_pane()
        {
            var builder = new Builder();
            ButtonBody body = new CheckRun(Roles, builder, null, new WorkerDocument(), Nbh, new Table(), null, null).Body();

            body.Preview(Context("R4-1", null));
            body.Start(Context("R4-1", null), later => { });

            WorkerRequest request = builder.Requests[0];
            Assert.Equal(Path.GetFullPath(prototypes), request.PrototypeFolder);
            Assert.All(request.Inputs.Where(input => input.Role.StartsWith(BuildSteps.PrototypeRole, StringComparison.Ordinal)),
                input => Assert.Equal(request.PrototypeFolder, Path.GetDirectoryName(input.Path)));
        }

        [Fact]
        public void R376_the_dry_run_builds_into_the_dry_folder_and_greys_save_overrides()
        {
            var builder = new Builder();
            var table = new Table();
            string dry = Path.Combine(work, "dry", "R4-2");
            RunContext context = Context("R4-2", dry);
            ButtonBody body = new CheckRun(Roles, builder, (path, folder, request) => builder.Run(request), new WorkerDocument(), Nbh, table, null, null).Body();

            RunSummary summary = body.Preview(context);

            Assert.Equal(dry, builder.Requests.Single().OutputFolder);
            Assert.False(table.Shown.Single().SaveAllowed);
            Assert.Contains("4 Check, NBH 012", summary.Counts);
            Assert.Contains(context.Outputs, output => output.Value == Path.Combine(dry, "checks_012.json"));
            Assert.Equal(Overrides, File.ReadAllText(Path.Combine(root, "repo", "config", "overrides", "made-up.json")));
        }

        [Fact]
        public void R376_each_stop_is_named_other_inputs_the_override_list_the_level_profile_and_a_list_a_build_refuses()
        {
            ButtonBody Body(string profile) => new CheckRun(Roles, new Builder(), null, new WorkerDocument(), Nbh, new Table(), null, profile).Body();
            string list = Path.Combine(root, "repo", "config", "overrides", "made-up.json");

            Assert.Contains("(R277)", Assert.Throws<InvalidDataException>(() => Body("nope").Preview(Context("R4-3", null))).Message);
            File.WriteAllText(list, Overrides.Replace("\"source\": \"parsons\"", "\"source\": \"native\""));
            Assert.Contains("(R280)", Assert.Throws<InvalidDataException>(() => Body(null).Preview(Context("R4-4", null))).Message);
            File.Delete(list);
            Assert.Contains("the override list is missing", Assert.Throws<InvalidDataException>(() => Body(null).Preview(Context("R4-5", null))).Message);
            File.AppendAllText(Path.Combine(root, "plots.csv"), "2\n");
            Assert.Contains("2 Read data was done on other inputs, changed since: plots.csv", Assert.Throws<InvalidDataException>(() => Body(null).Preview(Context("R4-6", null))).Message);
        }

        [Fact]
        public void R376_Q117_the_key_reads_give_the_links_by_plot_and_a_plot_two_links_name_is_left_out()
        {
            JObject read = JObject.Parse(@"{ 'documents': [ { 'kind': 'project', 'file': 'made/kx-site.rvt', 'links': [
  { 'file': 'P-ARC-VL4-A.rvt', 'parameters': { 'Plot': '1' }, 'transform': { 'mirrored': true } },
  { 'file': 'P-ARC-TH2-A.rvt', 'parameters': { 'Plot': '2' }, 'transform': { 'mirrored': false } },
  { 'file': 'P-ARC-TH2-A.rvt', 'parameters': { 'Plot': '2' }, 'transform': { 'mirrored': false } } ] } ] }");

            Dictionary<string, KeyLink> links = CheckRun.KeyLinks(read, "kx", "Plot");

            Assert.Equal(new[] { "1" }, links.Keys);
            Assert.Equal("kx-site.rvt", links["1"].Model);
            Assert.Equal("kx", links["1"].Code);
            Assert.True(links["1"].Mirrored);
        }

        // R376. Two Saves in one window as the window sends them, each the rows changed since the table opened: the second writes the
        // list one Save of the same choices writes, never an entry twice that a build refuses (R280), and rows set back after a Save
        // leave the list as the table opened on it, so no build again
        [Fact]
        public void R376_two_saves_in_one_window_write_each_entry_once_and_rows_set_back_leave_the_list_as_it_opened()
        {
            string reads = Path.Combine(root, "key reads");
            Directory.CreateDirectory(reads);
            File.WriteAllText(Path.Combine(reads, "testhost_kx_prototypes.json"),
                "{ 'documents': [ { 'kind': 'project', 'file': 'made/kx-site.rvt', 'links': [ { 'file': 'P-ARC-VL4-A.rvt', 'parameters': { 'Plot': '1' }, 'transform': { 'mirrored': true } } ] } ] }");
            var roles = new Dictionary<string, string>(Roles) { ["Key reads"] = CheckRun.KeyReads };
            var inputs = new[] { new RunInput("Settings", configPath, false), new RunInput("Prototypes", prototypes, true), new RunInput("Key reads", reads, true) };
            var context = new RunContext(4, "4 Check", "R4-7", work, null, true, inputs, new Dictionary<string, string>());
            string list = Path.Combine(root, "repo", "config", "overrides", "made-up.json");
            string afterTwo = null, afterBack = null;
            var builder = new Builder();
            var table = new Table
            {
                Answer = (view, callbacks) =>
                {
                    var state = new CheckTableState(view);
                    CheckRow hand = view.Rows.Single(row => row.Plot == "1" && row.Check == "Hand");
                    CheckRow other = view.Rows.Single(row => row.Plot == "7");
                    state.Set(hand.Index, CheckUse.Past);
                    callbacks.Save(state.Choices());
                    state.MarkSaved();
                    state.Set(other.Index, CheckUse.Skip);
                    callbacks.Save(state.Choices());
                    state.MarkSaved();
                    afterTwo = File.ReadAllText(list);
                    state.Set(hand.Index, CheckUse.Rule);
                    state.Set(other.Index, CheckUse.Rule);
                    callbacks.Save(state.Choices());
                    state.MarkSaved();
                    afterBack = File.ReadAllText(list);
                    return new CheckTableAnswer { Saved = true };
                },
            };
            RunLater end = null;
            ButtonBody body = new CheckRun(roles, builder, null, new WorkerDocument(), Nbh, table, null, null).Body();

            body.Preview(context);
            body.Start(context, later => end = later);

            Assert.Null(end.Failure);
            Assert.Single(afterTwo.Split('\n'), line => line.StartsWith("    {\"plot\": \"1\", \"field\": \"mirrored\"", StringComparison.Ordinal));
            Assert.Single(afterTwo.Split('\n'), line => line.StartsWith("    {\"plot\": \"7\", \"reason\": ", StringComparison.Ordinal));
            string copy = Path.Combine(root, "after-two.json");
            File.WriteAllText(copy, afterTwo);
            OverrideList read = PlanOverrides.Load(copy, JObject.Parse(File.ReadAllText(configPath)), JObject.Parse("{ 'job': 'parsons' }"), "p", "project.json");
            Assert.Equal(true, read.Of("1")["mirrored"].Value<bool>());
            Assert.Equal(Overrides, afterBack);
            Assert.Single(builder.Requests);
        }
        // R376. The plan is built on the list by the SHA-256 its inputs name. A list edited on disk between the build and the first Save
        // is refused at that Save with the file named, never merged in, and the run ends failed with no check.json, as the plan of the
        // run was built on another list than the one on disk
        [Fact]
        public void R376_a_list_edited_on_disk_after_the_build_is_refused_at_the_first_save_and_the_run_writes_no_check_json()
        {
            string list = Path.Combine(root, "repo", "config", "overrides", "made-up.json");
            string edited = Overrides.Replace("0.05", "0.06");
            string refused = null;
            var builder = new Builder();
            var table = new Table
            {
                Answer = (view, callbacks) =>
                {
                    File.WriteAllText(list, edited);
                    CheckRow level = view.Rows.Single(row => row.Check == "Level");
                    refused = Assert.Throws<InvalidOperationException>(() => callbacks.Save(new[] { new CheckChoice(level.Index, CheckUse.Rule) })).Message;
                    return new CheckTableAnswer();
                },
            };
            RunLater end = null;
            ButtonBody body = new CheckRun(Roles, builder, null, new WorkerDocument(), Nbh, table, null, null).Body();

            body.Preview(Context("R4-8", null));
            body.Start(Context("R4-8", null), later => end = later);

            Assert.Equal("made-up.json changed on disk since 4 Check built the plan on it, so Save overrides does not write over it. Close the table and run 4 Check again (R376)", refused);
            Assert.Equal(edited, File.ReadAllText(list));
            Assert.Equal("4 Check did not finish, nothing was kept: made-up.json on disk is not the list this run of 4 Check built the plan on or saved, so the run does not end done and writes no check.json. Run 4 Check again (R376)", end.Failure);
            Assert.False(File.Exists(Path.Combine(work, "check_R4-8", CheckRun.RecordFile)));
            Assert.Single(builder.Requests);
        }

        // R376. The same at the close of the window: a list edited on disk with no Save, or after the last Save of the window, is not the
        // list the plan stands on, so no check.json names the plan and no build again takes the edit in silently
        [Fact]
        public void R376_a_list_edited_on_disk_while_the_window_is_open_ends_the_run_failed_with_no_check_json_with_or_without_a_save()
        {
            string list = Path.Combine(root, "repo", "config", "overrides", "made-up.json");
            var builder = new Builder();
            var table = new Table { Answer = (view, callbacks) => { File.AppendAllText(list, "\n"); return new CheckTableAnswer(); } };
            RunLater end = null;
            new CheckRun(Roles, builder, null, new WorkerDocument(), Nbh, table, null, null).Body().Start(Context("R4-9", null), later => end = later);

            Assert.Contains("made-up.json on disk is not the list this run of 4 Check built the plan on or saved", end.Failure);
            Assert.False(File.Exists(Path.Combine(work, "check_R4-9", CheckRun.RecordFile)));

            File.WriteAllText(list, Overrides);
            builder = new Builder();
            table = new Table
            {
                Answer = (view, callbacks) =>
                {
                    callbacks.Save(new[] { new CheckChoice(view.Rows.Single(row => row.Check == "Level").Index, CheckUse.Rule) });
                    File.AppendAllText(list, "\n");
                    return new CheckTableAnswer { Saved = true };
                },
            };
            end = null;
            new CheckRun(Roles, builder, null, new WorkerDocument(), Nbh, table, null, null).Body().Start(Context("R4-10", null), later => end = later);

            Assert.Contains("made-up.json on disk is not the list this run of 4 Check built the plan on or saved", end.Failure);
            Assert.False(File.Exists(Path.Combine(work, "check_R4-10", CheckRun.RecordFile)));
            Assert.Single(builder.Requests);
        }

        // R376, R377. F1 of the second review: a real Start runs to done, writes check.json last, and 7 Place villas opens the plan that
        // record names through VillaLoad by the SHA-256 recorded
        [Fact]
        public void R376_R377_a_real_run_ends_done_and_villa_load_opens_the_plan_its_check_json_names_by_the_recorded_hash()
        {
            var builder = new Builder { WritesPlan = true };
            RunLater end = null;
            new CheckRun(Roles, builder, null, new WorkerDocument(), Nbh, new Table(), null, null).Body().Start(Context("R4-11", null), later => end = later);

            Assert.Null(end.Failure);
            string folder = Path.Combine(work, "check_R4-11");
            Assert.True(File.Exists(Path.Combine(folder, CheckRun.RecordFile)));
            Tuple<string, string> named = VillaLoad.LastPlan(work, "012");
            Assert.Equal(Path.Combine(folder, "plan_012.json"), named.Item1);
            Assert.Equal(FileHash.Sha256(named.Item1), named.Item2);
            Assert.Equal(end.Outputs[named.Item1], named.Item2);
            Assert.Equal("012", PlanSource.Open(named.Item1, named.Item2).Neighbourhood);
        }
    }
}
