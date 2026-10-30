using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.CheckTable
{
    // R376. What the check table shows of a row, apart from the value the override list keeps. Common holds it so the net8.0 tests
    // reach it with no window
    public static class CheckShown
    {
        // The code names of the plan check rows: the field of a decision of the override list after its source, and the rule names
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "entry", CheckShownText.Entry },
            { "ffl", CheckShownText.Ffl },
            { "height", CheckShownText.Ffl },
            { "rampFront", CheckShownText.RampFront },
            { "rampFrontFall", CheckShownText.RampFrontFall },
            { "rampSlope", CheckShownText.RampType },
            { "linkOrigin", CheckShownText.LinkOrigin },
            { "linkRotation", CheckShownText.LinkRotation },
            { "yardOutline", CheckShownText.YardOutline },
            { "rampOutline", CheckShownText.RampOutline },
            { "generic", CheckShownText.GenericFloor },
            { "steps", CheckShownText.Steps },
            { "hand villa", CheckShownText.VillaHand },
            { "hand cluster", CheckShownText.ClusterHand },
            { "hand alone", CheckShownText.SingleHand },
            { "fittedOutlineRefit", CheckShownText.FittedYard },
            { "clusterSide", CheckShownText.ClusterSide },
            { "plotFrameRefit", CheckShownText.PlotFrame },
            { "blockMirrorRoadSide", CheckShownText.RoadSide },
            { "blockMirrorOpenEnd", CheckShownText.OpenEnd },
        };

        private static readonly Regex QuestionId = new Regex(@"^Q\d+$");
        private const string ParameterHead = "parameter ";
        private const string Slot = "<n>";
        private const string Digits = "0.#########";

        // The plain name of a code name, the code name itself where none is known. A decision of the override list is its source then its
        // field, as "parsons ffl", so a code name with no name of its own is tried again without its first word
        public static string RuleName(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "";
            }
            string text = code.Trim();
            string found = Known(text);
            int space = text.IndexOf(' ');
            if (found == null && space > 0)
            {
                found = Known(text.Substring(space + 1).TrimStart());
            }
            return found ?? text;
        }

        private static string Known(string text)
        {
            if (Names.TryGetValue(text, out string name))
            {
                return name;
            }
            if (QuestionId.IsMatch(text))
            {
                return CheckShownText.Question.Replace(Slot, text);
            }
            return text.StartsWith(ParameterHead, StringComparison.Ordinal) && text.Length > ParameterHead.Length
                ? CheckShownText.Parameter.Replace(Slot, text.Substring(ParameterHead.Length)) : null;
        }

        // A past value in words and units. A gap to the rule keeps its sign as above or below, a position gap as along the front and into
        // the plot, a rotation gap in degrees, a bool as the hand, any other value as the list writes it
        public static string Past(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                return "";
            }
            if (value.Type == JTokenType.Boolean)
            {
                return (bool)value ? CheckShownText.Mirrored : CheckShownText.NotMirrored;
            }
            if (value is JObject gap && gap.Count == 1)
            {
                JToken metres = gap["gapToRuleM"];
                if (metres is JArray along && along.Count == 2 && IsNumber(along[0]) && IsNumber(along[1]))
                {
                    return ReplaceFirst(ReplaceFirst(CheckShownText.Along, Number(along[0])), Number(along[1]));
                }
                if (IsNumber(metres))
                {
                    return Gap(metres, CheckShownText.Above, CheckShownText.Below);
                }
                if (IsNumber(gap["gapToRuleDeg"]))
                {
                    return Gap(gap["gapToRuleDeg"], CheckShownText.DegreesAbove, CheckShownText.DegreesBelow);
                }
            }
            return CheckTableState.Text(value);
        }

        private static bool IsNumber(JToken token)
        {
            return token != null && (token.Type == JTokenType.Float || token.Type == JTokenType.Integer);
        }

        private static string Number(JToken token)
        {
            return ((double)token).ToString(Digits, CultureInfo.InvariantCulture);
        }

        private static string Gap(JToken token, string above, string below)
        {
            double amount = (double)token;
            return amount == 0 ? CheckShownText.OnRule : (amount > 0 ? above : below).Replace(Slot, Math.Abs(amount).ToString(Digits, CultureInfo.InvariantCulture));
        }

        private static string ReplaceFirst(string text, string to)
        {
            int at = text.IndexOf(Slot, StringComparison.Ordinal);
            return at < 0 ? text : text.Substring(0, at) + to + text.Substring(at + Slot.Length);
        }
    }
}
