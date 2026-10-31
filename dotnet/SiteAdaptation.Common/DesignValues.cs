using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R405, Q103. The design values a build, the placer and button 1 take, from designValues of config/project.json, the block
    // python/design_values.py reads. Current is the block of the config of the run, set by Use where a run loads its config (the
    // worker, button 1, button 7), else that of the config/project.json of the repo above the assembly, as a test or a build of the
    // repo runs. A value missing is refused by its name and never given a default, which would be a project value in code
    public sealed class DesignValues
    {
        public const string Block = "designValues";

        private static readonly object Gate = new object();
        private static DesignValues current;

        private DesignValues()
        {
        }

        public double PlacerCoordinateToleranceM { get; private set; }
        public double PlacerAngleToleranceDeg { get; private set; }
        public double PlacerZToleranceM { get; private set; }
        public double PlacerReadBackOriginM { get; private set; }
        public double PlacerReadBackAngleDeg { get; private set; }
        public double FloorTopToleranceM { get; private set; }
        public double FloorLevelToleranceM { get; private set; }
        public double FloorCornerOnVertexM { get; private set; }
        public double FloorShortestEdgeM { get; private set; }
        public double WallReadBackM { get; private set; }
        public double FrontTieM { get; private set; }
        public double FrontOutwardProbeM { get; private set; }
        public IReadOnlyDictionary<string, double[]> Quadrants { get; private set; }
        public double HandSideBandM { get; private set; }
        public int HandBufferSegmentsPerQuarter { get; private set; }
        public double SiteToleranceMm { get; private set; }
        public double SameMm { get; private set; }
        public double SameDeg { get; private set; }
        public long CmGrainMm { get; private set; }
        public int Decimals { get; private set; }
        public int AngleDecimals { get; private set; }
        // The config file the values were read from, null for a config given as JSON
        public string Source { get; private set; }

        public static DesignValues From(JObject config)
        {
            JObject block = config?[Block] as JObject ?? throw new InvalidDataException("the config holds no " + Block + " block (R405)");
            var quadrants = new Dictionary<string, double[]>();
            foreach (JProperty quadrant in Node(block, "fronts.quadrantsDeg").Children<JProperty>())
            {
                double[] span = (quadrant.Value as JArray)?.Select(item => item.Value<double>()).ToArray();
                if (span == null || span.Length != 2)
                {
                    throw new InvalidDataException(Block + ".fronts.quadrantsDeg." + quadrant.Name + " of the config is no pair of bearings (R405)");
                }
                quadrants[quadrant.Name] = span;
            }
            return new DesignValues
            {
                PlacerCoordinateToleranceM = Number(block, "placer.coordinateToleranceM"),
                PlacerAngleToleranceDeg = Number(block, "placer.angleToleranceDeg"),
                PlacerZToleranceM = Number(block, "placer.zToleranceM"),
                PlacerReadBackOriginM = Number(block, "placer.readBackOriginM"),
                PlacerReadBackAngleDeg = Number(block, "placer.readBackAngleDeg"),
                FloorTopToleranceM = Number(block, "floors.topToleranceM"),
                FloorLevelToleranceM = Number(block, "floors.levelToleranceM"),
                FloorCornerOnVertexM = Number(block, "floors.cornerOnVertexM"),
                FloorShortestEdgeM = Number(block, "floors.shortestEdgeM"),
                WallReadBackM = Number(block, "walls.readBackM"),
                FrontTieM = Number(block, "fronts.frontTieM"),
                FrontOutwardProbeM = Number(block, "fronts.outwardProbeM"),
                Quadrants = quadrants,
                HandSideBandM = Number(block, "hands.sideBandM"),
                HandBufferSegmentsPerQuarter = Whole(block, "hands.bufferSegmentsPerQuarter"),
                SiteToleranceMm = Number(block, "siteCoordinates.toleranceMm"),
                SameMm = Number(block, "samePoint.sameMm"),
                SameDeg = Number(block, "samePoint.sameDeg"),
                CmGrainMm = Whole(block, "levels.cmGrainMm"),
                Decimals = Whole(block, "rounding.decimals"),
                AngleDecimals = Whole(block, "rounding.angleDecimals"),
            };
        }

        public static DesignValues Load(string path)
        {
            DesignValues values = From(Read(path));
            values.Source = path;
            return values;
        }

        // A run that names its config file, a button by the settings of its session or a plain addinRun of the test host, takes its
        // values before its body. Null when they are set or no file is named, else the text the run stops with, so a config the run
        // cannot read never throws into Revit. A config with no block leaves the values as UseConfigOf does
        public static string UseConfigFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            try
            {
                JObject config = Read(path);
                if (config[Block] is JObject)
                {
                    DesignValues values = From(config);
                    values.Source = path;
                    Use(values);
                }
                return null;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is InvalidDataException)
            {
                return "the design values were not read from the config " + path + ": " + failure.Message + " (R405)";
            }
        }

        private static JObject Read(string path)
        {
            return PyMath.ReadJson(path, "config", true) as JObject ?? throw new InvalidDataException("the config is no JSON object (R405): " + path);
        }

        // The values of the run. The first read falls back on the config of the repo, refused by R405 when no repo lies above
        public static DesignValues Current
        {
            get
            {
                lock (Gate)
                {
                    if (current == null)
                    {
                        string path = RepoConfig(AppDomain.CurrentDomain.BaseDirectory);
                        current = Load(path ?? throw new InvalidDataException("no config of the run gave the design values and no config/project.json lies above " + AppDomain.CurrentDomain.BaseDirectory + " (R405)"));
                    }
                    return current;
                }
            }
        }

        // True when Current has values to give, from a config of the run or the config/project.json above the assembly
        public static bool Known
        {
            get
            {
                lock (Gate)
                {
                    return current != null || RepoConfig(AppDomain.CurrentDomain.BaseDirectory) != null;
                }
            }
        }

        public static void Use(DesignValues values)
        {
            lock (Gate)
            {
                current = values ?? throw new ArgumentNullException(nameof(values));
            }
        }

        // A run whose config holds the block takes its values. A config of a test with no block leaves the values as they are
        public static void UseConfigOf(JObject config)
        {
            if (config?[Block] is JObject)
            {
                Use(From(config));
            }
        }

        public static string RepoConfig(string start)
        {
            for (DirectoryInfo folder = string.IsNullOrEmpty(start) ? null : new DirectoryInfo(start); folder != null; folder = folder.Parent)
            {
                string candidate = Path.Combine(folder.FullName, "config", "project.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static JToken Node(JObject block, string dotted)
        {
            JToken node = block;
            foreach (string part in dotted.Split('.'))
            {
                node = (node as JObject)?[part];
                if (node == null)
                {
                    throw new InvalidDataException("the config holds no " + Block + "." + dotted + " (R405)");
                }
            }
            return node;
        }

        private static double Number(JObject block, string dotted)
        {
            JToken node = Node(block, dotted);
            if (node.Type != JTokenType.Float && node.Type != JTokenType.Integer)
            {
                throw new InvalidDataException(Block + "." + dotted + " of the config is no number (R405)");
            }
            return node.Value<double>();
        }

        private static int Whole(JObject block, string dotted)
        {
            JToken node = Node(block, dotted);
            if (node.Type != JTokenType.Integer)
            {
                throw new InvalidDataException(Block + "." + dotted + " of the config is no whole number (R405)");
            }
            return node.Value<int>();
        }
    }
}
