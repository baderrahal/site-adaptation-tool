// R104, Lane B 5b. The top level windows of one process for the freeze guard of Run-TestHost.ps1, which compiles this file with
// Add-Type -Path. GetWindowText of another process sends no message, so a hung Revit cannot hold the guard
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SiteAdaptation
{
    public static class FreezeWindows
    {
        private delegate bool EnumProc(IntPtr window, IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc callback, IntPtr data);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder text, int length);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        // Each top level window of the process as handle, class, title and whether it shows
        public static List<string[]> Of(int processId)
        {
            var found = new List<string[]>();
            EnumWindows((window, data) =>
            {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner == (uint)processId)
                {
                    var title = new StringBuilder(512);
                    var name = new StringBuilder(256);
                    GetWindowText(window, title, title.Capacity);
                    GetClassName(window, name, name.Capacity);
                    found.Add(new[] { window.ToInt64().ToString(), name.ToString(), title.ToString(), IsWindowVisible(window) ? "1" : "0" });
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }
}
