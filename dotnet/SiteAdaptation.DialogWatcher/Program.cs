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
    // R108. Watches the windows of one Revit process while it starts, from outside Revit. It clicks Do Not Load on the
    // security dialog of every add-in that is not one of this tool, Load Once on the box of one of the three installed DLLs
    // of this tool, and stops Revit on any dialog it cannot answer that way. It never clicks anything else
    public static class Program
    {
        private const int Answered = 0;
        private const int StoppedRevit = 3;
        private const int BadArguments = 2;

        private sealed class Options
        {
            public int Pid;
            public string PidFile;
            public string Out;
            public string StopLog;
            public double Minutes = 10;
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
            if (options.PidFile != null)
            {
                // Lane B 5b. The first report tells the run script the watcher is up, and only then does Revit start
                report.Result = WatcherStart.WaitingResult;
                JsonOut.Write(options.Out, report);
                int? pid = WatcherStart.WaitForPid(() => File.Exists(options.PidFile) ? File.ReadAllText(options.PidFile) : null, () => DateTime.UtcNow,
                    report.StartedUtc.AddMinutes(options.Minutes), () => Thread.Sleep(200));
                if (pid == null)
                {
                    report.Result = "no pid of Revit in " + options.PidFile + " within " + options.Minutes + " minutes";
                    report.FinishedUtc = DateTime.UtcNow;
                    JsonOut.Write(options.Out, report);
                    return Answered;
                }
                options.Pid = pid.Value;
                report.Pid = pid.Value;
                report.Result = null;
            }
            int code = Watch(options, report);
            report.FinishedUtc = DateTime.UtcNow;
            JsonOut.Write(options.Out, report);
            return code;
        }

        private static int Watch(Options options, WatchReport report)
        {
            DateTime deadline = DateTime.UtcNow.AddMinutes(options.Minutes);
            var handled = new HashSet<string>(StringComparer.Ordinal);
            var seenOnce = new Dictionary<string, string>(StringComparer.Ordinal);
            var windows = new HashSet<string>();
            while (true)
            {
                Process revit = Alive(options.Pid);
                if (revit == null)
                {
                    report.Result = "Revit exited";
                    return Answered;
                }
                if (TestHostStarted(options.StopLog, report.StartedUtc))
                {
                    report.Result = "the test host wrote its first log step";
                    return Answered;
                }
                if (DateTime.UtcNow > deadline)
                {
                    report.Result = "watched " + options.Minutes + " minutes";
                    return Answered;
                }
                AutomationElementCollection tops;
                try
                {
                    tops = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, options.Pid));
                }
                catch (Exception failure)
                {
                    report.Notes.Add("windows not read: " + failure.Message);
                    Thread.Sleep(500);
                    continue;
                }
                // Revit shows the security box as a child window of its main window, so the windows one level down are looked at too
                foreach (AutomationElement window in Candidates(tops))
                {
                    string key;
                    string title;
                    string windowClass;
                    List<string> texts;
                    List<string> buttons;
                    try
                    {
                        key = Key(window);
                        title = window.Current.Name;
                        windowClass = window.Current.ClassName;
                        if (handled.Contains(key))
                        {
                            continue;
                        }
                        if (!SecurityDialogs.IsDialog(windowClass, title))
                        {
                            if (windows.Add(windowClass + " | " + title))
                            {
                                report.Windows.Add(new WindowSeen { Utc = DateTime.UtcNow, WindowClass = windowClass, Title = title });
                            }
                            continue;
                        }
                        ReadParts(window, out texts, out buttons);
                    }
                    catch (Exception)
                    {
                        // The window closed while it was read
                        continue;
                    }
                    string handle = key;
                    // A dialog is answered when it reads the same twice, so a box still filling its text is not judged half drawn
                    string picture = title + "\n" + string.Join("\n", texts) + "\n" + string.Join("\n", buttons);
                    string before;
                    if (!seenOnce.TryGetValue(handle, out before) || before != picture)
                    {
                        seenOnce[handle] = picture;
                        continue;
                    }
                    SecurityDialogEntry entry = SecurityDialogs.Read(title, windowClass, texts, buttons);
                    entry.Pid = options.Pid;
                    WatchAction action = SecurityDialogs.Decide(entry, options.OurIds, options.OurMarks, options.OurPaths);
                    entry.Action = action.ToString();
                    handled.Add(handle);
                    // A progress window is logged once and left, the freeze guard of R108 still ends a window that hangs
                    if (action == WatchAction.WaitProgress)
                    {
                        entry.Text = null;
                        report.Dialogs.Add(entry);
                        JsonOut.Write(options.Out, report);
                        continue;
                    }
                    string label = SecurityDialogs.ButtonFor(action);
                    if (label != null && Click(window, entry, label))
                    {
                        entry.Text = null;
                        report.Dialogs.Add(entry);
                        JsonOut.Write(options.Out, report);
                        continue;
                    }
                    report.Dialogs.Add(entry);
                    report.Result = "stopped Revit: " + entry.Action + " on " + title;
                    Stop(revit, report);
                    return StoppedRevit;
                }
                Thread.Sleep(500);
            }
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
                report.Notes.Add("Revit could not be stopped: " + failure.Message);
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
                    case "--pid-file":
                        options.PidFile = value;
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
            if ((options.Pid <= 0) == (options.PidFile == null) || string.IsNullOrEmpty(options.Out) || options.OurIds.Count == 0)
            {
                throw new ArgumentException("--pid or --pid-file, one of them, --out and --ours are needed");
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
    }
}
