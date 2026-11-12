using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R830 to R834, task OLD-1p-build-1. The folder read leaves out the past outputs of the input folder: the folders of dataCheck.skipFolders and
    // the files of dataCheck.skipFiles, by the worker walk, the hash of the add-in and the folder roles alike. The tree is made up, every file is
    // a stand in, and the only names are those config/project.json names itself
    public sealed class R830_SkipPastOutputsTests : IDisposable
    {
        private const string Villa = "X-ARC-NEW1-ZZ-MOD-1.rvt";
        private static readonly string[] PastFolders = { "01_Output", "Native Files", "Revit All Zones" };
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa_r830_" + Guid.NewGuid().ToString("N"));
        private int runs;

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static JObject RepoConfig()
        {
            return JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
        }

        // The file names of two site models as answerKeys of config names them, no other name of a project is in this file
        private static string[] SiteModels()
        {
            return ((JArray)RepoConfig()["answerKeys"]["keys"]).Select(key => Path.GetFileName((string)key["source"])).Distinct().Take(2).ToArray();
        }

        // The made up tree: the sample drawing and workbook, the three past folders with three Revit files each, and one model folder that holds
        // two site models as single files beside the folder of the villa file
        private string Tree()
        {
            string folder = Path.Combine(root, "handed over");
            Directory.CreateDirectory(folder);
            string sample = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(WorkerRunTests.RepoConfig())), "samples", "new-project");
            foreach (string file in Directory.GetFiles(sample, "*.dxf").Concat(Directory.GetFiles(sample, "*.xlsx")))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), true);
            }
            foreach (string past in PastFolders)
            {
                foreach (string name in new[] { "Old-A.rvt", "Old-B.rvt", "Old-C.rvt" })
                {
                    Stand(Path.Combine(folder, "sub", past, name));
                }
            }
            foreach (string model in SiteModels())
            {
                Stand(Path.Combine(folder, "models", model));
            }
            Stand(Path.Combine(folder, "models", "villas", Villa));
            return folder;
        }

        private static void Stand(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "stand in");
        }

        private string ConfigFile(string folder, Action<JObject> change = null)
        {
            JObject config = RepoConfig();
            config["inputRoot"] = folder;
            config["designBasis"]["mode"] = DesignBasis.Rev6;
            config["siteBuild"]["minFreeGB"] = 0.1;
            change?.Invoke(config);
            string path = Path.Combine(root, "project.json");
            File.WriteAllText(path, config.ToString());
            return path;
        }

        private string Work()
        {
            string work = Path.Combine(root, "work", "model");
            Directory.CreateDirectory(work);
            return work;
        }

        // Reader 1, the walk of the worker: button 2 as the add-in presses it, with the worker command itself. Gives data_check.json and the gathered files
        private JObject[] WorkerRead(string configPath, string work)
        {
            var worker = new Worker();
            var run = new ReadDataRun(PaneRoles, worker, new WorkerDocument { Path = Document, SessionId = "s1" }, () => new SettingsScope()) { ModePick = () => DesignBasis.Rev6 };
            var flow = new ButtonFlow(new State(configPath, work), new NoModel(), new Yes(), InputHash.Of, () => RunIds.New(2, DateTime.Now) + "-" + (++runs).ToString("D3"));
            RunOutcome last = null;
            RunOutcome started = flow.Run(2, "2 Read data", run.Body(), Document, outcome => last = outcome);
            RunOutcome done = started.End == RunEnd.Started ? last : started;
            Assert.True(done.End == RunEnd.Committed, done.Message);
            string read = ReviewLoad.LatestReadFolder(work);
            return new[] { JObject.Parse(File.ReadAllText(Path.Combine(read, "data_check.json"))), JObject.Parse(File.ReadAllText(Path.Combine(read, DataCheckApply.InputFile))) };
        }

        // Reader 2, the hash and plan of the add-in
        private static ReadInput Walked(string configPath, string work)
        {
            ReadPlan plan = ReadInputs.Raw(new[] { new RunInput("Settings", configPath, false) }, PaneRoles, work, DesignBasis.Rev6);
            Assert.Empty(plan.Stops);
            return plan.Inputs.Single(input => input.Role == ReadInputs.ScanFolder);
        }

        // Reader 3, the folder roles
        private static string VillaFolder(string configPath, string folder)
        {
            FolderRoles.Found found = FolderRoles.Of(JObject.Parse(File.ReadAllText(configPath)), folder, null);
            return found.Paths.TryGetValue(InputNames.Prototypes, out string path) ? path : string.Join(". ", found.Left);
        }

        private static string[] Names(JToken list)
        {
            return ((JArray)list).Select(item => (string)item).ToArray();
        }

        [Fact]
        public void R830_R831_R832_R833_the_four_past_places_are_left_out_by_all_three_readers_and_named_and_the_villa_folder_is_read()
        {
            string folder = Tree();
            string configPath = ConfigFile(folder);
            string work = Work();
            string[] models = SiteModels();
            Assert.Equal(2, models.Length);

            // Reader 1, the worker: the villa file is read, no past file is, and the result counts and names what was left out
            JObject[] read = WorkerRead(configPath, work);
            Assert.Equal(new[] { Villa }, Names(read[1]["prototypeNames"]));
            Assert.Equal(PastFolders.Select(past => "sub/" + past), Names(read[0]["skippedFolders"]).OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal(models.Select(model => "models/" + model).OrderBy(name => name, StringComparer.Ordinal), Names(read[0]["skippedFiles"]));
            string line = (string)read[0]["skippedLine"];
            Assert.Contains("3 folders", line);
            Assert.Contains("2 files", line);
            Assert.Contains("dataCheck.skipFolders", line);
            Assert.Contains("dataCheck.skipFiles", line);

            // Reader 2, the hash: the sample files and the villa file are held, a change in a past place is no new read, a change of the villa file is one
            ReadInput before = Walked(configPath, work);
            Assert.Equal(Directory.GetFiles(folder).Length + 1, before.FilesHeld);
            foreach (string past in PastFolders)
            {
                File.AppendAllText(Path.Combine(folder, "sub", past, "Old-A.rvt"), "changed");
            }
            File.AppendAllText(Path.Combine(folder, "models", models[0]), "changed");
            Assert.Equal(before.Sha256, Walked(configPath, work).Sha256);
            File.AppendAllText(Path.Combine(folder, "models", "villas", Villa), "changed");
            Assert.NotEqual(before.Sha256, Walked(configPath, work).Sha256);

            // Reader 3, the folder roles: the folder of the one villa file, not a folder of two site models or of three past files
            Assert.Equal(Path.Combine(folder, "models", "villas"), VillaFolder(configPath, folder));
        }

        // The broken case of R831: with no file pattern every reader takes the site models, which a pass of the test above could not tell
        [Fact]
        public void R832_broken_with_no_file_pattern_all_three_readers_take_the_site_models()
        {
            string folder = Tree();
            string configPath = ConfigFile(folder, config => config[DataCheckSettings.Block]["skipFiles"] = new JArray());
            string work = Work();
            string[] models = SiteModels();
            JObject[] read = WorkerRead(configPath, work);
            Assert.Equal(models.Concat(new[] { Villa }).OrderBy(name => name, StringComparer.Ordinal), Names(read[1]["prototypeNames"]).OrderBy(name => name, StringComparer.Ordinal));
            Assert.Empty(Names(read[0]["skippedFiles"]));
            Assert.Contains("0 files", (string)read[0]["skippedLine"]);
            Assert.Equal(Directory.GetFiles(folder).Length + 3, Walked(configPath, work).FilesHeld);
            Assert.Equal(Path.Combine(folder, "models"), VillaFolder(configPath, folder));
        }

        // The broken case of R830: with the two new folders off the list every reader walks them
        [Fact]
        public void R832_broken_with_the_two_folders_off_the_list_all_three_readers_walk_them()
        {
            string folder = Tree();
            string configPath = ConfigFile(folder, config => config[DataCheckSettings.Block]["skipFolders"] = new JArray(PastFolders[0]));
            string work = Work();
            JObject[] read = WorkerRead(configPath, work);
            Assert.Equal(7, Names(read[1]["prototypeNames"]).Length);
            Assert.Equal(new[] { "sub/" + PastFolders[0] }, Names(read[0]["skippedFolders"]));
            Assert.Contains("1 folder ", (string)read[0]["skippedLine"]);
            Assert.Equal(Directory.GetFiles(folder).Length + 7, Walked(configPath, work).FilesHeld);
            Assert.Contains("prototypes: two folders hold 3 each", VillaFolder(configPath, folder));
        }

        // R834. A list of the wrong shape is refused by every reader with a line that names the list, never read as no pattern
        [Theory]
        [InlineData("not a list")]
        [InlineData("(")]
        public void R834_a_file_pattern_list_of_the_wrong_shape_is_refused_by_all_three_readers(string wrong)
        {
            string folder = Tree();
            string configPath = ConfigFile(folder, config => config[DataCheckSettings.Block]["skipFiles"] = wrong == "(" ? (JToken)new JArray(wrong) : wrong);
            JObject config = JObject.Parse(File.ReadAllText(configPath));
            Assert.Contains("dataCheck.skipFiles", Assert.Throws<InvalidDataException>(() => DataCheckSettings.From(config)).Message);
            ReadPlan plan = ReadInputs.Raw(new[] { new RunInput("Settings", configPath, false) }, PaneRoles, Work(), DesignBasis.Rev6);
            Assert.Contains("dataCheck.skipFiles", Assert.Single(plan.Stops));
            Assert.Contains("dataCheck.skipFiles", VillaFolder(configPath, folder));
        }

        // R834. A project whose settings hold no file pattern list skips no file, a list is asked of no project
        [Fact]
        public void R834_no_file_pattern_list_skips_no_file()
        {
            string folder = Tree();
            string configPath = ConfigFile(folder, config => ((JObject)config[DataCheckSettings.Block]).Remove("skipFiles"));
            Assert.Equal(Directory.GetFiles(folder).Length + 3, Walked(configPath, Work()).FilesHeld);
        }

        // R830, R831. The settings of the repo hold the two folders, and the file patterns take every site model answerKeys names and no villa file
        [Fact]
        public void R830_R831_config_holds_the_two_folders_and_patterns_that_take_each_site_model_and_no_villa_file()
        {
            JObject config = RepoConfig();
            string[] folders = Names(config[DataCheckSettings.Block]["skipFolders"]);
            Assert.Contains("01_Output", folders);
            Assert.Contains("Revit All Zones", folders);
            Assert.Contains("Native Files", folders);
            DataCheckSettings settings = DataCheckSettings.From(config);
            JObject[] keys = ((JArray)config["answerKeys"]["keys"]).OfType<JObject>().ToArray();
            Assert.NotEmpty(keys);
            foreach (JObject key in keys)
            {
                Assert.True(DataCheckSettings.SkipsFile(settings.SkipFiles, Path.GetFileName((string)key["source"])), (string)key["source"]);
                Assert.True(DataCheckSettings.SkipsFile(settings.SkipFiles, ((string)key["copy"]).ToUpperInvariant()), (string)key["copy"]);
            }
            JObject names = (JObject)config["prototypeNames"];
            string villa = (string)names["prefix"] + (string)names["discipline"] + "-NEW1" + (string)names["suffix"] + "1.rvt";
            Assert.False(DataCheckSettings.SkipsFile(settings.SkipFiles, villa));
            // A site model name on a file that is no Revit file is no site model, the pattern holds the file type
            Assert.False(DataCheckSettings.SkipsFile(settings.SkipFiles, Path.ChangeExtension(Path.GetFileName((string)keys[0]["source"]), ".xlsx")));
            Assert.Contains("CHOSEN by Code", (string)config[DataCheckSettings.Block]["skipFilesWhy"]);
        }

        private static readonly Dictionary<string, string> PaneRoles = new Dictionary<string, string> { ["Settings"] = ReadInputs.Config };
        private const string Document = @"C:\Models\site.rvt";

        private sealed class State : IRunState
        {
            private readonly string configPath;
            private readonly string work;

            public State(string configPath, string work)
            {
                this.configPath = configPath;
                this.work = work;
            }

            public bool DryRunFirst => false;

            public string WorkFolder(string documentPath)
            {
                return work;
            }

            public int MissingStep(int button)
            {
                return 0;
            }

            public IReadOnlyList<RunInput> Inputs(int button)
            {
                return new[] { new RunInput("Settings", configPath, false) };
            }

            public void Committed(int button, RunRecord record)
            {
            }
        }

        private sealed class NoModel : IModel
        {
            public IModelTransaction Start(string name)
            {
                throw new InvalidOperationException("button 2 opens no transaction");
            }
        }

        private sealed class Yes : IConfirm
        {
            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                return true;
            }
        }

        private sealed class Worker : IWorkerStart
        {
            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                WorkerRunner.Run(requestPath, new Dictionary<string, WorkerCommand> { ["dataCheck"] = CadReader.DataCheckCommand.Run }, () => 1e6, () => 0.0);
                ended(WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId)));
            }
        }
    }
}
