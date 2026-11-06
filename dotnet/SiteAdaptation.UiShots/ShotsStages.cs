using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.AddIn.ScanResults;

namespace SiteAdaptation.UiShots
{
    // R680. The pane in 3 stages with the sample data and made up data check rows, no real value, one shot per card
    public static partial class Shots
    {
        // The rows of a data check as the worker words them, made up
        private static readonly DataCheckRow[] SampleRows =
        {
            new DataCheckRow { Need = "plotOutlines", Name = "Plot outlines", Mark = "strong", Line = "The plots of the sample drawing." },
            new DataCheckRow { Need = "villaType", Name = "Villa type", Mark = "weak", Line = "Two sources differ on some plots." },
            new DataCheckRow { Need = "rules", Name = "Rules", Mark = "missing", Line = "No rules file in the folder." },
        };

        // The stages page laid out at the start width of the pane, so a test reads its parts. withRows puts the made up rows into card 1
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
                foreach (DataCheckRow row in SampleRows)
                {
                    stages.CheckRows.Add(row);
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
