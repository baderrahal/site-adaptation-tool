using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteAdaptation.Common;

namespace SiteAdaptation.TestHost
{
    // R384, docs/design/08-install.md line 15. Pictures of our own Revit only: the live pane page of the add-in by RenderTargetBitmap,
    // and the main window of this process by PrintWindow, cut to the ribbon. Never a grab of the screen or of another process
    internal static class RevitShots
    {
        // PW_RENDERFULLCONTENT, so the DirectX and WPF parts of the window are drawn into the picture too
        private const uint RenderFullContent = 2;
        private const double Dpi = 96.0;

        [StructLayout(LayoutKind.Sequential)]
        private struct Bounds
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PrintWindow(IntPtr handle, IntPtr context, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr handle, out Bounds bounds);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr handle, IntPtr context);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr context);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr context, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr context, IntPtr item);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr item);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr context);

        // The pane page as Revit hosts it, found among the WPF sources of this thread, drawn at 100 percent scale
        public static RunShot Pane(Type pageType, string file)
        {
            FrameworkElement page = pageType == null ? null : FindPage(pageType);
            if (page == null)
            {
                return new RunShot { Name = "pane", Blank = true, Detail = "the pane page is in no WPF source of this Revit, the pane may be hidden" };
            }
            int width = (int)Math.Ceiling(page.ActualWidth);
            int height = (int)Math.Ceiling(page.ActualHeight);
            if (width < 1 || height < 1)
            {
                return new RunShot { Name = "pane", Blank = true, Detail = "the pane page is laid out at " + width + " by " + height + " px" };
            }
            // Through a brush, so the offset of the page inside the pane does not shift it out of the picture
            var visual = new DrawingVisual();
            using (DrawingContext drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(new VisualBrush(page), null, new Rect(0, 0, width, height));
            }
            var bitmap = new RenderTargetBitmap(width, height, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return Saved("pane", file, bitmap, "the live page of the pane");
        }

        private static FrameworkElement FindPage(Type pageType)
        {
            foreach (PresentationSource source in PresentationSource.CurrentSources)
            {
                var queue = new Queue<DependencyObject>();
                if (source.RootVisual != null)
                {
                    queue.Enqueue(source.RootVisual);
                }
                while (queue.Count > 0)
                {
                    DependencyObject item = queue.Dequeue();
                    if (pageType.IsInstanceOfType(item))
                    {
                        return (FrameworkElement)item;
                    }
                    if (item is Visual || item is System.Windows.Media.Media3D.Visual3D)
                    {
                        for (int at = 0; at < VisualTreeHelper.GetChildrenCount(item); at++)
                        {
                            queue.Enqueue(VisualTreeHelper.GetChild(item, at));
                        }
                    }
                }
            }
            return null;
        }

        // The ribbon of Revit is the RibbonControl of AdWindows, read by reflection as the add-in builds its tab with it. The tab of the
        // add-in is made the active one, so the picture shows its panels. False when the tab is not found
        public static bool ActivateTab(string title, List<string> notes)
        {
            try
            {
                object ribbon = Ribbon();
                foreach (object tab in (IEnumerable)ribbon.GetType().GetProperty("Tabs").GetValue(ribbon))
                {
                    if (tab.GetType().GetProperty("Title").GetValue(tab) as string == title)
                    {
                        tab.GetType().GetProperty("IsActive").SetValue(tab, true);
                        return true;
                    }
                }
                notes.Add("the ribbon has no tab " + title);
            }
            catch (Exception failure)
            {
                notes.Add("the ribbon tab was not made active: " + Inner(failure));
            }
            return false;
        }

        // left, top, right, bottom of the ribbon in screen pixels, null when it is not read
        public static int[] RibbonBounds(List<string> notes)
        {
            try
            {
                var ribbon = (FrameworkElement)Ribbon();
                Point topLeft = ribbon.PointToScreen(new Point(0, 0));
                Point bottomRight = ribbon.PointToScreen(new Point(ribbon.ActualWidth, ribbon.ActualHeight));
                return new[] { (int)Math.Floor(topLeft.X), (int)Math.Floor(topLeft.Y), (int)Math.Ceiling(bottomRight.X), (int)Math.Ceiling(bottomRight.Y) };
            }
            catch (Exception failure)
            {
                notes.Add("the ribbon bounds were not read: " + Inner(failure));
                return null;
            }
        }

        private static object Ribbon()
        {
            Assembly windows = AppDomain.CurrentDomain.GetAssemblies().First(each => each.GetName().Name == "AdWindows");
            object ribbon = windows.GetType("Autodesk.Windows.ComponentManager", true).GetProperty("Ribbon").GetValue(null);
            if (ribbon == null)
            {
                throw new InvalidOperationException("ComponentManager.Ribbon is null");
            }
            return ribbon;
        }

        // The main window of this Revit by PrintWindow, saved whole and cut to the ribbon. revitHandle is UIApplication.MainWindowHandle,
        // checked against the handle of the process
        public static List<RunShot> Window(IntPtr revitHandle, int[] ribbon, string windowFile, string ribbonFile)
        {
            var shots = new List<RunShot>();
            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            bool same = handle == revitHandle;
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out Bounds bounds))
            {
                shots.Add(new RunShot { Name = "window", Blank = true, SameHandle = same, Detail = "the main window of this process has no handle or no rectangle" });
                return shots;
            }
            int[] window = { bounds.Left, bounds.Top, bounds.Right, bounds.Bottom };
            BitmapSource picture = Print(handle, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, out bool printed);
            RunShot whole = Saved("window", windowFile, picture, "PrintWindow " + (printed ? "drew" : "failed"));
            whole.WindowRect = window;
            whole.SameHandle = same;
            shots.Add(whole);
            int[] crop = ShotCrop.Within(window, ribbon);
            if (crop == null)
            {
                return shots;
            }
            var cut = new CroppedBitmap(picture, new Int32Rect(crop[0], crop[1], crop[2], crop[3]));
            cut.Freeze();
            RunShot part = Saved("ribbon", ribbonFile, cut, "the ribbon cut from the window picture");
            part.WindowRect = window;
            part.PartRect = ribbon;
            part.Crop = crop;
            part.SameHandle = same;
            shots.Add(part);
            return shots;
        }

        private static BitmapSource Print(IntPtr handle, int width, int height, out bool printed)
        {
            IntPtr screen = GetWindowDC(handle);
            IntPtr context = CreateCompatibleDC(screen);
            IntPtr bitmap = CreateCompatibleBitmap(screen, width, height);
            IntPtr old = SelectObject(context, bitmap);
            try
            {
                printed = PrintWindow(handle, context, RenderFullContent);
                SelectObject(context, old);
                BitmapSource picture = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                picture.Freeze();
                return picture;
            }
            finally
            {
                DeleteObject(bitmap);
                DeleteDC(context);
                ReleaseDC(handle, screen);
            }
        }

        private static RunShot Saved(string name, string file, BitmapSource bitmap, string detail)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(file))
            {
                encoder.Save(stream);
            }
            var bgra = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
            bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
            ShotPixels seen = ShotPixels.Of(pixels);
            return new RunShot
            {
                Name = name,
                File = file,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight,
                Colours = seen.Colours,
                BlackShare = seen.BlackShare,
                Blank = seen.Blank,
                Detail = detail,
            };
        }

        private static string Inner(Exception failure)
        {
            Exception inner = failure is TargetInvocationException && failure.InnerException != null ? failure.InnerException : failure;
            return inner.GetType().Name + " " + inner.Message;
        }
    }
}
