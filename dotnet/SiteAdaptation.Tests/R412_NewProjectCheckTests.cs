using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CadReader;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R412. The read only check of the inputs of a new project, NewProjectCheck of Common with the readers of the worker, on small inputs built in
    // code (R10). The inputs and the lines are those of python/tests/test_r412_new_project_check.py, so the two sides give one report
    public sealed class R412_NewProjectCheckTests : IDisposable
    {
        private const string Guid = "7a7813d7-ad61-4bf2-a75f-6234c92957bb";
        private const string Prototype = "KEO-ARC-C10-ZZ.rvt";

        private static readonly string[][] Lines =
        {
            new[] { "position file", "ok", "pos.json holds the project position, the survey point, the project base point and 2 levels", null },
            new[]
            {
                "parameter sheet", "does not fit",
                "3 of 4 parameter names have no GUID in parameters.xlsx: AA_Two (no GUID), AA_Three (not in the sheet), AA_Four (not in the sheet)",
                "the names are held to the prefix AA_ of the ROSHN Makkah parameters",
            },
            new[] { "villa offsets", "does not fit", "plotFrame.units of prototype_offsets.json holds no offset for the unit codes: VL3, TH1", "measured on the ROSHN Makkah past job N1" },
            new[]
            {
                "hand tables", "does not fit", "the hand tables hold no entry for the prototype files of the unit codes: TH1",
                "the tables are keyed by the KEO prototype file names of the ROSHN Makkah job",
            },
            new[] { "prototype files", "does not fit", "the prototype folder protos holds no single file for the unit codes: TH1", "files are found by the -ARC-<code>- part of the KEO file names" },
            new[] { "Schedule N1", "ok", "s1.xlsx sheet Sched holds the Plot No. header row", "held to the sheet Sched and a header cell with Plot No." },
        };

        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r412_" + System.Guid.NewGuid().ToString("N"));
        private string work, root, config;

        public R412_NewProjectCheckTests()
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

        // A workbook of one sheet, each row an array of texts, a null leaves the cell empty
        private static void SheetBook(string path, string sheet, List<string[]> rows)
        {
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart book = document.AddWorkbookPart();
                book.Workbook = new Workbook();
                var data = new SheetData();
                for (int index = 0; index < rows.Count; index++)
                {
                    var row = new Row { RowIndex = (uint)(index + 1) };
                    for (int column = 0; column < rows[index].Length; column++)
                    {
                        if (rows[index][column] != null)
                        {
                            row.Append(new Cell { CellReference = (char)('A' + column) + (index + 1).ToString(), DataType = CellValues.InlineString, InlineString = new InlineString(new Text(rows[index][column])) });
                        }
                    }
                    data.Append(row);
                }
                var part = book.AddNewPart<WorksheetPart>();
                part.Worksheet = new Worksheet(new SheetDimension { Reference = "A1:B" + rows.Count }, data);
                book.Workbook.AppendChild(new Sheets()).Append(new Sheet { Id = book.GetIdOfPart(part), SheetId = 1, Name = sheet });
            }
        }

        private void Build()
        {
            work = Path.Combine(folder, "w");
            root = Path.Combine(folder, "in");
            config = Path.Combine(folder, "cfg");
            foreach (string path in new[] { work, Path.Combine(root, "refs"), Path.Combine(root, "protos"), config })
            {
                Directory.CreateDirectory(path);
            }
            string point = "{ 'internalM': [1.0, 2.0, 3.0], 'sharedM': [4.0, 5.0, 6.0] }";
            File.WriteAllText(Path.Combine(work, "pos.json"), JObject.Parse(@"{ 'format': 'site-adaptation-position', 'formatVersion': 1,
                'projectPosition': { 'eastM': 1.0, 'northM': 2.0, 'elevationM': 3.0, 'angleDeg': 0.5 }, 'surveyPoint': " + point + ", 'projectBasePoint': " + point + @",
                'levels': [ { 'name': 'L1', 'elevationM': 0.0 }, { 'name': 'L2', 'elevationM': 3.0 } ], 'levelBase': 'Project Base Point' }").ToString());
            var schedule = new List<string[]> { new[] { "t" } };
            schedule.AddRange(Enumerable.Repeat(new string[] { null }, 38));
            schedule.Add(new[] { "Plot No.", "Size" });
            SheetBook(Path.Combine(root, "refs", "s1.xlsx"), "Sched", schedule);
            File.WriteAllBytes(Path.Combine(root, "protos", Prototype), new byte[] { 120 });
            SheetBook(Path.Combine(config, "parameters.xlsx"), "Parameters", new List<string[]>
            {
                new[] { "Parameter", "GUID", "Data type", "Group", "Category", "Filled by", "Sample value", "Keep" },
                new[] { "AA_One", Guid, "Text", "Data", "Floors", "5", null, "Yes" }, new[] { "AA_Two", null, "Text", "Data", "RVT Links", "7", null, "Yes" },
            });
            File.WriteAllText(Path.Combine(config, "prototype_offsets.json"), "{ \"plotFrame\": { \"units\": { \"C10\": {} } } }");
            File.WriteAllText(Path.Combine(config, "rules.json"), JObject.Parse(@"{ 'typologies': { 'C10': {}, 'VL3': { 'keo_file': 'C10' }, 'TH1': {} },
                'levelProfiles': { 'default': 'p', 'p': { 'keyRules': { 'parameter AA_Four': {}, 'hand villa': { 'byFile': { '" + Prototype + @"': {} } } } } } }").ToString());
            File.WriteAllText(Path.Combine(config, "project.json"), JObject.Parse(@"{
                'inputRoot': " + Newtonsoft.Json.JsonConvert.ToString(root) + @", 'floors': { 'plotParameter': 'AA_One' },
                'pastJob': { 'neighbourhood': 'N1', 'prototypeFolder': 'protos', 'parameters': [ 'AA_Two', 'AA_Three' ] }, 'majority': { 'parameterLevels': [] },
                'connect': { 'neighbourhoods': { 'N1': { 'schedule': 's1.xlsx' }, 'N2': null }, 'scheduleSheet': 'Sched' },
                'addin': { 'workbookRoles': { 'bk': 'dlus' }, 'position': { 'file': '{workRoot}/pos.json' }, 'readData': { 'scheduleFolder': 'refs' },
                    'parameterSheet': " + Newtonsoft.Json.JsonConvert.ToString(Path.Combine(config, "parameters.xlsx")) + @" } }").ToString());
        }

        private NewProjectReport Run()
        {
            return NewProjectCheck.Check(Path.Combine(config, "project.json"), Path.Combine(config, "rules.json"), Path.Combine(config, "prototype_offsets.json"), work, root, WorkerReads.CheckReaders());
        }

        private static string[] Shown(CheckLine line)
        {
            return new[] { line.Input, line.Status, line.Text, line.RoshnNote };
        }

        [Fact]
        public void R412_the_check_gives_one_line_per_input_with_its_status_and_roshn_note_on_made_up_inputs()
        {
            Build();
            NewProjectReport report = Run();
            Assert.Equal(Lines.Length, report.Lines.Count);
            for (int index = 0; index < Lines.Length; index++)
            {
                Assert.Equal(Lines[index], Shown(report.Lines[index]));
                Assert.Equal(Lines[index][3] != null, report.Lines[index].RoshnOnly);
            }
            JObject json = report.ToJson();
            Assert.Equal(JObject.Parse("{ 'ok': 2, 'missing': 0, 'doesNotFit': 4, 'roshnOnly': 5 }").ToString(), json["counts"].ToString());
            Assert.Equal(6, report.Inputs.Count);
            Assert.All(report.Inputs, input => Assert.True((bool)input["unchanged"]));
            Assert.Equal(report.Inputs.Select(input => (string)input["name"]).OrderBy(name => name, StringComparer.Ordinal), report.Inputs.Select(input => (string)input["name"]));
            string[] text = report.Text().Split('\n');
            Assert.Equal("New project check, 6 lines: 2 ok, 0 missing, 4 do not fit, 5 ROSHN only", text[0]);
            Assert.Equal("position file: ok. pos.json holds the project position, the survey point, the project base point and 2 levels", text[1]);
            Assert.Equal("Inputs opened: 6, none changed", text[text.Length - 2]);
        }

        // R511. The source the project holds decides, not the default of a new project in config, so a past job with its position file still gets the line. The same cases in the Python
        private CheckLine PositionOf(string source, string file = null)
        {
            return NewProjectCheck.Check(Path.Combine(config, "project.json"), Path.Combine(config, "rules.json"), Path.Combine(config, "prototype_offsets.json"), work, root, WorkerReads.CheckReaders(), source, file)
                .Lines.Single(line => line.Input == "position file");
        }

        [Fact]
        public void R511_the_new_project_check_reads_the_source_the_project_holds()
        {
            Build();
            File.Delete(Path.Combine(work, "pos.json"));
            JObject held = JObject.Parse(File.ReadAllText(Path.Combine(config, "project.json")));
            held["coordinates"] = new JObject { ["source"] = "positionFile", ["newProjectSource"] = "cad" };
            File.WriteAllText(Path.Combine(config, "project.json"), held.ToString());

            Assert.Equal("missing", PositionOf(null).Status);
            Assert.Equal("missing", PositionOf("positionFile").Status);
            Assert.Equal("ok", PositionOf("cad").Status);
            Assert.Equal("not asked, this project takes its position from CAD", PositionOf("cad").Text);
            Assert.Equal("not asked, this project takes its position from typed values", PositionOf("typed").Text);

            held["coordinates"]["source"] = "cad";
            File.WriteAllText(Path.Combine(config, "project.json"), held.ToString());
            Assert.Equal("ok", PositionOf(null).Status);
            Assert.Equal("missing", PositionOf("positionFile").Status);
            Assert.Equal("gone.json is not there", PositionOf("positionFile", Path.Combine(work, "gone.json")).Text);
        }

        [Fact]
        public void R412_an_input_that_is_not_there_is_missing_and_one_that_cannot_be_read_does_not_fit()
        {
            Build();
            File.Delete(Path.Combine(work, "pos.json"));
            File.Delete(Path.Combine(root, "refs", "s1.xlsx"));
            File.WriteAllText(Path.Combine(config, "prototype_offsets.json"), "[]");
            File.Delete(Path.Combine(root, "protos", Prototype));
            Dictionary<string, CheckLine> got = Run().Lines.ToDictionary(line => line.Input);
            Assert.Equal(new[] { "missing", "pos.json is not there" }, new[] { got["position file"].Status, got["position file"].Text });
            Assert.Equal(new[] { "does not fit", "prototype_offsets.json cannot be read as a JSON object" }, new[] { got["villa offsets"].Status, got["villa offsets"].Text });
            Assert.Equal(new[] { "missing", "s1.xlsx is not there" }, new[] { got["Schedule N1"].Status, got["Schedule N1"].Text });
            Assert.Equal("the prototype folder protos holds no single file for the unit codes: C10, VL3, TH1", got["prototype files"].Text);
        }

        [Fact]
        public void R412_a_position_file_with_a_missing_point_or_level_does_not_fit_and_names_what_is_wrong()
        {
            Build();
            string path = Path.Combine(work, "pos.json");
            JObject found = JObject.Parse(File.ReadAllText(path));
            found["formatVersion"] = 2;
            found["surveyPoint"]["sharedM"] = new JArray(1.0, 2.0);
            ((JObject)found["projectPosition"]).Remove("angleDeg");
            ((JObject)found["levels"][1]).Remove("name");
            found.Remove("levelBase");
            File.WriteAllText(path, found.ToString());
            CheckLine line = Run().Lines[0];
            Assert.Equal(new[] { "does not fit", "pos.json lacks or has wrong: formatVersion, projectPosition.angleDeg, surveyPoint.sharedM, levels[1].name, levelBase" }, new[] { line.Status, line.Text });
            Assert.False(line.RoshnOnly);
        }

        [Fact]
        public void R412_an_input_that_changes_while_the_check_reads_it_fails_the_run()
        {
            Build();
            string book = Path.Combine(root, "refs", "s1.xlsx");
            NewProjectReaders real = WorkerReads.CheckReaders();
            bool touched = false;
            var touching = new NewProjectReaders
            {
                SheetNames = real.SheetNames,
                Rows = (path, sheet, count) =>
                {
                    IReadOnlyList<IReadOnlyList<string>> rows = real.Rows(path, sheet, count);
                    if (!touched && Path.GetFullPath(path) == Path.GetFullPath(book))
                    {
                        touched = true;
                        using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        {
                            stream.WriteByte(120);
                        }
                    }
                    return rows;
                },
            };
            var refused = Assert.Throws<InvalidDataException>(() => NewProjectCheck.Check(Path.Combine(config, "project.json"), Path.Combine(config, "rules.json"),
                Path.Combine(config, "prototype_offsets.json"), work, root, touching));
            Assert.Contains("an input changed while the check read it (D19)", refused.Message);
            Assert.Contains("its size", refused.Message);
            Assert.Contains("SHA-256", refused.Message);
        }

        [Fact]
        public void R412_the_output_folder_is_never_beside_an_input_or_under_the_input_root()
        {
            Build();
            NewProjectReport report = Run();
            var beside = Assert.Throws<InvalidDataException>(() => NewProjectCheck.KeepApart(config, report.Paths, report.InputRoot));
            Assert.Contains("beside an input", beside.Message);
            var under = Assert.Throws<InvalidDataException>(() => NewProjectCheck.KeepApart(Path.Combine(root, "reports"), report.Paths, report.InputRoot));
            Assert.Contains("under the input root", under.Message);
            NewProjectCheck.KeepApart(Path.Combine(folder, "out"), report.Paths, report.InputRoot);
        }

        [Fact]
        public void R412_the_worker_command_newProjectCheck_writes_the_report_to_the_output_folder_and_nothing_beside_an_input()
        {
            Build();
            // The worker takes the time limits and the memory floor from the config it is given, so the made up settings are laid over the repo config
            JObject merged = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            foreach (JProperty item in JObject.Parse(File.ReadAllText(Path.Combine(config, "project.json"))).Properties())
            {
                if (item.Name == "addin")
                {
                    foreach (JProperty part in ((JObject)item.Value).Properties())
                    {
                        merged["addin"][part.Name] = part.Value;
                    }
                }
                else
                {
                    merged[item.Name] = item.Value;
                }
            }
            merged["addin"]["position"]["file"] = Path.Combine(work, "pos.json");
            merged["addin"]["workerTimeLimitS"] = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["addin"]["workerTimeLimitS"];
            File.WriteAllText(Path.Combine(config, "project.json"), merged.ToString());
            string output = Path.Combine(folder, "out");
            var request = new WorkerRequest
            {
                FormatVersion = 1, RunId = "n1", Command = NewProjectCheck.Command, Document = new WorkerDocument { Path = @"C:\Projects\a.rvt", SessionId = "s1" },
                Inputs = new List<WorkerFile>
                {
                    Input("config", Path.Combine(config, "project.json")), Input("rules", Path.Combine(config, "rules.json")), Input("offsets", Path.Combine(config, "prototype_offsets.json")),
                },
                OutputFolder = output, TimeLimitS = 120,
            };
            Func<string> listed = () => string.Join("|", new[] { work, root, config }.SelectMany(path => Directory.GetFiles(path, "*", SearchOption.AllDirectories)).OrderBy(path => path, StringComparer.Ordinal));
            string before = listed();
            int code = WorkerRunner.Run(request.Write(folder), WorkerReads.Commands(), () => 90000.0, () => 0.0);
            WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(folder, "n1"));
            Assert.True(code == 0, "exit " + code + ": " + answer.Message);
            Assert.Equal(new[] { NewProjectCheck.JsonName, NewProjectCheck.TextName }, Directory.GetFiles(output).Select(path => Path.GetFileName(path)).OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal(6, answer.Counts["lines"]);
            JObject written = JObject.Parse(File.ReadAllText(Path.Combine(output, NewProjectCheck.JsonName)));
            Assert.Equal(NewProjectCheck.Format, (string)written["format"]);
            Assert.StartsWith("New project check, 6 lines", File.ReadAllText(Path.Combine(output, NewProjectCheck.TextName)));
            string after = listed();
            Assert.Equal(before, after);
        }

        private static WorkerFile Input(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }
    }
}
