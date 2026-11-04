using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R370, docs/design/08-install.md. Install-SiteAdaptation.ps1 run in PowerShell 7 against a temporary add-ins root and two
    // planted build folders, so no real %APPDATA% folder and no real build is touched. The files only, so net48 and net8.0
    public sealed class InstallScriptTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa-install-" + Guid.NewGuid().ToString("N"));
        private readonly string addins;
        private readonly string addInBuild;
        private readonly string workerBuild;

        public InstallScriptTests()
        {
            addins = Path.Combine(root, "Addins", "2024");
            addInBuild = Path.Combine(root, "build", "addin");
            workerBuild = Path.Combine(root, "build", "worker");
            Directory.CreateDirectory(addins);
            Plant(addInBuild, "SiteAdaptation.AddIn.dll", "SiteAdaptation.AddIn.pdb", "SiteAdaptation.Common.dll", "SiteAdaptation.Common.pdb", "Clipper2Lib.dll", "Newtonsoft.Json.dll", Path.Combine("Theme", "Fonts", "OFL.txt"));
            Plant(workerBuild, "CadReader.exe", "CadReader.exe.config", "CadReader.pdb", "ACadSharp.dll", "Clipper2Lib.dll", "Newtonsoft.Json.dll", "SiteAdaptation.Common.dll", "SiteAdaptation.Common.pdb", "cad_layers.json");
            File.WriteAllText(Path.Combine(workerBuild, "SiteAdaptation.Common.dll"), "the Common of an older worker build");
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private static void Plant(string folder, params string[] names)
        {
            foreach (string name in names)
            {
                string path = Path.Combine(folder, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "planted " + name);
            }
        }

        private static string Script()
        {
            return Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Install-SiteAdaptation.ps1");
        }

        private static string Quoted(string text)
        {
            return "'" + text.Replace("'", "''") + "'";
        }

        private const string NoRevit = "@()";
        private const string Revit2023 = "@([pscustomobject]@{ Path = 'C:\\Program Files\\Autodesk\\Revit 2023\\Revit.exe'; Major = 23 })";
        private const string Revit2024 = "@([pscustomobject]@{ Path = 'C:\\Program Files\\Autodesk\\Revit 2024\\Revit.exe'; Major = 24 })";

        private string SetFolder => Path.Combine(addins, "SiteAdaptationAddIn", ShippedSet.FolderName);

        // R486. The set a build reads, measured by the readers of the C#: CheckRun.BesideConfig, ReadInputs of addin.readData.cadLayers, ParameterSheet.PathOf of
        // addin.parameterSheet, and the rules profile of the key profile. The rest of config has no C# reader
        [Fact]
        public void R486_the_install_ships_the_set_a_build_reads_in_its_layout_with_no_override_list_and_nothing_revit_loads()
        {
            Tuple<int, string> run = Install(NoRevit);
            Assert.True(run.Item1 == 0, run.Item2);
            string repo = ThemeGateTests.RepoRoot();
            Newtonsoft.Json.Linq.JObject config = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(repo, "config", "project.json")));
            var wanted = new[]
            {
                "config/project.json", "config/floor_outlines.json", "config/prototype_offsets.json", "config/rule_values.json", "config/" + (string)config["addin"]["readData"]["cadLayers"],
                (string)config["addin"]["parameterSheet"], (string)config["profile"],
            }.Select(path => path.Replace('/', Path.DirectorySeparatorChar)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] landed = Directory.GetFiles(SetFolder, "*", SearchOption.AllDirectories).Select(path => path.Substring(SetFolder.Length + 1)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.Equal(wanted, landed);
            foreach (string path in landed)
            {
                Assert.Equal(FileHash.Sha256(Path.Combine(repo, path)), FileHash.Sha256(Path.Combine(SetFolder, path)));
            }
            Assert.DoesNotContain(landed, path => path.Contains("overrides"));
            Assert.DoesNotContain(landed, path => new[] { ".dll", ".addin", ".exe" }.Contains(Path.GetExtension(path)));
            // A second install holds the same set, a file taken off the list does not stay
            File.WriteAllText(Path.Combine(SetFolder, "config", "old.json"), "{}");
            Assert.Equal(0, Install(NoRevit).Item1);
            Assert.False(File.Exists(Path.Combine(SetFolder, "config", "old.json")));
        }

        // R486, R487. The set the install really lands, copied for a new project, its Save setup, and 4 Check of the made up site through the real worker, no edit of any JSON
        [Fact]
        public void R486_R487_a_new_project_from_the_installed_set_reaches_a_plan_through_the_real_worker()
        {
            Assert.Equal(0, Install(NoRevit).Item1);
            string workRoot = Path.Combine(root, "work root");
            ProjectSet set = new ShippedSet(SetFolder, workRoot, "Site A", true).Take(File.ReadAllText);
            ProjectSettings settings = SetupSave.Apply(new SetupDraft { NewSet = set }, new ProjectSettings(), File.ReadAllText);
            Assert.Equal(DesignBasis.Rev6, settings.ProjectMode);
            string before = FileHash.Sha256(settings.ConfigFile);
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest seed = steps.Request("seed", BuildPlan.Steps);
                SiteAdaptation.Common.Flow.RunLater end;
                R484_ProjectModeTests.RealWorker worker = R484_ProjectModeTests.CheckOn(Path.Combine(root, "site"), seed, settings.ConfigFile, settings.RulesFile, settings.LevelProfile, "4_set", settings.ProjectMode, out end);
                Assert.True(worker.Exit == 0, "exit " + worker.Exit + ": " + WorkerAnswer.Read(WorkerFiles.AnswerPath(worker.Request.OutputFolder, worker.Request.RunId)).Message);
                Assert.Null(end.Failure);
                Assert.True(File.Exists(Path.Combine(worker.Request.OutputFolder, BuildPlan.FileName(null))));
                Assert.Empty((Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(worker.Request.Inputs.Single(input => input.Role == "overrides").Path))["overrides"]);
                Assert.Equal(before, FileHash.Sha256(settings.ConfigFile));
            }
        }

        // Breaker 2, 1. Save overrides on the list of the installed set: Skip on a rule row, Past on a key row, and the real worker builds on the result
        [Fact]
        public void R487_save_overrides_on_the_list_of_the_installed_set_and_the_real_worker_builds_on_it()
        {
            Assert.Equal(0, Install(NoRevit).Item1);
            ProjectSet set = new ShippedSet(SetFolder, Path.Combine(root, "work root"), "Site A", true).Take(File.ReadAllText);
            ProjectSettings settings = SetupSave.Apply(new SetupDraft { NewSet = set }, new ProjectSettings(), File.ReadAllText);
            Newtonsoft.Json.Linq.JObject config = ProjectModes.Apply(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(set.ConfigFile)), settings.ProjectMode);
            string list = Common.CheckTable.CheckRun.OverridesPath(config, set.ConfigFile, set.RulesFile);
            string key = (string)config["majority"]["groups"]["parsons"][0];
            var rows = new System.Collections.Generic.List<Common.CheckTable.CheckRow>
            {
                new Common.CheckTable.CheckRow { Index = 0, Plot = "3", Check = "Team check", From = "team", OpenUse = Common.CheckTable.CheckUse.Rule, PastWhy = "the team marked it" },
                new Common.CheckTable.CheckRow { Index = 1, Plot = "5", Check = "Level", Field = "steps", From = "missing", OpenUse = Common.CheckTable.CheckUse.Rule, PastForm = "wholeNumber", PastValue = 2, PastOffered = true, Key = key, Model = "a.rvt" },
            };
            var uses = new[] { new Common.CheckTable.CheckChoice(0, Common.CheckTable.CheckUse.Skip), new Common.CheckTable.CheckChoice(1, Common.CheckTable.CheckUse.Past) };
            Assert.Equal(2, Common.CheckTable.OverrideEdits.Save(list, rows, uses, config, "R4-1"));
            using (var steps = new LevelFloorStepTests())
            {
                WorkerRequest seed = steps.Request("seed", BuildPlan.Steps);
                SiteAdaptation.Common.Flow.RunLater end;
                R484_ProjectModeTests.RealWorker worker = R484_ProjectModeTests.CheckOn(Path.Combine(root, "site"), seed, settings.ConfigFile, settings.RulesFile, settings.LevelProfile, "4_list", settings.ProjectMode, out end);
                Assert.True(worker.Exit == 0, "exit " + worker.Exit + ": " + WorkerAnswer.Read(WorkerFiles.AnswerPath(worker.Request.OutputFolder, worker.Request.RunId)).Message);
                Assert.Null(end.Failure);
                Newtonsoft.Json.Linq.JObject plan = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(worker.Request.OutputFolder, BuildPlan.FileName(null))));
                string[] written = plan["plots"].Select(item => (string)item["plot"]).ToArray();
                Assert.DoesNotContain("3", written);
                Assert.Contains("5", written);
            }
        }

        private static string Checks()
        {
            return Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "InstallChecks.ps1");
        }

        // Runs the install with -NoBuild and the running Revit processes the body of a scriptblock gives, and gives its exit code and
        // what it printed
        private Tuple<int, string> Install(string processes, string before = "")
        {
            return Pwsh(before + "& " + Quoted(Script()) + " -NoBuild -AddinsRoot " + Quoted(addins) + " -AddInBuild " + Quoted(addInBuild)
                + " -WorkerBuild " + Quoted(workerBuild) + " -RevitProcesses { " + processes + " }");
        }

        private static Tuple<int, string> Pwsh(string text)
        {
            var info = new ProcessStartInfo("pwsh", "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(text)))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (Process process = Process.Start(info))
            {
                // Both at once: under a long worktree path the error of a refused install passes the 4 KB of its pipe, and pwsh blocks on it while stdout is read
                var error = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();
                Assert.True(process.WaitForExit(60000), "pwsh did not end in 60 s");
                return Tuple.Create(process.ExitCode, output + error.Result);
            }
        }

        private string[] Files(string folder)
        {
            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(path => path.Substring(addins.Length + 1)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        [WindowsFact]
        public void R370_the_install_writes_the_addin_and_worker_folders_and_the_manifest_with_the_full_dll_path()
        {
            Tuple<int, string> run = Install(NoRevit);
            Assert.True(run.Item1 == 0, run.Item2);

            string folder = Path.Combine(addins, "SiteAdaptationAddIn");
            string worker = Path.Combine(folder, "Worker");
            Assert.Equal(new[]
            {
                @"SiteAdaptationAddIn\Clipper2Lib.dll",
                @"SiteAdaptationAddIn\OFL.txt",
                @"SiteAdaptationAddIn\SiteAdaptation.AddIn.dll",
                @"SiteAdaptationAddIn\SiteAdaptation.AddIn.pdb",
                @"SiteAdaptationAddIn\SiteAdaptation.Common.dll",
                @"SiteAdaptationAddIn\SiteAdaptation.Common.pdb",
                @"SiteAdaptationAddIn\Worker\ACadSharp.dll",
                @"SiteAdaptationAddIn\Worker\CadReader.exe",
                @"SiteAdaptationAddIn\Worker\CadReader.exe.config",
                @"SiteAdaptationAddIn\Worker\CadReader.pdb",
                @"SiteAdaptationAddIn\Worker\Clipper2Lib.dll",
                @"SiteAdaptationAddIn\Worker\Newtonsoft.Json.dll",
                @"SiteAdaptationAddIn\Worker\SiteAdaptation.Common.dll",
                @"SiteAdaptationAddIn\Worker\SiteAdaptation.Common.pdb",
            }, Files(folder).Where(path => !path.Contains(ShippedSet.FolderName)));
            Assert.Equal(FileHash.Sha256(Path.Combine(addInBuild, "SiteAdaptation.Common.dll")), FileHash.Sha256(Path.Combine(worker, "SiteAdaptation.Common.dll")));

            XElement repo = XDocument.Load(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "SiteAdaptationAddIn_Revit2024.addin")).Root.Element("AddIn");
            XElement addIn = XDocument.Load(Path.Combine(addins, "SiteAdaptationAddIn_Revit2024.addin")).Root.Element("AddIn");
            Assert.Equal("Application", (string)addIn.Attribute("Type"));
            Assert.Equal("Site Adaptation", (string)addIn.Element("Name"));
            Assert.Equal(Path.Combine(folder, "SiteAdaptation.AddIn.dll"), (string)addIn.Element("Assembly"));
            Assert.Equal("SiteAdaptation.AddIn.App", (string)addIn.Element("FullClassName"));
            Assert.Equal((string)repo.Element("AddInId"), (string)addIn.Element("AddInId"));
            Assert.True(Guid.TryParse((string)addIn.Element("AddInId"), out _));
            Assert.Equal("SITEADAPT", (string)addIn.Element("VendorId"));
            Assert.Equal("Site Adaptation Tool", (string)addIn.Element("VendorDescription"));

            string dotnet = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet");
            var others = Directory.GetFiles(dotnet, "*.addin", SearchOption.AllDirectories)
                .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part == "bin" || part == "obj"))
                .Where(path => !path.EndsWith("SiteAdaptationAddIn_Revit2024.addin", StringComparison.Ordinal))
                .Select(path => (string)XDocument.Load(path).Root.Element("AddIn").Element("AddInId")).ToList();
            Assert.Equal(3, others.Count);
            Assert.DoesNotContain((string)addIn.Element("AddInId"), others, StringComparer.OrdinalIgnoreCase);
        }

        [WindowsFact]
        public void R370_the_install_copies_no_newtonsoft_into_the_addin_folder_and_no_config_file()
        {
            Tuple<int, string> run = Install(NoRevit);
            Assert.True(run.Item1 == 0, run.Item2);

            string folder = Path.Combine(addins, "SiteAdaptationAddIn");
            Assert.False(File.Exists(Path.Combine(folder, "Newtonsoft.Json.dll")));
            // R486. Only the shipped set holds a JSON of the repo
            Assert.Empty(Directory.GetFiles(addins, "*.json", SearchOption.AllDirectories).Where(path => !path.Contains(Path.DirectorySeparatorChar + ShippedSet.FolderName + Path.DirectorySeparatorChar)));
        }

        [WindowsFact]
        public void R370_the_install_refuses_while_revit_2024_runs_and_writes_nothing_but_a_running_revit_2023_lets_it_install()
        {
            Tuple<int, string> run = Install(Revit2024);
            Assert.NotEqual(0, run.Item1);
            Assert.Contains("Revit 2024 is running. Close it before the install.", run.Item2);
            Assert.Empty(Directory.GetFileSystemEntries(addins));

            run = Install(Revit2023);
            Assert.True(run.Item1 == 0, run.Item2);
            Assert.True(File.Exists(Path.Combine(addins, "SiteAdaptationAddIn_Revit2024.addin")));
        }

        [WindowsFact]
        public void R370_the_install_checks_revit_2024_again_after_the_build_right_before_the_first_copy()
        {
            Tuple<int, string> run = Install("$global:asked++\nif ($global:asked -ge 2) { " + Revit2024 + " } else { @() }", "$global:asked = 0\n");

            Assert.NotEqual(0, run.Item1);
            Assert.Contains("Revit 2024 is running. Close it before the install.", run.Item2);
            Assert.Empty(Directory.GetFileSystemEntries(addins));
        }

        [WindowsFact]
        public void R370_revit_2024_is_told_by_its_version_or_its_folder_and_revit_2023_and_2025_are_not()
        {
            string list = "@("
                + "[pscustomobject]@{ Path = 'C:\\Program Files\\Autodesk\\Revit 2023\\Revit.exe'; Major = 23 }, "
                + "[pscustomobject]@{ Path = 'D:\\Apps\\Revit 2025\\Revit.exe'; Major = 25 }, "
                + "[pscustomobject]@{ Path = 'D:\\Apps\\Revit\\Revit.exe'; Major = 24 }, "
                + "[pscustomobject]@{ Path = 'C:\\Program Files\\Autodesk\\Revit 2024\\Revit.exe'; Major = $null }, "
                + "[pscustomobject]@{ Path = $null; Major = $null })";
            Tuple<int, string> run = Pwsh("$ErrorActionPreference = 'Stop'\n. " + Quoted(Checks()) + "\n(Select-Revit2024 " + list + " | ForEach-Object { [string]$_.Path + '|' + [string]$_.Major }) -join ';'");

            Assert.True(run.Item1 == 0, run.Item2);
            Assert.Equal(@"D:\Apps\Revit\Revit.exe|24;C:\Program Files\Autodesk\Revit 2024\Revit.exe|;|", run.Item2.Trim());
        }

        [WindowsFact]
        public void R370_the_test_host_install_checks_one_common_in_every_folder_of_the_tool_the_addin_folder_too()
        {
            string built = Path.Combine(addInBuild, "SiteAdaptation.Common.dll");
            string stale = Path.Combine(addins, "SiteAdaptationAddIn", "SiteAdaptation.Common.dll");
            foreach (string folder in new[] { "SiteAdaptation", "SiteAdaptationPlacer", Path.Combine("SiteAdaptationTestHost", "Watcher"), Path.Combine("SiteAdaptationAddIn", "Worker"), "SiteAdaptationAddIn" })
            {
                Directory.CreateDirectory(Path.Combine(addins, folder));
                File.Copy(built, Path.Combine(addins, folder, "SiteAdaptation.Common.dll"));
            }
            File.WriteAllText(stale, "the Common of an earlier add-in install");

            Tuple<int, string> run = Pwsh("$ErrorActionPreference = 'Stop'\n. " + Quoted(Checks()) + "\n(Get-StaleCommons " + Quoted(addins) + " (Get-FileHash -LiteralPath " + Quoted(built) + ").Hash) -join ';'");
            Assert.True(run.Item1 == 0, run.Item2);
            Assert.Equal(stale, run.Item2.Trim());

            string host = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.TestHost", "Install-TestHost.ps1"));
            Assert.Contains(@". (Join-Path $repo 'dotnet\SiteAdaptation.AddIn\InstallChecks.ps1')", host);
            Assert.Contains("$stale = @(Get-StaleCommons $addins $built)", host);
            Assert.Matches(new Regex(@"\$same = \$stale\.Count -eq 0 -and "), host);
            Assert.DoesNotContain("$hashes = @(", host);
        }

        // SETUP-sign. Revit asks once per manifest with an unsigned DLL, so the install leaves one manifest and one entry DLL of the tool
        [WindowsFact]
        public void SETUP_sign_the_install_takes_away_the_developer_manifests_an_older_install_left_and_keeps_every_other_maker()
        {
            string[] dev = { "SiteAdaptation_Revit2024.addin", "SiteAdaptationPlacer_Revit2024.addin", "SiteAdaptationTestHost_Revit2024.addin" };
            foreach (string name in dev.Concat(new[] { "OtherVendor.addin" }))
            {
                File.WriteAllText(Path.Combine(addins, name), "<RevitAddIns />");
            }

            Tuple<int, string> run = Install(NoRevit);

            Assert.True(run.Item1 == 0, run.Item2);
            Assert.Equal(new[] { "SiteAdaptationAddIn_Revit2024.addin" }, Directory.GetFiles(addins, "SiteAdaptation*.addin").Select(Path.GetFileName).ToArray());
            Assert.True(File.Exists(Path.Combine(addins, "OtherVendor.addin")));
            Assert.Contains("taken away: " + string.Join(", ", dev), run.Item2);
            XElement manifest = XDocument.Load(Path.Combine(addins, "SiteAdaptationAddIn_Revit2024.addin")).Root;
            Assert.Single(manifest.Elements("AddIn"));
            Assert.EndsWith("SiteAdaptation.AddIn.dll", manifest.Element("AddIn").Element("Assembly").Value);
        }

        // As Pwsh, but stdout and stderr apart as R448_RevitListenerTests does, so a stray line on stderr never changes what a test compares
        private static Tuple<int, string, string> PwshApart(string text)
        {
            var info = new ProcessStartInfo("pwsh", "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(text)))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (Process process = Process.Start(info))
            {
                var error = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return Tuple.Create(process.ExitCode, output, error.Result);
            }
        }

        private static string HostFolderFile(string name)
        {
            return Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.TestHost", name);
        }

        private static string[] Lines(string text)
        {
            return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).ToArray();
        }

        // SETUP-sign. A run holds a stamp while it needs the developer manifests. The first run to end leaves them, the last takes them away, and the
        // stamp of a process that is gone, or of one whose id another process took over, holds nothing. Revit processes play no part in it
        [WindowsFact]
        public void SETUP_sign_the_developer_manifests_stay_until_the_last_stamp_of_a_live_run_is_gone()
        {
            string repo = ThemeGateTests.RepoRoot();
            string stamps = Path.Combine(root, "stamps");
            Plant(Path.Combine(addins, "SiteAdaptationPlacer"), "SiteAdaptation.Placer.dll");
            int other = Process.GetCurrentProcess().Id;
            string script = string.Join("\n", new[]
            {
                "$ErrorActionPreference = 'Stop'",
                ". " + Quoted(HostFolderFile("TestHostPaths.ps1")),
                "Add-DevManifestStamp " + Quoted(stamps) + " $PID",
                "Add-DevManifestStamp " + Quoted(stamps) + " " + other,
                // A process that is gone, and an id of a live process with a start time that is not its own
                "Set-Content -LiteralPath " + Quoted(Path.Combine(stamps, "999999.stamp")) + " -Value '999999 1'",
                "Set-Content -LiteralPath " + Quoted(Path.Combine(stamps, "4242.stamp")) + " -Value ('" + other + " 1')",
                "'written: ' + ((Install-DevManifests " + Quoted(repo) + " " + Quoted(addins) + ") -join ',')",
                "'first: ' + (Remove-DevManifestsWhenFree " + Quoted(addins) + " " + Quoted(stamps) + " $PID)",
                "'manifests after first: ' + @(Get-ChildItem " + Quoted(addins) + " -Filter *.addin).Count",
                "'second: ' + (Remove-DevManifestsWhenFree " + Quoted(addins) + " " + Quoted(stamps) + " " + other + ")",
                "'manifests after second: ' + @(Get-ChildItem " + Quoted(addins) + " -Filter *.addin).Count",
                "'stamps left: ' + @(Get-ChildItem " + Quoted(stamps) + " -Filter *.stamp).Count",
            });

            Tuple<int, string, string> run = PwshApart(script);

            Assert.True(run.Item1 == 0, run.Item3 + run.Item2);
            Assert.Equal(new[]
            {
                "written: SiteAdaptationPlacer_Revit2024.addin",
                "first: kept, 1 other run(s) hold them",
                "manifests after first: 1",
                "second: removed SiteAdaptationPlacer_Revit2024.addin",
                "manifests after second: 0",
                "stamps left: 0",
            }, Lines(run.Item2));
            Assert.True(File.Exists(Path.Combine(addins, "SiteAdaptationPlacer", "SiteAdaptation.Placer.dll")));
        }

        // SETUP-sign. Get-ToolAddIns writes the manifests, so a throw after the write must take them and its stamp away before the error leaves
        [WindowsFact]
        public void SETUP_sign_get_tool_add_ins_leaves_no_manifest_and_no_stamp_when_it_throws_after_writing()
        {
            string repo = ThemeGateTests.RepoRoot();
            string stamps = Path.Combine(root, "stamps");
            Plant(Path.Combine(addins, "SiteAdaptation"), "RevitPrototypeReader.dll");
            Plant(Path.Combine(addins, "SiteAdaptationPlacer"), "SiteAdaptation.Placer.dll");
            Plant(Path.Combine(addins, "SiteAdaptationTestHost"), "SiteAdaptation.TestHost.dll");
            string script = string.Join("\n", new[]
            {
                "$ErrorActionPreference = 'Stop'",
                ". " + Quoted(HostFolderFile("TestHostPaths.ps1")),
                "try { Get-ToolAddIns -Repo " + Quoted(repo) + " -Addins " + Quoted(addins) + " -StampFolder " + Quoted(stamps) + " | Out-Null } catch { 'threw: ' + $_.Exception.Message }",
                "'manifests left: ' + @(Get-ChildItem " + Quoted(addins) + " -Filter *.addin).Count",
                "'stamps left: ' + @(Get-ChildItem " + Quoted(stamps) + " -Filter *.stamp).Count",
            });

            Tuple<int, string, string> run = PwshApart(script);

            Assert.True(run.Item1 == 0, run.Item3 + run.Item2);
            string[] lines = Lines(run.Item2);
            Assert.StartsWith("threw: The add-in c330760d-8384-4e69-b4a2-2fe26e0c45fe of dotnet\\SiteAdaptation.AddIn\\SiteAdaptationAddIn_Revit2024.addin is not installed once", lines[0]);
            Assert.Equal(new[] { "manifests left: 0", "stamps left: 0" }, lines.Skip(1).ToArray());
        }

        // SETUP-sign. Run-TestHost.ps1 and Start-RevitListener.ps1 take the stamp with the manifests, remove them on any error from that call on and when
        // they end. A text check of the order, since a real run needs Revit
        [WindowsFact]
        public void SETUP_sign_the_run_script_and_the_listener_release_the_developer_manifests_on_every_way_out()
        {
            foreach (string name in new[] { "Run-TestHost.ps1", "Start-RevitListener.ps1" })
            {
                string text = File.ReadAllText(HostFolderFile(name));
                int write = text.IndexOf("Get-ToolAddIns -Repo $repo -Addins $addins -StampFolder $stampFolder", StringComparison.Ordinal);
                int trap = text.IndexOf("trap {", StringComparison.Ordinal);
                int body = text.IndexOf("\ntry {", StringComparison.Ordinal);
                int release = text.LastIndexOf("Remove-DevManifestsWhenFree $addins $stampFolder $PID)\"", StringComparison.Ordinal);
                Assert.True(write > 0, name + " writes the manifests with a stamp folder");
                Assert.True(write < trap && trap < body, name + ": the trap comes right after the write and before the try of the run");
                Assert.True(text.IndexOf("Remove-DevManifestsWhenFree", trap, StringComparison.Ordinal) < body, name + ": the trap removes them");
                Assert.True(release > body, name + " removes them in the finally of the run");
                Assert.DoesNotContain("Get-RevitProcesses", text);
            }
        }

        [WindowsFact]
        public void R370_the_install_puts_one_common_into_every_folder_of_the_tool_and_fails_on_one_it_cannot_replace()
        {
            string reader = Path.Combine(addins, "SiteAdaptation", "SiteAdaptation.Common.dll");
            string placer = Path.Combine(addins, "SiteAdaptationPlacer", "SiteAdaptation.Common.dll");
            string watcher = Path.Combine(addins, "SiteAdaptationTestHost", "Watcher", "SiteAdaptation.Common.dll");
            string other = Path.Combine(addins, "OtherVendor", "SiteAdaptation.Common.dll");
            foreach (string path in new[] { reader, placer, watcher, other })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "a Common of an earlier build");
            }
            string built = FileHash.Sha256(Path.Combine(addInBuild, "SiteAdaptation.Common.dll"));

            Tuple<int, string> run = Install(NoRevit);
            Assert.True(run.Item1 == 0, run.Item2);
            Assert.All(new[] { reader, placer, watcher }, path => Assert.Equal(built, FileHash.Sha256(path)));
            Assert.Equal("a Common of an earlier build", File.ReadAllText(other));
            Assert.Contains("one SiteAdaptation.Common.dll in 5 folders", run.Item2);

            File.WriteAllText(placer, "a different Common");
            using (new FileStream(placer, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                run = Install(NoRevit);
            }
            Assert.NotEqual(0, run.Item1);
            Assert.Contains("The Common DLLs differ", run.Item2);
            Assert.Contains(Path.Combine("SiteAdaptationPlacer", "SiteAdaptation.Common.dll"), run.Item2);
            Assert.Equal("a different Common", File.ReadAllText(placer));
        }

        [Fact]
        public void R370_the_install_asks_powershell_7_and_reads_and_writes_no_registry_key()
        {
            string[] lines = File.ReadAllLines(Script());
            Assert.Equal("#Requires -Version 7", lines[0]);
            string code = string.Join("\n", lines.Concat(File.ReadAllLines(Checks())).Where(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal)));
            Assert.DoesNotMatch(new Regex(@"HKCU|HKLM|HKEY_|Registry|ItemProperty|reg(\.exe)?\s|Microsoft\.Win32", RegexOptions.IgnoreCase), code);
            Assert.DoesNotMatch(new Regex(@"Newtonsoft", RegexOptions.IgnoreCase), code);
        }
    }
}
