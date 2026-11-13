using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common.ReadData
{
    // R439. The scan folders of a work folder, scan_<run id> with scan.json, as the scan before the old read of step 2 wrote them. R847: no
    // step writes one since that read went, Latest stays for the facts of New project (LatestScanFacts), which falls back on the read
    public static class ScanStep
    {
        public const string FolderPrefix = "scan_";
        public const string ResultFile = "scan.json";

        // R439. scan.json of the newest scan folder of the work folder, by its run id, which starts with the button and the time, else null
        public static string Latest(string workFolder)
        {
            return Scans(workFolder).FirstOrDefault();
        }

        private static IEnumerable<string> Scans(string workFolder)
        {
            if (string.IsNullOrEmpty(workFolder) || !Directory.Exists(workFolder))
            {
                return Enumerable.Empty<string>();
            }
            return Directory.GetDirectories(workFolder, FolderPrefix + "*").OrderByDescending(folder => Path.GetFileName(folder), StringComparer.Ordinal)
                .Select(folder => Path.Combine(folder, ResultFile)).Where(File.Exists).ToList();
        }
    }
}
