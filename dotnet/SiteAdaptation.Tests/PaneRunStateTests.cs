using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R372, R382. The pane side fills IRunState of the flow of a button: the work folder, the dry run tick, the missing step, the
    // inputs and the step line of a run that committed. On net48, with the view model of the pane and no Revit
    public sealed class PaneRunStateTests
    {
        private const string Folder = @"C:\Job\";

        [Fact]
        public void R372_the_run_state_gives_the_work_folder_of_the_document_by_the_root_and_refuses_one_inside_input_root()
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { Folder + "project.json", "{\"inputRoot\": \"D:\\\\Sources\"}" } };
            Func<string, string> read = path => files.TryGetValue(path, out string text) ? text : null;
            string root = @"D:\Work";
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" });
            var state = new PaneRunState(model, () => root, read);

            // The hash is the one PaneTests holds against hashlib of Python
            Assert.Equal(@"D:\Work\Model A_45e7c493", state.WorkFolder(@"C:\Work\Site\Model A.rvt"));
            root = @"D:\Sources\Work";
            Assert.Contains("lies inside inputRoot", Assert.Throws<InvalidOperationException>(() => state.WorkFolder(@"C:\Work\Site\Model A.rvt")).Message);
            files.Remove(Folder + "project.json");
            Assert.Equal(@"D:\Sources\Work\Model A_45e7c493", state.WorkFolder(@"C:\Work\Site\Model A.rvt"));
        }

        [Fact]
        public void R372_the_run_state_gives_the_dry_run_tick_of_the_pane()
        {
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" });
            var state = new PaneRunState(model, () => @"D:\Work", path => null);
            Assert.True(state.DryRunFirst);
            model.DryRun = false;
            Assert.False(state.DryRunFirst);
            model.Load(null);
            Assert.False(state.DryRunFirst);
        }

        [Fact]
        public void R372_the_run_state_gives_the_step_a_button_needs_and_0_when_none()
        {
            PaneViewModel model = Loaded(new PaneSettings());
            var state = new PaneRunState(model, () => @"D:\Work", path => null);
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 4 }, Enumerable.Range(1, 9).Select(state.MissingStep));
            model.PickConfig(Folder + "project.json");
            Assert.Equal(0, state.MissingStep(1));
            Assert.Equal(1, state.MissingStep(2));
        }

        [Fact]
        public void R372_the_run_state_gives_each_button_the_settings_file_in_5a()
        {
            PaneViewModel model = Loaded(new PaneSettings());
            var state = new PaneRunState(model, () => @"D:\Work", path => null);
            RunInput none = state.Inputs(1).Single();
            Assert.Equal("Settings", none.Name);
            Assert.Null(none.Path);
            Assert.False(none.IsFolder);
            model.PickConfig(Folder + "project.json");
            // Button 2 reads the input files of the pane beside it since 5c (R375)
            Assert.All(Enumerable.Range(1, 9).Where(button => button != 2 && button != 4), button => Assert.Equal(Folder + "project.json", state.Inputs(button).Single().Path));
            Assert.Equal(Folder + "project.json", state.Inputs(2).First().Path);
            // Button 4 reads the Rules file and the Prototypes folder of the pane beside it since 5c (R376)
            Assert.Equal(new[] { "Settings", "Rules", "Prototypes" }, state.Inputs(4).Select(input => input.Name));
            Assert.Equal(Folder + "project.json", state.Inputs(4).First().Path);
        }

        // R375. Button 2 gets the Settings file and the seven input files of the pane it reads, each by its row, Road surface as a
        // folder, the two workbooks of the row DLUS and sales by their own names, and each name has its role in the request
        [Fact]
        public void R375_the_run_state_gives_button_2_the_input_files_of_the_pane_each_with_its_role()
        {
            var settings = new PaneSettings { ConfigFile = Folder + "project.json" };
            settings.Pick(InputKeys.Baseplan, Folder + "baseplan.dwg");
            settings.Pick(InputKeys.RoadSurface, Folder + "roads");
            settings.Pick(InputKeys.Sales, Folder + "sales.xlsx");
            settings.Pick(InputKeys.Prototypes, Folder + "prototypes");
            var state = new PaneRunState(Loaded(settings), () => @"D:\Work", path => null);
            List<RunInput> inputs = state.Inputs(2).ToList();
            Assert.Equal(new[] { "Settings", "Baseplan", "Villa blocks", "Curbs", "Road surface", "DLUS schedule", "Sales schedule", "Plot numbers" }, inputs.Select(input => input.Name));
            Assert.Equal(Folder + "baseplan.dwg", inputs[1].Path);
            Assert.Null(inputs[2].Path);
            Assert.True(inputs[4].IsFolder);
            Assert.Equal(Folder + "roads", inputs[4].Path);
            Assert.Equal(Folder + "sales.xlsx", inputs[6].Path);
            Assert.All(inputs, input => Assert.True(SiteAdaptation.AddIn.ReadDataBody.Roles.ContainsKey(input.Name), input.Name));
            Assert.Equal(new[] { "config", "baseplan", "villaBlocks", "curbs", "roadSurface", "dlus", "sales", "plotNumbers" }, inputs.Select(input => SiteAdaptation.AddIn.ReadDataBody.Roles[input.Name]));
        }

        [Fact]
        public void R372_a_run_that_committed_marks_its_step_done_or_with_its_issues_and_a_failed_move_leaves_it()
        {
            PaneViewModel model = Loaded(new PaneSettings { ConfigFile = Folder + "project.json" });
            var state = new PaneRunState(model, () => @"D:\Work", path => null);

            state.Committed(1, Record(new string[0], new[] { @"D:\Work\a.json" }));
            Assert.Equal("Ready", model.Steps[0].ChipText);
            state.Committed(1, Record(new string[0], new string[0]));
            Assert.Equal(new[] { "Done", "Ready", "Needs step 2" }, model.Steps.Take(3).Select(step => step.ChipText));
            Assert.Equal("Run 2 Read data", model.BoxButton);
            Assert.Equal(0, state.MissingStep(2));
            state.Committed(2, Record(new[] { "one", "two" }, new string[0]));
            Assert.Equal("2 issues", model.Steps[1].ChipText);
            Assert.Equal("Done with 2 issues", model.Steps[1].ChipTip);
            Assert.Equal(MarkKind.Issues, model.Steps[1].Mark);
            Assert.Equal("Ready", model.Steps[2].ChipText);

            DocumentSession first = model.Document;
            model.Load(new DocumentSession("other", Folder + "Other.rvt", "Other"));
            Assert.Equal("Needs settings", model.Steps[0].ChipText);
            model.Load(first);
            Assert.Equal("Done", model.Steps[0].ChipText);
        }

        // A second way: the flow of PR 89 run through the run state of the pane, its stops and a real run that commits
        [Fact]
        public void R372_the_flow_of_a_button_runs_on_the_run_state_of_the_pane_from_its_stops_to_the_step_line()
        {
            string root = Path.Combine(Path.GetTempPath(), "pane-flow-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                string config = Path.Combine(root, "project.json");
                string document = Path.Combine(root, "Site.rvt");
                PaneViewModel model = Loaded(new PaneSettings());
                var state = new PaneRunState(model, () => Path.Combine(root, "work"), WorkFolder.ReadIfThere);
                var body = new ButtonBody { Preview = context => new RunSummary("1 plot", null), Apply = context => new string[0] };
                var flow = new ButtonFlow(state, new Model(), new Confirm(), InputHash.Of, () => "run1");

                Assert.Equal("Save the project first, its work folder is named by its file", flow.Run(1, "1 Set up site", body, string.Empty).Message);
                Assert.Equal("1 Set up site needs Settings, the file is not chosen", flow.Run(1, "1 Set up site", body, document).Message);
                Assert.Equal("2 Read data needs step 1", flow.Run(2, "2 Read data", body, document).Message);
                model.PickConfig(config);
                Assert.Equal("1 Set up site needs Settings, the file is not on disk", flow.Run(1, "1 Set up site", body, document).Message);
                File.WriteAllText(config, "{}");

                RunOutcome outcome = flow.Run(1, "1 Set up site", body, document);
                Assert.Equal(RunEnd.Committed, outcome.End);
                Assert.Equal("Done", model.Steps[0].ChipText);
                Assert.Equal("Ready", model.Steps[1].ChipText);
                Assert.Equal("Run 2 Read data", model.BoxButton);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static PaneViewModel Loaded(PaneSettings settings)
        {
            var model = new PaneViewModel(new NoDisk());
            model.Load(new DocumentSession("s", Folder + "Site.rvt", "Site"));
            model.LoadSettings(settings);
            return model;
        }

        private static RunRecord Record(IReadOnlyList<string> issues, IReadOnlyList<string> failedMoves)
        {
            return new RunRecord("run1", new Dictionary<string, string>(), issues, new Dictionary<string, string>(), failedMoves);
        }

        private sealed class NoDisk : IInputProbe
        {
            public InputFacts Look(string path, bool folder)
            {
                return new InputFacts();
            }

            public void Hash(string path, bool folder, Action<string> done)
            {
            }

            public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
            {
                fallback = null;
                return new string[0];
            }
        }

        private sealed class Model : IModel
        {
            public IModelTransaction Start(string name)
            {
                return new Transaction();
            }
        }

        private sealed class Transaction : IModelTransaction
        {
            public IReadOnlyList<string> Warnings { get; } = new string[0];
            public IReadOnlyList<string> Errors { get; } = new string[0];

            public bool Commit()
            {
                return true;
            }

            public void RollBack()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class Confirm : IConfirm
        {
            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                return true;
            }
        }
    }
}
