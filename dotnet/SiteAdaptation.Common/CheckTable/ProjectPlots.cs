using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ReadData;

namespace SiteAdaptation.Common.CheckTable
{
    // R780, F26 of the short check of 7 Nov. Under rev6 the build of 4 Check takes its plots from the plot table of the read of the project,
    // read_plotTable.json of the latest read, never from plots.csv and labels.csv of the data book of the work root, which belong to the past job.
    // The worker build reads plots by the roles plotsCsv and labels as CSV, so the table is written as those two files beside the read, in a folder
    // of the work folder, and the same table gives the same bytes, so the hash a later step holds the plan to stays the same
    public static class ProjectPlots
    {
        public const string FolderPrefix = "plots_";
        public const string PlotsFile = "plots.csv";
        public const string LabelsFile = "labels.csv";

        // The columns of plots.csv the build steps read by name, BuildSteps, FloorOutlines, CheckRows, LevelExcel
        public static readonly string[] PlotsColumns = { "locationPlanNumber", "baseplanLabel", "typologyRev9", "nbhId", "zone", "footprintBlock" };

        // The columns of labels.csv PlotScope reads, a row of kind clientCode per plot with its number and area
        public static readonly string[] LabelsColumns = { "kind", "number", "nbh" };

        public const string ClientCode = "clientCode";

        public sealed class Written
        {
            public string Plots { get; set; }
            public string Labels { get; set; }
            public int Rows { get; set; }
            public string Missing { get; set; }
        }

        // The two files of the plot table of the read folder, or Missing with why when the read holds no plot table. Never a file of the data book
        public static Written Write(string readFolder, string workFolder)
        {
            string table = readFolder == null ? null : Path.Combine(readFolder, ReadDataRun.PlotTableFile);
            if (table == null || !File.Exists(table))
            {
                return new Written { Missing = "the plots of the project are missing: the read " + (readFolder == null ? "" : Path.GetFileName(readFolder) + " ") + "holds no plot table " + ReadDataRun.PlotTableFile + ", run 2 Read data again (R780)" };
            }
            JObject read = JObject.Parse(WorkFiles.ReadText(table));
            List<string[]> rows = Rows(read["plots"] as JArray ?? new JArray());
            string folder = Path.Combine(workFolder, FolderPrefix + Path.GetFileName(readFolder));
            Directory.CreateDirectory(folder);
            var written = new Written { Plots = Path.Combine(folder, PlotsFile), Labels = Path.Combine(folder, LabelsFile), Rows = rows.Count };
            Keep(written.Plots, Csv(PlotsColumns, rows));
            Keep(written.Labels, Csv(LabelsColumns, rows.Select(row => new[] { ClientCode, row[0], row[3] })));
            return written;
        }

        // R780. One row per plot of the table with a number: the location plan number when it is one, else the official number, else the
        // baseplan label, the number the data check gives a plot (DataCheckCodes.PlotTable). A plot of two location plan numbers keeps its official one
        public static List<string[]> Rows(JArray plots)
        {
            var rows = new List<string[]>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject plot in plots.OfType<JObject>())
            {
                string label = Text(plot["baseplanLabel"]);
                string located = Text(plot["locationPlanNumber"]);
                string number = located.Length > 0 && located.IndexOf('|') < 0 ? located : Text(plot["officialNumber"]).Length > 0 ? Text(plot["officialNumber"]) : label;
                if (number.Length == 0 || !seen.Add(number))
                {
                    continue;
                }
                rows.Add(new[] { number, label, Text(plot["typologyRev9"]), Text(plot["nbhId"]), Text(plot["zone"]), Text(plot["footprintBlock"]) });
            }
            return rows;
        }

        private static string Text(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? "" : token.Type == JTokenType.String ? ((string)token).Trim() : token.ToString().Trim();
        }

        private static string Csv(IEnumerable<string> header, IEnumerable<string[]> rows)
        {
            var text = new StringBuilder();
            text.Append(string.Join(",", header.Select(Cell))).Append('\n');
            foreach (string[] row in rows)
            {
                text.Append(string.Join(",", row.Select(Cell))).Append('\n');
            }
            return text.ToString();
        }

        // A cell with a comma, a quote or a line break is quoted as the csv module of Python writes it
        private static string Cell(string value)
        {
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // The file is written only when its text changes, so a file a run holds by its hash is never touched again by the same table
        private static void Keep(string path, string text)
        {
            if (File.Exists(path) && WorkFiles.ReadText(path) == text)
            {
                return;
            }
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
