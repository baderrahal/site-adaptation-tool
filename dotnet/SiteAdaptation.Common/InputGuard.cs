using System.Collections.Generic;
using System.IO;

namespace SiteAdaptation.Common
{
    // D19. What a run records of each file it opens, before and after, so a read that changed a real input fails
    public sealed class InputMark
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string WriteTimeUtc { get; set; }
        public string Sha256 { get; set; }
    }

    public static class InputGuard
    {
        public static InputMark Mark(string path)
        {
            var info = new FileInfo(path);
            return new InputMark { Name = info.Name, Size = info.Length, WriteTimeUtc = info.LastWriteTimeUtc.ToString("o"), Sha256 = FileHash.Sha256(path) };
        }

        // What differs between two marks of one file, null when nothing does. A copy over a file keeps the write time of its source, so
        // the content counts as well as the size and the time
        public static string Change(InputMark before, InputMark after)
        {
            var parts = new List<string>();
            if (before.Size != after.Size)
            {
                parts.Add("size");
            }
            if (before.WriteTimeUtc != after.WriteTimeUtc)
            {
                parts.Add("write time");
            }
            if (!string.Equals(before.Sha256, after.Sha256, System.StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("SHA-256");
            }
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        // The line a read in place ends with when its input changed while it was read, null when it did not
        public static string ChangedWhileRead(string path, InputMark before)
        {
            string change = Change(before, Mark(path));
            return change == null ? null : "the input changed while it was read, its " + change + " (D19): " + path;
        }
    }
}
