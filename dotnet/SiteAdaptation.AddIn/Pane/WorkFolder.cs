using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.AddIn.Pane
{
    // R372, docs/design/06-settings.md Work folder. The work root comes from the machine file of Bader's QL2, else from the
    // Documents folder of the user. The folder of a document is named by its file and the hash of its path, so two files of one
    // name in two folders never share one, and a document with no path has none
    public static class WorkFolder
    {
        public const string Folder = "SiteAdaptation";
        public const string MachineFile = "machine.json";
        public const string SaveFirst = "Save the project first, its work folder is named by its file";

        // readText stands for the file system, so a test needs no machine file. A machine file that is there but gives no
        // workRoot is refused, never passed over for the default
        public static string Root(string appData, string myDocuments, Func<string, string> readText)
        {
            string machine = Path.Combine(appData, Folder, MachineFile);
            string text = readText(machine);
            if (text == null)
            {
                return Path.Combine(myDocuments, Folder);
            }
            JToken root;
            try
            {
                root = JToken.Parse(text);
            }
            catch (JsonException error)
            {
                throw new InvalidDataException(machine + " is no JSON: " + error.Message, error);
            }
            JToken workRoot = root.Type == JTokenType.Object ? root["workRoot"] : null;
            if (workRoot == null || workRoot.Type != JTokenType.String || string.IsNullOrWhiteSpace(workRoot.Value<string>()))
            {
                throw new InvalidDataException(machine + " gives no workRoot");
            }
            return workRoot.Value<string>();
        }

        public static string ReadIfThere(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        // <root>\<file name without extension>_<first 8 hex of the SHA-256 of the full path in lower case>, null with no path
        public static string For(string root, string pathName)
        {
            if (string.IsNullOrEmpty(pathName))
            {
                return null;
            }
            return Path.Combine(root, Path.GetFileNameWithoutExtension(pathName) + "_" + PathHash(pathName));
        }

        // Windows paths ignore case, so the path is hashed in lower case after GetFullPath, as UTF-8
        public static string PathHash(string pathName)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Path.GetFullPath(pathName).ToLowerInvariant());
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var text = new StringBuilder(8);
                for (int at = 0; at < 4; at++)
                {
                    text.Append(hash[at].ToString("x2", CultureInfo.InvariantCulture));
                }
                return text.ToString();
            }
        }

        // R1. The sources stay read only, so a work folder inside inputRoot is refused
        public static bool Inside(string folder, string inputRoot)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(inputRoot))
            {
                return false;
            }
            string inner = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string outer = Path.GetFullPath(inputRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return inner.StartsWith(outer, StringComparison.OrdinalIgnoreCase);
        }

        // What a button does before its run: the stop text, or null when the work folder is good. inputRoot is null when no
        // config names one
        public static string Stop(string root, string pathName, string inputRoot)
        {
            string folder = For(root, pathName);
            if (folder == null)
            {
                return SaveFirst;
            }
            if (Inside(folder, inputRoot))
            {
                return "The work folder " + folder + " lies inside inputRoot " + inputRoot + ", the sources stay read only";
            }
            return null;
        }

        // inputRoot of the config file the settings name, null when the file is not there or names none
        public static string InputRoot(string configFile, Func<string, string> readText)
        {
            string text = string.IsNullOrEmpty(configFile) ? null : readText(configFile);
            if (text == null)
            {
                return null;
            }
            JToken root = JToken.Parse(text);
            JToken value = root.Type == JTokenType.Object ? root["inputRoot"] : null;
            return value == null || value.Type != JTokenType.String ? null : value.Value<string>();
        }
    }
}
