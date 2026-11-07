using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common
{
    // R750, F10 of run 1 of FAST-3. Every request of a data check the pane makes, the old read and the raw read of card 1 alike, takes its
    // parameter sheet here, so the need sharedParameters reads the sheet the pane shows. The worker never looks for one itself, as a
    // config and a folder alone must say missing (R700)
    public static class DataCheckSheet
    {
        // The sheet the project settings picked, else the one config names, as the row Shared parameters of the pane shows it (R506). Null
        // when neither is on disk, as the runner refuses an input that is not. The empty SHA-256 is the one R600 allows a data check
        public static WorkerFile Input(ProjectSettings project, string configFile)
        {
            string sheet = null;
            if (project?.Inputs != null && project.Inputs.TryGetValue(InputNames.ParameterSheet, out SettingsInput picked))
            {
                sheet = picked?.Path;
            }
            if (string.IsNullOrEmpty(sheet) || !File.Exists(sheet))
            {
                sheet = OfConfig(configFile);
            }
            return !string.IsNullOrEmpty(sheet) && File.Exists(sheet) ? new WorkerFile { Role = InputNames.ParameterSheet, Path = Path.GetFullPath(sheet), Sha256 = string.Empty } : null;
        }

        private static string OfConfig(string configFile)
        {
            if (string.IsNullOrEmpty(configFile) || !File.Exists(configFile))
            {
                return null;
            }
            try
            {
                return SetUp.ParameterSheet.PathOf(configFile, JObject.Parse(WorkFiles.ReadText(configFile)));
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is InvalidDataException || failure is JsonException || failure is ArgumentException || failure is NotSupportedException)
            {
                return null;
            }
        }
    }
}
