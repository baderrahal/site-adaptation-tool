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
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R473, R474. The worker read takes picks.json, on a made up picks.json, a made up baseplan and made up inputs in a temporary folder, so
    // no real input is read and no Revit is needed. R847: the resolve of the picks against the scan went with the old read of step 2, its tests
    // with it. python/tests/test_r473_read_picks.py holds the Python side
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

        // R847. picks.json as the resolve of the old read of step 2 wrote it, made by hand since that resolve went with the old read. kinds maps a
        // kind to its source and from, a kind not given is the default of config
        private static JObject Picks(IDictionary<string, string[]> kinds = null, params JObject[] conflicts)
        {
            var named = new JObject();
            foreach (string kind in ReadPicks.Kinds)
            {
                string[] entry = null;
                kinds?.TryGetValue(kind, out entry);
                named[kind] = new JObject { ["source"] = entry?[0], ["from"] = entry?[1] ?? ReadPicks.FromDefault };
            }
            return new JObject { ["format"] = ReadPicks.Format, ["kinds"] = named, ["conflicts"] = new JArray(conflicts), ["lines"] = new JArray() };
        }

        // The picks the old resolve gave for a plot outline pick on the made up scan: the pick, the plot number and villa type the scan proposed
        private static JObject Outlined(string outline)
        {
            return Picks(new Dictionary<string, string[]>
            {
                ["plotOutline"] = new[] { outline, ReadPicks.FromPick }, ["plotNumber"] = new[] { "Base.dwg|MP_PLOT_TEXT", ReadPicks.FromProposal },
                ["villaType"] = new[] { SalesColumn, ReadPicks.FromProposal },
            });
        }

        // R473. picks.json read with another file than the one given for its input keeps the default and says why
        [Fact]
        public void R473_a_pick_of_another_file_keeps_the_default_with_the_line()
        {
            var renamed = new ReadPicks.PickFiles { Baseplan = "Base rev2.dwg", VillaBlocks = "Blocks.dwg", Sales = "sales.xlsx", SalesSheet = "Sales" };
            ReadPicks.PicksTaken taken = ReadPicks.Take(Picks(new Dictionary<string, string[]> { ["plotOutline"] = new[] { BothOutlines, ReadPicks.FromPick } }), renamed);
            Assert.Null(taken.PlotLineLayers);
            Assert.Contains(taken.Lines, line => line.Contains(BothOutlines) && line.Contains("Base rev2.dwg"));
            Assert.Equal(new[] { "MP_PLOT_ALT", "MP_PLOT_LINE" }, ReadPicks.Take(Outlined(BothOutlines), Files).PlotLineLayers);
        }

        // R473. A villa type of blocks, texts or a PDF page is no shape the read takes, it keeps the column of config. A sheet pick of the
        // sales sheet sets the typology column to its header, when the header row holds it
        [Fact]
        public void R473_a_villa_type_pick_of_blocks_keeps_the_default_and_a_sheet_pick_sets_the_typology_column()
        {
            ReadPicks.PicksTaken blocks = ReadPicks.Take(Picks(new Dictionary<string, string[]> { ["villaType"] = new[] { BlockTypes, ReadPicks.FromPick } }), Files);
            Assert.Null(blocks.TypologyHeader);
            Assert.Contains(blocks.Lines, line => line.Contains(BlockTypes));
            Assert.Equal("Type B", ReadPicks.Take(Picks(new Dictionary<string, string[]> { ["villaType"] = new[] { OtherColumn, ReadPicks.FromPick } }), Files).TypologyHeader);
            var columns = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("plot", "Plot No"), new KeyValuePair<string, string>("typology", "Typology_Rev9") };
            var rows = new List<object[]> { new object[] { "Plot No", "Typology_Rev9", " Type B " }, new object[] { 12.0, "DP1", "VL2" } };
            var lines = new List<string>();
            Assert.Equal("Type B", ReadPicks.TypologyColumns(columns, rows, 1, "Type B", lines).Single(column => column.Key == "typology").Value);
            Assert.Empty(lines);
            Assert.Equal("Typology_Rev9", ReadPicks.TypologyColumns(columns, rows, 1, "Type C", lines).Single(column => column.Key == "typology").Value);
            Assert.Contains("Type C", Assert.Single(lines));
        }

        // R473. A villa type conflict pick in picks.json sets the villa type of that plot in the plot table and of no other plot
        [Fact]
        public void R473_a_conflict_pick_changes_one_plots_villa_type()
        {
            JObject picks = Picks(null, new JObject { ["plot"] = "12", ["field"] = "villa type", ["source"] = BlockTypes, ["value"] = "VL2" });
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

        // R473, breaker 2 of FIX-1NOV-A2. A read of the location plan that fails after a plot outline or plot number source was taken keeps its
        // text and names the source and where to change it. With no such source the text stays byte for byte, a read of no input picks or of the defaults
        [Fact]
        public void R473_a_failed_location_plan_read_names_the_plot_outline_and_number_taken_and_with_none_stays_as_it_was()
        {
            Directory.CreateDirectory(folder);
            string none = LocationFailure(null, "f1");
            Assert.Equal("page.pdf: an affine fit of the page needs three points, it has 0 (R181)", none);
            Assert.Equal(none, LocationFailure(Picks(), "f2"));
            string picked = LocationFailure(Outlined(BothOutlines), "f3");
            Assert.StartsWith(none, picked);
            Assert.Contains(BothOutlines, picked);
            Assert.Contains("Base.dwg|MP_PLOT_TEXT", picked);
            Assert.Contains("What the tool read", picked);
        }

        // The worker read of a page of no label against the baseplan, one schedule and the sales workbook, each made up, gives the refusal of the fit
        private string LocationFailure(JObject picks, string runId)
        {
            JObject settings = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            string fit = (string)settings["dataBook"]["locationPlan"]["fitNeighbourhood"];
            string scheduleSheet = (string)settings["connect"]["scheduleSheet"];
            settings["connect"]["neighbourhoods"] = new JObject { [fit] = settings["connect"]["neighbourhoods"][fit] };
            string config = Write(Path.Combine(folder, "location.json"), settings.ToString());
            string layers = Path.Combine(Path.GetDirectoryName(WorkerRunTests.RepoConfig()), "cad_layers.json");
            string baseplan = Path.Combine(folder, "Base.dwg");
            if (!File.Exists(baseplan))
            {
                Baseplan(baseplan);
            }
            var pdf = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
            UglyToad.PdfPig.Writer.PdfDocumentBuilder.AddedFont font = pdf.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
            pdf.AddPage(400, 300).AddText("no label", 10, new UglyToad.PdfPig.Core.PdfPoint(50, 200), font);
            string page = Path.Combine(folder, "page.pdf");
            File.WriteAllBytes(page, pdf.Build());
            var inputs = new List<WorkerFile>
            {
                Input("config", config), Input("baseplan", baseplan), Input("cadLayers", layers), Input(LocationPlan.PlotNumbersRole, page),
                Input(LocationPlan.ScheduleRole + fit, MadeBook("schedule.xlsx", new Dictionary<string, System.Tuple<int, string[]>>
                {
                    [scheduleSheet] = System.Tuple.Create(1, new[] { "Plot No.", "Unit", "FFL *", "SSL", "El @ Entry", "Steps Needed" }),
                })),
                Input("sales", MadeBook("sales.xlsx", SalesSheets(settings))),
            };
            if (picks != null)
            {
                string picksPath = Path.Combine(folder, runId, "picks.json");
                Write(picksPath, picks.ToString(Newtonsoft.Json.Formatting.Indented));
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
            Assert.True(code == 2, "exit " + code + ": " + answer.Message);
            return answer.Message;
        }

        // The sheets of the sales workbook config names by their header rows, a sheet two settings name under the columns of both
        private static Dictionary<string, System.Tuple<int, string[]>> SalesSheets(JObject settings)
        {
            var sheets = new Dictionary<string, System.Tuple<int, string[]>>();
            foreach (string path in new[] { "rev9.dlus", "rev9.sales", "locationPlan.sales", "locationPlan.codeTable" })
            {
                JToken entry = settings["dataBook"].SelectToken(path);
                string[] columns = ((JObject)entry["columns"]).Properties().Select(column => (string)column.Value).ToArray();
                string sheet = (string)entry["sheet"];
                sheets[sheet] = System.Tuple.Create((int)entry["headerRow"], sheets.TryGetValue(sheet, out System.Tuple<int, string[]> held) ? held.Item2.Union(columns).ToArray() : columns);
            }
            return sheets;
        }

        // A made up workbook, per sheet the header row given and one row of plot 101 under it, a number in every cell
        private string MadeBook(string name, Dictionary<string, System.Tuple<int, string[]>> sheets)
        {
            string path = Path.Combine(folder, name);
            File.Delete(path);
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart book = document.AddWorkbookPart();
                book.Workbook = new Workbook();
                Sheets list = book.Workbook.AppendChild(new Sheets());
                uint id = 1;
                foreach (KeyValuePair<string, System.Tuple<int, string[]>> sheet in sheets)
                {
                    uint row = (uint)sheet.Value.Item1;
                    Cell[] header = sheet.Value.Item2.Select((text, column) => new Cell { CellReference = Letter(column) + row, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) }).ToArray();
                    Cell[] values = sheet.Value.Item2.Select((text, column) => new Cell { CellReference = Letter(column) + (row + 1), CellValue = new CellValue("101") }).ToArray();
                    var part = book.AddNewPart<WorksheetPart>();
                    part.Worksheet = new Worksheet(new SheetData(new Row(header) { RowIndex = row }, new Row(values) { RowIndex = row + 1 }));
                    list.Append(new Sheet { Id = book.GetIdOfPart(part), SheetId = id++, Name = sheet.Key });
                }
            }
            return path;
        }

        private static string Letter(int column)
        {
            return ((char)('A' + column)).ToString();
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
            Assert.Equal(1, ReadPlots(config, baseplan, layers, Picks(), "p1"));
            Assert.Equal(1, ReadPlots(config, baseplan, layers, Outlined(Outline), "p2"));
            Assert.Equal(2, ReadPlots(config, baseplan, layers, Outlined(BothOutlines), "p3"));
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
            System.Tuple<JObject, WorkerAnswer> none = WorkerRead(config, baseplan, layers, null, "n1");
            System.Tuple<JObject, WorkerAnswer> defaults = WorkerRead(config, baseplan, layers, Picks(), "n2");
            Assert.Equal(none.Item1["plots"], defaults.Item1["plots"], JToken.EqualityComparer);
            Assert.Equal(none.Item2.Counts, defaults.Item2.Counts);
            Assert.Equal(none.Item2.Issues, defaults.Item2.Issues);
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
        private System.Tuple<JObject, WorkerAnswer> WorkerRead(string config, string baseplan, string layers, JObject picks, string runId)
        {
            var inputs = new List<WorkerFile> { Input("config", config), Input("baseplan", baseplan), Input("cadLayers", layers) };
            if (picks != null)
            {
                string picksPath = Path.Combine(folder, runId, "picks.json");
                Write(picksPath, picks.ToString(Newtonsoft.Json.Formatting.Indented));
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
            return System.Tuple.Create(JObject.Parse(File.ReadAllText(Path.Combine(request.OutputFolder, "read_baseplan.json"))), answer);
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }
    }
}
