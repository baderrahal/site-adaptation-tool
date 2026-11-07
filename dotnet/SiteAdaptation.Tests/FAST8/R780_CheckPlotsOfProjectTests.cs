using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.ReadData;
using Xunit;

namespace SiteAdaptation.Tests.FAST8
{
    // R780, F26 of the short check of 7 Nov. Under rev6, 4 Check takes its plots from the plot table of the read of the project and never from
    // plots.csv or labels.csv of the data book of the work root, which the read named. All made up in code
    public sealed class R780_CheckPlotsOfProjectTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "r780-" + Guid.NewGuid().ToString("N"));

        public R780_CheckPlotsOfProjectTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private static string Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string> { ["Settings"] = CheckRun.Config, ["Prototypes"] = CheckRun.Prototypes };

        private string Work => Path.Combine(root, "work");

        private string Book => Path.Combine(root, "data_book");

        private string ReadFolder => Path.Combine(Work, ReadDataRun.FolderPrefix + "0123");

        // A work folder whose latest read named plots.csv and labels.csv of a data book beside it, as the read for the build did on 7 Nov
        private string Site(string mode, bool table = true)
        {
            string config = Path.Combine(root, "repo", "config", "project.json");
            var settings = new JObject
            {
                ["profile"] = "rules/made-up.json",
                ["majority"] = JObject.Parse("{ 'groups': { 'native': ['n1'], 'parsons': ['kx'] }, 'overrideDefaultSources': ['parsons'], 'linkPlotParameter': 'Plot' }"),
                ["addin"] = JObject.Parse("{ 'workerTimeLimitS': { 'build': 60 }, 'readData': { 'dataBookFolder': 'data_book' } }"),
                ["designBasis"] = new JObject { ["mode"] = mode, ["modes"] = new JArray("rev6", "pastMade"), ["roadLevel"] = new JObject { ["surfaces"] = new JArray() } },
            };
            Write(config, settings.ToString());
            Write(Path.Combine(root, "repo", "config", "floor_outlines.json"), "{}");
            Write(Path.Combine(root, "repo", "config", "prototype_offsets.json"), "{}");
            Write(Path.Combine(root, "repo", "config", "overrides", "made-up.json"), "{ \"note\": \"made up\", \"overrides\": [] }");
            Write(Path.Combine(root, "repo", "rules", "made-up.json"), "{ 'levelProfiles': { 'default': 'p', 'p': { 'job': 'parsons', 'frontRoad': 1, 'rampSlopePercent': 1, 'rampTypes': {}, 'height': 1 } } }");
            Write(Path.Combine(root, "prototypes", "P-ARC-VL4-A.rvt"), "made up");
            Write(Path.Combine(Work, "site.json"), "{}");
            string plots = Write(Path.Combine(Book, "plots.csv"), "locationPlanNumber,baseplanLabel\n900,900\n");
            string labels = Write(Path.Combine(Book, "location_plan", "labels.csv"), "kind,number,nbh\nclientCode,900,B9\n");
            foreach (string name in CheckRun.ReadFiles.Values)
            {
                Write(Path.Combine(ReadFolder, name), "{ 'plots': [] }");
            }
            if (table)
            {
                var rows = new JArray
                {
                    new JObject { ["baseplanLabel"] = "11", ["officialNumber"] = "11", ["locationPlanNumber"] = null, ["nbhId"] = "A1", ["typologyRev9"] = "V1" },
                    new JObject { ["baseplanLabel"] = "12", ["officialNumber"] = "12", ["locationPlanNumber"] = null, ["nbhId"] = "A1", ["typologyRev9"] = null },
                };
                Write(Path.Combine(ReadFolder, ReadDataRun.PlotTableFile), new JObject { ["plots"] = rows }.ToString());
            }
            var inputs = new[] { Tuple.Create("config", config), Tuple.Create("plotsCsv", plots), Tuple.Create("labels", labels) };
            var record = new JObject
            {
                ["format"] = ReadDataRun.RecordFormat, ["runId"] = "R2-1",
                ["inputs"] = new JArray(inputs.Select(item => new JObject { ["role"] = item.Item1, ["path"] = item.Item2, ["sha256"] = FileHash.Sha256(item.Item2) })),
            };
            Write(Path.Combine(ReadFolder, ReadDataRun.RecordFile), record.ToString());
            return config;
        }

        private CheckRun.CheckPlan Plan(string config, string mode)
        {
            var run = new CheckRun(Roles, null, null, new WorkerDocument(), null, null, null, null) { ModePick = () => mode };
            var inputs = new[] { new RunInput("Settings", config, false), new RunInput("Prototypes", Path.Combine(root, "prototypes"), true) };
            return run.Plan(new RunContext(4, "4 Check", "R4-1", Work, null, true, inputs, new Dictionary<string, string>()));
        }

        private static string PathOf(CheckRun.CheckPlan plan, string role)
        {
            return plan.Inputs.Where(input => input.Role == role).Select(input => input.Path).SingleOrDefault();
        }

        [Fact]
        public void R780_under_rev6_check_takes_the_plots_of_the_plot_table_and_no_file_of_the_data_book()
        {
            CheckRun.CheckPlan plan = Plan(Site("rev6"), "rev6");
            Assert.Empty(plan.Stops);
            string book = Path.GetFullPath(Book);
            Assert.DoesNotContain(plan.Inputs, input => input.Path.StartsWith(book, StringComparison.OrdinalIgnoreCase));
            string plots = PathOf(plan, "plotsCsv"), labels = PathOf(plan, "labels");
            Assert.Equal(Path.Combine(Path.GetFullPath(Work), ProjectPlots.FolderPrefix + Path.GetFileName(ReadFolder), ProjectPlots.PlotsFile), plots);
            List<Dictionary<string, string>> rows = CsvRows.Read(plots, CsvRows.PlotsColumns);
            Assert.Equal(new[] { "11", "12" }, rows.Select(row => row["locationPlanNumber"]));
            Assert.Equal(new[] { "V1", "" }, rows.Select(row => row["typologyRev9"]));
            Assert.Equal(new[] { "A1", "A1" }, rows.Select(row => row["nbhId"]));
            List<Dictionary<string, string>> scope = CsvRows.Read(labels, CsvRows.LabelsColumns);
            Assert.Equal(new[] { "clientCode|11|A1", "clientCode|12|A1" }, scope.Select(row => row["kind"] + "|" + row["number"] + "|" + row["nbh"]));
            Assert.Equal(FileHash.Sha256(plots), plan.Inputs.Single(input => input.Role == "plotsCsv").Sha256);
        }

        [Fact]
        public void R780_a_data_book_file_gone_or_changed_since_the_read_stops_nothing_under_rev6()
        {
            string config = Site("rev6");
            File.Delete(Path.Combine(Book, "plots.csv"));
            File.WriteAllText(Path.Combine(Book, "location_plan", "labels.csv"), "kind,number,nbh\nclientCode,901,B9\n");
            CheckRun.CheckPlan plan = Plan(config, "rev6");
            Assert.Empty(plan.Stops);
            Assert.NotNull(PathOf(plan, "plotsCsv"));
        }

        [Fact]
        public void R780_a_read_with_no_plot_table_says_the_plots_are_missing_and_never_falls_back_on_the_data_book()
        {
            CheckRun.CheckPlan plan = Plan(Site("rev6", false), "rev6");
            Assert.Contains(plan.Stops, stop => stop.StartsWith("the plots of the project are missing", StringComparison.Ordinal) && stop.Contains(ReadDataRun.PlotTableFile));
            Assert.Null(PathOf(plan, "plotsCsv"));
            Assert.Null(PathOf(plan, "labels"));
        }

        [Fact]
        public void R780_a_plots_file_of_the_project_outside_the_data_book_stays_and_one_inside_it_is_known_by_its_folder()
        {
            string config = Site("rev6");
            JObject settings = JObject.Parse(File.ReadAllText(config));
            settings["addin"]["readData"]["dataBookFolder"] = "other_book";
            File.WriteAllText(config, settings.ToString());
            CheckRun.CheckPlan plan = Plan(config, "rev6");
            Assert.Equal(Path.GetFullPath(Path.Combine(Book, "plots.csv")), Path.GetFullPath(PathOf(plan, "plotsCsv")));
            Assert.True(ProjectPlots.InDataBook(Path.Combine(Book, "location_plan", "labels.csv"), JObject.Parse("{ 'addin': { 'readData': { 'dataBookFolder': 'data_book' } } }"), Work));
            Assert.False(ProjectPlots.InDataBook(Path.Combine(root, "data_book_two", "plots.csv"), JObject.Parse("{ 'addin': { 'readData': { 'dataBookFolder': 'data_book' } } }"), Work));
            Assert.True(ProjectPlots.InDataBook(Path.Combine(Book, "plots.csv"), new JObject { ["addin"] = new JObject { ["readData"] = new JObject { ["dataBookFolder"] = Book } } }, Path.Combine(root, "elsewhere", "w")));
            Assert.False(ProjectPlots.InDataBook(Path.Combine(Book, "plots.csv"), new JObject(), Work));
        }

        [Fact]
        public void R780_the_past_job_mode_keeps_the_data_book_of_its_read()
        {
            CheckRun.CheckPlan plan = Plan(Site("pastMade"), "pastMade");
            Assert.Equal(Path.GetFullPath(Path.Combine(Book, "plots.csv")), Path.GetFullPath(PathOf(plan, "plotsCsv")));
            Assert.False(Directory.Exists(Path.Combine(Work, ProjectPlots.FolderPrefix + Path.GetFileName(ReadFolder))));
        }

        [Fact]
        public void R780_the_same_table_gives_the_same_file_untouched_and_a_new_table_a_new_file()
        {
            string config = Site("rev6");
            string plots = PathOf(Plan(config, "rev6"), "plotsCsv");
            DateTime first = File.GetLastWriteTimeUtc(plots);
            File.SetLastWriteTimeUtc(plots, first.AddMinutes(-5));
            Plan(config, "rev6");
            Assert.Equal(first.AddMinutes(-5), File.GetLastWriteTimeUtc(plots));
            var rows = new JArray(new JObject { ["baseplanLabel"] = "13", ["officialNumber"] = "13", ["nbhId"] = "A2", ["typologyRev9"] = "V2" });
            File.WriteAllText(Path.Combine(ReadFolder, ReadDataRun.PlotTableFile), new JObject { ["plots"] = rows }.ToString());
            CheckRun.CheckPlan again = Plan(config, "rev6");
            Assert.Equal(new[] { "13" }, CsvRows.Read(PathOf(again, "plotsCsv"), CsvRows.PlotsColumns).Select(row => row["locationPlanNumber"]));
        }

        [Fact]
        public void R780_the_number_of_a_row_is_its_one_location_plan_number_else_its_official_number_else_its_label()
        {
            var plots = new JArray
            {
                new JObject { ["baseplanLabel"] = "L1", ["officialNumber"] = "21", ["locationPlanNumber"] = "31" },
                new JObject { ["baseplanLabel"] = "L2", ["officialNumber"] = "22", ["locationPlanNumber"] = "32|33" },
                new JObject { ["baseplanLabel"] = "L3", ["officialNumber"] = null, ["locationPlanNumber"] = null },
                new JObject { ["baseplanLabel"] = "L4", ["officialNumber"] = "21" },
                new JObject { ["baseplanLabel"] = "L5", ["officialNumber"] = "21" },
                new JObject { ["baseplanLabel"] = null, ["officialNumber"] = null },
                new JObject { ["baseplanLabel"] = "L6", ["officialNumber"] = "26", ["typologyRev9"] = "V,1", ["zone"] = "Z \"2\"" },
            };
            List<string[]> rows = ProjectPlots.Rows(plots);
            Assert.Equal(new[] { "31", "22", "L3", "21", "26" }, rows.Select(row => row[0]));
            string folder = Path.Combine(root, "quoted");
            Directory.CreateDirectory(folder);
            Write(Path.Combine(folder, ReadDataRun.PlotTableFile), new JObject { ["plots"] = plots }.ToString());
            ProjectPlots.Written written = ProjectPlots.Write(folder, Work);
            Dictionary<string, string> last = CsvRows.Read(written.Plots, CsvRows.PlotsColumns).Last();
            Assert.Equal("V,1", last["typologyRev9"]);
            Assert.Equal("Z \"2\"", last["zone"]);
            Assert.Equal(5, written.Rows);
        }
    }
}
