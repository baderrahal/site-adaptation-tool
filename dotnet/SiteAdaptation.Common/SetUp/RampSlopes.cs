using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.SetUp
{
    // R373, docs/design/13-workflow-changes.md Set up site. The row Ramp slope: its choices in percent, its default from the rules
    // profile and the floor type of each slope
    public static class RampSlopes
    {
        // The choices are not in the rules profile yet and rules/ is Lane A's file. Join item W1 of steps/lane-b.md moves this list
        // beside rampTypes of levelProfiles.parsons2024Rev6, then it is read from there
        public static readonly IReadOnlyList<double> Choices = new[] { 0.5, 1.0, 2.0 }.Concat(Enumerable.Range(3, 13).Select(step => (double)step)).ToList();

        // levelProfiles.<profile>.rampSlopePercent.default of the rules profile
        public static double Default(JObject rules, string levelProfile)
        {
            JObject profile = Profile(rules, levelProfile);
            JToken value = (profile["rampSlopePercent"] as JObject)?["default"];
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer))
            {
                throw new InvalidDataException("the level profile holds no number rampSlopePercent.default, the default of the row Ramp slope (R373)");
            }
            double slope = value.Value<double>();
            if (!Choices.Contains(slope))
            {
                throw new InvalidDataException("rampSlopePercent.default " + Text(slope) + " is none of the choices of the row Ramp slope (R373)");
            }
            return slope;
        }

        // The type rampTypes of the profile names for the slope, else Ramps <slope>% as rampTypes names its own, "Ramps 0.5%"
        public static string TypeName(JObject rules, string levelProfile, double slope)
        {
            JObject types = Profile(rules, levelProfile)["rampTypes"] as JObject;
            JToken named = types?[Text(slope)];
            return named != null && named.Type == JTokenType.String ? (string)named : "Ramps " + Text(slope) + "%";
        }

        public static string Text(double slope)
        {
            return slope.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static JObject Profile(JObject rules, string levelProfile)
        {
            JObject profiles = rules["levelProfiles"] as JObject ?? throw new InvalidDataException("the rules profile holds no levelProfiles (R277)");
            string name = string.IsNullOrEmpty(levelProfile) ? (string)profiles["default"] : levelProfile;
            return (name == null ? null : profiles[name] as JObject) ?? throw new InvalidDataException("the rules profile holds no level profile " + (name ?? "default") + " (R277)");
        }
    }
}
