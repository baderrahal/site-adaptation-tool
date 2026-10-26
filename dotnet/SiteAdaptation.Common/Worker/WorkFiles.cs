using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SiteAdaptation.Common
{
    // R347. Every file the worker or the add-in writes goes first to <name>.tmp-<run id> in the folder of its name and is then moved
    // onto its name, so a partial file of a run that failed, timed out or was killed is never taken as an output
    public static class WorkFiles
    {
        public const string Marker = ".tmp-";

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
