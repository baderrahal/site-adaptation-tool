using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R348 to R350. The read steps of the parity harness on the C# side, the C# of python/read_steps.py: per item of the read of button 2,
    // the values the Python readers it replaces give. The DWG steps take the outputs of the worker read as written by R2, as the Python
    // side takes the CadReader outputs, so both sides start from the same numbers of the drawing
    public static class ReadSteps
    {
        public const string Labels = "labels";
        public const string Blocks = "blocks";
        public const string Curbs = "curbs";
        public const string Rows = "rows";
        public const string Setbacks = "setbacks";
        public const string Surfaces = "surfaces";
        public const string RoadLevels = "roadLevels";
        public const string AreaKey = "areaKey";
        public const string DlusSetbacks = "dlusSetbacks";
        public const string Sales = "sales";

        public static readonly string[] All = { Labels, Blocks, Curbs, Rows, Setbacks, Surfaces, RoadLevels, AreaKey, DlusSetbacks, Sales };

        private static double Number(JToken token)
        {
            return token.Value<double>();
        }

        private static double[] Point(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? null : ((JArray)token).Select(Number).ToArray();
        }

        private static List<double[]> Points(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? null : ((JArray)token).Select(Point).ToList();
        }

        private static double? Single(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? (double?)null : Number(token);
        }

        // A value of no geometry as the read holds it, so its type stays that of the JSON on both sides
        private static JToken Kept(JToken token)
        {
            return token == null ? JValue.CreateNull() : token.DeepClone();
        }

        private static string Text(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        // R348. Per label of the plot shapes, as the read spells it, the rings, label types and areas of its shapes in the order read and
        // whether yard_rule.whole_label takes it (read_steps.label_step)
        public static Dictionary<string, Dictionary<string, object>> LabelStep(JArray plots)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (JToken item in plots)
            {
                if (Text(item["kind"]) != "plot_shape")
                {
                    continue;
                }
                string label = Text(item["plot"]) ?? "";
                Dictionary<string, object> group;
                if (!found.TryGetValue(label, out group))
                {
                    group = new Dictionary<string, object>
                    {
                        ["rings"] = new List<List<double[]>>(), ["types"] = new List<JToken>(), ["areas"] = new List<object>(), ["whole"] = FaceStep.WholeLabel(label) != null,
                    };
                    found[label] = group;
                }
                ((List<List<double[]>>)group["rings"]).Add((item["vertices"] as JArray ?? new JArray()).Select(vertex => new[] { Number(vertex[0]), Number(vertex[1]) }).ToList());
                ((List<JToken>)group["types"]).Add(Kept(item["type"]));
                ((List<object>)group["areas"]).Add(Single(item["area"]));
            }
            return found;
        }

        // The items by their handle, a handle met again named handle#n by its count, in the order read (read_steps.by_handle)
        private static Dictionary<string, Dictionary<string, object>> ByHandle(IEnumerable<KeyValuePair<string, Dictionary<string, object>>> items)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            var counts = new Dictionary<string, int>();
            foreach (KeyValuePair<string, Dictionary<string, object>> item in items)
            {
                string handle = item.Key ?? "";
                int count;
                counts.TryGetValue(handle, out count);
                counts[handle] = ++count;
                found[count == 1 ? handle : handle + "#" + count] = item.Value;
            }
            return found;
        }

        // R348. Per block insert its name, layer, mirror flag and scale, its insert point, rotation and outline (read_steps.block_step)
        public static Dictionary<string, Dictionary<string, object>> BlockStep(JArray blocks)
        {
            return ByHandle(blocks.Select(block => new KeyValuePair<string, Dictionary<string, object>>(Text(block["handle"]), new Dictionary<string, object>
            {
                ["name"] = Kept(block["name"]), ["layer"] = Kept(block["layer"]), ["mirrored"] = Kept(block["mirrored"]), ["scale"] = Kept(block["scale"]),
                ["insertPoint"] = Point(block["insertPoint"]), ["rotationDeg"] = Single(block["rotationDeg"]), ["outline"] = Points(block["outline"]),
            })));
        }

        // R348, cad_analysis.road_edge_pieces. A polyline or line of a layer named, or of that layer inside a bound xref named
        // <xref>$<n>$<layer>, without case, with the points of its line, closed when the polyline is closed, kept with two points or more
        public static List<KeyValuePair<JToken, List<double[]>>> EdgePieces(IEnumerable<JToken> pieces, IEnumerable<string> layers)
        {
            List<string> wanted = (layers ?? Enumerable.Empty<string>()).ToList();
            if (wanted.Count == 0)
            {
                throw new InvalidDataException("analysis.roadEdgeLayers names no layer");
            }
            var found = new List<KeyValuePair<JToken, List<double[]>>>();
            foreach (JToken piece in pieces)
            {
                if (!IsEdge(Text(piece["kind"]), Text(piece["layer"]), wanted))
                {
                    continue;
                }
                List<double[]> points = (piece["pts"] as JArray ?? new JArray()).Where(point => point is JArray pair && pair.Count >= 2)
                    .Select(point => new[] { Number(point[0]), Number(point[1]) }).ToList();
                JToken closed = piece["closed"];
                if (closed != null && closed.Type == JTokenType.Boolean && (bool)closed && points.Count >= 3)
                {
                    points.Add(points[0]);
                }
                if (points.Count >= 2)
                {
                    found.Add(new KeyValuePair<JToken, List<double[]>>(piece, points));
                }
            }
            return found;
        }

        // A polyline or a line of one of the layers, without case, or of that layer inside a bound xref, <xref>$<n>$<layer>
        public static bool IsEdge(string kind, string layer, IEnumerable<string> layers)
        {
            string lower = (layer ?? "").ToLowerInvariant();
            return (kind == "polyline" || kind == "line") && layers.Select(name => (name ?? "").ToLowerInvariant()).Any(name => lower == name || lower.EndsWith("$" + name, StringComparison.Ordinal));
        }

        // R348. Per piece of the edge layers, by its place in the read, its handle, layer, kind and closed flag and the points of its line
        public static Dictionary<string, Dictionary<string, object>> EdgeStep(IEnumerable<JToken> pieces, IEnumerable<string> layers)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            int index = 0;
            foreach (KeyValuePair<JToken, List<double[]>> item in EdgePieces(pieces, layers))
            {
                found[(index++).ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object>
                {
                    ["handle"] = Kept(item.Key["handle"]), ["layer"] = Kept(item.Key["layer"]), ["kind"] = Kept(item.Key["kind"]), ["closed"] = Kept(item.Key["closed"]), ["points"] = item.Value,
                };
            }
            return found;
        }

        // R349. Per surface of the LandXML reads in the order of the files, its file, unit and counts, bounds and level range
        public static Dictionary<string, Dictionary<string, object>> SurfaceStep(IEnumerable<LandXmlFile> files)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (LandXmlFile file in files)
            {
                foreach (LandXmlSurface surface in file.Surfaces)
                {
                    if (found.ContainsKey(surface.Name))
                    {
                        throw new InvalidDataException("two road surfaces are named " + surface.Name + ", a surface is known by its name");
                    }
                    double[] low = null, high = null;
                    double? zMin = null, zMax = null;
                    for (int point = 0; point < surface.PointCount; point++)
                    {
                        double x = surface.Points[point * 3], y = surface.Points[point * 3 + 1], z = surface.Points[point * 3 + 2];
                        low = low == null ? new[] { x, y } : new[] { Math.Min(low[0], x), Math.Min(low[1], y) };
                        high = high == null ? new[] { x, y } : new[] { Math.Max(high[0], x), Math.Max(high[1], y) };
                        zMin = zMin.HasValue ? Math.Min(zMin.Value, z) : z;
                        zMax = zMax.HasValue ? Math.Max(zMax.Value, z) : z;
                    }
                    found[surface.Name] = new Dictionary<string, object>
                    {
                        ["file"] = file.File, ["linearUnit"] = file.LinearUnit, ["epsgCode"] = file.EpsgCode, ["pointCount"] = surface.PointCount, ["faceCount"] = surface.FaceCount,
                        ["visibleFaceCount"] = surface.VisibleFaceCount, ["invisibleFaceCount"] = surface.InvisibleFaceCount, ["badFaceCount"] = surface.BadFaceCount,
                        ["badPointCount"] = surface.BadPointCount,
                        ["declaredZRange"] = surface.DeclaredSeen ? new JObject { ["zMin"] = surface.DeclaredZMin, ["zMax"] = surface.DeclaredZMax } : (JToken)JValue.CreateNull(),
                        ["bboxMin"] = low, ["bboxMax"] = high, ["zMin"] = zMin, ["zMax"] = zMax,
                    };
                }
            }
            return found;
        }

        // The rings of the baseplan face of each plot number: the plot shapes of three vertices or more of a whole label, numbered by
        // plots.csv, a later label of one number over an earlier one (read_steps.face_rings)
        public static Dictionary<string, List<double[][]>> FaceRings(JArray plots, IEnumerable<Dictionary<string, string>> plotsCsv)
        {
            var rings = new Dictionary<string, List<double[][]>>();
            var order = new List<string>();
            foreach (JToken item in plots)
            {
                string label = Text(item["kind"]) == "plot_shape" ? FaceStep.WholeLabel(Text(item["plot"])) : null;
                JArray vertices = item["vertices"] as JArray ?? new JArray();
                if (label == null || vertices.Count < 3)
                {
                    continue;
                }
                if (!rings.ContainsKey(label))
                {
                    rings[label] = new List<double[][]>();
                    order.Add(label);
                }
                rings[label].Add(vertices.Select(vertex => new[] { Number(vertex[0]), Number(vertex[1]) }).ToArray());
            }
            Dictionary<string, string> numberOf = FaceStep.NumberOfLabel(plotsCsv);
            var found = new Dictionary<string, List<double[][]>>();
            foreach (string label in order)
            {
                string number;
                if (numberOf.TryGetValue(label, out number))
                {
                    found[number] = rings[label];
                }
            }
            return found;
        }

        // R349. Per vertex of the face of each plot, <plot>/<k> in the order of its rings, the road level of the first surface of the
        // order that covers it, snapped within snap, and the surface that gave it (read_steps.road_level_step)
        public static Dictionary<string, Dictionary<string, object>> RoadLevelStep(IEnumerable<string> numbers, IReadOnlyDictionary<string, List<double[][]>> faces, IReadOnlyList<TinSurface> surfaces, double snap)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                List<double[][]> rings;
                if (!faces.TryGetValue(number, out rings))
                {
                    continue;
                }
                int index = 0;
                foreach (double[] vertex in rings.SelectMany(ring => ring))
                {
                    KeyValuePair<double?, string> level = TinSurface.RoadLevel(surfaces, vertex[0], vertex[1], snap);
                    found[number + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object>
                    {
                        ["point"] = new[] { vertex[0], vertex[1] }, ["level"] = level.Key, ["surface"] = level.Value,
                    };
                    index++;
                }
            }
            return found;
        }

        // R350. Per plot of the DLUS or the sales sheet, the zone and the neighbourhood of each and two rows that differ (area_key.by_plot)
        public static Dictionary<string, Dictionary<string, object>> AreaKeyStep(SheetRows.AreaKey dlus, SheetRows.AreaKey sales)
        {
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string plot in dlus.Order.Concat(sales.Order.Where(plot => !dlus.Values.ContainsKey(plot))))
            {
                KeyValuePair<string, string> own, other;
                bool inDlus = dlus.Values.TryGetValue(plot, out own), inSales = sales.Values.TryGetValue(plot, out other);
                found[plot] = new Dictionary<string, object>
                {
                    ["inDlus"] = inDlus, ["zone"] = inDlus ? own.Key : null, ["nbhId"] = inDlus ? own.Value : null, ["twoRows"] = dlus.TwoRows.Contains(plot),
                    ["inSales"] = inSales, ["salesZone"] = inSales ? other.Key : null, ["salesNbhId"] = inSales ? other.Value : null, ["salesTwoRows"] = sales.TwoRows.Contains(plot),
                };
            }
            return found;
        }

        // The plot of a DLUS row as rev9_workbooks.setbacks joins it: a number by Python int, every other value as its text
        public static string SetbackKey(JToken plot)
        {
            object value = SheetRows.Cell(plot);
            switch (value)
            {
                case double number:
                    return SheetRows.PlotText(Math.Truncate(number));
                case bool flag:
                    return flag ? "1" : "0";
                default:
                    return SheetRows.PyText(value);
            }
        }

        // R350. Per row of the DLUS setback sheet, by its plot, its plot cell and every other column of the config (sheet_rows)
        public static Dictionary<string, Dictionary<string, object>> DlusSetbackStep(IEnumerable<JObject> rows)
        {
            return ByHandle(rows.Select(item =>
            {
                var fields = new Dictionary<string, object> { ["plotCell"] = Kept(item["plot"]) };
                foreach (JProperty column in item.Properties().Where(column => column.Name != "plot"))
                {
                    fields[column.Name] = Kept(column.Value);
                }
                return new KeyValuePair<string, Dictionary<string, object>>(SetbackKey(item["plot"]), fields);
            }));
        }

        // R350. Per plot of the sales sheet its row as corner_front.sales_rows gives it and the plain values of the columns of
        // dataBook.rev9.sales as sheet_rows gives them, the last row of the plot (read_steps.sales_step)
        public static Dictionary<string, Dictionary<string, object>> SalesStep(JObject rows, IEnumerable<JObject> plainRows)
        {
            var plain = new Dictionary<string, JObject>();
            foreach (JObject item in plainRows)
            {
                string number = SheetRows.PlotNumber(SheetRows.Cell(item["plot"]));
                if (number != null)
                {
                    plain[number] = item;
                }
            }
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (JProperty row in rows.Properties())
            {
                JObject held;
                found[row.Name] = new Dictionary<string, object> { ["row"] = row.Value.DeepClone(), ["plain"] = plain.TryGetValue(row.Name, out held) ? held.DeepClone() : JValue.CreateNull() };
            }
            return found;
        }
    }
}
