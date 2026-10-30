using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Update
{
    // R402. The list the worker gives back as JSON (UpdateFromReport.ResultJson) read into an UpdateList again, so the add-in, which holds no
    // OpenXml, shows it and hands the ticked changes to Apply. A number of El @ Entry and FFL is a double, Steps a long and Mirror a bool as
    // Compare made them, whatever the JSON text of a whole number looks like
    public static class UpdateListJson
    {
        public static UpdateList Read(JObject json)
        {
            var list = new UpdateList { Refusal = Text(json["refusal"]) };
            foreach (JObject item in (json["changes"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string field = Text(item["field"]);
                var change = new UpdateChange
                {
                    Plot = Text(item["plot"]), Field = field, FileName = Text(item["file"]), Sheet = Text(item["sheet"]), Row = (int?)item["row"] ?? 0,
                    InModel = Cell(field, item["inModel"]), Invalid = Text(item["invalid"]), NotSavable = Text(item["notSavable"]), Ticked = (bool?)item["ticked"] ?? false,
                };
                change.InExcel = change.Invalid == null ? Cell(field, item["inExcel"]) : Plain(item["inExcel"]);
                change.Checks.AddRange((item["checks"] as JArray ?? new JArray()).Select(value => (string)value));
                change.Breaks.AddRange((item["breaks"] as JArray ?? new JArray()).Select(value => (string)value));
                change.ManualRules.AddRange((item["manualRules"] as JArray ?? new JArray()).Select(value => (string)value));
                list.Changes.Add(change);
            }
            foreach (JObject item in (json["ignored"] as JArray ?? new JArray()).OfType<JObject>())
            {
                list.Ignored.Add(new IgnoredCell
                {
                    Plot = Text(item["plot"]), FileName = Text(item["file"]), Sheet = Text(item["sheet"]), Column = Text(item["column"]), Row = (int?)item["row"] ?? 0,
                    InModel = Plain(item["inModel"]), InExcel = Plain(item["inExcel"]), Why = Text(item["why"]),
                });
            }
            list.Lacks.AddRange((json["lacks"] as JArray ?? new JArray()).Select(value => (string)value));
            return list;
        }

        private static string Text(JToken value)
        {
            return value == null || value.Type == JTokenType.Null ? null : (string)value;
        }

        private static object Plain(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                return null;
            }
            switch (value.Type)
            {
                case JTokenType.Integer:
                    return (long)value;
                case JTokenType.Float:
                    return (double)value;
                case JTokenType.Boolean:
                    return (bool)value;
                default:
                    return (string)value;
            }
        }

        private static object Cell(string field, JToken value)
        {
            object plain = Plain(value);
            if (plain == null)
            {
                return null;
            }
            if (field == UpdateText.Entry || field == UpdateText.Ffl)
            {
                return plain is long whole ? (double)whole : plain;
            }
            if (field == UpdateText.Steps)
            {
                return plain is double number ? (object)(long)Math.Round(number) : plain;
            }
            return plain;
        }
    }
}
