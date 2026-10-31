using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
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
    // R377. The loader of button 7: the plan of the latest real run of 4 Check by its SHA-256, the footprint of the latest read of
    // button 2 and the record of the last run of 7. Made up files only
    public sealed class R377_VillaLoadTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "villaload-" + Guid.NewGuid().ToString("N"));
        private readonly string config;

        public R377_VillaLoadTests()
        {
            Directory.CreateDirectory(work);
            config = Path.Combine(work, "project.json");
            JToken addin = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")))["addin"];
            File.WriteAllText(config, new JObject { ["addin"] = new JObject { ["lightMode"] = addin["lightMode"], ["sharedParameters"] = addin["sharedParameters"] } }.ToString());
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private RunContext Context()
        {
            return new RunContext(7, "Place villas", "r7", work, null, true, new RunInput[0], new Dictionary<string, string>());
        }

        private string Plan(string folder, string nbh)
        {
            string path = Path.Combine(work, folder, CheckRun.PlanName(nbh));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var plan = new PlanFile
            {
                Format = PlanFile.FormatName, FormatVersion = PlanFile.Version, Neighbourhood = nbh,
                SurveyPoint = new PlanSurveyPoint { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 1000.0, 2000.0, 0.0 } },
                AngleToTrueNorthDeg = 0, PrototypeFolder = "protos", Plots = new List<PlanPlot>(),
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(plan));
            Record(folder, path);
            return path;
        }

        // The record a real run of 4 Check that ended done writes in its folder, naming each output with its SHA-256
        private void Record(string folder, params string[] outputs)
        {
            File.WriteAllText(Path.Combine(work, folder, "check.json"), new JObject
            {
                ["format"] = "site-adaptation-check-run", ["runId"] = folder.Substring("check_".Length),
                ["outputs"] = new JArray(outputs.Select(output => new JObject { ["path"] = output, ["sha256"] = FileHash.Sha256(output) })),
            }.ToString());
        }

        [Fact]
        public void R377_a_plan_changed_after_the_4_check_run_that_wrote_it_is_refused_through_the_loader()
        {
            string plan = Plan("check_R4-20261030-090000", "012");
            File.AppendAllText(plan, " ");

            var refused = Assert.Throws<InvalidDataException>(() => VillaLoad.Load(Context(), "012", config, null, "p", null));

            Assert.Contains("SHA-256 differs", refused.Message);
        }

        // S3 of the 5c review. The last real run of 4 Check is the newest check folder: a plan_all.json of a plot list or the whole
        // project, or a build again that ended with no record, never hands 7 an older plan of the NBH
        [Fact]
        public void R377_the_loader_takes_only_the_plan_the_last_4_check_record_names_and_stops_on_one_that_covers_no_nbh()
        {
            Plan("check_R4-20261029-100000", "012");
            Plan("check_R4-20261030-090000", "all");

            var all = Assert.Throws<InvalidDataException>(() => VillaLoad.Load(Context(), "012", config, null, "p", null));
            Assert.Contains("built no plan of NBH 012", all.Message);
            Assert.Contains("plan_all.json", all.Message);

            Plan("check_R4-20261030-100000", "012");
            string again = Path.Combine(work, "check_R4-20261030-100000-again");
            Directory.CreateDirectory(again);
            File.WriteAllText(Path.Combine(again, CheckRun.PlanName("012")), "{}");

            var failed = Assert.Throws<InvalidDataException>(() => VillaLoad.Load(Context(), "012", config, null, "p", null));
            Assert.Contains("check_R4-20261030-100000-again", failed.Message);
            Assert.Contains("did not end done", failed.Message);
        }

        [Fact]
        public void R377_the_loader_takes_the_plan_of_the_latest_check_folder_with_its_hash_the_latest_footprint_and_the_last_record()
        {
            Plan("check_R4-20261029-100000", "012");
            string latest = Plan("check_R4-20261030-090000", "012");
            Plan("check_R4-20261029-100000-again", "013");
            string read = Path.Combine(work, "read_R2-1");
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, "read.json"), "{ \"runId\": \"R2-1\" }");
            File.WriteAllText(Path.Combine(read, "read_villaBlocks.json"), new JObject { ["blocks"] = new JArray() }.ToString());
            File.WriteAllText(Path.Combine(work, "place_012.json"), new JObject { ["placed"] = new JArray("101") }.ToString());

            VillaInputs inputs = VillaLoad.Load(Context(), "012", config, VillaModes.Links, "parsons2024Rev6", plot => true);

            Assert.Equal(latest, inputs.PlanPath);
            Assert.Equal(VillaModes.Links, inputs.Mode);
            Assert.Contains("101", inputs.LastPlaced);
            Assert.Empty(inputs.Footprint);
            Assert.True(inputs.InScope("1"));
            File.Delete(Path.Combine(work, "place_012.json"));
            Assert.Empty(VillaLoad.Load(Context(), "012", config, null, "p", null).LastPlaced);
        }

        [Fact]
        public void R377_the_loader_stops_and_says_why_with_no_nbh_and_with_no_plan_of_the_nbh()
        {
            Assert.Contains("one NBH", Assert.Throws<InvalidDataException>(() => VillaLoad.Load(Context(), null, config, null, "p", null)).Message);
            Assert.Contains("4 Check", Assert.Throws<InvalidDataException>(() => VillaLoad.Load(Context(), "012", config, null, "p", null)).Message);
            Assert.Contains("4 Check", Assert.Throws<InvalidDataException>(() => VillaLoad.Load(Context(), "013", config, null, "p", null)).Message);
        }

        private void ReadWithTable(params string[] rows)
        {
            string read = Path.Combine(work, "read_R2-1");
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, ReadDataRun.RecordFile), new JObject { ["format"] = ReadDataRun.RecordFormat, ["runId"] = "R2-1" }.ToString());
            var plots = new JArray(rows.Select(row => new JObject { ["officialNumber"] = row.Split(':')[0], ["nbhId"] = row.Split(':')[1] }));
            File.WriteAllText(Path.Combine(read, "read_plotTable.json"), new JObject { ["plots"] = plots }.ToString());
        }

        [Fact]
        public void R377_a_plot_list_scope_of_the_pane_places_only_the_plots_of_the_list_and_takes_their_nbh()
        {
            Plan("check_R4-20261030-090000", "012");
            ReadWithTable("101:012", "102:012", "103:012", "201:013");

            VillaInputs inputs = VillaLoad.Load(Context(), new SettingsScope { Kind = ScopeKinds.Plots, Plots = "101, 103" }, config, null, "p");

            Assert.EndsWith(CheckRun.PlanName("012"), inputs.PlanPath);
            Assert.True(inputs.InScope("101"));
            Assert.True(inputs.InScope("103"));
            Assert.False(inputs.InScope("102"));
        }

        [Fact]
        public void R377_a_plot_list_over_two_nbhs_or_naming_no_plot_of_the_table_stops_the_load_with_the_reason()
        {
            Plan("check_R4-20261030-090000", "012");
            ReadWithTable("101:012", "201:013");

            Assert.Contains("one NBH", Assert.Throws<InvalidDataException>(() => VillaLoad.Load(Context(), new SettingsScope { Kind = ScopeKinds.Plots, Plots = "101, 201" }, config, null, "p")).Message);
            Assert.Contains("999", Assert.Throws<InvalidDataException>(() => VillaLoad.Load(Context(), new SettingsScope { Kind = ScopeKinds.Plots, Plots = "999" }, config, null, "p")).Message);
        }

        [Fact]
        public void R377_an_nbh_scope_places_every_plot_of_the_plan_and_a_whole_project_scope_stops_for_it_names_no_nbh()
        {
            Plan("check_R4-20261030-090000", "012");

            VillaInputs inputs = VillaLoad.Load(Context(), new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "012" }, config, null, "p");

            Assert.Null(inputs.InScope);
            Assert.Contains("one NBH", Assert.Throws<InvalidDataException>(() => VillaLoad.Load(Context(), new SettingsScope(), config, null, "p")).Message);
        }
        // R377. A check folder with no check.json either did not end done or came before check.json was written, so the stop names the
        // missing file and never says only that the run did not end done
        [Fact]
        public void R377_a_last_check_folder_with_no_check_json_is_named_plainly_in_the_stop()
        {
            Directory.CreateDirectory(Path.Combine(work, "check_R4-20261030-110000"));

            var failed = Assert.Throws<InvalidDataException>(() => VillaLoad.LastPlan(work, "012"));

            Assert.Equal("the last run of 4 Check, check_R4-20261030-110000, holds no check.json, it did not end done or came before check.json was written, so it names no plan. "
                + "Run 4 Check on NBH 012 again", failed.Message);
        }
    }
}
