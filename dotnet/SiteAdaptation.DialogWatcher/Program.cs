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
    // R106. Watches the windows of one Revit process while it starts, from outside Revit. It clicks Do Not Load on the
    // security dialog of every add-in that is not one of this tool and stops Revit on any dialog it cannot answer that way.
    // It never clicks anything else
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
            public List<string> OurIds = new List<string>();
            public List<string> OurMarks = new List<string>();
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
            report.FinishedUtc = DateTime.UtcNow;
            JsonOut.Write(options.Out, report);
            return code;
        }

        private static int Watch(Options options, WatchReport report)
        {
            DateTime deadline = DateTime.UtcNow.AddMinutes(options.Minutes);
            var handled = new HashSet<int>();
            var seenOnce = new Dictionary<int, string>();
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
                foreach (AutomationElement window in tops)
                {
                    int handle;
                    string title;
                    string windowClass;
                    List<string> texts;
                    List<string> buttons;
                    try
                    {
                        handle = window.Current.NativeWindowHandle;
                        title = window.Current.Name;
                        windowClass = window.Current.ClassName;
                        if (handled.Contains(handle))
                        {
                            continue;
                        }
                        ReadParts(window, out texts, out buttons);
                    }
                    catch (Exception)
                    {
                        // The window closed while it was read
                        continue;
                    }
                    if (!SecurityDialogs.IsDialog(windowClass, title, buttons))
                    {
                        if (windows.Add(windowClass + " | " + title))
                        {
                            report.Windows.Add(new WindowSeen { Utc = DateTime.UtcNow, WindowClass = windowClass, Title = title });
                        }
                        continue;
                    }
                    // A dialog is answered when it reads the same twice, so a box still filling its text is not judged half drawn
                    string picture = title + "\n" + string.Join("\n", texts) + "\n" + string.Join("\n", buttons);
                    string before;
                    if (!seenOnce.TryGetValue(handle, out before) || before != picture)
                    {
                        seenOnce[handle] = picture;
                        continue;
                    }
                    SecurityDialogEntry entry = SecurityDialogs.Read(title, windowClass, texts, buttons);
                    WatchAction action = SecurityDialogs.Decide(entry, options.OurIds, options.OurMarks);
                    entry.Action = action.ToString();
                    handled.Add(handle);
                    if (action == WatchAction.DoNotLoad && Click(window, entry))
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
                if (part.Current.ControlType == ControlType.Button)
                {
                    buttons.Add(name.Trim());
                }
                else
                {
                    texts.Add(name.Trim());
                }
            }
        }

        // Do Not Load and nothing else. False when the button is not there or does not take the click
        private static bool Click(AutomationElement window, SecurityDialogEntry entry)
        {
            foreach (AutomationElement part in window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
            {
                string name = part.Current.Name;
                if (!SecurityDialogs.IsDoNotLoad(name))
                {
                    continue;
                }
                object pattern;
                if (!part.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                {
                    entry.Action = "StopNoButton, Do Not Load takes no click";
                    return false;
                }
                ((InvokePattern)pattern).Invoke();
                entry.Button = SecurityDialogs.DoNotLoad;
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
    }
}
