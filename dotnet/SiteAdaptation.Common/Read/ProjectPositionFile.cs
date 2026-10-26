using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R353. addin.position of config/project.json: the position file button 1 reads, the pass limit of its check points in mm, the
    // decimals their gap is shown and judged at, and the key whose read the file is written from, majority.siteReference of the group
    // of majority.check.group, as python/position_file.settings_of and key_of take them
    public sealed class PositionSettings
    {
        public string File { get; private set; }
        public double CheckPointLimitMm { get; private set; }
        public int GapDecimals { get; private set; }
        public string Key { get; private set; }

        public static PositionSettings From(JObject config)
        {
            JObject block = (config["addin"] as JObject)?["position"] as JObject ?? throw new InvalidDataException("config holds no addin.position, the position file and the check point limit (R353)");
            JToken limit = block["checkPointLimitMm"], decimals = block["gapDecimals"];
            double limitMm = limit != null && (limit.Type == JTokenType.Float || limit.Type == JTokenType.Integer) ? limit.Value<double>() : double.NaN;
            if (double.IsNaN(limitMm) || double.IsInfinity(limitMm) || limitMm <= 0)
            {
                throw new InvalidDataException("addin.position.checkPointLimitMm is no length in mm above 0 (R353)");
            }
            if (decimals == null || decimals.Type != JTokenType.Integer || decimals.Value<long>() < 0 || decimals.Value<long>() > 6)
            {
                throw new InvalidDataException("addin.position.gapDecimals is no whole number of decimals from 0 to 6 (R353)");
            }
            string file = block["file"]?.Type == JTokenType.String ? (string)block["file"] : null;
            if (file == null || !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("addin.position.file names no .json file (R353)");
            }
            JObject majority = config["majority"] as JObject;
            string group = (majority?["check"] as JObject)?["group"]?.Type == JTokenType.String ? (string)majority["check"]["group"] : null;
            JToken key = group == null ? null : (majority?["siteReference"] as JObject)?[group];
            if (key == null || key.Type != JTokenType.String || ((string)key).Length == 0)
            {
                throw new InvalidDataException("majority.siteReference names no reference key for the group " + (group ?? "null") + " of majority.check.group (R353)");
            }
            return new PositionSettings { File = file, CheckPointLimitMm = limitMm, GapDecimals = (int)decimals.Value<long>(), Key = (string)key };
        }
    }

    public sealed class PositionLevel
    {
        public string Name { get; set; }
        public double ElevationM { get; set; }
        public double ProjectElevationM { get; set; }
    }

    // R353. The project position file of button 1 from the read of an answer key (R311, R331), python/position_file.py step for step:
    // the project position of the internal origin in shared coordinates with its angle, the survey point and the project base point in
    // internal and shared coordinates, the levels on their one Elevation Base and the units, held against keyRules.site, and the check
    // points of button 1, each shared point taken to its internal point by SiteCoordinates of R211 and by the survey point pair of the key read
    public sealed class ProjectPositionFile
    {
        public const string Format = "site-adaptation-position";
        public const int FormatVersion = 1;
        public const string PositionStep = "position";
        public const string CheckPointsStep = "checkPoints";
        public const string KeyReadRole = "keyRead";
        public const string RulesRole = "rules";
        public static readonly string[] PositionRoles = { KeyReadRole, RulesRole };
        public static readonly string[] CheckPointRoles = { KeyReadRole, RulesRole, "baseplan", LocationPlan.PlotNumbersRole };
        // A level step of the file is the one of the rules within half a mm, the angle within a millionth of a degree, as R331 holds a read
        private const double SameMm = 0.5;
        private const double SameDeg = 1e-6;

        public string Key { get; private set; }
        public string Model { get; private set; }
        public SitePosition Position { get; private set; }
        public double AngleToTrueNorthDeg { get; private set; }
        public double[] SurveyInternal { get; private set; }
        public double[] SurveyShared { get; private set; }
        public double[] BaseInternal { get; private set; }
        public double[] BaseShared { get; private set; }
        public List<PositionLevel> Levels { get; private set; }
        public string LevelBase { get; private set; }
        public Dictionary<string, string> Units { get; private set; }

        private static double Finite(JToken value, string what)
        {
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer))
            {
                throw new InvalidDataException(what + " of the key read is no finite number (R353)");
            }
            double number = value.Value<double>();
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                throw new InvalidDataException(what + " of the key read is no finite number (R353)");
            }
            return number;
        }

        private static double[] FinitePoint(JToken value, string what)
        {
            JArray items = value as JArray;
            if (items == null || items.Count != 3 || items.Any(item => item.Type != JTokenType.Float && item.Type != JTokenType.Integer || double.IsNaN(item.Value<double>()) || double.IsInfinity(item.Value<double>())))
            {
                throw new InvalidDataException(what + " of the key read is no point of three finite numbers (R353)");
            }
            return items.Select(item => item.Value<double>()).ToArray();
        }

        // The one project document of a reader output, in metres and degrees
        public static JObject DocumentOf(JObject read)
        {
            List<JObject> documents = (read["documents"] as JArray ?? new JArray()).OfType<JObject>().Where(document => (string)document["kind"] == "project").ToList();
            if (documents.Count != 1)
            {
                throw new InvalidDataException("the key read holds " + documents.Count + " project documents, the position file is written from one (R353)");
            }
            if ((string)read["lengthUnit"] != "m" || (string)read["angleUnit"] != "deg")
            {
                throw new InvalidDataException("the key read gives lengths in " + (string)read["lengthUnit"] + " and angles in " + (string)read["angleUnit"] + ", the position file takes m and deg (R353)");
            }
            return documents[0];
        }

        public static ProjectPositionFile From(JObject document, string key)
        {
            JObject site = document["site"] as JObject ?? throw new InvalidDataException("the key read holds no site facts, a read of R311 is needed (R353)");
            JObject notRead = site["notRead"] as JObject;
            List<string> missing = new[] { "projectPosition", "levels", "units" }.Where(name => (notRead != null && notRead[name] != null) || site[name] == null || site[name].Type == JTokenType.Null).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidDataException("the site facts of the key read hold no " + string.Join(", ", missing) + " (R353)");
            }
            JObject at = (JObject)site["projectPosition"];
            List<JObject> levels = ((JArray)site["levels"]).OfType<JObject>().Select(level => new { level, height = Finite(level["projectElevation"], "projectElevation of " + (string)level["name"]) })
                .OrderBy(item => item.height).ThenBy(item => (string)item.level["name"], StringComparer.Ordinal).Select(item => item.level).ToList();
            if (levels.Count == 0)
            {
                throw new InvalidDataException("the key read holds no level (R353)");
            }
            List<JToken> bases = levels.Select(level => level["elevationBase"]).GroupBy(item => item?.ToString(Newtonsoft.Json.Formatting.None)).Select(group => group.First()).ToList();
            if (bases.Count != 1 || bases[0] == null || bases[0].Type != JTokenType.String)
            {
                throw new InvalidDataException("the levels of the key read stand on the Elevation Bases " + string.Join(", ", bases.Select(item => item == null ? "None" : item.ToString(Newtonsoft.Json.Formatting.None))) + ", the position file takes one (R353)");
            }
            JObject survey = document["surveyPoint"] as JObject ?? throw new InvalidDataException("the key read holds no surveyPoint (R353)");
            JObject basePoint = document["basePoint"] as JObject ?? throw new InvalidDataException("the key read holds no basePoint (R353)");
            JObject units = site["units"] as JObject;
            return new ProjectPositionFile
            {
                Key = key,
                Model = (string)document["file"],
                Position = new SitePosition
                {
                    EastM = Finite(at["eastWest"], "eastWest"), NorthM = Finite(at["northSouth"], "northSouth"), ElevationM = Finite(at["elevation"], "elevation"), AngleDeg = Finite(at["angleDeg"], "angleDeg"),
                },
                AngleToTrueNorthDeg = Finite(document["angleToTrueNorthDeg"], "angleToTrueNorthDeg"),
                SurveyInternal = FinitePoint(survey["position"], "surveyPoint.position"),
                SurveyShared = FinitePoint(survey["sharedPosition"], "surveyPoint.sharedPosition"),
                BaseInternal = FinitePoint(basePoint["position"], "basePoint.position"),
                BaseShared = FinitePoint(basePoint["sharedPosition"], "basePoint.sharedPosition"),
                Levels = levels.Select(level => new PositionLevel { Name = (string)level["name"], ElevationM = Finite(level["elevation"], "elevation of " + (string)level["name"]), ProjectElevationM = level["projectElevation"].Value<double>() }).ToList(),
                LevelBase = (string)bases[0],
                Units = new[] { "system", "length", "lengthSystem" }.ToDictionary(name => name, name => units?[name]?.Type == JTokenType.String ? (string)units[name] : null),
            };
        }

        // What of the file differs from keyRules.site: the angle, the level names in order, the step of each level over the lowest
        // within half a mm and the level base, empty when none does
        public static List<string> AgainstRules(ProjectPositionFile file, JObject siteRules)
        {
            var found = new List<string>();
            JToken angle = siteRules?["angleToTrueNorthDeg"];
            if (angle == null || angle.Type == JTokenType.Null || Math.Abs(file.Position.AngleDeg.Value - angle.Value<double>()) > SameDeg)
            {
                found.Add("angle " + ExactDouble.Text(file.Position.AngleDeg.Value) + ", keyRules.site.angleToTrueNorthDeg " + (angle == null ? "None" : angle.ToString(Newtonsoft.Json.Formatting.None)));
            }
            List<string> names = file.Levels.Select(level => level.Name).ToList();
            JArray ruleNames = siteRules?["levels"] as JArray;
            if (ruleNames == null || !ruleNames.Select(item => (string)item).SequenceEqual(names))
            {
                found.Add("levels " + string.Join(", ", names) + ", keyRules.site.levels " + (ruleNames == null ? "None" : ruleNames.ToString(Newtonsoft.Json.Formatting.None)));
            }
            double lowest = file.Levels[0].ElevationM;
            foreach (PositionLevel level in file.Levels.Skip(1))
            {
                JToken rule = (siteRules?["levelStepsM"] as JObject)?[level.Name];
                if (rule == null || rule.Type == JTokenType.Null || Math.Abs(level.ElevationM - lowest - rule.Value<double>()) * 1000.0 > SameMm)
                {
                    found.Add("the step of " + level.Name + ", keyRules.site.levelStepsM " + (rule == null ? "None" : rule.ToString(Newtonsoft.Json.Formatting.None)));
                }
            }
            JToken levelBase = siteRules?["levelBase"];
            if (levelBase == null || levelBase.Type != JTokenType.String || (string)levelBase != file.LevelBase)
            {
                found.Add("level base " + file.LevelBase + ", keyRules.site.levelBase " + (levelBase == null ? "None" : levelBase.ToString(Newtonsoft.Json.Formatting.None)));
            }
            return found;
        }

        // The second route: the internal point of a shared point by the survey point pair of the key read and its angle to true north,
        // never by its project position
        public static double[] BySurveyPair(JObject document, double[] shared)
        {
            double[] surveyInternal = FinitePoint(document["surveyPoint"]?["position"], "surveyPoint.position");
            double[] surveyShared = FinitePoint(document["surveyPoint"]?["sharedPosition"], "surveyPoint.sharedPosition");
            double angle = Finite(document["angleToTrueNorthDeg"], "angleToTrueNorthDeg") * Math.PI / 180.0;
            double east = shared[0] - surveyShared[0], north = shared[1] - surveyShared[1];
            return new[]
            {
                Math.Cos(angle) * east + Math.Sin(angle) * north + surveyInternal[0],
                -Math.Sin(angle) * east + Math.Cos(angle) * north + surveyInternal[1],
                shared[2] - surveyShared[2] + surveyInternal[2],
            };
        }

        // The shared points of the check points in their order: control 1 to 3 at elevation 0, plots min and plots max of the face
        // centroids at elevation 0, the survey point and the project base point of the file
        public static List<KeyValuePair<string, double[]>> CadPoints(IReadOnlyList<double[]> controls, IReadOnlyList<double[]> centroids, ProjectPositionFile file)
        {
            if (centroids.Count == 0)
            {
                throw new InvalidDataException("the baseplan holds no face, the check points take plots min and plots max from its faces (R353)");
            }
            var points = controls.Select((point, k) => new KeyValuePair<string, double[]>("control " + (k + 1), new[] { point[0], point[1], 0.0 })).ToList();
            points.Add(new KeyValuePair<string, double[]>("plots min", new[] { centroids.Min(point => point[0]), centroids.Min(point => point[1]), 0.0 }));
            points.Add(new KeyValuePair<string, double[]>("plots max", new[] { centroids.Max(point => point[0]), centroids.Max(point => point[1]), 0.0 }));
            points.Add(new KeyValuePair<string, double[]>("survey point", (double[])file.SurveyShared.Clone()));
            points.Add(new KeyValuePair<string, double[]>("project base point", (double[])file.BaseShared.Clone()));
            return points;
        }

        // Each check point by name: its shared point, its internal point by the file, by the survey pair of the key read when a read is
        // given, the gap of the two in mm and whether its gap at decimals by R385 is at most limitMm
        public static Dictionary<string, Dictionary<string, object>> CheckPoints(ProjectPositionFile file, IReadOnlyList<double[]> controls, IReadOnlyList<double[]> centroids, JObject document,
            double limitMm, int decimals)
        {
            var rows = new Dictionary<string, Dictionary<string, object>>();
            foreach (KeyValuePair<string, double[]> point in CadPoints(controls, centroids, file))
            {
                double[] internalPoint = SiteCoordinates.ToInternal(file.Position, point.Value);
                double[] key = document == null ? null : BySurveyPair(document, point.Value);
                double? gap = key == null ? (double?)null : SiteCoordinates.Gap(internalPoint, key) * 1000.0;
                rows[point.Key] = new Dictionary<string, object>
                {
                    ["sharedM"] = point.Value, ["internalM"] = internalPoint, ["keyInternalM"] = key, ["gapMm"] = gap,
                    ["pass"] = gap == null ? (object)null : Rounding.Fixed(gap.Value, decimals) <= limitMm,
                };
            }
            return rows;
        }

        // The step position of the parity, one item
        public Dictionary<string, Dictionary<string, object>> PositionStepItems()
        {
            return new Dictionary<string, Dictionary<string, object>>
            {
                [PositionStep] = new Dictionary<string, object>
                {
                    ["key"] = Key, ["projectPositionM"] = new[] { Position.EastM.Value, Position.NorthM.Value, Position.ElevationM.Value }, ["angleDeg"] = Position.AngleDeg.Value,
                    ["surveyInternalM"] = SurveyInternal, ["surveySharedM"] = SurveyShared, ["baseInternalM"] = BaseInternal, ["baseSharedM"] = BaseShared,
                    ["levelNames"] = Levels.Select(level => level.Name).ToList(), ["levelElevationsM"] = Levels.Select(level => level.ElevationM).ToArray(),
                    ["levelProjectElevationsM"] = Levels.Select(level => level.ProjectElevationM).ToArray(), ["levelBase"] = LevelBase,
                },
            };
        }

        private static JArray Fixed(IEnumerable<double> values, int decimals)
        {
            return new JArray(values.Select(value => Rounding.Fixed(value, decimals)));
        }

        // The file as written, every length by R385 at 3 decimals and the angles at 6, as position_file.written gives it
        public JObject Written()
        {
            return new JObject
            {
                ["format"] = Format, ["formatVersion"] = FormatVersion, ["key"] = Key, ["model"] = Model,
                ["projectPosition"] = new JObject
                {
                    ["eastM"] = Rounding.Fixed(Position.EastM.Value, 3), ["northM"] = Rounding.Fixed(Position.NorthM.Value, 3), ["elevationM"] = Rounding.Fixed(Position.ElevationM.Value, 3),
                    ["angleDeg"] = Rounding.Fixed(Position.AngleDeg.Value, 6),
                },
                ["angleToTrueNorthDeg"] = Rounding.Fixed(AngleToTrueNorthDeg, 6),
                ["surveyPoint"] = new JObject { ["internalM"] = Fixed(SurveyInternal, 3), ["sharedM"] = Fixed(SurveyShared, 3) },
                ["projectBasePoint"] = new JObject { ["internalM"] = Fixed(BaseInternal, 3), ["sharedM"] = Fixed(BaseShared, 3) },
                ["levels"] = new JArray(Levels.Select(level => new JObject { ["name"] = level.Name, ["elevationM"] = Rounding.Fixed(level.ElevationM, 3), ["projectElevationM"] = Rounding.Fixed(level.ProjectElevationM, 3) })),
                ["levelBase"] = LevelBase,
                ["units"] = JObject.FromObject(Units),
            };
        }

        // keyRules.site of a level profile of the rules profile: the request names one or the profile gives levelProfiles.default
        public static JObject SiteRules(JObject rules, string levelProfile)
        {
            JObject profiles = rules["levelProfiles"] as JObject ?? throw new InvalidDataException("the rules profile holds no levelProfiles (R277)");
            string name = string.IsNullOrEmpty(levelProfile) ? (string)profiles["default"] : levelProfile;
            JObject profile = name == null ? null : profiles[name] as JObject;
            if (profile == null)
            {
                throw new InvalidDataException("the rules profile holds no level profile " + (name ?? "default") + " (R277)");
            }
            return (profile["keyRules"] as JObject)?["site"] as JObject ?? new JObject();
        }
    }
}
