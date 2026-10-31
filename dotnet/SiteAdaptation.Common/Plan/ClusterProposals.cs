using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R411. The proposal of a unit plot, python/cluster_proposals.py: the cluster as plot numbers or null, the mirror flag or null and the reason
    public sealed class ClusterProposal
    {
        public List<string> Cluster { get; set; }

        public JToken Mirrored { get; set; }

        public string Reason { get; set; }
    }

    // R411, round 4i. The cluster and mirror proposals of a build under rev6 with designBasis.clusters.propose, by the manual Rev 06 2.1.A to
    // 2.1.E: a detached unit alone with no hand, an attached unit in the one split of its row of attached units into the clusters of the rules
    // profile, each half of a cluster the hand keyRules hand cluster gives the prototype file for its side, else no proposal with the reason
    public static class ClusterProposals
    {
        // The attachment words of typologies of the rules profile, 2.1.A a detached unit, 2.1.B and 2.1.C the units that stand against others
        public const string Detached = "detached";
        private static readonly string[] Attached = { "one_side", "both_sides" };
        private static readonly string[] SideNames = { "left", "right" };
        private const string HandTable = "hand cluster";

        public const string DetachedWhy = "unit {0} is detached, Rev 06 2.1.A sets a detached unit alone and gives it no hand, so no mirror flag, Q226 (R411)";
        public const string NoAttachment = "typologies of the rules profile give unit {0} no attachment Rev 06 2.1 names, so no proposal, Q229 (R411)";
        public const string Crowded = "plot {0} of the row {1} has {2} neighbours on its {3} side, so the row is no single line of units, no proposal, Q229 (R411)";
        public const string NoSplit = "the row {0} of units {1} splits into no clusters of the rules profile, Rev 06 2.1.E, so no proposal, Q229 (R411)";
        public const string Several = "the row {0} of units {1} splits {2} ways into clusters of the rules profile, Rev 06 2.1.E, so no proposal, Q229 (R411)";
        public const string Proposed = "cluster {0} of the rules profile, the one split of the row {1} by Rev 06 2.1.E, the {2} half, {3} (R411)";
        public const string Hand = "the hand of {0} for that side in keyRules hand cluster, the halves opposite hands by 2.1.B and 2.1.C";
        public const string NoFile = "unit {0} links no prototype file, so no mirror flag";
        public const string NoHand = "keyRules hand cluster holds no {0} hand of {1}, so no mirror flag";
        public const string Middle = "the middle unit is in neither half of 2.1.B and 2.1.C, so no mirror flag, Q229";

        private static string Format(string text, params object[] values)
        {
            return string.Format(CultureInfo.InvariantCulture, text, values);
        }

        // cluster_proposals.sequences: clusters of the rules profile as unit sequences, each once under the first code that holds it, so a code
        // of cluster_aliases, the same sequence under another name, counts once
        public static List<KeyValuePair<string, string[]>> Sequences(JObject rules)
        {
            var found = new List<KeyValuePair<string, string[]>>();
            foreach (JProperty cluster in (rules["clusters"] as JObject ?? new JObject()).Properties())
            {
                string[] units = ((JArray)cluster.Value).Select(unit => (string)unit).ToArray();
                if (!found.Any(item => item.Value.SequenceEqual(units)))
                {
                    found.Add(new KeyValuePair<string, string[]>(cluster.Name, units));
                }
            }
            return found;
        }

        public static string AttachmentOf(string unit, JObject rules)
        {
            JToken attachment = unit == null ? null : (rules["typologies"]?[unit] as JObject)?["attachment"];
            return attachment != null && attachment.Type == JTokenType.String ? (string)attachment : null;
        }

        private static List<string> Near(IReadOnlyDictionary<string, Sides> neighbours, string plot, string side)
        {
            if (!neighbours.TryGetValue(plot, out Sides sides) || sides == null)
            {
                return new List<string>();
            }
            return side == "left" ? sides.Left : sides.Right;
        }

        // cluster_proposals.row_of: the row of attached unit plots of plot, left to right, walked by the side neighbours until a side holds no
        // single attached unit plot, and the first plot of it with more than one neighbour on a side, with that side and count, or null
        public static List<string> RowOf(string plot, IReadOnlyDictionary<string, Sides> neighbours, Func<string, bool> attached, out Tuple<string, string, int> crowded)
        {
            var row = new List<string> { plot };
            var seen = new HashSet<string>(StringComparer.Ordinal) { plot };
            foreach (string side in SideNames)
            {
                string at = plot;
                while (true)
                {
                    List<string> near = Near(neighbours, at, side);
                    if (near.Count != 1 || seen.Contains(near[0]) || !attached(near[0]))
                    {
                        break;
                    }
                    at = near[0];
                    seen.Add(at);
                    if (side == "left")
                    {
                        row.Insert(0, at);
                    }
                    else
                    {
                        row.Add(at);
                    }
                }
            }
            crowded = null;
            foreach (string item in row)
            {
                foreach (string side in SideNames)
                {
                    int count = Near(neighbours, item, side).Count;
                    if (crowded == null && count > 1)
                    {
                        crowded = Tuple.Create(item, side, count);
                    }
                }
            }
            return row;
        }

        private static bool Matches(IReadOnlyList<string> units, int start, string[] sequence)
        {
            return start + sequence.Length <= units.Count && sequence.Select((unit, index) => unit == units[start + index]).All(same => same);
        }

        // cluster_proposals.splits: how many ways units splits into consecutive sequences of known, and the one split as start, end and code when
        // there is exactly one
        public static long Splits(IReadOnlyList<string> units, List<KeyValuePair<string, string[]>> known, out List<Tuple<int, int, string>> split)
        {
            var ways = new long[units.Count + 1];
            ways[units.Count] = 1;
            for (int start = units.Count - 1; start >= 0; start--)
            {
                ways[start] = known.Where(item => Matches(units, start, item.Value)).Sum(item => ways[start + item.Value.Length]);
            }
            split = null;
            if (ways[0] != 1)
            {
                return ways[0];
            }
            split = new List<Tuple<int, int, string>>();
            int at = 0;
            while (at < units.Count)
            {
                KeyValuePair<string, string[]> piece = known.First(item => Matches(units, at, item.Value) && ways[at + item.Value.Length] > 0);
                split.Add(Tuple.Create(at, at + piece.Value.Length, piece.Key));
                at += piece.Value.Length;
            }
            return 1;
        }

        // cluster_proposals.mirror_of: the side of a unit in its cluster, left the first half as ClusterRoles names it, the mirror flag keyRules
        // hand cluster gives the file of the unit for that side and the words of the reason
        public static string MirrorOf(int index, int count, string unit, IReadOnlyDictionary<string, string> files, JObject keyRules, out JToken mirrored, out string why)
        {
            string side = index < (count - 1) / 2.0 ? "left" : index > (count - 1) / 2.0 ? "right" : "middle";
            mirrored = null;
            if (side == "middle")
            {
                why = Middle;
                return side;
            }
            if (files == null || !files.TryGetValue(unit, out string link) || link == null)
            {
                why = Format(NoFile, unit);
                return side;
            }
            JToken hand = (keyRules?[HandTable]?["byFile"] as JObject)?[link]?[side];
            if (hand == null || hand.Type == JTokenType.Null)
            {
                why = Format(NoHand, side, link);
                return side;
            }
            mirrored = hand.DeepClone();
            why = Format(Hand, link);
            return side;
        }

        private static string UnitOf(IReadOnlyDictionary<string, Dictionary<string, string>> rows, string plot)
        {
            if (rows.TryGetValue(plot, out Dictionary<string, string> row) && row != null && row.TryGetValue("typologyRev9", out string unit) && !string.IsNullOrEmpty(unit))
            {
                return unit;
            }
            return null;
        }

        // cluster_proposals.proposals: per unit plot of numbers its proposal by the attachment of its unit, its row of side neighbours, the
        // clusters of the rules profile and keyRules hand cluster
        public static Dictionary<string, ClusterProposal> Of(IEnumerable<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows,
            IReadOnlyDictionary<string, Sides> neighbours, JObject rules, JObject keyRules, IReadOnlyDictionary<string, string> files)
        {
            List<KeyValuePair<string, string[]>> known = Sequences(rules);
            Func<string, bool> attached = plot => Attached.Contains(AttachmentOf(UnitOf(rows, plot), rules));
            var found = new Dictionary<string, ClusterProposal>(StringComparer.Ordinal);
            foreach (string number in numbers)
            {
                string unit = UnitOf(rows, number);
                if (unit == null)
                {
                    continue;
                }
                string kind = AttachmentOf(unit, rules);
                if (kind == Detached)
                {
                    found[number] = new ClusterProposal { Cluster = new List<string> { number }, Reason = Format(DetachedWhy, unit) };
                    continue;
                }
                if (!Attached.Contains(kind))
                {
                    found[number] = new ClusterProposal { Reason = Format(NoAttachment, unit) };
                    continue;
                }
                List<string> row = RowOf(number, neighbours, attached, out Tuple<string, string, int> crowded);
                List<string> units = row.Select(plot => UnitOf(rows, plot)).ToList();
                string named = string.Join(" ", row);
                if (crowded != null)
                {
                    found[number] = new ClusterProposal { Reason = Format(Crowded, crowded.Item1, named, crowded.Item3, crowded.Item2) };
                    continue;
                }
                long count = Splits(units, known, out List<Tuple<int, int, string>> split);
                if (split == null)
                {
                    found[number] = new ClusterProposal { Reason = count == 0 ? Format(NoSplit, named, string.Join(" ", units)) : Format(Several, named, string.Join(" ", units), count) };
                    continue;
                }
                int at = row.IndexOf(number);
                Tuple<int, int, string> piece = split.First(item => item.Item1 <= at && at < item.Item2);
                string side = MirrorOf(at - piece.Item1, piece.Item2 - piece.Item1, unit, files, keyRules, out JToken mirrored, out string why);
                found[number] = new ClusterProposal { Cluster = row.GetRange(piece.Item1, piece.Item2 - piece.Item1), Mirrored = mirrored, Reason = Format(Proposed, piece.Item3, named, side, why) };
            }
            return found;
        }
    }
}
