using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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

        // R347, Q083. Null when a command whose whole peak config names may read on, else the refusal with the numbers named. Before the
        // first read the free memory is held against the whole peak plus the floor, before each later read against the rest of it, the
        // peak less the private memory the worker holds now, plus the floor, since what the worker holds is no longer free
        public static string CommandRefusal(string command, double? freeMiB, double peakMiB, double? heldMiB, double floorGiB)
        {
            double rest = heldMiB.HasValue ? Math.Max(0, peakMiB - heldMiB.Value) : peakMiB;
            string need = heldMiB.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "the rest of the whole {0} needs {1:0.00} GiB, its peak {2:0.00} GiB less the {3:0.00} GiB the worker holds, and the floor {4:0.00} GiB (Q083)",
                    command, rest / MiBPerGiB, peakMiB / MiBPerGiB, heldMiB.Value / MiBPerGiB, floorGiB)
                : string.Format(CultureInfo.InvariantCulture, "the whole {0} needs {1:0.00} GiB and the floor {2:0.00} GiB (Q083)", command, peakMiB / MiBPerGiB, floorGiB);
            if (!freeMiB.HasValue)
            {
                return "free memory could not be read, " + need;
            }
            if (freeMiB.Value >= rest + floorGiB * MiBPerGiB)
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

        // The private memory this process holds now, PagefileUsage on Windows and the private bytes of /proc on Linux, null when it is not
        // given
        public static double? PrivateMiB()
        {
            try
            {
                using (Process self = Process.GetCurrentProcess())
                {
                    return self.PrivateMemorySize64 / (1024.0 * 1024.0);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Q084. The drawings of a read in the order they are read: the one of the largest measured peak of addin.workerPeakMiB first, a
        // drawing not measured taking a peak scaled by its size from the measured ones (AddinConfig.WorkerPeakMiB), so the largest read
        // meets the least memory held, drawings of one peak in the order of roles. Only the roles given, each once
        public static List<string> ReadOrder(IEnumerable<WorkerFile> inputs, IReadOnlyList<string> roles, AddinConfig config)
        {
            List<WorkerFile> drawings = inputs.Where(input => roles.Contains(input.Role)).GroupBy(input => input.Role).Select(group => group.First()).ToList();
            return drawings.Select(input => new
            {
                input.Role,
                Peak = config.WorkerPeakMiB(input.Role, Path.GetFileName(input.Path), File.Exists(input.Path) ? new FileInfo(input.Path).Length : -1) ?? 0,
                Place = Enumerable.Range(0, roles.Count).First(index => roles[index] == input.Role),
            }).OrderByDescending(item => item.Peak).ThenBy(item => item.Place).Select(item => item.Role).ToList();
        }

        // Q084. The memory of a read let go before the next: a full blocking collection that compacts the large object heap too, run
        // twice around the finalizers, so the document of a drawing read before is not held when the next read starts. The private
        // memory after it in MiB
        public static double Release()
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            return PrivateMiB() ?? 0;
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
