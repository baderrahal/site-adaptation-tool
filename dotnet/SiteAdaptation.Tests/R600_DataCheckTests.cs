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
    // R600 to R606. The data check of stage 1 on made up reads: the settings, the groups found by distance, the hand of a villa block, the
    // marks and plain lines, and the worker runner taking an empty SHA-256 for dataCheck only
    public sealed class R600_DataCheckTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r600_" + Guid.NewGuid().ToString("N"));

        public R600_DataCheckTests()
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

        private static JObject Config()
        {
            return new JObject
            {
                [DataCheckSettings.Block] = new JObject
                {
                    ["scan"] = new JObject
                    {
                        ["labelPattern"] = @"^(?:(.*?)\s+)?(\S*\d\S*)$", ["tokenSplit"] = "[^A-Za-z0-9]+", ["plotNumberShifts"] = new JArray(0), ["pairMinSegments"] = 100,
                        ["pairThreads"] = 1, ["pairTimeCapS"] = 60, ["pdfJoinGapPt"] = 0.3, ["pdfLineTolerancePt"] = 0.05, ["pdfPairDyPt"] = new JArray(1.3, 1.8), ["pdfPairDxPt"] = 1.5,
                    },
                    ["groupGapsM"] = new JArray(100.0, 1.0), ["wordReachM"] = 30, ["wordShare"] = 0.9, ["wordMinGroups"] = 3, ["roadReachM"] = 20, ["roadMinSegments"] = 2, ["roadOutsideShare"] = 0.5,
                    ["drawingPeak"] = new JObject { ["dwg"] = 252, ["dxf"] = 106 },
                    ["strongShare"] = 0.9, ["usableShare"] = 0.5, ["prototypeFilePattern"] = "(?:^|-)ARC-([A-Za-z0-9]+)(?:-[A-Za-z0-9]{2})?-MOD",
                },
            };
        }

        private static DataCheckSettings Settings()
        {
            return DataCheckSettings.From(Config());
        }

        private static JArray Pt(double x, double y)
        {
            return new JArray(x, y);
        }

        private static JObject Face(double x, double y, double w, double d)
        {
            return new JObject
            {
                ["ring"] = new JArray(Pt(x, y), Pt(x + w, y), Pt(x + w, y + d), Pt(x, y + d)), ["centroid"] = Pt(x + w / 2, y + d / 2), ["area"] = w * d,
            };
        }

        // Five plots of 12 by 20 m: 1 and 2 touch, 3 and 4 touch and lie 10 m above them, 5 lies 300 m away. Hands are the mirror of each insert
        private static JObject Drawing(bool[] hands, string numberText = "{0}", string[] blocks = null, bool addInsertBothHands = false)
        {
            var origins = new[] { Tuple.Create(0.0, 0.0), Tuple.Create(12.0, 0.0), Tuple.Create(0.0, 30.0), Tuple.Create(12.0, 30.0), Tuple.Create(300.0, 0.0) };
            blocks = blocks ?? new[] { "VA1", "VA1", "VB2", "VB2", "VA1" };
            var faces = new JArray();
            var texts = new JArray();
            var inserts = new JArray();
            for (int index = 0; index < origins.Length; index++)
            {
                faces.Add(Face(origins[index].Item1, origins[index].Item2, 12, 20));
                texts.Add(new JObject { ["layer"] = "N", ["text"] = string.Format(numberText, 101 + index), ["x"] = origins[index].Item1 + 3, ["y"] = origins[index].Item2 + 2 });
                inserts.Add(new JObject { ["layer"] = "H", ["block"] = blocks[index], ["x"] = origins[index].Item1 + 6, ["y"] = origins[index].Item2 + 10, ["mirrored"] = hands[index] });
            }
            if (addInsertBothHands)
            {
                inserts.Add(new JObject { ["layer"] = "H", ["block"] = "VA1", ["x"] = 8.0, ["y"] = 12.0, ["mirrored"] = !hands[0] });
            }
            texts.Add(new JObject { ["layer"] = "T", ["text"] = "Phase A", ["x"] = 0.0, ["y"] = -10.0 });
            texts.Add(new JObject { ["layer"] = "T", ["text"] = "Phase B", ["x"] = 300.0, ["y"] = -10.0 });
            var kerb = new JArray(new JArray(0.0, 20.0, 24.0, 20.0), new JArray(300.0, 20.0, 312.0, 20.0));
            return new JObject
            {
                ["source"] = "site.dxf", ["units"] = "Meters", ["layers"] = new JArray(),
                ["candidates"] = new JArray(new JObject { ["layers"] = new JArray("P"), ["faces"] = faces }),
                ["texts"] = texts, ["inserts"] = inserts, ["lines"] = new JObject { ["K"] = kerb }, ["arcs"] = new JObject(), ["notRead"] = new JArray(), ["notTraced"] = new JArray(),
            };
        }

        private static DataCheckInput Input(JObject drawing, bool prototypes = true)
        {
            var input = new DataCheckInput();
            input.CadReads.Add(drawing);
            if (prototypes)
            {
                input.PrototypeNames.Add("ARC-VA1-MOD.rvt");
                input.PrototypeNames.Add("ARC-VB2-MOD.rvt");
            }
            return input;
        }

        private static JObject NeedOf(DataCheckOutput output, string need)
        {
            return ((JArray)output.Check["needs"]).OfType<JObject>().Single(row => (string)row["need"] == need);
        }

        private static JObject PlotOf(DataCheckOutput output, string number)
        {
            return ((JArray)output.Plots["plots"]).OfType<JObject>().Single(row => (string)row["number"] == number);
        }

        [Fact]
        public void R600_the_settings_read_from_the_block_and_refuse_a_missing_or_wrong_one()
        {
            DataCheckSettings settings = Settings();
            Assert.Equal(new[] { 100.0, 1.0 }, settings.GroupGapsM);
            Assert.Equal("VA1", settings.PrototypeCode("ARC-VA1-MOD.rvt"));
            Assert.Equal("C10", settings.PrototypeCode("RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt"));
            Assert.Null(settings.PrototypeCode("notes.rvt"));
            Assert.Throws<InvalidDataException>(() => DataCheckSettings.From(new JObject()));
            JObject rising = Config();
            rising[DataCheckSettings.Block]["groupGapsM"] = new JArray(1.0, 100.0);
            Assert.Contains("must fall", Assert.Throws<InvalidDataException>(() => DataCheckSettings.From(rising)).Message);
            JObject noGroup = Config();
            noGroup[DataCheckSettings.Block]["prototypeFilePattern"] = "ARC-[A-Z0-9]+-MOD";
            Assert.Contains("names no group", Assert.Throws<InvalidDataException>(() => DataCheckSettings.From(noGroup)).Message);
            JObject noShare = Config();
            noShare[DataCheckSettings.Block]["strongShare"] = 1.5;
            Assert.Throws<InvalidDataException>(() => DataCheckSettings.From(noShare));
        }

        [Fact]
        public void R601_plots_form_groups_by_the_distance_between_their_outlines_at_each_level()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, true, false, false, false })));
            var plots = ((JArray)output.Plots["plots"]).OfType<JObject>().ToDictionary(row => (string)row["number"]);
            Assert.Equal(5, plots.Count);
            string Group(string number, int level) => (string)plots[number]["groups"][level];
            Assert.Equal(Group("101", 0), Group("104", 0));
            Assert.NotEqual(Group("101", 0), Group("105", 0));
            Assert.Equal(Group("101", 1), Group("102", 1));
            Assert.NotEqual(Group("101", 1), Group("103", 1));
            Assert.Equal(Group("103", 1), Group("104", 1));
            Assert.NotEqual(Group("105", 1), Group("101", 1));
        }

        [Fact]
        public void R601_the_distance_is_turned_into_the_unit_the_drawing_states()
        {
            JObject drawing = Drawing(new[] { false, false, false, false, false });
            drawing["units"] = "Millimeters";
            // In millimetres the plots lie 0.012 m wide, so every plot is within 100 m of the next and the far one is 0.3 m, one group at level 1
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(drawing));
            var plots = ((JArray)output.Plots["plots"]).OfType<JObject>().ToList();
            Assert.Single(plots.Select(row => (string)row["groups"][0]).Distinct());
            Assert.Equal(0.001, DataCheckSettings.MetersPerUnit("Millimeters"));
            Assert.Null(DataCheckSettings.MetersPerUnit("Parsecs"));
            drawing["units"] = "Parsecs";
            DataCheckOutput stopped = DataCheckLogic.Run(Settings(), Input(drawing));
            Assert.Equal("weak", (string)NeedOf(stopped, "groupLevels")["mark"]);
            Assert.Contains("Parsecs", (string)NeedOf(stopped, "groupLevels")["line"]);
        }

        [Fact]
        public void R602_the_hand_of_a_villa_block_is_the_mirror_of_its_insert_and_both_hands_in_a_plot_give_none()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, true, false, false, true })));
            Assert.False((bool)PlotOf(output, "101")["mirror"]);
            Assert.True((bool)PlotOf(output, "102")["mirror"]);
            Assert.True((bool)PlotOf(output, "105")["mirror"]);
            Assert.Equal("VB2", (string)PlotOf(output, "103")["villaType"]);
            // The mirror is a weak value of the check whatever the sign says, the hand of a prototype file is not known
            JObject value = ((JArray)output.Check["values"]).OfType<JObject>().Single(row => (string)row["need"] == "mirror");
            Assert.Equal("weak", (string)value["mark"]);
            Assert.Contains("5 of 5 plots", (string)value["line"]);
            Assert.Equal("missing", (string)((JArray)DataCheckLogic.Run(Settings(), new DataCheckInput()).Check["values"])[0]["mark"]);
            // Break one thing and the check names it: a second insert of the other hand in plot 101
            DataCheckOutput both = DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, true, false, false, true }, addInsertBothHands: true)));
            JObject plot = PlotOf(both, "101");
            Assert.Null((bool?)plot["mirror"]);
            Assert.Equal("missing", (string)plot["source"]["mirror"]);
            Assert.Contains("both mirrored and not mirrored", (string)plot["missing"]["mirror"]);
        }

        [Fact]
        public void R602_with_no_prototype_file_the_villa_type_is_missing_with_its_reason()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, false, false, false, false }), prototypes: false));
            JObject plot = PlotOf(output, "101");
            Assert.Null((string)plot["villaType"]);
            Assert.Equal("missing", (string)plot["source"]["villaType"]);
            Assert.Contains("no prototype file", (string)plot["missing"]["villaType"]);
            Assert.Equal("missing", (string)NeedOf(output, "villaType")["mark"]);
            Assert.Equal("missing", (string)NeedOf(output, "prototypes")["mark"]);
        }

        [Fact]
        public void R602_a_sheet_that_contradicts_the_blocks_is_said_and_makes_the_villa_type_weak()
        {
            DataCheckInput input = Input(Drawing(new[] { false, false, false, false, false }));
            DataCheckOutput agreeing = DataCheckLogic.Run(Settings(), input);
            Assert.Equal("strong", (string)NeedOf(agreeing, "villaType")["mark"]);
            Assert.DoesNotContain("another kind", (string)NeedOf(agreeing, "villaType")["line"]);
            // The sheet gives VB2 where the blocks hold VA1 on three plots of five
            input.Excels.Add(new ScanWorkbook
            {
                Name = "p.xlsx",
                Sheets = new List<ScanSheet>
                {
                    ScanSheet.Of("S", new List<IList<string>>
                    {
                        new List<string> { "Plot", "Type" },
                        new List<string> { "101", "VB2" }, new List<string> { "102", "VB2" }, new List<string> { "103", "VB2" },
                        new List<string> { "104", "VB2" }, new List<string> { "105", "VB2" },
                    }),
                },
            });
            DataCheckOutput output = DataCheckLogic.Run(Settings(), input);
            JObject need = NeedOf(output, "villaType");
            Assert.Equal("weak", (string)need["mark"]);
            Assert.Contains("another kind of source gives another type on 3", (string)need["line"]);
            // The plot keeps the code of the first kind and says what disagrees
            Assert.Equal("VA1", (string)PlotOf(output, "101")["villaType"]);
            Assert.Contains("VB2", (string)PlotOf(output, "101")["disputed"]["villaType"]);
            Assert.Empty(((JObject)PlotOf(output, "103")["disputed"]).Properties());
        }

        [Fact]
        public void R603_a_word_in_the_drawing_alone_needs_three_groups_and_two_groups_give_none_with_the_reason()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, false, false, false, false })));
            JArray levels = (JArray)output.Profile["levels"];
            // Phase A and Phase B name the 2 groups of level 1, which is under the 3 a word of the drawing alone needs
            Assert.Null((string)levels[0]["word"]);
            Assert.Equal("missing", (string)levels[0]["source"]);
            Assert.Contains("covers only 2 of 2 groups", (string)levels[0]["why"]);
            Assert.Equal("Phase", (string)levels[0]["candidates"][0]);
            Assert.Null((string)levels[1]["word"]);
            Assert.Equal("weak", (string)NeedOf(output, "groupLevels")["mark"]);
        }

        [Fact]
        public void R603_a_word_that_covers_three_groups_alone_is_taken_and_two_words_that_tie_give_none()
        {
            JObject drawing = Drawing(new[] { false, false, false, false, false });
            var texts = (JArray)drawing["texts"];
            // Level 2 has 3 groups: the row of plots 101 and 102, the row of 103 and 104 and the far plot
            foreach (var at in new[] { Tuple.Create("Block 1", 0.0, -10.0), Tuple.Create("Block 2", 0.0, 60.0), Tuple.Create("Block 3", 300.0, -10.0) })
            {
                texts.Add(new JObject { ["layer"] = "T", ["text"] = at.Item1, ["x"] = at.Item2, ["y"] = at.Item3 });
            }
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(drawing));
            Assert.Equal("Block", (string)((JArray)output.Profile["levels"])[1]["word"]);
            // Break it: a second word that also names the 3 groups, the level has two candidates and no word
            foreach (var at in new[] { Tuple.Create("Notes 1", 1.0, -9.0), Tuple.Create("Notes 2", 1.0, 59.0), Tuple.Create("Notes 3", 301.0, -9.0) })
            {
                texts.Add(new JObject { ["layer"] = "T", ["text"] = at.Item1, ["x"] = at.Item2, ["y"] = at.Item3 });
            }
            JObject level = (JObject)((JArray)DataCheckLogic.Run(Settings(), Input(drawing)).Profile["levels"])[1];
            Assert.Null((string)level["word"]);
            Assert.Equal("missing", (string)level["source"]);
            Assert.Contains("unsure between", (string)level["why"]);
            Assert.Equal(new[] { "Block", "Notes" }, level["candidates"].Select(item => (string)item).OrderBy(name => name).ToArray());
        }

        [Fact]
        public void R603_three_words_on_two_groups_never_give_a_word_even_when_one_is_the_projects_own()
        {
            // Phase A and B against Facades 1 and 2 and Notes 1 and 2 on the same two areas
            JObject drawing = Drawing(new[] { false, false, false, false, false });
            var texts = (JArray)drawing["texts"];
            foreach (var at in new[] { Tuple.Create("Facades 1", 1.0, -9.0), Tuple.Create("Facades 2", 301.0, -9.0), Tuple.Create("Notes 1", 2.0, -8.0), Tuple.Create("Notes 2", 302.0, -8.0) })
            {
                texts.Add(new JObject { ["layer"] = "T", ["text"] = at.Item1, ["x"] = at.Item2, ["y"] = at.Item3 });
            }
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(drawing));
            JObject level = (JObject)((JArray)output.Profile["levels"])[0];
            Assert.Null((string)level["word"]);
            Assert.Equal(3, level["candidates"].Count());
            Assert.Contains("unsure between", (string)NeedOf(output, "groupLevels")["line"]);
            // A header of a sheet that parts the plots the same way settles it
            DataCheckInput input = Input(drawing);
            input.Excels.Add(new ScanWorkbook
            {
                Name = "p.xlsx",
                Sheets = new List<ScanSheet>
                {
                    ScanSheet.Of("S", new List<IList<string>>
                    {
                        new List<string> { "Plot", "Phase" },
                        new List<string> { "101", "A" }, new List<string> { "102", "A" }, new List<string> { "103", "A" }, new List<string> { "104", "A" }, new List<string> { "105", "B" },
                    }),
                },
            });
            Assert.Equal("Phase", (string)((JArray)DataCheckLogic.Run(Settings(), input).Profile["levels"])[0]["word"]);
        }

        [Fact]
        public void R603_a_header_of_a_sheet_names_a_level_when_its_values_part_the_plots_as_the_groups_do()
        {
            DataCheckInput input = Input(Drawing(new[] { false, false, false, false, false }));
            input.CadReads[0]["texts"] = new JArray(((JArray)input.CadReads[0]["texts"]).Where(text => (string)text["layer"] != "T"));
            input.Excels.Add(new ScanWorkbook
            {
                Name = "p.xlsx",
                Sheets = new List<ScanSheet>
                {
                    ScanSheet.Of("S", new List<IList<string>>
                    {
                        new List<string> { "Plot", "Sector", "Cluster", "Type" },
                        new List<string> { "101", "N", "N1", "VA1" }, new List<string> { "102", "N", "N1", "VA1" }, new List<string> { "103", "N", "N2", "VB2" },
                        new List<string> { "104", "N", "N2", "VB2" }, new List<string> { "105", "S", "S1", "VA1" },
                    }),
                },
            });
            DataCheckOutput output = DataCheckLogic.Run(Settings(), input);
            JArray levels = (JArray)output.Profile["levels"];
            Assert.Equal("Sector", (string)levels[0]["word"]);
            Assert.Equal("Cluster", (string)levels[1]["word"]);
            Assert.Equal("strong", (string)NeedOf(output, "groupLevels")["mark"]);
            Assert.Equal("N1", (string)PlotOf(output, "101")["groups"][1]);
            // A column that parts the plots another way is no word of any level
            input.Excels[0].Sheets[0].Rows[0][2] = "N2";
            Assert.Null((string)((JArray)DataCheckLogic.Run(Settings(), input).Profile["levels"])[1]["word"]);
        }

        [Fact]
        public void R604_road_edges_are_the_layer_of_lines_outside_the_plots_nearest_to_most_of_them()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, false, false, false, false })));
            JObject road = NeedOf(output, "roadEdges");
            // All 5 plots lie near the layer, which proves no road, so the mark is at most usable
            Assert.Equal("usable", (string)road["mark"]);
            Assert.Contains("layer K", (string)road["line"]);
            Assert.Contains("not scored", (string)road["line"]);
        }

        [Fact]
        public void R605_every_need_has_a_mark_and_a_plain_line_of_at_most_160_characters_even_when_nothing_is_there()
        {
            foreach (DataCheckOutput output in new[]
            {
                DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, true, false, false, true }))),
                DataCheckLogic.Run(Settings(), new DataCheckInput()),
                DataCheckLogic.Stopped(Settings(), new DataCheckInput(), "The check stopped."),
                DataCheckLogic.Stopped(null, new DataCheckInput(), "The project settings hold no values for this check."),
            })
            {
                JArray needs = (JArray)output.Check["needs"];
                Assert.Equal(DataCheckLogic.Needs.OrderBy(name => name), needs.Select(row => (string)row["need"]).OrderBy(name => name));
                foreach (JObject row in needs.OfType<JObject>())
                {
                    string line = (string)row["line"];
                    Assert.Contains((string)row["mark"], new[] { "strong", "usable", "weak", "missing" });
                    Assert.True(line.Length > 0 && line.Length <= 160 && !line.Contains("\n"), line);
                    Assert.False(Regex.IsMatch(line, @"[{}\[\]]|\bjson\b|\bnull\b|\bconfig\b|\bR\d{3}\b|[a-z][A-Z]"), line);
                }
            }
            DataCheckOutput empty = DataCheckLogic.Run(Settings(), new DataCheckInput());
            Assert.All(((JArray)empty.Check["needs"]).OfType<JObject>(), row => Assert.Equal("missing", (string)row["mark"]));
            Assert.Empty((JArray)empty.Plots["plots"]);
            Assert.Equal(2, ((JArray)empty.Profile["levels"]).Count);
        }

        [Fact]
        public void R605_a_label_outside_every_outline_lowers_the_mark_and_is_counted_in_the_line()
        {
            JObject drawing = Drawing(new[] { false, false, false, false, false });
            var texts = (JArray)drawing["texts"];
            for (int extra = 0; extra < 3; extra++)
            {
                texts.Add(new JObject { ["layer"] = "N", ["text"] = (900 + extra).ToString(), ["x"] = 1000.0 + extra, ["y"] = 1000.0 });
            }
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(drawing));
            JObject outlines = NeedOf(output, "plotOutlines");
            Assert.Equal("usable", (string)outlines["mark"]);
            Assert.Contains("3 numbers outside every outline", (string)outlines["line"]);
        }

        [Fact]
        public void R600_a_surface_file_gives_road_levels_and_its_coordinate_system_gives_coordinates()
        {
            DataCheckInput input = Input(Drawing(new[] { false, false, false, false, false }));
            input.LandXmlFiles = 2;
            input.LandXmlWithCoordinateSystem = 1;
            input.RuleFiles = 1;
            input.SharedParameterFiles = 1;
            DataCheckOutput output = DataCheckLogic.Run(Settings(), input);
            Assert.Equal("usable", (string)NeedOf(output, "roadLevels")["mark"]);
            Assert.Equal("usable", (string)NeedOf(output, "coordinates")["mark"]);
            Assert.Equal("usable", (string)NeedOf(output, "rules")["mark"]);
            Assert.Equal("usable", (string)NeedOf(output, "sharedParameters")["mark"]);
            Assert.Equal("weak", (string)NeedOf(DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, false, false, false, false }))), "coordinates")["mark"]);
        }

        [Fact]
        public void R606_a_drawing_that_could_not_be_read_is_named_in_the_files_with_its_reason()
        {
            DataCheckInput input = Input(Drawing(new[] { false, false, false, false, false }));
            input.Notes.Add(new DataCheckNote { Name = "broken.dwg", Kind = "drawing", Read = false, Reason = "InvalidDataException: not a drawing" });
            DataCheckOutput output = DataCheckLogic.Run(Settings(), input);
            JObject file = ((JArray)output.Check["files"]).OfType<JObject>().Single(row => (string)row["name"] == "broken.dwg");
            Assert.Equal("not read", (string)file["status"]);
            Assert.Contains("not a drawing", (string)file["reason"]);
            Assert.Contains("1 drawing could not be read", (string)NeedOf(output, "plotOutlines")["line"]);
            Assert.Equal(5, ((JArray)output.Plots["plots"]).Count);
        }

        [Fact]
        public void R605_an_outline_with_no_number_is_said_and_the_mark_is_not_strong()
        {
            JObject drawing = Drawing(new[] { false, false, false, false, false });
            var faces = (JArray)drawing["candidates"][0]["faces"];
            faces.Add(Face(600, 0, 12, 20));
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(drawing));
            JObject outlines = NeedOf(output, "plotOutlines");
            Assert.Contains("6 outlines found", (string)outlines["line"]);
            Assert.Contains("1 with none or more than one", (string)outlines["line"]);
            Assert.Equal("usable", (string)outlines["mark"]);
        }

        [Fact]
        public void R605_a_label_of_two_numbers_is_flagged_and_a_number_used_twice_makes_the_need_weak_with_no_share_over_100()
        {
            JObject drawing = Drawing(new[] { false, false, false, false, false });
            ((JArray)drawing["texts"])[1]["text"] = "12 13";
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(drawing));
            JObject plot = PlotOf(output, "13");
            Assert.Contains("more than one number", (string)plot["weak"]["number"]);
            JObject numbers = NeedOf(output, "plotNumbers");
            Assert.Equal("usable", (string)numbers["mark"]);
            Assert.Contains("1 label holds more than one number", (string)numbers["line"]);
            // The same number on two outlines: both are left out and the need says so in counts
            JObject twice = Drawing(new[] { false, false, false, false, false });
            ((JArray)twice["texts"])[4]["text"] = "101";
            DataCheckOutput repeated = DataCheckLogic.Run(Settings(), Input(twice));
            JObject line = NeedOf(repeated, "plotNumbers");
            Assert.Equal("weak", (string)line["mark"]);
            Assert.Contains("5 outlines hold one and 3 numbers are distinct", (string)line["line"]);
            Assert.Contains("1 number appears more than once", (string)line["line"]);
            Assert.DoesNotContain(" of ", (string)line["line"]);
        }

        [Fact]
        public void R602_a_plot_with_a_hole_is_flagged_and_what_lies_in_the_hole_is_left_out_of_its_mirror()
        {
            JObject drawing = Drawing(new[] { false, true, false, false, false });
            // A courtyard inside plot 101 with no number, drawn on the plot layer, and a block of the other hand in it
            ((JArray)drawing["candidates"][0]["faces"]).Add(Face(4, 4, 3, 3));
            ((JArray)drawing["inserts"]).Add(new JObject { ["layer"] = "H", ["block"] = "VB2", ["x"] = 5.5, ["y"] = 5.5, ["mirrored"] = true });
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(drawing));
            JObject outer = PlotOf(output, "101");
            Assert.Equal(DataCheckLogic.HoleWhy, (string)outer["weak"]["outline"]);
            // The block in the hole does not make the plot hold both hands, its own block is not mirrored
            Assert.False((bool)outer["mirror"]);
            Assert.True((bool)PlotOf(output, "102")["mirror"]);
            Assert.Null((string)PlotOf(output, "102")["weak"]["outline"]);
            // The reason to leave it out is only that it lies in the hole, without the hole the same block makes both hands
            JObject whole = Drawing(new[] { false, true, false, false, false });
            ((JArray)whole["inserts"]).Add(new JObject { ["layer"] = "H", ["block"] = "VB2", ["x"] = 5.5, ["y"] = 5.5, ["mirrored"] = true });
            Assert.Null((bool?)PlotOf(DataCheckLogic.Run(Settings(), Input(whole)), "101")["mirror"]);
        }

        [Fact]
        public void R601_a_drawing_with_no_unit_header_is_assumed_metres_and_the_groups_say_so()
        {
            DataCheckInput input = Input(Drawing(new[] { false, false, false, false, false }));
            input.Excels.Add(new ScanWorkbook
            {
                Name = "p.xlsx",
                Sheets = new List<ScanSheet>
                {
                    ScanSheet.Of("S", new List<IList<string>>
                    {
                        new List<string> { "Plot", "Sector", "Cluster" },
                        new List<string> { "101", "N", "N1" }, new List<string> { "102", "N", "N1" }, new List<string> { "103", "N", "N2" }, new List<string> { "104", "N", "N2" }, new List<string> { "105", "S", "S1" },
                    }),
                },
            });
            DataCheckOutput stated = DataCheckLogic.Run(Settings(), input);
            Assert.Equal("strong", (string)NeedOf(stated, "groupLevels")["mark"]);
            input.CadReads[0]["units"] = "Unitless";
            DataCheckOutput assumed = DataCheckLogic.Run(Settings(), input);
            JObject need = NeedOf(assumed, "groupLevels");
            Assert.Equal("weak", (string)need["mark"]);
            Assert.Contains("states no unit and metres are assumed", (string)need["line"]);
            Assert.Contains("metres are assumed", (string)PlotOf(assumed, "101")["weak"]["groups"]);
            // A unit the table lacks gives no group at all, never one group for every plot
            input.CadReads[0]["units"] = "Parsecs";
            DataCheckOutput none = DataCheckLogic.Run(Settings(), input);
            JObject plot = PlotOf(none, "101");
            Assert.Empty((JArray)plot["groups"]);
            Assert.Equal("missing", (string)plot["source"]["groups"]);
            Assert.Contains("Parsecs", (string)plot["missing"]["groups"]);
            Assert.Equal("weak", (string)NeedOf(none, "groupLevels")["mark"]);
        }

        [Fact]
        public void R602_a_sheet_that_joins_few_plots_makes_the_villa_type_weak_so_a_worse_join_never_reads_better()
        {
            Func<string, DataCheckInput> with = style =>
            {
                DataCheckInput input = Input(Drawing(new[] { false, false, false, false, false }));
                var rows = new List<IList<string>> { new List<string> { "Plot", "Type" } };
                for (int index = 0; index < 5; index++)
                {
                    rows.Add(new List<string> { string.Format(style, 101 + index), new[] { "VA1", "VA1", "VB2", "VB2", "VA1" }[index] });
                }
                input.Excels.Add(new ScanWorkbook { Name = "p.xlsx", Sheets = new List<ScanSheet> { ScanSheet.Of("S", rows) } });
                return input;
            };
            // The sheet numbers match the drawing, the sheet joins all 5 plots
            Assert.Equal("strong", (string)NeedOf(DataCheckLogic.Run(Settings(), with("{0}")), "villaType")["mark"]);
            JObject joined = NeedOf(DataCheckLogic.Run(Settings(), with("{0}")), "villaType");
            Assert.DoesNotContain("joins only", (string)joined["line"]);
            // The sheet numbers are in another style, it holds 5 codes and joins 0 plots, and the need cannot read better than the first
            JObject apart = NeedOf(DataCheckLogic.Run(Settings(), with("LOT-{0}")), "villaType");
            Assert.Equal("weak", (string)apart["mark"]);
            Assert.Contains("a sheet holds 5 villa type codes and joins only 0 plots", (string)apart["line"]);
        }

        [Fact]
        public void R602_the_mirror_is_weak_on_every_plot_that_has_one_so_the_weakness_reaches_plots_json()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, true, false, false, true })));
            JObject plot = PlotOf(output, "102");
            Assert.Equal("computed", (string)plot["source"]["mirror"]);
            Assert.Contains("which hand a prototype file has is not known", (string)plot["weak"]["mirror"]);
        }

        [Fact]
        public void R601_two_candidate_outlines_that_give_the_same_plots_are_a_named_tie_broken_to_the_fewest_layers()
        {
            JObject drawing = Drawing(new[] { false, false, false, false, false });
            var candidates = (JArray)drawing["candidates"];
            // The plot layer with a layer of block lines added gives the same 5 plots and 5 faces without a label
            JArray extra = new JArray(((JArray)candidates[0]["faces"]).Select(face => (JToken)face.DeepClone()));
            extra.Add(Face(10, 10, 2, 2));
            candidates.Add(new JObject { ["layers"] = new JArray("0", "P"), ["faces"] = extra });
            DataCheckInput input = Input(drawing);
            DataCheckOutput output = DataCheckLogic.Run(Settings(), input);
            Assert.Equal(5, ((JArray)output.Plots["plots"]).Count);
            Assert.Contains("A tie of candidates was broken", (string)NeedOf(output, "plotOutlines")["line"]);
            Assert.Contains(((JArray)output.Check["ties"]).Select(item => (string)item), text => text.Contains("the one with the fewest layers was taken"));
            // Break it: a tie between two candidates of one layer each has no fewest, so the proposal is not taken and the tie is named
            JObject apart = Drawing(new[] { false, false, false, false, false });
            ((JArray)apart["candidates"]).Add(new JObject { ["layers"] = new JArray("Q"), ["faces"] = ((JArray)apart["candidates"][0]["faces"]).DeepClone() });
            DataCheckOutput lost = DataCheckLogic.Run(Settings(), Input(apart));
            JObject line = NeedOf(lost, "plotOutlines");
            Assert.Equal("missing", (string)line["mark"]);
            Assert.Contains("tie and none is taken", (string)line["line"]);
            Assert.Contains(((JArray)lost.Check["ties"]).Select(item => (string)item), text => text.Contains("P") && text.Contains("Q"));
        }

        [Fact]
        public void R606_the_settings_refuse_a_block_without_a_minimum_of_groups_a_road_share_or_a_drawing_peak()
        {
            foreach (string key in new[] { "wordMinGroups", "roadOutsideShare", "drawingPeak" })
            {
                JObject config = Config();
                ((JObject)config[DataCheckSettings.Block]).Remove(key);
                Assert.Throws<InvalidDataException>(() => DataCheckSettings.From(config));
            }
            Assert.Equal(252.0 * 10, Settings().DrawingPeakMiB(".DWG", 10));
            Assert.Equal(106.0 * 10, Settings().DrawingPeakMiB(".dxf", 10));
            Assert.Null(Settings().DrawingPeakMiB(".svg", 10));
            JObject noTable = Config();
            noTable[DataCheckSettings.Block]["drawingPeak"] = new JObject { ["why"] = "none" };
            Assert.Throws<InvalidDataException>(() => DataCheckSettings.From(noTable));
            Assert.True(DataCheckSettings.UnitAssumed("Unitless"));
            Assert.True(DataCheckSettings.UnitAssumed(""));
            Assert.False(DataCheckSettings.UnitAssumed("Meters"));
        }

        [Fact]
        public void R607_the_scores_table_of_the_scenarios_holds_counts_and_marks_for_all_five()
        {
            string text = File.ReadAllText(Path.Combine(RepoRoot(), "steps", "scenarios.md"));
            foreach (string scenario in new[] { "S1", "S2", "S3", "S4", "S5" })
            {
                Assert.Matches(@"(?m)^\| *" + scenario + @" *\|.*\d+ of \d+.*\|\s*$", text);
            }
        }

        [Fact]
        public void R608_the_sample_project_names_a_python_build_that_is_in_the_repo()
        {
            string sample = Path.Combine(RepoRoot(), "samples", "new-project");
            JObject build = JObject.Parse(File.ReadAllText(Path.Combine(sample, "build.json")));
            string script = (string)build["command"][0];
            Assert.EndsWith(".py", script);
            Assert.True(File.Exists(Path.Combine(RepoRoot(), script.Replace('/', Path.DirectorySeparatorChar))), script);
            Assert.True(File.Exists(Path.Combine(sample, "site.dxf")));
        }

        private static string RepoRoot()
        {
            string at = AppContext.BaseDirectory;
            while (at != null && !File.Exists(Path.Combine(at, "CLAUDE.md")))
            {
                at = Path.GetDirectoryName(at);
            }
            return at ?? throw new InvalidOperationException("the repo root with CLAUDE.md was not found above " + AppContext.BaseDirectory);
        }

        // The runner

        private WorkerRequest Request(string command, string role = "scanFolder", string sha = "")
        {
            string config = Path.Combine(folder, "project.json");
            File.WriteAllText(config, new JObject { ["siteBuild"] = new JObject { ["minFreeGB"] = 0.1 }, ["addin"] = new JObject
            {
                ["workerTimeLimitMinS"] = 0.5,
                ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "a.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) },
            } }.ToString());
            string scan = Path.Combine(folder, "handed over");
            Directory.CreateDirectory(scan);
            File.WriteAllText(Path.Combine(scan, "note.txt"), "one");
            return new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = "r1", Document = new WorkerDocument { Path = @"C:\Projects\site\a.rvt", SessionId = "s1" }, Command = command,
                Inputs = new List<WorkerFile> { new WorkerFile { Role = "config", Path = config, Sha256 = "" }, new WorkerFile { Role = role, Path = role == "scanFolder" ? scan : config, Sha256 = sha } },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, TimeLimitS = 600,
            };
        }

        [Fact]
        public void R600_the_command_dataCheck_takes_a_folder_and_an_empty_hash_for_its_config_and_folder_only()
        {
            WorkerRequest request = Request("dataCheck");
            string seen = null;
            var commands = new Dictionary<string, WorkerCommand> { ["dataCheck"] = run => seen = run.Input("scanFolder") };
            int code = WorkerRunner.Run(request.Write(folder), commands, () => 9000.0);
            Assert.True(code == 0, WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Message);
            Assert.Equal(request.Inputs[1].Path, seen);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1"));
            Assert.Equal("done", answer.Status);
            Assert.Equal(new[] { "config" }, answer.Opened.Select(item => item.Role).ToArray());

            // Another command keeps refusing an empty hash, for the config and for any role
            foreach (string command in new[] { "build", "scan" })
            {
                WorkerRequest other = Request(command, "plotsCsv");
                bool ran = false;
                var guarded = new Dictionary<string, WorkerCommand> { [command] = run => ran = true };
                Assert.Equal(4, WorkerRunner.Run(other.Write(folder), guarded, () => 9000.0));
                Assert.False(ran, command);
            }

            // dataCheck with a role other than config and scanFolder is refused as well
            WorkerRequest odd = Request("dataCheck", "plotsCsv");
            Assert.Equal(4, WorkerRunner.Run(odd.Write(folder), commands, () => 9000.0));

            // A folder that is not there is a refusal that names it
            WorkerRequest gone = Request("dataCheck");
            gone.Inputs[1].Path = Path.Combine(folder, "nothing here");
            Assert.Equal(2, WorkerRunner.Run(gone.Write(folder), commands, () => 9000.0));
            Assert.Contains("is not on disk", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Message);
        }

        [Fact]
        public void R600_a_file_the_command_opens_is_marked_before_and_after_and_a_change_ends_the_run_with_4()
        {
            WorkerRequest request = Request("dataCheck");
            string file = Path.Combine(request.Inputs[1].Path, "note.txt");
            var steady = new Dictionary<string, WorkerCommand> { ["dataCheck"] = run => { InputMark mark; Assert.Null(run.OpenFile("drawing", file, out mark)); File.ReadAllText(file); } };
            Assert.Equal(0, WorkerRunner.Run(request.Write(folder), steady, () => 9000.0));
            WorkerOpened opened = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Opened.Single(item => item.Role == "drawing");
            Assert.Equal(file, opened.Path);
            Assert.Equal(opened.Sha256Before, opened.Sha256After);
            Assert.True(opened.SizeBefore > 0 && opened.WriteTimeBefore != null);

            var changing = new Dictionary<string, WorkerCommand> { ["dataCheck"] = run => { InputMark mark; run.OpenFile("drawing", file, out mark); File.AppendAllText(file, "two"); } };
            Assert.Equal(4, WorkerRunner.Run(request.Write(folder), changing, () => 9000.0));
            Assert.Contains("drawing changed while it was read", WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Message);
        }

        private WorkerRequest FolderRequest(string scan, double peakMibPerMib)
        {
            JObject full = Config();
            full[DataCheckSettings.Block]["drawingPeak"] = new JObject { ["dwg"] = peakMibPerMib, ["dxf"] = peakMibPerMib };
            full["siteBuild"] = new JObject { ["minFreeGB"] = 0.1 };
            full["addin"] = new JObject
            {
                ["workerTimeLimitMinS"] = 0.5,
                ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "a.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) },
            };
            string config = Path.Combine(folder, "full.json");
            File.WriteAllText(config, full.ToString());
            return new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = "r1", Document = new WorkerDocument { Path = @"C:\Projects\site\a.rvt", SessionId = "s1" }, Command = "dataCheck",
                Inputs = new List<WorkerFile> { new WorkerFile { Role = "config", Path = config, Sha256 = "" }, new WorkerFile { Role = "scanFolder", Path = scan, Sha256 = "" } },
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = folder, TimeLimitS = 600,
            };
        }

        private JObject FileOf(string name)
        {
            JObject check = JObject.Parse(File.ReadAllText(Path.Combine(folder, "data_check.json")));
            return ((JArray)check["files"]).OfType<JObject>().Single(row => (string)row["name"] == name);
        }

        [Fact]
        public void R606_a_file_another_program_holds_open_is_named_as_not_read_and_the_run_carries_on()
        {
            string scan = Path.Combine(folder, "handed over");
            Directory.CreateDirectory(scan);
            string locked = Path.Combine(scan, "locked.dxf");
            File.WriteAllText(locked, "0");
            var commands = new Dictionary<string, WorkerCommand> { ["dataCheck"] = CadReader.DataCheckCommand.Run };
            using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                int code = WorkerRunner.Run(FolderRequest(scan, 252).Write(folder), commands, () => 9000.0);
                Assert.True(code == 0, WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Message);
            }
            JObject row = FileOf("locked.dxf");
            Assert.Equal("not read", (string)row["status"]);
            Assert.False(string.IsNullOrEmpty((string)row["reason"]));
        }

        [Fact]
        public void R606_a_drawing_is_read_only_when_the_free_memory_holds_its_own_peak_from_the_block_and_the_floor()
        {
            string scan = Path.Combine(folder, "handed over");
            Directory.CreateDirectory(scan);
            File.WriteAllText(Path.Combine(scan, "big.dxf"), "0\nEOF\n");
            var commands = new Dictionary<string, WorkerCommand> { ["dataCheck"] = CadReader.DataCheckCommand.Run };
            // A tiny file times a huge peak per MiB is more than the free memory, so it is named as not read and says why
            int code = WorkerRunner.Run(FolderRequest(scan, 5e9).Write(folder), commands, () => 9000.0);
            Assert.True(code == 0, WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Message);
            Assert.Contains("free memory", (string)FileOf("big.dxf")["reason"]);
            // With a peak that fits the file is opened, and a drawing that is no drawing is named with its own error instead
            code = WorkerRunner.Run(FolderRequest(scan, 1).Write(folder), commands, () => 9000.0);
            Assert.True(code == 0, WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "r1")).Message);
            Assert.DoesNotContain("free memory", (string)FileOf("big.dxf")["reason"]);
        }
    }
}
