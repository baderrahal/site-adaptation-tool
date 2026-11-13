using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R412. One line of the report: the input, its status (ok, missing, does not fit), a plain sentence and the note why the input is held
    // to a name only the ROSHN Makkah job has today, null when it is not
    public sealed class CheckLine
    {
        public string Input { get; set; }

        public string Status { get; set; }

        public string Text { get; set; }

        public string RoshnNote { get; set; }

        public bool RoshnOnly => RoshnNote != null;
    }

    // R412. What the worker reads of a workbook for the check, given by the caller since OpenXml loads only in the worker. SheetNames gives
    // the sheet names or null when the file is no workbook. Rows gives the first count rows of a sheet as the text of each cell, a cell that
    // is no text as an empty text, every row when count is below 0. R847: the PDF reader went with the line of the location plan PDF of the
    // data book
    public sealed class NewProjectReaders
    {
        public Func<string, IReadOnlyList<string>> SheetNames { get; set; }

        public Func<string, string, int, IReadOnlyList<IReadOnlyList<string>>> Rows { get; set; }
    }

    public sealed class NewProjectReport
    {
        public List<CheckLine> Lines { get; set; } = new List<CheckLine>();

        public List<JObject> Inputs { get; set; } = new List<JObject>();

        // The paths of the inputs opened, the output folder is held apart from them
        public List<string> Paths { get; set; } = new List<string>();

        public string InputRoot { get; set; }

        public int Count(string status)
        {
            return Lines.Count(each => each.Status == status);
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["format"] = NewProjectCheck.Format,
                ["formatVersion"] = NewProjectCheck.FormatVersion,
                ["lines"] = new JArray(Lines.Select(each => new JObject
                {
                    ["input"] = each.Input, ["status"] = each.Status, ["text"] = each.Text, ["roshnOnly"] = each.RoshnOnly,
                    ["roshnNote"] = each.RoshnNote == null ? JValue.CreateNull() : new JValue(each.RoshnNote),
                })),
                ["counts"] = new JObject
                {
                    ["ok"] = Count(NewProjectCheck.Ok), ["missing"] = Count(NewProjectCheck.Missing), ["doesNotFit"] = Count(NewProjectCheck.DoesNotFit),
                    ["roshnOnly"] = Lines.Count(each => each.RoshnOnly),
                },
                ["inputs"] = new JArray(Inputs),
            };
        }

        // The text form: a head line, one line per input, the count of the inputs opened
        public string Text()
        {
            var lines = new List<string>
            {
                "New project check, " + Lines.Count + " lines: " + Count(NewProjectCheck.Ok) + " ok, " + Count(NewProjectCheck.Missing) + " missing, "
                    + Count(NewProjectCheck.DoesNotFit) + " do not fit, " + Lines.Count(each => each.RoshnOnly) + " ROSHN only",
            };
            lines.AddRange(Lines.Select(each => each.Input + ": " + each.Status + ". " + each.Text + (each.RoshnOnly ? ". ROSHN only: " + each.RoshnNote : "")));
            lines.Add("Inputs opened: " + Inputs.Count + ", none changed");
            return string.Join("\n", lines) + "\n";
        }
    }

    // R412. The read only check of the inputs of a new project, python/new_project_check.py step for step. It reads the inputs the config
    // names and says in one plain line each what is missing or does not fit. Every input it opens is marked before its first read and again
    // at the end, and a change fails the run (D19)
    public static class NewProjectCheck
    {
        public const string Command = "newProjectCheck";
        public const string Format = "site-adaptation-new-project-check";
        public const int FormatVersion = 1;
        public const string Ok = "ok";
        public const string Missing = "missing";
        public const string DoesNotFit = "does not fit";
        public const string PositionFromCad = "not asked, this project takes its position from CAD";
        public const string PositionTyped = "not asked, this project takes its position from typed values";
        public const string ConfigRole = "config";
        public const string RulesRole = "rules";
        public const string OffsetsRole = "offsets";
        public const string JsonName = "new_project_check.json";
        public const string TextName = "new_project_check.txt";
        public const string InputRootVariable = "SITE_ADAPTATION_INPUT";

        private const string PositionFormat = "site-adaptation-position";
        private const string ScheduleMarker = "plot no.";
        private static readonly string[] PositionParts = { "eastM", "northM", "elevationM", "angleDeg" };
        private static readonly string[] PositionPoints = { "surveyPoint", "projectBasePoint" };
        private static readonly string[] HandTables = { "hand villa", "hand cluster", "hand alone" };
        private static readonly Regex ParameterPrefix = new Regex("^[A-Z]{2,5}_", RegexOptions.CultureInvariant);
        private static readonly Regex Guid = new Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\\z", RegexOptions.CultureInvariant);

        // The files one check opened, each marked before its first read
        private sealed class Run
        {
            public readonly string WorkRoot;
            public readonly string InputRoot;
            public readonly NewProjectReaders Readers;
            public readonly Dictionary<string, KeyValuePair<string, InputMark>> Marks = new Dictionary<string, KeyValuePair<string, InputMark>>();
            public readonly List<string> Order = new List<string>();
            private readonly Dictionary<string, IReadOnlyList<string>> names = new Dictionary<string, IReadOnlyList<string>>();
            private readonly HashSet<string> tried = new HashSet<string>();

            public Run(string workRoot, string inputRoot, NewProjectReaders readers)
            {
                WorkRoot = workRoot;
                InputRoot = inputRoot;
                Readers = readers;
            }

            public static string Key(string path)
            {
                return Path.GetFullPath(path).ToLowerInvariant();
            }

            public void Opened(string path)
            {
                string key = Key(path);
                if (!Marks.ContainsKey(key))
                {
                    Marks[key] = new KeyValuePair<string, InputMark>(path, InputGuard.Mark(path));
                    Order.Add(key);
                }
            }

            public JObject JsonObject(string path)
            {
                Opened(path);
                try
                {
                    return JToken.Parse(File.ReadAllText(path).TrimStart('﻿')) as JObject;
                }
                catch (JsonException)
                {
                    return null;
                }
            }

            // The sheet names of a workbook, null when it cannot be read as one
            public IReadOnlyList<string> Book(string path)
            {
                string key = Key(path);
                if (tried.Add(key))
                {
                    Opened(path);
                    names[key] = Readers.SheetNames(path);
                }
                return names[key];
            }
        }

        private static CheckLine Line(string name, string status, string text, string roshnNote = null)
        {
            return new CheckLine { Input = name, Status = status, Text = text, RoshnNote = roshnNote };
        }

        private static bool IsNumber(JToken token)
        {
            return token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float);
        }

        private static bool IsPoint(JToken token)
        {
            JArray list = token as JArray;
            return list != null && list.Count == 3 && list.All(IsNumber);
        }

        private static bool IsText(JToken token)
        {
            return token != null && token.Type == JTokenType.String;
        }

        // The value at a dotted path of nested objects, null when a step is not there or holds null
        private static JToken Get(JToken node, string dotted)
        {
            foreach (string part in dotted.Split('.'))
            {
                JObject item = node as JObject;
                if (item == null || item[part] == null)
                {
                    return null;
                }
                node = item[part];
                if (node.Type == JTokenType.Null)
                {
                    return null;
                }
            }
            return node;
        }

        // A text of config with the work root mark taken for the root, as with_work_root does in Python
        private static string Str(JToken settings, string dotted, string workRoot)
        {
            JToken value = Get(settings, dotted);
            return IsText(value) ? WorkRoot.Resolve((string)value, () => workRoot) : null;
        }

        private static List<string> Texts(JToken token)
        {
            JArray list = token as JArray;
            return list == null ? new List<string>() : list.Where(IsText).Select(item => (string)item).ToList();
        }

        // Python truth of a JSON value, a null, an empty text, a zero, false, an empty list or object are false
        private static bool Truthy(JToken token)
        {
            if (token == null)
            {
                return false;
            }
            switch (token.Type)
            {
                case JTokenType.Null:
                    return false;
                case JTokenType.String:
                    return ((string)token).Length > 0;
                case JTokenType.Boolean:
                    return (bool)token;
                case JTokenType.Integer:
                case JTokenType.Float:
                    return (double)token != 0;
                case JTokenType.Array:
                case JTokenType.Object:
                    return token.HasValues;
                default:
                    return true;
            }
        }

        // R511. The source the project holds, else coordinates.source of config, never the default of a new project, so a past job with its position file still gets its line
        private static CheckLine PositionLine(JObject settings, Run run, string coordinateSource, string positionFile)
        {
            const string name = "position file";
            string source = coordinateSource ?? SetUp.SiteSources.OfConfig(settings);
            if (source == SetUp.SiteSources.Cad)
            {
                return Line(name, Ok, PositionFromCad);
            }
            if (source == SetUp.SiteSources.Typed)
            {
                return Line(name, Ok, PositionTyped);
            }
            string file = positionFile ?? Str(settings, "addin.position.file", run.WorkRoot);
            if (file == null || !file.ToLowerInvariant().EndsWith(".json", StringComparison.Ordinal))
            {
                return Line(name, DoesNotFit, "config names no .json file in addin.position.file");
            }
            string shown = Path.GetFileName(file);
            if (!File.Exists(file))
            {
                return Line(name, Missing, shown + " is not there");
            }
            JObject found = run.JsonObject(file);
            if (found == null)
            {
                return Line(name, DoesNotFit, shown + " cannot be read as a JSON object");
            }
            var faults = new List<string>();
            if ((string)(IsText(found["format"]) ? found["format"] : null) != PositionFormat)
            {
                faults.Add("format");
            }
            if (!IsNumber(found["formatVersion"]) || (double)found["formatVersion"] != 1)
            {
                faults.Add("formatVersion");
            }
            JObject at = found["projectPosition"] as JObject;
            foreach (string part in PositionParts)
            {
                if (at == null || !IsNumber(at[part]))
                {
                    faults.Add("projectPosition." + part);
                }
            }
            foreach (string point in PositionPoints)
            {
                foreach (string frame in new[] { "internalM", "sharedM" })
                {
                    JObject value = found[point] as JObject;
                    if (value == null || !IsPoint(value[frame]))
                    {
                        faults.Add(point + "." + frame);
                    }
                }
            }
            JArray levels = found["levels"] as JArray;
            if (levels == null || levels.Count == 0)
            {
                faults.Add("levels");
            }
            else
            {
                for (int index = 0; index < levels.Count; index++)
                {
                    JObject level = levels[index] as JObject;
                    if (level == null || !IsText(level["name"]))
                    {
                        faults.Add("levels[" + index + "].name");
                    }
                    if (level == null || !IsNumber(level["elevationM"]))
                    {
                        faults.Add("levels[" + index + "].elevationM");
                    }
                }
            }
            if (!IsText(found["levelBase"]))
            {
                faults.Add("levelBase");
            }
            if (faults.Count > 0)
            {
                return Line(name, DoesNotFit, shown + " lacks or has wrong: " + string.Join(", ", faults));
            }
            return Line(name, Ok, shown + " holds the project position, the survey point, the project base point and " + levels.Count + " levels");
        }

        private static JObject DefaultProfile(JObject rules)
        {
            JObject profiles = rules["levelProfiles"] as JObject;
            JToken name = profiles?["default"];
            return IsText(name) ? profiles[(string)name] as JObject ?? new JObject() : new JObject();
        }

        // floors.plotParameter, pastJob.parameters, majority.parameterLevels, then each parameter entry of the keyRules of the default level
        // profile, each once in that order
        private static List<string> ParameterNames(JObject settings, JObject rules)
        {
            var found = new List<string>();
            JToken plot = Get(settings, "floors.plotParameter");
            if (IsText(plot))
            {
                found.Add((string)plot);
            }
            found.AddRange(Texts(Get(settings, "pastJob.parameters")));
            found.AddRange(Texts(Get(settings, "majority.parameterLevels")));
            JObject keyRules = DefaultProfile(rules)["keyRules"] as JObject;
            if (keyRules != null)
            {
                found.AddRange(keyRules.Properties().Where(item => item.Name.StartsWith("parameter ", StringComparison.Ordinal)).Select(item => item.Name.Substring("parameter ".Length)));
            }
            return found.Distinct().ToList();
        }

        // R461. The parameter sheet addin.parameterSheet names, its rows of Keep Yes, holds a GUID for every parameter name the tool uses
        private static CheckLine ParameterLine(string configPath, JObject settings, JObject rules, Run run)
        {
            const string name = "parameter sheet";
            JToken named = Get(settings, "addin.parameterSheet");
            if (!IsText(named))
            {
                return Line(name, Missing, "config names no addin.parameterSheet");
            }
            string path = SetUp.ParameterSheet.PathOf(configPath, settings);
            if (!File.Exists(path))
            {
                return Line(name, Missing, "the parameter sheet " + Path.GetFileName(path) + " is not there");
            }
            run.Opened(path);
            SetUp.ParameterSheet sheet;
            try
            {
                sheet = SetUp.ParameterSheet.FromFile(path);
            }
            catch (InvalidDataException error)
            {
                return Line(name, DoesNotFit, error.Message);
            }
            string file = Path.GetFileName(path);
            var guids = sheet.Parameters.ToDictionary(item => item.Name, item => item.Guid, StringComparer.Ordinal);
            List<string> names = ParameterNames(settings, rules);
            var faults = new List<string>();
            foreach (string each in names)
            {
                if (!guids.ContainsKey(each))
                {
                    faults.Add(each + " (not in the sheet)");
                }
                else if (guids[each] == null || !Guid.IsMatch(guids[each]))
                {
                    faults.Add(each + " (no GUID)");
                }
            }
            string prefix = names.Select(each => ParameterPrefix.Match(each)).Where(match => match.Success).Select(match => match.Value).FirstOrDefault();
            string note = prefix != null ? "the names are held to the prefix " + prefix + " of the ROSHN Makkah parameters" : null;
            if (faults.Count > 0)
            {
                return Line(name, DoesNotFit, faults.Count + " of " + names.Count + " parameter names have no GUID in " + file + ": " + string.Join(", ", faults), note);
            }
            return Line(name, Ok, "all " + names.Count + " parameter names have a GUID in " + file, note);
        }

        private static List<string> UnitCodes(JObject rules)
        {
            JObject typologies = rules["typologies"] as JObject;
            return typologies == null ? new List<string>() : typologies.Properties().Select(item => item.Name).ToList();
        }

        private static CheckLine OffsetsLine(JObject settings, JObject rules, string offsetsPath, Run run)
        {
            const string name = "villa offsets";
            JToken hood = Get(settings, "pastJob.neighbourhood");
            string note = "measured on the ROSHN Makkah past job" + (IsText(hood) ? " " + (string)hood : "");
            string shown = Path.GetFileName(offsetsPath);
            if (!File.Exists(offsetsPath))
            {
                return Line(name, Missing, shown + " is not there");
            }
            JObject found = run.JsonObject(offsetsPath);
            if (found == null)
            {
                return Line(name, DoesNotFit, shown + " cannot be read as a JSON object");
            }
            JObject units = Get(found, "plotFrame.units") as JObject;
            if (units == null)
            {
                return Line(name, DoesNotFit, shown + " holds no plotFrame.units", note);
            }
            List<string> codes = UnitCodes(rules);
            List<string> lacking = codes.Where(code => units[code] == null).ToList();
            if (lacking.Count > 0)
            {
                return Line(name, DoesNotFit, "plotFrame.units of " + shown + " holds no offset for the unit codes: " + string.Join(", ", lacking), note);
            }
            return Line(name, Ok, "plotFrame.units of " + shown + " holds an offset for all " + codes.Count + " unit codes of typologies", note);
        }

        // BuildSteps.PrototypeFolder: the input root with pastJob.upgradedPrototypeFolder, else pastJob.prototypeFolder, null when one is not named
        private static string PrototypeFolder(JObject settings, string inputRoot)
        {
            JObject job = settings["pastJob"] as JObject ?? new JObject();
            JToken named = Truthy(job["upgradedPrototypeFolder"]) ? job["upgradedPrototypeFolder"] : job["prototypeFolder"];
            if (string.IsNullOrEmpty(inputRoot) || !IsText(named) || ((string)named).Length == 0)
            {
                return null;
            }
            return Path.Combine(inputRoot, (string)named);
        }

        private static Dictionary<string, string> PrototypeFiles(string folder, JObject rules)
        {
            return Hands.PrototypeFiles(Directory.GetFiles(folder).Select(file => Path.GetFileName(file)).ToList(), rules);
        }

        private static CheckLine HandsLine(JObject rules, string folder)
        {
            const string name = "hand tables";
            const string note = "the tables are keyed by the KEO prototype file names of the ROSHN Makkah job";
            JObject keyRules = DefaultProfile(rules)["keyRules"] as JObject;
            List<JObject> tables = HandTables.Select(table => keyRules?[table] as JObject).Where(table => table != null && table["byFile"] is JObject).ToList();
            if (tables.Count == 0)
            {
                return Line(name, DoesNotFit, "the rules profile holds no byFile in the hand tables of its keyRules", note);
            }
            if (folder == null || !Directory.Exists(folder))
            {
                return Line(name, Missing, "the hand tables are named by prototype file and the prototype folder is not there", note);
            }
            var held = new HashSet<string>(tables.SelectMany(table => ((JObject)table["byFile"]).Properties().Select(item => item.Name)));
            Dictionary<string, string> files = PrototypeFiles(folder, rules);
            List<string> codes = UnitCodes(rules);
            List<string> lacking = codes.Where(code => !files.ContainsKey(code) || !held.Contains(files[code])).ToList();
            if (lacking.Count > 0)
            {
                return Line(name, DoesNotFit, "the hand tables hold no entry for the prototype files of the unit codes: " + string.Join(", ", lacking), note);
            }
            return Line(name, Ok, "the hand tables hold an entry for the prototype file of all " + codes.Count + " unit codes", note);
        }

        private static CheckLine PrototypeLine(JObject rules, string folder)
        {
            const string name = "prototype files";
            const string note = "files are found by the -ARC-<code>- part of the KEO file names";
            if (folder == null || !Directory.Exists(folder))
            {
                return Line(name, Missing, "the prototype folder " + (folder == null ? "named by pastJob" : Path.GetFileName(folder)) + " is not there", note);
            }
            Dictionary<string, string> files = PrototypeFiles(folder, rules);
            List<string> codes = UnitCodes(rules);
            List<string> lacking = codes.Where(code => !files.ContainsKey(code)).ToList();
            string shown = Path.GetFileName(folder);
            if (lacking.Count > 0)
            {
                return Line(name, DoesNotFit, "the prototype folder " + shown + " holds no single file for the unit codes: " + string.Join(", ", lacking), note);
            }
            return Line(name, Ok, "the prototype folder " + shown + " holds a file for all " + codes.Count + " unit codes", note);
        }

        private static List<CheckLine> ScheduleLines(JObject settings, Run run, string inputRoot)
        {
            JObject hoods = Get(settings, "connect.neighbourhoods") as JObject;
            string folder = Str(settings, "addin.readData.scheduleFolder", run.WorkRoot), sheet = Str(settings, "connect.scheduleSheet", run.WorkRoot);
            var entries = new List<KeyValuePair<string, string>>();
            if (hoods != null)
            {
                foreach (JProperty hood in hoods.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    JObject value = hood.Value as JObject;
                    if (value != null && IsText(value["schedule"]))
                    {
                        entries.Add(new KeyValuePair<string, string>(hood.Name, (string)value["schedule"]));
                    }
                }
            }
            var found = new List<CheckLine>();
            if (entries.Count == 0)
            {
                return found;
            }
            if (folder == null || sheet == null || string.IsNullOrEmpty(inputRoot))
            {
                found.Add(Line("Schedules", DoesNotFit, "config names no addin.readData.scheduleFolder, connect.scheduleSheet or input root"));
                return found;
            }
            string note = "held to the sheet " + sheet + " and a header cell with Plot No.";
            foreach (KeyValuePair<string, string> entry in entries)
            {
                string name = "Schedule " + entry.Key;
                string path = Path.Combine(inputRoot, folder, entry.Value);
                string shown = Path.GetFileName(path);
                if (!File.Exists(path))
                {
                    found.Add(Line(name, Missing, shown + " is not there", note));
                    continue;
                }
                IReadOnlyList<string> names = run.Book(path);
                if (names == null)
                {
                    found.Add(Line(name, DoesNotFit, shown + " cannot be read as a workbook", note));
                    continue;
                }
                string wanted = SheetRows.PyStrip(sheet).ToLowerInvariant();
                string matching = names.FirstOrDefault(each => SheetRows.PyStrip(each).ToLowerInvariant() == wanted);
                if (matching == null)
                {
                    found.Add(Line(name, DoesNotFit, shown + " has no sheet " + sheet, note));
                    continue;
                }
                bool marked = run.Readers.Rows(path, matching, -1).Any(row => row.Any(cell => cell.ToLowerInvariant().Contains(ScheduleMarker)));
                found.Add(marked ? Line(name, Ok, shown + " sheet " + sheet + " holds the Plot No. header row", note)
                    : Line(name, DoesNotFit, shown + " sheet " + sheet + " has no cell holding Plot No. in any row", note));
            }
            return found;
        }

        // The report of the check. The inputs are marked before their first read and again at the end, a change throws
        public static NewProjectReport Check(string configPath, string rulesPath, string offsetsPath, string workRoot, string inputRoot, NewProjectReaders readers, string coordinateSource = null, string positionFile = null)
        {
            var settings = JObject.Parse(File.ReadAllText(configPath).TrimStart('﻿'));
            JObject rules = null;
            if (File.Exists(rulesPath))
            {
                try
                {
                    rules = JToken.Parse(File.ReadAllText(rulesPath).TrimStart('﻿')) as JObject;
                }
                catch (JsonException)
                {
                }
            }
            if (rules == null)
            {
                throw new InvalidDataException("the rules profile " + Path.GetFileName(rulesPath) + " is not there or is no JSON object");
            }
            if (!(rules["typologies"] is JObject))
            {
                throw new InvalidDataException("the rules profile " + Path.GetFileName(rulesPath) + " holds no typologies");
            }
            string environment = Environment.GetEnvironmentVariable(InputRootVariable);
            string root = !string.IsNullOrEmpty(inputRoot) ? inputRoot : !string.IsNullOrEmpty(environment) ? environment : Str(settings, "inputRoot", workRoot);
            var run = new Run(workRoot, root, readers);
            run.Opened(configPath);
            run.Opened(rulesPath);
            string folder = PrototypeFolder(settings, root);
            var lines = new List<CheckLine> { PositionLine(settings, run, coordinateSource, positionFile), ParameterLine(configPath, settings, rules, run), OffsetsLine(settings, rules, offsetsPath, run), HandsLine(rules, folder), PrototypeLine(rules, folder) };
            lines.AddRange(ScheduleLines(settings, run, root));
            var changed = new List<string>();
            var inputs = new List<JObject>();
            foreach (string key in run.Order)
            {
                KeyValuePair<string, InputMark> held = run.Marks[key];
                string differs = InputGuard.Change(held.Value, InputGuard.Mark(held.Key));
                if (differs != null)
                {
                    changed.Add(held.Key + " its " + differs);
                }
                inputs.Add(new JObject
                {
                    ["name"] = held.Value.Name, ["size"] = held.Value.Size, ["writeTimeUtc"] = held.Value.WriteTimeUtc, ["sha256"] = held.Value.Sha256, ["unchanged"] = differs == null,
                });
            }
            if (changed.Count > 0)
            {
                throw new InvalidDataException("an input changed while the check read it (D19): " + string.Join(", and ", changed));
            }
            return new NewProjectReport
            {
                Lines = lines,
                Inputs = inputs.OrderBy(item => (string)item["name"], StringComparer.Ordinal).ThenBy(item => (string)item["sha256"], StringComparer.Ordinal).ToList(),
                Paths = run.Order.Select(key => run.Marks[key].Key).ToList(),
                InputRoot = root,
            };
        }

        // The output folder is no folder of an input and none under the input root, so nothing is written beside an input
        public static void KeepApart(string outFolder, IEnumerable<string> inputPaths, string inputRoot)
        {
            string output = Run.Key(outFolder).TrimEnd(Path.DirectorySeparatorChar);
            foreach (string path in inputPaths)
            {
                if (Run.Key(Path.GetDirectoryName(Path.GetFullPath(path))).TrimEnd(Path.DirectorySeparatorChar) == output)
                {
                    throw new InvalidDataException("the output folder is the folder of the input " + Path.GetFileName(path) + ", a check writes nothing beside an input");
                }
            }
            if (!string.IsNullOrEmpty(inputRoot))
            {
                string top = Run.Key(inputRoot).TrimEnd(Path.DirectorySeparatorChar);
                if (output == top || output.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("the output folder is under the input root, a check writes nothing there (D19)");
                }
            }
        }
    }
}
