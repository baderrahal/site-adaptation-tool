using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Round ADDIN Phase 1, the 7 Parsons models: the memory samples of every test host window (R310), the site facts of the reader (R311)
    // and the source of a site upgrade (R204)
    public class ParsonsModelTests
    {
        private static string RepoPath(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return Path.Combine(new[] { folder }.Concat(parts).ToArray());
        }

        // Runs one command in PowerShell 7 with the sample functions loaded and gives what it printed
        private static string Pwsh(string command)
        {
            string functions = RepoPath("dotnet", "SiteAdaptation.TestHost", "WindowSamples.ps1");
            string text = ". '" + functions.Replace("'", "''") + "'\n" + command;
            var info = new ProcessStartInfo("pwsh", "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(text)))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                Assert.True(process.WaitForExit(60000), "pwsh did not end in 60 s");
                Assert.True(process.ExitCode == 0, "pwsh failed: " + error);
                return output;
            }
        }

        [Fact]
        public void R310_the_peaks_are_the_largest_samples_also_past_2_gib()
        {
            string json = Pwsh(
                "$samples = @(\n"
                + "  [ordered]@{ privateBytes = [long]1900MB; peakPrivateBytes = [long]1950MB; workingSetBytes = [long]1800MB; peakWorkingSetBytes = [long]1850MB; responding = $true; freeBytes = [long]7GB },\n"
                + "  [ordered]@{ privateBytes = [long]6400MB; peakPrivateBytes = [long]11000MB; workingSetBytes = [long]5700MB; peakWorkingSetBytes = [long]5800MB; responding = $false; freeBytes = [long]1100MB },\n"
                + "  [ordered]@{ privateBytes = [long]10900MB; peakPrivateBytes = [long]10950MB; workingSetBytes = [long]4900MB; peakWorkingSetBytes = [long]6000MB; responding = $false; freeBytes = [long]2200MB })\n"
                + "Get-WindowPeaks -Samples $samples -FloorGB 2 | ConvertTo-Json -Compress");
            JObject peaks = JObject.Parse(json);

            Assert.Equal(3, (int)peaks["samples"]);
            // The true peaks take the peaks Windows keeps between two samples, the sampled ones are kept apart
            Assert.Equal(11000, (long)peaks["peakPrivateMb"]);
            Assert.Equal(6000, (long)peaks["peakWorkingSetMb"]);
            Assert.Equal(10900, (long)peaks["sampledPrivateMb"]);
            Assert.Equal(5700, (long)peaks["sampledWorkingSetMb"]);
            Assert.Equal(1.07, (double)peaks["minFreeGb"], 2);
            Assert.Equal(1, (int)peaks["samplesUnderFloor"]);
            Assert.Equal(2, (int)peaks["notResponding"]);
        }

        [Fact]
        public void R310_the_peaks_windows_kept_to_the_exit_count_when_they_pass_every_sample()
        {
            string json = Pwsh(
                "$samples = @([ordered]@{ privateBytes = [long]800MB; peakPrivateBytes = [long]850MB; workingSetBytes = [long]900MB; peakWorkingSetBytes = [long]950MB; responding = $true; freeBytes = [long]5GB })\n"
                + "$exit = [ordered]@{ peakPrivateBytes = [long]3000MB; peakWorkingSetBytes = [long]2900MB }\n"
                + "Get-WindowPeaks -Samples $samples -FloorGB 2 -Exit $exit | ConvertTo-Json -Compress");
            JObject peaks = JObject.Parse(json);

            Assert.Equal(3000, (long)peaks["peakPrivateMb"]);
            Assert.Equal(2900, (long)peaks["peakWorkingSetMb"]);
            Assert.Equal(800, (long)peaks["sampledPrivateMb"]);
            Assert.True((bool)peaks["throughExit"]);
            Assert.Equal(1, (int)peaks["samples"]);

            JObject without = JObject.Parse(Pwsh(
                "Get-WindowPeaks -Samples @([ordered]@{ privateBytes = [long]800MB; peakPrivateBytes = [long]850MB; workingSetBytes = [long]900MB; peakWorkingSetBytes = [long]950MB; responding = $true; freeBytes = [long]5GB }) -FloorGB 2 | ConvertTo-Json -Compress"));
            Assert.False((bool)without["throughExit"]);
            Assert.Equal(850, (long)without["peakPrivateMb"]);
        }

        [Fact]
        public void R310_the_exit_peaks_of_a_real_process_are_read_after_it_ended_and_hold_what_it_took()
        {
            JObject exit = JObject.Parse(Pwsh(
                "$info = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path, @('-NoProfile', '-Command', '$b = [byte[]]::new(300MB); [Array]::Fill($b, [byte]1)'))\n"
                + "$info.UseShellExecute = $false\n"
                + "$child = [Diagnostics.Process]::Start($info)\n"
                + "$child.WaitForExit()\n"
                + "[ordered]@{ exited = $child.HasExited; peaks = (Get-ExitPeaks -Process $child) } | ConvertTo-Json -Compress"));

            Assert.True((bool)exit["exited"]);
            Assert.True((long)exit["peaks"]["peakPrivateBytes"] >= 300L * 1024 * 1024);
            Assert.True((long)exit["peaks"]["peakWorkingSetBytes"] >= 300L * 1024 * 1024);
        }

        [Fact]
        public void R310_no_sample_gives_no_peak_and_a_sample_of_a_real_process_holds_every_field()
        {
            foreach (string samples in new[] { "@()", "$null", "@($null)" })
            {
                JObject none = JObject.Parse(Pwsh("Get-WindowPeaks -Samples " + samples + " -FloorGB 2 | ConvertTo-Json -Compress"));
                Assert.Equal(0, (int)none["samples"]);
                Assert.Equal(JTokenType.Null, none["peakPrivateMb"].Type);
                Assert.Equal(JTokenType.Null, none["minFreeGb"].Type);
                Assert.Equal(0, (int)none["notResponding"]);
            }

            JObject sample = JObject.Parse(Pwsh("Get-WindowSample -Process (Get-Process -Id $PID) | ConvertTo-Json -Compress"));
            foreach (string field in new[] { "privateBytes", "peakPrivateBytes", "workingSetBytes", "peakWorkingSetBytes", "freeBytes" })
            {
                Assert.True((long)sample[field] > 0, field);
            }
            Assert.Equal(JTokenType.Boolean, sample["responding"].Type);
            Assert.True(DateTime.Parse((string)sample["utc"]).Year >= 2026);
        }

        [Fact]
        public void R310_the_run_script_samples_every_window_from_the_config_and_prints_the_peaks_of_the_samples()
        {
            string script = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            JObject config = JObject.Parse(File.ReadAllText(RepoPath("config", "project.json")));

            Assert.Equal(5, (int)config["testHost"]["sampleSeconds"]);
            Assert.Contains(". (Join-Path $PSScriptRoot 'WindowSamples.ps1')", script);
            Assert.Contains("$sampleSeconds = [int]$config.testHost.sampleSeconds", script);
            Assert.Contains("$process.WaitForExit($sampleSeconds * 1000)", script);
            Assert.Contains("$sample = Get-WindowSample -Process $process", script);
            Assert.Contains("Add-Content -Path $samplesPath", script);
            Assert.Contains("$peaks = Get-WindowPeaks -Samples $samples -FloorGB ([double]$config.siteBuild.minFreeGB)", script);
            Assert.Contains("$sampleFailures.Add(", script);
            Assert.Contains("$peaks.sampleFailures = @($sampleFailures)", script);
            Assert.Contains("$exitPeaks = Get-ExitPeaks -Process $process", script);
            Assert.Contains("-Exit $exitPeaks", script);
            Assert.Contains("peakMb     = $peaks.peakWorkingSetMb", script);
            Assert.Contains("memory     = $peaks", script);
            // The old peak froze under 2 GiB: Max(Int32, Int32) threw past it and an empty catch hid the throw
            Assert.DoesNotContain("[Math]::Max($peak", script);
            Assert.DoesNotContain("$peak = 0", script);
        }

        private static RawSite Site()
        {
            return new RawSite
            {
                ActiveSite = "Internal",
                Sites = new List<string> { "Site B", "Internal" },
                PlaceName = "Makkah",
                ProjectPositionFeet = new[] { 1000 * UnitMath.FeetPerMetre, -500 * UnitMath.FeetPerMetre, 10 * UnitMath.FeetPerMetre },
                AngleRadians = -Math.PI / 2,
                Levels = new List<RawLevel>
                {
                    new RawLevel { Name = "Level 2", ElementId = 12, ElevationFeet = 3 * UnitMath.FeetPerMetre, ProjectElevationFeet = 3 * UnitMath.FeetPerMetre, ElevationBase = "Project Base Point" },
                    new RawLevel { Name = "Level 1", ElementId = 11, ElevationFeet = 0, ProjectElevationFeet = 0, ElevationBase = "Project Base Point" },
                    new RawLevel { Name = "Ground", ElementId = 10, ElevationFeet = 0, ProjectElevationFeet = 0, ElevationBase = "Survey Point" },
                },
                UnitSystem = "Metric",
                UnitTypeIds = new Dictionary<string, string>
                {
                    ["length"] = "autodesk.unit.unit:millimeters-1.0.1",
                    ["area"] = "autodesk.unit.unit:squareMeters-1.0.1",
                    ["angle"] = "autodesk.unit.unit:degrees-1.0.1",
                    ["slope"] = "autodesk.unit.unit:percentage-1.0.0",
                },
                LengthAccuracy = 1,
                Categories = new List<RawCategory>
                {
                    new RawCategory { Name = "Walls", Type = "Model" },
                    new RawCategory { Name = "Floors", Type = "Model" },
                    new RawCategory { Name = "Floors", Type = "Model" },
                    new RawCategory { Name = "Spot Elevations", Type = "Annotation" },
                    new RawCategory { Name = null, Type = null },
                },
            };
        }

        [Fact]
        public void R311_the_project_position_of_the_internal_origin_is_in_metres_and_its_angle_in_degrees_from_0_to_360()
        {
            SiteFacts facts = SiteFacts.Build(Site());

            Assert.Equal(1000, facts.ProjectPosition.EastWest, 9);
            Assert.Equal(-500, facts.ProjectPosition.NorthSouth, 9);
            Assert.Equal(10, facts.ProjectPosition.Elevation, 9);
            Assert.Equal(270, facts.ProjectPosition.AngleDeg, 9);
            Assert.Equal("Internal", facts.ActiveSite);
            Assert.Equal(new[] { "Internal", "Site B" }, facts.Sites);
            Assert.Equal("Makkah", facts.PlaceName);
        }

        [Fact]
        public void R311_levels_are_in_metres_by_elevation_then_name_as_the_api_gives_both_elevations_with_their_base()
        {
            List<LevelFact> levels = SiteFacts.Build(Site()).Levels;

            Assert.Equal(new[] { "Ground", "Level 1", "Level 2" }, levels.Select(level => level.Name));
            Assert.Equal(3, levels[2].Elevation, 9);
            Assert.Equal(3, levels[2].ProjectElevation, 9);
            Assert.Equal(12, levels[2].ElementId);
            Assert.Equal("Survey Point", levels[0].ElevationBase);
        }

        [Fact]
        public void R311_units_keep_the_name_of_each_unit_type_id_and_the_system()
        {
            UnitFacts units = SiteFacts.Build(Site()).Units;

            Assert.Equal("Metric", units.System);
            Assert.Equal("millimeters", units.Length);
            Assert.Equal("squareMeters", units.Area);
            Assert.Equal("degrees", units.Angle);
            Assert.Equal("percentage", units.Slope);
            Assert.Equal(1, units.LengthAccuracy);
            Assert.Equal("feetFractionalInches", SiteFacts.UnitName("autodesk.unit.unit:feetFractionalInches-1.0.1"));
            Assert.Equal("odd", SiteFacts.UnitName("odd"));
            Assert.Null(SiteFacts.UnitName(null));
        }

        [Fact]
        public void R311_elements_are_counted_by_category_type_and_name_and_those_with_no_category_apart()
        {
            List<CategoryCount> categories = SiteFacts.Build(Site()).Categories;

            Assert.Equal(new[] { "Annotation Spot Elevations 1", "Model Floors 2", "Model Walls 1", "(none) (no category) 1" },
                categories.Select(item => item.Type + " " + item.Name + " " + item.Count));
        }

        [Fact]
        public void R311_a_part_that_did_not_read_is_left_out_and_the_document_writes_site_after_its_older_keys()
        {
            RawSite raw = Site();
            raw.ProjectPositionFeet = null;
            raw.AngleRadians = null;
            raw.Levels = null;
            raw.UnitTypeIds = null;
            raw.Categories = null;
            SiteFacts facts = SiteFacts.Build(raw);
            Assert.Null(facts.ProjectPosition);
            Assert.Empty(facts.Levels);
            Assert.Null(facts.Units.Length);
            Assert.Empty(facts.Categories);

            var entry = new DocumentEntry { File = "site.rvt", Kind = "project", AngleToTrueNorthDeg = 0, Prototypes = new List<PrototypeRow>(), Site = SiteFacts.Build(Site()) };
            string json = JsonOut.ToJson(entry);
            Assert.True(json.IndexOf("\"prototypes\"", StringComparison.Ordinal) < json.IndexOf("\"site\"", StringComparison.Ordinal));
            Assert.Contains("\"activeSite\": \"Internal\"", json);
            Assert.Contains("\"eastWest\": 1000.0", json);
        }

        [Fact]
        public void R311_the_reader_fills_the_raw_site_in_revit_and_builds_it_in_common()
        {
            string extractor = File.ReadAllText(RepoPath("dotnet", "RevitPrototypeReader", "RevitExtractor.cs"));
            string revit = File.ReadAllText(RepoPath("dotnet", "RevitPrototypeReader", "RevitSite.cs"));

            Assert.Contains("entry.Site = SiteFacts.Build(RevitSite.Read(doc, errors));", extractor);
            Assert.Contains("ActiveProjectLocation", revit);
            Assert.Contains("ProjectElevation", revit);
            Assert.Contains("SpecTypeId.Length", revit);
            Assert.DoesNotContain("FeetToMetres", revit);
            Assert.DoesNotContain("Transaction", revit);
        }

        [Fact]
        public void R204_a_site_upgrade_may_name_its_source_copy_in_the_work_folder_and_its_target_follows_that_name()
        {
            string script = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            int at = script.IndexOf("'siteUpgrade' {", StringComparison.Ordinal);
            Assert.True(at > 0);
            string block = script.Substring(at, script.IndexOf("'read' {", at, StringComparison.Ordinal) - at);

            Assert.Contains("$original = if ($round.source) { [string]$round.source } else { Join-Path $work $build.answerKey.model }", block);
            Assert.Contains("if ((Split-Path $original) -ne $work) {", block);
            Assert.Contains("$target = Join-Path $work ([IO.Path]::GetFileNameWithoutExtension($original) + '_TEST-' + $Code + '.rvt')", block);
            Assert.True(block.IndexOf("(Split-Path $original) -ne $work", StringComparison.Ordinal) < block.IndexOf("$roundJob.source = $original", StringComparison.Ordinal));
            Assert.DoesNotContain("$keyStem", block);
        }
    }
}
