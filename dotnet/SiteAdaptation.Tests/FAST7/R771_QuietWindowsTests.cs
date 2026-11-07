#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.ConfirmProfile;
using SiteAdaptation.AddIn.Fast7;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R771, F6 of run 1 of FAST-3. A step 2 the Build put back shows no check window, keeps the profile the person confirmed last and the picks of
    // the scan, and opens the Confirm only when nothing was confirmed yet. Made up values only
    // QuietRead, ScanGate.Window and the windows of ConfirmProfileStep are one per process, so this class runs alone, after the classes that run in
    // parallel, and no other test sees the Build's quiet read
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class QuietReadCollection
    {
        public const string Name = "R771 quiet read";
    }

    [Collection(QuietReadCollection.Name)]
    public sealed class R771_QuietWindowsTests : IDisposable
    {
        private const string Check = "{\"needs\":[{\"need\":\"plotOutlines\",\"mark\":\"strong\",\"line\":\"Made up plots.\"}],\"files\":[{\"name\":\"a.dwg\",\"status\":\"read\",\"reason\":\"\"}],\"stopped\":\"\"}";
        private const string Profile = "{\"levels\":[{\"level\":1,\"word\":null,\"source\":\"missing\"},{\"level\":2,\"word\":\"Block\",\"source\":\"computed\"}],"
            + "\"codeMap\":{\"proposed\":[]},\"typologyColumn\":{\"proposed\":{\"workbook\":\"w.xlsx\",\"sheet\":\"S\",\"column\":\"Type\",\"plots\":9},\"candidates\":[{\"workbook\":\"w.xlsx\",\"sheet\":\"S\",\"column\":\"Type\",\"plots\":9}]},"
            + "\"areaColumn\":{\"proposed\":null,\"candidates\":[]},\"hands\":{\"proposed\":{}}}";

        private readonly Action<IntPtr, DataCheckResult> checkBefore = ConfirmProfileStep.ShowCheck;
        private readonly Action<IntPtr, ConfirmProfileSession> confirmBefore = ConfirmProfileStep.ShowConfirm;
        private readonly Func<IntPtr, string, ProjectSettings, ProjectSettings> windowBefore = ScanGate.Window;
        private readonly Func<bool> quietBefore = QuietRead.Now;
        private readonly string folder = Path.Combine(Path.GetTempPath(), "R771_" + Guid.NewGuid().ToString("N"));

        public R771_QuietWindowsTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            ConfirmProfileStep.ShowCheck = checkBefore;
            ConfirmProfileStep.ShowConfirm = confirmBefore;
            ScanGate.Window = windowBefore;
            QuietRead.Now = quietBefore;
            Directory.Delete(folder, true);
        }

        // A made up read of the scan folder given, with the record read.json that names it as R771 reads it
        private Dictionary<string, string> ReadOutputs(string scanFolder = null, string name = "read_x")
        {
            string read = Path.Combine(folder, name);
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, ReadDataRun.RecordFile), new JObject
            {
                ["inputs"] = new JArray(new JObject { ["role"] = ReadInputs.ScanFolder, ["path"] = scanFolder ?? Path.Combine(folder, "folder A") }),
            }.ToString());
            File.WriteAllText(Path.Combine(read, "data_check.json"), Check);
            File.WriteAllText(Path.Combine(read, "plots.json"), "{\"plots\":[{\"number\":\"1\"}]}");
            File.WriteAllText(Path.Combine(read, "project_profile.json"), Profile);
            return Directory.GetFiles(read).ToDictionary(path => path, path => "00");
        }

        private PaneSettings Project()
        {
            string config = Path.Combine(folder, "config.json");
            JObject root = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            root["inputRoot"] = Path.Combine(folder, "input");
            File.WriteAllText(config, root.ToString());
            return new PaneSettings(new ProjectSettings { ConfigFile = config });
        }

        private RunLater Press(PaneSettings settings, Dictionary<string, string> outputs)
        {
            var read = new ButtonBody { Preview = context => new RunSummary("p", new string[0]), Start = (context, done) => done(RunLater.Done("Read done.", new string[0], outputs)) };
            RunLater ended = null;
            ConfirmProfileStep.Wrap(read, settings, IntPtr.Zero).Start(new RunContext(2, "2 Read data", "2_run", folder, null, true, new RunInput[0], new Dictionary<string, string>()), later => ended = later);
            return ended;
        }

        [Fact]
        public void R771_a_quiet_read_with_a_confirmed_profile_shows_no_window_and_keeps_the_profile()
        {
            PaneSettings settings = Project();
            Dictionary<string, string> outputs = ReadOutputs();
            var calls = new List<string>();
            ConfirmProfileStep.ShowCheck = (owner, result) => calls.Add("check");
            ConfirmProfileStep.ShowConfirm = (owner, session) => { calls.Add("confirm"); session.Confirm(); };

            // The read of card 1, where the person confirms
            QuietRead.Now = () => false;
            Assert.Null(Press(settings, outputs).Failure);
            Assert.Equal(new[] { "check", "confirm" }, calls);
            string confirmed = Path.Combine(folder, ConfirmedProfile.FileName);
            byte[] kept = File.ReadAllBytes(confirmed);

            // The step 2 the Build put back
            calls.Clear();
            QuietRead.Now = () => true;
            RunLater quiet = Press(settings, outputs);
            Assert.Null(quiet.Failure);
            Assert.Empty(calls);
            Assert.Contains(ScanResultsText.QuietKept, quiet.Message);
            Assert.Equal(kept, File.ReadAllBytes(confirmed));
        }

        // Lead's point of 18:20: the demo reads two folders. A profile confirmed for folder A never stands for folder B, the Confirm opens
        [Fact]
        public void R771_a_quiet_read_of_another_folder_than_the_one_confirmed_opens_the_confirm()
        {
            PaneSettings settings = Project();
            var calls = new List<string>();
            ConfirmProfileStep.ShowCheck = (owner, result) => calls.Add("check");
            ConfirmProfileStep.ShowConfirm = (owner, session) => { calls.Add("confirm"); session.Confirm(); };
            QuietRead.Now = () => false;
            Assert.Null(Press(settings, ReadOutputs(Path.Combine(folder, "folder A"), "read_a")).Failure);
            Assert.True(ConfirmedFor.Holds(folder, Path.Combine(folder, "FOLDER A") + Path.DirectorySeparatorChar));
            Assert.False(ConfirmedFor.Holds(folder, Path.Combine(folder, "folder B")));

            calls.Clear();
            QuietRead.Now = () => true;
            RunLater ended = Press(settings, ReadOutputs(Path.Combine(folder, "folder B"), "read_b"));
            Assert.Null(ended.Failure);
            Assert.Equal(new[] { "confirm" }, calls);
            Assert.DoesNotContain(ScanResultsText.QuietKept, ended.Message);
            // The Confirm of folder B now holds for B
            Assert.True(ConfirmedFor.Holds(folder, Path.Combine(folder, "folder B")));
        }

        // Review of PR 267: a write that fails after a Confirm leaves no older folder in force
        [Fact]
        public void R771_a_failed_write_of_the_folder_leaves_no_older_folder_in_force()
        {
            File.WriteAllText(Path.Combine(folder, ConfirmedProfile.FileName), "{}");
            Assert.True(ConfirmedFor.Write(folder, Path.Combine(folder, "folder A")));
            Assert.True(ConfirmedFor.Holds(folder, Path.Combine(folder, "folder A")));
            // Broken on purpose: a folder name no path takes, so the write of folder B fails
            Assert.False(ConfirmedFor.Write(folder, "folder|B"));
            Assert.False(ConfirmedFor.Holds(folder, Path.Combine(folder, "folder A")));
            Assert.False(File.Exists(Path.Combine(folder, ConfirmedFor.FileName)));
        }

        [Fact]
        public void R771_a_quiet_read_with_nothing_confirmed_yet_still_opens_the_confirm_and_no_check_window()
        {
            PaneSettings settings = Project();
            var calls = new List<string>();
            ConfirmProfileStep.ShowCheck = (owner, result) => calls.Add("check");
            ConfirmProfileStep.ShowConfirm = (owner, session) => { calls.Add("confirm"); session.Cancel(); };
            QuietRead.Now = () => true;
            // Broken on purpose: no confirmed profile, so the Build may not take the fresh proposal for the person
            RunLater ended = Press(settings, ReadOutputs());
            Assert.Null(ended.Failure);
            Assert.Equal(new[] { "confirm" }, calls);
            Assert.False(File.Exists(Path.Combine(folder, ConfirmedProfile.FileName)));
        }

        [Fact]
        public void R771_a_quiet_scan_gate_opens_no_window_and_keeps_the_picks()
        {
            var settings = new PaneSettings();
            settings.Project.ScanPicks = new SortedDictionary<string, string> { ["villaType"] = "made up.xlsx|Sheet 1|Type|shift 0" };
            int shown = 0;
            ScanGate.Window = (owner, path, current) => { shown++; return null; };
            QuietRead.Now = () => true;
            ScanGateAnswer answer = ScanGate.Ask("scan.json", settings, IntPtr.Zero, path => R435_ScanViewTests.Scan(false, 2).ToString());
            Assert.True(answer.GoOn);
            Assert.Equal(ScanResultsText.QuietKept, answer.Line);
            Assert.Equal(0, shown);
            Assert.Equal("made up.xlsx|Sheet 1|Type|shift 0", settings.Project.ScanPicks["villaType"]);
            // Not quiet: the same scan opens the window, Cancel stops the read
            QuietRead.Now = () => false;
            Assert.False(ScanGate.Ask("scan.json", settings, IntPtr.Zero, path => R435_ScanViewTests.Scan(false, 2).ToString()).GoOn);
            Assert.Equal(1, shown);
        }
    }
}
#endif
