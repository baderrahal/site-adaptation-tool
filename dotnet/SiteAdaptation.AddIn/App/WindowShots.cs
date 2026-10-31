using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn.CheckTable;
using SiteAdaptation.AddIn.HowItWorks;
using SiteAdaptation.AddIn.NewProject;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ReviewData;
using SiteAdaptation.AddIn.RulesWindow;
using SiteAdaptation.AddIn.SetUpSite;
using SiteAdaptation.AddIn.Update;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.ManualRules;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Update;
using RulesView = SiteAdaptation.AddIn.RulesWindow.RulesWindow;

namespace SiteAdaptation.AddIn
{
    // Lane B 6a (R384). The windows of the add-in as their Dialog builds them, from the view a button gives it, laid out offscreen at the
    // size of the theme and never shown, so the test host draws each one to PNG before it answers the Dialog and no window waits for a
    // click. Every pick is null here, the window is closed once drawn and its session is not the one the host answers with
    public static class WindowShots
    {
        private const double Dpi = 96.0;

        public static Window SetUp(SetUpSiteView view, SetUpSiteCallbacks callbacks)
        {
            return LaidOut(new SetUpSiteWindow(new SetUpSiteSession(view, callbacks, () => null)));
        }

        public static Window Review(ReviewView view, ReviewCallbacks callbacks)
        {
            return LaidOut(new ReviewDataWindow(new ReviewDataSession(view, callbacks)));
        }

        // A rule picked in the table opens no second window in a shot
        public static Window Check(CheckTableView view, CheckTableCallbacks callbacks, string rulesFile)
        {
            return LaidOut(new CheckTableWindow(new CheckTableSession(view, callbacks, CheckTableDialog.RuleLine(rulesFile), id => { })));
        }

        public static Window Rules(ManualRuleTable table, string selectedId)
        {
            return LaidOut(new RulesView(new RulesWindowSession(table, selectedId, null, null)));
        }

        // The window after the two Excel files were read, the list of the read in it, as a pick of both would leave it
        public static Window Update(UpdateFromReportCallbacks callbacks, UpdateList list, IReadOnlyList<string> files)
        {
            var session = new UpdateFromReportSession(callbacks, () => null);
            session.Model.Load(list, files);
            return LaidOut(new UpdateFromReportWindow(session));
        }

        // As the pane opens it on its document: the settings of the document, the saved starts of the work root and the facts of the work
        // folder of the document, read by NewProjectDialog.Facts
        public static Window NewProject(DocumentSession document, string root)
        {
            ProjectSettings current = document.Settings?.Project;
            string workFolder = WorkFolder.For(root, document.Path);
            IReadOnlyList<SavedStart> starts = SavedStarts.List(SavedStarts.Folder(root));
            DrawingFactsResult facts = NewProjectDialog.Facts.Read(current?.ConfigFile, workFolder);
            var model = new NewProjectViewModel(current, document.Title, starts, facts, NewProjectDialog.There, new DialogPicker(), WorkFolder.ReadIfThere);
            return LaidOut(new NewProjectWindow(model));
        }

        // canOpenNew as the pane gives it, true while a document is open
        public static Window HowItWorks(bool canOpenNew)
        {
            return LaidOut(new HowItWorksWindow(new HowItWorksViewModel(canOpenNew)));
        }

        // The laid out content of the window drawn at 96 DPI, then the window closed, so it never shows and changes nothing
        public static BitmapSource Draw(Window window)
        {
            try
            {
                var body = (FrameworkElement)window.Content;
                var bitmap = new RenderTargetBitmap((int)body.ActualWidth, (int)body.ActualHeight, Dpi, Dpi, PixelFormats.Pbgra32);
                bitmap.Render(body);
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                window.Close();
            }
        }

        // A window that is not shown builds no visual tree of its own, so its content is measured at the width the window takes from the
        // theme, the dialog width when it names none, and the height it asks for, capped by the MaxHeight of the theme, and arranged there.
        // Its data context comes down the logical tree. The picture test of R372 lays its windows out here too
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

        private static Window LaidOut(Window window)
        {
            LayOut(window);
            return window;
        }
    }
}
