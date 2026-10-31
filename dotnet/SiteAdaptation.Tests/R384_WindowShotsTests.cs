using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteAdaptation.AddIn;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.SetUp;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Update;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Lane B 6a (R384). The windows a buttons job of the test host draws, each built by WindowShots of the add-in from the view its button
    // gave the Dialog, laid out offscreen at its design size, drawn and closed. WPF, so net48 only
    public sealed class R384_WindowShotsTests
    {
        private const int DialogWidth = 1280;
        private const int NewProjectWidth = 1080;

        [Fact]
        public void R384_a_window_shot_draws_as_the_picture_test_draws_the_same_window()
        {
            OnSta(() =>
            {
                var pairs = new List<Tuple<string, Window>>
                {
                    Tuple.Create(Shots.SetUpSite, WindowShots.SetUp(Shots.SampleSetUpView(true, true), new SetUpSiteCallbacks())),
                    Tuple.Create(Shots.Rules, WindowShots.Rules(Shots.SampleRulesTable(), "M06")),
                    Tuple.Create(Shots.HowItWorks, WindowShots.HowItWorks(true)),
                };
                foreach (Tuple<string, Window> pair in pairs)
                {
                    double differ = Shots.Differ(WindowShots.Draw(pair.Item2), Shots.Render(pair.Item1));
                    Assert.True(differ <= UiShotTests.MostDiffering, pair.Item1 + " differs on " + differ);
                }
            });
        }

        [Fact]
        public void R384_each_window_of_a_shot_is_drawn_at_its_design_width_and_is_not_blank()
        {
            string root = Folder();
            OnSta(() =>
            {
                var windows = new List<Tuple<string, Window, int>>
                {
                    Tuple.Create(ButtonShots.ReviewData, WindowShots.Review(Shots.SampleReviewView(true), new ReviewCallbacks()), DialogWidth),
                    Tuple.Create(ButtonShots.CheckTable, WindowShots.Check(Shots.SampleCheckView(false), new CheckTableCallbacks(), Shots.RulesFile()), DialogWidth),
                    Tuple.Create(ButtonShots.UpdateFromReport, WindowShots.Update(new UpdateFromReportCallbacks(), Shots.SampleUpdateList(), new[] { "012_Levels.xlsx", "012_Report.xlsx" }), DialogWidth),
                    Tuple.Create(ButtonShots.NewProject, WindowShots.NewProject(Session(), root), NewProjectWidth),
                };
                foreach (Tuple<string, Window, int> window in windows)
                {
                    BitmapSource drawn = WindowShots.Draw(window.Item2);
                    Assert.Equal(window.Item3, drawn.PixelWidth);
                    Assert.True(drawn.PixelHeight > 300 && drawn.PixelHeight <= 800, window.Item1 + " " + drawn.PixelHeight);
                    Assert.False(Seen(drawn).Blank, window.Item1);
                }
            });
        }

        // Closed with no change: a window drawn for a shot runs none of its callbacks, writes no file and is closed when drawn
        [Fact]
        public void R384_a_window_shot_runs_no_callback_writes_nothing_and_closes_the_window()
        {
            int calls = 0;
            int closed = 0;
            string root = Folder();
            OnSta(() =>
            {
                var setUp = new SetUpSiteCallbacks { DryRun = slope => { calls++; return null; }, SheetChosen = path => { calls++; return null; } };
                var review = new ReviewCallbacks { Save = checks => { calls++; return "saved"; }, Export = () => { calls++; return null; } };
                var check = new CheckTableCallbacks { Save = choices => { calls++; return "saved"; }, Export = () => { calls++; return null; } };
                var update = new UpdateFromReportCallbacks { Read = files => { calls++; return null; }, Preview = changes => { calls++; return null; } };
                var windows = new List<Window>
                {
                    WindowShots.SetUp(Shots.SampleSetUpView(false, true), setUp),
                    WindowShots.Review(Shots.SampleReviewView(true), review),
                    WindowShots.Check(Shots.SampleCheckView(false), check, Shots.RulesFile()),
                    WindowShots.Update(update, Shots.SampleUpdateList(), new[] { "012_Levels.xlsx", "012_Report.xlsx" }),
                    WindowShots.Rules(Shots.SampleRulesTable(), "M06"),
                    WindowShots.NewProject(Session(), root),
                    WindowShots.HowItWorks(true),
                };
                foreach (Window window in windows)
                {
                    window.Closed += (sender, args) => closed++;
                    WindowShots.Draw(window);
                }
            });
            Assert.Equal(0, calls);
            Assert.Equal(7, closed);
            Assert.Empty(Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories));
        }

        // The names of the shots are the names of the picture test, so a shot of a run and its baseline pair up by name
        [Fact]
        public void R384_the_shot_names_are_the_window_names_of_the_picture_test()
        {
            Assert.Equal(Shots.SetUpSite, ButtonShots.SetUpSite);
            Assert.Equal(Shots.ReviewData, ButtonShots.ReviewData);
            Assert.Equal(Shots.CheckTable, ButtonShots.CheckTable);
            Assert.Equal(Shots.Rules, ButtonShots.Rules);
            Assert.Equal(Shots.UpdateFromReport, ButtonShots.UpdateFromReport);
            Assert.Equal(Shots.NewProject, ButtonShots.NewProject);
            Assert.Equal(Shots.HowItWorks, ButtonShots.HowItWorks);
            Assert.Equal(Shots.Pane, ButtonShots.Pane);
        }

        private static DocumentSession Session()
        {
            return new DocumentSession(Guid.NewGuid().ToString("D"), @"C:\Sample\012-TEST-NEW.rvt", "012-TEST-NEW") { Settings = new PaneSettings(new ProjectSettings()) };
        }

        private static string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "r384-shots-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static ShotPixels Seen(BitmapSource bitmap)
        {
            var bgra = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
            bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
            return ShotPixels.Of(pixels);
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
