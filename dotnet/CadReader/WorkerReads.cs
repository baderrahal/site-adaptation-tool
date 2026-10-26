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
    // R348 to R350. The command read of the worker, the reads of button 2 (docs/addin-design.md 3.2): each drawing of the request read
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
                List<Dictionary<string, string>> labels = CsvRows.Read(run.Input("labels"));
                List<Dictionary<string, string>> plotsCsv = CsvRows.Read(run.Input("plotsCsv"));
                Dictionary<string, List<double[][]>> faces = ReadSteps.FaceRings((JArray)reads["baseplan"]["plots"], plotsCsv);
                made[ReadSteps.RoadLevels] = ReadSteps.RoadLevelStep(PlotScope.Numbers(request.Scope, labels), faces, surfaces, settings.SnapM);
            }
            AddinConfig config = AddinConfig.Load(run.Input("config"));
            foreach (string step in request.Steps ?? new List<string>())
            {
                run.Counts[step + " items"] = made[step].Count;
                run.WriteJson(request.StepsFolder, StepFile.FileName(step, "csharp"), StepFile.Build(step, "csharp", StepFile.ScopeText(request.Scope), made[step], config.StepFields(step)));
            }
        }

        // R353. The command position of button 1: the project position file from the read of the reference key, refused when it differs
        // from keyRules.site of the level profile, written to the output folder under the file name of addin.position.file, and when the
        // request names the baseplan and the location plan the check points of button 1, the control points of the sheet, plots min and
        // plots max of the face centroids, the survey point and the project base point, each by the file and by the survey pair of the read
        private static void Position(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            if (string.IsNullOrEmpty(request.OutputFolder))
            {
                throw run.Refusal("a position names no output folder for the position file (R353)");
            }
            if (request.Inputs.All(input => input.Role != ProjectPositionFile.RulesRole) || request.Inputs.All(input => input.Role != ProjectPositionFile.KeyReadRole))
            {
                throw run.Refusal("the position file is written from the read of a key and held against keyRules.site, the request needs the inputs keyRead and rules (R353)");
            }
            bool withBaseplan = request.Inputs.Any(input => input.Role == "baseplan"), withPlan = request.Inputs.Any(input => input.Role == LocationPlan.PlotNumbersRole);
            if (withBaseplan != withPlan)
            {
                throw run.Refusal("the check points need the baseplan and the location plan, the inputs baseplan and plotNumbers, the request names one of them (R353)");
            }
            PositionSettings settings;
            JObject document;
            ProjectPositionFile file;
            List<string> differ;
            try
            {
                settings = PositionSettings.From((JObject)Parse(WorkFiles.ReadText(run.Input("config"))));
                JObject siteRules = ProjectPositionFile.SiteRules((JObject)Parse(WorkFiles.ReadText(run.Input(ProjectPositionFile.RulesRole))), request.LevelProfile);
                document = ProjectPositionFile.DocumentOf((JObject)Parse(WorkFiles.ReadText(run.Input(ProjectPositionFile.KeyReadRole))));
                file = ProjectPositionFile.From(document, settings.Key);
                differ = ProjectPositionFile.AgainstRules(file, siteRules);
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            if (differ.Count > 0)
            {
                throw run.Refusal("the key read differs from keyRules.site of the level profile: " + string.Join(", and ", differ) + " (R353)");
            }
            var made = new Dictionary<string, Dictionary<string, Dictionary<string, object>>> { [ProjectPositionFile.PositionStep] = file.PositionStepItems() };
            if (withBaseplan)
            {
                if (request.Inputs.All(input => input.Role != "cadLayers"))
                {
                    throw run.Refusal("a read of a drawing needs the input cadLayers, the keep and drop list of the layers (config/cad_layers.json)");
                }
                JObject baseplan = Drawing(run, "baseplan", LayerConfig.Load(run.Input("cadLayers")), ReadSettings.Load(run.Input("config")));
                AddinConfig config = AddinConfig.Load(run.Input("config"));
                try
                {
                    Dictionary<string, PlanFace> faces = LocationPlan.Faces((JArray)baseplan["plots"], config.ParityTolerance("areas") ?? throw new InvalidDataException("addin.parity.kinds.areas is null, the tolerance of the baseplan faces (R345)"));
                    List<double[]> centroids = faces.Values.OrderBy(face => long.Parse(face.Label, System.Globalization.CultureInfo.InvariantCulture)).Select(face => new[] { face.CentroidX, face.CentroidY }).ToList();
                    PdfPage.Content page = PdfPage.Read(run.Input(LocationPlan.PlotNumbersRole));
                    List<double[]> controls = LocationPlan.ControlTexts(LocationPlan.Words(page.Chars)).Select(text => new[] { text.East, text.North }).ToList();
                    made[ProjectPositionFile.CheckPointsStep] = ProjectPositionFile.CheckPoints(file, controls, centroids, document, settings.CheckPointLimitMm, settings.GapDecimals);
                }
                catch (InvalidDataException refused)
                {
                    throw run.Refusal(refused.Message);
                }
                Dictionary<string, Dictionary<string, object>> rows = made[ProjectPositionFile.CheckPointsStep];
                run.Counts["check points"] = rows.Count;
                run.Counts["check points that pass"] = rows.Values.Count(row => true.Equals(row["pass"]));
                run.WriteJson(request.OutputFolder, "checkPoints.json", new JObject
                {
                    ["limitMm"] = settings.CheckPointLimitMm,
                    ["points"] = new JArray(rows.Select(row => new JObject
                    {
                        ["name"] = row.Key, ["sharedM"] = new JArray(((double[])row.Value["sharedM"]).Select(value => Rounding.Fixed(value, 3))),
                        ["internalM"] = new JArray(((double[])row.Value["internalM"]).Select(value => Rounding.Fixed(value, 3))),
                        ["keyInternalM"] = new JArray(((double[])row.Value["keyInternalM"]).Select(value => Rounding.Fixed(value, 3))),
                        ["gapMm"] = Rounding.Fixed((double)row.Value["gapMm"], settings.GapDecimals), ["pass"] = (bool)row.Value["pass"],
                    })),
                });
            }
            run.WriteJson(request.OutputFolder, Path.GetFileName(settings.File), file.Written());
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
        // layout, as CadReader writes them in plots.json, blocks.json and pieces.json
        private static JObject Drawing(WorkerContext run, string role, LayerConfig layers, ReadSettings settings)
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
                output["plots"] = AsWritten(extract.Plots.Plots);
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
    }
}
