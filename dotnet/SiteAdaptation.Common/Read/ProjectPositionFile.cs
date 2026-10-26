using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R353. addin.position of config/project.json: the position file button 1 reads, the pass limit of its check points in mm, the
    // decimals their gap is shown and judged at, the key whose read the file is written from, majority.siteReference of the group of
    // majority.check.group, and the keys of that group in majority.groups, as python/position_file.settings_of, key_of and group_of take them
    public sealed class PositionSettings
    {
        public string File { get; private set; }
        public double CheckPointLimitMm { get; private set; }
        public int GapDecimals { get; private set; }
        public string Key { get; private set; }
        public List<string> Group { get; private set; }

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
            JArray keys = (majority["groups"] as JObject)?[group] as JArray;
            if (keys == null || keys.Count == 0 || keys.All(item => (string)item != (string)key))
            {
                throw new InvalidDataException("majority.groups names no keys of the group " + group + " with its reference key (R353)");
            }
            return new PositionSettings { File = file, CheckPointLimitMm = limitMm, GapDecimals = (int)decimals.Value<long>(), Key = (string)key, Group = keys.Select(item => (string)item).ToList() };
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
    // internal and shared coordinates, the levels on their one Elevation Base, the units, the length accuracy and the active site with the
    // sites, held against every value keyRules.site gives, the reference key held against every other key of its group as R331 holds a
    // read, and the check points of button 1, the corners of the Generic floors of the extreme plots of each key read at full precision
    // held against the vertices of the baseplan face of the plot through the position
    public sealed class ProjectPositionFile
    {
        public const string Format = "site-adaptation-position";
        public const int FormatVersion = 1;
        public const string PositionStep = "position";
        public const string CheckPointsStep = "checkPoints";
        public const string KeyReadRole = "keyRead";
        public const string KeyPointsRole = "keyPoints";
        public const string RulesRole = "rules";
        public static readonly string[] PositionRoles = { KeyReadRole, RulesRole, KeyReadRole + ":*" };
        public static readonly string[] CheckPointRoles = { KeyReadRole, RulesRole, "baseplan", KeyPointsRole + ":*" };
        // A level step of the file is the one of the rules within half a mm, the angle within a millionth of a degree, as R331 holds a read
        private const double SameMm = 0.5;
        private const double SameDeg = 1e-6;
        private static readonly string[] UnitFields = { "system", "length", "lengthSystem" };
        private static readonly string[] Points = { "surveyPoint.position", "surveyPoint.sharedPosition", "basePoint.position", "basePoint.sharedPosition" };

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
        public double? LengthAccuracy { get; private set; }
        public string ActiveSite { get; private set; }
        public JToken Sites { get; private set; }
        public string PlaceName { get; private set; }

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

        private static string Text(JToken value)
        {
            return value == null ? "None" : value.Type == JTokenType.String ? (string)value : value.ToString(Newtonsoft.Json.Formatting.None);
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
            JToken accuracy = units?["lengthAccuracy"];
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
                Units = UnitFields.ToDictionary(name => name, name => units?[name]?.Type == JTokenType.String ? (string)units[name] : null),
                LengthAccuracy = accuracy != null && (accuracy.Type == JTokenType.Float || accuracy.Type == JTokenType.Integer) ? accuracy.Value<double>() : (double?)null,
                ActiveSite = site["activeSite"]?.Type == JTokenType.String ? (string)site["activeSite"] : null,
                Sites = site["sites"]?.DeepClone() ?? JValue.CreateNull(),
                PlaceName = site["placeName"]?.Type == JTokenType.String ? (string)site["placeName"] : null,
            };
        }

        // What of the file differs from keyRules.site, every value it gives: oneSet, the angle, the active site, the sites, the level names
        // in order, the step of each level over the lowest within half a mm, the units, the level base and the length accuracy of the key of
        // the file, empty when none does
        public static List<string> AgainstRules(ProjectPositionFile file, JObject siteRules)
        {
            var found = new List<string>();
            JToken oneSet = siteRules?["oneSet"];
            if (oneSet == null || oneSet.Type != JTokenType.Boolean || !(bool)oneSet)
            {
                found.Add("keyRules.site.oneSet " + Text(oneSet) + ", the keys hold no one set");
            }
            JToken angle = siteRules?["angleToTrueNorthDeg"];
            if (angle == null || angle.Type == JTokenType.Null || Math.Abs(file.Position.AngleDeg.Value - angle.Value<double>()) > SameDeg)
            {
                found.Add("angle " + ExactDouble.Text(file.Position.AngleDeg.Value) + ", keyRules.site.angleToTrueNorthDeg " + Text(angle));
            }
            JToken activeSite = siteRules?["activeSite"];
            if (activeSite == null || activeSite.Type != JTokenType.String || (string)activeSite != file.ActiveSite)
            {
                found.Add("activeSite " + (file.ActiveSite ?? "None") + ", keyRules.site.activeSite " + Text(activeSite));
            }
            JToken sites = siteRules?["sites"];
            if (sites == null || !JToken.DeepEquals(sites, file.Sites))
            {
                found.Add("sites " + Text(file.Sites) + ", keyRules.site.sites " + Text(sites));
            }
            List<string> names = file.Levels.Select(level => level.Name).ToList();
            JArray ruleNames = siteRules?["levels"] as JArray;
            if (ruleNames == null || !ruleNames.Select(item => (string)item).SequenceEqual(names))
            {
                found.Add("levels " + string.Join(", ", names) + ", keyRules.site.levels " + Text(ruleNames));
            }
            double lowest = file.Levels[0].ElevationM;
            foreach (PositionLevel level in file.Levels.Skip(1))
            {
                JToken rule = (siteRules?["levelStepsM"] as JObject)?[level.Name];
                if (rule == null || rule.Type == JTokenType.Null || Math.Abs(level.ElevationM - lowest - rule.Value<double>()) * 1000.0 > SameMm)
                {
                    found.Add("the step of " + level.Name + ", keyRules.site.levelStepsM " + Text(rule));
                }
            }
            JObject ruleUnits = siteRules?["units"] as JObject;
            if (ruleUnits == null || UnitFields.Any(name => (ruleUnits[name]?.Type == JTokenType.String ? (string)ruleUnits[name] : null) != file.Units[name]))
            {
                found.Add("units " + string.Join(", ", UnitFields.Select(name => name + " " + (file.Units[name] ?? "None"))) + ", keyRules.site.units " + Text(ruleUnits));
            }
            JToken levelBase = siteRules?["levelBase"];
            if (levelBase == null || levelBase.Type != JTokenType.String || (string)levelBase != file.LevelBase)
            {
                found.Add("level base " + file.LevelBase + ", keyRules.site.levelBase " + Text(levelBase));
            }
            JToken accuracy = (siteRules?["lengthAccuracy"] as JObject)?[file.Key];
            if (accuracy == null || (accuracy.Type != JTokenType.Float && accuracy.Type != JTokenType.Integer) || !file.LengthAccuracy.HasValue || accuracy.Value<double>() != file.LengthAccuracy.Value)
            {
                found.Add("lengthAccuracy " + (file.LengthAccuracy.HasValue ? ExactDouble.Text(file.LengthAccuracy.Value) : "None") + " of " + file.Key + ", keyRules.site.lengthAccuracy " + Text(accuracy));
            }
            return found;
        }

        private static double[] PartPoint(JObject document, string path)
        {
            string[] parts = path.Split('.');
            JArray items = (document[parts[0]] as JObject)?[parts[1]] as JArray;
            return items == null ? null : items.Select(item => item.Value<double>()).ToArray();
        }

        // cad_links.angle_gap: the gap of two angles in degrees, the turn closed
        private static double AngleGap(double first, double second)
        {
            double gap = PyMod(Math.Abs(first - second), 360.0);
            return Math.Min(gap, 360.0 - gap);
        }

        private static double PyMod(double value, double modulus)
        {
            double rest = value % modulus;
            return rest < 0 ? rest + modulus : rest;
        }

        private static double Distance(double[] a, double[] b)
        {
            return Math.Sqrt(Enumerable.Range(0, Math.Min(a.Length, b.Length)).Sum(index => (a[index] - b[index]) * (a[index] - b[index])));
        }

        // What of one key read differs from the read of the reference key, as key_rules.site_facts holds it (R331), by field: the gap in mm of
        // a point, the position or a level, of the angle in degrees, or the value of a name, a unit or the level base
        private static SortedDictionary<string, string> Differ(JObject document, JObject reference)
        {
            var differ = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in Points)
            {
                double[] mine = PartPoint(document, path), theirs = PartPoint(reference, path);
                if (mine == null || theirs == null)
                {
                    continue;
                }
                double gap = Distance(mine, theirs) * 1000.0;
                if (gap > SameMm)
                {
                    differ[path] = SheetRows.PyRepr(Rounding.Fixed(gap, 1)) + " mm";
                }
            }
            JToken angle = document["angleToTrueNorthDeg"], baseAngle = reference["angleToTrueNorthDeg"];
            if (angle != null && angle.Type != JTokenType.Null && baseAngle != null && baseAngle.Type != JTokenType.Null)
            {
                double gap = AngleGap(angle.Value<double>(), baseAngle.Value<double>());
                if (gap > SameDeg)
                {
                    differ["angleToTrueNorthDeg"] = SheetRows.PyRepr(Rounding.Fixed(gap, 6)) + " deg";
                }
            }
            JObject site = document["site"] as JObject, baseSite = reference["site"] as JObject;
            if (site == null || baseSite == null)
            {
                return differ;
            }
            Func<JObject, double[]> position = item => new[] { "eastWest", "northSouth", "elevation" }.Select(name => (item["projectPosition"] as JObject)?[name]?.Value<double>() ?? double.NaN).ToArray();
            Func<JObject, double> turn = item => (item["projectPosition"] as JObject)?["angleDeg"]?.Value<double>() ?? double.NaN;
            double positionGap = Distance(position(site), position(baseSite)) * 1000.0;
            if (positionGap > SameMm || AngleGap(turn(site), turn(baseSite)) > SameDeg)
            {
                differ["projectPosition"] = SheetRows.PyRepr(Rounding.Fixed(positionGap, 1)) + " mm";
            }
            Func<JObject, Dictionary<string, double?>> levels = item => (item["levels"] as JArray ?? new JArray()).OfType<JObject>()
                .GroupBy(level => (string)level["name"]).ToDictionary(group => group.Key, group => group.Last()["elevation"]?.Type == JTokenType.Float || group.Last()["elevation"]?.Type == JTokenType.Integer ? group.Last()["elevation"].Value<double>() : (double?)null);
            Dictionary<string, double?> mineLevels = levels(site), theirLevels = levels(baseSite);
            foreach (string name in mineLevels.Keys.Union(theirLevels.Keys).OrderBy(name => name, StringComparer.Ordinal))
            {
                double? mine = mineLevels.TryGetValue(name, out double? a) ? a : null, theirs = theirLevels.TryGetValue(name, out double? b) ? b : null;
                if (mine == null || theirs == null)
                {
                    differ["levels." + name] = "None";
                }
                else if (Math.Abs(mine.Value - theirs.Value) * 1000.0 > SameMm)
                {
                    differ["levels." + name] = SheetRows.PyRepr(Rounding.Fixed(Math.Abs(mine.Value - theirs.Value) * 1000.0, 1)) + " mm";
                }
            }
            foreach (string name in new[] { "activeSite", "sites", "placeName" })
            {
                if (!JToken.DeepEquals(site[name] ?? JValue.CreateNull(), baseSite[name] ?? JValue.CreateNull()))
                {
                    differ[name] = Text(site[name]);
                }
            }
            foreach (string name in UnitFields)
            {
                if (!JToken.DeepEquals((site["units"] as JObject)?[name] ?? JValue.CreateNull(), (baseSite["units"] as JObject)?[name] ?? JValue.CreateNull()))
                {
                    differ[name] = Text((site["units"] as JObject)?[name]);
                }
            }
            Func<JObject, string> levelBase = item => string.Join("|", (item["levels"] as JArray ?? new JArray()).OfType<JObject>().Where(level => level["elevationBase"] != null && level["elevationBase"].Type != JTokenType.Null)
                .Select(level => level["elevationBase"].ToString()).Distinct().OrderBy(text => text, StringComparer.Ordinal));
            if (levelBase(site) != levelBase(baseSite))
            {
                differ["levelBase"] = levelBase(site);
            }
            return differ;
        }

        // python/position_file.set_differs: the reference key read held against the read of every other key of its group as R331 holds a
        // read, one line per key and field that differs, and a key of the group with no read, empty when the set is one
        public static List<string> SetDiffers(JObject reference, IReadOnlyDictionary<string, JObject> others, string referenceKey, IReadOnlyList<string> group)
        {
            List<string> found = group.Where(key => key != referenceKey && !others.ContainsKey(key)).Select(key => key + " no read, every key of the group is held against the reference").ToList();
            foreach (KeyValuePair<string, JObject> other in others.Where(item => item.Key != referenceKey).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                foreach (KeyValuePair<string, string> field in Differ(other.Value, reference))
                {
                    found.Add(other.Key + " " + field.Key + " " + field.Value);
                }
            }
            return found;
        }

        // python/position_file.generic_types: the type names of the Generic floors of a key, floors.keyTypes of its code, else floors.types
        public static List<string> GenericTypes(JObject config, string key)
        {
            JObject floors = config["floors"] as JObject ?? throw new InvalidDataException("config holds no floors (R112)");
            JArray own = ((floors["keyTypes"] as JObject)?[key.ToUpperInvariant()] as JObject)?["generic"] as JArray;
            JArray types = own ?? (floors["types"] as JObject)?["generic"] as JArray ?? throw new InvalidDataException("config holds no floors.types.generic (R112)");
            return types.Select(item => (string)item).ToList();
        }

        // python/position_file.plot_order: a plot by its number, a plot of digits as the number it writes, every other after them as its text
        private static IComparable[] PlotOrder(string plot)
        {
            bool digits = plot.Length > 0 && plot.All(letter => letter >= '0' && letter <= '9');
            return digits ? new IComparable[] { 0, System.Numerics.BigInteger.Parse(plot, CultureInfo.InvariantCulture), "" } : new IComparable[] { 1, System.Numerics.BigInteger.Zero, plot };
        }

        private static int ComparePlots(string a, string b)
        {
            IComparable[] first = PlotOrder(a), second = PlotOrder(b);
            for (int index = 0; index < first.Length; index++)
            {
                int order = index == 2 ? string.CompareOrdinal((string)first[index], (string)second[index]) : first[index].CompareTo(second[index]);
                if (order != 0)
                {
                    return order;
                }
            }
            return 0;
        }

        private static double[] Mean(JArray corners)
        {
            double x = 0, y = 0;
            foreach (JToken corner in corners)
            {
                x += corner[0].Value<double>();
            }
            foreach (JToken corner in corners)
            {
                y += corner[1].Value<double>();
            }
            return new[] { x / corners.Count, y / corners.Count };
        }

        // python/position_file.chosen_floors: the Generic floors of the extreme plots of a key, the plots with one floor of the types whose
        // corners have the least mean east, the greatest mean east, the least mean north and the greatest mean north, a tie to the lower
        // plot, each plot once in that order
        public static List<KeyValuePair<string, JObject>> ChosenFloors(JObject points, IReadOnlyList<string> types)
        {
            var byPlot = new Dictionary<string, List<JObject>>();
            foreach (JObject floor in (points["floors"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string plot = floor["plot"]?.Type == JTokenType.String ? (string)floor["plot"] : null;
                if (types.Contains((string)floor["typeName"]) && !string.IsNullOrEmpty(plot) && (floor["corners"] as JArray)?.Count >= 3)
                {
                    if (!byPlot.ContainsKey(plot))
                    {
                        byPlot[plot] = new List<JObject>();
                    }
                    byPlot[plot].Add(floor);
                }
            }
            Dictionary<string, JObject> single = byPlot.Where(item => item.Value.Count == 1).ToDictionary(item => item.Key, item => item.Value[0]);
            if (single.Count == 0)
            {
                throw new InvalidDataException("the key points of " + (string)points["model"] + " hold no Generic floor of " + string.Join(", ", types) + " on a plot of one such floor, the check points need one (R353)");
            }
            Dictionary<string, double[]> mean = single.ToDictionary(item => item.Key, item => Mean((JArray)item.Value["corners"]));
            var picked = new List<string>();
            foreach (int[] pick in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 1 }, new[] { 1, -1 } })
            {
                List<string> plots = single.Keys.ToList();
                plots.Sort((a, b) =>
                {
                    int order = (pick[1] * mean[a][pick[0]]).CompareTo(pick[1] * mean[b][pick[0]]);
                    return order != 0 ? order : ComparePlots(a, b);
                });
                if (!picked.Contains(plots[0]))
                {
                    picked.Add(plots[0]);
                }
            }
            return picked.Select(plot => new KeyValuePair<string, JObject>(plot, single[plot])).ToList();
        }

        private static double[] Corner(JToken value)
        {
            return value.Select(item => item.Value<double>()).ToArray();
        }

        // python/position_file.check_points: per key the corners of the Generic floors of its extreme plots, each with its CAD point, the
        // nearest vertex of the baseplan face that holds the middle of its floor taken to shared coordinates by the file, the gap of that
        // point taken to internal by the file to the corner in plan in mm, the gap to the shared corner Revit gives, and whether it passes
        public static Dictionary<string, Dictionary<string, object>> CheckPoints(ProjectPositionFile file, IReadOnlyList<KeyValuePair<string, JObject>> pointsByKey,
            IReadOnlyDictionary<string, PlanFace> faces, IReadOnlyDictionary<string, List<string>> typesByKey, double limitMm, int decimals)
        {
            var index = new FaceIndex(faces.Values);
            var rows = new Dictionary<string, Dictionary<string, object>>();
            foreach (KeyValuePair<string, JObject> key in pointsByKey)
            {
                foreach (KeyValuePair<string, JObject> chosen in ChosenFloors(key.Value, typesByKey[key.Key]))
                {
                    var corners = (JArray)chosen.Value["corners"];
                    var sharedCorners = (JArray)chosen.Value["sharedCorners"];
                    double[] centre = Mean(corners);
                    double[] middle = SiteCoordinates.ToShared(file.Position, new[] { centre[0], centre[1], 0.0 });
                    string face = index.FaceOf(middle[0], middle[1]);
                    List<double[]> vertices = face == null ? new List<double[]>() : faces[face].Rings.SelectMany(ring => ring).ToList();
                    for (int k = 0; k < corners.Count; k++)
                    {
                        double[] corner = Corner(corners[k]), sharedCorner = Corner(sharedCorners[k]);
                        double[] cad = null, internalPoint = null;
                        double? gap = null, revit = null;
                        if (vertices.Count > 0)
                        {
                            double[] at = SiteCoordinates.ToShared(file.Position, corner);
                            double best = double.PositiveInfinity;
                            foreach (double[] vertex in vertices)
                            {
                                double distance = LocationPlan.Hypot(vertex[0] - at[0], vertex[1] - at[1]);
                                if (distance < best)
                                {
                                    best = distance;
                                    cad = new[] { vertex[0], vertex[1] };
                                }
                            }
                            double[] taken = SiteCoordinates.ToInternal(file.Position, new[] { cad[0], cad[1], 0.0 });
                            internalPoint = new[] { taken[0], taken[1] };
                            gap = LocationPlan.Hypot(internalPoint[0] - corner[0], internalPoint[1] - corner[1]) * 1000.0;
                            revit = LocationPlan.Hypot(cad[0] - sharedCorner[0], cad[1] - sharedCorner[1]) * 1000.0;
                        }
                        rows[key.Key + "/" + chosen.Key + "/" + k.ToString(CultureInfo.InvariantCulture)] = new Dictionary<string, object>
                        {
                            ["key"] = key.Key, ["plot"] = chosen.Key, ["face"] = face, ["sharedM"] = cad, ["internalM"] = internalPoint, ["keyInternalM"] = new[] { corner[0], corner[1] },
                            ["keySharedM"] = new[] { sharedCorner[0], sharedCorner[1] }, ["gapMm"] = gap, ["revitGapMm"] = revit,
                            ["pass"] = gap.HasValue && Rounding.Fixed(gap.Value, decimals) <= limitMm,
                        };
                    }
                }
            }
            return rows;
        }

        // The step position of the parity, one item, with the keys the reference key was held against
        public Dictionary<string, Dictionary<string, object>> PositionStepItems(IReadOnlyList<string> held)
        {
            return new Dictionary<string, Dictionary<string, object>>
            {
                [PositionStep] = new Dictionary<string, object>
                {
                    ["key"] = Key, ["projectPositionM"] = new[] { Position.EastM.Value, Position.NorthM.Value, Position.ElevationM.Value }, ["angleDeg"] = Position.AngleDeg.Value,
                    ["surveyInternalM"] = SurveyInternal, ["surveySharedM"] = SurveyShared, ["baseInternalM"] = BaseInternal, ["baseSharedM"] = BaseShared,
                    ["levelNames"] = Levels.Select(level => level.Name).ToList(), ["levelElevationsM"] = Levels.Select(level => level.ElevationM).ToArray(),
                    ["levelProjectElevationsM"] = Levels.Select(level => level.ProjectElevationM).ToArray(), ["levelBase"] = LevelBase,
                    ["activeSite"] = ActiveSite, ["sites"] = Sites.DeepClone(), ["heldAgainst"] = new JArray(held),
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
                ["lengthAccuracy"] = LengthAccuracy.HasValue ? new JValue(LengthAccuracy.Value) : JValue.CreateNull(),
                ["activeSite"] = ActiveSite, ["sites"] = Sites.DeepClone(), ["placeName"] = PlaceName,
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
