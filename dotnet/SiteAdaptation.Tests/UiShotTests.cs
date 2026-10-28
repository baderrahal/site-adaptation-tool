using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteAdaptation.UiShots;
using Xunit;
using Xunit.Abstractions;

namespace SiteAdaptation.Tests
{
    // R372, control 3 of docs/design/12-ui-spec.md. Each window renders offscreen with sample data at 100 percent scale through the
    // one entry the command line uses, and a window Bader approved holds to its baseline PNG within 0.5 percent of its pixels.
    // WPF, so net48 only
    public sealed class UiShotTests
    {
        public const double MostDiffering = 0.005;
        private const int RecordMax = 100 * 1024;
        private readonly ITestOutputHelper output;

        public UiShotTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void R372_each_window_renders_at_96_dpi_the_pane_380_wide_and_each_png_within_the_100_kb_of_r400()
        {
            var sizes = new List<string>();
            OnSta(() =>
            {
                foreach (string window in Shots.Windows)
                {
                    BitmapSource bitmap = Shots.Render(window);
                    byte[] png = Shots.Png(bitmap);
                    sizes.Add(window + " " + bitmap.PixelWidth + "x" + bitmap.PixelHeight + " " + png.Length + " bytes");
                    Assert.Equal(96.0, bitmap.DpiX, 6);
                    Assert.True(png.Length <= RecordMax, sizes.Last());
                    Assert.True(bitmap.PixelHeight > 64, sizes.Last());
                    if (window != Shots.RibbonIcons)
                    {
                        Assert.Equal(380, bitmap.PixelWidth);
                        Assert.True(bitmap.PixelHeight > 1000, sizes.Last());
                    }
                }
                Assert.Throws<ArgumentException>(() => Shots.Render("set-up-site"));
            });
            sizes.ForEach(output.WriteLine);
            Assert.Equal(3, sizes.Count);
        }

        [Fact]
        public void R372_the_drafts_in_steps_ui_shots_are_there_each_within_100_kb()
        {
            string folder = Path.Combine(ThemeGateTests.RepoRoot(), "steps", "ui-shots");
            foreach (string window in Shots.Windows)
            {
                var file = new FileInfo(Path.Combine(folder, window + ".png"));
                Assert.True(file.Exists, file.FullName);
                Assert.True(file.Length <= RecordMax, file.FullName + " " + file.Length);
            }
            Assert.True(File.Exists(Path.Combine(folder, "README.md")));
            Assert.True(Directory.Exists(Path.Combine(folder, "baseline")));
        }

        // Empty until Bader approves a draft, then the window must render within 0.5 percent of its pixels of the baseline
        [Fact]
        public void R372_each_window_with_an_approved_baseline_renders_within_half_a_percent_of_its_pixels()
        {
            string baseline = Path.Combine(ThemeGateTests.RepoRoot(), "steps", "ui-shots", "baseline");
            var compared = new List<string>();
            OnSta(() =>
            {
                foreach (string window in Shots.Windows)
                {
                    string path = Path.Combine(baseline, window + ".png");
                    if (!File.Exists(path))
                    {
                        continue;
                    }
                    double share = Shots.Differ(Shots.Load(path), Shots.Load(Png(window)));
                    compared.Add(window + " differs on " + (share * 100).ToString("0.000") + " percent of its pixels");
                    Assert.True(share <= MostDiffering, compared.Last());
                }
            });
            if (compared.Count == 0)
            {
                output.WriteLine("No baseline yet in " + baseline + ", nothing to compare until Bader approves a draft");
            }
            compared.ForEach(output.WriteLine);
            Assert.Empty(Directory.EnumerateFiles(baseline, "*.png").Select(Path.GetFileNameWithoutExtension).Except(Shots.Windows));
        }

        [Fact]
        public void R372_the_compare_counts_the_pixels_that_differ_and_takes_a_size_change_as_all_of_them()
        {
            OnSta(() =>
            {
                BitmapSource ground = Plain(100, 100, 0);
                Assert.Equal(0.0, Shots.Differ(ground, Plain(100, 100, 0)));
                Assert.Equal(0.004, Shots.Differ(ground, Plain(100, 100, 40)), 6);
                Assert.Equal(0.006, Shots.Differ(ground, Plain(100, 100, 60)), 6);
                Assert.True(Shots.Differ(ground, Plain(100, 100, 40)) <= MostDiffering);
                Assert.False(Shots.Differ(ground, Plain(100, 100, 60)) <= MostDiffering);
                Assert.Equal(1.0, Shots.Differ(ground, Plain(100, 101, 0)));

                BitmapSource pane = Shots.Render(Shots.Pane);
                BitmapSource again = Shots.Load(Png(Shots.Pane));
                Assert.Equal(0.0, Shots.Differ(Shots.Load(Png(Shots.Pane)), again));
                Assert.True(Shots.Differ(again, Shots.Load(Png(Shots.PaneFirstOpen))) > MostDiffering);
                Assert.Equal(pane.PixelWidth, again.PixelWidth);
            });
        }

        private static string Png(string window)
        {
            string path = Path.Combine(Path.GetTempPath(), "ui-shot-" + window + "-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, Shots.Png(Shots.Render(window)));
            return path;
        }

        // A white bitmap with its first count pixels black
        private static BitmapSource Plain(int width, int height, int count)
        {
            byte[] pixels = Enumerable.Repeat((byte)255, width * height * 4).ToArray();
            for (int at = 0; at < count; at++)
            {
                pixels[at * 4] = 0;
                pixels[at * 4 + 1] = 0;
                pixels[at * 4 + 2] = 0;
            }
            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception caught)
                {
                    failure = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException("on the STA thread: " + failure);
            }
        }
    }
}
