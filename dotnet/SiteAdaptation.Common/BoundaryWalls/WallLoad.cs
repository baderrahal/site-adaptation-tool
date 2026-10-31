using System;
using System.IO;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.PlaceVillas;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.BoundaryWalls
{
    // R380. The inputs of button 8 from the work folder, as FloorLoad takes those of 5 (Q190): the plan of NBH the last real run of 4 Check
    // wrote, held against the SHA-256 its check.json names, its walls, the level profile of that build, walls_<NBH>.json of the last real
    // run of 8 and majority.wallTypes.boundary of the config of the session, the pattern of the wall type BW
    public static class WallLoad
    {
        public const string BoundaryKey = "majority.wallTypes.boundary";

        public static WallInputs Walls(RunContext context, SettingsScope scope, string configPath)
        {
            Tuple<string, Func<string, bool>> chosen = FloorLoad.Scope(context, scope);
            string nbh = chosen.Item1;
            Tuple<string, string> named = VillaLoad.LastPlan(context.WorkFolder, nbh);
            PlanFile plan = PlanSource.Open(named.Item1, named.Item2);
            string lastPath = Path.Combine(context.WorkFolder, WallsRun.RecordName(nbh));
            JObject last = File.Exists(lastPath) ? JObject.Parse(WorkFiles.ReadText(lastPath)) : null;
            return new WallInputs
            {
                Plan = plan, Walls = WallPlan.Parse(JObject.Parse(WorkFiles.ReadText(named.Item1))), PlanPath = named.Item1, Profile = FloorLoad.Request(named.Item1).LevelProfile,
                InScope = chosen.Item2, BoundaryType = BoundaryType(configPath), LastPlaced = WallsRun.Placed(last),
            };
        }

        public static string BoundaryType(string configPath)
        {
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                throw new InvalidDataException("the settings name no config file on disk, so Walls has no " + BoundaryKey + ", the pattern of the wall type BW (R380)");
            }
            string pattern = (string)(JObject.Parse(WorkFiles.ReadText(configPath))["majority"]?["wallTypes"]?["boundary"] as JValue);
            if (string.IsNullOrEmpty(pattern))
            {
                throw new InvalidDataException("the config " + Path.GetFileName(configPath) + " holds no " + BoundaryKey + ", the pattern of the wall type BW (R380)");
            }
            return pattern;
        }
    }
}
