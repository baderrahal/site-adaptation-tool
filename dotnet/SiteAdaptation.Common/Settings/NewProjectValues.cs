using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Settings
{
    // R795, R796, F8. The shipped set is the config and rules profile of the past job, so a new project would inherit its rules name, road surfaces,
    // schedules, profile file and position file. Empty project takes them through here: each past value is emptied, so the windows say not chosen yet
    // and the person picks it, the project name comes from the folder of the model. A key the shipped config does not hold stays absent. The past job
    // keeps its own files as they are, only the copy of a new project changes
    public static class NewProjectValues
    {
        // CHOSEN by Code. The file name of the rules profile of a new project, beside the place the shipped profile had
        public const string RulesName = "profile.json";

        // The old read takes no plot table of a past schedule. The pick of Set up names the road surfaces, a surface of the past job no longer stands for them
        private static readonly string[] EmptyLists = { "schedules", "designBasis.roadLevel.surfaces", "siteBuild.roadSurfaces", "majority.datumSurfaces" };
        private static readonly string[] EmptyBlocks = { "connect.neighbourhoods" };

        // Choose project folder writes inputRoot. The toposolid surface and the fit neighbourhood have no neutral value, null says not chosen
        private static readonly string[] NoValue = { "inputRoot", "dataBook.locationPlan.fitNeighbourhood", "pastJob.schedule", "floors.roadTopography.surface" };

        private const string Note = "R795. The settings of a new project, copied from the shipped set of the install with every value of the past job emptied: rules profile, road surfaces, schedules and position file. Choose project folder writes inputRoot";
        private const string NotMeasured = "Not measured";
        private const string NotMeasuredState = "notMeasured";

        // R795. The config of a new project from the shipped one. projectFolder is the folder of the copy under the work root, by its name
        // there, so the position file it names is its own and on no disk until a person makes one
        public static JObject Config(JObject shipped, string projectFolder)
        {
            JObject config = (JObject)shipped.DeepClone();
            if (config["profile"]?.Type == JTokenType.String)
            {
                config["profile"] = RulesPath((string)config["profile"]);
            }
            if (config["note"] != null)
            {
                config["note"] = Note;
            }
            foreach (string path in EmptyLists)
            {
                Replace(config, path, new JArray());
            }
            foreach (string path in EmptyBlocks)
            {
                Replace(config, path, new JObject());
            }
            foreach (string path in NoValue)
            {
                Replace(config, path, JValue.CreateNull());
            }
            // R799. Set up site checks the CAD against this file, the past file under the work root filled the Position file column of every new project
            Replace(config, "addin.position.file", WorkRoot.Mark + "/" + ShippedSet.ProjectsFolder + "/" + projectFolder + "/position/position.json");
            return config;
        }

        // R796. The rules profile of a new project: named by the project, and no rule says a past job measure. shippedProfile is the profile path the
        // shipped config names, the status block of R401 keys its entries by it, so they move to the path of the new profile
        public static JObject Rules(JObject shipped, string projectName, string shippedProfile)
        {
            JObject rules = (JObject)shipped.DeepClone();
            if (rules["project"] != null)
            {
                rules["project"] = projectName;
            }
            // CHOSEN by Code. The surfaces a key rule was measured against on the past models name past road surfaces, no build reads them
            foreach (JObject profile in ((rules["levelProfiles"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>()).Select(item => item.Value).OfType<JObject>())
            {
                foreach (JObject rule in ((profile["keyRules"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>()).Select(item => item.Value).OfType<JObject>())
                {
                    rule.Remove("candidates");
                }
            }
            JObject status = rules["status"] as JObject;
            JToken entries = shippedProfile == null ? null : status?[shippedProfile];
            if (entries != null)
            {
                status.Remove(shippedProfile);
                status[RulesPath(shippedProfile)] = entries;
            }
            foreach (JObject rule in ((rules["manualRules"] as JObject)?["rules"] as JArray ?? new JArray()).OfType<JObject>())
            {
                if (rule["pastJob"] != null)
                {
                    rule["pastJob"] = NotMeasured;
                }
                if (rule["pastJobState"] != null)
                {
                    rule["pastJobState"] = NotMeasuredState;
                }
            }
            return rules;
        }

        // The profile path of a new project for the profile path the shipped config names, in the same folder
        public static string RulesPath(string shippedProfile)
        {
            string folder = Path.GetDirectoryName(shippedProfile.Replace('\\', '/')) ?? string.Empty;
            return folder.Length == 0 ? RulesName : folder.Replace('\\', '/') + "/" + RulesName;
        }

        private static void Replace(JObject config, string path, JToken value)
        {
            string[] parts = path.Split('.');
            JObject block = config;
            for (int at = 0; at < parts.Length - 1 && block != null; at++)
            {
                block = block[parts[at]] as JObject;
            }
            if (block != null && block[parts[parts.Length - 1]] != null)
            {
                block[parts[parts.Length - 1]] = value;
            }
        }
    }
}
