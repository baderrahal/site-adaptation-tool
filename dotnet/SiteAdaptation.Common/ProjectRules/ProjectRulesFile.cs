using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.ProjectRules
{
    // R660, R663. What rulesSetup, rulesChange and RulesForBuild agree on: the file names, the words of a status and a source, and the fixed rules.
    // The fixed rules sit here and not in a template, so no template and no edit of a rules file can turn one off
    internal static class ProjectRulesFile
    {
        public const string FileName = "project_rules.json";
        public const string LogName = "rules_log.jsonl";
        public const string SetupLine = "rulesSetup";
        public const string NotConfirmed = "not confirmed";
        public const string Confirmed = "confirmed";
        public const string FixedStatus = "fixed";
        public const string Warn = "warn";
        public const string Block = "block";
        public const string BeforeBuild = "beforeBuild";
        public const string Missing = "missing";
        public const string FromRule = "rule";
        public const string Default = "default";
        public const string Computed = "computed";
        public const string PersonEntered = "person entered";
        public const string Past = "past";
        public const string MissingMark = "No value yet. A person enters it, the tool never guesses one.";

        // R663. Q514 is open and no check exists, so the rule says so and holds no value. Writing true here would claim a check ran
        public const string SaudiChecks = "fixed.saudiChecks";
        public const string SaudiLine = "The Saudi checks are not defined yet and none ran, see Q514.";

        private static readonly string[][] FixedRows =
        {
            new[] { SaudiChecks, "The Saudi checks" },
            new[] { "fixed.noGuessedValue", "A missing value is never guessed" },
            new[] { "fixed.everyChangeLogged", "Every rule change is logged" },
        };

        public static bool IsFixed(string id)
        {
            return FixedRows.Any(row => row[0] == id);
        }

        public static IEnumerable<JObject> FixedRules()
        {
            return FixedRows.Select(FixedRule);
        }

        private static JObject FixedRule(string[] row)
        {
            var rule = new JObject { ["id"] = row[0], ["name"] = row[1], ["rule"] = "R663", ["status"] = FixedStatus, ["fixed"] = true };
            if (row[0] == SaudiChecks)
            {
                rule["value"] = JValue.CreateNull();
                rule["source"] = Missing;
                rule["marked"] = SaudiLine;
            }
            else
            {
                rule["value"] = true;
                rule["source"] = FromRule;
            }
            return rule;
        }

        // A date or a number a person typed stays as typed, the plain parse would turn a text that looks like a date into a date and write it back changed
        public static JObject Read(string path)
        {
            using (var reader = new JsonTextReader(new StringReader(WorkFiles.ReadText(path))) { DateParseHandling = DateParseHandling.None })
            {
                return JObject.Load(reader);
            }
        }

        // A person reads the answer of a run, so a broken file gets a plain line and not the words of the JSON reader
        public static JObject ReadOrRefuse(string path, string what, WorkerContext run)
        {
            try
            {
                return Read(path);
            }
            catch (JsonException)
            {
                throw run.Refusal(what + " " + Path.GetFileName(path) + " cannot be read, it is not a whole JSON file, so nothing changed");
            }
        }

        public static JObject Config(WorkerContext run)
        {
            return ProjectModes.Apply(Read(run.Input("config")), run.Request.ProjectMode);
        }

        // R350. A double reads back as the same double on both targets
        public static string Text(JObject content, Formatting formatting)
        {
            using (var text = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(text) { Formatting = formatting })
            {
                content.WriteTo(writer, new ExactDouble.Converter());
                writer.Flush();
                return text.ToString();
            }
        }

        // The log needs a name, so a request with no one behind it changes nothing
        public static string Who(WorkerContext run)
        {
            string who = run.Request.Who?.Trim();
            if (string.IsNullOrEmpty(who))
            {
                throw run.Refusal("the request names no person, every rule change is logged with who made it (R663)");
            }
            return who;
        }

        public static string PastJobMode(JObject config)
        {
            return (config?["projectRules"] as JObject)?["pastJobMode"]?.Type == JTokenType.String ? (string)config["projectRules"]["pastJobMode"] : null;
        }

        // R661. Null when the token is a value, else why not in plain words. A blank or a number JSON cannot write would read as person entered and confirmed with
        // nothing behind it. Inside a list or an object it is the same, so a list holding a blank is no value either
        public static string NoValueReason(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                return "empty";
            }
            switch (value.Type)
            {
                case JTokenType.String:
                    return string.IsNullOrWhiteSpace((string)value) ? "a blank text" : null;
                case JTokenType.Float:
                    double number = Convert.ToDouble(((JValue)value).Value, CultureInfo.InvariantCulture);
                    return double.IsNaN(number) || double.IsInfinity(number) ? "not a number a file can hold" : null;
                case JTokenType.Array:
                    var list = (JArray)value;
                    return list.Count == 0 ? "an empty list" : list.Select(NoValueReason).FirstOrDefault(reason => reason != null);
                case JTokenType.Object:
                    var map = (JObject)value;
                    return map.Count == 0 ? "an empty object" : map.Properties().Select(item => NoValueReason(item.Value)).FirstOrDefault(reason => reason != null);
                default:
                    return null;
            }
        }

        // R661. Exact, as a double holds no whole number past 2 to the 53, so 9007199254740993 over 9007199254740992 is a change. 2 and 2.0 are one value, a list or an object goes by its values
        public static bool SameValue(JToken one, JToken other)
        {
            if (one == null || other == null)
            {
                return one == null && other == null;
            }
            if (one is JArray list && other is JArray others)
            {
                return list.Count == others.Count && Enumerable.Range(0, list.Count).All(at => SameValue(list[at], others[at]));
            }
            if (one is JObject map && other is JObject maps)
            {
                return map.Count == maps.Count && map.Properties().All(item => maps[item.Name] != null && SameValue(item.Value, maps[item.Name]));
            }
            if (IsNumber(one) && IsNumber(other))
            {
                return SameNumber(((JValue)one).Value, ((JValue)other).Value);
            }
            return JToken.DeepEquals(one, other);
        }

        private static bool IsNumber(JToken token)
        {
            return token.Type == JTokenType.Integer || token.Type == JTokenType.Float;
        }

        private static bool IsWhole(object number)
        {
            return !(number is double || number is float || number is decimal);
        }

        private static BigInteger Whole(object number)
        {
            return number is BigInteger big ? big : number is ulong large ? large : Convert.ToInt64(number, CultureInfo.InvariantCulture);
        }

        private static bool SameNumber(object one, object other)
        {
            if (IsWhole(one) && IsWhole(other))
            {
                return Whole(one) == Whole(other);
            }
            if (!IsWhole(one) && !IsWhole(other))
            {
                return Convert.ToDouble(one, CultureInfo.InvariantCulture) == Convert.ToDouble(other, CultureInfo.InvariantCulture);
            }
            double part = Convert.ToDouble(IsWhole(one) ? other : one, CultureInfo.InvariantCulture);
            return !double.IsNaN(part) && !double.IsInfinity(part) && part == Math.Floor(part) && new BigInteger(part) == Whole(IsWhole(one) ? one : other);
        }

        // R661. The runner cannot guard the rules file of rulesChange, as the command changes it, so the shipped templates are guarded here by where they lie
        public static bool UnderTemplates(string path)
        {
            List<string> parts = Path.GetFullPath(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToList();
            return Enumerable.Range(0, Math.Max(parts.Count - 1, 0)).Any(at =>
                string.Equals(parts[at], "rules", StringComparison.OrdinalIgnoreCase) && string.Equals(parts[at + 1], "templates", StringComparison.OrdinalIgnoreCase));
        }

        // A run writes under the work root only and never into a real input folder or the public repo (CLAUDE.md). The rules are the one thing a command
        // writes outside its output folder, so the two checks the runner gives the output folder are made here for the project folder. Null when it may be written
        public static string PlaceRefusal(string folder, JObject config)
        {
            string tree = WorkFiles.RepoAbove(folder);
            if (tree != null)
            {
                return "the project folder " + folder + " lies inside the git work tree " + tree + ", rules of a project never go into git";
            }
            var roots = new List<string>();
            if (config?["inputRoot"]?.Type == JTokenType.String)
            {
                roots.Add((string)config["inputRoot"]);
            }
            if (config?.SelectToken("testHost.listener.inputRoots") is JArray more)
            {
                roots.AddRange(more.Where(root => root.Type == JTokenType.String).Select(root => (string)root));
            }
            string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string root in roots.Where(root => !string.IsNullOrEmpty(root)))
            {
                string top = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(full, top, StringComparison.OrdinalIgnoreCase) || full.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return "the project folder " + folder + " lies inside the input folder " + top + ", a run never writes there";
                }
            }
            return null;
        }

        // The new rules go on their name by a rename, so a reader sees the old file or the new one. The temporary file of a write that failed does not stay among the project's files
        public static void Write(string path, JObject rules, string runId)
        {
            try
            {
                WorkFiles.WriteText(path, runId, Text(rules, Formatting.Indented));
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                WorkFiles.DeleteRun(Path.GetDirectoryName(Path.GetFullPath(path)), runId);
                throw;
            }
        }
    }

    // R661, R663. The log of one project, held open with no other writer while a run changes the rules, so two changes at once cannot lose one.
    // The line goes in before the file changes: a stop between them leaves a line to look into, the other order would leave a change no one logged
    internal sealed class ChangeLog : IDisposable
    {
        private readonly FileStream file;
        private readonly string path;
        private readonly WorkerContext run;

        private ChangeLog(FileStream file, string path, WorkerContext run)
        {
            this.file = file;
            this.path = path;
            this.run = run;
        }

        public static ChangeLog Open(string folder, WorkerContext run)
        {
            string path = Path.Combine(folder, ProjectRulesFile.LogName);
            try
            {
                return new ChangeLog(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read), path, run);
            }
            catch (IOException failure)
            {
                throw run.Refusal("the log of the rules of this project cannot be opened, so nothing changed. Another change may be running: " + failure.Message);
            }
        }

        public static string Now()
        {
            return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        // One change, its line first. Whatever fails after that line adds a second line that names the run, so a reader pairs the two and takes no change from them (R661)
        public void Change(string who, string when, string rule, JToken before, JToken after, Action write)
        {
            Add(who, when, rule, before, after, null);
            try
            {
                write();
            }
            catch (Exception failure)
            {
                try
                {
                    Add(who, when, rule, before, after, failure.Message);
                }
                catch (Exception second) when (second is IOException || second is UnauthorizedAccessException)
                {
                    run.Issues.Add("the change of " + rule + " failed and the log could not say so, its last line reads as a change: " + second.Message);
                }
                throw;
            }
        }

        private void Add(string who, string when, string rule, JToken before, JToken after, string failed)
        {
            var line = new JObject { ["who"] = who, ["when"] = when, ["run"] = run.Request.RunId, ["rule"] = rule, ["old"] = before?.DeepClone() ?? JValue.CreateNull(), ["new"] = after?.DeepClone() ?? JValue.CreateNull() };
            if (failed != null)
            {
                line["failed"] = failed;
            }
            var bytes = new List<byte>();
            // A stop in the middle of an earlier line leaves no line end, the next line must not run on from it
            if (file.Length > 0)
            {
                file.Seek(-1, SeekOrigin.End);
                if (file.ReadByte() != '\n')
                {
                    bytes.Add((byte)'\n');
                }
            }
            bytes.AddRange(new System.Text.UTF8Encoding(false).GetBytes(ProjectRulesFile.Text(line, Formatting.None) + "\n"));
            file.Seek(0, SeekOrigin.End);
            file.Write(bytes.ToArray(), 0, bytes.Count);
            file.Flush(true);
        }

        // A refused run holds no line, and an empty log beside the rules would read as a log that lost its lines
        public void Dispose()
        {
            bool empty = file.Length == 0;
            file.Dispose();
            if (empty)
            {
                File.Delete(path);
            }
        }
    }
}
