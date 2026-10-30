using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common.ManualRules
{
    // R403. What the past job did for a rule
    public enum PastJobState
    {
        NotMeasured,
        Matches,
        Differs,
    }

    // R403. A past job fact given by the override list or the build, the text the window shows beside the state
    public sealed class PastJobFact
    {
        public PastJobFact(PastJobState state, string text)
        {
            State = state;
            Text = text;
        }

        public PastJobState State { get; }
        public string Text { get; }
    }

    // R403. The past job of one rule. Text is null for a rule not measured, which the window says as not read yet
    public sealed class PastJobView
    {
        public PastJobView(PastJobState state, string text)
        {
            State = state;
            Text = text;
        }

        public PastJobState State { get; }
        public string Text { get; }
    }

    public enum ManualChipKind
    {
        All,
        Group,
        PastJobDiffers,
    }

    // R403. A chip of the window: All, one group of the block, or Past job differs
    public sealed class ManualChip
    {
        private ManualChip(ManualChipKind kind, string group)
        {
            Kind = kind;
            Group = group;
        }

        public static ManualChip All { get; } = new ManualChip(ManualChipKind.All, null);

        public static ManualChip PastJobDiffers { get; } = new ManualChip(ManualChipKind.PastJobDiffers, null);

        public static ManualChip OfGroup(string group)
        {
            return new ManualChip(ManualChipKind.Group, group);
        }

        public ManualChipKind Kind { get; }

        public string Group { get; }
    }

    // R403. The rows the window shows and the chips that pick them. facts gives the past job of a rule from the override list or the
    // build, null for none, and a rule it gives nothing for keeps the past job the block holds
    public sealed class ManualRuleTable
    {
        private readonly ManualRuleBlock block;
        private readonly Func<string, PastJobFact> facts;

        public ManualRuleTable(ManualRuleBlock block, Func<string, PastJobFact> facts)
        {
            this.block = block;
            this.facts = facts;
        }

        public ManualRuleBlock Block => block;

        // All, each group in the order the block first names it, then Past job differs
        public IReadOnlyList<ManualChip> Chips
        {
            get
            {
                var chips = new List<ManualChip> { ManualChip.All };
                chips.AddRange(block.Rules.Select(rule => rule.Group).Distinct(StringComparer.Ordinal).Select(ManualChip.OfGroup));
                chips.Add(ManualChip.PastJobDiffers);
                return chips;
            }
        }

        public IReadOnlyList<ManualRule> Rows(ManualChip chip)
        {
            switch (chip.Kind)
            {
                case ManualChipKind.Group: return block.Rules.Where(rule => rule.Group == chip.Group).ToList();
                case ManualChipKind.PastJobDiffers: return block.Rules.Where(rule => PastJob(rule).State == PastJobState.Differs).ToList();
                default: return block.Rules;
            }
        }

        public int CountOf(ManualChip chip)
        {
            return Rows(chip).Count;
        }

        public PastJobView PastJob(ManualRule rule)
        {
            PastJobFact fact = facts?.Invoke(rule.Id);
            if (fact != null)
            {
                return new PastJobView(fact.State, fact.Text);
            }
            return new PastJobView(rule.PastJobState, rule.PastJobState == PastJobState.NotMeasured ? null : rule.PastJob);
        }
    }
}
