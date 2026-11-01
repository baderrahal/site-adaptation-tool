using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CadReader;
using CSMath;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Tags;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R450 to R453, FIX-1NOV-B. The mass file of each prototype, its base by the wall ring or the slab, the masses placed as links and
    // the footprint check by the house outline. A fake maker and a fake document, so these run on net8.0 too
    public class R450_MassFilesTests : IDisposable
    {
        private readonly string work = Path.Combine(Path.GetTempPath(), "r450_" + Guid.NewGuid().ToString("N"));
        private readonly string prototypes;
        private readonly string masses;

        public R450_MassFilesTests()
        {
            prototypes = Path.Combine(work, "Prototypes");
            masses = Path.Combine(work, "masses");
            Directory.CreateDirectory(prototypes);
            File.WriteAllText(Path.Combine(prototypes, "VD4.rvt"), "prototype");
        }

        public void Dispose()
        {
            Directory.Delete(work, true);
        }

        private static readonly double[][] Slab = { new[] { 0.0, 0.0 }, new[] { 8.0, 0.0 }, new[] { 8.0, 15.0 }, new[] { 0.0, 15.0 } };

        // The outer face of the walls 0.6 m outside the slab and their inner face on it, the two loops of a closed ring
        private static readonly double[][] Outer = { new[] { -0.6, -0.6 }, new[] { 8.6, -0.6 }, new[] { 8.6, 15.6 }, new[] { -0.6, 15.6 } };

        private static double[][] Strip(double x0, double y0, double x1, double y1)
        {
            return new[] { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } };
        }

        private sealed class FakeMaker : IMassFileMaker
        {
            public List<IReadOnlyList<double[]>> Loops = new List<IReadOnlyList<double[]>> { Outer, Slab };
            public int Reads;
            public readonly List<string> Written = new List<string>();

            public MassBaseRead ReadBase(string prototypePath, LightModeConfig config)
            {
                Reads++;
                return new MassBaseRead { WallLoops = Loops, Slab = Slab };
            }

            public void Write(string massPath, IReadOnlyList<double[]> ring, double heightM)
            {
                Written.Add(massPath);
                File.WriteAllText(massPath, "mass " + ring.Count + " " + Guid.NewGuid().ToString("N"));
            }
        }

        private sealed class SlabReader : IBaseShapeReader
        {
            public IReadOnlyList<double[]> Read(string prototypePath, LightModeConfig config) => Slab;
        }

        private JObject Block(string linkFolder, string massFolder = null, bool houses = true)
        {
            var block = new JObject
            {
                ["villaModeDefault"] = "masses", ["solidHeightM"] = 3.0, ["baseBoundary"] = "lowestFloor", ["baseBoundaryFloorType"] = null,
                ["footprintLimitMm"] = 100.0, ["footprintSampleStepMm"] = 10.0,
                ["linkFolder"] = linkFolder, ["massFolder"] = massFolder ?? masses, ["wallCutHeightM"] = 1.0,
                ["wallRingMinShare"] = 0.9, ["wallRingMaxShare"] = 1.3,
            };
            if (houses)
            {
                block["houseLayer"] = "0";
                block["houseMinAreaM2"] = 60.0;
            }
            return block;
        }

        private LightModeConfig Config(string linkFolder, string massFolder = null, bool houses = true)
        {
            return LightModeConfig.From(new JObject { ["addin"] = new JObject { ["lightMode"] = Block(linkFolder, massFolder, houses) } });
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
                Parameters = new Dictionary<string, Sourced<string>> { [LinkFields.PlotNumber] = new Sourced<string> { Value = number } },
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

        private sealed class Run
        {
            public FakeVillas Document;
            public PlaceVillasRun Body;
            public FakeMaker Maker = new FakeMaker();
        }

        private Run Make(PlanFile plan, string mode, LightModeConfig config, FakeVillas document = null, Dictionary<string, string> lastHashes = null, List<double[][]> footprint = null)
        {
            var run = new Run { Document = document ?? new FakeVillas() };
            Func<RunContext, VillaInputs> load = context => new VillaInputs
            {
                Plan = plan, PlanPath = Path.Combine(work, "plan_012.json"), Config = config, Mode = mode, Profile = "parsons2024Rev6",
                ParameterGuids = new Dictionary<string, string> { [LinkFields.PlotNumber] = FakeVillas.PlotGuid },
                Footprint = footprint ?? new List<double[][]>(), LastHashes = lastHashes ?? new Dictionary<string, string>(),
            };
            run.Body = new PlaceVillasRun(run.Document, load, context => new ShapeCache(context.WorkFolder, new SlabReader(), FileHash.Sha256), FileHash.Sha256, lines => true,
                context => new MassFileSet(run.Maker, FileHash.Sha256, () => work));
            return run;
        }

        private RunContext Context(string runId)
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

        [Fact]
        public void R451_a_wall_ring_round_the_slab_is_the_mass_base_with_every_corner()
        {
            double[][] lShape = { new[] { -0.6, -0.6 }, new[] { 8.6, -0.6 }, new[] { 8.6, 8.0 }, new[] { 6.0, 8.0 }, new[] { 6.0, 15.6 }, new[] { -0.6, 15.6 } };
            double[][] lSlab = { new[] { 0.0, 0.0 }, new[] { 8.0, 0.0 }, new[] { 8.0, 7.4 }, new[] { 5.4, 7.4 }, new[] { 5.4, 15.0 }, new[] { 0.0, 15.0 } };
            MassBase mass = WallRing.Choose(new List<IReadOnlyList<double[]>> { lSlab, lShape }, lSlab, 0.9, 1.3);
            Assert.Equal(WallRing.FromWalls, mass.Source);
            Assert.False(mass.Flagged);
            Assert.Equal(6, mass.Ring.Count);
            Assert.Equal(new[] { 6.0, 8.0 }, mass.Ring[3]);
        }

        [Fact]
        public void R451_walls_that_do_not_close_into_a_ring_give_the_largest_slab_flagged()
        {
            var strips = new List<IReadOnlyList<double[]>> { Strip(-0.6, -0.6, 8.6, 0.0), Strip(-0.6, 0.0, 0.0, 15.6) };
            MassBase open = WallRing.Choose(strips, Slab, 0.9, 1.3);
            Assert.Equal(WallRing.FromSlab, open.Source);
            Assert.True(open.Flagged);
            Assert.Contains("middle of the slab", open.Reason);
            Assert.Equal(Slab.Length, open.Ring.Count);

            MassBase none = WallRing.Choose(new List<IReadOnlyList<double[]>>(), Slab, 0.9, 1.3);
            Assert.Equal(WallRing.FromSlab, none.Source);
            Assert.Contains("no exterior wall", none.Reason);
        }

        [Fact]
        public void R451_a_ring_under_min_share_of_the_slab_area_gives_the_slab()
        {
            MassBase small = WallRing.Choose(new List<IReadOnlyList<double[]>> { Strip(2.0, 5.0, 6.0, 10.0) }, Slab, 0.9, 1.3);
            Assert.Equal(WallRing.FromSlab, small.Source);
            Assert.Contains("minShare", small.Reason);
            Assert.Equal(WallRing.FromWalls, WallRing.Choose(new List<IReadOnlyList<double[]>> { Strip(0.1, 0.1, 7.9, 14.9) }, Slab, 0.9, 1.3).Source);
        }

        [Fact]
        public void R450_the_mass_keys_read_from_config_and_a_bad_link_folder_is_refused()
        {
            MassFileConfig config = Config(LinkFolders.Masses, "{workRoot}\\masses").MassFiles;
            Assert.True(config.AsLinks);
            Assert.Equal(1.0, config.WallCutHeightM);
            Assert.Equal(0.9, config.WallRingMinShare);
            Assert.Equal(Path.Combine(work, "masses"), config.FolderFor(prototypes, () => work));
            Assert.Equal(prototypes, Config(LinkFolders.Prototypes).MassFiles.FolderFor(prototypes, () => work));
            Assert.False(Config(LinkFolders.Solids).MassFiles.AsLinks);

            JObject old = Block(LinkFolders.Masses);
            old.Remove("linkFolder");
            Assert.Null(LightModeConfig.From(new JObject { ["addin"] = new JObject { ["lightMode"] = old } }).MassFiles);

            Assert.Contains("linkFolder", Assert.Throws<InvalidDataException>(() => Config("elsewhere")).Message);
            JObject share = Block(LinkFolders.Masses);
            share["wallRingMinShare"] = 1.5;
            Assert.Contains("wallRingMinShare", Assert.Throws<InvalidDataException>(() => LightModeConfig.From(new JObject { ["addin"] = new JObject { ["lightMode"] = share } })).Message);
        }

        [Fact]
        public void R450_a_mass_file_is_named_as_its_prototype_with_a_sidecar_of_hash_version_source_and_points()
        {
            string prototype = Path.Combine(prototypes, "VD4.rvt");
            var maker = new FakeMaker();
            var set = new MassFileSet(maker, FileHash.Sha256, () => work);
            LightModeConfig config = Config(LinkFolders.Masses);
            Assert.True(set.State(prototype, config).Stale);

            MassBase made = set.Make(prototype, config);

            string mass = Path.Combine(masses, "VD4.rvt");
            Assert.Equal(new[] { mass }, maker.Written);
            JObject sidecar = JObject.Parse(File.ReadAllText(mass + ".json"));
            Assert.Equal(FileHash.Sha256(prototype), (string)sidecar["prototypeSha256"]);
            Assert.Equal(MassFiles.ReaderVersion, (int)sidecar["readerVersion"]);
            Assert.Equal(WallRing.FromWalls, (string)sidecar["source"]);
            Assert.Equal(4, ((JArray)sidecar["points"]).Count);
            Assert.Equal(WallRing.FromWalls, made.Source);
            MassState state = set.State(prototype, config);
            Assert.False(state.Stale);
            Assert.Equal(Outer.Select(point => point[0]), state.Base.Ring.Select(point => point[0]));
            Assert.Equal("prototype", File.ReadAllText(prototype));
        }

        [Fact]
        public void R450_a_mass_is_made_again_when_the_prototype_hash_the_reader_version_or_the_source_keys_change()
        {
            string key = MassFiles.SourceKey(Config(LinkFolders.Masses));
            JObject fresh = MassFiles.Sidecar("VD4.rvt", "aa", key, new MassBase(Slab, WallRing.FromSlab, "test"));
            Assert.False(MassFiles.Stale(fresh, "AA", key));
            Assert.True(MassFiles.Stale(fresh, "bb", key));
            Assert.True(MassFiles.Stale(fresh, "aa", key + " other"));
            JObject older = (JObject)fresh.DeepClone();
            older["readerVersion"] = MassFiles.ReaderVersion - 1;
            Assert.True(MassFiles.Stale(older, "aa", key));
            Assert.True(MassFiles.Stale(null, "aa", key));
        }

        [Fact]
        public void R450_a_mass_folder_on_the_prototype_folder_is_refused_and_the_prototype_kept()
        {
            string prototype = Path.Combine(prototypes, "VD4.rvt");
            var maker = new FakeMaker();
            var error = Assert.Throws<InvalidDataException>(() => new MassFileSet(maker, FileHash.Sha256, () => work).Make(prototype, Config(LinkFolders.Masses, prototypes)));
            Assert.Contains("overwrite", error.Message);
            Assert.Empty(maker.Written);
            Assert.Equal("prototype", File.ReadAllText(prototype));
        }

        [Fact]
        public void R452_masses_place_one_link_per_plot_of_one_type_per_mass_file_tagged_mass()
        {
            Run run = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 90, true)), VillaModes.Masses, Config(LinkFolders.Masses));
            Assert.Contains("mass files to make 1", run.Body.Body().Preview(Context("p")).Counts);
            Apply(run, Context("r1"));
            string mass = Path.Combine(masses, "VD4.rvt");
            Assert.Equal(new[] { mass }, run.Document.Created);
            Assert.Equal(1, run.Maker.Reads);
            Assert.Equal(2, run.Document.Elements.Count);
            Assert.All(run.Document.Elements, element => Assert.False(element.IsSolid));
            Assert.All(run.Document.Elements, element => Assert.Equal("type1", element.TypeKey));
            Assert.All(run.Document.Elements, element => Assert.Equal(PlaceVillasRun.MassPart, element.Tag.Key.Part));
            FakeElement mirrored = run.Document.Elements.Single(element => element.Tag.Key.Plot == "102");
            Assert.NotNull(mirrored.Steps.MirrorNormal);
            Assert.Equal(new[] { 90.0 }, mirrored.Tag.Placed.Values["rotationDeg"]);
            Assert.Equal("102", mirrored.Parameters[LinkFields.PlotNumber]);
            // A second run with nothing changed makes no mass and keeps both links
            Run again = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 90, true)), VillaModes.Masses, Config(LinkFolders.Masses), run.Document);
            Assert.Contains("already placed 2", again.Body.Body().Preview(Context("p2")).Counts);
            Assert.Equal(0, again.Maker.Reads);
        }

        [Fact]
        public void R452_a_solid_of_an_earlier_masses_run_is_swapped_for_the_mass_link()
        {
            Run solids = Make(Plan(Plot("101", 1100, 30, true)), VillaModes.Masses, Config(LinkFolders.Solids));
            Apply(solids, Context("r1"));
            Assert.True(solids.Document.Elements.Single().IsSolid);
            Run links = Make(Plan(Plot("101", 1100, 30, true)), VillaModes.Masses, Config(LinkFolders.Masses), solids.Document);
            Assert.Contains("swap 1", links.Body.Body().Preview(Context("p")).Counts);
            Apply(links, Context("r2"));
            FakeElement link = solids.Document.Elements.Single();
            Assert.False(link.IsSolid);
            Assert.Equal(PlaceVillasRun.MassPart, link.Tag.Key.Part);
            Assert.Equal(new[] { 30.0 }, link.Tag.Placed.Values["rotationDeg"]);
        }

        [Fact]
        public void R452_a_mass_link_is_swapped_for_a_solid_when_link_folder_is_solids_again()
        {
            Run links = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses));
            Apply(links, Context("r1"));
            Run solids = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(LinkFolders.Solids), links.Document);
            Assert.Contains("swap 1", solids.Body.Body().Preview(Context("p")).Counts);
            Apply(solids, Context("r2"));
            FakeElement solid = links.Document.Elements.Single();
            Assert.True(solid.IsSolid);
            Assert.Equal(PlaceVillasRun.MassPart, solid.Tag.Key.Part);
        }

        [Fact]
        public void R452_a_mass_file_made_again_reloads_its_type_so_every_instance_follows()
        {
            Run first = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses));
            Apply(first, Context("r1"));
            string mass = Path.Combine(masses, "VD4.rvt");
            var last = new Dictionary<string, string> { [mass] = FileHash.Sha256(mass) };
            File.WriteAllText(Path.Combine(prototypes, "VD4.rvt"), "prototype changed");
            Run second = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses), first.Document, last);
            Apply(second, Context("r2"));
            Assert.Single(second.Maker.Written);
            Assert.Equal(new[] { "reload type1" }, first.Document.Loaded);
            Assert.Equal(2, first.Document.Elements.Count);
        }

        [Fact]
        public void R452_link_folder_prototypes_or_a_folder_links_those_files_with_no_code_change()
        {
            Run run = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(LinkFolders.Prototypes));
            Apply(run, Context("r1"));
            Assert.Equal(new[] { Path.Combine(prototypes, "VD4.rvt") }, run.Document.Created);
            Assert.Equal(0, run.Maker.Reads);

            string keo = Path.Combine(work, "KEO");
            Directory.CreateDirectory(keo);
            File.WriteAllText(Path.Combine(keo, "VD4.rvt"), "keo");
            Run other = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(keo));
            Apply(other, Context("r2"));
            Assert.Equal(new[] { Path.Combine(keo, "VD4.rvt") }, other.Document.Created);
        }

        [Fact]
        public void R452_links_mode_swaps_a_mass_link_for_its_real_link()
        {
            Run masses = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses));
            Apply(masses, Context("r1"));
            Run links = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links, Config(LinkFolders.Masses), masses.Document);
            Assert.Contains("swap 1", links.Body.Body().Preview(Context("p")).Counts);
            Apply(links, Context("r2"));
            FakeElement link = masses.Document.Elements.Single();
            Assert.Equal("", link.Tag.Key.Part);
            Assert.Equal("type2", link.TypeKey);
            Assert.Equal(Path.Combine(prototypes, "VD4.rvt"), masses.Document.Created[1]);
        }

        [Fact]
        public void R452_R451_a_slab_base_flags_its_prototype_in_the_run_lines_and_the_footprint_file()
        {
            Run run = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses));
            run.Maker.Loops = new List<IReadOnlyList<double[]>> { Strip(-0.6, -0.6, 8.6, 0.0) };
            IReadOnlyList<string> issues = Apply(run, Context("r1"));
            Assert.Contains(issues, line => line.Contains("prototype VD4.rvt") && line.Contains("Q279"));
            JObject footprint = JObject.Parse(File.ReadAllText(Directory.GetFiles(work, "footprint_012.json*").Single()));
            Assert.Equal(WallRing.FromSlab, (string)footprint["masses"][0]["source"]);
            Assert.True((bool)footprint["masses"][0]["flagged"]);
        }

        private static JArray Points(double[][] ring)
        {
            return new JArray(ring.Select(point => new JArray(point[0], point[1])));
        }

        [Fact]
        public void R453_the_footprint_takes_the_house_ring_on_the_house_layer_over_the_least_area()
        {
            double[][] envelope = Strip(0, 0, 12, 25);
            double[][] house = Strip(1, 3, 11, 17.06);
            var read = new JObject
            {
                ["blocks"] = new JArray(new JObject
                {
                    ["name"] = "VL4", ["outline"] = Points(envelope),
                    ["rings"] = new JArray(
                        new JObject { ["layer"] = "0", ["points"] = Points(house) },
                        new JObject { ["layer"] = "0", ["points"] = Points(Strip(1, 18, 7, 24)) },
                        new JObject { ["layer"] = "Defpoints", ["points"] = Points(envelope) }),
                }),
            };
            List<double[][]> houses = FootprintCheck.FromRead(read, Config(LinkFolders.Masses));
            Assert.Single(houses);
            Assert.Equal(house.Select(point => point[1]), houses[0].Select(point => point[1]));
            List<double[][]> old = FootprintCheck.FromRead(read, Config(LinkFolders.Masses, houses: false));
            Assert.Equal(envelope.Select(point => point[0]), old.Single().Select(point => point[0]));
        }

        [Fact]
        public void R453_the_footprint_file_holds_the_largest_gap_and_the_count_over_the_limit_per_prototype()
        {
            LightModeConfig config = Config(LinkFolders.Masses);
            var houses = new List<double[][]> { Strip(0, 0, 10, 14), Strip(100, 0, 110, 14) };
            var rows = new List<FootprintRow>
            {
                FootprintCheck.Row("101", Strip(0, 0, 10, 14), houses, config, "VL4.rvt"),
                FootprintCheck.Row("102", Strip(100.6, 0, 110.6, 14), houses, config, "VL4.rvt"),
                FootprintCheck.Row("201", Strip(0.05, 0, 10.05, 14), houses, config, "DP4.rvt"),
            };
            JObject json = FootprintCheck.Json(rows);
            JObject vl4 = (JObject)json["prototypes"].Single(item => (string)item["prototype"] == "VL4.rvt");
            Assert.Equal(600.0, (double)vl4["largestGapMm"], 3);
            Assert.Equal(1, (int)vl4["overLimit"]);
            Assert.Equal(2, (int)vl4["plots"]);
            Assert.Equal(0, (int)json["prototypes"].Single(item => (string)item["prototype"] == "DP4.rvt")["overLimit"]);
            Assert.Contains(FootprintCheck.SummaryLines(rows), line => line.Contains("VL4.rvt") && line.Contains("600 mm") && line.Contains("1 of 2"));
        }

        private JObject Record(string runId)
        {
            return JObject.Parse(File.ReadAllText(Directory.GetFiles(work, "place_012.json*").Single(path => path.EndsWith(runId, StringComparison.Ordinal))));
        }

        // Fix pass finding 1 (a). A mass link stands where the run now links the prototypes, so its transform alone would keep it
        [Fact]
        public void R452_masses_then_prototypes_on_one_document_swap_each_mass_link_for_the_prototype_link()
        {
            Run masses = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses));
            Apply(masses, Context("r1"));
            Run protos = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(LinkFolders.Prototypes), masses.Document);
            Assert.Contains("swap 1", protos.Body.Body().Preview(Context("p")).Counts);
            Apply(protos, Context("r2"));
            FakeElement link = masses.Document.Elements.Single();
            Assert.Equal(Path.Combine(prototypes, "VD4.rvt"), masses.Document.Types.Single(type => type.Key == link.TypeKey).FullPath);
            Assert.Equal(PlaceVillasRun.MassPart, link.Tag.Key.Part);
        }

        // Fix pass finding 1 (b). The plan gives the plot another prototype, so the mass and the link of the old one are swapped
        [Fact]
        public void R452_a_plot_whose_prototype_changes_in_the_plan_swaps_its_mass_and_its_link()
        {
            File.WriteAllText(Path.Combine(prototypes, "VL4.rvt"), "other prototype");
            foreach (string mode in VillaModes.All)
            {
                var document = new FakeVillas();
                Apply(Make(Plan(Plot("101", 1100, 0, false)), mode, Config(LinkFolders.Masses), document), Context("r1" + mode));
                PlanPlot changed = Plot("101", 1100, 0, false);
                changed.PrototypeFile.Value = "VL4.rvt";
                Run second = Make(Plan(changed), mode, Config(LinkFolders.Masses), document);
                Assert.Contains("swap 1", second.Body.Body().Preview(Context("p" + mode)).Counts);
                Apply(second, Context("r2" + mode));
                FakeElement link = document.Elements.Single();
                Assert.Equal("VL4.rvt", Path.GetFileName(document.Types.Single(type => type.Key == link.TypeKey).FullPath));
            }
        }

        // Breaker 2 of FIX-1NOV-B. A mass link moved by hand keeps where it stands when its plot changes prototype, as R368 leaves a hand
        // change, and the run says it was changed by hand
        [Fact]
        public void R452_R368_a_hand_moved_mass_link_whose_plot_changes_prototype_is_swapped_where_it_stands()
        {
            File.WriteAllText(Path.Combine(prototypes, "VL4.rvt"), "other prototype");
            var document = new FakeVillas();
            Apply(Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses), document), Context("r1"));
            FakeElement old = document.Elements.Single();
            double[] hand = { old.Steps.Move[0] + 3.0, old.Steps.Move[1] - 2.0, old.Steps.Move[2] };
            old.Steps = PlacementMath.Steps(hand, 15, false);
            PlanPlot changed = Plot("101", 1100, 0, false);
            changed.PrototypeFile.Value = "VL4.rvt";
            Run second = Make(Plan(changed), VillaModes.Masses, Config(LinkFolders.Masses), document);
            Assert.Contains("swap 1", second.Body.Body().Preview(Context("p")).Counts);
            IReadOnlyList<string> lines = Apply(second, Context("r2"));
            FakeElement link = document.Elements.Single();
            Assert.NotEqual(old.UniqueId, link.UniqueId);
            Assert.Equal("VL4.rvt", Path.GetFileName(document.Types.Single(type => type.Key == link.TypeKey).FullPath));
            Assert.Equal(hand, link.Steps.Move);
            Assert.Equal(15, link.Steps.RotationRad * 180 / Math.PI, 9);
            Assert.Contains(lines, line => line.StartsWith("plot 101: changed by hand in ", StringComparison.Ordinal));
            // The run after reads the new link as changed by hand and leaves it, it swaps nothing
            RunSummary third = Make(Plan(changed), VillaModes.Masses, Config(LinkFolders.Masses), document).Body.Body().Preview(Context("p3"));
            Assert.Contains("swap 0, move 0, already placed 0, skip 1", third.Counts);
            Assert.Contains(third.PerPlot, line => line.StartsWith("plot 101: changed by hand in ", StringComparison.Ordinal) && line.EndsWith("left as it is", StringComparison.Ordinal));
        }

        // Breaker 2 of FIX-1NOV-B. Every tagged link of the plot that shows the old file is swapped, none is kept as placed
        [Fact]
        public void R452_two_tagged_links_of_one_plot_with_the_old_file_are_both_swapped()
        {
            File.WriteAllText(Path.Combine(prototypes, "VL4.rvt"), "other prototype");
            var document = new FakeVillas();
            Apply(Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses), document), Context("r1"));
            FakeElement first = document.Elements.Single();
            document.Elements.Add(new FakeElement
            {
                UniqueId = "x1", TypeKey = first.TypeKey, PlotNumber = "101", Steps = first.Steps,
                Tag = new PlacedTag(first.Tag.Version, first.Tag.Key, first.Tag.Run, first.Tag.Profile, "x1", first.Tag.Placed),
            });
            PlanPlot changed = Plot("101", 1100, 0, false);
            changed.PrototypeFile.Value = "VL4.rvt";
            Run second = Make(Plan(changed), VillaModes.Masses, Config(LinkFolders.Masses), document);
            Assert.Contains("swap 1", second.Body.Body().Preview(Context("p")).Counts);
            Apply(second, Context("r2"));
            FakeElement link = document.Elements.Single();
            Assert.Equal("VL4.rvt", Path.GetFileName(document.Types.Single(type => type.Key == link.TypeKey).FullPath));
        }

        // Fix pass finding 2. The record of a Links run keeps the hash of the mass file it did not link, so the next Masses run reloads it
        [Fact]
        public void R452_a_mass_made_again_after_a_links_run_is_still_reloaded()
        {
            Run first = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses));
            Apply(first, Context("r1"));
            // The Links run takes plot 101 alone, so the mass of 102 stays and must follow the remade file
            Run links = Make(Plan(Plot("101", 1100, 0, false)), VillaModes.Links, Config(LinkFolders.Masses), first.Document, PlaceVillasRun.Hashes(Record("r1")));
            Apply(links, Context("r2"));
            File.WriteAllText(Path.Combine(prototypes, "VD4.rvt"), "prototype changed");
            Run again = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses), first.Document, PlaceVillasRun.Hashes(Record("r2")));
            Apply(again, Context("r3"));
            Assert.Single(again.Maker.Written);
            Assert.Contains("reload type1", first.Document.Loaded);
        }

        // Fix pass finding 3. A mass still to make has no base, so a dry run cannot say it lies within the limit
        [Fact]
        public void R453_a_dry_run_tells_the_masses_still_to_make_apart_from_those_within_the_footprint_limit()
        {
            Run run = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Masses, Config(LinkFolders.Masses));
            string counts = run.Body.Body().Preview(Context("p")).Counts;
            Assert.DoesNotContain("over the footprint limit 0,", counts);
            Assert.Contains("footprint not measured 2, the footprint check runs after the masses are made", counts);
        }

        // Fix pass finding 4. Every key the mass is made from is in its source key
        [Fact]
        public void R450_a_change_of_solid_height_or_base_boundary_makes_the_masses_again()
        {
            string prototype = Path.Combine(prototypes, "VD4.rvt");
            var set = new MassFileSet(new FakeMaker(), FileHash.Sha256, () => work);
            set.Make(prototype, Config(LinkFolders.Masses));
            Assert.False(set.State(prototype, Config(LinkFolders.Masses)).Stale);
            JObject taller = Block(LinkFolders.Masses);
            taller["solidHeightM"] = 4.0;
            Assert.True(set.State(prototype, LightModeConfig.From(new JObject { ["addin"] = new JObject { ["lightMode"] = taller } })).Stale);
            JObject typed = Block(LinkFolders.Masses);
            typed["baseBoundary"] = "floorType";
            typed["baseBoundaryFloorType"] = "Slab 200";
            Assert.True(set.State(prototype, LightModeConfig.From(new JObject { ["addin"] = new JObject { ["lightMode"] = typed } })).Stale);
            JObject wider = Block(LinkFolders.Masses);
            wider["wallRingMaxShare"] = 1.5;
            Assert.True(set.State(prototype, LightModeConfig.From(new JObject { ["addin"] = new JObject { ["lightMode"] = wider } })).Stale);
        }

        // Fix pass finding 5. A ring far over the slab, a garden wall of Exterior function in the union, is not the house
        [Fact]
        public void R451_a_wall_ring_over_max_share_of_the_slab_gives_the_slab()
        {
            MassBase big = WallRing.Choose(new List<IReadOnlyList<double[]>> { Strip(-5, -5, 13, 20) }, Slab, 0.9, 1.3);
            Assert.Equal(WallRing.FromSlab, big.Source);
            Assert.Contains("maxShare", big.Reason);
            Assert.Equal(WallRing.FromWalls, WallRing.Choose(new List<IReadOnlyList<double[]>> { Outer }, Slab, 0.9, 1.3).Source);
            Assert.Equal(1.3, Config(LinkFolders.Masses).MassFiles.WallRingMaxShare);
            JObject under = Block(LinkFolders.Masses);
            under["wallRingMaxShare"] = 0.5;
            Assert.Contains("wallRingMaxShare", Assert.Throws<InvalidDataException>(() => LightModeConfig.From(new JObject { ["addin"] = new JObject { ["lightMode"] = under } })).Message);
        }

        // Fix pass finding 6. The outline of a wall from its location line, the openings never cut it
        [Fact]
        public void R451_a_straight_wall_gives_its_full_width_from_the_exterior_face_extended_by_half_the_width_at_both_ends()
        {
            double[][] centre = WallStrips.Outline(new List<double[]> { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 } }, 0.3, 0, new[] { 0.0, -1.0 });
            Assert.Equal(new[] { -0.15, -0.15, 10.15, 0.15 }, Box(centre));
            Assert.Equal(0.3 * 10.3, RingMath.Area(centre), 9);
            double[][] face = WallStrips.Outline(new List<double[]> { new[] { 0.0, 0.0 }, new[] { 10.0, 0.0 } }, 0.3, WallStrips.LocationOffset(WallStrips.FinishFaceExterior, 0.3, null, -1, -1), new[] { 0.0, 1.0 });
            Assert.Equal(new[] { -0.15, -0.3, 10.15, 0.0 }, Box(face));
        }

        [Fact]
        public void R451_the_location_line_of_a_wall_is_held_from_its_centre_by_its_layers()
        {
            double[] layers = { 0.02, 0.2, 0.03 };
            Assert.Equal(0, WallStrips.LocationOffset(WallStrips.WallCenterline, 0.25, layers, 1, 1), 9);
            Assert.Equal(0.125, WallStrips.LocationOffset(WallStrips.FinishFaceExterior, 0.25, layers, 1, 1), 9);
            Assert.Equal(-0.125, WallStrips.LocationOffset(WallStrips.FinishFaceInterior, 0.25, layers, 1, 1), 9);
            Assert.Equal(0.105, WallStrips.LocationOffset(WallStrips.CoreExterior, 0.25, layers, 1, 1), 9);
            Assert.Equal(-0.095, WallStrips.LocationOffset(WallStrips.CoreInterior, 0.25, layers, 1, 1), 9);
            Assert.Equal(0.005, WallStrips.LocationOffset(WallStrips.CoreCenterline, 0.25, layers, 1, 1), 9);
        }

        [Fact]
        public void R451_an_arc_wall_tessellated_gives_one_mitred_outline_at_its_width()
        {
            var arc = Enumerable.Range(0, 9).Select(step => new[] { 5 * Math.Cos(step * Math.PI / 16), 5 * Math.Sin(step * Math.PI / 16) }).ToList();
            double[][] outline = WallStrips.Outline(arc, 0.2, 0, new[] { Math.Cos(Math.PI / 4), Math.Sin(Math.PI / 4) });
            Assert.Equal(18, outline.Length);
            Assert.Equal(0.2 * (16 * 5 * Math.Sin(Math.PI / 32) + 0.2), RingMath.Area(outline), 9);
            Assert.True(FaceRings.IsSimple(outline));
            double[] middle = outline[4];
            Assert.Equal(5.1, Math.Sqrt(middle[0] * middle[0] + middle[1] * middle[1]), 2);
        }

        private static double[] Box(double[][] ring)
        {
            return new[] { ring.Min(point => point[0]), ring.Min(point => point[1]), ring.Max(point => point[0]), ring.Max(point => point[1]) }.Select(value => Math.Round(value, 9)).ToArray();
        }

        // Fix pass finding 8. A footprint read of before R453 holds no rings, one line says so and no plot is flagged for it
        [Fact]
        public void R453_a_footprint_read_with_no_house_rings_says_read_data_again_once_and_flags_no_plot()
        {
            var read = new JObject { ["blocks"] = new JArray(new JObject { ["name"] = "VL4", ["outline"] = Points(Strip(1000, 1990, 1300, 2030)) }) };
            LightModeConfig config = Config(LinkFolders.Masses);
            List<double[][]> footprint = FootprintCheck.FromRead(read, config);
            Run run = Make(Plan(Plot("101", 1100, 0, false), Plot("102", 1200, 0, false)), VillaModes.Masses, config, footprint: footprint);
            Assert.Contains("footprint not checked, the footprint read has no house outlines", run.Body.Body().Preview(Context("p")).Counts);
            IReadOnlyList<string> issues = Apply(run, Context("r1"));
            Assert.Single(issues, line => line.Contains("the footprint read has no house outlines, run 2 Read data again"));
            Assert.DoesNotContain(issues, line => line.Contains("flagged (R404)"));
        }

        // Review of FIX-1NOV-B, one copy of each ring rule. Bit for bit on rings at site coordinates, where the order of the sums shows
        [Fact]
        public void R451_R453_ring_math_is_the_one_ring_area_point_test_and_centroid_of_floors_scan_and_plot_labels()
        {
            var random = new Random(451);
            for (int round = 0; round < 200; round++)
            {
                double east = 40000 + random.NextDouble() * 1000, north = 2.4e6 + random.NextDouble() * 1000;
                int corners = 3 + random.Next(10);
                List<double[]> ring = Enumerable.Range(0, corners).Select(index => index * 2 * Math.PI / corners)
                    .Select(turn => new[] { east + Math.Cos(turn) * (5 + random.NextDouble() * 20), north + Math.Sin(turn) * (5 + random.NextDouble() * 20) }).ToList();
                Assert.Equal(BitConverter.DoubleToInt64Bits(FloorMath.SignedArea(ring)), BitConverter.DoubleToInt64Bits(RingMath.Area(ring)));
                double[] centre = RingMath.Centroid(ring), labelled = PlotLabels.Centroid(ring);
                Assert.Equal(BitConverter.DoubleToInt64Bits(labelled[0]), BitConverter.DoubleToInt64Bits(centre[0]));
                Assert.Equal(BitConverter.DoubleToInt64Bits(labelled[1]), BitConverter.DoubleToInt64Bits(centre[1]));
                IEnumerable<double[]> points = ring.Concat(Enumerable.Range(0, 20).Select(index => new[] { east - 25 + random.NextDouble() * 50, north - 25 + random.NextDouble() * 50 }));
                foreach (double[] point in points)
                {
                    bool holds = RingMath.Holds(ring, point);
                    Assert.Equal(ScanLogic.Contains(ring, point[0], point[1]), holds);
                    Assert.Equal(PlotLabels.Contains(ring, point[0], point[1]), holds);
                }
            }
        }

        [Fact]
        public void R453_an_open_polyline_of_a_block_is_joined_into_a_ring_in_world_coordinates()
        {
            var document = new CadDocument();
            var block = new BlockRecord("VL4");
            document.BlockRecords.Add(block);
            block.Entities.Add(new LwPolyline(new[] { new LwPolyline.Vertex(new XY(0, 0)), new LwPolyline.Vertex(new XY(10, 0)), new LwPolyline.Vertex(new XY(10, 14)), new LwPolyline.Vertex(new XY(0, 14)) }));
            document.Entities.Add(new Insert(block) { InsertPoint = new XYZ(100, 200, 0) });

            CadExtract extract = CadExtractor.Extract(document, "dummy", null, null, null, new List<string>());

            BlockRing ring = extract.Blocks.Blocks.Single().Rings.Single();
            Assert.Equal("0", ring.Layer);
            Assert.Equal(140.0, ring.Area, 3);
            Assert.Equal(4, ring.Points.Count);
            Assert.Equal(new[] { 100.0, 214.0 }, ring.Points[3]);
            Assert.Null(CadExtractor.JoinedRing(new List<double[]> { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 0.0, 0.0 } }));
        }
    }
}
