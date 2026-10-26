using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R345. The step file of the C# side, the same form as python/parity.py step_file: its side and scope, and per plot the raw
    // values, every digit of a double kept, and the written values, each number of a field with decimals rounded by R385, a list of
    // points point by point. The worker writes it through WorkerContext.WriteJson, which adds the run stamp of its inputs (R347)
    public static class StepFile
    {
        public const string Format = "siteAdaptationStep";

        // 2 since the review of PR 50: files named by side, the scope, the run stamp and every configured field on every plot
        public const int FormatVersion = 2;

        // step_<step>_<side>.json, so a folder shared by the two sides never holds one file of both
        public static string FileName(string step, string side)
        {
            return "step_" + step + "_" + side + ".json";
        }

        // R345. The step files of the C# side of the steps a request names, deleted by the worker before it builds them and by the
        // launch when a run did not pass, so a run that failed leaves none and the parity finds it missing. A step file of an earlier
        // run may hold inputs that still hash the same. Never inside a git work tree, which the worker refuses before any write
        public static List<string> DeleteSteps(WorkerRequest request)
        {
            var deleted = new List<string>();
            if (request?.Steps == null || string.IsNullOrEmpty(request.StepsFolder) || WorkFiles.RepoAbove(request.StepsFolder) != null)
            {
                return deleted;
            }
            foreach (string step in request.Steps.Where(WorkerFiles.IsRunId))
            {
                string path = Path.Combine(request.StepsFolder, FileName(step, "csharp"));
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted.Add(path);
                }
            }
            return deleted;
        }

        // The scope as plan_from_book.py names it: project, nbh=<NBH> or plots=<numbers>
        public static string ScopeText(WorkerScope scope)
        {
            switch (scope?.Kind)
            {
                case "project":
                    return "project";
                case "nbh":
                    return "nbh=" + scope.Nbh;
                case "plots":
                    return "plots=" + string.Join(",", scope.Plots ?? new List<string>());
                default:
                    throw new InvalidDataException("the scope " + scope?.Kind + " is not project, nbh or plots");
            }
        }

        public static string Text(string step, string side, string scope, IDictionary<string, Dictionary<string, object>> plots, IReadOnlyList<ParityField> fields)
        {
            return Build(step, side, scope, plots, fields).ToString(Formatting.Indented);
        }

        public static JObject Build(string step, string side, string scope, IDictionary<string, Dictionary<string, object>> plots, IReadOnlyList<ParityField> fields)
        {
            var byName = fields.ToDictionary(field => field.Name);
            var fieldBlock = new JObject();
            foreach (ParityField field in fields)
            {
                var item = new JObject { ["kind"] = field.Kind };
                if (field.Decimals.HasValue)
                {
                    item["decimals"] = field.Decimals.Value;
                }
                fieldBlock[field.Name] = item;
            }
            var plotBlock = new JObject();
            foreach (KeyValuePair<string, Dictionary<string, object>> plot in plots)
            {
                var raw = new JObject();
                var written = new JObject();
                foreach (KeyValuePair<string, object> value in plot.Value)
                {
                    ParityField field;
                    int? decimals = byName.TryGetValue(value.Key, out field) ? field.Decimals : null;
                    raw[value.Key] = Token(value.Value, null);
                    written[value.Key] = Token(value.Value, decimals);
                }
                plotBlock[plot.Key] = new JObject { ["raw"] = raw, ["written"] = written };
            }
            return new JObject { ["format"] = Format, ["formatVersion"] = FormatVersion, ["step"] = step, ["side"] = side, ["scope"] = scope, ["fields"] = fieldBlock, ["plots"] = plotBlock };
        }

        // A double as its 17 digit text, which reads back as the same double on net48 and net8.0, rounded first when decimals are
        // named, and a list of numbers or of lists of numbers item by item
        private static JToken Token(object value, int? decimals)
        {
            if (value is double number)
            {
                return Number(number, decimals);
            }
            if (value is IEnumerable items && !(value is string))
            {
                var list = new JArray();
                foreach (object item in items)
                {
                    list.Add(Token(item, decimals));
                }
                return list;
            }
            return value == null ? JValue.CreateNull() : JToken.FromObject(value);
        }

        private static JToken Number(double value, int? decimals)
        {
            double shown = decimals.HasValue ? Rounding.Fixed(value, decimals.Value) : value;
            if (double.IsNaN(shown) || double.IsInfinity(shown))
            {
                return JValue.CreateNull();
            }
            return new JRaw(shown.ToString("G17", CultureInfo.InvariantCulture));
        }
    }
}
