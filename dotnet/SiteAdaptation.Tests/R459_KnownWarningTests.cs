using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R459. The known warnings a step accepts: the decision of each warning from its words and elements, the optional side of a transaction, the
    // file a real run leaves and the count of 4 Check. FailuresAccessor has no public constructor, so the Revit side is what a run in Revit proves
    public sealed class R459_KnownWarningTests : IDisposable
    {
        private const string Overlap = "Highlighted floors overlap.";
        private const string Generic = "Generic 150mm";
        private const string Yard = "yard";
        private const string Ramp = "Ramps 2%";
        private const string RunId = "5_20261102-210000-000";

        private readonly string work = Path.Combine(Path.GetTempPath(), "r459_" + Guid.NewGuid().ToString("N"));

        public R459_KnownWarningTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private static JObject Config(string pairs = "[[\"generic\", \"yard\"], [\"generic\", \"ramp\"], [\"ramp\", \"yard\"]]")
        {
            return JObject.Parse("{\"floors\": {\"plotParameter\": \"Plot\", \"types\": {\"generic\": [\"" + Generic + "\"], \"yard\": [\"" + Yard + "\"], \"ramp\": [\"Ramps 0.5%\", \"" + Ramp + "\", \"Flat Ramps\"]},"
                + "\"knownWarnings\": {\"note\": \"made up\", \"entries\": [{\"text\": \"" + Overlap + "\", \"samePlot\": true, \"rolePairs\": " + pairs + "}]}}}");
        }

        private static KnownWarningList List()
        {
            return KnownWarningList.OfFloors(Config());
        }

        private static WarningElement Floor(string type, string plot, string id = null)
        {
            return new WarningElement { Id = id ?? type + "|" + plot, TypeName = type, Plot = plot };
        }

        private static string RepoConfig()
        {
            return Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json");
        }

        [Fact]
        public void R459_a_known_warning_of_one_plot_is_accepted_with_its_plot_and_two_floor_types_in_either_order()
        {
            AcceptedWarning accepted = List().Decide(Overlap, new[] { Floor(Generic, "VD4-101", "a"), Floor(Yard, "VD4-101", "b") });
            Assert.Equal(Overlap, accepted.Text);
            Assert.Equal("VD4-101", accepted.Plot);
            Assert.Equal(new[] { Generic, Yard, "a", "b" }, new[] { accepted.FirstType, accepted.SecondType, accepted.FirstId, accepted.SecondId });

            accepted = List().Decide(Overlap, new[] { Floor(Ramp, "VD4-101", "c"), Floor(Generic, "VD4-101", "a") });
            Assert.Equal(new[] { Generic, Ramp, "a", "c" }, new[] { accepted.FirstType, accepted.SecondType, accepted.FirstId, accepted.SecondId });
        }

        [Fact]
        public void R459_a_plot_value_with_spaces_around_it_is_the_same_plot()
        {
            Assert.Equal("VD4-101", List().Decide(Overlap, new[] { Floor(Generic, " VD4-101"), Floor(Yard, "VD4-101 ") }).Plot);
        }

        [Fact]
        public void R459_any_other_warning_shows()
        {
            KnownWarningList list = List();
            WarningElement[] pair = { Floor(Generic, "VD4-101"), Floor(Yard, "VD4-101") };
            Assert.Null(list.Decide("Line in Sketch is slightly off axis and may cause inaccuracies.", pair));
            Assert.False(list.Knows("Line in Sketch is slightly off axis and may cause inaccuracies."));
            Assert.True(list.Knows(Overlap));
            // Another case is another text, so a Revit in another language shows every warning
            Assert.Null(list.Decide(Overlap.ToUpperInvariant(), pair));
        }

        [Fact]
        public void R459_an_overlap_of_two_plots_shows()
        {
            Assert.Null(List().Decide(Overlap, new[] { Floor(Generic, "VD4-101"), Floor(Yard, "VD4-102") }));
            Assert.Null(List().Decide(Overlap, new[] { Floor(Ramp, "VD4-102"), Floor(Generic, "VD4-101") }));
        }

        [Theory]
        [InlineData(Yard, Yard)]
        [InlineData(Ramp, Ramp)]
        [InlineData(Generic, Generic)]
        public void R459_a_pair_the_list_does_not_name_shows_even_on_one_plot(string first, string second)
        {
            Assert.Null(List().Decide(Overlap, new[] { Floor(first, "VD4-101", "a"), Floor(second, "VD4-101", "b") }));
        }

        // Q367, Bader 2 Nov: a ramp with a yard joins the list, on one plot only
        [Theory]
        [InlineData(Yard, Ramp)]
        [InlineData(Ramp, Yard)]
        public void R459_a_ramp_with_a_yard_is_accepted_on_one_plot_and_shows_on_two_plots(string first, string second)
        {
            AcceptedWarning accepted = List().Decide(Overlap, new[] { Floor(first, "VD4-101", "a"), Floor(second, "VD4-101", "b") });
            Assert.Equal(new[] { Ramp, Yard }, new[] { accepted.FirstType, accepted.SecondType });
            Assert.Null(List().Decide(Overlap, new[] { Floor(first, "VD4-101", "a"), Floor(second, "VD4-102", "b") }));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void R459_an_element_with_no_plot_value_shows(string plot)
        {
            Assert.Null(List().Decide(Overlap, new[] { Floor(Generic, plot, "a"), Floor(Yard, "VD4-101", "b") }));
            Assert.Null(List().Decide(Overlap, new[] { Floor(Generic, "VD4-101", "a"), Floor(Yard, plot, "b") }));
            Assert.Null(List().Decide(Overlap, new[] { Floor(Generic, plot, "a"), Floor(Yard, plot, "b") }));
        }

        [Fact]
        public void R459_a_warning_without_exactly_two_elements_or_of_a_type_of_no_role_shows()
        {
            Assert.Null(List().Decide(Overlap, new WarningElement[0]));
            Assert.Null(List().Decide(Overlap, new[] { Floor(Generic, "VD4-101") }));
            Assert.Null(List().Decide(Overlap, new[] { Floor(Generic, "VD4-101", "a"), Floor(Yard, "VD4-101", "b"), Floor(Ramp, "VD4-101", "c") }));
            Assert.Null(List().Decide(Overlap, new[] { Floor("Other floor", "VD4-101", "a"), Floor(Yard, "VD4-101", "b") }));
            Assert.Null(List().Decide(Overlap, new[] { Floor(null, "VD4-101", "a"), Floor(Yard, "VD4-101", "b") }));
        }

        [Fact]
        public void R459_a_pair_changed_in_the_config_changes_what_is_accepted_with_no_other_change()
        {
            KnownWarningList narrow = KnownWarningList.OfFloors(Config("[[\"generic\", \"yard\"]]"));
            Assert.NotNull(narrow.Decide(Overlap, new[] { Floor(Generic, "VD4-101", "a"), Floor(Yard, "VD4-101", "b") }));
            Assert.Null(narrow.Decide(Overlap, new[] { Floor(Generic, "VD4-101", "a"), Floor(Ramp, "VD4-101", "b") }));
            Assert.Null(narrow.Decide(Overlap, new[] { Floor(Yard, "VD4-101", "a"), Floor(Ramp, "VD4-101", "b") }));
        }

        [Fact]
        public void R459_an_entry_that_does_not_ask_one_plot_takes_two_plots_and_still_no_empty_one()
        {
            var list = new KnownWarningList(new[] { new KnownWarning { Text = "Walls meet.", SamePlot = false, RolePairs = new[] { new[] { "a", "b" } } } }, "Plot",
                new Dictionary<string, IReadOnlyList<string>> { ["a"] = new[] { "A" }, ["b"] = new[] { "B" } });
            Assert.Equal("1, 2", list.Decide("Walls meet.", new[] { Floor("A", "1"), Floor("B", "2") }).Plot);
            Assert.Null(list.Decide("Walls meet.", new[] { Floor("A", "1"), Floor("B", null) }));
        }

        [Fact]
        public void R459_config_with_no_block_gives_no_list()
        {
            Assert.Null(KnownWarningList.OfFloors(new JObject()));
            Assert.Null(KnownWarningList.OfFloors(JObject.Parse("{\"floors\": {\"plotParameter\": \"Plot\", \"types\": {}}}")));
        }

        [Fact]
        public void R459_the_real_config_parses_to_the_words_and_the_three_pairs_of_q367_with_every_role_a_floor_role()
        {
            JObject config = JObject.Parse(File.ReadAllText(RepoConfig()));
            KnownWarningList list = KnownWarningList.OfFloors(config);
            KnownWarning entry = Assert.Single(list.Entries);
            Assert.Equal(Overlap, entry.Text);
            Assert.True(entry.SamePlot);
            Assert.Equal(new[] { "generic|yard", "generic|ramp", "ramp|yard" }, entry.RolePairs.Select(pair => pair[0] + "|" + pair[1]).ToArray());
            Assert.Equal("Plot", list.PlotParameter);
            foreach (string ramp in ((JArray)config["floors"]["types"]["ramp"]).Select(item => (string)item))
            {
                Assert.NotNull(list.Decide(Overlap, new[] { Floor(Generic, "VD4-101", "a"), Floor(ramp, "VD4-101", "b") }));
                Assert.NotNull(list.Decide(Overlap, new[] { Floor(Yard, "VD4-101", "a"), Floor(ramp, "VD4-101", "b") }));
                Assert.Null(list.Decide(Overlap, new[] { Floor(Yard, "VD4-101", "a"), Floor(ramp, "VD4-102", "b") }));
                Assert.Null(list.Decide(Overlap, new[] { Floor(ramp, "VD4-101", "a"), Floor(ramp, "VD4-101", "b") }));
            }
            Assert.Null(list.Decide(Overlap, new[] { Floor(Yard, "VD4-101", "a"), Floor(Yard, "VD4-101", "b") }));
            Assert.Null(list.Decide(Overlap, new[] { Floor(Generic, "VD4-101", "a"), Floor(Generic, "VD4-101", "b") }));
        }

        [Theory]
        [InlineData("{\"entries\": 1}", "entries")]
        [InlineData("{\"entries\": [{\"samePlot\": true, \"rolePairs\": [[\"generic\", \"yard\"]]}]}", "text")]
        [InlineData("{\"entries\": [{\"text\": \"x\", \"rolePairs\": [[\"generic\", \"yard\"]]}]}", "samePlot")]
        [InlineData("{\"entries\": [{\"text\": \"x\", \"samePlot\": true}]}", "rolePairs")]
        [InlineData("{\"entries\": [{\"text\": \"x\", \"samePlot\": true, \"rolePairs\": [[\"generic\"]]}]}", "rolePairs")]
        [InlineData("{\"entries\": [{\"text\": \"x\", \"samePlot\": true, \"rolePairs\": [[\"generic\", \"nope\"]]}]}", "nope")]
        public void R459_a_block_of_the_wrong_form_is_refused_by_name_before_the_model_changes(string block, string named)
        {
            JObject config = Config();
            config["floors"]["knownWarnings"] = JObject.Parse(block);
            Assert.Contains(named, Assert.Throws<InvalidDataException>(() => KnownWarningList.OfFloors(config)).Message);
        }

        // A transaction of the flow with the optional side, standing in for RevitTransaction, whose list the test reads as the collector would
        private sealed class KnownModel : IModel, IModelTransaction, IKnownWarningTransaction
        {
            public KnownWarningList Given;
            public List<AcceptedWarning> ToAccept = new List<AcceptedWarning>();
            public List<string> Shown = new List<string>();
            public bool CommitEnds = true;
            private bool committed;

            public IModelTransaction Start(string name) => this;
            public bool Commit() { committed = CommitEnds; return CommitEnds; }
            public void RollBack() { }
            public void Dispose() { }
            public IReadOnlyList<string> Warnings => Shown;
            public IReadOnlyList<string> Errors => new string[0];
            public void AcceptKnownWarnings(KnownWarningList list) { Given = list; }
            public IReadOnlyList<AcceptedWarning> AcceptedWarnings => committed ? ToAccept : new List<AcceptedWarning>();
        }

        // The transaction of the flow with no optional side, as every test fake of the other buttons
        private sealed class PlainModel : IModel, IModelTransaction
        {
            public List<string> Shown = new List<string>();
            public IModelTransaction Start(string name) => this;
            public bool Commit() => true;
            public void RollBack() { }
            public void Dispose() { }
            public IReadOnlyList<string> Warnings => Shown;
            public IReadOnlyList<string> Errors => new string[0];
        }

        private sealed class State : IRunState
        {
            public string Work;
            public string WorkFolder(string documentPath) => Work;
            public bool DryRunFirst => false;
            public int MissingStep(int button) => 0;
            public IReadOnlyList<RunInput> Inputs(int button) => new RunInput[0];
            public void Committed(int button, RunRecord record) { }
        }

        private sealed class Yes : IConfirm
        {
            public bool Apply(string label, RunSummary summary, bool dryRun) => true;
        }

        private RunOutcome Run(IModel model, ButtonBody body)
        {
            return new ButtonFlow(new State { Work = work }, model, new Yes(), input => "", () => RunId).Run(5, "5 Floors and ramps", body, @"C:\Models\Site.rvt");
        }

        private FloorsRun Floors(FakeFloors document, KnownWarningList list)
        {
            return new FloorsRun(document, context => new FloorInputs
            {
                Plan = FloorPlans.Plan(FloorPlans.Plot("101")), PlanPath = Path.Combine(work, "plan_012.json"), Profile = "parsons2024Rev6",
                LastPlaced = new HashSet<string>(StringComparer.Ordinal), KnownWarnings = list,
            });
        }

        private static AcceptedWarning Accepted(string first, string second, string plot = "VD4-101")
        {
            return new AcceptedWarning { Text = Overlap, Plot = plot, FirstType = Generic, SecondType = Yard, FirstId = first, SecondId = second };
        }

        private string AcceptedPath()
        {
            return Path.Combine(work, FloorsRun.AcceptedName(FloorPlans.Plan(FloorPlans.Plot("101")).Neighbourhood ?? "all"));
        }

        [Fact]
        public void R459_step_5_gives_its_list_and_a_real_run_counts_what_was_accepted_apart_from_its_issues()
        {
            var document = new FakeFloors();
            var model = new KnownModel { Shown = { "Some other warning." }, ToAccept = { Accepted("f1", "f2"), Accepted("f1", "f3") } };
            KnownWarningList list = List();

            RunOutcome outcome = Run(model, Floors(document, list).Body());

            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            Assert.Same(list, model.Given);
            // No message of its own, a message is a TaskDialog and no box stops step 5
            Assert.Null(outcome.Message);
            Assert.Contains("Some other warning.", outcome.Record.Issues);
            Assert.DoesNotContain(outcome.Record.Issues, issue => issue.Contains(Overlap));
            Assert.Equal(FileHash.Sha256(AcceptedPath()), outcome.Record.Outputs[AcceptedPath()]);
            Assert.Equal(2, AcceptedWarningFile.Load(AcceptedPath()).Count);
        }

        [Fact]
        public void R459_a_real_run_that_accepted_nothing_still_leaves_its_file_with_a_count_of_0()
        {
            RunOutcome outcome = Run(new KnownModel(), Floors(new FakeFloors(), List()).Body());
            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            Assert.Empty(AcceptedWarningFile.Load(AcceptedPath()));
        }

        [Fact]
        public void R459_a_run_that_rolls_back_leaves_no_file()
        {
            var model = new KnownModel { CommitEnds = false, ToAccept = { Accepted("f1", "f2") } };
            RunOutcome outcome = Run(model, Floors(new FakeFloors(), List()).Body());
            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.False(File.Exists(AcceptedPath()));
        }

        [Fact]
        public void R459_with_no_list_or_a_transaction_with_no_optional_side_the_run_is_as_before_and_leaves_no_file()
        {
            var plain = new PlainModel { Shown = { Overlap } };
            RunOutcome outcome = Run(plain, Floors(new FakeFloors(), List()).Body());
            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            Assert.Contains(Overlap, outcome.Record.Issues);
            Assert.False(File.Exists(AcceptedPath()));

            var known = new KnownModel { Shown = { Overlap } };
            outcome = Run(known, Floors(new FakeFloors(), null).Body());
            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            Assert.Null(known.Given);
            Assert.Contains(Overlap, outcome.Record.Issues);
            Assert.False(File.Exists(AcceptedPath()));
        }

        [Fact]
        public void R459_a_failure_after_the_commit_is_an_issue_and_the_run_stays_committed()
        {
            var body = new ButtonBody
            {
                Preview = context => new RunSummary("", null),
                Apply = context => new string[0],
                AfterCommit = context => throw new IOException("the disk is full"),
            };
            RunOutcome outcome = Run(new PlainModel(), body);
            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Contains(outcome.Record.Issues, issue => issue.Contains("the disk is full"));
        }

        [Fact]
        public void R459_a_second_run_keeps_what_stands_takes_a_pair_once_and_drops_a_floor_that_is_gone()
        {
            List<AcceptedWarning> merged = AcceptedWarningFile.Merge(new[] { Accepted("f1", "f2"), Accepted("f1", "gone"), Accepted("f4", "f5") },
                new[] { Accepted("f2", "f1"), Accepted("f6", "f7"), Accepted("f6", "f7") }, new HashSet<string> { "f1", "f2", "f4", "f5" });
            Assert.Equal(new[] { "f2|f1", "f4|f5", "f6|f7" }, merged.Select(item => item.FirstId + "|" + item.SecondId).OrderBy(item => item, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void R459_the_second_run_of_step_5_keeps_the_list_of_the_first_for_floors_that_stand()
        {
            var document = new FakeFloors();
            Run(new KnownModel(), Floors(document, List()).Body());
            string[] ids = document.Items.Select(item => item.UniqueId).ToArray();
            File.WriteAllText(AcceptedPath(), AcceptedWarningFile.Text("5_0", "012", new[] { Accepted(ids[0], ids[1]), Accepted(ids[0], "gone") }));

            // All three floors stand, so the second run accepts nothing new and does not empty the list
            RunOutcome outcome = Run(new KnownModel(), Floors(document, List()).Body());

            Assert.True(outcome.End == RunEnd.Committed, outcome.End + " " + outcome.Message);
            AcceptedWarning kept = Assert.Single(AcceptedWarningFile.Load(AcceptedPath()));
            Assert.Equal(ids[1], kept.SecondId);
        }

        [Fact]
        public void R459_the_file_reads_back_and_a_file_that_does_not_read_gives_none()
        {
            string path = AcceptedPath();
            Assert.Null(AcceptedWarningFile.Load(path));
            File.WriteAllText(path, AcceptedWarningFile.Text(RunId, "012", new[] { Accepted("f1", "f2") }));
            AcceptedWarning back = Assert.Single(AcceptedWarningFile.Load(path));
            Assert.Equal(new[] { Overlap, "VD4-101", Generic, Yard, "f1", "f2" }, new[] { back.Text, back.Plot, back.FirstType, back.SecondType, back.FirstId, back.SecondId });
            Assert.Null(AcceptedWarningFile.Refusal(JObject.Parse(File.ReadAllText(path))));

            File.WriteAllText(path, "not json");
            Assert.Null(AcceptedWarningFile.Load(path));
            File.WriteAllText(path, "{\"format\": \"other\"}");
            Assert.Null(AcceptedWarningFile.Load(path));
            Assert.NotNull(AcceptedWarningFile.Refusal(JObject.Parse("{\"format\": \"other\"}")));
        }

        private static CheckTableView View()
        {
            return new CheckTableView
            {
                Counts = new[] { new KeyValuePair<string, int>("All", 4), new KeyValuePair<string, int>("Hand", 4), new KeyValuePair<string, int>("Assumed", 2) },
                Rows = new[] { new CheckRow { Index = 0, Plot = "101", Check = "Hand", OpenUse = CheckUse.Rule } },
            };
        }

        [Fact]
        public void R459_4_check_shows_the_count_after_the_other_chips_only_when_step_5_left_a_file_and_lists_no_row_on_it()
        {
            CheckTableView view = View();
            CheckTableBuild.AddAccepted(view, 957);
            Assert.Equal(new[] { "All 4", "Hand 4", "Assumed 2", CheckTableState.AcceptedChip + " 957" }, view.Counts.Select(item => item.Key + " " + item.Value).ToArray());
            Assert.Equal("Accepted at step 5", CheckTableState.AcceptedChip);
            var state = new CheckTableState(view);
            Assert.Equal(957, state.CountOf(CheckTableState.AcceptedChip));
            Assert.Equal(4, state.CountOf(CheckTableState.AllChip));
            Assert.Empty(state.Filter(CheckTableState.AcceptedChip, ""));
            Assert.Single(state.Filter(CheckTableState.AllChip, ""));

            CheckTableView none = View();
            CheckTableBuild.AddAccepted(none, null);
            Assert.Equal(3, none.Counts.Count);

            // A file with 0 is a step 5 that ran and accepted none, which is said, and no file is no chip
            CheckTableView zero = View();
            CheckTableBuild.AddAccepted(zero, 0);
            Assert.Equal(4, zero.Counts.Count);
        }
    }
}
