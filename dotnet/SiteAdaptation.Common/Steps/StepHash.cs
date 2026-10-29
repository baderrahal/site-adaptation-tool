using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SiteAdaptation.Common.Flow;

namespace SiteAdaptation.Common.Steps
{
    // R366. The one SHA-256 over every input of a step, each input hashed by InputHash, a file by its content and a folder by its list
    public static class StepHash
    {
        // Each name and hash with its length before it, so no name or hash can run into the next, in name order
        public static string Combine(IReadOnlyDictionary<string, string> hashes)
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, string> item in hashes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                string hash = item.Value ?? "";
                text.Append(item.Key.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(item.Key)
                    .Append(hash.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(hash).Append('\n');
            }
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                return string.Concat(hash.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        // None when an input is not chosen or not on disk, so the step is not done and its button stop names the input (R382)
        public static string Over(IEnumerable<RunInput> inputs)
        {
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            bool missing = false;
            foreach (RunInput input in inputs)
            {
                if (hashes.ContainsKey(input.Name))
                {
                    throw new ArgumentException("the input " + input.Name + " is given twice", nameof(inputs));
                }
                if (string.IsNullOrEmpty(input.Path) || (input.IsFolder ? !Directory.Exists(input.Path) : !File.Exists(input.Path)))
                {
                    missing = true;
                    hashes[input.Name] = null;
                    continue;
                }
                hashes[input.Name] = InputHash.Of(input);
            }
            return missing ? null : Combine(hashes);
        }

        // The SHA-256 of each output the run record lists that is on disk now
        public static Dictionary<string, string> OnDisk(IEnumerable<string> paths)
        {
            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in paths.Where(File.Exists))
            {
                found[path] = FileHash.Sha256(path);
            }
            return found;
        }
    }
}
