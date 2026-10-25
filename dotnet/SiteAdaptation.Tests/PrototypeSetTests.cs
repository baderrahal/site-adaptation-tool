using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Round ADDIN Phase 1 part 2: the upgrade job of R109 on a prototype set of config/project.json, the 13 STR prototypes (R313)
    public class PrototypeSetTests
    {
        private const string Token = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
        private static readonly DateTime Now = new DateTime(2026, 10, 25, 7, 0, 0, DateTimeKind.Utc);
        private const string StrFile = "RRE-WP-NSP-KEO-STR-TH1-ZZ-MOD-3100.rvt";
        private const string Source = @"C:\Models\STR\Prototypes\" + StrFile;
        private const string Target = @"C:\Models\ARC\Prototypes_2024\STR\" + StrFile;

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

        // Runs one command in PowerShell 7 with the set functions loaded and gives what it printed
        private static string Pwsh(string command)
        {
            string functions = RepoPath("dotnet", "SiteAdaptation.TestHost", "PrototypeSets.ps1");
            string text = "$ErrorActionPreference = 'Stop'\n. '" + functions.Replace("'", "''") + "'\n" + command;
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
                return output.Trim();
            }
        }

        private static string Quoted(string text)
        {
            return "'" + text.Replace("'", "''") + "'";
        }

        // A config as ConvertFrom-Json gives it, with the set changed by the test
        private static string Config(string set)
        {
            string json = "{\"pastJob\": {\"prototypeFolder\": \"Zone 02\\\\ARC\\\\Prototypes\", \"upgradedPrototypeFolder\": \"Zone 02\\\\ARC\\\\Prototypes_2024\"},"
                + " \"prototypeSets\": {\"STR\": " + set + "}}";
            return "$config = " + Quoted(json) + " | ConvertFrom-Json\n";
        }

        private const string StrSet = "{\"source\": \"Zone 02\\\\STR\\\\Prototypes\", \"target\": \"Zone 02\\\\ARC\\\\Prototypes_2024\\\\STR\", \"discipline\": \"STR\", \"codePrefix\": \"S\"}";

        private static string Set(string change)
        {
            JObject set = JObject.Parse(StrSet);
            foreach (JProperty property in JObject.Parse(change).Properties())
            {
                if (property.Value.Type == JTokenType.Null)
                {
                    set.Remove(property.Name);
                }
                else
                {
                    set[property.Name] = property.Value;
                }
            }
            return set.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string Refusal(string call)
        {
            return "try { " + call + " | Out-Null\n'accepted' }\ncatch { 'refused: ' + $_.Exception.Message }";
        }

        [Fact]
        public void R313_a_set_takes_the_one_prototype_file_of_its_discipline_and_code_from_its_source_folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "sets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "RRE-WP-NSP-KEO-STR-VL4-ZZ-MOD-1400_backup"));
            try
            {
                foreach (string name in new[] { "RRE-WP-NSP-KEO-STR-C10-ZZ-MOD-1500.rvt", "RRE-WP-NSP-KEO-STR-DP1-ZZ-MOD-2100.rvt", "RRE-WP-NSP-KEO-STR-C10-ZZ-MOD-1500.txt",
                    "RRE-WP-NSP-KEO-STR-TH1-ZZ-MOD-3100.rvt", "RRE-WP-NSP-KEO-STR-TH1-ZZ-MOD-3101.rvt", "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt",
                    @"RRE-WP-NSP-KEO-STR-VL4-ZZ-MOD-1400_backup\RRE-WP-NSP-KEO-STR-VL4-ZZ-MOD-1400.rvt" })
                {
                    File.WriteAllText(Path.Combine(folder, name), "dummy");
                }
                string pick = "Get-SetPrototype -Folder " + Quoted(folder) + " -Discipline STR -Code ";

                Assert.Equal("RRE-WP-NSP-KEO-STR-C10-ZZ-MOD-1500.rvt", Pwsh(pick + "C10"));
                Assert.Equal("RRE-WP-NSP-KEO-STR-DP1-ZZ-MOD-2100.rvt", Pwsh(pick + "DP1"));
                // Two files of one code, none, one of another discipline, or one only in a folder inside the set folder are refused,
                // nothing is guessed
                Assert.Contains("holds 2 files", Pwsh(Refusal(pick + "TH1")));
                Assert.Contains("holds 0 files", Pwsh(Refusal(pick + "VL4")));
                Assert.Contains("holds 0 files", Pwsh(Refusal(pick + "DP")));
                Assert.Contains("holds 0 files", Pwsh(Refusal(pick + "DP2")));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void R313_the_job_code_of_a_set_is_its_prefix_and_the_code_and_fits_the_job_file_names()
        {
            Assert.Equal("STH1", Pwsh(Config(StrSet) + "Get-SetJobCode -Set (Get-PrototypeSet -Config $config -Name STR) -Code TH1"));
            Assert.Equal("SC10", Pwsh(Config(StrSet) + "Get-SetJobCode -Set (Get-PrototypeSet -Config $config -Name STR) -Code C10"));
            Assert.StartsWith("refused: ", Pwsh(Config(StrSet) + Refusal("Get-SetJobCode -Set (Get-PrototypeSet -Config $config -Name STR) -Code VL46")));
        }

        [Theory]
        [InlineData("{\"target\": null}", "names no target")]
        [InlineData("{\"discipline\": null}", "names no discipline")]
        [InlineData("{\"source\": \"C:\\\\STR\"}", "plain path")]
        [InlineData("{\"target\": \"Zone 02\\\\..\\\\STR\"}", "plain path")]
        [InlineData("{\"target\": \"Zone 02\\\\STR\\\\Prototypes\"}", "source folder")]
        [InlineData("{\"target\": \"zone 02\\\\arc\\\\prototypes_2024\"}", "folder of its own")]
        [InlineData("{\"target\": \"Zone 02/ARC/Prototypes\"}", "folder of its own")]
        [InlineData("{\"codePrefix\": \"s\"}", "capital")]
        [InlineData("{\"discipline\": \"ARC\"}", "other than ARC")]
        [InlineData("{\"discipline\": \"St\"}", "three capitals")]
        [InlineData("{\"extra\": 1}", "exactly")]
        public void R313_a_set_with_a_field_missing_or_extra_a_path_not_plain_or_the_folder_of_the_arc_prototypes_as_target_is_refused(string change, string reason)
        {
            string output = Pwsh(Config(Set(change)) + Refusal("Get-PrototypeSet -Config $config -Name STR"));
            Assert.StartsWith("refused: ", output);
            Assert.Contains(reason, output);
        }

        [Fact]
        public void R313_a_set_the_config_does_not_name_is_refused_in_any_case_of_its_name()
        {
            Assert.Equal("accepted", Pwsh(Config(StrSet) + Refusal("Get-PrototypeSet -Config $config -Name STR")));
            Assert.Contains("no prototype set", Pwsh(Config(StrSet) + Refusal("Get-PrototypeSet -Config $config -Name str")));
            Assert.Contains("no prototype set", Pwsh(Config(StrSet) + Refusal("Get-PrototypeSet -Config $config -Name MEP")));
        }

        [Fact]
        public void R313_the_real_config_upgrades_the_str_prototypes_into_a_folder_of_their_own_in_the_upgraded_prototype_folder()
        {
            JObject config = JObject.Parse(File.ReadAllText(RepoPath("config", "project.json")));
            JObject set = (JObject)config["prototypeSets"]["STR"];
            Assert.Equal(@"00_Revit Models\Zone 02\STR\Prototypes", (string)set["source"]);
            Assert.Equal((string)config["pastJob"]["upgradedPrototypeFolder"] + @"\STR", (string)set["target"]);
            Assert.Equal("STR", (string)set["discipline"]);
            Assert.Equal("S", (string)set["codePrefix"]);
            string real = "$config = Get-Content -Raw " + Quoted(RepoPath("config", "project.json")) + " | ConvertFrom-Json\n";
            Assert.Equal("accepted", Pwsh(real + Refusal("Get-PrototypeSet -Config $config -Name STR")));
            // Every code of the test host order gives a job code the job file names of R110 take
            foreach (string code in config["testHost"]["order"].Select(token => (string)token))
            {
                Assert.Matches(@"\AS[A-Z][A-Z0-9]{1,2}\z", "S" + code);
            }
        }

        [Fact]
        public void R313_the_run_script_takes_the_folders_and_the_file_of_a_set_writes_its_discipline_and_prefix_and_reads_no_plan_for_it()
        {
            // The working copy may hold CRLF, the runner LF
            string script = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1")).Replace("\r\n", "\n");

            Assert.Contains(". (Join-Path $PSScriptRoot 'PrototypeSets.ps1')", script);
            Assert.Contains("[string]$PrototypeSet", script);
            Assert.Contains("throw '-PrototypeSet goes with -Upgrade only.'", script);
            // The job code is made before the names of the job file and of the outputs
            int jobCode = script.IndexOf("$Code = Get-SetJobCode -Set $set -Code $prototypeCode", StringComparison.Ordinal);
            int name = script.IndexOf("$name = if ($DryStart)", StringComparison.Ordinal);
            Assert.True(jobCode > 0 && name > jobCode);
            // A set takes its file from its source folder and never reads the plan, the ARC upgrade still takes the plan in the else branch
            int branch = script.IndexOf("    if ($set) {\n        # R313. A set takes", StringComparison.Ordinal);
            int setFile = script.IndexOf("$prototype = Get-SetPrototype -Folder $sourceFolder -Discipline $set.discipline -Code $prototypeCode", StringComparison.Ordinal);
            int other = script.IndexOf("    else {", setFile, StringComparison.Ordinal);
            int plan = script.IndexOf("$plan = Get-Content -Raw $planPath | ConvertFrom-Json", setFile, StringComparison.Ordinal);
            Assert.True(branch > 0 && setFile > branch && other > setFile && plan > other);
            Assert.DoesNotContain("$plan", script.Substring(branch, other - branch));
            Assert.Contains("$sourceFolder = if ($set) { Join-Path $root $set.source } else { Join-Path $root $config.pastJob.prototypeFolder }", script);
            Assert.Contains("$targetFolder = if ($set) { Join-Path $root $set.target } else { Join-Path $root $config.pastJob.upgradedPrototypeFolder }", script);
            Assert.Contains("$source = Join-Path $sourceFolder $prototype", script);
            // The guard of the test host sees the discipline and the prefix of the set in the job
            Assert.Contains("$job.discipline = [string]$set.discipline", script);
            Assert.Contains("$job.codePrefix = [string]$set.codePrefix", script);
        }

        private static string Upgrade(Action<JObject> change = null)
        {
            var job = new JObject
            {
                ["kind"] = "upgrade",
                ["token"] = Token,
                ["code"] = "STH1",
                ["prototypeFile"] = StrFile,
                ["source"] = Source,
                ["target"] = Target,
                ["discipline"] = "STR",
                ["codePrefix"] = "S",
                ["createdUtc"] = Now.AddMinutes(-2).ToString("o"),
            };
            change?.Invoke(job);
            return job.ToString();
        }

        private static GuardResult Check(string text)
        {
            return TestHostGuard.Check(text, Token, Now, path => path == Source, path => true);
        }

        [Fact]
        public void R313_the_guard_takes_an_upgrade_of_a_set_whose_file_holds_its_discipline_and_the_code_after_its_prefix()
        {
            GuardResult result = Check(Upgrade());
            Assert.True(result.Act, result.Reason);
            Assert.Equal("STR", result.Job.Discipline);
            Assert.Equal("S", result.Job.CodePrefix);
            // The ARC upgrade of R109 names neither and keeps its ARC file
            GuardResult arc = Check(Upgrade(job =>
            {
                job.Remove("discipline");
                job.Remove("codePrefix");
                job["code"] = "TH1";
            }));
            Assert.Contains("RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-<n>.rvt", arc.Reason);
        }

        [Fact]
        public void R313_the_guard_refuses_a_set_upgrade_of_another_file_code_prefix_or_discipline_and_a_set_on_any_other_job()
        {
            Assert.Contains("RRE-WP-NSP-KEO-STR-TH1-ZZ-MOD-<n>.rvt", Check(Upgrade(job => job["prototypeFile"] = "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt")).Reason);
            Assert.False(Check(Upgrade(job => job["code"] = "TH1")).Act);
            Assert.False(Check(Upgrade(job => job["code"] = "XTH1")).Act);
            Assert.False(Check(Upgrade(job => job["codePrefix"] = "s")).Act);
            Assert.False(Check(Upgrade(job => job["discipline"] = "ARC")).Act);
            Assert.False(Check(Upgrade(job => job["discipline"] = "st")).Act);
            Assert.False(Check(Upgrade(job => job.Remove("codePrefix"))).Act);
            Assert.False(Check(Upgrade(job => job.Remove("discipline"))).Act);
            Assert.Contains("upgrade job only", Check(Upgrade(job =>
            {
                job["kind"] = "place";
                job.Remove("source");
                job.Remove("target");
            })).Reason);
        }
    }
}
