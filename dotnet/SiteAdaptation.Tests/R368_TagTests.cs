using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Tags;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R368, docs/design/03a-buttons-shared-and-1.md, What every button shares. The element tags, the match of a second run and the
    // schema versions
    public class R368_TagTests
    {
        private static readonly Regex SchemaName = new Regex("^[A-Za-z][A-Za-z0-9_]*$");

        private static PlacedValues Values(double x, double rotation)
        {
            return new PlacedValues(new Dictionary<string, double[]> { { "origin", new[] { x, 20.0, 0.0 } }, { "rotation", new[] { rotation } } });
        }

        private static PlanItem Item(string plot, PlacedValues plan, bool inScope = true, bool wasPlaced = true, string kind = PlacedKinds.Link, string part = "")
        {
            return new PlanItem(new TagKey(plot, kind, part), plan, inScope, wasPlaced);
        }

        private static PlacedTag Tag(string plot, string placedId, PlacedValues placed, int version = 1, string kind = PlacedKinds.Link, string part = "")
        {
            return new PlacedTag(version, new TagKey(plot, kind, part), "run1", "parsons2024Rev6", placedId, placed);
        }

        private static FoundElement Tagged(string uniqueId, PlacedTag tag, PlacedValues current)
        {
            return FoundElement.Tagged(uniqueId, tag, current);
        }

        private static MatchOutcome One(PlanItem item, params FoundElement[] found)
        {
            return SecondRun.Match(new[] { item }, found, new HashSet<string>()).Outcomes.Single();
        }

        [Fact]
        public void R368_the_placed_schema_lists_every_version_with_its_guid_name_and_field_names()
        {
            SchemaVersion first = PlacedSchema.Versions[0];
            Assert.Single(PlacedSchema.Versions);
            Assert.Equal(1, first.Version);
            Assert.Equal("SiteAdaptation_Placed", first.Name);
            Assert.Equal(new Guid("4437df95-47ea-4c18-98ba-8aa3d97386ac"), first.Guid);
            Assert.Equal(new[] { "plot", "kind", "part", "run", "profile", "placedId", "placed" }, first.Fields);
            Assert.Same(first, PlacedSchema.Latest);
            Assert.Equal("SiteAdaptation_Placed_2", PlacedSchema.NameOf(2));
            Assert.Equal(PlacedSchema.Versions.Count, PlacedSchema.Versions.Select(version => version.Guid).Distinct().Count());
            Assert.DoesNotContain(SettingsSchema.Guid, PlacedSchema.Versions.Select(version => version.Guid));
            Assert.Equal(SettingsSchema.VendorId, PlacedSchema.VendorId);
        }

        [Fact]
        public void R368_every_schema_and_field_name_is_letters_digits_and_underscore()
        {
            var names = new List<string> { SettingsSchema.Name, SettingsSchema.Field };
            foreach (SchemaVersion version in PlacedSchema.Versions)
            {
                Assert.Equal(PlacedSchema.NameOf(version.Version), version.Name);
                names.Add(version.Name);
                names.AddRange(version.Fields);
            }
            Assert.DoesNotContain(names, name => !SchemaName.IsMatch(name));
            Assert.DoesNotContain(names, name => !PlacedSchema.IsName(name));
            Assert.False(PlacedSchema.IsName("SiteAdaptation.Placed"));
            Assert.False(PlacedSchema.IsName("1Placed"));
            Assert.False(PlacedSchema.IsName(""));
        }

        [Fact]
        public void R368_the_kinds_are_link_generic_yard_ramp_and_boundary_wall()
        {
            Assert.Equal(new[] { "link", "generic", "yard", "ramp", "boundaryWall" }, PlacedKinds.All);
            Assert.Throws<ArgumentException>(() => new TagKey("2290", "floor", ""));
        }

        [Fact]
        public void R368_placed_values_are_rounded_by_r385_and_compared_at_its_decimals()
        {
            Assert.True(Values(10.0004, 90).SameAs(Values(10.0, 90.0)));
            Assert.False(Values(10.0005, 90).SameAs(Values(10.0, 90.0)));
            Assert.Equal(new[] { "origin" }, Values(10.0005, 90).Differences(Values(10.0, 90.0)));
            Assert.Equal(new[] { "origin", "rotation" }, Values(11, 91).Differences(Values(10.0, 90.0)));
            var floor = new PlacedValues(new Dictionary<string, double[]> { { "top", new[] { 1.0 } } });
            Assert.Equal(new[] { "origin", "rotation", "top" }, floor.Differences(Values(10.0, 90.0)));
            Assert.Equal(10.001, Values(10.0005, 90).Values["origin"][0]);
        }

        [Fact]
        public void R368_a_tag_is_written_as_string_fields_and_read_back_by_the_field_names_of_its_version()
        {
            PlacedTag tag = Tag("2290", "uid-a", Values(10.12345, 90), kind: PlacedKinds.BoundaryWall, part: "front");
            IReadOnlyDictionary<string, string> fields = tag.ToFields();
            Assert.Equal(PlacedSchema.Latest.Fields.OrderBy(name => name, StringComparer.Ordinal), fields.Keys.OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal("{\"origin\":[10.123,20,0],\"rotation\":[90]}", fields["placed"]);
            PlacedTag read = PlacedTag.FromFields(PlacedSchema.Versions[0], fields);
            Assert.Equal(tag.Key, read.Key);
            Assert.Equal("uid-a", read.PlacedId);
            Assert.True(read.Placed.SameAs(tag.Placed));
            Assert.Equal(1, read.Version);
            var missing = fields.Where(field => field.Key != "placedId").ToDictionary(field => field.Key, field => field.Value);
            Assert.Throws<InvalidDataException>(() => PlacedTag.FromFields(PlacedSchema.Versions[0], missing));
        }

        [Fact]
        public void R368_a_new_tag_holds_the_plan_values_the_unique_id_of_the_element_and_the_latest_version()
        {
            PlanItem item = Item("2290", Values(10, 90));
            PlacedTag tag = PlacedTag.ForPlacement(item, "run2", "parsons2024Rev6", "uid-new");
            Assert.Equal(PlacedSchema.Latest.Version, tag.Version);
            Assert.Equal("uid-new", tag.PlacedId);
            Assert.Equal("run2", tag.Run);
            Assert.True(tag.Placed.SameAs(item.Plan));
        }

        [Fact]
        public void R368_a_tagged_element_equal_to_the_plan_is_skipped_as_already_placed()
        {
            MatchOutcome outcome = One(Item("2290", Values(10, 90)), Tagged("uid-a", Tag("2290", "uid-a", Values(10, 90)), Values(10.0004, 90)));
            Assert.Equal(MatchCase.AlreadyPlaced, outcome.Case);
            Assert.Equal(MatchAction.None, outcome.Action);
            Assert.Equal("uid-a", outcome.Element);
        }

        [Fact]
        public void R368_a_tagged_element_equal_to_its_own_placed_values_but_not_the_plan_is_moved_as_the_plan_changed()
        {
            MatchOutcome outcome = One(Item("2290", Values(12, 90)), Tagged("uid-a", Tag("2290", "uid-a", Values(10, 90)), Values(10, 90)));
            Assert.Equal(MatchCase.PlanChanged, outcome.Case);
            Assert.Equal(MatchAction.Update, outcome.Action);
            Assert.Equal(new[] { "origin" }, outcome.Differences);
        }

        [Fact]
        public void R368_a_plan_change_outside_the_scope_is_listed_and_not_moved()
        {
            MatchOutcome outcome = One(Item("2290", Values(12, 90), inScope: false), Tagged("uid-a", Tag("2290", "uid-a", Values(10, 90)), Values(10, 90)));
            Assert.Equal(MatchCase.PlanChanged, outcome.Case);
            Assert.Equal(MatchAction.None, outcome.Action);
        }

        [Fact]
        public void R368_a_tagged_element_off_its_own_placed_values_is_listed_with_what_differs_and_left()
        {
            MatchOutcome outcome = One(Item("2290", Values(10, 90)), Tagged("uid-a", Tag("2290", "uid-a", Values(10, 90)), Values(10, 180)));
            Assert.Equal(MatchCase.ChangedByHand, outcome.Case);
            Assert.Equal(MatchAction.None, outcome.Action);
            Assert.Equal(new[] { "rotation" }, outcome.Differences);
        }

        [Fact]
        public void R368_a_hand_change_is_set_to_the_plan_only_for_an_element_the_user_sets_in_the_scope()
        {
            PlacedTag tag = Tag("2290", "uid-a", Values(10, 90));
            PlanItem[] items = { Item("2290", Values(10, 90)), Item("2291", Values(10, 90), inScope: false) };
            FoundElement[] found =
            {
                Tagged("uid-a", tag, Values(10, 180)),
                Tagged("uid-b", Tag("2291", "uid-b", Values(10, 90)), Values(10, 180)),
            };
            List<MatchOutcome> left = SecondRun.Match(items, found, new HashSet<string>()).Outcomes.ToList();
            Assert.All(left, outcome => Assert.Equal(MatchAction.None, outcome.Action));
            List<MatchOutcome> set = SecondRun.Match(items, found, new HashSet<string> { "uid-a", "uid-b" }).Outcomes.ToList();
            Assert.Equal(MatchCase.ChangedByHand, set[0].Case);
            Assert.Equal(MatchAction.Update, set[0].Action);
            Assert.Equal(MatchAction.None, set[1].Action);
        }

        [Fact]
        public void R368_a_tagged_element_deleted_by_hand_is_placed_again_in_the_scope_and_only_listed_outside_it()
        {
            MatchOutcome inside = One(Item("2290", Values(10, 90)));
            Assert.Equal(MatchCase.Gone, inside.Case);
            Assert.Equal(MatchAction.Place, inside.Action);
            MatchOutcome outside = One(Item("2290", Values(10, 90), inScope: false));
            Assert.Equal(MatchCase.Gone, outside.Case);
            Assert.Equal(MatchAction.None, outside.Action);
        }

        [Fact]
        public void R368_an_item_never_placed_is_placed_in_the_scope_and_left_outside_it()
        {
            MatchOutcome inside = One(Item("2290", Values(10, 90), wasPlaced: false));
            Assert.Equal(MatchCase.New, inside.Case);
            Assert.Equal(MatchAction.Place, inside.Action);
            MatchOutcome outside = One(Item("2290", Values(10, 90), inScope: false, wasPlaced: false));
            Assert.Equal(MatchCase.OutsideScope, outside.Case);
            Assert.Equal(MatchAction.None, outside.Action);
        }

        [Fact]
        public void R368_of_two_elements_with_one_key_the_one_holding_its_own_unique_id_stays_and_the_copy_is_listed_and_never_changed()
        {
            PlacedTag tag = Tag("2290", "uid-a", Values(10, 90));
            MatchOutcome outcome = One(Item("2290", Values(12, 90)), Tagged("uid-copy", tag, Values(40, 90)), Tagged("uid-a", tag, Values(10, 90)));
            Assert.Equal(MatchCase.PlanChanged, outcome.Case);
            Assert.Equal(MatchAction.Update, outcome.Action);
            Assert.Equal("uid-a", outcome.Element);
            Assert.Equal(new[] { "uid-copy" }, outcome.Listed);
            Assert.Contains(outcome.Issues, issue => issue.Contains("uid-copy") && issue.Contains("copy of plot 2290"));
        }

        [Fact]
        public void R368_when_no_element_or_several_hold_their_own_unique_id_every_element_of_the_key_is_listed_and_the_item_skipped()
        {
            PlacedTag tag = Tag("2290", "uid-a", Values(10, 90));
            MatchOutcome none = One(Item("2290", Values(10, 90)), Tagged("uid-b", tag, Values(10, 90)), Tagged("uid-c", tag, Values(10, 90)));
            Assert.Equal(MatchCase.CopiesUnresolved, none.Case);
            Assert.Equal(MatchAction.None, none.Action);
            Assert.Null(none.Element);
            Assert.Equal(new[] { "uid-b", "uid-c" }, none.Listed);
            MatchOutcome lone = One(Item("2290", Values(10, 90)), Tagged("uid-b", tag, Values(10, 90)));
            Assert.Equal(MatchCase.CopiesUnresolved, lone.Case);
            Assert.Equal(new[] { "uid-b" }, lone.Listed);
            PlacedTag other = Tag("2290", "uid-d", Values(10, 90));
            MatchOutcome several = One(Item("2290", Values(10, 90)), Tagged("uid-a", tag, Values(10, 90)), Tagged("uid-d", other, Values(10, 90)));
            Assert.Equal(MatchCase.CopiesUnresolved, several.Case);
            Assert.Equal(new[] { "uid-a", "uid-d" }, several.Listed);
        }

        [Fact]
        public void R368_an_element_of_the_same_plot_and_kind_with_no_tag_is_never_changed_and_the_item_is_skipped_as_not_by_this_tool()
        {
            MatchOutcome outcome = One(Item("2290", Values(10, 90), wasPlaced: false), FoundElement.Untagged("uid-hand", "2290", PlacedKinds.Link));
            Assert.Equal(MatchCase.NotByThisTool, outcome.Case);
            Assert.Equal(MatchAction.None, outcome.Action);
            Assert.Equal(new[] { "uid-hand" }, outcome.Listed);
            Assert.Contains(outcome.Issues, issue => issue.Contains("already placed, not by this tool"));
            MatchOutcome otherKind = One(Item("2290", Values(10, 90), wasPlaced: false), FoundElement.Untagged("uid-hand", "2290", PlacedKinds.Yard));
            Assert.Equal(MatchCase.New, otherKind.Case);
        }

        [Fact]
        public void R368_a_tagged_element_no_plan_item_holds_is_listed_and_never_changed()
        {
            MatchResult result = SecondRun.Match(new[] { Item("2290", Values(10, 90)) }, new[] { Tagged("uid-a", Tag("2290", "uid-a", Values(10, 90)), Values(10, 90)), Tagged("uid-z", Tag("2299", "uid-z", Values(1, 0)), Values(1, 0)) }, new HashSet<string>());
            Assert.Equal(new[] { "uid-z" }, result.Unplanned);
        }

        [Fact]
        public void R368_two_plan_items_with_one_key_are_refused()
        {
            Assert.Throws<ArgumentException>(() => SecondRun.Match(new[] { Item("2290", Values(10, 90)), Item("2290", Values(12, 90)) }, new FoundElement[0], new HashSet<string>()));
        }

        [Fact]
        public void R368_a_changed_element_tagged_by_an_older_version_gets_the_latest_entity_and_the_old_one_deleted()
        {
            var older = new SchemaVersion(0, new Guid("00000000-0000-0000-0000-000000000001"), "SiteAdaptation_Placed_0", PlacedSchema.Latest.Fields);
            PlacedTag old = Tag("2290", "uid-a", Values(10, 90), version: 0);
            MatchOutcome moved = SecondRun.Match(new[] { Item("2290", Values(12, 90)) }, new[] { Tagged("uid-a", old, Values(10, 90)) }, new HashSet<string>(), new[] { older, PlacedSchema.Latest }).Outcomes.Single();
            Assert.Equal(MatchAction.Update, moved.Action);
            Assert.Same(older, moved.DeleteVersion);
            PlacedTag next = moved.NextTag("run2", "parsons2024Rev6");
            Assert.Equal(PlacedSchema.Latest.Version, next.Version);
            Assert.Equal("uid-a", next.PlacedId);
            Assert.True(next.Placed.SameAs(Values(12, 90)));
            MatchOutcome kept = SecondRun.Match(new[] { Item("2290", Values(10, 90)) }, new[] { Tagged("uid-a", old, Values(10, 90)) }, new HashSet<string>(), new[] { older, PlacedSchema.Latest }).Outcomes.Single();
            Assert.Equal(MatchAction.None, kept.Action);
            Assert.Null(kept.DeleteVersion);
            MatchOutcome latest = One(Item("2290", Values(12, 90)), Tagged("uid-a", Tag("2290", "uid-a", Values(10, 90)), Values(10, 90)));
            Assert.Null(latest.DeleteVersion);
        }

        [Fact]
        public void R368_an_element_found_under_two_versions_counts_once_by_its_latest_tag()
        {
            var older = new SchemaVersion(0, new Guid("00000000-0000-0000-0000-000000000001"), "SiteAdaptation_Placed_0", PlacedSchema.Latest.Fields);
            FoundElement[] found = { Tagged("uid-a", Tag("2290", "uid-a", Values(10, 90), version: 0), Values(12, 90)), Tagged("uid-a", Tag("2290", "uid-a", Values(12, 90)), Values(12, 90)) };
            MatchOutcome outcome = SecondRun.Match(new[] { Item("2290", Values(12, 90)) }, found, new HashSet<string>(), new[] { older, PlacedSchema.Latest }).Outcomes.Single();
            Assert.Equal(MatchCase.AlreadyPlaced, outcome.Case);
            Assert.Empty(outcome.Listed);
        }

        // The cases of 03a written out as a table, row by row, and held against one match of every row at once
        [Fact]
        public void R368_one_run_over_every_row_of_the_case_table_of_03a_gives_the_case_and_action_of_each_row()
        {
            string[] tagged =
            {
                "plan in - AlreadyPlaced None", "plan out - AlreadyPlaced None", "plan in set AlreadyPlaced None", "plan out set AlreadyPlaced None",
                "own in - PlanChanged Update", "own out - PlanChanged None", "own in set PlanChanged Update", "own out set PlanChanged None",
                "hand in - ChangedByHand None", "hand out - ChangedByHand None", "hand in set ChangedByHand Update", "hand out set ChangedByHand None",
            };
            string[] missing =
            {
                "placed in - Gone Place", "placed out - Gone None", "never in - New Place", "never out - OutsideScope None",
                "placed in byHand NotByThisTool None", "placed out byHand NotByThisTool None", "never in byHand NotByThisTool None", "never out byHand NotByThisTool None",
            };
            PlacedValues plan = Values(12, 90);
            PlacedValues own = Values(10, 90);
            var items = new List<PlanItem>();
            var found = new List<FoundElement>();
            var set = new HashSet<string>();
            var expected = new List<string>();
            int plot = 3000;
            foreach (string[] row in tagged.Select(text => text.Split(' ')))
            {
                string id = "uid-" + plot;
                items.Add(Item(plot.ToString(), plan, inScope: row[1] == "in"));
                PlacedValues current = row[0] == "plan" ? plan : row[0] == "own" ? own : Values(10, 180);
                found.Add(Tagged(id, Tag(plot.ToString(), id, own), current));
                if (row[2] == "set")
                {
                    set.Add(id);
                }
                expected.Add(row[3] + " " + row[4]);
                plot++;
            }
            foreach (string[] row in missing.Select(text => text.Split(' ')))
            {
                items.Add(Item(plot.ToString(), plan, inScope: row[1] == "in", wasPlaced: row[0] == "placed"));
                if (row[2] == "byHand")
                {
                    found.Add(FoundElement.Untagged("uid-" + plot, plot.ToString(), PlacedKinds.Link));
                }
                expected.Add(row[3] + " " + row[4]);
                plot++;
            }
            List<string> got = SecondRun.Match(items, found, set).Outcomes.Select(outcome => outcome.Case + " " + outcome.Action).ToList();
            int matched = expected.Zip(got, (want, have) => want == have ? 1 : 0).Sum();
            Assert.Equal(20, expected.Count);
            Assert.Equal(expected, got);
            Assert.Equal(20, matched);
        }

        [Fact]
        public void R368_the_revit_side_collects_every_listed_version_by_extensible_storage_filter_and_deletes_the_old_entity_on_write()
        {
            string source = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Revit", "PlacedTagStore.cs"));
            Assert.Contains("PlacedSchema.Versions", source);
            Assert.Contains("ExtensibleStorageFilter", source);
            Assert.Contains("Schema.Lookup", source);
            Assert.Contains("DeleteEntity", source);
            Assert.Contains("IsModifiable", source);
            Assert.Contains("AccessLevel.Vendor", source);
            Assert.DoesNotMatch(new Regex(@"[0-9a-f]{8}-[0-9a-f]{4}-"), source);
        }
    }
}
