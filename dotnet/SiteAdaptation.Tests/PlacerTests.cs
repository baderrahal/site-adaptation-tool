using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Place from plan on fakes. The fake carries out the steps with real matrix math, so the step order and the mirror plane are proven.
    // Nothing here says what Revit itself does with them
    public class PlacerTests
    {
        private const string Folder = @"C:\Prototypes";
        private static readonly double[] Position = { 0, 0, 5 };
        private static readonly double[] SharedPosition = { 1000, 2000, 5 };

        internal sealed class FakeTarget : IPlacerTarget
        {
            public SurveyReading SurveyValue = new SurveyReading { Position = Position, SharedPosition = SharedPosition, AngleToTrueNorthDeg = 0 };
            public List<string> PlotNumbers = new List<string>();
            public List<LinkTypeEntry> Types = new List<LinkTypeEntry>();
            public HashSet<string> Files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(Folder, "VD2.rvt"), Path.Combine(Folder, "DP4.rvt") };
            public HashSet<string> InstanceParameters = new HashSet<string> { "RRE_ZZ_SP_Plot-Number", "CASE", "RRE_ZZ_SP_Prototype-FFL" };
            public HashSet<string> ReadOnlyParameters = new HashSet<string>();
            public List<string> Loads = new List<string>();
            public int Begins;
            public int RollBacks;
            public int Commits;
            public bool CommitFails;
            public int FailPlaceAt = -1;
            public Func<PlacedTransform, PlacedTransform> Distort;
            public List<string> WarningLines = new List<string>();
            public Dictionary<long, PlacedTransform> Instances = new Dictionary<long, PlacedTransform>();
            public Dictionary<long, Dictionary<string, string>> Values = new Dictionary<long, Dictionary<string, string>>();
            public Dictionary<long, string> TypeOf = new Dictionary<long, string>();
            private Dictionary<long, PlacedTransform> savedInstances;
            private List<LinkTypeEntry> savedTypes;
            private int placeCalls;
            private long nextId = 100;

            public string DocumentTitle
            {
                get { return "copy.rvt"; }
            }

            public IList<string> Warnings
            {
                get { return WarningLines; }
            }

            public SurveyReading Survey()
            {
                return SurveyValue;
            }

            public IList<string> LinkPlotNumbers()
            {
                return PlotNumbers;
            }

            public IList<LinkTypeEntry> LinkTypes()
            {
                return Types;
            }

            public bool FileExists(string path)
            {
                return Files.Contains(path);
            }

            public void Begin()
            {
                Begins++;
                savedInstances = new Dictionary<long, PlacedTransform>(Instances);
                savedTypes = new List<LinkTypeEntry>(Types);
            }

            public string LoadLinkType(string fullPath)
            {
                Loads.Add(fullPath);
                string key = "type" + Loads.Count;
                Types.Add(new LinkTypeEntry { FileName = Path.GetFileName(fullPath), Key = key });
                return key;
            }

            public long Place(string linkTypeKey, PlacementSteps steps)
            {
                if (placeCalls++ == FailPlaceAt)
                {
                    throw new InvalidOperationException("the fake fails on purpose");
                }
                long id = nextId++;
                Instances[id] = Apply(steps);
                TypeOf[id] = linkTypeKey;
                Values[id] = new Dictionary<string, string>();
                return id;
            }

            public PlacedTransform ReadBack(long instanceId)
            {
                PlacedTransform found = Instances[instanceId];
                return Distort == null ? found : Distort(found);
            }

            public ParameterWrite Write(long instanceId, string name, string value)
            {
                if (!InstanceParameters.Contains(name))
                {
                    return ParameterWrite.Missing;
                }
                if (ReadOnlyParameters.Contains(name))
                {
                    return ParameterWrite.ReadOnly;
                }
                Values[instanceId][name] = value;
                return ParameterWrite.Written;
            }

            public CommitResult Commit()
            {
                Commits++;
                if (CommitFails)
                {
                    Restore();
                    return new CommitResult { Committed = false, Reason = "RolledBack" };
                }
                return new CommitResult { Committed = true, Reason = "Committed" };
            }

            public void RollBack()
            {
                RollBacks++;
                Restore();
            }

            private void Restore()
            {
                Instances = savedInstances;
                Types.Clear();
                Types.AddRange(savedTypes);
            }
        }

        // Origin to origin, then move, turn about the vertical through the moved point, then mirror across the plane through it
        internal static PlacedTransform Apply(PlacementSteps steps)
        {
            double[] origin = { 0, 0, 0 };
            double[][] axes = { new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, new[] { 0.0, 0, 1 } };
            double[] p = steps.Move;
            origin = Add(origin, p);
            double cos = Math.Cos(steps.RotationRad);
            double sin = Math.Sin(steps.RotationRad);
            Func<double[], double[]> turn = v => new[] { cos * v[0] - sin * v[1], sin * v[0] + cos * v[1], v[2] };
            origin = Add(p, turn(Sub(origin, p)));
            axes = axes.Select(turn).ToArray();
            if (steps.MirrorNormal != null)
            {
                double[] n = steps.MirrorNormal;
                Func<double[], double[]> flip = v => Sub(v, Scale(n, 2 * Dot(v, n)));
                origin = Add(p, flip(Sub(origin, p)));
                axes = axes.Select(flip).ToArray();
            }
            return new PlacedTransform { Origin = origin, AxisX = axes[0], AxisY = axes[1], AxisZ = axes[2] };
        }

        private static double[] Add(double[] a, double[] b)
        {
            return new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
        }

        private static double[] Sub(double[] a, double[] b)
        {
            return new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        }

        private static double[] Scale(double[] a, double k)
        {
            return new[] { a[0] * k, a[1] * k, a[2] * k };
        }

        private static double Dot(double[] a, double[] b)
        {
            return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        }

        private static Sourced<T> S<T>(T value)
        {
            return new Sourced<T> { Value = value, Source = "pastJob" };
        }

        internal static PlanPlot Plot(string plot, double e, double n, double ffl, double rotation = 30, bool mirrored = false, string file = "VD2.rvt")
        {
            return new PlanPlot
            {
                Plot = plot,
                Unit = S("VD2"),
                PrototypeFile = S(file),
                Case = S("A"),
                Insert = S(new[] { e, n, ffl }),
                RotationDeg = S<double?>(rotation),
                Mirrored = S<bool?>(mirrored),
                Levels = new PlanLevels { Ffl = S<double?>(ffl) },
                Parameters = new Dictionary<string, Sourced<string>> { { "RRE_ZZ_SP_Plot-Number", S(plot) }, { "CASE", S("A") } },
            };
        }

        internal static PlanFile Plan(params PlanPlot[] plots)
        {
            return new PlanFile
            {
                Format = PlanFile.FormatName,
                FormatVersion = PlanFile.Version,
                Neighbourhood = "AAA",
                SurveyPoint = new PlanSurveyPoint { Position = (double[])Position.Clone(), SharedPosition = (double[])SharedPosition.Clone() },
                AngleToTrueNorthDeg = 0,
                PrototypeFolder = Folder,
                Plots = plots.ToList(),
            };
        }

        private static PlacerReport Real(PlanFile plan, IPlacerTarget target)
        {
            return Placer.Run(plan, "plan.json", target, true);
        }

        private static string RepoFile(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return Path.Combine(new[] { folder }.Concat(parts).ToArray());
        }

        // R80 plan file

        [Fact]
        public void R80_the_placer_reads_the_plan_shape_the_builder_writes()
        {
            string json = @"{""format"": ""site-adaptation-plan"", ""formatVersion"": 1, ""neighbourhood"": ""AAA"",
                ""coordinateSystem"": {""name"": ""shared"", ""lengthUnit"": ""m"", ""angleUnit"": ""deg""},
                ""surveyPoint"": {""position"": [0, 0, 5], ""sharedPosition"": [1000, 2000, 5]}, ""angleToTrueNorthDeg"": 0,
                ""prototypeFolder"": ""C:\\Prototypes"", ""sources"": {}, ""summary"": {}, ""missing"": [], ""conflicts"": [],
                ""plots"": [{""plot"": ""101"", ""unit"": {""value"": ""VD2"", ""source"": ""pastJob""},
                  ""case"": {""value"": ""A"", ""source"": ""pastJob""}, ""prototypeFile"": {""value"": ""VD2.rvt"", ""source"": ""pastJob""},
                  ""insert"": {""value"": [1003.5, 2004.25, 10.0], ""source"": ""pastJob""}, ""rotationDeg"": {""value"": 210.106, ""source"": ""pastJob""},
                  ""mirrored"": {""value"": true, ""source"": ""pastJob""},
                  ""levels"": {""ffl"": {""value"": 10.0, ""source"": ""pastJob""}, ""entry"": {""value"": 9.8123, ""source"": ""pastJob""}, ""steps"": {""value"": 1, ""source"": ""pastJob""}},
                  ""parameters"": {""RRE_ZZ_SP_Fa\u00e7ade-Color"": {""value"": ""XX"", ""source"": ""pastJob""}},
                  ""floors"": {""generic"": {""z"": {""value"": 10.0, ""source"": ""computed""}}}}]}";
            PlanFile plan = PlanFile.Parse(json);

            Assert.Null(Placer.CheckFormat(plan));
            PlanPlot plot = plan.Plots.Single();
            Assert.Equal(new[] { 1003.5, 2004.25, 10.0 }, plot.Insert.Value);
            Assert.Equal(210.106, plot.RotationDeg.Value.Value);
            Assert.True(plot.Mirrored.Value.Value);
            Assert.Equal(10.0, plot.Levels.Ffl.Value.Value);
            Assert.Equal(1, plot.Levels.Steps.Value.Value);
            Assert.Equal("XX", plot.Parameters["RRE_ZZ_SP_Fa\u00e7ade-Color"].Value);
            Assert.Equal("pastJob", plot.Insert.Source);
        }

        // R83 coordinates check

        [Fact]
        public void R83_a_survey_point_more_than_1_mm_apart_on_any_axis_stops_and_places_nothing()
        {
            for (int axis = 0; axis < 3; axis++)
            {
                var target = new FakeTarget();
                double[] shared = (double[])SharedPosition.Clone();
                shared[axis] += 0.0011;
                target.SurveyValue = new SurveyReading { Position = Position, SharedPosition = shared, AngleToTrueNorthDeg = 0 };
                PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10)), target);

                Assert.True(report.Stopped);
                Assert.False(report.Coordinates.Match);
                Assert.Contains("shared position " + new[] { "X", "Y", "Z" }[axis], report.Failure);
                Assert.Equal(0, target.Begins);
                Assert.Empty(target.Instances);
                Assert.Equal(0, report.Counts.Placed);
            }
        }

        [Fact]
        public void R83_the_internal_survey_position_is_checked_too_and_within_1_mm_goes_on()
        {
            var moved = new FakeTarget();
            moved.SurveyValue = new SurveyReading { Position = new[] { 0.002, 0, 5 }, SharedPosition = SharedPosition, AngleToTrueNorthDeg = 0 };
            Assert.Contains("survey point position X", Real(Plan(Plot("101", 1003, 2004, 10)), moved).Failure);

            var near = new FakeTarget();
            near.SurveyValue = new SurveyReading { Position = new[] { 0.0009, 0, 5 }, SharedPosition = new[] { 1000.0009, 2000, 5 }, AngleToTrueNorthDeg = 0 };
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10)), near);
            Assert.False(report.Stopped);
            Assert.True(report.Coordinates.Match);
            Assert.Equal(1, report.Counts.Placed);
        }

        [Fact]
        public void R83_an_angle_to_true_north_that_differs_or_is_not_zero_stops()
        {
            var turned = new FakeTarget();
            turned.SurveyValue.AngleToTrueNorthDeg = 12.5;
            Assert.Contains("angle to true north", Real(Plan(Plot("101", 1003, 2004, 10)), turned).Failure);

            var both = new FakeTarget();
            both.SurveyValue.AngleToTrueNorthDeg = 12.5;
            PlanFile plan = Plan(Plot("101", 1003, 2004, 10));
            plan.AngleToTrueNorthDeg = 12.5;
            PlacerReport report = Real(plan, both);
            Assert.True(report.Stopped);
            Assert.Contains("only 0 is handled", report.Failure);

            var wrapped = new FakeTarget();
            wrapped.SurveyValue.AngleToTrueNorthDeg = 359.9995;
            Assert.False(Real(Plan(Plot("101", 1003, 2004, 10)), wrapped).Stopped);
        }

        [Fact]
        public void R83_a_plan_of_another_format_or_version_stops()
        {
            PlanFile plan = Plan(Plot("101", 1003, 2004, 10));
            plan.FormatVersion = 2;
            PlacerReport report = Real(plan, new FakeTarget());
            Assert.True(report.Stopped);
            Assert.Contains("version 1", report.Failure);

            PlanFile other = Plan(Plot("101", 1003, 2004, 10));
            other.Format = "something";
            Assert.True(Real(other, new FakeTarget()).Stopped);
        }

        [Fact]
        public void R83_the_placer_never_changes_the_project_coordinates()
        {
            string[] banned = { "SetProjectPosition", "new ProjectPosition", ".SharedPosition =", ".Position =", "BASEPOINT_", "MoveBasePoint", "MoveOriginToHostOrigin", "AcquireCoordinates", "PublishCoordinates", "ActiveProjectLocation =", "ProjectLocation.Create", "Duplicate(" };
            foreach (string source in Directory.GetFiles(RepoFile("dotnet", "SiteAdaptation.Placer"), "*.cs"))
            {
                string text = File.ReadAllText(source);
                foreach (string word in banned)
                {
                    Assert.False(text.Contains(word), Path.GetFileName(source) + " contains " + word);
                }
            }
        }

        // R84 villa link placement

        [Fact]
        public void R84_the_internal_point_is_the_insert_minus_the_survey_offset()
        {
            Assert.Equal(new[] { 3.0, 4.5, 10 }, PlacementMath.InternalPoint(new[] { 1003.0, 2004.5, 10 }, new[] { 1000.0, 2000, 0 }));
        }

        [Fact]
        public void R84_position_rotation_and_z_of_each_placed_link_match_the_plan()
        {
            var target = new FakeTarget();
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10.25, 30), Plot("102", 1050, 2010, 11.5, 300.5)), target);

            Assert.Equal(2, report.Counts.Placed);
            PlacedTransform first = target.Instances[report.Placed[0].InstanceId.Value];
            Assert.Equal(3, first.Origin[0], 9);
            Assert.Equal(4, first.Origin[1], 9);
            Assert.Equal(10.25, first.Origin[2], 9);
            Assert.Equal(30, LinkMath.PlanAngleDeg(first.AxisX), 6);
            PlacedTransform second = target.Instances[report.Placed[1].InstanceId.Value];
            Assert.Equal(11.5, second.Origin[2], 9);
            Assert.Equal(300.5, LinkMath.PlanAngleDeg(second.AxisX), 6);
            Assert.False(LinkMath.Describe(second.Origin, second.AxisX, second.AxisY, second.AxisZ).Mirrored);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(30.106)]
        [InlineData(90)]
        [InlineData(210.106)]
        [InlineData(359.5)]
        public void R84_a_mirrored_link_keeps_its_origin_and_x_axis_and_flips_y(double rotation)
        {
            PlacementSteps steps = PlacementMath.Steps(new[] { 3.0, 4, 10 }, rotation, true);
            PlacedTransform placed = Apply(steps);
            double turn = rotation * Math.PI / 180;

            Assert.Equal(new[] { 3.0, 4, 10 }, placed.Origin.Select(value => Math.Round(value, 9)).ToArray());
            Assert.Equal(Math.Cos(turn), placed.AxisX[0], 9);
            Assert.Equal(Math.Sin(turn), placed.AxisX[1], 9);
            Assert.Equal(Math.Sin(turn), placed.AxisY[0], 9);
            Assert.Equal(-Math.Cos(turn), placed.AxisY[1], 9);
            Assert.Equal(1, placed.AxisZ[2], 9);
            LinkTransform seen = LinkMath.Describe(placed.Origin, placed.AxisX, placed.AxisY, placed.AxisZ);
            Assert.True(seen.Mirrored);
            Assert.Equal(0, Placer.AngleGap(seen.RotationDeg, rotation), 6);
            Assert.Null(PlacementMath.Mismatch(placed, new[] { 3.0, 4, 10 }, rotation, true));
        }

        [Fact]
        public void R84_a_link_type_is_loaded_once_per_file_and_an_existing_type_is_used_by_file_name_without_case()
        {
            var target = new FakeTarget();
            target.Types.Add(new LinkTypeEntry { FileName = "dp4.RVT", Key = "existing" });
            PlanFile plan = Plan(Plot("101", 1003, 2004, 10), Plot("102", 1020, 2004, 10), Plot("103", 1040, 2004, 10, file: "DP4.rvt"), Plot("104", 1060, 2004, 10));
            PlacerReport report = Real(plan, target);

            Assert.Equal(4, report.Counts.Placed);
            Assert.Equal(new[] { Path.Combine(Folder, "VD2.rvt") }, target.Loads);
            Assert.Equal("existing", target.TypeOf[report.Placed[2].InstanceId.Value]);
            Assert.Single(report.Placed.Select(placed => target.TypeOf[placed.InstanceId.Value]).Where(key => key != "existing").Distinct());
        }

        [Fact]
        public void R84_plots_that_lack_a_link_value_or_whose_insert_z_is_not_the_ffl_or_whose_file_is_missing_are_skipped()
        {
            PlanPlot noRotation = Plot("101", 1003, 2004, 10);
            noRotation.RotationDeg = null;
            PlanPlot zOff = Plot("102", 1020, 2004, 10);
            zOff.Insert.Value[2] = 10.0015;
            PlanPlot noFile = Plot("103", 1040, 2004, 10, file: "VL9.rvt");
            PlanPlot path = Plot("104", 1060, 2004, 10, file: @"..\VD2.rvt");
            PlanPlot zNear = Plot("105", 1080, 2004, 10);
            zNear.Insert.Value[2] = 10.0009;
            var target = new FakeTarget();
            PlacerReport report = Real(Plan(noRotation, zOff, noFile, path, zNear), target);

            Dictionary<string, string> reasons = report.Skipped.ToDictionary(item => item.Plot, item => item.Reason);
            Assert.Equal("the plan lacks rotationDeg", reasons["101"]);
            Assert.StartsWith("insert Z and FFL differ by 1.5 mm", reasons["102"]);
            Assert.StartsWith("prototype file not found", reasons["103"]);
            Assert.StartsWith("prototypeFile must be a file name", reasons["104"]);
            Assert.Equal(new[] { "105" }, report.Placed.Select(placed => placed.Plot).ToArray());
        }

        [Fact]
        public void R84_a_plot_listed_twice_in_the_plan_is_placed_once()
        {
            var target = new FakeTarget();
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10), Plot(" 101", 1020, 2004, 10)), target);

            Assert.Single(target.Instances);
            Assert.Equal("the plan lists this plot twice, the first one is used", report.Skipped.Single().Reason);
        }

        [Fact]
        public void R84_a_read_back_that_does_not_match_the_plan_rolls_the_run_back()
        {
            var target = new FakeTarget();
            target.Distort = found => new PlacedTransform { Origin = new[] { found.Origin[0] + 0.002, found.Origin[1], found.Origin[2] }, AxisX = found.AxisX, AxisY = found.AxisY, AxisZ = found.AxisZ };
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10)), target);

            Assert.True(report.RolledBack);
            Assert.Contains("origin is 2 mm off", report.Failure);
            Assert.Empty(target.Instances);
            Assert.Equal(1, target.RollBacks);
        }

        [Fact]
        public void R84_a_mirror_flag_or_rotation_that_does_not_match_is_found_on_read_back()
        {
            PlacedTransform plain = Apply(PlacementMath.Steps(new[] { 3.0, 4, 10 }, 30, false));
            Assert.Contains("mirrored is False", PlacementMath.Mismatch(plain, new[] { 3.0, 4, 10 }, 30, true));
            Assert.Contains("rotation is 30", PlacementMath.Mismatch(plain, new[] { 3.0, 4, 10 }, 30.02, false));
            Assert.Null(PlacementMath.Mismatch(plain, new[] { 3.0, 4, 10 }, 30.009, false));
        }

        [Fact]
        public void R84_the_revit_side_mirrors_with_mirror_elements_and_no_copy()
        {
            string text = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.Placer", "RevitPlacerTarget.cs"));

            Assert.Matches(@"ElementTransformUtils\.MirrorElements\(document, ids, Plane\.CreateByNormalAndOrigin\([^;]*\), false\)", text);
            Assert.DoesNotContain("MirrorElement(", text);
            Assert.Contains("CanMirrorElements", text);
            Assert.Contains("ImportPlacement.Origin", text);
        }

        [Fact]
        public void R84_the_placer_never_changes_the_reader()
        {
            string project = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.Placer", "SiteAdaptation.Placer.csproj"));

            Assert.DoesNotContain("RevitPrototypeReader", project);
            Assert.Contains("SiteAdaptation.Common", project);
        }

        // R85 link parameters

        [Fact]
        public void R85_parameters_are_written_by_name_and_a_missing_or_read_only_one_is_an_issue_not_a_stop()
        {
            PlanPlot plot = Plot("101", 1003, 2004, 10);
            plot.Parameters["RRE_ZZ_SP_RVT-Name"] = S("101 | VD2");
            PlanPlot second = Plot("102", 1020, 2004, 10);
            var target = new FakeTarget();
            target.ReadOnlyParameters.Add("CASE");
            PlacerReport report = Real(Plan(plot, second), target);

            Assert.Equal(2, report.Counts.Placed);
            Assert.False(report.RolledBack);
            Assert.Equal("101", target.Values[report.Placed[0].InstanceId.Value]["RRE_ZZ_SP_Plot-Number"]);
            Assert.Equal(2, report.Counts.ParametersWritten);
            Assert.Contains("plot 101: the link instance has no parameter RRE_ZZ_SP_RVT-Name", report.Issues);
            Assert.Contains("plot 102: CASE is read only", report.Issues);
            Assert.Equal(new[] { "RRE_ZZ_SP_Plot-Number" }, report.Placed[1].Parameters);
        }

        [Fact]
        public void R85_the_prototype_ffl_parameter_is_never_written()
        {
            PlanPlot plot = Plot("101", 1003, 2004, 10);
            plot.Parameters["RRE_ZZ_SP_Prototype-FFL"] = S("10.000");
            var target = new FakeTarget();
            PlacerReport report = Real(Plan(plot), target);

            Assert.False(target.Values[report.Placed[0].InstanceId.Value].ContainsKey("RRE_ZZ_SP_Prototype-FFL"));
            Assert.Contains("plot 101: RRE_ZZ_SP_Prototype-FFL is in the plan and is never written", report.Issues);
            PlacerReport dry = Placer.Run(Plan(plot), "plan.json", new FakeTarget(), false);
            Assert.DoesNotContain("RRE_ZZ_SP_Prototype-FFL", dry.Placed.Single().Parameters);
        }

        // R86 dry run and one transaction

        [Fact]
        public void R86_the_dry_run_changes_nothing_opens_no_transaction_and_loads_no_link()
        {
            var target = new FakeTarget();
            target.PlotNumbers.Add("102");
            PlacerReport report = Placer.Run(Plan(Plot("101", 1003, 2004, 10, 210.106, true), Plot("102", 1020, 2004, 10)), "plan.json", target, false);

            Assert.Equal("dryRun", report.Mode);
            Assert.Equal(0, target.Begins);
            Assert.Empty(target.Loads);
            Assert.Empty(target.Instances);
            PlacedPlot would = report.Placed.Single();
            Assert.Equal("101", would.Plot);
            Assert.Equal("VD2.rvt", would.File);
            Assert.Equal(210.106, would.RotationDeg);
            Assert.True(would.Mirrored);
            Assert.Null(would.InstanceId);
            Assert.Equal(new[] { "RRE_ZZ_SP_Plot-Number", "CASE" }, would.Parameters);
            Assert.Equal("102", report.Skipped.Single().Plot);
        }

        [Fact]
        public void R86_a_failure_on_any_plot_rolls_the_whole_run_back()
        {
            var target = new FakeTarget { FailPlaceAt = 1 };
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10), Plot("102", 1020, 2004, 10, file: "DP4.rvt")), target);

            Assert.True(report.RolledBack);
            Assert.Contains("the fake fails on purpose", report.Failure);
            Assert.Equal(1, target.RollBacks);
            Assert.Equal(0, target.Commits);
            Assert.Empty(target.Instances);
            Assert.Empty(target.Types);
            Assert.Empty(report.Placed);
            Assert.Equal(0, report.Counts.ParametersWritten);
        }

        [Fact]
        public void R86_a_commit_that_does_not_end_committed_keeps_nothing()
        {
            var target = new FakeTarget { CommitFails = true };
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10)), target);

            Assert.True(report.RolledBack);
            Assert.Contains("did not commit", report.Failure);
            Assert.Empty(target.Instances);
            Assert.Equal(0, report.Counts.Placed);
        }

        [Fact]
        public void R86_a_link_that_does_not_load_rolls_the_run_back()
        {
            var target = new NoLoadTarget();
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10)), target);

            Assert.True(report.RolledBack);
            Assert.Contains("did not load", report.Failure);
            Assert.Equal(1, target.RollBacks);
        }

        private sealed class NoLoadTarget : IPlacerTarget
        {
            private readonly FakeTarget inner = new FakeTarget();
            public int RollBacks;
            public string DocumentTitle { get { return "copy.rvt"; } }
            public IList<string> Warnings { get { return new List<string>(); } }
            public SurveyReading Survey() { return inner.Survey(); }
            public IList<string> LinkPlotNumbers() { return new List<string>(); }
            public IList<LinkTypeEntry> LinkTypes() { return new List<LinkTypeEntry>(); }
            public bool FileExists(string path) { return true; }
            public void Begin() { }
            public string LoadLinkType(string fullPath) { return null; }
            public long Place(string linkTypeKey, PlacementSteps steps) { throw new InvalidOperationException("never reached"); }
            public PlacedTransform ReadBack(long instanceId) { return null; }
            public ParameterWrite Write(long instanceId, string name, string value) { return ParameterWrite.Missing; }
            public CommitResult Commit() { return new CommitResult { Committed = true }; }
            public void RollBack() { RollBacks++; }
        }

        [Fact]
        public void R86_a_revit_warning_is_an_issue_and_does_not_stop_the_run()
        {
            var target = new FakeTarget();
            target.WarningLines.Add("Elements overlap");
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10)), target);

            Assert.False(report.RolledBack);
            Assert.Equal(1, report.Counts.Placed);
            Assert.Contains("Revit warning: Elements overlap", report.Issues);
        }

        [Fact]
        public void R86_the_command_runs_in_manual_mode_and_the_real_run_is_the_second_choice()
        {
            string text = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.Placer", "PlaceFromPlanCommand.cs"));

            Assert.Contains("[Transaction(TransactionMode.Manual)]", text);
            Assert.Contains("answer == TaskDialogResult.CommandLink2", text);
            Assert.Contains("CommandLink1, \"Dry run\"", text);
            int transactions = Regex.Matches(File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.Placer", "RevitPlacerTarget.cs")), "new Transaction\\(").Count;
            Assert.Equal(1, transactions);
        }

        // R87 report and skip

        [Fact]
        public void R87_a_plot_already_held_by_a_link_instance_is_skipped_and_never_placed_twice()
        {
            var target = new FakeTarget();
            target.PlotNumbers.AddRange(new[] { null, " 101 " });
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10), Plot("102", 1020, 2004, 10)), target);

            Assert.Equal("already placed, a link instance of the document has this plot number", report.Skipped.Single(item => item.Plot == "101").Reason);
            Assert.Equal(new[] { "102" }, report.Placed.Select(placed => placed.Plot).ToArray());
        }

        [Fact]
        public void R87_the_report_holds_mode_plan_document_coordinates_counts_placed_skipped_issues_and_rollback()
        {
            var target = new FakeTarget();
            PlanPlot skipped = Plot("102", 1020, 2004, 10);
            skipped.Mirrored = null;
            PlacerReport report = Real(Plan(Plot("101", 1003, 2004, 10, 30, true), skipped), target);
            JObject json = JObject.Parse(JsonOut.ToJson(report));

            Assert.Equal("real", (string)json["mode"]);
            Assert.Equal("plan.json", (string)json["planFile"]);
            Assert.Equal("copy.rvt", (string)json["document"]);
            Assert.True((bool)json["coordinates"]["match"]);
            Assert.Equal(new[] { 1000.0, 2000, 5 }, json["coordinates"]["documentSharedPosition"].Select(value => (double)value).ToArray());
            Assert.Equal(2, (int)json["counts"]["plotsInPlan"]);
            Assert.Equal(1, (int)json["counts"]["placed"]);
            Assert.Equal(1, (int)json["counts"]["skipped"]);
            Assert.Equal(2, (int)json["counts"]["parametersWritten"]);
            Assert.False((bool)json["rolledBack"]);
            Assert.False((bool)json["stopped"]);
            Assert.Equal(100, (long)json["placed"][0]["instanceId"]);
            Assert.True((bool)json["placed"][0]["mirrored"]);
            Assert.Equal("the plan lacks mirrored", (string)json["skipped"][0]["reason"]);
            Assert.NotNull(json["issues"]);
            Assert.Null(json["parametersWritten"]);
        }

        [Fact]
        public void R87_the_command_writes_the_report_after_every_run_a_stop_included()
        {
            string text = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.Placer", "PlaceFromPlanCommand.cs"));
            int run = text.IndexOf("Common.Placer.Run(", StringComparison.Ordinal);
            int write = text.IndexOf("JsonOut.Write(reportPath, report)", StringComparison.Ordinal);

            Assert.True(run > 0 && write > run);
            Assert.DoesNotContain("return", text.Substring(run, write - run));
            Assert.Contains("\"placer_report.json\"", text);
        }

        // R88 placer install

        [Fact]
        public void R88_the_manifest_names_the_full_dll_path_in_its_own_folder_the_command_class_and_its_own_add_in_id()
        {
            string manifest = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.Placer", "SiteAdaptationPlacer_Revit2024.addin"));
            string reader = File.ReadAllText(RepoFile("dotnet", "RevitPrototypeReader", "SiteAdaptation.addin"));
            string command = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.Placer", "PlaceFromPlanCommand.cs"));

            Assert.Matches(@"<Assembly>C:\\Users\\[^\\]+\\AppData\\Roaming\\Autodesk\\Revit\\Addins\\2024\\SiteAdaptationPlacer\\SiteAdaptation\.Placer\.dll</Assembly>", manifest);
            Assert.Contains("<FullClassName>SiteAdaptation.Placer.PlaceFromPlanCommand</FullClassName>", manifest);
            Assert.Contains("<AvailabilityClassName>SiteAdaptation.Placer.ProjectOpen</AvailabilityClassName>", manifest);
            Assert.Contains("namespace SiteAdaptation.Placer", command);
            Assert.Contains("public class PlaceFromPlanCommand : IExternalCommand", command);
            Assert.Contains("public class ProjectOpen : IExternalCommandAvailability", command);
            string id = Regex.Match(manifest, "<AddInId>([^<]+)</AddInId>").Groups[1].Value;
            Assert.True(Guid.TryParse(id, out _));
            Assert.DoesNotContain(id, reader);
        }

        [Fact]
        public void R88_the_placer_builds_for_net48_with_its_own_assembly_name_and_sits_in_the_solution()
        {
            string project = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.Placer", "SiteAdaptation.Placer.csproj"));
            string props = File.ReadAllText(RepoFile("dotnet", "Directory.Build.props"));
            string solution = File.ReadAllText(RepoFile("dotnet", "SiteAdaptation.sln"));

            Assert.Contains("<AssemblyName>SiteAdaptation.Placer</AssemblyName>", project);
            Assert.Contains("Nice3point.Revit.Api.RevitAPI\" Version=\"2024.", project);
            Assert.Contains("<TargetFramework>net48</TargetFramework>", props);
            Assert.Contains("SiteAdaptation.Placer\\SiteAdaptation.Placer.csproj", solution);
        }
    }
}
