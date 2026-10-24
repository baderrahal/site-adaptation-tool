using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common;

namespace CadReader
{
    internal static class Program
    {
        private const string ConfigName = "cad_layers.json";

        private static int Main(string[] args)
        {
            string layersPath = null;
            bool headerOnly = false;
            var positional = new List<string>();
            for (int index = 0; index < args.Length; index++)
            {
                if (args[index] == "--header-only")
                {
                    headerOnly = true;
                }
                else if (args[index] == "--layers" && index + 1 < args.Length)
                {
                    layersPath = args[++index];
                }
                else
                {
                    positional.Add(args[index]);
                }
            }
            if (positional.Count != 2)
            {
                Console.Error.WriteLine("Usage: CadReader <file.dwg|file.dxf> <output folder> [--layers cad_layers.json] [--header-only]");
                return 1;
            }
            string input = positional[0];
            string outDir = positional[1];
            if (!File.Exists(input))
            {
                Console.Error.WriteLine("File not found: " + input);
                return 2;
            }

            LayerConfig config;
            try
            {
                config = LoadConfig(layersPath);
            }
            catch (Exception failure)
            {
                Console.Error.WriteLine("Could not read the layer config: " + failure.GetType().Name + ": " + failure.Message);
                return 4;
            }

            var problems = new List<string>();
            if (headerOnly)
            {
                try
                {
                    var header = CadExtractor.Header(CadExtractor.Open(input, problems), Path.GetFileName(input));
                    Directory.CreateDirectory(outDir);
                    JsonOut.Write(Path.Combine(outDir, "header.json"), header);
                    Console.WriteLine("header, classes " + header.Classes.Count + ", entities " + header.ModelSpaceEntities);
                    return 0;
                }
                catch (Exception failure)
                {
                    Console.Error.WriteLine("Could not read " + input + ": " + failure.GetType().Name + ": " + failure.Message);
                    return 3;
                }
            }
            CadExtract extract;
            int pieces;
            try
            {
                var document = CadExtractor.Open(input, problems);
                Directory.CreateDirectory(outDir);
                using (var writer = new JsonOut.ArrayWriter(Path.Combine(outDir, "pieces.json"), Path.GetFileName(input), "pieces"))
                {
                    extract = CadExtractor.Extract(document, Path.GetFileName(input), config, writer.Add, input, problems);
                    pieces = writer.Count;
                }
            }
            catch (Exception failure)
            {
                Console.Error.WriteLine("Could not read " + input + ": " + failure.GetType().Name + ": " + failure.Message);
                return 3;
            }

            CadExtractor.WriteFiles(extract, outDir);
            Console.WriteLine("layers " + extract.Layers.Layers.Count + ", pieces " + pieces + ", plots " + extract.Plots.Summary.Plots + ", other faces " + extract.OtherFaces.Faces.Count + ", texts " + extract.Texts.Texts.Count);
            Console.WriteLine("blocks " + extract.Blocks.Blocks.Count + ", mirrored " + extract.Blocks.Blocks.Count(block => block.Mirrored));
            if (problems.Count > 0)
            {
                Console.Error.WriteLine("reader notes " + problems.Count + ", first: " + problems[0]);
            }
            return 0;
        }

        private static LayerConfig LoadConfig(string explicitPath)
        {
            if (explicitPath != null)
            {
                return LayerConfig.Load(explicitPath);
            }
            string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigName);
            return File.Exists(beside) ? LayerConfig.Load(beside) : new LayerConfig();
        }
    }
}
