using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Objects;
using CadReader;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R454 to R456. The coordinate system of the inputs, the site position from CAD and the three control points, on made up plots far
    // from the site. The UTM math is held against two methods of its own: the meridian arc by numeric integration on the central meridian
    // and a forward series off it
    public sealed class R454_SiteCoordinatesTests : IDisposable
    {
        private const string Definition = "<?xml version=\"1.0\" encoding=\"UTF-16\" standalone=\"no\" ?><Dictionary version=\"1.0\" xmlns=\"http://www.osgeo.org/mapguide/coordinatesystem\">"
            + "<ProjectedCoordinateSystem id=\"UTM84-37N\"><Name>UTM84-37N</Name><DatumId>WGS84</DatumId></ProjectedCoordinateSystem>"
            + "<GeodeticDatum id=\"WGS84\"><Name>WGS84</Name></GeodeticDatum></Dictionary>";

        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r454_" + Guid.NewGuid().ToString("N"));
        private readonly JObject config;

        public R454_SiteCoordinatesTests()
        {
            Directory.CreateDirectory(folder);
            config = JObject.Parse(System.IO.File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")));
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private CoordinateSettings Settings()
        {
            return CoordinateSettings.From(config);
        }

        private static StatedSystem File(string file, string name)
        {
            return new StatedSystem { File = file, Name = name };
        }

        [Fact]
        public void R454_config_holds_the_coordinates_block()
        {
            CoordinateSettings settings = Settings();
            Assert.Contains(settings.Source, new[] { CoordinateSettings.FromCadSource, CoordinateSettings.FromPositionFile });
            Assert.Equal(32637, settings.Systems["UTM84-37N"]);
            Assert.Equal(32637, settings.ExpectedEpsg);
            Assert.Equal(0.1, settings.ControlPointLimitMm);
            Assert.Equal(3, settings.ControlPointCount);
            Assert.False(string.IsNullOrEmpty(settings.PositionName));
            JObject broken = (JObject)config.DeepClone();
            broken["coordinates"]["source"] = "survey";
            Assert.Throws<InvalidDataException>(() => CoordinateSettings.From(broken));
            broken.Remove("coordinates");
            Assert.Throws<InvalidDataException>(() => CoordinateSettings.From(broken));
        }

        [Fact]
        public void R454_one_stated_system_agrees_and_a_file_that_states_none_does_not_stop()
        {
            SystemsCheck check = CoordinateSystems.Agree(new[] { File("Road Layout.dwg", "UTM84-37N"), File("Baseplan.dwg", null), File("roads.xml", null) }, Settings());

            Assert.Equal(SystemAgreement.Agree, check.Outcome);
            Assert.Equal(32637, check.Epsg);
            Assert.Equal(32637, check.EpsgForMath);
            Assert.Empty(check.Stops);
            Assert.Contains("Baseplan.dwg states no coordinate system", check.Lines);
            Assert.Contains("Road Layout.dwg states UTM84-37N, EPSG 32637", check.Lines);
        }

        [Fact]
        public void R454_two_stated_systems_that_differ_are_a_conflict_that_stops()
        {
            SystemsCheck check = CoordinateSystems.Agree(new[] { File("a.dwg", "UTM84-37N"), File("b.xml", "32638") }, Settings());

            Assert.Equal(SystemAgreement.Conflict, check.Outcome);
            Assert.Null(check.Epsg);
            Assert.Single(check.Stops);
            Assert.Contains("a.dwg EPSG 32637", check.Stops[0]);
            Assert.Contains("b.xml EPSG 32638", check.Stops[0]);
        }

        [Fact]
        public void R454_a_stated_system_unlike_the_expected_code_or_of_no_known_code_stops()
        {
            SystemsCheck other = CoordinateSystems.Agree(new[] { File("b.xml", "EPSG:32638") }, Settings());
            Assert.Equal(SystemAgreement.Agree, other.Outcome);
            Assert.Equal(32638, other.Epsg);
            Assert.Contains("EPSG 32637 of coordinates.expectedEpsg", Assert.Single(other.Stops));

            SystemsCheck unknown = CoordinateSystems.Agree(new[] { File("c.dwg", "LL84") }, Settings());
            Assert.Contains("coordinates.systems", Assert.Single(unknown.Stops));
        }

        [Fact]
        public void R454_no_file_stating_a_system_gives_none_stated_and_the_expected_code_for_the_math()
        {
            SystemsCheck check = CoordinateSystems.Agree(new[] { File("Baseplan.dwg", null) }, Settings());

            Assert.Equal(SystemAgreement.NoneStated, check.Outcome);
            Assert.Null(check.Epsg);
            Assert.Equal(32637, check.EpsgForMath);
            Assert.Empty(check.Stops);
        }

        [Fact]
        public void R454_the_worker_reads_the_geodata_system_of_a_drawing_and_the_epsg_code_of_a_landxml()
        {
            var document = new CadDocument();
            Assert.Null(CoordinateSystemRead.OfDrawing(document));
            CadDictionary extension = document.ModelSpace.CreateExtendedDictionary();
            extension.Add(CadDictionary.GeographicData, new GeoData { CoordinateSystemDefinition = Definition });
            Assert.Equal("UTM84-37N", CoordinateSystemRead.OfDrawing(document));
            Assert.Equal("UTM84-37N", CoordinateSystemRead.NameOfDefinition("PROJCS[\"UTM84-37N\",GEOGCS[\"WGS84\"]]"));

            string stated = Path.Combine(folder, "stated.xml");
            System.IO.File.WriteAllText(stated, "<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\"><Units><Metric linearUnit=\"meter\"/></Units><CoordinateSystem epsgCode=\"32637\"/><Surfaces/></LandXML>");
            string none = Path.Combine(folder, "none.xml");
            System.IO.File.WriteAllText(none, "<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\"><Units><Metric linearUnit=\"meter\"/></Units><Surfaces/></LandXML>");
            Assert.Equal("32637", CoordinateSystemRead.OfLandXml(stated));
            Assert.Null(CoordinateSystemRead.OfLandXml(none));

            Assert.Contains(CoordinateRequest.Command, WorkerRequest.Commands);
            Assert.True(WorkerReads.Commands().ContainsKey(CoordinateRequest.Command));
        }

        [Fact]
        public void R454_the_request_names_each_drawing_and_landxml_with_its_hash_and_the_answer_reads_back()
        {
            string configPath = Path.Combine(folder, "config", "project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            System.IO.File.WriteAllText(configPath, config.ToString());
            System.IO.File.WriteAllText(Path.Combine(folder, "config", "cad_layers.json"), "{}");
            string baseplan = Path.Combine(folder, "Baseplan.dwg");
            string roads = Path.Combine(folder, "roads");
            Directory.CreateDirectory(roads);
            System.IO.File.WriteAllText(baseplan, "made up");
            System.IO.File.WriteAllText(Path.Combine(roads, "r1.xml"), "<LandXML/>");
            System.IO.File.WriteAllText(Path.Combine(folder, "sales.xlsx"), "not a drawing");
            var inputs = new List<RunInput>
            {
                new RunInput(InputNames.Baseplan, baseplan, false), new RunInput(InputNames.RoadSurface, roads, true),
                new RunInput(InputNames.Sales, Path.Combine(folder, "sales.xlsx"), false), new RunInput("Settings", configPath, false),
            };

            WorkerRequest request = CoordinateRequest.Make(inputs, new Dictionary<string, string> { [InputNames.Baseplan] = "baseplan", [InputNames.RoadSurface] = "roadSurface", [InputNames.Sales] = "sales", ["Settings"] = "config" }, configPath, "run1", Path.Combine(folder, "work"), new WorkerDocument { Path = "a.rvt", SessionId = "s" }, 600);

            Assert.Equal(CoordinateRequest.Command, request.Command);
            Assert.Equal(new[] { "baseplan", "cadLayers", "config", "roadSurface:r1.xml" }, request.Inputs.Select(input => input.Role).OrderBy(role => role, StringComparer.Ordinal).ToArray());
            Assert.All(request.Inputs, input => Assert.Equal(FileHash.Sha256(input.Path), input.Sha256));

            JObject answer = new JObject
            {
                ["files"] = new JArray(new JObject { ["file"] = "Baseplan.dwg", ["name"] = null }, new JObject { ["file"] = "r1.xml", ["name"] = "32637" }),
                ["plots"] = new JArray(new JObject { ["plot"] = "7", ["vertices"] = new JArray(new JArray(1.0, 2.0), new JArray(3.0, 2.0), new JArray(3.0, 4.0)) }),
            };
            CoordinateRead read = CoordinateRead.Parse(answer);
            Assert.Null(read.Files[0].Name);
            Assert.Equal("32637", read.Files[1].Name);
            Assert.Equal("7", read.Plots.Single().Plot);
            Assert.Equal(3, read.Plots.Single().Vertices.Count);
        }

        // The meridian arc of WGS84 from the equator, by Simpson's rule over 20000 steps, a method of its own beside the series of UtmMath
        private static double MeridianArc(double latitudeDeg)
        {
            const double a = 6378137.0;
            const double f = 1 / 298.257223563;
            double e2 = f * (2 - f);
            double end = latitudeDeg * Math.PI / 180;
            const int steps = 20000;
            double h = end / steps;
            double sum = 0;
            for (int i = 0; i <= steps; i++)
            {
                double phi = i * h;
                double s = Math.Sin(phi);
                double radius = a * (1 - e2) / Math.Pow(1 - e2 * s * s, 1.5);
                sum += radius * (i == 0 || i == steps ? 1 : i % 2 == 1 ? 4 : 2);
            }
            return sum * h / 3;
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(10.0)]
        [InlineData(21.4)]
        [InlineData(45.0)]
        [InlineData(80.0)]
        public void R455_utm_on_the_central_meridian_gives_the_latitude_of_the_meridian_arc(double latitudeDeg)
        {
            double[] at = UtmMath.ToLatLon(500000.0, 0.9996 * MeridianArc(latitudeDeg), UtmMath.ZoneOf(32637));

            Assert.Equal(latitudeDeg, at[0], 1e-7);
            Assert.Equal(39.0, at[1], 1e-7);
        }

        // The forward Krueger series to n cubed, written apart from the inverse of UtmMath
        private static double[] Forward(double latitudeDeg, double longitudeDeg, double centralDeg, bool north)
        {
            const double a = 6378137.0;
            const double f = 1 / 298.257223563;
            double n = f / (2 - f);
            double big = a / (1 + n) * (1 + n * n / 4 + n * n * n * n / 64);
            double[] alpha = { n / 2 - 2 * n * n / 3 + 5 * n * n * n / 16, 13 * n * n / 48 - 3 * n * n * n / 5, 61 * n * n * n / 240 };
            double phi = latitudeDeg * Math.PI / 180;
            double lambda = (longitudeDeg - centralDeg) * Math.PI / 180;
            double c = 2 * Math.Sqrt(n) / (1 + n);
            double t = Math.Sinh(Atanh(Math.Sin(phi)) - c * Atanh(c * Math.Sin(phi)));
            double xi = Math.Atan(t / Math.Cos(lambda));
            double eta = Atanh(Math.Sin(lambda) / Math.Sqrt(1 + t * t));
            double east = eta, northing = xi;
            for (int j = 1; j <= 3; j++)
            {
                east += alpha[j - 1] * Math.Cos(2 * j * xi) * Math.Sinh(2 * j * eta);
                northing += alpha[j - 1] * Math.Sin(2 * j * xi) * Math.Cosh(2 * j * eta);
            }
            return new[] { 500000.0 + 0.9996 * big * east, (north ? 0.0 : 10000000.0) + 0.9996 * big * northing };
        }

        private static double Atanh(double x)
        {
            return 0.5 * Math.Log((1 + x) / (1 - x));
        }

        [Theory]
        [InlineData(32637, 21.4, 39.8)]
        [InlineData(32637, 21.0, 37.6)]
        [InlineData(32637, 23.9, 41.9)]
        [InlineData(32734, -33.9, 18.4)]
        public void R455_utm_off_the_central_meridian_gives_back_the_point_of_the_forward_series(int epsg, double latitudeDeg, double longitudeDeg)
        {
            UtmZone zone = UtmMath.ZoneOf(epsg);
            double[] grid = Forward(latitudeDeg, longitudeDeg, zone.CentralMeridianDeg, zone.North);

            double[] at = UtmMath.ToLatLon(grid[0], grid[1], zone);

            Assert.Equal(latitudeDeg, at[0], 1e-7);
            Assert.Equal(longitudeDeg, at[1], 1e-7);
        }

        [Fact]
        public void R455_the_zone_and_hemisphere_come_from_the_epsg_code()
        {
            Assert.Equal(37, UtmMath.ZoneOf(32637).Number);
            Assert.True(UtmMath.ZoneOf(32637).North);
            Assert.Equal(39.0, UtmMath.ZoneOf(32637).CentralMeridianDeg);
            Assert.False(UtmMath.ZoneOf(32734).North);
            Assert.Equal(21.0, UtmMath.ZoneOf(32734).CentralMeridianDeg);
            Assert.Throws<InvalidDataException>(() => UtmMath.ZoneOf(4326));
        }

        // Made up plots, a block of 10 by 20 m squares, none of the site
        private static List<CadPlot> Plots()
        {
            var plots = new List<CadPlot>();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double x = 400100.4 + i * 12, y = 2500200.2 + j * 22;
                    plots.Add(new CadPlot { Plot = (i * 3 + j + 1).ToString(), Vertices = new List<double[]> { new[] { x, y }, new[] { x + 10, y }, new[] { x + 10, y + 20 }, new[] { x, y + 20 } } });
                }
            }
            return plots;
        }

        [Fact]
        public void R455_the_shared_origin_is_the_centre_of_the_plot_box_rounded_with_the_angle_elevation_and_name_of_config()
        {
            CoordinateSettings settings = Settings();
            settings.OriginRoundM = 1.0;
            settings.OriginElevationM = 12.5;

            CadSite site = SiteFromCad.FromCad(Plots(), settings, 32637);

            // The box runs 400100.4 to 400146.4 east and 2500200.2 to 2500264.2 north
            Assert.Equal(400123.0, site.Position.EastM);
            Assert.Equal(2500232.0, site.Position.NorthM);
            Assert.Equal(0.0, site.Position.ElevationM);
            Assert.Equal(settings.AngleToTrueNorthDeg, site.Position.AngleDeg);
            Assert.Equal(new[] { 0.0, 0.0, 12.5 }, site.SurveyInternal);
            Assert.Equal(new[] { 400123.0, 2500232.0, 12.5 }, site.SurveyShared);
            Assert.Equal(site.SurveyInternal, site.BaseInternal);
            Assert.Equal(site.SurveyShared, site.BaseShared);
            double[] at = UtmMath.ToLatLon(400123.0, 2500232.0, UtmMath.ZoneOf(32637));
            Assert.Equal(at[0], site.LatitudeDeg);
            Assert.Equal(at[1], site.LongitudeDeg);
            Assert.Equal(settings.PositionName, site.PositionName);
            Assert.Throws<InvalidDataException>(() => SiteFromCad.FromCad(new List<CadPlot>(), settings, 32637));
        }

        [Fact]
        public void R456_pick_gives_three_corners_far_apart_by_a_fixed_rule()
        {
            List<ControlPoint> points = ControlPoints.Pick(Plots(), 3);

            Assert.Equal(3, points.Count);
            // The south west corner, the corner farthest from it, then the corner farthest from both
            Assert.Equal(new[] { 400100.4, 2500200.2, 0.0 }, points[0].SharedM);
            Assert.Equal("1", points[0].Plot);
            Assert.Equal(new[] { 400146.4, 2500264.2, 0.0 }, points[1].SharedM);
            Assert.Equal("12", points[1].Plot);
            Assert.Equal(new[] { 400146.4, 2500200.2, 0.0 }, points[2].SharedM);
            Assert.Equal(points.Select(point => point.Name), ControlPoints.Pick(Plots().AsEnumerable().Reverse().ToList(), 3).Select(point => point.Name));
            Assert.Throws<InvalidDataException>(() => ControlPoints.Pick(Plots().Take(0).ToList(), 3));
        }

        [Fact]
        public void R456_check_passes_within_the_limit_and_fails_past_it()
        {
            var position = new SitePosition { EastM = 400123.0, NorthM = 2500232.0, ElevationM = 0.0, AngleDeg = 0.0 };
            ControlPoint point = ControlPoints.Pick(Plots(), 3)[1];

            CheckPointRow exact = ControlPoints.Check(point, position, internalM => SiteCoordinates.ToShared(position, internalM), 0.1, 2);
            CheckPointRow off = ControlPoints.Check(point, position, internalM => SiteCoordinates.ToShared(position, internalM).Select((value, i) => i == 0 ? value + 0.0002 : value).ToArray(), 0.1, 2);
            CheckPointRow before = ControlPoints.Check(point, position, null, 0.1, 2);

            Assert.True(exact.Pass);
            Assert.Equal(0.0, exact.DiffMm);
            Assert.False(off.Pass);
            Assert.Equal(0.2, off.DiffMm);
            Assert.Null(before.DiffMm);
            Assert.Null(before.Revit);
            Assert.Equal(point.Name, exact.Point);
        }

        private SetUpTarget CadTarget(SystemsCheck systems)
        {
            JObject rules = JObject.Parse(System.IO.File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "rules", "roshn-makkah.json")));
            var position = new JObject
            {
                ["format"] = ProjectPositionFile.Format, ["formatVersion"] = ProjectPositionFile.FormatVersion,
                ["projectPosition"] = new JObject { ["eastM"] = 1.0, ["northM"] = 2.0, ["elevationM"] = 0.0, ["angleDeg"] = 0.0 },
                ["surveyPoint"] = new JObject { ["internalM"] = new JArray(0.0, 0.0, 0.0), ["sharedM"] = new JArray(1.0, 2.0, 0.0) },
                ["projectBasePoint"] = new JObject { ["internalM"] = new JArray(0.0, 0.0, 0.0), ["sharedM"] = new JArray(1.0, 2.0, 0.0) },
                ["levels"] = new JArray(new JObject { ["name"] = "Level 1", ["elevationM"] = 0.0, ["projectElevationM"] = 0.0 }),
                ["levelBase"] = "Survey Point", ["units"] = new JObject { ["system"] = "METRIC", ["length"] = "meters" }, ["lengthAccuracy"] = 0.01, ["activeSite"] = "Internal",
            };
            CoordinateSettings settings = Settings();
            CadSite site = SiteFromCad.FromCad(Plots(), settings, systems.EpsgForMath);
            site.Systems = systems;
            site.ControlPoints = ControlPoints.Pick(Plots(), settings.ControlPointCount);
            site.ControlPointLimitMm = settings.ControlPointLimitMm;
            return SetUpTarget.From(config, rules, null, position, RampSlopes.Default(rules, null), ParameterSheet.FromConfig(config), site);
        }

        [Fact]
        public void R455_the_target_takes_the_cad_site_over_the_position_file_and_sets_the_named_position_and_site_location()
        {
            SetUpTarget target = CadTarget(CoordinateSystems.Agree(new[] { File("Road Layout.dwg", "UTM84-37N") }, Settings()));
            var now = new SiteState { Units = target.Units };

            Assert.Equal(400123.0, target.Position.EastM);
            Assert.Equal(target.Cad.SurveyShared, target.SurveyShared);
            Assert.Equal(Settings().PositionName, target.ActiveSite);
            SetUpChanges changes = SetUpPlanner.Changes(now, target);
            Assert.True(changes.Location);
            Assert.Contains("site location", SetUpPlanner.ReadBack(now, target));
            Assert.Contains("EPSG 32637", SetUpTexts.CoordinateSystemText(target));

            var placed = new SiteState { PositionName = target.Cad.PositionName, LatitudeDeg = target.Cad.LatitudeDeg, LongitudeDeg = target.Cad.LongitudeDeg, PlaceName = target.Cad.PlaceName };
            Assert.False(SetUpPlanner.Changes(placed, target).Location);
        }

        [Fact]
        public void R456_the_control_points_are_rows_of_the_set_up_site_view_and_a_conflict_is_a_stop()
        {
            SetUpTarget target = CadTarget(CoordinateSystems.Agree(new[] { File("a.dwg", "UTM84-37N"), File("b.xml", "32638") }, Settings()));
            SitePosition at = target.Position;

            SetUpSiteView view = SetUpPlanner.View(new SiteState(), target, internalM => SiteCoordinates.ToShared(at, internalM));

            Assert.Equal(5, view.CheckPoints.Count);
            Assert.Equal(5, view.Passing);
            Assert.Equal(target.ControlPoints.Select(point => point.Name), view.CheckPoints.Skip(2).Select(row => row.Point));
            Assert.Contains(view.Stops, stop => stop.Contains("b.xml EPSG 32638"));
            Assert.Contains("a.dwg states UTM84-37N, EPSG 32637", view.Waiting);
        }
    }
}
