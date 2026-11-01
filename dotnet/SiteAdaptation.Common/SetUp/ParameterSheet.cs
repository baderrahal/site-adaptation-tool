using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // One shared parameter of the parameter sheet. Guid is null when the sheet gives none. FilledBy is the number of the button that
    // writes it, null when none does
    public sealed class ParameterSpec
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public string DataType { get; set; }
        public string Group { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public int? FilledBy { get; set; }
    }

    // R461, R373, Q155. The shared parameters of button 1 come from one source, the parameter sheet: config/shared_parameters.xlsx
    // that addin.parameterSheet of config names, or the sheet chosen in Set up site, sheet Parameters, one row per parameter with the
    // columns Parameter, GUID, Data type, Group, Category, Filled by, Sample value and Keep. A row of Keep No is left out, so a new
    // project edits only this sheet. The names and GUIDs are those the past links of the 7 keys hold (R460)
    public sealed class ParameterSheet
    {
        public static readonly string[] Columns = { "Parameter", "GUID", "Data type", "Group", "Category", "Filled by", "Sample value", "Keep" };
        public const string SheetName = "Parameters";
        public const string TextType = "TEXT";
        public const string FilledByNone = "none";

        // R465. The buttons that write a parameter of the sheet, Floors the floor parameter of floors.plotParameter and Place villas
        // every link parameter it owns
        public const int FloorsButton = 5;
        public const int PlaceVillasButton = 7;

        // The sheet file name
        public string Sheet { get; private set; }

        public List<ParameterSpec> Parameters { get; private set; } = new List<ParameterSpec>();

        // The names of the rows of Keep No
        public List<string> LeftOut { get; private set; } = new List<string>();

        // The text after "From the parameter sheet: "
        public string Source
        {
            get
            {
                string text = Sheet + ", " + Parameters.Count.ToString(CultureInfo.InvariantCulture) + " parameters";
                return LeftOut.Count == 0 ? text : text + ", " + LeftOut.Count.ToString(CultureInfo.InvariantCulture) + " left out";
            }
        }

        // R465. The GUID of each parameter a button fills, by name
        public Dictionary<string, string> GuidsFilledBy(int button)
        {
            return Parameters.Where(item => item.Guid != null && item.FilledBy == button).ToDictionary(item => item.Name, item => item.Guid, StringComparer.Ordinal);
        }

        // R461. The path of the parameter sheet of config, addin.parameterSheet, relative to the repo root, the folder above the one
        // config is in, as every path of config
        public static string PathOf(string configPath, JObject config)
        {
            JToken named = (config?["addin"] as JObject)?["parameterSheet"];
            if (named == null || named.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)named))
            {
                throw new InvalidDataException("config names no addin.parameterSheet, the parameter sheet of button 1 (R461)");
            }
            string path = (string)named;
            return Path.IsPathRooted(path) ? path : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(configPath))), path);
        }

        // R461. The parameter sheet of config, read by ParameterBook (R462)
        public static ParameterSheet FromConfig(string configPath, JObject config)
        {
            return FromFile(PathOf(configPath, config));
        }

        public static ParameterSheet FromFile(string path)
        {
            return FromRows(Path.GetFileName(path), ParameterBook.Read(path, SheetName));
        }

        // The rows of the sheet as a sheet reader gives them, the first holding the column names in any order and case. A row of no
        // values is left out. Category is one cell, the names parted by commas. Sample value is for the reader of the sheet and is
        // never read
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
                throw new InvalidDataException("the parameter sheet " + sheetName + " has no column " + string.Join(", ", missing) + " in its first row (R461)");
            }
            var sheet = new ParameterSheet { Sheet = sheetName };
            foreach (object[] row in rows.Skip(1))
            {
                string[] cells = at.Select(index => index < row.Length ? Cell(row[index]) : null).ToArray();
                if (cells.All(cell => cell == null))
                {
                    continue;
                }
                if (cells[0] == null)
                {
                    throw new InvalidDataException("the parameter sheet " + sheetName + " holds a parameter with no name (Q155)");
                }
                if (!Keep(cells[7], cells[0], sheetName))
                {
                    sheet.LeftOut.Add(cells[0]);
                    continue;
                }
                sheet.Parameters.Add(new ParameterSpec
                {
                    Name = cells[0],
                    Guid = cells[1],
                    DataType = cells[2] == null ? null : cells[2].ToUpperInvariant(),
                    Group = cells[3],
                    Categories = (cells[4] ?? "").Split(',').Select(name => name.Trim()).Where(name => name.Length > 0).ToList(),
                    FilledBy = FilledBy(cells[5], cells[0], sheetName),
                });
            }
            Refuse(sheet.Parameters, sheet.LeftOut, "the parameter sheet " + sheetName);
            // R465. RRE_ZZ_SP_Prototype-FFL is never written, a sheet that gives it a button is refused
            ParameterSpec ffl = sheet.Parameters.FirstOrDefault(item => item.Name == Placer.FflParameter);
            if (ffl != null && ffl.FilledBy != null)
            {
                throw new InvalidDataException("the parameter sheet " + sheetName + " gives " + Placer.FflParameter + " to button " + ffl.FilledBy.Value.ToString(CultureInfo.InvariantCulture)
                    + ", it is never written, Filled by " + FilledByNone + " (R465)");
            }
            return sheet;
        }

        // Yes or No in any case, a blank or another text refused so a row is never left out by a slip
        private static bool Keep(string cell, string name, string sheetName)
        {
            if (string.Equals(cell, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(cell, "No", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            throw new InvalidDataException("the parameter sheet " + sheetName + " gives " + name + " the Keep " + (cell ?? "blank") + ", Yes or No (R461)");
        }

        // The number of a button from 1 to 9 at the start of the cell, as 7 or 7 Place villas, or none
        private static int? FilledBy(string cell, string name, string sheetName)
        {
            if (string.Equals(cell, FilledByNone, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (cell != null && cell[0] >= '1' && cell[0] <= '9' && (cell.Length == 1 || cell[1] == ' '))
            {
                return cell[0] - '0';
            }
            throw new InvalidDataException("the parameter sheet " + sheetName + " gives " + name + " the Filled by " + (cell ?? "blank") + ", a button from 1 to 9 or " + FilledByNone + " (R465)");
        }

        // A name twice, kept or left out, is refused. A GUID of no GUID shape is refused here, a missing one is a stop of the planner,
        // which names it
        private static void Refuse(List<ParameterSpec> parameters, List<string> leftOut, string source)
        {
            List<string> twice = parameters.Select(item => item.Name).Concat(leftOut).GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
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

        private static string Cell(object value)
        {
            string text = value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
