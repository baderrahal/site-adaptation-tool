using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Tags;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R570 to R574. The toposolid of button 5 on a fake document and a small TIN: the surface is a plane over shared x 900 to 1400, y 1900
    // to 2200, level 10 + 0.001 (x - 1000), with one point of no visible face. No Revit type, so these run on net8.0 too
    public class R570_RoadTopographyTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "r570_" + Guid.NewGuid().ToString("N"));

        public R570_RoadTopographyTests()
        {
            Directory.CreateDirectory(work);
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private static double Road(double x)
        {
            return 10 + 0.001 * (x - 1000);
        }

        // A grid of 50 m with a far point no face uses, as a LandXML can hold
        internal static LandXmlSurface Tin(double fromX = 900, double toX = 1400, double fromY = 1900, double toY = 2200)
        {
            var points = new List<double>();
            var faces = new List<int>();
            int columns = (int)((toX - fromX) / 50) + 1, rows = (int)((toY - fromY) / 50) + 1;
            for (int column = 0; column < columns; column++)
            {
                for (int row = 0; row < rows; row++)
                {
                    double x = fromX + 50 * column;
                    points.AddRange(new[] { x, fromY + 50 * row, Road(x) });
                }
            }
            for (int column = 0; column < columns - 1; column++)
            {
                for (int row = 0; row < rows - 1; row++)
                {
                    int a = column * rows + row, b = a + rows;
                    faces.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
                }
            }
            points.AddRange(new[] { 1100.0, 2050.5, 99.0 });
            return new LandXmlSurface { Name = "Road", Points = points.ToArray(), Faces = faces.ToArray() };
        }

        internal static RoadTopographySettings Settings()
        {
            return new RoadTopographySettings { TypeName = "Road surface", Surface = "Road", MarginM = 5, LimitMm = 5, MinCoverShare = 0.5, PointGapM = 0.01, MaxPoints = 5000, SnapM = 0.005 };
        }

        private static JObject Config()
        {
            return JObject.Parse(@"{""siteBuild"": {""snapM"": 0.005}, ""floors"": {""roadTopography"": {""typeName"": ""Road surface"", ""surface"": ""Road"", ""marginM"": 5.0, ""limitMm"": 5.0,
                ""minCoverShare"": 0.5, ""pointGapM"": 0.01, ""maxPoints"": 5000}}}");
        }

        // The ramp of plot n on the road at both front corners, off metres above it when off is given
        internal static PlanPlot Plot(string number, double off = 0)
        {
            PlanPlot plot = FloorPlans.Plot(number);
            double[][] corners = plot.Floors.Ramp.Corners.Value.ToArray();
            corners[0] = new[] { corners[0][0], corners[0][1], Road(corners[0][0]) + off };
            corners[1] = new[] { corners[1][0], corners[1][1], Road(corners[1][0]) + off };
            plot.Floors.Ramp = FloorPlans.Ramp("VD4-" + number, corners);
            return plot;
        }

        private static ToposolidPlan PlanOf(LandXmlSurface surface, IEnumerable<double[]> outlines, IEnumerable<RampEdge> edges, RoadTopographySettings settings)
        {
            return RoadTopography.Plan(new RoadSurfaceRead { Surface = surface, Settings = settings }, outlines, edges);
        }

        private FloorsRun Make(FakeFloors document, PlanFile plan, bool on = true, LandXmlSurface surface = null, Func<string, bool> inScope = null, string sha = "abc", RoadTopographySettings settings = null)
        {
            return new FloorsRun(document, context => new FloorInputs
            {
                Plan = plan, PlanPath = Path.Combine(work, "plan_012.json"), Profile = "parsons2024Rev6", InScope = inScope,
                RoadTopography = on ? () => new RoadSurfaceRead { Settings = settings ?? Settings(), Surface = surface ?? Tin(), Mark = new InputMark { Name = "Road.xml", Size = 5, WriteTimeUtc = "t", Sha256 = sha } } : (Func<RoadSurfaceRead>)null,
            });
        }

        // The model of the flow for the toposolid step: a roll back takes back what the document holds of toposolids, as Revit does
        private sealed class SolidModel : IModel
        {
            private readonly FakeFloors document;

            public SolidModel(FakeFloors document)
            {
                this.document = document;
            }

            public int Started;
            public int RolledBack;
            public string RefuseCommit;
            public string[] Warn = new string[0];

            public IModelTransaction Start(string name)
            {
                Started++;
                return new SolidTransaction(this, document.Solids.ToList());
            }

            private sealed class SolidTransaction : IModelTransaction, IKnownWarningTransaction
            {
                private readonly SolidModel model;
                private readonly List<FakeFloors.Solid> before;
                private bool open = true;

                public SolidTransaction(SolidModel model, List<FakeFloors.Solid> before)
                {
                    this.model = model;
                    this.before = before;
                }

                private KnownWarningList known;

                public void AcceptKnownWarnings(KnownWarningList list)
                {
                    known = list;
                }

                public IReadOnlyList<AcceptedWarning> AcceptedWarnings => model.Warn.Where(text => known != null && known.Knows(text)).Select(text => known.Decide(text, new List<WarningElement>())).ToList();
                public IReadOnlyList<string> Warnings => model.Warn.Where(text => known == null || !known.Knows(text)).ToList();
                public IReadOnlyList<string> Errors => model.RefuseCommit == null ? new string[0] : new[] { model.RefuseCommit };

                public bool Commit()
                {
                    if (model.RefuseCommit != null)
                    {
                        return false;
                    }
                    open = false;
                    return true;
                }

                public void RollBack()
                {
                    if (open)
                    {
                        open = false;
                        model.RolledBack++;
                        model.document.Solids.Clear();
                        model.document.Solids.AddRange(before);
                    }
                }

                public void Dispose()
                {
                    RollBack();
                }
            }
        }

        private SolidModel solidModel;

        private RunContext Context(string runId = "5_1", bool dry = false)
        {
            return new RunContext(5, "5 Floors and ramps", runId, work, dry ? Path.Combine(work, "dry", runId) : null, true, new RunInput[0], new Dictionary<string, string>());
        }

        // Preview, Apply and the step after the commit, as the flow runs them
        private IReadOnlyList<string> Apply(FloorsRun run, RunContext context, FakeFloors document)
        {
            ButtonBody body = run.Body();
            body.Preview(context);
            var issues = new List<string>(body.Apply(context));
            solidModel = solidModel ?? new SolidModel(document);
            context.Model = solidModel;
            body.AfterCommit(context);
            issues.AddRange(context.IssuesAfterCommit);
            return issues;
        }

        private static IEnumerable<string> Topo(IEnumerable<string> lines)
        {
            return lines.Where(line => line.StartsWith(RoadTopography.Line, StringComparison.Ordinal));
        }

        // What the flow does at its commit, as the outputs of a run stand under temporary names until then
        private static JObject Report(RunContext context)
        {
            KeyValuePair<string, string> output = context.Outputs.Last(item => Path.GetFileName(item.Value) == "toposolid_012.json");
            return JObject.Parse(File.ReadAllText(output.Key));
        }

        private static RampEdge Edge(string plot, double x, double y)
        {
            return new RampEdge { Plot = plot, Start = new[] { x, y, Road(x) }, End = new[] { x + 10, y, Road(x + 10) } };
        }

        [Fact]
        public void R570_the_settings_are_read_from_the_block_of_config()
        {
            RoadTopographySettings read = RoadTopographySettings.Of(Config());

            Assert.Equal("Road surface", read.TypeName);
            Assert.Equal("Road", read.Surface);
            Assert.Equal(5.0, read.MarginM);
            Assert.Equal(5.0, read.LimitMm);
            Assert.Equal(0.5, read.MinCoverShare);
            Assert.Equal(0.01, read.PointGapM);
            Assert.Equal(5000, read.MaxPoints);
            Assert.Equal(0.005, read.SnapM);
        }

        [Theory]
        [InlineData("typeName")]
        [InlineData("surface")]
        [InlineData("marginM")]
        [InlineData("limitMm")]
        [InlineData("minCoverShare")]
        [InlineData("pointGapM")]
        [InlineData("maxPoints")]
        public void R570_a_missing_key_is_refused_by_its_name(string key)
        {
            JObject config = Config();
            ((JObject)config["floors"]["roadTopography"]).Remove(key);

            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => RoadTopographySettings.Of(config));
            Assert.Contains("floors.roadTopography." + key, refused.Message);
        }

        [Fact]
        public void R570_config_of_the_repo_names_the_type_and_surface_with_a_why_for_each()
        {
            JObject config = JObject.Parse(File.ReadAllText(RepoFile("config", "project.json")));
            RoadTopographySettings read = RoadTopographySettings.Of(config);
            JObject block = (JObject)config["floors"]["roadTopography"];

            Assert.Equal((string)config["designBasis"]["roadLevel"]["surfaces"][0], read.Surface);
            foreach (string key in new[] { "typeName", "surface", "marginM", "limitMm", "minCoverShare", "pointGapM", "maxPoints" })
            {
                Assert.False(string.IsNullOrWhiteSpace((string)block[key + "Why"]), key);
            }
        }

        [Fact]
        public void R571_the_boundary_is_the_hull_of_the_plots_grown_by_the_margin()
        {
            var outlines = new[] { new[] { 1000.0, 2000 }, new[] { 1010.0, 2000 }, new[] { 1005.0, 2005 }, new[] { 1010.0, 2020 }, new[] { 1000.0, 2020 } };

            ToposolidPlan plan = PlanOf(Tin(), outlines, new RampEdge[0], Settings());

            Assert.Null(plan.Refused);
            Assert.Equal(new[] { "995,1995", "1015,1995", "1015,2025", "995,2025" }, plan.Boundary.Select(point => Math.Round(point[0], 6) + "," + Math.Round(point[1], 6)));
        }

        [Fact]
        public void R571_the_points_are_the_vertices_inside_and_the_levels_at_the_corners()
        {
            var outlines = new[] { new[] { 1040.0, 2040 }, new[] { 1060.0, 2040 }, new[] { 1060.0, 2060 }, new[] { 1040.0, 2060 } };

            ToposolidPlan plan = PlanOf(Tin(), outlines, new RampEdge[0], Settings());

            Assert.Equal(1, plan.VerticesInside);
            // The four corners each at the level of the plane under them, then the one vertex of the grid inside
            Assert.Equal(5, plan.Points.Count);
            Assert.All(plan.Points, point => Assert.Equal(Road(point[0]), point[2], 9));
            Assert.Contains(plan.Points, point => point[0] == 1050 && point[1] == 2050);
            Assert.Equal(0, plan.CornersOffSurface);
        }

        [Fact]
        public void R571_a_point_of_no_visible_face_is_no_vertex_of_the_surface()
        {
            var outlines = new[] { new[] { 1095.0, 2045 }, new[] { 1105.0, 2045 }, new[] { 1105.0, 2055 }, new[] { 1095.0, 2055 } };

            ToposolidPlan plan = PlanOf(Tin(), outlines, new RampEdge[0], Settings());

            Assert.DoesNotContain(plan.Points, point => point[2] == 99.0);
        }

        [Fact]
        public void R571_a_corner_off_the_surface_takes_the_level_of_the_nearest_vertex_and_is_counted()
        {
            // The hull grown by 5 m stands 3 m over the west end of the surface at x 900, so its west corners lie off it
            var outlines = new[] { new[] { 902.0, 2000 }, new[] { 1010.0, 2000 }, new[] { 1010.0, 2020 }, new[] { 902.0, 2020 } };

            ToposolidPlan plan = PlanOf(Tin(), outlines, new RampEdge[0], Settings());

            Assert.Null(plan.Refused);
            Assert.Equal(2, plan.CornersOffSurface);
            Assert.All(plan.Points.Where(point => Math.Abs(point[0] - 897) < 1e-6), point => Assert.Equal(Road(900), point[2], 9));
        }

        [Fact]
        public void R571_a_surface_with_no_vertex_in_the_boundary_is_refused_in_a_plain_line()
        {
            var outlines = new[] { new[] { 5000.0, 5000 }, new[] { 5010.0, 5000 }, new[] { 5010.0, 5010 } };

            ToposolidPlan plan = PlanOf(Tin(), outlines, new RampEdge[0], Settings());

            Assert.Contains("does not cover", plan.Refused);
            Assert.Contains("Road", plan.Refused);
        }

        [Fact]
        public void R571_a_surface_under_too_few_road_edges_is_refused_in_a_plain_line()
        {
            // One ramp stands on the surface and three do not, so 9 of the 12 points of their road edges are off it
            var edges = new[] { Edge("1", 1000, 2000), Edge("2", 8000, 2000), Edge("3", 8100, 2000), Edge("4", 8200, 2000) };
            var outlines = new[] { new[] { 1000.0, 2000 }, new[] { 1010.0, 2000 }, new[] { 1010.0, 2020 } };

            ToposolidPlan plan = PlanOf(Tin(), outlines, edges, Settings());

            Assert.Contains("does not cover", plan.Refused);
            Assert.Contains("9 of 12", plan.Refused);
        }

        [Fact]
        public void R571_no_area_in_the_scope_is_refused_in_a_plain_line()
        {
            ToposolidPlan plan = PlanOf(Tin(), new[] { new[] { 1000.0, 2000 }, new[] { 1010.0, 2000 } }, new RampEdge[0], Settings());

            Assert.Contains("no area", plan.Refused);
        }

        [Fact]
        public void R571_the_road_edge_samples_join_the_points_so_the_model_meets_them()
        {
            var outlines = new[] { new[] { 1020.0, 2020 }, new[] { 1030.0, 2020 }, new[] { 1030.0, 2030 } };

            ToposolidPlan plan = PlanOf(Tin(), outlines, new[] { Edge("1", 1020, 2020) }, Settings());

            Assert.Equal(new[] { "start", "middle", "end" }, plan.Edges.Select(edge => edge.At));
            Assert.Contains(plan.Points, point => point[0] == 1025 && point[1] == 2020 && Math.Abs(point[2] - Road(1025)) < 1e-9);
        }

        [Fact]
        public void R571_points_nearer_than_the_gap_are_one()
        {
            var tin = new LandXmlSurface
            {
                Name = "Road", Faces = new[] { 0, 1, 2, 3, 4, 5 },
                Points = new[] { 900.0, 1900, 1, 1100, 1900, 1, 1100, 2100, 1, 1000.0, 2000, 1, 1000.004, 2000, 1, 1000, 2000.004, 1 },
            };
            var outlines = new[] { new[] { 990.0, 1990 }, new[] { 1010.0, 1990 }, new[] { 1010.0, 2010 }, new[] { 990.0, 2010 } };

            ToposolidPlan plan = PlanOf(tin, outlines, new RampEdge[0], Settings());

            Assert.Equal(3, plan.VerticesInside);
            // The four corners and one of the three vertices 4 mm apart
            Assert.Equal(5, plan.Points.Count);
        }

        [Fact]
        public void R571_over_the_limit_the_vertices_are_thinned_and_the_corners_and_edges_stay()
        {
            RoadTopographySettings settings = Settings();
            settings.MaxPoints = 10;
            var outlines = new[] { new[] { 950.0, 1950 }, new[] { 1350.0, 1950 }, new[] { 1350.0, 2150 }, new[] { 950.0, 2150 } };

            ToposolidPlan plan = PlanOf(Tin(), outlines, new[] { Edge("1", 1000, 2000) }, settings);

            Assert.True(plan.Thinned > 0);
            Assert.True(plan.Points.Count <= 10);
            Assert.Contains(plan.Points, point => point[0] == 1005 && point[1] == 2000);
            Assert.Equal(4, plan.Boundary.Count);
            foreach (double[] corner in plan.Boundary)
            {
                Assert.Contains(plan.Points, point => point[0] == corner[0] && point[1] == corner[1]);
            }
        }

        [Fact]
        public void R572_the_toposolid_is_made_after_the_floors_on_the_level_of_the_ramp_and_tagged_for_the_nbh()
        {
            var document = new FakeFloors();
            Apply(Make(document, FloorPlans.Plan(Plot("101"), Plot("102"))), Context(), document);

            Assert.Equal(6, document.Items.Count);
            FakeFloors.Solid made = Assert.Single(document.Solids);
            Assert.Equal("Road surface", made.TypeName);
            Assert.Equal("10", made.LevelKey);
            Assert.Equal(RoadTopography.Kind, made.Tag.Key.Kind);
            Assert.Equal("012", made.Tag.Key.Plot);
            Assert.Equal(made.UniqueId, made.Tag.PlacedId);
            // Internal is shared less the survey offset of the fake, 1000, 2000, 0, the boundary on the level and each point at its road level
            Assert.Contains(made.Boundary, corner => Math.Abs(corner[0] - 95) < 1e-6 && Math.Abs(corner[1] + 10) < 1e-6 && corner[2] == 0);
            Assert.All(made.Points, point => Assert.Equal(Road(point[0] + 1000), point[2], 9));
            Assert.True(document.Calls.IndexOf("toposolid") > document.Calls.LastIndexOf("regenerate"));
        }

        [Fact]
        public void R572_a_plot_list_leaves_the_toposolid_with_one_line_no_read_and_no_report()
        {
            var document = new FakeFloors();
            PlanFile plan = FloorPlans.Plan(Plot("101"), Plot("103"));
            Apply(Make(document, plan), Context("5_1"), document);
            string first = Assert.Single(document.Solids).UniqueId;
            var run = new FloorsRun(document, context => new FloorInputs
            {
                Plan = plan, PlanPath = Path.Combine(work, "plan_012.json"), Profile = "parsons2024Rev6", InScope = plot => plot == "101",
                RoadTopography = () => throw new InvalidOperationException("a plot list reads no surface"),
            });
            RunContext second = Context("5_2");

            ButtonBody body = run.Body();
            RunSummary summary = body.Preview(second);
            var issues = new List<string>(body.Apply(second));
            second.Model = solidModel;
            body.AfterCommit(second);

            Assert.Equal(first, Assert.Single(document.Solids).UniqueId);
            Assert.Equal(RoadTopography.PlotListLine, Assert.Single(Topo(summary.PerPlot)));
            Assert.Equal(RoadTopography.PlotListLine, Assert.Single(Topo(issues)));
            Assert.Empty(second.IssuesAfterCommit);
            Assert.DoesNotContain(second.Outputs, output => output.Value.Contains("toposolid_"));
        }

        [Fact]
        public void R572_a_plot_list_of_every_plot_covers_the_nbh()
        {
            var document = new FakeFloors();

            Apply(Make(document, FloorPlans.Plan(Plot("101"), Plot("103")), true, null, plot => true), Context(), document);

            Assert.Single(document.Solids);
        }

        [Fact]
        public void R572_a_second_press_with_nothing_changed_keeps_the_toposolid_and_reads_it_back()
        {
            var document = new FakeFloors { TopLevel = (x, y) => Road(x + 1000) };
            PlanFile plan = FloorPlans.Plan(Plot("101"));
            Apply(Make(document, plan), Context("5_1"), document);
            string first = Assert.Single(document.Solids).UniqueId;
            document.Calls.Clear();
            int started = solidModel.Started;
            RunContext context = Context("5_2");

            FloorsRun run = Make(document, plan);
            RunSummary summary = run.Body().Preview(context);
            IReadOnlyList<string> issues = Apply(run, context, document);

            Assert.Equal(first, Assert.Single(document.Solids).UniqueId);
            Assert.Equal(started, solidModel.Started);
            Assert.DoesNotContain("toposolid", document.Calls);
            Assert.Contains("triangles", document.Calls);
            Assert.Contains("left as it is", Assert.Single(Topo(summary.PerPlot)));
            Assert.Empty(Topo(issues));
            JObject report = Report(context);
            Assert.Equal(RoadTopography.Keep, (string)report["action"]);
            Assert.Equal(first, (string)report["element"]);
            Assert.Equal(0.0, (double)report["largestModelMm"], 1);
        }

        [Theory]
        [InlineData("another-hash", 5.0)]
        [InlineData("abc", 6.0)]
        public void R572_another_surface_or_setting_replaces_it_and_the_lines_say_so(string sha, double margin)
        {
            var document = new FakeFloors();
            PlanFile plan = FloorPlans.Plan(Plot("101"));
            Apply(Make(document, plan), Context("5_1"), document);
            FakeFloors.Solid first = Assert.Single(document.Solids);
            first.SubDivisions = 2;
            RoadTopographySettings settings = Settings();
            settings.MarginM = margin;
            RunContext context = Context("5_2");

            FloorsRun run = Make(document, plan, sha: sha, settings: settings);
            RunSummary summary = run.Body().Preview(context);
            IReadOnlyList<string> issues = Apply(run, context, document);

            Assert.NotEqual(first.UniqueId, Assert.Single(document.Solids).UniqueId);
            Assert.Contains("replaces the toposolid", Assert.Single(Topo(summary.PerPlot)));
            Assert.Contains("2 subdivisions", Assert.Single(Topo(summary.PerPlot)));
            Assert.Contains(Topo(issues), line => line.Contains("replaces the toposolid") && line.Contains("hand edit"));
            Assert.Equal(RoadTopography.Replace, (string)Report(context)["action"]);
        }

        [Fact]
        public void R572_a_tag_from_before_the_key_is_replaced()
        {
            var document = new FakeFloors();
            document.Solids.Add(new FakeFloors.Solid { UniqueId = "old", Tag = FakeFloors.ToposolidTag("012", "old") });

            Apply(Make(document, FloorPlans.Plan(Plot("101"))), Context(), document);

            Assert.DoesNotContain(document.Solids, solid => solid.UniqueId == "old");
            Assert.Single(document.Solids);
        }

        [Fact]
        public void R572_an_error_at_the_commit_of_the_toposolid_loses_neither_the_floors_nor_the_last_one()
        {
            var document = new FakeFloors();
            PlanFile plan = FloorPlans.Plan(Plot("101"));
            Apply(Make(document, plan), Context("5_1"), document);
            string first = Assert.Single(document.Solids).UniqueId;
            solidModel.RefuseCommit = "Revit refuses the top";
            RunContext context = Context("5_2");

            IReadOnlyList<string> issues = Apply(Make(document, plan, sha: "another"), context, document);

            Assert.Equal(3, document.Items.Count);
            Assert.Equal(first, Assert.Single(document.Solids).UniqueId);
            Assert.Equal(1, solidModel.RolledBack);
            string line = Assert.Single(Topo(issues));
            Assert.Contains("Revit refuses the top", line);
            Assert.Contains("The floors stand", line);
            JObject report = Report(context);
            Assert.False((bool)report["made"]);
            Assert.Contains("Revit refuses the top", (string)report["problem"]);
        }

        [Fact]
        public void R572_a_throw_rolls_back_its_transaction_and_reads_nothing_of_the_document_after_it()
        {
            var document = new FakeFloors { RefuseToposolid = "regeneration failed" };

            IReadOnlyList<string> issues = Apply(Make(document, FloorPlans.Plan(Plot("101"))), Context(), document);

            Assert.Equal(1, solidModel.RolledBack);
            Assert.DoesNotContain("triangles", document.Calls);
            Assert.Contains("regeneration failed", Assert.Single(Topo(issues)));
        }

        [Fact]
        public void R572_a_warning_of_the_toposolid_transaction_is_an_issue_line()
        {
            var document = new FakeFloors();
            solidModel = new SolidModel(document) { Warn = new[] { "Highlighted lines overlap." } };

            IReadOnlyList<string> issues = Apply(Make(document, FloorPlans.Plan(Plot("101"))), Context(), document);

            Assert.Contains(RoadTopography.Line + "Revit warns, Highlighted lines overlap.", issues);
        }

        [Fact]
        public void R575_a_warning_the_config_names_is_taken_counted_and_any_other_still_shows()
        {
            var document = new FakeFloors();
            solidModel = new SolidModel(document) { Warn = new[] { "Highlighted toposolid and floor overlap.", "Highlighted toposolid and floor overlap.", "Highlighted lines overlap." } };
            RoadTopographySettings settings = Settings();
            settings.AcceptWarnings = new[] { "Highlighted toposolid and floor overlap." };
            settings.PlotParameter = "Plot";
            RunContext context = Context();

            IReadOnlyList<string> issues = Apply(Make(document, FloorPlans.Plan(Plot("101")), settings: settings), context, document);

            Assert.Contains(RoadTopography.Line + "Revit warns, Highlighted lines overlap.", issues);
            Assert.DoesNotContain(RoadTopography.Line + "Revit warns, Highlighted toposolid and floor overlap.", issues);
            Assert.Contains(RoadTopography.AcceptedLine(2, new[] { "Highlighted toposolid and floor overlap." }), issues);
            Assert.Equal(2, (int)Report(context)["warningsAccepted"]);
        }

        [Fact]
        public void R575_no_words_in_config_leaves_every_warning_as_it_was()
        {
            Assert.Null(KnownWarningList.OfWords(new string[0], "Plot"));
            RoadTopographySettings read = RoadTopographySettings.Of(JObject.Parse("{'siteBuild':{'snapM':0.005},'floors':{'plotParameter':'Plot','roadTopography':{'typeName':'T','surface':'S','marginM':5,'limitMm':5,'minCoverShare':0.5,'pointGapM':0.01,'maxPoints':10}}}"));
            Assert.Empty(read.AcceptWarnings);
            Assert.Equal("Plot", read.PlotParameter);
        }

        [Fact]
        public void R575_the_words_alone_decide_and_other_words_do_not_pass()
        {
            KnownWarningList list = KnownWarningList.OfWords(new[] { "Highlighted toposolid and floor overlap." }, "Plot");

            AcceptedWarning taken = list.Decide("Highlighted toposolid and floor overlap.", new[] { new WarningElement { Id = "a", TypeName = "Road surface" }, new WarningElement { Id = "b", TypeName = "Ramps 2%" } });

            Assert.Equal("Road surface", taken.FirstType);
            Assert.Equal("b", taken.SecondId);
            Assert.False(list.Knows("Highlighted floors overlap."));
            Assert.Null(list.Decide("Highlighted floors overlap.", new[] { new WarningElement { Id = "a", TypeName = "x" }, new WarningElement { Id = "b", TypeName = "y" } }));
        }

        [Fact]
        public void R572_the_type_made_is_in_the_report_with_the_type_it_came_from()
        {
            var document = new FakeFloors { DuplicatedFrom = "Toposolid 300" };
            RunContext context = Context();

            Apply(Make(document, FloorPlans.Plan(Plot("101"))), context, document);

            Assert.Equal("Toposolid 300", (string)Report(context)["typeDuplicatedFrom"]);
        }

        [Fact]
        public void R572_with_the_tick_off_a_toposolid_that_stands_stays_with_one_line()
        {
            var document = new FakeFloors();
            PlanFile plan = FloorPlans.Plan(Plot("101"));
            Apply(Make(document, plan), Context("5_1"), document);
            string first = Assert.Single(document.Solids).UniqueId;
            RunContext context = Context("5_2");

            FloorsRun run = Make(document, plan, false);
            RunSummary summary = run.Body().Preview(context);
            IReadOnlyList<string> issues = Apply(run, context, document);

            Assert.Equal(first, Assert.Single(document.Solids).UniqueId);
            Assert.Equal(RoadTopography.TickOffLine, Assert.Single(Topo(summary.PerPlot)));
            Assert.Equal(RoadTopography.TickOffLine, Assert.Single(Topo(issues)));
            Assert.DoesNotContain(context.Outputs, output => output.Value.Contains("toposolid_"));
        }

        [Fact]
        public void R571_the_corners_made_up_and_the_points_left_out_reach_the_preview_and_the_done_line()
        {
            var document = new FakeFloors { TopLevel = (x, y) => Road(x + 1000) };
            RoadTopographySettings settings = Settings();
            settings.MaxPoints = 12;
            // The surface starts at x 1097 and the boundary at 1095, so the west corners lie off it
            RunContext context = Context();
            FloorsRun run = Make(document, FloorPlans.Plan(Plot("101")), surface: Tin(1097, 1397, 1900, 2200), settings: settings);

            string preview = Assert.Single(Topo(run.Body().Preview(context).PerPlot));
            string done = Assert.Single(Topo(Apply(run, context, document)));

            foreach (string line in new[] { preview, done })
            {
                Assert.Contains("2 of 4 boundary corners lie off the surface and take the level of the nearest surface point", line);
            }
            ToposolidPlan plan = PlanOf(Tin(1097, 1397, 1900, 2200), new[] { new[] { 1100.0, 1995 }, new[] { 1110.0, 1995 }, new[] { 1110.0, 2030 }, new[] { 1100.0, 2030 } }, new[] { Edge("101", 1100, 1995) }, settings);
            Assert.Equal(0, plan.Thinned);

            var many = FakeOver(settings);
            Assert.Contains("surface points left out to stay within 12", RoadTopography.PreviewLine(many, settings, RoadTopography.Make, new ToposolidElement[0]));
            Assert.Contains("surface points left out to stay within 12", RoadTopography.DoneLine(many, settings, RoadTopography.Make, new ToposolidElement[0]));
        }

        private static ToposolidPlan FakeOver(RoadTopographySettings settings)
        {
            var outlines = new[] { new[] { 950.0, 1950 }, new[] { 1350.0, 1950 }, new[] { 1350.0, 2150 }, new[] { 950.0, 2150 } };
            return PlanOf(Tin(), outlines, new RampEdge[0], settings);
        }

        [Fact]
        public void R571_the_tin_of_a_read_is_built_once()
        {
            var read = new RoadSurfaceRead { Surface = Tin(), Settings = Settings() };

            Assert.Same(read.Tin, read.Tin);
        }

        [Fact]
        public void R573_a_ramp_this_press_leaves_as_it_stands_is_marked_and_counts_in_no_largest()
        {
            var document = new FakeFloors { TopLevel = (x, y) => Road(x + 1000) };
            PlanFile plan = FloorPlans.Plan(Plot("101", 0.04));
            Apply(Make(document, plan), Context("5_1"), document);
            FakeFloors.Floor ramp = document.Items.Single(floor => floor.Tag.Key.Kind == PlacedKinds.Ramp);
            ramp.CornerOffsets[0] += 0.5;
            RunContext context = Context("5_2");

            FloorsRun run = Make(document, plan);
            string preview = Assert.Single(Topo(run.Body().Preview(context).PerPlot));
            IReadOnlyList<string> issues = Apply(run, context, document);

            JObject report = Report(context);
            Assert.Equal(3, (int)report["skippedPoints"]);
            Assert.All((JArray)report["edges"], edge => Assert.True((bool)edge["skipped"]));
            Assert.Equal(JTokenType.Null, report["largestMm"].Type);
            Assert.Equal(0, (int)report["overLimit"]);
            Assert.Contains("3 points of ramps left as they stand are not counted", preview);
            Assert.Empty(Topo(issues));
        }

        [Fact]
        public void R572_a_second_run_replaces_its_own_and_touches_no_other()
        {
            var document = new FakeFloors();
            document.Solids.Add(new FakeFloors.Solid { UniqueId = "other-nbh", Tag = FakeFloors.ToposolidTag("013", "other-nbh") });
            document.Solids.Add(new FakeFloors.Solid { UniqueId = "by-hand" });
            // A copy carries the entity of the one it was copied from, with that placed id
            document.Solids.Add(new FakeFloors.Solid { UniqueId = "copy", Tag = FakeFloors.ToposolidTag("012", "placed-earlier") });
            PlanFile plan = FloorPlans.Plan(Plot("101"));

            Apply(Make(document, plan), Context("5_1"), document);
            string first = document.Solids.Single(solid => solid.Tag?.Run == "5_1").UniqueId;
            Apply(Make(document, plan, sha: "another"), Context("5_2"), document);

            Assert.DoesNotContain(document.Solids, solid => solid.UniqueId == first);
            Assert.Single(document.Solids, solid => solid.Tag?.Run == "5_2");
            Assert.Equal(new[] { "by-hand", "copy", "other-nbh" }, document.Solids.Where(solid => solid.Tag?.Run != "5_2").Select(solid => solid.UniqueId).OrderBy(id => id, StringComparer.Ordinal));
        }

        [Fact]
        public void R572_a_refused_create_keeps_the_floors_and_the_last_toposolid_and_is_an_issue_line()
        {
            var document = new FakeFloors();
            PlanFile plan = FloorPlans.Plan(Plot("101"));
            Apply(Make(document, plan), Context("5_1"), document);
            string first = Assert.Single(document.Solids).UniqueId;
            document.RefuseToposolid = "the sketch is not closed";

            IReadOnlyList<string> issues = Apply(Make(document, plan, sha: "another"), Context("5_2"), document);

            Assert.Equal(3, document.Items.Count);
            Assert.Equal(first, Assert.Single(document.Solids).UniqueId);
            string line = Assert.Single(issues, issue => issue.StartsWith("Road topography:", StringComparison.Ordinal));
            Assert.Contains("the sketch is not closed", line);
            Assert.Contains("no toposolid", line);
        }

        [Fact]
        public void R572_a_surface_that_cannot_be_read_keeps_the_floors_and_is_an_issue_line()
        {
            var document = new FakeFloors();
            var run = new FloorsRun(document, context => new FloorInputs
            {
                Plan = FloorPlans.Plan(Plot("101")), PlanPath = Path.Combine(work, "plan_012.json"), Profile = "parsons2024Rev6",
                RoadTopography = () => throw new InvalidDataException("the surface is not on disk"),
            });

            IReadOnlyList<string> issues = Apply(run, Context(), document);

            Assert.Equal(3, document.Items.Count);
            Assert.Empty(document.Solids);
            Assert.Contains(issues, issue => issue.Contains("the surface is not on disk"));
        }

        [Fact]
        public void R572_a_dry_run_writes_nothing_into_the_model_and_says_what_it_would_make()
        {
            var document = new FakeFloors();
            RunContext context = Context("5_1", true);

            RunSummary summary = Make(document, FloorPlans.Plan(Plot("101"))).Body().Preview(context);

            Assert.Empty(document.Solids);
            Assert.DoesNotContain("toposolid", document.Calls);
            Assert.Contains(summary.PerPlot, line => line.StartsWith("Road topography: toposolid of type Road surface", StringComparison.Ordinal));
            Assert.False((bool)Report(context)["made"]);
        }

        [Fact]
        public void R572_the_tick_off_makes_nothing_and_writes_no_report()
        {
            var document = new FakeFloors();
            RunContext context = Context();

            IReadOnlyList<string> issues = Apply(Make(document, FloorPlans.Plan(Plot("101")), false), context, document);

            Assert.Empty(document.Solids);
            Assert.DoesNotContain(issues, issue => issue.StartsWith("Road topography:", StringComparison.Ordinal));
            Assert.DoesNotContain(context.Outputs, output => output.Value.Contains("toposolid_"));
        }

        [Fact]
        public void R573_the_report_gives_each_road_edge_point_with_the_differences_and_no_coordinate()
        {
            var document = new FakeFloors { TopLevel = (x, y) => Road(x + 1000) + 0.002 };
            RunContext context = Context();

            IReadOnlyList<string> issues = Apply(Make(document, FloorPlans.Plan(Plot("101", 0.004))), context, document);

            Assert.DoesNotContain(issues, issue => issue.StartsWith("Road topography:", StringComparison.Ordinal));
            JObject report = Report(context);
            Assert.Equal("site-adaptation-toposolid", (string)report["format"]);
            Assert.True((bool)report["made"]);
            Assert.Equal("Road surface", (string)report["type"]);
            Assert.Equal("Road", (string)report["surface"]["name"]);
            Assert.Equal("abc", (string)report["surface"]["sha256"]);
            JArray edges = (JArray)report["edges"];
            Assert.Equal(new[] { "start", "middle", "end" }, edges.Select(edge => (string)edge["at"]));
            Assert.All(edges, edge => Assert.Equal("101", (string)edge["plot"]));
            Assert.All(edges, edge => Assert.Equal(4.0, (double)edge["differenceMm"], 1));
            // The fake top stands 2 mm over the road, the ramp 4 mm over, so 2 mm to the model
            Assert.All(edges, edge => Assert.Equal(2.0, (double)edge["modelDifferenceMm"], 1));
            Assert.Equal(4.0, (double)report["largestMm"], 1);
            Assert.Equal(2.0, (double)report["largestModelMm"], 1);
            Assert.Equal(5.0, (double)report["limitMm"]);
            Assert.Equal(0, (int)report["overLimit"]);
            Assert.Equal(0, (int)report["overLimitModel"]);
            Assert.All(edges.Children<JObject>(), edge => Assert.False(edge.ContainsKey("x") || edge.ContainsKey("y")));
        }

        [Fact]
        public void R573_a_difference_over_the_limit_is_counted_and_an_issue_line()
        {
            var document = new FakeFloors { TopLevel = (x, y) => Road(x + 1000) };
            RunContext context = Context();

            IReadOnlyList<string> issues = Apply(Make(document, FloorPlans.Plan(Plot("101", 0.04), Plot("102"))), context, document);

            JObject report = Report(context);
            Assert.Equal(3, (int)report["overLimit"]);
            Assert.Equal(3, (int)report["overLimitModel"]);
            Assert.Equal(40.0, (double)report["largestMm"], 1);
            string line = Assert.Single(issues, issue => issue.StartsWith("Road topography:", StringComparison.Ordinal));
            Assert.Contains("3 of 6", line);
            Assert.Contains("5 mm", line);
            Assert.Contains("toposolid_012.json", line);
        }

        [Fact]
        public void R573_the_highest_triangle_counts_so_the_underside_is_no_top()
        {
            var edges = new List<RoadEdgePoint> { new RoadEdgePoint { Plot = "1", At = "start", X = 1, Y = 1, RampLevel = 12 } };
            double[] triangles =
            {
                0, 0, 5, 10, 0, 5, 0, 10, 5,
                0, 0, 7, 10, 0, 7, 0, 10, 7,
                // A vertical side stands over the point in plan and gives it no level
                1, 1, 0, 1, 1, 50, 2, 2, 0,
            };

            // A plain x y of the sample is the shared one, the triangles are internal, with no shift in plan in this test
            RoadTopography.ReadBack(edges, triangles, new[] { 0.0, 0.0, 100.0 });

            Assert.Equal(107, edges[0].ModelLevel.Value, 9);
        }

        [Fact]
        public void R573_a_failed_read_back_stays_a_report_of_the_surface_with_an_issue_line()
        {
            var document = new FakeFloors { RefuseTriangles = "no geometry" };
            RunContext context = Context();

            IReadOnlyList<string> issues = Apply(Make(document, FloorPlans.Plan(Plot("101"))), context, document);

            Assert.Single(document.Solids);
            Assert.Contains(issues, issue => issue.StartsWith("Road topography:", StringComparison.Ordinal) && issue.Contains("no geometry"));
            JObject report = Report(context);
            Assert.True((bool)report["made"]);
            Assert.Equal(JTokenType.Null, report["largestModelMm"].Type);
        }

        [Fact]
        public void R574_the_tick_is_on_by_default_and_left_out_of_the_json_so_the_format_stays()
        {
            var settings = new ProjectSettings();
            string text = SettingsJson.Write(settings);

            Assert.True(settings.RoadTopography);
            Assert.DoesNotContain("roadTopography", text);
            Assert.Equal(SettingsJson.Version, JObject.Parse(text)["formatVersion"].Value<int>());
            Assert.True(SettingsJson.Read("{\"formatVersion\": 1}").RoadTopography);
        }

        [Fact]
        public void R574_the_tick_off_is_kept_by_the_settings_and_by_a_pre_setting()
        {
            var settings = new ProjectSettings { RoadTopography = false };
            string text = SettingsJson.Write(settings);

            Assert.Contains("\"roadTopography\": false", text);
            Assert.False(SettingsJson.Read(text).RoadTopography);
            var into = new ProjectSettings();
            PreSetting.Import(PreSetting.Export(settings), into);
            Assert.False(into.RoadTopography);
        }

        private static string RepoFile(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(folder, "CLAUDE.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            return Path.Combine(new[] { folder }.Concat(parts).ToArray());
        }
    }
}
