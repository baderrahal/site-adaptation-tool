using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // R373, R374, docs/design/03a-buttons-shared-and-1.md 1 Set up site. What button 1 sets: the Parsons position of the position file
    // (Q068), its levels on their one base, the units, the floor types of config with the Ramps type of the slope chosen, the wall
    // types and the parameters of the parameter sheet. The pass limit and the decimals of the check points come with it
    public sealed class SetUpTarget
    {
        public SitePosition Position { get; set; }
        public double[] SurveyInternal { get; set; }
        public double[] SurveyShared { get; set; }
        public double[] BaseInternal { get; set; }
        public double[] BaseShared { get; set; }
        public List<PositionLevel> Levels { get; set; } = new List<PositionLevel>();
        public string LevelBase { get; set; }
        public string ActiveSite { get; set; }
        public UnitState Units { get; set; } = new UnitState();

        // The floor types in the order of the Floor types row, generic first, so the first is the one the others are copied from
        public List<string> FloorTypes { get; set; } = new List<string>();
        public List<string> GenericTypes { get; set; } = new List<string>();
        public List<string> WallTypes { get; set; } = new List<string>();
        public double RampSlope { get; set; }
        public string RampType { get; set; }
        public ParameterSheet Parameters { get; set; }

        // The parameter of floors.plotParameter, bound once Q156 gives it a GUID
        public string PlotParameter { get; set; }
        public string ParameterGroup { get; set; }
        public double PassLimitMm { get; set; }
        public int GapDecimals { get; set; }

        // config, the rules profile and the position file as button 1 reads them, with the slope chosen in the dialog
        public static SetUpTarget From(JObject config, JObject rules, string levelProfile, JObject position, double rampSlope, ParameterSheet parameters)
        {
            PositionSettings settings = PositionSettings.From(config);
            JObject floors = config["floors"] as JObject ?? throw new InvalidDataException("config holds no floors block, the floor types of button 1 (R112)");
            JObject types = floors["types"] as JObject ?? throw new InvalidDataException("config holds no floors.types (R112)");
            if (!RampSlopes.Choices.Contains(rampSlope))
            {
                throw new InvalidDataException("the ramp slope " + RampSlopes.Text(rampSlope) + "% is none of the choices of the row Ramp slope (R373)");
            }
            var target = new SetUpTarget
            {
                RampSlope = rampSlope,
                RampType = RampSlopes.TypeName(rules, levelProfile, rampSlope),
                GenericTypes = Names(types["generic"]),
                Parameters = parameters,
                PlotParameter = floors["plotParameter"]?.Type == JTokenType.String ? (string)floors["plotParameter"] : null,
                ParameterGroup = SetUpDefaults.ParameterGroup(config),
                WallTypes = SetUpDefaults.WallTypes(config),
                PassLimitMm = settings.CheckPointLimitMm,
                GapDecimals = settings.GapDecimals,
            };
            foreach (string name in SiteCoordinates.FloorKinds.SelectMany(kind => Names(types[kind])).Concat(new[] { target.RampType }))
            {
                if (!target.FloorTypes.Contains(name))
                {
                    target.FloorTypes.Add(name);
                }
            }
            if (target.GenericTypes.Count == 0)
            {
                throw new InvalidDataException("floors.types.generic of config names no floor type (R112)");
            }
            ReadPosition(target, position);
            return target;
        }

        // The position file as the worker writes it, ProjectPositionFile.Written (R353)
        private static void ReadPosition(SetUpTarget target, JObject file)
        {
            if (file == null || (string)file["format"] != ProjectPositionFile.Format || file["formatVersion"]?.Type != JTokenType.Integer || (int)file["formatVersion"] != ProjectPositionFile.FormatVersion)
            {
                throw new InvalidDataException("the position file is no " + ProjectPositionFile.Format + " of version " + ProjectPositionFile.FormatVersion + " (R353, Q068)");
            }
            JObject at = file["projectPosition"] as JObject ?? throw new InvalidDataException("the position file holds no projectPosition (R353)");
            target.Position = new SitePosition { EastM = Number(at["eastM"], "eastM"), NorthM = Number(at["northM"], "northM"), ElevationM = Number(at["elevationM"], "elevationM"), AngleDeg = Number(at["angleDeg"], "angleDeg") };
            target.SurveyInternal = Point(file["surveyPoint"]?["internalM"], "surveyPoint.internalM");
            target.SurveyShared = Point(file["surveyPoint"]?["sharedM"], "surveyPoint.sharedM");
            target.BaseInternal = Point(file["projectBasePoint"]?["internalM"], "projectBasePoint.internalM");
            target.BaseShared = Point(file["projectBasePoint"]?["sharedM"], "projectBasePoint.sharedM");
            target.Levels = (file["levels"] as JArray ?? new JArray()).OfType<JObject>().Select(level => new PositionLevel
            {
                Name = (string)level["name"], ElevationM = Number(level["elevationM"], "levels.elevationM"), ProjectElevationM = Number(level["projectElevationM"], "levels.projectElevationM"),
            }).ToList();
            if (target.Levels.Count == 0 || target.Levels.Any(level => string.IsNullOrWhiteSpace(level.Name)))
            {
                throw new InvalidDataException("the position file holds no level with a name (R353)");
            }
            target.LevelBase = file["levelBase"]?.Type == JTokenType.String ? (string)file["levelBase"] : throw new InvalidDataException("the position file holds no levelBase (R353)");
            target.ActiveSite = file["activeSite"]?.Type == JTokenType.String ? (string)file["activeSite"] : null;
            string length = (string)(file["units"] as JObject)?["length"];
            JToken accuracy = file["lengthAccuracy"];
            if (length == null || accuracy == null || (accuracy.Type != JTokenType.Float && accuracy.Type != JTokenType.Integer))
            {
                throw new InvalidDataException("the position file holds no units.length and lengthAccuracy (R353)");
            }
            target.Units = new UnitState
            {
                System = (string)(file["units"] as JObject)?["system"], Length = length, LengthAccuracy = accuracy.Value<double>(),
                Area = SetUpDefaults.AreaUnit, Angle = SetUpDefaults.AngleUnit, Slope = SetUpDefaults.SlopeUnit,
            };
        }

        private static List<string> Names(JToken names)
        {
            return (names as JArray ?? new JArray()).Where(item => item.Type == JTokenType.String).Select(item => (string)item).ToList();
        }

        private static double Number(JToken value, string what)
        {
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer) || double.IsNaN(value.Value<double>()) || double.IsInfinity(value.Value<double>()))
            {
                throw new InvalidDataException(what + " of the position file is no finite number (R353)");
            }
            return value.Value<double>();
        }

        private static double[] Point(JToken value, string what)
        {
            JArray items = value as JArray;
            if (items == null || items.Count != 3)
            {
                throw new InvalidDataException(what + " of the position file is no point of three numbers (R353)");
            }
            return items.Select(item => Number(item, what)).ToArray();
        }
    }

    // R373. What config does not hold yet for button 1, each read from config when the key is there and else the value the design
    // gives, so a run never guesses. Each key is named in the report of 5b for Lane A to add (no project value in code, CLAUDE.md)
    public static class SetUpDefaults
    {
        // 03a 1 Set up site step 1: area in square meters, angle and slope in degrees
        public const string AreaUnit = "squareMeters";
        public const string AngleUnit = "degrees";
        public const string SlopeUnit = "slopeDegrees";

        // 03a 1 Set up site Outputs: BW and the most used garden type of the IMC key (READ key_reads.json)
        private static readonly string[] Walls = { "BW", "Generic Wall-150mm_ Garden" };

        // addin.wallTypes, a list of names, else the two of 03a
        public static List<string> WallTypes(JObject config)
        {
            JArray named = (config["addin"] as JObject)?["wallTypes"] as JArray;
            return named != null && named.Count > 0 ? named.Select(item => (string)item).ToList() : Walls.ToList();
        }

        // siteBuild.parameterGroup, the group of the shared parameter file R212 writes
        public static string ParameterGroup(JObject config)
        {
            JToken group = (config["siteBuild"] as JObject)?["parameterGroup"];
            if (group == null || group.Type != JTokenType.String || ((string)group).Length == 0)
            {
                throw new InvalidDataException("config holds no siteBuild.parameterGroup, the group of the shared parameter file of button 1 (R212)");
            }
            return (string)group;
        }
    }
}
