using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SiteAdaptation.Common
{
    // One dialog the watcher of R106 met while Revit started
    public class SecurityDialogEntry
    {
        public DateTime Utc { get; set; }
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
        StopOurs,
        StopUnreadable,
        StopNoButton,
    }

    // R106. How the watcher reads an add-in security dialog of Revit and what it does with it.
    // It only ever clicks Do Not Load, and only for an add-in that is not one of this tool
    public static class SecurityDialogs
    {
        public const string DoNotLoad = "Do Not Load";
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

        // ourIds are the AddInIds of the three .addin files of this tool, ourMarks their names and DLL file names
        public static WatchAction Decide(SecurityDialogEntry entry, ICollection<string> ourIds, IEnumerable<string> ourMarks)
        {
            if (string.IsNullOrWhiteSpace(entry.AddInName) && entry.AddInId == null)
            {
                return WatchAction.StopUnreadable;
            }
            bool ours = (entry.AddInId != null && ourIds.Contains(entry.AddInId, StringComparer.OrdinalIgnoreCase))
                || ourMarks.Any(mark => !string.IsNullOrWhiteSpace(mark) && (entry.Text ?? "").IndexOf(mark, StringComparison.OrdinalIgnoreCase) >= 0);
            if (ours)
            {
                return WatchAction.StopOurs;
            }
            if (!entry.Buttons.Any(IsDoNotLoad))
            {
                return WatchAction.StopNoButton;
            }
            return WatchAction.DoNotLoad;
        }
    }
}
