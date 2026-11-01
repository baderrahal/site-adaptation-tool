using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace CadReader
{
    // R348 to R350. The command read of the worker, the reads of button 2 (docs/design/03b-buttons-2-to-8.md 3.2): each drawing of the request read
    // with ACadSharp by the CadExtractor of CadReader, refused when it is a drawing of drawings.referenceOnly (R300) or its header InsUnits
    // is not Meters, each road surface LandXML read in the order of siteBuild.roadSurfaces and refused when it is not in meter, and the
    // DLUS and sales workbooks read by their sheets in config. Each read is written to the output folder of the request at the
    // decimals of R2, read_<role>.json, and the read steps of the request are written from those outputs, so a step takes the numbers a
    // later build reads. Every input is read through WorkerContext.Input, the free memory checked right before it (R347)
    public static class WorkerReads
    {
        public static Dictionary<string, WorkerCommand> Commands()
        {
            Dictionary<string, WorkerCommand> commands = WorkerCommands.Default();
            commands["read"] = Read;
            commands["position"] = Position;
            // R364. The level Excel of button 6, made from a plan and written with OpenXml, which only the worker loads
            commands["levelExcel"] = LevelWorkbook.Command;
            // R365. The report Excel of button 8, made from a plan and written with OpenXml as the level Excel is
            commands["reportExcel"] = ReportWorkbook.Command;
            // R402. The edited level Excel and report Excel read back and compared with the plan, for Update from report
            commands["updateFromReport"] = UpdateFromReportWorker.Command;
            // R412. The read only check of the inputs of a new project, the report in plain words
            commands[NewProjectCheck.Command] = NewProject;
            // R420 to R427. The scanner of any CAD, Excel and PDF the user gives, which reads them with ACadSharp, OpenXml and PdfPig, so in the worker only
            commands["scan"] = CadScan.Command;
            // R454. The coordinate system each drawing and LandXML states and the plot outlines of the baseplan, for button 1
            commands[SiteAdaptation.Common.SetUp.CoordinateRequest.Command] = CoordinateSystemRead.Command;
            return commands;
        }

        private static readonly string[] Drawings = { "baseplan", "villaBlocks", "curbs" };

        // A JSON text read with its texts kept as texts, never taken for dates
        private static JToken Parse(string text)
        {
            using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
            {
                return JToken.Load(reader);
            }
        }

        // An object written as the CadReader outputs are written, at the decimals of R2 (JsonOut), then read back as JSON
        private static JToken AsWritten(object value)
        {
            return Parse(JsonOut.ToJson(value));
        }

        private static void Read(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            if (string.IsNullOrEmpty(request.OutputFolder))
            {
                throw run.Refusal("a read names no output folder for its outputs");
            }
            ReadSettings settings = ReadSettings.Load(run.Input("config"));
            // R352. The decimals of the block order of the plot table, addin.plotTable, its own setting and never a tolerance of the parity,
            // held before any read
            int overlapDecimals = 0;
            if (request.Inputs.Any(input => input.Role == PlotTable.BorderRole))
            {
                try
                {
                    overlapDecimals = PlotTableSettings.From(JObject.Parse(WorkFiles.ReadText(run.Input("config")))).OverlapDecimals;
                }
                catch (InvalidDataException refused)
                {
                    throw run.Refusal(refused.Message);
                }
            }
            var steps = new HashSet<string>(request.Steps ?? new List<string>());
            // H1 of the fifth review of PR 55. The read steps are of the whole project, the scope read_steps.py writes them with, where a read
            // of a neighbourhood took the road levels of the plots of a labels.csv with no column nbh, none, and was done
            if (steps.Count > 0 && request.Scope?.Kind != "project")
            {
                throw run.Refusal("the read steps are of the whole project, the scope project of read_steps.py, a read of the steps " + string.Join(", ", request.Steps)
                    + " with the scope " + StepFile.ScopeText(request.Scope) + " is refused (R348)");
            }
            // H2 of the fifth review of PR 55. Every tolerance of the parity held before any read, as read_steps.py holds them by
            // parity.tolerances, where only the steps of the location plan and the position held the areas tolerance
            if (steps.Count > 0)
            {
                try
                {
                    AddinConfig.Load(run.Input("config")).ParityTolerances();
                }
                catch (InvalidDataException refused)
                {
                    throw run.Refusal(refused.Message);
                }
            }
            var made = new Dictionary<string, Dictionary<string, Dictionary<string, object>>>();
            var reads = new Dictionary<string, JObject>();
            bool anyDrawing = Drawings.Any(role => request.Inputs.Any(input => input.Role == role));
            LayerConfig layers = null;
            if (anyDrawing)
            {
                if (request.Inputs.All(input => input.Role != "cadLayers"))
                {
                    throw run.Refusal("a read of a drawing needs the input cadLayers, the keep and drop list of the layers (config/cad_layers.json)");
                }
                layers = LayerConfig.Load(run.Input("cadLayers"));
            }
            // Q084. The drawings one at a time, the largest measured peak first, each let go with its document before the next read
            List<string> order = WorkerMemory.ReadOrder(request.Inputs, Drawings, AddinConfig.Load(run.Input("config")));
            foreach (string role in order)
            {
                reads[role] = Drawing(run, role, layers, settings);
                run.WriteJson(request.OutputFolder, "read_" + role + ".json", reads[role]);
                run.Counts["drawing " + role + " read as"] = order.IndexOf(role) + 1;
                WorkerMemory.Release();
                run.Counts["memory released after a drawing"] = order.IndexOf(role) + 1;
            }
            List<TinSurface> surfaces = Surfaces(run, settings, steps, made);
            WorkerMemory.Release();
            var sheets = new Dictionary<string, XlsxSheet.Sheet>();
            SheetRows.AreaKey[] areaKeys = Workbooks(run, settings, steps, made, sheets);
            JObject baseplan = reads.TryGetValue("baseplan", out JObject read) ? read : null;
            Located located = Location(run, settings, baseplan, steps, made, sheets);
            if (located != null)
            {
                WorkerMemory.Release();
            }
            Table(run, located, baseplan, reads.TryGetValue("villaBlocks", out JObject footprint) ? footprint : null, areaKeys, overlapDecimals, steps, made);
            if (steps.Contains(ReadSteps.Labels))
            {
                made[ReadSteps.Labels] = ReadSteps.LabelStep((JArray)reads["baseplan"]["plots"]);
            }
            if (steps.Contains(ReadSteps.Blocks))
            {
                made[ReadSteps.Blocks] = ReadSteps.BlockStep((JArray)reads["villaBlocks"]["blocks"]);
            }
            if (steps.Contains(ReadSteps.Setbacks))
            {
                made[ReadSteps.Setbacks] = ReadSteps.EdgeStep((JArray)reads["villaBlocks"]["pieces"], settings.SetbackLayers);
            }
            if (steps.Contains(ReadSteps.Curbs))
            {
                made[ReadSteps.Curbs] = ReadSteps.EdgeStep((JArray)reads["curbs"]["pieces"], settings.CurbLayers);
            }
            if (steps.Contains(ReadSteps.Rows))
            {
                made[ReadSteps.Rows] = ReadSteps.EdgeStep((JArray)reads["curbs"]["pieces"], settings.RowLayers);
            }
            if (steps.Contains(ReadSteps.RoadLevels))
            {
                // G1, H1. As read_steps.py takes them through plan_from_book.book_rows and then nbh_plots, the plots of the whole project
                List<Dictionary<string, string>> plotsCsv = CsvRows.Read(run.Input("plotsCsv"), CsvRows.PlotsColumns);
                List<string> numbers = PlotScope.Of(run);
                Dictionary<string, List<double[][]>> faces = ReadSteps.FaceRings((JArray)reads["baseplan"]["plots"], plotsCsv);
                made[ReadSteps.RoadLevels] = ReadSteps.RoadLevelStep(numbers, faces, surfaces, settings.SnapM);
            }
            AddinConfig config = AddinConfig.Load(run.Input("config"));
            foreach (string step in request.Steps ?? new List<string>())
            {
                run.Counts[step + " items"] = made[step].Count;
                run.WriteJson(request.StepsFolder, StepFile.FileName(step, "csharp"), StepFile.Build(step, "csharp", StepFile.ScopeText(request.Scope), made[step], config.StepFields(step)));
            }
        }

        // R353. The command position of button 1: the project position file from the read of the reference key, refused when it differs
        // from keyRules.site of the level profile, when a key read of the group is missing, of another key than its role names (answerKeys
        // and key_copies.json, the input keyCopies) or lacks a value the set is held on, or when the reference key differs from another key
        // of its group, keyRead:<key> each, as R331 holds a read. Written to the output folder under the file name of addin.position.file.
        // When the request names the baseplan, the check points of button 1: the corners of the Generic floors of the extreme plots of each
        // key of keyPoints:<key> at full precision, the points held to their key and to the file, against the vertices of the baseplan face
        // of the plot at full precision through the position, the face held to the numbering of the build (labels and plotShifts). A check
        // that fails ends the run done with an issue per failed point after the summary line, and the position file records the summary
        private static void Position(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            if (string.IsNullOrEmpty(request.OutputFolder))
            {
                throw run.Refusal("a position names no output folder for the position file (R353)");
            }
            string[] needed = { ProjectPositionFile.RulesRole, ProjectPositionFile.KeyReadRole, ProjectPositionFile.KeyCopiesRole };
            if (needed.Any(role => request.Inputs.All(input => input.Role != role)))
            {
                throw run.Refusal("the position file is written from the read of a key, held against keyRules.site and every key read held to its key, the request needs the inputs keyRead, rules and keyCopies (R353)");
            }
            string readPrefix = ProjectPositionFile.KeyReadRole + ":", pointsPrefix = ProjectPositionFile.KeyPointsRole + ":";
            List<string> pointKeys = request.Inputs.Where(input => input.Role != null && input.Role.StartsWith(pointsPrefix, StringComparison.Ordinal)).Select(input => input.Role.Substring(pointsPrefix.Length)).ToList();
            bool withBaseplan = request.Inputs.Any(input => input.Role == "baseplan");
            if (withBaseplan != (pointKeys.Count > 0))
            {
                throw run.Refusal("the check points hold the baseplan against the key points of the keys, the inputs baseplan and keyPoints:<key>, the request names one of them (R353)");
            }
            if (withBaseplan && new[] { ProjectPositionFile.LabelsRole, ProjectPositionFile.PlotShiftsRole, "cadLayers" }.Any(role => request.Inputs.All(input => input.Role != role)))
            {
                throw run.Refusal("the check points hold the face of each point to the numbering of the build, the request needs the inputs labels, plotShifts and cadLayers (R353)");
            }
            PositionSettings settings;
            ProjectPositionFile file;
            List<string> differ, set;
            JObject config;
            Dictionary<string, string> copies;
            try
            {
                config = (JObject)Parse(WorkFiles.ReadText(run.Input("config")));
                settings = PositionSettings.From(config);
                copies = ProjectPositionFile.AnswerCopies(config["answerKeys"] as JObject, Parse(WorkFiles.ReadText(run.Input(ProjectPositionFile.KeyCopiesRole))) as JObject);
                JObject siteRules = ProjectPositionFile.SiteRules((JObject)Parse(WorkFiles.ReadText(run.Input(ProjectPositionFile.RulesRole))), request.LevelProfile);
                JObject document = ProjectPositionFile.DocumentOf((JObject)Parse(WorkFiles.ReadText(run.Input(ProjectPositionFile.KeyReadRole))));
                file = ProjectPositionFile.From(document, settings.Key);
                differ = ProjectPositionFile.AgainstRules(file, siteRules);
                List<string> stray = request.Inputs.Select(input => input.Role).Where(role => role != null && role.StartsWith(readPrefix, StringComparison.Ordinal))
                    .Select(role => role.Substring(readPrefix.Length)).Where(key => !settings.Group.Contains(key) || key == settings.Key).ToList();
                stray.AddRange(pointKeys.Where(key => !settings.Group.Contains(key)));
                if (stray.Count > 0)
                {
                    throw new InvalidDataException("the key reads or key points " + string.Join(", ", stray) + " are of no key of the group " + string.Join(", ", settings.Group) + " (R353)");
                }
                if (differ.Count == 0)
                {
                    var documents = new Dictionary<string, JObject> { [settings.Key] = document };
                    foreach (string key in settings.Group.Where(key => key != settings.Key && request.Inputs.Any(input => input.Role == readPrefix + key)))
                    {
                        documents[key] = ProjectPositionFile.DocumentOf((JObject)Parse(WorkFiles.ReadText(run.Input(readPrefix + key))));
                    }
                    set = ProjectPositionFile.SetDiffers(documents, settings.Key, settings.Group, copies);
                }
                else
                {
                    set = new List<string>();
                }
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            if (differ.Count > 0)
            {
                throw run.Refusal("the key read differs from keyRules.site of the level profile: " + string.Join(", and ", differ) + " (R353)");
            }
            if (set.Count > 0)
            {
                throw run.Refusal("the reference key " + settings.Key + " and the keys of its group are no one set: " + string.Join(", and ", set) + " (R353)");
            }
            List<string> held = settings.Group.Where(key => key != settings.Key).ToList();
            run.Counts["keys held against the reference"] = held.Count;
            var made = new Dictionary<string, Dictionary<string, Dictionary<string, object>>> { [ProjectPositionFile.PositionStep] = file.PositionStepItems(held) };
            Dictionary<string, object> summary = null;
            if (withBaseplan)
            {
                var points = new Dictionary<string, JObject>();
                var types = new Dictionary<string, List<string>>();
                List<Dictionary<string, string>> labels;
                Dictionary<string, int> shifts;
                try
                {
                    foreach (string key in settings.Group.Where(pointKeys.Contains))
                    {
                        points[key] = KeyPointsFile.PointsOf(WorkFiles.ReadText(run.Input(pointsPrefix + key)));
                        types[key] = ProjectPositionFile.GenericTypes(config, key);
                    }
                    List<string> faults = points.SelectMany(item => ProjectPositionFile.PointsFaults(item.Value, item.Key, copies, file)).ToList();
                    if (faults.Count > 0)
                    {
                        throw new InvalidDataException("the key points are not those of their keys and of the position file: " + string.Join(", and ", faults) + " (R353)");
                    }
                    labels = CsvRows.Read(run.Input(ProjectPositionFile.LabelsRole));
                    shifts = ProjectPositionFile.Shifts(Parse(WorkFiles.ReadText(run.Input(ProjectPositionFile.PlotShiftsRole))) as JObject);
                }
                catch (InvalidDataException refused)
                {
                    throw run.Refusal(refused.Message);
                }
                JObject baseplan = Drawing(run, "baseplan", LayerConfig.Load(run.Input("cadLayers")), ReadSettings.Load(run.Input("config")), true);
                AddinConfig addin = AddinConfig.Load(run.Input("config"));
                Dictionary<string, Dictionary<string, object>> rows;
                try
                {
                    Dictionary<string, PlanFace> faces = LocationPlan.Faces((JArray)baseplan["plots"], addin.ParityTolerance("areas") ?? throw new InvalidDataException("addin.parity.kinds.areas is null, the tolerance of the baseplan faces (R345)"));
                    rows = ProjectPositionFile.CheckPoints(file, points, settings.Group, faces, types, ProjectPositionFile.Numbering(labels, shifts), settings.CheckPointLimitMm, settings.GapDecimals,
                        ProjectPositionFile.MergesOf((JArray)baseplan["plots"]));
                }
                catch (InvalidDataException refused)
                {
                    throw run.Refusal(refused.Message);
                }
                made[ProjectPositionFile.CheckPointsStep] = rows;
                summary = ProjectPositionFile.Summary(rows, settings.CheckPointLimitMm);
                run.Counts["check points"] = (int)summary["points"];
                run.Counts["check points that pass"] = (int)summary["pass"];
                run.Counts["check points of no face"] = (int)summary["noFace"];
                run.Counts["check points on the face of another plot"] = (int)summary["otherFace"];
                run.Counts["check points over the limit"] = (int)summary["overLimit"];
                run.Counts["keys with no key points"] = (int)summary["noPoints"];
                run.Counts["check points of a number on two faces"] = (int)summary["twoFaces"];
                run.Counts["check points on a vertex merged over the limit"] = (int)summary["merged"];
                if ((bool)summary["failed"])
                {
                    run.Issues.Add(ProjectPositionFile.SummaryLine(summary));
                    run.Issues.AddRange(ProjectPositionFile.Issues(rows, settings.GapDecimals));
                }
                Func<object, int, JToken> pair = (value, decimals) => value == null ? JValue.CreateNull() : new JArray(((double[])value).Select(item => Rounding.Fixed(item, decimals)));
                Func<object, JToken> gap = value => value == null ? JValue.CreateNull() : new JValue(Rounding.Fixed((double)value, settings.GapDecimals));
                run.WriteJson(request.OutputFolder, "checkPoints.json", new JObject
                {
                    ["summary"] = ProjectPositionFile.SummaryJson(summary),
                    ["points"] = new JArray(rows.Select(row => new JObject
                    {
                        ["name"] = row.Key, ["key"] = (string)row.Value["key"], ["plot"] = (string)row.Value["plot"], ["face"] = (string)row.Value["face"], ["expectedFace"] = (string)row.Value["expectedFace"],
                        ["numberFaces"] = row.Value["numberFaces"] == null ? JValue.CreateNull() : new JArray((string[])row.Value["numberFaces"]), ["mergeMm"] = gap(row.Value["mergeMm"]),
                        ["sharedM"] = pair(row.Value["sharedM"], 3), ["internalM"] = pair(row.Value["internalM"], 3), ["keyInternalM"] = pair(row.Value["keyInternalM"], 3),
                        ["keySharedM"] = pair(row.Value["keySharedM"], 3), ["gapMm"] = gap(row.Value["gapMm"]), ["revitGapMm"] = gap(row.Value["revitGapMm"]), ["pass"] = (bool)row.Value["pass"],
                        ["why"] = (string)row.Value["why"],
                    })),
                });
            }
            if (!withBaseplan)
            {
                // R353, as R387 lists a check that cannot be made: the file is written and the answer says the check points were not made
                run.Issues.Add(ProjectPositionFile.NotMade);
            }
            run.WriteJson(request.OutputFolder, Path.GetFileName(settings.File), file.Written(summary));
            run.Counts["levels"] = file.Levels.Count;
            AddinConfig fields = AddinConfig.Load(run.Input("config"));
            foreach (string step in request.Steps ?? new List<string>())
            {
                run.Counts[step + " items"] = made[step].Count;
                run.WriteJson(request.StepsFolder, StepFile.FileName(step, "csharp"), StepFile.Build(step, "csharp", StepFile.ScopeText(request.Scope), made[step], fields.StepFields(step)));
            }
        }

        // R348. One drawing read: refused for reference only (R300) or when its header InsUnits is not Meters, then extracted with the
        // layer list, the plots of the baseplan, the blocks and the setback pieces of the footprint, the curb and ROW pieces of the road
        // layout, as CadReader writes them in plots.json, blocks.json and pieces.json, or with exact the plots with every digit of their
        // vertices as plots_exact.json of --exact gives them, for the check points of R353
        private static JObject Drawing(WorkerContext run, string role, LayerConfig layers, ReadSettings settings, bool exact = false)
        {
            string path = run.Input(role);
            string name = Path.GetFileName(path);
            if (settings.IsReferenceOnly(name))
            {
                throw run.Refusal(name + " is kept for reference only (drawings.referenceOnly) and is never read for " + role + " (R300)");
            }
            var problems = new List<string>();
            CadDocument document = CadExtractor.Open(path, problems);
            string units = document.Header.InsUnits.ToString();
            string refusal = CadUnits.HeaderRefusal(name, units);
            if (refusal != null)
            {
                throw run.Refusal(refusal);
            }
            List<string> wanted = role == "curbs" ? settings.CurbLayers.Concat(settings.RowLayers).ToList() : role == "villaBlocks" ? settings.SetbackLayers : new List<string>();
            var kept = new List<Piece>();
            CadExtract extract = CadExtractor.Extract(document, name, layers, piece =>
            {
                if (wanted.Count > 0 && ReadSteps.IsEdge(piece.Kind, piece.Layer, wanted))
                {
                    kept.Add(piece);
                }
            }, path, problems);
            var output = new JObject { ["source"] = name, ["role"] = role, ["units"] = units, ["notes"] = problems.Count };
            if (role == "baseplan")
            {
                output["summary"] = AsWritten(extract.Plots.Summary);
                output["plots"] = exact ? CadExtractor.ExactPlots(extract, units)["plots"] : AsWritten(extract.Plots.Plots);
            }
            if (role == "villaBlocks")
            {
                output["blocks"] = AsWritten(extract.Blocks.Blocks);
            }
            if (wanted.Count > 0)
            {
                output["pieces"] = AsWritten(kept);
            }
            run.Counts[role + " pieces kept"] = kept.Count;
            return output;
        }

        // R349. The road surfaces of siteBuild.roadSurfaces in their order, each the input roadSurface:<name>, every one of the order and no
        // other, the first surface of each file, named as a build names it. A LandXML not in meter is refused, never converted
        private static List<TinSurface> Surfaces(WorkerContext run, ReadSettings settings, HashSet<string> steps, Dictionary<string, Dictionary<string, Dictionary<string, object>>> made)
        {
            WorkerRequest request = run.Request;
            List<string> roles = request.Inputs.Select(input => input.Role).Where(role => role != null && role.StartsWith(WorkerRunner.RoadSurfaceRole, StringComparison.Ordinal)).ToList();
            var surfaces = new List<TinSurface>();
            if (roles.Count == 0)
            {
                return surfaces;
            }
            // R344. The snap of the road level is held against snapMaxM before any surface is read, a refusal and never a read in vain
            try
            {
                _ = settings.SnapM;
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            List<string> order = settings.RoadSurfaces;
            List<string> extra = roles.Where(role => !order.Contains(role.Substring(WorkerRunner.RoadSurfaceRole.Length))).ToList();
            List<string> missing = order.Where(name => !roles.Contains(WorkerRunner.RoadSurfaceRole + name)).ToList();
            if (extra.Count > 0 || missing.Count > 0)
            {
                throw run.Refusal("the road surfaces of the request are not those of siteBuild.roadSurfaces: not in config " + string.Join(", ", extra) + ", not in the request " + string.Join(", ", missing));
            }
            var files = new List<LandXmlFile>();
            var summary = new JObject();
            foreach (string name in order)
            {
                LandXmlFile file;
                try
                {
                    file = LandXmlReader.Read(run.Input(WorkerRunner.RoadSurfaceRole + name));
                }
                catch (LandXmlUnitException refused)
                {
                    throw run.Refusal(refused.Message);
                }
                if (file.Surfaces.Count == 0)
                {
                    throw new InvalidDataException(file.File + " holds no surface");
                }
                files.Add(file);
                LandXmlSurface first = file.Surfaces[0];
                surfaces.Add(new TinSurface(ReadSettings.RoadName(name), first.Points, first.Faces));
            }
            Dictionary<string, Dictionary<string, object>> step = ReadSteps.SurfaceStep(files);
            foreach (KeyValuePair<string, Dictionary<string, object>> item in step)
            {
                summary[item.Key] = AsWritten(item.Value);
            }
            run.WriteJson(request.OutputFolder, "read_roadSurfaces.json", new JObject { ["order"] = new JArray(order), ["surfaces"] = summary });
            run.Counts["road surfaces"] = surfaces.Count;
            if (steps.Contains(ReadSteps.Surfaces))
            {
                made[ReadSteps.Surfaces] = step;
            }
            return surfaces;
        }

        // R350. The sheets of dataBook of config in the DLUS and sales workbooks, each read once, the area key, the DLUS setbacks and the
        // sales rows as the Python readers give them, written to read_workbooks.json
        // A sheet of a workbook input read once per run, by its role and sheet, its rows and its date and duration cells counted. A sheet
        // missing, a date cell of no date or a value past the stated dimension is refused before anything of it is used
        private static List<object[]> SheetOf(WorkerContext run, Dictionary<string, XlsxSheet.Sheet> sheets, string role, string name)
        {
            string key = role + "\n" + name;
            XlsxSheet.Sheet sheet;
            if (!sheets.TryGetValue(key, out sheet))
            {
                string path = run.Input(role);
                try
                {
                    sheet = XlsxSheet.Read(path, name);
                }
                catch (InvalidDataException refused)
                {
                    throw run.Refusal(Path.GetFileName(path) + ": " + refused.Message);
                }
                sheets[key] = sheet;
                run.Counts["sheet " + name + " rows"] = sheet.Rows.Count;
                run.Counts["sheet " + name + " date cells"] = sheet.DateCells;
                run.Counts["sheet " + name + " duration cells"] = sheet.DurationCells;
            }
            return sheet.Rows;
        }

        // The area key of the DLUS and of the sales sheet when the request names both workbooks, else null
        private static SheetRows.AreaKey[] Workbooks(WorkerContext run, ReadSettings settings, HashSet<string> steps, Dictionary<string, Dictionary<string, Dictionary<string, object>>> made, Dictionary<string, XlsxSheet.Sheet> sheets)
        {
            WorkerRequest request = run.Request;
            bool dlus = request.Inputs.Any(input => input.Role == "dlus"), sales = request.Inputs.Any(input => input.Role == "sales");
            SheetRows.AreaKey[] areaKeys = null;
            if (!dlus && !sales)
            {
                return null;
            }
            Func<ReadSettings.SheetSetting, List<object[]>> rowsOf = setting => SheetOf(run, sheets, setting.Role, setting.Sheet);
            // R350. A table of no row is refused, so a step is never written empty: a sheet whose rows went missing, such as one read
            // only to a stated dimension out of date, gave 0 items on both sides and the parity held (the review of PR 53)
            Action<int, ReadSettings.SheetSetting, string> held = (count, setting, what) =>
            {
                if (count == 0)
                {
                    throw run.Refusal("the sheet " + setting.Sheet + " of the workbook " + setting.Role + " holds no row under its header for " + what + ", a table of no row is refused (R350)");
                }
            };
            var output = new JObject();
            if (dlus && sales)
            {
                ReadSettings.SheetSetting areaDlus = settings.Sheet("dataBook.data5.areaKey.dlus"), areaSales = settings.Sheet("dataBook.data5.areaKey.sales");
                SheetRows.AreaKey dlusKey = SheetRows.ByPlot(SheetRows.Rows(areaDlus.Sheet, rowsOf(areaDlus), areaDlus.HeaderRow, areaDlus.Columns));
                held(dlusKey.Order.Count, areaDlus, "the area key");
                SheetRows.AreaKey salesKey = SheetRows.ByPlot(SheetRows.Rows(areaSales.Sheet, rowsOf(areaSales), areaSales.HeaderRow, areaSales.Columns));
                held(salesKey.Order.Count, areaSales, "the area key");
                areaKeys = new[] { dlusKey, salesKey };
                Dictionary<string, Dictionary<string, object>> area = ReadSteps.AreaKeyStep(dlusKey, salesKey);
                output["areaKey"] = JObject.FromObject(area);
                if (steps.Contains(ReadSteps.AreaKey))
                {
                    made[ReadSteps.AreaKey] = area;
                }
            }
            if (sales)
            {
                ReadSettings.SheetSetting setbacks = settings.Sheet("dataBook.rev9.dlus");
                List<Dictionary<string, object>> setbackRows = SheetRows.Rows(setbacks.Sheet, rowsOf(setbacks), setbacks.HeaderRow, setbacks.Columns);
                held(setbackRows.Count, setbacks, "the DLUS setbacks");
                output["dlusSetbacks"] = new JArray(setbackRows.Select(SheetRows.ToJson));
                ReadSettings.SheetSetting plain = settings.Sheet("dataBook.rev9.sales"), rows = settings.SalesRows();
                List<Dictionary<string, object>> plainRows = SheetRows.Rows(plain.Sheet, rowsOf(plain), plain.HeaderRow, plain.Columns);
                held(plainRows.Count, plain, "the sales values");
                JObject salesRows = SheetRows.SalesRows(rowsOf(rows), rows.Columns[0].Value);
                held(salesRows.Count, rows, "the sales rows");
                output["sales"] = salesRows;
                output["salesPlain"] = new JArray(plainRows.Select(SheetRows.ToJson));
                if (steps.Contains(ReadSteps.DlusSetbacks))
                {
                    made[ReadSteps.DlusSetbacks] = ReadSteps.DlusSetbackStep(setbackRows);
                }
                if (steps.Contains(ReadSteps.Sales))
                {
                    made[ReadSteps.Sales] = ReadSteps.SalesStep(salesRows, plainRows);
                }
            }
            run.WriteJson(request.OutputFolder, "read_workbooks.json", output);
            return areaKeys;
        }

        // R351, R352. What the plot table takes from the location plan read: its rows, the faces of the baseplan, the official numbers of
        // each schedule and the shifts
        private sealed class Located
        {
            public LocationPlan.Result Result;
            public Dictionary<string, PlanFace> Faces;
            public Dictionary<string, HashSet<string>> Officials;
            public List<KeyValuePair<string, int>> Shifts;
            public double MinShare;
        }

        // R351. The location plan of the request, the input plotNumbers, read by PdfPig and numbered by the schedule fit of R181 on the
        // faces of the baseplan read of the same request, the schedule of each neighbourhood of connect.neighbourhoods the input
        // schedule:<NBH>, the sales rows and the code table from the workbook of their setting, then checked by the control point fit of
        // R387, which numbers nothing. Written to read_locationPlan.json and to the steps of the location plan the request names
        private static Located Location(WorkerContext run, ReadSettings settings, JObject baseplan, HashSet<string> steps, Dictionary<string, Dictionary<string, Dictionary<string, object>>> made,
            Dictionary<string, XlsxSheet.Sheet> sheets)
        {
            WorkerRequest request = run.Request;
            if (request.Inputs.All(input => input.Role != LocationPlan.PlotNumbersRole))
            {
                return null;
            }
            if (baseplan == null)
            {
                throw run.Refusal("the location plan is numbered on the faces of the baseplan, a read of plotNumbers needs the input baseplan in the same request (R351)");
            }
            LocationPlanSettings plan;
            try
            {
                plan = LocationPlanSettings.From(JObject.Parse(WorkFiles.ReadText(run.Input("config"))));
            }
            catch (Exception refused) when (refused is InvalidDataException || refused is InvalidCastException || refused is NullReferenceException || refused is ArgumentException)
            {
                throw run.Refusal("the location plan settings of config: " + refused.Message);
            }
            List<string> extra = request.Inputs.Select(input => input.Role).Where(role => role != null && role.StartsWith(LocationPlan.ScheduleRole, StringComparison.Ordinal)
                && plan.Neighbourhoods.All(hood => LocationPlan.ScheduleRole + hood.Key != role)).ToList();
            if (extra.Count > 0)
            {
                throw run.Refusal("the schedules " + string.Join(", ", extra) + " are of no neighbourhood of connect.neighbourhoods (R351)");
            }
            var officials = new Dictionary<string, HashSet<string>>();
            foreach (KeyValuePair<string, LocationPlanSettings.ScheduleEntry> hood in plan.Neighbourhoods)
            {
                string role = LocationPlan.ScheduleRole + hood.Key;
                if (request.Inputs.All(input => input.Role != role))
                {
                    throw run.Refusal("the location plan fit needs the schedule of " + hood.Key + ", " + hood.Value.Schedule + ", the input " + role + " (R351)");
                }
                string path = run.Input(role);
                try
                {
                    officials[hood.Key] = ScheduleRows.Officials(SheetOf(run, sheets, role, XlsxSheet.SheetName(path, plan.ScheduleSheet)), Path.GetFileName(path));
                }
                catch (InvalidDataException refused)
                {
                    throw run.Refusal(Path.GetFileName(path) + ": " + refused.Message);
                }
                run.Counts["schedule " + hood.Key + " plots"] = officials[hood.Key].Count;
            }
            string salesRole = settings.WorkbookRole(plan.Sales.Workbook);
            List<Dictionary<string, string>> sales, table;
            try
            {
                sales = LocationPlan.Table(SheetOf(run, sheets, salesRole, plan.Sales.Sheet), plan.Sales.HeaderRow, plan.Sales.Columns, "the sheet " + plan.Sales.Sheet);
                table = LocationPlan.Table(SheetOf(run, sheets, salesRole, plan.CodeTable.Sheet), plan.CodeTable.HeaderRow, plan.CodeTable.Columns, "the sheet " + plan.CodeTable.Sheet);
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            AddinConfig config = AddinConfig.Load(run.Input("config"));
            Dictionary<string, PlanFace> faces;
            try
            {
                faces = LocationPlan.Faces((JArray)baseplan["plots"], config.ParityTolerance("areas") ?? throw new InvalidDataException("addin.parity.kinds.areas is null, the tolerance of the baseplan faces (R345)"));
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            string pdf = run.Input(LocationPlan.PlotNumbersRole);
            PdfPage.Content page;
            LocationPlan.Result result;
            try
            {
                page = PdfPage.Read(pdf);
                result = LocationPlan.Number(page.Chars, page.Curves, faces, officials, sales, table, plan);
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(Path.GetFileName(pdf) + ": " + refused.Message);
            }
            run.Counts["location plan characters"] = page.Chars.Count;
            run.Counts["location plan curves"] = page.Curves.Count;
            run.Counts["location plan lines"] = result.Lines;
            run.Counts["location plan labels"] = result.Labels.Count;
            run.Counts["location plan labels on a face"] = result.Rows.Count(row => row.Face != null);
            run.Counts["location plan faces numbered otherwise"] = result.Control.Listed.Count;
            if (result.Control.Failed != null)
            {
                // R387. The check numbers nothing, so a check that cannot be made is an issue and the numbering goes on
                run.Issues.Add("the control point check of the location plan could not be made, the numbering of the schedule fit goes on: " + result.Control.Failed);
            }
            var output = new JObject
            {
                ["source"] = Path.GetFileName(pdf),
                ["rotation"] = page.Rotation,
                ["fitNeighbourhood"] = plan.FitNeighbourhood,
                ["labels"] = new JArray(result.Rows.Select(row => new JObject
                {
                    ["number"] = row.Number, ["code"] = row.Code, ["kind"] = row.Kind, ["face"] = row.Face, ["offset"] = row.Offset, ["group"] = row.Group,
                    ["pageX"] = Rounding.Fixed(row.PageX, 3), ["pageY"] = Rounding.Fixed(row.PageY, 3), ["newCode"] = row.NewCode, ["rev9"] = row.Rev9,
                    ["nbh"] = row.Nbh, ["zone"] = row.Zone, ["east"] = Rounding.Fixed(row.East, 3), ["north"] = Rounding.Fixed(row.North, 3),
                })),
                ["fits"] = JObject.FromObject(LocationPlan.FitStep(result)),
                ["controlCheck"] = JObject.FromObject(LocationPlan.CheckStep(result)["control"]),
                ["facesNumberedOtherwise"] = JObject.FromObject(LocationPlan.FacesStep(result)),
            };
            run.WriteJson(request.OutputFolder, "read_locationPlan.json", output);
            var named = new Dictionary<string, Func<LocationPlan.Result, Dictionary<string, Dictionary<string, object>>>>
            {
                [LocationPlan.LabelStepName] = LocationPlan.LabelStep,
                [LocationPlan.FitStepName] = LocationPlan.FitStep,
                [LocationPlan.CheckStepName] = LocationPlan.CheckStep,
                [LocationPlan.FacesStepName] = LocationPlan.FacesStep,
            };
            foreach (KeyValuePair<string, Func<LocationPlan.Result, Dictionary<string, Dictionary<string, object>>>> step in named.Where(item => steps.Contains(item.Key)))
            {
                made[step.Key] = step.Value(result);
            }
            JToken share;
            try
            {
                share = settings.Get("dataBook.wiring.minShare");
            }
            catch (InvalidDataException)
            {
                share = null;
            }
            return new Located
            {
                Result = result, Faces = faces, Officials = officials, Shifts = plan.Neighbourhoods.Select(hood => new KeyValuePair<string, int>(hood.Key, hood.Value.Shift)).ToList(),
                MinShare = share == null || (share.Type != JTokenType.Float && share.Type != JTokenType.Integer) ? double.NaN : share.Value<double>(),
            };
        }

        // R352. The plot table of the request, the input border, per baseplan face inside the Phase 1 border, from the location plan, the
        // footprint blocks and the area key of the same request, written to read_plotTable.json and to the step plotTable
        private static void Table(WorkerContext run, Located located, JObject baseplan, JObject footprint, SheetRows.AreaKey[] areaKeys, int overlapDecimals, HashSet<string> steps,
            Dictionary<string, Dictionary<string, Dictionary<string, object>>> made)
        {
            WorkerRequest request = run.Request;
            if (request.Inputs.All(input => input.Role != PlotTable.BorderRole))
            {
                return;
            }
            if (located == null || footprint == null || areaKeys == null)
            {
                throw run.Refusal("the plot table needs the location plan, the footprint blocks and the area key of the same request, the inputs plotNumbers, villaBlocks, dlus and sales (R352)");
            }
            if (double.IsNaN(located.MinShare) || double.IsInfinity(located.MinShare) || located.MinShare <= 0 || located.MinShare > 1)
            {
                throw run.Refusal("dataBook.wiring.minShare of config is no share above 0 and at most 1, the least share of a footprint block in a face (R352)");
            }
            List<double[]> border;
            try
            {
                border = PlotTable.Border(CsvRows.Read(run.Input(PlotTable.BorderRole)));
            }
            catch (Exception refused) when (refused is InvalidDataException || refused is FormatException)
            {
                throw run.Refusal("the border: " + refused.Message);
            }
            Dictionary<string, Dictionary<string, object>> table;
            try
            {
                table = PlotTable.Build((JArray)baseplan["plots"], located.Faces, border, located.Officials, located.Shifts,
                    located.Result.Rows, (JArray)footprint["blocks"], located.MinShare, areaKeys[0], areaKeys[1], overlapDecimals);
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            run.Counts["plot table faces"] = table.Count;
            var rows = new JArray();
            foreach (Dictionary<string, object> row in table.Values)
            {
                var item = new JObject();
                foreach (KeyValuePair<string, object> cell in row)
                {
                    item[cell.Key] = cell.Value is double number ? new JValue(Rounding.Fixed(number, 4)) : cell.Value is double[] pair ? new JArray(pair.Select(value => Rounding.Fixed(value, 4)))
                        : cell.Value is JToken token ? token.DeepClone() : cell.Value == null ? JValue.CreateNull() : JToken.FromObject(cell.Value);
                }
                rows.Add(item);
            }
            run.WriteJson(request.OutputFolder, "read_plotTable.json", new JObject { ["border"] = border.Count, ["minShare"] = located.MinShare, ["plots"] = rows });
            if (steps.Contains(PlotTable.StepName))
            {
                made[PlotTable.StepName] = table;
            }
        }

        // R412. The command newProjectCheck: the inputs config names for a project read and the report written as JSON and as text into the
        // output folder of the request, nothing else written. The request names the inputs config, rules and offsets
        private static void NewProject(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            if (string.IsNullOrEmpty(request.OutputFolder))
            {
                throw run.Refusal("a newProjectCheck names no output folder for its report (R412)");
            }
            string config = run.Input(NewProjectCheck.ConfigRole), rules = run.Input(NewProjectCheck.RulesRole), offsets = run.Input(NewProjectCheck.OffsetsRole);
            NewProjectReport report;
            try
            {
                report = NewProjectCheck.Check(config, rules, offsets, WorkRoot.OfThisMachine(), null, CheckReaders());
                NewProjectCheck.KeepApart(request.OutputFolder, report.Paths, report.InputRoot);
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            run.WriteJson(request.OutputFolder, NewProjectCheck.JsonName, report.ToJson());
            byte[] text = new System.Text.UTF8Encoding(false).GetBytes(report.Text());
            run.WriteFile(request.OutputFolder, NewProjectCheck.TextName, stream => stream.Write(text, 0, text.Length));
            run.Counts["lines"] = report.Lines.Count;
            run.Counts["ok"] = report.Count(NewProjectCheck.Ok);
            run.Counts["missing"] = report.Count(NewProjectCheck.Missing);
            run.Counts["does not fit"] = report.Count(NewProjectCheck.DoesNotFit);
            run.Counts["ROSHN only"] = report.Lines.Count(line => line.RoshnOnly);
        }

        // R412. What the check reads of a workbook and of a PDF, with OpenXml and PdfPig which only the worker loads. A file that cannot be
        // opened gives null, the sheet rows are the cells of XlsxSheet as texts
        public static NewProjectReaders CheckReaders()
        {
            return new NewProjectReaders
            {
                SheetNames = path =>
                {
                    try
                    {
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (DocumentFormat.OpenXml.Packaging.SpreadsheetDocument book = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(stream, false))
                        {
                            return book.WorkbookPart.Workbook.Sheets.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().Select(item => item.Name?.Value ?? "").ToList();
                        }
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                },
                Rows = (path, sheet, count) =>
                {
                    IEnumerable<object[]> rows = XlsxSheet.Read(path, sheet).Rows;
                    return (count < 0 ? rows : rows.Take(count)).Select(row => (IReadOnlyList<string>)row.Select(cell => cell is string text ? SheetRows.PyStrip(text) : "").ToList()).ToList();
                },
                Pdf = path =>
                {
                    try
                    {
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (UglyToad.PdfPig.PdfDocument document = UglyToad.PdfPig.PdfDocument.Open(stream))
                        {
                            string text = document.NumberOfPages == 0 ? "" : string.Concat(document.GetPage(1).Letters.Select(letter => letter.Value));
                            return new PdfFacts { Pages = document.NumberOfPages, Tokens = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries) };
                        }
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                },
            };
        }
    }
}
