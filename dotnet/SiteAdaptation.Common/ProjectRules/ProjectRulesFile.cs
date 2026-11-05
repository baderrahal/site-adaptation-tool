using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

        private static readonly string[][] FixedRows =
        {
            new[] { "fixed.saudiChecks", "The Saudi checks always run" },
            new[] { "fixed.noGuessedValue", "A missing value is never guessed" },
            new[] { "fixed.everyChangeLogged", "Every rule change is logged" },
        };

        public static bool IsFixed(string id)
        {
            return FixedRows.Any(row => row[0] == id);
        }

        public static IEnumerable<JObject> FixedRules()
        {
            return FixedRows.Select(row => new JObject
            {
                ["id"] = row[0], ["name"] = row[1], ["value"] = true, ["source"] = FromRule, ["rule"] = "R663", ["status"] = FixedStatus, ["fixed"] = true,
            });
        }

        // A date or a number a person typed stays as typed, the plain parse would turn a text that looks like a date into a date and write it back changed
        public static JObject Read(string path)
        {
            using (var reader = new JsonTextReader(new StringReader(WorkFiles.ReadText(path))) { DateParseHandling = DateParseHandling.None })
            {
                return JObject.Load(reader);
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

    // R663. The log of one project, held open with no other writer while a run changes the rules, so two changes at once cannot lose one.
    // The line goes in before the file changes: a stop between them leaves a line to look into, the other order would leave a change no one logged
    internal sealed class ChangeLog : IDisposable
    {
        private readonly FileStream file;

        private ChangeLog(FileStream file)
        {
            this.file = file;
        }

        public static ChangeLog Open(string folder, WorkerContext run)
        {
            string path = Path.Combine(folder, ProjectRulesFile.LogName);
            try
            {
                return new ChangeLog(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read));
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

        public void Add(string who, string when, string rule, JToken before, JToken after, string failed = null)
        {
            var line = new JObject { ["who"] = who, ["when"] = when, ["rule"] = rule, ["old"] = before?.DeepClone() ?? JValue.CreateNull(), ["new"] = after?.DeepClone() ?? JValue.CreateNull() };
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

        public void Dispose()
        {
            file.Dispose();
        }
    }
}
