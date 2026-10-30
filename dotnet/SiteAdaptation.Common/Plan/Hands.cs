using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R357. The side neighbours of a unit plot, majority.side_neighbours: left and right seen from the road, and whether it has a front
    public sealed class Sides
    {
        public List<string> Left { get; } = new List<string>();

        public List<string> Right { get; } = new List<string>();

        public bool Framed { get; set; }
    }

    // R357. The place of a plot in its cluster, cad_links.cluster_roles
    public sealed class ClusterRole
    {
        public string Cluster { get; set; }

        public int Units { get; set; }

        public int Index { get; set; }

        public string Side { get; set; }

        public string Role { get; set; }

        public List<string> Plots { get; set; }
    }

    // R357. The row pair of a unit in no cluster, key_rules.row_pairs
    public sealed class RowPair
    {
        public int Left { get; set; }

        public int Right { get; set; }

        public int Length { get; set; }

        public string Side { get; set; }
    }

    // R357. What plan_from_book.rule_inputs gives a build by keyRules for the hands, and the roles of the clusters of its scope
    public sealed class HandContext
    {
        public Dictionary<string, Sides> Neighbours { get; set; }

        public Dictionary<string, string> OpenEnds { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> RoadSides { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> Pairs { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> Kinds { get; } = new Dictionary<string, string>();

        // The row pairs of the plain walk, which the row partners of the levels of R359 are made of
        public Dictionary<string, RowPair> RowPairs { get; set; }

        // Q105. The row pairs the hands read: those of the plain walk, and for a cluster unit of the table of hand alone that of the walk as
        // if no cluster held it
        public Dictionary<string, RowPair> HandRowPairs { get; set; }

        public Dictionary<string, ClusterRole> Roles { get; set; }
    }

    // R357, R271, R333, R337. The hand a build by keyRules takes for a plot, as plan_from_book.py gives it: the factor the coded rule of hand
    // villa, hand cluster or hand alone names, looked up in the table of the file the plot links (plan_rules.key_hand), the factors from the
    // block mirror flag of plots.csv, the side of the plot in its cluster, the open end and the side road of the front and the pair of its
    // row of units in no cluster, the side neighbours those of majority.side_neighbours by the length of the boundary of a face within
    // the buffer of another at 0.05 m, the buffer as GEOS builds it
    public static class Hands
    {
        public const string StepName = "hands";

        // majority.side_neighbours buffers a face by 0.05 m, shapely by 16 segments a quarter circle
        private const double Band = 0.05;
        private const double Quantum = Math.PI / 2 / 16;

        // How far past an edge of the buffer a point still counts as on it, so an edge two faces share, which lies on the face the buffer is
        // of, counts as GEOS counts it, 0.05 m inside the buffer
        private const double Slack = 1e-9;

        private static readonly Regex FileUnit = new Regex("-ARC-([A-Z0-9]+)-", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // link_summary.file_unit: the unit code a prototype file name holds, upper case, null for a name of none. R376 holds the file a
        // key links against Typology_Rev9 of the plot by it
        public static string UnitOfFile(string name)
        {
            Match match = FileUnit.Match(name ?? "");
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
        }

        // majority.kind_of: the kind of the typology of the profile, else of the first prefix of majority.kindByPrefix the code starts with
        public static string KindOf(string code, JObject rules, JObject kindByPrefix)
        {
            JToken kind = code == null ? null : (rules["typologies"] as JObject)?[code]?["kind"];
            if (kind != null && kind.Type == JTokenType.String && ((string)kind).Length > 0)
            {
                return (string)kind;
            }
            if (string.IsNullOrEmpty(code))
            {
                return null;
            }
            if (kindByPrefix == null)
            {
                throw new InvalidDataException(KindByPrefixLine);
            }
            return kindByPrefix.Properties().Where(item => code.StartsWith(item.Name, StringComparison.Ordinal)).Select(item => (string)item.Value).FirstOrDefault();
        }

        private const string KindByPrefixLine = "config holds no majority.kindByPrefix object of texts, the kind of a unit code the rules profile gives none (R357)";

        // plan_from_book.hand_settings: the settings of the majority rule a build by keyRules reads for the hands, refused when one is missing or
        // of another kind and never read as empty, with the lines of Python (B3 of the review of PR 55)
        public static JObject Majority(JObject config)
        {
            if (!(config["majority"] is JObject block))
            {
                throw new InvalidDataException("config holds no majority block, the settings of the majority rule a build by keyRules reads (R357)");
            }
            if (!(block["pairKinds"] is JArray kinds) || kinds.Any(kind => kind.Type != JTokenType.String))
            {
                throw new InvalidDataException("config holds no majority.pairKinds list of texts, the unit kinds that pair in a row of units (R357)");
            }
            if (!(block["kindByPrefix"] is JObject prefixes) || prefixes.Properties().Any(item => item.Value.Type != JTokenType.String))
            {
                throw new InvalidDataException(KindByPrefixLine);
            }
            JToken share = block["sideShare"];
            if (share == null || (share.Type != JTokenType.Integer && share.Type != JTokenType.Float))
            {
                throw new InvalidDataException("config holds no majority.sideShare number, the share of the depth a side neighbour shares (R357)");
            }
            return block;
        }

        // majority.pair_kind: the kind of the unit of a plot of digits, none for a face of two plan numbers
        public static string PairKind(string plot, IReadOnlyDictionary<string, Dictionary<string, string>> rows, JObject rules, JObject kindByPrefix)
        {
            if (!PyMath.IsDigit(plot))
            {
                return null;
            }
            rows.TryGetValue(plot, out Dictionary<string, string> row);
            string code = null;
            row?.TryGetValue("typologyRev9", out code);
            return KindOf(code, rules, kindByPrefix);
        }

        // The buffer of a face as GEOS builds it for a positive distance: the face, the band of each edge on its outer side and the fan of
        // each convex corner, its arc in steps of at most a sixteenth of a quarter circle (OffsetSegmentGenerator::addDirectedFillet)
        private sealed class Buffer
        {
            private readonly double[][] ring;
            private readonly double r;
            private readonly List<double[]> bands = new List<double[]>();
            private readonly List<double[][]> fans = new List<double[][]>();

            public Buffer(double[][] ring, double r)
            {
                this.ring = ring;
                this.r = r;
                int count = ring.Length;
                double orientation = Signed(ring) > 0 ? 1.0 : -1.0;
                var normals = new double[count][];
                for (int index = 0; index < count; index++)
                {
                    double[] a = ring[index], b = ring[(index + 1) % count];
                    double length = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
                    double ux = (b[0] - a[0]) / length, uy = (b[1] - a[1]) / length;
                    normals[index] = new[] { uy * orientation, -ux * orientation };
                    bands.Add(new[] { a[0], a[1], length, ux, uy, normals[index][0], normals[index][1] });
                    double[] na = { a[0] + r * normals[index][0], a[1] + r * normals[index][1] }, nb = { b[0] + r * normals[index][0], b[1] + r * normals[index][1] };
                    Edges.Add(new[] { a, b });
                    Edges.Add(new[] { nb, na });
                    Edges.Add(new[] { a, na });
                    Edges.Add(new[] { b, nb });
                }
                for (int index = 0; index < count; index++)
                {
                    double[] p = ring[index], before = ring[(index + count - 1) % count], after = ring[(index + 1) % count];
                    double turn = (p[0] - before[0]) * (after[1] - p[1]) - (p[1] - before[1]) * (after[0] - p[0]);
                    if (turn * orientation <= 0)
                    {
                        continue;
                    }
                    double[] n1 = normals[(index + count - 1) % count], n2 = normals[index];
                    double start = Math.Atan2(n1[1], n1[0]), end = Math.Atan2(n2[1], n2[0]);
                    double span = end - start;
                    while (span <= -Math.PI)
                    {
                        span += 2 * Math.PI;
                    }
                    while (span > Math.PI)
                    {
                        span -= 2 * Math.PI;
                    }
                    int segments = (int)(Math.Abs(span) / Quantum + 0.5);
                    var fan = new List<double[]> { p, new[] { p[0] + r * n1[0], p[1] + r * n1[1] } };
                    for (int step = 1; step < segments; step++)
                    {
                        double angle = start + span * step / segments;
                        fan.Add(new[] { p[0] + r * Math.Cos(angle), p[1] + r * Math.Sin(angle) });
                    }
                    fan.Add(new[] { p[0] + r * n2[0], p[1] + r * n2[1] });
                    if (Signed(fan.ToArray()) < 0)
                    {
                        fan.Reverse();
                    }
                    double[][] corners = fan.ToArray();
                    fans.Add(corners);
                    for (int side = 0; side < corners.Length; side++)
                    {
                        Edges.Add(new[] { corners[side], corners[(side + 1) % corners.Length] });
                    }
                }
            }

            // Every edge where a point of a line can pass into the buffer or out of it
            public List<double[][]> Edges { get; } = new List<double[][]>();

            public bool Holds(double x, double y)
            {
                if (LocationPlan.Inside(ring, x, y))
                {
                    return true;
                }
                foreach (double[] band in bands)
                {
                    double along = (x - band[0]) * band[3] + (y - band[1]) * band[4], outward = (x - band[0]) * band[5] + (y - band[1]) * band[6];
                    if (along >= -Slack && along <= band[2] + Slack && outward >= -Slack && outward <= r + Slack)
                    {
                        return true;
                    }
                }
                foreach (double[][] fan in fans)
                {
                    bool inside = true;
                    for (int side = 0; side < fan.Length && inside; side++)
                    {
                        double[] a = fan[side], b = fan[(side + 1) % fan.Length];
                        inside = (b[0] - a[0]) * (y - a[1]) - (b[1] - a[1]) * (x - a[0]) >= -Slack;
                    }
                    if (inside)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private static double Signed(double[][] ring)
        {
            double sum = 0;
            for (int index = 0; index < ring.Length; index++)
            {
                double[] a = ring[index], b = ring[(index + 1) % ring.Length];
                sum += (a[0] - ring[0][0]) * (b[1] - ring[0][1]) - (b[0] - ring[0][0]) * (a[1] - ring[0][1]);
            }
            return sum;
        }

        // The length of the boundary of face within the buffer of other at r, face.boundary.intersection(other.buffer(r)).length of shapely:
        // each edge of the face cut where it crosses an edge of the buffer, each piece in or out by its middle. Within 6.6E-09 m of shapely on
        // the 16,386 pairs of faces of the baseplan whose boxes lie within 0.05 m (round_addin logs p4e_band_check.txt)
        public static double BandLength(BuildFace face, BuildFace other, double r)
        {
            var buffer = new Buffer(other.Ring, r);
            double total = 0;
            double[][] ring = face.Ring;
            for (int index = 0; index < ring.Length; index++)
            {
                double[] p = ring[index], q = ring[(index + 1) % ring.Length];
                double dx = q[0] - p[0], dy = q[1] - p[1];
                double length = Math.Sqrt(dx * dx + dy * dy);
                var cuts = new List<double> { 0.0, 1.0 };
                foreach (double[][] edge in buffer.Edges)
                {
                    double ex = edge[1][0] - edge[0][0], ey = edge[1][1] - edge[0][1];
                    double denominator = dx * ey - dy * ex;
                    if (denominator == 0)
                    {
                        continue;
                    }
                    double t = ((edge[0][0] - p[0]) * ey - (edge[0][1] - p[1]) * ex) / denominator;
                    double u = ((edge[0][0] - p[0]) * dy - (edge[0][1] - p[1]) * dx) / denominator;
                    if (t > 0 && t < 1 && u >= 0 && u <= 1)
                    {
                        cuts.Add(t);
                    }
                }
                cuts.Sort();
                double covered = 0;
                for (int cut = 0; cut + 1 < cuts.Count; cut++)
                {
                    double t0 = cuts[cut], t1 = cuts[cut + 1];
                    if (t1 > t0)
                    {
                        double middle = (t0 + t1) / 2;
                        if (buffer.Holds(p[0] + middle * dx, p[1] + middle * dy))
                        {
                            covered += t1 - t0;
                        }
                    }
                }
                total += covered * length;
            }
            return total;
        }

        // majority.side_neighbours: per unit plot with a face the unit plots whose face holds at least share of the depth of its boundary
        // within 0.05 m, left or right by the centroids along the right hand of its front, none for a plot with no front
        public static Dictionary<string, Sides> SideNeighbours(IEnumerable<string> units, IReadOnlyDictionary<string, BuildFace> faces, IReadOnlyDictionary<string, FrontFrame> frames, double share, double depth)
        {
            List<string> labels = units.Where(faces.ContainsKey).ToList();
            var found = new Dictionary<string, Sides>();
            foreach (string number in labels)
            {
                BuildFace face = faces[number];
                frames.TryGetValue(number, out FrontFrame frame);
                var sides = new Sides { Framed = frame != null };
                if (frame != null)
                {
                    foreach (string other in labels)
                    {
                        BuildFace near = faces[other];
                        if (other == number || near.Low[0] > face.High[0] + Band || near.High[0] < face.Low[0] - Band || near.Low[1] > face.High[1] + Band || near.High[1] < face.Low[1] - Band)
                        {
                            continue;
                        }
                        if (BandLength(face, near, Band) < share * depth)
                        {
                            continue;
                        }
                        double offset = (near.CentroidX - face.CentroidX) * frame.R[0] + (near.CentroidY - face.CentroidY) * frame.R[1];
                        (offset < 0 ? sides.Left : sides.Right).Add(other);
                    }
                }
                found[number] = sides;
            }
            return found;
        }

        // majority.road_side: where the first road edge of the ring other than the front lies seen from the road, back when it lies more
        // across the plot than along it, else left or right, none with no other road edge, null with no face or no front
        public static string RoadSide(BuildFace face, FrontFrame frame, RoadLines roads, double limit)
        {
            if (face == null || frame == null)
            {
                return null;
            }
            RoadEdge edge = Fronts.RoadEdges(face, roads, limit).FirstOrDefault(item => !Fronts.SameEdge(frame, item));
            if (edge == null)
            {
                return "none";
            }
            double mx = (edge.First[0] + edge.Second[0]) / 2 - face.CentroidX, my = (edge.First[1] + edge.Second[1]) / 2 - face.CentroidY;
            double along = mx * frame.R[0] + my * frame.R[1], across = mx * frame.N[0] + my * frame.N[1];
            return Math.Abs(across) > Math.Abs(along) ? "back" : along < 0 ? "left" : "right";
        }

        // Python str(int(text)) of a plot of decimal digits of any script, refused for a text int() refuses
        private static string WholeText(string text)
        {
            var digits = new System.Text.StringBuilder();
            foreach (char letter in (text ?? "").Trim())
            {
                int digit = System.Globalization.CharUnicodeInfo.GetDecimalDigitValue(letter);
                if (digit < 0)
                {
                    throw new InvalidDataException("invalid literal for int() with base 10: '" + text + "', a plot of a cluster is a whole number (R357)");
                }
                digits.Append((char)('0' + digit));
            }
            string whole = digits.ToString().TrimStart('0');
            if (digits.Length == 0)
            {
                throw new InvalidDataException("invalid literal for int() with base 10: '" + text + "', a plot of a cluster is a whole number (R357)");
            }
            return whole.Length == 0 ? "0" : whole;
        }

        // cad_links.cluster_roles: the plots of each cluster of two or more whose plots all have a face, left to right by their centroids along
        // the right hand of the first plot with a front, the left half left, the right half right, a middle plot middle, pair, end or middle
        public static Dictionary<string, ClusterRole> ClusterRoles(IEnumerable<SiteCluster> clusters, IReadOnlyDictionary<string, BuildFace> faces, IReadOnlyDictionary<string, FrontFrame> frames)
        {
            var roles = new Dictionary<string, ClusterRole>();
            foreach (SiteCluster cluster in clusters)
            {
                List<string> plots = cluster.Plots.Select(WholeText).ToList();
                if (plots.Count < 2 || plots.Any(plot => !faces.ContainsKey(plot)))
                {
                    continue;
                }
                FrontFrame frame = plots.Select(plot => frames.TryGetValue(plot, out FrontFrame found) ? found : null).FirstOrDefault(found => found != null);
                if (frame == null)
                {
                    continue;
                }
                List<string> ordered = plots.OrderBy(plot => faces[plot].CentroidX * frame.R[0] + faces[plot].CentroidY * frame.R[1]).ToList();
                int count = ordered.Count;
                for (int index = 0; index < count; index++)
                {
                    roles[ordered[index]] = new ClusterRole
                    {
                        Cluster = cluster.Code, Units = count, Index = index + 1, Plots = ordered,
                        Side = index < (count - 1) / 2.0 ? "left" : index > (count - 1) / 2.0 ? "right" : "middle",
                        Role = count == 2 ? "pair" : index == 0 || index == count - 1 ? "end" : "middle",
                    };
                }
            }
            return roles;
        }

        // key_rules.row_pairs: per counted unit, of the kinds and in no cluster, its row of counted units walked left and right while each step
        // has one, paired from its right end
        public static Dictionary<string, RowPair> RowPairs(IEnumerable<string> units, IReadOnlyDictionary<string, Sides> neighbours, Func<string, string> kindOf, Func<string, bool> inCluster,
            ICollection<string> kinds)
        {
            Func<string, bool> counted = plot => kinds.Contains(kindOf(plot)) && !inCluster(plot);
            Func<string, string, int> walk = (plot, side) =>
            {
                var seen = new HashSet<string> { plot };
                string at = plot;
                int steps = 0;
                while (true)
                {
                    List<string> beside = (neighbours.TryGetValue(at, out Sides sides) ? (side == "left" ? sides.Left : sides.Right) : new List<string>())
                        .Where(other => !seen.Contains(other) && counted(other)).ToList();
                    if (beside.Count != 1)
                    {
                        return steps;
                    }
                    at = beside[0];
                    seen.Add(at);
                    steps++;
                }
            };
            var found = new Dictionary<string, RowPair>();
            foreach (string plot in units)
            {
                if (!counted(plot) || !neighbours.ContainsKey(plot))
                {
                    continue;
                }
                int left = walk(plot, "left"), right = walk(plot, "right"), length = left + right + 1;
                found[plot] = new RowPair { Left = left, Right = right, Length = length, Side = length < 2 ? null : right % 2 == 0 ? "right" : "left" };
            }
            return found;
        }

        // plan_rules.unit_class
        public static string UnitClass(string kind, ClusterRole role)
        {
            return kind == "villa" ? "villa" : role != null ? "cluster" : "alone";
        }

        // plan_rules.hand_table, Q098: the class whose hand table gives the hand, the unit class, but a cluster unit whose file holds no table of
        // hand cluster and one of hand alone takes hand alone, as no footprint cluster of the 7 keys holds a DP2 unit since Q089
        public static string HandTable(JObject keyRules, string unitClass, string linkFile)
        {
            if (unitClass == "cluster" && linkFile != null && !Holds(keyRules["hand cluster"]?["byFile"], linkFile) && Holds(keyRules["hand alone"]?["byFile"], linkFile))
            {
                return "alone";
            }
            return unitClass;
        }

        // byFile.get(file) is not None of Python
        private static bool Holds(JToken byFile, string linkFile)
        {
            JToken found = (byFile as JObject)?[linkFile];
            return found != null && found.Type != JTokenType.Null;
        }

        // plan_rules.joined_factors
        public static Dictionary<string, string> JoinedFactors(Dictionary<string, string> factors)
        {
            factors.TryGetValue("blockMirror", out string mirror);
            factors.TryGetValue("roadSide", out string road);
            factors.TryGetValue("openEnd", out string open);
            factors.TryGetValue("rowPairSide", out string pair);
            factors["blockMirrorRoadSide"] = mirror == null || road == null ? null : mirror + " " + road;
            factors["blockMirrorOpenEnd"] = mirror == null || open == null ? null : mirror + " " + open;
            factors["rowPairSideOrBlockMirrorOpenEnd"] = !string.IsNullOrEmpty(pair) ? "pair " + pair : factors["blockMirrorOpenEnd"] == null ? null : "alone " + factors["blockMirrorOpenEnd"];
            return factors;
        }

        // plan_rules.key_hand: the mirror flag the table of the file gives the factor its coded rule names, and the rule, no flag when the
        // factor or the table gives none
        public static KeyValuePair<JToken, string> KeyHand(JObject keyRules, string unitClass, string linkFile, Dictionary<string, string> factors)
        {
            JObject entry = keyRules["hand " + unitClass] as JObject;
            string rule = (string)entry?["rule"];
            factors.TryGetValue(rule ?? "", out string value);
            if (value == null)
            {
                return new KeyValuePair<JToken, string>(null, rule);
            }
            // byFile and each table of it are objects since CheckKeyRules held them so, a list never read as no hand (P3 of the review of PR 55)
            JToken found = linkFile == null ? null : entry["byFile"]?[linkFile];
            if (found != null && found.Type != JTokenType.Object)
            {
                throw new InvalidDataException("keyRules hand " + unitClass + " holds byFile " + linkFile + " as " + BuildRules.ShapeName(found.Type) + ", a build takes it as an object of factor values (R337)");
            }
            JToken hand = (found as JObject)?[value];
            return new KeyValuePair<JToken, string>(hand?.DeepClone(), rule);
        }

        // plan_from_cad.prototype_files: the file of each code in the names of the folder in their order, a code of two files given none,
        // then a code with none the file of its keo_file
        public static Dictionary<string, string> PrototypeFiles(IEnumerable<string> listing, JObject rules)
        {
            var found = new Dictionary<string, string>();
            var twice = new HashSet<string>();
            foreach (string name in listing.OrderBy(item => item, StringComparer.Ordinal))
            {
                Match match = FileUnit.Match(name ?? "");
                if (!match.Success)
                {
                    continue;
                }
                string unit = match.Groups[1].Value.ToUpperInvariant();
                if (found.ContainsKey(unit))
                {
                    twice.Add(unit);
                }
                found[unit] = name;
            }
            Dictionary<string, string> files = found.Where(item => !twice.Contains(item.Key)).ToDictionary(item => item.Key, item => item.Value);
            foreach (JProperty typology in (rules["typologies"] as JObject ?? new JObject()).Properties())
            {
                string keo = (typology.Value as JObject)?["keo_file"]?.Type == JTokenType.String ? (string)typology.Value["keo_file"] : null;
                if (!files.ContainsKey(typology.Name) && !twice.Contains(typology.Name) && keo != null && files.ContainsKey(keo))
                {
                    files[typology.Name] = files[keo];
                }
            }
            return files;
        }

        private static string Code(IReadOnlyDictionary<string, Dictionary<string, string>> rows, string number)
        {
            rows.TryGetValue(number, out Dictionary<string, string> row);
            string code = null;
            row?.TryGetValue("typologyRev9", out code);
            return string.IsNullOrEmpty(code) ? null : code;
        }

        // plan_from_book.alone_table_units, Q105: the unit plots in a cluster that take the table of hand alone (HandTable, Q098), whose row pair
        // is walked as if no cluster held them, for their hand alone. No key rules or no files leave the set empty
        public static HashSet<string> AloneTableUnits(IEnumerable<string> units, IReadOnlyDictionary<string, Dictionary<string, string>> rows,
            IReadOnlyDictionary<string, ClusterRole> roles, JObject keyRules, IReadOnlyDictionary<string, string> files, Func<string, string> kindOf)
        {
            var found = new HashSet<string>();
            if (keyRules == null || files == null)
            {
                return found;
            }
            foreach (string plot in units)
            {
                string code = Code(rows, plot);
                roles.TryGetValue(plot, out ClusterRole role);
                string unitClass = UnitClass(kindOf(code), role);
                string file = code != null && files.TryGetValue(code, out string named) ? named : null;
                if (unitClass == "cluster" && HandTable(keyRules, unitClass, file) == "alone")
                {
                    found.Add(plot);
                }
            }
            return found;
        }

        // What rule_inputs gives a build by keyRules and the roles build takes, frames holding the front of every plot of the scope, of
        // every unit plot and of every plot of the clusters
        public static HandContext Context(IReadOnlyList<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows, IReadOnlyDictionary<string, BuildFace> faces,
            IReadOnlyList<SiteCluster> clusters, IReadOnlyDictionary<string, FrontFrame> frames, RoadLines roads, JObject rules, JObject config, JObject keyRules = null,
            IReadOnlyDictionary<string, string> files = null)
        {
            JObject block = Majority(config);
            JObject prefixes = (JObject)block["kindByPrefix"];
            List<string> units = rows.Where(item => Code(rows, item.Key) != null && faces.ContainsKey(item.Key)).Select(item => item.Key).ToList();
            var context = new HandContext
            {
                Neighbours = SideNeighbours(units, faces, frames, BuildFace.Number(block["sideShare"]), BuildFace.Number(rules["plot"]["long_side"]) / 1000.0),
            };
            foreach (string number in numbers)
            {
                frames.TryGetValue(number, out FrontFrame frame);
                if (context.Neighbours.TryGetValue(number, out Sides sides) && frame != null)
                {
                    context.OpenEnds[number] = sides.Left.Count == 0 && sides.Right.Count == 0 ? "both" : sides.Left.Count == 0 ? "left" : sides.Right.Count == 0 ? "right" : "none";
                }
                if (roads.Count > 0)
                {
                    faces.TryGetValue(number, out BuildFace face);
                    context.RoadSides[number] = RoadSide(face, frame, roads, BuildFace.Number(config["siteBuild"]["corner"]["roadEdgeM"]));
                }
            }
            var unitFaces = units.ToDictionary(number => number, number => faces[number]);
            Dictionary<string, ClusterRole> ruleRoles = ClusterRoles(clusters.Where(cluster => cluster.Plots.Count > 1), unitFaces, frames);
            var kinds = new HashSet<string>(((JArray)block["pairKinds"]).Select(item => (string)item));
            context.RowPairs = RowPairs(units, context.Neighbours, plot => PairKind(plot, rows, rules, prefixes), ruleRoles.ContainsKey, kinds);
            context.HandRowPairs = context.RowPairs;
            HashSet<string> free = AloneTableUnits(units, rows, ruleRoles, keyRules, files, code => KindOf(code, rules, prefixes));
            if (free.Count > 0)
            {
                // Q105. The row pair of such a unit as if no cluster held it, for its hand alone, so RowPairs stays that of the plain walk
                Dictionary<string, RowPair> walked = RowPairs(units, context.Neighbours, plot => PairKind(plot, rows, rules, prefixes), plot => ruleRoles.ContainsKey(plot) && !free.Contains(plot), kinds);
                context.HandRowPairs = new Dictionary<string, RowPair>(context.RowPairs);
                foreach (string plot in free.Where(walked.ContainsKey))
                {
                    context.HandRowPairs[plot] = walked[plot];
                }
            }
            foreach (string number in numbers)
            {
                context.Pairs[number] = context.HandRowPairs.TryGetValue(number, out RowPair pair) ? pair.Side : null;
                context.Kinds[number] = KindOf(Code(rows, number), rules, prefixes);
            }
            var wanted = new HashSet<string>(numbers);
            context.Roles = ClusterRoles(clusters.Where(cluster => cluster.Plots.Any(wanted.Contains)), faces, frames);
            return context;
        }

        // plan_from_book.hand_step: per plot of the scope the factors, the table it takes (HandTable), its rule and the hand of its file, before an
        // override of the list
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows, HandContext context,
            JObject keyRules, IReadOnlyDictionary<string, string> files, JObject rules, JObject config)
        {
            JObject prefixes = (JObject)Majority(config)["kindByPrefix"];
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                rows.TryGetValue(number, out Dictionary<string, string> row);
                string unit = Code(rows, number);
                string mirrored = null;
                row?.TryGetValue("footprintMirrored", out mirrored);
                context.Roles.TryGetValue(number, out ClusterRole role);
                context.Neighbours.TryGetValue(number, out Sides sides);
                context.HandRowPairs.TryGetValue(number, out RowPair pair);
                context.OpenEnds.TryGetValue(number, out string open);
                context.RoadSides.TryGetValue(number, out string road);
                context.Pairs.TryGetValue(number, out string pairSide);
                context.Kinds.TryGetValue(number, out string kind);
                Dictionary<string, string> factors = JoinedFactors(new Dictionary<string, string>
                {
                    ["all"] = "all", ["blockMirror"] = mirrored == "yes" || mirrored == "no" ? mirrored : null, ["clusterSide"] = role != null ? role.Side : "alone",
                    ["openEnd"] = open, ["roadSide"] = road, ["rowPairSide"] = pairSide,
                });
                string unitClass = UnitClass(kind, role);
                string linkFile = unit != null && files.TryGetValue(unit, out string file) ? file : null;
                string table = unit != null ? HandTable(keyRules, unitClass, linkFile) : null;
                KeyValuePair<JToken, string> hand = unit != null ? KeyHand(keyRules, table, linkFile, factors) : new KeyValuePair<JToken, string>(null, null);
                factors.TryGetValue(hand.Value ?? "", out string factor);
                found[number] = new Dictionary<string, object>
                {
                    ["unit"] = unit, ["kind"] = kind, ["pairKind"] = PairKind(number, rows, rules, prefixes), ["unitClass"] = unitClass,
                    ["cluster"] = role?.Plots == null ? null : new List<string>(role.Plots), ["clusterCode"] = role?.Cluster, ["clusterSide"] = role?.Side, ["clusterRole"] = role?.Role,
                    ["clusterIndex"] = role == null ? null : (object)role.Index, ["clusterUnits"] = role == null ? null : (object)role.Units,
                    ["left"] = sides == null ? null : sides.Left.OrderBy(item => item, PyMath.SortKey).ToList(), ["right"] = sides == null ? null : sides.Right.OrderBy(item => item, PyMath.SortKey).ToList(),
                    ["openEnd"] = open, ["roadSide"] = road,
                    ["rowPair"] = pair == null ? null : new JObject { ["left"] = pair.Left, ["right"] = pair.Right, ["length"] = pair.Length, ["side"] = pair.Side },
                    ["blockMirror"] = factors["blockMirror"], ["blockMirrorRoadSide"] = factors["blockMirrorRoadSide"], ["blockMirrorOpenEnd"] = factors["blockMirrorOpenEnd"],
                    ["rowPairSideOrBlockMirrorOpenEnd"] = factors["rowPairSideOrBlockMirrorOpenEnd"], ["handTable"] = table, ["handRule"] = hand.Value, ["factor"] = hand.Value == null ? null : factor,
                    ["linkFile"] = linkFile, ["mirrored"] = hand.Key,
                };
            }
            return found;
        }
    }
}
