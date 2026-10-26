using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace SiteAdaptation.Common
{
    // R347, R200. Free physical memory against the measured peak of an input plus the floor of siteBuild.minFreeGB, in GiB as
    // window_lock.py reads it, before each large read of the worker
    public static class WorkerMemory
    {
        public const double MiBPerGiB = 1024.0;

        // Null when the worker may read, else the refusal with both numbers named
        public static string Refusal(string input, double? freeMiB, double peakMiB, double floorGiB)
        {
            string need = string.Format(CultureInfo.InvariantCulture, "the {0} read needs {1:0.00} GiB and the floor {2:0.00} GiB", input, peakMiB / MiBPerGiB, floorGiB);
            if (!freeMiB.HasValue)
            {
                return "free memory could not be read, " + need;
            }
            if (freeMiB.Value >= peakMiB + floorGiB * MiBPerGiB)
            {
                return null;
            }
            return string.Format(CultureInfo.InvariantCulture, "free memory {0:0.00} GiB, ", freeMiB.Value / MiBPerGiB) + need;
        }

        [StructLayout(LayoutKind.Sequential)]
        private sealed class MemoryStatus
        {
            public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            public uint Load;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus status);

        // GlobalMemoryStatusEx on Windows, MemAvailable of /proc/meminfo on Linux for the tests, null when neither gives it
        public static double? FreeMiB()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var status = new MemoryStatus();
                    return GlobalMemoryStatusEx(status) ? status.AvailablePhysical / (1024.0 * 1024.0) : (double?)null;
                }
                foreach (string line in File.ReadAllLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        return double.Parse(parts[1], CultureInfo.InvariantCulture) / 1024.0;
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The peak private memory of this process as Windows keeps it (PeakPagefileUsage), as R310 samples Revit
        public static double PeakPrivateMiB()
        {
            using (Process self = Process.GetCurrentProcess())
            {
                return self.PeakPagedMemorySize64 / (1024.0 * 1024.0);
            }
        }
    }
}
