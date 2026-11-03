using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Review 1 and breaker 1 of COORD-3NOV-A. No source is ever taken with no click, a source that fails opens the window and asks, a source is held only once committed
    public sealed partial class SetUpSiteLogicTests
    {
        private static SiteState Holding()
        {
            SiteState state = Template();
            state.Position = At;
            return state;
        }

        [Fact]
        public void R513_a_blank_model_with_no_baseplan_under_the_position_file_asks_and_never_stays_on_the_file_unseen()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var dialog = new FakeDialog();
            RunOutcome outcome = PressSource(document, dialog, context => throw new NoBaseplanException(), null, calls, out SetUpRun run);

            Assert.NotEqual(RunEnd.Committed, outcome.End);
            Assert.Null(run.Target.PositionSource);
            Assert.NotEmpty(dialog.Shown[0].Stops);
            Assert.Equal(0.0, document.State.Position.EastM);
        }

        [Fact]
        public void R513_a_held_file_that_is_gone_opens_the_window_and_asks()
        {
            var settings = new ProjectSettings { CoordinateSource = SiteSources.PositionFile, PositionFile = Path.Combine(root, "gone.json") };
            var dialog = new FakeDialog { Answer = false };
            PressSource(new FakeDocument(Holding()), dialog, context => MadeRead(), settings, new List<string>(), out SetUpRun run);

            Assert.Single(dialog.Shown);
            Assert.Contains(dialog.Shown[0].Stops, stop => stop.Contains("gone.json"));
            Assert.Null(run.Target.PositionSource);
        }

        [Fact]
        public void R513_the_default_file_missing_on_a_new_pc_opens_the_window_and_asks()
        {
            config["addin"]["position"]["file"] = Path.Combine(root, "no_such_position.json");
            File.WriteAllText(configPath, config.ToString());
            var dialog = new FakeDialog { Answer = false };
            PressSource(new FakeDocument(Holding()), dialog, context => MadeRead(), null, new List<string>(), out SetUpRun run);

            Assert.Single(dialog.Shown);
            Assert.Contains(dialog.Shown[0].Stops, stop => stop.Contains("no_such_position.json"));
        }

        [Fact]
        public void R513_a_position_file_that_reads_as_another_format_opens_the_window_and_asks()
        {
            string other = Path.Combine(root, "other.json");
            File.WriteAllText(other, "{\"format\": \"other\"}");
            var settings = new ProjectSettings { CoordinateSource = SiteSources.PositionFile, PositionFile = other };
            var dialog = new FakeDialog { Answer = false };
            PressSource(new FakeDocument(Holding()), dialog, context => MadeRead(), settings, new List<string>(), out SetUpRun run);

            Assert.Single(dialog.Shown);
            Assert.Null(run.Target.PositionSource);
        }

        [Fact]
        public void R513_held_typed_on_a_model_outside_utm_opens_the_window_and_asks()
        {
            SiteState state = Template();
            state.Position = new SitePosition { EastM = 10.0, NorthM = 10.0, ElevationM = 0.0, AngleDeg = 0.0 };
            var dialog = new FakeDialog { Answer = false };
            PressSource(new FakeDocument(state), dialog, null, new ProjectSettings { CoordinateSource = SiteSources.Typed }, new List<string>(), out SetUpRun run);

            Assert.Single(dialog.Shown);
            Assert.Null(run.Target.PositionSource);
            Assert.NotEmpty(dialog.Shown[0].Stops);
        }

        [Fact]
        public void R511_a_slip_in_config_stays_a_failure_of_the_run_and_asks_nothing()
        {
            config["coordinates"]["source"] = "survey";
            File.WriteAllText(configPath, config.ToString());
            var dialog = new FakeDialog();
            RunOutcome outcome = PressSource(new FakeDocument(Holding()), dialog, context => MadeRead(), null, new List<string>(), out SetUpRun run);

            Assert.NotEqual(RunEnd.Committed, outcome.End);
            Assert.Empty(dialog.Shown);
        }

        [Fact]
        public void R511_a_failed_apply_leaves_no_source_in_the_settings_and_a_commit_holds_it_in_the_entity_and_in_memory()
        {
            var settings = new ProjectSettings();
            var calls = new List<string>();
            var failing = new FakeDocument(Template()) { Calls = calls, SkewLevelsMm = 0.6 };
            RunOutcome failed = PressSource(failing, new FakeDialog(), context => MadeRead(), settings, calls, out SetUpRun first);
            Assert.NotEqual(RunEnd.Committed, failed.End);
            Assert.Null(settings.CoordinateSource);

            var entity = new RecordingEntity(calls);
            RunOutcome outcome = PressSource(new FakeDocument(Template()) { Calls = calls }, new FakeDialog(), context => MadeRead(), settings, calls, out SetUpRun second, entity);
            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            Assert.Equal(SiteSources.Cad, settings.CoordinateSource);
            Assert.Equal(SiteSources.Cad, SettingsJson.Read(entity.Json).CoordinateSource);
        }

        [Fact]
        public void R511_a_saved_past_job_on_a_model_with_coordinates_keeps_both_source_fields_null()
        {
            var settings = new ProjectSettings();
            var calls = new List<string>();
            var entity = new RecordingEntity(calls);
            RunOutcome outcome = PressSource(new FakeDocument(Holding()) { Calls = calls }, new FakeDialog(), context => throw new InvalidOperationException("no read"), settings, calls, out SetUpRun run, entity);

            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            Assert.Null(settings.CoordinateSource);
            Assert.Null(settings.PositionFile);
            Assert.Null(SettingsJson.Read(entity.Json).CoordinateSource);
            Assert.Null(SettingsJson.Read(entity.Json).PositionFile);
        }

        [Fact]
        public void R513_use_cad_reads_again_under_a_run_id_of_its_own_as_the_launch_refuses_one_it_has_a_log_of()
        {
            string layers = Path.Combine(root, "config", (string)config["addin"]["readData"]["cadLayers"]);
            File.WriteAllText(layers, "{}");
            string baseplan = Path.Combine(root, "b.dwg");
            File.WriteAllText(baseplan, "made up");
            var inputs = new List<RunInput> { new RunInput("Baseplan", baseplan, false) };
            var roles = new Dictionary<string, string> { ["Baseplan"] = CoordinateRequest.BaseplanRole };
            var ids = new List<string>();
            Func<WorkerRequest, string, WorkerAnswer> worker = (request, folder) =>
            {
                Assert.False(File.Exists(WorkerFiles.LogPath(folder, request.RunId)), request.RunId);
                Directory.CreateDirectory(Path.GetDirectoryName(WorkerFiles.LogPath(folder, request.RunId)));
                File.WriteAllText(WorkerFiles.LogPath(folder, request.RunId), "made up");
                ids.Add(request.RunId);
                Directory.CreateDirectory(request.OutputFolder);
                File.WriteAllText(Path.Combine(request.OutputFolder, CoordinateRequest.OutputFile),
                    new JObject { ["files"] = new JArray(), ["plots"] = new JArray(new JObject { ["plot"] = "1", ["vertices"] = new JArray(new JArray(300010.0, 2400020.0), new JArray(300090.0, 2400020.0), new JArray(300090.0, 2400060.0)) }) }.ToString());
                return new WorkerAnswer { Status = WorkerExit.Status(WorkerExit.Done) };
            };
            var dialog = new FakeDialog { Pick = new SiteSourcePick { Source = SiteSources.Cad } };
            RunOutcome outcome = PressSource(new FakeDocument(Template()), dialog, context => CoordinateRequest.Run(context, inputs, roles, Settings, new WorkerDocument(), worker), new ProjectSettings { CoordinateSource = SiteSources.Cad }, new List<string>(), out SetUpRun run);

            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            Assert.True(ids.Count >= 2);
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Equal(SiteSources.Cad, run.Target.PositionSource);
        }

        // A typed origin is checked by the box of the plots alone, the check rows hold the site to itself
        private SetUpSiteView TypedView(double east, double north, Func<RunContext, CoordinateRead> read)
        {
            var dialog = new FakeDialog { Answer = false, Pick = new SiteSourcePick { Source = SiteSources.Typed, EastM = east, NorthM = north, AngleDeg = 0.0 } };
            PressSource(new FakeDocument(Template()), dialog, read, new ProjectSettings { CoordinateSource = SiteSources.PositionFile }, new List<string>(), out SetUpRun run);
            return dialog.Shown[1];
        }

        [Fact]
        public void R513_a_typed_origin_inside_the_box_shows_its_distance_and_stops_nothing()
        {
            SetUpSiteView view = TypedView(300050.0, 2400040.0, context => MadeRead());
            Assert.Equal("0 mm, inside the box", view.WillSet.Single(row => row.Label == SetUpTexts.TypedToPlots).Value);
            Assert.Empty(view.Stops);
        }

        [Fact]
        public void R513_a_typed_origin_off_the_box_under_the_limit_warns_and_over_it_stops()
        {
            SetUpSiteView near = TypedView(300190.0, 2400040.0, context => MadeRead());
            Assert.Equal("100000 mm, outside the box", near.WillSet.Single(row => row.Label == SetUpTexts.TypedToPlots).Value);
            Assert.Contains("The typed origin lies 100 m off the box of the plots. Read the values again.", near.Waiting);
            Assert.Empty(near.Stops);

            double limit = (double)config["coordinates"]["typedOffBoxLimitM"];
            SetUpSiteView far = TypedView(300090.0 + limit + 1.0, 2400040.0, context => MadeRead());
            Assert.Contains(far.Stops, stop => stop.Contains("coordinates.typedOffBoxLimitM"));
            Assert.DoesNotContain(far.Waiting, line => line.Contains("off the box"));
        }

        [Theory]
        [InlineData("no reader")]
        [InlineData("no baseplan")]
        [InlineData("a stop")]
        public void R513_typed_values_with_no_plots_to_hold_them_to_say_so_in_one_line(string how)
        {
            Func<RunContext, CoordinateRead> read = how == "no reader" ? (Func<RunContext, CoordinateRead>)null : how == "no baseplan" ? context => throw new NoBaseplanException()
                : (Func<RunContext, CoordinateRead>)(context => new CoordinateRead { Files = new List<StatedSystem> { new StatedSystem { File = "a.dwg", Name = "UTM84-37N" }, new StatedSystem { File = "b.xml", Name = "32638" } }, Plots = MadePlots });
            SetUpSiteView view = TypedView(300050.0, 2400040.0, read);
            Assert.Equal("No plots read. The typed values have no check, read them again.", view.WillSet.Single(row => row.Label == SetUpTexts.TypedToPlots).Value);
            Assert.Empty(view.Stops);
        }

        [Fact]
        public void R513_a_typed_angle_other_than_config_is_refused_by_a_line_that_names_it()
        {
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => SiteSources.Typing(300100.0, 2400200.0, 2.5, CoordinateSettings.From(config)));
            Assert.Contains("2.5", refused.Message);
            Assert.Contains("R83", refused.Message);
        }

        private SetUpTarget CadOver()
        {
            return SetUpTarget.From(config, Rules(), null, null, 0.5, ParameterSheet.FromConfig(configPath, config), SiteFromCad.FromCad(MadePlots, CoordinateSettings.From(config), 32637));
        }

        [Theory]
        [InlineData(13, 0, 0, "13 placed links, 0 masses and 0 floors")]
        [InlineData(0, 4, 0, "0 placed links, 4 masses and 0 floors")]
        [InlineData(0, 0, 336, "0 placed links, 0 masses and 336 floors")]
        public void R523_a_move_of_a_model_with_coordinates_and_placed_content_stops_and_names_the_counts(int links, int masses, int floors, string said)
        {
            SiteState state = Holding();
            state.PlacedLinks = links;
            state.Masses = masses;
            state.FloorsByType["Generic 150mm"] = floors;
            SetUpSiteView view = SetUpPlanner.View(state, CadOver(), null);
            Assert.Contains(view.Stops, stop => stop.Contains(said));
        }

        [Fact]
        public void R523_nothing_stops_with_nothing_placed_on_a_blank_model_or_when_nothing_moves()
        {
            string[] counted = { "placed links" };
            Assert.DoesNotContain(SetUpPlanner.View(Holding(), CadOver(), null).Stops, stop => counted.Any(stop.Contains));

            SiteState blank = Template();
            blank.PlacedLinks = 13;
            Assert.DoesNotContain(SetUpPlanner.View(blank, CadOver(), null).Stops, stop => counted.Any(stop.Contains));

            SiteState other = Holding();
            other.FloorsByType["A floor of no tool type"] = 40;
            Assert.DoesNotContain(SetUpPlanner.View(other, CadOver(), null).Stops, stop => counted.Any(stop.Contains));

            SetUpTarget same = CadOver();
            SiteState there = Template();
            there.Position = same.Position;
            there.Survey = new BasePointState { InternalM = same.SurveyInternal, SharedM = same.SurveyShared };
            there.ProjectBase = new BasePointState { InternalM = same.BaseInternal, SharedM = same.BaseShared };
            there.PlacedLinks = 13;
            Assert.DoesNotContain(SetUpPlanner.View(there, same, null).Stops, stop => counted.Any(stop.Contains));
        }

        [Fact]
        public void R523_a_move_with_placed_content_never_reaches_the_model()
        {
            SiteState state = Holding();
            state.PlacedLinks = 13;
            var calls = new List<string>();
            var document = new FakeDocument(state) { Calls = calls };
            var dialog = new FakeDialog { Pick = new SiteSourcePick { Source = SiteSources.Cad } };
            RunOutcome outcome = PressSource(document, dialog, context => MadeRead(), new ProjectSettings(), calls, out SetUpRun run);

            Assert.NotEqual(RunEnd.Committed, outcome.End);
            Assert.Contains("13 placed links", outcome.Message);
            Assert.Equal(At.EastM, document.State.Position.EastM);
        }

        [Fact]
        public void R512_the_survey_point_height_has_a_row_of_its_own_so_0_mm_of_elevation_never_reads_as_one_frame()
        {
            JObject file = PositionFile();
            file["projectPosition"]["elevationM"] = 0.0;
            CadSite cad = SiteFromCad.FromCad(MadePlots, CoordinateSettings.From(config), 32637);
            cad.SurveyShared = new[] { cad.SurveyShared[0], cad.SurveyShared[1], 0.0 };
            file["surveyPoint"]["sharedM"] = new JArray(cad.SurveyShared[0], cad.SurveyShared[1], 12.5);
            List<SourceRow> rows = SiteSources.Compare(cad, file, SetUpDefaults.SiteUnits(config));

            Assert.Equal("0 mm", rows.Single(row => row.Label == SetUpTexts.Elevation).Difference);
            Assert.Equal("0 mm", rows.Single(row => row.Label == SetUpTexts.SurveyPoint).Difference);
            Assert.Equal(2, rows.Single(row => row.Label == SetUpTexts.SurveyPoint).Cad.Split(',').Length);
            Assert.Equal("12500 mm", rows.Single(row => row.Label == SetUpTexts.SurveyHeight).Difference);
        }

        [Fact]
        public void R512_a_check_file_that_does_not_read_says_why_the_check_did_not_run()
        {
            string other = Path.Combine(root, "other.json");
            File.WriteAllText(other, "{\"format\": \"other\"}");
            var dialog = new FakeDialog { Answer = false };
            PressSource(new FakeDocument(Template()), dialog, context => MadeRead(), new ProjectSettings { CoordinateSource = SiteSources.Cad, PositionFile = other }, new List<string>(), out SetUpRun run);

            Assert.StartsWith("The check against the position file did not run: ", dialog.Shown[0].CheckLine);
            Assert.Contains("site-adaptation", dialog.Shown[0].CheckLine);
            Assert.Empty(dialog.Shown[0].SourceRows);
        }

        [Fact]
        public void R510_a_blank_model_under_the_position_file_says_the_place_of_the_template_stays()
        {
            SiteState blank = Template();
            blank.PlaceName = "Madeup Town";
            Assert.Contains("Madeup Town stays", SetUpPlanner.View(blank, Target(), null).WillSet.Single(row => row.Label == SetUpTexts.Place).Value);
            Assert.DoesNotContain(SetUpPlanner.View(blank, CadOver(), null).WillSet, row => row.Label == SetUpTexts.Place);
            blank.Position = At;
            Assert.DoesNotContain(SetUpPlanner.View(blank, Target(), null).WillSet, row => row.Label == SetUpTexts.Place);
        }

        // Written so the second press has nothing to set but the source
        [Fact]
        public void R514_a_press_that_changes_only_the_source_writes_site_json_and_the_settings()
        {
            var settings = new ProjectSettings { CoordinateSource = SiteSources.Cad };
            var calls = new List<string>();
            var entity = new RecordingEntity(calls);
            var document = new FakeDocument(Template()) { Calls = calls };
            Assert.Equal(RunEnd.Committed, PressSource(document, new FakeDialog(), context => MadeRead(), settings, calls, out SetUpRun first, entity).End);
            Assert.Equal(SiteSources.Cad, (string)JObject.Parse(File.ReadAllText(SitePath()))[SetUpRun.PositionSourceKey]);

            var dialog = new FakeDialog { Pick = new SiteSourcePick { Source = SiteSources.Typed, EastM = 300050.0, NorthM = 2400040.0, AngleDeg = 0.0 } };
            RunOutcome moved = PressSource(document, dialog, context => MadeRead(), settings, calls, out SetUpRun second, entity);

            Assert.True(moved.End == RunEnd.Committed, moved.Message);
            Assert.True(dialog.Shown[1].NothingDiffers);
            Assert.Equal(SiteSources.Typed, (string)JObject.Parse(File.ReadAllText(SitePath()))[SetUpRun.PositionSourceKey]);
            Assert.Equal(SiteSources.Typed, settings.CoordinateSource);
            Assert.Equal(SiteSources.Typed, SettingsJson.Read(entity.Json).CoordinateSource);
        }
    }
}
