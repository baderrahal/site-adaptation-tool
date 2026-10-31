using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R438, the gate of button 2 after the scan, and its wiring in ReadDataBody. net48 only, it loads the add-in
    public sealed class ScanGateTests : IDisposable
    {
        private readonly Func<IntPtr, string, ProjectSettings, ProjectSettings> before = ScanGate.Window;
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_scangate_" + Guid.NewGuid().ToString("N"));

        public ScanGateTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            ScanGate.Window = before;
            Directory.Delete(folder, true);
        }

        private static Func<string, string> Text(bool villaClear, int conflicts)
        {
            return path => R435_ScanViewTests.Scan(villaClear, conflicts).ToString();
        }

        [Fact]
        public void R438_every_value_clear_and_no_conflict_gives_one_line_and_no_window()
        {
            ScanGate.Window = (owner, path, settings) => throw new InvalidOperationException("no window");
            ScanGateAnswer answer = ScanGate.Ask("scan.json", new PaneSettings(), IntPtr.Zero, Text(true, 0));
            Assert.True(answer.GoOn);
            Assert.Equal("Read 5 files, 5 plots, every value clear.", answer.Line);
        }

        [Fact]
        public void R438_a_value_to_pick_or_a_conflict_open_opens_the_window_and_cancel_stops_the_read()
        {
            int shown = 0;
            ScanGate.Window = (owner, path, settings) => { shown++; return null; };
            Assert.False(ScanGate.Ask("scan.json", new PaneSettings(), IntPtr.Zero, Text(false, 0)).GoOn);
            Assert.False(ScanGate.Ask("scan.json", new PaneSettings(), IntPtr.Zero, Text(true, 2)).GoOn);
            Assert.Equal(2, shown);
        }

        [Fact]
        public void R438_use_these_picks_lays_the_picks_into_the_pane_settings_and_the_read_goes_on()
        {
            var settings = new PaneSettings();
            ScanGate.Window = (owner, path, current) =>
            {
                ProjectSettings next = SettingsJson.Read(SettingsJson.Write(current));
                next.ScanPicks = new SortedDictionary<string, string> { ["villaType"] = "list.xlsx|Sheet 1|Type|shift 0" };
                next.ConflictPicks = new SortedDictionary<string, string> { ["1|villa type"] = "feet.dwg|block|BLOCKS" };
                return next;
            };
            ScanGateAnswer answer = ScanGate.Ask("scan.json", settings, IntPtr.Zero, Text(false, 1));
            Assert.True(answer.GoOn);
            Assert.Equal(ScanResultsText.PicksSaved, answer.Line);
            Assert.Equal("list.xlsx|Sheet 1|Type|shift 0", settings.Project.ScanPicks["villaType"]);
            Assert.Equal("feet.dwg|block|BLOCKS", settings.Project.ConflictPicks["1|villa type"]);
        }

        [Fact]
        public void R438_the_scan_of_button_2_takes_the_settings_file_as_config_and_the_drawings_of_the_pane()
        {
            string config = Path.Combine(folder, "project.json");
            string site = Path.Combine(folder, "site.dwg");
            File.WriteAllText(config, "{}");
            File.WriteAllText(site, "x");
            var settings = new PaneSettings { ConfigFile = config };
            settings.Inputs[InputNames.Baseplan] = new SettingsInput { Path = site };
            IReadOnlyList<Common.WorkerFile> inputs = ReadDataBody.ScanInputs(settings);
            Assert.Equal(new[] { "scanCad:1", ReadInputs.Config }, inputs.Select(input => input.Role));
        }
    }
}
