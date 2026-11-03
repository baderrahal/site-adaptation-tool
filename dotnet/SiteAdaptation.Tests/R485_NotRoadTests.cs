using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R485. designBasis.roadLevel.notRoad, the grading files of the road folder that no build takes, read under rev6 alone. Made up names, the 7 stems read
    // from the shipped config. python/tests/test_r485_not_road.py holds the same lines
    public sealed class R485_NotRoadTests : IDisposable
    {
        private const string Road = "Made Up Job__Road A", Grading = "Made Up Job_Grading__Lot 7 - (3)";
        private const string Why = "made up: a grading of the lots, not finished road";
        private const string Line = Grading + " is no road surface: " + Why + " (R485)";
        private readonly string root = Path.Combine(Path.GetTempPath(), "r485-" + Guid.NewGuid().ToString("N"));

        public R485_NotRoadTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private static string PastMode()
        {
            return ((JArray)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["designBasis"]["modes"]).Select(mode => (string)mode).Single(mode => mode != DesignBasis.Rev6);
        }

        private static JObject Config(string mode, params string[] surfaces)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["designBasis"]["mode"] = mode;
            config["designBasis"]["roadLevel"]["surfaces"] = new JArray(surfaces.Cast<object>().ToArray());
            config["designBasis"]["roadLevel"]["notRoad"] = new JObject { [Grading] = Why };
            return config;
        }

        private string RoadFolder()
        {
            string folder = Path.Combine(root, "roads");
            Directory.CreateDirectory(folder);
            foreach (string name in new[] { Road, Grading })
            {
                File.WriteAllText(Path.Combine(folder, name + ".xml"), "<LandXML/>");
            }
            return folder;
        }

        [Fact]
        public void R485_a_not_road_file_is_left_out_of_the_window_under_rev6()
        {
            Assert.Equal(new[] { Road }, RoadSurfaceChoices.InFolder(RoadFolder(), Config(DesignBasis.Rev6, Road)));
        }

        [Fact]
        public void R485_not_road_is_ignored_under_the_past_job()
        {
            JObject past = Config(PastMode(), Road);
            Assert.Equal(new[] { Grading, Road }, RoadSurfaceChoices.InFolder(RoadFolder(), past));
            Assert.Null(RoadSurfaceChoices.NotRoadWhy(past, Grading));
            Assert.Equal(PastMode(), DesignBasis.Of(Config(PastMode(), Grading), () => null).Mode);
            Assert.Null(new ReadSettings(past).NotRoadRefusal(new[] { Grading }));
        }

        [Fact]
        public void R485_the_project_mode_turns_it_on_over_a_past_job_config()
        {
            JObject config = Config(PastMode(), Road);
            Assert.Null(RoadSurfaceChoices.NotRoadWhy(config, Grading));
            Assert.Equal(Why, RoadSurfaceChoices.NotRoadWhy(ProjectModes.Apply(config, DesignBasis.Rev6), Grading));
        }

        [Fact]
        public void R485_the_stem_is_held_whatever_its_case_as_a_windows_file_name_is()
        {
            Assert.Equal(Why, RoadSurfaceChoices.NotRoadWhy(Config(DesignBasis.Rev6, Road), Grading.ToUpperInvariant()));
            Assert.Null(RoadSurfaceChoices.NotRoadWhy(Config(DesignBasis.Rev6, Road), Road));
        }

        [Fact]
        public void R485_a_build_refuses_a_not_road_file_of_config_with_its_file_and_why_the_line_of_python()
        {
            Assert.Equal(Line, Assert.Throws<InvalidDataException>(() => DesignBasis.Of(Config(DesignBasis.Rev6, Road, Grading), () => null)).Message);
            string python = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "python", "design_basis.py"));
            Assert.Contains("\"%s is no road surface: %s (R485)\"", python);
        }

        [Fact]
        public void R485_a_build_refuses_a_not_road_file_of_the_pick()
        {
            DesignBasis basis = DesignBasis.Of(Config(DesignBasis.Rev6, Road), () => null);
            Assert.Equal(Line, Assert.Throws<InvalidDataException>(() => basis.TakePick(new[] { Grading })).Message);
            basis.TakePick(new[] { Road });
            Assert.Equal(new[] { Road }, basis.Surfaces);
        }

        [Fact]
        public void R485_the_worker_read_refuses_a_not_road_file_by_the_same_line()
        {
            Assert.Equal(Line, new ReadSettings(Config(DesignBasis.Rev6, Road)).NotRoadRefusal(new[] { Road, Grading }));
            Assert.Null(new ReadSettings(Config(DesignBasis.Rev6, Road)).NotRoadRefusal(new[] { Road }));
        }

        [Fact]
        public void R485_read_inputs_stop_on_a_not_road_file_of_the_pick_with_the_same_line()
        {
            string config = Path.Combine(root, "project.json");
            File.WriteAllText(config, Config(PastMode(), Road).ToString());
            var pane = new List<RunInput> { new RunInput("Settings", config, false), new RunInput("Road surface", RoadFolder(), true) };
            var roles = new Dictionary<string, string> { ["Settings"] = "config", ["Road surface"] = ReadInputs.RoadSurface };
            ReadPlan plan = ReadInputs.Make(pane, roles, Path.Combine(root, "work"), new[] { Road, Grading }, DesignBasis.Rev6);
            Assert.Contains(Line, plan.Stops);
        }

        [Theory]
        [InlineData("[\"list\"]")]
        [InlineData("\"text\"")]
        [InlineData("{ \"stem\": \"\" }")]
        [InlineData("{ \"stem\": 7 }")]
        public void R485_a_not_road_that_is_no_object_of_stem_and_why_is_refused(string bad)
        {
            JObject config = Config(DesignBasis.Rev6, Road);
            config["designBasis"]["roadLevel"]["notRoad"] = JToken.Parse(bad);
            Assert.Equal("designBasis.roadLevel.notRoad must be an object of file stem to why (R485)", Assert.Throws<InvalidDataException>(() => DesignBasis.Of(config, () => null)).Message);
            Assert.Null(RoadSurfaceChoices.NotRoadWhy(config, "stem"));
        }

        [Fact]
        public void R485_the_shipped_config_lists_the_7_files_each_with_grading_and_not_finished_road_and_none_is_a_named_surface()
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            JObject road = (JObject)config["designBasis"]["roadLevel"];
            JObject listed = (JObject)road["notRoad"];
            Assert.Equal(7, listed.Count);
            Assert.All(listed.Properties(), item =>
            {
                Assert.Contains("grading", ((string)item.Value).ToLowerInvariant());
                Assert.Contains("not finished road", (string)item.Value);
            });
            var names = new HashSet<string>(listed.Properties().Select(item => item.Name));
            Assert.Empty(names.Intersect(road["surfaces"].Select(name => (string)name)));
            Assert.Empty(names.Intersect(config["siteBuild"]["roadSurfaces"].Select(name => (string)name)));
        }
    }
}
