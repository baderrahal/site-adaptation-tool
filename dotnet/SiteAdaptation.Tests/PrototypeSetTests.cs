using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R313. prototypeNames of config/project.json as the run script writes it into every job that names a prototype file
    internal static class JobNames
    {
        public static JObject Arc()
        {
            return new JObject { ["prefix"] = "RRE-WP-NSP-KEO-", ["discipline"] = "ARC", ["suffix"] = "-ZZ-MOD-" };
        }

        // A job of a test that names a prototype file and says nothing of prototypeNames gets them, as the run script writes them
        public static JObject AddTo(JObject job)
        {
            if (job["prototypeFile"] != null && job["prototypeFile"].Type != JTokenType.Null && !job.ContainsKey("prototypeNames"))
            {
                job["prototypeNames"] = Arc();
            }
            return job;
        }
    }

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

        private const string ArcNames = "{\"prefix\": \"RRE-WP-NSP-KEO-\", \"discipline\": \"ARC\", \"suffix\": \"-ZZ-MOD-\"}";

        // A config as ConvertFrom-Json gives it, with the set and the name parts changed by the test
        private static string Config(string set, string names = ArcNames)
        {
            string json = "{\"pastJob\": {\"prototypeFolder\": \"Zone 02\\\\ARC\\\\Prototypes\", \"upgradedPrototypeFolder\": \"Zone 02\\\\ARC\\\\Prototypes_2024\"},"
                + (names == null ? "" : " \"prototypeNames\": " + names + ",") + " \"prototypeSets\": {\"STR\": " + set + "}}";
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
                string pick = Config(StrSet) + "Get-SetPrototype -Folder " + Quoted(folder) + " -Names (Get-PrototypeNames -Config $config) -Discipline STR -Code ";

                Assert.Equal("RRE-WP-NSP-KEO-STR-C10-ZZ-MOD-1500.rvt", Pwsh(pick + "C10"));
                Assert.Equal("RRE-WP-NSP-KEO-STR-DP1-ZZ-MOD-2100.rvt", Pwsh(pick + "DP1"));
                // Two files of one code, none, one of another discipline, or one only in a folder inside the set folder are refused,
                // nothing is guessed
                Assert.Contains("holds 2 files", Pwsh(Refusal(pick + "TH1")));
                Assert.Contains("holds 0 files", Pwsh(Refusal(pick + "VL4")));
                Assert.Contains("holds 0 files", Pwsh(Refusal(pick + "DP")));
                Assert.Contains("holds 0 files", Pwsh(Refusal(pick + "DP2")));
                // The name parts come from the config, never from the code: other parts pick another file
                File.WriteAllText(Path.Combine(folder, "ABC-STR-C10-Q-1.rvt"), "dummy");
                string other = Config(StrSet, "{\"prefix\": \"ABC-\", \"discipline\": \"ARC\", \"suffix\": \"-Q-\"}");
                Assert.Equal("ABC-STR-C10-Q-1.rvt", Pwsh(other + "Get-SetPrototype -Folder " + Quoted(folder) + " -Names (Get-PrototypeNames -Config $config) -Discipline STR -Code C10"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [WindowsFact]
        public void R313_the_job_code_of_a_set_is_its_prefix_and_the_code_and_fits_the_job_file_names()
        {
            Assert.Equal("STH1", Pwsh(Config(StrSet) + "Get-SetJobCode -Set (Get-PrototypeSet -Config $config -Name STR) -Code TH1"));
            Assert.Equal("SC10", Pwsh(Config(StrSet) + "Get-SetJobCode -Set (Get-PrototypeSet -Config $config -Name STR) -Code C10"));
            Assert.StartsWith("refused: ", Pwsh(Config(StrSet) + Refusal("Get-SetJobCode -Set (Get-PrototypeSet -Config $config -Name STR) -Code VL46")));
        }

        [WindowsTheory]
        [InlineData("{\"target\": null}", "names no target")]
        [InlineData("{\"discipline\": null}", "names no discipline")]
        [InlineData("{\"source\": \"C:\\\\STR\"}", "plain path")]
        [InlineData("{\"target\": \"Zone 02\\\\..\\\\STR\"}", "plain path")]
        [InlineData("{\"target\": \"Zone 02\\\\STR\\\\Prototypes\"}", "source folder")]
        [InlineData("{\"target\": \"zone 02\\\\arc\\\\prototypes_2024\"}", "folder of its own")]
        [InlineData("{\"target\": \"Zone 02/ARC/Prototypes\"}", "folder of its own")]
        // Places, not text: Windows opens Prototypes_2024. as Prototypes_2024, and a folder above or inside a folder holds its files
        [InlineData("{\"target\": \"Zone 02\\\\ARC\\\\Prototypes_2024.\"}", "folder of its own")]
        [InlineData("{\"target\": \"zone 02/arc/PROTOTYPES_2024 .\"}", "folder of its own")]
        [InlineData("{\"target\": \"Zone 02\\\\ARC\"}", "folder of its own")]
        [InlineData("{\"target\": \"Zone 02\\\\ARC.\\\\Prototypes\\\\STR\"}", "folder of its own")]
        [InlineData("{\"target\": \"Zone 02\\\\STR\\\\Prototypes.\"}", "source folder")]
        [InlineData("{\"target\": \"Zone 02\\\\STR\\\\Prototypes\\\\2024\"}", "source folder")]
        [InlineData("{\"target\": \"Zone 02\\\\STR\"}", "source folder")]
        [InlineData("{\"source\": \"Zone 02\\\\ARC\\\\Prototypes_2024\\\\STR\\\\2021\"}", "source folder")]
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

        [WindowsFact]
        public void R313_a_set_gives_its_folders_as_windows_opens_them_so_the_run_script_never_writes_a_trailing_dot_into_a_job()
        {
            string set = Set("{\"source\": \"Zone 02/STR/Prototypes.\", \"target\": \"Zone 02\\\\ARC\\\\Prototypes_2024\\\\STR. .\"}");
            string output = Pwsh(Config(set) + "$set = Get-PrototypeSet -Config $config -Name STR\n$set.source + '|' + $set.target");
            Assert.Equal(@"Zone 02\STR\Prototypes|Zone 02\ARC\Prototypes_2024\STR", output);
        }

        [WindowsTheory]
        [InlineData(null, "no prototypeNames")]
        [InlineData("{\"prefix\": \"RRE-WP-NSP-KEO-\", \"discipline\": \"ARC\"}", "exactly")]
        [InlineData("{\"prefix\": \"RRE-WP-NSP-KEO-\", \"discipline\": \"ARC\", \"suffix\": \"-ZZ-MOD-\", \"extra\": 1}", "exactly")]
        [InlineData("{\"prefix\": \"rre-\", \"discipline\": \"ARC\", \"suffix\": \"-ZZ-MOD-\"}", "prefix")]
        [InlineData("{\"prefix\": \"RRE-WP-NSP-KEO\", \"discipline\": \"ARC\", \"suffix\": \"-ZZ-MOD-\"}", "prefix")]
        [InlineData("{\"prefix\": \"RRE-WP-NSP-KEO-\", \"discipline\": \"AR\", \"suffix\": \"-ZZ-MOD-\"}", "discipline")]
        [InlineData("{\"prefix\": \"RRE-WP-NSP-KEO-\", \"discipline\": \"ARC\", \"suffix\": \"ZZ-MOD\"}", "suffix")]
        [InlineData("{\"prefix\": \"RRE-WP-NSP-KEO-\", \"discipline\": \"STR\", \"suffix\": \"-ZZ-MOD-\"}", "other than STR")]
        public void R313_the_name_parts_of_the_prototype_files_come_from_the_config_whole_or_the_set_is_refused(string names, string reason)
        {
            string output = Pwsh(Config(StrSet, names) + Refusal("Get-PrototypeSet -Config $config -Name STR"));
            Assert.StartsWith("refused: ", output);
            Assert.Contains(reason, output);
        }

        [WindowsFact]
        public void R313_a_set_the_config_does_not_name_is_refused_in_any_case_of_its_name()
        {
            Assert.Equal("accepted", Pwsh(Config(StrSet) + Refusal("Get-PrototypeSet -Config $config -Name STR")));
            Assert.Contains("no prototype set", Pwsh(Config(StrSet) + Refusal("Get-PrototypeSet -Config $config -Name str")));
            Assert.Contains("no prototype set", Pwsh(Config(StrSet) + Refusal("Get-PrototypeSet -Config $config -Name MEP")));
        }

        [WindowsFact]
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
            // The name parts moved from the code into the config give the same names and the same set as before
            Assert.True(JToken.DeepEquals(JobNames.Arc(), config["prototypeNames"]));
            Assert.Equal(@"RRE-WP-NSP-KEO-|ARC|-ZZ-MOD-|00_Revit Models\Zone 02\STR\Prototypes|00_Revit Models\Zone 02\ARC\Prototypes_2024\STR|STR|S",
                Pwsh(real + "$names = Get-PrototypeNames -Config $config\n$set = Get-PrototypeSet -Config $config -Name STR\n"
                    + "@($names.prefix, $names.discipline, $names.suffix, $set.source, $set.target, $set.discipline, $set.codePrefix) -join '|'"));
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
            int setFile = script.IndexOf("$prototype = Get-SetPrototype -Folder $sourceFolder -Names $names -Discipline $set.discipline -Code $prototypeCode", StringComparison.Ordinal);
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

        [Fact]
        public void R313_the_run_script_reads_the_name_parts_from_the_config_and_writes_them_into_every_job_with_a_prototype_file()
        {
            string script = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1")).Replace("\r\n", "\n");
            string sets = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "PrototypeSets.ps1"));

            Assert.Contains("$names = Get-PrototypeNames -Config $config", script);
            // The place, the upgrade and the floor spec jobs and the round linksAndFloors job, the four that name a prototype file
            Assert.Equal(3, script.Split(new[] { "$job.prototypeNames = $names" }, StringSplitOptions.None).Length - 1);
            Assert.Contains("$roundJob.prototypeNames = $names", script);
            Assert.Contains("if ($prototype) {\n                $job.prototypeNames = $names", script);
            Assert.Equal(2, script.Split(new[] { "[regex]::Escape($names.suffix)" }, StringSplitOptions.None).Length - 1);
            // No part of a prototype file name is left in the code
            foreach (string text in new[] { script, sets })
            {
                Assert.DoesNotContain("KEO", text);
                Assert.DoesNotContain("ZZ-MOD", text);
                Assert.DoesNotContain("'ARC'", text);
            }
        }

        [WindowsFact]
        public void R313_the_guard_takes_the_name_parts_of_a_prototype_file_from_the_job_and_refuses_a_job_without_them()
        {
            const string other = "ABC-XYZ-TH1-Q-3100.rvt";
            JObject names = new JObject { ["prefix"] = "ABC-", ["discipline"] = "XYZ", ["suffix"] = "-Q-" };
            GuardResult result = Check(Upgrade(job =>
            {
                job.Remove("discipline");
                job.Remove("codePrefix");
                job["code"] = "TH1";
                job["prototypeFile"] = other;
                job["source"] = @"C:\Models\STR\Prototypes\" + other;
                job["target"] = @"C:\Models\ARC\Prototypes_2024\STR\" + other;
                job["prototypeNames"] = names;
            }), @"C:\Models\STR\Prototypes\" + other);
            Assert.True(result.Act, result.Reason);
            Assert.Equal("XYZ", result.Job.PrototypeNames.Discipline);
            // The ARC file of the code is not a file of those parts
            Assert.Contains("ABC-XYZ-TH1-Q-<n>.rvt", Check(Upgrade(job =>
            {
                job.Remove("discipline");
                job.Remove("codePrefix");
                job["code"] = "TH1";
                job["prototypeNames"] = names;
            })).Reason);
            Assert.Contains("prototypeNames", Check(Upgrade(job => job["prototypeNames"] = null)).Reason);
            Assert.Contains("prefix", Check(Upgrade(job => job["prototypeNames"]["prefix"] = "abc-")).Reason);
            Assert.Contains("discipline", Check(Upgrade(job => job["prototypeNames"]["discipline"] = "AR")).Reason);
            Assert.Contains("suffix", Check(Upgrade(job => job["prototypeNames"]["suffix"] = "Q")).Reason);
            // A set of the discipline of the plan files is refused by the discipline of the job, ARC by the real config
            Assert.Contains("other than XYZ", Check(Upgrade(job =>
            {
                job["prototypeNames"] = names;
                job["discipline"] = "XYZ";
            })).Reason);
            // A job of a kind with no prototype file names no name parts either
            Assert.Contains("no prototypeNames", Check(Upgrade(job =>
            {
                job["kind"] = "siteUpgrade";
                job.Remove("prototypeFile");
                job.Remove("discipline");
                job.Remove("codePrefix");
            })).Reason);
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
                ["prototypeNames"] = JobNames.Arc(),
                ["createdUtc"] = Now.AddMinutes(-2).ToString("o"),
            };
            change?.Invoke(job);
            return job.ToString();
        }

        private static GuardResult Check(string text, string source = Source)
        {
            return TestHostGuard.Check(text, Token, Now, path => path == source, path => true);
        }

        [WindowsFact]
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
