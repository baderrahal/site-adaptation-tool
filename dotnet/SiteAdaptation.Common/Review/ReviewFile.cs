using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Review
{
    // R406. review_<NBH>.json in the work folder, the team checks of one NBH (CHOSEN by the main session). One entry per plot the team
    // marked, with the plot, the check OK or Wrong, the note, the run id of the read it checked and the hash of the plot table of that
    // read. A plot not checked has no entry. It is written by a temporary name and a rename as every work file is (R347). 4 Check
    // lists the plots marked Wrong by WrongPlots (wave 2)
    public static class ReviewFile
    {
        public const string Format = "site-adaptation-review";
        public const int FormatVersion = 1;

        public static string PathOf(string folder, string nbh)
        {
            if (string.IsNullOrEmpty(nbh) || nbh.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("the neighbourhood " + nbh + " cannot name a file", nameof(nbh));
            }
            return Path.Combine(folder, "review_" + nbh + ".json");
        }

        // R442. The fields of the cluster confirm. clusterUnconfirmed is what buttons 5 to 8 read to skip a plot with the reason of
        // ClusterConfirm.NotConfirmedReason. A file of before R442 has none of them and reads as no plot confirmed and none to skip
        public const string ConfirmedField = "clusterConfirmed";
        public const string UnconfirmedField = "clusterUnconfirmed";
        public const string PlanField = "clusterPlanSha256";

        public static string Write(string folder, string nbh, IEnumerable<ReviewCheck> checks, string runId)
        {
            return Write(folder, nbh, checks, runId, null);
        }

        // clusters null writes the file of R406 alone, with no field of R442
        public static string Write(string folder, string nbh, IEnumerable<ReviewCheck> checks, string runId, ClusterConfirm clusters)
        {
            var root = new JObject
            {
                ["format"] = Format,
                ["formatVersion"] = FormatVersion,
                ["neighbourhood"] = nbh,
                ["runId"] = runId,
                ["checks"] = new JArray(checks.Select(check => new JObject
                {
                    ["plot"] = check.Plot,
                    ["check"] = check.Check,
                    ["note"] = check.Note ?? "",
                    ["runId"] = check.RunId,
                    ["plotTableHash"] = check.PlotTableHash,
                })),
            };
            if (clusters != null)
            {
                root[ConfirmedField] = new JArray(clusters.Confirmed());
                root[UnconfirmedField] = new JArray(clusters.Unconfirmed());
                root[PlanField] = clusters.PlanSha256;
            }
            return WorkFiles.WriteText(PathOf(folder, nbh), runId, root.ToString(Formatting.Indented));
        }

        // A file that is not there is no check yet. A file of another format, or an entry with no plot or a check that is not OK or
        // Wrong, is named, never taken as empty
        public static List<ReviewCheck> Read(string path)
        {
            if (!File.Exists(path))
            {
                return new List<ReviewCheck>();
            }
            JObject root;
            try
            {
                root = JObject.Parse(WorkFiles.ReadText(path));
            }
            catch (JsonException error)
            {
                throw new InvalidDataException(path + " is no JSON: " + error.Message);
            }
            if ((string)root["format"] != Format || (int?)root["formatVersion"] != FormatVersion || !(root["checks"] is JArray))
            {
                throw new InvalidDataException(path + " is no review file of format " + Format + " " + FormatVersion);
            }
            var found = new List<ReviewCheck>();
            foreach (JToken entry in (JArray)root["checks"])
            {
                string plot = (string)entry["plot"], check = (string)entry["check"];
                if (string.IsNullOrEmpty(plot) || !ReviewCheckValue.IsValid(check))
                {
                    throw new InvalidDataException(path + " holds an entry with plot " + (plot ?? "None") + " and check " + (check ?? "None") + ", a check is OK or Wrong");
                }
                found.Add(new ReviewCheck { Plot = plot, Check = check, Note = (string)entry["note"] ?? "", RunId = (string)entry["runId"], PlotTableHash = (string)entry["plotTableHash"] });
            }
            return found;
        }

        // R442. The cluster confirm the file keeps, every field empty in a file that is not there or was written before R442. A field
        // that is there and is not a list of plots is named, never taken as empty
        public static ClusterSaved ReadClusters(string path)
        {
            if (!File.Exists(path))
            {
                return new ClusterSaved();
            }
            JObject root;
            try
            {
                root = JObject.Parse(WorkFiles.ReadText(path));
            }
            catch (JsonException error)
            {
                throw new InvalidDataException(path + " is no JSON: " + error.Message);
            }
            JToken plan = root[PlanField];
            return new ClusterSaved
            {
                Confirmed = Plots(root, ConfirmedField, path),
                Unconfirmed = Plots(root, UnconfirmedField, path),
                PlanSha256 = plan != null && plan.Type == JTokenType.String ? (string)plan : null,
            };
        }

        // R442. The plots buttons 5 to 8 skip for NBH, those with a cluster proposal the team did not confirm when it last saved
        public static List<string> ClusterUnconfirmed(string folder, string nbh)
        {
            return ReadClusters(PathOf(folder, nbh)).Unconfirmed.ToList();
        }

        private static List<string> Plots(JObject root, string field, string path)
        {
            JToken token = root[field];
            if (token == null || token.Type == JTokenType.Null)
            {
                return new List<string>();
            }
            if (!(token is JArray list) || list.Any(item => item.Type != JTokenType.String || string.IsNullOrEmpty((string)item)))
            {
                throw new InvalidDataException(path + " holds a field " + field + " that is not a list of plot numbers");
            }
            return list.Select(item => (string)item).ToList();
        }

        // The plots marked Wrong for 4 Check. With a plot table hash only the checks made on that read count, null gives every one
        public static List<ReviewCheck> WrongPlots(string folder, string nbh, string plotTableHash)
        {
            return Read(PathOf(folder, nbh)).Where(check => check.Check == ReviewCheckValue.Wrong && (plotTableHash == null || check.PlotTableHash == plotTableHash)).ToList();
        }
    }
}
