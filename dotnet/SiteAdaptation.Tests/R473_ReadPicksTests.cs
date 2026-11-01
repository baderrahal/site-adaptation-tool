using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CadReader;
using CSMath;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R473, R474. Button 2 reads with the picks of the window What the tool read, on a made up scan.json, a made up baseplan and made up
    // inputs in a temporary folder, so no real input is read and no Revit is needed. python/tests/test_r473_read_picks.py holds the Python side
    public sealed class R473_ReadPicksTests : IDisposable
    {
        private const string Outline = "Base.dwg|MP_PLOT_LINE";
        private const string BothOutlines = "Base.dwg|MP_PLOT_ALT + MP_PLOT_LINE";
        private const string SalesColumn = "sales.xlsx|Sales|Typology_Rev9|shift 0";
        private const string OtherColumn = "sales.xlsx|Sales|Type B|shift 0";
        private const string BlockTypes = "Blocks.dwg|block|MP_VL";

        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa-picks-" + Guid.NewGuid().ToString("N"));

        private static readonly ReadPicks.PickFiles Files = new ReadPicks.PickFiles { Baseplan = "Base.dwg", VillaBlocks = "Blocks.dwg", Sales = "sales.xlsx", SalesSheet = "Sales" };

        public void Dispose()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        private static JObject Row(string kind, string source, string use)
        {
            return new JObject { ["kind"] = kind, ["source"] = source, ["found"] = "", ["score"] = 1, ["proposedUse"] = use };
        }

        private static JObject Scan(string baseplan = "Base.dwg", string sales = "sales.xlsx", string sheet = "Sales")
        {
            Func<string, string> named = source => source.Replace("Base.dwg", baseplan).Replace("sales.xlsx|Sales", sales + "|" + sheet);
            return new JObject
            {
                ["rows"] = new JArray(
                    Row("plotOutline", named(Outline), "plot outlines"), Row("plotOutline", named(BothOutlines), "check"), Row("plotNumber", named("Base.dwg|MP_PLOT_TEXT"), "plot numbers"),
                    Row("villaBlock", "Blocks.dwg|MP_VL", "villa blocks"), Row("villaType", named(SalesColumn), "villa type"), Row("villaType", named(OtherColumn), "check"),
                    Row("villaType", BlockTypes, "check")),
                ["conflicts"] = new JArray(
                    new JObject { ["plot"] = "12", ["field"] = "villa type", ["valueA"] = "DP1", ["sourceA"] = named(SalesColumn), ["valueB"] = "VL2", ["sourceB"] = BlockTypes },
                    new JObject { ["plot"] = "13", ["field"] = "label", ["valueA"] = "13", ["sourceA"] = named(Outline), ["valueB"] = "13a", ["sourceB"] = "Other.dwg|X" }),
            };
        }

        private static ProjectSettings Picked(IDictionary<string, string> scanPicks, IDictionary<string, string> conflictPicks = null)
        {
            return new ProjectSettings
            {
                ScanPicks = scanPicks == null ? null : new SortedDictionary<string, string>(scanPicks, StringComparer.Ordinal),
                ConflictPicks = conflictPicks == null ? null : new SortedDictionary<string, string>(conflictPicks, StringComparer.Ordinal),
            };
        }

        private static string From(JObject picks, string kind)
        {
            return (string)picks["kinds"][kind]["from"];
        }

        private static string Source(JObject picks, string kind)
        {
            return (string)picks["kinds"][kind]["source"];
        }

        // R473. With no scan in the work folder a saved pick names no source of a scan, so every kind keeps the default of config as before
        [Fact]
        public void R473_no_scan_keeps_the_default_of_config_for_every_kind()
        {
            JObject picks = ReadPicks.Resolve(Picked(new Dictionary<string, string> { ["plotOutline"] = BothOutlines }, new Dictionary<string, string> { ["12|villa type|" + BlockTypes] = BlockTypes }), null, Files);
            foreach (string kind in ReadPicks.Kinds)
            {
                Assert.Equal("default", From(picks, kind));
                Assert.Null(Source(picks, kind));
            }
            Assert.Empty((JArray)picks["conflicts"]);
            ReadPicks.PicksTaken taken = ReadPicks.Take(picks, Files);
            Assert.Null(taken.PlotLineLayers);
            Assert.Null(taken.PlotLabelLayers);
            Assert.Null(taken.VillaBlockLayer);
            Assert.Null(taken.TypologyHeader);
            Assert.Empty(taken.Conflicts);
        }

        // R473. A saved pick that names a source of the scan is read, one that names none, a file renamed by a new revision, gives the proposal
        [Fact]
        public void R473_a_pick_of_a_source_of_the_scan_is_read_and_a_pick_naming_none_falls_back_to_the_proposal()
        {
            JObject picks = ReadPicks.Resolve(Picked(new Dictionary<string, string> { ["plotOutline"] = BothOutlines, ["plotNumber"] = "Base.dwg|GONE" }), Scan(), Files);
            Assert.Equal("pick", From(picks, "plotOutline"));
            Assert.Equal(BothOutlines, Source(picks, "plotOutline"));
            Assert.Equal("proposal", From(picks, "plotNumber"));
            Assert.Equal("Base.dwg|MP_PLOT_TEXT", Source(picks, "plotNumber"));
            // Breaker 1 of FIX-1NOV-A2: a villa block source names one insert layer of a drawing that holds one villa type per layer, so the
            // proposal is not taken and falls back quietly
            Assert.Equal("default", From(picks, "villaBlock"));
            ReadPicks.PicksTaken taken = ReadPicks.Take(picks, Files);
            Assert.Equal(new[] { "MP_PLOT_ALT", "MP_PLOT_LINE" }, taken.PlotLineLayers);
            Assert.Equal(new[] { "MP_PLOT_TEXT" }, taken.PlotLabelLayers);
            Assert.Null(taken.VillaBlockLayer);
            Assert.Equal("Typology_Rev9", taken.TypologyHeader);
            Assert.Empty(taken.Lines);
        }

        // R473. A pick of another file than the one given for its input keeps the default and says why, at the resolve and at the read
        [Fact]
        public void R473_a_pick_of_another_file_keeps_the_default_with_the_line()
        {
            var renamed = new ReadPicks.PickFiles { Baseplan = "Base rev2.dwg", VillaBlocks = "Blocks.dwg", Sales = "sales.xlsx", SalesSheet = "Sales" };
            JObject picks = ReadPicks.Resolve(Picked(new Dictionary<string, string> { ["plotOutline"] = BothOutlines }), Scan(), renamed);
            Assert.Equal("default", From(picks, "plotOutline"));
            Assert.Null(Source(picks, "plotOutline"));
            Assert.Contains(((JArray)picks["lines"]).Select(line => (string)line), line => line.Contains(BothOutlines) && line.Contains("Base rev2.dwg"));
            // picks.json resolved on the right file and read with another one keeps the default at the read too
            ReadPicks.PicksTaken taken = ReadPicks.Take(ReadPicks.Resolve(Picked(new Dictionary<string, string> { ["plotOutline"] = BothOutlines }), Scan(), Files), renamed);
            Assert.Null(taken.PlotLineLayers);
            Assert.Contains(taken.Lines, line => line.Contains(BothOutlines) && line.Contains("Base rev2.dwg"));
        }

        // R473. A villa type of blocks, texts or a PDF page is no shape the read takes, it keeps the column of config. A sheet pick of the
        // sales sheet sets the typology column to its header, when the header row holds it
        // R473, review 1 of FIX-1NOV-A2. A proposal of the scan the read can not take, a villa type of blocks, falls back to the default
        // with no line, so a read with no pick ends with no issue of it, while the same source picked says why
        [Fact]
        public void R473_a_proposal_the_read_can_not_take_falls_back_with_no_line_and_the_same_pick_says_why()
        {
            var scan = new JObject { ["rows"] = new JArray(Row("villaType", BlockTypes, "villa type")), ["conflicts"] = new JArray() };
            JObject proposed = ReadPicks.Resolve(null, scan, Files);
            Assert.Equal(ReadPicks.FromDefault, From(proposed, "villaType"));
            Assert.Empty((JArray)proposed["lines"]);
            JObject picked = ReadPicks.Resolve(Picked(new Dictionary<string, string> { ["villaType"] = BlockTypes }), scan, Files);
            Assert.Equal(ReadPicks.FromDefault, From(picked, "villaType"));
            Assert.Single((JArray)picked["lines"]);
        }

        [Fact]
        public void R473_a_villa_type_pick_of_blocks_keeps_the_default_and_a_sheet_pick_sets_the_typology_column()
        {
            JObject blocks = ReadPicks.Resolve(Picked(new Dictionary<string, string> { ["villaType"] = BlockTypes }), Scan(), Files);
            Assert.Equal("default", From(blocks, "villaType"));
            Assert.Contains(((JArray)blocks["lines"]).Select(line => (string)line), line => line.Contains(BlockTypes));
            JObject sheet = ReadPicks.Resolve(Picked(new Dictionary<string, string> { ["villaType"] = OtherColumn }), Scan(), Files);
            Assert.Equal("pick", From(sheet, "villaType"));
            Assert.Equal("Type B", ReadPicks.Take(sheet, Files).TypologyHeader);
            var columns = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("plot", "Plot No"), new KeyValuePair<string, string>("typology", "Typology_Rev9") };
            var rows = new List<object[]> { new object[] { "Plot No", "Typology_Rev9", " Type B " }, new object[] { 12.0, "DP1", "VL2" } };
            var lines = new List<string>();
            Assert.Equal("Type B", ReadPicks.TypologyColumns(columns, rows, 1, "Type B", lines).Single(column => column.Key == "typology").Value);
            Assert.Empty(lines);
            Assert.Equal("Typology_Rev9", ReadPicks.TypologyColumns(columns, rows, 1, "Type C", lines).Single(column => column.Key == "typology").Value);
            Assert.Contains("Type C", Assert.Single(lines));
        }

        // R473. A conflict pick whose key, plot, field and second source, matches a conflict of the scan sets that plot and field to the value
        // of the chosen source, here one plot's villa type. A pick of no conflict of the scan is left out, a label the read takes from the plot
        // drawing only says why
        [Fact]
        public void R473_a_conflict_pick_changes_one_plots_villa_type()
        {
            var conflicts = new Dictionary<string, string>
            {
                ["12|villa type|" + BlockTypes] = BlockTypes, ["13|label|Other.dwg|X"] = "Other.dwg|X", ["14|villa type|" + BlockTypes] = BlockTypes,
            };
            JObject picks = ReadPicks.Resolve(Picked(null, conflicts), Scan(), Files);
            JObject applied = (JObject)Assert.Single((JArray)picks["conflicts"]);
            Assert.Equal(new JObject { ["plot"] = "12", ["field"] = "villa type", ["source"] = BlockTypes, ["value"] = "VL2" }, applied, JToken.EqualityComparer);
            Assert.Contains(((JArray)picks["lines"]).Select(line => (string)line), line => line.Contains("13") && line.Contains("Other.dwg|X"));
            var table = new Dictionary<string, Dictionary<string, object>>
            {
                ["12"] = new Dictionary<string, object> { ["baseplanLabel"] = "12", ["typologyRev9"] = "DP1" },
                ["13"] = new Dictionary<string, object> { ["baseplanLabel"] = "13", ["typologyRev9"] = "DP1" },
            };
            var lines = new List<string>();
            Assert.Equal(1, ReadPicks.ApplyConflicts(table, ReadPicks.Take(picks, Files).Conflicts, lines));
            Assert.Equal("VL2", table["12"]["typologyRev9"]);
            Assert.Equal("DP1", table["13"]["typologyRev9"]);
            Assert.Empty(lines);
        }

        // R473. The worker read takes the plot outline layers of the pick over plotLineLayers of the layer list, so one changed pick gives
        // another plot count. picks.json of the defaults reads as before
        [Fact]
        public void R473_a_plot_outline_pick_changes_the_plot_count_of_the_worker_read()
        {
            Directory.CreateDirectory(folder);
            string baseplan = Baseplan(Path.Combine(folder, "Base.dwg"));
            string config = Path.Combine(folder, "project.json");
            File.Copy(WorkerRunTests.RepoConfig(), config);
            string layers = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "cad_layers.json");
            Assert.Equal(1, ReadPlots(config, baseplan, layers, ReadPicks.Resolve(null, null, Files), "p1"));
            Assert.Equal(1, ReadPlots(config, baseplan, layers, ReadPicks.Resolve(Picked(new Dictionary<string, string> { ["plotOutline"] = Outline }), Scan(), Files), "p2"));
            Assert.Equal(2, ReadPlots(config, baseplan, layers, ReadPicks.Resolve(Picked(new Dictionary<string, string> { ["plotOutline"] = BothOutlines }), Scan(), Files), "p3"));
        }

        // R473, review 1 of FIX-1NOV-A2. A read with picks.json of the defaults gives the plots, counts and issues of a read with no input
        // picks, the read of before R473
        [Fact]
        public void R473_a_read_with_default_picks_is_the_read_with_no_picks()
        {
            Directory.CreateDirectory(folder);
            string baseplan = Baseplan(Path.Combine(folder, "Base.dwg"));
            string config = Path.Combine(folder, "project.json");
            File.Copy(WorkerRunTests.RepoConfig(), config);
            string layers = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "cad_layers.json");
            Tuple<JObject, WorkerAnswer> none = WorkerRead(config, baseplan, layers, null, "n1");
            Tuple<JObject, WorkerAnswer> defaults = WorkerRead(config, baseplan, layers, ReadPicks.Resolve(null, null, Files), "n2");
            Assert.Equal(none.Item1["plots"], defaults.Item1["plots"], JToken.EqualityComparer);
            Assert.Equal(none.Item2.Counts, defaults.Item2.Counts);
            Assert.Equal(none.Item2.Issues, defaults.Item2.Issues);
        }

        // R473, R474. Button 2 writes picks.json into the folder of the read as the input picks, so the hash of the read, and its folder,
        // changes with a pick, and read.json names the source of each kind and from where, pick, proposal or default. With no scan and
        // no pick every kind is the default
        [Fact]
        public void R474_read_json_names_each_source_and_r473_the_read_hash_changes_with_a_pick()
        {
            string work = Path.Combine(folder, "SiteAdaptation", "site_0123abcd");
            List<RunInput> inputs = Inputs(work);
            ProjectSettings settings = null;
            var worker = new PlotTableWorker();
            var run = new ReadDataRun(Roles, worker, new WorkerDocument { Path = @"C:\Models\site.rvt", SessionId = "s1" }, () => new SettingsScope(), () => settings);

            JObject first = Start(run, worker, work, inputs, "2_20261101-000001-001");
            Assert.All(ReadPicks.Kinds, kind => Assert.Equal("default", (string)first["sources"]["kinds"][kind]["from"]));
            Assert.Empty((JArray)first["sources"]["conflicts"]);

            string scanFolder = Path.Combine(work, "scan_2_20261101-000002-000");
            Directory.CreateDirectory(scanFolder);
            File.WriteAllText(Path.Combine(scanFolder, "scan.json"), Scan("baseplan.dwg", "sales.xlsx", SalesSheet()).ToString());
            JObject second = Start(run, worker, work, inputs, "2_20261101-000003-001");
            Assert.Equal("proposal", (string)second["sources"]["kinds"]["plotOutline"]["from"]);
            Assert.Equal("baseplan.dwg|MP_PLOT_LINE", (string)second["sources"]["kinds"]["plotOutline"]["source"]);
            Assert.Equal("default", (string)second["sources"]["kinds"]["villaBlock"]["from"]);

            settings = Picked(new Dictionary<string, string> { ["plotOutline"] = "baseplan.dwg|MP_PLOT_ALT + MP_PLOT_LINE" },
                new Dictionary<string, string> { ["12|villa type|" + BlockTypes] = BlockTypes });
            JObject third = Start(run, worker, work, inputs, "2_20261101-000004-001");
            Assert.Equal(new JObject { ["source"] = "baseplan.dwg|MP_PLOT_ALT + MP_PLOT_LINE", ["from"] = "pick" }, third["sources"]["kinds"]["plotOutline"], JToken.EqualityComparer);
            Assert.Equal(new JObject { ["plot"] = "12", ["field"] = "villa type", ["source"] = BlockTypes }, Assert.Single((JArray)third["sources"]["conflicts"]), JToken.EqualityComparer);

            List<string> folders = worker.Requests.Select(request => request.OutputFolder).ToList();
            Assert.Equal(3, folders.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (WorkerRequest request in worker.Requests)
            {
                WorkerFile picks = Assert.Single(request.Inputs, input => input.Role == "picks");
                Assert.Equal(Path.Combine(request.OutputFolder, "picks.json"), picks.Path);
                Assert.Equal(FileHash.Sha256(picks.Path), picks.Sha256);
            }
            // The same pick again reuses the read of the third run and starts no worker
            JObject again = Start(run, worker, work, inputs, "2_20261101-000005-001");
            Assert.Equal(3, worker.Requests.Count);
            Assert.Equal("pick", (string)again["sources"]["kinds"]["plotOutline"]["from"]);
        }

        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string>
        {
            ["Settings"] = "config", ["Baseplan"] = "baseplan", ["Villa blocks"] = "villaBlocks", ["Curbs"] = "curbs", ["Road surface"] = "roadSurface",
            ["DLUS schedule"] = "dlus", ["Sales schedule"] = "sales", ["Plot numbers"] = "plotNumbers",
        };

        private static string SalesSheet()
        {
            return (string)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig())).SelectToken("dataBook.locationPlan.sales.sheet");
        }

        private JObject Start(ReadDataRun run, PlotTableWorker worker, string work, List<RunInput> inputs, string runId)
        {
            var context = new RunContext(2, "2 Read data", runId, work, null, true, inputs, new Dictionary<string, string>());
            RunLater later = null;
            run.Body().Start(context, end => later = end);
            Assert.NotNull(later);
            Assert.True(later.Failure == null, later.Failure);
            string table = later.Outputs.Keys.Single();
            return JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(table), ReadDataRun.RecordFile)));
        }

        // The made up inputs of button 2 as the tests of R375 make them
        private List<RunInput> Inputs(string work)
        {
            string input = Path.Combine(folder, "input");
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            config["inputRoot"] = input;
            string configPath = Write(Path.Combine(folder, "config", "project.json"), config.ToString());
            Write(Path.Combine(folder, "config", "cad_layers.json"), "{}");
            var inputs = new List<RunInput>
            {
                new RunInput("Settings", configPath, false),
                new RunInput("Baseplan", Write(Path.Combine(input, "baseplan.dwg"), "a made up baseplan"), false),
                new RunInput("Villa blocks", Write(Path.Combine(input, "blocks.dwg"), "made up blocks"), false),
                new RunInput("Curbs", Write(Path.Combine(input, "curbs.dwg"), "made up curbs"), false),
                new RunInput("DLUS schedule", Write(Path.Combine(input, "dlus.xlsx"), "a made up DLUS"), false),
                new RunInput("Sales schedule", Write(Path.Combine(input, "sales.xlsx"), "a made up sales"), false),
                new RunInput("Plot numbers", Write(Path.Combine(input, "plan.pdf"), "a made up page"), false),
            };
            string roads = Path.Combine(input, "roads");
            foreach (JToken name in (JArray)config["siteBuild"]["roadSurfaces"])
            {
                Write(Path.Combine(roads, (string)name + ".xml"), "<LandXML/>");
            }
            inputs.Add(new RunInput("Road surface", roads, true));
            foreach (JProperty hood in ((JObject)config["connect"]["neighbourhoods"]).Properties().Where(hood => hood.Value.Type == JTokenType.Object))
            {
                Write(Path.Combine(input, "01_Output", "1.11 References", (string)hood.Value["schedule"]), "a made up schedule of " + hood.Name);
            }
            string book = Path.Combine(folder, "SiteAdaptation", "data_book");
            Write(Path.Combine(book, "plots.csv"), "label\n1\n");
            Write(Path.Combine(book, "location_plan", "labels.csv"), "number\n2301\n");
            Write(Path.Combine(book, "phase1_border.csv"), "x,y\n0,0\n");
            Directory.CreateDirectory(work);
            return inputs;
        }

        private static string Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        // A made up baseplan in metres: plot 12 drawn on MP_PLOT_LINE and plot 13 on MP_PLOT_ALT, both labels on MP_PLOT_TEXT
        private static string Baseplan(string path)
        {
            var document = new CadDocument();
            document.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Meters;
            var line = new Layer("MP_PLOT_LINE");
            var alt = new Layer("MP_PLOT_ALT");
            var text = new Layer("MP_PLOT_TEXT");
            document.Layers.Add(line);
            document.Layers.Add(alt);
            document.Layers.Add(text);
            Square(document, line, 1000, 5000);
            Square(document, alt, 1100, 5000);
            document.Entities.Add(new MText { Value = "DP1\\P12", InsertPoint = new XYZ(1005, 5010, 0), Layer = text });
            document.Entities.Add(new MText { Value = "DP1\\P13", InsertPoint = new XYZ(1105, 5010, 0), Layer = text });
            using (var writer = new DwgWriter(path, document))
            {
                writer.Write();
            }
            return path;
        }

        private static void Square(CadDocument document, Layer layer, double x, double y)
        {
            double[] corners = { x, y, x + 10, y, x + 10, y + 20, x, y + 20 };
            for (int corner = 0; corner < 4; corner++)
            {
                int next = (corner + 1) % 4;
                document.Entities.Add(new Line { StartPoint = new XYZ(corners[corner * 2], corners[corner * 2 + 1], 0), EndPoint = new XYZ(corners[next * 2], corners[next * 2 + 1], 0), Layer = layer });
            }
        }

        // The plot shapes of the baseplan the worker command read writes with these picks
        private int ReadPlots(string config, string baseplan, string layers, JObject picks, string runId)
        {
            JObject read = WorkerRead(config, baseplan, layers, picks, runId).Item1;
            return ((JArray)read["plots"]).Count(plot => (string)plot["kind"] == "plot_shape");
        }

        // The worker read of the baseplan with picks.json as its input picks, or with no input picks when picks is null, its read_baseplan.json and answer
        private Tuple<JObject, WorkerAnswer> WorkerRead(string config, string baseplan, string layers, JObject picks, string runId)
        {
            var inputs = new List<WorkerFile> { Input("config", config), Input("baseplan", baseplan), Input("cadLayers", layers) };
            if (picks != null)
            {
                string picksPath = Path.Combine(folder, runId, "picks.json");
                ReadPicks.Write(picksPath, runId, picks);
                inputs.Add(Input("picks", picksPath));
            }
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = runId, Command = "read", Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = inputs, Scope = new WorkerScope { Kind = "project" }, OutputFolder = Path.Combine(folder, runId, "out"), Steps = new List<string>(),
                StepsFolder = Path.Combine(folder, runId, "steps"), TimeLimitS = 600,
            };
            int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, runId));
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            return Tuple.Create(JObject.Parse(File.ReadAllText(Path.Combine(request.OutputFolder, "read_baseplan.json"))), answer);
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }

        // The worker of button 2 run in process with a made up command read that writes a plot table of one plot
        private sealed class PlotTableWorker : IWorkerStart
        {
            public List<WorkerRequest> Requests { get; } = new List<WorkerRequest>();

            public void Start(string requestPath, string workFolder, WorkerRequest request, Action<WorkerAnswer> ended)
            {
                Requests.Add(request);
                var commands = new Dictionary<string, WorkerCommand>
                {
                    ["read"] = context => context.WriteJson(request.OutputFolder, ReadDataRun.PlotTableFile,
                        new JObject { ["plots"] = new JArray(new JObject { ["baseplanLabel"] = "12", ["officialNumber"] = "2301", ["nbhId"] = "013" }) }),
                };
                WorkerRunner.Run(requestPath, commands, () => 1e6, () => 0.0);
                ended(WorkerAnswer.Read(WorkerFiles.AnswerPath(workFolder, request.RunId)));
            }
        }
    }
}
