#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.PlaceVillas;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests.FAST13
{
    // R810 to R812, F33. The Revit copies of the running year beside the Prototypes folder: found per code, linked by step 7 and named in
    // the row Prototypes. The folders and files are made up here, the year of each file is given by the test, no Revit file is read
    public sealed class PrototypeCopiesTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "fast13-" + Guid.NewGuid().ToString("N"));
        private readonly Dictionary<string, int?> years = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private string File2(string folder, string name, int? year)
        {
            string directory = Path.Combine(work, folder);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, "prototype");
            years[path] = year;
            return path;
        }

        private int? YearOf(string path) => years.TryGetValue(path, out int? year) ? year : null;

        private PrototypeCopies Find(int running = 2024) => PrototypeCopies.Find(Path.Combine(work, "Prototypes"), running, YearOf);

        [Fact]
        public void R810_a_copy_counts_only_in_a_folder_beside_whose_name_holds_the_year_and_only_when_it_reads_that_year()
        {
            File2("Prototypes", "A.rvt", 2021);
            File2("Prototypes", "B.rvt", 2021);
            File2("Prototypes", "C.rvt", 2021);
            File2("Prototypes", "D.rvt", 2024);
            File2("Prototypes", "A.0001.rvt", 2021);
            string copyA = File2("Prototypes_2024", "A.rvt", 2024);
            File2("Prototypes_2024", "B.rvt", 2021);
            File2("Upgraded", "C.rvt", 2024);

            PrototypeCopies copies = Find();

            Assert.Equal(Path.Combine(work, "Prototypes_2024"), copies.CopiesFolder);
            Assert.Equal(new[] { "A.rvt", "B.rvt", "C.rvt", "D.rvt" }, copies.Codes.Select(code => code.Name).ToArray());
            Assert.Equal(new int?[] { 2024, 2021, 2021, 2024 }, copies.Codes.Select(code => code.Year).ToArray());
            Assert.Equal(new[] { copyA, null, null, null }, copies.Codes.Select(code => code.Copy).ToArray());
            Assert.Equal(1, copies.CopyCount);
            Assert.Equal(copyA, copies.FileFor("a.RVT"));
            Assert.Equal(Path.Combine(work, "Prototypes", "B.rvt"), copies.FileFor("B.rvt"));
            // A Revit of another year finds no folder of its year
            Assert.Null(Find(2025).CopiesFolder);
        }

        [Fact]
        public void R810_the_folder_with_the_most_copies_wins_and_a_tie_goes_by_name()
        {
            File2("Prototypes", "A.rvt", 2021);
            File2("Prototypes", "B.rvt", 2021);
            File2("Z 2024", "A.rvt", 2024);
            File2("Z 2024", "B.rvt", 2024);
            File2("P2024", "A.rvt", 2024);
            Assert.Equal(Path.Combine(work, "Z 2024"), Find().CopiesFolder);
            File2("P2024", "B.rvt", 2024);
            Assert.Equal(Path.Combine(work, "P2024"), Find().CopiesFolder);
        }

        [Fact]
        public void R810_no_folder_no_prototype_file_and_no_copy_give_no_copies_folder()
        {
            Assert.Null(Find());
            Directory.CreateDirectory(Path.Combine(work, "Prototypes"));
            Assert.Null(Find());
            File2("Prototypes", "A.rvt", null);
            File2("Prototypes 2024", "B.rvt", 2024);
            PrototypeCopies none = Find();
            Assert.Null(none.CopiesFolder);
            Assert.Null(none.Codes.Single().Year);
            Assert.Equal(Path.Combine(work, "Prototypes", "A.rvt"), none.FileFor("A.rvt"));
        }

        private static PlanPlot Plot(string number)
        {
            return new PlanPlot
            {
                Plot = number, PrototypeFile = new Sourced<string> { Value = "VD4.rvt" }, LinkName = new Sourced<string> { Value = "VD4-" + number },
                Insert = new Sourced<double[]> { Value = new[] { 1100.0, 2000.0, 5.0 } }, RotationDeg = new Sourced<double?> { Value = 0 }, Mirrored = new Sourced<bool?> { Value = false },
                Levels = new PlanLevels { Ffl = new Sourced<double?> { Value = 5.0 } },
                Parameters = new Dictionary<string, Sourced<string>> { [LinkFields.PlotNumber] = new Sourced<string> { Value = number } },
            };
        }

        private FakeVillas Place(PrototypeCopies copies)
        {
            var plan = new PlanFile
            {
                Format = PlanFile.FormatName, FormatVersion = PlanFile.Version, Neighbourhood = "012",
                SurveyPoint = new PlanSurveyPoint { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 1000.0, 2000.0, 0.0 } },
                AngleToTrueNorthDeg = 0, PrototypeFolder = Path.Combine(work, "Prototypes"), Plots = new List<PlanPlot> { Plot("101"), Plot("102") },
            };
            var config = LightModeConfig.From(new JObject { ["addin"] = new JObject { ["lightMode"] = new JObject { ["villaModeDefault"] = "links", ["solidHeightM"] = 3.0, ["baseBoundary"] = "lowestFloor", ["baseBoundaryFloorType"] = null, ["footprintLimitMm"] = 100.0, ["footprintSampleStepMm"] = 10.0 } } });
            var document = new FakeVillas();
            Func<RunContext, VillaInputs> load = context => new VillaInputs
            {
                Plan = plan, PlanPath = Path.Combine(work, "plan_012.json"), Config = config, Mode = VillaModes.Links, Profile = "parsons2024Rev6", Copies = copies,
                ParameterGuids = new Dictionary<string, string> { [LinkFields.PlotNumber] = FakeVillas.PlotGuid },
            };
            ButtonBody body = new PlaceVillasRun(document, load, context => null, path => new string('a', 64), lines => true).Body();
            var run = new RunContext(7, "Place villas", "r7", work, null, true, new RunInput[0], new Dictionary<string, string>());
            body.Preview(run);
            body.Outside?.Invoke(run)?.Dispose();
            body.Apply(run);
            return document;
        }

        [Fact]
        public void R811_step_7_links_the_copy_of_a_code_and_the_file_of_the_folder_when_it_has_none()
        {
            string old = File2("Prototypes", "VD4.rvt", 2021);
            string copy = File2("Prototypes_2024", "VD4.rvt", 2024);

            FakeVillas linked = Place(Find());
            Assert.Equal(new[] { copy }, linked.Created.ToArray());
            Assert.Equal(2, linked.Elements.Count);

            // The copy still of Revit 2021 is no copy, the run links the file of the folder as before, as it does with no copies
            years[copy] = 2021;
            Assert.Equal(new[] { old }, Place(Find()).Created.ToArray());
            Assert.Equal(new[] { old }, Place(null).Created.ToArray());
        }

        [Fact]
        public void R811_the_copies_of_another_folder_are_not_taken_and_the_box_counts_the_copy()
        {
            File2("Prototypes", "VD4.rvt", 2021);
            string copy = File2("Prototypes_2024", "VD4.rvt", 2024);
            PrototypeCopies copies = Find();
            var inputs = new VillaInputs { Plan = new PlanFile { PrototypeFolder = Path.Combine(work, "Prototypes") + Path.DirectorySeparatorChar }, Copies = copies };
            Assert.Equal(copy, inputs.PrototypeFile("VD4.rvt"));
            inputs.Plan.PrototypeFolder = Path.Combine(work, "Other");
            Assert.Equal(Path.Combine(work, "Other", "VD4.rvt"), inputs.PrototypeFile("VD4.rvt"));

            RunSummary summary = LinkUpgrade.WithCopies(new RunSummary("plots 2", new List<string> { "plot 101: link" }), copies);
            Assert.Equal(new[] { "links the Revit 2024 copies in Prototypes_2024 for 1 of 1 prototype files (R811)", "plot 101: link" }, summary.PerPlot.ToArray());
            File2("Prototypes", "VD5.rvt", 2021);
            Assert.Contains("prototype VD5.rvt has no Revit 2024 copy, it is linked from Prototypes as before", LinkUpgrade.WithCopies(new RunSummary("c", new List<string>()), Find()).PerPlot);
            Assert.Single(LinkUpgrade.WithCopies(new RunSummary("c", new List<string> { "x" }), PrototypeCopies.Find(Path.Combine(work, "Prototypes_2024"), 2024, YearOf)).PerPlot);
        }

        [Fact]
        public void R811_with_copies_reads_the_running_revit_and_a_version_that_is_no_number_finds_none()
        {
            File2("Prototypes", "VD4.rvt", 2021);
            File2("Prototypes_2024", "VD4.rvt", 2024);
            var inputs = new VillaInputs { Plan = new PlanFile { PrototypeFolder = Path.Combine(work, "Prototypes") } };
            Assert.Null(PlaceVillasBody.WithCopies(inputs, "next").Copies);
            Assert.NotNull(PlaceVillasBody.WithCopies(inputs, "2024").Copies);
            Assert.Null(PlaceVillasBody.WithCopies(null, "2024"));
        }

        [Fact]
        public void R812_the_row_names_the_folder_of_copies_and_the_year_per_code()
        {
            File2("Prototypes", "VA1.rvt", 2021);
            File2("Prototypes", "VB2.rvt", 2021);
            File2("Prototypes", "VC3.rvt", null);
            File2("Prototypes_2024", "VA1.rvt", 2024);
            PrototypeCopies copies = Find();
            const string Expected = "Revit 2024 copies in Prototypes_2024 for 1 of 3 files, step 7 links them. By code: VA1 2024, VB2 2021 no copy, VC3 year not read no copy.";
            Assert.Equal(Expected, PaneViewModel.CopiesNote(copies));

            var asked = new List<string>();
            PaneViewModel pane = Pane((path, done) => done(2021), (path, year, done) =>
            {
                asked.Add(path + "|" + year);
                done(copies);
            });
            Assert.Equal(Expected, Row(pane).Note);
            Assert.Equal(new[] { Path.Combine(work, "Prototypes") + "|2024" }, asked.ToArray());
            // No folder of copies keeps the R496 warning, and with no copies look the row is as it was
            Assert.StartsWith("These files are Revit 2021", Row(Pane((path, done) => done(2021), (path, year, done) => done(Find(2025)))).Note);
            Assert.StartsWith("These files are Revit 2021", Row(Pane((path, done) => done(2021), null)).Note);
        }

        private static FileRowViewModel Row(PaneViewModel pane) => pane.InputRows.Single(item => item.Keys.Contains(InputKeys.Prototypes));

        private PaneViewModel Pane(Action<string, Action<int?>> look, Action<string, int, Action<PrototypeCopies>> copiesLook)
        {
            var settings = new ProjectSettings { ConfigFile = "cfg" };
            settings.Inputs[InputNames.Prototypes] = new SettingsInput { Path = Path.Combine(work, "Prototypes") };
            var pane = new PaneViewModel(new FoundProbe()) { FormatLook = look, CopiesLook = copiesLook, RunningRevit = 2024 };
            pane.Load(new DocumentSession("s", Path.Combine(work, "x.rvt"), "x"));
            pane.LoadSettings(new PaneSettings(settings));
            return pane;
        }

        private sealed class FoundProbe : IInputProbe
        {
            public InputFacts Look(string path, bool folder) => new InputFacts { Found = !string.IsNullOrEmpty(path), Size = 5, Stamp = 7 };

            public void Hash(string path, bool folder, Action<string> done)
            {
            }

            public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
            {
                fallback = null;
                return new string[0];
            }

            public string RulesName(string rulesFile) => null;

            public string ConfigSheet(string configFile) => null;

            public SheetRead ReadSheet(string path) => null;

            public string VillaModeDefault(string configFile) => null;
        }
    }
}
#endif
