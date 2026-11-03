using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Settings
{
    // R541. One value of the rules profile the team may change, config/rule_values.json names it
    public sealed class RuleValue
    {
        public string Key { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public IReadOnlyList<string> Path { get; set; }
        public double Manual { get; set; }
        public double Project { get; set; }
        public string Unit { get; set; }
        public string Page { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public IReadOnlyList<int> Steps { get; set; }

        public bool Changed => Project != Manual;
    }

    // R541. The values of Site Adaptation Rules. Only a value by its path changes, so rule logic stays as it is. Save writes a copy per project under the work
    // root, never the file it read and never the shipped set
    public static class RuleValues
    {
        public const string CatalogName = "rule_values.json";
        public const string ChangedName = "changed_rules.json";
        private const string RulesFolder = "rules";
        private const string CsvHead = "key,id,name,manual,project,unit,page";

        public static IReadOnlyList<RuleValue> Read(string catalogText, string rulesText)
        {
            JObject rules = JObject.Parse(rulesText);
            return ((JArray)JObject.Parse(catalogText)["rows"]).Select(row =>
            {
                string[] path = row["path"].Select(part => (string)part).ToArray();
                return new RuleValue
                {
                    Key = (string)row["key"],
                    Id = (string)row["id"],
                    Name = (string)row["name"],
                    Path = path,
                    Manual = (double)row["manual"],
                    Project = (double)Node(rules, path),
                    Unit = (string)row["unit"],
                    Page = (string)row["page"],
                    Min = (double)row["min"],
                    Max = (double)row["max"],
                    Steps = row["steps"].Select(step => (int)step).ToList(),
                };
            }).ToList();
        }

        // The number of a typed value, null with the reason in refusal when it is no number or lies outside the limits of the row
        public static double? Parse(RuleValue row, string text, out string refusal)
        {
            refusal = null;
            if (!double.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value))
            {
                refusal = row.Name + ": " + text + " is no number";
                return null;
            }
            if (value < row.Min || value > row.Max)
            {
                refusal = row.Name + ": " + text + " lies outside " + Text(row.Min) + " to " + Text(row.Max) + " " + row.Unit;
                return null;
            }
            return value;
        }

        // The rules text with each row at its project value, nothing else of the file changes
        public static string Apply(string rulesText, IEnumerable<RuleValue> rows)
        {
            JObject rules = JObject.Parse(rulesText);
            foreach (RuleValue row in rows)
            {
                JToken node = Node(rules, row.Path);
                node.Replace(row.Project == Math.Floor(row.Project) && node.Type == JTokenType.Integer ? new JValue((long)row.Project) : new JValue(row.Project));
            }
            return rules.ToString(Formatting.Indented);
        }

        // <work root>\New projects\<folder of this model>\rules\<name of the rules file>, the place of the rules of an Empty project, so one project keeps one copy
        public static string CopyPath(string workRoot, string modelFolder, string rulesFile)
        {
            return System.IO.Path.Combine(workRoot, ShippedSet.ProjectsFolder, modelFolder, RulesFolder, System.IO.Path.GetFileName(rulesFile));
        }

        // Writes the copy and the list of its changed rules for the report, each by a name next to it and a move, so a failed write leaves the file as it was
        public static void WriteCopy(string copyPath, string rulesText, IEnumerable<RuleValue> rows)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(copyPath));
            Replace(copyPath, rulesText);
            Replace(ChangedPath(copyPath), ChangedText(rows));
        }

        public static string ChangedPath(string rulesFile)
        {
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(rulesFile), ChangedName);
        }

        public static string ChangedText(IEnumerable<RuleValue> rows)
        {
            return new JObject
            {
                ["changed"] = new JArray(rows.Where(row => row.Changed).Select(row => new JObject
                {
                    ["key"] = row.Key, ["id"] = row.Id, ["name"] = row.Name, ["manual"] = row.Manual, ["project"] = row.Project, ["unit"] = row.Unit, ["page"] = row.Page,
                })),
            }.ToString(Formatting.Indented);
        }

        public static string ExportText(IEnumerable<RuleValue> rows)
        {
            var text = new StringBuilder(CsvHead).Append("\r\n");
            foreach (RuleValue row in rows)
            {
                text.Append(string.Join(",", new[] { row.Key, row.Id, row.Name, Text(row.Manual), Text(row.Project), row.Unit, row.Page }.Select(Cell))).Append("\r\n");
            }
            return text.ToString();
        }

        // The project values of an exported file by key. A key of no row, or a value its row refuses, stops the import with the reason and changes nothing
        public static IReadOnlyDictionary<string, double> Import(string csvText, IReadOnlyList<RuleValue> rows, out string refusal)
        {
            refusal = null;
            string[] lines = (csvText ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0 || lines[0].Trim().TrimStart('﻿') != CsvHead)
            {
                refusal = "this file is no export of these rules";
                return null;
            }
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string line in lines.Skip(1))
            {
                string[] cells = Cells(line);
                RuleValue row = cells.Length < 5 ? null : rows.FirstOrDefault(item => item.Key == cells[0]);
                if (row == null)
                {
                    refusal = "this line names no rule: " + line;
                    return null;
                }
                double? value = Parse(row, cells[4], out refusal);
                if (value == null)
                {
                    return null;
                }
                values[row.Key] = value.Value;
            }
            return values;
        }

        public static string Text(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static JToken Node(JObject rules, IEnumerable<string> path)
        {
            JToken node = rules;
            foreach (string part in path)
            {
                node = node?[part];
            }
            if (node == null || (node.Type != JTokenType.Integer && node.Type != JTokenType.Float))
            {
                throw new InvalidDataException("the rules file holds no number at " + string.Join(".", path));
            }
            return node;
        }

        private static void Replace(string path, string text)
        {
            string next = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(next, text);
            if (File.Exists(path))
            {
                File.Replace(next, path, null);
            }
            else
            {
                File.Move(next, path);
            }
        }

        private static string Cell(string text)
        {
            return (text ?? "").IndexOfAny(new[] { ',', '"' }) < 0 ? text : "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static string[] Cells(string line)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool inside = false;
            for (int at = 0; at < line.Length; at++)
            {
                char letter = line[at];
                if (letter == '"' && inside && at + 1 < line.Length && line[at + 1] == '"')
                {
                    cell.Append('"');
                    at++;
                }
                else if (letter == '"')
                {
                    inside = !inside;
                }
                else if (letter == ',' && !inside)
                {
                    cells.Add(cell.ToString());
                    cell.Clear();
                }
                else
                {
                    cell.Append(letter);
                }
            }
            cells.Add(cell.ToString());
            return cells.ToArray();
        }
    }
}
