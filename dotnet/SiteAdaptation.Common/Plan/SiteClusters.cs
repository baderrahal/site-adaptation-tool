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

        // A value as Python %s writes it in a message: None, a text as it is, a number by repr, a list as [a, b]
        public static string PyText(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return "None";
            }
            if (token is JArray items)
            {
                return "[" + string.Join(", ", items.Select(PyText)) + "]";
            }
            if (token.Type == JTokenType.Float)
            {
                return SheetRows.PyRepr((double)token);
            }
            if (token.Type == JTokenType.Boolean)
            {
                return (bool)token ? "True" : "False";
            }
            return token.ToString();
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
