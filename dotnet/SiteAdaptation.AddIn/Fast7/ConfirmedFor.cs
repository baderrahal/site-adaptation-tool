using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.ReadData;

namespace SiteAdaptation.AddIn.Fast7
{
    // R771, Lead's point of 7 Nov 18:20. confirmed_profile.json names no folder, and a project may read two folders. So each Confirm that saves
    // writes beside it the scan folder of the read it confirmed, and a step 2 the Build put back keeps the profile only for that same folder,
    // by full path and case blind. Any other case opens the Confirm window as before
    public static class ConfirmedFor
    {
        public const string FileName = "confirmed_for.json";

        // The scan folder a read folder was read from, by the scanFolder input of its record read.json, null when it names none or does not read
        public static string ScanFolderOf(string readFolder)
        {
            if (string.IsNullOrEmpty(readFolder))
            {
                return null;
            }
            try
            {
                string record = Path.Combine(readFolder, ReadDataRun.RecordFile);
                if (!File.Exists(record))
                {
                    return null;
                }
                JArray inputs = JObject.Parse(WorkFiles.ReadText(record))["inputs"] as JArray;
                JToken scan = inputs?.OfType<JObject>().FirstOrDefault(input => (string)input["role"] == ReadInputs.ScanFolder);
                return scan?["path"]?.Type == JTokenType.String ? (string)scan["path"] : null;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is InvalidCastException)
            {
                return null;
            }
        }

        // After a Confirm that saved: the folder it was confirmed for. The file of the Confirm before goes first, so a folder not known or a write
        // that fails leaves no old folder in force. A file that cannot be written is told back as false and no more: with no file the next quiet
        // read opens the Confirm, as before R771, so the Confirm that just saved stays good
        public static bool Write(string workFolder, string scanFolder)
        {
            string path = Path.Combine(workFolder, FileName);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                if (string.IsNullOrEmpty(scanFolder))
                {
                    return true;
                }
                File.WriteAllText(path, new JObject { ["scanFolder"] = Path.GetFullPath(scanFolder) }.ToString());
                return true;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is ArgumentException || failure is NotSupportedException)
            {
                return false;
            }
        }

        // True when the profile confirmed in the work folder was confirmed for this scan folder
        public static bool Holds(string workFolder, string scanFolder)
        {
            string path = Path.Combine(workFolder, FileName);
            if (string.IsNullOrEmpty(scanFolder) || !File.Exists(Path.Combine(workFolder, ConfirmedProfile.FileName)) || !File.Exists(path))
            {
                return false;
            }
            try
            {
                JToken named = JObject.Parse(WorkFiles.ReadText(path))["scanFolder"];
                return named?.Type == JTokenType.String && string.Equals(Full((string)named), Full(scanFolder), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is ArgumentException || failure is NotSupportedException)
            {
                return false;
            }
        }

        private static string Full(string folder)
        {
            return Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
