using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R790, F22 of FAST-9. What the worker gathered from the folder, kept beside the outputs of the read before the check ran on it, so a Confirm
    // can run the check again with the confirmed columns on the read just made and no drawing is read twice. The check itself is the one step the
    // worker and the Confirm share, so both give the same plots for the same gathered files and the same confirmed profile
    public static class DataCheckApply
    {
        public const string InputFile = "data_check_input.json";
        public const string Format = "site-adaptation-data-check-input";

        // R820. The role of the gathered files in a request of the worker command dataCheck, given in place of the folder
        public const string GatheredRole = "gathered";

        // R790. The longest file Load takes, in bytes: its text is one .NET string, which holds at most about 1.07 billion characters. A larger
        // file is refused by the apply before it is read, the confirmed values then count from the next read (R792)
        public const long MaxBytes = 1000000000;

        // The gathered files as they are before the check, written to the output by the worker with no indent and the drawing reads streamed
        // as they are, as on S1 they are about 600 MB with no indent and 1.7 GB with one. The confirmed profile is left out, the check of a
        // Confirm sets its own, and the ties are left out, the check fills them
        public static void Write(Newtonsoft.Json.JsonWriter writer, Newtonsoft.Json.JsonConverter exact, DataCheckInput input, string sheetPath)
        {
            writer.Formatting = Newtonsoft.Json.Formatting.None;
            writer.WritePropertyName("format");
            writer.WriteValue(Format);
            writer.WritePropertyName("cadReads");
            writer.WriteStartArray();
            foreach (JObject read in input.CadReads)
            {
                read.WriteTo(writer, exact);
            }
            writer.WriteEndArray();
            foreach (JProperty item in Rest(input, sheetPath).Properties())
            {
                item.WriteTo(writer, exact);
            }
        }

        // R823. The drawing read just made with only the candidates TopCandidates keeps, taken out at once so the read holds no more than the check
        // can use. Gives the count taken out
        public static int Prune(DataCheckSettings settings, JObject read)
        {
            HashSet<string> kept = TopCandidates(settings, read);
            if (kept == null)
            {
                return 0;
            }
            var candidates = (JArray)read["candidates"];
            List<JToken> gone = candidates.Where(candidate => !kept.Contains(CandidateKey(read, candidate))).ToList();
            foreach (JToken candidate in gone)
            {
                candidate.Remove();
            }
            return gone.Count;
        }

        // R823, F22 of FAST-15. The outline candidates of one drawing a check can still take: those of the top score of the drawing, every one of a
        // tie kept, so the pick in the drawing, the pick across drawings and the break of a tie of R601 come out as on the whole list. The score of an
        // outline is the faces that hold one plot number, from the texts, the label pattern and the faces alone, no confirmed value changes it, so a
        // Confirm takes the same outline. A drawing whose best score is 0 proposes no outline, so none is kept. On S1 the other candidates were 922 of
        // the 1,033 MB of the file. Null keeps every candidate: a drawing with none, or one the scan cannot score
        public static HashSet<string> TopCandidates(DataCheckSettings settings, JObject read)
        {
            if (settings == null || !(read["candidates"] is JArray candidates) || candidates.Count == 0)
            {
                return null;
            }
            List<JObject> rows;
            try
            {
                var prototypes = new HashSet<string>(StringComparer.Ordinal);
                JObject scan = ScanLogic.Result(settings.Scan, prototypes, prototypes, settings.StrongShare, new HashSet<string>(StringComparer.Ordinal), new[] { read },
                    new ScanWorkbook[0], new ScanPdf[0], new JArray());
                rows = ((JArray)scan["rows"]).OfType<JObject>().Where(row => (string)row["kind"] == "plotOutline").ToList();
            }
            catch (Exception)
            {
                return null;
            }
            if (rows.Count == 0)
            {
                return null;
            }
            int best = rows.Max(row => (int)row["score"]);
            if (best <= 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }
            return new HashSet<string>(rows.Where(row => (int)row["score"] == best).Select(row => (string)row["source"]), StringComparer.Ordinal);
        }

        // The source of a candidate as the plotOutline row of the scan names it, file and its layers in ordinal order
        private static string CandidateKey(JObject read, JToken candidate)
        {
            IEnumerable<string> layers = (candidate["layers"] as JArray ?? new JArray()).Select(layer => layer.Type == JTokenType.Null ? "" : (string)layer).OrderBy(name => name, StringComparer.Ordinal);
            string source = read["source"] == null || read["source"].Type == JTokenType.Null ? "" : (string)read["source"];
            return source + "|" + string.Join(ScanLogic.LayerJoin, layers);
        }

        // The parameter sheet is kept by its path, as the shape of its rows is the sheet reader's
        private static JObject Rest(DataCheckInput input, string sheetPath)
        {
            return new JObject
            {
                ["excels"] = JArray.FromObject(input.Excels),
                ["pdfs"] = JArray.FromObject(input.Pdfs),
                ["prototypeNames"] = new JArray(input.PrototypeNames),
                ["notes"] = JArray.FromObject(input.Notes),
                ["landXmlFiles"] = input.LandXmlFiles,
                ["landXmlWithCoordinateSystem"] = input.LandXmlWithCoordinateSystem,
                ["ruleFiles"] = input.RuleFiles,
                ["sharedParameterFiles"] = input.SharedParameterFiles,
                ["sheetName"] = input.SheetName,
                ["sheetFound"] = input.SheetFound,
                ["sheetPath"] = sheetPath,
                ["sheetProblem"] = input.SheetProblem,
                ["skippedFolders"] = new JArray(input.SkippedFolders),
                ["skippedFiles"] = new JArray(input.SkippedFiles),
            };
        }

        // The text of the file Save wrote, every number read back as the same double. A file of another shape throws InvalidDataException
        public static DataCheckInput Load(string text)
        {
            JObject file;
            try
            {
                file = PyJson.Parse(text) as JObject;
            }
            catch (FormatException failure)
            {
                throw new InvalidDataException("the gathered files of the read do not read: " + failure.Message + " (R790)");
            }
            if (file == null || (string)file["format"] != Format)
            {
                throw new InvalidDataException("the read holds no gathered files of this version of the tool (R790)");
            }
            var input = new DataCheckInput
            {
                LandXmlFiles = (int)file["landXmlFiles"],
                LandXmlWithCoordinateSystem = (int)file["landXmlWithCoordinateSystem"],
                RuleFiles = (int)file["ruleFiles"],
                SharedParameterFiles = (int)file["sharedParameterFiles"],
                SheetName = (string)file["sheetName"],
                SheetFound = (bool)file["sheetFound"],
                SheetProblem = (string)file["sheetProblem"],
            };
            input.CadReads.AddRange(List(file, "cadReads").OfType<JObject>());
            input.Excels.AddRange(List(file, "excels").ToObject<List<ScanWorkbook>>());
            input.Pdfs.AddRange(List(file, "pdfs").ToObject<List<ScanPdf>>());
            input.PrototypeNames.AddRange(List(file, "prototypeNames").Select(name => (string)name));
            input.Notes.AddRange(List(file, "notes").ToObject<List<DataCheckNote>>());
            input.SkippedFolders.AddRange(List(file, "skippedFolders").Select(name => (string)name));
            // R833. A gathered file written before this rule holds no such list, it left out no file
            input.SkippedFiles.AddRange((file["skippedFiles"] as JArray ?? new JArray()).Select(name => (string)name));
            string sheetPath = (string)file["sheetPath"];
            // The sheet as the worker read it: read again only when the worker read it whole, so a sheet it did not find stays not found
            if (input.SheetFound && input.SheetProblem == null && sheetPath != null)
            {
                try
                {
                    input.Sheet = SetUp.ParameterSheet.FromFile(sheetPath);
                }
                catch (Exception failure)
                {
                    input.SheetProblem = failure.Message;
                }
            }
            return input;
        }

        private static JArray List(JObject file, string key)
        {
            return file[key] as JArray ?? throw new InvalidDataException("the gathered files of the read hold no list " + key + " (R790)");
        }

        // R790. The check on the gathered files and the ring of each plot, as the worker ran it before this rule. A check that throws is a check
        // that stopped, its reason in input.Stopped. The notes of the plot outlines go to issues
        public static DataCheckOutput Check(DataCheckSettings settings, DataCheckInput input, List<string> issues)
        {
            DataCheckOutput output;
            try
            {
                output = DataCheckLogic.Run(settings, input);
            }
            catch (Exception failure)
            {
                input.Stopped = failure.GetType().Name + ": " + failure.Message;
                output = DataCheckLogic.Stopped(settings, input, "The check stopped before it finished, the reason is in the stopped note.");
            }
            if (input.Stopped == null)
            {
                // R645. The build draws the plots, so the table carries each ring stage 1 chose. A plot without one is said in an issue of the run
                issues.AddRange(SiteBuild.PlotOutlines.Add(output));
            }
            return output;
        }
    }
}
