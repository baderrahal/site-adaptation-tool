using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;
#if NETFRAMEWORK
using SiteAdaptation.AddIn.Fast5;
using SiteAdaptation.AddIn.Pane;
#endif

namespace SiteAdaptation.Tests
{
    // R840 and R843, task OLD-1d1-build-2. The Build runs on the input folder alone: the read for the build takes the plots and the labels of the
    // road levels from the plot table of the read of card 1, no folder named data_book is asked for, and a stop of the Build names only stops.
    // Every file name, plot number and area is made up, every drawing a stand in. Nothing here reads a real work folder
    public sealed class R840_BuildWithNoDataBookTests : IDisposable
    {
        private const string Road = "Made Up Road A";
        private const string Table = "{\"plots\":[{\"baseplanLabel\":\"MU-1\",\"officialNumber\":\"9001\",\"nbhId\":\"Z9\",\"typologyRev9\":\"T1\"},"
            + "{\"baseplanLabel\":\"MU-2\",\"locationPlanNumber\":\"9002\",\"nbhId\":\"Z9\"}]}";
        private readonly string root = Path.Combine(Path.GetTempPath(), "r840-" + Guid.NewGuid().ToString("N"));

        public R840_BuildWithNoDataBookTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private string Work => Path.Combine(root, "work root", "model_1234abcd");

        private string Folder()
        {
            string folder = Path.Combine(root, "project");
            foreach (string file in new[] { "Made Plan.dwg", "Made Houses.dwg", "Made Roads.dwg", "Made Book D.xlsx", "Made Book S.xlsx", Path.Combine("surfaces", Road + ".xml"),
                Path.Combine("protos", "Made-A.rvt") })
            {
                string path = Path.Combine(folder, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "stand in");
            }
            return folder;
        }

        // The settings of the repo as they ship, with the made up folder and names. The data book they name for the old read is nowhere on disk
        private string ConfigFile(string folder)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["inputRoot"] = folder;
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["designBasis"]["roadLevel"]["surfaces"] = new JArray(Road);
            config["designBasis"]["roadLevel"]["notRoad"] = new JObject();
            config["drawings"] = new JObject { ["baseplan"] = "Made Plan.dwg", ["villaBlocks"] = "Made Houses.dwg", ["roadLayout"] = "Made Roads.dwg", ["referenceOnly"] = new JArray() };
            config["dataBook"]["rev9"]["workbooks"] = new JObject { ["d"] = "Made Book D.xlsx", ["s"] = "Made Book S.xlsx" };
            config["addin"]["workbookRoles"] = new JObject { ["d"] = "dlus", ["s"] = "sales" };
            string home = Path.Combine(root, "config");
            Directory.CreateDirectory(home);
            File.WriteAllText(Path.Combine(home, (string)config["addin"]["readData"]["cadLayers"]), "{}");
            string file = Path.Combine(home, "project.json");
            File.WriteAllText(file, config.ToString());
            return file;
        }

        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string>
        {
            ["Settings"] = "config", ["Baseplan"] = "baseplan", ["Villa blocks"] = "villaBlocks", ["Curbs"] = "curbs", ["Road surface"] = ReadInputs.RoadSurface,
            ["DLUS"] = "dlus", ["Sales"] = "sales",
        };

        private static List<RunInput> Pane(string configFile, string folder)
        {
            return new List<RunInput>
            {
                new RunInput("Settings", configFile, false), new RunInput("Baseplan", Path.Combine(folder, "Made Plan.dwg"), false),
                new RunInput("Villa blocks", Path.Combine(folder, "Made Houses.dwg"), false), new RunInput("Curbs", Path.Combine(folder, "Made Roads.dwg"), false),
                new RunInput("Road surface", Path.Combine(folder, "surfaces"), true), new RunInput("DLUS", Path.Combine(folder, "Made Book D.xlsx"), false),
                new RunInput("Sales", Path.Combine(folder, "Made Book S.xlsx"), false),
            };
        }

        // The read of card 1 as the data check leaves it: its record and its plot table
        private string CardOneRead(string table)
        {
            string folder = Path.Combine(Work, "read_made1");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, ReadDataRun.PlotTableFile), table);
            File.WriteAllText(Path.Combine(folder, ReadDataRun.RecordFile),
                new JObject { ["format"] = ReadDataRun.RecordFormat, ["runId"] = "2_20261112-010000-000", ["inputs"] = new JArray(), ["outputs"] = new JArray() }.ToString());
            return folder;
        }

        [Fact]
        public void R840_the_read_for_the_build_takes_plots_and_labels_from_the_read_of_card_1_with_no_data_book_on_disk()
        {
            string folder = Folder();
            string configFile = ConfigFile(folder);
            CardOneRead(Table);
            Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(Work), "data_book")));
            ReadPlan plan = ReadInputs.ForBuild(Pane(configFile, folder), Roles, Work, null, DesignBasis.Rev6);
            Assert.Empty(plan.Stops);
            Assert.Contains(ReadSteps.RoadLevels, plan.Steps);
            ReadInput plots = plan.Inputs.Single(input => input.Role == "plotsCsv"), labels = plan.Inputs.Single(input => input.Role == "labels");
            // Both files lie in the work folder of the model, written from the plot table of the read
            Assert.StartsWith(Path.GetFullPath(Work) + Path.DirectorySeparatorChar, plots.Path);
            Assert.StartsWith(Path.GetFullPath(Work) + Path.DirectorySeparatorChar, labels.Path);
            string[] rows = File.ReadAllLines(plots.Path);
            Assert.Equal(string.Join(",", ProjectPlots.PlotsColumns), rows[0]);
            Assert.Equal(new[] { "9001,MU-1,T1,Z9,,", "9002,MU-2,,Z9,," }, rows.Skip(1).ToArray());
            Assert.Equal(new[] { "kind,number,nbh", "clientCode,9001,Z9", "clientCode,9002,Z9" }, File.ReadAllLines(labels.Path));
            Assert.DoesNotContain(plan.Inputs, input => input.Role == "border" || input.Path.IndexOf("data_book", StringComparison.OrdinalIgnoreCase) >= 0);
            // The same table gives the same files, so the read is the same read
            Assert.Equal(plan.Hash, ReadInputs.ForBuild(Pane(configFile, folder), Roles, Work, null, DesignBasis.Rev6).Hash);
        }

        [Fact]
        public void R840_with_no_read_or_no_plot_in_the_table_the_read_for_the_build_stops_and_names_the_plot_table()
        {
            string folder = Folder();
            string configFile = ConfigFile(folder);
            string none = Assert.Single(ReadInputs.ForBuild(Pane(configFile, folder), Roles, Work, null, DesignBasis.Rev6).Stops);
            Assert.StartsWith("the plots of the project are missing", none);
            Assert.DoesNotContain("data book", none);
            CardOneRead("{\"plots\":[]}");
            string empty = Assert.Single(ReadInputs.ForBuild(Pane(configFile, folder), Roles, Work, null, DesignBasis.Rev6).Stops);
            Assert.Contains("holds no plot with a number", empty);
        }

        [Fact]
        public void R843_a_line_of_why_the_folder_gave_no_file_is_a_cause_only_for_a_row_still_empty()
        {
            string folder = Folder();
            JObject config = JObject.Parse(File.ReadAllText(ConfigFile(folder)));
            File.Delete(Path.Combine(folder, "surfaces", Road + ".xml"));
            File.Delete(Path.Combine(folder, "protos", "Made-A.rvt"));
            config["drawings"]["villaBlocks"] = "Not There.dwg";
            FolderRoles.Found found = FolderRoles.Of(config, folder, null);
            Assert.Equal(new[] { "villaBlocks: Not There.dwg is not in the folder", "roadSurface: no folder holds a road surface named in config or picked", "prototypes: no folder holds a prototype file" }, found.Left);
            var picked = new HashSet<string> { InputNames.RoadSurface, InputNames.Prototypes };
            Assert.Equal(new[] { "villaBlocks: Not There.dwg is not in the folder" }, FolderRoles.Causes(found, picked.Contains));
            Assert.Equal(found.Left, FolderRoles.Causes(found, role => false));
            // A line about the folder as a whole is about no row and stays
            FolderRoles.Found gone = FolderRoles.Of(config, Path.Combine(root, "no such folder"), null);
            Assert.Equal(gone.Left, FolderRoles.Causes(gone, role => true));
            Assert.Single(gone.Left);
        }

#if NETFRAMEWORK
        [Fact]
        public void R843_a_stop_of_the_build_names_no_road_surface_or_prototype_note_when_both_rows_are_picked()
        {
            string folder = Folder();
            var settings = new PaneSettings { ConfigFile = ConfigFile(folder) };
            // The folder holds no road surface of the list and no prototype file, and a person picked both rows elsewhere
            File.Delete(Path.Combine(folder, "surfaces", Road + ".xml"));
            File.Delete(Path.Combine(folder, "protos", "Made-A.rvt"));
            string surfaces = Path.Combine(root, "picked surfaces"), prototypes = Path.Combine(root, "picked prototypes");
            Directory.CreateDirectory(surfaces);
            File.WriteAllText(Path.Combine(surfaces, Road + ".xml"), "stand in");
            Directory.CreateDirectory(prototypes);
            settings.Pick(InputKeys.RoadSurface, surfaces);
            settings.Pick(InputKeys.Prototypes, prototypes);
            BuildRead.FillRows(settings, settings.Pick);
            // The stop: the work folder holds no read of card 1
            bool? ok = null;
            string line = null;
            BuildRead.Start(settings, Work, new WorkerDocument { Path = Path.Combine(Work, "a.rvt"), SessionId = "s1" }, new NoWorker(), "2 Made up read", (done, said) => { ok = done; line = said; });
            Assert.False(ok);
            Assert.StartsWith("the plots of the project are missing", line);
            Assert.DoesNotContain("roadSurface:", line);
            Assert.DoesNotContain("prototypes:", line);
            Assert.DoesNotContain("data book", line);
            // With the prototype row empty its line is a cause again
            settings.Pick(InputKeys.Prototypes, null);
            BuildRead.Start(settings, Work, new WorkerDocument { Path = Path.Combine(Work, "a.rvt"), SessionId = "s1" }, new NoWorker(), "2 Made up read", (done, said) => { ok = done; line = said; });
            Assert.Contains("prototypes: no folder holds a prototype file", line);
            Assert.DoesNotContain("roadSurface:", line);
        }

        private sealed class NoWorker : IWorkerStart
        {
            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                throw new InvalidOperationException("a read that stops starts no worker");
            }
        }
#endif
    }
}
