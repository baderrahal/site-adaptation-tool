using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;
using SiteAdaptation.Common.SiteBuild;
using Xunit;

namespace SiteAdaptation.Tests.FAST2
{
    // R705, Q504 answer 5A. A plot with no floor level is marked, steps 6 to 8 skip it and the issues name it with the words no road level
    public class R705_NoRoadLevelTests
    {
        [Fact]
        public void R705_each_plot_with_no_floor_level_is_one_issue_with_no_road_level_and_steps_6_to_8_hold_none_of_them()
        {
            var issues = new List<PlanIssue>();
            List<PlanPlot> plots = PlanPlots.Make(new[] { new PlotIn { Number = "1", VillaType = "T1" }, new PlotIn { Number = "2", VillaType = "T2" } },
                RuleNumbers.From(new RuleForBuild[0], issues), issues);
            List<PlanIssue> none = issues.Where(issue => issue.Line.Contains("no road level")).ToList();
            Assert.Equal(new[] { "1", "2" }, none.Select(issue => issue.Plot).ToArray());
            Assert.All(plots, plot => Assert.Contains(plot.Marked, mark => mark.StartsWith("ffl: ")));
            JArray steps = PlanSteps.Make(plots);
            foreach (int button in new[] { 6, 7, 8 })
            {
                Assert.Equal(0, (int)steps.Single(step => (int)step["button"] == button)["ready"]);
            }
        }
    }
}
