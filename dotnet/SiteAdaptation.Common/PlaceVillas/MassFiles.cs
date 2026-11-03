using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R450. The values of addin.lightMode.linkFolder: solids keeps the DirectShape of R404 in the site, masses links the mass files of
    // massFolder, prototypes links the prototype files of the plan. Any other value is a folder, so the KEO files swap in by config
    public static class LinkFolders
    {
        public const string Solids = "solids";
        public const string Masses = "masses";
        public const string Prototypes = "prototypes";
        public static readonly string[] Named = { Solids, Masses, Prototypes };
    }

    // R450, R451. The mass file keys of addin.lightMode
    public sealed class MassFileConfig
    {
        public string LinkFolder { get; private set; }

        // May start with the {workRoot} mark
        public string MassFolder { get; private set; }

        // The share of the slab area the ring must cover to be the base (R504)
        public double WallRingMinShare { get; private set; }

        // The largest share, so a ring that holds a garden wall of Exterior function is not the base (R504)
        public double WallRingMaxShare { get; private set; }

        // R500 to R503. The limits of the ring builder
        public RingLimits Limits { get; private set; }

        public bool AsLinks => LinkFolder != LinkFolders.Solids;

        // Null when the block names no linkFolder, a Settings file saved before R450, which keeps the solids of R404
        public static MassFileConfig From(JObject block)
        {
            JToken link = block?["linkFolder"];
            if (link == null)
            {
                return null;
            }
            string value = link.Type == JTokenType.String ? ((string)link).Trim() : null;
            if (string.IsNullOrEmpty(value) || (!LinkFolders.Named.Contains(value) && !value.StartsWith(WorkRoot.Mark, StringComparison.Ordinal) && !Path.IsPathRooted(value)))
            {
                throw new InvalidDataException("addin.lightMode.linkFolder is " + link + ", not one of " + string.Join(", ", LinkFolders.Named) + " nor a full folder path (R450)");
            }
            var config = new MassFileConfig { LinkFolder = value };
            if (value == LinkFolders.Masses)
            {
                JToken folder = block["massFolder"];
                config.MassFolder = folder?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)folder) ? ((string)folder).Trim()
                    : throw new InvalidDataException("addin.lightMode.massFolder is " + (folder?.ToString() ?? "missing") + ", linkFolder masses needs the folder of the mass files (R450)");
                config.Limits = new RingLimits
                {
                    OutsideM = Number(block, "wallRingOutsideM", 0, double.MaxValue), InsideM = Number(block, "wallRingInsideM", 0, double.MaxValue),
                    MinHeightM = Number(block, "wallRingMinHeightM", 0, double.MaxValue), ShortestEdgeM = Number(block, "wallRingShortestEdgeM", 0, double.MaxValue),
                    CornerGapM = Number(block, "wallRingCornerGapM", 0, double.MaxValue), SlotM = Number(block, "wallRingSlotM", 0, double.MaxValue),
                    ArcStepM = Number(block, "wallRingArcStepM", 0, double.MaxValue), BottomAboveM = Number(block, "wallRingBottomAboveM", 0, double.MaxValue),
                    SampleM = Number(block, "wallRingSampleM", 0, double.MaxValue), ParallelSine = Number(block, "wallRingParallelSine", 0, 1), OnLineM = Number(block, "wallRingOnLineM", 0, double.MaxValue),
                };
                config.WallRingMinShare = Number(block, "wallRingMinShare", 0, 1);
                config.WallRingMaxShare = Number(block, "wallRingMaxShare", 0, double.MaxValue);
                if (config.WallRingMaxShare < config.WallRingMinShare)
                {
                    throw new InvalidDataException("addin.lightMode.wallRingMaxShare is " + block["wallRingMaxShare"] + ", under wallRingMinShare " + block["wallRingMinShare"] + " (R504)");
                }
            }
            return config;
        }

        // The folder each link file is taken from, the mass folder, the prototype folder of the plan or the folder named
        public string FolderFor(string prototypeFolder, Func<string> root)
        {
            switch (LinkFolder)
            {
                case LinkFolders.Masses:
                    return WorkRoot.Resolve(MassFolder, root);
                case LinkFolders.Prototypes:
                    return prototypeFolder;
                case LinkFolders.Solids:
                    return null;
                default:
                    return WorkRoot.Resolve(LinkFolder, root);
            }
        }

        // Over low and at most high
        private static double Number(JObject block, string key, double low, double high)
        {
            JToken token = block[key];
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer) || !(token.Value<double>() > low) || token.Value<double>() > high)
            {
                throw new InvalidDataException("addin.lightMode." + key + " is " + (token?.ToString() ?? "missing") + ", not a number over "
                    + low.ToString(CultureInfo.InvariantCulture) + (high < double.MaxValue ? " and at most " + high.ToString(CultureInfo.InvariantCulture) : "") + " (R450)");
            }
            return token.Value<double>();
        }
    }

    // R450. One mass file per prototype file, named exactly as it, with a sidecar JSON beside it that says what it was made from
    public static class MassFiles
    {
        // Raised whenever the Revit read of the base or the file written changes, so every mass file is made again
        public const int ReaderVersion = 5;
        public const string Format = "site-adaptation-mass-file";
        public const string SidecarSuffix = ".json";

        // R450. Every key a mass file is made from besides its prototype, so a change of any of them makes every mass again
        public static string SourceKey(LightModeConfig config)
        {
            MassFileConfig mass = config.MassFiles;
            RingLimits ring = mass.Limits;
            return string.Format(CultureInfo.InvariantCulture, "solidHeight {0:R} m, baseBoundary {1} {2}, wallRing minShare {3:R}, maxShare {4:R}, outside {5:R} m, inside {6:R} m, minHeight {7:R} m, shortestEdge {8:R} m, cornerGap {9:R} m, slot {10:R} m, arcStep {11:R} m, bottomAbove {12:R} m, sample {13:R} m, parallelSine {14:R}, onLine {15:R} m",
                config.SolidHeightM, config.BaseBoundary, config.BaseBoundaryFloorType ?? "", mass.WallRingMinShare, mass.WallRingMaxShare, ring.OutsideM, ring.InsideM, ring.MinHeightM, ring.ShortestEdgeM, ring.CornerGapM, ring.SlotM, ring.ArcStepM, ring.BottomAboveM, ring.SampleM, ring.ParallelSine, ring.OnLineM);
        }

        public static string PathFor(string folder, string prototypePath)
        {
            return Path.Combine(folder, Path.GetFileName(prototypePath));
        }

        public static string SidecarPath(string massPath)
        {
            return massPath + SidecarSuffix;
        }

        // The points are what R404 and the footprint check read, the edges what a mass is made from, the walls say what the ring took (R500)
        public static JObject Sidecar(string prototypePath, string prototypeSha256, string sourceKey, MassBase mass)
        {
            return new JObject
            {
                ["edges"] = new JArray(mass.Edges.Select(EdgeJson)),
                ["wallsUsed"] = new JArray((mass.Build?.Used ?? new List<string>()).Cast<object>().ToArray()),
                ["wallsBackFace"] = new JArray(mass.BackFace.Cast<object>().ToArray()),
                ["check"] = Check(mass),
                ["wallsLeftOut"] = new JArray((mass.Build?.LeftOut ?? new List<WallLeftOut>()).Select(wall => new JObject { ["id"] = wall.Id, ["why"] = wall.Why })),
                ["wallsSkipped"] = new JArray(mass.Skipped.Cast<object>().ToArray()),
                ["format"] = Format, ["readerVersion"] = ReaderVersion, ["prototype"] = Path.GetFileName(prototypePath), ["prototypeSha256"] = prototypeSha256,
                ["sourceKey"] = sourceKey, ["source"] = mass.Source, ["flagged"] = mass.Flagged, ["reason"] = mass.Reason,
                ["points"] = new JArray(mass.Ring.Select(point => new JArray(point[0], point[1]))),
            };
        }

        // True when no sidecar reads, or its prototype hash, reader version or source keys differ from now
        public static bool Stale(JObject sidecar, string prototypeSha256, string sourceKey)
        {
            return sidecar == null || (string)sidecar["format"] != Format || (int?)sidecar["readerVersion"] != ReaderVersion
                || !string.Equals((string)sidecar["prototypeSha256"], prototypeSha256, StringComparison.OrdinalIgnoreCase) || (string)sidecar["sourceKey"] != sourceKey
                || !(sidecar["points"] is JArray points) || points.Count < 3;
        }

        public static MassBase Base(JObject sidecar)
        {
            List<double[]> points = ((JArray)sidecar["points"]).Select(point => new[] { (double)point[0], (double)point[1] }).ToList();
            List<RingEdge> edges = (sidecar["edges"] as JArray)?.Select(EdgeOf).ToList();
            var read = new MassBase(points, (string)sidecar["source"], (string)sidecar["reason"], edges);
            read.Skipped = (sidecar["wallsSkipped"] as JArray)?.Select(id => (string)id).ToList() ?? new List<string>();
            read.BackFace = (sidecar["wallsBackFace"] as JArray)?.Select(id => (string)id).ToList() ?? new List<string>();
            return read;
        }

        // R504. What the table per prototype of a real run reads: the ring the walls gave against the slab, how far its edges lie off the real
        // side faces, the edges with no wall on them, the walls no link shows. Null values where the walls gave no ring
        private static JObject Check(MassBase mass)
        {
            WallRingBuild build = mass.Build;
            return new JObject
            {
                ["ringAreaM2"] = mass.BuiltAreaM2, ["slabAreaM2"] = mass.SlabAreaM2, ["share"] = mass.BuiltAreaM2.HasValue && mass.SlabAreaM2 > 0 ? mass.BuiltAreaM2 / mass.SlabAreaM2 : null,
                ["edges"] = build?.Edges.Count, ["connectingEdges"] = build?.ConnectingEdges, ["largestOffFaceMm"] = build?.LargestOffFaceM * 1000, ["offFaceUnmeasured"] = build?.OffFaceUnmeasured,
                ["wallsLeftByOption"] = build?.LeftByOption, ["primaryOptions"] = new JArray((build?.PrimaryOptions ?? new List<string>()).Cast<object>().ToArray()),
            };
        }

        private static JObject EdgeJson(RingEdge edge)
        {
            var json = new JObject { ["start"] = new JArray(edge.Start[0], edge.Start[1]), ["end"] = new JArray(edge.End[0], edge.End[1]) };
            if (edge.IsArc)
            {
                json["centre"] = new JArray(edge.Centre[0], edge.Centre[1]);
                json["counterClockwise"] = edge.CounterClockwise;
            }
            return json;
        }

        private static RingEdge EdgeOf(JToken json)
        {
            double[] Point(JToken point) => new[] { (double)point[0], (double)point[1] };
            return json["centre"] == null ? new RingEdge(Point(json["start"]), Point(json["end"])) : new RingEdge(Point(json["start"]), Point(json["end"]), Point(json["centre"]), (bool)json["counterClockwise"]);
        }

        public static JObject Read(string massPath)
        {
            string sidecar = SidecarPath(massPath);
            if (!File.Exists(massPath) || !File.Exists(sidecar))
            {
                return null;
            }
            try
            {
                return JObject.Parse(WorkFiles.ReadText(sidecar));
            }
            catch (Exception failure) when (failure is IOException || failure is Newtonsoft.Json.JsonException)
            {
                // A sidecar that does not read makes the mass again, never trusted
                return null;
            }
        }
    }

    // R450. What a run knows of the mass of one prototype before it makes any
    public sealed class MassState
    {
        public string MassPath { get; set; }
        public bool Stale { get; set; }

        // Null while stale
        public MassBase Base { get; set; }
    }

    // R450. The mass files of a run: the state of each from its sidecar, and the making of one outside any transaction of the site
    public sealed class MassFileSet
    {
        private readonly IMassFileMaker maker;
        private readonly Func<string, string> hash;
        private readonly Func<string> root;

        public MassFileSet(IMassFileMaker maker, Func<string, string> hash, Func<string> root)
        {
            this.maker = maker;
            this.hash = hash;
            this.root = root;
        }

        public string LinkFolder(MassFileConfig config, string prototypeFolder)
        {
            return config.FolderFor(prototypeFolder, root);
        }

        public MassState State(string prototypePath, LightModeConfig config)
        {
            string mass = MassFiles.PathFor(LinkFolder(config.MassFiles, null), prototypePath);
            JObject sidecar = MassFiles.Read(mass);
            bool stale = MassFiles.Stale(sidecar, hash(prototypePath), MassFiles.SourceKey(config));
            return new MassState { MassPath = mass, Stale = stale, Base = stale ? null : MassFiles.Base(sidecar) };
        }

        // Writes only the mass file and its sidecar, refused where the mass file would be the prototype itself
        public MassBase Make(string prototypePath, LightModeConfig config)
        {
            string folder = LinkFolder(config.MassFiles, null);
            string mass = MassFiles.PathFor(folder, prototypePath);
            if (string.Equals(Path.GetFullPath(mass), Path.GetFullPath(prototypePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("the mass folder " + folder + " is the folder of the prototype " + Path.GetFileName(prototypePath) + ", a mass file would overwrite it (R450)");
            }
            string sha = hash(prototypePath);
            MassBaseRead read = maker.ReadBase(prototypePath, config) ?? throw new InvalidDataException(Path.GetFileName(prototypePath) + " gave no base read (R451)");
            RingLimits limits = config.MassFiles.Limits;
            MassBase chosen = WallRing.Choose(WallRingBuilder.Build(read.Walls, read.Slab, limits), read.Slab, config.MassFiles.WallRingMinShare, config.MassFiles.WallRingMaxShare, limits.ArcStepM);
            Directory.CreateDirectory(folder);
            maker.Write(mass, chosen.Edges, config.SolidHeightM);
            WorkFiles.WriteText(MassFiles.SidecarPath(mass), "mass", MassFiles.Sidecar(prototypePath, sha, MassFiles.SourceKey(config), chosen).ToString(Newtonsoft.Json.Formatting.Indented));
            return chosen;
        }
    }
}
