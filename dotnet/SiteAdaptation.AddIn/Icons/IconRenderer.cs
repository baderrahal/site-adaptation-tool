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
        private const double Dpi = 96.0;

        public static BitmapSource Render(ResourceDictionary theme, string key, int pixels)
        {
            if (!(theme[key] is Drawing drawing))
            {
                throw new ArgumentException("the theme holds no drawing " + key, nameof(key));
            }
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.PushClip(new RectangleGeometry(new Rect(0, 0, pixels, pixels)));
                context.PushTransform(new ScaleTransform(pixels / Grid, pixels / Grid));
                context.DrawDrawing(drawing);
            }
            var bitmap = new RenderTargetBitmap(pixels, pixels, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
