using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R632. What a person confirmed in the window, the file of the optional input role projectProfile. Nothing here is read from a past file,
    // a value the person did not confirm stays a proposal and changes no plot
    public sealed class ConfirmedProfile
    {
        public const string Role = "projectProfile";
        public const string PersonEntered = "person entered";
        public const string Left = "left";
        public const string Right = "right";

        public List<KeyValuePair<string, string>> CodeMap { get; } = new List<KeyValuePair<string, string>>();
        public Dictionary<string, string> Hands { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<int, string> Levels { get; } = new Dictionary<int, string>();
        public ConfirmedColumn Column { get; private set; }

        // A file the person made is never half taken: a value of the wrong shape refuses the run with a line that names it, so a run that
        // goes on has honoured all of it
        public static ConfirmedProfile From(JObject file, int levels)
        {
            var profile = new ConfirmedProfile();
            if (file["codeMap"] != null)
            {
                foreach (JToken row in List(file["codeMap"], "codeMap"))
                {
                    string old = Text(row, "old", "codeMap");
                    string now = Text(row, "new", "codeMap");
                    if (!profile.CodeMap.Any(pair => pair.Key == old && pair.Value == now))
                    {
                        profile.CodeMap.Add(new KeyValuePair<string, string>(old, now));
                    }
                }
            }
            if (file["hands"] != null)
            {
                var hands = file["hands"] as JObject ?? throw new InvalidDataException("the project profile holds hands that are no table of code and hand (R632)");
                foreach (JProperty hand in hands.Properties())
                {
                    string value = hand.Value.Type == JTokenType.String ? (string)hand.Value : null;
                    if (value != Left && value != Right)
                    {
                        throw new InvalidDataException("the project profile gives prototype " + hand.Name + " a hand that is not left or right (R632)");
                    }
                    profile.Hands[hand.Name] = value;
                }
            }
            if (file["levels"] != null)
            {
                foreach (JToken row in List(file["levels"], "levels"))
                {
                    JToken level = row is JObject ? row["level"] : null;
                    if (level == null || level.Type != JTokenType.Integer || (long)level < 1 || (long)level > levels)
                    {
                        throw new InvalidDataException("the project profile names a level that is not a whole number from 1 to " + levels + " (R632)");
                    }
                    profile.Levels[(int)level] = Text(row, "word", "levels");
                }
            }
            if (file["typologyColumn"] != null && file["typologyColumn"].Type != JTokenType.Null)
            {
                JToken column = file["typologyColumn"];
                profile.Column = new ConfirmedColumn
                {
                    Sheet = Text(column, "sheet", "typologyColumn"),
                    Column = Text(column, "column", "typologyColumn"),
                    Workbook = column["workbook"]?.Type == JTokenType.String ? (string)column["workbook"] : null,
                };
            }
            return profile;
        }

        private static JArray List(JToken token, string name)
        {
            return token as JArray ?? throw new InvalidDataException("the project profile holds " + name + " that is no list (R632)");
        }

        private static string Text(JToken row, string key, string name)
        {
            JToken value = row is JObject ? row[key] : null;
            if (value == null || value.Type != JTokenType.String || ((string)value).Trim().Length == 0)
            {
                throw new InvalidDataException("the project profile holds a row of " + name + " with no text for " + key + " (R632)");
            }
            return ((string)value).Trim();
        }
    }

    public sealed class ConfirmedColumn
    {
        public string Workbook { get; set; }
        public string Sheet { get; set; }
        public string Column { get; set; }
    }
}
