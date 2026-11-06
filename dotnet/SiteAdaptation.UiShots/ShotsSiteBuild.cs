using System;
using System.IO;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn.Pane;

namespace SiteAdaptation.UiShots
{
    // R687. The 2D preview of a plan over made up plot outlines in a folder of the temp folder, a small block of 6 plots and one apart, no real value. A draft for Bader
    public static partial class Shots
    {
        private static string SamplePreview()
        {
            var plots = new System.Text.StringBuilder("{\"plots\":[");
            for (int at = 0; at < 6; at++)
            {
                double east = 12 * at;
                plots.Append(at == 0 ? "" : ",").Append("{\"number\":\"" + (at + 1) + "\",\"outline\":[[" + east + ",0],[" + (east + 12) + ",0],[" + (east + 12) + ",30],[" + east + ",30],[" + east + ",0]]}");
            }
            plots.Append(",{\"number\":\"7\",\"outline\":[[20,45],[44,45],[44,60],[20,60],[20,45]]}],\"issues\":2}");
            return plots.ToString();
        }

        // Laid out over the made up plan, so a test reads its parts. The folder goes once the window is laid out
        public static SiteBuildPreviewWindow LaidOutPreview()
        {
            string folder = Path.Combine(Path.GetTempPath(), "SiteAdaptation.UiShots." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, SiteBuildResult.PreviewName), SamplePreview());
                var plan = new SiteBuildResult { Folder = folder, PlotsWithOutline = 7 };
                plan.Issues.Add("Made up issue one");
                plan.Issues.Add("Made up issue two");
                var window = new SiteBuildPreviewWindow(plan);
                LayOut(window);
                return window;
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static BitmapSource BuildPreviewPage()
        {
            return Draw(Body(LaidOutPreview()));
        }
    }
}
