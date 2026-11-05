using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R630 to R634. The code table, the typology column, what a person confirmed, the hand and the group words on made up reads of the shape the
    // real inputs have: a label is an MTEXT of a type code line then a number line, a block name holds the code with a word beside it, a table
    // sits under a title row with its old code twice and qualifiers on its codes, and the prototype files carry the new codes.
    public sealed class R630_StageOneGapsTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa_r635_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static DataCheckSettings Settings()
        {
            return DataCheckSettings.From(new JObject
            {
                [DataCheckSettings.Block] = new JObject
                {
                    ["scan"] = new JObject
                    {
                        ["labelPattern"] = @"^(?:(.*?)\s+)?(\S*\d\S*)$", ["tokenSplit"] = "[^A-Za-z0-9]+", ["plotNumberShifts"] = new JArray(0), ["pairMinSegments"] = 100,
                        ["pairThreads"] = 1, ["pairTimeCapS"] = 60, ["pdfJoinGapPt"] = 0.3, ["pdfLineTolerancePt"] = 0.05, ["pdfPairDyPt"] = new JArray(1.3, 1.8), ["pdfPairDxPt"] = 1.5,
                    },
                    ["groupGapsM"] = new JArray(100.0, 1.0), ["wordReachM"] = 30, ["wordShare"] = 0.9, ["wordMinGroups"] = 3, ["standInMaxArea"] = 4, ["roadReachM"] = 20, ["roadMinSegments"] = 2, ["roadOutsideShare"] = 0.5,
                    ["drawingPeak"] = new JObject { ["dwg"] = 252, ["dxf"] = 106 },
                    ["strongShare"] = 0.9, ["usableShare"] = 0.5, ["prototypeFilePattern"] = "(?:^|-)ARC-([A-Za-z0-9]+)(?:-[A-Za-z0-9]{2})?-MOD",
                    ["codePattern"] = @"^[A-Za-z]+\d+[A-Za-z0-9.]*(?:-[A-Za-z0-9]+)*(?:\s*\([^()]*\))?$", ["codeTableMaxPairs"] = 100, ["handGuessShare"] = 0.6, ["groupNamePattern"] = @"^(?:[A-Z0-9][A-Za-z0-9-]{0,3}|\S*\d\S*)$",
                },
            });
        }

        private static JArray Pt(double x, double y)
        {
            return new JArray(x, y);
        }

        private static JObject Face(double x, double y)
        {
            return new JObject { ["ring"] = new JArray(Pt(x, y), Pt(x + 12, y), Pt(x + 12, y + 20), Pt(x, y + 20)), ["centroid"] = Pt(x + 6, y + 10), ["area"] = 240.0 };
        }

        // Plots in rows of five with a road between the rows. Each plot is (number, label type code, block name, mirrored)
        private static JObject Drawing(IList<(string Number, string Code, string Block, bool Mirrored)> plots, IEnumerable<JObject> more = null)
        {
            var faces = new JArray();
            var texts = new JArray();
            var inserts = new JArray();
            for (int at = 0; at < plots.Count; at++)
            {
                double x = (at % 5) * 12.0, y = (at / 5) * 35.0;
                faces.Add(Face(x, y));
                texts.Add(new JObject { ["layer"] = "LOTS-TEXT", ["text"] = plots[at].Code + "\n" + plots[at].Number, ["x"] = x + 3, ["y"] = y + 2 });
                if (plots[at].Block != null)
                {
                    inserts.Add(new JObject { ["layer"] = "HOUSES", ["block"] = plots[at].Block, ["x"] = x + 6, ["y"] = y + 10, ["mirrored"] = plots[at].Mirrored });
                }
            }
            foreach (JObject text in more ?? Enumerable.Empty<JObject>())
            {
                texts.Add(text);
            }
            return new JObject
            {
                ["source"] = "site.dwg", ["path"] = "site.dwg", ["size"] = 1, ["sha256"] = "x", ["units"] = "Meters", ["notRead"] = new JArray(), ["notTraced"] = new JArray(),
                ["layers"] = new JArray(new JObject { ["layer"] = "LOTS", ["closedOutlines"] = plots.Count, ["texts"] = 0, ["blocks"] = 0, ["segments"] = plots.Count * 4 }),
                ["candidates"] = new JArray(new JObject { ["layers"] = new JArray("LOTS"), ["faces"] = faces }),
                ["texts"] = texts, ["inserts"] = inserts, ["lines"] = new JObject(), ["arcs"] = new JObject(),
            };
        }

        private static ScanWorkbook Book(string name, params ScanSheet[] sheets)
        {
            return new ScanWorkbook { Name = name, Sheets = sheets.ToList() };
        }

        private static ScanSheet Sheet(string name, params string[][] rows)
        {
            return ScanSheet.Of(name, rows.Select(row => (IList<string>)row.ToList()));
        }

        // Ten plots in two rows. The drawing still speaks an older layout, its codes OLD1 and OLD2, where the prototype files are NEW1 and NEW2
        private static List<(string, string, string, bool)> TenPlots()
        {
            var plots = new List<(string, string, string, bool)>();
            for (int at = 0; at < 10; at++)
            {
                string code = at % 2 == 0 ? "OLD1" : "OLD2";
                plots.Add(((101 + at).ToString(), code, "House_" + code, at % 3 == 0));
            }
            return plots;
        }

        private static DataCheckInput Input(ConfirmedProfile confirmed = null, params ScanWorkbook[] books)
        {
            var input = new DataCheckInput { Confirmed = confirmed };
            input.CadReads.Add(Drawing(TenPlots()));
            input.PrototypeNames.AddRange(new[] { "X-ARC-NEW1-ZZ-MOD-1.rvt", "X-ARC-NEW2-ZZ-MOD-1.rvt" });
            input.Excels.AddRange(books);
            return input;
        }

        private static ScanWorkbook RealShapeTable(string sheet = "A sheet of any name")
        {
            // A title row, then a header row, then a word column beside the two code columns, and rows with no new code
            return Book("book.xlsx", Sheet(sheet,
                new[] { "Sizes (m)", "", "", "" },
                new[] { "Form", "Former", "Current", "Area" },
                new[] { "Villa", "OLD1", "NEW1", "400" },
                new[] { "Villa", "OLD2-0 (ZQ)", "NEW2", "250" },
                new[] { "Duplex", "OLD2", "NEW3", "250" },
                new[] { "", "OLD9", "", "250" },
                new[] { "", "NEW1", "NEW1", "400" }));
        }

        private static Dictionary<string, JObject> Plots(DataCheckOutput output)
        {
            return ((JArray)output.Plots["plots"]).OfType<JObject>().ToDictionary(row => (string)row["number"]);
        }

        private static ScanWorkbook SalesBook(string header = "Rev")
        {
            var rows = new List<string[]> { new[] { "Plot", "Note", header } };
            for (int at = 0; at < 10; at++)
            {
                rows.Add(new[] { (101 + at).ToString(), "text", at % 2 == 0 ? "NEW2" : "NEW1" });
            }
            return Book("sales.xlsx", Sheet("Any", rows.ToArray()));
        }

        [Fact]
        public void R630_a_table_under_a_title_row_with_qualifiers_is_found_by_what_it_holds()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(null, RealShapeTable()));
            var proposed = ((JArray)output.Profile["codeMap"]["proposed"]).Select(row => (string)row["old"] + ">" + (string)row["new"]).ToList();
            Assert.Equal(new[] { "OLD1>NEW1", "OLD2-0 (ZQ)>NEW2", "OLD2>NEW3", "NEW1>NEW1" }, proposed);
            Assert.Equal("computed", (string)output.Profile["codeMap"]["source"]);
            Assert.All((JArray)output.Profile["codeMap"]["proposed"], row => Assert.Equal("computed", (string)row["source"]));
        }

        [Fact]
        public void R630_the_side_the_drawing_speaks_is_the_old_one_whichever_side_it_is_on()
        {
            ScanWorkbook turned = Book("book.xlsx", Sheet("S", new[] { "A", "B" }, new[] { "NEW1", "OLD1" }, new[] { "NEW2", "OLD2" }));
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(null, turned));
            Assert.Equal(new[] { "OLD1>NEW1", "OLD2>NEW2" }, ((JArray)output.Profile["codeMap"]["proposed"]).Select(row => (string)row["old"] + ">" + (string)row["new"]));
        }

        [Fact]
        public void R630_columns_that_are_no_code_table_give_none()
        {
            // A plot list, repeated pairs, no rename at all, no relation to a prototype and a row with one pair each say no
            var notTables = new[]
            {
                Book("a.xlsx", Sheet("Plots", new[] { "Plot", "A", "B" }, new[] { "101", "OLD1", "NEW1" }, new[] { "102", "OLD2", "NEW2" }, new[] { "103", "OLD1", "NEW1" }, new[] { "104", "OLD2", "NEW2" }, new[] { "105", "OLD1", "NEW1" }, new[] { "106", "OLD1", "NEW1" })),
                Book("b.xlsx", Sheet("S", new[] { "A", "B" }, new[] { "OLD1", "NEW1" }, new[] { "OLD1", "NEW1" }, new[] { "OLD1", "NEW1" }, new[] { "OLD2", "NEW2" }, new[] { "OLD2", "NEW2" })),
                Book("c.xlsx", Sheet("S", new[] { "A", "B" }, new[] { "NEW1", "NEW1" }, new[] { "NEW2", "NEW2" })),
                Book("d.xlsx", Sheet("S", new[] { "A", "B" }, new[] { "Q1", "Q2" }, new[] { "Q3", "Q4" })),
                Book("e.xlsx", Sheet("S", new[] { "A", "B" }, new[] { "OLD1", "NEW1" })),
                Book("f.xlsx", Sheet("S", new[] { "Type", "Area" }, new[] { "Villa", "400" }, new[] { "Duplex", "250" })),
            };
            foreach (ScanWorkbook book in notTables)
            {
                DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(null, book));
                Assert.Empty((JArray)output.Profile["codeMap"]["proposed"]);
                Assert.Equal("missing", (string)output.Profile["codeMap"]["source"]);
            }
        }

        [Fact]
        public void R631_a_typology_column_that_says_more_than_the_drawing_is_proposed()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(null, SalesBook()));
            JToken proposed = output.Profile["typologyColumn"]["proposed"];
            Assert.Equal("Any", (string)proposed["sheet"]);
            Assert.Equal("Rev", (string)proposed["column"]);
            Assert.Equal("sales.xlsx", (string)proposed["workbook"]);
            Assert.Null((string)output.Profile["typologyColumn"]["confirmed"]);
            Assert.Equal("computed", (string)output.Profile["typologyColumn"]["source"]);
        }

        [Fact]
        public void R631_of_two_equal_columns_the_later_is_first_and_a_group_column_or_a_copy_of_the_drawing_is_none()
        {
            var rows = new List<string[]> { new[] { "Plot", "First", "Later", "Row", "Same" } };
            for (int at = 0; at < 10; at++)
            {
                string code = at % 2 == 0 ? "NEW2" : "NEW1";
                rows.Add(new[] { (101 + at).ToString(), code, code, at < 5 ? "R1" : "R2", at % 2 == 0 ? "OLD1" : "OLD2" });
            }
            var input = Input(null, Book("b.xlsx", Sheet("S", rows.ToArray())));
            input.PrototypeNames.AddRange(new[] { "X-ARC-OLD1-ZZ-MOD-1.rvt", "X-ARC-OLD2-ZZ-MOD-1.rvt" });
            DataCheckOutput output = DataCheckLogic.Run(Settings(), input);
            var columns = ((JArray)output.Profile["typologyColumn"]["candidates"]).Select(row => (string)row["column"]).ToList();
            Assert.Equal("Later", (string)output.Profile["typologyColumn"]["proposed"]["column"]);
            Assert.Equal(new[] { "Later", "First" }, columns);
        }

        [Fact]
        public void R632_the_confirmed_column_leads_and_a_plot_it_does_not_list_says_why_and_keeps_no_stale_code()
        {
            var rows = new List<string[]> { new[] { "Plot", "Rev" } };
            for (int at = 0; at < 8; at++)
            {
                rows.Add(new[] { (101 + at).ToString(), at == 7 ? "OS" : "NEW1" });
            }
            ConfirmedProfile confirmed = ConfirmedProfile.From(JObject.Parse("{\"typologyColumn\": {\"sheet\": \"Any\", \"column\": \"Rev\"}}"), 2);
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(confirmed, Book("s.xlsx", Sheet("Any", rows.ToArray()))));
            Dictionary<string, JObject> plots = Plots(output);
            Assert.Equal("NEW1", (string)plots["101"]["villaType"]);
            Assert.Equal("computed", (string)plots["101"]["source"]["villaType"]);
            // Listed with a word that is no code, and not listed at all: the drawing's code is not kept for either
            Assert.Null((string)plots["108"]["villaType"]);
            Assert.Contains("OS", (string)plots["108"]["missing"]["villaType"]);
            Assert.Null((string)plots["110"]["villaType"]);
            Assert.Contains("no code", (string)plots["110"]["missing"]["villaType"]);
            Assert.Equal("person entered", (string)output.Profile["typologyColumn"]["source"]);
            Assert.True((bool)output.Profile["typologyColumn"]["confirmed"]["found"]);
            Assert.Contains("confirmed column", ((JArray)output.Check["needs"]).OfType<JObject>().First(row => (string)row["need"] == "villaType")["line"].ToString());
        }

        [Fact]
        public void R632_a_confirmed_column_gone_from_the_folder_changes_no_plot_and_is_said()
        {
            ConfirmedProfile confirmed = ConfirmedProfile.From(JObject.Parse("{\"typologyColumn\": {\"sheet\": \"Gone\", \"column\": \"Rev\"}}"), 2);
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(confirmed, SalesBook()));
            Assert.False((bool)output.Profile["typologyColumn"]["confirmed"]["found"]);
            Assert.Contains(((JArray)output.Check["files"]).OfType<JObject>(), row => (string)row["kind"] == "confirmed column" && (string)row["status"] == "not read");
        }

        [Fact]
        public void R632_a_confirmed_old_code_the_prototypes_do_not_hold_is_read_from_the_mtext_and_turned()
        {
            // The prototype files hold only the new codes, so without the map the labels OLD1 and OLD2 give nothing
            DataCheckOutput none = DataCheckLogic.Run(Settings(), Input());
            Assert.Null((string)Plots(none)["101"]["villaType"]);
            ConfirmedProfile confirmed = ConfirmedProfile.From(JObject.Parse("{\"codeMap\": [{\"old\": \"OLD1\", \"new\": \"NEW1\"}, {\"old\": \"OLD2\", \"new\": \"NEW2\"}]}"), 2);
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(confirmed));
            Dictionary<string, JObject> plots = Plots(output);
            Assert.Equal("NEW1", (string)plots["101"]["villaType"]);
            Assert.Equal("OLD1", (string)plots["101"]["codeRead"]);
            Assert.Equal("NEW2", (string)plots["102"]["villaType"]);
            Assert.Equal("person entered", (string)output.Profile["codeMap"]["source"]);
            Assert.All((JArray)output.Profile["codeMap"]["confirmed"], row => Assert.Equal("person entered", (string)row["source"]));
        }

        [Fact]
        public void R632_a_code_already_new_stands_and_an_old_code_with_two_new_ones_is_no_pick()
        {
            // Bounded to what a real table holds: one old name kept, and split between two new ones
            ConfirmedProfile confirmed = ConfirmedProfile.From(JObject.Parse("{\"codeMap\": [{\"old\": \"OLD1\", \"new\": \"OLD1\"}, {\"old\": \"OLD1\", \"new\": \"NEW1\"}, {\"old\": \"OLD2\", \"new\": \"NEW1\"}, {\"old\": \"OLD2\", \"new\": \"NEW2\"}]}"), 2);
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input(confirmed));
            Dictionary<string, JObject> plots = Plots(output);
            Assert.Equal("OLD1", (string)plots["101"]["villaType"]);
            Assert.Null(plots["101"]["weak"]["villaType"]);
            Assert.Equal("OLD2", (string)plots["102"]["villaType"]);
            Assert.Contains("2 new codes", (string)plots["102"]["weak"]["villaType"]);
            Assert.Contains("two new codes", ((JArray)output.Check["needs"]).OfType<JObject>().First(row => (string)row["need"] == "villaType")["line"].ToString());
        }

        [Theory]
        [InlineData("{\"codeMap\": {\"old\": \"A\"}}")]
        [InlineData("{\"codeMap\": [{\"old\": \"A\"}]}")]
        [InlineData("{\"codeMap\": [{\"old\": \" \", \"new\": \"B1\"}]}")]
        [InlineData("{\"hands\": [\"left\"]}")]
        [InlineData("{\"hands\": {\"A1\": \"up\"}}")]
        [InlineData("{\"levels\": [{\"level\": 3, \"word\": \"A\"}]}")]
        [InlineData("{\"levels\": [{\"level\": 1}]}")]
        [InlineData("{\"typologyColumn\": {\"sheet\": \"A\"}}")]
        public void R632_a_confirmed_file_of_the_wrong_shape_is_refused_with_its_reason(string text)
        {
            Assert.Contains("(R632)", Assert.Throws<InvalidDataException>(() => ConfirmedProfile.From(JObject.Parse(text), 2)).Message);
        }

        [Fact]
        public void R633_the_guess_is_per_prototype_and_an_even_split_or_no_block_is_no_guess()
        {
            var input = new DataCheckInput();
            var plots = new List<(string, string, string, bool)>();
            for (int at = 0; at < 10; at++)
            {
                // A1 drawn mostly unmirrored, B2 mostly mirrored, C3 even, and D4 has no block
                string code = at < 3 ? "A1" : at < 6 ? "B2" : "C3";
                bool mirrored = code == "B2" ? at != 3 : code == "C3" ? at % 2 == 0 : at == 9;
                plots.Add(((101 + at).ToString(), code, "House_" + code, mirrored));
            }
            input.CadReads.Add(Drawing(plots));
            input.PrototypeNames.AddRange(new[] { "X-ARC-A1-ZZ-MOD-1.rvt", "X-ARC-B2-ZZ-MOD-1.rvt", "X-ARC-C3-ZZ-MOD-1.rvt", "X-ARC-D4-ZZ-MOD-1.rvt" });
            JToken hands = DataCheckLogic.Run(Settings(), input).Profile["hands"];
            Assert.Equal("right", (string)hands["proposed"]["A1"]);
            Assert.Equal("left", (string)hands["proposed"]["B2"]);
            Assert.Null((string)hands["proposed"]["C3"]);
            Assert.Null((string)hands["proposed"]["D4"]);
            Assert.Empty((JObject)hands["confirmed"]);
            Assert.Equal(2, (int)hands["evidence"]["B2"]["mirrored"]);
            Assert.Contains("guess", (string)hands["proposedAs"]);
        }

        [Fact]
        public void R633_a_block_of_another_type_keeps_the_mirror_weak_though_the_hand_is_confirmed()
        {
            ConfirmedProfile confirmed = ConfirmedProfile.From(JObject.Parse("{\"typologyColumn\": {\"sheet\": \"Any\", \"column\": \"Rev\"}, \"hands\": {\"NEW1\": \"left\", \"NEW2\": \"left\"}}"), 2);
            DataCheckInput input = Input(confirmed, SalesBook());
            input.PrototypeNames.Add("X-ARC-OLD1-ZZ-MOD-1.rvt");
            DataCheckOutput output = DataCheckLogic.Run(Settings(), input);
            JObject plot = Plots(output)["101"];
            Assert.Equal("NEW2", (string)plot["villaType"]);
            // Plot 101 holds a block drawn mirrored (at 0 of the made up list), a left hand turns it over
            Assert.False((bool)plot["mirror"]);
            Assert.Contains("not of the plot's own type", (string)plot["weak"]["mirror"]);
        }

        [Fact]
        public void R634_a_nearby_note_and_a_name_with_no_group_give_no_word()
        {
            var notes = new List<JObject>();
            foreach (double y in new[] { -5.0, 40.0 })
            {
                notes.Add(new JObject { ["layer"] = "NOTES", ["text"] = "General " + (y < 0 ? "note" : "remark"), ["x"] = 0.0, ["y"] = y });
            }
            var input = new DataCheckInput();
            input.CadReads.Add(Drawing(TenPlots(), notes));
            input.PrototypeNames.Add("X-ARC-OLD1-ZZ-MOD-1.rvt");
            DataCheckOutput output = DataCheckLogic.Run(Settings(), input);
            Assert.All((JArray)output.Profile["levels"], row =>
            {
                Assert.Null((string)row["word"]);
                Assert.Equal("missing", (string)row["source"]);
            });
            Assert.Contains("name each one once", ((JArray)output.Check["needs"]).OfType<JObject>().First(row => (string)row["need"] == "groupLevels")["line"].ToString());
        }

        [Fact]
        public void R634_a_column_of_one_value_over_a_lone_group_names_nothing()
        {
            // The whole level is one big group and a stray far plot, the column holds one value and no row for the stray
            var plots = new List<(string, string, string, bool)>();
            var rows = new List<string[]> { new[] { "Plot", "Rooms" } };
            for (int at = 0; at < 10; at++)
            {
                plots.Add(((101 + at).ToString(), "OLD1", null, false));
                rows.Add(new[] { (101 + at).ToString(), "4" });
            }
            JObject drawing = Drawing(plots);
            ((JArray)drawing["candidates"][0]["faces"]).Add(Face(5000, 0));
            ((JArray)drawing["texts"]).Add(new JObject { ["layer"] = "LOTS-TEXT", ["text"] = "OLD1\n999", ["x"] = 5003, ["y"] = 2 });
            var input = new DataCheckInput();
            input.CadReads.Add(drawing);
            input.PrototypeNames.Add("X-ARC-OLD1-ZZ-MOD-1.rvt");
            input.Excels.Add(Book("s.xlsx", Sheet("S", rows.ToArray())));
            DataCheckOutput output = DataCheckLogic.Run(Settings(), input);
            Assert.Null((string)output.Profile["levels"][0]["word"]);
            Assert.Null(output.Profile["levels"][0]["candidates"]);
        }

        [Fact]
        public void R634_a_person_names_a_level_over_what_was_found()
        {
            ConfirmedProfile confirmed = ConfirmedProfile.From(JObject.Parse("{\"levels\": [{\"level\": 2, \"word\": \"Court\"}]}"), 2);
            JToken levels = DataCheckLogic.Run(Settings(), Input(confirmed)).Profile["levels"];
            Assert.Equal("Court", (string)levels[1]["word"]);
            Assert.Equal("person entered", (string)levels[1]["source"]);
            Assert.Equal("missing", (string)levels[0]["source"]);
        }

        // The call of the add-in on a project with no past file: ButtonFlow, the body of ReadDataRun and the real worker command on the made up sample

        private static string RepoRoot()
        {
            string at = AppContext.BaseDirectory;
            while (at != null && !File.Exists(Path.Combine(at, "CLAUDE.md")))
            {
                at = Path.GetDirectoryName(at);
            }
            return at ?? throw new InvalidOperationException("the repo root was not found above " + AppContext.BaseDirectory);
        }

        private string Project(string mode)
        {
            string folder = Path.Combine(root, "handed over");
            Directory.CreateDirectory(folder);
            string sample = Path.Combine(RepoRoot(), "samples", "new-project");
            foreach (string file in Directory.GetFiles(sample, "*.dxf").Concat(Directory.GetFiles(sample, "*.xlsx")))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), true);
            }
            JObject config = JObject.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "config", "project.json")));
            config["inputRoot"] = folder;
            config["designBasis"]["mode"] = mode;
            config["siteBuild"]["minFreeGB"] = 0.1;
            string path = Path.Combine(root, "project.json");
            File.WriteAllText(path, config.ToString());
            return path;
        }

        private sealed class FolderState : IRunState
        {
            private readonly string configPath;
            private readonly string work;

            public FolderState(string configPath, string work)
            {
                this.configPath = configPath;
                this.work = work;
            }

            public bool DryRunFirst => false;

            public string WorkFolder(string documentPath)
            {
                return work;
            }

            public int MissingStep(int button)
            {
                return 0;
            }

            // The pane of a project with no past file gives the settings file alone, ReadInputs.NoPastFiles is what tells the add-in so
            public IReadOnlyList<RunInput> Inputs(int button)
            {
                return new[] { new RunInput("Settings", configPath, false) };
            }

            public void Committed(int button, RunRecord record)
            {
            }
        }

        private sealed class NoModel : IModel
        {
            public IModelTransaction Start(string name)
            {
                throw new InvalidOperationException("button 2 opens no transaction");
            }
        }

        private sealed class Yes : IConfirm
        {
            public RunSummary Asked { get; private set; }

            public bool Apply(string label, RunSummary summary, bool dryRun)
            {
                Asked = summary;
                return true;
            }
        }

        private sealed class DataCheckWorker : IWorkerStart
        {
            public readonly List<WorkerRequest> Requests = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                var commands = new Dictionary<string, WorkerCommand> { ["dataCheck"] = CadReader.DataCheckCommand.Run };
                WorkerRunner.Run(requestPath, commands, () => 1e6, () => 0.0);
                ended(WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId)));
            }
        }

        private static readonly Dictionary<string, string> PaneRoles = new Dictionary<string, string> { ["Settings"] = ReadInputs.Config };
        private const string Document = @"C:\Models\site.rvt";
        private int runs;

        // Button 2 as the add-in presses it: ButtonFlow on the body of ReadDataRun, with the worker command itself
        private RunOutcome Press(string configPath, string work, DataCheckWorker worker, string mode, Yes confirm = null)
        {
            var run = new ReadDataRun(PaneRoles, worker, new WorkerDocument { Path = Document, SessionId = "s1" }, () => new SettingsScope()) { ModePick = () => mode };
            var flow = new ButtonFlow(new FolderState(configPath, work), new NoModel(), confirm ?? new Yes(), InputHash.Of, () => RunIds.New(2, DateTime.Now) + "-" + (++runs).ToString("D3"));
            RunOutcome last = null;
            RunOutcome started = flow.Run(2, "2 Read data", run.Body(), Document, outcome => last = outcome);
            return started.End == RunEnd.Started ? last : started;
        }

        private string WorkFolderOf()
        {
            string work = Path.Combine(root, "work", "model");
            Directory.CreateDirectory(work);
            return work;
        }

        [Fact]
        public void R635_a_raw_folder_reads_through_the_call_of_the_add_in_and_review_data_follows()
        {
            string configPath = Project(DesignBasis.Rev6);
            string work = WorkFolderOf();
            Assert.True(ReadInputs.NoPastFiles(configPath, DesignBasis.Rev6, work));
            var worker = new DataCheckWorker();
            var confirm = new Yes();
            RunOutcome done = Press(configPath, work, worker, DesignBasis.Rev6, confirm);
            Assert.True(done.End == RunEnd.Committed, done.Message);
            Assert.True(done.Record.StepDone);
            // The preview lists the folder as an input of the read
            Assert.Contains(confirm.Asked.PerPlot, line => line.Contains("handed over"));
            WorkerRequest request = Assert.Single(worker.Requests);
            Assert.Equal("dataCheck", request.Command);
            Assert.Equal(new[] { "config", "scanFolder" }, request.Inputs.Select(item => item.Role));

            string folder = ReviewLoad.LatestReadFolder(work);
            Assert.NotNull(folder);
            JObject table = JObject.Parse(File.ReadAllText(Path.Combine(folder, ReadDataRun.PlotTableFile)));
            JObject plots = JObject.Parse(File.ReadAllText(Path.Combine(folder, "plots.json")));
            Assert.Equal(((JArray)plots["plots"]).Count, ((JArray)table["plots"]).Count);
            Assert.NotEmpty((JArray)table["plots"]);
            JObject first = ((JArray)plots["plots"]).OfType<JObject>().First();
            string nbh = (string)first["groups"][0];
            Assert.Contains(nbh, ReviewLoad.LatestNbhCodes(work));

            // 3 Review data follows, each row with the type, the mirror and the area of plots.json
            ReviewInput review = ReviewLoad.Load(work, nbh, null);
            ReviewRow row = review.Rows.Single(item => item.PlotNo == (string)first["number"]);
            Assert.Equal((string)first["villaType"], row.VillaType);
            Assert.Equal((bool?)first["mirror"], row.Mirror);
            Assert.Equal((double)first["areaM2"], row.AreaM2);
            Assert.True(row.AreaM2 > 0);

            // The same folder and settings again start no worker, the read is reused
            RunOutcome again = Press(configPath, work, worker, DesignBasis.Rev6);
            Assert.True(again.End == RunEnd.Committed, again.Message);
            Assert.Single(worker.Requests);
        }

        [Fact]
        public void R635_a_confirmed_file_in_the_work_folder_is_an_input_and_a_new_read()
        {
            string configPath = Project(DesignBasis.Rev6);
            string work = WorkFolderOf();
            var worker = new DataCheckWorker();
            Assert.Equal(RunEnd.Committed, Press(configPath, work, worker, DesignBasis.Rev6).End);
            File.WriteAllText(Path.Combine(work, ConfirmedProfile.FileName), "{\"levels\": [{\"level\": 1, \"word\": \"Court\"}]}");
            Assert.Equal(RunEnd.Committed, Press(configPath, work, worker, DesignBasis.Rev6).End);
            Assert.Equal(2, worker.Requests.Count);
            Assert.Contains(worker.Requests[1].Inputs, item => item.Role == ConfirmedProfile.Role);
            Assert.NotEqual(worker.Requests[0].OutputFolder, worker.Requests[1].OutputFolder);
            JObject profile = JObject.Parse(File.ReadAllText(Path.Combine(worker.Requests[1].OutputFolder, "project_profile.json")));
            Assert.Equal("person entered", (string)profile["levels"][0]["source"]);
        }

        [Fact]
        public void R635_a_changed_file_of_the_folder_is_a_new_read()
        {
            string configPath = Project(DesignBasis.Rev6);
            string work = WorkFolderOf();
            var worker = new DataCheckWorker();
            Assert.Equal(RunEnd.Committed, Press(configPath, work, worker, DesignBasis.Rev6).End);
            File.AppendAllText(Path.Combine((string)JObject.Parse(File.ReadAllText(configPath))["inputRoot"], "a note.txt"), "added");
            Assert.Equal(RunEnd.Committed, Press(configPath, work, worker, DesignBasis.Rev6).End);
            Assert.Equal(2, worker.Requests.Count);
        }

        [Fact]
        public void R635_past_job_mode_keeps_the_old_read_and_its_stop()
        {
            const string Past = "roshnPastJob";
            string configPath = Project(Past);
            string work = WorkFolderOf();
            Assert.False(ReadInputs.NoPastFiles(configPath, Past, work));
            var worker = new DataCheckWorker();
            RunOutcome stopped = Press(configPath, work, worker, Past);
            Assert.Equal(RunEnd.Stopped, stopped.End);
            Assert.Contains("is not on disk", stopped.Message);
            Assert.Empty(worker.Requests);
        }

        [Fact]
        public void R635_a_rev6_project_that_holds_every_past_file_keeps_the_old_read()
        {
            string configPath = Project(DesignBasis.Rev6);
            string work = WorkFolderOf();
            JObject config = JObject.Parse(File.ReadAllText(configPath));
            string input = (string)config["inputRoot"];
            string schedules = (string)config["addin"]["readData"]["scheduleFolder"];
            foreach (JProperty hood in ((JObject)config["connect"]["neighbourhoods"]).Properties().Where(item => item.Value.Type == JTokenType.Object && item.Value["schedule"] != null))
            {
                string path = Path.Combine(input, schedules, (string)hood.Value["schedule"]);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "made up");
            }
            string book = Path.Combine(root, "work", (string)config["addin"]["readData"]["dataBookFolder"]);
            foreach (JProperty file in ((JObject)config["addin"]["readData"]["dataBookFiles"]).Properties())
            {
                string path = Path.Combine(book, (string)file.Value);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "made up");
            }
            Assert.False(ReadInputs.NoPastFiles(configPath, DesignBasis.Rev6, work));
            Assert.Null(ReadInputs.Raw(new[] { new RunInput("Settings", configPath, false) }, PaneRoles, work, DesignBasis.Rev6));
            // One gone makes it a project with no past files
            File.Delete(Path.Combine(book, (string)((JObject)config["addin"]["readData"]["dataBookFiles"]).Properties().First().Value));
            Assert.True(ReadInputs.NoPastFiles(configPath, DesignBasis.Rev6, work));
        }

        [Fact]
        public void R635_the_plot_table_has_a_row_per_numbered_plot_with_its_sources()
        {
            JObject table = DataCheckLogic.PlotTable(JObject.Parse("{\"plots\": [{\"number\": \"7\", \"villaType\": null, \"mirror\": null, \"areaM2\": 12.5, \"groups\": [\"L1-1\", \"L2-3\"], \"source\": {\"villaType\": \"missing\"}, \"missing\": {\"villaType\": \"why\"}, \"disputed\": {}, \"weak\": {}}, {\"number\": null, \"villaType\": null, \"mirror\": null, \"groups\": []}, {\"number\": \"8\", \"villaType\": \"A1\", \"mirror\": true, \"groups\": []}]}"));
            var rows = ((JArray)table["plots"]).OfType<JObject>().ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal("L1-1", (string)rows[0]["nbhId"]);
            Assert.Equal("7", (string)rows[0]["officialNumber"]);
            Assert.Equal(12.5, (double)rows[0]["faceAreaM2"]);
            Assert.Equal("why", (string)rows[0]["missing"]["villaType"]);
            Assert.Null((string)rows[1]["nbhId"]);
            Assert.Equal("A1", (string)rows[1]["typologyRev9"]);
        }
    }
}
