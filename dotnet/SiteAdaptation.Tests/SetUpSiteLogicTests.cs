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
    // R373, R374, R383. Button 1 Set up site in Common with a fake document, on net48 and net8.0. The config and the rules profile are
    // the repo's own, the position file is made here of numbers of no real site, so no real coordinate or level is in the tests
    public sealed class SetUpSiteLogicTests : IDisposable
    {
        private const string Settings = "Settings";
        private const string RunId = "1_20261029-140000-000";
        private const string Document = @"C:\Models\Site.rvt";
        private static readonly SitePosition At = new SitePosition { EastM = 1000.0, NorthM = 2000.0, ElevationM = 10.0, AngleDeg = 30.0 };
        private static readonly double[] SurveyInternal = { -12.0, 7.5, -10.0 };
        private static readonly double[] BaseInternal = { 0.0, 0.0, 0.0 };
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa-setup-" + Guid.NewGuid().ToString("N"));
        private readonly string work;
        private readonly string configPath;
        private readonly JObject config;

        public SetUpSiteLogicTests()
        {
            work = Path.Combine(root, "work");
            Directory.CreateDirectory(work);
            Directory.CreateDirectory(Path.Combine(root, "config"));
            string repo = ThemeGateTests.RepoRoot();
            string positionPath = Path.Combine(root, "position_test.json");
            File.WriteAllText(positionPath, PositionFile().ToString());
            config = JObject.Parse(File.ReadAllText(Path.Combine(repo, "config", "project.json")));
            config["profile"] = Path.Combine(repo, "rules", "roshn-makkah.json");
            config["addin"]["position"]["file"] = positionPath;
            config["addin"]["parameterSheet"] = Path.Combine(repo, "config", "shared_parameters.xlsx");
            configPath = Path.Combine(root, "config", "project.json");
            File.WriteAllText(configPath, config.ToString());
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private static JObject Rules()
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "rules", "roshn-makkah.json")));
        }

        // The written shape of ProjectPositionFile, the levels on the survey point 4 m apart
        private static JObject PositionFile()
        {
            double[] survey = SiteCoordinates.ToShared(At, SurveyInternal);
            double[] basePoint = SiteCoordinates.ToShared(At, BaseInternal);
            return new JObject
            {
                ["format"] = ProjectPositionFile.Format, ["formatVersion"] = ProjectPositionFile.FormatVersion, ["key"] = "test", ["model"] = "test.rvt",
                ["projectPosition"] = new JObject { ["eastM"] = At.EastM, ["northM"] = At.NorthM, ["elevationM"] = At.ElevationM, ["angleDeg"] = At.AngleDeg },
                ["angleToTrueNorthDeg"] = At.AngleDeg,
                ["surveyPoint"] = new JObject { ["internalM"] = new JArray(SurveyInternal), ["sharedM"] = new JArray(survey) },
                ["projectBasePoint"] = new JObject { ["internalM"] = new JArray(BaseInternal), ["sharedM"] = new JArray(basePoint) },
                ["levels"] = new JArray(
                    new JObject { ["name"] = "Level 1", ["elevationM"] = 20.0, ["projectElevationM"] = 10.0 },
                    new JObject { ["name"] = "Level 2", ["elevationM"] = 24.0, ["projectElevationM"] = 14.0 }),
                ["levelBase"] = "Survey Point",
                ["units"] = new JObject { ["system"] = "METRIC", ["length"] = "meters", ["lengthSystem"] = "METRIC" },
                ["lengthAccuracy"] = 0.01, ["activeSite"] = "Internal", ["sites"] = new JArray("Internal"), ["placeName"] = "test",
            };
        }

        private SetUpTarget Target(double slope = 0.5, ParameterSheet sheet = null)
        {
            return SetUpTarget.From(config, Rules(), null, PositionFile(), slope, sheet ?? ParameterSheet.FromConfig(configPath, config));
        }

        // A parameter sheet of the eight columns of R461, one row per parameter of name, GUID, group, categories, Filled by and Keep
        private static List<object[]> Sheet(params string[][] rows)
        {
            var found = new List<object[]> { ParameterSheet.Columns.Cast<object>().ToArray() };
            found.AddRange(rows.Select(row => new object[] { row[0], row[1], "Text", row[2], row[3], row[4], "x", row[5] }));
            return found;
        }

        // A project from the default metric template as the probe of 5b read it: millimeters, two levels on the project base point at 0,
        // floor types and basic wall types of its own, the survey point and the project base point at the internal origin
        private static SiteState Template()
        {
            return new SiteState
            {
                Units = new UnitState { System = "METRIC", Length = "millimeters", LengthAccuracy = 1, Area = "squareMeters", Angle = "degrees", Slope = "slopeDegrees" },
                Survey = new BasePointState { Clipped = true },
                Levels = { new LevelState { Name = "Level 1", ElevationM = 0, ProjectElevationM = 0, Base = LevelBases.ProjectBasePoint }, new LevelState { Name = "Level 2", ElevationM = 4, ProjectElevationM = 4, Base = LevelBases.ProjectBasePoint } },
                FloorTypes = { "Generic Floor - 400mm", "Generic 150mm" },
                WallTypes = { "Generic - 150mm" },
            };
        }

        [Fact]
        public void R373_the_badge_says_empty_project_found_with_no_sign_on_a_template_that_holds_a_floor_type_and_the_levels_of_config()
        {
            SetUpSiteView view = SetUpPlanner.View(Template(), Target(), null);

            Assert.False(view.ExistingSite);
            Assert.Empty(view.Signs);
            Assert.Empty(view.Stops);
            Assert.False(view.DryRunDone);
        }

        [Fact]
        public void R373_each_sign_of_an_existing_site_file_is_named_and_a_point_within_half_a_mm_of_the_origin_is_none()
        {
            SetUpTarget target = Target();
            ParameterSpec prototype = target.Parameters.Parameters.First(item => item.Name == "RRE_ZZ_SP_Prototype");
            SiteState near = Template();
            near.Survey.InternalM = new[] { 0.0004, 0, 0 };
            near.Position = new SitePosition { EastM = 0.0004, NorthM = 0, ElevationM = 0, AngleDeg = 0 };
            Assert.Empty(SetUpPlanner.Signs(near, target));

            SiteState site = Template();
            site.SettingsEntity = true;
            site.Position = new SitePosition { EastM = 1, NorthM = 0, ElevationM = 0, AngleDeg = 0 };
            site.Survey.InternalM = new[] { 0.0, 0.0006, 0 };
            site.ProjectBase.InternalM = new[] { 0.0, 0, -0.0006 };
            site.SharedParameters.Add(new ParameterState { Name = "a name of the client", Guid = prototype.Guid, Categories = { "RVT Links" } });
            site.SharedParameters.Add(new ParameterState { Name = "CASE", Guid = Guid.NewGuid().ToString("D"), Categories = { "RVT Links" } });
            site.SharedParameters.Add(new ParameterState { Name = "Plot", Guid = Guid.NewGuid().ToString("D"), Categories = { "Floors" } });
            site.SharedParameters.Add(new ParameterState { Name = "not bound", Guid = target.Parameters.Parameters[0].Guid });

            List<string> signs = SetUpPlanner.Signs(site, target);

            Assert.Equal(new[]
            {
                SetUpTexts.SignSettings, "shared parameters of the tool: CASE, Plot, a name of the client", SetUpTexts.SignPosition, SetUpTexts.SignSurvey, SetUpTexts.SignBase,
            }, signs);
            Assert.True(SetUpPlanner.View(site, target, null).ExistingSite);
        }

        [Fact]
        public void R373_a_parameter_of_a_sheet_name_with_another_guid_stops_with_the_name_the_guid_found_and_the_guid_of_the_sheet()
        {
            SetUpTarget target = Target();
            ParameterSpec number = target.Parameters.Parameters.First(item => item.Name == "RRE_ZZ_SP_Plot-Number");
            string other = "11111111-2222-3333-4444-555555555555";
            SiteState site = Template();
            site.SharedParameters.Add(new ParameterState { Name = number.Name, Guid = other, Categories = { "RVT Links" } });

            SetUpSiteView view = SetUpPlanner.View(site, target, null);

            Assert.Equal(new[] { "RRE_ZZ_SP_Plot-Number: GUID found " + other + ", GUID of the sheet " + number.Guid }, view.Stops);
            Assert.True(view.ExistingSite);
        }

        [Fact]
        public void R373_a_parameter_of_the_sheet_with_no_guid_stops_and_plot_with_no_guid_waits_on_q153_and_is_no_stop()
        {
            List<object[]> rows = Sheet(new[] { "Plot", null, "Data", "Floors", "5", "Yes" }, new[] { "RRE_ZZ_SP_Prototype", null, "Data", "RVT Links", "7", "Yes" });
            SetUpTarget target = Target(sheet: ParameterSheet.FromRows("parameters.xlsx", rows));

            SetUpSiteView view = SetUpPlanner.View(Template(), target, null);

            Assert.Equal(new[] { "RRE_ZZ_SP_Prototype has no GUID in the parameter sheet" }, view.Stops);
            Assert.Equal(new[] { "Plot not bound, waiting on Q156" }, view.Waiting);
            List<object[]> noPlot = Sheet(new[] { "CASE", "c0b2c4ee-e129-4a42-bcb6-225759b874eb", "Data", "RVT Links", "7", "Yes" });
            Assert.Equal(new[] { "Plot not bound, waiting on Q156" }, SetUpPlanner.View(Template(), Target(sheet: ParameterSheet.FromRows("p.xlsx", noPlot)), null).Waiting);
        }

        // R461. config/shared_parameters.xlsx of the repo, the one source: the 10 parameters of the villa links of the 7 keys and Plot of
        // Q156, each with its GUID, TEXT, the group Data, the categories the keys bind it on and its button
        [Fact]
        public void R461_the_parameter_sheet_of_config_gives_the_10_link_parameters_and_plot_with_their_guids_and_buttons()
        {
            ParameterSheet sheet = ParameterSheet.FromConfig(configPath, config);

            Assert.Equal("shared_parameters.xlsx", sheet.Sheet);
            Assert.Equal(11, sheet.Parameters.Count);
            Assert.Empty(sheet.LeftOut);
            Assert.Equal(new[] { "RRE_ZZ_SP_Plot-Number", "RRE_ZZ_SP_Prototype", "RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_Façade-Color", "RRE_ZZ_SP_Façade-Design-Option",
                "RRE_ZZ_SP_Prototype-Foundation-Type", "RRE_ZZ_SP_Prototype-HVAC-Schedule-Type", "RRE_ZZ_SP_RVT-Name", "CASE", "RRE_ZZ_SP_Prototype-FFL", "Plot" }, sheet.Parameters.Select(item => item.Name));
            Assert.All(sheet.Parameters, item => Assert.True(Guid.TryParseExact(item.Guid, "D", out _), item.Name));
            Assert.All(sheet.Parameters, item => Assert.Equal("TEXT", item.DataType));
            Assert.All(sheet.Parameters, item => Assert.Equal("Data", item.Group));
            ParameterSpec plot = sheet.Parameters.Last();
            Assert.Equal("faecae98-f82c-46b5-bd80-489380545236", plot.Guid);
            Assert.Equal(new[] { "Floors" }, plot.Categories);
            Assert.Equal(ParameterSheet.FloorsButton, plot.FilledBy);
            Assert.Null(sheet.Parameters.Single(item => item.Name == Placer.FflParameter).FilledBy);
            Assert.Equal(9, sheet.GuidsFilledBy(ParameterSheet.PlaceVillasButton).Count);
            Assert.Equal(new[] { "Detail Items", "RVT Links" }, sheet.Parameters.First(item => item.Name == "CASE").Categories);
            Assert.Equal("From the parameter sheet: shared_parameters.xlsx, 11 parameters", SetUpTexts.FromSheet(sheet.Source));
            SetUpSiteView view = SetUpPlanner.View(Template(), Target(), null);
            Assert.Empty(view.Waiting);
            Assert.Empty(view.Stops);
            Assert.Equal("shared_parameters.xlsx", view.ParameterSheetName);
            Assert.Contains(SetUpPlanner.Changes(Template(), Target()).Parameters, item => item.Name == "Plot" && item.Guid == plot.Guid);
        }

        [Fact]
        public void R461_config_with_no_parameter_sheet_named_is_refused()
        {
            var none = (JObject)config.DeepClone();
            ((JObject)none["addin"]).Remove("parameterSheet");

            Assert.Equal("config names no addin.parameterSheet, the parameter sheet of button 1 (R461)", Assert.Throws<InvalidDataException>(() => ParameterSheet.FromConfig(configPath, none)).Message);
            Assert.Equal(Path.Combine(root, "x.xlsx"), ParameterSheet.PathOf(configPath, new JObject { ["addin"] = new JObject { ["parameterSheet"] = "x.xlsx" } }));
        }

        [Fact]
        public void R461_a_parameter_sheet_is_read_by_its_eight_columns_in_any_order_and_keep_no_leaves_a_row_out()
        {
            var rows = new List<object[]>
            {
                new object[] { "guid", "PARAMETER", "Category", "Data type", "group", "Filled By", "Sample value", "keep", "a note" },
                new object[] { "C0B2C4EE-E129-4A42-BCB6-225759B874EB", " CASE ", "Detail Items, RVT Links", "text", "Data", "7 Place villas", "A", "yes", "x" },
                new object[] { "6e8e827b-cc9a-4da2-9ddb-c8e3f09ffa36", "RRE_ZZ_SP_RVT-Name", "RVT Links", "Text", "Data", "7", "", "No", null },
                new object[] { null, null, null, null, null, null, null, null, null },
            };

            ParameterSheet sheet = ParameterSheet.FromRows("ROSHN parameters.xlsx", rows);

            ParameterSpec item = Assert.Single(sheet.Parameters);
            Assert.Equal("CASE", item.Name);
            Assert.Equal("c0b2c4ee-e129-4a42-bcb6-225759b874eb", item.Guid);
            Assert.Equal("TEXT", item.DataType);
            Assert.Equal("Data", item.Group);
            Assert.Equal(7, item.FilledBy);
            Assert.Equal(new[] { "Detail Items", "RVT Links" }, item.Categories);
            Assert.Equal(new[] { "RRE_ZZ_SP_RVT-Name" }, sheet.LeftOut);
            Assert.Equal("ROSHN parameters.xlsx, 1 parameters, 1 left out", sheet.Source);
            rows.Add(new object[] { null, "RRE_ZZ_SP_RVT-Name", "Floors", "Text", "Data", "none", null, "Yes", null });
            Assert.Equal("the parameter sheet a.xlsx names RRE_ZZ_SP_RVT-Name twice, one parameter per name (Q155)", Assert.Throws<InvalidDataException>(() => ParameterSheet.FromRows("a.xlsx", rows)).Message);
            Assert.Equal("the parameter sheet a.xlsx has no column Group, Category, Filled by, Sample value, Keep in its first row (R461)",
                Assert.Throws<InvalidDataException>(() => ParameterSheet.FromRows("a.xlsx", new List<object[]> { new object[] { "Parameter", "GUID", "Data type" } })).Message);
            Assert.Equal("the parameter sheet a.xlsx gives A the GUID not a guid, which is no GUID (Q155)",
                Assert.Throws<InvalidDataException>(() => ParameterSheet.FromRows("a.xlsx", Sheet(new[] { "A", "not a guid", "Data", "Floors", "7", "Yes" }))).Message);
            Assert.Equal("the parameter sheet a.xlsx gives A the Keep blank, Yes or No (R461)",
                Assert.Throws<InvalidDataException>(() => ParameterSheet.FromRows("a.xlsx", Sheet(new[] { "A", null, "Data", "Floors", "7", null }))).Message);
            Assert.Equal("the parameter sheet a.xlsx gives A the Filled by Floors, a button from 1 to 9 or none (R465)",
                Assert.Throws<InvalidDataException>(() => ParameterSheet.FromRows("a.xlsx", Sheet(new[] { "A", null, "Data", "Floors", "Floors", "Yes" }))).Message);
        }

        // R461. A sheet picked by Browse that is not the one of config stops button 1 with the reason, since button 7 writes by the sheet of
        // config, and the sheet of config picked again binds as before
        [Fact]
        public void R461_a_picked_sheet_other_than_that_of_config_stops_button_1_and_the_same_sheet_runs()
        {
            var calls = new List<string>();
            var other = new FakeDialog { Browse = Path.Combine(work, "other.xlsx") };

            RunOutcome stopped = Run(new FakeDocument(Template()) { Calls = calls }, other, new FakeTags(calls), calls);

            string stop = "other.xlsx is not the parameter sheet of config, shared_parameters.xlsx, the one every button reads. Edit shared_parameters.xlsx instead (R461)";
            Assert.Contains(stop, other.Shown.Last().Stops);
            Assert.NotEqual(RunEnd.Committed, stopped.End);
            Assert.DoesNotContain("commit", calls);
            calls.Clear();
            var same = new FakeDialog { Browse = (string)config["addin"]["parameterSheet"] };
            Assert.Equal(RunEnd.Committed, Run(new FakeDocument(Template()) { Calls = calls }, same, new FakeTags(calls), calls).End);
            Assert.DoesNotContain(stop, same.Shown.Last().Stops);
        }

        // R464, breaker 1. A parameter bound by its GUID on its categories under another group is bound again under the group of the sheet,
        // and one under that group already is not
        [Fact]
        public void R464_a_parameter_bound_under_another_group_than_the_sheet_is_bound_again()
        {
            SiteState site = Template();
            ParameterSpec number = ParameterSheet.FromConfig(configPath, config).Parameters[0];
            site.SharedParameters.Add(new ParameterState { Name = number.Name, Guid = number.Guid, Categories = number.Categories.ToList(), Group = "Identity Data" });

            Assert.Contains(SetUpPlanner.Changes(site, Target()).Parameters, item => item.Name == number.Name);

            site.SharedParameters[site.SharedParameters.Count - 1].Group = "data";
            Assert.DoesNotContain(SetUpPlanner.Changes(site, Target()).Parameters, item => item.Name == number.Name);
            site.SharedParameters[site.SharedParameters.Count - 1].Group = null;
            Assert.DoesNotContain(SetUpPlanner.Changes(site, Target()).Parameters, item => item.Name == number.Name);
        }

        // R465. RRE_ZZ_SP_Prototype-FFL is never written, the floor parameter is Floors' alone, a kept parameter needs a group
        [Fact]
        public void R465_a_sheet_that_gives_the_ffl_a_button_is_refused_and_the_plot_parameter_of_another_button_or_no_group_stops()
        {
            string ffl = Assert.Throws<InvalidDataException>(() => ParameterSheet.FromRows("a.xlsx", Sheet(new[] { Placer.FflParameter, null, "Data", "RVT Links", "7", "Yes" }))).Message;
            Assert.Equal("the parameter sheet a.xlsx gives RRE_ZZ_SP_Prototype-FFL to button 7, it is never written, Filled by none (R465)", ffl);
            Assert.Empty(ParameterSheet.FromRows("a.xlsx", Sheet(new[] { Placer.FflParameter, null, "Data", "RVT Links", "none", "No" })).Parameters);
            List<object[]> rows = Sheet(new[] { "Plot", "faecae98-f82c-46b5-bd80-489380545236", "Data", "Floors", "7", "Yes" },
                new[] { "CASE", "c0b2c4ee-e129-4a42-bcb6-225759b874eb", null, "RVT Links", "7", "Yes" });

            SetUpSiteView view = SetUpPlanner.View(Template(), Target(sheet: ParameterSheet.FromRows("p.xlsx", rows)), null);

            Assert.Equal(new[] { "Plot is filled by Floors, give it Filled by 5 in the parameter sheet", "CASE has no Group in the parameter sheet" }, view.Stops);
        }

        [Fact]
        public void R373_the_ramp_slope_choices_are_half_1_2_then_3_to_15_by_1_and_the_default_and_the_types_come_from_the_rules_profile()
        {
            JObject rules = Rules();

            Assert.Equal(new[] { 0.5, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, RampSlopes.Choices(rules, null));
            Assert.Equal(new[] { 0.5, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, Target().RampSlopeChoices);
            Assert.Equal(0.5, RampSlopes.Default(rules, null));
            Assert.Equal("Ramps 0.5%", RampSlopes.TypeName(rules, null, 0.5));
            Assert.Equal("Ramps 2%", RampSlopes.TypeName(rules, null, 2));
            Assert.Equal("Ramps 7%", RampSlopes.TypeName(rules, null, 7));
            Assert.Equal(16, SetUpPlanner.View(Template(), Target(), null).RampSlopeChoices.Count);
            Assert.Throws<InvalidDataException>(() => Target(2.5));
        }

        [Fact]
        public void R373_the_floor_types_are_those_of_config_with_one_ramps_type_for_the_slope_chosen_and_the_generic_type_first()
        {
            Assert.Equal(new[] { "Generic 150mm", "yard", "Ramps 0.5%", "Ramps 2%", "Flat Ramps" }, Target().FloorTypes);
            Assert.Equal(new[] { "Generic 150mm", "yard", "Ramps 0.5%", "Ramps 2%", "Flat Ramps", "Ramps 7%" }, Target(7).FloorTypes);
            Assert.Equal(new[] { "BW", "Generic Wall-150mm_ Garden" }, Target().WallTypes);
            SetUpSiteView view = SetUpPlanner.View(Template(), Target(7), null);
            Assert.Equal("Generic 150mm, yard, Flat Ramps, and one Ramps type per slope below", view.WillSet.First(row => row.Label == "Floor types").Value);
            Assert.Equal("7%", view.WillSet.First(row => row.Label == "Ramp slope").Value);
            Assert.True(view.WillSet.First(row => row.Label == "Ramp slope").Differs);
        }

        // J18 of steps/lane-b.md. The wall types, the units of area, angle and slope and the ramp slope choices of W1 come from
        // addin.wallTypes and addin.siteUnits of config and rampSlopeChoices of the level profile, the values of 03a, and each is
        // refused by its name when missing
        [Fact]
        public void R373_J18_the_wall_types_the_units_and_the_ramp_slope_choices_come_from_config_and_the_rules_profile()
        {
            SetUpTarget target = Target();
            Assert.Equal(new[] { "BW", "Generic Wall-150mm_ Garden" }, target.WallTypes);
            Assert.Equal(new[] { "squareMeters", "degrees", "slopeDegrees" }, new[] { target.Units.Area, target.Units.Angle, target.Units.Slope });

            JObject noWalls = (JObject)config.DeepClone();
            ((JObject)noWalls["addin"]).Remove("wallTypes");
            Assert.Contains("addin.wallTypes", Assert.Throws<InvalidDataException>(() => SetUpDefaults.WallTypes(noWalls)).Message);
            JObject noSlope = (JObject)config.DeepClone();
            ((JObject)noSlope["addin"]["siteUnits"]).Remove("slope");
            Assert.Contains("addin.siteUnits.slope", Assert.Throws<InvalidDataException>(() => SetUpDefaults.SiteUnits(noSlope)).Message);
            JObject rules = Rules();
            ((JObject)rules["levelProfiles"]["parsons2024Rev6"]).Remove("rampSlopeChoices");
            Assert.Contains("rampSlopeChoices", Assert.Throws<InvalidDataException>(() => RampSlopes.Choices(rules, null)).Message);
        }

        [Fact]
        public void R373_a_check_point_holds_the_cad_shared_point_against_the_revit_point_of_its_internal_point_in_mm_at_gap_decimals()
        {
            // Q142 (Bader, 29 Oct 2026). Button 1 against Revit at 0.1 mm, addin.position.revitCheckPointLimitMm, a key of its own (W2)
            SetUpTarget target = Target();
            Assert.Equal(0.1, target.PassLimitMm);
            Assert.Equal(2, target.GapDecimals);

            List<CheckPointRow> before = SetUpPlanner.CheckPoints(target, null);
            Assert.Equal(new[] { "Survey point", "Project base point" }, before.Select(row => row.Point));
            Assert.All(before, row => Assert.Null(row.Revit));
            Assert.All(before, row => Assert.Null(row.DiffMm));

            List<CheckPointRow> exact = SetUpPlanner.CheckPoints(target, point => SiteCoordinates.ToShared(target.Position, point));
            Assert.All(exact, row => Assert.Equal(0.0, row.DiffMm));
            Assert.All(exact, row => Assert.True(row.Pass));
            Assert.Equal(SetUpTexts.Coordinate(target.SurveyShared), exact[0].Cad);

            // 0.104 mm shows 0.10 and passes at 0.1 mm, 0.106 mm shows 0.11 and fails, what the table shows is what is judged
            List<CheckPointRow> off = SetUpPlanner.CheckPoints(target, point => Moved(SiteCoordinates.ToShared(target.Position, point), 0.000104));
            Assert.Equal(0.1, off[0].DiffMm);
            Assert.True(off[0].Pass);
            List<CheckPointRow> over = SetUpPlanner.CheckPoints(target, point => Moved(SiteCoordinates.ToShared(target.Position, point), 0.000106));
            Assert.Equal(0.11, over[0].DiffMm);
            Assert.False(over[0].Pass);
            Assert.Equal("Pass limit 0.1 mm. 0 of 2 pass", SetUpTexts.PassLine(target.PassLimitMm, over.Count(row => row.Pass), over.Count));
            SetUpSiteView view = SetUpPlanner.View(Template(), target, point => SiteCoordinates.ToShared(target.Position, point));
            Assert.Equal(2, view.GapDecimals);
            Assert.Equal(0.1, view.PassLimitMm);
            Assert.Equal(2, view.Passing);
            Assert.True(view.DryRunDone);
        }

        private static double[] Moved(double[] point, double east)
        {
            return new[] { point[0] + east, point[1], point[2] };
        }

        [Fact]
        public void R374_the_rows_mark_only_what_differs_and_a_document_that_holds_the_target_sets_nothing()
        {
            SetUpTarget target = Target();
            var document = new FakeDocument(Template());
            SetUpChanges first = SetUpPlanner.Changes(document.State, target);
            Assert.True(first.Units && first.Position && first.SurveyPoint && first.LevelBase);
            Assert.False(first.BasePoint);
            Assert.Equal(new[] { "yard", "Ramps 0.5%", "Ramps 2%", "Flat Ramps" }, first.FloorTypes);
            Assert.Equal(11, first.Parameters.Count);

            document.Hold(target);
            SetUpSiteView view = SetUpPlanner.View(document.State, target, null);

            Assert.False(SetUpPlanner.Changes(document.State, target).Any);
            Assert.True(view.NothingDiffers);
            Assert.All(view.WillSet, row => Assert.False(row.Differs, row.Label));
            Assert.Null(SetUpPlanner.ReadBack(document.State, target));
        }

        [Fact]
        public void R331_the_read_back_names_each_value_off_by_more_than_half_a_mm()
        {
            SetUpTarget target = Target();
            var document = new FakeDocument(Template());
            document.Hold(target);
            document.State.Survey.SharedM[0] += 0.0004;
            Assert.Null(SetUpPlanner.ReadBack(document.State, target));

            document.State.Survey.SharedM[0] += 0.0002;
            document.State.Levels[1].ProjectElevationM += 0.0006;
            document.State.SharedParameters[0].Name += "x";

            Assert.Equal("survey point shared, Level 2, parameter name " + target.Parameters.Parameters[0].Name, SetUpPlanner.ReadBack(document.State, target));
        }

        // R382 with the two members of a body of 5b: its own confirm in place of the flow's, and the scope outside the transaction,
        // opened before it starts and closed after it commits or rolls back
        [Fact]
        public void R382_a_body_with_its_own_confirm_skips_the_confirm_of_the_flow_and_its_outside_scope_wraps_the_transaction()
        {
            var calls = new List<string>();
            var model = new FakeModel(calls);
            var flow = new ButtonFlow(new FakeState(work, configPath), model, null, input => "h", () => RunId);
            ButtonBody body = Body(calls, null);

            RunOutcome outcome = flow.Run(1, "1 Set up site", body, Document);

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Equal(new[] { "preview", "confirm", "outside", "start", "apply", "readBack", "commit", "close" }, calls);

            calls.Clear();
            outcome = flow.Run(1, "1 Set up site", Body(calls, "Level 1"), Document);
            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.Equal(new[] { "preview", "confirm", "outside", "start", "apply", "readBack", "rollBack", "close" }, calls);
        }

        private static ButtonBody Body(List<string> calls, string readBack)
        {
            return new ButtonBody
            {
                Preview = context => { calls.Add("preview"); return null; },
                Confirm = (context, summary) => { calls.Add("confirm"); return true; },
                Outside = context => { calls.Add("outside"); return new Closing(() => calls.Add("close")); },
                Apply = context => { calls.Add("apply"); return null; },
                ReadBack = context => { calls.Add("readBack"); return readBack; },
            };
        }

        [Fact]
        public void R374_apply_sets_only_what_differs_in_the_order_of_03a_and_a_second_run_sets_nothing_and_says_so()
        {
            var document = new FakeDocument(Template());
            var dialog = new FakeDialog();
            var calls = new List<string>();
            document.Calls = calls;
            var tags = new FakeTags(calls);

            RunOutcome first = Run(document, dialog, tags, calls);

            Assert.Equal(RunEnd.Committed, first.End);
            Assert.Equal(new[] { "useParameterFile", "start", "units", "position", "levels", "types", "parameters", "tag", "commit", "restoreParameterFile" }, calls);
            Assert.Equal(new[] { "yard", "Ramps 0.5%", "Ramps 2%", "Flat Ramps" }, document.Made);
            Assert.Equal(new[] { SetUpTexts.SettingsNotWritten }, first.Record.Issues);
            Assert.False(dialog.Shown[0].ExistingSite);

            calls.Clear();
            RunOutcome second = Run(document, dialog, tags, calls);

            // Breaker pass 1 of 5b: nothing differs, so no transaction, no site.json and no run record, the window said so
            Assert.Equal(RunEnd.Cancelled, second.End);
            Assert.Empty(calls);
            Assert.Null(second.Record);
            Assert.True(dialog.Shown[1].NothingDiffers);
            Assert.True(dialog.Shown[1].ExistingSite);
            Assert.Contains(SetUpSiteTestsText.NothingDiffers, dialog.Summaries[1]);
        }

        [Fact]
        public void R374_a_dry_run_fills_the_revit_column_by_the_transform_in_memory_and_cancel_changes_nothing()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var dialog = new FakeDialog { DryRunSlope = 7, Answer = false };

            RunOutcome outcome = Run(document, dialog, new FakeTags(calls), calls);

            Assert.Equal(RunEnd.Cancelled, outcome.End);
            Assert.Equal(new[] { "sharedOf", "sharedOf" }, calls);
            SetUpSiteView dry = dialog.DryViews.Single();
            Assert.True(dry.DryRunDone);
            Assert.All(dry.CheckPoints, row => Assert.Equal(0.0, row.DiffMm));
            Assert.Equal(7, dry.RampSlope);
            Assert.Empty(Directory.GetFiles(work, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public void R374_the_shared_parameter_file_is_written_in_the_work_folder_set_outside_the_transaction_put_back_and_deleted()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };

            Run(document, new FakeDialog(), new FakeTags(calls), calls);

            string path = Path.Combine(work, "shared_" + RunId + ".txt");
            Assert.Equal(path, document.ParameterFile);
            Assert.Contains("PARAM\t" + ParameterSheet.FromConfig(configPath, config).Parameters[0].Guid + "\tRRE_ZZ_SP_Plot-Number\tTEXT", document.ParameterText);
            Assert.Contains("RRE_ZZ_SP_Fa\u00e7ade-Color", document.ParameterText);
            Assert.False(File.Exists(path));
            Assert.True(calls.IndexOf("useParameterFile") < calls.IndexOf("start"));
            Assert.True(calls.IndexOf("restoreParameterFile") > calls.IndexOf("commit"));
        }

        [Fact]
        public void R374_a_read_back_that_differs_rolls_back_puts_the_setting_back_and_leaves_no_site_json()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls, SkewLevelsMm = 0.6 };

            RunOutcome outcome = Run(document, new FakeDialog(), new FakeTags(calls), calls);

            Assert.Equal(RunEnd.RolledBack, outcome.End);
            Assert.Contains("The read back does not match: Level 1, Level 2", outcome.Message);
            Assert.Equal("rollBack", calls[calls.Count - 2]);
            Assert.Equal("restoreParameterFile", calls.Last());
            Assert.Empty(Directory.GetFiles(work, "*", SearchOption.AllDirectories));
        }

        // R454 to R456. With coordinates.source cad button 1 sets the named position and the site location before the position, takes
        // the shared origin of the CAD plots and passes the read back with its control points. A conflict of systems stops before any set
        [Fact]
        public void R455_button_1_with_the_cad_source_sets_the_location_then_the_position_of_the_plots()
        {
            config["coordinates"]["source"] = "cad";
            File.WriteAllText(configPath, config.ToString());
            var plots = new List<CadPlot> { new CadPlot { Plot = "1", Vertices = new List<double[]> { new[] { 300010.0, 2400020.0 }, new[] { 300090.0, 2400020.0 }, new[] { 300090.0, 2400060.0 } } } };
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var run = new SetUpRun(document, new FakeDialog(), new FakeTags(calls), File.ReadAllText, Settings, null)
                .ReadingCad(context => new CoordinateRead { Files = new List<StatedSystem> { new StatedSystem { File = "Road Layout.dwg", Name = "UTM84-37N" } }, Plots = plots });
            var flow = new ButtonFlow(new FakeState(work, configPath), new FakeModel(calls), null, input => "h", () => RunId);

            RunOutcome outcome = flow.Run(1, "1 Set up site", run.Body(), Document);

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.True(calls.IndexOf("location") >= 0 && calls.IndexOf("location") < calls.IndexOf("position"));
            Assert.Equal(300050.0, document.State.Position.EastM);
            Assert.Equal(2400040.0, document.State.Position.NorthM);
            Assert.Equal((string)config["coordinates"]["positionName"], document.State.PositionName);
            Assert.Equal(5, run.View.CheckPoints.Count(row => row.Pass));

            var conflict = new SetUpRun(new FakeDocument(Template()) { Calls = new List<string>() }, new FakeDialog(), new FakeTags(new List<string>()), File.ReadAllText, Settings, null)
                .ReadingCad(context => new CoordinateRead { Files = new List<StatedSystem> { new StatedSystem { File = "a.dwg", Name = "UTM84-37N" }, new StatedSystem { File = "b.xml", Name = "32638" } }, Plots = plots });
            RunOutcome stopped = flow.Run(1, "1 Set up site", conflict.Body(), Document);
            Assert.NotEqual(RunEnd.Committed, stopped.End);
            Assert.Contains("b.xml EPSG 32638", stopped.Message);

            var none = new SetUpRun(new FakeDocument(Template()) { Calls = new List<string>() }, new FakeDialog(), new FakeTags(new List<string>()), File.ReadAllText, Settings, null);
            Assert.Contains("reads no CAD", flow.Run(1, "1 Set up site", none.Body(), Document).Message);
        }

        [Fact]
        public void R374_site_json_holds_the_facts_read_back_and_moves_onto_its_name_after_the_commit()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };

            RunOutcome outcome = Run(document, new FakeDialog(), new FakeTags(calls), calls);

            string site = Path.Combine(work, "site.json");
            Assert.True(outcome.Record.StepDone);
            Assert.True(outcome.Record.Outputs.ContainsKey(site));
            JObject facts = JObject.Parse(File.ReadAllText(site));
            Assert.Equal(SetUpRun.SiteFormat, (string)facts["format"]);
            Assert.Equal(new[] { "Level 1", "Level 2" }, facts["levels"].Select(level => (string)level["name"]));
            Assert.Equal(20.0, (double)facts["levels"][0]["elevationM"], 9);
            Assert.Equal("Survey Point", (string)facts["levels"][0]["elevationBase"]);
            Assert.Equal(SurveyInternal, facts["surveyPoint"]["position"].Select(value => (double)value).ToArray());
            Assert.Equal(new[] { "site.json" }, Directory.GetFiles(work).Select(Path.GetFileName));
        }

        [Fact]
        public void R373_a_stop_found_before_apply_stops_the_button_with_its_list_and_changes_nothing()
        {
            var calls = new List<string>();
            SiteState site = Template();
            site.SharedParameters.Add(new ParameterState { Name = "CASE", Guid = "11111111-2222-3333-4444-555555555555", Categories = { "RVT Links" } });
            var document = new FakeDocument(site) { Calls = calls };

            RunOutcome outcome = Run(document, new FakeDialog(), new FakeTags(calls), calls);

            Assert.Equal(RunEnd.Stopped, outcome.End);
            Assert.Contains("CASE: GUID found 11111111-2222-3333-4444-555555555555, GUID of the sheet c0b2c4ee-e129-4a42-bcb6-225759b874eb", outcome.Message);
            Assert.Empty(calls);
        }

        [Fact]
        public void R383_button_1_calls_the_one_tag_step_once_inside_the_transaction_after_the_steps_and_before_the_commit()
        {
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var tags = new FakeTags(calls);

            Run(document, new FakeDialog(), tags, calls);

            Assert.Equal(new[] { RunId + " 1" }, tags.Asked);
            Assert.True(calls.IndexOf("tag") > calls.IndexOf("parameters") && calls.IndexOf("tag") < calls.IndexOf("commit"));
            Assert.Empty(new NoTags().Tag(RunId, 1));
        }

        // J1 and J19 of steps/lane-b.md. Button 1 writes the Settings entity of R367 with the record of its real run inside its
        // transaction, after the tag step and before the commit, and the settings of the pane are not changed by it
        [Fact]
        public void R367_button_1_writes_the_settings_entity_with_its_last_run_inside_the_transaction()
        {
            const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            var calls = new List<string>();
            var document = new FakeDocument(Template()) { Calls = calls };
            var entity = new RecordingEntity(calls);
            var settings = new ProjectSettings { ConfigFile = configPath, DryRunFirst = false };
            var run = new SetUpRun(document, new FakeDialog(), new FakeTags(calls), File.ReadAllText, Settings, null)
                .Recording(() => settings, entity, () => new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc));
            var flow = new ButtonFlow(new FakeState(work, configPath), new FakeModel(calls), null, input => hash, () => RunId);

            RunOutcome outcome = flow.Run(1, "1 Set up site", run.Body(), Document);

            Assert.Equal(RunEnd.Committed, outcome.End);
            Assert.Empty(outcome.Record.Issues);
            Assert.True(calls.IndexOf("settings") > calls.IndexOf("tag") && calls.IndexOf("settings") < calls.IndexOf("commit"));
            ProjectSettings written = SettingsJson.Read(entity.Json);
            ButtonRun last = written.LastRuns["1"];
            Assert.Equal("2026-10-30T12:00:00.000Z", last.Time);
            Assert.Equal(RunModes.Direct, last.Mode);
            Assert.Equal(ScopeKinds.Project, last.Scope.Kind);
            Assert.Equal(hash, last.InputHashes[Settings]);
            Assert.Equal(configPath, written.ConfigFile);
            Assert.Empty(settings.LastRuns);

            // A second run with nothing to set opens no transaction, so the entity is not written again
            calls.Clear();
            Assert.Equal(RunEnd.Cancelled, flow.Run(1, "1 Set up site", run.Body(), Document).End);
            Assert.Equal(1, entity.Writes);
        }

        private sealed class RecordingEntity : ISettingsEntity
        {
            private readonly List<string> calls;

            public RecordingEntity(List<string> calls)
            {
                this.calls = calls;
            }

            public string Json { get; private set; }
            public int Writes { get; private set; }

            public string Read()
            {
                return Json;
            }

            public void Write(string json)
            {
                calls.Add("settings");
                Writes++;
                Json = json;
            }
        }

        private RunOutcome Run(FakeDocument document, FakeDialog dialog, ITagStep tags, List<string> calls)
        {
            var run = new SetUpRun(document, dialog, tags, File.ReadAllText, Settings, null);
            var flow = new ButtonFlow(new FakeState(work, configPath), new FakeModel(calls), null, input => "h", () => RunId);
            return flow.Run(1, "1 Set up site", run.Body(), Document);
        }

        private static class SetUpSiteTestsText
        {
            public const string NothingDiffers = "Nothing differs, Apply changes nothing.";
        }

        // A document that holds a SiteState and carries out each step on it, as Revit would, recording each call
        private sealed class FakeDocument : ISiteDocument
        {
            public FakeDocument(SiteState state)
            {
                State = state;
            }

            public SiteState State { get; }
            public List<string> Calls { get; set; } = new List<string>();
            public List<string> Made { get; } = new List<string>();
            public string ParameterFile { get; private set; }
            public string ParameterText { get; private set; }
            public double SkewLevelsMm { get; set; }

            public SiteState Read()
            {
                return Copy(State);
            }

            public double[] SharedOf(SitePosition position, double[] internalM)
            {
                Calls.Add("sharedOf");
                return SiteCoordinates.ToShared(position, internalM);
            }

            public double[] SharedNow(double[] internalM)
            {
                return SiteCoordinates.ToShared(State.Position, internalM);
            }

            public void SetUnits(UnitState units)
            {
                Calls.Add("units");
                State.Units = new UnitState { System = State.Units.System, Length = units.Length, LengthAccuracy = units.LengthAccuracy, Area = units.Area, Angle = units.Angle, Slope = units.Slope };
            }

            public void SetLocation(SetUpTarget target)
            {
                Calls.Add("location");
                State.PositionName = target.Cad.PositionName;
                State.LatitudeDeg = target.Cad.LatitudeDeg;
                State.LongitudeDeg = target.Cad.LongitudeDeg;
                State.PlaceName = target.Cad.PlaceName;
            }

            public void SetPosition(SetUpTarget target, SetUpChanges changes)
            {
                Calls.Add("position");
                State.Position = target.Position;
                State.Survey.InternalM = (double[])target.SurveyInternal.Clone();
                State.ProjectBase.InternalM = (double[])target.BaseInternal.Clone();
                State.Survey.SharedM = SiteCoordinates.ToShared(State.Position, State.Survey.InternalM);
                State.ProjectBase.SharedM = SiteCoordinates.ToShared(State.Position, State.ProjectBase.InternalM);
            }

            public void SetLevels(SetUpTarget target, SetUpChanges changes)
            {
                Calls.Add("levels");
                foreach (PositionLevel level in target.Levels)
                {
                    LevelState have = State.Levels.FirstOrDefault(item => item.Name == level.Name);
                    if (have == null)
                    {
                        have = new LevelState { Name = level.Name };
                        State.Levels.Add(have);
                    }
                    have.Base = target.LevelBase;
                    have.ElevationM = level.ElevationM + SkewLevelsMm / 1000.0;
                    have.ProjectElevationM = level.ProjectElevationM + SkewLevelsMm / 1000.0;
                }
            }

            public void MakeTypes(SetUpTarget target, SetUpChanges changes)
            {
                Calls.Add("types");
                Made.AddRange(changes.FloorTypes);
                State.FloorTypes.AddRange(changes.FloorTypes);
                State.WallTypes.AddRange(changes.WallTypes);
            }

            public void BindParameters(SetUpTarget target, IReadOnlyList<ParameterSpec> parameters)
            {
                Calls.Add("parameters");
                Assert.NotNull(ParameterFile);
                foreach (ParameterSpec item in parameters)
                {
                    State.SharedParameters.Add(new ParameterState { Name = item.Name, Guid = item.Guid, Categories = item.Categories.ToList(), Group = item.Group });
                }
            }

            public IDisposable UseParameterFile(string path)
            {
                Calls.Add("useParameterFile");
                ParameterFile = path;
                ParameterText = File.ReadAllText(path, SharedParameterText.FileEncoding);
                return new Closing(() => Calls.Add("restoreParameterFile"));
            }

            // The commit or the roll back of the model is on the fake model, so a roll back puts the state back by the copy it took
            public void Hold(SetUpTarget target)
            {
                SetUnits(target.Units);
                SetPosition(target, null);
                SetLevels(target, null);
                State.FloorTypes.AddRange(target.FloorTypes.Where(name => !State.FloorTypes.Contains(name)));
                State.WallTypes.AddRange(target.WallTypes.Where(name => !State.WallTypes.Contains(name)));
                BindParameters2(target);
                State.SettingsEntity = true;
                Calls.Clear();
            }

            private void BindParameters2(SetUpTarget target)
            {
                foreach (ParameterSpec item in target.Parameters.Parameters)
                {
                    State.SharedParameters.Add(new ParameterState { Name = item.Name, Guid = item.Guid, Categories = item.Categories.ToList(), Group = item.Group });
                }
            }

            private static SiteState Copy(SiteState state)
            {
                return JObject.FromObject(state).ToObject<SiteState>();
            }
        }

        private sealed class FakeDialog : ISetUpSiteDialog
        {
            public List<SetUpSiteView> Shown { get; } = new List<SetUpSiteView>();
            public List<SetUpSiteView> DryViews { get; } = new List<SetUpSiteView>();
            public List<string> Summaries { get; } = new List<string>();
            public double? DryRunSlope { get; set; }
            public bool Answer { get; set; } = true;

            // A sheet picked by Browse, given to the run as the window does
            public string Browse { get; set; }

            public SetUpSiteAnswer Show(SetUpSiteView view, SetUpSiteCallbacks callbacks)
            {
                Shown.Add(view);
                Summaries.Add(view.NothingDiffers ? SetUpTexts.NothingDiffers : "");
                double slope = view.RampSlope;
                if (DryRunSlope.HasValue)
                {
                    slope = DryRunSlope.Value;
                    DryViews.Add(callbacks.DryRun(slope));
                }
                if (Browse != null)
                {
                    Shown.Add(callbacks.SheetChosen(Browse));
                }
                return new SetUpSiteAnswer { Apply = Answer, RampSlope = slope, ParameterSheet = Browse };
            }
        }

        private sealed class FakeTags : ITagStep
        {
            private readonly List<string> calls;

            public FakeTags(List<string> calls)
            {
                this.calls = calls;
            }

            public List<string> Asked { get; } = new List<string>();

            public IReadOnlyList<string> Tag(string runId, int button)
            {
                calls.Add("tag");
                Asked.Add(runId + " " + button);
                return new string[0];
            }
        }

        private sealed class Closing : IDisposable
        {
            private readonly Action end;

            public Closing(Action end)
            {
                this.end = end;
            }

            public void Dispose()
            {
                end();
            }
        }

        private sealed class FakeState : IRunState
        {
            private readonly string work;
            private readonly string configPath;

            public FakeState(string work, string configPath)
            {
                this.work = work;
                this.configPath = configPath;
            }

            public bool DryRunFirst { get; set; }

            public string WorkFolder(string documentPath)
            {
                return work;
            }

            public int MissingStep(int button)
            {
                return 0;
            }

            public IReadOnlyList<RunInput> Inputs(int button)
            {
                return new[] { new RunInput(Settings, configPath, false) };
            }

            public void Committed(int button, RunRecord record)
            {
            }
        }

        // A model whose roll back is told to the calls, the document state of the fake is kept as Apply left it
        private sealed class FakeModel : IModel
        {
            private readonly List<string> calls;

            public FakeModel(List<string> calls)
            {
                this.calls = calls;
            }

            public IModelTransaction Start(string name)
            {
                Assert.Equal("Site Adaptation 1 Set up site", name);
                calls.Add("start");
                return new Transaction(calls);
            }

            private sealed class Transaction : IModelTransaction
            {
                private readonly List<string> calls;

                public Transaction(List<string> calls)
                {
                    this.calls = calls;
                }

                public IReadOnlyList<string> Warnings
                {
                    get { return new string[0]; }
                }

                public IReadOnlyList<string> Errors
                {
                    get { return new string[0]; }
                }

                public bool Commit()
                {
                    calls.Add("commit");
                    return true;
                }

                public void RollBack()
                {
                    calls.Add("rollBack");
                }

                public void Dispose()
                {
                }
            }
        }
    }
}
