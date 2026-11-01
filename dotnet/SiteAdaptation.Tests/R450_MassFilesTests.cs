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
                ["linkFolder"] = linkFolder, ["massFolder"] = massFolder ?? masses, ["wallCutHeightM"] = 1.0, ["wallRingMinShare"] = 0.9,
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
            MassBase mass = WallRing.Choose(new List<IReadOnlyList<double[]>> { lSlab, lShape }, lSlab, 0.9);
            Assert.Equal(WallRing.FromWalls, mass.Source);
            Assert.False(mass.Flagged);
            Assert.Equal(6, mass.Ring.Count);
            Assert.Equal(new[] { 6.0, 8.0 }, mass.Ring[3]);
        }

        [Fact]
        public void R451_walls_that_do_not_close_into_a_ring_give_the_largest_slab_flagged()
        {
            var strips = new List<IReadOnlyList<double[]>> { Strip(-0.6, -0.6, 8.6, 0.0), Strip(-0.6, 0.0, 0.0, 15.6) };
            MassBase open = WallRing.Choose(strips, Slab, 0.9);
            Assert.Equal(WallRing.FromSlab, open.Source);
            Assert.True(open.Flagged);
            Assert.Contains("middle of the slab", open.Reason);
            Assert.Equal(Slab.Length, open.Ring.Count);

            MassBase none = WallRing.Choose(new List<IReadOnlyList<double[]>>(), Slab, 0.9);
            Assert.Equal(WallRing.FromSlab, none.Source);
            Assert.Contains("no exterior wall", none.Reason);
        }

        [Fact]
        public void R451_a_ring_under_min_share_of_the_slab_area_gives_the_slab()
        {
            MassBase small = WallRing.Choose(new List<IReadOnlyList<double[]>> { Strip(2.0, 5.0, 6.0, 10.0) }, Slab, 0.9);
            Assert.Equal(WallRing.FromSlab, small.Source);
            Assert.Contains("minShare", small.Reason);
            Assert.Equal(WallRing.FromWalls, WallRing.Choose(new List<IReadOnlyList<double[]>> { Strip(0.1, 0.1, 7.9, 14.9) }, Slab, 0.9).Source);
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
            string key = Config(LinkFolders.Masses).MassFiles.SourceKey;
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
