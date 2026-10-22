using System;
using System.Collections.Generic;
using System.IO;

namespace CadReader
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: CadReader <file.dwg|file.dxf> <output folder>");
                return 1;
            }
            string input = args[0];
            string outDir = args[1];
            if (!File.Exists(input))
            {
                Console.Error.WriteLine("File not found: " + input);
                return 2;
            }

            var problems = new List<string>();
            CadExtract extract;
            try
            {
                var document = CadExtractor.Open(input, problems);
                extract = CadExtractor.Extract(document, Path.GetFileName(input));
            }
            catch (Exception failure)
            {
                Console.Error.WriteLine("Could not read " + input + ": " + failure.GetType().Name + ": " + failure.Message);
                return 3;
            }

            CadExtractor.WriteFiles(extract, outDir);
            Console.WriteLine("layers " + extract.Layers.Layers.Count + ", plots " + extract.Plots.Plots.Count + ", texts " + extract.Texts.Texts.Count);
            if (problems.Count > 0)
            {
                Console.Error.WriteLine("reader notes " + problems.Count + ", first: " + problems[0]);
            }
            return 0;
        }
    }
}
