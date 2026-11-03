using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // R373, R374, docs/design/03a-buttons-shared-and-1.md 1 Set up site. What button 1 sets: the Parsons position of the position file
    // (Q068) or the site of the CAD inputs when coordinates.source is cad (R455) with the levels and units still of the file (Q328), its levels on their one base, the units, the floor types of config with the Ramps type of the slope chosen, the wall
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
        public List<double> RampSlopeChoices { get; set; }
        public string RampType { get; set; }
        public ParameterSheet Parameters { get; set; }

        // The parameter of floors.plotParameter, bound once Q156 gives it a GUID
        public string PlotParameter { get; set; }
        public string ParameterGroup { get; set; }
        public double PassLimitMm { get; set; }
        public int GapDecimals { get; set; }

        // R454 to R456. The site of the CAD inputs when coordinates.source is cad, null when the position file gives the position
        public CadSite Cad { get; set; }

        // R514. cad, positionFile or typed, null while no usable source is chosen
        public string PositionSource { get; set; }

        // R513. Why CAD gives no coordinates, and with it true when the target holds no site at all, so nothing of the position is planned and the window asks
        public string SourceProblem { get; set; }
        public bool NoSite { get; set; }

        // R514. The name of the position file, for the row Position from
        public string PositionFileName { get; set; }

        // R510. The limits of a blank model, null for a config of before the keys
        public BlankLimits BlankLimits { get; set; }
        public List<ControlPoint> ControlPoints { get; set; } = new List<ControlPoint>();

        // config, the rules profile and the position file as button 1 reads them, with the slope chosen in the dialog
        // R511, R513. Under CAD or a typed site the position file is only a check, so the levels and units come from keyRules.site, what the worker holds the file against
        public static SetUpTarget From(JObject config, JObject rules, string levelProfile, JObject position, double rampSlope, ParameterSheet parameters, CadSite cad = null, string sourceProblem = null, string positionFile = null)
        {
            PositionSettings settings = PositionSettings.From(config);
            JObject floors = config["floors"] as JObject ?? throw new InvalidDataException("config holds no floors block, the floor types of button 1 (R112)");
            JObject types = floors["types"] as JObject ?? throw new InvalidDataException("config holds no floors.types (R112)");
            List<double> choices = RampSlopes.Choices(rules, levelProfile);
            if (!choices.Contains(rampSlope))
            {
                throw new InvalidDataException("the ramp slope " + RampSlopes.Text(rampSlope) + "% is none of the choices of the row Ramp slope (R373)");
            }
            var target = new SetUpTarget
            {
                RampSlope = rampSlope,
                RampSlopeChoices = choices,
                RampType = RampSlopes.TypeName(rules, levelProfile, rampSlope),
                GenericTypes = Names(types["generic"]),
                Parameters = parameters,
                PlotParameter = floors["plotParameter"]?.Type == JTokenType.String ? (string)floors["plotParameter"] : null,
                ParameterGroup = SetUpDefaults.ParameterGroup(config),
                WallTypes = SetUpDefaults.WallTypes(config),
                PassLimitMm = settings.RevitCheckPointLimitMm,
                GapDecimals = settings.GapDecimals,
                SourceProblem = sourceProblem,
                BlankLimits = SiteSources.Limits(config),
                PositionFileName = Path.GetFileName(positionFile ?? settings.File),
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
            if (cad != null || (position == null && sourceProblem != null))
            {
                ReadRules(target, rules, levelProfile, SetUpDefaults.SiteUnits(config), settings.Key, cad == null ? 0.0 : cad.SurveyInternal[2]);
            }
            else
            {
                ReadPosition(target, position, SetUpDefaults.SiteUnits(config));
                target.PositionSource = SiteSources.PositionFile;
            }
            if (cad == null && position == null && sourceProblem != null)
            {
                var origin = new[] { 0.0, 0.0, 0.0 };
                target.NoSite = true;
                target.Position = new SitePosition { EastM = 0.0, NorthM = 0.0, ElevationM = 0.0, AngleDeg = 0.0 };
                target.SurveyInternal = origin;
                target.SurveyShared = origin;
                target.BaseInternal = origin;
                target.BaseShared = origin;
            }
            if (cad != null)
            {
                target.PositionSource = cad.Source;
                target.Cad = cad;
                target.Position = cad.Position;
                target.SurveyInternal = cad.SurveyInternal;
                target.SurveyShared = cad.SurveyShared;
                target.BaseInternal = cad.BaseInternal;
                target.BaseShared = cad.BaseShared;
                target.ActiveSite = cad.PositionName;
                target.ControlPoints = cad.ControlPoints;
            }
            return target;
        }

        // R511. keyRules.site of the level profile: the names, the base, the steps over the lowest level, which stands at 0 on the base as in the position file of the keys, and
        // the length accuracy of the reference key of majority.siteReference, whose position file a past job takes
        private static void ReadRules(SetUpTarget target, JObject rules, string levelProfile, Dictionary<string, string> units, string key, double baseHeightM)
        {
            JObject site = ProjectPositionFile.SiteRules(rules, levelProfile);
            List<string> names = Names(site["levels"]);
            if (names.Count == 0)
            {
                throw new InvalidDataException("keyRules.site of the rules profile holds no levels, the levels of button 1 under CAD (R511)");
            }
            target.Levels = names.Select((name, at) =>
            {
                double step = at == 0 ? 0.0 : Rule((site["levelStepsM"] as JObject)?[name], "levelStepsM." + name);
                return new PositionLevel { Name = name, ElevationM = step, ProjectElevationM = step + baseHeightM };
            }).ToList();
            target.LevelBase = site["levelBase"]?.Type == JTokenType.String ? (string)site["levelBase"] : throw new InvalidDataException("keyRules.site of the rules profile holds no levelBase (R511)");
            JObject held = site["units"] as JObject;
            string length = held?["length"]?.Type == JTokenType.String ? (string)held["length"] : throw new InvalidDataException("keyRules.site of the rules profile holds no units.length (R511)");
            target.Units = new UnitState
            {
                System = (string)held["system"], Length = length, LengthAccuracy = Rule((site["lengthAccuracy"] as JObject)?[key], "lengthAccuracy." + key),
                Area = units["area"], Angle = units["angle"], Slope = units["slope"],
            };
        }

        private static double Rule(JToken value, string what)
        {
            return value != null && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer) ? value.Value<double>() : throw new InvalidDataException("keyRules.site of the rules profile holds no number " + what + " (R511)");
        }

        // The position file as the worker writes it, ProjectPositionFile.Written (R353)
        internal static void ReadPosition(SetUpTarget target, JObject file, Dictionary<string, string> units)
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
                Area = units["area"], Angle = units["angle"], Slope = units["slope"],
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

    // R373, J18. The settings of button 1 config holds since Lane B 5c, each refused by its name when missing, never a value of code
    public static class SetUpDefaults
    {
        public static readonly string[] UnitSpecs = { "area", "angle", "slope" };

        // addin.siteUnits, the unit of each spec 03a 1 Set up site step 1 sets, by the names SiteFacts.UnitName gives
        public static Dictionary<string, string> SiteUnits(JObject config)
        {
            JObject units = (config["addin"] as JObject)?["siteUnits"] as JObject;
            var found = new Dictionary<string, string>();
            foreach (string spec in UnitSpecs)
            {
                JToken unit = units?[spec];
                if (unit == null || unit.Type != JTokenType.String || ((string)unit).Length == 0)
                {
                    throw new InvalidDataException("config holds no addin.siteUnits." + spec + ", the " + spec + " unit of button 1 (R373)");
                }
                found[spec] = (string)unit;
            }
            return found;
        }

        // addin.wallTypes, the wall types of 03a 1 Set up site Outputs
        public static List<string> WallTypes(JObject config)
        {
            JArray named = (config["addin"] as JObject)?["wallTypes"] as JArray;
            if (named == null || named.Count == 0 || named.Any(item => item.Type != JTokenType.String || ((string)item).Length == 0))
            {
                throw new InvalidDataException("config holds no list of names addin.wallTypes, the wall types of button 1 (R373)");
            }
            return named.Select(item => (string)item).ToList();
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
