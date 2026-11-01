using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.AddIn.Loader
{
    // R444, Lane C DEMO-FIX. The loader of every button, started by Buttons.Run for the ribbon and the pane alike. The step runs on the
    // thread of Revit and keeps it busy, so the loader has a thread and a dispatcher of its own. LoaderRule decides when it stands. A
    // run that goes on outside Revit (R375) keeps it until its end comes back or the user hides it. No step stops in the middle of
    // its work (R382 asks Cancel before the work only), so the loader has no Cancel
    public sealed class RunLoader : IDisposable
    {
        private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(250);

        private readonly object gate = new object();
        private readonly string step;
        private readonly IntPtr main;
        private Dispatcher dispatcher;
        private bool closed;
        private bool outside;

        private RunLoader(string step, IntPtr main)
        {
            this.step = step;
            this.main = main;
        }

        // True while its thread runs, for the tests
        public bool Running { get; private set; }

        public static RunLoader Start(string step, IntPtr main)
        {
            var loader = new RunLoader(step, main);
            var thread = new Thread(loader.Run) { IsBackground = true, Name = "SiteAdaptation.Loader" };
            thread.SetApartmentState(ApartmentState.STA);
            loader.Running = true;
            thread.Start();
            return loader;
        }

        // The run goes on outside Revit, its end closes the loader through Dispose
        public void Outside()
        {
            Dispatcher on;
            lock (gate)
            {
                if (closed)
                {
                    return;
                }
                outside = true;
                on = dispatcher;
            }
            on?.BeginInvoke(new Action(() => window?.Outside()));
        }

        public void Dispose()
        {
            Dispatcher on;
            lock (gate)
            {
                closed = true;
                on = dispatcher;
            }
            on?.BeginInvokeShutdown(DispatcherPriority.Normal);
        }

        private LoaderWindow window;

        private void Run()
        {
            try
            {
                lock (gate)
                {
                    if (closed)
                    {
                        return;
                    }
                    dispatcher = Dispatcher.CurrentDispatcher;
                }
                // Reviewer of DEMO-FIX: an exception left on this thread would end Revit, and the loader only shows, so it ends quietly
                dispatcher.UnhandledException += (sender, e) => e.Handled = true;
                Stopwatch clock = Stopwatch.StartNew();
                window = new LoaderWindow(step);
                if (outside)
                {
                    window.Outside();
                }
                window.HideRequested += Dispose;
                IntPtr own = new WindowInteropHelper(window).EnsureHandle();
                var timer = new DispatcherTimer(TickEvery, DispatcherPriority.Normal, (sender, e) => Tick(clock.Elapsed, own), dispatcher);
                timer.Start();
                Dispatcher.Run();
                timer.Stop();
                window.Close();
            }
            catch (Exception)
            {
                window = null;
            }
            finally
            {
                Running = false;
            }
        }

        private void Tick(TimeSpan elapsed, IntPtr own)
        {
            try
            {
                Show(elapsed, own);
            }
            catch (Exception)
            {
                window?.Hide();
            }
        }

        private void Show(TimeSpan elapsed, IntPtr own)
        {
            IntPtr front = GetForegroundWindow();
            bool shown = LoaderRule.Shown(elapsed, LoaderRule.MainInFront(front, main, own, ClassOf(front)));
            window.Tick(elapsed);
            if (shown && !window.IsVisible)
            {
                // The rectangle of Windows is in pixels and the place of WPF in its own units, so the scale of the screen is taken out
                HwndSource source = HwndSource.FromHwnd(own);
                if (source?.CompositionTarget != null && GetWindowRect(main, out Rect area))
                {
                    Point middle = source.CompositionTarget.TransformFromDevice.Transform(new Point((area.Left + area.Right) / 2.0, (area.Top + area.Bottom) / 2.0));
                    window.Left = middle.X - window.Width / 2.0;
                    window.Top = middle.Y - window.ActualHeight / 2.0;
                }
                window.Show();
            }
            else if (!shown && window.IsVisible)
            {
                window.Hide();
            }
        }

        private static string ClassOf(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return null;
            }
            var name = new StringBuilder(64);
            return GetClassName(handle, name, name.Capacity) > 0 ? name.ToString() : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr handle, StringBuilder name, int capacity);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr handle, out Rect area);
    }
}
