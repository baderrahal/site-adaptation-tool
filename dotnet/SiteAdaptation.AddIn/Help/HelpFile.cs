using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using SiteAdaptation.Common.Help;

namespace SiteAdaptation.AddIn.Help
{
    // R540. The help file travels inside the add-in DLL, so an install can not leave it behind. Read once a session
    public static class HelpFile
    {
        private const string Resource = "SiteAdaptation.AddIn.Help.help.json";
        private static readonly Lazy<HelpBook> Held = new Lazy<HelpBook>(Read);

        public static HelpBook Book => Held.Value;

        private static HelpBook Read()
        {
            using (Stream stream = typeof(HelpFile).Assembly.GetManifestResourceStream(Resource))
            using (var reader = new StreamReader(stream ?? throw new FileNotFoundException("the add-in holds no help file", Resource)))
            {
                return HelpBook.Parse(reader.ReadToEnd());
            }
        }
    }

    // R540. One box at a time, beside the i that opened it. It does not hold Revit or New project, so the user can read and work
    public static class HelpBox
    {
        private static HelpBoxWindow open;

        public static void Show(string key, FrameworkElement source)
        {
            HelpEntry entry = HelpFile.Book.Find(key);
            if (entry == null)
            {
                return;
            }
            open?.Close();
            var window = new HelpBoxWindow(entry);
            Window host = Window.GetWindow(source);
            if (host != null && host.IsVisible)
            {
                window.Owner = host;
            }
            Point at = source.PointToScreen(new Point(0, 0));
            PresentationSource shown = PresentationSource.FromVisual(source);
            Visual root = shown?.RootVisual;
            Point hostFrom = root == null ? at : root.PointToScreen(new Point(0, 0));
            double hostWide = root is FrameworkElement rootElement ? rootElement.ActualWidth : 0;
            // R558. The work area of the screen that holds the i, in pixels as Screen gives it, turned to the units of the window
            System.Drawing.Rectangle screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)at.X, (int)at.Y)).WorkingArea;
            Point workFrom = new Point(screen.Left, screen.Top), workTo = new Point(screen.Right, screen.Bottom);
            if (shown?.CompositionTarget != null)
            {
                Matrix fromDevice = shown.CompositionTarget.TransformFromDevice;
                at = fromDevice.Transform(at);
                hostFrom = fromDevice.Transform(hostFrom);
                workFrom = fromDevice.Transform(workFrom);
                workTo = fromDevice.Transform(workTo);
            }
            Func<double, HelpBoxSpot> place = height => HelpPlace.Of(hostFrom.X, hostFrom.X + hostWide, at.Y - SystemParameters.SmallIconHeight, window.Width, height,
                workFrom.X, workFrom.Y, workTo.X, workTo.Y, SystemParameters.SmallIconWidth);
            HelpBoxSpot before = place(0);
            window.MaxHeight = Math.Min(window.MaxHeight, before.MaxHeight);
            window.Left = before.Left;
            window.Top = before.Top;
            window.Closed += (sender, args) =>
            {
                if (ReferenceEquals(open, sender))
                {
                    open = null;
                }
            };
            open = window;
            window.Show();
            // The height is known only once the box is shown, its text fills it
            window.Top = place(window.ActualHeight).Top;
        }
    }
}
