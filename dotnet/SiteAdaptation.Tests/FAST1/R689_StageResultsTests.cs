#if NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R689, R690. Card 3: the issues of the last build, the source mix of its values, the rules table from a rule issue and Update from report. Made up plan only
    public sealed class R689_StageResultsTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "R689_" + Guid.NewGuid().ToString("N"));

        public R689_StageResultsTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private static JObject Value(string source)
        {
            return new JObject { ["value"] = source == "missing" ? null : new JValue(1), ["source"] = source };
        }

        private string Plan(string folder, DateTime written)
        {
            string dir = Path.Combine(work, folder);
            Directory.CreateDirectory(dir);
            var plan = new JObject
            {
                ["issues"] = new JArray(new JObject { ["plot"] = "1", ["line"] = "Made up fault" }, new JObject { ["plot"] = null, ["line"] = "2 of 15 rules are not confirmed" }),
                ["blocking"] = new JArray(),
                ["plots"] = new JArray(
                    new JObject { ["number"] = "1", ["values"] = new JObject { ["a"] = Value("computed"), ["b"] = Value("rule"), ["c"] = Value("missing") } },
                    new JObject { ["number"] = "2", ["values"] = new JObject { ["a"] = Value("computed"), ["b"] = Value("person entered"), ["c"] = Value("default") } }),
            };
            string path = Path.Combine(dir, SiteBuildResult.PlanName);
            File.WriteAllText(path, plan.ToString());
            File.SetLastWriteTimeUtc(path, written);
            return path;
        }

        [Fact]
        public void R689_the_newest_plan_of_the_work_folder_gives_the_issues_and_the_source_mix()
        {
            Assert.Null(StageResults.LatestPlan(work));
            Assert.Null(StageResults.LatestPlan(null));
            Plan("build_a", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            string newer = Plan("build_b", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
            // A folder of another name is no build
            Plan("other_c", new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(newer, StageResults.LatestPlan(work));

            StageResults results = StageResults.Read(newer);
            Assert.True(results.HasPlan);
            // Review of PR 6: the issues are those of the plan, the closing messages of the steps are no issues
            Assert.Equal(new[] { "1: Made up fault", "2 of 15 rules are not confirmed" }, results.Issues.ToArray());
            Assert.True(results.RuleIssue);
            Assert.Equal(2, results.Mix.ComputedCount);
            Assert.Equal(1, results.Mix.RuleCount);
            Assert.Equal(1, results.Mix.PersonCount);
            Assert.Equal(1, results.Mix.MissingCount);
            Assert.Equal(1, results.Mix.OtherCount);
            Assert.Equal("Values: computed 2, rule 1, person entered 1, missing 1, other 1.", results.Mix.Line);
            // Broken on purpose: a plan that does not read gives no plan, no issue and no mix
            File.WriteAllText(newer, "{ not json");
            StageResults broken = StageResults.Read(newer);
            Assert.False(broken.HasPlan);
            Assert.Empty(broken.Issues);
            Assert.False(broken.RuleIssue);
            Assert.False(StageResults.Read(null).HasPlan);
        }

        [Fact]
        public void R689_R690_card_3_shows_the_count_the_bar_the_rules_from_a_rule_issue_and_asks_for_update_from_report()
        {
            string plan = Plan("build_a", DateTime.UtcNow);
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var stages = (StagesViewModel)Shots.LaidOutStages(false).DataContext;
                    Assert.Equal(StageResultsText.NoBuild, stages.IssuesLine);
                    Assert.False(stages.HasMix);
                    stages.ResultsNow = () => StageResults.Read(plan);
                    stages.Refresh();
                    Assert.Equal("2 issues from the last build.", stages.IssuesLine);
                    Assert.True(stages.HasMix);
                    Assert.True(stages.RuleIssue);
                    Assert.Equal(2, stages.MixComputed.Value);
                    Assert.True(stages.MixComputed.IsStar);
                    Assert.Equal(StageResultsText.ReportLine, stages.Card3.Line);
                    bool update = false;
                    stages.UpdateRequested += () => update = true;
                    stages.UpdateCommand.Execute(null);
                    Assert.True(update);
                }
                catch (Exception caught)
                {
                    failure = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.True(failure == null, failure?.ToString());
        }

        // R691, Q504 5A: the plots the plan lists with the words no road level are counted on card 3, any case of the words
        [Fact]
        public void R691_the_plots_with_no_road_level_are_counted_on_card_3()
        {
            // The words of R705 of FAST-2's plan, in any case
            var results = new StageResults { HasPlan = true };
            results.Issues.AddRange(new[] { "12: Plot 12 has no road level, so it has no floor level and steps 6 Levels, 7 Place villas and 8 Walls skip it.", "13: No road level", "14: Made up fault" });
            Assert.Equal(2, results.NoRoadLevelCount);
            // Broken on purpose: other words are no such plot
            var other = new StageResults();
            other.Issues.Add("12: road levels differ");
            Assert.Equal(0, other.NoRoadLevelCount);
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var stages = (StagesViewModel)Shots.LaidOutStages(false).DataContext;
                    Assert.Equal(string.Empty, stages.NoRoadLevelLine);
                    stages.ResultsNow = () => results;
                    stages.Refresh();
                    Assert.Equal("2 plots have no road level, so no floor level, and steps 6 to 8 leave them out.", stages.NoRoadLevelLine);
                }
                catch (Exception caught)
                {
                    failure = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.True(failure == null, failure?.ToString());
        }

        [Fact]
        public void R690_update_from_report_runs_the_command_of_its_ribbon_button()
        {
            string host = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "App", "PaneHost.cs"));
            Assert.Contains("runner.Command = nameof(Commands.UpdateFromReportCommand);", host);
            Assert.Contains("Buttons.Run(command, app);", host);
        }
    }
}
#endif
