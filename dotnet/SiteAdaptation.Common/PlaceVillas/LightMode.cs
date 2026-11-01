using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R404. Where the base boundary of a prototype is read. Q164 is open, its recommended reading is the outer loop of the floor of the
    // lowest level, so the answer of Bader changes config and never code
    public static class BaseBoundarySources
    {
        public const string LowestFloor = "lowestFloor";
        public const string FloorType = "floorType";
        public const string AreaBoundary = "areaBoundary";
        public static readonly string[] All = { LowestFloor, FloorType, AreaBoundary };
    }

    // R404, J4. addin.lightMode of config/project.json, every key refused by its name when it is missing or out of range
    public sealed class LightModeConfig
    {
        public const string Block = "lightMode";

        public string VillaModeDefault { get; private set; }
        public double SolidHeightM { get; private set; }
        public string BaseBoundary { get; private set; }

        // The floor type whose outer loop is the base when BaseBoundary is floorType, else null
        public string BaseBoundaryFloorType { get; private set; }
        public double FootprintLimitMm { get; private set; }

        // The step at which the distance of R404 samples each edge, so its result is exact to half of it
        public double FootprintSampleStepMm { get; private set; }

        // R450. Null for a Settings file saved before R450, which keeps the solids of R404
        public MassFileConfig MassFiles { get; private set; }

        // R453. The layer of the house outline inside a footprint block and the least area of one, both null for a Settings file saved
        // before R453, which keeps the block outline of R404
        public string HouseLayer { get; private set; }
        public double? HouseMinAreaM2 { get; private set; }

        public static LightModeConfig Load(string path)
        {
            return From(JObject.Parse(File.ReadAllText(path)));
        }

        public static LightModeConfig From(JObject root)
        {
            JObject block = (root?["addin"] as JObject)?[Block] as JObject ?? throw new InvalidDataException("config holds no block addin." + Block + " (R404)");
            var config = new LightModeConfig
            {
                VillaModeDefault = OneOf(block, "villaModeDefault", VillaModes.All),
                SolidHeightM = Positive(block, "solidHeightM"),
                BaseBoundary = OneOf(block, "baseBoundary", BaseBoundarySources.All),
                BaseBoundaryFloorType = block["baseBoundaryFloorType"] == null || block["baseBoundaryFloorType"].Type == JTokenType.Null ? null : (string)block["baseBoundaryFloorType"],
                FootprintLimitMm = Positive(block, "footprintLimitMm"),
                FootprintSampleStepMm = Positive(block, "footprintSampleStepMm"),
                MassFiles = MassFileConfig.From(block),
            };
            if (block["houseLayer"] != null || block["houseMinAreaM2"] != null)
            {
                JToken layer = block["houseLayer"];
                config.HouseLayer = layer?.Type == JTokenType.String && ((string)layer).Length > 0 ? (string)layer
                    : throw new InvalidDataException("addin." + Block + ".houseLayer is " + (layer?.ToString() ?? "missing") + ", houseMinAreaM2 needs the layer of the house outline (R453)");
                config.HouseMinAreaM2 = Positive(block, "houseMinAreaM2");
            }
            if (config.BaseBoundary == BaseBoundarySources.FloorType && string.IsNullOrWhiteSpace(config.BaseBoundaryFloorType))
            {
                throw new InvalidDataException("addin." + Block + ".baseBoundaryFloorType names no floor type, which baseBoundary floorType needs (R404)");
            }
            return config;
        }

        private static string OneOf(JObject block, string key, string[] all)
        {
            string value = block[key]?.Type == JTokenType.String ? (string)block[key] : null;
            if (value == null || !all.Contains(value))
            {
                throw new InvalidDataException("addin." + Block + "." + key + " is " + (block[key]?.ToString() ?? "missing") + ", not one of " + string.Join(", ", all) + " (R404)");
            }
            return value;
        }

        private static double Positive(JObject block, string key)
        {
            JToken token = block[key];
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer) || !(token.Value<double>() > 0))
            {
                throw new InvalidDataException("addin." + Block + "." + key + " is " + (token?.ToString() ?? "missing") + ", not a number over 0 (R404)");
            }
            return token.Value<double>();
        }
    }

    // R404, D2, Q129. The mode picked for the run in the pane row Villa mode wins, else the default of config, Masses for test runs and
    // for a run Bader starts by hand
    public static class VillaModeChoice
    {
        public static string Resolve(string picked, LightModeConfig config)
        {
            if (picked == null)
            {
                return config.VillaModeDefault;
            }
            if (!VillaModes.All.Contains(picked))
            {
                throw new ArgumentException("the villa mode " + picked + " is not one of " + string.Join(", ", VillaModes.All), nameof(picked));
            }
            return picked;
        }
    }
}
