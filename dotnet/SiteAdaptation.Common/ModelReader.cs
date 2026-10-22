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
            errors.AddRange(host.Errors);
            if (errors.Count > 0)
            {
                entry.Errors = errors;
            }
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
