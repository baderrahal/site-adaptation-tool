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

        // R513. The site of typed values, built as R455 builds one from CAD on coordinates.expectedEpsg. Refused outside the UTM range, so a slip of a digit never reaches the model
        public static CadSite Typing(double east, double north, double angle, CoordinateSettings settings)
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
            CadSite site = SiteFromCad.At(east, north, angle, settings, settings.ExpectedEpsg);
            site.Source = Typed;
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
                new SourceRow
                {
                    Label = SetUpTexts.SurveyPoint, Cad = SetUpTexts.Coordinate(cad.SurveyShared), File = SetUpTexts.Coordinate(read.SurveyShared),
                    Difference = Millimetres(SiteCoordinates.Gap(cad.SurveyShared, read.SurveyShared)),
                },
            };
            if (cad.PlotBox != null)
            {
                double[] box = cad.PlotBox;
                double east = Math.Max(Math.Max(box[0] - other.EastM.Value, 0.0), other.EastM.Value - box[1]);
                double north = Math.Max(Math.Max(box[2] - other.NorthM.Value, 0.0), other.NorthM.Value - box[3]);
                double off = Math.Sqrt(east * east + north * north);
                rows.Add(new SourceRow { Label = SetUpTexts.FileOriginToPlots, Cad = SetUpTexts.PlotBox, File = off > 0 ? SetUpTexts.OutsidePlots : SetUpTexts.InsidePlots, Difference = Millimetres(off) });
            }
            return rows;
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

        public void Start(RunContext context, SiteState now)
        {
            bool blank = SiteSources.NoCoordinates(now, SiteSources.Limits(config));
            picked = held?.PositionFile;
            string source = SiteSources.Starting(config, held?.CoordinateSource, blank, readCad != null);
            if (source == SiteSources.Typed)
            {
                UseSite(SiteSources.Typing(now.Position.EastM.Value, now.Position.NorthM.Value, now.Position.AngleDeg.Value, CoordinateSettings.From(config)));
            }
            else if (source == SiteSources.PositionFile)
            {
                UseFile(readJson(FilePath()));
            }
            else
            {
                ReadCad(context, source, held?.CoordinateSource == null && SiteSources.OfConfig(config) == SiteSources.PositionFile);
            }
        }

        // R513, a click of the window. A pick that does not read throws and leaves the choice as it was, the window shows the reason
        public void Choose(RunContext context, SiteSourcePick pick)
        {
            switch (pick?.Source)
            {
                case SiteSources.Cad:
                    ReadCad(context, SiteSources.Cad, false);
                    break;
                case SiteSources.PositionFile:
                    JObject file = readJson(pick.PositionFile ?? FilePath());
                    SetUpTarget.ReadPosition(new SetUpTarget(), file, SetUpDefaults.SiteUnits(config));
                    picked = pick.PositionFile ?? picked;
                    UseFile(file);
                    break;
                case SiteSources.Typed:
                    if (pick.EastM == null || pick.NorthM == null || pick.AngleDeg == null)
                    {
                        throw new InvalidDataException(SetUpTexts.TypedNotNumbers);
                    }
                    UseSite(SiteSources.Typing(pick.EastM.Value, pick.NorthM.Value, pick.AngleDeg.Value, CoordinateSettings.From(config)));
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

        private void ReadCad(RunContext context, string source, bool mayStayOnFile)
        {
            // A slip in the block is a fault of config and no reason to ask the user for a source
            CoordinateSettings.From(config);
            try
            {
                UseSite(SiteFromCad.Load(config, context, readCad, source));
                check = ReadCheck();
            }
            catch (NoBaseplanException) when (mayStayOnFile)
            {
                UseFile(readJson(FilePath()));
            }
            catch (Exception failure) when (readCad != null && (failure is InvalidOperationException || failure is InvalidDataException || failure is IOException))
            {
                Cad = null;
                Position = null;
                Source = null;
                check = null;
                Problem = string.Join(" ", failure.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        private void UseSite(CadSite site)
        {
            Cad = site;
            Position = null;
            Source = site.Source;
            Problem = null;
            check = null;
        }

        private void UseFile(JObject file)
        {
            Cad = null;
            Position = file;
            Source = SiteSources.PositionFile;
            Problem = null;
            check = null;
        }

        private JObject ReadCheck()
        {
            try
            {
                JObject file = readJson(FilePath());
                SetUpTarget.ReadPosition(new SetUpTarget(), file, SetUpDefaults.SiteUnits(config));
                return file;
            }
            catch (Exception failure) when (failure is InvalidDataException || failure is IOException || failure is JsonException || failure is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
