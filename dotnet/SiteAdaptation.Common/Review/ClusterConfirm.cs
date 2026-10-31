using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.PlaceVillas;

namespace SiteAdaptation.Common.Review
{
    // R442. The proposal of R411 of one plot as the plan of 4 Check writes it under plots[].proposal. Cluster is null when the build gave
    // only a reason (Q229), Mirrored null for a unit with no Rev 6 hand (Q226)
    public sealed class ClusterMark
    {
        public string Plot { get; set; }

        public IReadOnlyList<string> Cluster { get; set; }

        public bool? Mirrored { get; set; }

        public string Reason { get; set; }

        public bool Proposed => Cluster != null && Cluster.Count > 0;
    }

    // R442. What review_<NBH>.json keeps of the cluster confirm: the plots confirmed, the plots with a proposal not confirmed and the
    // SHA-256 of the plan the team confirmed against. Every field empty or null in a file written before R442
    public sealed class ClusterSaved
    {
        public IReadOnlyList<string> Confirmed { get; set; } = new string[0];

        public IReadOnlyList<string> Unconfirmed { get; set; } = new string[0];

        public string PlanSha256 { get; set; }
    }

    // R442. The proposals of the plan of the last done 4 Check and the plots the team confirmed in 3 Review data. A confirm holds only
    // against the plan it was made on, so a build again with new proposals asks again (CHOSEN by Code, as a check of R406 holds only
    // against its plot table). With no plan the saved confirm is kept as it was, so a failed 4 Check loses no tick
    public sealed class ClusterConfirm
    {
        // The plain reason buttons 5 to 8 give for a plot of clusterUnconfirmed
        public const string NotConfirmedReason = "cluster not confirmed in 3 Review data";

        // Review of SCAN-UI: the reason of the plan names questions and rules, Q229 or (R411), which no window text shows
        public static string Plain(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return reason;
            }
            string plain = System.Text.RegularExpressions.Regex.Replace(reason, @"\s*\((?:R\d+|Q\d{3})\)|,\s*Q\d{3}|\s*Q\d{3}", "");
            return plain.Trim().TrimEnd(',').Trim();
        }

        private readonly Dictionary<string, ClusterMark> marks;
        private readonly HashSet<string> confirmed;
        private readonly string planSha256;
        private readonly ClusterSaved saved;

        public ClusterConfirm(IEnumerable<ClusterMark> marks, string planSha256, ClusterSaved saved)
        {
            this.marks = (marks ?? new ClusterMark[0]).ToDictionary(mark => mark.Plot, StringComparer.Ordinal);
            this.planSha256 = planSha256;
            this.saved = saved ?? new ClusterSaved();
            bool samePlan = planSha256 != null && string.Equals(this.saved.PlanSha256, planSha256, StringComparison.OrdinalIgnoreCase);
            confirmed = new HashSet<string>(samePlan ? this.saved.Confirmed.Where(HasProposal) : new string[0], StringComparer.Ordinal);
        }

        public static ClusterConfirm None => new ClusterConfirm(null, null, null);

        // True from the first tick after the last save
        public bool Changed { get; private set; }

        public bool AnyProposed => marks.Values.Any(mark => mark.Proposed);

        public bool AnyToConfirm => marks.Values.Any(mark => mark.Proposed && !confirmed.Contains(mark.Plot));

        public string PlanSha256 => planSha256 ?? saved.PlanSha256;

        public ClusterMark MarkOf(string plot)
        {
            return plot != null && marks.TryGetValue(plot, out ClusterMark mark) ? mark : null;
        }

        public bool HasProposal(string plot)
        {
            return MarkOf(plot)?.Proposed == true;
        }

        public bool IsConfirmed(string plot)
        {
            return HasProposal(plot) && confirmed.Contains(plot);
        }

        public void Confirm(string plot, bool yes)
        {
            if (!HasProposal(plot))
            {
                throw new ArgumentException("plot " + plot + " has no cluster proposal of 4 Check to confirm", nameof(plot));
            }
            if (yes ? confirmed.Add(plot) : confirmed.Remove(plot))
            {
                Changed = true;
            }
        }

        // Confirm all proposed: every plot with a proposal, the count newly confirmed
        public int ConfirmAll()
        {
            int added = marks.Values.Where(mark => mark.Proposed).Count(mark => confirmed.Add(mark.Plot));
            Changed |= added > 0;
            return added;
        }

        public void MarkSaved()
        {
            Changed = false;
        }

        // Ordered by plot as the checks are, so the file is the same for the same ticks
        public List<string> Confirmed()
        {
            return planSha256 == null ? saved.Confirmed.ToList() : confirmed.OrderBy(plot => plot, StringComparer.Ordinal).ToList();
        }

        public List<string> Unconfirmed()
        {
            return planSha256 == null ? saved.Unconfirmed.ToList()
                : marks.Values.Where(mark => mark.Proposed && !confirmed.Contains(mark.Plot)).Select(mark => mark.Plot).OrderBy(plot => plot, StringComparer.Ordinal).ToList();
        }

        // The proposal as the window shows it, "<cluster> mirror <yes|no|none>" with the plots of the cluster joined by a bare + so a pair
        // fits the column of 112, the plain text for a reason with no cluster, null with
        // no proposal so the plot table value stands
        public static string Shown(ClusterMark mark)
        {
            if (mark == null)
            {
                return null;
            }
            if (!mark.Proposed)
            {
                return ClusterConfirmText.NoProposal;
            }
            string mirror = mark.Mirrored.HasValue ? (mark.Mirrored.Value ? ClusterConfirmText.MirrorYes : ClusterConfirmText.MirrorNo) : ClusterConfirmText.MirrorNone;
            string[] parts = Regex.Split(ClusterConfirmText.Proposed, "<[^>]+>");
            return parts[0] + string.Join("+", mark.Cluster) + parts[1] + mirror + parts[2];
        }

        // The proposals of a plan file, one per entry of plots that holds a proposal with a reason. A plan of the past job holds none
        public static List<ClusterMark> FromPlan(JObject plan)
        {
            var found = new List<ClusterMark>();
            foreach (JObject entry in (plan?["plots"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string plot = entry["plot"]?.Type == JTokenType.String ? (string)entry["plot"] : null;
                if (plot == null || !(entry["proposal"] is JObject proposal) || proposal["reason"]?.Type != JTokenType.String)
                {
                    continue;
                }
                JArray cluster = proposal["cluster"] as JArray;
                JToken mirrored = proposal["mirrored"];
                found.Add(new ClusterMark
                {
                    Plot = plot,
                    Cluster = cluster?.Select(item => (string)item).ToList(),
                    Mirrored = mirrored?.Type == JTokenType.Boolean ? (bool?)(bool)mirrored : null,
                    Reason = Plain((string)proposal["reason"]),
                });
            }
            return found;
        }

        // The proposals of the plan of NBH the last done 4 Check wrote, found by VillaLoad.LastPlan as buttons 5 to 8 find it, with the
        // SHA-256 its record names. No plan, or a plan whose SHA-256 is not the recorded one, is nothing to confirm: buttons 5 to 8 stop
        // on that plan by themselves
        public static Tuple<List<ClusterMark>, string> Load(string workFolder, string nbh)
        {
            var none = Tuple.Create(new List<ClusterMark>(), (string)null);
            Tuple<string, string> named;
            try
            {
                named = VillaLoad.LastPlan(workFolder, nbh);
            }
            catch (InvalidDataException)
            {
                return none;
            }
            catch (JsonException)
            {
                return none;
            }
            if (named.Item2 == null || !File.Exists(named.Item1) || !string.Equals(FileHash.Sha256(named.Item1), named.Item2, StringComparison.OrdinalIgnoreCase))
            {
                return none;
            }
            return Tuple.Create(FromPlan(JObject.Parse(WorkFiles.ReadText(named.Item1))), named.Item2);
        }
    }
}
