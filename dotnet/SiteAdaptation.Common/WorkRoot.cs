using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R372, QL2 of steps/lane-b.md, J8. The work root of this machine: workRoot of %APPDATA%\SiteAdaptation\machine.json, the file and
    // the field the add-in reads for its work folders, else the SiteAdaptation folder of the Windows Documents folder, which on a
    // OneDrive machine lies in OneDrive. The add-in takes the work folders of its documents from it, through WorkFolder.Root, and the test
    // host and its run script their job, log and output files
    public static class WorkRoot
    {
        public const string Folder = "SiteAdaptation";
        public const string MachineFile = "machine.json";

        public static string MachinePath(string appData)
        {
            return Path.Combine(appData, Folder, MachineFile);
        }

        public static string ReadIfThere(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        // readText stands for the file system and gives null for no file. A machine file that is there but gives no plain full
        // workRoot is refused, never passed over for the default
        public static string Of(string appData, string myDocuments, Func<string, string> readText)
        {
            string machine = MachinePath(appData);
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
            string value = workRoot != null && workRoot.Type == JTokenType.String ? workRoot.Value<string>() : null;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(machine + " gives no workRoot");
            }
            if (!TestHostGuard.IsPlainFullPath(value))
            {
                throw new InvalidDataException(machine + " gives a workRoot that is not a plain full path: " + value);
            }
            // The test host keeps its job, log and output files in <documents>\SiteAdaptation by TestHostGuard, and the add-in and
            // the test host read one root, so a root of another name is refused for both
            if (!string.Equals(Path.GetFileName(value), Folder, StringComparison.Ordinal))
            {
                throw new InvalidDataException(machine + " gives a workRoot whose last folder is not named " + Folder + ": " + value);
            }
            return value;
        }

        // TestHostGuard keeps the files of the test host in <documents>\SiteAdaptation, so the test host takes the folder above the
        // root. The default root of Documents is checked here too
        public static string TestHostDocuments(string root)
        {
            if (!TestHostGuard.IsPlainFullPath(root) || !string.Equals(Path.GetFileName(root), Folder, StringComparison.Ordinal))
            {
                throw new InvalidDataException("the work root of the test host is a plain full path to a folder named " + Folder + ": " + root);
            }
            return Path.GetDirectoryName(root);
        }
    }
}
