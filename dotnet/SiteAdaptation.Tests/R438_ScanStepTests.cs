using System;
using System.IO;
using SiteAdaptation.Common.ReadData;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R439. The newest scan folder of a work folder, which LatestScanFacts of New project reads. R847: the scan step before the old read of
    // step 2 went with that read, its tests with it
    public sealed class R438_ScanStepTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "sa-scanstep-" + Guid.NewGuid().ToString("N"));
        private readonly string work;

        public R438_ScanStepTests()
        {
            work = Path.Combine(root, "SiteAdaptation", "site_0123abcd");
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void R439_latest_takes_the_newest_scan_folder_that_holds_a_scan()
        {
            // The result file is the one the worker command scan writes
            Assert.Equal(CadReader.CadScan.ResultFile, ScanStep.ResultFile);
            Assert.Null(ScanStep.Latest(work));
            Write(Path.Combine(work, "scan_2_20261031-100000-000", ScanStep.ResultFile), "{}");
            string newest = Write(Path.Combine(work, "scan_2_20261031-110000-000", ScanStep.ResultFile), "{}");
            Directory.CreateDirectory(Path.Combine(work, "scan_2_20261031-115900-000"));
            Write(Path.Combine(work, "read_ffff", ScanStep.ResultFile), "{}");
            Assert.Equal(newest, ScanStep.Latest(work));
        }

        private static string Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }
    }
}
