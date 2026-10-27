using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R354. A cluster of a build: its code, its plots as the build takes them and its source, footprint alone since Q089
    public sealed class SiteCluster
    {
        public string Code { get; set; }

        public List<string> Plots { get; set; }

        public string Source { get; set; }
    }

    // R354, R236. The clusters a build by keyRules takes, as plan_from_book.site_clusters gives them: the footprint clusters over every unit
    // plot, the plots that share one Building Footprint block of the name plots.csv wires to them, the block of that name that shares the
    // most of each face, a later block of the read taking a face only by a larger share, a face of two plan numbers left out, each of two
    // plots or more. Since Q089 no cluster of an analysis is joined to them
    public static class SiteClusters
    {
        public const string StepName = "clusters";
        public const string FootprintSource = "footprint";

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

        // plan_from_book.site_clusters: the footprint clusters alone since Q089, the plots of a face of two plan numbers left out
        public static List<SiteCluster> Site(IEnumerable<string> units, IReadOnlyDictionary<string, Dictionary<string, string>> rows, IReadOnlyDictionary<string, BuildFace> faces,
            IReadOnlyDictionary<string, List<double[][]>> blocks)
        {
            var found = new List<SiteCluster>();
            foreach (SiteCluster cluster in Footprint(units, rows, faces, blocks))
            {
                List<string> plots = cluster.Plots.Where(PyMath.IsDigit).ToList();
                if (plots.Count > 1)
                {
                    found.Add(new SiteCluster { Code = cluster.Code, Plots = plots, Source = FootprintSource });
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
