#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.HowItWorks;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R845 and R846, Bader 12 Nov 2026: the 3 cards are the only way in. Advanced shows no step row, the ribbon no numbered button, and the
    // step code stays as the engine behind the cards. Made up values only
    public sealed class R845_CardsOnlyWayInTests
    {
        private static readonly string[] StepCommands = { "SetUpSiteCommand", "ReadDataCommand", "ReviewDataCommand", "CheckCommand", "FloorsAndRampsCommand", "LevelsCommand",
            "PlaceVillasCommand", "WallsCommand", "ReportCommand", "UpdateFromReportCommand" };

        private static string Source(params string[] parts)
        {
            return File.ReadAllText(Path.Combine(new[] { ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn" }.Concat(parts).ToArray()));
        }

        // Step 1 done and step 2 ready, every later step waits on the one before
        private static List<StepStatus> SecondIsNext()
        {
            var steps = new List<StepStatus> { new StepStatus(StepKind.Done), new StepStatus(StepKind.Ready) };
            steps.AddRange(Enumerable.Range(2, 7).Select(before => new StepStatus(StepKind.NeedsStep, before)));
            return steps;
        }

        private static bool Drawn(FrameworkElement element)
        {
            return element.ActualHeight > 0 && element.ActualWidth > 0;
        }

        [Fact]
        public void R845_advanced_shows_no_step_row_and_no_i_of_a_step_and_keeps_its_other_rows()
        {
            string page = Source("Pane", "SitePanePage.xaml");
            foreach (string gone in new[] { "StepRow", "ShownSteps", "RunCommand", "OnStepHelp", "Text=\"Steps\"", "Run them in order" })
            {
                Assert.DoesNotContain(gone, page);
            }
            Assert.DoesNotContain("OnStepHelp", Source("Pane", "SitePanePage.xaml.cs"));

            Fast7Sta.Run(() =>
            {
                SitePanePage old = Shots.LaidOutPane(true);
                var model = (PaneViewModel)old.DataContext;
                List<string> names = Enumerable.Range(1, 9).Select(PaneViewModel.StepName).ToList();
                List<string> texts = Fast7Sta.Visuals(old).OfType<TextBlock>().Where(Drawn).Select(block => block.Text).ToList();
                List<object> buttons = Fast7Sta.Visuals(old).OfType<Button>().Where(Drawn).Select(button => button.Content).ToList();

                // The names of the 9 steps stood one per row. Now the box alone names one, the next step, and no button names a step
                Assert.Equal(new[] { model.BoxName }, texts.Where(names.Contains));
                Assert.Contains(model.BoxName, names);
                Assert.Empty(texts.Where(PaneText.StepLines.Contains));
                Assert.DoesNotContain(buttons, content => content is string text && Regex.IsMatch(text, @"^(Run )?[1-9] "));
                // What else Advanced holds stays
                Assert.Equal(new object[] { "Edit", "Change", "How it works", "New project", "Use a saved project", "Export" }, buttons.Take(6));
                Assert.Equal(8, buttons.Count(content => Equals(content, "Browse")));
                Assert.Contains("Pick surfaces", buttons);
                Assert.Equal(15, buttons.Count);
                foreach (string kept in new[] { "Level profile", "Pre-setting", "Input files", "Scope", "Villa mode" })
                {
                    Assert.Contains(kept, texts);
                }
                Assert.Single(Fast7Sta.Visuals(old).OfType<CheckBox>().Where(Drawn), tick => Equals(tick.Content, PaneText.RoadTopography));
                Assert.Single(Fast7Sta.Visuals(old).OfType<CheckBox>().Where(Drawn), tick => Equals(tick.Content, "Dry run first, change nothing"));
            });
        }

        [Fact]
        public void R845_the_box_of_advanced_names_the_next_step_and_starts_none()
        {
            Fast7Sta.Run(() =>
            {
                PaneViewModel model = ((StagesViewModel)Shots.LaidOutStages(false).DataContext).Model;
                model.SetSteps(SecondIsNext());
                var asked = new List<int>();
                model.RunRequested += asked.Add;

                Assert.Equal("Next step", model.BoxLabel);
                Assert.Equal("Read data", model.BoxName);
                Assert.StartsWith(PaneText.StepLine2, model.BoxLine);
                Assert.False(model.BoxButtonShown);
                Assert.Equal(string.Empty, model.BoxButton);
                model.BoxCommand.Execute(null);
                Assert.Empty(asked);
            });
        }

        [Fact]
        public void R845_the_render_of_the_pane_with_advanced_open_holds_the_cards_and_the_old_pane_under_them()
        {
            Fast7Sta.Run(() =>
            {
                BitmapSource closed = Shots.Render(Shots.PaneStagesHead);
                BitmapSource open = Shots.Render(Shots.PaneAdvanced);
                Assert.Equal(380, open.PixelWidth);
                // The cards alone are under 900 high, the old pane under them adds its three parts
                Assert.True(open.PixelHeight > 1300 && open.PixelHeight > closed.PixelHeight, open.PixelHeight.ToString());
                Assert.Contains(Shots.PaneAdvanced, Shots.Drafts);
            });
        }

        [Fact]
        public void R846_the_ribbon_holds_site_adaptation_rules_and_show_pane_and_no_step_button()
        {
            Assert.Equal(new[] { "Rules", "Pane" }, RibbonLayout.Panels.Select(panel => panel.Name));
            Assert.Equal(new[] { "Site Adaptation Rules", "Show pane" }, RibbonLayout.AllButtons.Select(button => button.Label));
            Assert.All(RibbonLayout.AllButtons, button => Assert.Equal(0, button.Number));
            Assert.All(RibbonLayout.AllButtons, button => Assert.NotNull(typeof(RibbonLayout).Assembly.GetType(button.CommandClass)));

            // No command class of a step is left for Revit to run
            string commands = Source("Commands", "ButtonCommands.cs");
            foreach (string gone in StepCommands)
            {
                Assert.Null(typeof(RibbonLayout).Assembly.GetType(RibbonButtonSpec.CommandNamespace + gone));
                Assert.DoesNotContain("class " + gone, commands);
            }
            // The ribbon is built from the panels alone, never from the steps
            string app = Source("App", "App.cs");
            Assert.Contains("foreach (RibbonPanelSpec panel in RibbonLayout.Panels)", app);
            Assert.DoesNotContain("RibbonLayout.Steps", app);
        }

        [Fact]
        public void R845_R846_the_three_cards_still_reach_steps_1_to_9_and_update_from_report()
        {
            // The engine still knows each step and Update from report, as Buttons.Run of the cards and of the test host finds them
            Assert.Equal(Enumerable.Range(1, 9), RibbonLayout.Buttons.Select(button => button.Number));
            Assert.Equal(new[] { "1 Set up site", "2 Read data", "3 Review data", "4 Check", "5 Floors and ramps", "6 Levels", "7 Place villas", "8 Walls", "9 Report" },
                Enumerable.Range(1, 9).Select(number => RibbonLayout.Button(number).Label));
            Assert.Equal("Update from report", RibbonLayout.ButtonOf(ButtonsRun.UpdateCommand).Label);
            Assert.Equal(10, RibbonLayout.Steps.Count);
            Assert.Empty(RibbonLayout.AllButtons.Intersect(RibbonLayout.Steps));
            Assert.Throws<ArgumentOutOfRangeException>(() => RibbonLayout.Button(10));
            Assert.Throws<ArgumentOutOfRangeException>(() => RibbonLayout.ButtonOf("NoSuchCommand"));

            var reached = new SortedSet<int>();
            int reads = 0;
            int updates = 0;
            int builds = 0;
            Fast7Sta.Run(() =>
            {
                var stages = (StagesViewModel)Shots.LaidOutStages(false).DataContext;
                var asked = new List<int>();
                stages.Model.RunRequested += asked.Add;
                stages.ChooseFolderRequested += () => reads++;
                stages.UpdateRequested += () => updates++;
                stages.BuildRequested += () => builds++;

                // Card 1: step 1 by its button, step 2 by Choose project folder once step 1 is done, step 3 by its button
                stages.Model.SetSteps(SecondIsNext());
                stages.SetUpCommand.Execute(null);
                stages.Card1.Command.Execute(null);
                stages.Model.SetSteps(Enumerable.Range(1, 9).Select(step => new StepStatus(StepKind.Done)).ToList());
                Assert.True(stages.ReviewCommand.CanExecute(null));
                stages.ReviewCommand.Execute(null);
                // Card 3: step 9 by Report, Update from report by its own ask
                Assert.True(stages.Card3.Command.CanExecute(null));
                stages.Card3.Command.Execute(null);
                Assert.True(stages.UpdateCommand.CanExecute(null));
                stages.UpdateCommand.Execute(null);
                // Card 2: Build asks the host, which runs the steps of the chain
                stages.Card2.Command.Execute(null);
                Assert.Equal(new[] { 1, 3, 9 }, asked);
                reached.UnionWith(asked);
            });
            Assert.Equal(new[] { 1, 1, 1 }, new[] { reads, updates, builds });
            reached.Add(StagesViewModel.ReadDataStep);
            reached.UnionWith(BuildChain.Order);
            Assert.Equal(Enumerable.Range(1, 9), reached);

            // The host hands each ask to the one entry of the engine
            string host = Source("App", "PaneHost.cs");
            Assert.Contains("stages.ChooseFolderRequested += () => ChooseProjectFolder(stages);", host);
            Assert.Contains("stages.FolderChosen(before, chosen);", host);
            Assert.Contains("Model.RequestRun(StepAfterFolder(Model.Statuses, before, chosen));", Source("Pane", "StageCards.cs"));
            Assert.Contains("runner.Command = Common.ButtonsRun.UpdateCommand;", host);
            Assert.Contains("Buttons.Run(command, app);", host);
            Assert.Contains("Buttons.Run(Number, app);", host);
            Assert.Contains("model.RequestRun(number);", host);
        }

        [Fact]
        public void R845_no_word_of_the_pane_the_help_or_how_it_works_sends_a_person_to_a_row_or_a_button_that_is_gone()
        {
            Assert.DoesNotContain("Advanced", HowItWorksText.Line);
            Assert.DoesNotContain("button per step", HowItWorksText.Line);
            // How it works names, per step, the button of the card that runs it
            var how = new HowItWorksViewModel();
            Assert.Equal(new[] { "1 Set up site", "Choose project folder", "3 Review data", "Build", "Build", "Build", "Build", "Build", "Report" }, how.Rows.Select(row => row.Button));
            Assert.Equal("5 buttons in the pane", how.CountLine);

            string guide = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "steps", "buttons-guide.md"));
            foreach (string text in new[] { guide, Source("Help", "help.json") })
            {
                Assert.DoesNotMatch(@"\bRun [1-9] [A-Z]", text);
                Assert.DoesNotContain("or the ribbon", text);
            }
            Assert.Contains("The three cards of the pane start every step.", guide);
            foreach (string file in new[] { "StageText.cs", "StageResultsText.cs", "SiteBuildText.cs", "PaneText.cs" })
            {
                Assert.DoesNotMatch(@"step row|from Advanced|in Advanced|Run <n> <name>", Source("Pane", file));
            }
        }
    }
}
#endif
