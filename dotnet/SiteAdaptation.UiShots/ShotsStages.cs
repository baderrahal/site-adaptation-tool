using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ConfirmProfile;
using SiteAdaptation.AddIn.ScanResults;

namespace SiteAdaptation.UiShots
{
    // R680. The pane in 3 stages with the sample data and made up data check rows, no real value, one shot per card
    public static partial class Shots
    {
        // The needs of a data check as the worker writes them, made up
        private const string SampleCheck = "{\"needs\":[" +
            "{\"need\":\"plotOutlines\",\"mark\":\"strong\",\"line\":\"The plots of the sample drawing.\"}," +
            "{\"need\":\"villaType\",\"mark\":\"weak\",\"line\":\"Two sources differ on some plots.\"}," +
            "{\"need\":\"rules\",\"mark\":\"missing\",\"line\":\"No rules file in the folder.\"}]}";

        // R689. Card 3 after a made up build: 3 issues, one of them about rules, and the source mix of 40 made up values
        private static StageResults SampleResults()
        {
            var results = new StageResults { HasPlan = true, Mix = new SourceMix { ComputedCount = 20, RuleCount = 10, PersonCount = 4, MissingCount = 6 } };
            results.Issues.Add("1: Made up fault");
            results.Issues.Add("2 of 15 rules are not confirmed");
            results.Issues.Add("Walls: made up message");
            return results;
        }

        // The stages page laid out at the start width of the pane, so a test reads its parts. withRows gives card 1 a made up read in a
        // folder of the temp folder, read as the pane reads the work folder of a model, so the shot shows what a person sees after a read
        public static StagesPage LaidOutStages(bool withRows)
        {
            var model = new PaneViewModel(new SampleProbe()) { ReadText = Sample.ConfigText };
            model.Load(Sample.Document());
            model.LoadSettings(Sample.Settings());
            model.SetNbhCodes(new[] { Sample.Nbh });
            model.SetSteps(Sample.Steps());
            var stages = new StagesViewModel(model);
            if (withRows)
            {
                string work = Path.Combine(Path.GetTempPath(), "SiteAdaptation.UiShots." + Guid.NewGuid().ToString("N"));
                string read = Path.Combine(work, "read-sample");
                Directory.CreateDirectory(read);
                try
                {
                    File.WriteAllText(Path.Combine(read, DataCheckRows.FileName), SampleCheck);
                    File.WriteAllText(Path.Combine(read, ConfirmProfileStep.ProfileFile), "{}");
                    stages.WorkFolderNow = () => work;
                    stages.ResultsNow = SampleResults;
                    stages.Refresh();
                }
                finally
                {
                    Directory.Delete(work, true);
                }
            }
            var page = new StagesPage(stages, new SitePanePage(model));
            double width = (double)page.FindResource("SA.Size.Pane");
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

        private static BitmapSource StageCardShot(int card)
        {
            StagesPage page = LaidOutStages(true);
            var box = (FrameworkElement)page.FindName("Card" + card + "Box");
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(box.ActualWidth), (int)Math.Ceiling(box.ActualHeight), Dpi, Dpi, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawRectangle(new VisualBrush(box), null, new Rect(0, 0, box.ActualWidth, box.ActualHeight));
            }
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
