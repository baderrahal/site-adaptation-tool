using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.SetUp
{
    // R510. The two limits coordinates.noCoordinatesLimitMm and noCoordinatesLimitDeg of config
    public sealed class BlankLimits
    {
        public double Mm { get; set; }
        public double Deg { get; set; }
    }

    // R513. What the user chose in the window: the source, with the file picked or the typed values where it needs them
    public sealed class SiteSourcePick
    {
        public string Source { get; set; }
        public string PositionFile { get; set; }
        public double? EastM { get; set; }
        public double? NorthM { get; set; }
        public double? AngleDeg { get; set; }
    }

    // R512. One row of the section CAD and position file
    public sealed class SourceRow
    {
        public string Label { get; set; }
        public string Cad { get; set; }
        public string File { get; set; }
        public string Difference { get; set; }
    }

    // R513. The pane names no baseplan drawing. A type of its own, so a blank model under the position file can stay on the file (R511)
    public sealed class NoBaseplanException : InvalidOperationException
    {
        public NoBaseplanException()
            : base("The pane names no baseplan drawing, so the tool has no coordinates from CAD.")
        {
        }
    }

    // R510 to R514. Where the site position of button 1 comes from, with no Revit type
    public static class SiteSources
    {
        public const string Cad = CoordinateSettings.FromCadSource;
        public const string PositionFile = CoordinateSettings.FromPositionFile;
        public const string Typed = "typed";

        public static readonly string[] All = { Cad, PositionFile, Typed };

        // R543. The source a site.json names, null for no file, a file that does not read or a file of before the key
        public static string Written(string siteText)
        {
            if (string.IsNullOrEmpty(siteText))
            {
                return null;
            }
            try
            {
                JToken written = JObject.Parse(siteText)[SetUpRun.PositionSourceKey];
                return written != null && written.Type == JTokenType.String ? (string)written : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // Null for a config of before the keys, a shipped set copied once keeps its own, so such a project says nothing and runs as before
        public static BlankLimits Limits(JObject config)
        {
            JObject block = config?["coordinates"] as JObject;
            double? mm = Positive(block?["noCoordinatesLimitMm"]);
            double? deg = Positive(block?["noCoordinatesLimitDeg"]);
            return mm == null || deg == null ? null : new BlankLimits { Mm = mm.Value, Deg = deg.Value };
        }

        // R510. The position and the two points alone decide. A new project from the default metric template reads all of them 0 and still holds a place name with its latitude
        // and longitude and one active location (probe of 3 Nov 2026), so the site location can never tell a blank model
        public static bool NoCoordinates(SiteState now, BlankLimits limits)
        {
            if (limits == null)
            {
                return false;
            }
            SitePosition at = now.Position;
            bool zero = Math.Abs(at.EastM.Value) * 1000.0 <= limits.Mm && Math.Abs(at.NorthM.Value) * 1000.0 <= limits.Mm && Math.Abs(at.ElevationM.Value) * 1000.0 <= limits.Mm
                && Math.Abs(SetUpPlanner.Turn(at.AngleDeg.Value)) <= limits.Deg;
            return zero && AtOrigin(now.Survey, limits) && AtOrigin(now.ProjectBase, limits);
        }

        private static bool AtOrigin(BasePointState point, BlankLimits limits)
        {
            var origin = new[] { 0.0, 0.0, 0.0 };
            return SiteCoordinates.Gap(point.InternalM, origin) * 1000.0 <= limits.Mm && SiteCoordinates.Gap(point.SharedM, origin) * 1000.0 <= limits.Mm;
        }

        // R511. coordinates.newProjectSource when it names a source a project can start on, else null, so a hand edit falls back on coordinates.source
        public static string NewProject(JObject config)
        {
            JToken source = (config?["coordinates"] as JObject)?["newProjectSource"];
            return source != null && source.Type == JTokenType.String && ((string)source == Cad || (string)source == PositionFile) ? (string)source : null;
        }

        // R511. The source a press starts on. A choice held in the settings stands, typed only while the model still holds the values. A blank model takes CAD
        // whatever coordinates.source says, the position file was never meant to set one unseen. Everything else stays as coordinates.source gives, so a saved
        // job does not move. A slip in coordinates.source is given back as it stands and SiteFromCad.Load refuses it by name
        public static string Starting(JObject config, string held, bool blank, bool canReadCad)
        {
            JObject block = config?["coordinates"] as JObject;
            if (block == null)
            {
                return PositionFile;
            }
            if (held != null && !(held == Typed && blank))
            {
                return held;
            }
            return blank && canReadCad ? Cad : OfConfig(config);
        }

        // coordinates.source as a text, null for none
        public static string OfConfig(JObject config)
        {
            JToken source = (config?["coordinates"] as JObject)?["source"];
            return source != null && source.Type == JTokenType.String ? (string)source : null;
        }

        // R513. The site of typed values, built as R455 builds one from CAD on coordinates.expectedEpsg. The UTM range refuses only a value that is no easting or northing at all, a
        // wrong digit inside the range passes it, and the check rows pass too since they hold the site to itself. The box of the plots, when plots read, is the one check that can
        // see it (SetUpPlanner). The angle is held to coordinates.angleToTrueNorthDeg, since a build refuses any other (R83, Placer)
        public static CadSite Typing(double east, double north, double angle, CoordinateSettings settings, double[] plotBox = null)
        {
            if (!Finite(east) || !Finite(north) || !Finite(angle))
            {
                throw new InvalidDataException(SetUpTexts.TypedNotNumbers);
            }
            string outside = CoordinateSystems.TypedOutside(east, north, settings, settings.ExpectedEpsg);
            if (outside != null)
            {
                throw new InvalidDataException(outside);
            }
            if (Math.Abs(SetUpPlanner.Turn(angle - settings.AngleToTrueNorthDeg)) > SetUpPlanner.SameDeg)
            {
                throw new InvalidDataException(SetUpTexts.TypedAngleRefused(angle, settings.AngleToTrueNorthDeg));
            }
            CadSite site = SiteFromCad.At(east, north, angle, settings, settings.ExpectedEpsg);
            site.Source = Typed;
            site.PlotBox = plotBox;
            site.OffBoxLimitM = settings.TypedOffBoxLimitM;
            site.Systems = new SystemsCheck { Outcome = SystemAgreement.NoneStated, EpsgForMath = settings.ExpectedEpsg };
            site.ControlPointLimitMm = settings.ControlPointLimitMm;
            return site;
        }

        // R512. CAD beside the position file. The CAD origin is the rounded centre of the plots and the file holds an origin of its own, so Easting and Northing differ by that
        // choice alone. The last row is what holds meaning: how far the origin of the file lies off the box of the plots, 0 when both are in one frame
        public static List<SourceRow> Compare(CadSite cad, JObject file, Dictionary<string, string> units)
        {
            var read = new SetUpTarget();
            SetUpTarget.ReadPosition(read, file, units);
            SitePosition mine = cad.Position, other = read.Position;
            var rows = new List<SourceRow>
            {
                Metres(SetUpTexts.Easting, mine.EastM.Value, other.EastM.Value),
                Metres(SetUpTexts.Northing, mine.NorthM.Value, other.NorthM.Value),
                Metres(SetUpTexts.Elevation, mine.ElevationM.Value, other.ElevationM.Value),
                new SourceRow
                {
                    Label = SetUpTexts.Angle, Cad = SetUpTexts.Number(mine.AngleDeg.Value, 6), File = SetUpTexts.Number(other.AngleDeg.Value, 6),
                    Difference = SetUpTexts.Number(Math.Abs(SetUpPlanner.Turn(mine.AngleDeg.Value - other.AngleDeg.Value)), 6) + " deg",
                },
                // East and north alone, the row Elevation is the height of the origin and reads 0 mm while the survey point stands at another height, so the height has its own row
                new SourceRow
                {
                    Label = SetUpTexts.SurveyPoint, Cad = SetUpTexts.Coordinate(Flat(cad.SurveyShared)), File = SetUpTexts.Coordinate(Flat(read.SurveyShared)),
                    Difference = Millimetres(SiteCoordinates.Gap(new[] { cad.SurveyShared[0], cad.SurveyShared[1], 0.0 }, new[] { read.SurveyShared[0], read.SurveyShared[1], 0.0 })),
                },
                Metres(SetUpTexts.SurveyHeight, cad.SurveyShared[2], read.SurveyShared[2]),
            };
            if (cad.PlotBox != null)
            {
                double off = OffBox(cad.PlotBox, other.EastM.Value, other.NorthM.Value);
                rows.Add(new SourceRow { Label = SetUpTexts.FileOriginToPlots, Cad = SetUpTexts.PlotBox, File = off > 0 ? SetUpTexts.OutsidePlots : SetUpTexts.InsidePlots, Difference = Millimetres(off) });
            }
            return rows;
        }

        // R512, R513. How far a point lies off the box of the plots, least east, most east, least north, most north, in metres, 0 inside it
        public static double OffBox(double[] box, double east, double north)
        {
            double offEast = Math.Max(Math.Max(box[0] - east, 0.0), east - box[1]);
            double offNorth = Math.Max(Math.Max(box[2] - north, 0.0), north - box[3]);
            return Math.Sqrt(offEast * offEast + offNorth * offNorth);
        }

        // R523. Moving the position or the survey point moves no element (RevitSiteDocument), so on a model that holds coordinates and placed content everything would stand off the
        // site. Null when nothing moves, the model is blank or nothing is placed, else the plain line that stops button 1
        public static string PlacedStop(SiteState now, SetUpTarget target, SetUpChanges changes)
        {
            if ((!changes.Position && !changes.SurveyPoint) || NoCoordinates(now, target.BlankLimits))
            {
                return null;
            }
            int floors = target.FloorTypes.Sum(name => now.FloorsByType != null && now.FloorsByType.TryGetValue(name, out int count) ? count : 0);
            return now.PlacedLinks + now.Masses + floors == 0 ? null : SetUpTexts.PlacedContent(now.PlacedLinks, now.Masses, floors);
        }

        private static double[] Flat(double[] point)
        {
            return new[] { point[0], point[1] };
        }

        private static SourceRow Metres(string label, double mine, double other)
        {
            return new SourceRow { Label = label, Cad = SetUpPlanner.Invariant(mine, 3), File = SetUpPlanner.Invariant(other, 3), Difference = Millimetres(Math.Abs(mine - other)) };
        }

        private static string Millimetres(double metres)
        {
            return SetUpTexts.Number(metres * 1000.0, 2) + " mm";
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double? Positive(JToken value)
        {
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer))
            {
                return null;
            }
            double number = value.Value<double>();
            return Finite(number) && number > 0 ? number : (double?)null;
        }
    }

    // R511, R513. The source of one press of button 1, what it read and what the user chose in the window. A file picked and the source go into the settings, the typed values
    // never do: R367 keeps every coordinate out of the settings JSON, and a second press reads them back from the model that holds them
    public sealed class SiteChoice
    {
        private readonly JObject config;
        private readonly Func<RunContext, CoordinateRead> readCad;
        private readonly Func<string, JObject> readJson;
        private readonly ProjectSettings held;
        private string picked;
        private JObject check;
        private double[] plotBox;
        private bool boxRead;

        public SiteChoice(JObject config, Func<RunContext, CoordinateRead> readCad, Func<string, JObject> readJson, ProjectSettings held)
        {
            this.config = config;
            this.readCad = readCad;
            this.readJson = readJson;
            this.held = held;
        }

        // Null while no usable source is there, the window then asks
        public string Source { get; private set; }

        public CadSite Cad { get; private set; }

        // The position file when it is the source, null when it is only a check
        public JObject Position { get; private set; }

        // Why CAD gives no coordinates, one line
        public string Problem { get; private set; }

        public string PickedFile => picked;

        // R512. Why the check against the position file did not run, null when it ran or is not asked
        public string CheckLine { get; private set; }

        // R513. Whatever a source fails on, the window opens and asks. Only a slip in config itself stays a failure of the run
        public void Start(RunContext context, SiteState now)
        {
            bool blank = SiteSources.NoCoordinates(now, SiteSources.Limits(config));
            picked = held?.PositionFile;
            string source = SiteSources.Starting(config, held?.CoordinateSource, blank, readCad != null);
            if (source == SiteSources.Typed)
            {
                CoordinateSettings settings = CoordinateSettings.From(config);
                double[] box = ReadBox(context);
                try
                {
                    UseSite(SiteSources.Typing(now.Position.EastM.Value, now.Position.NorthM.Value, now.Position.AngleDeg.Value, settings, box));
                }
                catch (InvalidDataException failure)
                {
                    Ask(failure);
                }
            }
            else if (source == SiteSources.PositionFile)
            {
                string path = FilePath();
                try
                {
                    UseFile(ReadFile(path));
                }
                catch (Exception failure) when (Unreadable(failure))
                {
                    Ask(failure);
                }
            }
            else
            {
                ReadCad(context, source);
            }
        }

        // R513, a click of the window. A pick that does not read throws and leaves the choice as it was, the window shows the reason
        public void Choose(RunContext context, SiteSourcePick pick)
        {
            switch (pick?.Source)
            {
                case SiteSources.Cad:
                    ReadCad(context, SiteSources.Cad);
                    break;
                case SiteSources.PositionFile:
                    JObject file = ReadFile(pick.PositionFile ?? FilePath());
                    picked = pick.PositionFile ?? picked;
                    UseFile(file);
                    break;
                case SiteSources.Typed:
                    if (pick.EastM == null || pick.NorthM == null || pick.AngleDeg == null)
                    {
                        throw new InvalidDataException(SetUpTexts.TypedNotNumbers);
                    }
                    UseSite(SiteSources.Typing(pick.EastM.Value, pick.NorthM.Value, pick.AngleDeg.Value, CoordinateSettings.From(config), ReadBox(context)));
                    break;
                default:
                    throw new InvalidDataException("the window names no site source of " + string.Join(", ", SiteSources.All) + " (R513)");
            }
        }

        // The source written only where it is not what config already gives, so a saved past job keeps settings of the same bytes and a second press stays on the choice
        public void Hold(ProjectSettings settings)
        {
            if (Source == null || settings == null)
            {
                return;
            }
            if (settings.CoordinateSource != null || Source != (SiteSources.OfConfig(config) ?? SiteSources.PositionFile))
            {
                settings.CoordinateSource = Source;
            }
            if (picked != null)
            {
                settings.PositionFile = picked;
            }
        }

        // R512. Empty unless CAD is the source and a position file reads
        public List<SourceRow> Rows()
        {
            return Source == SiteSources.Cad && Cad != null && check != null ? SiteSources.Compare(Cad, check, SetUpDefaults.SiteUnits(config)) : new List<SourceRow>();
        }

        private string FilePath()
        {
            return picked ?? PositionSettings.From(config).File;
        }

        private void ReadCad(RunContext context, string source)
        {
            // A slip in the block is a fault of config and no reason to ask the user for a source
            CoordinateSettings.From(config);
            try
            {
                CadSite site = SiteFromCad.Load(config, context, readCad, source);
                KeepBox(site);
                UseSite(site);
                check = ReadCheck();
            }
            catch (Exception failure) when (readCad != null && Unreadable(failure))
            {
                boxRead = true;
                plotBox = null;
                Ask(failure);
            }
        }

        // R513. The box of the plots a typed origin is held to, read once a press. A CAD with a stop gives none, a box outside the UTM range says nothing of a typed origin, and no
        // read at all leaves the typed values with no check, which the window says
        private double[] ReadBox(RunContext context)
        {
            if (!boxRead && readCad != null)
            {
                try
                {
                    KeepBox(SiteFromCad.Load(config, context, readCad, SiteSources.Cad));
                }
                catch (Exception failure) when (Unreadable(failure))
                {
                    plotBox = null;
                }
                boxRead = true;
            }
            return plotBox;
        }

        private void KeepBox(CadSite site)
        {
            boxRead = true;
            plotBox = site != null && site.Systems.Stops.Count == 0 ? site.PlotBox : null;
        }

        private JObject ReadFile(string path)
        {
            JObject file = readJson(path);
            SetUpTarget.ReadPosition(new SetUpTarget(), file, SetUpDefaults.SiteUnits(config));
            return file;
        }

        private static bool Unreadable(Exception failure)
        {
            return failure is InvalidOperationException || failure is InvalidDataException || failure is IOException || failure is JsonException || failure is UnauthorizedAccessException;
        }

        private void Ask(Exception failure)
        {
            Cad = null;
            Position = null;
            Source = null;
            check = null;
            CheckLine = null;
            Problem = OneLine(failure.Message);
        }

        private static string OneLine(string text)
        {
            return string.Join(" ", text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private void UseSite(CadSite site)
        {
            Cad = site;
            Position = null;
            Source = site.Source;
            Problem = null;
            check = null;
            CheckLine = null;
        }

        private void UseFile(JObject file)
        {
            Cad = null;
            Position = file;
            Source = SiteSources.PositionFile;
            Problem = null;
            check = null;
            CheckLine = null;
        }

        private JObject ReadCheck()
        {
            try
            {
                return ReadFile(FilePath());
            }
            catch (FileNotFoundException)
            {
                CheckLine = SetUpTexts.CheckNotRun("the file is not on disk");
            }
            catch (Exception failure) when (Unreadable(failure))
            {
                CheckLine = SetUpTexts.CheckNotRun(OneLine(failure.Message));
            }
            return null;
        }
    }
}
