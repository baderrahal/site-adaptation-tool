#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R686, R687. The press of Build on card 2 over siteBuild of Common, on a made up read in the temp folder. Every point is made up, far from any real site
    public sealed class R686_SiteBuildFlowTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "R686_" + Guid.NewGuid().ToString("N"));

        public R686_SiteBuildFlowTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private static string Square(double east, double north)
        {
            return "[[" + east + "," + north + "],[" + (east + 10) + "," + north + "],[" + (east + 10) + "," + (north + 20) + "],[" + east + "," + (north + 20) + "]]";
        }

        private SiteBuildPlace Place(string plots)
        {
            string work = Path.Combine(root, "work");
            string read = Path.Combine(work, "read-a");
            Directory.CreateDirectory(read);
            File.WriteAllText(Path.Combine(read, "plots.json"), plots);
            File.WriteAllText(Path.Combine(read, "project_profile.json"), "{}");
            JObject config = SiteBuildSamples.Config();
            config["inputRoot"] = Path.Combine(root, "input");
            config["siteBuild"] = new JObject { ["minFreeGB"] = 0.1 };
            config["addin"] = new JObject { ["workerTimeLimitMinS"] = 0.5, ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "a.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) } };
            config["designBasis"] = new JObject { ["mode"] = "made up new", ["modes"] = new JArray("made up old", "made up new") };
            config["projectRules"] = new JObject { ["manualName"] = "a made up manual", ["pastJobMode"] = "made up old", ["ruleIds"] = new JArray("M1") };
            string configFile = Path.Combine(root, "project.json");
            File.WriteAllText(configFile, config.ToString());
            return new SiteBuildPlace { ConfigFile = configFile, ReadFolder = read, WorkFolder = work, Document = new WorkerDocument { Path = Path.Combine(work, "a.rvt"), SessionId = "s1" } };
        }

        private string TwoPlots { get { return "{\"plots\":[{\"number\":\"1\",\"villaType\":\"T1\",\"outline\":" + Square(400000, 1000000) + "},{\"number\":\"2\",\"villaType\":\"T1\",\"outline\":" + Square(400010, 1000000) + "}]}"; } }

        [Fact]
        public void R686_the_scope_is_said_in_the_words_of_the_project()
        {
            Assert.Equal(SiteBuildText.ScopeProject, SiteBuildPlan.ScopeLine(null));
            Assert.Equal(SiteBuildText.ScopeProject, SiteBuildPlan.ScopeLine(new SettingsScope()));
            // Review of PR 234: siteBuild plans every plot, so a narrower scope says so
            Assert.Equal("Scope: the neighbourhood N1. The plan and its preview show every plot of stage 1.", SiteBuildPlan.ScopeLine(new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "N1" }));
            Assert.Equal("Scope: the plots 1, 2. The plan and its preview show every plot of stage 1.", SiteBuildPlan.ScopeLine(new SettingsScope { Kind = ScopeKinds.Plots, Plots = "1, 2" }));
            // A neighbourhood scope with no name is the whole project
            Assert.Equal(SiteBuildText.ScopeProject, SiteBuildPlan.ScopeLine(new SettingsScope { Kind = ScopeKinds.Nbh }));
        }

        [Fact]
        public void R686_the_coordinates_come_from_the_drawing_with_the_file_difference_in_mm_when_there_is_one()
        {
            Assert.Equal("Coordinates from the drawing: east 400005 m, north 1000010 m.", SiteBuildPlan.CoordinatesLine(new JObject { ["eastM"] = 400005.0, ["northM"] = 1000010.0, ["differenceMm"] = null }));
            Assert.Equal("Coordinates from the drawing: east 400005 m, north 1000010 m, 12.5 mm from the position file.", SiteBuildPlan.CoordinatesLine(new JObject { ["eastM"] = 400005.0, ["northM"] = 1000010.0, ["differenceMm"] = 12.5 }));
            Assert.Equal("No coordinates from the drawing: no outline", SiteBuildPlan.CoordinatesLine(new JObject { ["source"] = "missing", ["eastM"] = null, ["why"] = "no outline" }));
            Assert.EndsWith(" The position file did not read: made up fault", SiteBuildPlan.CoordinatesLine(new JObject { ["eastM"] = 1.0, ["northM"] = 2.0, ["positionFile"] = new JObject { ["error"] = "made up fault" } }));
        }

        // Review of PR 234: the position file picked in Set up site goes to siteBuild, so the difference in mm shows
        [Fact]
        public void R686_the_position_file_of_the_settings_or_of_config_goes_to_the_plan_and_its_difference_shows()
        {
            SiteBuildPlace place = Place(TwoPlots);
            Assert.Equal("picked.json", SiteBuildPlan.PositionFileOf("picked.json", place.ConfigFile));
            Assert.Null(SiteBuildPlan.PositionFileOf(null, place.ConfigFile));
            JObject config = JObject.Parse(File.ReadAllText(place.ConfigFile));
            ((JObject)config["addin"])["position"] = new JObject { ["file"] = "position/site.json" };
            File.WriteAllText(place.ConfigFile, config.ToString());
            Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(root), "position", "site.json")), SiteBuildPlan.PositionFileOf(null, place.ConfigFile));
            string file = Path.Combine(root, "position.json");
            File.WriteAllText(file, "{\"eastM\":400010.5,\"northM\":1000020.0}");
            place.PositionFile = file;
            var flow = new SiteBuildFlow { Place = () => place };
            Assert.True(flow.Run(false, out string line), line);
            Assert.Contains(" mm from the position file.", line);
        }

        [Fact]
        public void R686_a_press_makes_the_plan_asks_once_for_rules_shows_the_preview_and_says_the_coordinates()
        {
            SiteBuildPlace place = Place(TwoPlots);
            var asked = new List<string>();
            int previews = 0;
            SiteBuildResult shown = null;
            var flow = new SiteBuildFlow { Place = () => place, AskRules = text => { asked.Add(text); return false; }, ShowPreview = plan => { previews++; shown = plan; return true; } };
            Assert.True(flow.Run(true, out string line), line);
            Assert.Single(asked);
            Assert.EndsWith(" Use the manual template for this project now?", asked[0]);
            Assert.Equal(1, previews);
            Assert.Contains("Coordinates from the drawing: east ", line);
            Assert.EndsWith(SiteBuildText.PlanReady, line);
            Assert.Equal(2, shown.PlotsWithOutline);
            Assert.True(File.Exists(shown.Preview));
            Assert.Equal(2, SiteBuildPreview.Outlines(shown.Preview).Count);
            // Asked once only, and no preview with the tick off
            Assert.True(flow.Run(false, out line), line);
            Assert.Single(asked);
            Assert.Equal(1, previews);
            // Cancel of the preview stops with nothing written past the plan
            flow.ShowPreview = plan => false;
            Assert.False(flow.Run(true, out line));
            Assert.DoesNotContain(SiteBuildText.PlanReady, line);
            // Review of PR 234: rules are per project, so another project asks once too
            SiteBuildPlace other = Place(TwoPlots);
            other.ConfigFile = Path.Combine(root, "other.json");
            File.Copy(place.ConfigFile, other.ConfigFile);
            flow.Place = () => other;
            flow.ShowPreview = plan => true;
            Assert.True(flow.Run(false, out line), line);
            Assert.Equal(2, asked.Count);
        }

        [Fact]
        public void R686_a_yes_sets_the_rules_up_and_makes_the_plan_again()
        {
            SiteBuildPlace place = Place(TwoPlots);
            int setUps = 0;
            var flow = new SiteBuildFlow { Place = () => place, AskRules = text => true, UseManual = () => { setUps++; return null; } };
            Assert.True(flow.Run(false, out string line), line);
            Assert.Equal(1, setUps);
            // A setup that is refused says why among the issues and the build goes on
            SiteBuildResult shown = null;
            var refusing = new SiteBuildFlow { Place = () => place, AskRules = text => true, UseManual = () => "Made up refusal", ShowPreview = plan => { shown = plan; return true; } };
            Assert.True(refusing.Run(true, out line), line);
            Assert.Equal("Made up refusal", shown.Issues[0]);
        }

        [Fact]
        public void R686_only_a_blocking_line_stops_and_says_why_in_plain_words()
        {
            var flow = new SiteBuildFlow { Place = () => Place("{\"plots\":[]}") };
            Assert.False(flow.Run(false, out string line));
            Assert.StartsWith("Build stopped: ", line);
            Assert.DoesNotContain("{", line);
            Assert.DoesNotContain("null", line);
            // No read of stage 1, no plan
            var none = new SiteBuildFlow { Place = () => null };
            Assert.False(none.Run(false, out line));
            Assert.Equal(SiteBuildText.NoRead, line);
            var noRead = new SiteBuildFlow { Place = () => new SiteBuildPlace { ReadFolder = Path.Combine(root, "none"), WorkFolder = root } };
            Assert.False(noRead.Run(false, out line));
            Assert.Equal(TextTemplate.Fill(SiteBuildText.NotRun, SiteBuildText.NoRead), line);
        }

        [Fact]
        public void R687_the_preview_keeps_the_shape_inside_the_frame_with_north_up()
        {
            var rings = new List<List<double[]>>
            {
                new List<double[]> { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 }, new[] { 10.0, 20.0 } },
                new List<double[]> { new[] { 100.0, 50.0 }, new[] { 110.0, 50.0 }, new[] { 110.0, 70.0 } },
            };
            List<PointCollection> fitted = SiteBuildPreview.Fit(rings, 640, 400, 12);
            Assert.Equal(2, fitted.Count);
            Assert.All(fitted.SelectMany(ring => ring), point => Assert.InRange(point.X, 12 - 1e-6, 628 + 1e-6));
            Assert.All(fitted.SelectMany(ring => ring), point => Assert.InRange(point.Y, 12 - 1e-6, 388 + 1e-6));
            // North up: the point further north stands higher, a smaller y
            Assert.True(fitted[0][2].Y < fitted[0][1].Y);
            // One scale for both axes: 10 east and 20 north keep their ratio
            Assert.Equal(2.0, (fitted[0][1].Y - fitted[0][2].Y) / (fitted[0][1].X - fitted[0][0].X), 6);
            Assert.Empty(SiteBuildPreview.Fit(new List<List<double[]>>(), 640, 400, 12));
            Assert.Empty(SiteBuildPreview.Outlines(Path.Combine(root, "none.json")));
        }
    }
}
#endif
