using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Tags;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R377, R404. Button 7 Place villas on a fake document: Masses and Links, the swap, the second run, the link types by full path and
    // the checks of R83 to R85. No Revit type, so these run on net8.0 too
    public class R377_PlaceVillasTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "r377_" + Guid.NewGuid().ToString("N"));
        private readonly string prototypes;

        public R377_PlaceVillasTests()
        {
            prototypes = Path.Combine(work, "Prototypes");
            Directory.CreateDirectory(prototypes);
            File.WriteAllText(Path.Combine(prototypes, "VD4.rvt"), "prototype");
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private static readonly double[][] Shape = { new[] { 0.0, 0.0 }, new[] { 8.0, 0.0 }, new[] { 8.0, 15.0 }, new[] { 0.0, 15.0 } };

        private sealed class FixedShape : IBaseShapeReader
        {
            public int Reads;

            public IReadOnlyList<double[]> Read(string prototypePath, LightModeConfig config)
            {
                Reads++;
                return Shape;
            }
        }

        private PlanPlot Plot(string number, double east, double rotation, bool mirrored)
        {
            return new PlanPlot
            {
                Plot = number,
                PrototypeFile = new Sourced<string> { Value = "VD4.rvt" },
                LinkName = new Sourced<string> { Value = "VD4-" + number },
                Insert = new Sourced<double[]> { Value = new[] { east, 2000.0, 5.0 } },
                RotationDeg = new Sourced<double?> { Value = rotation },
                Mirrored = new Sourced<bool?> { Value = mirrored },
                Levels = new PlanLevels { Ffl = new Sourced<double?> { Value = 5.0 } },
                Parameters = new Dictionary<string, Sourced<string>> { [LinkFields.PlotNumber] = new Sourced<string> { Value = number }, ["CASE"] = new Sourced<string> { Value = "A" } },
            };
        }

        private PlanFile Plan(params PlanPlot[] plots)
        {
            return new PlanFile
            {
                Format = PlanFile.FormatName, FormatVersion = PlanFile.Version, Neighbourhood = "012",
                SurveyPoint = new PlanSurveyPoint { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 1000.0, 2000.0, 0.0 } },
                AngleToTrueNorthDeg = 0, PrototypeFolder = prototypes, Plots = plots.ToList(),
            };
        }

        private static LightModeConfig Config()
        {
            return LightModeConfig.From(new JObject
            {
                ["addin"] = new JObject
                {
                    ["lightMode"] = new JObject
                    {
                        ["villaModeDefault"] = "masses", ["solidHeightM"] = 3.0, ["baseBoundary"] = "lowestFloor", ["baseBoundaryFloorType"] = null,
                        ["footprintLimitMm"] = 100.0, ["footprintSampleStepMm"] = 10.0,
                    },
                },
            });
        }

        private sealed class Run
        {
            public FakeVillas Document;
            public PlaceVillasRun Body;
            public FixedShape Reader = new FixedShape();
            public bool LoadAnswer = true;
            public List<IReadOnlyList<string>> Asked = new List<IReadOnlyList<string>>();
        }

        private Run Make(PlanFile plan, string mode, FakeVillas document = null, Dictionary<string, string> lastHashes = null, List<double[][]> footprint = null,
            Dictionary<string, string> guids = null, ICollection<string> others = null)
        {
            var run = new Run { Document = document ?? new FakeVillas() };
            Func<RunContext, VillaInputs> load = context => new VillaInputs
            {
                Plan = plan, PlanPath = Path.Combine(work, "plan_012.json"), Config = Config(), Mode = mode, Profile = "parsons2024Rev6",
                ParameterGuids = new Dictionary<string, string>(guids ?? new Dictionary<string, string>()) { [LinkFields.PlotNumber] = FakeVillas.PlotGuid, ["CASE"] = "c0000000-0000-0000-0000-000000000000" },
                Footprint = footprint ?? new List<double[][]>(), LastHashes = lastHashes ?? new Dictionary<string, string>(), FilledByOthers = others ?? new HashSet<string>(),
            };
            run.Body = new PlaceVillasRun(run.Document, load, context => new ShapeCache(context.WorkFolder, run.Reader, path => new string('a', 64)), path => new string('a', 64),
                lines => { run.Asked.Add(lines); return run.LoadAnswer; });
            return run;
        }

        private RunContext Context(string runId = "r7")
        {
            return new RunContext(7, "Place villas", runId, work, null, true, new RunInput[0], new Dictionary<string, string>());
        }

        private static IReadOnlyList<string> Apply(Run run, RunContext context)
        {
            ButtonBody body = run.Body.Body();
            body.Preview(context);
            body.Outside?.Invoke(context)?.Dispose();
            return body.Apply(context);
        }

        // R465. A parameter of the plan the parameter sheet gives another button, or none, is never written by button 7 and is no issue
        [Fact]
        public void R465_button_7_writes_only_the_parameters_the_sheet_gives_it()
        {
            Run own = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses);
            IReadOnlyList<string> ownIssues = Apply(own, Context());
            Assert.Equal("A", own.Document.Elements.Single(element => element.IsSolid).Parameters["CASE"]);
            Run other = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, others: new HashSet<string> { "CASE" });

            Assert.Equal(ownIssues, Apply(other, Context()));

            FakeElement solid = other.Document.Elements.Single(element => element.IsSolid);
            Assert.False(solid.Parameters.ContainsKey("CASE"));
            Assert.Equal("101", solid.Parameters[LinkFields.PlotNumber]);
            // The FFL of the plan, given to none by the sheet of the repo, is never written, the steps and the name still are
            Run level = LevelRun(Plan(LevelPlot("101", 612.345)), VillaModes.Links, null, new HashSet<string> { Placer.FflParameter });
            IReadOnlyList<string> issues = Apply(level, Context());
            FakeElement link = level.Document.Elements.Single();
            Assert.False(link.Parameters.ContainsKey(Placer.FflParameter));
            Assert.Equal("3", link.Parameters[StepsName]);
            Assert.DoesNotContain(issues, line => line.Contains(Placer.FflParameter));
        }

        [Fact]
        public void R404_masses_place_one_tagged_solid_per_villa_with_its_plot_number_and_name()
        {
            Run run = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 90, true)), VillaModes.Masses);
            Apply(run, Context());
            List<FakeElement> solids = run.Document.Elements.Where(element => element.IsSolid).ToList();
            Assert.Equal(2, solids.Count);
            Assert.All(solids, solid => Assert.Equal(PlaceVillasRun.MassPart, solid.Tag.Key.Part));
            Assert.All(solids, solid => Assert.Equal(PlacedKinds.Link, solid.Tag.Key.Kind));
            Assert.Equal(new[] { "101", "102" }, solids.Select(solid => solid.Parameters[LinkFields.PlotNumber]));
            Assert.Equal("VD4-102", solids[1].Name);
            Assert.Equal(1, run.Reader.Reads);
            Assert.Empty(run.Document.Created);
            // W6, the solid stands at the FFL of the plan like the link
            Assert.Equal(5.0, solids[0].Steps.Move[2]);
            Assert.Equal(new[] { 100.0, 0.0 }, solids[0].Tag.Placed.Values["origin"]);
            Assert.Equal(new[] { 5.0 }, solids[0].Tag.Placed.Values["z"]);
        }

        [Fact]
        public void R404_the_footprint_rows_are_written_and_a_flag_never_stops_the_run()
        {
            List<double[][]> blocks = new List<double[][]> { ShapeMath.Place(Shape, new[] { 1100.5, 2000.0 }, 0, false).ToArray() };
            Run run = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, footprint: blocks);
            IReadOnlyList<string> issues = Apply(run, Context());
            Assert.Contains(issues, line => line.Contains("plot 101") && line.Contains("footprint"));
            string file = Directory.GetFiles(work, "footprint_012.json*").Single();
            JObject rows = JObject.Parse(File.ReadAllText(file));
            Assert.True((bool)rows["rows"][0]["flagged"]);
            Assert.Equal(500.0, (double)rows["rows"][0]["distanceMm"], 3);
            Assert.Single(run.Document.Elements);
        }

        [Fact]
        public void R377_a_second_run_skips_the_villas_equal_to_the_plan_and_moves_those_the_plan_moved_in_place()
        {
            Run first = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Masses);
            Apply(first, Context("r1"));
            string kept = first.Document.Elements[1].UniqueId;
            Run second = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1250, 0, false)), VillaModes.Masses, first.Document);
            RunSummary summary = second.Body.Body().Preview(Context("r2"));
            Assert.Contains("move 1", summary.Counts);
            Assert.Contains("already placed 1", summary.Counts);
            Apply(second, Context("r2"));
            Assert.Equal(2, first.Document.Elements.Count);
            Assert.Equal(kept, first.Document.Elements[1].UniqueId);
            Assert.Equal(new[] { 250.0, 0.0 }, first.Document.Elements[1].Tag.Placed.Values["origin"]);
            Assert.Equal(1, first.Document.Reshaped);
        }

        [Fact]
        public void R377_an_untagged_link_of_a_plot_is_listed_and_never_moved()
        {
            var document = new FakeVillas();
            document.Elements.Add(new FakeElement { UniqueId = "hand", PlotNumber = "101", Steps = new PlacementSteps { Move = new[] { 5.0, 5.0, 0.0 } } });
            foreach (string mode in VillaModes.All)
            {
                Run run = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), mode, document);
                IReadOnlyList<string> issues = Apply(run, Context(mode));
                Assert.Contains(issues, line => line.Contains("hand"));
                Assert.Equal(new[] { 5.0, 5.0, 0.0 }, document.Elements.Single(element => element.UniqueId == "hand").Steps.Move);
            }
        }

        [Fact]
        public void R404_links_mode_swaps_each_tagged_solid_for_its_link_at_the_same_transform()
        {
            Run masses = Make(Plan(Plot("101", 1100, 30, true), Plot("102", 1200, 0, false)), VillaModes.Masses);
            Apply(masses, Context("r1"));
            Run links = Make(Plan(Plot("101", 1100, 30, true), Plot("102", 1200, 0, false)), VillaModes.Links, masses.Document);
            Assert.Contains("swap 2", links.Body.Body().Preview(Context("r2")).Counts);
            Apply(links, Context("r2"));
            Assert.DoesNotContain(masses.Document.Elements, element => element.IsSolid);
            Assert.Equal(2, masses.Document.Elements.Count);
            FakeElement link = masses.Document.Elements.Single(element => element.Tag.Key.Plot == "101");
            Assert.Equal("", link.Tag.Key.Part);
            Assert.Equal(new[] { 100.0, 0.0 }, link.Tag.Placed.Values["origin"]);
            Assert.Equal(new[] { 30.0 }, link.Tag.Placed.Values["rotationDeg"]);
            Assert.Equal(new[] { 1.0 }, link.Tag.Placed.Values["mirrored"]);
            Assert.Single(masses.Document.Created);
        }

        [Fact]
        public void R404_a_swap_that_fails_its_read_back_fails_the_whole_apply_so_the_flow_rolls_it_back()
        {
            Run masses = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Masses);
            Apply(masses, Context("r1"));
            masses.Document.OffReadBack = 200;
            Run links = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Links, masses.Document);
            var error = Assert.ThrowsAny<Exception>(() => Apply(links, Context("r2")));
            Assert.Contains("plot 102", error.Message);
        }

        [Fact]
        public void R404_links_mode_on_a_model_with_no_solids_places_the_links_the_normal_way()
        {
            Run run = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links);
            Apply(run, Context());
            Assert.Single(run.Document.Elements);
            Assert.False(run.Document.Elements[0].IsSolid);
            Assert.Equal(0, run.Reader.Reads);
        }

        // R404. A masses run places no solid on a plot where a link stands, and that villa stands, so the run is done with each plot listed
        [Fact]
        public void R404_a_masses_run_over_plots_where_links_stand_places_no_solid_and_counts_those_villas_as_standing()
        {
            Run links = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Links);
            Apply(links, Context("r1"));
            Run masses = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Masses, links.Document);

            IReadOnlyList<string> issues = Apply(masses, Context("r2"));

            Assert.DoesNotContain(links.Document.Elements, element => element.IsSolid);
            Assert.Equal(2, issues.Count(line => line.Contains("a link stands on the plot")));
            JObject record = JObject.Parse(File.ReadAllText(Directory.GetFiles(work, "place_012.json*").Single(path => path.EndsWith("r2", StringComparison.Ordinal))));
            Assert.Equal(new[] { "101", "102" }, record["placed"].Select(token => (string)token));
        }

        [Fact]
        public void R404_a_run_that_leaves_no_villa_of_the_scope_placed_never_reports_done()
        {
            var document = new FakeVillas();
            document.Elements.Add(new FakeElement { UniqueId = "hand", PlotNumber = "101", Steps = new PlacementSteps { Move = new[] { 5.0, 5.0, 0.0 } } });
            Run run = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links, document);
            var error = Assert.ThrowsAny<Exception>(() => Apply(run, Context()));
            Assert.Contains("nothing", error.Message);
        }

        [Fact]
        public void R377_a_link_type_is_matched_by_its_full_path_and_one_of_the_same_name_elsewhere_is_listed_not_reused()
        {
            var document = new FakeVillas();
            string elsewhere = Path.Combine(work, "Old", "VD4.rvt");
            document.Types.Add(new VillaLinkType { FullPath = elsewhere, Key = "old" });
            Run run = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links, document);
            IReadOnlyList<string> issues = Apply(run, Context());
            Assert.Equal(new[] { Path.Combine(prototypes, "VD4.rvt") }, document.Created);
            Assert.Contains(issues, line => line.Contains(elsewhere) && line.Contains(Path.Combine(prototypes, "VD4.rvt")));
            // The same path in another case and spelling is the same type, so nothing new is made
            var same = new FakeVillas();
            same.Types.Add(new VillaLinkType { FullPath = Path.Combine(prototypes, ".", "vd4.RVT"), Key = "same" });
            Apply(Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links, same), Context());
            Assert.Empty(same.Created);
            Assert.Equal("same", same.Elements[0].TypeKey);
        }

        [Fact]
        public void R377_a_type_to_load_or_reload_is_asked_outside_the_transaction_and_cancel_changes_nothing()
        {
            string path = Path.Combine(prototypes, "VD4.rvt");
            var document = new FakeVillas();
            document.Types.Add(new VillaLinkType { FullPath = path, Key = "unloaded", Loaded = false });
            Run cancel = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links, document);
            cancel.LoadAnswer = false;
            Assert.ThrowsAny<Exception>(() => Apply(cancel, Context()));
            Assert.Contains(cancel.Asked.Single(), line => line.Contains("load") && line.Contains(path));
            Assert.Contains(cancel.Asked.Single(), line => line.Contains("Undo"));
            Assert.Empty(document.Loaded);
            Assert.Empty(document.Elements);
            Run load = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links, document);
            Apply(load, Context());
            Assert.Equal(new[] { "load unloaded" }, document.Loaded);

            var changed = new FakeVillas();
            changed.Types.Add(new VillaLinkType { FullPath = path, Key = "loaded" });
            Run reload = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links, changed, new Dictionary<string, string> { [path] = new string('b', 64) });
            Apply(reload, Context());
            Assert.Equal(new[] { "reload loaded" }, changed.Loaded);
        }

        [Fact]
        public void R377_coordinates_off_the_plan_stop_before_anything_is_placed()
        {
            PlanFile plan = Plan(Plot("101", 1100, 0, false));
            plan.SurveyPoint.SharedPosition = new[] { 1000.01, 2000.0, 0.0 };
            Run run = Make(plan, VillaModes.Masses);
            Assert.ThrowsAny<Exception>(() => run.Body.Body().Preview(Context()));
            Assert.Empty(run.Document.Elements);
        }

        [Fact]
        public void R377_a_link_whose_plot_number_is_not_written_fails_the_apply()
        {
            var document = new FakeVillas { Refuse = LinkFields.PlotNumber };
            Run run = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links, document);
            var error = Assert.ThrowsAny<Exception>(() => Apply(run, Context()));
            Assert.Contains(LinkFields.PlotNumber, error.Message);
        }

        [Fact]
        public void R404_a_solid_whose_category_lacks_the_plot_number_is_an_issue_since_its_tag_holds_the_plot()
        {
            var document = new FakeVillas { MissingOnSolids = true };
            Run run = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, document);
            IReadOnlyList<string> issues = Apply(run, Context());
            Assert.Contains(issues, line => line.Contains(LinkFields.PlotNumber) && line.Contains("plot 101"));
            Assert.Single(document.Elements);
        }

        [Fact]
        public void R377_the_plan_is_the_one_of_the_last_real_run_of_button_4_by_its_sha256_never_a_dry_folder()
        {
            string path = Path.Combine(work, "plan_012.json");
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(Plan(Plot("101", 1100, 0, false))));
            string sha = FileHash.Sha256(path);
            Assert.Equal("012", PlanSource.Open(path, sha).Neighbourhood);
            Assert.Throws<InvalidDataException>(() => PlanSource.Open(path, new string('0', 64)));
            Assert.Throws<InvalidDataException>(() => PlanSource.Open(path, null));
            string dry = Path.Combine(work, "dry", "r1", "plan_012.json");
            Directory.CreateDirectory(Path.GetDirectoryName(dry));
            File.Copy(path, dry);
            Assert.Throws<InvalidDataException>(() => PlanSource.Open(dry, sha));
        }

        [Fact]
        public void R377_the_inputs_join_the_plan_the_light_mode_keys_the_pane_mode_the_footprint_and_the_last_record()
        {
            string plan = Path.Combine(work, "plan_012.json");
            File.WriteAllText(plan, Newtonsoft.Json.JsonConvert.SerializeObject(Plan(Plot("101", 1100, 0, false))));
            string config = Path.Combine(work, "project.json");
            JToken addin = JObject.Parse(File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "config", "project.json")))["addin"];
            File.WriteAllText(config, new JObject { ["addin"] = new JObject { ["lightMode"] = addin["lightMode"], ["parameterSheet"] = Path.Combine(ThemeGateTests.RepoRoot(), "config", "shared_parameters.xlsx") } }.ToString());
            var blocks = new JObject { ["blocks"] = new JArray(new JObject { ["name"] = "VD4", ["outline"] = new JArray(Shape.Select(point => new JArray(point[0], point[1]))) }) };
            var last = new JObject { ["prototypes"] = new JObject { ["x.rvt"] = new string('e', 64) }, ["placed"] = new JArray("101") };
            VillaInputs inputs = VillaInputs.Of(plan, FileHash.Sha256(plan), config, null, "parsons2024Rev6", null, blocks, last);
            Assert.Equal(VillaModes.Masses, inputs.Mode);
            Assert.Equal(VillaModes.Links, VillaInputs.Of(plan, FileHash.Sha256(plan), config, VillaModes.Links, "p", null, blocks, null).Mode);
            Assert.Single(inputs.Footprint);
            // R465. The 9 link parameters button 7 fills, the FFL and Plot of Floors never
            Assert.Equal(9, inputs.ParameterGuids.Count);
            Assert.Equal(new[] { "Plot", "RRE_ZZ_SP_Prototype-FFL" }, inputs.FilledByOthers.OrderBy(name => name, StringComparer.Ordinal));
            Assert.Contains("101", inputs.LastPlaced);
            Assert.Equal(new string('e', 64), inputs.LastHashes["x.rvt"]);
        }

        [Fact]
        public void R377_the_record_lists_the_hash_of_each_prototype_file_for_the_reload_of_the_next_run()
        {
            Run run = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses);
            Apply(run, Context());
            JObject record = JObject.Parse(File.ReadAllText(Directory.GetFiles(work, "place_012.json*").Single()));
            Assert.Equal(new string('a', 64), (string)record["prototypes"][Path.Combine(prototypes, "VD4.rvt")]);
            Assert.Equal(VillaModes.Masses, (string)record["mode"]);
            Assert.Equal(new[] { "101" }, record["placed"].Select(token => (string)token));
            Assert.Equal(new Dictionary<string, string> { [Path.Combine(prototypes, "VD4.rvt")] = new string('a', 64) }, PlaceVillasRun.Hashes(record));
        }

        private const string FflGuid = "f0000000-0000-0000-0000-000000000000";
        private const string StepsGuid = "f1000000-0000-0000-0000-000000000000";
        private const string NameGuid = "f2000000-0000-0000-0000-000000000000";

        private PlanPlot LevelPlot(string number, double ffl)
        {
            PlanPlot plot = Plot(number, 1100, 0, false);
            plot.Insert.Value[2] = ffl;
            plot.Levels.Ffl.Value = ffl;
            plot.Parameters[Placer.FflParameter] = new Sourced<string> { Value = ffl.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) };
            plot.Parameters[StepsName] = new Sourced<string> { Value = "3" };
            plot.Parameters[RvtName] = new Sourced<string> { Value = number + " | VD4" };
            return plot;
        }

        private const string StepsName = "RRE_ZZ_SP_Number-of-Steps";
        private const string RvtName = "RRE_ZZ_SP_RVT-Name";

        private Run LevelRun(PlanFile plan, string mode, FakeVillas document = null, ICollection<string> others = null)
        {
            var guids = new Dictionary<string, string> { [Placer.FflParameter] = FflGuid, [StepsName] = StepsGuid, [RvtName] = NameGuid };
            Run run = Make(plan, mode, document, guids: guids, others: others);
            run.Document.Names[FflGuid] = Placer.FflParameter;
            run.Document.Names[StepsGuid] = StepsName;
            run.Document.Names[NameGuid] = RvtName;
            return run;
        }

        // W6. Button 7 places each link with its Z at the FFL of the plan and writes the level parameters with the others
        [Fact]
        public void R377_W6_a_link_stands_at_the_ffl_of_the_plan_and_writes_prototype_ffl_steps_and_rvt_name()
        {
            Run run = LevelRun(Plan(LevelPlot("101", 612.345)), VillaModes.Links);
            IReadOnlyList<string> issues = Apply(run, Context());
            FakeElement link = run.Document.Elements.Single();
            Assert.Equal(612.345, link.Steps.Move[2], 9);
            Assert.Equal(new[] { 612.345 }, link.Tag.Placed.Values["z"]);
            Assert.Equal("612.345", link.Parameters[Placer.FflParameter]);
            Assert.Equal("3", link.Parameters[StepsName]);
            Assert.Equal("101 | VD4", link.Parameters[RvtName]);
            Assert.DoesNotContain(issues, line => line.Contains(Placer.FflParameter));
        }

        [Fact]
        public void R377_W6_the_z_is_read_back_within_1_mm_on_links_and_on_masses()
        {
            foreach (string mode in VillaModes.All)
            {
                var document = new FakeVillas { OffReadBackZ = 0.002 };
                Run run = LevelRun(Plan(LevelPlot("101", 612.345)), mode, document);
                var error = Assert.ThrowsAny<Exception>(() => Apply(run, Context(mode)));
                Assert.Contains("plot 101", error.Message);
                var near = new FakeVillas { OffReadBackZ = 0.0009 };
                Apply(LevelRun(Plan(LevelPlot("101", 612.345)), mode, near), Context(mode + "near"));
                Assert.Equal(612.345, near.Elements.Single().Steps.Move[2], 9);
            }
        }

        // W6. A document whose survey point has a shared elevation moves the internal Z by the same offset as the floors of button 5
        [Fact]
        public void R377_W6_the_internal_z_is_the_ffl_less_the_shared_elevation_of_the_survey_point()
        {
            var document = new FakeVillas { SharedZ = 600.0 };
            PlanFile plan = Plan(LevelPlot("101", 612.345));
            plan.SurveyPoint.SharedPosition = new[] { 1000.0, 2000.0, 600.0 };
            Apply(LevelRun(plan, VillaModes.Links, document), Context());
            Assert.Equal(12.345, document.Elements.Single().Steps.Move[2], 9);
        }

        [Fact]
        public void R377_W6_a_plot_with_no_ffl_or_an_insert_z_off_its_ffl_is_skipped_and_listed()
        {
            PlanPlot none = LevelPlot("101", 612.345);
            none.Levels.Ffl = null;
            PlanPlot off = LevelPlot("102", 612.345);
            off.Insert.Value[2] = 612.5;
            Run run = LevelRun(Plan(none, off, LevelPlot("103", 600.0)), VillaModes.Links);
            IReadOnlyList<string> issues = Apply(run, Context());
            Assert.Contains(issues, line => line.Contains("plot 101") && line.Contains("levels.ffl"));
            Assert.Contains(issues, line => line.Contains("plot 102") && line.Contains("insert Z"));
            Assert.Equal(new[] { "103" }, run.Document.Elements.Select(element => element.Tag.Key.Plot));
        }

        // W6. A villa a 5c run placed at Z 0 holds no z in its tag, so a run of 5d moves it to the FFL in place, never as changed by hand
        [Fact]
        public void R377_W6_a_villa_of_5c_at_z_0_is_moved_to_the_ffl_in_place()
        {
            foreach (string mode in VillaModes.All)
            {
                Run first = LevelRun(Plan(LevelPlot("101", 612.345)), mode);
                Apply(first, Context("r1" + mode));
                FakeElement placed = first.Document.Elements.Single();
                placed.Steps = new PlacementSteps { Move = new[] { placed.Steps.Move[0], placed.Steps.Move[1], 0.0 }, RotationRad = placed.Steps.RotationRad, MirrorNormal = placed.Steps.MirrorNormal };
                PlacedTag tag = placed.Tag;
                placed.Tag = new PlacedTag(tag.Version, tag.Key, tag.Run, tag.Profile, tag.PlacedId, new PlacedValues(tag.Placed.Values.Where(item => item.Key != "z").ToDictionary(item => item.Key, item => item.Value.ToArray())));
                Run second = LevelRun(Plan(LevelPlot("101", 612.345)), mode, first.Document);
                Assert.Contains("move 1", second.Body.Body().Preview(Context("r2" + mode)).Counts);
                Apply(second, Context("r2" + mode));
                Assert.Equal(placed.UniqueId, first.Document.Elements.Single().UniqueId);
                Assert.Equal(612.345, first.Document.Elements.Single().Steps.Move[2], 9);
            }
        }
    }

    internal sealed class FakeElement
    {
        public string UniqueId;
        public bool IsSolid;
        public string TypeKey;
        public string PlotNumber;
        public PlacedTag Tag;
        public string Name;
        public PlacementSteps Steps;
        public IReadOnlyList<double[]> Shape;
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();
    }

    internal sealed class FakeVillas : IVillaDocument
    {
        public readonly List<FakeElement> Elements = new List<FakeElement>();
        public readonly List<VillaLinkType> Types = new List<VillaLinkType>();
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Loaded = new List<string>();
        public string Refuse;
        public bool MissingOnSolids;
        public double? OffReadBack;
        public double OffReadBackZ;
        public double SharedZ;
        public readonly Dictionary<string, string> Names = new Dictionary<string, string>();
        public int Reshaped;
        private int next;

        public string Title => "site.rvt";

        public SurveyReading Survey()
        {
            return new SurveyReading { Position = new[] { 0.0, 0.0, 0.0 }, SharedPosition = new[] { 1000.0, 2000.0, SharedZ }, AngleToTrueNorthDeg = 0 };
        }

        public IReadOnlyList<VillaLinkType> LinkTypes() => Types.ToList();

        public const string PlotGuid = "a0000000-0000-0000-0000-000000000000";

        public IReadOnlyList<VillaElement> Villas(string plotNumberGuid)
        {
            Assert.Equal(PlotGuid, plotNumberGuid);
            return Elements.Select(element => new VillaElement
            {
                UniqueId = element.UniqueId, IsSolid = element.IsSolid, Tag = element.Tag, PlotNumber = element.PlotNumber,
                Transform = element.IsSolid ? null : Transform(element.Steps),
                Base = element.IsSolid ? Base(element) : null,
            }).ToList();
        }

        public bool FileExists(string path) => File.Exists(path);

        public string LoadType(string key)
        {
            Loaded.Add("load " + key);
            Types.Single(type => type.Key == key).Loaded = true;
            return null;
        }

        public string ReloadType(string key)
        {
            Loaded.Add("reload " + key);
            return null;
        }

        public string CreateType(string fullPath)
        {
            Created.Add(fullPath);
            string key = "type" + Created.Count;
            Types.Add(new VillaLinkType { FullPath = fullPath, Key = key });
            return key;
        }

        public string PlaceLink(string typeKey, PlacementSteps steps)
        {
            var element = new FakeElement { UniqueId = "e" + next++, TypeKey = typeKey, Steps = steps };
            Elements.Add(element);
            return element.UniqueId;
        }

        public string PlaceSolid(IReadOnlyList<double[]> shape, double heightM, PlacementSteps steps)
        {
            var element = new FakeElement { UniqueId = "e" + next++, IsSolid = true, Steps = steps, Shape = shape };
            Elements.Add(element);
            return element.UniqueId;
        }

        public void MoveLink(string uniqueId, PlacementSteps steps)
        {
            Find(uniqueId).Steps = steps;
        }

        public void ReshapeSolid(string uniqueId, IReadOnlyList<double[]> shape, double heightM, PlacementSteps steps)
        {
            Reshaped++;
            Find(uniqueId).Steps = steps;
        }

        public void Delete(string uniqueId)
        {
            Elements.Remove(Find(uniqueId));
        }

        public PlacedTransform ReadBackLink(string uniqueId)
        {
            FakeElement element = Find(uniqueId);
            PlacedTransform transform = Transform(element.Steps);
            if (OffReadBack.HasValue && Math.Abs(element.Steps.Move[0] - OffReadBack.Value) < 1e-9)
            {
                transform.Origin = new[] { transform.Origin[0] + 0.01, transform.Origin[1], transform.Origin[2] };
            }
            transform.Origin = new[] { transform.Origin[0], transform.Origin[1], transform.Origin[2] + OffReadBackZ };
            return transform;
        }

        public IReadOnlyList<double[]> ReadBackBase(string uniqueId)
        {
            return Base(Find(uniqueId)).Select(point => new[] { point[0], point[1], point[2] + OffReadBackZ }).ToList();
        }

        public ParameterWrite Write(string uniqueId, string guid, string value)
        {
            FakeElement element = Find(uniqueId);
            string name = guid == PlotGuid ? LinkFields.PlotNumber : guid == null ? null : Names.TryGetValue(guid, out string named) ? named : "CASE";
            if (name == null)
            {
                return ParameterWrite.Missing;
            }
            if (name == Refuse)
            {
                return ParameterWrite.Refused;
            }
            if (element.IsSolid && MissingOnSolids)
            {
                return ParameterWrite.Missing;
            }
            element.Parameters[name] = value;
            if (name == LinkFields.PlotNumber && !element.IsSolid)
            {
                element.PlotNumber = value;
            }
            return ParameterWrite.Written;
        }

        public ParameterWrite WriteName(string uniqueId, string value)
        {
            Find(uniqueId).Name = value;
            return ParameterWrite.Written;
        }

        public void Tag(string uniqueId, PlacedTag tag)
        {
            Find(uniqueId).Tag = tag;
        }

        private FakeElement Find(string uniqueId) => Elements.Single(element => element.UniqueId == uniqueId);

        private static IReadOnlyList<double[]> Base(FakeElement element)
        {
            double turn = element.Steps.RotationRad * 180 / Math.PI;
            double z = element.Steps.Move.Length > 2 ? element.Steps.Move[2] : 0;
            return ShapeMath.Place(element.Shape, element.Steps.Move, turn, element.Steps.MirrorNormal != null).Select(point => new[] { point[0], point[1], z }).ToList();
        }

        private static PlacedTransform Transform(PlacementSteps steps)
        {
            double cos = Math.Cos(steps.RotationRad), sin = Math.Sin(steps.RotationRad);
            double flip = steps.MirrorNormal == null ? 1 : -1;
            return new PlacedTransform
            {
                Origin = steps.Move, AxisX = new[] { cos, sin, 0.0 }, AxisY = new[] { -sin * flip, cos * flip, 0.0 }, AxisZ = new[] { 0.0, 0.0, 1.0 },
            };
        }
    }
}
