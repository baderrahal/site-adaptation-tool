#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.ConfirmProfile;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R636. The window Confirm what was found and the step that shows it after a read of a folder with no past file. net48 only
    public sealed class R636_ConfirmWindowTests : IDisposable
    {
        private const string Check = "{\"needs\":[{\"need\":\"plotOutlines\",\"mark\":\"strong\",\"line\":\"Found 12 plot outlines.\"}],\"files\":[{\"name\":\"a.dwg\",\"status\":\"read\",\"reason\":\"\"}],\"stopped\":\"\"}";

        private const string Profile = "{\"levels\":[{\"level\":1,\"word\":null,\"source\":\"missing\"},{\"level\":2,\"word\":\"Block\",\"source\":\"computed\"}],"
            + "\"codeMap\":{\"proposed\":[{\"old\":\"A1\",\"new\":\"B1\",\"source\":\"computed\"}],\"foundIn\":\"codes.xlsx, Sheet 1\",\"direction\":\"the drawings use the old side more\",\"unsure\":false},"
            + "\"typologyColumn\":{\"proposed\":{\"workbook\":\"w.xlsx\",\"sheet\":\"S\",\"column\":\"Type\",\"plots\":9},\"candidates\":[{\"workbook\":\"w.xlsx\",\"sheet\":\"S\",\"column\":\"Type\",\"plots\":9},{\"workbook\":\"w.xlsx\",\"sheet\":\"S\",\"column\":\"Other\",\"plots\":7}]},"
            + "\"areaColumn\":{\"proposed\":null,\"candidates\":[]},"
            + "\"hands\":{\"proposed\":{\"A1\":\"right\",\"C3\":null}}}";

        private const string Empty = "{\"levels\":[],\"codeMap\":{\"proposed\":[]},\"typologyColumn\":{\"proposed\":null,\"candidates\":[]},\"areaColumn\":{\"proposed\":null,\"candidates\":[]},\"hands\":{\"proposed\":{}}}";

        private readonly Action<IntPtr, DataCheckResult> checkBefore = ConfirmProfileStep.ShowCheck;
        private readonly Action<IntPtr, ConfirmProfileSession> confirmBefore = ConfirmProfileStep.ShowConfirm;
        private readonly Func<string, string, bool> askBefore = ConfirmProfileStep.AskDiscard;
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r636_" + Guid.NewGuid().ToString("N"));

        public R636_ConfirmWindowTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            ConfirmProfileStep.ShowCheck = checkBefore;
            ConfirmProfileStep.ShowConfirm = confirmBefore;
            ConfirmProfileStep.AskDiscard = askBefore;
            Directory.Delete(folder, true);
        }

        private string ConfirmedFile
        {
            get { return Path.Combine(folder, ConfirmedProfile.FileName); }
        }

        private JObject ConfirmedNow()
        {
            return JObject.Parse(File.ReadAllText(ConfirmedFile));
        }

        [Fact]
        public void The_window_shows_what_the_check_proposed()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            Assert.Equal("Type", model.Typology.Column);
            Assert.Equal(2, model.TypologyColumns.Count);
            Assert.Equal("Type in S of w.xlsx, 9 plots (proposed)", model.Typology.Label);
            Assert.Null(model.Area);
            Assert.Equal("A1 -> B1", model.CodeMapText);
            Assert.Equal("Proposed from codes.xlsx, Sheet 1.", model.CodeMapLine);
            Assert.Equal("", model.CodeMapDoubt);
            Assert.Equal(new[] { "A1", "C3" }, model.Hands.Select(hand => hand.Code));
            Assert.Equal("no guess", model.Hands[1].GuessText);
            Assert.Equal("", model.Levels[0].Word);
            Assert.Equal("Block", model.Levels[1].Word);
        }

        [Fact]
        public void A_hand_guess_is_shown_and_does_not_start_selected_so_a_press_writes_no_hand()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            Assert.Equal("guess: right", model.Hands[0].GuessText);
            Assert.Equal("", model.Hands[0].Chosen);
            new ConfirmProfileSession(model, folder, "run").Confirm();
            Assert.Null(ConfirmedNow()["hands"]);
            model.Hands[0].Chosen = ConfirmedProfile.Left;
            new ConfirmProfileSession(model, folder, "run").Confirm();
            Assert.Equal("left", (string)ConfirmedNow()["hands"]["A1"]);
        }

        [Fact]
        public void A_press_on_Confirm_takes_both_columns_and_the_code_map_as_the_window_shows_them()
        {
            new ConfirmProfileSession(ConfirmProfileModel.Read(Profile), folder, "run").Confirm();
            JObject file = ConfirmedNow();
            Assert.Equal("Type", (string)file["typologyColumn"]["column"]);
            Assert.Equal("B1", (string)file["codeMap"][0]["new"]);
            Assert.Null(file["areaColumn"]);
        }

        [Fact]
        public void A_computed_level_word_left_alone_is_not_written_a_typed_one_is()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            Assert.Equal("found by the tool", model.Levels[1].SourceText);
            new ConfirmProfileSession(model, folder, "run").Confirm();
            Assert.Null(ConfirmedNow()["levels"]);
            model.Levels[0].Word = "Area";
            new ConfirmProfileSession(model, folder, "run").Confirm();
            Assert.Equal(new[] { 1 }, ConfirmedNow()["levels"].Select(row => (int)row["level"]));
            model.Levels[1].Word = "Cluster";
            new ConfirmProfileSession(model, folder, "run").Confirm();
            Assert.Equal(new[] { 1, 2 }, ConfirmedNow()["levels"].Select(row => (int)row["level"]));
        }

        [Fact]
        public void A_word_confirmed_earlier_stays_and_says_so_the_header_of_a_confirmed_area_column_is_no_typed_word()
        {
            JObject profile = JObject.Parse(Profile);
            profile["levels"][0]["word"] = "Zone header";
            profile["levels"][0]["source"] = ConfirmedProfile.PersonEntered;
            profile["levels"][1]["word"] = "Typed";
            profile["levels"][1]["source"] = ConfirmedProfile.PersonEntered;
            profile["areaColumn"]["confirmed"] = new JObject { ["workbook"] = "w.xlsx", ["sheet"] = "S", ["column"] = "Zone header" };
            ConfirmProfileModel model = ConfirmProfileModel.Read(profile.ToString());
            Assert.Equal("the header of the area column", model.Levels[0].SourceText);
            Assert.Equal("confirmed on an earlier read", model.Levels[1].SourceText);
            new ConfirmProfileSession(model, folder, "run").Confirm();
            Assert.Equal(new[] { 2 }, ConfirmedNow()["levels"].Select(row => (int)row["level"]));
        }

        [Fact]
        public void A_code_map_whose_direction_is_a_guess_or_unsure_says_so_before_anyone_confirms_it()
        {
            // The text the check writes for a table read left to right, private in Common, held here so a change of it fails this test
            FieldInfo guess = typeof(DataCheckLogic).GetField("Guess", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.StartsWith(ConfirmProfileModel.DirectionGuessWord, (string)guess.GetValue(null));
            JObject profile = JObject.Parse(Profile);
            profile["codeMap"]["direction"] = (string)guess.GetValue(null);
            Assert.Equal(ConfirmProfileText.CodeMapGuess, ConfirmProfileModel.Read(profile.ToString()).CodeMapDoubt);
            profile["codeMap"]["direction"] = "the drawings use the old side more";
            profile["codeMap"]["unsure"] = true;
            Assert.Equal(ConfirmProfileText.CodeMapUnsure, ConfirmProfileModel.Read(profile.ToString()).CodeMapDoubt);
            profile["codeMap"]["confirmed"] = new JArray(new JObject { ["old"] = "A1", ["new"] = "B1" });
            Assert.Equal("", ConfirmProfileModel.Read(profile.ToString()).CodeMapDoubt);
        }

        [Fact]
        public void The_words_of_a_guess_and_of_a_proposal_show_only_when_there_is_one()
        {
            Assert.True(ConfirmProfileModel.Read(Profile).AnyGuess);
            Assert.Equal(ConfirmProfileText.IntroFound, ConfirmProfileModel.Read(Profile).Intro);
            ConfirmProfileModel empty = ConfirmProfileModel.Read(Empty);
            Assert.False(empty.AnyGuess);
            Assert.Equal(ConfirmProfileText.IntroNothing, empty.Intro);
            Assert.Equal(ConfirmProfileText.CodeMapNone, empty.CodeMapLine);
        }

        [Fact]
        public void A_check_that_proposed_nothing_gives_a_window_with_nothing_picked_and_nothing_to_save()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Empty);
            Assert.Null(model.Typology);
            Assert.Null(model.Area);
            Assert.Empty(model.Hands);
            Assert.True(model.GivesNothing);
            var session = new ConfirmProfileSession(model, folder, "run");
            session.Confirm();
            Assert.Equal(ConfirmProfileText.NothingGiven, session.Result);
            Assert.False(File.Exists(ConfirmedFile));
        }

        [Fact]
        public void Clearing_every_block_removes_the_file_of_an_earlier_confirm_and_says_so()
        {
            File.WriteAllText(ConfirmedFile, "{\"hands\":{\"A1\":\"left\"}}");
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            model.Typology = null;
            model.CodeMapText = "";
            bool finished = false;
            var session = new ConfirmProfileSession(model, folder, "run");
            session.Finished += () => finished = true;
            session.Confirm();
            Assert.True(finished);
            Assert.Equal(ConfirmProfileText.Removed, session.Result);
            Assert.False(File.Exists(ConfirmedFile));
        }

        [Fact]
        public void Confirm_writes_a_file_the_read_accepts()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            model.Levels[0].Word = "Area";
            model.CodeMapText = "A1 -> B1\r\nC1, D1\r\nQ7 (old note) -> Q9\r\n";
            model.Hands[1].Chosen = "left";
            var session = new ConfirmProfileSession(model, folder, "run");
            session.Confirm();
            Assert.Equal(ConfirmProfileText.Saved, session.Result);
            JObject file = ConfirmedNow();
            Assert.Equal(3, ((JArray)file["codeMap"]).Count);
            Assert.Equal("Q7 (old note)", (string)file["codeMap"][2]["old"]);
            Assert.Equal("left", (string)file["hands"]["C3"]);
            Assert.Equal("Area", (string)file["levels"][0]["word"]);
            Assert.Equal("Type", (string)file["typologyColumn"]["column"]);
            ConfirmedProfile read = ConfirmedProfile.From(file, 2);
            Assert.Equal(3, read.CodeMap.Count);
            Assert.Equal("Type", read.Column.Column);
        }

        [Theory]
        [InlineData("A1 B1\r\nonlyone", "Line 2 of the code map needs")]
        [InlineData("A1 -> B1 -> C1", "Line 1 of the code map needs")]
        [InlineData("A1 ->", "Line 1 of the code map needs")]
        [InlineData("A1 B1\r\nA2 --> B2", "Line 2 of the code map holds a code that starts or ends")]
        [InlineData("A1 ->> B1", "Line 1 of the code map holds a code that starts or ends")]
        [InlineData("-A1 -> B1", "Line 1 of the code map holds a code that starts or ends")]
        [InlineData("A1> B1", "Line 1 of the code map holds a code that starts or ends")]
        public void A_code_map_line_that_is_no_pair_or_holds_a_broken_arrow_is_refused_and_nothing_is_written(string text, string says)
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            model.CodeMapText = text;
            var session = new ConfirmProfileSession(model, folder, "run");
            bool finished = false;
            session.Finished += () => finished = true;
            session.Confirm();
            Assert.False(finished);
            Assert.Contains(says, session.Refusal);
            Assert.StartsWith("Nothing was saved: ", session.Refusal);
            Assert.False(File.Exists(ConfirmedFile));
        }

        [Fact]
        public void A_dash_inside_a_code_is_kept()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            model.CodeMapText = "Q-1 -> Q-2";
            Assert.Equal("Q-1", model.CodeMap().Single().Key);
            Assert.Equal("Q-2", model.CodeMap().Single().Value);
        }

        [Fact]
        public void Cancel_saves_nothing_and_says_so()
        {
            var session = new ConfirmProfileSession(ConfirmProfileModel.Read(Profile), folder, "run");
            session.Cancel();
            Assert.Equal(ConfirmProfileText.CancelledLine, session.Result);
            Assert.False(File.Exists(ConfirmedFile));
        }

        private string ReadFolder()
        {
            string read = Path.Combine(folder, "read_x");
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, "data_check.json"), Check);
            File.WriteAllText(Path.Combine(read, "plots.json"), "{\"plots\":[{\"number\":\"1\"},{\"number\":\"2\"}]}");
            File.WriteAllText(Path.Combine(read, "project_profile.json"), Profile);
            return read;
        }

        // The read behind the step: it counts its starts and ends as it is told
        private sealed class CountingRead
        {
            private readonly RunLater end;

            public CountingRead(RunLater end)
            {
                this.end = end;
            }

            public int Starts { get; private set; }

            public ButtonBody Body()
            {
                return new ButtonBody
                {
                    Preview = context => new RunSummary("p", new string[0]),
                    Start = (context, done) =>
                    {
                        Starts++;
                        done(end);
                    },
                };
            }
        }

        private static RunContext Context(string work)
        {
            return new RunContext(2, "2 Read data", "2_run", work, null, true, new RunInput[0], new Dictionary<string, string>());
        }

        private static PaneSettings NoSettings()
        {
            return new PaneSettings();
        }

        private static RunLater Press(ButtonBody body, RunContext context)
        {
            RunLater ended = null;
            body.Start(context, later => ended = later);
            return ended;
        }

        [Fact]
        public void After_the_read_the_check_of_its_folder_is_shown_once_then_the_confirm_window_and_the_read_starts_once()
        {
            string read = ReadFolder();
            var outputs = Directory.GetFiles(read).ToDictionary(path => path, path => "00");
            var calls = new List<string>();
            ConfirmProfileStep.ShowCheck = (owner, result) => calls.Add("check " + result.PlotsFound);
            ConfirmProfileStep.ShowConfirm = (owner, session) => { calls.Add("confirm"); session.Confirm(); };
            var reading = new CountingRead(RunLater.Done("Read done.", new string[0], outputs));
            RunLater ended = Press(ConfirmProfileStep.Wrap(reading.Body(), NoSettings(), IntPtr.Zero), Context(folder));
            Assert.Equal(1, reading.Starts);
            Assert.Equal(new[] { "check 2", "confirm" }, calls);
            Assert.Null(ended.Failure);
            Assert.Contains(ConfirmProfileText.Saved, ended.Message);
            Assert.True(File.Exists(ConfirmedFile));
        }

        [Fact]
        public void A_cancel_in_the_window_is_not_a_failure()
        {
            string read = ReadFolder();
            var outputs = Directory.GetFiles(read).ToDictionary(path => path, path => "00");
            ConfirmProfileStep.ShowCheck = (owner, result) => { };
            ConfirmProfileStep.ShowConfirm = (owner, session) => session.Cancel();
            RunLater ended = Press(ConfirmProfileStep.Wrap(new CountingRead(RunLater.Done("Read done.", new string[0], outputs)).Body(), NoSettings(), IntPtr.Zero), Context(folder));
            Assert.Null(ended.Failure);
            Assert.Contains(ConfirmProfileText.CancelledLine, ended.Message);
            Assert.False(File.Exists(ConfirmedFile));
        }

        [Fact]
        public void A_read_that_failed_opens_no_window_and_keeps_its_reason()
        {
            bool shown = false;
            ConfirmProfileStep.ShowCheck = (owner, result) => shown = true;
            ConfirmProfileStep.ShowConfirm = (owner, session) => shown = true;
            RunLater ended = Press(ConfirmProfileStep.Wrap(new CountingRead(RunLater.Failed("the worker ran out of time")).Body(), NoSettings(), IntPtr.Zero), Context(folder));
            Assert.False(shown);
            Assert.Equal("the worker ran out of time", ended.Failure);
        }

        [Fact]
        public void A_check_that_stopped_shows_its_note_opens_no_confirm_window_and_fails_in_plain_words()
        {
            string read = ReadFolder();
            File.WriteAllText(Path.Combine(read, "data_check.json"), Check.Replace("\"stopped\":\"\"", "\"stopped\":\"IOException: the file is locked\""));
            var outputs = Directory.GetFiles(read).ToDictionary(path => path, path => "00");
            var calls = new List<string>();
            ConfirmProfileStep.ShowCheck = (owner, result) => calls.Add("check " + result.Stopped);
            ConfirmProfileStep.ShowConfirm = (owner, session) => calls.Add("confirm");
            RunLater ended = Press(ConfirmProfileStep.Wrap(new CountingRead(RunLater.Done("Read done.", new string[0], outputs)).Body(), NoSettings(), IntPtr.Zero), Context(folder));
            Assert.Equal(new[] { "check IOException: the file is locked" }, calls);
            Assert.Contains("the data check stopped before it finished: IOException: the file is locked", ended.Failure);
        }

        [Fact]
        public void A_read_with_no_result_file_says_so_in_plain_words()
        {
            RunLater ended = Press(ConfirmProfileStep.Wrap(new CountingRead(RunLater.Done("Read done.", new string[0], new Dictionary<string, string>())).Body(), NoSettings(), IntPtr.Zero), Context(folder));
            Assert.Contains(ConfirmProfileText.NoCheckFile, ended.Failure);
        }

        [Fact]
        public void A_window_opened_after_a_confirmed_read_starts_on_what_was_confirmed()
        {
            JObject profile = JObject.Parse(Profile);
            profile["typologyColumn"]["confirmed"] = new JObject { ["workbook"] = "w.xlsx", ["sheet"] = "S", ["column"] = "Other" };
            profile["codeMap"]["confirmed"] = new JArray(new JObject { ["old"] = "Q7 (old note)", ["new"] = "Q9" });
            profile["hands"]["confirmed"] = new JObject { ["C3"] = "left", ["Z9"] = "right" };
            ConfirmProfileModel model = ConfirmProfileModel.Read(profile.ToString());
            Assert.Equal("Other", model.Typology.Column);
            Assert.EndsWith(" (confirmed)", model.Typology.Label);
            Assert.Equal("Q7 (old note) -> Q9", model.CodeMapText);
            Assert.Equal(ConfirmProfileText.CodeMapConfirmed, model.CodeMapLine);
            Assert.Equal("left", model.Hands.Single(hand => hand.Code == "C3").Chosen);
            Assert.Equal("right", model.Hands.Single(hand => hand.Code == "Z9").Chosen);
            Assert.Equal("", model.Hands.Single(hand => hand.Code == "A1").Chosen);
        }

        // The worker of the tests: it writes what a data check writes and answers done
        private sealed class CheckWorker : IWorkerStart
        {
            public readonly List<WorkerRequest> Requests = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                Directory.CreateDirectory(request.OutputFolder);
                var written = new Dictionary<string, string>
                {
                    ["data_check.json"] = Check, ["plots.json"] = "{\"plots\":[{\"number\":\"1\"}]}", ["project_profile.json"] = Profile, ["read_plotTable.json"] = "{\"plots\":[{\"officialNumber\":\"1\",\"nbhId\":\"A\"}]}",
                };
                var outputs = new List<WorkerFile>();
                foreach (KeyValuePair<string, string> file in written)
                {
                    string path = Path.Combine(request.OutputFolder, file.Key);
                    File.WriteAllText(path, file.Value);
                    outputs.Add(new WorkerFile { Path = path, Sha256 = FileHash.Sha256(path) });
                }
                ended(new WorkerAnswer { Status = "done", Outputs = outputs });
            }
        }

        // A worker that takes the request and gives no answer, so a press of the old read stops at its own check and opens no window
        private sealed class SilentWorker : IWorkerStart
        {
            public readonly List<WorkerRequest> Requests = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
            }
        }

        // A copy of the shipped config whose inputRoot is a folder of the test, under the mode given
        private PaneSettings Project(string mode)
        {
            string input = Path.Combine(folder, "input");
            Directory.CreateDirectory(input);
            File.WriteAllText(Path.Combine(input, "a.txt"), "a");
            string config = Path.Combine(folder, "config.json");
            JObject root = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
            root["inputRoot"] = input;
            File.WriteAllText(config, root.ToString());
            return new PaneSettings(new ProjectSettings { ConfigFile = config, ProjectMode = mode });
        }

        private string Work()
        {
            string work = Path.Combine(folder, "work");
            Directory.CreateDirectory(work);
            return work;
        }

        private static RunContext PressContext(PaneSettings settings, string work)
        {
            var inputs = new[] { new RunInput(PaneText.Settings, settings.ConfigFile, false) };
            return new RunContext(2, "2 Read data", "2_raw", work, null, true, inputs, new Dictionary<string, string>());
        }

        [Fact]
        public void The_raw_branch_through_For_runs_one_check_with_no_scan_then_the_two_windows()
        {
            // Dropping the raw branch of ReadDataBody.Choose fails this test: the press would go to the check of the old read and its scan
            var worker = new CheckWorker();
            var calls = new List<string>();
            ConfirmProfileStep.ShowCheck = (owner, result) => calls.Add("check");
            ConfirmProfileStep.ShowConfirm = (owner, session) => { calls.Add("confirm"); session.Cancel(); };
            var document = new DocumentSession("s1", Path.Combine(folder, "model.rvt"), "model") { Settings = Project("rev6") };
            string work = Work();
            ButtonBody body = ReadDataBody.For(document, IntPtr.Zero, worker);
            RunContext context = PressContext(document.Settings, work);
            Assert.NotNull(body.Preview(context));
            RunLater ended = Press(body, context);
            Assert.Null(ended.Failure);
            WorkerRequest only = Assert.Single(worker.Requests);
            Assert.Equal(ReadDataRun.DataCheckCommand, only.Command);
            Assert.DoesNotContain(DataCheckStep.RunIdSuffix, only.RunId);
            Assert.StartsWith(ReadDataRun.FolderPrefix, Path.GetFileName(only.OutputFolder));
            Assert.Equal(new[] { "check", "confirm" }, calls);
        }

        [Fact]
        public void Past_job_mode_keeps_the_inputs_of_the_pane_and_the_old_body_with_its_own_check()
        {
            var worker = new SilentWorker();
            bool confirmShown = false;
            ConfirmProfileStep.ShowCheck = (owner, result) => confirmShown = true;
            ConfirmProfileStep.ShowConfirm = (owner, session) => confirmShown = true;
            PaneSettings settings = Project("roshnPastJob");
            string work = Work();
            Assert.False(ReadInputs.NoPastFiles(settings.ConfigFile, "roshnPastJob", work));
            Assert.Equal(PaneRunState.InputsOf(settings, 2).Select(input => input.Name), PaneRunState.InputsInFolder(settings, 2, work).Select(input => input.Name));
            var document = new DocumentSession("s1", Path.Combine(folder, "model.rvt"), "model") { Settings = settings };
            ButtonBody body = ReadDataBody.For(document, IntPtr.Zero, worker);
            body.Preview(PressContext(settings, work));
            Press(body, PressContext(settings, work));
            WorkerRequest first = Assert.Single(worker.Requests);
            Assert.EndsWith(DataCheckStep.RunIdSuffix, first.RunId);
            Assert.StartsWith(DataCheckStep.FolderPrefix, Path.GetFileName(first.OutputFolder));
            Assert.False(confirmShown);
        }

        [Fact]
        public void Choose_takes_the_body_the_project_asks_for_at_the_press()
        {
            int raw = 0;
            int past = 0;
            var rawBody = new ButtonBody { Preview = context => new RunSummary("raw", new string[0]), Start = (context, done) => raw++ };
            var pastBody = new ButtonBody { Preview = context => new RunSummary("past", new string[0]), Start = (context, done) => past++ };
            PaneSettings settings = Project("rev6");
            ButtonBody body = ReadDataBody.Choose(settings, pastBody, rawBody);
            body.Start(PressContext(settings, Work()), later => { });
            Assert.Equal(1, raw);
            Assert.Equal(0, past);
            settings.Project.ProjectMode = "roshnPastJob";
            body.Start(PressContext(settings, Work()), later => { });
            Assert.Equal(1, raw);
            Assert.Equal(1, past);
        }

        [Fact]
        public void A_confirmed_file_the_worker_would_refuse_asks_and_a_yes_discards_it_with_no_worker()
        {
            PaneSettings settings = Project("rev6");
            string work = Work();
            string file = Path.Combine(work, ConfirmedProfile.FileName);
            File.WriteAllText(file, "{\"levels\":[{\"level\":99,\"word\":\"Past the levels\"}]}");
            string asked = null;
            ConfirmProfileStep.AskDiscard = (label, why) => { asked = why; return true; };
            var reading = new CountingRead(RunLater.Failed("no read should start"));
            RunLater ended = Press(ConfirmProfileStep.Wrap(reading.Body(), settings, IntPtr.Zero), PressContext(settings, work));
            Assert.Contains("names a level that is not a whole number", asked);
            Assert.Equal(0, reading.Starts);
            Assert.False(File.Exists(file));
            Assert.Equal(ConfirmProfileText.Discarded, ended.Failure);
        }

        [Fact]
        public void A_no_to_the_discard_keeps_the_file_and_says_why_the_read_did_not_run()
        {
            PaneSettings settings = Project("rev6");
            string work = Work();
            string file = Path.Combine(work, ConfirmedProfile.FileName);
            File.WriteAllText(file, "not json");
            ConfirmProfileStep.AskDiscard = (label, why) => false;
            var reading = new CountingRead(RunLater.Failed("no read should start"));
            RunLater ended = Press(ConfirmProfileStep.Wrap(reading.Body(), settings, IntPtr.Zero), PressContext(settings, work));
            Assert.Equal(0, reading.Starts);
            Assert.True(File.Exists(file));
            Assert.Contains("The run failed: ", ended.Failure);
        }

        [Fact]
        public void A_good_confirmed_file_asks_nothing_and_the_read_starts()
        {
            PaneSettings settings = Project("rev6");
            string work = Work();
            File.WriteAllText(Path.Combine(work, ConfirmedProfile.FileName), "{\"levels\":[{\"level\":1,\"word\":\"Area\"}]}");
            ConfirmProfileStep.AskDiscard = (label, why) => throw new InvalidOperationException("no prompt: " + why);
            var reading = new CountingRead(RunLater.Failed("stop here"));
            RunLater ended = Press(ConfirmProfileStep.Wrap(reading.Body(), settings, IntPtr.Zero), PressContext(settings, work));
            Assert.Equal(1, reading.Starts);
            Assert.Equal("stop here", ended.Failure);
        }

        [Fact]
        public void The_input_folder_and_the_confirmed_file_are_inputs_of_button_2_on_a_raw_project()
        {
            PaneSettings settings = Project("rev6");
            string work = Work();
            Assert.True(ReadInputs.NoPastFiles(settings.ConfigFile, "rev6", work), "the planted project is a raw read");
            List<RunInput> before = PaneRunState.InputsInFolder(settings, 2, work).ToList();
            Assert.Equal(new[] { PaneText.Settings, PaneText.InputFolder }, before.Select(input => input.Name));
            Assert.True(before[1].IsFolder);
            string hash = InputHash.Of(before[1]);
            File.WriteAllText(Path.Combine(before[1].Path, "b.txt"), "a changed folder is another read");
            Assert.NotEqual(hash, InputHash.Of(before[1]));
            File.WriteAllText(Path.Combine(work, ConfirmedProfile.FileName), "{}");
            Assert.Equal(new[] { PaneText.Settings, PaneText.InputFolder, PaneText.ConfirmedValues }, PaneRunState.InputsInFolder(settings, 2, work).Select(input => input.Name));
        }

        [Fact]
        public void Button_2_keeps_the_old_list_of_inputs_where_there_is_no_work_folder()
        {
            var settings = new PaneSettings();
            Assert.Equal(PaneRunState.InputsOf(settings, 2).Count, PaneRunState.InputsInFolder(settings, 2, null).Count);
            Assert.Equal(PaneRunState.InputsOf(settings, 4).Count, PaneRunState.InputsInFolder(settings, 4, folder).Count);
        }

        [Fact]
        public void The_hand_choices_are_the_values_the_read_takes()
        {
            Assert.Equal(new[] { "", ConfirmedProfile.Left, ConfirmedProfile.Right }, ConfirmProfileWindow.HandChoices);
        }
    }
}
#endif
