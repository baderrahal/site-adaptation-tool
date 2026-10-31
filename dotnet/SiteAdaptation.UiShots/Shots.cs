using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Icons;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.AddIn.ReviewData;
using SiteAdaptation.AddIn.RulesWindow;
using SiteAdaptation.AddIn.SetUpSite;
using SiteAdaptation.AddIn.Theme;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ManualRules;
using SiteAdaptation.Common.Review;
using SiteAdaptation.AddIn.Update;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Update;

namespace SiteAdaptation.UiShots
{
    // R372, control 3 of docs/design/12-ui-spec.md. The one entry that renders each window offscreen at 100 percent scale, 96
    // DPI, with the sample data below and no Revit. The command line writes the drafts, the picture test renders again and holds
    // each against its baseline. Call it on a thread of a single threaded apartment
    public static partial class Shots
    {
        public const string RibbonIcons = "ribbon-icons";
        public const string Pane = "pane";
        public const string PaneFirstOpen = "pane-first-open";
        public const string SetUpSite = "setup-site";
        public const string ReviewData = "review-data";
        public const string Rules = "rules";
        public const string CheckTable = "check-table";
        public const string UpdateFromReport = "update-from-report";
        public const string NewProject = "new-project";
        public const string HowItWorks = "how-it-works";
        public const string ScanResults = "scan-results";
        public static readonly string[] Windows = { RibbonIcons, Pane, PaneFirstOpen, SetUpSite, ReviewData, Rules, CheckTable, UpdateFromReport, NewProject, HowItWorks, ScanResults };

        private const double Dpi = 96.0;

        public static BitmapSource Render(string window)
        {
            switch (window)
            {
                case RibbonIcons: return Icons();
                case Pane: return PanePage(true);
                case PaneFirstOpen: return PanePage(false);
                case SetUpSite: return SetUpPage();
                case ReviewData: return ReviewPage();
                case Rules: return RulesPage();
                case CheckTable: return CheckPage();
                case UpdateFromReport: return UpdatePage();
                case NewProject: return NewProjectPage();
                case HowItWorks: return HowItWorksPage();
                case ScanResults: return ScanResultsPage();
                default: throw new ArgumentException("no window " + window + ", only " + string.Join(", ", Windows), nameof(window));
            }
        }

        public static List<string> WriteAll(string folder)
        {
            Directory.CreateDirectory(folder);
            var written = new List<string>();
            foreach (string window in Windows)
            {
                string path = Path.Combine(folder, window + ".png");
                File.WriteAllBytes(path, Png(Render(window)));
                written.Add(path);
            }
            return written;
        }

        // R400 caps every file of steps/ at 100 KB, so a picture is kept in a palette of its own 256 colours, which the flat colours
        // and the edges of the text fit in. The palette is the 256 colours the picture holds most of, so a flat ground keeps its
        // exact colour and no dither noise is spread over it, which the optimal palette of WPF did on a window with large grounds
        public static byte[] Png(BitmapSource bitmap)
        {
            var opaque = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
            byte[] bgr = new byte[opaque.PixelWidth * opaque.PixelHeight * 3];
            opaque.CopyPixels(bgr, opaque.PixelWidth * 3, 0);
            var counts = new Dictionary<int, int>();
            for (int at = 0; at < bgr.Length; at += 3)
            {
                int key = (bgr[at + 2] << 16) | (bgr[at + 1] << 8) | bgr[at];
                counts[key] = counts.TryGetValue(key, out int seen) ? seen + 1 : 1;
            }
            List<Color> colors = counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(256)
                .Select(pair => Color.FromRgb((byte)(pair.Key >> 16), (byte)(pair.Key >> 8), (byte)pair.Key)).ToList();
            // Each pixel takes the nearest colour of the palette by its own distance, with no dither, which FormatConvertedBitmap adds
            var nearest = new Dictionary<int, byte>();
            byte[] index = new byte[opaque.PixelWidth * opaque.PixelHeight];
            for (int at = 0; at < index.Length; at++)
            {
                int key = (bgr[at * 3 + 2] << 16) | (bgr[at * 3 + 1] << 8) | bgr[at * 3];
                if (!nearest.TryGetValue(key, out byte found))
                {
                    int best = int.MaxValue;
                    for (int item = 0; item < colors.Count; item++)
                    {
                        int dr = colors[item].R - (key >> 16), dg = colors[item].G - ((key >> 8) & 255), db = colors[item].B - (key & 255);
                        int distance = dr * dr + dg * dg + db * db;
                        if (distance < best)
                        {
                            best = distance;
                            found = (byte)item;
                        }
                    }
                    nearest[key] = found;
                }
                index[at] = found;
            }
            BitmapSource indexed = BitmapSource.Create(opaque.PixelWidth, opaque.PixelHeight, bitmap.DpiX, bitmap.DpiY, PixelFormats.Indexed8, new BitmapPalette(colors), index, opaque.PixelWidth);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(indexed));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        public static BitmapSource Load(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                return new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            }
        }

        // A window as its PNG gives it back, in memory, so a compare leaves no file behind
        public static BitmapSource AsPng(string window)
        {
            using (var stream = new MemoryStream(Png(Render(window))))
            {
                return new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            }
        }

        // The share of pixels that differ in any channel, 1 when the sizes differ
        public static double Differ(BitmapSource one, BitmapSource other)
        {
            if (one.PixelWidth != other.PixelWidth || one.PixelHeight != other.PixelHeight)
            {
                return 1.0;
            }
            byte[] a = Pixels(one);
            byte[] b = Pixels(other);
            int differ = 0;
            for (int at = 0; at < a.Length; at += 4)
            {
                if (a[at] != b[at] || a[at + 1] != b[at + 1] || a[at + 2] != b[at + 2] || a[at + 3] != b[at + 3])
                {
                    differ++;
                }
            }
            return differ / (double)(a.Length / 4);
        }

        public static byte[] Pixels(BitmapSource bitmap)
        {
            var bgra = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
            bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
            return pixels;
        }

        // The icons of every ribbon button in its order as the ribbon takes them, the 32 px set over the 16 px set, on the white of
        // the theme
        private static BitmapSource Icons()
        {
            ResourceDictionary theme = ThemeDictionary.Load();
            double gap = (double)theme["SA.Space.L"];
            List<RibbonButtonSpec> buttons = RibbonLayout.AllButtons.ToList();
            int width = (int)(buttons.Count * 32 + (buttons.Count + 1) * gap);
            int height = (int)(32 + 16 + 3 * gap);
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawRectangle((Brush)theme["SA.Surface"], null, new Rect(0, 0, width, height));
                for (int at = 0; at < buttons.Count; at++)
                {
                    double left = gap + at * (32 + gap);
                    context.DrawImage(IconRenderer.Render(theme, buttons[at].IconKey32, 32, 1.0), new Rect(left, gap, 32, 32));
                    context.DrawImage(IconRenderer.Render(theme, buttons[at].IconKey16, 16, 1.0), new Rect(left + 8, 32 + 2 * gap, 16, 16));
                }
            }
            var bitmap = new RenderTargetBitmap(width, height, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource PanePage(bool inUse)
        {
            SitePanePage page = LaidOutPane(inUse);
            var bitmap = new RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(page);
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource SetUpPage()
        {
            SetUpSiteWindow window = LaidOutSetUp(SampleSetUpView(true, true));
            FrameworkElement body = Body(window);
            var bitmap = new RenderTargetBitmap((int)body.ActualWidth, (int)body.ActualHeight, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(body);
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource ReviewPage()
        {
            ReviewDataWindow window = LaidOutReview(SampleReviewView(true));
            FrameworkElement body = Body(window);
            var bitmap = new RenderTargetBitmap((int)body.ActualWidth, (int)body.ActualHeight, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(body);
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource UpdatePage()
        {
            FrameworkElement body = Body(LaidOutUpdate());
            var bitmap = new RenderTargetBitmap((int)body.ActualWidth, (int)body.ActualHeight, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(body);
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource RulesPage()
        {
            RulesWindow window = LaidOutRules(SampleRulesTable(), "M06");
            FrameworkElement body = Body(window);
            var bitmap = new RenderTargetBitmap((int)body.ActualWidth, (int)body.ActualHeight, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(body);
            bitmap.Freeze();
            return bitmap;
        }

        // The Site Adaptation Rules window on a table, laid out as the others and not shown, the rule of selectedId selected
        public static RulesWindow LaidOutRules(ManualRuleTable table, string selectedId)
        {
            var window = new RulesWindow(new RulesWindowSession(table, selectedId, path => false, path => { }));
            LayOut(window);
            return window;
        }

        // The Update from report window with the sample list, measured and arranged and not shown, so a test can read its parts
        public static UpdateFromReportWindow LaidOutUpdate()
        {
            var session = new UpdateFromReportSession(new UpdateFromReportCallbacks(), () => null);
            session.Model.Load(SampleUpdateList(), new[] { "012_Levels_parsons2024Rev6.xlsx", "012_Report.xlsx" });
            var window = new UpdateFromReportWindow(session);
            LayOut(window);
            return window;
        }

        // The block manualRules of the rules profile of the repo, texts only and no coordinate or level, found by walking up from the
        // folder of the program to the folder that holds rules/roshn-makkah.json
        public static ManualRuleTable SampleRulesTable()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "rules", "roshn-makkah.json")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            if (folder == null)
            {
                throw new FileNotFoundException("rules/roshn-makkah.json was not found above " + AppDomain.CurrentDomain.BaseDirectory);
            }
            return new ManualRuleTable(ManualRuleBlock.FromFile(Path.Combine(folder, "rules", "roshn-makkah.json")), null);
        }

        // The Review data window with a view, laid out as the Set up site one, and the first plot selected so the side box is filled
        public static ReviewDataWindow LaidOutReview(ReviewView view)
        {
            var session = new ReviewDataSession(view, new ReviewCallbacks());
            if (session.Model.Rows.Count > 0)
            {
                session.Model.Selected = session.Model.Rows[0];
            }
            var window = new ReviewDataWindow(session);
            LayOut(window);
            return window;
        }

        // Made up numbers only, no coordinate and no level of a real model. Five plots of a made up NBH 012, the first three with a
        // plan fact of button 4 and corners, two checked OK and one Wrong. withPlan false is the view before button 4 has built, every
        // value of the plan not read yet
        public static ReviewView SampleReviewView(bool withPlan)
        {
            var table = new JObject();
            string[] types = { "VL4", "VL6", "C10", "TH2", "DP1", "DT3" };
            for (int at = 0; at < 5; at++)
            {
                string label = (1001 + at).ToString();
                table[label] = new JObject
                {
                    ["baseplanLabel"] = label,
                    ["neighbourhood"] = "012",
                    ["locationPlanNumber"] = (31 + at).ToString(),
                    ["officialNumber"] = (1031 + at).ToString(),
                    ["typologyRev9"] = at == 4 ? JValue.CreateNull() : (JToken)types[at % types.Length],
                    ["footprintMirrored"] = at == 4 ? JValue.CreateNull() : (JToken)(at % 3 == 0),
                    ["faceAreaM2"] = 300.0 + at * 12.5,
                };
            }
            var plan = new Dictionary<string, ReviewPlanFact>();
            var rings = new Dictionary<string, double[][]>();
            for (int at = 0; withPlan && at < 3; at++)
            {
                double x = 5000 + at * 30, y = 6000;
                string label = (1001 + at).ToString();
                plan[label] = new ReviewPlanFact { Cluster = "C" + (at / 2 + 1), Mirror = at % 3 == 0, FrontEdge = 0, RoadAtFront = "R" + (at + 1) };
                rings[label] = new[] { new[] { x, y }, new[] { x + 20, y }, new[] { x + 20, y + 25 }, new[] { x, y + 25 } };
            }
            JObject site = JObject.Parse("{ 'angleToTrueNorthDeg': 0.0, 'surveyPoint': { 'position': [100.0, 200.0, 0.0], 'sharedPosition': [5000.0, 6000.0, 0.0] } }");
            List<ReviewRow> rows = ReviewTable.Rows(table, "012", plan, rings, site);
            var checks = new List<ReviewCheck>
            {
                new ReviewCheck { Plot = "31", Check = ReviewCheckValue.Ok, Note = "", RunId = "sample", PlotTableHash = "sample" },
                new ReviewCheck { Plot = "32", Check = ReviewCheckValue.Ok, Note = "", RunId = "sample", PlotTableHash = "sample" },
                new ReviewCheck { Plot = "34", Check = ReviewCheckValue.Wrong, Note = "Front edge on the wrong side", RunId = "sample", PlotTableHash = "sample" },
            };
            return new ReviewView
            {
                Nbh = "012",
                Sources = new[] { "ROSHN Makkah Baseplan.dwg", "Makkah Building Footprint.dwg" },
                State = new ReviewState(rows, checks, "sample", "sample"),
            };
        }

        // Made up numbers only. Four changes of NBH 012: one that follows the rules, one that breaks a profile rule, one that breaks a manual
        // rule and starts unticked, one of a cell with no number, and one cell of a column that is not editable
        public static UpdateList SampleUpdateList()
        {
            var list = new UpdateList();
            list.Changes.Add(SampleChange("102", UpdateText.Entry, 10.2, 10.15, true, null, null, null));
            list.Changes.Add(SampleChange("103", UpdateText.Steps, 2L, 3L, true, "stepsRule", null, null));
            list.Changes.Add(SampleChange("101", UpdateText.Ffl, 10.2, 10.25, false, "stepHeights", "M08", null));
            list.Changes.Add(SampleChange("104", UpdateText.Entry, 10.0, null, false, null, null, "the cell holds no number"));
            list.Ignored.Add(new IgnoredCell { Plot = "101", Column = "SSL", FileName = "012_Levels_parsons2024Rev6.xlsx", Sheet = "Levels", Why = "not editable" });
            return list;
        }

        private static UpdateChange SampleChange(string plot, string field, object inModel, object inExcel, bool ticked, string breaks, string manual, string invalid)
        {
            var change = new UpdateChange { Plot = plot, Field = field, InModel = inModel, InExcel = inExcel, Ticked = ticked, Invalid = invalid };
            // A level change the checks ran on, so the first row reads that it follows the rules
            change.Checks.Add(SiteAdaptation.Common.Update.UpdateFromReport.FflOverEntry);
            if (breaks != null)
            {
                change.Breaks.Add(breaks);
            }
            if (manual != null)
            {
                change.ManualRules.Add(manual);
            }
            return change;
        }

        // The Set up site window with a view, measured and arranged at the width of the theme and not shown, so a test can read its parts
        public static SetUpSiteWindow LaidOutSetUp(SetUpSiteView view)
        {
            var window = new SetUpSiteWindow(new SetUpSiteSession(view, new SetUpSiteCallbacks(), () => null));
            LayOut(window);
            return window;
        }

        // A window that is not shown builds no visual tree of its own, so its content is measured at the width the window takes from the
        // theme, the dialog width when it names none, and the height it asks for, capped by the MaxHeight of the theme, and arranged there.
        // Its data context comes down the logical tree
        public static FrameworkElement LayOut(Window window)
        {
            var body = (FrameworkElement)window.Content;
            double width = double.IsNaN(window.Width) ? (double)window.FindResource("SA.Size.DialogWidth") : window.Width;
            double cap = (double)window.FindResource("SA.Size.DialogMaxHeight");
            for (int pass = 0; pass < 3; pass++)
            {
                body.Measure(new Size(width, cap));
                double height = Math.Min(Math.Ceiling(body.DesiredSize.Height), cap);
                body.Arrange(new Rect(0, 0, width, height));
                body.UpdateLayout();
            }
            return body;
        }

        public static FrameworkElement Body(Window window)
        {
            return (FrameworkElement)window.Content;
        }

        // Made up numbers only, no coordinate and no level of a real model. The view of an existing site file after the dry run, the
        // rows that differ marked, or before it with the Revit column empty
        public static SetUpSiteView SampleSetUpView(bool dryRunDone, bool existing)
        {
            var view = new SetUpSiteView
            {
                ExistingSite = existing,
                DryRunDone = dryRunDone,
                PassLimitMm = 0.1,
                RampSlope = 0.5,
                ParameterSheetName = "Parameter sheet.xlsx",
                ParameterCount = 12,
            };
            if (existing)
            {
                view.Signs.AddRange(new[] { "Settings entity found", "Survey point off the internal origin" });
            }
            view.RampSlopeChoices.AddRange(new[] { 0.5, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 });
            // R441, the section Current files, DRAFT of SCAN-UI, the site drawing ticked so Apply shows as it does after a dry run
            view.CurrentFiles = new List<CurrentFileRow>
            {
                new CurrentFileRow { Input = InputNames.Baseplan, FileName = "site.dwg", Site = true, Current = true },
                new CurrentFileRow { Input = InputNames.VillaBlocks, FileName = "footprints.dwg", Current = true },
                new CurrentFileRow { Input = InputNames.Sales, FileName = "plot list.xlsx", Current = false },
            };
            view.RulesProfiles = new List<RulesChoice> { new RulesChoice { Name = "Sample project", Path = "rules.json" } };
            view.RulesFile = "rules.json";
            view.Now.AddRange(new[]
            {
                new SetUpRow { Label = "Survey point", Value = "E 1000.000, N 2000.000, Z 0.000" },
                new SetUpRow { Label = "Project base point", Value = "E 1000.000, N 2000.000, Z 0.000" },
                new SetUpRow { Label = "Levels", Value = "Level 1, Level 2" },
                new SetUpRow { Label = "Floor types and wall types", Value = "Generic 150mm, Ramps 0.5%, BW" },
                new SetUpRow { Label = "Shared parameters", Value = "9 found" },
                new SetUpRow { Label = "Units", Value = "meters, accuracy 0.01" },
            });
            view.WillSet.AddRange(new[]
            {
                new SetUpRow { Label = "Coordinate system", Value = "Shared coordinates of CAD, site Internal", Differs = false },
                new SetUpRow { Label = "Survey point", Value = "E 1000.000, N 2000.000, Z 0.000" },
                new SetUpRow { Label = "Angle to true north", Value = "0" },
                new SetUpRow { Label = "Levels", Value = "Level 1 at 0.000, Level 2 at 4.000 over Level 1", Differs = true },
                new SetUpRow { Label = "Floor types", Value = SetUpTexts.FloorTypesRow },
                new SetUpRow { Label = "Ramp slope", Value = "0.5%" },
                new SetUpRow { Label = "Wall types", Value = "BW, Generic Wall-150mm_ Garden", Differs = true },
                new SetUpRow { Label = "Shared parameters", Value = SetUpTexts.FromSheet("Parameter sheet.xlsx, 12 parameters") },
                new SetUpRow { Label = "Units", Value = "meters, accuracy 0.01" },
            });
            string[] names = { "control 1", "control 2", "control 3", "plots min", "plots max", "survey point", "project base point" };
            for (int at = 0; at < names.Length; at++)
            {
                string cad = (1000 + at * 10) + ".000, " + (2000 + at * 10) + ".000, " + at + ".000";
                view.CheckPoints.Add(new CheckPointRow
                {
                    Point = names[at],
                    Cad = cad,
                    Revit = dryRunDone ? cad : null,
                    DiffMm = dryRunDone ? (double?)0.0 : null,
                    Pass = dryRunDone,
                });
            }
            view.Passing = dryRunDone ? names.Length : 0;
            return view;
        }

        // The pane with the sample data, measured and arranged at the start width of the spec, so a test can read its parts
        public static SitePanePage LaidOutPane(bool inUse)
        {
            return LaidOutPane(inUse, true);
        }

        // With no document the pane shows its labels with no values and every button grey, as Revit shows it with no project open
        public static SitePanePage LaidOutPane(bool inUse, bool withDocument)
        {
            var model = new PaneViewModel(new SampleProbe());
            model.Load(withDocument ? Sample.Document() : null);
            if (inUse && withDocument)
            {
                model.LoadSettings(Sample.Settings());
                model.SetNbhCodes(new[] { Sample.Nbh });
                model.SetSteps(Sample.Steps());
            }
            model.SetSavedStarts(new[] { Sample.SavedProject });
            var page = new SitePanePage(model);
            double width = (double)page.FindResource("SA.Size.Pane");
            // The rows of an ItemsControl come after the first layout pass, so the height is taken once it holds still
            double height = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                page.Measure(new Size(width, double.PositiveInfinity));
                double measured = Math.Ceiling(page.DesiredSize.Height);
                page.Arrange(new Rect(0, 0, width, measured));
                page.UpdateLayout();
                if (measured == height)
                {
                    break;
                }
                height = measured;
            }
            return page;
        }
    }

    // The mock's NBH 012 column of docs/design/07-mock.md 7.2 with file names only, no real path, coordinate or level. The size
    // and the hash of Plot numbers are the mock's, every other size and hash is a sample value, steps/ui-shots/README.md. The mock
    // has no parameter sheet, its name is a sample name
    internal static class Sample
    {
        public const string Nbh = "012";

        // The saved project of Start from the boards name, a sample file name, no file of a user
        public const string SavedProject = "ROSHN Makkah";
        private const string Folder = @"C:\Sample\";

        public static readonly Dictionary<string, Tuple<long, string>> Files = new Dictionary<string, Tuple<long, string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "project.json", Tuple.Create(48213L, (string)null) },
            { "roshn-makkah.json", Tuple.Create(906118L, (string)null) },
            { "ROSHN Makkah Baseplan.dwg", Tuple.Create(61458432L, (string)null) },
            { "Makkah Building Footprint.dwg", Tuple.Create(9437184L, (string)null) },
            { "Makkah Phase 1_Road Layout.dwg", Tuple.Create(120586240L, (string)null) },
            { "landxml", Tuple.Create(236978176L, (string)null) },
            { "TBD-PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_Rev9.0.xlsx", Tuple.Create(1258291L, (string)null) },
            { "PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx", Tuple.Create(838861L, (string)null) },
            { "01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf", Tuple.Create(3056293L, "7061ac0d") },
            { "Prototypes_2024", Tuple.Create(1076887552L, (string)null) },
            { "Parameter sheet.xlsx", Tuple.Create(52224L, (string)null) },
        };

        public static DocumentSession Document()
        {
            return new DocumentSession("sample", Folder + "01101-PSN-IMC-R17B-TEST-NEW.rvt", "01101-PSN-IMC-R17B-TEST-NEW");
        }

        public static PaneSettings Settings()
        {
            var settings = new PaneSettings
            {
                ConfigFile = Folder + "project.json",
                RulesFile = Folder + "roshn-makkah.json",
                Scope = new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = Nbh },
            };
            settings.Pick(InputKeys.Baseplan, Folder + "ROSHN Makkah Baseplan.dwg");
            settings.Pick(InputKeys.VillaBlocks, Folder + "Makkah Building Footprint.dwg");
            settings.Pick(InputKeys.Curbs, Folder + "Makkah Phase 1_Road Layout.dwg");
            settings.Pick(InputKeys.RoadSurface, Folder + "landxml");
            settings.Pick(InputKeys.Dlus, Folder + "TBD-PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_Rev9.0.xlsx");
            settings.Pick(InputKeys.Sales, Folder + "PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx");
            settings.Pick(InputKeys.PlotNumbers, Folder + "01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf");
            settings.Pick(InputKeys.Prototypes, Folder + "Prototypes_2024");
            settings.Pick(InputKeys.ParameterSheet, Folder + "Parameter sheet.xlsx");
            return settings;
        }

        // The mock's state after Set up site, Read data and Check on NBH 012, in the order of 13-workflow-changes.md: 3 Review data
        // done, 4 Check with its issues, 5 next and 9 Report ready, since it needs 4 only
        public static IReadOnlyList<StepStatus> Steps()
        {
            return new[]
            {
                new StepStatus(StepKind.Done),
                new StepStatus(StepKind.Done),
                new StepStatus(StepKind.Done),
                new StepStatus(StepKind.Issues, 1247),
                new StepStatus(StepKind.Ready),
                new StepStatus(StepKind.NeedsStep, 5),
                new StepStatus(StepKind.NeedsStep, 6),
                new StepStatus(StepKind.NeedsStep, 7),
                new StepStatus(StepKind.Ready),
            };
        }
    }

    internal sealed class SampleProbe : IInputProbe
    {
        public InputFacts Look(string path, bool folder)
        {
            return Sample.Files.TryGetValue(Path.GetFileName(path), out Tuple<long, string> file) ? new InputFacts { Found = true, Size = file.Item1 } : new InputFacts();
        }

        // A sample hash of the file name, so no hash of a real file shows unless the mock gives it
        public void Hash(string path, bool folder, Action<string> done)
        {
            string name = Path.GetFileName(path);
            if (Sample.Files.TryGetValue(name, out Tuple<long, string> file) && file.Item2 != null)
            {
                done(file.Item2);
                return;
            }
            using (var sha = SHA256.Create())
            {
                done(string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(name)).Select(value => value.ToString("x2"))));
            }
        }

        public string VillaModeDefault(string configFile)
        {
            return VillaModes.Masses;
        }

        // The name the Pane board shows on the Rules row
        public string RulesName(string rulesFile)
        {
            return string.IsNullOrEmpty(rulesFile) ? null : "ROSHN Makkah";
        }

        public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
        {
            fallback = null;
            if (string.IsNullOrEmpty(rulesFile))
            {
                return new string[0];
            }
            fallback = "parsons2024Rev6";
            return new[] { "parsons2024Rev6", "native2022" };
        }
    }
}
