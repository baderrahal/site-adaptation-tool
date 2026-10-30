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

            public WorkerAnswer Run(WorkerRequest request)
            {
                Requests.Add(request);
                Directory.CreateDirectory(request.StepsFolder);
                File.WriteAllText(Path.Combine(request.StepsFolder, StepFile.FileName(CheckRows.StepName, "csharp")), R376_CheckTableTests.Step().ToString());
                return new WorkerAnswer { RunId = request.RunId, Status = WorkerExit.Status(WorkerExit.Done), Outputs = new List<WorkerFile>() };
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
            Assert.Contains("\"use\": \"rule\", \"byHand\": true}", File.ReadAllText(Path.Combine(root, "repo", "config", "overrides", "made-up.json")));
            // The second build reads the list as saved, and the record names the checks of its run with the SHA-256
            Assert.NotEqual(first.Inputs.Single(input => input.Role == "overrides").Sha256, builder.Requests[1].Inputs.Single(input => input.Role == "overrides").Sha256);
            string checks = end.Outputs.Keys.Single(path => Path.GetFileName(path) == "checks_012.json");
            Assert.Equal(FileHash.Sha256(checks), end.Outputs[checks]);
            Assert.Equal("4 Check, NBH 012, 4 plots", (string)JObject.Parse(File.ReadAllText(checks))["title"]);
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
    }
}
