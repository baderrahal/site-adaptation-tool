using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SiteAdaptation.Common
{
    // R347. Every file the worker or the add-in writes goes first to <name>.tmp-<run id> in the folder of its name and is then moved
    // onto its name, so a partial file of a run that failed, timed out or was killed is never taken as an output
    public static class WorkFiles
    {
        public const string Marker = ".tmp-";

        // R347. The marks of a commit beside the temporary name of an output: the earlier file under the name, a name the run adds
        // with no earlier file, and on the first output the commit point, once every output is on its name
        public const string BackupMark = ".backup";
        public const string AddedMark = ".added";
        public const string CommittedMark = ".committed";

        public static string TemporaryName(string path, string runId)
        {
            return path + Marker + runId;
        }

        public static bool IsTemporary(string path)
        {
            return Path.GetFileName(path).Contains(Marker);
        }

        // File.Replace when the name is there, File.Move when it is not
        public static void Commit(string temporary, string path)
        {
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        public static string WriteText(string path, string runId, string text)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(folder);
            string temporary = TemporaryName(path, runId);
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            Commit(temporary, path);
            return path;
        }

        // R347. The outputs of a run move onto their names all or none. A name that holds a file is replaced with the earlier file
        // kept as its backup, a new name is marked as added before its move, and once every output is on its name the commit point
        // is marked on the first. When a move fails every name goes back to what it held and IOException names the failure. After a
        // kill RestoreRun puts the names back by the same marks, or keeps them all past the commit point. EndCommit drops the marks
        public static void CommitAll(IReadOnlyList<KeyValuePair<string, string>> outputs, string runId)
        {
            if (outputs.Count == 0)
            {
                return;
            }
            try
            {
                foreach (KeyValuePair<string, string> output in outputs)
                {
                    if (File.Exists(output.Value))
                    {
                        File.Replace(output.Key, output.Value, output.Key + BackupMark);
                    }
                    else
                    {
                        File.WriteAllBytes(output.Key + AddedMark, new byte[0]);
                        File.Move(output.Key, output.Value);
                    }
                }
                File.WriteAllBytes(outputs[0].Key + CommittedMark, new byte[0]);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                List<string> left = PutBack(outputs.Select(output => output.Value), runId);
                throw new IOException("the outputs of run " + runId + " could not all move onto their names, " + failure.Message.Trim().TrimEnd('.')
                    + (left.Count == 0 ? ", every name holds what it held before the run" : ", and these could not be put back: " + string.Join(", ", left)));
            }
        }

        // The marks of a commit that passed, deleted once the answer is written. The ones that could not be deleted are returned
        public static List<string> EndCommit(IReadOnlyList<KeyValuePair<string, string>> outputs)
        {
            var left = new List<string>();
            foreach (KeyValuePair<string, string> output in outputs)
            {
                Delete(output.Key + BackupMark, left);
                Delete(output.Key + AddedMark, left);
            }
            if (outputs.Count > 0)
            {
                Delete(outputs[0].Key + CommittedMark, left);
            }
            return left;
        }

        // R347. After the kill of a run: a commit cut short goes back, every name to what it held before the run, and a commit past
        // its point stays whole. The marks go either way. Null when the run left no mark, else what was done, for the answer
        public static string RestoreRun(IEnumerable<string> folders, string runId)
        {
            string suffix = Marker + runId;
            var marks = folders.Where(place => !string.IsNullOrEmpty(place) && Directory.Exists(place)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(place => Directory.GetFiles(place, "*" + suffix + ".*", SearchOption.TopDirectoryOnly)).ToList();
            List<string> Named(string mark)
            {
                return marks.Where(path => Path.GetFileName(path).EndsWith(suffix + mark, StringComparison.Ordinal)).Select(path => path.Substring(0, path.Length - (suffix + mark).Length)).ToList();
            }
            List<string> names = Named(BackupMark).Concat(Named(AddedMark)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            List<string> committed = Named(CommittedMark);
            if (names.Count == 0 && committed.Count == 0)
            {
                return null;
            }
            var left = new List<string>();
            if (committed.Count > 0)
            {
                foreach (string name in names)
                {
                    Delete(TemporaryName(name, runId) + BackupMark, left);
                    Delete(TemporaryName(name, runId) + AddedMark, left);
                }
            }
            else
            {
                left.AddRange(PutBack(names, runId));
            }
            foreach (string name in committed)
            {
                Delete(TemporaryName(name, runId) + CommittedMark, left);
            }
            if (left.Count > 0)
            {
                throw new IOException("the outputs of run " + runId + " could not all be put back after the kill: " + string.Join(", ", left));
            }
            return committed.Count > 0 ? "its outputs had all moved onto their names before the kill and stay" : "the outputs it had moved onto their names were put back as they were before the run";
        }

        // Each name back to what it held before the run: its backup onto it, a name the run added deleted, its marks gone. The names
        // that could not be put back are returned with why
        private static List<string> PutBack(IEnumerable<string> names, string runId)
        {
            var left = new List<string>();
            foreach (string name in names)
            {
                string temporary = TemporaryName(name, runId);
                try
                {
                    if (File.Exists(temporary + BackupMark))
                    {
                        if (File.Exists(name))
                        {
                            File.Replace(temporary + BackupMark, name, null);
                        }
                        else
                        {
                            File.Move(temporary + BackupMark, name);
                        }
                    }
                    else if (File.Exists(temporary + AddedMark) && File.Exists(name))
                    {
                        File.Delete(name);
                    }
                    if (File.Exists(temporary + AddedMark))
                    {
                        File.Delete(temporary + AddedMark);
                    }
                    if (File.Exists(temporary + CommittedMark))
                    {
                        File.Delete(temporary + CommittedMark);
                    }
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                {
                    left.Add(name + " (" + failure.GetType().Name + ": " + failure.Message + ")");
                }
            }
            return left;
        }

        private static void Delete(string path, List<string> left)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                left.Add(path + " (" + failure.GetType().Name + ": " + failure.Message + ")");
            }
        }

        // R347. The folder of the git work tree that holds a folder, the first one at or above it with a .git entry, a folder or the
        // file of a worktree, else null. The folder is walked as given and again as resolved through every link and junction on it,
        // so a junction into a work tree is found. Real coordinates and levels never go into git, so the worker refuses an output or
        // steps folder there, as python/parity.py and the Python steps refuse one
        public static string RepoAbove(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }
            string full = Path.GetFullPath(folder);
            return TreeAbove(full) ?? TreeAbove(FinalPath(full));
        }

        private static string TreeAbove(string folder)
        {
            for (var step = new DirectoryInfo(folder); step != null; step = step.Parent)
            {
                string git = Path.Combine(step.FullName, ".git");
                if (Directory.Exists(git) || File.Exists(git))
                {
                    return step.FullName;
                }
            }
            return null;
        }

        // R347. A path with every link and junction on it resolved, GetFinalPathNameByHandle on Windows and realpath elsewhere, the
        // part not on disk yet joined to the resolved part above it. IOException when the part on disk cannot be opened
        public static string FinalPath(string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetPathRoot(full) ?? "";
            if (full.Length > root.Length)
            {
                full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            string existing = full;
            var rest = new Stack<string>();
            while (!Directory.Exists(existing) && !File.Exists(existing))
            {
                string parent = Path.GetDirectoryName(existing);
                if (parent == null)
                {
                    return full;
                }
                rest.Push(Path.GetFileName(existing));
                existing = parent;
            }
            string resolved = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WindowsFinalPath(existing) : UnixFinalPath(existing);
            while (rest.Count > 0)
            {
                resolved = Path.Combine(resolved, rest.Pop());
            }
            return resolved;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint size, uint flags);

        [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
        private static extern IntPtr RealPath(string path, IntPtr resolved);

        [DllImport("libc", EntryPoint = "free")]
        private static extern void Free(IntPtr memory);

        private static string WindowsFinalPath(string path)
        {
            // No access asked, every share allowed and the backup flag, which opens a folder to read its name only
            const uint ShareAll = 7, OpenExisting = 3, BackupSemantics = 0x02000000;
            using (SafeFileHandle handle = CreateFileW(path, 0, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    throw new IOException("the folder " + path + " could not be opened to resolve its links and junctions, Win32 error " + Marshal.GetLastWin32Error());
                }
                var text = new StringBuilder(1024);
                uint length = GetFinalPathNameByHandleW(handle, text, (uint)text.Capacity, 0);
                if (length >= text.Capacity)
                {
                    text = new StringBuilder((int)length + 1);
                    length = GetFinalPathNameByHandleW(handle, text, (uint)text.Capacity, 0);
                }
                if (length == 0 || length >= text.Capacity)
                {
                    throw new IOException("the final path of " + path + " could not be read, Win32 error " + Marshal.GetLastWin32Error());
                }
                string final = text.ToString();
                if (final.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
                {
                    return @"\\" + final.Substring(8);
                }
                return final.StartsWith(@"\\?\", StringComparison.Ordinal) ? final.Substring(4) : final;
            }
        }

        private static string UnixFinalPath(string path)
        {
            IntPtr found = RealPath(path, IntPtr.Zero);
            if (found == IntPtr.Zero)
            {
                throw new IOException("the folder " + path + " could not be resolved through its links, errno " + Marshal.GetLastWin32Error());
            }
            try
            {
                return Marshal.PtrToStringAnsi(found);
            }
            finally
            {
                Free(found);
            }
        }

        // R347, R354. The bytes of an input, for a reader that decodes them as Python does (PyMath.ReadJson, CsvRows.Read), a name holding
        // .tmp- never read
        public static byte[] ReadBytes(string path)
        {
            if (IsTemporary(path))
            {
                throw new InvalidOperationException("a file under a temporary name is never read: " + path);
            }
            return File.ReadAllBytes(path);
        }

        // A name holding .tmp- is never read
        public static string ReadText(string path)
        {
            if (IsTemporary(path))
            {
                throw new InvalidOperationException("a file under a temporary name is never read: " + path);
            }
            return File.ReadAllText(path);
        }

        // The temporary files of one run id in the folder itself, deleted by a run at its start and by the add-in when it ends the
        // run at its time limit. A run never deletes those of another run id, which may be a run going on beside it, and never walks
        // down the folder, since a folder such as Documents holds junctions such as My Music that deny the walk. A file that cannot be
        // deleted is named in the IOException thrown once every other file was tried
        public static List<string> DeleteRun(string folder, string runId)
        {
            var deleted = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return deleted;
            }
            var failed = new List<string>();
            foreach (string path in Directory.GetFiles(folder, "*" + Marker + runId, SearchOption.TopDirectoryOnly))
            {
                if (!Path.GetFileName(path).EndsWith(Marker + runId, StringComparison.Ordinal))
                {
                    continue;
                }
                try
                {
                    File.Delete(path);
                    deleted.Add(path);
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                {
                    failed.Add(path + " (" + failure.GetType().Name + ": " + failure.Message + ")");
                }
            }
            if (failed.Count > 0)
            {
                throw new IOException("the temporary files of run " + runId + " could not be deleted: " + string.Join(", ", failed));
            }
            return deleted;
        }
    }

    public static class FileHash
    {
        // The SHA-256 of the content of a file in lower case hex, the file opened to read only (R1)
        public static string Sha256(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(stream).Select(item => item.ToString("x2")));
            }
        }
    }
}
