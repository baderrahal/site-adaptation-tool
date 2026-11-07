using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ManualRules;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
#if NETFRAMEWORK
using SiteAdaptation.AddIn.NewProject;
#endif
using Xunit;

namespace SiteAdaptation.Tests.FAST10
{
    // R795 to R799, F8. A project made with Empty project from the set the install ships, the config and rules profile of the repo, holds no rules
    // name, road surface, schedule or profile of the past job. The past values are read from the repo files, so no project value stands in this file
    public sealed class NewProjectNoPastJobTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "fast10-" + Guid.NewGuid().ToString("N"));
        private readonly string repo = ThemeGateTests.RepoRoot();
        private readonly string shipped;
        private readonly string work;
        private readonly JObject pastConfig;
        private readonly JObject pastRules;

        public NewProjectNoPastJobTests()
        {
            shipped = Path.Combine(root, "addin", ShippedSet.FolderName);
            work = Path.Combine(root, "work root");
            pastConfig = JObject.Parse(File.ReadAllText(Path.Combine(repo, "config", "project.json")));
            pastRules = JObject.Parse(File.ReadAllText(Path.Combine(repo, (string)pastConfig["profile"])));
            // The files the install lands in ShippedSet, Install-SiteAdaptation.ps1 $setFiles
            foreach (string file in new[] { "config/project.json", "config/floor_outlines.json", "config/prototype_offsets.json", "config/rule_values.json", "config/" + (string)pastConfig["addin"]["readData"]["cadLayers"], (string)pastConfig["addin"]["parameterSheet"], (string)pastConfig["profile"], "rules/templates/manual.json" })
            {
                string target = Path.Combine(shipped, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(repo, file), target);
            }
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        // Every value of the past job the four kinds of F8 name, from the repo files
        private IReadOnlyList<string> PastValues()
        {
            var values = new List<string> { (string)pastRules["project"], Path.GetFileNameWithoutExtension((string)pastConfig["profile"]) };
            values.AddRange(pastConfig.SelectToken("designBasis.roadLevel.surfaces").Select(item => (string)item));
            values.AddRange(pastConfig.SelectToken("siteBuild.roadSurfaces").Select(item => (string)item));
            values.AddRange(pastConfig["schedules"].Select(item => (string)item));
            values.AddRange(((JObject)pastConfig.SelectToken("connect.neighbourhoods")).Properties().Where(hood => hood.Value.Type == JTokenType.Object).Select(hood => (string)hood.Value["schedule"]));
            values.Add((string)pastConfig.SelectToken("pastJob.schedule"));
            values.Add(Path.GetFileName((string)pastConfig.SelectToken("addin.position.file")));
            return values.Where(value => !string.IsNullOrEmpty(value)).Distinct().ToList();
        }

        private void HoldsNone(string text, string where)
        {
            foreach (string value in PastValues())
            {
                Assert.False(text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0, where + " holds the past value " + value);
            }
        }

        private ShippedSet Set()
        {
            return new ShippedSet(shipped, work, "Site A", true);
        }

        [Fact]
        public void R795_R796_the_copy_of_empty_project_holds_no_rules_name_road_surface_schedule_or_profile_of_the_past_job()
        {
            Assert.True(PastValues().Count >= 8);
            ProjectSet set = Set().Take(File.ReadAllText);
            HoldsNone(File.ReadAllText(set.ConfigFile), "the config of the new project");
            HoldsNone(File.ReadAllText(set.RulesFile), "the rules profile of the new project");
            HoldsNone(set.RulesFile, "the path of the rules profile");
            Assert.Equal(NewProjectValues.RulesName, Path.GetFileName(set.RulesFile));
            Assert.Equal("Site A", (string)JObject.Parse(File.ReadAllText(set.RulesFile))["project"]);
            Assert.False(File.Exists(Path.Combine(Set().ProjectFolder, (string)pastConfig["profile"])));
            // The level profile of a new build stays the one of the shipped profile
            Assert.Equal((string)pastRules["levelProfiles"]["default"], set.LevelProfile);
            JObject config = JObject.Parse(File.ReadAllText(set.ConfigFile));
            Assert.Equal(JTokenType.Null, config["inputRoot"].Type);
            // R401. The status entries of the rules values follow the profile to its new path
            JObject rules = JObject.Parse(File.ReadAllText(set.RulesFile));
            Assert.Equal(((JObject)pastRules["status"][(string)pastConfig["profile"]]).Count, ((JObject)rules["status"][(string)config["profile"]]).Count);
        }

        [Fact]
        public void R796_the_rules_window_of_the_new_project_says_no_past_job_measure()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            var table = new ManualRuleTable(ManualRuleBlock.FromFile(set.RulesFile), null);
            Assert.NotEmpty(table.Rows(ManualChip.All));
            Assert.All(table.Rows(ManualChip.All), rule => Assert.Equal(PastJobState.NotMeasured, table.PastJob(rule).State));
            Assert.All(table.Rows(ManualChip.All), rule => Assert.Null(table.PastJob(rule).Text));
            // The past job keeps its measures
            Assert.Contains(ManualRuleBlock.FromFile(Path.Combine(repo, (string)pastConfig["profile"])).Rules, rule => rule.PastJobState != PastJobState.NotMeasured);
        }

        [Fact]
        public void R798_the_read_of_the_new_project_names_no_schedule_or_surface_of_the_past_job()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            var pane = new[] { new RunInput("Settings", set.ConfigFile, false), new RunInput("Road surface", Path.Combine(root, "roads"), true) };
            var roles = new Dictionary<string, string> { ["Settings"] = ReadInputs.Config, ["Road surface"] = ReadInputs.RoadSurface };
            Directory.CreateDirectory(Path.Combine(root, "roads"));
            ReadPlan plan = ReadInputs.Make(pane, roles, Path.Combine(root, "work"), null, DesignBasis.Rev6);
            string lines = string.Join("\n", plan.Stops.Concat(plan.PastLacks).Concat(plan.Inputs.Select(input => input.Path)));
            HoldsNone(lines, "the read");
            Assert.Contains(ReadInputs.NoSchedule, plan.Stops);
            Assert.DoesNotContain(plan.Stops, line => line.Contains("fit schedule"));
        }

        [Fact]
        public void R799_the_position_file_of_the_new_project_is_its_own_and_on_no_disk()
        {
            ProjectSet set = Set().Take(File.ReadAllText);
            JObject config = JObject.Parse(File.ReadAllText(set.ConfigFile));
            string file = WorkRoot.Resolve((string)config.SelectToken("addin.position.file"), () => work);
            Assert.StartsWith(Set().ProjectFolder, file);
            Assert.False(File.Exists(file));
        }

        [Fact]
        public void R795_the_past_job_mode_keeps_its_values_and_the_shipped_files_stay_as_they_were()
        {
            string[] before = Directory.GetFiles(shipped, "*", SearchOption.AllDirectories).Select(path => path + "|" + FileHash.Sha256(path)).OrderBy(line => line, StringComparer.Ordinal).ToArray();
            Set().Take(File.ReadAllText);
            Assert.Equal(before, Directory.GetFiles(shipped, "*", SearchOption.AllDirectories).Select(path => path + "|" + FileHash.Sha256(path)).OrderBy(line => line, StringComparer.Ordinal).ToArray());
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(shipped, "config", "project.json")));
            Assert.NotEmpty((JArray)config.SelectToken("designBasis.roadLevel.surfaces"));
            Assert.NotEmpty((JArray)config["schedules"]);
            Assert.Equal((string)pastConfig["profile"], (string)config["profile"]);
            Assert.False(string.IsNullOrEmpty((string)JObject.Parse(File.ReadAllText(Path.Combine(shipped, (string)config["profile"])))["project"]));
        }

        [Fact]
        public void R795_a_key_the_shipped_config_does_not_hold_stays_absent()
        {
            JObject made = NewProjectValues.Config(JObject.Parse("{ 'profile': 'rules/a.json', 'designBasis': { 'mode': 'old', 'modes': ['old'] } }"), "Site A");
            Assert.Equal("rules/" + NewProjectValues.RulesName, (string)made["profile"]);
            Assert.Null(made["schedules"]);
            Assert.Null(made["addin"]);
            Assert.Equal("old", (string)made["designBasis"]["mode"]);
            Assert.Equal(NewProjectValues.RulesName, NewProjectValues.RulesPath("a.json"));
        }

        [Fact]
        public void R795_a_build_of_the_new_project_takes_the_pick_of_set_up_and_refuses_none_as_before()
        {
            JObject config = ProjectModes.Apply(NewProjectValues.Config(pastConfig, "Site A"), DesignBasis.Rev6);
            var picked = new[] { "made up road A", "made up road B" };
            DesignBasis basis = DesignBasis.Of(config, () => null, picked);
            basis.TakePick(picked);
            Assert.Equal(picked, basis.Surfaces);
            Assert.Contains(DesignBasis.BadSurfaces, Assert.Throws<InvalidDataException>(() => DesignBasis.Of(config, () => null)).Message);
            Assert.Contains(DesignBasis.BadPick, Assert.Throws<InvalidDataException>(() => DesignBasis.Of(config, () => null, new string[0]).TakePick(new string[0])).Message);
            // A config that names surfaces keeps them where no pick is made
            Assert.Equal(pastConfig.SelectToken("designBasis.roadLevel.surfaces").Select(item => (string)item), DesignBasis.Of(ProjectModes.Apply((JObject)pastConfig.DeepClone(), DesignBasis.Rev6), () => null).Surfaces);
        }

#if NETFRAMEWORK
        private NewProjectViewModel Window()
        {
            return new NewProjectViewModel(new ProjectSettings(), "Site", new SavedStart[0], null, (path, isFolder) => File.Exists(path) || Directory.Exists(path), null, path => File.Exists(path) ? File.ReadAllText(path) : null, null, true, (inFolder, effective) => null, Set());
        }

        [Fact]
        public void R797_the_new_project_window_proposes_no_road_surface_of_the_past_job_before_and_after_save()
        {
            NewProjectViewModel model = Window();
            SetupRowViewModel road = model.Rows.Single(item => item.Row.Id == SetupRows.RoadLevels);
            string shown = string.Join("\n", model.Rows.Select(row => row.Help + "|" + row.HelpLine).Concat(new[] { road.SurfacesLine, model.ModeLine, model.StopLine, model.TodoLine, model.ReportLine }));
            HoldsNone(shown, "the New project window");
            Assert.Equal(RoadSurfacesText.NonePicked, road.SurfacesLine);
            model.Save();
            Assert.NotNull(model.Result);
            RoadPickState state = RoadPickState.Of(model.Result, File.ReadAllText);
            Assert.Empty(state.Effective);
            HoldsNone(model.Result.RulesFile + model.Result.ConfigFile, "the settings of the new project");
            // The Rules row of the pane names the project, not the past job
            Assert.Equal("Site A", AddIn.Pane.RulesProfiles.Name(File.ReadAllText(model.Result.RulesFile)));
        }

        [Fact]
        public void R798_step_4_of_the_pane_names_no_past_job()
        {
            Assert.DoesNotContain("past job", AddIn.Pane.PaneText.StepLines[3]);
        }
#endif
    }
}
