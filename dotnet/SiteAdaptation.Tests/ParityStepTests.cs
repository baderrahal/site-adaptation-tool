using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R345. The C# side of the parity harness on made up files: the step faces, the step file with its raw and written values and
    // the worker build that writes it, the tolerances read from config/project.json, on net48 and net8.0
    public sealed class ParityStepTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r345_" + Guid.NewGuid().ToString("N"));

        public ParityStepTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
        }

        private static JObject Shape(string kind, string label, params double[] xy)
        {
            var vertices = new JArray();
            for (int index = 0; index < xy.Length; index += 2)
            {
                vertices.Add(new JArray(xy[index], xy[index + 1]));
            }
            return new JObject { ["kind"] = kind, ["plot"] = label, ["vertices"] = vertices };
        }

        // The made up baseplan of python/tests/test_parity.py: label 2 drawn twice, 02 and 2, a label that is no whole number,
        // a face of two vertices and a polyline
        private static JArray Plots()
        {
            return new JArray
            {
                Shape("plot_shape", "1", 0, 0, 10, 0, 10, 20, 0, 20),
                Shape("plot_shape", "02", 20, 0, 30, 0, 30, 10, 20, 10),
                Shape("plot_shape", "2", 40, 0, 50, 0, 50, 10, 40, 10),
                Shape("plot_shape", "3a", 0, 30, 10, 30, 10, 40),
                Shape("plot_shape", "4", 0, 50, 10, 50),
                Shape("polyline", "5", 0, 60, 10, 60, 10, 70),
            };
        }

        // The areas tolerance of addin.parity, the most the union of the rings of a label may fall short of their sum
        private const double Overlap = 0.000001;

        private static List<double[]> Ring(params double[] xy)
        {
            var ring = new List<double[]>();
            for (int index = 0; index < xy.Length; index += 2)
            {
                ring.Add(new[] { xy[index], xy[index + 1] });
            }
            return FaceRings.Clean(ring);
        }

        private static readonly double[] Square = { 0, 0, 10, 0, 10, 10, 0, 10 };

        private const string PlotsCsv = "baseplanLabel,locationPlanNumber,typologyRev9\n1,101,DP1\n2,102,DP1\n,103,TH1\n";
        private const string LabelsCsv = "number,code,kind,nbh\n101,DP1,clientCode,012\n102,DP1,clientCode,012\n103,TH1,clientCode,\n104,x,number,012\n";

        [Fact]
        public void R345_the_config_names_the_tolerances_of_the_design_by_kind_of_field()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            foreach (string kind in new[] { "points", "levels", "areas", "angles" })
            {
                Assert.Equal(0.000001, config.ParityTolerance(kind));
            }
            Assert.Null(config.ParityTolerance("noGeometry"));
            Assert.Throws<InvalidDataException>(() => config.ParityTolerance("volumes"));
            var fields = config.StepFields(FaceStep.Name).ToDictionary(field => field.Name);
            Assert.Equal(new[] { "face", "area", "centroid", "boundsMin", "boundsMax" }, fields.Keys.ToArray());
            Assert.Equal("noGeometry", fields["face"].Kind);
            Assert.Null(fields["face"].Decimals);
            Assert.Equal("areas", fields["area"].Kind);
            Assert.Equal(3, fields["area"].Decimals);
            Assert.Equal("points", fields["centroid"].Kind);
            Assert.Throws<InvalidDataException>(() => config.StepFields("walls"));
        }

        [Fact]
        public void R345_the_face_of_a_plot_is_its_area_centroid_and_bounds_as_the_build_takes_the_face()
        {
            var faces = FaceStep.Build(new[] { "101", "102", "103" }, Plots(), CsvRows.Parse(PlotsCsv), Overlap);
            Assert.Equal(new[] { "101", "102", "103" }, faces.Keys.ToArray());
            Assert.Equal(true, faces["101"]["face"]);
            Assert.Equal(200.0, (double)faces["101"]["area"]);
            Assert.Equal(new[] { 5.0, 10.0 }, (double[])faces["101"]["centroid"]);
            Assert.Equal(new[] { 0.0, 0.0 }, (double[])faces["101"]["boundsMin"]);
            Assert.Equal(new[] { 10.0, 20.0 }, (double[])faces["101"]["boundsMax"]);
            // Labels 02 and 2 are one plot, two squares joined as the build joins them
            Assert.Equal(200.0, (double)faces["102"]["area"]);
            Assert.Equal(new[] { 35.0, 5.0 }, (double[])faces["102"]["centroid"]);
            Assert.Equal(new[] { 20.0, 0.0 }, (double[])faces["102"]["boundsMin"]);
            // A plot with no face holds every field of the step, null, as the Python step does, so a field missing is a failure
            Assert.Equal(new Dictionary<string, object> { ["face"] = false, ["area"] = null, ["centroid"] = null, ["boundsMin"] = null, ["boundsMax"] = null }, faces["103"]);
        }

        [Fact]
        public void R345_the_area_and_centroid_are_taken_about_the_first_vertex_so_site_coordinates_keep_their_digits()
        {
            // A made up square of 10 m far from the origin, turned by 30 degrees, whose area a plain shoelace loses in the last digits
            double angle = Math.PI / 6, east = 512345.678, north = 2400123.456;
            var xy = new List<double>();
            foreach (var (u, v) in new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 10.0), (0.0, 10.0) })
            {
                xy.Add(east + u * Math.Cos(angle) - v * Math.Sin(angle));
                xy.Add(north + u * Math.Sin(angle) + v * Math.Cos(angle));
            }
            var plots = new JArray { Shape("plot_shape", "7", xy.ToArray()) };
            var face = FaceStep.Build(new[] { "107" }, plots, CsvRows.Parse("baseplanLabel,locationPlanNumber\n7,107\n"), Overlap)["107"];
            Assert.InRange(Math.Abs((double)face["area"] - 100.0), 0, 1e-8);
            double[] centroid = (double[])face["centroid"];
            Assert.InRange(Math.Abs(centroid[0] - (east + 5 * Math.Cos(angle) - 5 * Math.Sin(angle))), 0, 1e-8);
            Assert.InRange(Math.Abs(centroid[1] - (north + 5 * Math.Sin(angle) + 5 * Math.Cos(angle))), 0, 1e-8);
        }

        [Fact]
        public void R345_a_simple_ring_holds_and_a_bow_tie_a_fold_or_a_touch_is_not_simple()
        {
            Assert.True(FaceRings.IsSimple(Ring(Square)));
            double east = 512345.678, north = 2400123.456;
            Assert.True(FaceRings.IsSimple(Ring(east, north, east + 10, north, east + 10, north + 10, east, north + 10, east, north)));
            Assert.Equal(3, Ring(0, 0, 0, 0, 1, 0, 1, 1, 0, 0).Count);
            Assert.False(FaceRings.IsSimple(Ring(0, 0, 10, 10, 10, 0, 0, 10)));
            Assert.False(FaceRings.IsSimple(Ring(0, 0, 10, 0, 10, 10, 10, 5, 0, 10)));
            Assert.False(FaceRings.IsSimple(Ring(0, 0, 10, 0, 5, 5, 10, 10, 0, 10, 5, 5)));
        }

        [Fact]
        public void R345_the_union_of_rings_by_slabs_is_their_sum_when_they_lie_apart_or_share_an_edge()
        {
            Assert.InRange(FaceRings.UnionArea(new[] { Ring(Square), Ring(20, 0, 30, 0, 30, 10, 20, 10) }), 200 - 1e-9, 200 + 1e-9);
            Assert.InRange(FaceRings.UnionArea(new[] { Ring(Square), Ring(10, 0, 20, 0, 20, 10, 10, 10) }), 200 - 1e-9, 200 + 1e-9);
            Assert.InRange(FaceRings.UnionArea(new[] { Ring(Square), Ring(Square) }), 100 - 1e-9, 100 + 1e-9);
            double half = 7.0710678118654755, octagon = 8 * 25 * (Math.Sqrt(2) - 1);
            double turned = FaceRings.UnionArea(new[] { Ring(-5, -5, 5, -5, 5, 5, -5, 5), Ring(0, -half, half, 0, 0, half, -half, 0) });
            Assert.InRange(turned, 200 - octagon - 1e-9, 200 - octagon + 1e-9);
        }

        [Fact]
        public void R345_the_face_step_refuses_a_bow_tie_and_an_outline_drawn_twice_as_the_python_build_does()
        {
            var csv = CsvRows.Parse("baseplanLabel,locationPlanNumber\n1,101\n2,102\n");
            var bowTie = new JArray { Shape("plot_shape", "1", Square), Shape("plot_shape", "2", 20, 0, 30, 10, 30, 0, 20, 10) };
            Assert.Contains("label 2 is not a simple ring", Assert.Throws<InvalidDataException>(() => FaceStep.Build(new[] { "101" }, bowTie, csv, Overlap)).Message);
            var twice = new JArray { Shape("plot_shape", "1", Square), Shape("plot_shape", "1", Square) };
            Assert.Contains("label 1 overlap by 100.000 m2", Assert.Throws<InvalidDataException>(() => FaceStep.Build(new[] { "101" }, twice, csv, Overlap)).Message);
            var shared = new JArray { Shape("plot_shape", "1", Square), Shape("plot_shape", "1", 10, 0, 20, 0, 20, 10, 10, 10) };
            Assert.Equal(200.0, (double)FaceStep.Build(new[] { "101" }, shared, csv, Overlap)["101"]["area"], 9);
            // The same lines as python/face_rings.py, in the same order: the rings that are not simple, then the labels that overlap
            var plots = new JArray
            {
                Shape("plot_shape", "1", Square), Shape("plot_shape", "2", 0, 0, 10, 10, 10, 0, 0, 10), Shape("plot_shape", "3", Square), Shape("plot_shape", "03", Square),
                Shape("plot_shape", "4", Square), Shape("plot_shape", "4", 10, 0, 20, 0, 20, 10, 10, 10), Shape("plot_shape", "5a", 0, 0, 10, 10, 10, 0, 0, 10),
                Shape("plot_shape", "6", 0, 0, 1, 0), Shape("polyline", "7", 0, 0, 10, 10, 10, 0, 0, 10),
            };
            Assert.Equal(new[]
            {
                "the baseplan face of label 2 is not a simple ring, two of its edges meet, shapely would mend it by buffer(0) and the C# would not, so the build refuses it (R345)",
                "the baseplan faces of label 3 overlap by 100.000 m2, shapely joins them by a union and the C# would sum them, so the build refuses them (R345)",
            }, FaceRings.Refusals(plots, Overlap));
        }

        [Fact]
        public void R345_a_label_is_a_whole_number_of_ascii_digits_as_yard_rule_reads_it()
        {
            Assert.Equal("12", FaceStep.WholeLabel(" 0012 "));
            Assert.Equal("0", FaceStep.WholeLabel("000"));
            Assert.Null(FaceStep.WholeLabel("12a"));
            Assert.Null(FaceStep.WholeLabel("-3"));
            Assert.Null(FaceStep.WholeLabel("1.5"));
            Assert.Null(FaceStep.WholeLabel(""));
            Assert.Null(FaceStep.WholeLabel(null));
            Assert.Null(FaceStep.WholeLabel("\u0663"));
        }

        [Fact]
        public void R345_csv_rows_read_quotes_commas_and_line_breaks_as_python_csv_does()
        {
            var rows = CsvRows.Parse("a,b,c\r\n1,\"two, three\",\"say \"\"hi\"\"\"\n4,\"line\nbreak\",\n7,8\n");
            Assert.Equal(3, rows.Count);
            Assert.Equal("two, three", rows[0]["b"]);
            Assert.Equal("say \"hi\"", rows[0]["c"]);
            Assert.Equal("line\nbreak", rows[1]["b"]);
            Assert.Equal("", rows[1]["c"]);
            Assert.Equal("8", rows[2]["b"]);
            Assert.False(rows[2].ContainsKey("c"));
        }

        // H1 of the fifth review of PR 55, test_build_steps.SCOPE_CASES: a scope that numbers no plot and a row of kind clientCode with no
        // number refused with the line of Python, where a build of no plot was reported done and the row was skipped
        [Fact]
        public void R345_a_scope_that_numbers_no_plot_or_a_client_code_row_with_no_number_is_refused_with_the_line_of_python()
        {
            string none = "the scope {0} numbers no plot, a build or a read of no plot is refused (R345)";
            string noNumber = "a row of kind clientCode of the labels holds no number, row {0} of the rows read (R345)";
            var project = new WorkerScope { Kind = "project" };
            var cases = new[]
            {
                Tuple.Create("kind,number,nbh\nlandUse,1,012\nclientCode,2,012\n", new WorkerScope { Kind = "nbh", Nbh = "013" }, string.Format(none, "nbh=013")),
                Tuple.Create("kind,number,nbh\nlandUse,1,012\n", project, string.Format(none, "project")),
                Tuple.Create("kind,number,nbh\n\"\"\n", project, string.Format(none, "project")),
                Tuple.Create("kind,number,nbh\nclientCode,1,012\nclientCode\n", project, string.Format(noNumber, 2)),
                Tuple.Create("kind,number,nbh\nclientCode,1,012\nclientCode,,012\n", new WorkerScope { Kind = "nbh", Nbh = "012" }, string.Format(noNumber, 2)),
            };
            string labels = Path.Combine(folder, "labels.csv");
            foreach (Tuple<string, WorkerScope, string> item in cases)
            {
                File.WriteAllText(labels, item.Item1);
                Assert.Equal(item.Item3, Assert.Throws<InvalidDataException>(() => PlotScope.Numbers(item.Item2, CsvRows.Read(labels, CsvRows.LabelsOf(item.Item2)))).Message);
            }
            var empty = new WorkerScope { Kind = "plots", Plots = new List<string>() };
            Assert.Equal(string.Format(none, "plots="), Assert.Throws<InvalidDataException>(() => PlotScope.Numbers(empty, new List<Dictionary<string, string>>())).Message);
        }

        // H1 of the fifth review of PR 55: a line of one empty quoted field is a row of csv, which CsvRows dropped as an empty line
        [Fact]
        public void R345_a_line_of_one_empty_quoted_field_is_a_row_and_an_empty_line_is_none()
        {
            List<Dictionary<string, string>> rows = CsvRows.Parse("kind,number\n\"\"\n\nclientCode,1\n");
            Assert.Equal(2, rows.Count);
            Assert.Equal("", rows[0]["kind"]);
            Assert.False(rows[0].ContainsKey("number"));
            Assert.Equal("1", rows[1]["number"]);
            Assert.Single(CsvRows.Parse("kind,number\n\"\"\n"));
            string labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(labels, "kind,number\n\n");
            Assert.Equal("labels.csv holds no row under its header (R345)", Assert.Throws<InvalidDataException>(() => CsvRows.Read(labels, CsvRows.LabelsColumns)).Message);
        }

        // H1 of the fifth review of PR 55: the columns of labels.csv each scope reads, nbh besides for a neighbourhood, refused by its line
        [Fact]
        public void R345_labels_of_names_the_columns_a_scope_reads_and_a_neighbourhood_needs_the_column_nbh()
        {
            var project = new WorkerScope { Kind = "project" };
            var nbh = new WorkerScope { Kind = "nbh", Nbh = "012" };
            Assert.Equal(CsvRows.LabelsColumns, CsvRows.LabelsOf(project));
            Assert.Equal(CsvRows.LabelsColumns.Concat(new[] { "nbh" }), CsvRows.LabelsOf(nbh));
            string labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(labels, "kind,number\nclientCode,1\n");
            Assert.Equal("labels.csv has no column nbh in its header (R345)", Assert.Throws<InvalidDataException>(() => CsvRows.Read(labels, CsvRows.LabelsOf(nbh))).Message);
            Assert.Equal(new[] { "1" }, PlotScope.Numbers(project, CsvRows.Read(labels, CsvRows.LabelsOf(project))));
        }

        // H1 of the fifth review of PR 55: a plot list needs no labels.csv, as plan_from_book.py never reads --labels for a cover file, and the
        // project and a neighbourhood do, as it needs --labels for all and nbh=<NBH>
        [Fact]
        public void R345_a_plot_list_needs_no_labels_as_a_cover_file_of_python_and_the_project_does()
        {
            string plots = Path.Combine(folder, "plots.json");
            File.WriteAllText(plots, new JObject { ["units"] = "Meters", ["plots"] = Plots() }.ToString());
            string data = Path.Combine(folder, "plots.csv");
            File.WriteAllText(data, PlotsCsv);
            string config = WorkerRunTests.RepoConfig();
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = "q1", Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile>
                {
                    new WorkerFile { Role = "config", Path = config, Sha256 = FileHash.Sha256(config) },
                    new WorkerFile { Role = "baseplanPlots", Path = plots, Sha256 = FileHash.Sha256(plots) },
                    new WorkerFile { Role = "plotsCsv", Path = data, Sha256 = FileHash.Sha256(data) },
                },
                Scope = new WorkerScope { Kind = "plots", Plots = new List<string> { "103", "101" } }, OutputFolder = folder, Steps = new List<string> { "faces" },
                StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
            int code = WorkerRunner.Run(request.Write(folder), WorkerCommands.Default(), () => 9000.0);
            Assert.True(code == 0, WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "q1")).Message);
            JObject file = RunStamp.Read(Path.Combine(folder, "steps", "step_faces_csharp.json"));
            Assert.Equal(new[] { "101", "103" }, ((JObject)file["plots"]).Properties().Select(item => item.Name).ToArray());
            Assert.Equal("plots=103,101", (string)file["scope"]);
            request.RunId = "q2";
            request.Scope = new WorkerScope { Kind = "project" };
            Assert.Equal(2, WorkerRunner.Run(request.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the step faces needs the input labels", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "q2")).Message);
        }

        [Fact]
        public void R345_the_plots_of_a_scope_come_from_the_client_code_labels_of_the_location_plan()
        {
            var labels = CsvRows.Parse(LabelsCsv);
            Assert.Equal(new[] { "101", "102", "103" }, PlotScope.Numbers(new WorkerScope { Kind = "project" }, labels));
            Assert.Equal(new[] { "101", "102" }, PlotScope.Numbers(new WorkerScope { Kind = "nbh", Nbh = "012" }, labels));
            Assert.Equal(new[] { "9", "102", "2290" }, PlotScope.Numbers(new WorkerScope { Kind = "plots", Plots = new List<string> { "2290", "9", "102", "9" } }, labels));
            Assert.Throws<InvalidDataException>(() => PlotScope.Numbers(new WorkerScope { Kind = "zone" }, labels));
        }

        [Fact]
        public void R345_the_step_file_holds_every_digit_of_the_raw_values_and_the_values_written_by_r385()
        {
            var fields = AddinConfig.Load(WorkerRunTests.RepoConfig()).StepFields(FaceStep.Name);
            var plots = new Dictionary<string, Dictionary<string, object>>
            {
                ["101"] = new Dictionary<string, object> { ["face"] = true, ["area"] = 0.1 + 0.2, ["centroid"] = new[] { 2.0465, 0.7 + 0.0005 }, ["boundsMin"] = new[] { -0.0004, 1.0 }, ["boundsMax"] = new[] { 3.0, 4.0 } },
                ["103"] = new Dictionary<string, object> { ["face"] = false, ["area"] = null, ["centroid"] = null, ["boundsMin"] = null, ["boundsMax"] = null },
            };
            string text = StepFile.Text(FaceStep.Name, "csharp", "nbh=012", plots, fields);
            Assert.Contains("\"area\": 0.30000000000000004", text);
            JObject file = JObject.Parse(text);
            Assert.Equal("siteAdaptationStep", (string)file["format"]);
            Assert.Equal(2, (int)file["formatVersion"]);
            Assert.Equal("nbh=012", (string)file["scope"]);
            Assert.Equal("faces", (string)file["step"]);
            Assert.Equal("csharp", (string)file["side"]);
            JToken first = file["plots"]["101"];
            Assert.Equal(0.1 + 0.2, (double)first["raw"]["area"]);
            Assert.Equal(0.3, (double)first["written"]["area"]);
            Assert.Equal(new[] { 2.047, 0.701 }, first["written"]["centroid"].Select(item => (double)item).ToArray());
            Assert.Equal(0.0, (double)first["written"]["boundsMin"][0]);
            Assert.True((bool)first["raw"]["face"] && (bool)first["written"]["face"]);
            Assert.Equal(JTokenType.Null, file["plots"]["103"]["raw"]["area"].Type);
            Assert.Equal(JTokenType.Null, file["plots"]["103"]["written"]["centroid"].Type);
            Assert.Equal("areas", (string)file["fields"]["area"]["kind"]);
        }

        [Fact]
        public void R345_a_list_of_points_is_written_point_by_point_raw_and_by_r385()
        {
            var fields = new List<ParityField> { new ParityField { Name = "outline", Kind = "points", Decimals = 3 } };
            var plots = new Dictionary<string, Dictionary<string, object>>
            {
                ["101"] = new Dictionary<string, object> { ["outline"] = new[] { new[] { 2361234.1235, 0.7 + 0.0005 }, new[] { 2.0465, -0.0004 } } },
            };
            JObject file = StepFile.Build("outlines", "csharp", "project", plots, fields);
            JToken raw = file["plots"]["101"]["raw"]["outline"];
            JToken written = file["plots"]["101"]["written"]["outline"];
            Assert.Equal(0.7 + 0.0005, (double)raw[0][1]);
            Assert.Equal(new[] { 2361234.124, 0.701 }, written[0].Select(item => (double)item).ToArray());
            Assert.Equal(new[] { 2.047, 0.0 }, written[1].Select(item => (double)item).ToArray());
        }

        [Theory]
        [InlineData("project", null, "project")]
        [InlineData("nbh", "012", "nbh=012")]
        public void R345_the_scope_of_a_step_file_is_named_as_the_python_step_names_it(string kind, string nbh, string named)
        {
            Assert.Equal(named, StepFile.ScopeText(new WorkerScope { Kind = kind, Nbh = nbh }));
            Assert.Equal("plots=9,102", StepFile.ScopeText(new WorkerScope { Kind = "plots", Plots = new List<string> { "9", "102" } }));
            Assert.Equal("step_faces_csharp.json", StepFile.FileName("faces", "csharp"));
        }

        [Fact]
        public void R345_the_worker_build_writes_the_step_file_of_the_faces_of_its_scope_and_names_it_in_the_answer()
        {
            string plots = Path.Combine(folder, "plots.json");
            File.WriteAllText(plots, new JObject { ["units"] = "Meters", ["plots"] = Plots() }.ToString());
            string data = Path.Combine(folder, "plots.csv");
            File.WriteAllText(data, PlotsCsv);
            string labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(labels, LabelsCsv);
            string config = WorkerRunTests.RepoConfig();
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = "p1", Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile>
                {
                    new WorkerFile { Role = "config", Path = config, Sha256 = FileHash.Sha256(config) },
                    new WorkerFile { Role = "baseplanPlots", Path = plots, Sha256 = FileHash.Sha256(plots) },
                    new WorkerFile { Role = "plotsCsv", Path = data, Sha256 = FileHash.Sha256(data) },
                    new WorkerFile { Role = "labels", Path = labels, Sha256 = FileHash.Sha256(labels) },
                },
                Scope = new WorkerScope { Kind = "nbh", Nbh = "012" }, OutputFolder = folder, Steps = new List<string> { "faces" },
                StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
            Assert.Equal(0, WorkerRunner.Run(request.Write(folder), WorkerCommands.Default(), () => 9000.0));
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "p1"));
            // The file is named by its side, so the two sides never share one name in one folder
            string step = Path.Combine(folder, "steps", "step_faces_csharp.json");
            Assert.Equal(step, answer.Outputs.Single().Path);
            Assert.Equal(FileHash.Sha256(step), answer.Outputs.Single().Sha256);
            Assert.Equal(2, answer.Counts["faces plots"]);
            Assert.Equal(2, answer.Counts["faces with a face"]);
            JObject file = RunStamp.Read(step);
            Assert.Equal(new[] { "101", "102" }, ((JObject)file["plots"]).Properties().Select(item => item.Name).ToArray());
            Assert.Equal("nbh=012", (string)file["scope"]);
            Assert.Equal("p1", (string)file["run"]["runId"]);

            request.RunId = "p2";
            request.Inputs.RemoveAt(3);
            Assert.Equal(2, WorkerRunner.Run(request.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.Contains("the step faces needs the input labels", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "p2")).Message);
        }

        // The request of a worker build of the step faces on the made up baseplan, plots.csv and labels.csv
        private WorkerRequest FaceRequest(string runId)
        {
            string plots = Path.Combine(folder, "plots.json");
            File.WriteAllText(plots, new JObject { ["units"] = "Meters", ["plots"] = Plots() }.ToString());
            string data = Path.Combine(folder, "plots.csv");
            File.WriteAllText(data, PlotsCsv);
            string labels = Path.Combine(folder, "labels.csv");
            File.WriteAllText(labels, LabelsCsv);
            string config = WorkerRunTests.RepoConfig();
            return new WorkerRequest
            {
                FormatVersion = 1, RunId = runId, Command = "build", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile>
                {
                    new WorkerFile { Role = "config", Path = config, Sha256 = FileHash.Sha256(config) },
                    new WorkerFile { Role = "baseplanPlots", Path = plots, Sha256 = FileHash.Sha256(plots) },
                    new WorkerFile { Role = "plotsCsv", Path = data, Sha256 = FileHash.Sha256(data) },
                    new WorkerFile { Role = "labels", Path = labels, Sha256 = FileHash.Sha256(labels) },
                },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, Steps = new List<string> { "faces" },
                StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
            };
        }

        [Fact]
        public void R345_a_worker_run_that_fails_leaves_no_step_file_of_its_steps()
        {
            string step = Path.Combine(folder, "steps", "step_faces_csharp.json");
            Assert.Equal(0, WorkerRunner.Run(FaceRequest("f1").Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.True(File.Exists(step));
            // The same inputs, so the step file of f1 would still hash the same, and a build that throws before it writes the step
            var throwing = new Dictionary<string, WorkerCommand> { ["build"] = run => throw new InvalidDataException("a made up failure of the build") };
            Assert.Equal(1, WorkerRunner.Run(FaceRequest("f2").Write(folder), throwing, () => 9000.0));
            Assert.False(File.Exists(step), "a run that failed leaves no step file, so the parity finds it missing");

            Assert.Equal(0, WorkerRunner.Run(FaceRequest("f3").Write(folder), WorkerCommands.Default(), () => 9000.0));
            WorkerRequest refused = FaceRequest("f4");
            refused.Inputs[2].Sha256 = new string('0', 64);
            Assert.Equal(4, WorkerRunner.Run(refused.Write(folder), WorkerCommands.Default(), () => 9000.0));
            Assert.False(File.Exists(step));
        }

        [Fact]
        public void R345_a_tolerance_is_a_finite_number_of_zero_or_more_or_null_for_an_exact_kind()
        {
            string path = Path.Combine(folder, "config.json");
            foreach (string bad in new[] { "true", "false", "NaN", "Infinity", "-0.000001", "\"0.000001\"", "[0.1]" })
            {
                File.WriteAllText(path, "{\"addin\": {\"parity\": {\"kinds\": {\"areas\": " + bad + ", \"noGeometry\": null}, \"steps\": {}}}}");
                Exception refused = Record.Exception(() => AddinConfig.Load(path).ParityTolerance("areas"));
                Assert.True(refused is InvalidDataException, bad + " gave " + (refused == null ? "no refusal" : refused.GetType().Name));
                Assert.Contains("finite number of zero or more", refused.Message);
            }
            File.WriteAllText(path, "{\"addin\": {\"parity\": {\"kinds\": {\"areas\": 0, \"noGeometry\": null}, \"steps\": {}}}}");
            Assert.Equal(0.0, AddinConfig.Load(path).ParityTolerance("areas"));
            Assert.Null(AddinConfig.Load(path).ParityTolerance("noGeometry"));
        }

        // H2 of the fifth review of PR 55, test_parity.TOLERANCES_LINES: every kind the fields of the steps name held in their order with
        // the lines of parity.tolerances
        [Fact]
        public void R345_parity_tolerances_hold_every_kind_the_steps_name_with_the_lines_of_python()
        {
            string path = Path.Combine(folder, "config.json");
            var cases = new[]
            {
                Tuple.Create("{\"areas\": 0.1}", "{\"faces\": {\"area\": {\"kind\": \"volumes\", \"decimals\": 3}}}", "addin.parity.steps.faces.area names the kind volumes, which addin.parity.kinds does not hold (R345)"),
                Tuple.Create("{\"areas\": 0.1}", "{\"faces\": {\"area\": {\"decimals\": 3}}}", "addin.parity.steps.faces.area names the kind None, which addin.parity.kinds does not hold (R345)"),
                Tuple.Create("{\"areas\": 0.1}", "{\"faces\": {\"area\": {\"kind\": \"areas\"}}}", "addin.parity.steps.faces.area: a kind with a tolerance has decimals and a kind compared exactly has none (R345)"),
                Tuple.Create("{\"areas\": null}", "{\"faces\": {\"area\": {\"kind\": \"areas\", \"decimals\": 3}}}", "addin.parity.steps.faces.area: a kind with a tolerance has decimals and a kind compared exactly has none (R345)"),
                Tuple.Create("{\"areas\": 0.1, \"points\": NaN}", "{\"faces\": {\"area\": {\"kind\": \"areas\", \"decimals\": 3}, \"centroid\": {\"kind\": \"points\", \"decimals\": 3}}}",
                    "addin.parity.kinds.points is NaN, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
                Tuple.Create("{\"areas\": 0.1}", "[]", "config holds no addin.parity block with kinds and steps (R345)"),
            };
            foreach (Tuple<string, string, string> item in cases)
            {
                File.WriteAllText(path, "{\"addin\": {\"parity\": {\"kinds\": " + item.Item1 + ", \"steps\": " + item.Item2 + "}}}");
                Assert.Equal(item.Item3, Assert.Throws<InvalidDataException>(() => AddinConfig.Load(path).ParityTolerances()).Message);
            }
            AddinConfig.Load(WorkerRunTests.RepoConfig()).ParityTolerances();
        }
    }
}
