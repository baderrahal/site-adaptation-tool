using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // One shared parameter button 1 binds. Guid is null when the source gives none
    public sealed class ParameterSpec
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public string DataType { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
    }

    // R373, Q155 by its recommended reading. The parameters and their GUIDs come only from the parameter sheet, one sheet with one row
    // per parameter and the columns Name, GUID, Data type and Categories, never made by the tool. Until a sheet is chosen they are the
    // ones config measured on the answer keys, addin.sharedParameters, which the read job of the test host took from the 7 keys, then
    // those of addin.fixedParameters (Q156)
    public sealed class ParameterSheet
    {
        public static readonly string[] Columns = { "Name", "GUID", "Data type", "Categories" };
        public const string TextType = "TEXT";

        // The sheet file name, null for the parameters of config
        public string Sheet { get; private set; }

        public List<ParameterSpec> Parameters { get; private set; } = new List<ParameterSpec>();

        public bool Chosen
        {
            get { return Sheet != null; }
        }

        // The text after "From the parameter sheet: "
        public string Source
        {
            get
            {
                string count = Parameters.Count.ToString(CultureInfo.InvariantCulture);
                return Chosen ? Sheet + ", " + count + " parameters" : "not chosen, " + count + " parameters of the answer keys";
            }
        }

        // addin.sharedParameters.parameters of config: name, guid and the categories the keys bind it on, all text parameters as the
        // keys hold them (03a 3.1 Outputs)
        public static ParameterSheet FromConfig(JObject config)
        {
            JArray items = ((config["addin"] as JObject)?["sharedParameters"] as JObject)?["parameters"] as JArray;
            if (items == null || items.Count == 0)
            {
                throw new InvalidDataException("config holds no addin.sharedParameters.parameters, the parameters of button 1 until a parameter sheet is chosen (R353, Q155)");
            }
            var sheet = new ParameterSheet();
            sheet.Parameters.AddRange(items.OfType<JObject>().Select(FromItem));
            Refuse(sheet.Parameters, "addin.sharedParameters");
            // Q156. addin.fixedParameters holds the GUIDs Bader fixed, such as Plot, which no key binds as a shared parameter and no run writes
            JArray fixedItems = ((config["addin"] as JObject)?["fixedParameters"] as JObject)?["parameters"] as JArray;
            if (fixedItems == null)
            {
                return sheet;
            }
            List<ParameterSpec> fixedParameters = fixedItems.OfType<JObject>().Select(FromItem).ToList();
            Refuse(fixedParameters, "addin.fixedParameters");
            foreach (ParameterSpec item in fixedParameters)
            {
                if (sheet.Parameters.Any(measured => string.Equals(measured.Name, item.Name, StringComparison.Ordinal)))
                {
                    throw new InvalidDataException("config names " + item.Name + " in both addin.sharedParameters and addin.fixedParameters, one parameter per name (Q156)");
                }
                sheet.Parameters.Add(item);
            }
            return sheet;
        }

        private static ParameterSpec FromItem(JObject item)
        {
            return new ParameterSpec
            {
                Name = Text(item["name"]),
                Guid = Text(item["guid"]),
                DataType = TextType,
                Categories = (item["on"] as JArray ?? new JArray()).Select(Text).Where(name => name != null).ToList(),
            };
        }

        // The rows of the sheet as a sheet reader gives them, the first holding the four column names in any order and case. A row of
        // no values is left out. Categories are one cell, the names parted by commas
        public static ParameterSheet FromRows(string sheetName, IReadOnlyList<object[]> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                throw new InvalidDataException("the parameter sheet " + sheetName + " holds no row (Q155)");
            }
            List<string> header = rows[0].Select(cell => (Cell(cell) ?? "").ToLowerInvariant()).ToList();
            int[] at = Columns.Select(column => header.IndexOf(column.ToLowerInvariant())).ToArray();
            List<string> missing = Columns.Where((column, index) => at[index] < 0).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidDataException("the parameter sheet " + sheetName + " has no column " + string.Join(", ", missing) + " in its first row (Q155)");
            }
            var sheet = new ParameterSheet { Sheet = sheetName };
            foreach (object[] row in rows.Skip(1))
            {
                string[] cells = at.Select(index => index < row.Length ? Cell(row[index]) : null).ToArray();
                if (cells.All(cell => cell == null))
                {
                    continue;
                }
                sheet.Parameters.Add(new ParameterSpec
                {
                    Name = cells[0],
                    Guid = cells[1],
                    DataType = cells[2] == null ? null : cells[2].ToUpperInvariant(),
                    Categories = (cells[3] ?? "").Split(',').Select(name => name.Trim()).Where(name => name.Length > 0).ToList(),
                });
            }
            Refuse(sheet.Parameters, "the parameter sheet " + sheetName);
            return sheet;
        }

        // A GUID of no GUID shape is refused here, a missing one is a stop of the planner, which names it
        private static void Refuse(List<ParameterSpec> parameters, string source)
        {
            if (parameters.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            {
                throw new InvalidDataException(source + " holds a parameter with no name (Q155)");
            }
            List<string> twice = parameters.GroupBy(item => item.Name, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
            if (twice.Count > 0)
            {
                throw new InvalidDataException(source + " names " + string.Join(", ", twice) + " twice, one parameter per name (Q155)");
            }
            foreach (ParameterSpec item in parameters.Where(item => item.Guid != null))
            {
                Guid parsed;
                if (!System.Guid.TryParseExact(item.Guid, "D", out parsed))
                {
                    throw new InvalidDataException(source + " gives " + item.Name + " the GUID " + item.Guid + ", which is no GUID (Q155)");
                }
                item.Guid = parsed.ToString("D");
            }
        }

        private static string Text(JToken value)
        {
            return value != null && value.Type == JTokenType.String && ((string)value).Trim().Length > 0 ? ((string)value).Trim() : null;
        }

        private static string Cell(object value)
        {
            string text = value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
