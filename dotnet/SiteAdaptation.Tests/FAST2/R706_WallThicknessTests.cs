using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ProjectRules;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.SiteBuild;
using Xunit;

namespace SiteAdaptation.Tests.FAST2
{
    // R706, Q149 and Q336, item 7 of FAST-2
    public class R706_WallThicknessTests
    {
        private static RuleForBuild Rule(string id, string text)
        {
            return new RuleForBuild { Id = id, Value = new JValue(text), Source = ProjectRulesFile.FromRule, Status = "not confirmed" };
        }

        [Fact]
        public void R706_the_boundary_wall_thickness_comes_from_rule_m14_in_metres_with_the_rule_and_its_status()
        {
            var issues = new List<PlanIssue>();
            RuleNumbers numbers = RuleNumbers.From(new[] { Rule("M14", "Irregular plots: boundary wall 200 mm inside the plot limit, garden wall extends 2.0 m") }, issues);
            Assert.Equal(0.2, numbers.BoundaryWallThicknessM.Value, 9);
            Assert.Empty(issues);
            PlanValue thickness = PlanPlots.Make(new[] { new PlotIn { Number = "1", VillaType = "T1" } }, numbers, issues)[0].Get("boundaryWallThicknessM");
            Assert.Equal("rule", thickness.Source);
            Assert.Equal("M14", thickness.Rule);
            Assert.Equal("not confirmed", thickness.Status);
        }

        [Fact]
        public void R706_a_text_in_another_form_gives_no_thickness_and_an_issue_and_no_rule_leaves_it_missing()
        {
            var issues = new List<PlanIssue>();
            RuleNumbers numbers = RuleNumbers.From(new[] { Rule("M14", "a thin wall") }, issues);
            Assert.Null(numbers.BoundaryWallThicknessM);
            Assert.Single(issues);
            var none = new List<PlanIssue>();
            PlanPlot plot = PlanPlots.Make(new[] { new PlotIn { Number = "1", VillaType = "T1" } }, RuleNumbers.From(new RuleForBuild[0], none), none)[0];
            Assert.True(plot.Get("boundaryWallThicknessM").IsMissing);
            Assert.Contains(plot.Marked, mark => mark.StartsWith("boundaryWallThicknessM: "));
        }

        [Fact]
        public void Q336_the_link_parameters_of_the_shipped_sheet_are_in_the_group_text_and_plot_on_floors_in_data()
        {
            string config = Path.Combine(R700_SharedParametersNeedTests.RepoRoot(), "config", "project.json");
            ParameterSheet sheet = ParameterSheet.FromConfig(config, JObject.Parse(File.ReadAllText(config)));
            List<ParameterSpec> links = sheet.Parameters.Where(item => item.LinksOnly).ToList();
            Assert.Equal(10, links.Count);
            Assert.All(links, item => Assert.Equal("Text", item.Group));
            Assert.Equal("Data", sheet.Parameters.Single(item => item.Name == "Plot").Group);
        }
    }
}
