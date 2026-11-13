#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests.FAST6
{
    // R750, F10 of run 1 of FAST-3. The sheet a data check takes, from the project settings else config. A made up folder and the parameter
    // sheet of the repo, which holds no project data. R847: the data check before the old read of step 2 went with that read, its test of the
    // request with it, the read of the folder takes the same sheet (F10_RawReadSheetTests)
    public sealed class R750_DataCheckSheetTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "R750_" + Guid.NewGuid().ToString("N"));

        public R750_DataCheckSheetTests()
        {
            Directory.CreateDirectory(Path.Combine(root, "input"));
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private static string RepoSheet()
        {
            return Path.Combine(ThemeGateTests.RepoRoot(), "config", "shared_parameters.xlsx");
        }

        // A copy of the config of the repo on a folder of the test, naming the sheet as given
        private string Config(string sheet)
        {
            JObject config = JObject.Parse(File.ReadAllText(R405_DesignValuesTests.RepoConfigPath()));
            config["inputRoot"] = Path.Combine(root, "input");
            config["addin"]["parameterSheet"] = sheet;
            string file = Path.Combine(root, "config", "project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, config.ToString());
            return file;
        }

        [Fact]
        public void R750_the_sheet_comes_from_the_project_settings_else_config_and_never_from_a_file_that_is_not_there()
        {
            string config = Config(RepoSheet());
            Assert.Equal(Path.GetFullPath(RepoSheet()), DataCheckSheet.Input(null, config).Path);
            var project = new ProjectSettings { ConfigFile = config };
            project.Inputs[InputNames.ParameterSheet] = new SettingsInput { Path = Path.Combine(root, "gone.xlsx") };
            Assert.Equal(Path.GetFullPath(RepoSheet()), DataCheckSheet.Input(project, config).Path);
            Assert.Equal(string.Empty, DataCheckSheet.Input(project, config).Sha256);
            Assert.Null(DataCheckSheet.Input(project, Path.Combine(root, "no_config.json")));
            Assert.Null(DataCheckSheet.Input(null, null));
            File.WriteAllText(config, "not json");
            Assert.Null(DataCheckSheet.Input(null, config));
        }
    }
}
#endif
