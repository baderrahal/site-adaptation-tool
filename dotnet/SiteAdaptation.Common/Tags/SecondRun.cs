using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common.Tags
{
    // R368. One item of the plan of a button, in its scope or outside it. WasPlaced comes from the record of the last real run of the
    // button, since a model with no element of a key cannot tell an item deleted by hand from one never placed
    public sealed class PlanItem
    {
        public PlanItem(TagKey key, PlacedValues plan, bool inScope, bool wasPlaced)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            InScope = inScope;
            WasPlaced = wasPlaced;
        }

        public TagKey Key { get; }
        public PlacedValues Plan { get; }
        public bool InScope { get; }
        public bool WasPlaced { get; }
    }

    // An element the Revit side found: one with a tag and its values as the model holds them now, or one of a plot and kind with none
    public sealed class FoundElement
    {
        private FoundElement(string uniqueId, PlacedTag tag, string plot, string kind, PlacedValues current)
        {
            UniqueId = uniqueId ?? throw new ArgumentNullException(nameof(uniqueId));
            Tag = tag;
            Plot = plot;
            Kind = kind;
            Current = current;
        }

        public string UniqueId { get; }

        // Null for an element with no entity
        public PlacedTag Tag { get; }
        public string Plot { get; }
        public string Kind { get; }

        // Null for an element with no entity
        public PlacedValues Current { get; }

        public static FoundElement Tagged(string uniqueId, PlacedTag tag, PlacedValues current)
        {
            return new FoundElement(uniqueId, tag ?? throw new ArgumentNullException(nameof(tag)), tag.Key.Plot, tag.Key.Kind, current ?? throw new ArgumentNullException(nameof(current)));
        }

        public static FoundElement Untagged(string uniqueId, string plot, string kind)
        {
            return new FoundElement(uniqueId, null, plot, kind, null);
        }
    }

    // The cases of a second run in 03a, with New and OutsideScope for an item never placed
    public enum MatchCase
    {
        New,
        OutsideScope,
        AlreadyPlaced,
        PlanChanged,
        ChangedByHand,
        Gone,
        CopiesUnresolved,
        NotByThisTool,
    }

    // None leaves the model as it is, Place places the item, Update moves or updates the element of Element in place
    public enum MatchAction
    {
        None,
        Place,
        Update,
    }

    public sealed class MatchOutcome
    {
        private readonly SchemaVersion latest;

        internal MatchOutcome(PlanItem item, MatchCase matchCase, MatchAction action, FoundElement element, IReadOnlyList<string> differences, IReadOnlyList<string> listed, IReadOnlyList<string> issues, SchemaVersion deleteVersion, SchemaVersion latest)
        {
            Item = item;
            Case = matchCase;
            Action = action;
            Element = element == null ? null : element.UniqueId;
            Tag = element == null ? null : element.Tag;
            Differences = differences;
            Listed = listed;
            Issues = issues;
            DeleteVersion = deleteVersion;
            this.latest = latest;
        }

        public PlanItem Item { get; }
        public MatchCase Case { get; }
        public MatchAction Action { get; }

        // The UniqueId of the element the tool placed for the item, null when none or when it cannot tell which
        public string Element { get; }
        public PlacedTag Tag { get; }

        // The names of the values that differ: plan against the element when the plan changed, element against its own placed values
        // when it was changed by hand
        public IReadOnlyList<string> Differences { get; }

        // The UniqueIds listed and left: the copies, every element of the key when none or several hold their own UniqueId, or the
        // elements with no entity
        public IReadOnlyList<string> Listed { get; }
        public IReadOnlyList<string> Issues { get; }

        // The older version whose entity the update deletes in its transaction, after it writes the latest one
        public SchemaVersion DeleteVersion { get; }

        // The entity an update writes: the latest version, the plan values, the placedId the element keeps
        public PlacedTag NextTag(string run, string profile)
        {
            if (Action != MatchAction.Update)
            {
                throw new InvalidOperationException(Item.Key + " is not updated, its case is " + Case);
            }
            return new PlacedTag(latest.Version, Item.Key, run, profile, Tag.PlacedId, Item.Plan);
        }
    }

    public sealed class MatchResult
    {
        internal MatchResult(IReadOnlyList<MatchOutcome> outcomes, IReadOnlyList<string> unplanned)
        {
            Outcomes = outcomes;
            Unplanned = unplanned;
        }

        // One per plan item, in the order of the plan
        public IReadOnlyList<MatchOutcome> Outcomes { get; }

        // Tagged elements whose key no plan item holds, listed and never changed
        public IReadOnlyList<string> Unplanned { get; }
    }

    // R368, 03a A second run. Each item of the plan is reported and left by default, the tool never reverts a hand change on its own
    public static class SecondRun
    {
        private static readonly IReadOnlyList<string> None = new string[0];

        public static MatchResult Match(IEnumerable<PlanItem> items, IEnumerable<FoundElement> found, ICollection<string> setToPlan)
        {
            return Match(items, found, setToPlan, PlacedSchema.Versions);
        }

        // setToPlan holds the UniqueIds the user set to the plan in the summary, every other hand change is left
        public static MatchResult Match(IEnumerable<PlanItem> items, IEnumerable<FoundElement> found, ICollection<string> setToPlan, IReadOnlyList<SchemaVersion> versions)
        {
            List<PlanItem> plan = items.ToList();
            PlanItem twice = plan.GroupBy(item => item.Key).Where(group => group.Count() > 1).Select(group => group.First()).FirstOrDefault();
            if (twice != null)
            {
                throw new ArgumentException("two plan items hold " + twice.Key);
            }
            List<FoundElement> elements = found.ToList();
            // An element tagged by two versions, when a change stopped between the write and the delete, counts once by its latest tag
            List<FoundElement> tagged = elements.Where(element => element.Tag != null).GroupBy(element => element.UniqueId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(element => element.Tag.Version).First()).ToList();
            ILookup<TagKey, FoundElement> byKey = tagged.ToLookup(element => element.Tag.Key);
            ILookup<string, FoundElement> untagged = elements.Where(element => element.Tag == null).ToLookup(element => element.Plot + "\n" + element.Kind, StringComparer.Ordinal);
            SchemaVersion latest = versions[versions.Count - 1];

            var outcomes = new List<MatchOutcome>();
            foreach (PlanItem item in plan)
            {
                List<FoundElement> group = byKey[item.Key].OrderBy(element => element.UniqueId, StringComparer.Ordinal).ToList();
                if (group.Count == 0)
                {
                    outcomes.Add(Missing(item, untagged[item.Key.Plot + "\n" + item.Key.Kind].ToList(), latest));
                    continue;
                }
                List<FoundElement> holders = group.Where(element => string.Equals(element.UniqueId, element.Tag.PlacedId, StringComparison.Ordinal)).ToList();
                if (holders.Count != 1)
                {
                    string issue = item.Key + ": " + group.Count + " elements hold its tag and " + holders.Count + " of them its own UniqueId, every one is left and the item skipped";
                    outcomes.Add(new MatchOutcome(item, MatchCase.CopiesUnresolved, MatchAction.None, null, None, Ids(group), new[] { issue }, null, latest));
                    continue;
                }
                FoundElement placed = holders[0];
                List<FoundElement> copies = group.Where(element => element != placed).ToList();
                List<string> issues = copies.Select(copy => item.Key + ": element " + copy.UniqueId + " is a copy of plot " + item.Key.Plot + ", never changed").ToList();
                outcomes.Add(Tagged(item, placed, Ids(copies), issues, setToPlan, versions, latest));
            }
            var planned = new HashSet<TagKey>(plan.Select(item => item.Key));
            return new MatchResult(outcomes, Ids(tagged.Where(element => !planned.Contains(element.Tag.Key))));
        }

        private static MatchOutcome Missing(PlanItem item, List<FoundElement> byHand, SchemaVersion latest)
        {
            if (byHand.Count > 0)
            {
                IReadOnlyList<string> ids = Ids(byHand);
                string issue = item.Key + ": already placed, not by this tool, element " + string.Join(", ", ids) + " left and the item skipped";
                return new MatchOutcome(item, MatchCase.NotByThisTool, MatchAction.None, null, None, ids, new[] { issue }, null, latest);
            }
            MatchCase matchCase = item.WasPlaced ? MatchCase.Gone : item.InScope ? MatchCase.New : MatchCase.OutsideScope;
            return new MatchOutcome(item, matchCase, item.InScope ? MatchAction.Place : MatchAction.None, null, None, None, None, null, latest);
        }

        private static MatchOutcome Tagged(PlanItem item, FoundElement placed, IReadOnlyList<string> copies, IReadOnlyList<string> issues, ICollection<string> setToPlan, IReadOnlyList<SchemaVersion> versions, SchemaVersion latest)
        {
            MatchCase matchCase;
            MatchAction action = MatchAction.None;
            IReadOnlyList<string> differences = None;
            if (placed.Current.SameAs(item.Plan))
            {
                matchCase = MatchCase.AlreadyPlaced;
            }
            else if (placed.Current.SameAs(placed.Tag.Placed))
            {
                matchCase = MatchCase.PlanChanged;
                differences = item.Plan.Differences(placed.Current);
                action = item.InScope ? MatchAction.Update : MatchAction.None;
            }
            else
            {
                matchCase = MatchCase.ChangedByHand;
                differences = placed.Current.Differences(placed.Tag.Placed);
                action = item.InScope && setToPlan.Contains(placed.UniqueId) ? MatchAction.Update : MatchAction.None;
            }
            SchemaVersion delete = null;
            if (action == MatchAction.Update && placed.Tag.Version != latest.Version)
            {
                delete = versions.FirstOrDefault(version => version.Version == placed.Tag.Version);
                if (delete == null)
                {
                    throw new InvalidOperationException(item.Key + ": its tag is of version " + placed.Tag.Version + ", which the schema list does not hold");
                }
            }
            return new MatchOutcome(item, matchCase, action, placed, differences, copies, issues, delete, latest);
        }

        private static IReadOnlyList<string> Ids(IEnumerable<FoundElement> elements)
        {
            return elements.Select(element => element.UniqueId).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }
    }
}
