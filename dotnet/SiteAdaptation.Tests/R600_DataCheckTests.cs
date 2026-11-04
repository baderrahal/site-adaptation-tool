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
                    ["groupGapsM"] = new JArray(100.0, 1.0), ["wordReachM"] = 30, ["wordShare"] = 0.9, ["roadReachM"] = 20, ["roadMinSegments"] = 2,
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
            Assert.Contains("another kind of source gives another type on 3 of them", (string)need["line"]);
            // The plot keeps the code of the first kind and says what disagrees
            Assert.Equal("VA1", (string)PlotOf(output, "101")["villaType"]);
            Assert.Contains("VB2", (string)PlotOf(output, "101")["disputed"]["villaType"]);
            Assert.Empty(((JObject)PlotOf(output, "103")["disputed"]).Properties());
        }

        [Fact]
        public void R603_the_word_of_a_level_comes_from_texts_that_name_each_group_of_it_with_a_word_of_its_own()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(Drawing(new[] { false, false, false, false, false })));
            JArray levels = (JArray)output.Profile["levels"];
            Assert.Equal("Phase", (string)levels[0]["word"]);
            Assert.Equal("computed", (string)levels[0]["source"]);
            // Level 2 has 3 groups and only 2 carry a text, under the share of 0.9
            Assert.Null((string)levels[1]["word"]);
            Assert.Equal("missing", (string)levels[1]["source"]);
            Assert.Equal("usable", (string)NeedOf(output, "groupLevels")["mark"]);
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
            Assert.Equal("strong", (string)road["mark"]);
            Assert.Contains("layer K", (string)road["line"]);
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
            Assert.Contains("3 numbers are outside", (string)outlines["line"]);
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
    }
}
