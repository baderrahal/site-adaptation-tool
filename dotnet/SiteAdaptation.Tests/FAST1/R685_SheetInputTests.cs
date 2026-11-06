#if NETFRAMEWORK
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R685. Stage 1 sends the parameter sheet with the data check, so the need sharedParameters of R700 reads it. Made up files only
    public sealed class R685_SheetInputTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "R685_" + Guid.NewGuid().ToString("N"));

        public R685_SheetInputTests()
        {
            Directory.CreateDirectory(Path.Combine(root, "config"));
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private PaneSettings Settings(string named)
        {
            string config = Path.Combine(root, "config", "project.json");
            File.WriteAllText(config, new JObject { ["addin"] = new JObject { ["parameterSheet"] = named } }.ToString());
            return new PaneSettings { ConfigFile = config };
        }

        [Fact]
        public void R685_the_sheet_of_the_settings_goes_first_then_the_one_of_config_and_none_when_neither_is_on_disk()
        {
            PaneSettings settings = Settings("config/sheet.xlsx");
            // Broken on purpose: neither file on disk, no input, as the runner refuses an input that is not
            Assert.Null(DataCheckStep.SheetInput(settings));
            File.WriteAllText(Path.Combine(root, "config", "sheet.xlsx"), "made up");
            WorkerFile fromConfig = DataCheckStep.SheetInput(settings);
            Assert.Equal(InputNames.ParameterSheet, fromConfig.Role);
            Assert.Equal(Path.Combine(root, "config", "sheet.xlsx"), fromConfig.Path);
            Assert.Equal(string.Empty, fromConfig.Sha256);
            string picked = Path.Combine(root, "picked.xlsx");
            settings.Pick(InputKeys.ParameterSheet, picked);
            // A picked sheet that is gone falls back on the one of config
            Assert.Equal(fromConfig.Path, DataCheckStep.SheetInput(settings).Path);
            File.WriteAllText(picked, "made up");
            Assert.Equal(picked, DataCheckStep.SheetInput(settings).Path);
            // A config that names no sheet and no pick gives none
            Assert.Null(DataCheckStep.SheetInput(Settings("")));
        }
    }
}
#endif
