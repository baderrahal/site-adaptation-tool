using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
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
            return path;
        }

        [Fact]
        public void R377_the_loader_takes_the_plan_of_the_latest_check_folder_with_its_hash_the_latest_footprint_and_the_last_record()
        {
            Plan("check_R4-20261029-100000", "012");
            string latest = Plan("check_R4-20261030-090000", "012");
            Plan("check_R4-20261030-090000-again", "013");
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
    }
}
