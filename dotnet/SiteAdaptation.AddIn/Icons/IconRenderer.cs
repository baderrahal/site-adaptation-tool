using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SiteAdaptation.AddIn.Icons
{
    // R371, docs/design/12-ui-spec.md. The icons are drawn on a 32 unit grid in Theme.xaml and rendered at start, so the theme
    // stays their one source and no PNG file can drift from it
    public static class IconRenderer
    {
        public const double Grid = 32.0;
        private const double BaseDpi = 96.0;

        // The scale WPF renders at in this process. A visual on no window takes the system DPI of the process, so on a laptop
        // screen at 150 percent it is 1.5 and in a process that is not DPI aware it is 1
        public static double ScreenScale()
        {
            DpiScale dpi = VisualTreeHelper.GetDpi(new DrawingVisual());
            return dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        }

        // A bitmap of units * scale pixels at 96 * scale DPI, so it keeps a logical size of units and the ribbon shows it sharp
        // with no scaling of its own
        public static BitmapSource Render(ResourceDictionary theme, string key, int units, double scale)
        {
            if (!(theme[key] is Drawing drawing))
            {
                throw new ArgumentException("the theme holds no drawing " + key, nameof(key));
            }
            int pixels = (int)Math.Round(units * scale, MidpointRounding.AwayFromZero);
            if (units < 1 || pixels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "an icon needs at least one pixel");
            }
            double dpi = BaseDpi * pixels / units;
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.PushClip(new RectangleGeometry(new Rect(0, 0, units, units)));
                context.PushTransform(new ScaleTransform(units / Grid, units / Grid));
                context.DrawDrawing(drawing);
            }
            var bitmap = new RenderTargetBitmap(pixels, pixels, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
