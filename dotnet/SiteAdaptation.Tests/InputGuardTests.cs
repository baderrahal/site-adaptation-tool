using System;
using System.IO;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    public sealed class InputGuardTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_guard_" + Guid.NewGuid().ToString("N"));

        public InputGuardTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        [Fact]
        public void D19_a_mark_holds_the_name_size_write_time_and_sha256_and_a_second_mark_of_the_same_file_is_no_change()
        {
            string path = Path.Combine(folder, "plots.csv");
            File.WriteAllText(path, "plot\n101\n");
            InputMark mark = InputGuard.Mark(path);
            Assert.Equal("plots.csv", mark.Name);
            Assert.Equal(9, mark.Size);
            Assert.Equal(File.GetLastWriteTimeUtc(path).ToString("o"), mark.WriteTimeUtc);
            Assert.Equal(FileHash.Sha256(path), mark.Sha256);
            Assert.Null(InputGuard.Change(mark, InputGuard.Mark(path)));
        }

        [Fact]
        public void D19_a_change_of_the_size_the_write_time_or_the_content_alone_is_named()
        {
            string path = Path.Combine(folder, "plots.csv");
            File.WriteAllText(path, "plot\n101\n");
            DateTime written = File.GetLastWriteTimeUtc(path);
            InputMark before = InputGuard.Mark(path);

            // A copy over a file keeps the write time of its source, so the content is held as well
            File.WriteAllText(path, "plot\n102\n");
            File.SetLastWriteTimeUtc(path, written);
            Assert.Equal("SHA-256", InputGuard.Change(before, InputGuard.Mark(path)));

            File.WriteAllText(path, "plot\n101\n");
            File.SetLastWriteTimeUtc(path, written.AddSeconds(5));
            Assert.Equal("write time", InputGuard.Change(before, InputGuard.Mark(path)));

            File.AppendAllText(path, "103\n");
            File.SetLastWriteTimeUtc(path, written);
            Assert.Equal("size, SHA-256", InputGuard.Change(before, InputGuard.Mark(path)));
        }
    }
}
