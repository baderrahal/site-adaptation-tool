using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.Common.Review
{
    // R406. The input of button 3 from the files of the work folder: the read of button 2, its plot table and the SHA-256 of that table,
    // the checks of review_<NBH>.json and site.json of button 1 for the model coordinates of the corners. The NBH is the one of the
    // scope. What the build of button 4 knows of a plot is not read here, so cluster, front edge and corners stay empty until then, apart
    // from the cluster proposals of R442, which ClusterConfirm.Load takes from the plan of the last done 4 Check
    public static class ReviewLoad
    {
        // The drawings the band line "Read from" names, in the order of the read
        private static readonly string[] SourceRoles = { "baseplan", "villaBlocks", "curbs" };

        // The read folder whose read.json holds the largest run id, which starts with the button and the time, null when none
        public static string LatestReadFolder(string workFolder)
        {
            if (!Directory.Exists(workFolder))
            {
                return null;
            }
            return Directory.GetDirectories(workFolder, ReadDataRun.FolderPrefix + "*")
                .Select(folder => new { folder, record = Record(folder) })
                .Where(item => item.record != null)
                .OrderByDescending(item => (string)item.record["runId"], StringComparer.Ordinal)
                .Select(item => item.folder).FirstOrDefault();
        }

        // named is an output the record of step 2 names, such as its plot table, and its folder is the read taken. Null takes the latest
        public static ReviewInput Load(string workFolder, string nbh, string named)
        {
            if (string.IsNullOrWhiteSpace(nbh))
            {
                throw new InvalidDataException("3 Review data shows one NBH, the scope names none. Choose an NBH in Scope of the pane");
            }
            string folder = named != null ? Path.GetDirectoryName(Path.GetFullPath(named)) : LatestReadFolder(workFolder);
            JObject record = folder == null ? null : Record(folder);
            if (record == null)
            {
                throw new InvalidDataException("the work folder holds no read of 2 Read data with its " + ReadDataRun.RecordFile + ", run 2 Read data first");
            }
            string tablePath = Path.Combine(folder, ReadDataRun.PlotTableFile);
            if (!File.Exists(tablePath))
            {
                throw new InvalidDataException("the read " + Path.GetFileName(folder) + " holds no plot table " + ReadDataRun.PlotTableFile + ", run 2 Read data again");
            }
            JObject table = JObject.Parse(WorkFiles.ReadText(tablePath));
            var byLabel = new JObject();
            foreach (JObject row in (table["plots"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string label = row["baseplanLabel"]?.Type == JTokenType.String ? (string)row["baseplanLabel"] : null;
                if (label != null)
                {
                    byLabel[label] = row;
                }
            }
            string sitePath = Path.Combine(workFolder, SetUpRun.SiteFile);
            JObject site = File.Exists(sitePath) ? JObject.Parse(WorkFiles.ReadText(sitePath)) : null;
            List<string> sources = SourceRoles.SelectMany(role => ((JArray)record["inputs"] ?? new JArray()).Where(input => (string)input["role"] == role))
                .Select(input => Path.GetFileName((string)input["path"] ?? "")).ToList();
            string trimmed = nbh.Trim();
            string reviewPath = ReviewFile.PathOf(workFolder, trimmed);
            Tuple<List<ClusterMark>, string> proposals = ClusterConfirm.Load(workFolder, trimmed);
            return new ReviewInput
            {
                Nbh = trimmed,
                Rows = ReviewTable.Rows(byLabel, trimmed, null, null, site),
                Checks = ReviewFile.Read(reviewPath),
                PlotTableHash = FileHash.Sha256(tablePath),
                ReadRunId = (string)record["runId"],
                Sources = sources,
                Proposals = proposals.Item1,
                PlanSha256 = proposals.Item2,
                ClusterSaved = ReviewFile.ReadClusters(reviewPath),
            };
        }

        private static JObject Record(string folder)
        {
            string path = Path.Combine(folder, ReadDataRun.RecordFile);
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                JObject record = JObject.Parse(WorkFiles.ReadText(path));
                return (string)record["format"] == ReadDataRun.RecordFormat ? record : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
