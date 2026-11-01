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
    // Lane B 5a PR 6: the test host side of the first Revit run of the add-in. Files only, so it runs on net48 and net8.0
    public class RevitRunTests
    {
        private const string Addins = @"C:\Users\x\AppData\Roaming\Autodesk\Revit\Addins\2024";
        private const string AddInDll = Addins + @"\SiteAdaptationAddIn\SiteAdaptation.AddIn.dll";

        private static string RepoPath(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules", "index.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return Path.Combine(new[] { folder }.Concat(parts).ToArray());
        }

        private static string Quoted(string text)
        {
            return "'" + text.Replace("'", "''") + "'";
        }

        // Runs one command in PowerShell 7 with the path functions of the run script loaded, and gives the exit code and what it printed
        private static Tuple<int, string, string> Pwsh(string command)
        {
            string functions = RepoPath("dotnet", "SiteAdaptation.TestHost", "TestHostPaths.ps1");
            string text = "$ErrorActionPreference = 'Stop'\n. " + Quoted(functions) + "\n" + command;
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
                return Tuple.Create(process.ExitCode, output.Trim(), error.Trim());
            }
        }

        // The manifests as Install-TestHost.ps1 and Install-SiteAdaptation.ps1 write them, each Assembly the DLL under the add-ins root
        private static string Installed()
        {
            string folder = Path.Combine(Path.GetTempPath(), "sa_addins_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var installs = new[]
            {
                Tuple.Create(RepoPath("dotnet", "RevitPrototypeReader", "SiteAdaptation.addin"), "SiteAdaptation_Revit2024.addin", @"SiteAdaptation\RevitPrototypeReader.dll"),
                Tuple.Create(RepoPath("dotnet", "SiteAdaptation.Placer", "SiteAdaptationPlacer_Revit2024.addin"), "SiteAdaptationPlacer_Revit2024.addin", @"SiteAdaptationPlacer\SiteAdaptation.Placer.dll"),
                Tuple.Create(RepoPath("dotnet", "SiteAdaptation.TestHost", "SiteAdaptationTestHost_Revit2024.addin"), "SiteAdaptationTestHost_Revit2024.addin", @"SiteAdaptationTestHost\SiteAdaptation.TestHost.dll"),
                Tuple.Create(RepoPath("dotnet", "SiteAdaptation.AddIn", "SiteAdaptationAddIn_Revit2024.addin"), "SiteAdaptationAddIn_Revit2024.addin", @"SiteAdaptationAddIn\SiteAdaptation.AddIn.dll"),
            };
            foreach (Tuple<string, string, string> install in installs)
            {
                var xml = System.Xml.Linq.XDocument.Load(install.Item1);
                xml.Descendants("Assembly").Single().Value = Addins + @"\" + install.Item3;
                xml.Save(Path.Combine(folder, install.Item2));
            }
            return folder;
        }

        private static JObject ToolAddIns()
        {
            Tuple<int, string, string> run = Pwsh("Get-ToolAddIns -Repo " + Quoted(RepoPath()) + " -Addins " + Quoted(Installed()) + " | ConvertTo-Json -Depth 3");
            Assert.True(run.Item1 == 0, run.Item3);
            return JObject.Parse(run.Item2);
        }

        // The Revit 2024 box of an unsigned add-in, as SecurityDialogTests reads it
        private static SecurityDialogEntry RevitBox(string name, string location)
        {
            var texts = new[]
            {
                "The publisher of this add-in could not be verified. What do you want to do?",
                "Name:                " + name + "\r\nPublisher:           Unknown Publisher\r\nLocation:            " + location + "\r\nIssuer:                 None",
            };
            return SecurityDialogs.Read("Security - Unsigned Add-In", "#32770", texts, new[] { "Always Load", "Load Once", "Do Not Load" });
        }

        [WindowsFact]
        public void R108_the_watcher_lists_the_add_in_manifest_and_dll_so_its_box_gets_load_once()
        {
            JObject tool = ToolAddIns();
            List<string> ids = tool["ids"].Values<string>().ToList();
            List<string> marks = tool["marks"].Values<string>().ToList();
            List<string> paths = tool["paths"].Values<string>().ToList();

            Assert.Equal(4, ids.Count);
            Assert.Equal(4, paths.Count);
            Assert.Contains("c330760d-8384-4e69-b4a2-2fe26e0c45fe", ids);
            Assert.Contains("Site Adaptation", marks);
            Assert.Contains("SiteAdaptation.AddIn.dll", marks);
            Assert.Contains(AddInDll, paths);
            Assert.Equal(WatchAction.LoadOnceOurs, SecurityDialogs.Decide(RevitBox("Site Adaptation", AddInDll), ids, marks, paths));
            // Without the add-in in the lists, as before, its box was a stranger's and got Do Not Load
            List<int> before = Enumerable.Range(0, 4).Where(at => !paths[at].EndsWith("SiteAdaptation.AddIn.dll", StringComparison.Ordinal)).ToList();
            Assert.Equal(WatchAction.DoNotLoad, SecurityDialogs.Decide(RevitBox("Site Adaptation", AddInDll), before.Select(at => ids[at]).ToList(),
                marks.Where(mark => mark != "Site Adaptation" && mark != "SiteAdaptation.AddIn.dll"), before.Select(at => paths[at])));
        }

        [WindowsFact]
        public void R108_each_listed_path_is_the_folder_and_dll_its_install_script_writes()
        {
            JObject tool = ToolAddIns();
            string addInInstall = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.AddIn", "Install-SiteAdaptation.ps1"));
            string hostInstall = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Install-TestHost.ps1"));

            Assert.Contains("Join-Path $AddinsRoot 'SiteAdaptationAddIn'", addInInstall);
            Assert.Contains("$dll = Join-Path $addInFolder 'SiteAdaptation.AddIn.dll'", addInInstall);
            foreach (string folder in new[] { "SiteAdaptation", "SiteAdaptationPlacer", "SiteAdaptationTestHost" })
            {
                Assert.Contains("Join-Path $addins '" + folder + "'", hostInstall);
            }
            foreach (string path in tool["paths"].Values<string>())
            {
                Assert.StartsWith(Addins + @"\", path);
            }
            // Each manifest of the list is a repo file, and the ids the watcher takes are theirs in the same order
            List<string> manifests = tool["manifests"].Values<string>().ToList();
            List<string> ids = tool["ids"].Values<string>().ToList();
            Assert.Equal(4, manifests.Count);
            for (int at = 0; at < manifests.Count; at++)
            {
                var xml = System.Xml.Linq.XDocument.Load(RepoPath(manifests[at].Split('\\')));
                Assert.Equal(ids[at], xml.Descendants("AddInId").Single().Value);
            }
        }

        private const string AppData = @"C:\Users\x\AppData\Roaming";
        private const string MyDocuments = @"C:\Users\x\OneDrive - Org\Documents";
        private const string Machine = AppData + @"\SiteAdaptation\machine.json";

        private static Func<string, string> Reads(string text)
        {
            return path => path == Machine ? text : null;
        }

        [WindowsFact]
        public void R372_the_work_root_is_workRoot_of_the_machine_file_else_documents()
        {
            Assert.Equal(MyDocuments + @"\SiteAdaptation", WorkRoot.Of(AppData, MyDocuments, Reads(null)));
            Assert.Equal(@"C:\Users\x\Documents\SiteAdaptation", WorkRoot.Of(AppData, MyDocuments, Reads("{\"workRoot\": \"C:\\\\Users\\\\x\\\\Documents\\\\SiteAdaptation\"}")));
            Assert.Equal(Machine, WorkRoot.MachinePath(AppData));
        }

        [WindowsFact]
        public void R372_a_machine_file_that_gives_no_plain_full_workRoot_is_refused_never_passed_over()
        {
            foreach (string text in new[] { "not json", "[]", "{}", "{\"workRoot\": 3}", "{\"workRoot\": \" \"}", "{\"workRoot\": \"Documents\\\\SiteAdaptation\"}",
                "{\"workRoot\": \"C:\\\\a\\\\..\\\\SiteAdaptation\"}", "{\"workRoot\": \"C:\\\\Users\\\\x\\\\Documents\\\\Work\"}" })
            {
                InvalidDataException error = Assert.Throws<InvalidDataException>(() => WorkRoot.Of(AppData, MyDocuments, Reads(text)));
                Assert.Contains(Machine, error.Message);
            }
        }

        [WindowsFact]
        public void R372_the_test_host_keeps_its_files_in_the_work_root_itself()
        {
            string root = @"C:\Users\x\Documents\SiteAdaptation";
            string documents = WorkRoot.TestHostDocuments(root);

            Assert.Equal(root, TestHostGuard.SiteAdaptationFolder(documents));
            Assert.Equal(root + @"\testhost_SA5A_log.json", TestHostGuard.OutputPath(documents, "SA5A", "log"));
            // TestHostGuard names its folder SiteAdaptation, so a root of another name is refused and never mixed with a folder beside it
            Assert.Throws<InvalidDataException>(() => WorkRoot.TestHostDocuments(@"C:\Users\x\Documents\Work"));
            Assert.Throws<InvalidDataException>(() => WorkRoot.TestHostDocuments(@"C:\Users\x\Documents\SiteAdaptation\"));
        }

        [Fact]
        public void R372_the_job_session_and_the_run_script_read_the_work_root_of_the_machine_file()
        {
            string session = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "JobSession.cs"));
            string script = File.ReadAllText(RepoPath("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));

            Assert.Contains("string documents = WorkRoot.TestHostDocuments(WorkRoot.Of(", session);
            Assert.Contains("Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)", session);
            Assert.Contains("$docs = Get-WorkRoot -AppData $env:APPDATA -MyDocuments ([Environment]::GetFolderPath('MyDocuments'))", script);
            Assert.DoesNotContain("$docs = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'SiteAdaptation'", script);
        }

        // The second method for the addinRun job: the run folder the script makes passes the guard of the test host as the job names it,
        // and a second run of the same stamp is stopped before it can touch the first
        [WindowsFact]
        public void R103_the_run_folder_the_script_makes_is_one_the_guard_of_the_test_host_takes()
        {
            string documents = Path.Combine(Path.GetTempPath(), "sa-addinrun-" + Guid.NewGuid().ToString("N"));
            string root = Path.Combine(documents, WorkRoot.Folder);
            string template = Path.Combine(documents, "DefaultMetric.rte");
            Directory.CreateDirectory(root);
            File.WriteAllText(template, "a template stand in");
            try
            {
                Tuple<int, string, string> run = Pwsh("New-AddinRunFolder -Docs " + Quoted(root) + " -Code SA5A -Stamp 20261028-090000 | ConvertTo-Json");
                Assert.True(run.Item1 == 0, run.Item3);
                JObject made = JObject.Parse(run.Item2);
                string model = (string)made["model"];
                Assert.True(Directory.Exists((string)made["folder"]));
                Assert.Equal("SA5A-TEST-NEW.rvt", Path.GetFileName(model));

                string token = new string('a', 32);
                var job = new JObject { ["kind"] = "addinRun", ["token"] = token, ["code"] = "SA5A", ["model"] = model, ["template"] = template,
                    ["createdUtc"] = DateTime.UtcNow.ToString("o"), ["totalMinutes"] = 13 };
                GuardResult guard = TestHostGuard.Check(job.ToString(), token, DateTime.UtcNow, File.Exists, Directory.Exists);
                Assert.True(guard.Act, guard.Reason);
                Assert.Null(TestHostGuard.CheckJobPath(TestHostGuard.JobPath(documents, "SA5A", false), guard.Job, documents));
                Assert.Null(FileIdentity.CheckTargetFolder((string)made["folder"]));

                Tuple<int, string, string> again = Pwsh("try { New-AddinRunFolder -Docs " + Quoted(root) + " -Code SA5A -Stamp 20261028-090000 } catch { 'stopped' }");
                Assert.Equal("stopped", again.Item2);
            }
            finally
            {
                Directory.Delete(documents, true);
            }
        }

        // The second method: the run script reads the same machine files as WorkRoot and gives the same root or refuses the same
        [WindowsFact]
        public void R372_the_run_script_gives_the_work_root_the_test_host_takes()
        {
            string folder = Path.Combine(Path.GetTempPath(), "sa-workroot-" + Guid.NewGuid().ToString("N"));
            string appData = Path.Combine(folder, "AppData");
            string docs = Path.Combine(folder, "Docs");
            string machine = WorkRoot.MachinePath(appData);
            Directory.CreateDirectory(Path.GetDirectoryName(machine));
            try
            {
                var cases = new List<string> { null, "{\"workRoot\": " + JValue.CreateString(Path.Combine(folder, "Local", "SiteAdaptation")).ToString() + "}",
                    "{\"workRoot\": " + JValue.CreateString(Path.Combine(folder, "Local", "Work")).ToString() + "}", "{}", "not json" };
                int same = 0;
                foreach (string text in cases)
                {
                    if (text == null)
                    {
                        File.Delete(machine);
                    }
                    else
                    {
                        File.WriteAllText(machine, text);
                    }
                    string expected;
                    try
                    {
                        expected = WorkRoot.TestHostDocuments(WorkRoot.Of(appData, docs, WorkRoot.ReadIfThere));
                        expected = TestHostGuard.SiteAdaptationFolder(expected);
                    }
                    catch (InvalidDataException)
                    {
                        expected = "refused";
                    }
                    Tuple<int, string, string> run = Pwsh("try { Get-WorkRoot -AppData " + Quoted(appData) + " -MyDocuments " + Quoted(docs) + " } catch { 'refused' }");
                    Assert.True(run.Item1 == 0, run.Item3);
                    Assert.Equal(expected, run.Item2);
                    same++;
                }
                Assert.Equal(cases.Count, same);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        // Q215 (Bader, 2026-10-31). -Upgrade wrote the 2024 prototypes into the input root against D19, they are made once under the work root
        // Round 6a: -Upgrade read pastJob.output of config as "{workRoot}\plan_imc_past.json" with the mark not taken, so no upgrade ran.
        // The run script takes the mark as WorkRoot.Resolve does
        [WindowsFact]
        public void R109_the_run_script_takes_the_work_root_mark_of_a_config_path_as_work_root_resolve_does()
        {
            string root = Path.Combine(Path.GetTempPath(), "sa-mark-" + Guid.NewGuid().ToString("N"), WorkRoot.Folder);
            foreach (string path in new[] { WorkRoot.Mark + "\\plan_imc_past.json", WorkRoot.Mark + "/sub/plan.json", @"C:\Other\plan.json" })
            {
                Tuple<int, string, string> run = Pwsh("Resolve-WorkRootPath -Path " + Quoted(path) + " -Docs " + Quoted(root));
                Assert.True(run.Item1 == 0, run.Item3);
                Assert.Equal(WorkRoot.Resolve(path, () => root), run.Item2);
            }
            string script = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            Assert.Contains("Resolve-WorkRootPath -Path $config.pastJob.output -Docs $docs", script);
        }

        // FIX-1NOV-B, found by Lane D: a -RoundSpec job joined siteBuild.folder "{workRoot}\round_addin" with the mark not taken, so its
        // work folder was a relative path and no Revit window opened
        [WindowsFact]
        public void R109_a_round_spec_takes_the_work_root_mark_of_the_site_build_folder()
        {
            string script = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            Assert.Contains("$buildFolder = Resolve-WorkRootPath -Path $build.folder -Docs $docs", script);
            Assert.DoesNotContain("Join-Path $build.folder", script);
            Assert.DoesNotContain("$build.folder +", script);
            Assert.DoesNotContain("in $($build.folder)", script);
        }

        // FIX-1NOV-B, found by Lane D: from its R462 buttons 1 and 7 read a parameter workbook beside the config, and the run folder
        // took only the json files of config, so every setUp and buttons job would stop at button 1
        [WindowsFact]
        public void R109_setUp_and_buttons_jobs_take_the_workbooks_of_config_into_the_run_folder()
        {
            string script = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1"));
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(script, @"Join-Path \$repo 'config'\) -File -Filter '\*\.xlsx'").Count);
        }

        [WindowsFact]
        public void R109_Q215_the_upgrade_folder_lies_under_the_work_root_and_never_under_an_input_root()
        {
            string documents = Path.Combine(Path.GetTempPath(), "sa-upgrade-" + Guid.NewGuid().ToString("N"));
            string root = Path.Combine(documents, WorkRoot.Folder);
            string inputs = Path.Combine(documents, "inputs");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(inputs);
            try
            {
                Tuple<int, string, string> run = Pwsh("Get-UpgradeFolder -Docs " + Quoted(root) + " -InputRoots @(" + Quoted(inputs) + ")");
                Assert.True(run.Item1 == 0, run.Item3);
                Assert.Equal(Path.Combine(root, "prototypes_2024"), run.Item2);
                Assert.False(Directory.Exists(run.Item2));

                string inside = Path.Combine(inputs, WorkRoot.Folder);
                var refused = new[]
                {
                    "Get-UpgradeFolder -Docs " + Quoted(inside) + " -InputRoots @(" + Quoted(inputs) + ")",
                    "Get-UpgradeFolder -Docs " + Quoted(inside.ToUpperInvariant()) + " -InputRoots @(" + Quoted(inputs + "\\") + ")",
                    "Assert-OutsideInputRoots -Folder " + Quoted(Path.Combine(inputs, "Prototypes_2024", "STR")) + " -InputRoots @(" + Quoted(root) + ", " + Quoted(inputs) + ")",
                    "Assert-OutsideInputRoots -Folder " + Quoted(inputs) + " -InputRoots @(" + Quoted(inputs) + ")",
                };
                foreach (string command in refused)
                {
                    Tuple<int, string, string> stop = Pwsh("try {\n" + command + " | Out-Null\n'taken'\n} catch { 'refused' }");
                    Assert.True(stop.Item2 == "refused", command + " gave " + stop.Item2 + " " + stop.Item3);
                }
                Tuple<int, string, string> sibling = Pwsh("Assert-OutsideInputRoots -Folder " + Quoted(inputs + "2") + " -InputRoots @(" + Quoted(inputs) + ")\n'taken'");
                Assert.Equal("taken", sibling.Item2);
            }
            finally
            {
                Directory.Delete(documents, true);
            }
        }
    }
}
