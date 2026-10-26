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
        public const string KeyCopiesRole = "keyCopies";
        public const string LabelsRole = "labels";
        public const string PlotShiftsRole = "plotShifts";
        public const string NoPoints = "no key points";
        public const string NotMade = "the check points were not made, the request names no baseplan and no key points (R353)";
        // The kind location_plan writes for the client code labels of the unit plots, the rows the build and the faces step number by
        public const string PlotKind = "clientCode";
        public static readonly string[] PositionRoles = { KeyReadRole, RulesRole, KeyCopiesRole, KeyReadRole + ":*" };
        public static readonly string[] CheckPointRoles = { KeyReadRole, RulesRole, KeyCopiesRole, "baseplan", LabelsRole, PlotShiftsRole, KeyPointsRole + ":*" };
        // A level step of the file is the one of the rules within half a mm, the angle within a millionth of a degree, as R331 holds a read
        private const double SameMm = 0.5;
        private const double SameDeg = 1e-6;
        private static readonly string[] UnitFields = { "system", "length", "lengthSystem" };
        private static readonly string[] Points = { "surveyPoint.position", "surveyPoint.sharedPosition", "basePoint.position", "basePoint.sharedPosition" };
        private static readonly string[] PositionParts = { "eastWest", "northSouth", "elevation", "angleDeg" };
        // The job of a work copy the read job names, <copy stem>_TEST-<job>, as R200 names it
        private static readonly System.Text.RegularExpressions.Regex WorkJob = new System.Text.RegularExpressions.Regex(@"\A[A-Z][A-Z0-9]{1,3}\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        private static readonly string[][] WhyCounts =
        {
            new[] { "overLimit", "gap over the limit" }, new[] { "noFace", "no face" }, new[] { "otherFace", "face of another plot" }, new[] { "noNumber", "no face numbered" },
            new[] { "twoFaces", "number on two faces" }, new[] { "merged", "vertex merged over the limit" }, new[] { "noPoints", NoPoints },
        };

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

        // python/position_file.text_of: a value of a read as a line names it, None, a text as it is, a number as Python prints it, a list
        // or an object as compact JSON
        private static string PyText(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                return "None";
            }
            switch (value.Type)
            {
                case JTokenType.String:
                    return (string)value;
                case JTokenType.Boolean:
                    return (bool)value ? "True" : "False";
                case JTokenType.Float:
                    return SheetRows.PyRepr(value.Value<double>());
                case JTokenType.Integer:
                    return value.ToString(Newtonsoft.Json.Formatting.None);
                default:
                    return value.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        // python/position_file.json_kind: the JSON type of a token
        private static string JsonKind(JToken value)
        {
            switch (value.Type)
            {
                case JTokenType.Boolean:
                    return "boolean";
                case JTokenType.Integer:
                case JTokenType.Float:
                    return "number";
                case JTokenType.String:
                    return "string";
                case JTokenType.Array:
                    return "array";
                case JTokenType.Object:
                    return "object";
                default:
                    return "null";
            }
        }

        private static bool IsFinite(JToken value)
        {
            return value != null && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer) && !double.IsNaN(value.Value<double>()) && !double.IsInfinity(value.Value<double>());
        }

        private static bool IsPoint(JToken value)
        {
            return value is JArray items && items.Count == 3 && items.All(IsFinite);
        }

        private static bool IsText(JToken value)
        {
            return value != null && value.Type == JTokenType.String;
        }

        // python/position_file.read_faults: every value the set of the keys is held on that a key read is missing or did not read, one
        // line per field, <key> <field> missing or not read
        public static List<string> ReadFaults(JObject document, string key)
        {
            var found = new List<string>();
            foreach (string path in Points)
            {
                string[] parts = path.Split('.');
                if (!IsPoint((document[parts[0]] as JObject)?[parts[1]]))
                {
                    found.Add(key + " " + path + " missing");
                }
            }
            if (!IsFinite(document["angleToTrueNorthDeg"]))
            {
                found.Add(key + " angleToTrueNorthDeg missing");
            }
            JObject site = document["site"] as JObject;
            if (site == null)
            {
                found.Add(key + " site missing");
                return found;
            }
            var unread = new HashSet<string>((site["notRead"] as JObject)?.Properties().Select(item => item.Name) ?? Enumerable.Empty<string>());
            if (site["notRead"] != null && site["notRead"].Type != JTokenType.Null && site["notRead"].Type != JTokenType.Object)
            {
                found.Add(key + " site.notRead is no object");
            }
            found.AddRange(unread.OrderBy(name => name, StringComparer.Ordinal).Select(name => key + " site." + name + " not read"));
            if (!unread.Contains("projectPosition"))
            {
                JObject at = site["projectPosition"] as JObject;
                if (at == null)
                {
                    found.Add(key + " site.projectPosition missing");
                }
                else
                {
                    found.AddRange(PositionParts.Where(part => !IsFinite(at[part])).Select(part => key + " site.projectPosition." + part + " missing"));
                }
            }
            if (!unread.Contains("levels"))
            {
                JArray levels = site["levels"] as JArray;
                if (levels == null || levels.Count == 0)
                {
                    found.Add(key + " site.levels missing");
                }
                else
                {
                    for (int index = 0; index < levels.Count; index++)
                    {
                        string at = key + " site.levels[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                        JObject level = levels[index] as JObject;
                        if (level == null)
                        {
                            found.Add(at + " missing");
                            continue;
                        }
                        if (!IsText(level["name"]))
                        {
                            found.Add(at + ".name missing");
                        }
                        if (!IsFinite(level["elevation"]))
                        {
                            found.Add(at + ".elevation missing");
                        }
                        if (!IsText(level["elevationBase"]))
                        {
                            found.Add(at + ".elevationBase missing");
                        }
                    }
                }
            }
            foreach (string name in new[] { "activeSite", "placeName" })
            {
                if (!unread.Contains(name) && !IsText(site[name]))
                {
                    found.Add(key + " site." + name + " missing");
                }
            }
            JArray sites = site["sites"] as JArray;
            if (!unread.Contains("sites") && (sites == null || sites.Count == 0 || !sites.All(IsText)))
            {
                found.Add(key + " site.sites missing");
            }
            if (!unread.Contains("units"))
            {
                JObject units = site["units"] as JObject;
                if (units == null)
                {
                    found.Add(key + " site.units missing");
                }
                else
                {
                    found.AddRange(UnitFields.Where(part => !IsText(units[part])).Select(part => key + " site.units." + part + " missing"));
                    if (!IsFinite(units["lengthAccuracy"]))
                    {
                        found.Add(key + " site.units.lengthAccuracy missing");
                    }
                }
            }
            return found;
        }

        // python/position_file.answer_copies: the stem of the copy of each answer key by its key in lower case, from answerKeys of config
        // held against key_copies.json beside the copies (R312), a code the manifest lists with another copy or does not list refused
        public static Dictionary<string, string> AnswerCopies(JObject answerKeys, JObject manifest)
        {
            var listed = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (JToken item in manifest?["copies"] as JArray ?? new JArray())
            {
                if (item is JObject entry && IsText(entry["code"]))
                {
                    listed[(string)entry["code"]] = entry["copy"];
                }
            }
            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JToken item in answerKeys?["keys"] as JArray ?? new JArray())
            {
                JToken code = (item as JObject)?["code"], copy = (item as JObject)?["copy"];
                if (!IsText(code) || !IsText(copy))
                {
                    throw new InvalidDataException("answerKeys names a key of no code or no copy, a key read is held to its key by its copy (R353, R312)");
                }
                JToken other = listed.TryGetValue((string)code, out JToken given) ? given : null;
                if (!IsText(other) || (string)other != (string)copy)
                {
                    throw new InvalidDataException("answerKeys names for " + (string)code + " the copy " + (string)copy + " and key_copies.json " + PyText(other) + ", a key read is held to its key by one copy (R353, R312)");
                }
                string stem = (string)copy;
                found[((string)code).ToLowerInvariant()] = stem.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) ? stem.Substring(0, stem.Length - 4) : stem;
            }
            return found;
        }

        // python/position_file.model_faults: a read or a key points file is of its key when the model it names is a work copy of the copy of
        // that key, <copy stem>_TEST-<job>, else one line naming the key and the model
        public static List<string> ModelFaults(JToken name, string key, IReadOnlyDictionary<string, string> copies, string what = "model")
        {
            if (!copies.TryGetValue(key, out string stem))
            {
                return new List<string> { key + " " + what + " " + PyText(name) + " of no answer key" };
            }
            string text = IsText(name) ? (string)name : "";
            string bare = text.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) ? text.Substring(0, text.Length - 4) : text;
            string prefix = stem + "_TEST-";
            if (bare.Length < prefix.Length || !string.Equals(bare.Substring(0, prefix.Length), prefix, StringComparison.OrdinalIgnoreCase) || !WorkJob.IsMatch(bare.Substring(prefix.Length)))
            {
                return new List<string> { key + " " + what + " " + PyText(name) + " is not a work copy " + prefix + "<job> of its answer key" };
            }
            return new List<string>();
        }

        public static ProjectPositionFile From(JObject document, string key)
        {
            JObject site = document["site"] as JObject ?? throw new InvalidDataException("the key read holds no site facts, a read of R311 is needed (R353)");
            if (site["notRead"] != null && site["notRead"].Type != JTokenType.Null && site["notRead"].Type != JTokenType.Object)
            {
                throw new InvalidDataException("the site facts of the key read hold notRead as a JSON " + JsonKind(site["notRead"]) + ", an object of the parts not read is needed (R353)");
            }
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
                    differ[name] = PyText(site[name]);
                }
            }
            foreach (string name in UnitFields)
            {
                if (!JToken.DeepEquals((site["units"] as JObject)?[name] ?? JValue.CreateNull(), (baseSite["units"] as JObject)?[name] ?? JValue.CreateNull()))
                {
                    differ[name] = PyText((site["units"] as JObject)?[name]);
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

        // python/position_file.set_differs: the reads of the keys of a group held as one set, per key of the group in its order a read
        // given, of its key (ModelFaults) and holding every value the set is held on (ReadFaults), then the reference key held against
        // every other key as R331 holds a read, one line per key and field that differs, empty when the set is one
        public static List<string> SetDiffers(IReadOnlyDictionary<string, JObject> documents, string referenceKey, IReadOnlyList<string> group, IReadOnlyDictionary<string, string> copies)
        {
            var found = new List<string>();
            foreach (string key in group)
            {
                if (!documents.TryGetValue(key, out JObject document))
                {
                    found.Add(key + " no read, every key of the group is held against the reference");
                    continue;
                }
                found.AddRange(ModelFaults(document["file"], key, copies));
                found.AddRange(ReadFaults(document, key));
            }
            if (found.Count > 0)
            {
                return found;
            }
            JObject reference = documents[referenceKey];
            foreach (string key in group.Distinct().OrderBy(key => key, StringComparer.Ordinal))
            {
                foreach (KeyValuePair<string, string> field in Differ(documents[key], reference))
                {
                    found.Add(key + " " + field.Key + " " + field.Value);
                }
            }
            return found;
        }

        // python/position_file.points_faults: the key points of a key held to its key, by the model they name, and to the position file,
        // by their full precision project position within half a mm and a millionth of a degree
        public static List<string> PointsFaults(JObject points, string key, IReadOnlyDictionary<string, string> copies, ProjectPositionFile position)
        {
            List<string> found = ModelFaults(points["model"], key, copies, "keyPoints model");
            JObject at = points["projectPosition"] as JObject ?? new JObject();
            if (!PositionParts.All(part => IsFinite(at[part])))
            {
                found.Add(key + " keyPoints projectPosition missing");
                return found;
            }
            double gap = Distance(new[] { at["eastWest"].Value<double>(), at["northSouth"].Value<double>(), at["elevation"].Value<double>() },
                new[] { position.Position.EastM.Value, position.Position.NorthM.Value, position.Position.ElevationM.Value }) * 1000.0;
            if (gap > SameMm)
            {
                found.Add(key + " keyPoints projectPosition " + SheetRows.PyRepr(Rounding.Fixed(gap, 1)) + " mm off the position file");
            }
            double turn = AngleGap(at["angleDeg"].Value<double>(), position.Position.AngleDeg.Value);
            if (turn > SameDeg)
            {
                found.Add(key + " keyPoints projectPosition angle " + SheetRows.PyRepr(Rounding.Fixed(turn, 6)) + " deg off the position file");
            }
            return found;
        }

        // python/position_file.shifts_of: the shift of each neighbourhood config/plot_numbers.json names, a whole number
        public static Dictionary<string, int> Shifts(JObject plotNumbers)
        {
            var found = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (JProperty item in (plotNumbers?["neighbourhoods"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                JToken shift = (item.Value as JObject)?["shift"];
                if (shift == null || shift.Type != JTokenType.Integer)
                {
                    throw new InvalidDataException("config/plot_numbers.json gives " + item.Name + " a shift that is no whole number (R353, R28)");
                }
                found[item.Name] = shift.Value<int>();
            }
            return found;
        }

        // python/position_file.numbering: the faces of a plot by the numbering the build uses, the faces the client code rows of labels.csv of
        // the schedule fit of R351 give its number, one face, or two and more for a number on two faces, a conflict never resolved by the
        // shift, else for a key whose code config/plot_numbers.json names, the number less its shift, else none. Rows of open space, land use
        // and substation number no face. The faces in the order of their numbers
        public static Func<string, string, IReadOnlyList<string>> Numbering(IReadOnlyList<Dictionary<string, string>> labels, IReadOnlyDictionary<string, int> shifts)
        {
            var faces = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (Dictionary<string, string> row in labels)
            {
                string number = (row.TryGetValue("number", out string a) ? a ?? "" : "").Trim(), face = (row.TryGetValue("face", out string b) ? b ?? "" : "").Trim();
                if (number.Length > 0 && face.Length > 0 && row.TryGetValue("kind", out string kind) && kind == PlotKind)
                {
                    if (!faces.ContainsKey(number))
                    {
                        faces[number] = new HashSet<string>(StringComparer.Ordinal);
                    }
                    faces[number].Add(face);
                }
            }
            return (key, plot) =>
            {
                if (faces.TryGetValue(plot, out HashSet<string> held) && held.Count > 0)
                {
                    List<string> ordered = held.ToList();
                    ordered.Sort(ComparePlots);
                    return ordered;
                }
                bool digits = plot.Length > 0 && plot.All(letter => letter >= '0' && letter <= '9');
                return digits && shifts.TryGetValue(key.ToUpperInvariant(), out int shift)
                    ? new[] { (System.Numerics.BigInteger.Parse(plot, CultureInfo.InvariantCulture) - shift).ToString(CultureInfo.InvariantCulture) } : new string[0];
            };
        }

        // python/position_file.merges_of: the merge of each vertex of the faces of a baseplan read exact by label and ring, as LocationPlan.Faces
        // takes the rings, what CadReader merged into the vertex in m
        public static Dictionary<string, List<double[]>> MergesOf(JArray plots)
        {
            var found = new Dictionary<string, List<double[]>>(StringComparer.Ordinal);
            foreach (JToken entry in plots)
            {
                if ((string)entry["kind"] != "plot_shape")
                {
                    continue;
                }
                JToken plot = entry["plot"];
                string label = FaceStep.WholeLabel(plot == null || plot.Type == JTokenType.Null ? null : plot.ToString());
                double[][] ring = (entry["vertices"] as JArray ?? new JArray()).Select(vertex => new[] { (double)vertex[0], (double)vertex[1] }).ToArray();
                if (label == null || ring.Length < 3 || FaceStep.Ring(ring).Area == 0)
                {
                    continue;
                }
                if (!found.TryGetValue(label, out List<double[]> rings))
                {
                    rings = new List<double[]>();
                    found[label] = rings;
                }
                JArray merges = entry["vertexMergeM"] as JArray;
                rings.Add(merges == null ? new double[ring.Length] : merges.Select(value => (double)value).ToArray());
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

        // python/position_file.check_points: by <key>/<plot>/<corner>, per key of the group the corners of the Generic floors of its
        // extreme plots, each with its CAD point, the nearest vertex of the baseplan face that holds the middle of its floor taken to shared
        // coordinates by the file, the face the numbering gives its plot, the gap through the file in mm and whether it passes, and why not:
        // no face, no face numbered, the face of another plot or the gap over the limit. A key of the group with no key points is one row
        // of its own, why no key points
        public static Dictionary<string, Dictionary<string, object>> CheckPoints(ProjectPositionFile file, IReadOnlyDictionary<string, JObject> pointsByKey, IReadOnlyList<string> group,
            IReadOnlyDictionary<string, PlanFace> faces, IReadOnlyDictionary<string, List<string>> typesByKey, Func<string, string, IReadOnlyList<string>> faceNumbered, double limitMm, int decimals,
            IReadOnlyDictionary<string, List<double[]>> merges = null)
        {
            var index = new FaceIndex(faces.Values);
            var rows = new Dictionary<string, Dictionary<string, object>>();
            foreach (string key in group)
            {
                if (!pointsByKey.TryGetValue(key, out JObject points))
                {
                    rows[key] = new Dictionary<string, object>
                    {
                        ["key"] = key, ["plot"] = null, ["face"] = null, ["expectedFace"] = null, ["numberFaces"] = null, ["sharedM"] = null, ["internalM"] = null, ["keyInternalM"] = null,
                        ["keySharedM"] = null, ["gapMm"] = null, ["revitGapMm"] = null, ["mergeMm"] = null, ["pass"] = false, ["why"] = NoPoints,
                    };
                    continue;
                }
                foreach (KeyValuePair<string, JObject> chosen in ChosenFloors(points, typesByKey[key]))
                {
                    var corners = (JArray)chosen.Value["corners"];
                    var sharedCorners = (JArray)chosen.Value["sharedCorners"];
                    double[] centre = Mean(corners);
                    double[] middle = SiteCoordinates.ToShared(file.Position, new[] { centre[0], centre[1], 0.0 });
                    string face = index.FaceOf(middle[0], middle[1]);
                    List<string> numbered = faceNumbered(key, chosen.Key).ToList();
                    string expected = numbered.Count == 1 ? numbered[0] : null;
                    List<double[]> vertices = face == null ? new List<double[]>() : faces[face].Rings.SelectMany(ring => ring).ToList();
                    List<double> moved = face != null && merges != null && merges.TryGetValue(face, out List<double[]> own) ? own.SelectMany(ring => ring).ToList() : new List<double>();
                    for (int k = 0; k < corners.Count; k++)
                    {
                        double[] corner = Corner(corners[k]), sharedCorner = Corner(sharedCorners[k]);
                        double[] cad = null, internalPoint = null;
                        double? gap = null, revit = null, merge = null;
                        if (vertices.Count > 0)
                        {
                            double[] at = SiteCoordinates.ToShared(file.Position, corner);
                            double best = double.PositiveInfinity;
                            int nearest = 0;
                            for (int item = 0; item < vertices.Count; item++)
                            {
                                double distance = LocationPlan.Hypot(vertices[item][0] - at[0], vertices[item][1] - at[1]);
                                if (distance < best)
                                {
                                    best = distance;
                                    nearest = item;
                                }
                            }
                            cad = new[] { vertices[nearest][0], vertices[nearest][1] };
                            merge = moved.Count > 0 ? moved[nearest] * 1000.0 : 0.0;
                            double[] taken = SiteCoordinates.ToInternal(file.Position, new[] { cad[0], cad[1], 0.0 });
                            internalPoint = new[] { taken[0], taken[1] };
                            gap = LocationPlan.Hypot(internalPoint[0] - corner[0], internalPoint[1] - corner[1]) * 1000.0;
                            revit = LocationPlan.Hypot(cad[0] - sharedCorner[0], cad[1] - sharedCorner[1]) * 1000.0;
                        }
                        string why = face == null ? "no face" : numbered.Count == 0 ? "no face numbered" : numbered.Count > 1 ? "number on two faces" : face != expected ? "face of another plot"
                            : Rounding.Fixed(merge.Value, decimals) > limitMm ? "vertex merged over the limit" : Rounding.Fixed(gap.Value, decimals) > limitMm ? "gap over the limit" : null;
                        rows[key + "/" + chosen.Key + "/" + k.ToString(CultureInfo.InvariantCulture)] = new Dictionary<string, object>
                        {
                            ["key"] = key, ["plot"] = chosen.Key, ["face"] = face, ["expectedFace"] = expected, ["numberFaces"] = numbered.Count > 1 ? numbered.ToArray() : null,
                            ["sharedM"] = cad, ["internalM"] = internalPoint, ["keyInternalM"] = new[] { corner[0], corner[1] }, ["keySharedM"] = new[] { sharedCorner[0], sharedCorner[1] },
                            ["gapMm"] = gap, ["revitGapMm"] = revit, ["mergeMm"] = merge, ["pass"] = why == null, ["why"] = why,
                        };
                    }
                }
            }
            return rows;
        }

        // python/position_file.summary_of: the check points in counts as the position file records them, the points, those that pass,
        // each why apart, and whether the check failed
        public static Dictionary<string, object> Summary(IReadOnlyDictionary<string, Dictionary<string, object>> rows, double limitMm)
        {
            int points = rows.Count, pass = rows.Values.Count(row => true.Equals(row["pass"]));
            var found = new Dictionary<string, object> { ["limitMm"] = limitMm, ["points"] = points, ["pass"] = pass };
            foreach (string[] why in WhyCounts)
            {
                found[why[0]] = rows.Values.Count(row => (string)row["why"] == why[1]);
            }
            found["failed"] = pass < points;
            return found;
        }

        // The summary as the position file writes it, its counts in the order Summary gives them
        public static JObject SummaryJson(IReadOnlyDictionary<string, object> summary)
        {
            var found = new JObject { ["limitMm"] = (double)summary["limitMm"] };
            foreach (string name in new[] { "points", "pass" }.Concat(WhyCounts.Select(why => why[0])))
            {
                found[name] = (int)summary[name];
            }
            found["failed"] = (bool)summary["failed"];
            return found;
        }

        // python/position_file.summary_line: the line of a check, the first issue of the answer of the worker when the check failed
        public static string SummaryLine(IReadOnlyDictionary<string, object> summary)
        {
            return "the check points " + ((bool)summary["failed"] ? "failed" : "passed") + ": " + summary["pass"] + " of " + summary["points"] + " pass at " + SheetRows.PyRepr((double)summary["limitMm"])
                + " mm, gap over the limit " + summary["overLimit"] + ", no face " + summary["noFace"] + ", face of another plot " + summary["otherFace"] + ", no face numbered " + summary["noNumber"]
                + ", number on two faces " + summary["twoFaces"] + ", vertex merged over the limit " + summary["merged"] + ", no key points " + summary["noPoints"];
        }

        // python/position_file.issues_of: one line per check point that fails, its name and why, the faces of a number on two faces, its
        // merge when it was merged over the limit and its gap when it has one
        public static List<string> Issues(IReadOnlyDictionary<string, Dictionary<string, object>> rows, int decimals)
        {
            return rows.Where(row => !true.Equals(row.Value["pass"])).Select(row => row.Key + " " + (string)row.Value["why"]
                + (row.Value["numberFaces"] == null ? "" : " " + string.Join(", ", (string[])row.Value["numberFaces"]))
                + ((string)row.Value["why"] != "vertex merged over the limit" ? "" : ", merged " + SheetRows.PyRepr(Rounding.Fixed((double)row.Value["mergeMm"], decimals)) + " mm")
                + (row.Value["gapMm"] == null ? "" : ", gap " + SheetRows.PyRepr(Rounding.Fixed((double)row.Value["gapMm"], decimals)) + " mm")).ToList();
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

        // The file as written, every length by R385 at 3 decimals and the angles at 6, as position_file.written gives it, with the summary
        // of its check points when the check ran
        public JObject Written(IReadOnlyDictionary<string, object> summary = null)
        {
            var found = new JObject
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
            if (summary != null)
            {
                found["checkPoints"] = SummaryJson(summary);
            }
            return found;
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
