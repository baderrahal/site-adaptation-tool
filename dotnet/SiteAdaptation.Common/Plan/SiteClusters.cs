using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R354. A cluster of a build: its code, its plots as the build takes them and its source, footprint or analysis
    public sealed class SiteCluster
    {
        public string Code { get; set; }

        public List<string> Plots { get; set; }

        public string Source { get; set; }
    }

    // R354, R236. The clusters a build by keyRules takes, as plan_from_book.site_clusters gives them: the footprint clusters over every unit
    // plot, the plots that share one Building Footprint block of the name plots.csv wires to them, the block of that name that shares the
    // most of each face, a later block of the read taking a face only by a larger share, then each cluster of the analysis less the plots a
    // footprint cluster holds, a face of two plan numbers left out, each of two plots or more. The analysis is the file plan_from_book.py
    // takes by --analysis, held to the baseplan drawing it claims to be of (Q089)
    public static class SiteClusters
    {
        public const string StepName = "clusters";
        public const string FootprintSource = "footprint";
        public const string AnalysisSource = "analysis";

        // A block of no name is held under a key no row can name
        private const string NoName = "\u0000";

        // footprint_check.block_shapes: the outline of every block of three points or more by its name, in the order of the read, after
        // simple_blocks has refused a block whose cleaned outline of three points or more is not a simple ring, the first in the read
        public static Dictionary<string, List<double[][]>> BlocksByName(JArray blocks)
        {
            foreach (JToken block in blocks)
            {
                List<double[]> ring = FaceRings.Clean((block["outline"] as JArray ?? new JArray()).Select(point => new[] { BuildFace.Number(point[0]), BuildFace.Number(point[1]) }));
                if (ring.Count >= 3 && !FaceRings.IsSimple(ring))
                {
                    throw new InvalidDataException("the footprint block " + PyText(block["name"]) + " at " + PyText(block["insertPoint"])
                        + " is not a simple ring, two of its edges meet, shapely would mend it by buffer(0) and the C# would not, so the build refuses it (R354)");
                }
            }
            var found = new Dictionary<string, List<double[][]>>();
            foreach (JToken block in blocks)
            {
                JArray outline = block["outline"] as JArray;
                if (outline == null || outline.Count < 3)
                {
                    continue;
                }
                string name = block["name"] == null || block["name"].Type == JTokenType.Null ? NoName : block["name"].ToString();
                if (!found.ContainsKey(name))
                {
                    found[name] = new List<double[][]>();
                }
                found[name].Add(FaceRings.Clean(outline.Select(point => new[] { BuildFace.Number(point[0]), BuildFace.Number(point[1]) })).ToArray());
            }
            return found;
        }

        // R354, R337. A value as Python %s writes it in a message, str() of what json.loads gives: None, a text as it is, anything else by PyRepr, so a
        // list of texts is ['a', 'b'] as in Python and never [a, b] or JSON over several lines (F4 of the third review of PR 55)
        public static string PyText(JToken token)
        {
            return token != null && token.Type == JTokenType.String ? (string)token : PyRepr(token);
        }

        // R354, R337. repr() of Python of what json.loads gives: None, True and False, a float by its shortest digits, a text in quotes, a list as
        // [a, b] and an object as {'k': v}, each item by repr
        public static string PyRepr(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return "None";
            }
            switch (token.Type)
            {
                case JTokenType.Array:
                    return "[" + string.Join(", ", ((JArray)token).Select(PyRepr)) + "]";
                case JTokenType.Object:
                    return "{" + string.Join(", ", ((JObject)token).Properties().Select(item => TextRepr(item.Name) + ": " + PyRepr(item.Value))) + "}";
                case JTokenType.Float:
                    return SheetRows.PyRepr((double)token);
                case JTokenType.Boolean:
                    return (bool)token ? "True" : "False";
                case JTokenType.String:
                    return TextRepr((string)token);
                default:
                    return token.ToString();
            }
        }

        // R354, R337. repr() of a Python text: in single quotes, or double ones when it holds a single quote and no double one, the quote, the backslash,
        // a tab, a line feed and a carriage return escaped, another control character as \xNN or \uNNNN
        public static string TextRepr(string text)
        {
            char quote = text.IndexOf('\'') >= 0 && text.IndexOf('"') < 0 ? '"' : '\'';
            var built = new System.Text.StringBuilder().Append(quote);
            foreach (char letter in text)
            {
                if (letter == quote || letter == '\\')
                {
                    built.Append('\\').Append(letter);
                }
                else if (letter == '\t')
                {
                    built.Append("\\t");
                }
                else if (letter == '\n')
                {
                    built.Append("\\n");
                }
                else if (letter == '\r')
                {
                    built.Append("\\r");
                }
                else if (char.IsControl(letter))
                {
                    built.Append(letter < 0x100 ? "\\x" + ((int)letter).ToString("x2") : "\\u" + ((int)letter).ToString("x4"));
                }
                else
                {
                    built.Append(letter);
                }
            }
            return built.Append(quote).ToString();
        }

        // plan_from_book.analysis_clusters: the clusters of the analysis a build joins, refused when its source is not the baseplan drawing
        // drawings.baseplan names, compared as written, or it holds no clusters list. A plot that is no text is kept as null, which no
        // label numbers, as a Python dict of texts never holds a number
        public static List<SiteCluster> AnalysisOf(JObject analysis, string fileName, string baseplanName)
        {
            JToken source = analysis["source"];
            if (source == null || source.Type != JTokenType.String || (string)source != baseplanName)
            {
                throw new InvalidDataException(fileName + " is of " + PyText(source) + ", the analysis a build joins is of the baseplan drawings.baseplan names, " + baseplanName + " (R354)");
            }
            if (!(analysis["clusters"] is JArray clusters))
            {
                throw new InvalidDataException(fileName + " holds no clusters list, the clusters of the analysis a build joins (R354)");
            }
            return clusters.Select(cluster => new SiteCluster
            {
                Code = cluster["code"] == null || cluster["code"].Type == JTokenType.Null ? null : cluster["code"].ToString(),
                Plots = (cluster["plots"] as JArray ?? new JArray()).Select(plot => plot.Type == JTokenType.String ? (string)plot : null).ToList(),
                Source = AnalysisSource,
            }).ToList();
        }

        // R354, R2. Half a unit of the 3 decimals cad_analysis writes the rotation of a block at
        public const double AnalysisTurn = 0.0005;

        // plan_from_book.hold_analysis, Q089. The analysis records the name of its drawing and no hash or date of it, so it is held to the
        // reads it was made from by what it records of them: summary.plotFaces is the count of the plot faces of the baseplan read, each plot
        // of its plots list lies on the face of its label there with its type, cadType, and each plot it gives a block name lies on a block of
        // that name in the footprint read, the one that shares the most of its face, at its rotation within AnalysisTurn by the turn. With no
        // footprint read, blocks null, the blocks are not held
        public static void HoldAnalysis(JObject analysis, string fileName, JArray baseplanPlots, JArray blocks)
        {
            JToken counted = (analysis["summary"] as JObject)?["plotFaces"];
            if (!(analysis["plots"] is JArray records) || counted == null || counted.Type != JTokenType.Integer)
            {
                throw new InvalidDataException(fileName + " holds no plots list or no summary.plotFaces, which hold the analysis a build joins to the reads it was made from (R354)");
            }
            var faces = new Dictionary<string, JObject>(StringComparer.Ordinal);
            long count = 0;
            foreach (JObject entry in (baseplanPlots ?? new JArray()).OfType<JObject>())
            {
                JToken plot = entry["plot"];
                if (entry["kind"]?.Type == JTokenType.String && (string)entry["kind"] == "plot_shape" && plot != null && plot.Type != JTokenType.Null
                    && !(plot.Type == JTokenType.String && (string)plot == "") && entry["vertices"] is JArray vertices && vertices.Count >= 3)
                {
                    faces[PyText(plot)] = entry;
                    count++;
                }
            }
            string countedText = Convert.ToString(((JValue)counted).Value, System.Globalization.CultureInfo.InvariantCulture);
            if (countedText != count.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                throw new InvalidDataException(fileName + " counts " + countedText + " plot faces and the baseplan read holds " + count + ", the analysis a build joins is of the reads it was made from (R354)");
            }
            var off = new List<JToken>();
            foreach (JToken record in records)
            {
                JToken label = (record as JObject)?["plot"];
                if (label == null || label.Type != JTokenType.String || !faces.ContainsKey((string)label) || !Same(faces[(string)label]["type"], record["cadType"]))
                {
                    off.Add(label);
                }
            }
            if (off.Count > 0)
            {
                throw new InvalidDataException(off.Count + " plots of " + fileName + " lie on no face of their label or on one of another type in the baseplan read: "
                    + string.Join(", ", off.Take(10).Select(PyText)) + ", the analysis a build joins is of the reads it was made from (R354)");
            }
            if (blocks == null)
            {
                return;
            }
            var shapes = new Dictionary<string, List<Tuple<double[][], JToken>>>(StringComparer.Ordinal);
            foreach (JToken block in blocks)
            {
                if (block["outline"] is JArray outline && outline.Count >= 3 && block["name"]?.Type == JTokenType.String)
                {
                    string name = (string)block["name"];
                    if (!shapes.ContainsKey(name))
                    {
                        shapes[name] = new List<Tuple<double[][], JToken>>();
                    }
                    shapes[name].Add(Tuple.Create(FaceRings.Clean(outline.Select(point => new[] { BuildFace.Number(point[0]), BuildFace.Number(point[1]) })).ToArray(), block));
                }
            }
            foreach (JToken record in records)
            {
                JToken named = record["block"];
                if (named == null || named.Type != JTokenType.String || (string)named == "")
                {
                    continue;
                }
                string label = (string)record["plot"];
                BuildFace face = BuildFace.Of(label, ((JArray)faces[label]["vertices"]).Select(vertex => new[] { BuildFace.Number(vertex[0]), BuildFace.Number(vertex[1]) }).ToArray());
                JToken best = null;
                double held = 0.0;
                if (shapes.TryGetValue((string)named, out List<Tuple<double[][], JToken>> candidates))
                {
                    foreach (Tuple<double[][], JToken> candidate in candidates)
                    {
                        double share = Overlap(candidate.Item1, face);
                        if (share > held)
                        {
                            best = candidate.Item2;
                            held = share;
                        }
                    }
                }
                JToken turn = record["rotation"];
                JToken rotation = best?["rotationDeg"];
                bool numberTurn = turn != null && (turn.Type == JTokenType.Integer || turn.Type == JTokenType.Float);
                bool numberRotation = rotation != null && (rotation.Type == JTokenType.Integer || rotation.Type == JTokenType.Float);
                if (best == null || (numberTurn && !(numberRotation && Math.Abs(PyMath.Mod(BuildFace.Number(rotation) - BuildFace.Number(turn) + 180.0, 360.0) - 180.0) <= AnalysisTurn)))
                {
                    off.Add(record["plot"]);
                }
            }
            if (off.Count > 0)
            {
                throw new InvalidDataException(off.Count + " plots of " + fileName + " lie on no block of their name or on one of another rotation in the footprint read: "
                    + string.Join(", ", off.Take(10).Select(PyText)) + ", the analysis a build joins is of the reads it was made from (R354)");
            }
        }

        // Two values of a JSON file the same as Python == takes them, a missing value as None
        private static bool Same(JToken a, JToken b)
        {
            bool noA = a == null || a.Type == JTokenType.Null, noB = b == null || b.Type == JTokenType.Null;
            return noA || noB ? noA && noB : JToken.DeepEquals(a, b);
        }

        // plan_from_book.footprint_clusters over the unit plots
        public static List<SiteCluster> Footprint(IEnumerable<string> units, IReadOnlyDictionary<string, Dictionary<string, string>> rows, IReadOnlyDictionary<string, BuildFace> faces,
            IReadOnlyDictionary<string, List<double[][]>> blocks)
        {
            var groups = new Dictionary<Tuple<string, int>, List<string>>();
            foreach (string number in units)
            {
                Dictionary<string, string> row;
                BuildFace face;
                string name;
                if (!rows.TryGetValue(number, out row) || !faces.TryGetValue(number, out face) || !row.TryGetValue("footprintBlock", out name) || string.IsNullOrEmpty(name))
                {
                    continue;
                }
                List<double[][]> shapes;
                if (!blocks.TryGetValue(name, out shapes))
                {
                    continue;
                }
                Tuple<string, int> best = null;
                double held = 0.0;
                for (int index = 0; index < shapes.Count; index++)
                {
                    double share = Overlap(shapes[index], face);
                    if (share > held)
                    {
                        best = Tuple.Create(name, index);
                        held = share;
                    }
                }
                if (best != null)
                {
                    if (!groups.ContainsKey(best))
                    {
                        groups[best] = new List<string>();
                    }
                    groups[best].Add(number);
                }
            }
            return groups.OrderBy(item => item.Key.Item1, StringComparer.Ordinal).ThenBy(item => item.Key.Item2).Where(item => item.Value.Count > 1)
                .Select(item => new SiteCluster { Code = item.Key.Item1, Plots = item.Value.OrderBy(plot => plot, PyMath.SortKey).ToList(), Source = FootprintSource }).ToList();
        }

        // The area a block outline shares with a face, by the slabs of PolygonOverlap, 0 for outlines whose boxes lie apart or of under three points
        private static double Overlap(double[][] outline, BuildFace face)
        {
            if (outline.Length < 3 || outline.Min(point => point[0]) > face.High[0] || outline.Max(point => point[0]) < face.Low[0]
                || outline.Min(point => point[1]) > face.High[1] || outline.Max(point => point[1]) < face.Low[1])
            {
                return 0.0;
            }
            return PolygonOverlap.Area(face.Ring, outline);
        }

        // plan_from_book.numbered_clusters: the plots of each cluster of the analysis by their location plan numbers, a plot with none left out
        public static List<SiteCluster> Numbered(IEnumerable<SiteCluster> clusters, IReadOnlyDictionary<string, string> numberOf)
        {
            var found = new List<SiteCluster>();
            foreach (SiteCluster cluster in clusters)
            {
                List<string> plots = cluster.Plots.Where(label => label != null && numberOf.ContainsKey(label)).Select(label => numberOf[label]).ToList();
                if (plots.Count > 0)
                {
                    found.Add(new SiteCluster { Code = cluster.Code, Plots = plots, Source = cluster.Source });
                }
            }
            return found;
        }

        // plan_from_book.joined_clusters: the footprint clusters, then each cluster of the analysis less the plots they hold, of two or more
        public static List<SiteCluster> Joined(List<SiteCluster> footprint, IEnumerable<SiteCluster> analysis)
        {
            var taken = new HashSet<string>(footprint.SelectMany(cluster => cluster.Plots));
            var rest = analysis.Select(cluster => new SiteCluster { Code = cluster.Code, Plots = cluster.Plots.Where(plot => !taken.Contains(plot)).ToList(), Source = cluster.Source })
                .Where(cluster => cluster.Plots.Count > 1);
            return footprint.Concat(rest).ToList();
        }

        // plan_from_book.site_clusters: blocks or analysis may be null, as a build with no --footprint-blocks or no --analysis takes none
        public static List<SiteCluster> Site(IEnumerable<string> units, IReadOnlyDictionary<string, Dictionary<string, string>> rows, IReadOnlyDictionary<string, BuildFace> faces,
            IReadOnlyDictionary<string, List<double[][]>> blocks, IEnumerable<SiteCluster> analysis, IReadOnlyDictionary<string, string> numberOf)
        {
            List<SiteCluster> footprint = blocks == null ? new List<SiteCluster>() : Footprint(units, rows, faces, blocks);
            List<SiteCluster> joined = analysis == null ? footprint : Joined(footprint, Numbered(analysis, numberOf));
            var found = new List<SiteCluster>();
            for (int index = 0; index < joined.Count; index++)
            {
                List<string> plots = joined[index].Plots.Where(PyMath.IsDigit).ToList();
                if (plots.Count > 1)
                {
                    found.Add(new SiteCluster { Code = joined[index].Code, Plots = plots, Source = index < footprint.Count ? FootprintSource : AnalysisSource });
                }
            }
            return found;
        }

        // plan_from_book.cluster_step: every cluster that holds a plot of the scope, keyed by its plots in the order of their numbers joined by
        // a bar, a key met again with #2 and on after it
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IEnumerable<SiteCluster> clusters)
        {
            var wanted = new HashSet<string>(numbers);
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (SiteCluster cluster in clusters.Where(cluster => cluster.Plots.Any(wanted.Contains)))
            {
                string key = string.Join("|", cluster.Plots.OrderBy(plot => plot, PyMath.SortKey));
                if (found.ContainsKey(key))
                {
                    key = key + "#" + (found.Keys.Count(name => name.Split('#')[0] == key) + 1);
                }
                found[key] = new Dictionary<string, object> { ["plots"] = new List<string>(cluster.Plots), ["code"] = cluster.Code, ["source"] = cluster.Source };
            }
            return found;
        }
    }
}
