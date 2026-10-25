using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace SiteAdaptation.DialogWatcher
{
    // R108. Watches the windows of one Revit process from outside Revit. While Revit starts it clicks Do Not Load on the
    // security dialog of every add-in that is not one of this tool, Load Once on the box of one of the three installed DLLs
    // of this tool, and stops Revit on any dialog it cannot answer that way. R303: with --watch-job it then logs every window
    // of that Revit to its end and stops nothing. It never clicks anything else
    public static class Program
    {
        private const int Answered = 0;
        private const int StoppedRevit = 3;
        private const int BadArguments = 2;

        private sealed class Options
        {
            public int Pid;
            public string Out;
            public string StopLog;
            public double Minutes = 10;
            // R303. Watch on through the job after the first log step of the test host, logging, until Revit exits or Minutes end
            public bool WatchJob;
            public List<string> OurIds = new List<string>();
            public List<string> OurMarks = new List<string>();
            public List<string> OurPaths = new List<string>();
        }

        [STAThread]
        public static int Main(string[] args)
        {
            Options options;
            try
            {
                options = Parse(args);
            }
            catch (Exception failure)
            {
                Console.Error.WriteLine("dialog watcher: " + failure.Message);
                return BadArguments;
            }
            var report = new WatchReport { Pid = options.Pid, StartedUtc = DateTime.UtcNow };
            int code = Watch(options, report);
            Record(report, options.Out, value => value.FinishedUtc = DateTime.UtcNow);
            return code;
        }

        private const int KeepWatching = -1;

        // The report is changed by the main loop and by the start phase worker, so each change and its write hold this lock.
        // No UI Automation call is ever made while it is held
        private static void Record(WatchReport report, string path, Action<WatchReport> change)
        {
            lock (report)
            {
                change(report);
                JsonOut.Write(path, report);
            }
        }

        // R108, R303. The main loop never makes a UI Automation call itself, since such a call on a busy Revit waits until Revit
        // answers. Every 0.5 s it looks at the log, the time and the process. The start phase reads the windows by UI Automation
        // on one worker task, a new one only once the last has ended. The job phase lists the windows by Win32 on this thread
        private static int Watch(Options options, WatchReport report)
        {
            DateTime deadline = DateTime.UtcNow.AddMinutes(options.Minutes);
            var phase = new JobPhase();
            var startSeen = new StartSeen();
            var jobWindows = new HashSet<string>();
            var jobHandled = new HashSet<string>(StringComparer.Ordinal);
            var jobSeenOnce = new Dictionary<string, string>(StringComparer.Ordinal);
            System.Threading.Tasks.Task<int> uia = null;
            while (true)
            {
                if (uia != null && uia.IsCompleted)
                {
                    int code = uia.IsFaulted ? KeepWatching : uia.Result;
                    uia = null;
                    if (code != KeepWatching)
                    {
                        return code;
                    }
                }
                Process revit = Alive(options.Pid);
                bool firstStep = phase.On || TestHostStarted(options.StopLog, report.StartedUtc);
                string why = SecurityDialogs.StopReason(revit != null, firstStep, !options.WatchJob, DateTime.UtcNow, deadline, options.Minutes);
                if (why != null)
                {
                    Record(report, options.Out, value => value.Result = why);
                    return Answered;
                }
                if (firstStep && !phase.On)
                {
                    phase.On = true;
                    Record(report, options.Out, value => value.Notes.Add(DateTime.UtcNow.ToString("o") + " the test host wrote its first log step, from now on every window is listed by Win32 and only rule 3 answers are clicked"));
                }
                if (phase.On)
                {
                    WatchJob(options, report, revit, jobWindows, jobHandled, jobSeenOnce);
                }
                else if (uia == null)
                {
                    Process started = revit;
                    uia = System.Threading.Tasks.Task.Run(() => StartPhase(options, report, started, startSeen, phase));
                }
                Thread.Sleep(500);
            }
        }

        // The phase the start phase worker reads before it judges a dialog, so a dialog it finds after the first step stops nothing
        private sealed class JobPhase
        {
            public volatile bool On;
        }

        // What the start phase worker has seen, its own sets, never touched by the main loop
        private sealed class StartSeen
        {
            public readonly HashSet<string> Windows = new HashSet<string>();
            public readonly HashSet<string> Handled = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> SeenOnce = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        // R108. One look at the windows of Revit by UI Automation, as the watcher always looked while Revit starts
        private static int StartPhase(Options options, WatchReport report, Process revit, StartSeen seen, JobPhase phase)
        {
            AutomationElementCollection tops;
            try
            {
                tops = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, options.Pid));
            }
            catch (Exception failure)
            {
                Record(report, options.Out, value => value.Notes.Add("windows not read: " + failure.Message));
                return KeepWatching;
            }
            // Revit shows the security box as a child window of its main window, so the windows one level down are looked at too
            foreach (AutomationElement window in Candidates(tops))
            {
                string key;
                string title;
                string windowClass;
                try
                {
                    key = Key(window);
                    title = window.Current.Name;
                    windowClass = window.Current.ClassName;
                    if (seen.Handled.Contains(key))
                    {
                        continue;
                    }
                    if (!SecurityDialogs.IsDialog(windowClass, title))
                    {
                        // Every other window of this Revit once per window and title, with its buttons and first texts.
                        // The main frame is logged by class and title only, its parts are the whole Revit user interface
                        if (seen.Windows.Add(key + " | " + title))
                        {
                            List<string> shownTexts = new List<string>();
                            List<string> shownButtons = new List<string>();
                            if (!IsMainFrame(window, revit, title))
                            {
                                ReadParts(window, out shownTexts, out shownButtons);
                            }
                            var shown = new WindowSeen { Utc = DateTime.UtcNow, WindowClass = windowClass, Title = title, Buttons = shownButtons, Texts = shownTexts.Take(MaxTexts).ToList() };
                            Record(report, options.Out, value => value.Windows.Add(shown));
                        }
                        continue;
                    }
                }
                catch (Exception)
                {
                    // The window closed while it was read
                    continue;
                }
                int code = Judge(window, key, title, windowClass, options, report, revit, seen.Handled, seen.SeenOnce, phase.On);
                if (code != KeepWatching)
                {
                    return code;
                }
            }
            return KeepWatching;
        }

        // R303. The job phase. Every visible top window of this Revit is listed by Win32 calls that never wait on a busy window and
        // logged once per handle and title with its class, its button and text children and whether Windows calls it hung. Only a
        // dialog whose window is not hung is read by UI Automation and judged as R108 judges it, and nothing stops Revit here
        private static void WatchJob(Options options, WatchReport report, Process revit, HashSet<string> windows, HashSet<string> handled, Dictionary<string, string> seenOnce)
        {
            foreach (TopWindow top in TopWindows(options.Pid))
            {
                if (windows.Add(top.Handle + " | " + top.Title))
                {
                    var shown = new WindowSeen { Utc = DateTime.UtcNow, WindowClass = top.Class, Title = top.Title, Buttons = top.Buttons, Texts = top.Texts, Hung = top.Hung };
                    Record(report, options.Out, value => value.Windows.Add(shown));
                }
                string key = "win32 " + top.Handle;
                if (handled.Contains(key) || !SecurityDialogs.ReadByAutomation(true, top.Class, top.Title, top.Hung))
                {
                    continue;
                }
                AutomationElement window;
                try
                {
                    window = AutomationElement.FromHandle(top.Handle);
                }
                catch (Exception)
                {
                    // The window closed while it was read
                    continue;
                }
                Judge(window, key, top.Title, top.Class, options, report, revit, handled, seenOnce, true);
            }
        }

        // R108 and R303. A dialog read twice the same is judged: the rule 3 button is clicked, else in the start phase Revit is
        // stopped and in the job phase the dialog is logged in full and left alone. KeepWatching, or the exit code of a stop
        private static int Judge(AutomationElement window, string key, string title, string windowClass, Options options, WatchReport report, Process revit,
            HashSet<string> handled, Dictionary<string, string> seenOnce, bool jobPhase)
        {
            List<string> texts;
            List<string> buttons;
            try
            {
                ReadParts(window, out texts, out buttons);
            }
            catch (Exception)
            {
                // The window closed while it was read
                return KeepWatching;
            }
            // A dialog is answered when it reads the same twice, so a box still filling its text is not judged half drawn
            string picture = title + "\n" + string.Join("\n", texts) + "\n" + string.Join("\n", buttons);
            string before;
            if (!seenOnce.TryGetValue(key, out before) || before != picture)
            {
                seenOnce[key] = picture;
                return KeepWatching;
            }
            SecurityDialogEntry entry = SecurityDialogs.Read(title, windowClass, texts, buttons);
            entry.Pid = options.Pid;
            WatchAction action = SecurityDialogs.Decide(entry, options.OurIds, options.OurMarks, options.OurPaths);
            entry.Action = action.ToString();
            handled.Add(key);
            string label = SecurityDialogs.ButtonFor(action);
            if (label != null && Click(window, entry, label))
            {
                entry.Text = null;
                Record(report, options.Out, value => value.Dialogs.Add(entry));
                return KeepWatching;
            }
            if (!SecurityDialogs.StopsRevit(action, jobPhase))
            {
                // R303. After the first step a dialog rule 3 has no answer for is logged in full and left alone
                entry.Action = "Logged, " + entry.Action;
                Record(report, options.Out, value => value.Dialogs.Add(entry));
                return KeepWatching;
            }
            Record(report, options.Out, value =>
            {
                value.Dialogs.Add(entry);
                value.Result = "stopped Revit: " + entry.Action + " on " + title;
            });
            Stop(revit, report);
            return StoppedRevit;
        }

        private sealed class TopWindow
        {
            public IntPtr Handle;
            public string Class;
            public string Title;
            public bool Hung;
            public List<string> Buttons = new List<string>();
            public List<string> Texts = new List<string>();
        }

        private delegate bool EnumProc(IntPtr window, IntPtr data);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc callback, IntPtr data);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr data);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int InternalGetWindowText(IntPtr window, System.Text.StringBuilder text, int size);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, System.Text.StringBuilder text, int size);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsHungAppWindow(IntPtr window);

        // InternalGetWindowText and GetClassName read what Windows holds and send no message, so a busy Revit never holds them
        private static string TextOf(IntPtr window)
        {
            var text = new System.Text.StringBuilder(512);
            InternalGetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        private static string ClassOf(IntPtr window)
        {
            var text = new System.Text.StringBuilder(256);
            GetClassName(window, text, text.Capacity);
            return text.ToString();
        }

        // The visible top windows of one process, each with the texts of its Button and Static children
        private static List<TopWindow> TopWindows(int pid)
        {
            var found = new List<TopWindow>();
            EnumWindows((window, data) =>
            {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner != (uint)pid || !IsWindowVisible(window))
                {
                    return true;
                }
                var top = new TopWindow { Handle = window, Class = ClassOf(window), Title = TextOf(window), Hung = IsHungAppWindow(window) };
                EnumChildWindows(window, (child, more) =>
                {
                    string kind = ClassOf(child);
                    string text = TextOf(child).Trim();
                    if (text.Length > 0 && (kind == "Button" || kind == "CCPushButton") && top.Buttons.Count < MaxTexts)
                    {
                        top.Buttons.Add(text);
                    }
                    else if (text.Length > 0 && kind == "Static" && top.Texts.Count < MaxTexts)
                    {
                        top.Texts.Add(text);
                    }
                    return true;
                }, IntPtr.Zero);
                found.Add(top);
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private const int MaxTexts = 10;

        // The main frame of Revit, by its native handle or its title, whose parts are the whole user interface
        private static bool IsMainFrame(AutomationElement window, Process revit, string title)
        {
            int handle = window.Current.NativeWindowHandle;
            return (title ?? "").StartsWith("Autodesk Revit", StringComparison.Ordinal) || (revit != null && handle != 0 && new IntPtr(handle) == revit.MainWindowHandle);
        }

        // The top windows of Revit and the windows directly inside them
        private static IEnumerable<AutomationElement> Candidates(AutomationElementCollection tops)
        {
            var inner = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
            foreach (AutomationElement top in tops)
            {
                yield return top;
                AutomationElementCollection children;
                try
                {
                    children = top.FindAll(TreeScope.Children, inner);
                }
                catch (Exception)
                {
                    continue;
                }
                foreach (AutomationElement child in children)
                {
                    yield return child;
                }
            }
        }

        // The runtime id names one element for as long as it lives, a WPF window may have no native handle of its own
        private static string Key(AutomationElement element)
        {
            return string.Join(".", element.GetRuntimeId());
        }

        // A button is a Button control or a CCPushButton pane, which is how the Revit task dialog shows its buttons
        private static bool IsButton(AutomationElement part)
        {
            return part.Current.ControlType == ControlType.Button || part.Current.ClassName == "CCPushButton";
        }

        private static void ReadParts(AutomationElement window, out List<string> texts, out List<string> buttons)
        {
            texts = new List<string>();
            buttons = new List<string>();
            foreach (AutomationElement part in window.FindAll(TreeScope.Descendants, Condition.TrueCondition))
            {
                string name = part.Current.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                if (IsButton(part))
                {
                    buttons.Add(name.Trim());
                }
                else
                {
                    texts.Add(name.Trim());
                }
            }
        }

        private const uint ButtonClick = 0x00F5;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr window);

        // The one button the decision names, Do Not Load or for this tool Load Once, and nothing else. By Invoke when the button offers it, else by BM_CLICK to the button window itself.
        // False when the button is not there, takes no click or the dialog is still open 5 seconds later
        private static bool Click(AutomationElement window, SecurityDialogEntry entry, string label)
        {
            foreach (AutomationElement part in window.FindAll(TreeScope.Descendants, Condition.TrueCondition))
            {
                bool wanted = label == SecurityDialogs.DoNotLoad ? SecurityDialogs.IsDoNotLoad(part.Current.Name) : SecurityDialogs.IsLoadOnce(part.Current.Name);
                if (!IsButton(part) || !wanted)
                {
                    continue;
                }
                object pattern;
                int buttonHandle = part.Current.NativeWindowHandle;
                if (part.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                {
                    ((InvokePattern)pattern).Invoke();
                    entry.Button = label + " (invoke)";
                }
                else if (buttonHandle != 0)
                {
                    PostMessage(new IntPtr(buttonHandle), ButtonClick, IntPtr.Zero, IntPtr.Zero);
                    entry.Button = label + " (click message)";
                }
                else
                {
                    entry.Action = "StopNoButton, " + label + " takes no click";
                    return false;
                }
                int dialogHandle = window.Current.NativeWindowHandle;
                for (int wait = 0; wait < 50 && dialogHandle != 0 && IsWindow(new IntPtr(dialogHandle)); wait++)
                {
                    Thread.Sleep(100);
                }
                if (dialogHandle != 0 && IsWindow(new IntPtr(dialogHandle)))
                {
                    entry.Action = "StopNoButton, the dialog stayed open after " + label;
                    return false;
                }
                return true;
            }
            entry.Action = WatchAction.StopNoButton.ToString();
            return false;
        }

        private static void Stop(Process revit, WatchReport report)
        {
            try
            {
                revit.Kill();
                revit.WaitForExit(60000);
            }
            catch (Exception failure)
            {
                lock (report)
                {
                    report.Notes.Add("Revit could not be stopped: " + failure.Message);
                }
            }
        }

        private static Process Alive(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                return process.HasExited ? null : process;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool TestHostStarted(string log, DateTime sinceUtc)
        {
            if (string.IsNullOrEmpty(log) || !File.Exists(log) || File.GetLastWriteTimeUtc(log) < sinceUtc)
            {
                return false;
            }
            try
            {
                JToken steps = JObject.Parse(File.ReadAllText(log))["steps"];
                return steps != null && steps.HasValues;
            }
            catch (Exception)
            {
                // The test host may be writing the file right now
                return false;
            }
        }

        private static Options Parse(string[] args)
        {
            var options = new Options();
            for (int at = 0; at + 1 < args.Length; at += 2)
            {
                string value = args[at + 1];
                switch (args[at])
                {
                    case "--pid":
                        options.Pid = int.Parse(value);
                        break;
                    case "--out":
                        options.Out = value;
                        break;
                    case "--stop-log":
                        options.StopLog = value;
                        break;
                    case "--minutes":
                        options.Minutes = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "--watch-job":
                        options.WatchJob = bool.Parse(value);
                        break;
                    case "--ours":
                        options.OurIds.AddRange(value.Split(',').Select(id => id.Trim().ToLowerInvariant()).Where(id => id.Length > 0));
                        break;
                    case "--our-paths":
                        options.OurPaths.AddRange(value.Split('|').Select(path => path.Trim()).Where(path => path.Length > 0));
                        break;
                    case "--marks":
                        options.OurMarks.AddRange(value.Split('|').Select(mark => mark.Trim()).Where(mark => mark.Length > 0));
                        break;
                    default:
                        throw new ArgumentException("unknown argument " + args[at]);
                }
            }
            if (options.Pid <= 0 || string.IsNullOrEmpty(options.Out) || options.OurIds.Count == 0)
            {
                throw new ArgumentException("--pid, --out and --ours are needed");
            }
            return options;
        }
    }

    public class WatchReport
    {
        public int Pid { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? FinishedUtc { get; set; }
        public string Result { get; set; }
        public List<SecurityDialogEntry> Dialogs { get; set; } = new List<SecurityDialogEntry>();
        public List<WindowSeen> Windows { get; set; } = new List<WindowSeen>();
        public List<string> Notes { get; set; } = new List<string>();
    }

    public class WindowSeen
    {
        public DateTime Utc { get; set; }
        public string WindowClass { get; set; }
        public string Title { get; set; }
        public List<string> Buttons { get; set; } = new List<string>();
        public List<string> Texts { get; set; } = new List<string>();
        // R303. Whether Windows called the window hung when the job phase listed it, null in the start phase
        public bool? Hung { get; set; }
    }
}
