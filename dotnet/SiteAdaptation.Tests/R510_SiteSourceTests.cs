using System;
using System.Collections.Generic;
using System.Globalization;
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
    // R510 to R514. Where the site position of button 1 comes from, on made up plots far from the site
    public sealed partial class SetUpSiteLogicTests
    {
        private const string NoCoordinatesLine = "This model has no site coordinates.";

        private static readonly List<CadPlot> MadePlots = new List<CadPlot>
        {
            new CadPlot { Plot = "1", Vertices = new List<double[]> { new[] { 300010.0, 2400020.0 }, new[] { 300090.0, 2400020.0 }, new[] { 300090.0, 2400060.0 } } },
        };

        private static CoordinateRead MadeRead()
        {
            return new CoordinateRead { Files = new List<StatedSystem> { new StatedSystem { File = "Road Layout.dwg", Name = "UTM84-37N" } }, Plots = MadePlots };
        }

        private BlankLimits MadeLimits()
        {
            return SiteSources.Limits(config);
        }

        private RunOutcome PressSource(FakeDocument document, FakeDialog dialog, Func<RunContext, CoordinateRead> read, ProjectSettings settings, List<string> calls, out SetUpRun made)
        {
            made = new SetUpRun(document, dialog, new FakeTags(calls), File.ReadAllText, Settings, null);
            if (read != null)
            {
                made.ReadingCad(read);
            }
            if (settings != null)
            {
                made.Recording(() => settings, new RecordingEntity(calls), () => new DateTime(2026, 11, 3, 20, 0, 0, DateTimeKind.Utc));
            }
            var flow = new ButtonFlow(new FakeState(work, configPath), new FakeModel(calls), null, input => new string('a', 64), () => RunId);
            return flow.Run(1, "1 Set up site", made.Body(), Document);
        }

        [Fact]
        public void R510_a_model_with_every_value_at_zero_has_no_site_coordinates_and_the_place_name_does_not_decide()
        {
            SiteState blank = Template();
            Assert.True(SiteSources.NoCoordinates(blank, MadeLimits()));
            blank.PlaceName = "Boston, MA";
            blank.LatitudeDeg = 42.3;
            blank.LongitudeDeg = -71.0;
            Assert.True(SiteSources.NoCoordinates(blank, MadeLimits()));
        }

        [Theory]
        [InlineData(1.0, 0.0, 0.0, 0.0)]
        [InlineData(0.0, 1.0, 0.0, 0.0)]
        [InlineData(0.0, 0.0, 1.0, 0.0)]
        [InlineData(0.0, 0.0, 0.0, 1.0)]
        public void R510_any_project_position_off_zero_holds_coordinates(double east, double north, double elevation, double angle)
        {
            SiteState state = Template();
            state.Position = new SitePosition { EastM = east, NorthM = north, ElevationM = elevation, AngleDeg = angle };
            Assert.False(SiteSources.NoCoordinates(state, MadeLimits()));
        }

        [Fact]
        public void R510_a_survey_or_base_point_off_the_origin_holds_coordinates_and_a_config_with_no_limit_says_nothing()
        {
            SiteState survey = Template();
            survey.Survey = new BasePointState { InternalM = new[] { 5.0, 0.0, 0.0 } };
            Assert.False(SiteSources.NoCoordinates(survey, MadeLimits()));
            SiteState shared = Template();
            shared.ProjectBase = new BasePointState { SharedM = new[] { 0.0, 5.0, 0.0 } };
            Assert.False(SiteSources.NoCoordinates(shared, MadeLimits()));
            Assert.False(SiteSources.NoCoordinates(Template(), null));
            JObject older = (JObject)config.DeepClone();
            ((JObject)older["coordinates"]).Remove("noCoordinatesLimitMm");
            Assert.Null(SiteSources.Limits(older));
        }

        [Fact]
        public void R510_the_window_says_the_plain_line_on_a_blank_model_and_nothing_on_one_with_coordinates()
        {
            SetUpSiteView blank = SetUpPlanner.View(Template(), Target(), null);
            Assert.Equal(NoCoordinatesLine, blank.NoCoordinatesLine);
            SiteState held = Template();
            held.Position = At;
            Assert.Null(SetUpPlanner.View(held, Target(), null).NoCoordinatesLine);
        }

        // The probe of 3 Nov 2026: a new project from the default metric template holds a place with its latitude and longitude and every position value at 0
        [Fact]
        public void R510_the_place_of_a_template_is_named_as_that_and_its_latitude_never_shows_as_the_site()
        {
            SiteState blank = Template();
            blank.PlaceName = "Madeup Town";
            blank.LatitudeDeg = 11.25;
            blank.LongitudeDeg = 33.75;
            SetUpSiteView view = SetUpPlanner.View(blank, Target(), null);

            Assert.Equal(NoCoordinatesLine, view.NoCoordinatesLine);
            Assert.Equal("The place Madeup Town comes from the Revit template, it is not the site.", view.TemplatePlaceLine);
            Assert.DoesNotContain(view.Now, row => row.Value.Contains("Madeup Town") || row.Value.Contains("11.25") || row.Value.Contains("33.75"));

            blank.Position = At;
            Assert.Null(SetUpPlanner.View(blank, Target(), null).TemplatePlaceLine);
            blank.Position = new SitePosition { EastM = 0, NorthM = 0, ElevationM = 0, AngleDeg = 0 };
            blank.PlaceName = null;
            Assert.Null(SetUpPlanner.View(blank, Target(), null).TemplatePlaceLine);
        }

        [Fact]
        public void R511_repo_config_keeps_the_position_file_and_gives_a_new_project_cad()
        {
            Assert.Equal(SiteSources.PositionFile, (string)config["coordinates"]["source"]);
            Assert.Equal(SiteSources.Cad, SiteSources.NewProject(config));
            JObject slip = (JObject)config.DeepClone();
            slip["coordinates"]["newProjectSource"] = "survey";
            Assert.Null(SiteSources.NewProject(slip));
            Assert.Null(SiteSources.NewProject(new JObject()));
        }

        [Theory]
        [InlineData(null, false, true, "positionFile")]
        [InlineData(null, true, false, "positionFile")]
        [InlineData(null, true, true, "cad")]
        [InlineData("positionFile", true, true, "positionFile")]
        [InlineData("cad", false, true, "cad")]
        [InlineData("typed", false, true, "typed")]
        [InlineData("typed", true, true, "cad")]
        public void R511_the_source_of_a_press(string held, bool blank, bool canRead, string expected)
        {
            Assert.Equal(expected, SiteSources.Starting(config, held, blank, canRead));
        }

        [Fact]
        public void R511_empty_project_takes_the_source_of_config_and_a_saved_project_keeps_none()
        {
            Func<string, string> read = path => config.ToString();
            var fresh = new ProjectSettings { ConfigFile = configPath };
            ProjectModes.TakeNewProject(null, fresh, read);
            Assert.Equal(SiteSources.Cad, fresh.CoordinateSource);

            var saved = new ProjectSettings { ConfigFile = configPath };
            ProjectModes.TakeNewProject(configPath, saved, read);
            Assert.Null(saved.CoordinateSource);

            ProjectSettings again = SettingsJson.Read(SettingsJson.Write(fresh));
            Assert.Equal(SiteSources.Cad, again.CoordinateSource);
            Assert.Throws<InvalidDataException>(() => SettingsJson.Read(SettingsJson.Write(new ProjectSettings { CoordinateSource = "survey" })));
            Assert.Throws<InvalidDataException>(() => SettingsJson.Read("{\"formatVersion\": 1, \"coordinateSource\": 5}"));
        }

        [Fact]
        public void R511_a_blank_model_under_the_position_file_takes_cad_when_the_pane_names_a_baseplan()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            RunOutcome outcome = PressSource(document, new FakeDialog(), context => MadeRead(), null, calls, out SetUpRun run);

            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            Assert.Equal(300050.0, document.State.Position.EastM);
            Assert.Equal(SiteSources.Cad, run.Target.PositionSource);
        }

        [Fact]
        public void R511_a_blank_model_under_the_position_file_with_no_baseplan_stays_on_the_file_and_says_so()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var dialog = new FakeDialog();
            RunOutcome outcome = PressSource(document, dialog, context => throw new NoBaseplanException(), null, calls, out SetUpRun run);

            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            Assert.Equal(At.EastM, document.State.Position.EastM);
            Assert.Equal(SiteSources.PositionFile, run.Target.PositionSource);
            Assert.Contains(dialog.Shown[0].WillSet, row => row.Label == SetUpTexts.PositionFrom && row.Value.Contains("position_test.json"));
        }

        [Fact]
        public void R511_a_model_with_coordinates_and_no_source_held_stays_on_the_file_with_no_cad_read()
        {
            SiteState held = Template();
            held.Position = At;
            var calls = new List<string>();
            var dialog = new FakeDialog { Answer = false };
            PressSource(new FakeDocument(held) { Calls = calls }, dialog, context => throw new InvalidOperationException("no read"), null, calls, out SetUpRun run);

            Assert.Equal(SiteSources.PositionFile, run.Target.PositionSource);
            Assert.Null(dialog.Shown[0].NoCadLine);
        }

        [Fact]
        public void R511_under_cad_a_missing_position_file_does_not_stop_and_levels_and_units_come_from_the_rules()
        {
            config["addin"]["position"]["file"] = Path.Combine(root, "no_such_position.json");
            File.WriteAllText(configPath, config.ToString());
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var dialog = new FakeDialog();
            RunOutcome outcome = PressSource(document, dialog, context => MadeRead(), new ProjectSettings { CoordinateSource = SiteSources.Cad }, calls, out SetUpRun run);

            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            JObject site = ProjectPositionFile.SiteRules(Rules(), null);
            Assert.Equal(site["levels"].Select(item => (string)item), run.Target.Levels.Select(level => level.Name));
            Assert.Equal(0.0, run.Target.Levels[0].ElevationM);
            Assert.Equal((double)site["levelStepsM"]["Level 2"], run.Target.Levels[1].ElevationM);
            Assert.Equal((double)site["levelStepsM"]["Level 2"], run.Target.Levels[1].ProjectElevationM);
            Assert.Equal((string)site["levelBase"], run.Target.LevelBase);
            Assert.Equal((string)site["units"]["length"], run.Target.Units.Length);
            Assert.Equal((double)site["lengthAccuracy"][(string)config["majority"]["siteReference"]["parsons"]], run.Target.Units.LengthAccuracy);
            Assert.Empty(dialog.Shown[0].SourceRows);
        }

        [Fact]
        public void R512_cad_and_a_readable_file_show_side_by_side_with_the_difference_in_mm()
        {
            CadSite cad = SiteFromCad.FromCad(MadePlots, CoordinateSettings.From(config), 32637);
            List<SourceRow> rows = SiteSources.Compare(cad, PositionFile(), SetUpDefaults.SiteUnits(config));

            Assert.Equal(new[] { "Easting", "Northing", "Elevation", "Angle", "Survey point", "File origin to the plots" }, rows.Select(row => row.Label));
            Assert.Equal("300050.000", rows[0].Cad);
            Assert.Equal("1000.000", rows[0].File);
            Assert.Equal("299050000 mm", rows[0].Difference);
            Assert.Equal("30 deg", rows[3].Difference);
            Assert.EndsWith(" mm", rows[4].Difference);
            double gap = Math.Sqrt(Math.Pow(300010.0 - 1000.0, 2) + Math.Pow(2400020.0 - 2000.0, 2)) * 1000.0;
            Assert.Equal(gap.ToString("0.##", CultureInfo.InvariantCulture) + " mm", rows[5].Difference);
        }

        [Fact]
        public void R512_a_file_origin_inside_the_plot_box_is_zero_off_the_plots()
        {
            JObject inside = PositionFile();
            inside["projectPosition"]["eastM"] = 300030.0;
            inside["projectPosition"]["northM"] = 2400030.0;
            CadSite cad = SiteFromCad.FromCad(MadePlots, CoordinateSettings.From(config), 32637);
            Assert.Equal("0 mm", SiteSources.Compare(cad, inside, SetUpDefaults.SiteUnits(config)).Last().Difference);
        }

        [Fact]
        public void R512_the_view_holds_the_rows_under_cad_only_and_no_difference_stops_the_run()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var dialog = new FakeDialog();
            RunOutcome outcome = PressSource(document, dialog, context => MadeRead(), new ProjectSettings { CoordinateSource = SiteSources.Cad }, calls, out SetUpRun run);

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Equal(6, dialog.Shown[0].SourceRows.Count);
            Assert.Empty(dialog.Shown[0].Stops);

            var other = new FakeDialog { Answer = false };
            PressSource(new FakeDocument(Template()) { Calls = calls }, other, null, null, calls, out SetUpRun file);
            Assert.Empty(other.Shown[0].SourceRows);
        }

        [Theory]
        [InlineData("no baseplan")]
        [InlineData("no plots")]
        [InlineData("outside")]
        [InlineData("conflict")]
        public void R513_no_usable_cad_shows_why_in_one_line_and_apply_stays_off(string how)
        {
            Func<RunContext, CoordinateRead> read = context =>
            {
                switch (how)
                {
                    case "no baseplan":
                        throw new NoBaseplanException();
                    case "no plots":
                        return new CoordinateRead();
                    case "outside":
                        return new CoordinateRead { Plots = new List<CadPlot> { new CadPlot { Plot = "1", Vertices = new List<double[]> { new[] { 10.0, 10.0 }, new[] { 20.0, 10.0 }, new[] { 20.0, 20.0 } } } } };
                    default:
                        return new CoordinateRead { Files = new List<StatedSystem> { new StatedSystem { File = "a.dwg", Name = "UTM84-37N" }, new StatedSystem { File = "b.xml", Name = "32638" } }, Plots = MadePlots };
                }
            };
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var dialog = new FakeDialog();
            RunOutcome outcome = PressSource(document, dialog, read, new ProjectSettings { CoordinateSource = SiteSources.Cad }, calls, out SetUpRun run);

            Assert.NotEqual(RunEnd.Committed, outcome.End);
            Assert.False(string.IsNullOrEmpty(dialog.Shown[0].NoCadLine));
            Assert.DoesNotContain(Environment.NewLine, dialog.Shown[0].NoCadLine);
            Assert.NotEmpty(dialog.Shown[0].Stops);
            Assert.Equal(0.0, document.State.Position.EastM);
            Assert.DoesNotContain("position", calls);
        }

        [Fact]
        public void R513_with_no_site_the_rows_say_pick_a_source_and_the_check_points_wait()
        {
            var dialog = new FakeDialog { Answer = false };
            PressSource(new FakeDocument(Template()), dialog, context => throw new NoBaseplanException(), new ProjectSettings { CoordinateSource = SiteSources.Cad }, new List<string>(), out SetUpRun run);

            SetUpSiteView view = dialog.Shown[0];
            Assert.Null(run.Target.PositionSource);
            Assert.Equal(SetUpTexts.NoSourceYet, view.WillSet.Single(row => row.Label == SetUpTexts.PositionFrom).Value);
            Assert.Equal(SetUpTexts.NoSourceYet, view.WillSet.Single(row => row.Label == SetUpTexts.SurveyPoint).Value);
            Assert.Empty(view.CheckPoints);
            Assert.False(SetUpPlanner.Changes(Template(), run.Target).Position);
        }

        [Fact]
        public void R513_picking_a_position_file_after_no_cad_sets_that_file_and_holds_the_choice_in_the_settings()
        {
            string picked = Path.Combine(root, "picked.json");
            File.WriteAllText(picked, PositionFile().ToString());
            var settings = new ProjectSettings { CoordinateSource = SiteSources.Cad };
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var dialog = new FakeDialog { Pick = new SiteSourcePick { Source = SiteSources.PositionFile, PositionFile = picked } };
            RunOutcome outcome = PressSource(document, dialog, context => throw new NoBaseplanException(), settings, calls, out SetUpRun run);

            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            Assert.Equal(At.EastM, document.State.Position.EastM);
            Assert.Equal(SiteSources.PositionFile, run.Target.PositionSource);
            Assert.Null(dialog.Shown[1].NoCadLine);
            Assert.Equal(SiteSources.PositionFile, settings.CoordinateSource);
            Assert.Equal(picked, settings.PositionFile);
        }

        [Fact]
        public void R513_typing_the_values_builds_the_site_like_r455_and_holds_only_the_source()
        {
            var settings = new ProjectSettings { CoordinateSource = SiteSources.Cad };
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var dialog = new FakeDialog { Pick = new SiteSourcePick { Source = SiteSources.Typed, EastM = 300100.5, NorthM = 2400200.25, AngleDeg = 2.5 } };
            RunOutcome outcome = PressSource(document, dialog, context => throw new NoBaseplanException(), settings, calls, out SetUpRun run);

            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            Assert.Equal(300100.5, document.State.Position.EastM);
            Assert.Equal(2400200.25, document.State.Position.NorthM);
            Assert.Equal(2.5, document.State.Position.AngleDeg);
            Assert.Equal((string)config["coordinates"]["positionName"], document.State.PositionName);
            Assert.Equal(SiteSources.Typed, run.Target.PositionSource);
            Assert.Equal(SiteSources.Typed, settings.CoordinateSource);
            // R367. The settings JSON refuses a number with decimals, so the typed values stay in the model and never in the settings
            Assert.DoesNotContain("300100", SettingsJson.Write(settings));
        }

        [Fact]
        public void R513_a_second_press_of_typed_reads_the_values_from_the_model_and_nothing_differs()
        {
            SiteState placed = Template();
            CadSite typed = SiteSources.Typing(300100.5, 2400200.25, 2.5, CoordinateSettings.From(config));
            placed.Position = typed.Position;
            placed.Survey = new BasePointState { InternalM = typed.SurveyInternal, SharedM = typed.SurveyShared };
            placed.ProjectBase = new BasePointState { InternalM = typed.BaseInternal, SharedM = typed.BaseShared };
            var dialog = new FakeDialog { Answer = false };
            PressSource(new FakeDocument(placed), dialog, null, new ProjectSettings { CoordinateSource = SiteSources.Typed }, new List<string>(), out SetUpRun run);

            Assert.Equal(SiteSources.Typed, run.Target.PositionSource);
            Assert.False(SetUpPlanner.Changes(placed, run.Target).Position);
        }

        [Theory]
        [InlineData(10.0, 2400200.0, 0.0)]
        [InlineData(300100.0, -5.0, 0.0)]
        [InlineData(double.NaN, 2400200.0, 0.0)]
        [InlineData(300100.0, 2400200.0, double.PositiveInfinity)]
        public void R513_typed_values_outside_the_utm_range_or_not_finite_are_refused(double east, double north, double angle)
        {
            Assert.Throws<InvalidDataException>(() => SiteSources.Typing(east, north, angle, CoordinateSettings.From(config)));
        }

        [Fact]
        public void R513_a_model_with_coordinates_can_move_from_the_file_to_cad_in_the_window()
        {
            SiteState held = Template();
            held.Position = At;
            var settings = new ProjectSettings();
            var calls = new List<string>();
            var document = new FakeDocument(held) { Calls = calls };
            var dialog = new FakeDialog { Pick = new SiteSourcePick { Source = SiteSources.Cad } };
            RunOutcome outcome = PressSource(document, dialog, context => MadeRead(), settings, calls, out SetUpRun run);

            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            Assert.Equal(SiteSources.PositionFile, dialog.Shown[0].PositionSource);
            Assert.Equal(SiteSources.Cad, dialog.Shown[1].PositionSource);
            Assert.False(dialog.Shown[1].DryRunDone);
            Assert.Equal(300050.0, document.State.Position.EastM);
            Assert.Equal(SiteSources.Cad, settings.CoordinateSource);
        }

        [Fact]
        public void R513_a_bad_file_picked_leaves_the_choice_as_it_was()
        {
            string bad = Path.Combine(root, "bad.json");
            File.WriteAllText(bad, "{\"format\": \"other\"}");
            var dialog = new FakeDialog { Pick = new SiteSourcePick { Source = SiteSources.PositionFile, PositionFile = bad } };
            RunOutcome outcome = PressSource(new FakeDocument(Template()), dialog, context => MadeRead(), new ProjectSettings { CoordinateSource = SiteSources.Cad }, new List<string>(), out SetUpRun run);

            Assert.NotEqual(RunEnd.Committed, outcome.End);
            Assert.Contains("site-adaptation", outcome.Message);
            Assert.Equal(SiteSources.Cad, run.Target.PositionSource);
        }

        [Fact]
        public void R514_site_json_names_the_source_and_a_press_with_another_source_writes_it_again()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            RunOutcome outcome = PressSource(document, new FakeDialog(), context => MadeRead(), new ProjectSettings { CoordinateSource = SiteSources.Cad }, calls, out SetUpRun run);

            Assert.True(outcome.End == RunEnd.Committed, outcome.Message);
            string path = SitePath();
            Assert.Equal(SiteSources.Cad, (string)JObject.Parse(File.ReadAllText(path))[SetUpRun.PositionSourceKey]);
            SiteState now = document.Read();
            Assert.True(SetUpRun.SiteFileAgrees(path, now));
            Assert.True(SetUpRun.SiteFileAgrees(path, now, SiteSources.Cad));
            Assert.False(SetUpRun.SiteFileAgrees(path, now, SiteSources.Typed));
        }

        [Fact]
        public void R514_the_row_position_from_names_each_source()
        {
            SetUpSiteView file = SetUpPlanner.View(Template(), Target(), null);
            Assert.Equal(SiteSources.PositionFile, file.PositionSource);
            Assert.Equal(SetUpTexts.PositionFrom, file.WillSet[0].Label);
            Assert.StartsWith("Position file", file.WillSet[0].Value);
            Assert.False(file.WillSet[0].Differs);

            SetUpTarget typed = SetUpTarget.From(config, Rules(), null, null, 0.5, ParameterSheet.FromConfig(configPath, config), SiteSources.Typing(300100.0, 2400200.0, 0.0, CoordinateSettings.From(config)));
            Assert.Equal(SetUpTexts.FromTyped, SetUpPlanner.View(Template(), typed, null).WillSet[0].Value);

            SetUpTarget cad = SetUpTarget.From(config, Rules(), null, null, 0.5, ParameterSheet.FromConfig(configPath, config), SiteFromCad.FromCad(MadePlots, CoordinateSettings.From(config), 32637));
            Assert.Equal(SetUpTexts.FromCad, SetUpPlanner.View(Template(), cad, null).WillSet[0].Value);
        }
    }
}
