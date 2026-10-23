using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common
{
    public static class ModelReader
    {
        // Links in the host are depth 1 and links inside those are depth 2. Anything deeper is counted and not read
        public const int MaxLinkDepth = 2;

        public static void ReadSections(IModelSource host, DocumentEntry entry)
        {
            ReadSections(host, entry, new ReaderOptions());
        }

        public static void ReadSections(IModelSource host, DocumentEntry entry, ReaderOptions options)
        {
            var errors = new List<string>();
            var files = new LinkFileReader(options.LinkContents);
            entry.Links = Guard("links", errors, () => files.ReadAll(host));
            if (options.LinkContents == LinkContents.Once)
            {
                entry.LinkedFiles = files.Files;
            }
            entry.Walls = Guard("walls", errors, () => host.Walls().ToList());
            entry.Floors = Guard("floors", errors, () => host.Floors().ToList());
            entry.Ramps = Guard("ramps", errors, () => host.Ramps().ToList());
            entry.SpotElevations = Guard("spotElevations", errors, () => host.SpotElevations().ToList());
            entry.FloorTypes = Guard("floorTypes", errors, () => host.FloorTypes().ToList());
            ReadSpotHostFailures(entry.SpotElevations, errors);
            foreach (WallRow wall in entry.Walls)
            {
                ReadWallPlot(wall, errors);
            }
            var noPlot = new SortedDictionary<string, List<long?>>(StringComparer.Ordinal);
            foreach (FloorRow floor in entry.Floors)
            {
                ReadFloorPlot(floor, errors, noPlot);
                ChooseCorners(floor, errors);
            }
            foreach (KeyValuePair<string, List<long?>> type in noPlot)
            {
                errors.Add(NoPlotLine(type.Key, type.Value));
            }
            errors.AddRange(host.Errors);
            if (errors.Count > 0)
            {
                entry.Errors = errors;
            }
        }

        // Spots whose host could not be read are one line per reason, the spots themselves stay in the list
        private static void ReadSpotHostFailures(IEnumerable<SpotRow> spots, IList<string> errors)
        {
            foreach (IGrouping<string, SpotRow> group in spots.Where(spot => spot.HostFailure != null).GroupBy(spot => spot.HostFailure, StringComparer.Ordinal).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                errors.Add(ErrorLines.Grouped("spotElevations host", group.Key, group.Select(spot => spot.ElementId).ToList()));
            }
        }

        // The most element ids one missing Plot line names. The count in that line is always the full count
        private const int MaxIdsInLine = 5;

        // A floor always gets plotNumber and unit, null when the Plot parameter is missing or does not parse.
        // A floor that does not parse gets its own line in errors. Floors with no Plot parameter are collected by type and get one line per type
        private static void ReadFloorPlot(FloorRow floor, IList<string> errors, IDictionary<string, List<long?>> noPlot)
        {
            object value = null;
            bool has = floor.Parameters != null && floor.Parameters.TryGetValue(PlotParts.ParameterName, out value);
            string unit;
            string number;
            if (PlotParts.TryParse(value, out unit, out number))
            {
                floor.Unit = unit;
                floor.PlotNumber = number;
                return;
            }
            if (has)
            {
                errors.Add("floor " + floor.ElementId + ": Plot parameter does not read as unit and number");
                return;
            }
            string type = string.IsNullOrEmpty(floor.TypeName) ? "(no type)" : floor.TypeName;
            List<long?> ids;
            if (!noPlot.TryGetValue(type, out ids))
            {
                ids = new List<long?>();
                noPlot[type] = ids;
            }
            ids.Add(floor.ElementId);
        }

        private static string NoPlotLine(string type, IList<long?> ids)
        {
            string shown = string.Join(", ", ids.Take(MaxIdsInLine).Select(id => id.HasValue ? id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?"));
            string more = ids.Count > MaxIdsInLine ? "first " + MaxIdsInLine + " element ids " : "element ids ";
            return "Plot parameter is missing on " + ids.Count + (ids.Count == 1 ? " floor" : " floors") + " of type " + type + ", " + more + shown;
        }

        // A wall gets plotNumber and unit only when it holds a Plot parameter
        private static void ReadWallPlot(WallRow wall, IList<string> errors)
        {
            object value;
            if (wall.Parameters == null || !wall.Parameters.TryGetValue(PlotParts.ParameterName, out value))
            {
                return;
            }
            string unit;
            string number;
            if (PlotParts.TryParse(value, out unit, out number))
            {
                wall.Unit = unit;
                wall.PlotNumber = number;
                return;
            }
            errors.Add("wall " + wall.ElementId + ": Plot parameter does not read as unit and number");
        }

        // Real corner levels, from shape editing when it is on and from the outer loop end points otherwise
        private static void ChooseCorners(FloorRow floor, IList<string> errors)
        {
            List<double[]> corners;
            string source;
            if (FloorMath.TryCorners(floor.Shape, floor.BoundaryEnds, floor.TopElevation, out corners, out source))
            {
                floor.Corners = corners;
                floor.CornersSource = source;
                if (source == FloorMath.FromShapeEditor && floor.Shape.Others != null && floor.Shape.Others.Count > 0)
                {
                    floor.ShapePoints = floor.Shape.Others;
                }
                return;
            }
            errors.Add("floor " + floor.ElementId + ": no corners found");
        }

        // Reads the link entries and, in once mode, the contents of each distinct file from its first loaded link.
        // The queue is first in first out, so every file met at depth 1 is read before any file met only at depth 2
        private sealed class LinkFileReader
        {
            private readonly LinkContents mode;
            private readonly Dictionary<string, LinkedFileEntry> byFile = new Dictionary<string, LinkedFileEntry>(StringComparer.OrdinalIgnoreCase);
            private readonly Queue<Pending> queue = new Queue<Pending>();
            private readonly List<LinkEntry> allEntries = new List<LinkEntry>();

            public LinkFileReader(LinkContents mode)
            {
                this.mode = mode;
                Files = new List<LinkedFileEntry>();
            }

            public List<LinkedFileEntry> Files { get; }

            public List<LinkEntry> ReadAll(IModelSource host)
            {
                var top = ReadLinks(host, 1);
                while (queue.Count > 0)
                {
                    ReadContents(queue.Dequeue());
                }
                if (mode == LinkContents.Once)
                {
                    foreach (LinkEntry entry in allEntries.Where(item => item.File != null && byFile.ContainsKey(item.File)))
                    {
                        entry.ContentsRef = byFile[entry.File].File;
                    }
                }
                return top;
            }

            private List<LinkEntry> ReadLinks(IModelSource source, int depth)
            {
                var entries = new List<LinkEntry>();
                foreach (SourceLink link in source.Links())
                {
                    var entry = new LinkEntry { File = link.File, Transform = link.Transform };
                    entries.Add(entry);
                    allEntries.Add(entry);
                    var errors = new List<string>();
                    if (link.Problems != null)
                    {
                        errors.AddRange(link.Problems);
                    }
                    LinkFields.Apply(entry, link.Name, link.Parameters, errors);
                    entry.ElementId = link.ElementId;
                    entry.Status = link.Status;
                    entry.Loaded = link.Loaded;
                    entry.BoundingBoxCorners = link.BoxCorners;
                    entry.TransformShared = link.TransformShared;
                    entry.ParameterDetails = link.ParameterDetails;
                    try
                    {
                        IModelSource linked = link.Open == null ? null : link.Open();
                        if (linked == null)
                        {
                            errors.Add("Link is not loaded");
                        }
                        else if (mode == LinkContents.Once && link.File != null && !byFile.ContainsKey(link.File))
                        {
                            var file = new LinkedFileEntry { File = link.File, Instances = new List<PrototypeRow>() };
                            byFile[link.File] = file;
                            Files.Add(file);
                            queue.Enqueue(new Pending { File = file, Source = linked, Depth = depth });
                        }
                    }
                    catch (Exception failure)
                    {
                        errors.Add(Describe(failure));
                    }
                    if (errors.Count > 0)
                    {
                        entry.Errors = errors;
                    }
                }
                return entries;
            }

            private void ReadContents(Pending item)
            {
                var errors = new List<string>();
                try
                {
                    item.File.Instances = item.Source.Instances().ToList();
                    if (item.Depth < MaxLinkDepth)
                    {
                        item.File.Links = ReadLinks(item.Source, item.Depth + 1);
                    }
                    else
                    {
                        int deeper = item.Source.Links().Count();
                        if (deeper > 0)
                        {
                            item.File.DeeperLinks = deeper;
                        }
                    }
                    errors.AddRange(item.Source.Errors);
                }
                catch (Exception failure)
                {
                    errors.Add(Describe(failure));
                }
                if (errors.Count > 0)
                {
                    item.File.Errors = errors;
                }
            }
        }

        private sealed class Pending
        {
            public LinkedFileEntry File { get; set; }
            public IModelSource Source { get; set; }
            public int Depth { get; set; }
        }

        // The Revit API can throw on odd elements, so one failure must not lose the sections that did read
        private static List<T> Guard<T>(string section, IList<string> errors, Func<List<T>> read)
        {
            try
            {
                return read();
            }
            catch (Exception failure)
            {
                errors.Add(section + ": " + Describe(failure));
                return new List<T>();
            }
        }

        public static string Describe(Exception failure)
        {
            return failure.GetType().Name + ": " + failure.Message;
        }
    }
}
