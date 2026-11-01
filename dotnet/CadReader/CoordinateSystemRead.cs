using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using ACadSharp;
using ACadSharp.Objects;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.SetUp;

namespace CadReader
{
    // R454. The worker command crs: the coordinate system each drawing states by the GeoData of its model space and each LandXML by
    // the epsgCode of its CoordinateSystem, and the plot outlines of the baseplan for the site position of R455, written to crs.json.
    // Every input is opened through WorkerContext.Input, so its hash is held before and after as for every command
    public static class CoordinateSystemRead
    {
        private static readonly Regex WellKnownText = new Regex("^\\s*(PROJCS|PROJCRS|GEOGCS|GEOGCRS|GEODCRS)\\s*\\[\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        private const string Projected = "ProjectedCoordinateSystem";
        private const string AnySystem = "CoordinateSystem";

        public static void Command(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            if (string.IsNullOrEmpty(request.OutputFolder))
            {
                throw run.Refusal("a crs names no output folder for " + CoordinateRequest.OutputFile + " (R454)");
            }
            var files = new JArray();
            var plots = new JArray();
            int stated = 0;
            foreach (WorkerFile input in request.Inputs.Where(input => input.Role != CoordinateRequest.ConfigRole && input.Role != CoordinateRequest.LayersRole).OrderBy(input => input.Role, StringComparer.Ordinal))
            {
                string path = run.Input(input.Role);
                string name;
                if (CoordinateRequest.IsLandXml(path))
                {
                    name = OfLandXml(path);
                }
                else if (CoordinateRequest.IsDrawing(path))
                {
                    var problems = new List<string>();
                    CadDocument document = CadExtractor.Open(path, problems);
                    name = OfDrawing(document);
                    if (input.Role == CoordinateRequest.BaseplanRole)
                    {
                        plots = Plots(run, document, path, problems);
                    }
                    WorkerMemory.Release();
                }
                else
                {
                    throw run.Refusal("the input " + input.Role + " is " + Path.GetFileName(path) + ", neither a drawing nor a LandXML (R454)");
                }
                stated += name == null ? 0 : 1;
                files.Add(new JObject { ["role"] = input.Role, ["file"] = Path.GetFileName(path), ["name"] = name });
            }
            run.Counts["inputs read"] = files.Count;
            run.Counts["inputs stating a coordinate system"] = stated;
            run.Counts["plot outlines"] = plots.Count;
            run.WriteJson(request.OutputFolder, CoordinateRequest.OutputFile, new JObject { ["format"] = CoordinateRead.Format, ["files"] = files, ["plots"] = plots });
        }

        // The plots of the baseplan with a number and an outline of three corners or more, as CadExtractor reads them for button 2
        private static JArray Plots(WorkerContext run, CadDocument document, string path, List<string> problems)
        {
            string refusal = CadUnits.HeaderRefusal(Path.GetFileName(path), document.Header.InsUnits.ToString());
            if (refusal != null)
            {
                throw run.Refusal(refusal);
            }
            LayerConfig layers = LayerConfig.Load(run.Input(CoordinateRequest.LayersRole));
            CadExtract extract = CadExtractor.Extract(document, Path.GetFileName(path), layers, piece => { }, path, problems);
            var plots = new JArray();
            foreach (PlotEntry entry in extract.Plots.Plots.Where(entry => !string.IsNullOrEmpty(entry.Plot) && entry.Vertices != null && entry.Vertices.Count >= 3))
            {
                plots.Add(new JObject { ["plot"] = entry.Plot, ["vertices"] = new JArray(entry.Vertices.Select(vertex => new JArray(vertex.Select(value => (object)value).ToArray()))) });
            }
            return plots;
        }

        // The name of the coordinate system of the GeoData object of model space, null when the drawing holds none
        public static string OfDrawing(CadDocument document)
        {
            CadDictionary extension = document.ModelSpace?.XDictionary;
            if (extension != null && extension.TryGetEntry(CadDictionary.GeographicData, out GeoData geo))
            {
                return NameOfDefinition(geo.CoordinateSystemDefinition);
            }
            return null;
        }

        // The id of the projected system of the XML definition AutoCAD writes, else of the first coordinate system in it, or the name
        // of a well known text. Any other text is given as it is, so it shows and maps to no code
        public static string NameOfDefinition(string definition)
        {
            string text = (definition ?? "").Trim().TrimStart('﻿');
            if (text.Length == 0)
            {
                return null;
            }
            if (text.StartsWith("<", StringComparison.Ordinal))
            {
                string first = null;
                using (XmlReader reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element || !reader.LocalName.EndsWith(AnySystem, StringComparison.Ordinal) || string.IsNullOrEmpty(reader.GetAttribute("id")))
                        {
                            continue;
                        }
                        if (reader.LocalName == Projected)
                        {
                            return reader.GetAttribute("id");
                        }
                        first = first ?? reader.GetAttribute("id");
                    }
                }
                return first;
            }
            Match match = WellKnownText.Match(text);
            return match.Success ? match.Groups[2].Value : text;
        }

        // The epsgCode of the first CoordinateSystem element of a LandXML, null when there is none or it is empty
        public static string OfLandXml(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "CoordinateSystem")
                    {
                        string code = reader.GetAttribute("epsgCode");
                        return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
                    }
                }
            }
            return null;
        }
    }
}
