using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R348 to R350. The settings of the reads of button 2 in config/project.json, where Phase 4c moved them from python/c3d_probe.json
    // (docs/addin-design.md 3.2 and section 6): the road edge layers of analysis, the ROW and setback layers of dataBook.join, the road
    // surfaces of siteBuild in their order with the snap, the sheets of dataBook.rev9, data5.areaKey and locationPlan with the workbook
    // role of each by addin.workbookRoles, and drawings.referenceOnly. A setting missing is refused with its name, nothing has a default
    public sealed class ReadSettings
    {
        private static readonly string[] CadExtensions = { ".dwg", ".dxf", ".dwl", ".dwl2", ".bak", ".dwt", ".dws", ".sv$" };
        private readonly JObject root;

        public ReadSettings(JObject root)
        {
            this.root = root;
        }

        public static ReadSettings Load(string path)
        {
            using (var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(WorkFiles.ReadText(path))) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
            {
                return new ReadSettings(JObject.Load(reader));
            }
        }

        public JToken Get(string dotted)
        {
            JToken node = root;
            foreach (string part in dotted.Split('.'))
            {
                node = (node as JObject)?[part];
                if (node == null)
                {
                    throw new InvalidDataException("config holds no " + dotted);
                }
            }
            return node;
        }

        private List<string> Texts(string dotted)
        {
            return ((JArray)Get(dotted)).Select(item => (string)item).ToList();
        }

        public List<string> CurbLayers => Texts("analysis.roadEdgeLayers");

        public List<string> RowLayers => Texts("dataBook.join.row.layers");

        public List<string> SetbackLayers => new List<string> { (string)Get("dataBook.join.footprint.setbackLayer") };

        // R349. The LandXML files of the road surfaces by the name of each with no extension, in the order a build asks them
        public List<string> RoadSurfaces => Texts("siteBuild.roadSurfaces");

        // R344, plan_rules.road_snap. siteBuild.snapM, the one distance a front corner may lie off a road surface and still take its
        // level, a finite number of 0 m or more and at most siteBuild.snapMaxM, else refused, so a wide snap never takes a road sideways
        public double SnapM
        {
            get
            {
                double? limit = Distance(Get("siteBuild.snapMaxM"));
                if (!limit.HasValue)
                {
                    throw new InvalidDataException("siteBuild.snapMaxM must be a finite number of 0 m or more, it holds " + Get("siteBuild.snapMaxM").ToString(Newtonsoft.Json.Formatting.None) + " (R344)");
                }
                double? snap = Distance(Get("siteBuild.snapM"));
                if (!snap.HasValue || snap.Value > limit.Value)
                {
                    throw new InvalidDataException("siteBuild.snapM must be a finite number from 0 m to snapMaxM " + limit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " m, it holds " + Get("siteBuild.snapM").ToString(Newtonsoft.Json.Formatting.None) + " (R344)");
                }
                return snap.Value;
            }
        }

        private static double? Distance(JToken token)
        {
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            {
                return null;
            }
            double value = token.Value<double>();
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0 ? (double?)null : value;
        }

        // The name a build gives a surface, plan_from_book.load_roads: the part of the file name after its last double underscore
        public static string RoadName(string stem)
        {
            int mark = stem.LastIndexOf("__", System.StringComparison.Ordinal);
            string name = mark < 0 ? "" : stem.Substring(mark + 2);
            return name.Length > 0 ? name : stem;
        }

        public sealed class SheetSetting
        {
            public string Role { get; set; }
            public string Sheet { get; set; }
            public int HeaderRow { get; set; }
            public List<KeyValuePair<string, string>> Columns { get; set; }
        }

        // R350. A sheet setting of dataBook, with the role of the input that holds its workbook
        public SheetSetting Sheet(string dotted)
        {
            JObject entry = (JObject)Get(dotted);
            return new SheetSetting
            {
                Role = WorkbookRole((string)entry["workbook"]),
                Sheet = (string)entry["sheet"],
                HeaderRow = entry["headerRow"].Value<int>(),
                Columns = ((JObject)entry["columns"]).Properties().Select(column => new KeyValuePair<string, string>(column.Name, (string)column.Value)).ToList(),
            };
        }

        // R350. The input role of a workbook name of dataBook.rev9.workbooks, dlus or sales, by addin.workbookRoles
        public string WorkbookRole(string workbook)
        {
            string role = (string)((Get("addin.workbookRoles") as JObject)?[workbook ?? ""]);
            if (role != "dlus" && role != "sales")
            {
                throw new InvalidDataException("addin.workbookRoles names no role dlus or sales for the workbook " + workbook + " (R350)");
            }
            return role;
        }

        // The sheet and the plot column of the sales rows the build reads, dataBook.locationPlan.sales, in the workbook of dataBook.rev9.sales
        public SheetSetting SalesRows()
        {
            JObject entry = (JObject)Get("dataBook.locationPlan.sales");
            return new SheetSetting
            {
                Role = WorkbookRole((string)Get("dataBook.rev9.sales.workbook")),
                Sheet = (string)entry["sheet"],
                HeaderRow = 1,
                Columns = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("plot", (string)entry["columns"]["plot"]) },
            };
        }

        private static string FileName(string name)
        {
            return Path.GetFileName((name ?? "").Replace('/', '\\').Split('\\').Last()).Trim().ToLowerInvariant();
        }

        private static string Stem(string name)
        {
            string stem = FileName(name);
            string extension;
            while ((extension = CadExtensions.FirstOrDefault(stem.EndsWith)) != null)
            {
                stem = stem.Substring(0, stem.Length - extension.Length).Trim();
            }
            return stem;
        }

        private static bool HoldsToken(string text, string token)
        {
            int start = text.IndexOf(token, System.StringComparison.Ordinal);
            while (start >= 0)
            {
                int end = start + token.Length;
                if ((start == 0 || !char.IsLetterOrDigit(text[start - 1])) && (end == text.Length || !char.IsLetterOrDigit(text[end])))
                {
                    return true;
                }
                start = text.IndexOf(token, start + 1, System.StringComparison.Ordinal);
            }
            return false;
        }

        // R300, project_profile.is_reference_only. True when the file name holds the full stem of a drawing of drawings.referenceOnly as a
        // whole token
        public bool IsReferenceOnly(string source)
        {
            JArray names = (root["drawings"] as JObject)?["referenceOnly"] as JArray;
            string name = FileName(source);
            return name.Length > 0 && (names ?? new JArray()).Select(item => Stem((string)item)).Where(stem => stem.Length > 0).Any(stem => HoldsToken(name, stem));
        }
    }
}
