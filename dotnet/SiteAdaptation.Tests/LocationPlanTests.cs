using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CadReader;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R351. The location plan of the worker on made up pages, faces and workbooks built in code (R10), the C# side of
    // python/tests/test_location_plan.py and test_location_steps.py on the same made up data, on net48 and net8.0
    public sealed class LocationPlanTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r351_" + Guid.NewGuid().ToString("N"));

        public LocationPlanTests()
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

        private const string Code = @"(?P<base>[A-Z]{1,3}\d{1,2}(?:-0|-IM)?)(?:\s?\(|-)?(?P<suffix>BB|SA|QP)?\)?";

        private static readonly List<KeyValuePair<string, string>> Patterns = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("number", @"\d{1,4}"),
            new KeyValuePair<string, string>("clientCode", Code),
            new KeyValuePair<string, string>("openSpace", "OS"),
            new KeyValuePair<string, string>("landUse", @"[A-Z][A-Za-z0-9 .&/()-]*"),
        };

        // The characters of test_location_plan.chars: each step apart, 0.65 wide and 1.0 high
        private static List<PageChar> Chars(string text, double x, double top, double size = 1.48, double step = 0.7)
        {
            return text.Select((letter, k) => new PageChar { Text = letter.ToString(), X0 = x + k * step, X1 = x + k * step + 0.65, Top = top, Bottom = top + 1.0, Size = size, Upright = true }).ToList();
        }

        // The page to site map TRUTH of test_location_plan.py, east = 1.7 x + 0.3 y + 1000 and north = 0.3 x - 1.7 y + 2000
        private static double[] Truth(double x, double y)
        {
            return new[] { 1.7 * x + 0.3 * y + 1000.0, 0.3 * x - 1.7 * y + 2000.0 };
        }

        private static double[] PageOf(double east, double north)
        {
            double e = east - 1000.0, n = north - 2000.0, det = 1.7 * -1.7 - 0.3 * 0.3;
            return new[] { (e * -1.7 - n * 0.3) / det, (n * 1.7 - e * 0.3) / det };
        }

        private static LocationPlanSettings Settings()
        {
            return new LocationPlanSettings
            {
                SizeLow = 1.4, SizeHigh = 1.6, JoinGapPt = 0.3, LineTolerancePt = 0.05, Patterns = Patterns, CodeKinds = new List<string> { "clientCode", "openSpace", "landUse" },
                PairDyLow = 1.3, PairDyHigh = 1.8, PairDxPt = 1.5, FitNeighbourhood = "IMC", FitDropFactor = 3, FitRounds = 3, FitFloorM = 0.001,
                FormOfSuffix = new Dictionary<string, string> { ["BB"] = "Duplex", ["SA"] = "Villa", ["QP"] = "Duplex" }, ControlSearchPt = 120,
                MarkWidthPt = 3.96, MarkHeightPt = 3.96, MarkTolerancePt = 0.01, MarkPoints = 6,
                Neighbourhoods = new List<KeyValuePair<string, LocationPlanSettings.ScheduleEntry>> { new KeyValuePair<string, LocationPlanSettings.ScheduleEntry>("IMC", new LocationPlanSettings.ScheduleEntry { Schedule = "imc.xlsx", Shift = 10 }) },
                ScheduleSheet = "MP",
            };
        }

        [Fact]
        public void R351_characters_join_into_lines_and_split_at_a_gap_as_location_plan_text_lines()
        {
            List<PageChar> chars = Chars("DP4 (SA)", 10, 5).Concat(Chars("2172", 11, 6.56)).Concat(Chars("VL4", 30, 5)).Concat(Chars("9", 10, 5, 7)).ToList();
            List<TextLine> found = LocationPlan.TextLines(chars, 1.4, 1.6, 0.3, 0.05);
            Assert.Equal(new[] { "DP4 (SA)", "VL4", "2172" }, found.Select(line => line.Text));
            Assert.Equal((10 + 10 + 7 * 0.7 + 0.65) / 2, found[0].X, 12);
            string[] kinds = new[] { "2172", "TH3-0 (BB)", "DP1-SA", "VL3-0", "TH1-IM", "C10", "OS", "WADI PARK", "?" }.Select(text => LocationPlan.Classify(text, Patterns)).ToArray();
            Assert.Equal(new[] { "number", "clientCode", "clientCode", "clientCode", "clientCode", "clientCode", "openSpace", "landUse", "other" }, kinds);
        }

        [Fact]
        public void R351_a_code_pairs_with_the_number_just_under_it_one_to_one()
        {
            var codes = new List<TextLine> { new TextLine { X = 10, Top = 5 }, new TextLine { X = 20, Top = 5 }, new TextLine { X = 40, Top = 5 } };
            var numbers = new List<TextLine> { new TextLine { X = 10.2, Top = 6.56 }, new TextLine { X = 20, Top = 6.6 }, new TextLine { X = 20.5, Top = 6.5 }, new TextLine { X = 60, Top = 6.56 } };
            List<int> loneCodes, loneNumbers;
            List<int[]> pairs = LocationPlan.PairLabels(codes, numbers, 1.3, 1.8, 1.5, out loneCodes, out loneNumbers);
            Assert.Equal(new[] { "0,0", "1,1" }, pairs.Select(pair => pair[0] + "," + pair[1]).OrderBy(text => text));
            Assert.Equal(new[] { 2 }, loneCodes);
            Assert.Equal(new[] { 2, 3 }, loneNumbers);
        }

        [Fact]
        public void R351_the_robust_affine_fit_about_the_means_drops_the_outlier_and_holds_the_truth()
        {
            var page = new List<double[]>();
            for (int x = 0; x < 50; x += 10)
            {
                for (int y = 0; y < 40; y += 10)
                {
                    page.Add(new double[] { x, y });
                }
            }
            List<double[]> site = page.Select(point => Truth(point[0], point[1])).ToList();
            site[3] = new[] { site[3][0] + 30, site[3][1] };
            LocationPlan.RobustResult found = LocationPlan.RobustFit(page, site, 3, 3, 0.001);
            Assert.DoesNotContain(3, found.Keep);
            Assert.Equal(page.Count - 1, found.Keep.Count);
            Assert.Equal(30, found.Residual[3], 6);
            double[] at = found.Fit.Apply(500, 600);
            Assert.Equal(Truth(500, 600)[0], at[0], 6);
            Assert.Equal(Truth(500, 600)[1], at[1], 6);
            Assert.Equal(Math.Sqrt(Math.Abs(1.7 * -1.7 - 0.3 * 0.3)), found.Fit.Scale, 9);
            Assert.Equal(new[] { 2.0, 2.0, 3.0 }, LocationPlan.Stats(new[] { 3.0, 1.0, 2.0 }));
        }

        private static IEnumerable<PageCurve> Curve(double x, double y, double size, int points)
        {
            yield return new PageCurve { X0 = x - size / 2, X1 = x + size / 2, Top = y - size / 2, Bottom = y + size / 2, Points = points };
        }

        // The words of E = <east> N = <north> at (start x, top, east, north), as test_location_plan.control_words gives them
        private static List<PageWord> ControlWords(IEnumerable<double[]> texts)
        {
            var found = new List<PageWord>();
            foreach (double[] text in texts)
            {
                string[] parts = { "E", "=", text[2].ToString("0.000", CultureInfo.InvariantCulture), "N", "=", text[3].ToString("0.000", CultureInfo.InvariantCulture) };
                found.AddRange(parts.Select((part, k) => new PageWord { Text = part, X0 = text[0] + k * 5.0, Top = text[1] }));
            }
            return found;
        }

        [Fact]
        public void R351_the_marks_are_the_curves_of_the_mark_shape_and_the_similarity_of_three_marks_almost_on_one_line_holds_a_far_point()
        {
            var marks = new[] { new[] { 100.0, 100.0 }, new[] { 500.0, 100.6 }, new[] { 900.0, 101.0 } };
            var noise = new[] { new[] { 0.02, -0.01 }, new[] { -0.015, 0.02 }, new[] { 0.01, 0.005 } };
            List<double[]> stated = marks.Select((mark, k) => { double[] at = Truth(mark[0], mark[1]); return new[] { at[0] + noise[k][0], at[1] + noise[k][1] }; }).ToList();
            List<PageWord> words = ControlWords(marks.Select((mark, k) => new[] { mark[0] + 20.0, mark[1] - 8.0, stated[k][0], stated[k][1] }));
            List<PageCurve> curves = marks.SelectMany(mark => Curve(mark[0], mark[1], 3.96, 6)).Concat(marks.SelectMany(mark => Curve(mark[0] + 18.0, mark[1] - 7.0, 2.0, 4)))
                .Concat(marks.SelectMany(mark => Curve(mark[0] + 19.0, mark[1] - 7.5, 3.96, 5))).ToList();
            List<LocationPlan.ControlText> texts = LocationPlan.ControlTexts(words);
            Assert.Equal(3, texts.Count);
            Assert.Equal(stated[1][0], texts[1].East, 3);
            List<LocationPlan.ControlMark> found = LocationPlan.ControlMarks(curves, texts, Settings());
            Assert.Equal(marks.Select(mark => mark[0]), found.Select(mark => mark.X));
            PageFit start = LocationPlan.SimilarityFit(found.Select(mark => new[] { mark.X, mark.Y }).ToList(), found.Select(mark => new[] { mark.East, mark.North }).ToList());
            double[] far = start.Apply(500, 600), truth = Truth(500, 600);
            Assert.True(LocationPlan.Hypot(far[0] - truth[0], far[1] - truth[1]) < 0.05);
            // A text with no curve of the shape in its search, or with two, is refused, never a mark by its nearness
            Assert.Contains("R387", Assert.Throws<InvalidDataException>(() => LocationPlan.ControlMarks(curves.Skip(3), texts, Settings())).Message);
            Assert.Contains("R387", Assert.Throws<InvalidDataException>(() => LocationPlan.ControlMarks(curves.Concat(Curve(105.0, 95.0, 3.96, 6)), texts, Settings())).Message);
        }

        [Fact]
        public void R351_words_are_cut_as_pdfplumber_extract_words_cuts_them_and_a_control_text_is_read_from_them()
        {
            var chars = new List<PageChar>();
            double x = 50;
            foreach (char letter in "E = 570.25 N = 12.5")
            {
                chars.Add(new PageChar { Text = letter.ToString(), X0 = x, X1 = x + 3, Top = 40, Bottom = 46, Size = 6.2, Upright = true });
                x += 3;
            }
            // A character of another size starts a run of its own, a far one a word of its own
            chars.Add(new PageChar { Text = "Z", X0 = 200, X1 = 203, Top = 40.5, Bottom = 46.5, Size = 6.2, Upright = true });
            List<PageWord> words = LocationPlan.Words(chars);
            Assert.Equal(new[] { "E", "=", "570.25", "N", "=", "12.5", "Z" }, words.Select(word => word.Text));
            List<LocationPlan.ControlText> texts = LocationPlan.ControlTexts(words);
            Assert.Single(texts);
            Assert.Equal(570.25, texts[0].East);
            Assert.Equal(12.5, texts[0].North);
            Assert.Equal(50, texts[0].Word.X0);
        }

        private static JObject Face(string label, double x0, double y0, double x1, double y1)
        {
            return new JObject { ["kind"] = "plot_shape", ["plot"] = label, ["vertices"] = new JArray(new JArray(x0, y0), new JArray(x1, y0), new JArray(x1, y1), new JArray(x0, y1)) };
        }

        // Six faces of 10 m in a row on the site, labelled 101 to 106, as test_location_plan.square_faces
        private static Dictionary<string, PlanFace> SquareFaces()
        {
            var plots = new JArray();
            for (int k = 0; k < 6; k++)
            {
                plots.Add(Face((101 + k).ToString(CultureInfo.InvariantCulture), 1000.0 + 10 * k, 1900.0, 1010.0 + 10 * k, 1910.0));
            }
            return LocationPlan.Faces(plots, 0.000001);
        }

        [Fact]
        public void R351_a_face_numbered_otherwise_is_listed_apart_with_its_reason_and_the_check_numbers_nothing()
        {
            Dictionary<string, PlanFace> faces = SquareFaces();
            List<PlanFace> list = faces.Values.ToList();
            var sites = new List<double[]>();
            foreach (string name in new[] { "101", "102", "103", "104", "105", "106" })
            {
                sites.Add(new[] { faces[name].CentroidX, faces[name].CentroidY });
            }
            sites.Add(new[] { 1029.97, 1905.0 });
            // The schedule fit lies 0.05 m east of the truth and the control fit is the truth, so label 1007, 0.03 m inside face 103 by the
            // truth, goes on face 104 by the schedule fit, 0.02 m from its edge
            var rows = sites.Select((site, k) => new LocationPlan.Row { Number = (1001 + k).ToString(CultureInfo.InvariantCulture), Kind = "clientCode", East = site[0] + 0.05, North = site[1] }).ToList();
            foreach (LocationPlan.Row row in rows)
            {
                row.Face = LocationPlan.FaceOf(list, row.East, row.North);
            }
            List<string> control = sites.Select(site => LocationPlan.FaceOf(list, site[0], site[1])).ToList();
            List<LocationPlan.ListedFace> listed = LocationPlan.NumberedOtherwise(rows, control, sites, faces);
            Assert.Equal(new[] { "103|1003|1003,1007", "104|1004,1007|1004" }, listed.Select(item => item.Face + "|" + string.Join(",", item.Schedule) + "|" + string.Join(",", item.Control)));
            LocationPlan.Why why = listed[0].Why[0];
            Assert.Equal("1007", why.Number);
            Assert.Equal("104", why.ScheduleFace);
            Assert.Equal("103", why.ControlFace);
            Assert.Equal(0.02, why.EdgeM.Value, 6);
            Assert.Equal(0.05, why.GapM, 6);
            Assert.Equal("label 1007 on face 104 by the schedule fit and on face 103 by the control fit, 0.02 m from the edge of face 104, the fits 0.05 m apart at it", why.Text);
            Assert.Empty(LocationPlan.NumberedOtherwise(rows.Take(1).ToList(), new[] { "101" }, sites.Take(1).ToList(), faces));
        }

        [Fact]
        public void R351_a_point_on_no_face_or_on_two_faces_has_no_face_and_the_centroid_of_a_face_of_two_rings_is_their_weighted_centroid()
        {
            var plots = new JArray { Face("7", 0, 0, 10, 10), Face("7", 10, 0, 30, 10), Face("8", 100, 0, 110, 10), Face("9", 105, 0, 115, 10), Face("x", 0, 50, 5, 55) };
            Dictionary<string, PlanFace> faces = LocationPlan.Faces(plots, 0.000001);
            Assert.Equal(new[] { "7", "8", "9" }, faces.Keys.OrderBy(key => key));
            Assert.Equal((5 * 100 + 20 * 200) / 300.0, faces["7"].CentroidX, 9);
            List<PlanFace> list = faces.Values.ToList();
            Assert.Equal("7", LocationPlan.FaceOf(list, 20, 5));
            Assert.Null(LocationPlan.FaceOf(list, 107, 5));
            Assert.Null(LocationPlan.FaceOf(list, 50, 5));
            Assert.Equal(1.0, LocationPlan.EdgeDistance(faces["7"], 15, 9), 9);
        }

        // test_location_steps.made_page: six faces of 10 m in two rows of three labelled 101 to 106, a label of code and number at the
        // centre of each, numbered 111 to 116 by the IMC schedule of shift 10, and three control texts E = N = with the mark of each when
        // marks is true
        private static LocationPlan.Result MadeNumbering(bool marks, bool twice = false)
        {
            var plots = new JArray();
            for (int k = 0; k < 6; k++)
            {
                plots.Add(Face((101 + k).ToString(CultureInfo.InvariantCulture), 1000.0 + 10 * (k % 3), 1900.0 + 10 * (k / 3), 1010.0 + 10 * (k % 3), 1910.0 + 10 * (k / 3)));
            }
            Dictionary<string, PlanFace> faces = LocationPlan.Faces(plots, 0.000001);
            string[] codes = { "DP1 (QP)", "DP1-SA", "TH3-0 (BB)", "VL3-0", "DP1", "XX9" };
            var chars = new List<PageChar>();
            for (int k = 0; k < 6; k++)
            {
                double[] at = PageOf(1005.0 + 10 * (k % 3), 1905.0 + 10 * (k / 3));
                string number = (111 + k).ToString(CultureInfo.InvariantCulture);
                chars.AddRange(Chars(codes[k], at[0] - ((codes[k].Length - 1) * 0.7 + 0.65) / 2, at[1] - 1.28));
                chars.AddRange(Chars(number, at[0] - ((number.Length - 1) * 0.7 + 0.65) / 2, at[1] + 0.28));
            }
            var curves = new List<PageCurve>();
            // The middle mark lies 2 m off the line through the other two, so the similarity of the marks takes its mirror by their residuals and
            // never by the binary noise of three marks on one line
            foreach (double[] site in new[] { new[] { 500.0, 1880.0 }, new[] { 1030.0, 1882.5 }, new[] { 1560.0, 1881.0 } })
            {
                double[] mark = PageOf(site[0], site[1]);
                double x = mark[0] + 20.0;
                foreach (string word in new[] { "E", "=", site[0].ToString("0.000", CultureInfo.InvariantCulture), "N", "=", site[1].ToString("0.000", CultureInfo.InvariantCulture) })
                {
                    chars.AddRange(Chars(word, x, mark[1] - 8.0, 2.0));
                    x += word.Length * 0.7 + 6.0;
                }
                if (marks)
                {
                    curves.AddRange(Curve(mark[0], mark[1], 3.96, 6));
                }
                if (twice)
                {
                    curves.AddRange(Curve(mark[0] + 5.0, mark[1] - 5.0, 3.96, 6));
                }
            }
            var officials = new Dictionary<string, HashSet<string>> { ["IMC"] = new HashSet<string> { "111", "112", "113", "114", "115", "116" } };
            var sales = Enumerable.Range(111, 5).Select(plot => new Dictionary<string, string> { ["plot"] = plot.ToString(CultureInfo.InvariantCulture), ["typology"] = "DP1", ["nbh"] = "012", ["zone"] = "Zone 2" }).ToList();
            var table = new List<Dictionary<string, string>> { Row("Duplex", "DP1", "DP1"), Row("Villa", "DP1", "VD1") };
            return LocationPlan.Number(chars, curves, faces, officials, sales, table, Settings());
        }

        [Fact]
        public void R387_a_control_check_that_fails_is_an_issue_and_the_numbering_goes_on()
        {
            LocationPlan.Result checkedResult = MadeNumbering(true);
            Assert.Null(checkedResult.Control.Failed);
            Assert.Equal(3, checkedResult.Control.Marks);
            Assert.Equal(6, checkedResult.Control.RefineFaces);
            // No mark under the control texts: the check fails with why, every label is numbered as before and no face is listed apart
            LocationPlan.Result failed = MadeNumbering(false);
            Assert.Contains("curves of the mark shape", failed.Control.Failed);
            Assert.Contains("R387", failed.Control.Failed);
            Assert.Equal(checkedResult.Rows.Select(row => row.Number + "|" + row.Face), failed.Rows.Select(row => row.Number + "|" + row.Face));
            Assert.Empty(failed.Control.Listed);
            Dictionary<string, object> step = LocationPlan.CheckStep(failed)["control"];
            Assert.Equal(failed.Control.Failed, step["failed"]);
            Assert.Null(step["marks"]);
            Assert.Empty(LocationPlan.FacesStep(failed));
            Assert.Null(LocationPlan.CheckStep(checkedResult)["control"]["failed"]);
            // Two marks under each control text: the check fails, the labels keep their faces
            LocationPlan.Result twice = MadeNumbering(true, true);
            Assert.Contains("has 2 curves of the mark shape", twice.Control.Failed);
            Assert.Equal(checkedResult.Rows.Select(row => row.Number + "|" + row.Face), twice.Rows.Select(row => row.Number + "|" + row.Face));
        }

        // test_location_plan.made_pdf: a PDF of one page of 600 by 400 pt with a text and a stroked path, extra added to its page dictionary
        private static string MadePdf(string path, string extra)
        {
            string content = "BT /F1 10 Tf 100 300 Td (AB12) Tj ET 200 200 m 210 205 l 220 200 l S";
            string[] objects =
            {
                "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 400]" + extra + " /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
                "<< /Length " + content.Length + " >>\nstream\n" + content + "\nendstream", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            };
            var text = new System.Text.StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();
            for (int number = 1; number <= objects.Length; number++)
            {
                offsets.Add(text.Length);
                text.Append(number + " 0 obj\n" + objects[number - 1] + "\nendobj\n");
            }
            int start = text.Length;
            text.Append("xref\n0 " + (objects.Length + 1) + "\n0000000000 65535 f \n");
            foreach (int offset in offsets)
            {
                text.Append(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
            }
            text.Append("trailer\n<< /Size " + (objects.Length + 1) + " /Root 1 0 R >>\nstartxref\n" + start + "\n%%EOF\n");
            File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes(text.ToString()));
            return path;
        }

        [Fact]
        public void R351_a_page_whose_crop_box_is_not_its_media_box_is_refused_as_python_refuses_it()
        {
            // pdfplumber gives the points of the media box whatever the CropBox, PdfPig shifts them by the crop origin, so neither reads it
            foreach (string extra in new[] { " /CropBox [50 50 550 350]", " /CropBox [50 50 550 350] /Rotate 90" })
            {
                InvalidDataException refused = Assert.Throws<InvalidDataException>(() => PdfPage.Read(MadePdf(Path.Combine(folder, "crop.pdf"), extra)));
                Assert.Contains("CropBox", refused.Message);
                Assert.Contains("MediaBox", refused.Message);
            }
            foreach (string extra in new[] { "", " /CropBox [0 0 600 400]" })
            {
                PdfPage.Content page = PdfPage.Read(MadePdf(Path.Combine(folder, "plain.pdf"), extra));
                Assert.Equal("AB12", string.Concat(page.Chars.Select(item => item.Text)));
                Assert.Equal(100.0, page.Chars[0].X0, 6);
            }
        }

        // test_face_index.RINGS: face 5 drawn as two rings that share the edge x = 10, face 6 one ring that shares the edge x = 20 with the
        // second ring of 5
        private static List<PlanFace> EdgeFaces()
        {
            var plots = new JArray { Face("5", 0, 0, 10, 10), Face("5", 10, 0, 20, 10), Face("6", 20, 0, 30, 10) };
            return LocationPlan.Faces(plots, 0.000001).Values.ToList();
        }

        [Fact]
        public void R351_a_label_on_the_edge_two_rings_of_one_label_share_is_on_that_face_as_the_union_of_its_rings_holds_it()
        {
            List<PlanFace> faces = EdgeFaces();
            var index = new FaceIndex(faces);
            foreach (Func<double, double, string> faceOf in new Func<double, double, string>[] { (x, y) => LocationPlan.FaceOf(faces, x, y), index.FaceOf })
            {
                Assert.Equal("5", faceOf(10.0, 5.0));
                Assert.Equal("5", faceOf(10.0, 0.0));
                Assert.Equal("5", faceOf(5.0, 5.0));
                Assert.Equal("5", faceOf(15.0, 5.0));
                Assert.Equal("6", faceOf(25.0, 5.0));
                // On the edge of two faces, or on the outline of one ring alone, no face holds the point
                Assert.Null(faceOf(20.0, 5.0));
                Assert.Null(faceOf(0.0, 5.0));
                Assert.Null(faceOf(35.0, 5.0));
            }
            Assert.Empty(index.Holding(20.0, 5.0));
        }

        // test_face_index.made_rings: a grid of 12 by 9 squares of 10 m at site coordinates, a face of two squares that share an edge where
        // (7 row + 3 column) mod 10 is under 3, labels by row and column
        private static List<PlanFace> MadeRings()
        {
            var plots = new JArray();
            double x0 = 571000.0, y0 = 2364000.0;
            for (int row = 0; row < 9; row++)
            {
                int column = 0;
                while (column < 12)
                {
                    string label = (100 + row * 12 + column).ToString(CultureInfo.InvariantCulture);
                    int width = column < 11 && (7 * row + 3 * column) % 10 < 3 ? 2 : 1;
                    for (int k = 0; k < width; k++)
                    {
                        plots.Add(Face(label, x0 + 10 * (column + k), y0 + 10 * row, x0 + 10 * (column + k + 1), y0 + 10 * (row + 1)));
                    }
                    column += width;
                }
            }
            return LocationPlan.Faces(plots, 0.000001).Values.ToList();
        }

        private static int PyMod(int value, int modulus)
        {
            return ((value % modulus) + modulus) % modulus;
        }

        // test_face_index.made_points: a lattice of 2.5 m over the grid and around it, on its edges and vertices too, and a point at 3
        // decimals near each, the remainder of Python, never negative
        private static List<double[]> MadePoints()
        {
            var points = new List<double[]>();
            for (int i = -4; i < 53; i++)
            {
                for (int k = -4; k < 41; k++)
                {
                    points.Add(new[] { 571000.0 + 2.5 * i, 2364000.0 + 2.5 * k });
                    points.Add(new[] { 571000.0 + 2.5 * i + 0.001 * (PyMod(i * 7 + k * 3, 5) - 2), 2364000.0 + 2.5 * k + 0.001 * (PyMod(i * 3 + k * 5, 5) - 2) });
                }
            }
            return points;
        }

        [Fact]
        public void R351_the_grid_gives_every_point_the_faces_a_scan_of_every_face_gives()
        {
            List<PlanFace> faces = MadeRings();
            var index = new FaceIndex(faces);
            int one = 0;
            List<double[]> points = MadePoints();
            foreach (double[] point in points)
            {
                List<string> scan = faces.Where(face => LocationPlan.Holds(face, point[0], point[1])).Select(face => face.Label).OrderBy(label => long.Parse(label, CultureInfo.InvariantCulture)).ToList();
                Assert.Equal(scan, index.Holding(point[0], point[1]));
                Assert.Equal(scan.Count == 1 ? scan[0] : null, index.FaceOf(point[0], point[1]));
                Assert.Equal(scan.Count == 1 ? scan[0] : null, LocationPlan.FaceOf(faces, point[0], point[1]));
                one += scan.Count == 1 ? 1 : 0;
            }
            // test_face_index counts the same on the same rings and points
            Assert.Equal(new[] { FaceIndexCounts.Faces, FaceIndexCounts.Points, FaceIndexCounts.One }, new[] { faces.Count, points.Count, one });
            Assert.Empty(new FaceIndex(new List<PlanFace>()).Holding(0.0, 0.0));
        }

        // test_face_index.FACES, POINTS and ONE
        private static class FaceIndexCounts
        {
            public const int Faces = 78;
            public const int Points = 5130;
            public const int One = 2715;
        }

        // test_face_index.SPIKE_CELLS and SPIKE_ONE
        private static class SpikeCounts
        {
            public const int Cells = 130;
            public const int One = 2730;
        }

        [Fact]
        public void R351_a_face_with_a_far_vertex_is_scanned_apart_and_the_grid_stays_bounded()
        {
            // test_face_index: the made up faces and face 999, a simple ring with a spike 5 km west and south of them, whose box would cover
            // about 250,000 cells of 10 m
            List<PlanFace> faces = MadeRings();
            var spike = new PlanFace { Label = "999" };
            spike.Rings.Add(new[] { new[] { 571120.0, 2364000.0 }, new[] { 571130.0, 2364000.0 }, new[] { 571130.0, 2364010.0 }, new[] { 566000.0, 2359000.0 } });
            faces.Add(spike);
            var index = new FaceIndex(faces);
            Assert.Equal(new[] { "999" }, index.Wide);
            Assert.True(index.CellCount <= FaceIndex.MaxCellsPerFace * faces.Count, "cells " + index.CellCount);
            int one = 0;
            foreach (double[] point in MadePoints().Concat(new[] { new[] { 571125.0, 2364004.0 }, new[] { 568500.0, 2361500.5 }, new[] { 566000.0, 2359000.0 } }))
            {
                List<string> scan = faces.Where(face => LocationPlan.Holds(face, point[0], point[1])).Select(face => face.Label).OrderBy(label => long.Parse(label, CultureInfo.InvariantCulture)).ToList();
                Assert.Equal(scan, index.Holding(point[0], point[1]));
                one += scan.Count == 1 ? 1 : 0;
            }
            Assert.Equal(new[] { SpikeCounts.Cells, SpikeCounts.One }, new[] { index.CellCount, one });
        }

        [Fact]
        public void R351_a_face_whose_cells_cannot_be_counted_in_range_goes_to_the_wide_list_and_is_never_lost()
        {
            // test_face_index: 5 faces of 10 m in a row and face 999 with a vertex 1e9, 1e17 and 1e20 m west, the probe of the third review,
            // where a column past the range of a long cast lost the face
            string[] all = { "101", "102", "103", "104", "105", "999" };
            foreach (System.Tuple<double, string[]> far in new[] { System.Tuple.Create(1e9, new[] { "999" }), System.Tuple.Create(1e17, all), System.Tuple.Create(1e20, all) })
            {
                var faces = new List<PlanFace>();
                for (int k = 0; k < 5; k++)
                {
                    var face = new PlanFace { Label = (101 + k).ToString(CultureInfo.InvariantCulture) };
                    face.Rings.Add(new[] { new[] { 1000.0 + 10 * k, 2000.0 }, new[] { 1010.0 + 10 * k, 2000.0 }, new[] { 1010.0 + 10 * k, 2010.0 }, new[] { 1000.0 + 10 * k, 2010.0 } });
                    faces.Add(face);
                }
                var spike = new PlanFace { Label = "999" };
                spike.Rings.Add(new[] { new[] { 1000.0, 2020.0 }, new[] { 1010.0, 2020.0 }, new[] { 1010.0, 2030.0 }, new[] { -far.Item1, 2025.0 } });
                faces.Add(spike);
                var index = new FaceIndex(faces);
                Assert.Equal(far.Item2, index.Wide);
                foreach (double[] point in new[] { new[] { 1005.0, 2024.0 }, new[] { 1025.0, 2005.0 }, new[] { 1045.0, 2009.0 }, new[] { -far.Item1 / 2, 2025.0 }, new[] { 1e30, 2000.0 }, new[] { -1e30, 2025.0 } })
                {
                    List<string> scan = faces.Where(face => LocationPlan.Holds(face, point[0], point[1])).Select(face => face.Label).OrderBy(label => long.Parse(label, CultureInfo.InvariantCulture)).ToList();
                    Assert.Equal(scan, index.Holding(point[0], point[1]));
                }
                Assert.Equal(new[] { "999" }, index.Holding(1005.0, 2024.0));
                Assert.Equal(new[] { "103" }, index.Holding(1025.0, 2005.0));
            }
        }

        [Fact]
        public void R351_new_codes_come_by_the_exact_row_by_the_form_and_by_the_base_as_location_plan_new_code()
        {
            var table = new List<Dictionary<string, string>>
            {
                Row("Duplex", "DP1", "DP1"), Row("Villa", "DP1", "VD1"), Row("Duplex", "DP2-0 (BB)", "DP2"), Row(null, "DP2", null), Row(null, "TH3-0 (BB)", "DT3"), Row(null, "VL3-0", "VL6"),
            };
            var formOf = new Dictionary<string, string> { ["BB"] = "Duplex", ["SA"] = "Villa", ["QP"] = "Duplex" };
            string[] found = new[] { "TH3-0 (BB)", "VL3-0", "DP1-SA", "DP1 (QP)", "DP2 (BB)", "XX9", "WADI" }.Select(code => LocationPlan.NewCode(code, table, formOf, Code)).ToArray();
            Assert.Equal(new[] { "DT3", "VL6", "VD1", "DP1", "DP2", null, null }, found);
        }

        private static Dictionary<string, string> Row(string form, string old, string made)
        {
            return new Dictionary<string, string> { ["form"] = form, ["old"] = old, ["new"] = made };
        }

        [Fact]
        public void R351_a_table_is_read_by_its_header_names_as_location_plan_read_table()
        {
            var rows = new List<object[]>
            {
                new object[] { "title", null, null },
                new object[] { "Plot No", "Typology_Rev9", "x" },
                new object[] { 428L, "VD4", 1L },
                new object[] { 429.0, " DP2 ", null },
                new object[] { null, null, null },
            };
            var columns = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("plot", "Plot No"), new KeyValuePair<string, string>("typology", "Typology_Rev9") };
            List<Dictionary<string, string>> found = LocationPlan.Table(rows, 2, columns, "the sheet S");
            Assert.Equal(new[] { "428|VD4", "429.0|DP2" }, found.Select(item => item["plot"] + "|" + item["typology"]));
            Assert.Throws<InvalidDataException>(() => LocationPlan.Table(rows, 2, new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("zone", "Zone") }, "the sheet S"));
        }

        [Fact]
        public void R351_the_official_numbers_of_a_schedule_are_read_by_r9_from_the_plot_no_header_to_the_first_blank_or_total_row()
        {
            var rows = new List<object[]>
            {
                new object[] { "Schedule", null, null, null, null, null, null },
                new object[] { "Plot No.", "Unit", "FFL *", "SSL", "El @ Entry", "Steps Needed", null },
                new object[] { "2290", "DP1", 10.5, 10.3, 10.1, "2", null },
                new object[] { 2291L, "DP1", 10.5, 10.3, 10.1, 2L, null },
                new object[] { "Total", null, 1.0, null, null, null, null },
                new object[] { 2292L, "DP1", 10.5, 10.3, 10.1, 2L, null },
            };
            Assert.Equal(new[] { "2290", "2291" }, ScheduleRows.Officials(rows, "imc.xlsx").OrderBy(text => text));
            rows[3][3] = "level";
            Assert.Contains("no number", Assert.Throws<InvalidDataException>(() => ScheduleRows.Officials(rows, "imc.xlsx")).Message);
            Assert.Contains("Plot No.", Assert.Throws<InvalidDataException>(() => ScheduleRows.Officials(rows.Skip(2).ToList(), "imc.xlsx")).Message);
        }

        [Fact]
        public void R351_the_settings_of_the_location_plan_and_the_schedules_are_read_from_config()
        {
            LocationPlanSettings settings = LocationPlanSettings.Load(Path.Combine(RepoRoot(), "config", "project.json"));
            Assert.Equal("IMC", settings.FitNeighbourhood);
            Assert.Contains(settings.Neighbourhoods, hood => hood.Key == settings.FitNeighbourhood);
            Assert.Equal("number", settings.Patterns[0].Key);
            Assert.Equal(6, settings.MarkPoints);
            Assert.False(string.IsNullOrEmpty(settings.ScheduleSheet));
            Assert.Equal("accommodation", settings.Sales.Workbook);
        }

        private static string RepoRoot()
        {
            string at = AppContext.BaseDirectory;
            while (at != null && !File.Exists(Path.Combine(at, "config", "project.json")))
            {
                at = Path.GetDirectoryName(at);
            }
            return at ?? throw new InvalidOperationException("no repo above " + AppContext.BaseDirectory);
        }

        // A PDF of one page made by the writer of PdfPig: a text of Helvetica at a known place and a circle
        private string MadePdf()
        {
            var builder = new PdfDocumentBuilder();
            PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
            PdfPageBuilder page = builder.AddPage(400, 300);
            page.AddText("E = 12.5", 10, new PdfPoint(50, 200), font);
            page.DrawCircle(new PdfPoint(100, 100), 3.96, 1);
            string path = Path.Combine(folder, "page.pdf");
            File.WriteAllBytes(path, builder.Build());
            return path;
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        [Fact]
        public void R351_a_read_of_the_location_plan_with_no_baseplan_is_refused_and_its_steps_need_the_plan_the_baseplan_the_schedules_and_the_sales()
        {
            string config = Path.Combine(folder, "project.json");
            File.Copy(WorkerRunTests.RepoConfig(), config);
            string pdf = MadePdf();
            Func<string, List<string>, WorkerAnswer> run = (id, steps) =>
            {
                var request = new WorkerRequest
                {
                    FormatVersion = 1, RunId = id, Command = "read", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                    Inputs = new List<WorkerFile> { Input("config", config), Input(LocationPlan.PlotNumbersRole, pdf) }, Scope = new WorkerScope { Kind = "project" },
                    OutputFolder = Path.Combine(folder, "out"), Steps = steps, StepsFolder = Path.Combine(folder, "steps"), TimeLimitS = 600,
                };
                int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
                WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, id));
                Assert.True(code == 2, "exit " + code + ": " + answer.Message);
                return answer;
            };
            Assert.Contains("needs the input baseplan in the same request", run("l1", new List<string>()).Message);
            Assert.Contains("needs the input baseplan", run("l2", new List<string> { LocationPlan.LabelStepName }).Message);
            foreach (string step in new[] { LocationPlan.LabelStepName, LocationPlan.FitStepName, LocationPlan.CheckStepName, LocationPlan.FacesStepName })
            {
                Assert.Equal(new[] { "plotNumbers", "baseplan", "schedule:*", "sales" }, WorkerRunner.Steps[step]);
                Assert.Equal("read", WorkerRunner.StepCommand[step]);
            }
            Assert.False(File.Exists(Path.Combine(folder, "out", "read_locationPlan.json")));
        }

        [Fact]
        public void R351_the_pdf_page_gives_the_character_boxes_pdfminer_gives_from_the_descent_and_one_font_size()
        {
            PdfPage.Content page = PdfPage.Read(MadePdf());
            Assert.Equal(300, page.Height);
            Assert.Equal("E = 12.5", string.Concat(page.Chars.Select(c => c.Text)));
            PageChar first = page.Chars[0];
            // Helvetica descends 207 of 1000, its box runs from there to one font size over it, top measured from the top of the page
            Assert.Equal(50, first.X0, 9);
            Assert.Equal(10, first.Size, 9);
            Assert.Equal(300 - (200 + (1 - 0.207) * 10), first.Top, 9);
            Assert.Equal(300 - (200 - 0.207 * 10), first.Bottom, 9);
            Assert.True(first.Upright);
            Assert.True(page.Chars.All(c => c.X1 > c.X0));
            List<LocationPlan.ControlText> texts = LocationPlan.ControlTexts(LocationPlan.Words(page.Chars).Concat(new[] { new PageWord { Text = "N" }, new PageWord { Text = "=" }, new PageWord { Text = "7" } }).ToList());
            Assert.Single(texts);
            // The circle of the writer is a curve of a start and four segments with no close, five points, its box the box of those points
            PageCurve curve = Assert.Single(page.Curves);
            Assert.Equal(5, curve.Points);
            Assert.Equal(3.96, curve.X1 - curve.X0, 6);
            Assert.Equal(300 - (100 + 1.98), curve.Top, 6);
        }
    }
}
