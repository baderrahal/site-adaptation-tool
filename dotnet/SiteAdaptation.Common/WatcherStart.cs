using System;
using System.Globalization;

namespace SiteAdaptation.Common
{
    // R108, Lane B 5b. The dialog watcher starts before Revit, so no security box opens before anything looks at it. The run script
    // starts the watcher, waits for its first report, starts Revit and then writes the pid of Revit into the pid file the watcher reads
    public static class WatcherStart
    {
        public const string WaitingResult = "waiting for the pid of Revit";

        public static int? ReadPid(string text)
        {
            int pid;
            if (text == null || !int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out pid) || pid <= 0)
            {
                return null;
            }
            return pid;
        }

        // Null when no pid came by the deadline. A file still being written reads as no pid and is read again after the pause
        public static int? WaitForPid(Func<string> read, Func<DateTime> now, DateTime deadlineUtc, Action pause)
        {
            while (true)
            {
                int? pid = ReadPid(read());
                if (pid != null)
                {
                    return pid;
                }
                if (now() > deadlineUtc)
                {
                    return null;
                }
                pause();
            }
        }
    }
}
