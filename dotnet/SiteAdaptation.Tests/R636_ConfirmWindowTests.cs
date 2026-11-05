#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.ConfirmProfile;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R636 to R638. The window Confirm what was found and the step that shows it after a read of a folder with no past file. net48 only
    public sealed class R636_ConfirmWindowTests : IDisposable
    {
        private const string Check = "{\"needs\":[{\"need\":\"plotOutlines\",\"mark\":\"strong\",\"line\":\"Found 12 plot outlines.\"}],\"files\":[{\"name\":\"a.dwg\",\"status\":\"read\",\"reason\":\"\"}],\"stopped\":\"\"}";

        private const string Profile = "{\"levels\":[{\"level\":1,\"word\":null,\"source\":\"missing\"},{\"level\":2,\"word\":\"Block\",\"source\":\"computed\"}],"
            + "\"codeMap\":{\"proposed\":[{\"old\":\"A1\",\"new\":\"B1\",\"source\":\"computed\"}],\"foundIn\":\"codes.xlsx, Sheet 1\"},"
            + "\"typologyColumn\":{\"proposed\":{\"workbook\":\"w.xlsx\",\"sheet\":\"S\",\"column\":\"Type\",\"plots\":9},\"candidates\":[{\"workbook\":\"w.xlsx\",\"sheet\":\"S\",\"column\":\"Type\",\"plots\":9},{\"workbook\":\"w.xlsx\",\"sheet\":\"S\",\"column\":\"Other\",\"plots\":7}]},"
            + "\"areaColumn\":{\"proposed\":null,\"candidates\":[]},"
            + "\"hands\":{\"proposed\":{\"A1\":\"right\",\"C3\":null}}}";

        private const string Empty = "{\"levels\":[],\"codeMap\":{\"proposed\":[]},\"typologyColumn\":{\"proposed\":null,\"candidates\":[]},\"areaColumn\":{\"proposed\":null,\"candidates\":[]},\"hands\":{\"proposed\":{}}}";

        private readonly Action<IntPtr, DataCheckResult> checkBefore = ConfirmProfileStep.ShowCheck;
        private readonly Action<IntPtr, ConfirmProfileSession> confirmBefore = ConfirmProfileStep.ShowConfirm;
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r636_" + Guid.NewGuid().ToString("N"));

        public R636_ConfirmWindowTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            ConfirmProfileStep.ShowCheck = checkBefore;
            ConfirmProfileStep.ShowConfirm = confirmBefore;
            Directory.Delete(folder, true);
        }

        [Fact]
        public void The_window_offers_what_the_check_proposed_and_picks_nothing_else()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            Assert.Equal("Type", model.Typology.Column);
            Assert.Equal(2, model.TypologyColumns.Count);
            Assert.Null(model.Area);
            Assert.Equal("A1 -> B1", model.CodeMapText);
            Assert.Equal("codes.xlsx, Sheet 1", model.CodeMapFoundIn);
            Assert.Equal(new[] { "A1", "C3" }, model.Hands.Select(hand => hand.Code));
            Assert.Equal("right", model.Hands[0].Chosen);
            Assert.Equal("", model.Hands[1].Chosen);
            Assert.Equal("no guess", model.Hands[1].GuessText);
            Assert.Equal("", model.Levels[0].Word);
            Assert.Equal("Block", model.Levels[1].Word);
        }

        [Fact]
        public void A_check_that_proposed_nothing_gives_a_window_with_nothing_picked_and_nothing_to_save()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Empty);
            Assert.Null(model.Typology);
            Assert.Null(model.Area);
            Assert.Empty(model.Hands);
            Assert.True(model.GivesNothing);
            var session = new ConfirmProfileSession(model, folder, "run", 0);
            session.Confirm();
            Assert.False(session.Saved);
            Assert.Equal(ConfirmProfileText.NothingGiven, session.Result);
            Assert.False(File.Exists(Path.Combine(folder, ConfirmedProfile.FileName)));
        }

        [Fact]
        public void Confirm_writes_a_file_the_read_accepts_and_only_with_what_was_given()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            model.Levels[0].Word = "Area";
            model.CodeMapText = "A1 -> B1\r\nC1, D1\r\nQ7 (old note) -> Q9\r\n";
            model.Hands[1].Chosen = "left";
            var session = new ConfirmProfileSession(model, folder, "run", 2);
            session.Confirm();
            Assert.True(session.Saved);
            JObject file = JObject.Parse(File.ReadAllText(Path.Combine(folder, ConfirmedProfile.FileName)));
            Assert.Equal(3, ((JArray)file["codeMap"]).Count);
            Assert.Equal("Q7 (old note)", (string)file["codeMap"][2]["old"]);
            Assert.Equal("left", (string)file["hands"]["C3"]);
            Assert.Equal("Area", (string)file["levels"][0]["word"]);
            Assert.Equal("Type", (string)file["typologyColumn"]["column"]);
            Assert.Null(file["areaColumn"]);
            ConfirmedProfile read = ConfirmedProfile.From(file, 2);
            Assert.Equal(3, read.CodeMap.Count);
            Assert.Equal("Type", read.Column.Column);
        }

        [Fact]
        public void A_code_map_line_that_is_not_a_pair_is_refused_and_nothing_is_written()
        {
            ConfirmProfileModel model = ConfirmProfileModel.Read(Profile);
            model.CodeMapText = "A1 B1\r\nonlyone";
            var session = new ConfirmProfileSession(model, folder, "run", 2);
            bool finished = false;
            session.Finished += () => finished = true;
            session.Confirm();
            Assert.False(finished);
            Assert.False(session.Saved);
            Assert.Contains("Line 2", session.Refusal);
            Assert.False(File.Exists(Path.Combine(folder, ConfirmedProfile.FileName)));
        }

        [Fact]
        public void Cancel_saves_nothing_and_says_so()
        {
            var session = new ConfirmProfileSession(ConfirmProfileModel.Read(Profile), folder, "run", 2);
            session.Cancel();
            Assert.False(session.Saved);
            Assert.Equal(ConfirmProfileText.CancelledLine, session.Result);
            Assert.False(File.Exists(Path.Combine(folder, ConfirmedProfile.FileName)));
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

        private ButtonBody Read(RunLater end, out int starts)
        {
            int count = 0;
            ButtonBody body = new ButtonBody { Preview = context => new RunSummary("p", new string[0]), Start = (context, done) => { count++; done(end); } };
            starts = count;
            return body;
        }

        private static RunContext Context(string work)
        {
            return new RunContext(2, "2 Read data", "2_run", work, null, true, new RunInput[0], new Dictionary<string, string>());
        }

        [Fact]
        public void After_the_read_the_check_of_its_folder_is_shown_once_then_the_confirm_window()
        {
            string read = ReadFolder();
            var outputs = Directory.GetFiles(read).ToDictionary(path => path, path => "00");
            var calls = new List<string>();
            ConfirmProfileStep.ShowCheck = (owner, result) => calls.Add("check " + result.PlotsFound);
            ConfirmProfileStep.ShowConfirm = (owner, session) => { calls.Add("confirm"); session.Model.Area = null; session.Confirm(); };
            RunLater ended = null;
            ButtonBody body = ConfirmProfileStep.Wrap(Read(RunLater.Done("Read done.", new string[0], outputs), out _), IntPtr.Zero);
            body.Start(Context(folder), later => ended = later);
            Assert.Equal(new[] { "check 2", "confirm" }, calls);
            Assert.Null(ended.Failure);
            Assert.Contains("Saved confirmed_profile.json", ended.Message);
            Assert.True(File.Exists(Path.Combine(folder, ConfirmedProfile.FileName)));
        }

        [Fact]
        public void A_cancel_in_the_window_is_not_a_failure()
        {
            string read = ReadFolder();
            var outputs = Directory.GetFiles(read).ToDictionary(path => path, path => "00");
            ConfirmProfileStep.ShowCheck = (owner, result) => { };
            ConfirmProfileStep.ShowConfirm = (owner, session) => session.Cancel();
            RunLater ended = null;
            ConfirmProfileStep.Wrap(Read(RunLater.Done("Read done.", new string[0], outputs), out _), IntPtr.Zero).Start(Context(folder), later => ended = later);
            Assert.Null(ended.Failure);
            Assert.Contains(ConfirmProfileText.CancelledLine, ended.Message);
            Assert.False(File.Exists(Path.Combine(folder, ConfirmedProfile.FileName)));
        }

        [Fact]
        public void A_read_that_failed_opens_no_window_and_keeps_its_reason()
        {
            bool shown = false;
            ConfirmProfileStep.ShowCheck = (owner, result) => shown = true;
            ConfirmProfileStep.ShowConfirm = (owner, session) => shown = true;
            RunLater ended = null;
            ConfirmProfileStep.Wrap(Read(RunLater.Failed("the worker ran out of time"), out _), IntPtr.Zero).Start(Context(folder), later => ended = later);
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
            RunLater ended = null;
            ConfirmProfileStep.Wrap(Read(RunLater.Done("Read done.", new string[0], outputs), out _), IntPtr.Zero).Start(Context(folder), later => ended = later);
            Assert.Equal(new[] { "check IOException: the file is locked" }, calls);
            Assert.Contains("the data check stopped before it finished: IOException: the file is locked", ended.Failure);
        }

        [Fact]
        public void A_read_with_no_profile_file_says_so_in_plain_words()
        {
            RunLater ended = null;
            ConfirmProfileStep.Wrap(Read(RunLater.Done("Read done.", new string[0], new Dictionary<string, string>()), out _), IntPtr.Zero).Start(Context(folder), later => ended = later);
            Assert.Contains("the read wrote no data_check.json", ended.Failure);
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
            Assert.EndsWith("(confirmed)", model.Typology.Label);
            Assert.Equal("Q7 (old note) -> Q9", model.CodeMapText);
            Assert.True(model.CodeMapConfirmed);
            Assert.Equal("left", model.Hands.Single(hand => hand.Code == "C3").Chosen);
            Assert.Equal("right", model.Hands.Single(hand => hand.Code == "Z9").Chosen);
            Assert.Equal("right", model.Hands.Single(hand => hand.Code == "A1").Chosen);
        }

        [Fact]
        public void The_confirmed_file_is_an_input_of_button_2_where_the_work_folder_holds_it_and_none_before()
        {
            string config = Path.Combine(folder, "project.json");
            File.WriteAllText(config, "{\"inputRoot\":\"" + folder.Replace("\\", "\\\\") + "\",\"designBasis\":{\"mode\":\"rev6\",\"modes\":[\"rev6\"]},\"addin\":{}}");
            var settings = new PaneSettings { ConfigFile = config };
            settings.Project.ProjectMode = "rev6";
            string work = Path.Combine(folder, "work");
            Directory.CreateDirectory(work);
            Assert.True(SiteAdaptation.Common.ReadData.ReadInputs.NoPastFiles(config, "rev6", work), "the planted project is a raw read");
            Assert.Equal(new[] { PaneText.Settings }, PaneRunState.InputsInFolder(settings, 2, work).Select(input => input.Name));
            File.WriteAllText(Path.Combine(work, ConfirmedProfile.FileName), "{}");
            Assert.Equal(new[] { PaneText.Settings, PaneRunState.ConfirmedInput }, PaneRunState.InputsInFolder(settings, 2, work).Select(input => input.Name));
        }

        [Fact]
        public void Button_2_keeps_the_old_list_of_inputs_where_there_is_no_work_folder()
        {
            var settings = new PaneSettings();
            Assert.Equal(PaneRunState.InputsOf(settings, 2).Count, PaneRunState.InputsInFolder(settings, 2, null).Count);
            Assert.Equal(PaneRunState.InputsOf(settings, 4).Count, PaneRunState.InputsInFolder(settings, 4, folder).Count);
        }
    }
}
#endif
