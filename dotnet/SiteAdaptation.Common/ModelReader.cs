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
            var errors = new List<string>();
            entry.Links = Guard("links", errors, () => ReadLinks(host, 1));
            entry.Walls = Guard("walls", errors, () => host.Walls().ToList());
            entry.Ramps = Guard("ramps", errors, () => host.Ramps().ToList());
            errors.AddRange(host.Errors);
            if (errors.Count > 0)
            {
                entry.Errors = errors;
            }
        }

        private static List<LinkEntry> ReadLinks(IModelSource source, int depth)
        {
            var entries = new List<LinkEntry>();
            foreach (SourceLink link in source.Links())
            {
                var entry = new LinkEntry { File = link.File, Transform = link.Transform, Instances = new List<PrototypeRow>() };
                entries.Add(entry);
                var errors = new List<string>();
                try
                {
                    IModelSource linked = link.Open == null ? null : link.Open();
                    if (linked == null)
                    {
                        errors.Add("Link is not loaded");
                    }
                    else
                    {
                        entry.Instances = linked.Instances().ToList();
                        if (depth < MaxLinkDepth)
                        {
                            entry.Links = ReadLinks(linked, depth + 1);
                        }
                        else
                        {
                            int deeper = linked.Links().Count();
                            if (deeper > 0)
                            {
                                entry.DeeperLinks = deeper;
                            }
                        }
                        errors.AddRange(linked.Errors);
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
