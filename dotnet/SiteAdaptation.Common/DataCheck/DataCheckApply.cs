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

        // The gathered files as they are before the check: the confirmed profile is left out, the check of a Confirm sets its own, and the ties
        // are left out, the check fills them. The parameter sheet is kept by its path, as the shape of its rows is the sheet reader's
        public static JObject Save(DataCheckInput input, string sheetPath)
        {
            return new JObject
            {
                ["format"] = Format,
                ["cadReads"] = new JArray(input.CadReads.Select(read => read.DeepClone())),
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
