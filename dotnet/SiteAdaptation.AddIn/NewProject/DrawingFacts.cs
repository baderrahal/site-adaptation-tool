using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.ReadData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.Common.Scan;

namespace SiteAdaptation.AddIn.NewProject
{
    // What the block "What the tool read from the drawing" shows, null for each fact no reader gives yet
    public sealed class DrawingFactsResult
    {
        public int? Layers { get; set; }
        public string SurveyPoint { get; set; }
        public int? Plots { get; set; }
        public string PlotLayer { get; set; }
        public int? VillaTypes { get; set; }

        // R440. The scan.json the facts come from and what it offers the dropdowns, null when no scan is in the work folder
        public string ScanPath { get; set; }

        public ScanFactsData Scan { get; set; }
    }

    // The hook of the scanner of a later Lane B round (steps/waiting-on-others.md): it gives the layers of the drawing, the survey point
    // and the lists Change and View pick from. Until then NewProjectDialog.Facts is LastReadFacts
    public interface IDrawingFacts
    {
        DrawingFactsResult Read(string configFile, string workFolder);
    }

    // What the current readers already give: the unit plots and the villa types of the plot table of the latest read of 2 Read data in
    // the work folder (R375, ReviewLoad), and the plot line layers of the layer list beside config (addin.readData.cadLayers). The layers of
    // the drawing and the survey point come with the scanner, so they stay null. A file that is not there or not read gives null, never a guess
    public sealed class LastReadFacts : IDrawingFacts
    {
        public DrawingFactsResult Read(string configFile, string workFolder)
        {
            var facts = new DrawingFactsResult { PlotLayer = PlotLayer(configFile) };
            string folder = string.IsNullOrEmpty(workFolder) ? null : ReviewLoad.LatestReadFolder(workFolder);
            string table = folder == null ? null : Path.Combine(folder, ReadDataRun.PlotTableFile);
            if (table == null || !File.Exists(table))
            {
                return facts;
            }
            try
            {
                JObject plots = JObject.Parse(WorkFiles.ReadText(table));
                // Review of NEWPROJ-UI: a plot number met twice counts once, and only unit plots give a villa type
                facts.Plots = ReadDataRun.ScopePlots(plots).Where(plot => plot.IsUnit).Select(plot => plot.Number).Distinct(StringComparer.Ordinal).Count();
                facts.VillaTypes = (plots["plots"] as JArray ?? new JArray()).OfType<JObject>()
                    .Where(row => row["officialNumber"]?.Type == JTokenType.String)
                    .Select(row => row["typologyRev9"]?.Type == JTokenType.String ? ((string)row["typologyRev9"]).Trim() : "")
                    .Where(unit => unit.Length > 0).Distinct(StringComparer.Ordinal).Count();
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
            {
                facts.Plots = null;
                facts.VillaTypes = null;
            }
            return facts;
        }

        private static string PlotLayer(string configFile)
        {
            if (string.IsNullOrEmpty(configFile) || !File.Exists(configFile))
            {
                return null;
            }
            try
            {
                string list = (string)JObject.Parse(File.ReadAllText(configFile)).SelectToken("addin.readData.cadLayers");
                string path = string.IsNullOrEmpty(list) ? null : Path.Combine(Path.GetDirectoryName(configFile), list);
                if (path == null || !File.Exists(path))
                {
                    return null;
                }
                string[] layers = (JObject.Parse(File.ReadAllText(path))["plotLineLayers"] as JArray ?? new JArray()).Select(layer => (string)layer).Where(layer => !string.IsNullOrEmpty(layer)).ToArray();
                return layers.Length == 0 ? null : string.Join(", ", layers);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException || error is InvalidCastException || error is ArgumentException)
            {
                return null;
            }
        }
    }

    // R440, Q283. The facts of the latest scan of the work folder, the scan button 2 starts, else those of the latest read. A scan that
    // does not read gives the facts of the read, never a guess
    public sealed class LatestScanFacts : IDrawingFacts
    {
        public DrawingFactsResult Read(string configFile, string workFolder)
        {
            string path = string.IsNullOrEmpty(workFolder) ? null : ScanStep.Latest(workFolder);
            if (path != null)
            {
                try
                {
                    ScanFactsData scan = ScanFacts.Of(WorkFiles.ReadText(path));
                    return new DrawingFactsResult { Layers = scan.Layers, Plots = scan.Plots, PlotLayer = scan.PlotLayer, VillaTypes = scan.VillaTypes, ScanPath = path, Scan = scan };
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException || error is InvalidCastException)
                {
                }
            }
            return new LastReadFacts().Read(configFile, workFolder);
        }
    }
}
