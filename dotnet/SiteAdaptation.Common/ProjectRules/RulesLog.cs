using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.ProjectRules
{
    // R661, R662. The log read back, so a build can tell a change the tool made from a hand edit of the rules file. A line with failed cancels the line before it
    // of the same run and rule, as that change never reached the file. A line a stop cut short is passed over
    internal sealed class RulesLog
    {
        private readonly Dictionary<string, JObject> last = new Dictionary<string, JObject>();

        private RulesLog(bool gone)
        {
            Gone = gone;
        }

        // True when no log can be read beside the rules, so nothing in the rules file can be held against it
        public bool Gone { get; }

        public JObject LastOf(string rule)
        {
            JObject line;
            return last.TryGetValue(rule, out line) ? line : null;
        }

        // The setting the last logged change gave, null when none did, which reads as the warn rulesSetup writes
        public string Setting()
        {
            JToken setting = LastOf(ProjectRulesFile.BeforeBuild)?["new"];
            return setting?.Type == JTokenType.String ? (string)setting : null;
        }

        public static RulesLog Beside(string rulesPath)
        {
            string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rulesPath)), ProjectRulesFile.LogName);
            List<string> lines;
            try
            {
                if (!File.Exists(path))
                {
                    return new RulesLog(true);
                }
                // Read while a change may hold the log for writing, which a plain read of the whole file refuses
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(file, new UTF8Encoding(false)))
                {
                    lines = reader.ReadToEnd().Split('\n').ToList();
                }
            }
            catch (IOException)
            {
                return new RulesLog(true);
            }
            catch (System.UnauthorizedAccessException)
            {
                return new RulesLog(true);
            }
            var log = new RulesLog(false);
            foreach (JObject line in Standing(lines))
            {
                log.last[(string)line["rule"]] = line;
            }
            return log;
        }

        private static List<JObject> Standing(IEnumerable<string> lines)
        {
            var standing = new List<JObject>();
            foreach (JObject line in lines.Select(ReadLine).Where(line => line != null))
            {
                if (line["failed"] == null)
                {
                    standing.Add(line);
                    continue;
                }
                JObject cancelled = standing.LastOrDefault(earlier => (string)earlier["run"] == (string)line["run"] && (string)earlier["rule"] == (string)line["rule"]);
                if (cancelled != null)
                {
                    standing.Remove(cancelled);
                }
            }
            return standing;
        }

        // Null for a blank line, a line cut short or a line that names no rule
        private static JObject ReadLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            try
            {
                using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
                {
                    JObject line = JObject.Load(reader);
                    return line["rule"]?.Type == JTokenType.String && (line["run"] == null || line["run"].Type == JTokenType.String) ? line : null;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
