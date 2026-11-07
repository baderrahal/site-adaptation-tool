using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace CadReader
{
    // R600 to R606. The worker command dataCheck, stage 1 from the raw files of a folder as a team hands it over. It finds the drawings,
    // workbooks, PDF files, surface files and prototype files in the folder and its sub folders, reads each in place and never writes
    // there, and writes data_check.json, project_profile.json, plots.json and the gathered files of R790 into the output folder. A file that fails is recorded with
    // its exact error and the run carries on. Every file it opens is marked in the answer before and after (D19)
    public static class DataCheckCommand
    {
        public const string Command = "dataCheck";
        public const string FolderRole = "scanFolder";
        private const int PeekBytes = 64 * 1024;
        private const long RuleFileLimit = 5 * 1024 * 1024;

        public static void Run(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            if (string.IsNullOrEmpty(request.OutputFolder))
            {
                throw run.Refusal("a data check names no output folder (R600)");
            }
            string folder = Path.GetFullPath(run.Input(FolderRole));
            JObject config = JObject.Parse(WorkFiles.ReadText(run.Input("config")));
            var input = new DataCheckInput();
            DataCheckSettings settings = null;
            DataCheckOutput output;
            try
            {
                settings = DataCheckSettings.From(config);
            }
            catch (InvalidDataException refused)
            {
                input.Stopped = refused.Message;
            }
            if (settings != null)
            {
                input.Confirmed = Confirmed(run, settings);
            }
            if (settings == null)
            {
                output = DataCheckLogic.Stopped(null, input, "The project settings hold no values for this check, so nothing was read.");
            }
            else
            {
                Gather(run, folder, settings, input);
                string sheetPath = ReadParameterSheet(run, input);
                // R790. The gathered files before the check, so a Confirm runs the check again on this read and reads no drawing twice
                run.WriteJson(request.OutputFolder, DataCheckApply.InputFile, (writer, exact) => DataCheckApply.Write(writer, exact, input, sheetPath));
                output = DataCheckApply.Check(settings, input, run.Issues);
            }
            run.WriteJson(request.OutputFolder, SiteAdaptation.Common.ReadData.ReadDataRun.DataCheckFile, output.Check);
            run.WriteJson(request.OutputFolder, "project_profile.json", output.Profile);
            run.WriteJson(request.OutputFolder, "plots.json", output.Plots);
            run.WriteJson(request.OutputFolder, SiteAdaptation.Common.ReadData.ReadDataRun.PlotTableFile, DataCheckLogic.PlotTable(output.Plots));
            run.Counts["data check plots"] = ((JArray)output.Plots["plots"]).Count;
            run.Counts["data check drawings read"] = input.CadReads.Count;
            run.Counts["data check files not read"] = input.Notes.Count(note => !note.Read);
        }

        // R632. The file the window wrote with what a person confirmed, an optional input. One that cannot be read stops the run with its reason,
        // since a run that went on without it would hand back plots as though nothing was confirmed
        private static ConfirmedProfile Confirmed(WorkerContext run, DataCheckSettings settings)
        {
            if (!run.Request.Inputs.Any(item => item.Role == ConfirmedProfile.Role))
            {
                return null;
            }
            try
            {
                return ConfirmedProfile.From(JObject.Parse(WorkFiles.ReadText(run.Input(ConfirmedProfile.Role))), settings.GroupGapsM.Count);
            }
            catch (Exception failure) when (failure is InvalidDataException || failure is Newtonsoft.Json.JsonException)
            {
                throw run.Refusal("the project profile the window wrote cannot be read: " + failure.Message);
            }
        }

        // The files of the folder, each folder walked in ordinal order and a link or junction never followed, so a run reads only what the
        // folder holds
        private static List<string> Files(string folder, List<DataCheckNote> notes, HashSet<string> skip, List<string> skipped)
        {
            var found = new List<string>();
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                string here = pending.Pop();
                try
                {
                    foreach (string file in Directory.GetFiles(here))
                    {
                        found.Add(file);
                    }
                    foreach (string child in Directory.GetDirectories(here))
                    {
                        // R635. A folder of past outputs names no input of a new project, the tool cannot tell a past file by its own name
                        if (skip.Contains(Path.GetFileName(child)))
                        {
                            skipped.Add(Relative(folder, child));
                        }
                        else if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                        {
                            pending.Push(child);
                        }
                    }
                }
                catch (Exception failure)
                {
                    notes.Add(new DataCheckNote { Name = Relative(folder, here), Kind = "folder", Read = false, Reason = failure.GetType().Name + ": " + failure.Message });
                }
            }
            return found.OrderBy(file => file, StringComparer.Ordinal).ToList();
        }

        private static string Relative(string folder, string path)
        {
            string text = path.Length > folder.Length ? path.Substring(folder.Length).TrimStart('\\', '/') : ".";
            return text.Replace('\\', '/');
        }

        private static string Head(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var buffer = new byte[(int)Math.Min(PeekBytes, stream.Length)];
                int count = stream.Read(buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer, 0, count);
            }
        }

        private static void Gather(WorkerContext run, string folder, DataCheckSettings settings, DataCheckInput input)
        {
            foreach (string path in Files(folder, input.Notes, settings.SkipFolders, input.SkippedFolders))
            {
                string name = Relative(folder, path);
                string extension = Path.GetExtension(path).ToLowerInvariant();
                string file = Path.GetFileName(path);
                if (file.StartsWith("~$", StringComparison.Ordinal))
                {
                    continue;
                }
                switch (extension)
                {
                    case ".rvt":
                        input.PrototypeNames.Add(file);
                        break;
                    case ".dwg":
                    case ".dxf":
                        Try(run, input, name, path, "drawing", mark => ReadDrawing(input, settings, name, path, mark), settings.DrawingPeakMiB(extension, SizeMiB(path)));
                        break;
                    case ".xlsx":
                        Try(run, input, name, path, "workbook", mark => input.Excels.Add(ScanSources.Workbook(path)));
                        break;
                    case ".pdf":
                        Try(run, input, name, path, "pdf", mark => input.Pdfs.Add(ScanSources.Pdf(path, settings.Scan.PdfJoinGapPt, settings.Scan.PdfLineTolerancePt)));
                        break;
                    case ".xml":
                        Try(run, input, name, path, "xml", mark => PeekXml(input, path));
                        break;
                    case ".json":
                        Try(run, input, name, path, "json", mark => PeekRules(input, path));
                        break;
                    case ".txt":
                        Try(run, input, name, path, "text", mark => PeekParameters(input, path));
                        break;
                }
            }
        }

        // One file opened in place: marked before, read, and a failure recorded with its exact error. The notes of files that are not a
        // drawing, workbook or PDF are only kept when they failed, a folder holds many files that are no input of the check
        private static void Try(WorkerContext run, DataCheckInput input, string name, string path, string kind, Action<InputMark> read, double? peakMiB = null)
        {
            bool important = kind == "drawing" || kind == "workbook" || kind == "pdf";
            try
            {
                // Opened inside the try, a file another program holds open fails here and is recorded, the run carries on (R606)
                InputMark before;
                string refusal = run.OpenFile(kind, path, out before, peakMiB);
                if (refusal != null)
                {
                    input.Notes.Add(new DataCheckNote { Name = name, Kind = kind, Read = false, Reason = refusal });
                    return;
                }
                read(before);
                if (important)
                {
                    input.Notes.Add(new DataCheckNote { Name = name, Kind = kind, Read = true });
                }
            }
            catch (Exception failure)
            {
                input.Notes.Add(new DataCheckNote { Name = name, Kind = kind, Read = false, Reason = failure.GetType().Name + ": " + failure.Message });
            }
            finally
            {
                WorkerMemory.Release();
            }
        }

        private static double SizeMiB(string path)
        {
            try
            {
                return new FileInfo(path).Length / 1048576.0;
            }
            catch (IOException)
            {
                return 0;
            }
        }

        private static void ReadDrawing(DataCheckInput input, DataCheckSettings settings, string name, string path, InputMark mark)
        {
            var problems = new List<string>();
            JObject read = CadScan.Inventory(CadExtractor.Open(path, problems), name, path, mark.Size, mark.Sha256, settings.Scan.PairMinSegments, settings.Scan.PairThreads, settings.Scan.PairTimeCapS,
                Console.Error.WriteLine, true);
            input.CadReads.Add(read);
            if (problems.Count > 0)
            {
                input.Notes.Add(new DataCheckNote { Name = name, Kind = "drawing notes", Read = true, Reason = problems.Count + " reader notes, the first: " + problems[0] });
            }
        }

        // A surface file is XML that opens with the LandXML element. Its coordinate system element is looked for in the head only
        private static void PeekXml(DataCheckInput input, string path)
        {
            string head = Head(path);
            if (head.IndexOf("<LandXML", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }
            input.LandXmlFiles++;
            if (head.IndexOf("<CoordinateSystem", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                input.LandXmlWithCoordinateSystem++;
            }
        }

        // A rule set is a small JSON file that holds the typologies of a project
        private static void PeekRules(DataCheckInput input, string path)
        {
            if (new FileInfo(path).Length > RuleFileLimit)
            {
                return;
            }
            JObject file = JObject.Parse(WorkFiles.ReadText(path));
            if (file["typologies"] is JObject)
            {
                input.RuleFiles++;
            }
        }

        // R700. The parameter sheet of the project, the input of the request that the add-in or the scenario run takes from the project
        // settings, so the folder alone says nothing of it. The runner guards it as every input. A request that names none, or a sheet that
        // is not there or does not read, is said by the need sharedParameters and the run carries on
        private static string ReadParameterSheet(WorkerContext run, DataCheckInput input)
        {
            if (!run.Request.Inputs.Any(item => item.Role == SiteAdaptation.Common.Settings.InputNames.ParameterSheet))
            {
                return null;
            }
            string path = run.Input(SiteAdaptation.Common.Settings.InputNames.ParameterSheet);
            input.SheetName = Path.GetFileName(path);
            input.SheetFound = File.Exists(path);
            if (!input.SheetFound)
            {
                return path;
            }
            try
            {
                input.Sheet = SiteAdaptation.Common.SetUp.ParameterSheet.FromFile(path);
            }
            catch (Exception failure)
            {
                input.SheetProblem = failure.Message;
            }
            return path;
        }

        // A shared parameter file of Revit starts with its own comment line and holds a META line
        private static void PeekParameters(DataCheckInput input, string path)
        {
            string head = Head(path);
            if (head.IndexOf("shared parameter file", StringComparison.OrdinalIgnoreCase) >= 0 && head.Contains("*META"))
            {
                input.SharedParameterFiles++;
            }
        }
    }
}
