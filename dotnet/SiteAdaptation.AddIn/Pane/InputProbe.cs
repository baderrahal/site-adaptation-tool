using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SiteAdaptation.AddIn.Pane
{
    // What the pane knows of one input on disk: found, its size and, once hashed, its SHA-256
    public sealed class InputFacts
    {
        public bool Found { get; set; }
        public long Size { get; set; }

        // The last write time in UTC ticks, so a file written again is hashed again
        public long Stamp { get; set; }
    }

    internal sealed class HashedInput
    {
        public HashedInput(long size, long stamp, string sha256)
        {
            Size = size;
            Stamp = stamp;
            Sha256 = sha256;
        }

        public long Size { get; }
        public long Stamp { get; }
        public string Sha256 { get; }
    }

    // The faults of a caller of the pane, not texts of a window
    internal static class PaneErrors
    {
        public static Exception NoDocument()
        {
            return new InvalidOperationException("no document is open");
        }

        public static Exception Steps(int given)
        {
            return new ArgumentException("the pane shows " + StepPlan.Count + " steps, not " + given);
        }
    }

    // The pane asks the disk through this, so a test and the picture test give their own answers
    public interface IInputProbe
    {
        InputFacts Look(string path, bool folder);

        // done gets the lower case hex of the SHA-256, or null when the input could not be read
        void Hash(string path, bool folder, Action<string> done);

        IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback);
    }

    // The dialogs of Browse, so a test picks its own paths. Each gives null on Cancel. title is null for a dialog with no title of
    // the tool
    public interface IPathPicker
    {
        string File(string title);

        string Folder();
    }

    // R372, docs/design/03a-buttons-shared-and-1.md Steps. A file is hashed by its content, a folder by the list of its files with
    // name, size and last write time. The hash runs off the UI thread, since a drawing can be large, and done comes back through
    // post, the Dispatcher of the page in Revit, since a Revit event may run with no SynchronizationContext
    public sealed class DiskProbe : IInputProbe
    {
        public DiskProbe(Action<Action> post)
        {
            Post = post;
        }

        public Action<Action> Post { get; set; }

        public InputFacts Look(string path, bool folder)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new InputFacts();
            }
            try
            {
                if (folder)
                {
                    if (!Directory.Exists(path))
                    {
                        return new InputFacts();
                    }
                    List<FileInfo> files = FolderFiles(path).ToList();
                    return new InputFacts { Found = true, Size = files.Sum(file => file.Length), Stamp = files.Count == 0 ? 0 : files.Max(file => file.LastWriteTimeUtc.Ticks) ^ files.Count };
                }
                if (!File.Exists(path))
                {
                    return new InputFacts();
                }
                var info = new FileInfo(path);
                return new InputFacts { Found = true, Size = info.Length, Stamp = info.LastWriteTimeUtc.Ticks };
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                return new InputFacts();
            }
        }

        // The hash takes a thread of its own, not one of the pool. A large drawing holds it for seconds, and on .NET Framework
        // every running process whose output is redirected holds pool threads, so a hash queued on the pool waited past 10 s in
        // CI run 40684259718 and 40690865729 of PR 90
        public void Hash(string path, bool folder, Action<string> done)
        {
            Action<Action> post = Post;
            Task.Factory.StartNew(() =>
            {
                string hash;
                try
                {
                    hash = folder ? FolderSha256(path) : FileSha256(path);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
                {
                    hash = null;
                }
                post(() => done(hash));
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public IReadOnlyList<string> LevelProfiles(string rulesFile, out string fallback)
        {
            fallback = null;
            if (string.IsNullOrEmpty(rulesFile) || !File.Exists(rulesFile))
            {
                return new string[0];
            }
            try
            {
                return RulesProfiles.Names(File.ReadAllText(rulesFile), out fallback);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is Newtonsoft.Json.JsonException)
            {
                return new string[0];
            }
        }

        public static string FileSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan))
            {
                return Hex(sha.ComputeHash(stream));
            }
        }

        // One line per file, its path under the folder with forward slashes, its size and its last write time in UTC ticks, the
        // lines in ordinal order, so the same files give the same hash on every machine
        public static string FolderSha256(string folder)
        {
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            IEnumerable<string> lines = FolderFiles(folder)
                .Select(file => file.FullName.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/') + "|" + file.Length.ToString(CultureInfo.InvariantCulture) + "|" + file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture))
                .OrderBy(line => line, StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            {
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
            }
        }

        private static IEnumerable<FileInfo> FolderFiles(string folder)
        {
            return new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories);
        }

        private static string Hex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }
    }
}
