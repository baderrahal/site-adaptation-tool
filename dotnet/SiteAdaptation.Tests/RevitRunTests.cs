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

        private static JObject ToolAddIns()
        {
            Tuple<int, string, string> run = Pwsh("Get-ToolAddIns -Repo " + Quoted(RepoPath()) + " -Addins " + Quoted(Addins) + " | ConvertTo-Json -Depth 3");
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

        [Fact]
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

        [Fact]
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
                "{\"workRoot\": \"C:\\\\a\\\\..\\\\SiteAdaptation\"}" })
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
    }
}
