using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R354 to R357. The build steps of the worker command build, the C# of plan_from_book.py --steps on the reads of button 2: the inputs
    // the worker read wrote, read_baseplan.json, read_villaBlocks.json, read_curbs.json and read_workbooks.json, each held to what it claims
    // to be by its role or its workbook, its drawing in Meters and its run stamp, plots.csv and labels.csv of the data book, the rules file
    // with keyRules of the level profile of the request, the analysis of the clusters (Q089) and the road surfaces of the build in their
    // order. Every number of a JSON input is the nearest double of its text, as Python reads it, and each input is read once per run
    public static class BuildSteps
    {
        public static readonly string[] Names = { SiteClusters.StepName, Fronts.StepName, Hands.StepName, LinkFrames.StepName };

        public static readonly string[] ClusterRoles = { "rules", "plotsCsv", "labels", "baseplanRead", "villaBlocksRead", "analysis" };

        public static readonly string[] FrontRoles = { "rules", "plotsCsv", "labels", "baseplanRead", "curbsRead", "workbooksRead", WorkerRunner.RoadSurfaceRole + "*" };

        // R357. The role of a prototype file, prototype:<its file name>, one per file of the prototype folder
        public const string PrototypeRole = "prototype:";

        public static readonly string[] HandRoles = { "rules", "plotsCsv", "labels", "baseplanRead", "villaBlocksRead", "analysis", "curbsRead", "workbooksRead", PrototypeRole + "*" };

        // What the build steps read, each input read when a step first needs it
        private sealed class Site
        {
            private readonly WorkerContext run;
            private readonly AddinConfig config;
            private readonly Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private Dictionary<string, BuildFace> faces;
            private JObject baseplanRead;
            private List<SiteCluster> clusters;
            private RoadLines roads;
            private JObject sales;
            private List<TinSurface> surfaces;

            public Site(WorkerContext run, AddinConfig config)
            {
                this.run = run;
                this.config = config;
                Settings = PyMath.ReadJson(run.Input("config"), "config") as JObject ?? throw new InvalidDataException("the config is no JSON object");
                Rules = PyMath.ReadJson(run.Input("rules"), "rules") as JObject ?? throw new InvalidDataException("the rules file is no JSON object");
                RulesName = Path.GetFileName(run.Request.Inputs.First(input => input.Role == "rules").Path);
                List<Dictionary<string, string>> csv = CsvRows.Read(run.Input("plotsCsv"));
                Rows = new Dictionary<string, Dictionary<string, string>>();
                foreach (Dictionary<string, string> row in csv)
                {
                    if (row.TryGetValue("locationPlanNumber", out string number) && !string.IsNullOrEmpty(number))
                    {
                        Rows[number] = row;
                    }
                }
                NumberOf = FaceStep.NumberOfLabel(csv);
                Numbers = PlotScope.Numbers(run.Request.Scope, CsvRows.Read(run.Input("labels")));
            }

            public JObject Settings { get; }

            public JObject Rules { get; }

            public string RulesName { get; }

            public Dictionary<string, Dictionary<string, string>> Rows { get; }

            public Dictionary<string, string> NumberOf { get; }

            public List<string> Numbers { get; }

            public Dictionary<string, BuildFace> Faces
            {
                get
                {
                    if (faces == null)
                    {
                        JObject baseplan = BaseplanRead;
                        double overlap = config.ParityTolerance("areas") ?? throw new InvalidDataException("addin.parity.kinds.areas is null, the tolerance of the baseplan faces (R345)");
                        faces = BuildFace.Faces(baseplan["plots"] as JArray ?? throw new InvalidDataException("the baseplan read holds no plots list"), NumberOf, overlap);
                    }
                    return faces;
                }
            }

            public JObject BaseplanRead => baseplanRead ?? (baseplanRead = Read("baseplanRead", "baseplan"));

            // The unit plots, those of plots.csv with Typology_Rev9 and a face, in the order of plots.csv
            public List<string> Units => Rows.Where(item => item.Value.TryGetValue("typologyRev9", out string unit) && !string.IsNullOrEmpty(unit) && Faces.ContainsKey(item.Key)).Select(item => item.Key).ToList();

            public List<SiteCluster> Clusters
            {
                get
                {
                    if (clusters == null)
                    {
                        // The faces first, as plan_from_book.py refuses a face before it reads the footprint and the analysis
                        Dictionary<string, BuildFace> held = Faces;
                        JObject footprint = Read("villaBlocksRead", "villaBlocks");
                        // R300. The villa blocks of a drawing kept for reference only are never read
                        string source = (string)footprint["source"] ?? "";
                        if (ReadSettings.Load(run.Input("config")).IsReferenceOnly(source))
                        {
                            throw new InvalidDataException(source + " is kept for reference only (drawings.referenceOnly) and is never read for villaBlocks (R300)");
                        }
                        Dictionary<string, List<double[][]>> blocks = SiteClusters.BlocksByName(footprint["blocks"] as JArray ?? throw new InvalidDataException("the footprint read holds no blocks list"));
                        string analysisPath = run.Input("analysis");
                        JObject read = PyMath.ReadJson(analysisPath, "analysis") as JObject ?? new JObject();
                        List<SiteCluster> analysis = SiteClusters.AnalysisOf(read, Path.GetFileName(analysisPath), (string)Settings["drawings"]?["baseplan"]);
                        // R354. The analysis held to the reads it was made from, not by the name of its drawing alone
                        SiteClusters.HoldAnalysis(read, Path.GetFileName(analysisPath), BaseplanRead["plots"] as JArray, footprint["blocks"] as JArray);
                        clusters = SiteClusters.Site(Units, Rows, held, blocks, analysis, NumberOf);
                    }
                    return clusters;
                }
            }

            // R355. The lines of the curb layers of analysis.roadEdgeLayers in the curbs read, cad_analysis.road_edge_pieces, refused when none
            public RoadLines Roads
            {
                get
                {
                    if (roads == null)
                    {
                        JObject curbs = Read("curbsRead", "curbs");
                        List<string> layers = ReadSettings.Load(run.Input("config")).CurbLayers;
                        roads = new RoadLines(ReadSteps.EdgePieces(curbs["pieces"] as JArray ?? new JArray(), layers).Select(item => (IReadOnlyList<double[]>)item.Value));
                        if (roads.Count == 0)
                        {
                            throw new InvalidDataException("the road layout read holds no line of the curb layers analysis.roadEdgeLayers names, the front of a plot is the edge nearest a curb (R355)");
                        }
                    }
                    return roads;
                }
            }

            // R355, R350. The sales rows of the workbooks read, by plot, held to the workbook config names for them
            public JObject Sales
            {
                get
                {
                    if (sales == null)
                    {
                        JObject workbooks = PyMath.ReadJson(run.Input("workbooksRead"), "workbooksRead") as JObject ?? throw new InvalidDataException("the input workbooksRead is no JSON object");
                        string refusal = StampRefusal(workbooks, hashes);
                        if (refusal != null)
                        {
                            throw new InvalidDataException("the input workbooksRead: " + refusal);
                        }
                        string key = (string)Settings["dataBook"]?["locationPlan"]?["sales"]?["workbook"];
                        string expected = Path.GetFileName((string)Settings["dataBook"]?["rev9"]?["workbooks"]?[key ?? ""] ?? "");
                        string named = ((workbooks[RunStamp.Key] as JObject)?["inputs"] as JArray ?? new JArray()).Where(input => (string)input["role"] == "sales").Select(input => Path.GetFileName((string)input["path"] ?? "")).FirstOrDefault();
                        if (string.IsNullOrEmpty(expected) || named == null || !string.Equals(named, expected, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException("the input workbooksRead is the read of the sales workbook " + (named ?? "None") + ", config names " + expected
                                + " for the sales rows, dataBook.locationPlan.sales.workbook (R355)");
                        }
                        sales = workbooks["sales"] as JObject ?? throw new InvalidDataException("the input workbooksRead holds no sales rows (R355)");
                    }
                    return sales;
                }
            }

            // R349, R344. The road surfaces of the level profile in their order, plan_rules.road_names, each the input roadSurface:<name>, every
            // one of the order and no other, the first surface of each file, refused when not in meter
            public List<TinSurface> Surfaces(JObject levelProfile)
            {
                if (surfaces != null)
                {
                    return surfaces;
                }
                ReadSettings read = ReadSettings.Load(run.Input("config"));
                string surface = (string)levelProfile["frontRoad"]?["surface"];
                List<string> order = surface == "roadLevel" ? read.RoadSurfaces : new List<string> { surface };
                List<string> roles = run.Request.Inputs.Select(input => input.Role).Where(role => role != null && role.StartsWith(WorkerRunner.RoadSurfaceRole, StringComparison.Ordinal)).ToList();
                List<string> extra = roles.Where(role => !order.Contains(role.Substring(WorkerRunner.RoadSurfaceRole.Length))).ToList();
                List<string> missing = order.Where(name => !roles.Contains(WorkerRunner.RoadSurfaceRole + name)).ToList();
                if (extra.Count > 0 || missing.Count > 0)
                {
                    throw new InvalidDataException("the road surfaces of the request are not those of the level profile in their order: not in it " + string.Join(", ", extra) + ", not in the request " + string.Join(", ", missing));
                }
                surfaces = new List<TinSurface>();
                foreach (string name in order)
                {
                    LandXmlFile file;
                    try
                    {
                        file = LandXmlReader.Read(run.Input(WorkerRunner.RoadSurfaceRole + name));
                    }
                    catch (LandXmlUnitException refused)
                    {
                        throw new InvalidDataException(refused.Message);
                    }
                    if (file.Surfaces.Count == 0)
                    {
                        throw new InvalidDataException(file.File + " holds no surface");
                    }
                    surfaces.Add(new TinSurface(ReadSettings.RoadName(name), file.Surfaces[0].Points, file.Surfaces[0].Faces));
                }
                return surfaces;
            }

            // R357, R128. The prototype file of each code, plan_from_cad.prototype_files of the names of the inputs prototype:<name>, each
            // named by its own file name, all of the prototype folder of pastJob and every file of it, as plan_from_book.folder_files lists
            // the whole folder, so the request names each file it takes and the run stamp and parity see the listing (B2 of the review of PR 55)
            public Dictionary<string, string> Files()
            {
                List<WorkerFile> inputs = run.Request.Inputs.Where(input => input.Role != null && input.Role.StartsWith(PrototypeRole, StringComparison.Ordinal)).ToList();
                foreach (WorkerFile input in inputs)
                {
                    if (input.Role.Substring(PrototypeRole.Length) != Path.GetFileName(input.Path))
                    {
                        throw new InvalidDataException("the input " + input.Role + " is the file " + Path.GetFileName(input.Path) + ", a prototype file is named by its own file name (R357)");
                    }
                }
                List<string> folders = inputs.Select(input => Path.GetDirectoryName(Path.GetFullPath(input.Path))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (folders.Count > 1)
                {
                    throw new InvalidDataException("the prototype files of the request lie in " + folders.Count + " folders, " + string.Join(", ", folders) + ", a build takes the files of one folder (R357)");
                }
                string folder = PrototypeFolder(Settings);
                if (!Directory.Exists(folder))
                {
                    throw new InvalidDataException("the prototype folder " + folder + " of pastJob is not on disk (R357)");
                }
                if (folders.Count == 1 && !string.Equals(folders[0].TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("the prototype files of the request lie in " + folders[0] + ", pastJob names the prototype folder " + folder + " (R357)");
                }
                List<string> listed = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToList();
                List<string> named = inputs.Select(input => input.Role.Substring(PrototypeRole.Length)).ToList();
                List<string> notNamed = listed.Except(named, StringComparer.Ordinal).ToList();
                List<string> notHeld = named.Except(listed, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
                if (notNamed.Count > 0 || notHeld.Count > 0)
                {
                    throw new InvalidDataException("the prototype folder " + folder + " holds files the request does not name: " + (notNamed.Count > 0 ? string.Join(", ", notNamed) : "none")
                        + ", and the request names files it does not hold: " + (notHeld.Count > 0 ? string.Join(", ", notHeld) : "none") + ", a build takes the whole folder as plan_from_book.folder_files lists it (R357)");
                }
                return Hands.PrototypeFiles(listed, Rules);
            }

            // A read of the worker of the role it names, its drawing in Meters and every input of its run stamp on disk with its SHA-256
            private JObject Read(string role, string readRole)
            {
                string path = run.Input(role);
                JObject found = PyMath.ReadJson(path, role) as JObject ?? throw new InvalidDataException("the input " + role + " is no JSON object: " + path);
                JToken named = found["role"];
                if (named == null || named.Type != JTokenType.String || (string)named != readRole)
                {
                    throw new InvalidDataException("the input " + role + " is the read of " + SiteClusters.PyText(named) + ", the build takes the read of " + readRole + " (R354)");
                }
                string refusal = CadUnits.Refusal(path, found) ?? StampRefusal(found, hashes);
                if (refusal != null)
                {
                    throw new InvalidDataException("the input " + role + ": " + refusal);
                }
                return found;
            }
        }

        // plan_from_past.prototype_folders of input_root: the folder of SITE_ADAPTATION_INPUT, else inputRoot of config, with
        // pastJob.upgradedPrototypeFolder, else pastJob.prototypeFolder, the folder Python lists for a build
        public static string PrototypeFolder(JObject settings)
        {
            string root = Environment.GetEnvironmentVariable("SITE_ADAPTATION_INPUT");
            if (string.IsNullOrEmpty(root))
            {
                root = settings["inputRoot"]?.Type == JTokenType.String ? (string)settings["inputRoot"] : null;
            }
            JObject job = settings["pastJob"] as JObject;
            string upgraded = job?["upgradedPrototypeFolder"]?.Type == JTokenType.String ? (string)job["upgradedPrototypeFolder"] : null;
            string past = job?["prototypeFolder"]?.Type == JTokenType.String ? (string)job["prototypeFolder"] : null;
            string named = string.IsNullOrEmpty(upgraded) ? past : upgraded;
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(named))
            {
                throw new InvalidDataException("config holds no inputRoot or no pastJob.prototypeFolder, the folder of the prototype files (R357)");
            }
            return Path.Combine(root, named);
        }

        // RunStamp.Refusal with each input of the stamps of one run hashed once
        private static string StampRefusal(JObject output, Dictionary<string, string> hashes)
        {
            JArray inputs = (output[RunStamp.Key] as JObject)?["inputs"] as JArray;
            if (inputs == null)
            {
                return "the output holds no run stamp with its inputs (R347)";
            }
            if (inputs.Count == 0)
            {
                return "the run stamp of the output names no input, so a change of its inputs could not be seen (R347)";
            }
            foreach (JToken input in inputs)
            {
                string role = (string)input["role"], path = (string)input["path"], sha = (string)input["sha256"];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return "the input " + role + " of the output is not on disk: " + path;
                }
                string full = Path.GetFullPath(path);
                if (!hashes.TryGetValue(full, out string found))
                {
                    found = hashes[full] = FileHash.Sha256(full);
                }
                if (!string.Equals(found, sha, StringComparison.OrdinalIgnoreCase))
                {
                    return "the input " + role + " of the output changed since the output was written, its SHA-256 is not the one of the stamp: " + path;
                }
            }
            return null;
        }

        // The build steps the request names, on the inputs read once
        public static void Run(WorkerContext run, AddinConfig config, IEnumerable<string> steps)
        {
            WorkerRequest request = run.Request;
            List<string> asked = steps.ToList();
            var made = new Dictionary<string, Dictionary<string, Dictionary<string, object>>>();
            try
            {
                var site = new Site(run, config);
                JObject levelProfile = BuildRules.LevelProfile(site.Rules, request.LevelProfile);
                BuildRules.KeyRules(site.Rules, request.LevelProfile, string.Join(", ", Names));
                if (asked.Contains(SiteClusters.StepName))
                {
                    run.Counts["clusters of the build"] = site.Clusters.Count;
                    run.Counts["clusters of the build from the analysis"] = site.Clusters.Count(cluster => cluster.Source == SiteClusters.AnalysisSource);
                    made[SiteClusters.StepName] = SiteClusters.Step(site.Numbers, site.Clusters);
                }
                if (asked.Contains(Fronts.StepName))
                {
                    ReadSettings read = ReadSettings.Load(run.Input("config"));
                    double snap = read.SnapM;
                    List<TinSurface> surfaces = site.Surfaces(levelProfile);
                    double[] limits = Fronts.Limits(site.Rules, site.Settings, site.RulesName);
                    double roadEdgeM = BuildFace.Number(site.Settings["siteBuild"]["corner"]["roadEdgeM"]);
                    Dictionary<string, FrontItem> fronts = Fronts.Of(site.Numbers, site.Rows, site.Faces, site.Roads, limits,
                        (x, y) => TinSurface.RoadLevel(surfaces, x, y, snap), site.Rules, roadEdgeM, site.Sales, site.RulesName);
                    run.Counts["fronts with a frame"] = fronts.Values.Count(item => item.Frame != null);
                    run.Counts["fronts with no road level of a unit plot"] = fronts.Values.Count(item => item.Unit != null && item.Front == null);
                    made[Fronts.StepName] = Fronts.Step(site.Numbers, fronts, surfaces.Select(surface => surface.Name).ToList(), snap);
                }
                if (asked.Contains(Hands.StepName) || asked.Contains(LinkFrames.StepName))
                {
                    double roadEdgeM = BuildFace.Number(site.Settings["siteBuild"]["corner"]["roadEdgeM"]);
                    var wanted = new HashSet<string>(site.Numbers);
                    List<string> framed = site.Numbers.Concat(site.Units).Concat(site.Clusters.Where(cluster => cluster.Plots.Any(wanted.Contains)).SelectMany(cluster => cluster.Plots)).Distinct().ToList();
                    // R357. The front of each plot by the corner rule of R226 as rule_inputs takes it, with no road level
                    Dictionary<string, FrontItem> fronts = Fronts.Of(framed, site.Rows, site.Faces, site.Roads, Fronts.Limits(site.Rules, site.Settings, site.RulesName),
                        (x, y) => new KeyValuePair<double?, string>(null, null), site.Rules, roadEdgeM, site.Sales, site.RulesName);
                    HandContext context = Hands.Context(site.Numbers, site.Rows, site.Faces, site.Clusters, fronts.ToDictionary(item => item.Key, item => item.Value.Frame), site.Roads, site.Rules, site.Settings);
                    JObject key = BuildRules.KeyRules(site.Rules, request.LevelProfile, string.Join(", ", Names));
                    Dictionary<string, string> files = site.Files();
                    Dictionary<string, Dictionary<string, object>> hands = Hands.Step(site.Numbers, site.Rows, context, key, files, site.Rules, site.Settings);
                    run.Counts["hands of the scope with a mirror flag"] = hands.Values.Count(item => item["mirrored"] != null);
                    run.Counts["side neighbour pairs"] = context.Neighbours.Values.Sum(sides => sides.Left.Count + sides.Right.Count);
                    if (asked.Contains(Hands.StepName))
                    {
                        made[Hands.StepName] = hands;
                    }
                    if (asked.Contains(LinkFrames.StepName))
                    {
                        // R356. The link of each plot on the front of R355 with the hand of the rule (R357)
                        made[LinkFrames.StepName] = LinkFrames.Step(site.Numbers, site.Rows, fronts, context.Roles, hands, key, files, site.Rules);
                        run.Counts["links of the scope placed"] = made[LinkFrames.StepName].Values.Count(item => item["insert"] != null);
                    }
                }
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            foreach (KeyValuePair<string, Dictionary<string, Dictionary<string, object>>> step in made)
            {
                run.Counts[step.Key + " items"] = step.Value.Count;
                run.WriteJson(request.StepsFolder, StepFile.FileName(step.Key, "csharp"), StepFile.Build(step.Key, "csharp", StepFile.ScopeText(request.Scope), step.Value, config.StepFields(step.Key)));
            }
        }
    }
}
