using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common
{
    // R80, R208. What a plan of a build takes besides its step files: site.json of button 1 for the survey point, the angle and the level
    // elevations, config for the floor settings, and the level profile, keyRules, the override sources and plots.csv as the build took them
    public sealed class PlanInputs
    {
        public string Nbh { get; set; }

        public JObject Site { get; set; }

        public JObject Settings { get; set; }

        public string PrototypeFolder { get; set; }

        public string ProfileName { get; set; }

        public JObject LevelProfile { get; set; }

        public JObject KeyRules { get; set; }

        public IList<string> OverrideSources { get; set; }

        public Dictionary<string, Dictionary<string, string>> Rows { get; set; }

        // The rules profile for the KEO file of a code and cadLinks.floorWidthStepM, which fit a yard outline as the step floors fits it
        public JObject Rules { get; set; }

        public double WidthStep { get; set; }
    }

    // R80, R208, R347. plan_<NBH>.json of the worker command build, assembled from the step files the build wrote as plan_from_book.build
    // writes plan.json, every number at the decimals of R2 by R385 from the raw value of the step. The plots and the missing list follow the
    // order of the scope, each sourced value takes its source and status from the step status (R401), and the summary takes the table of the
    // step summary with the counts plan_from_book.build adds. The plan is an output of the run by its temporary name, moved with the steps
    public static class BuildPlan
    {
        public const string SiteRole = "site";

        // The steps a plan is assembled from, each of them asked by the build
        public static readonly string[] Steps = { SiteClusters.StepName, Fronts.StepName, Hands.StepName, LinkFrames.StepName, KeyLevels.StepName, FloorOutlines.StepName,
            Walls.StepName, PlanOverrides.StepName, LinkParameters.StepName, ValueStatus.StepName, PlanSummary.StepName };

        private static readonly string[] FloorKinds = { "generic", "yard", "ramp" };

        // plan_from_book.build, the sources block and the lines of a plot the build leaves out
        private static readonly JObject SourceTexts = new JObject { ["plots"] = "plots.csv of the data book snapshot", ["survey"] = "the new site file", ["pastModel"] = "none" };
        private const string NoUnit = "plots.csv holds no row or no Typology_Rev9 for the plot";
        private const string NoFfl = "levels.py gives no FFL";
        private const string NoSiteLevel = "the new site file has no level ";
        private const string NoOutline = "no fitted outline for ";
        private const string Within = "within 5 mm";
        private const string Other = "other or empty";
        private const double BookLevelM = 0.005;
        private static int Decimals => DesignValues.Current.Decimals;

        // A build writes its plan when it asks every step the plan is made from and names site.json of button 1, as button 4 does
        public static bool PlanAsked(IEnumerable<string> steps, IEnumerable<WorkerFile> inputs)
        {
            var asked = new HashSet<string>(steps ?? new string[0], StringComparer.Ordinal);
            return Steps.All(asked.Contains) && (inputs ?? new WorkerFile[0]).Any(input => input.Role == SiteRole);
        }

        public static string FileName(string nbh)
        {
            return "plan_" + (nbh ?? "all") + ".json";
        }

        // The NBH a plan names: that of a scope of one NBH, none for the whole project, and for a plot list the nbhId of plots.csv its plots share
        public static string NbhOf(WorkerScope scope, IEnumerable<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows)
        {
            if (scope?.Kind == ScopeKinds.Nbh)
            {
                return scope.Nbh;
            }
            if (scope?.Kind != ScopeKinds.Plots)
            {
                return null;
            }
            List<string> found = numbers.Select(number => rows.TryGetValue(number, out Dictionary<string, string> row) && row.TryGetValue("nbhId", out string nbh) ? nbh : null).Distinct().ToList();
            return found.Count == 1 && !string.IsNullOrEmpty(found[0]) ? found[0] : null;
        }

        // What a build took besides its steps, as BuildSteps takes it: the level profile of the request or the default (Q050), the override
        // sources of R330, the prototype folder of R357 and the NBH of the scope
        public static PlanInputs InputsOf(JObject settings, JObject rules, string levelProfile, string configName, JObject site, Dictionary<string, Dictionary<string, string>> rows,
            WorkerScope scope, IEnumerable<string> numbers)
        {
            return new PlanInputs
            {
                Nbh = NbhOf(scope, numbers, rows), Site = site, Settings = settings, PrototypeFolder = BuildSteps.PrototypeFolder(settings),
                ProfileName = BuildRules.ProfileName(rules, levelProfile), LevelProfile = BuildRules.LevelProfile(rules, levelProfile),
                KeyRules = BuildRules.KeyRules(rules, levelProfile, string.Join(", ", BuildSteps.Names)), OverrideSources = PlanOverrides.DefaultSources(settings, configName), Rows = rows,
                Rules = rules, WidthStep = FloorOutlines.WidthStep(settings, configName),
            };
        }

        // R347. The plan from the step files of the run at their temporary names, written by its own beside the outputs of the request
        public static string Write(WorkerContext run, IReadOnlyDictionary<string, string> stepFiles, PlanInputs inputs)
        {
            var steps = new Dictionary<string, JObject>();
            foreach (string step in Steps)
            {
                if (!stepFiles.TryGetValue(step, out string path))
                {
                    throw new InvalidDataException("the plan is made from the step " + step + ", the build wrote none (R80)");
                }
                // A temporary name is never read as an input, the plan reads only the step files this run wrote, before they move
                string temporary = run.Written.Where(item => item.Value == path).Select(item => item.Key).FirstOrDefault()
                    ?? throw new InvalidDataException("the plan is made from the step files of its own run, this run wrote no " + path + " (R347)");
                steps[step] = PyJson.Parse(File.ReadAllText(temporary, new System.Text.UTF8Encoding(false, true))) as JObject
                    ?? throw new InvalidDataException("the step file " + path + " is no JSON object");
            }
            return run.WriteJson(run.Request.OutputFolder, FileName(inputs.Nbh), Assemble(steps, inputs));
        }

        public static JObject Assemble(IReadOnlyDictionary<string, JObject> steps, PlanInputs inputs)
        {
            JObject overrides = PlotsOf(steps, PlanOverrides.StepName);
            List<string> numbers = overrides.Properties().Select(item => item.Name).ToList();
            Dictionary<string, double> levels = SiteLevels(inputs.Site);
            JObject floorsBlock = inputs.Settings["floors"] as JObject ?? throw new InvalidDataException("config has no floors block (R112)");
            var plots = new JArray();
            var missing = new JArray();
            var lacking = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (string number in numbers)
            {
                JObject ovRaw = Side(overrides, number, "raw"), ov = Side(overrides, number, "written");
                bool written = ov["written"]?.Type == JTokenType.Boolean && (bool)ov["written"];
                JObject record = Record(ov);
                List<JObject> lacks = Lacks(steps, inputs, number, ov, written, levels, floorsBlock);
                if (written)
                {
                    plots.Add(Entry(steps, inputs, number, ov, ovRaw, levels, floorsBlock, record));
                }
                if (lacks.Count > 0)
                {
                    var item = new JObject { ["plot"] = number, ["written"] = written, ["missing"] = new JArray(lacks) };
                    if (record != null && !written)
                    {
                        item["overrides"] = record;
                    }
                    missing.Add(item);
                    foreach (JObject lack in lacks)
                    {
                        lacking[(string)lack["key"]] = lacking.TryGetValue((string)lack["key"], out int count) ? count + 1 : 1;
                    }
                }
            }
            JArray walls = PlanWalls(steps, numbers);
            JObject table = (steps[PlanSummary.StepName]["tables"]?["summary"] as JObject) ?? throw new InvalidDataException("the step summary holds no table summary (R365)");
            JObject survey = inputs.Site["surveyPoint"] as JObject ?? throw new InvalidDataException("site.json holds no surveyPoint (R80)");
            return new JObject
            {
                ["format"] = PlanFile.FormatName,
                ["formatVersion"] = PlanFile.Version,
                ["neighbourhood"] = inputs.Nbh ?? "all",
                ["coordinateSystem"] = new JObject { ["name"] = "shared", ["lengthUnit"] = "m", ["angleUnit"] = "deg" },
                ["surveyPoint"] = new JObject { ["position"] = Point(survey["position"]), ["sharedPosition"] = Point(survey["sharedPosition"]) },
                ["angleToTrueNorthDeg"] = R3(inputs.Site["angleToTrueNorthDeg"]),
                ["prototypeFolder"] = inputs.PrototypeFolder,
                ["floorSettings"] = new JObject { ["types"] = floorsBlock["types"]?.DeepClone(), ["levels"] = floorsBlock["levels"]?.DeepClone(), ["plotParameter"] = floorsBlock["plotParameter"]?.DeepClone() },
                ["sources"] = SourceTexts.DeepClone(),
                ["statusFrom"] = StatusFrom(steps),
                ["summary"] = Summary(steps, inputs, numbers, plots, lacking, walls, table),
                ["plots"] = plots,
                ["walls"] = walls,
                ["missing"] = missing,
                ["conflicts"] = table["conflicts"]?.DeepClone() ?? new JArray(),
            };
        }

        private static JObject PlotsOf(IReadOnlyDictionary<string, JObject> steps, string step)
        {
            if (!steps.TryGetValue(step, out JObject file))
            {
                throw new InvalidDataException("the plan is made from the step " + step + ", the build wrote none (R80)");
            }
            return file["plots"] as JObject ?? new JObject();
        }

        private static JObject Side(JObject plots, string number, string side)
        {
            return (plots[number] as JObject)?[side] as JObject ?? new JObject();
        }

        private static JObject Of(IReadOnlyDictionary<string, JObject> steps, string step, string number, string side = "written")
        {
            return Side(PlotsOf(steps, step), number, side);
        }

        private static bool Held(JToken token)
        {
            return token != null && token.Type != JTokenType.Null;
        }

        // common.r3: a number at 3 decimals by R385, any other value as it is
        private static JToken R3(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
            {
                return token?.DeepClone() ?? JValue.CreateNull();
            }
            return new JValue(Rounding.Fixed((double)token, Decimals));
        }

        private static double Number(JToken token)
        {
            return (double)token;
        }

        private static JArray Point(JToken values)
        {
            return new JArray(((JArray)values).Select(R3));
        }

        private static Dictionary<string, double> SiteLevels(JObject site)
        {
            var found = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (JObject level in (site["levels"] as JArray ?? new JArray()).OfType<JObject>())
            {
                found[(string)level["name"]] = (double)level["elevationM"];
            }
            return found;
        }

        // Outcomes.plot of plan_from_book.py, null for a plot with no override of the list, which the step writes with nothing applied or left
        private static JObject Record(JObject ov)
        {
            JArray applied = ov["applied"] as JArray ?? new JArray();
            JObject left = ov["notApplied"] as JObject ?? new JObject();
            return applied.Count == 0 && left.Count == 0 ? null : new JObject { ["applied"] = applied.DeepClone(), ["notApplied"] = left.DeepClone() };
        }

        private static JObject Lack(string key, string why)
        {
            return new JObject { ["key"] = key, ["why"] = why };
        }

        // The missing keys of a plot in the order plan_from_book.build lists them: the skip list, the unit, the link, the FFL, the parameters
        // and, for a plot the plan writes, its floors
        private static List<JObject> Lacks(IReadOnlyDictionary<string, JObject> steps, PlanInputs inputs, string number, JObject ov, bool written, Dictionary<string, double> levels, JObject floorsBlock)
        {
            var found = new List<JObject>();
            if (Held(ov["skip"]))
            {
                found.Add(Lack("skip", "the skip list of the override list: " + (string)ov["skip"] + " (R386)"));
                return found;
            }
            if (!Held(ov["unit"]))
            {
                found.Add(Lack("unit", NoUnit));
                return found;
            }
            string unit = (string)ov["unit"];
            // An override of the mirror flag gives the link the hand the rule did not, so it lacks no mirror flag
            foreach (JObject lack in (Of(steps, LinkFrames.StepName, number)["lacks"] as JArray ?? new JArray()).OfType<JObject>())
            {
                if ((string)lack["key"] != "mirrored" || !Held(ov["mirrored"]))
                {
                    found.Add((JObject)lack.DeepClone());
                }
            }
            if (!Held(ov["ffl"]))
            {
                found.Add(Lack("levels.ffl", NoFfl));
            }
            bool mirrored = ov["mirrored"]?.Type == JTokenType.Boolean && (bool)ov["mirrored"];
            foreach (JToken name in Of(steps, LinkParameters.StepName, number)["lacks"] as JArray ?? new JArray())
            {
                found.Add(Lack("parameters." + (string)name, "the rule of keyRules gives no value for " + unit + (mirrored ? " mirrored" : "")));
            }
            if (!written)
            {
                return found;
            }
            JObject floors = Of(steps, FloorOutlines.StepName, number);
            var lacks = (floors["lacks"] as JArray ?? new JArray()).OfType<JObject>().ToList();
            foreach (string kind in FloorKinds)
            {
                string level = (string)floorsBlock["levels"]?[kind];
                bool made = kind == "generic" ? Held(floors["generic"]) : kind == "yard" ? Held(ov["yardOutline"]) : Held(ov["rampZ"]) && Held(ov["rampType"]);
                if (made && level != null && !levels.ContainsKey(level))
                {
                    found.Add(Lack("floors." + kind, (kind == "yard" ? "the site model has no level " : NoSiteLevel) + level));
                    continue;
                }
                if (made)
                {
                    continue;
                }
                List<JObject> rule = lacks.Where(lack => (string)lack["key"] == "floors." + kind).ToList();
                if (kind == "yard")
                {
                    // R358. The step floors fits the yard on the hand of the rule, the plan on the hand after a mirror override
                    YardFit fit = Yard(steps, inputs, number, unit, mirrored);
                    if (fit.Depth == null)
                    {
                        continue;
                    }
                    if (fit.Group == null)
                    {
                        found.Add(Lack("floors.yard", NoOutline + fit.Unit + (mirrored ? " mirrored" : " not mirrored") + " on a plot " + SheetRows.PyRepr(fit.Width) + " m wide"));
                        continue;
                    }
                }
                found.AddRange(rule.Select(lack => (JObject)lack.DeepClone()));
            }
            return found;
        }

        // A sourced value with the source, status and statusFrom the step status gives its field, refused when that step holds none
        private static JObject Sourced(JObject fields, string number, string key, JToken value)
        {
            JObject mark = fields?[key] as JObject;
            if (mark == null || !Held(mark["source"]))
            {
                throw new InvalidDataException("the step status gives plot " + number + " no source of " + key + ", the plan writes no value without its source (R80, R401)");
            }
            var found = new JObject { ["value"] = value, ["source"] = mark["source"].DeepClone() };
            if (mark["status"] != null)
            {
                found["status"] = mark["status"].DeepClone();
            }
            if (mark["statusFrom"] != null)
            {
                found["statusFrom"] = mark["statusFrom"].DeepClone();
            }
            return found;
        }

        private static JObject Loops(JToken rings)
        {
            return new JObject { ["loops"] = new JArray(((JArray)rings).Select(ring => new JObject { ["points"] = new JArray(((JArray)ring).Select(Point)), ["arcs"] = new JArray() })) };
        }

        private static JObject Entry(IReadOnlyDictionary<string, JObject> steps, PlanInputs inputs, string number, JObject ov, JObject ovRaw, Dictionary<string, double> levels, JObject floorsBlock, JObject record)
        {
            JObject fields = Of(steps, ValueStatus.StepName, number)["fields"] as JObject;
            JObject link = Of(steps, LinkFrames.StepName, number), rule = Of(steps, KeyLevels.StepName, number), floors = Of(steps, FloorOutlines.StepName, number);
            JObject front = Of(steps, Fronts.StepName, number, "raw"), parameters = Of(steps, LinkParameters.StepName, number);
            string unit = (string)ov["unit"];
            double ffl = Number(ovRaw["ffl"]);
            var insert = new JArray(R3(ovRaw["insert"][0]), R3(ovRaw["insert"][1]), R3(ovRaw["ffl"]));
            var entry = new JObject
            {
                ["plot"] = number,
                ["unit"] = Sourced(fields, number, "unit", unit),
                ["prototypeFile"] = Sourced(fields, number, "prototypeFile", link["prototypeFile"]?.DeepClone()),
                ["insert"] = Sourced(fields, number, "insert", insert),
                // The step writes the rotation by plan_rotation, as the plan does
                ["rotationDeg"] = Sourced(fields, number, "rotationDeg", ov["rotationDeg"].DeepClone()),
                ["mirrored"] = Sourced(fields, number, "mirrored", ov["mirrored"].DeepClone()),
            };
            // R2. El @ Entry, the height and the road at front at the 4 decimals the step holds them, the FFL and SSL at 3
            var planLevels = new JObject
            {
                ["ffl"] = Sourced(fields, number, "levels.ffl", R3(ovRaw["ffl"])), ["ssl"] = Sourced(fields, number, "levels.ssl", R3(ovRaw["ssl"])),
                ["entry"] = Sourced(fields, number, "levels.entry", ov["entry"].DeepClone()), ["height"] = Sourced(fields, number, "levels.height", ov["height"].DeepClone()),
                ["steps"] = Sourced(fields, number, "levels.steps", ov["steps"].DeepClone()),
            };
            if (Held(ov["roadAtFront"]))
            {
                planLevels["roadAtFrontM"] = Sourced(fields, number, "levels.roadAtFrontM", ov["roadAtFront"].DeepClone());
            }
            entry["levels"] = planLevels;
            var planParameters = new JObject();
            foreach (JProperty item in (parameters["parameters"] as JObject ?? new JObject()).Properties())
            {
                planParameters[item.Name] = Sourced(fields, number, "parameters." + item.Name, item.Value.DeepClone());
            }
            entry["parameters"] = planParameters;
            if (Held(rule["ffl"]))
            {
                // R340. The levels by the rule alone beside those after the overrides, with the front corners of the ramp at 3 decimals
                var levelRule = new JObject { ["entry"] = rule["entry"]?.DeepClone(), ["ffl"] = rule["ffl"]?.DeepClone(), ["height"] = rule["height"]?.DeepClone(), ["steps"] = rule["steps"]?.DeepClone() };
                foreach (JProperty item in (rule["parameters"] as JObject ?? new JObject()).Properties())
                {
                    levelRule[item.Name] = item.Value.DeepClone();
                }
                if (Held(front["roadLevelLeft"]) && Held(front["roadLevelRight"]))
                {
                    levelRule["rampFrontCorners"] = new JArray(R3(front["roadLevelLeft"]), R3(front["roadLevelRight"]));
                }
                entry["levelRule"] = levelRule;
            }
            if (Held(parameters["linkName"]))
            {
                entry["linkName"] = Sourced(fields, number, "linkName", parameters["linkName"].DeepClone());
            }
            var planFloors = new JObject();
            foreach (string kind in FloorKinds)
            {
                string level = (string)floorsBlock["levels"][kind];
                if (!levels.TryGetValue(level, out double elevation))
                {
                    continue;
                }
                string key = "floors." + kind;
                JObject floor = null;
                if (kind == "generic" && Held(floors["generic"]))
                {
                    floor = new JObject { ["type"] = Sourced(fields, number, key + ".type", floorsBlock["types"]["generic"][0].DeepClone()) };
                    FloorBase(floor, fields, number, key, level, elevation, Loops(floors["generic"]), unit);
                    floor["z"] = Sourced(fields, number, key + ".z", R3(ovRaw["ffl"]));
                    floor["heightOffset"] = Sourced(fields, number, key + ".heightOffset", new JValue(Rounding.Fixed(ffl - elevation, Decimals)));
                }
                else if (kind == "yard" && Held(ov["yardOutline"]))
                {
                    // R137. The yard of the unit whose outline the link holds, TH1-IM that of TH1, so its plot value names that unit
                    YardFit fit = Yard(steps, inputs, number, unit, (bool)ov["mirrored"]);
                    JToken type = fit.Group?["type"] ?? throw new InvalidDataException("plot " + number + " has a yard outline after the overrides and no fitted outline of " + fit.Unit + " (R358)");
                    floor = new JObject { ["type"] = Sourced(fields, number, key + ".type", type.DeepClone()) };
                    FloorBase(floor, fields, number, key, level, elevation, Loops(ov["yardOutline"]), fit.Unit);
                    floor["z"] = Sourced(fields, number, key + ".z", R3(ovRaw["yardZ"]));
                    floor["heightOffset"] = Sourced(fields, number, key + ".heightOffset", new JValue(Rounding.Fixed(Number(ovRaw["yardZ"]) - elevation, Decimals)));
                }
                else if (kind == "ramp" && Held(ov["rampZ"]) && Held(ov["rampType"]))
                {
                    floor = new JObject { ["type"] = Sourced(fields, number, key + ".type", ov["rampType"].DeepClone()) };
                    // Q116. The outline after the overrides, on the open edge of a turned plot, the rule outline in a step file before it
                    JArray outline = (JArray)(Held(ov["rampOutline"]) ? ov["rampOutline"] : floors["rampOutline"]);
                    var rings = new JArray();
                    rings.Add(outline.DeepClone());
                    FloorBase(floor, fields, number, key, level, elevation, Loops(rings), unit);
                    JArray heights = (JArray)ovRaw["rampZ"];
                    floor["corners"] = Sourced(fields, number, key + ".corners", new JArray(outline.Select((corner, index) => new JArray(R3(corner[0]), R3(corner[1]), R3(heights[index])))));
                    floor["heightOffset"] = Sourced(fields, number, key + ".heightOffset", new JValue(Rounding.Fixed(heights.Min(Number) - elevation, Decimals)));
                    floor["slopePercent"] = Sourced(fields, number, key + ".slopePercent", new JValue(Rounding.Fixed(Slope((string)floors["rampClass"]), Decimals)));
                }
                if (floor != null)
                {
                    planFloors[kind] = floor;
                }
            }
            entry["floors"] = planFloors;
            entry["roadSurfaces"] = new JArray(front["surfaceLeft"]?.DeepClone(), front["surfaceRight"]?.DeepClone());
            if (record != null)
            {
                entry["overrides"] = record;
            }
            return entry;
        }

        private sealed class YardFit
        {
            public string Unit { get; set; }

            public double Width { get; set; }

            public JObject Group { get; set; }

            public double? Depth { get; set; }
        }

        // R358, R137. The yard of a plot as FloorOutlines fits it, on the hand the plot is placed with: the depth keyRules.yard gives its zone, the
        // unit whose outline the link holds, TH1-IM that of TH1, the plot width by cadLinks.floorWidthStepM and the fitted outline of keyRules
        private static YardFit Yard(IReadOnlyDictionary<string, JObject> steps, PlanInputs inputs, string number, string unit, bool mirrored)
        {
            string zone = null;
            if (inputs.Rows.TryGetValue(number, out Dictionary<string, string> row))
            {
                row.TryGetValue("zone", out zone);
            }
            JArray groups = inputs.KeyRules?["yardOutline"]?["groups"] as JArray ?? new JArray();
            JToken keo = inputs.Rules?["typologies"]?[unit]?["keo_file"];
            string yardUnit = groups.OfType<JObject>().Any(group => (string)group["unit"] == unit) || keo?.Type != JTokenType.String || string.IsNullOrEmpty((string)keo) ? unit : (string)keo;
            double width = FloorOutlines.WidthKey(Number(Of(steps, Fronts.StepName, number, "raw")["length"]), inputs.WidthStep);
            return new YardFit { Unit = yardUnit, Width = width, Group = FloorOutlines.OutlineFor(groups, yardUnit, mirrored, width), Depth = FloorOutlines.YardDepth(inputs.KeyRules, zone ?? "") };
        }

        private static void FloorBase(JObject floor, JObject fields, string number, string key, string level, double elevation, JObject outline, string unit)
        {
            floor["level"] = Sourced(fields, number, key + ".level", level);
            floor["levelElevation"] = Sourced(fields, number, key + ".levelElevation", new JValue(Rounding.Fixed(elevation, Decimals)));
            floor["outline"] = Sourced(fields, number, key + ".outline", outline);
            floor["plotValue"] = Sourced(fields, number, key + ".plotValue", unit + "-" + number);
        }

        // R358, plan_rules.check_ramp_slopes. The slope of El @ Entry of a code is the one its ramp class names, which slope_class writes flat for 0
        private static double Slope(string rampClass)
        {
            if (rampClass == "flat")
            {
                return 0.0;
            }
            if (rampClass == null || !double.TryParse(rampClass, NumberStyles.Float, CultureInfo.InvariantCulture, out double slope))
            {
                throw new InvalidDataException("the ramp class " + (rampClass ?? "null") + " of the step floors names no slope (R358)");
            }
            return slope;
        }

        // R362. The walls of the step walls, plot by plot in the order of the scope and edge by edge, each value with its source and status
        private static JArray PlanWalls(IReadOnlyDictionary<string, JObject> steps, IEnumerable<string> numbers)
        {
            var found = new JArray();
            JObject plots = PlotsOf(steps, Walls.StepName);
            foreach (string number in numbers)
            {
                JObject item = Side(plots, number, "written");
                JArray edges = item["edges"] as JArray ?? new JArray();
                for (int index = 0; index < edges.Count; index++)
                {
                    JObject status = (JObject)item["status"][index];
                    var wall = new JObject { ["plot"] = number, ["edge"] = edges[index].DeepClone() };
                    foreach (string field in Walls.Fields)
                    {
                        var value = new JObject { ["value"] = item[field][index].DeepClone(), ["source"] = field == "plots" ? Walls.PlotsSource : Walls.Source };
                        value["status"] = status[field]?["status"]?.DeepClone();
                        value["statusFrom"] = status[field]?["statusFrom"]?.DeepClone();
                        wall[field] = value;
                    }
                    wall["basePlot"] = item["levelPlots"][index][0].DeepClone();
                    wall["topPlot"] = item["levelPlots"][index][1].DeepClone();
                    found.Add(wall);
                }
            }
            return found;
        }

        // R401. The details of the values and of the walls, which share one table in plan_from_book.build, by key in the order of sorted
        private static JObject StatusFrom(IReadOnlyDictionary<string, JObject> steps)
        {
            var found = new SortedDictionary<string, JToken>(StringComparer.Ordinal);
            foreach (string step in new[] { ValueStatus.StepName, Walls.StepName })
            {
                foreach (JProperty item in ((steps[step]["tables"]?["statusFrom"]) as JObject ?? new JObject()).Properties())
                {
                    found[item.Name] = item.Value.DeepClone();
                }
            }
            return new JObject(found.Select(item => new JProperty(item.Key, item.Value)));
        }

        private static JObject Counted(IEnumerable<string> keys, bool sorted)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (string key in keys)
            {
                if (!counts.ContainsKey(key))
                {
                    counts[key] = 0;
                    order.Add(key);
                }
                counts[key]++;
            }
            IEnumerable<string> names = sorted ? order.OrderBy(name => name, StringComparer.Ordinal) : order;
            return new JObject(names.Select(name => new JProperty(name, counts[name])));
        }

        private static JObject Summary(IReadOnlyDictionary<string, JObject> steps, PlanInputs inputs, List<string> numbers, JArray plots, SortedDictionary<string, int> lacking,
            JArray walls, JObject table)
        {
            JObject overrides = PlotsOf(steps, PlanOverrides.StepName), fronts = PlotsOf(steps, Fronts.StepName);
            var skipped = numbers.Where(number => Held(Side(overrides, number, "written")["skip"])).ToList();
            // The plots the build reaches past the skip list and the unit, whose road levels it counts
            var reached = numbers.Where(number => !Held(Side(overrides, number, "written")["skip"]) && Held(Side(overrides, number, "written")["unit"])).ToList();
            var noLevel = reached.Where(number => !(Held(Side(fronts, number, "raw")["roadLevelLeft"]) && Held(Side(fronts, number, "raw")["roadLevelRight"]))).ToList();
            var book = new List<string>();
            foreach (string number in reached.Except(noLevel))
            {
                JObject front = Side(fronts, number, "raw");
                inputs.Rows.TryGetValue(number, out Dictionary<string, string> row);
                foreach (string column in new[] { "roadLevelLeft", "roadLevelRight" })
                {
                    string other = null;
                    row?.TryGetValue(column, out other);
                    bool near = !string.IsNullOrEmpty(other) && Math.Abs(double.Parse(other, NumberStyles.Float, CultureInfo.InvariantCulture) - Number(front[column])) <= BookLevelM;
                    book.Add(near ? Within : Other);
                }
            }
            JObject clusters = PlotsOf(steps, SiteClusters.StepName);
            var members = new HashSet<string>(clusters.Properties().SelectMany(item => (item.Value["written"]?["plots"] as JArray ?? new JArray()).Select(plot => (string)plot)), StringComparer.Ordinal);
            members.UnionWith(numbers);
            members.UnionWith(numbers.Select(number => Of(steps, KeyLevels.StepName, number)["partner"]).Where(Held).Select(partner => (string)partner));
            JObject hands = PlotsOf(steps, Hands.StepName);
            var summary = new JObject
            {
                ["plots"] = numbers.Count, ["written"] = plots.Count, ["notWritten"] = numbers.Count - plots.Count,
                ["missingByKey"] = new JObject(lacking.Select(item => new JProperty(item.Key, item.Value))),
                ["skipped"] = new JArray(skipped.OrderBy(number => number, PyMath.SortKey)),
                ["clusters"] = clusters.Count, ["clusterPlots"] = members.Count,
                ["inACluster"] = numbers.Count(number => Held(Side(hands, number, "written")["clusterRole"])),
                ["roadSurfacesOfFrontCorners"] = table["roadSurfacesOfFrontCorners"]?.DeepClone(),
                ["plotsWithNoRoadLevel"] = new JArray(noLevel),
                ["noRoadLevel"] = new JArray(noLevel.Select(number => Side(fronts, number, "written")["noRoadLevel"]?.DeepClone())),
                ["roadFolderNotTaken"] = (inputs.Settings["siteBuild"]?["roadFolder"]?["notTaken"] as JObject)?.DeepClone() ?? new JObject(),
                ["frontRule"] = table["frontRule"]?.DeepClone(),
                ["frontLevelAgainstPlotsCsv"] = Counted(book, false),
                ["floorsFound"] = table["floorsFound"]?.DeepClone(),
                ["floorTypes"] = table["floorTypes"]?.DeepClone(),
                ["walls"] = WallSummary(walls, steps[Walls.StepName]["tables"]?["statusFrom"] as JObject ?? new JObject()),
                ["rules"] = Rules(inputs, numbers, overrides, plots, table),
            };
            return summary;
        }

        // plan_walls.summary
        private static JObject WallSummary(JArray walls, JObject details)
        {
            var byField = new JObject();
            foreach (string field in Walls.Fields)
            {
                byField["walls." + field] = Counted(walls.Select(wall => ValueStatus.Label((string)wall[field]["status"], details[(string)wall[field]["statusFrom"] ?? ""] as JObject)), true);
            }
            return new JObject
            {
                ["walls"] = walls.Count, ["byPlots"] = Counted(walls.Select(wall => ((JArray)wall["plots"]["value"]).Count.ToString(CultureInfo.InvariantCulture)), true), ["statusByField"] = byField,
            };
        }

        private static JObject Rules(PlanInputs inputs, List<string> numbers, JObject overrides, JArray plots, JObject table)
        {
            var applied = new List<string>();
            var why = new SortedDictionary<string, SortedDictionary<string, List<string>>>(StringComparer.Ordinal);
            foreach (string number in numbers)
            {
                JObject ov = Side(overrides, number, "written");
                applied.AddRange((ov["applied"] as JArray ?? new JArray()).Select(field => (string)field));
                foreach (JProperty left in (ov["notApplied"] as JObject ?? new JObject()).Properties())
                {
                    if (!why.TryGetValue(left.Name, out SortedDictionary<string, List<string>> whys))
                    {
                        why[left.Name] = whys = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
                    }
                    string text = (string)left.Value;
                    if (!whys.TryGetValue(text, out List<string> found))
                    {
                        whys[text] = found = new List<string>();
                    }
                    found.Add(number);
                }
            }
            // R337. The zones keyRules.yard says were not measured, by the plots the plan writes in each
            JObject notMeasured = inputs.KeyRules?["yard"]?["notMeasured"] as JObject ?? new JObject();
            var zones = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (JObject plot in plots.OfType<JObject>())
            {
                string zone = null;
                if (inputs.Rows.TryGetValue((string)plot["plot"], out Dictionary<string, string> row))
                {
                    row.TryGetValue("zone", out zone);
                }
                if (zone != null && Held(notMeasured[zone]) && !string.IsNullOrEmpty((string)notMeasured[zone]))
                {
                    zones[zone] = zones.TryGetValue(zone, out int count) ? count + 1 : 1;
                }
            }
            string job = (string)inputs.LevelProfile?["job"];
            JToken later = job == null ? null : inputs.Settings["majority"]?["overrideKeep"]?["laterFields"]?[job];
            return new JObject
            {
                ["levelProfile"] = inputs.ProfileName,
                ["overrideSource"] = new JArray(inputs.OverrideSources ?? new List<string>()),
                // The worker takes no override model, as button 4 names none (R280)
                ["overrideModel"] = new JArray(),
                ["applied"] = Counted(applied, true),
                ["statusByField"] = table["statusByField"]?.DeepClone(),
                ["assumedByField"] = table["assumedByField"]?.DeepClone(),
                ["statusPlots"] = table["statusPlots"]?.DeepClone(),
                // R340. A build by the level rules of keyRules applies an override to its own plot only, so none outside the build
                ["appliedOutsideBuild"] = new JObject(),
                ["handRules"] = table["handRules"]?.DeepClone(),
                ["keyRules"] = true,
                ["laterFields"] = later is JArray list ? list.DeepClone() : new JArray(),
                ["overridesNotApplied"] = table["overridesNotApplied"]?.DeepClone(),
                ["overridesNotAppliedWhy"] = new JObject(why.Select(field => new JProperty(field.Key, new JObject(field.Value.Select(item => new JProperty(item.Key,
                    new JObject { ["plots"] = item.Value.Count, ["first"] = new JArray(item.Value.OrderBy(number => number, PyMath.SortKey).Take(10)) })))))),
                ["yardNotMeasured"] = new JObject(zones.Select(zone => new JProperty(zone.Key, new JObject { ["plots"] = zone.Value, ["why"] = notMeasured[zone.Key].DeepClone() }))),
                ["steps"] = table["steps"]?.DeepClone(),
            };
        }
    }
}
