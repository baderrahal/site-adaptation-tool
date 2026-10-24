using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using SiteAdaptation.C3DProbe;
using SiteAdaptation.Common;

namespace SiteAdaptation.C3DWatcher
{
    // R161. Watches the windows of one Civil 3D process the data book runner started, from outside it. On every dialog it
    // clicks the one button CivilDialogs names: Load Once for the security dialog of the probe DLL, Do Not Load for any
    // other security dialog, the safe button for the rest. It logs every window and dialog and never touches another process
    public static class Program
    {
        private const uint ButtonClick = 0x00F5;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr window);

        [STAThread]
        public static int Main(string[] args)
        {
            int pid = 0;
            string probe = null;
            string output = null;
            double minutes = 90;
            for (int at = 0; at + 1 < args.Length; at += 2)
            {
                switch (args[at])
                {
                    case "--pid":
                        pid = int.Parse(args[at + 1]);
                        break;
                    case "--probe":
                        probe = args[at + 1];
                        break;
                    case "--out":
                        output = args[at + 1];
                        break;
                    case "--minutes":
                        minutes = double.Parse(args[at + 1], System.Globalization.CultureInfo.InvariantCulture);
                        break;
                }
            }
            if (pid <= 0 || string.IsNullOrEmpty(probe) || string.IsNullOrEmpty(output))
            {
                Console.Error.WriteLine("C3D watcher: --pid, --probe and --out are needed");
                return 2;
            }
            var report = new WatchLog { Pid = pid, Probe = probe, StartedUtc = DateTime.UtcNow };
            Watch(pid, probe, minutes, report, output);
            report.FinishedUtc = DateTime.UtcNow;
            JsonOut.Write(output, report);
            return 0;
        }

        private static void Watch(int pid, string probe, double minutes, WatchLog report, string output)
        {
            DateTime deadline = DateTime.UtcNow.AddMinutes(minutes);
            var seen = new Dictionary<string, string>();
            var answered = new HashSet<string>();
            var windows = new HashSet<string>();
            while (true)
            {
                if (!Alive(pid))
                {
                    report.Result = "Civil 3D exited";
                    return;
                }
                if (DateTime.UtcNow > deadline)
                {
                    report.Result = "watched " + minutes + " minutes";
                    return;
                }
                foreach (AutomationElement window in Windows(pid))
                {
                    string key;
                    string title;
                    string windowClass;
                    List<string> texts;
                    List<string> buttons;
                    try
                    {
                        key = string.Join(".", window.GetRuntimeId());
                        title = window.Current.Name;
                        windowClass = window.Current.ClassName;
                        if (windows.Add(windowClass + " | " + title))
                        {
                            report.Windows.Add(new SeenWindow { Utc = DateTime.UtcNow, WindowClass = windowClass, Title = title });
                            JsonOut.Write(output, report);
                        }
                        if (answered.Contains(key) || !(windowClass == "#32770" || (title ?? "").IndexOf("Security", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            continue;
                        }
                        Parts(window, out texts, out buttons);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    // A dialog is judged when it reads the same twice, so a box still filling in is not read half drawn
                    string picture = title + "\n" + string.Join("\n", texts) + "\n" + string.Join("\n", buttons);
                    if (!seen.TryGetValue(key, out string before) || before != picture)
                    {
                        seen[key] = picture;
                        continue;
                    }
                    DialogAnswer answer = CivilDialogs.Decide(title, texts, buttons, probe);
                    var entry = new SeenDialog
                    {
                        Utc = DateTime.UtcNow, Title = title, WindowClass = windowClass, Texts = texts, Buttons = buttons,
                        Action = answer.Action.ToString(), Why = answer.Why,
                    };
                    answered.Add(key);
                    if (answer.Button != null)
                    {
                        entry.Clicked = Click(window, answer.Button);
                    }
                    report.Dialogs.Add(entry);
                    JsonOut.Write(output, report);
                }
                Thread.Sleep(500);
            }
        }

        // The top windows of the process and the windows one level inside them, where AutoCAD may put a task dialog
        private static IEnumerable<AutomationElement> Windows(int pid)
        {
            AutomationElementCollection tops;
            try
            {
                tops = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, pid));
            }
            catch (Exception)
            {
                yield break;
            }
            var inner = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
            foreach (AutomationElement top in tops)
            {
                yield return top;
                AutomationElementCollection children = null;
                try
                {
                    children = top.FindAll(TreeScope.Children, inner);
                }
                catch (Exception)
                {
                }
                if (children == null)
                {
                    continue;
                }
                foreach (AutomationElement child in children)
                {
                    yield return child;
                }
            }
        }

        private static bool IsButton(AutomationElement part)
        {
            return part.Current.ControlType == ControlType.Button || part.Current.ClassName == "CCPushButton";
        }

        private static void Parts(AutomationElement window, out List<string> texts, out List<string> buttons)
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
                (IsButton(part) ? buttons : texts).Add(name.Trim());
            }
        }

        // Clicks the button whose plain label is the one decided, by Invoke or by a click message to the button itself
        private static string Click(AutomationElement window, string label)
        {
            foreach (AutomationElement part in window.FindAll(TreeScope.Descendants, Condition.TrueCondition))
            {
                if (!IsButton(part) || !string.Equals(CivilDialogs.Plain(part.Current.Name), label, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                int dialog = window.Current.NativeWindowHandle;
                int handle = part.Current.NativeWindowHandle;
                string how;
                if (part.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
                {
                    ((InvokePattern)pattern).Invoke();
                    how = "invoke";
                }
                else if (handle != 0)
                {
                    PostMessage(new IntPtr(handle), ButtonClick, IntPtr.Zero, IntPtr.Zero);
                    how = "click message";
                }
                else
                {
                    return label + " takes no click";
                }
                for (int wait = 0; wait < 50 && dialog != 0 && IsWindow(new IntPtr(dialog)); wait++)
                {
                    Thread.Sleep(100);
                }
                return label + " (" + how + ")" + (dialog != 0 && IsWindow(new IntPtr(dialog)) ? ", the dialog stayed open" : "");
            }
            return label + " not found";
        }

        private static bool Alive(int pid)
        {
            try
            {
                return !Process.GetProcessById(pid).HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    public class WatchLog
    {
        public int Pid { get; set; }
        public string Probe { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? FinishedUtc { get; set; }
        public string Result { get; set; }
        public List<SeenDialog> Dialogs { get; set; } = new List<SeenDialog>();
        public List<SeenWindow> Windows { get; set; } = new List<SeenWindow>();
    }

    public class SeenDialog
    {
        public DateTime Utc { get; set; }
        public string Title { get; set; }
        public string WindowClass { get; set; }
        public List<string> Texts { get; set; }
        public List<string> Buttons { get; set; }
        public string Action { get; set; }
        public string Why { get; set; }
        public string Clicked { get; set; }
    }

    public class SeenWindow
    {
        public DateTime Utc { get; set; }
        public string WindowClass { get; set; }
        public string Title { get; set; }
    }
}
