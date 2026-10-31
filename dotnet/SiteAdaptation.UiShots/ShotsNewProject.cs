using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn.HowItWorks;
using SiteAdaptation.AddIn.NewProject;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.UiShots
{
    // Lane C NEWPROJ-UI. New project and How it works rendered as the other windows, with sample data and no real path, coordinate or level:
    // the site drawing and the plot list found, road levels and the parameter sheet missing as on the board, the counts of the read block
    // sample values, steps/ui-shots/README.md
    public static partial class Shots
    {
        private static BitmapSource NewProjectPage()
        {
            return Draw(Body(LaidOutNewProject()));
        }

        private static BitmapSource HowItWorksPage()
        {
            return Draw(Body(LaidOutHowItWorks()));
        }

        private static BitmapSource Draw(FrameworkElement body)
        {
            var bitmap = new RenderTargetBitmap((int)body.ActualWidth, (int)body.ActualHeight, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(body);
            bitmap.Freeze();
            return bitmap;
        }

        public static NewProjectWindow LaidOutNewProject()
        {
            var window = new NewProjectWindow(SampleNewProject());
            LayOut(window);
            return window;
        }

        public static HowItWorksWindow LaidOutHowItWorks()
        {
            var window = new HowItWorksWindow(new HowItWorksViewModel());
            LayOut(window);
            return window;
        }

        public static NewProjectViewModel SampleNewProject()
        {
            ProjectSettings settings = Sample.Settings().Project;
            settings.Inputs.Remove(InputKeys.RoadSurface);
            settings.Inputs.Remove(InputKeys.ParameterSheet);
            var starts = new[] { new SavedStart(Sample.SavedProject, @"C:\Sample\" + Sample.SavedProject + SavedStarts.Extension, PreSetting.Export(settings)) };
            var facts = new DrawingFactsResult { Plots = 337, PlotLayer = "MP_PLOT_LINE, MP_PLOT_SUBD", VillaTypes = 14 };
            return new NewProjectViewModel(settings, Sample.Document().Title, starts, facts, (path, folder) => Sample.Files.ContainsKey(Path.GetFileName(path)), null, path => null);
        }
    }
}
