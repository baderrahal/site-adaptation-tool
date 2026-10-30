using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R406. The loader of button 3 on made up files of a work folder, no real read
    public sealed class R406_ReviewLoadTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "review-load-" + Guid.NewGuid().ToString("N"));

        public R406_ReviewLoadTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private string Read(string name, string runId, string nbhOfPlot)
        {
            string folder = Path.Combine(work, ReadDataRun.FolderPrefix + name);
            Directory.CreateDirectory(folder);
            var record = new JObject
            {
                ["format"] = ReadDataRun.RecordFormat, ["runId"] = runId,
                ["inputs"] = new JArray(
                    new JObject { ["role"] = "baseplan", ["path"] = Path.Combine(work, "base plan.dwg"), ["sha256"] = "aa" },
                    new JObject { ["role"] = "villaBlocks", ["path"] = Path.Combine(work, "blocks.dwg"), ["sha256"] = "bb" },
                    new JObject { ["role"] = "config", ["path"] = Path.Combine(work, "project.json"), ["sha256"] = "cc" }),
                ["outputs"] = new JArray(),
            };
            File.WriteAllText(Path.Combine(folder, ReadDataRun.RecordFile), record.ToString());
            var table = new JObject
            {
                ["border"] = 4, ["minShare"] = 0.5,
                ["plots"] = new JArray(
                    new JObject { ["baseplanLabel"] = "1001", ["neighbourhood"] = "IMX", ["nbhId"] = nbhOfPlot, ["locationPlanNumber"] = "12", ["officialNumber"] = "1012", ["typologyRev9"] = "VL4", ["footprintMirrored"] = true, ["faceAreaM2"] = 412.5 },
                    new JObject { ["baseplanLabel"] = "1002", ["neighbourhood"] = "IMX", ["nbhId"] = "099", ["locationPlanNumber"] = "3", ["officialNumber"] = "1003", ["typologyRev9"] = "TH2", ["footprintMirrored"] = false, ["faceAreaM2"] = 160.0 }),
            };
            File.WriteAllText(Path.Combine(folder, ReadDataRun.PlotTableFile), table.ToString());
            return folder;
        }

        [Fact]
        public void R406_the_loader_takes_the_latest_read_its_plot_table_hash_the_checks_kept_and_the_site_file()
        {
            Read("older", "R2-20261001-0900", "099");
            string latest = Read("newer", "R2-20261030-0900", "012");
            File.WriteAllText(Path.Combine(work, "site.json"), "{ 'surveyPoint': { 'position': [1.0, 2.0], 'sharedPosition': [11.0, 12.0] }, 'angleToTrueNorthDeg': 0 }");
            string hash = FileHash.Sha256(Path.Combine(latest, ReadDataRun.PlotTableFile));
            ReviewFile.Write(work, "012", new[] { new ReviewCheck { Plot = "12", Check = ReviewCheckValue.Wrong, Note = "turned", RunId = "R2-20261030-0900", PlotTableHash = hash } }, "R3-1");

            ReviewInput input = ReviewLoad.Load(work, "012", null);

            Assert.Equal(latest, ReviewLoad.LatestReadFolder(work));
            Assert.Equal("012", input.Nbh);
            Assert.Equal("R2-20261030-0900", input.ReadRunId);
            Assert.Equal(hash, input.PlotTableHash);
            // The NBH of a row is its area key nbhId, never the IM letter of the schedule (CLAUDE.md)
            Assert.Equal(new[] { "12" }, input.Rows.Select(row => row.PlotNo));
            Assert.Equal(new[] { "base plan.dwg", "blocks.dwg" }, input.Sources);
            Assert.Equal("turned", input.Checks.Single().Note);
        }

        [Fact]
        public void R406_the_loader_takes_the_read_folder_the_record_of_step_2_names_over_the_latest()
        {
            string named = Read("older", "R2-20261001-0900", "012");
            Read("newer", "R2-20261030-0900", "099");

            ReviewInput input = ReviewLoad.Load(work, "012", Path.Combine(named, ReadDataRun.PlotTableFile));

            Assert.Equal("R2-20261001-0900", input.ReadRunId);
            Assert.Equal(new[] { "12" }, input.Rows.Select(row => row.PlotNo));
        }

        [Fact]
        public void R406_the_loader_names_each_stop_a_missing_read_a_missing_plot_table_and_no_nbh()
        {
            Assert.Contains("2 Read data", Assert.Throws<InvalidDataException>(() => ReviewLoad.Load(work, "012", null)).Message);
            string folder = Read("one", "R2-1", "012");
            File.Delete(Path.Combine(folder, ReadDataRun.PlotTableFile));
            Assert.Contains(ReadDataRun.PlotTableFile, Assert.Throws<InvalidDataException>(() => ReviewLoad.Load(work, "012", null)).Message);
            Assert.Contains("NBH", Assert.Throws<InvalidDataException>(() => ReviewLoad.Load(work, " ", null)).Message);
        }
    }
}
