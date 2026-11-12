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
        public static StagesPage LaidOutStages(bool withRows, Action<StagesViewModel> before = null)
        {
            return LaidOutStages(withRows, before, false);
        }

        // R845. advancedOpen opens the part Advanced under the cards before the page is laid out, closed as a person first sees it otherwise
        private static StagesPage LaidOutStages(bool withRows, Action<StagesViewModel> before, bool advancedOpen)
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
            before?.Invoke(stages);
            var page = new StagesPage(stages, new SitePanePage(model));
            if (advancedOpen)
            {
                ((System.Windows.Controls.Expander)page.FindName("AdvancedBox")).IsExpanded = true;
                // A Frame shows its page once it navigates, which the dispatcher runs
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
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

        // R760, F12. Card 2 after a made up Build stopped at a step, the box with the step and the reason
        public const string SampleStop = "Build stopped at 4 Check: Made up reason the step gave.";

        private static BitmapSource StoppedCardShot()
        {
            return StageCardShot(2, stages => stages.ShowStop(Common.Flow.RunEnd.Stopped, SampleStop));
        }

        // R845. The whole pane with Advanced open: the 3 cards, and under them the old pane with no step row
        private static BitmapSource AdvancedOpenShot()
        {
            StagesPage page = LaidOutStages(true, null, true);
            var bitmap = new RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(page);
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource StageCardShot(int card, Action<StagesViewModel> before = null)
        {
            return BoxShot(LaidOutStages(true, before), "Card" + card + "Box");
        }

        // R763. One named part of the stages page, as the gear of the head
        private static BitmapSource BoxShot(StagesPage page, string name)
        {
            var box = (FrameworkElement)page.FindName(name);
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
