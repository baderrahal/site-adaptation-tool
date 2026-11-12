using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Help
{
    // R540. One entry of the help file: a step of the pane or a row of New project
    public sealed class HelpEntry
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Line { get; set; }
        public string What { get; set; }
        public IReadOnlyList<string> Files { get; set; }
        public string Before { get; set; }
        public IReadOnlyList<string> Click { get; set; }
        public IReadOnlyList<string> Limits { get; set; }
    }

    // R540. The help file python/build_help.py builds from steps/buttons-guide.md, so the pane and the guide never differ
    public sealed class HelpBook
    {
        private readonly Dictionary<string, HelpEntry> entries;

        private HelpBook(IEnumerable<HelpEntry> items)
        {
            entries = items.ToDictionary(item => item.Key, StringComparer.Ordinal);
        }

        public static HelpBook Parse(string text)
        {
            return new HelpBook(((JArray)JObject.Parse(text)["entries"]).Select(item => new HelpEntry
            {
                Key = (string)item["key"],
                Title = (string)item["title"],
                Line = (string)item["line"],
                What = (string)item["what"],
                Files = List(item["files"]),
                Before = (string)item["before"],
                Click = List(item["click"]),
                Limits = List(item["limits"]),
            }));
        }

        // Null for a key the file does not hold
        public HelpEntry Find(string key)
        {
            return key != null && entries.TryGetValue(key, out HelpEntry found) ? found : null;
        }

        private static IReadOnlyList<string> List(JToken token)
        {
            return token.Select(item => (string)item).ToList();
        }
    }
}
