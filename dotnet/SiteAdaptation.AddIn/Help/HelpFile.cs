using System;
using System.IO;
using System.Windows;
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
            if (shown?.CompositionTarget != null)
            {
                at = shown.CompositionTarget.TransformFromDevice.Transform(at);
            }
            window.Left = Math.Max(SystemParameters.VirtualScreenLeft, at.X - window.Width - SystemParameters.SmallIconWidth);
            window.Top = Math.Max(SystemParameters.VirtualScreenTop, at.Y - SystemParameters.SmallIconHeight);
            window.Closed += (sender, args) =>
            {
                if (ReferenceEquals(open, sender))
                {
                    open = null;
                }
            };
            open = window;
            window.Show();
        }
    }
}
