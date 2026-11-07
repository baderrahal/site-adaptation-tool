#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Fast7;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R768, F2 of run 1 of FAST-3. Set up site takes the Baseplan of the chosen project folder into an empty Baseplan row, and only that row.
    // Every file name is made up, every file is a stand in
    public sealed class R768_SetUpBaseplanTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "r768-" + Guid.NewGuid().ToString("N"));

        public R768_SetUpBaseplanTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        // A made up project folder and the config copy of a new project that names it, with the Baseplan by its file name
        private PaneSettings Project(string baseplanName)
        {
            string folder = Path.Combine(root, "project");
            foreach (string file in new[] { "Made Plan.dwg", "Made Houses.dwg", Path.Combine("protos", "Made-A.rvt") })
            {
                string path = Path.Combine(folder, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "stand in");
            }
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["inputRoot"] = folder;
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["drawings"] = new JObject { ["baseplan"] = baseplanName, ["villaBlocks"] = "Made Houses.dwg", ["referenceOnly"] = new JArray() };
            string configFolder = Path.Combine(root, "config");
            Directory.CreateDirectory(configFolder);
            string configFile = Path.Combine(configFolder, "project.json");
            File.WriteAllText(configFile, config.ToString());
            return new PaneSettings { ConfigFile = configFile };
        }

        [Fact]
        public void R768_an_empty_baseplan_row_takes_the_baseplan_of_the_folder_and_no_other_row_is_touched()
        {
            PaneSettings settings = Project("Made Plan.dwg");
            var picked = new List<string>();
            string taken = SetUpBaseplan.Fill(settings, (key, path) =>
            {
                picked.Add(key);
                settings.Pick(key, path);
            });
            string expected = Path.Combine(root, "project", "Made Plan.dwg");
            Assert.Equal(expected, taken);
            Assert.Equal(expected, settings.InputPath(InputKeys.Baseplan));
            // The Build fills the villa blocks too, Set up site leaves them for the Build
            Assert.Equal(new[] { InputKeys.Baseplan }, picked);
            Assert.Null(settings.InputPath(InputKeys.VillaBlocks));
        }

        [Fact]
        public void R768_a_baseplan_a_person_set_stays_and_a_name_the_folder_lacks_leaves_the_row_empty()
        {
            PaneSettings settings = Project("Made Plan.dwg");
            string own = Path.Combine(root, "my own plan.dwg");
            File.WriteAllText(own, "stand in");
            settings.Pick(InputKeys.Baseplan, own);
            Assert.Null(SetUpBaseplan.Fill(settings, settings.Pick));
            Assert.Equal(own, settings.InputPath(InputKeys.Baseplan));

            // Broken on purpose: the config names a Baseplan the folder does not hold, so nothing is taken
            PaneSettings missing = Project("No Such Plan.dwg");
            Assert.Null(SetUpBaseplan.Fill(missing, missing.Pick));
            Assert.Null(missing.InputPath(InputKeys.Baseplan));

            // No settings or no way to pick changes nothing
            Assert.Null(SetUpBaseplan.Fill(null, missing.Pick));
            Assert.Null(SetUpBaseplan.Fill(missing, null));
        }
    }
}
#endif
