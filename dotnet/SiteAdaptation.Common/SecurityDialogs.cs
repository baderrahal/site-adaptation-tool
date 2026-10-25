using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SiteAdaptation.Common
{
    // One dialog the watcher of R108 met while Revit started
    public class SecurityDialogEntry
    {
        public DateTime Utc { get; set; }
        // R110. The Revit process the dialog belongs to, so parallel windows are told apart in the log
        public int Pid { get; set; }
        public string Title { get; set; }
        public string WindowClass { get; set; }
        public string AddInName { get; set; }
        public string Vendor { get; set; }
        // Null when the dialog shows no GUID, as the Revit 2024 box does
        public string AddInId { get; set; }
        // The DLL the dialog names, it tells an add-in of this tool even when the dialog shows no GUID
        public string Location { get; set; }
        public string Button { get; set; }
        public string Action { get; set; }
        // The full text, kept when the watcher stopped Revit on this dialog
        public string Text { get; set; }
        public List<string> Buttons { get; set; }
    }

    public enum WatchAction
    {
        DoNotLoad,
        LoadOnceOurs,
        StopOurs,
        StopUnreadable,
        StopNoButton,
    }

    // R108. How the watcher reads an add-in security dialog of Revit and what it does with it.
    // It clicks Do Not Load for an add-in that is not one of this tool and Load Once for the three DLLs of this tool,
    // which leaves nothing kept. It never clicks the third button, the one that keeps an answer
    public static class SecurityDialogs
    {
        public const string DoNotLoad = "Do Not Load";
        public const string LoadOnce = "Load Once";
        private static readonly Regex Guid = new Regex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant);
        private static readonly Regex NameLine = new Regex(@"(?im)^\s*(add-?in\s+)?name\s*:\s*(?<value>.+?)\s*$", RegexOptions.CultureInvariant);
        private static readonly Regex VendorLine = new Regex(@"(?im)^\s*(vendor|publisher)[^:\r\n]*:\s*(?<value>.+?)\s*$", RegexOptions.CultureInvariant);
        private static readonly Regex LocationLine = new Regex(@"(?im)^\s*location\s*:\s*(?<value>.+?)\s*$", RegexOptions.CultureInvariant);

        public static bool IsDoNotLoad(string button)
        {
            return button != null && button.Trim().StartsWith(DoNotLoad, StringComparison.OrdinalIgnoreCase);
        }

        // A window the watcher must answer or stop on: a standard dialog of Revit or a window titled Security.
        // The buttons inside are not asked, the main window of Revit holds the security box and would count by them
        public static bool IsDialog(string windowClass, string title)
        {
            return windowClass == "#32770" || (title ?? "").IndexOf("Security", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static SecurityDialogEntry Read(string title, string windowClass, IEnumerable<string> texts, IEnumerable<string> buttons)
        {
            string text = string.Join("\n", (texts ?? new string[0]).Where(part => !string.IsNullOrWhiteSpace(part)));
            Match name = NameLine.Match(text);
            Match vendor = VendorLine.Match(text);
            Match id = Guid.Match(text);
            Match location = LocationLine.Match(text);
            return new SecurityDialogEntry
            {
                Utc = DateTime.UtcNow,
                Title = title,
                WindowClass = windowClass,
                AddInName = name.Success ? name.Groups["value"].Value : null,
                Vendor = vendor.Success ? vendor.Groups["value"].Value : null,
                AddInId = id.Success ? id.Value.ToLowerInvariant() : null,
                Location = location.Success ? location.Groups["value"].Value : null,
                Text = text,
                Buttons = (buttons ?? new string[0]).ToList(),
            };
        }

        public static bool IsLoadOnce(string button)
        {
            return button != null && button.Trim().StartsWith(LoadOnce, StringComparison.OrdinalIgnoreCase);
        }

        // ourIds are the AddInIds of the three .addin files of this tool, ourMarks their names and DLL file names,
        // ourPaths the full paths of the three installed DLLs. Only a box whose Location is one of ourPaths gets Load Once,
        // a box that looks like this tool in any other way stops Revit
        public static WatchAction Decide(SecurityDialogEntry entry, ICollection<string> ourIds, IEnumerable<string> ourMarks, IEnumerable<string> ourPaths)
        {
            if (string.IsNullOrWhiteSpace(entry.AddInName) && entry.AddInId == null)
            {
                return WatchAction.StopUnreadable;
            }
            bool exact = entry.Location != null && (ourPaths ?? new string[0]).Any(path => string.Equals(path, entry.Location.Trim(), StringComparison.OrdinalIgnoreCase));
            bool ours = exact || (entry.AddInId != null && ourIds.Contains(entry.AddInId, StringComparer.OrdinalIgnoreCase))
                || ourMarks.Any(mark => !string.IsNullOrWhiteSpace(mark) && (entry.Text ?? "").IndexOf(mark, StringComparison.OrdinalIgnoreCase) >= 0);
            if (ours)
            {
                if (!exact)
                {
                    return WatchAction.StopOurs;
                }
                return entry.Buttons.Any(IsLoadOnce) ? WatchAction.LoadOnceOurs : WatchAction.StopNoButton;
            }
            if (!entry.Buttons.Any(IsDoNotLoad))
            {
                return WatchAction.StopNoButton;
            }
            return WatchAction.DoNotLoad;
        }

        // The one button each action clicks, null for a stop
        public static string ButtonFor(WatchAction action)
        {
            switch (action)
            {
                case WatchAction.DoNotLoad:
                    return DoNotLoad;
                case WatchAction.LoadOnceOurs:
                    return LoadOnce;
                default:
                    return null;
            }
        }

        // R303. Why the watcher stops, or null while it goes on: Revit gone, or its minutes over. Only the start watch of R108,
        // stopAtFirstStep, also stops once the test host has written its first log step
        public static string StopReason(bool revitAlive, bool firstStepWritten, bool stopAtFirstStep, DateTime nowUtc, DateTime deadlineUtc, double minutes)
        {
            if (!revitAlive)
            {
                return "Revit exited";
            }
            if (stopAtFirstStep && firstStepWritten)
            {
                return "the test host wrote its first log step";
            }
            if (nowUtc > deadlineUtc)
            {
                return "watched " + minutes + " minutes";
            }
            return null;
        }

        // R303. Whether a dialog the watcher read stops Revit. While Revit starts, a dialog it cannot answer by rule 3 does (R108).
        // After the first step of the test host it is logged only, the test host and its limits own the job
        public static bool StopsRevit(WatchAction action, bool jobPhase)
        {
            return ButtonFor(action) == null && !jobPhase;
        }

        // R303. Whether the watcher reads a window by UI Automation. While Revit starts it reads every window as R108 says. In the
        // job phase only a dialog whose window is not hung, since a UI Automation call on a busy Revit waits until Revit answers,
        // which held the watcher for the whole image step of I25. The other windows are listed by Win32 calls that do not wait
        public static bool ReadByAutomation(bool jobPhase, string windowClass, string title, bool hung)
        {
            return !jobPhase || (IsDialog(windowClass, title) && !hung);
        }
    }
}
