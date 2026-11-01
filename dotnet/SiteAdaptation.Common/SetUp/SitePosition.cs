using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.Common.SetUp
{
    // R455. The outline of one plot of the baseplan read, its vertices in drawing metres
    public sealed class CadPlot
    {
        public string Plot { get; set; }
        public List<double[]> Vertices { get; set; }
    }

    // R455, R456. The site button 1 sets from CAD: the shared position of the internal origin, the survey point and the project base point,
    // the site location, the named position, the coordinate systems the inputs state and the control points
    public sealed class CadSite
    {
        public SitePosition Position { get; set; }
        public double[] SurveyInternal { get; set; }
        public double[] SurveyShared { get; set; }
        public double[] BaseInternal { get; set; }
        public double[] BaseShared { get; set; }
        public double LatitudeDeg { get; set; }
        public double LongitudeDeg { get; set; }
        public string PositionName { get; set; }
        public string PlaceName { get; set; }
        public int Epsg { get; set; }
        public SystemsCheck Systems { get; set; } = new SystemsCheck();
        public List<ControlPoint> ControlPoints { get; set; } = new List<ControlPoint>();
        public double ControlPointLimitMm { get; set; }
    }

    // R455. The site position from CAD. The shared origin is the centre of the box of every plot outline, rounded to
    // coordinates.originRoundM (CHOSEN, Q326), so the internal origin sits in the middle of the site. The internal origin keeps shared
    // height 0 and the survey point and project base point stand on it at coordinates.originElevationM, internal and shared, the relation
    // the position file of the past job holds (CHOSEN, Q327). True north is coordinates.angleToTrueNorthDeg, the grid of the drawing
    public static class SiteFromCad
    {
        public static CadSite FromCad(IReadOnlyList<CadPlot> plots, CoordinateSettings settings, int epsg)
        {
            List<double[]> vertices = plots.Where(plot => plot.Vertices != null).SelectMany(plot => plot.Vertices).ToList();
            if (vertices.Count == 0)
            {
                throw new InvalidDataException("the baseplan read holds no plot outline, so button 1 has no site position from CAD (R455)");
            }
            double east = Round((vertices.Min(vertex => vertex[0]) + vertices.Max(vertex => vertex[0])) / 2, settings.OriginRoundM);
            double north = Round((vertices.Min(vertex => vertex[1]) + vertices.Max(vertex => vertex[1])) / 2, settings.OriginRoundM);
            double[] at = UtmMath.ToLatLon(east, north, UtmMath.ZoneOf(epsg));
            var point = new[] { 0.0, 0.0, settings.OriginElevationM };
            var shared = new[] { east, north, settings.OriginElevationM };
            return new CadSite
            {
                Position = new SitePosition { EastM = east, NorthM = north, ElevationM = 0.0, AngleDeg = settings.AngleToTrueNorthDeg },
                SurveyInternal = point, SurveyShared = shared, BaseInternal = (double[])point.Clone(), BaseShared = (double[])shared.Clone(),
                LatitudeDeg = at[0], LongitudeDeg = at[1], PositionName = settings.PositionName, PlaceName = settings.PlaceName, Epsg = epsg,
            };
        }

        // R454 to R456 for button 1: null when config keeps the position file, coordinates.source positionFile or no block coordinates,
        // else the CAD read, its coordinate systems held together, the site and the control points
        public static CadSite Load(JObject config, RunContext context, Func<RunContext, CoordinateRead> read)
        {
            if (config["coordinates"] == null)
            {
                return null;
            }
            CoordinateSettings settings = CoordinateSettings.From(config);
            if (!settings.FromCad)
            {
                return null;
            }
            if (read == null)
            {
                throw new InvalidOperationException("coordinates.source is cad and this run of button 1 reads no CAD (R455)");
            }
            CoordinateRead cad = read(context);
            SystemsCheck systems = CoordinateSystems.Agree(cad.Files, settings);
            CadSite site = FromCad(cad.Plots, settings, systems.EpsgForMath);
            site.Systems = systems;
            site.ControlPoints = ControlPoints.Pick(cad.Plots, settings.ControlPointCount);
            site.ControlPointLimitMm = settings.ControlPointLimitMm;
            return site;
        }

        private static double Round(double value, double step)
        {
            return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
        }
    }

    // R454. crs.json of the worker command crs: each input with the system it states and the plot outlines of the baseplan
    public sealed class CoordinateRead
    {
        public const string Format = "site-adaptation-crs";

        public List<StatedSystem> Files { get; set; } = new List<StatedSystem>();
        public List<CadPlot> Plots { get; set; } = new List<CadPlot>();

        public static CoordinateRead Parse(JObject answer)
        {
            var read = new CoordinateRead();
            foreach (JObject file in (answer["files"] as JArray ?? throw new InvalidDataException("crs.json holds no files (R454)")).OfType<JObject>())
            {
                read.Files.Add(new StatedSystem { File = (string)file["file"], Name = file["name"]?.Type == JTokenType.String ? (string)file["name"] : null });
            }
            foreach (JObject plot in (answer["plots"] as JArray ?? new JArray()).OfType<JObject>())
            {
                read.Plots.Add(new CadPlot
                {
                    Plot = (string)plot["plot"],
                    Vertices = (plot["vertices"] as JArray ?? new JArray()).OfType<JArray>().Select(vertex => vertex.Select(value => value.Value<double>()).ToArray()).ToList(),
                });
            }
            return read;
        }
    }

    // R454. The request of the worker command crs: config, the layer list beside it, and each drawing and LandXML of the pane, a folder
    // of LandXML one input per file named role:file, every input with its SHA-256 so the worker holds it unchanged over the read
    public static class CoordinateRequest
    {
        public const string Command = "crs";
        public const string OutputFile = "crs.json";
        public const string BaseplanRole = "baseplan";
        public const string LayersRole = "cadLayers";
        public const string ConfigRole = "config";
        private static readonly string[] Drawings = { ".dwg", ".dxf" };
        private const string LandXmlExtension = ".xml";

        public static bool IsDrawing(string path)
        {
            return Drawings.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        public static bool IsLandXml(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() == LandXmlExtension;
        }

        public static WorkerRequest Make(IReadOnlyList<RunInput> inputs, IReadOnlyDictionary<string, string> roles, string configPath, string runId, string outputFolder, WorkerDocument document, double timeLimitS)
        {
            var files = new List<WorkerFile> { File(ConfigRole, configPath) };
            foreach (RunInput input in inputs.Where(input => !string.IsNullOrEmpty(input.Path) && roles.ContainsKey(input.Name) && roles[input.Name] != ConfigRole))
            {
                string role = roles[input.Name];
                if (input.IsFolder || Directory.Exists(input.Path))
                {
                    foreach (string path in Directory.GetFiles(input.Path).Where(IsLandXml).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        files.Add(File(role + ":" + Path.GetFileName(path), path));
                    }
                }
                else if (IsDrawing(input.Path) || IsLandXml(input.Path))
                {
                    files.Add(File(role, input.Path));
                }
            }
            if (files.Any(file => file.Role == BaseplanRole))
            {
                JObject read = (JObject.Parse(WorkFiles.ReadText(configPath))["addin"] as JObject)?["readData"] as JObject;
                string layers = read?["cadLayers"]?.Type == JTokenType.String ? (string)read["cadLayers"] : throw new InvalidDataException("addin.readData.cadLayers of config names no layer list, the plot outlines of the baseplan need it (R454)");
                files.Add(File(LayersRole, Path.Combine(Path.GetDirectoryName(configPath), layers)));
            }
            return new WorkerRequest
            {
                FormatVersion = WorkerRequest.CurrentFormatVersion, RunId = runId, Document = document, Command = Command, Inputs = files,
                Scope = new WorkerScope { Kind = "project" }, OutputFolder = outputFolder, TimeLimitS = timeLimitS,
            };
        }

        // Button 1 with coordinates.source cad: the request made of the pane inputs, the worker run and crs.json read back. The pane
        // inputs come from the caller since button 1 hashes the Settings file only (PaneRunState.InputsOf)
        public static CoordinateRead Run(RunContext context, IReadOnlyList<RunInput> inputs, IReadOnlyDictionary<string, string> roles, string settingsInput, WorkerDocument document, Func<WorkerRequest, string, WorkerAnswer> worker)
        {
            string configPath = context.Inputs.FirstOrDefault(input => input.Name == settingsInput)?.Path ?? throw new InvalidOperationException("the run names no " + settingsInput + " file");
            string output = Path.Combine(context.WorkFolder, "crs_" + context.RunId);
            WorkerRequest request = Make(inputs, roles, configPath, context.RunId + "-crs", output, document, AddinConfig.Load(configPath).WorkerTimeLimitS(Command));
            if (request.Inputs.All(input => input.Role != BaseplanRole))
            {
                throw new InvalidOperationException("coordinates.source is cad and the pane names no baseplan drawing (R455)");
            }
            WorkerAnswer answer = worker(request, context.WorkFolder);
            if (answer == null || answer.Status != WorkerExit.Status(WorkerExit.Done))
            {
                throw new InvalidOperationException("the worker command " + Command + " ended " + (answer == null ? "with no answer" : answer.Status + ", " + answer.Message));
            }
            string path = (answer.Outputs ?? new List<WorkerFile>()).Select(file => file.Path).FirstOrDefault(file => string.Equals(Path.GetFileName(file), OutputFile, StringComparison.OrdinalIgnoreCase))
                ?? Path.Combine(output, OutputFile);
            if (!System.IO.File.Exists(path))
            {
                throw new InvalidOperationException("the worker command " + Command + " wrote no " + OutputFile);
            }
            return CoordinateRead.Parse(JObject.Parse(WorkFiles.ReadText(path)));
        }

        private static WorkerFile File(string role, string path)
        {
            return new WorkerFile { Role = role, Path = path, Sha256 = FileHash.Sha256(path) };
        }
    }
}
