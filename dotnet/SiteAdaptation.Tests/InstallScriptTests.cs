using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SiteAdaptation.Common;
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
            Plant(addInBuild, "SiteAdaptation.AddIn.dll", "SiteAdaptation.AddIn.pdb", "SiteAdaptation.Common.dll", "SiteAdaptation.Common.pdb", "Newtonsoft.Json.dll", Path.Combine("Theme", "Fonts", "OFL.txt"));
            Plant(workerBuild, "CadReader.exe", "CadReader.exe.config", "CadReader.pdb", "ACadSharp.dll", "Newtonsoft.Json.dll", "SiteAdaptation.Common.dll", "SiteAdaptation.Common.pdb", "cad_layers.json");
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

        // Runs the install with -NoBuild and the check of a running Revit given, and gives its exit code and what it printed
        private Tuple<int, string> Install(bool revitRunning)
        {
            string text = "& " + Quoted(Script()) + " -NoBuild -AddinsRoot " + Quoted(addins) + " -AddInBuild " + Quoted(addInBuild)
                + " -WorkerBuild " + Quoted(workerBuild) + " -RevitRunning { $" + (revitRunning ? "true" : "false") + " }";
            var info = new ProcessStartInfo("pwsh", "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(text)))
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
                return Tuple.Create(process.ExitCode, output + error);
            }
        }

        private string[] Files(string folder)
        {
            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(path => path.Substring(addins.Length + 1)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        [WindowsFact]
        public void R370_the_install_writes_the_addin_and_worker_folders_and_the_manifest_with_the_full_dll_path()
        {
            Tuple<int, string> run = Install(false);
            Assert.True(run.Item1 == 0, run.Item2);

            string folder = Path.Combine(addins, "SiteAdaptationAddIn");
            string worker = Path.Combine(folder, "Worker");
            Assert.Equal(new[]
            {
                @"SiteAdaptationAddIn\OFL.txt",
                @"SiteAdaptationAddIn\SiteAdaptation.AddIn.dll",
                @"SiteAdaptationAddIn\SiteAdaptation.AddIn.pdb",
                @"SiteAdaptationAddIn\SiteAdaptation.Common.dll",
                @"SiteAdaptationAddIn\SiteAdaptation.Common.pdb",
                @"SiteAdaptationAddIn\Worker\ACadSharp.dll",
                @"SiteAdaptationAddIn\Worker\CadReader.exe",
                @"SiteAdaptationAddIn\Worker\CadReader.exe.config",
                @"SiteAdaptationAddIn\Worker\CadReader.pdb",
                @"SiteAdaptationAddIn\Worker\Newtonsoft.Json.dll",
                @"SiteAdaptationAddIn\Worker\SiteAdaptation.Common.dll",
                @"SiteAdaptationAddIn\Worker\SiteAdaptation.Common.pdb",
            }, Files(folder));
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
            Tuple<int, string> run = Install(false);
            Assert.True(run.Item1 == 0, run.Item2);

            string folder = Path.Combine(addins, "SiteAdaptationAddIn");
            Assert.False(File.Exists(Path.Combine(folder, "Newtonsoft.Json.dll")));
            Assert.Empty(Directory.GetFiles(addins, "*.json", SearchOption.AllDirectories));
        }

        [WindowsFact]
        public void R370_the_install_refuses_while_revit_runs_and_writes_nothing()
        {
            Tuple<int, string> run = Install(true);

            Assert.NotEqual(0, run.Item1);
            Assert.Contains("Revit is running. Close it before the install.", run.Item2);
            Assert.Empty(Directory.GetFileSystemEntries(addins));
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

            Tuple<int, string> run = Install(false);
            Assert.True(run.Item1 == 0, run.Item2);
            Assert.All(new[] { reader, placer, watcher }, path => Assert.Equal(built, FileHash.Sha256(path)));
            Assert.Equal("a Common of an earlier build", File.ReadAllText(other));
            Assert.Contains("one SiteAdaptation.Common.dll in 5 folders", run.Item2);

            File.WriteAllText(placer, "a different Common");
            using (new FileStream(placer, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                run = Install(false);
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
            string code = string.Join("\n", lines.Where(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal)));
            Assert.DoesNotMatch(new Regex(@"HKCU|HKLM|HKEY_|Registry|ItemProperty|reg(\.exe)?\s|Microsoft\.Win32", RegexOptions.IgnoreCase), code);
            Assert.DoesNotMatch(new Regex(@"Newtonsoft", RegexOptions.IgnoreCase), code);
        }
    }
}
