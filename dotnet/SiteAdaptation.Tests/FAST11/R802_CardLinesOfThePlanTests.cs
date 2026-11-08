#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests.FAST11
{
    // R801, R802, F29 and F30 of short check 2. Card 2 names the plots the plan of 4 Check leaves out with why, card 3 counts the plots of that
    // plan with no road level, not the plots of the whole read the build plan lists. A made up work folder only
    public sealed class R802_CardLinesOfThePlanTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "r802-" + Guid.NewGuid().ToString("N"));

        public R802_CardLinesOfThePlanTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private void Check()
        {
            string folder = Path.Combine(work, CheckRun.FolderPrefix + "20261108-1");
            Directory.CreateDirectory(folder);
            string plan = Path.Combine(folder, CheckRun.PlanName("N1"));
            File.WriteAllText(plan, "{\"neighbourhood\":\"N1\",\"summary\":{\"plots\":20,\"written\":17,\"notWritten\":3,\"notWrittenByFirstKey\":{\"levels.ffl\":2,\"unit\":1},"
                + "\"writtenWithMissing\":15,\"plotsWithNoRoadLevel\":[\"4\",\"9\"]}}");
            File.WriteAllText(Path.Combine(folder, CheckRun.RecordFile), CheckRun.Record("R4-1", new Dictionary<string, string> { [plan] = "made up" }).ToString());
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
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
        public void R801_R802_card_2_names_the_plots_left_out_and_card_3_counts_the_plan_of_the_scope()
        {
            Check();
            OnSta(() =>
            {
                var stages = (StagesViewModel)Shots.LaidOutStages(false).DataContext;
                // The build plan of the whole read lists 2615 plots with no road level, as on 7 Nov
                var results = new StageResults { HasPlan = true };
                for (int plot = 0; plot < 2615; plot++)
                {
                    results.Issues.Add(plot + ": " + Common.SiteBuild.PlanTexts.NoRoadLevel(plot.ToString()));
                }
                stages.ResultsNow = () => results;
                stages.Refresh();
                // Broken on purpose: with no work folder card 3 still counts the build plan, as R691 does
                Assert.StartsWith("2615 plots have no road level", stages.NoRoadLevelLine);
                Assert.Equal(string.Empty, stages.Card2.Line);
                stages.WorkFolderNow = () => work;
                stages.Refresh();
                Assert.Equal("2 plots have no road level, so no floor level, and steps 6 to 8 leave them out.", stages.NoRoadLevelLine);
                string left = "3 of 20 plots are not built: no floor level 2, no unit 1. Built with a value missing: 15 plots, each value tagged missing in the plan.";
                Assert.Equal(left, stages.Card2.Line);
                stages.ShowBuild("Build done: 8 steps.");
                Assert.Equal("Build done: 8 steps. " + left, stages.Card2.Line);
            });
        }
    }
}
#endif
