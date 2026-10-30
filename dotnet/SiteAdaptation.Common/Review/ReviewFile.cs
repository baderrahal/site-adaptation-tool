using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Review
{
    // R402. review_<NBH>.json in the work folder, the team checks of one NBH (CHOSEN by the main session). One entry per plot the team
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

        public static string Write(string folder, string nbh, IEnumerable<ReviewCheck> checks, string runId)
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

        // The plots marked Wrong for 4 Check. With a plot table hash only the checks made on that read count, null gives every one
        public static List<ReviewCheck> WrongPlots(string folder, string nbh, string plotTableHash)
        {
            return Read(PathOf(folder, nbh)).Where(check => check.Check == ReviewCheckValue.Wrong && (plotTableHash == null || check.PlotTableHash == plotTableHash)).ToList();
        }
    }
}
