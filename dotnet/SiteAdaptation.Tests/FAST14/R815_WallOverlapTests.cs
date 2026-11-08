using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common.BoundaryWalls;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests.FAST14
{
    // R815, F32 of run 2 of 8 Nov. Step 8 gives its transaction the words of the walls overlap of Revit, so the collector takes the warning
    // without a box and the issues of the step list the walls. The collector itself needs Revit, so the fake transaction decides as it does,
    // by the same calls of Common, and a read of WarningCollector.cs proves it calls them. Made up walls only, no Revit type
    public sealed class R815_WallOverlapTests : IDisposable
    {
        private const string RunId = "8_20261108-0900";
        private readonly string work = Path.Combine(Path.GetTempPath(), "r815_" + Guid.NewGuid().ToString("N"));

        public R815_WallOverlapTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private static readonly WarningElement[] TwoWalls =
        {
            new WarningElement { Id = "w-1", TypeName = "BW" },
            new WarningElement { Id = "w-2", TypeName = "BW" },
        };

        // The transaction of the flow with the known side. At the commit each warning Revit would raise goes as WarningCollector.Collect sends
        // it: accepted by the list, taken and listed by R782 and R815, or left for Revit to show
        private sealed class WallModel : IModel, IModelTransaction, IKnownWarningTransaction
        {
            public KnownWarningList Given;
            public List<string> Raised = new List<string>();
            public List<string> Shown = new List<string>();
            public List<string> Taken = new List<string>();

            public IModelTransaction Start(string name) => this;

            public bool Commit()
            {
                foreach (string text in Raised)
                {
                    IReadOnlyList<WarningElement> elements = Given != null && Given.Knows(text) ? TwoWalls : null;
                    AcceptedWarning accepted = elements == null ? null : Given.Decide(text, elements);
                    if (accepted == null && KnownWordsWarning.TakeAndList(Given, text, accepted))
                    {
                        Taken.Add(WallOverlapWarning.Owns(text) ? WallOverlapWarning.Line(elements) : KnownWordsWarning.Line(text, elements));
                    }
                    else if (accepted == null)
                    {
                        Shown.Add(text);
                    }
                }
                return true;
            }

            public void RollBack() { }
            public void Dispose() { }
            public IReadOnlyList<string> Warnings => Taken.Concat(Shown).ToList();
            public IReadOnlyList<string> Errors => new string[0];
            public void AcceptKnownWarnings(KnownWarningList list) { Given = list; }
            public IReadOnlyList<AcceptedWarning> AcceptedWarnings => new AcceptedWarning[0];
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

        private WallsRun Walls()
        {
            return new WallsRun(new FakeWalls(), context => new WallInputs
            {
                Plan = FloorPlans.Plan(FloorPlans.Plot("101"), FloorPlans.Plot("102")), Walls = WallPlans.Two(), PlanPath = Path.Combine(work, "plan_012.json"),
                Profile = "parsons2024Rev6", BoundaryType = "^BW$", LastPlaced = new HashSet<string>(StringComparer.Ordinal),
            });
        }

        private RunOutcome Run(WallModel model)
        {
            return new ButtonFlow(new State { Work = work }, model, new Yes(), input => "", () => RunId).Run(8, "8 Walls", Walls().Body(), @"C:\Models\Site.rvt");
        }

        [Fact]
        public void R815_step_8_takes_the_walls_overlap_with_no_box_and_counts_it_in_its_issues_with_each_wall()
        {
            var model = new WallModel { Raised = { WallOverlapWarning.Words, WallOverlapWarning.Words } };

            RunOutcome outcome = Run(model);

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.NotNull(model.Given);
            Assert.True(model.Given.Knows(WallOverlapWarning.Words));
            // Nothing is left for Revit to show in its box, and each warning is one issue of the step
            Assert.Empty(model.Shown);
            string line = "Walls overlap, taken without a box (R815): wall type BW id w-1, wall type BW id w-2. Revit says: " + WallOverlapWarning.Words;
            Assert.Equal(2, outcome.Record.Issues.Count(issue => issue == line));
        }

        [Fact]
        public void R815_a_warning_of_other_words_still_shows_as_revit_gives_it()
        {
            // Broken on purpose: the floors overlap and a made up warning are no words of step 8, so Revit still shows them
            var model = new WallModel { Raised = { "Highlighted floors overlap.", "Made up warning." } };

            RunOutcome outcome = Run(model);

            Assert.Equal(new[] { "Highlighted floors overlap.", "Made up warning." }, model.Shown);
            Assert.Empty(model.Taken);
            Assert.Contains("Made up warning.", outcome.Record.Issues);
            Assert.DoesNotContain(outcome.Record.Issues, issue => issue.Contains("R815"));
        }

        [Fact]
        public void R815_the_list_of_step_8_never_accepts_a_pair_apart_and_knows_the_words_padded_but_not_a_part()
        {
            KnownWarningList list = WallOverlapWarning.List();

            Assert.Null(list.PlotParameter);
            Assert.Null(list.Decide(WallOverlapWarning.Words, TwoWalls));
            Assert.True(KnownWordsWarning.TakeAndList(list, " " + WallOverlapWarning.Words + " ", null));
            Assert.True(WallOverlapWarning.Owns(" " + WallOverlapWarning.Words + " "));
            Assert.False(list.Knows("Highlighted walls overlap."));
            Assert.False(WallOverlapWarning.Owns("Highlighted floors overlap."));
            Assert.Equal("Walls overlap, taken without a box (R815): no wall named. Revit says: " + WallOverlapWarning.Words, WallOverlapWarning.Line(null));
        }

        [Fact]
        public void R815_the_collector_lists_the_walls_overlap_by_its_line_and_reads_no_plot_parameter_when_the_list_names_none()
        {
            string source = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Revit", "WarningCollector.cs"));

            Assert.Contains("WallOverlapWarning.Owns(text) ? WallOverlapWarning.Line(elements) : KnownWordsWarning.Line(text, elements)", source);
            Assert.Contains("string.IsNullOrEmpty(Known.PlotParameter) ? null", source);
            string run = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.Common", "BoundaryWalls", "WallsRun.cs"));
            Assert.Contains("context.AcceptKnownWarnings(WallOverlapWarning.List());", run);
        }
    }
}
