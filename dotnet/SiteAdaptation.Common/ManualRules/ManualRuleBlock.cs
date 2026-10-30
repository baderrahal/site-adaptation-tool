using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.ManualRules
{
    // R403. One rule of the block manualRules in the rules profile, M01 to M15 of docs/design/15-manual-rules.md. Every word of it comes
    // from the profile, none is written in code
    public sealed class ManualRule
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string Group { get; set; }

        // The Manual column: the section of the manual, or the revision the rule comes from
        public string ManualRef { get; set; }

        // The buttons that check the rule, as the ribbon names them
        public IReadOnlyList<string> CheckedIn { get; set; }

        // The section and the printed page with the page of the PDF file in brackets
        public string SectionPage { get; set; }

        // What the past job did, as the design table gives it, and its state
        public string PastJob { get; set; }
        public PastJobState PastJobState { get; set; }

        // The values of the text for the side box, each written in the text
        public IReadOnlyList<string> Values { get; set; }
    }

    // R403. The manual the rules come from, named by the profile so another project names its own
    public sealed class ManualInfo
    {
        public string Name { get; set; }
        public string Revision { get; set; }
        public string OlderRevision { get; set; }
        public string Number { get; set; }

        // The file name of the manual PDF and the folders to look for it in, in order
        public string File { get; set; }
        public IReadOnlyList<string> Folders { get; set; }
    }

    // R403. The block manualRules of the rules profile and the lookup of a rule by its id. 4 Check, Review data and the report call
    // Find or Line to show the manual rule of a row
    public sealed class ManualRuleBlock
    {
        public const string BlockName = "manualRules";

        private readonly Dictionary<string, ManualRule> byId;

        private ManualRuleBlock(ManualInfo manual, IReadOnlyList<ManualRule> rules)
        {
            Manual = manual;
            Rules = rules;
            byId = rules.ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        }

        public ManualInfo Manual { get; }

        public IReadOnlyList<ManualRule> Rules { get; }

        public static ManualRuleBlock FromFile(string rulesFile)
        {
            return Parse(JObject.Parse(System.IO.File.ReadAllText(rulesFile)));
        }

        public static ManualRuleBlock Parse(JObject rulesProfile)
        {
            if (!(rulesProfile[BlockName] is JObject block))
            {
                throw new InvalidDataException("the rules profile has no block " + BlockName);
            }
            if (!(block["manual"] is JObject manual) || !(block["rules"] is JArray list))
            {
                throw new InvalidDataException(BlockName + " needs manual and rules");
            }
            var info = new ManualInfo
            {
                Name = Required(manual, "name", "manual"),
                Revision = Required(manual, "revision", "manual"),
                OlderRevision = Required(manual, "olderRevision", "manual"),
                Number = Required(manual, "number", "manual"),
                File = Required(manual, "file", "manual"),
                Folders = (manual["folders"] as JArray ?? new JArray()).Select(item => (string)item).ToList(),
            };
            var rules = new List<ManualRule>();
            foreach (JToken item in list)
            {
                var entry = item as JObject ?? throw new InvalidDataException(BlockName + " rules hold an entry that is no object");
                string id = Required(entry, "id", "a rule");
                if (rules.Any(rule => rule.Id == id))
                {
                    throw new InvalidDataException(BlockName + " names " + id + " twice");
                }
                rules.Add(new ManualRule
                {
                    Id = id,
                    Text = Required(entry, "text", id),
                    Group = Required(entry, "group", id),
                    ManualRef = Required(entry, "manualRef", id),
                    CheckedIn = Strings(entry["checkedIn"]),
                    SectionPage = Required(entry, "sectionPage", id),
                    PastJob = Required(entry, "pastJob", id),
                    PastJobState = State(Required(entry, "pastJobState", id), id),
                    Values = Strings(entry["values"]),
                });
            }
            return new ManualRuleBlock(info, rules);
        }

        // The rule of an id, null for an id the block does not hold
        public ManualRule Find(string id)
        {
            return id != null && byId.TryGetValue(id, out ManualRule rule) ? rule : null;
        }

        // The id and the text for a side box, the id alone when the block does not hold it
        public string Line(string id)
        {
            ManualRule rule = Find(id);
            return rule == null ? id : rule.Id + " " + rule.Text;
        }

        private static string Required(JObject entry, string name, string where)
        {
            string value = entry[name] == null || entry[name].Type == JTokenType.Null ? null : (string)entry[name];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(BlockName + ": " + where + " has no " + name);
            }
            return value;
        }

        private static IReadOnlyList<string> Strings(JToken token)
        {
            return token is JArray array ? array.Select(item => (string)item).ToList() : new List<string>();
        }

        private static PastJobState State(string value, string id)
        {
            switch (value)
            {
                case "matches": return PastJobState.Matches;
                case "differs": return PastJobState.Differs;
                case "notMeasured": return PastJobState.NotMeasured;
                default: throw new InvalidDataException(BlockName + ": " + id + " has the past job state " + value);
            }
        }
    }
}
