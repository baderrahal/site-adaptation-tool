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
using SiteAdaptation.AddIn.SetUpSite;
using SiteAdaptation.AddIn.Theme;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.UiShots
{
    // R372, control 3 of docs/design/12-ui-spec.md. The one entry that renders each window offscreen at 100 percent scale, 96
    // DPI, with the sample data below and no Revit. The command line writes the drafts, the picture test renders again and holds
    // each against its baseline. Call it on a thread of a single threaded apartment
    public static class Shots
    {
        public const string RibbonIcons = "ribbon-icons";
        public const string Pane = "pane";
        public const string PaneFirstOpen = "pane-first-open";
        public const string SetUpSite = "setup-site";
        public static readonly string[] Windows = { RibbonIcons, Pane, PaneFirstOpen, SetUpSite };

        private const double Dpi = 96.0;

        public static BitmapSource Render(string window)
        {
            switch (window)
            {
                case RibbonIcons: return Icons();
                case Pane: return PanePage(true);
                case PaneFirstOpen: return PanePage(false);
                case SetUpSite: return SetUpPage();
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
        // and the edges of the text fit in
        public static byte[] Png(BitmapSource bitmap)
        {
            var opaque = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
            var indexed = new FormatConvertedBitmap(opaque, PixelFormats.Indexed8, new BitmapPalette(opaque, 256), 0);
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

        // The Set up site window with a view, measured and arranged at the width of the theme and not shown, so a test can read its parts
        public static SetUpSiteWindow LaidOutSetUp(SetUpSiteView view)
        {
            var window = new SetUpSiteWindow(new SetUpSiteSession(view, new SetUpSiteCallbacks(), () => null));
            LayOut(window);
            return window;
        }

        // A window that is not shown builds no visual tree of its own, so its content is measured at the width of the theme and the
        // height it asks for, capped by the MaxHeight of the theme, and arranged there. Its data context comes down the logical tree
        public static FrameworkElement LayOut(Window window)
        {
            var body = (FrameworkElement)window.Content;
            double width = (double)window.FindResource("SA.Size.DialogWidth");
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
                new SetUpRow { Label = "Floor types", Value = "" },
                new SetUpRow { Label = "Wall types", Value = "BW, Generic Wall-150mm_ Garden", Differs = true },
                new SetUpRow { Label = "Shared parameters", Value = "" },
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
                Scope = new PaneScope { Kind = ScopeKinds.Nbh, Nbh = Nbh },
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
