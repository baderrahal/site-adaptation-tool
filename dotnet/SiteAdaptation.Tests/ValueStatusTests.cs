using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R401, the plan part. The status beside each sourced value of a plot, the step status of the parity harness and its counts by field and
    // status, the C# side of python/tests/test_value_status.py on the same made up map, block and row, on net48 and net8.0
    public sealed class ValueStatusTests
    {
        private const string ProfileFile = "rules/made.json";
        private const string Override = "{\"status\":\"RECOMMENDED\",\"question\":\"Q900\",\"where\":\"a made up list\"}";
        private const string EntryText = "{\"entry\":\"rules/made.json levelProfiles.p1.keyRules.entry.rule\",\"status\":\"MEASURED\",\"share\":0.5,\"strength\":\"weak\",\"measuredBy\":\"levelProfiles.p1.keyRules.entry\"}";
        private const string FflText = "{\"entry\":\"rules/made.json levelProfiles.p1.keyRules.ffl.rule\",\"status\":\"MEASURED\",\"share\":0.95,\"strength\":\"strong\",\"measuredBy\":\"levelProfiles.p1.keyRules.ffl\"}";
        private const string HandText = "{\"entry\":\"rules/made.json levelProfiles.p1.keyRules.hand alone.rule\",\"status\":\"MEASURED\",\"share\":1.0,\"strength\":\"strong\",\"measuredBy\":\"levelProfiles.p1.keyRules.hand alone\"}";

        // test_value_status.MAP and BLOCK
        private static JObject Map(string sets = null)
        {
            return JObject.Parse(@"{
            ""sources"": {""override"": " + Override + @", ""locationPlan"": {""status"": ""DOCUMENT"", ""document"": ""a made up plan""}},
            ""fields"": {""levels.entry"": {""estimate"": [{""profile"": ""levelProfiles.{levelProfile}.keyRules.entry.rule""}]},
                         ""levels.ffl"": {""estimate"": {""profile"": ""levelProfiles.{levelProfile}.keyRules.ffl.rule""}},
                         ""levels.height"": {""estimate"": [{""profile"": ""levelProfiles.{levelProfile}.keyRules.ffl.rule""}, {""profile"": ""levelProfiles.{levelProfile}.keyRules.entry.rule""}]},
                         ""insert"": {""computed"": [{""profile"": ""levelProfiles.{levelProfile}.keyRules.hand {hand}.rule""}, {""profile"": ""levelProfiles.{levelProfile}.keyRules.ffl.rule""}]},
                         ""mirrored"": {""computed"": {""profile"": ""levelProfiles.{levelProfile}.keyRules.hand {hand}.rule""}},
                         ""parameters.*"": {""keyRules"": {""profile"": ""levelProfiles.{levelProfile}.keyRules.parameter {parameter}.rule""}},
                         ""parameters.CASE"": {""keyRules"": {""config"": ""made.caseRule""}},
                         ""floors.generic.type"": {""computed"": {""config"": ""floors.types.generic""}},
                         ""floors.generic.level"": {""computed"": {""profile"": ""levelProfiles.{levelProfile}.keyRules.notInTheBlock""}}},
            ""overrideSets"": " + (sets ?? @"{""entryM"": [""levels.entry"", ""levels.height""], ""heightM"": [""levels.ffl"", ""insert""]}") + @",
            ""overrideReads"": {""entryM"": {""levels.height"": [{""profile"": ""levelProfiles.{levelProfile}.keyRules.ffl.rule""}]}}, ""movedBy"": {},
            ""assumed"": [""RECOMMENDED"", ""CHOSEN by Code"", ""UNKNOWN"", ""MEASURED weak""], ""worstFirst"": [""UNKNOWN"", ""CHOSEN by Code"", ""RECOMMENDED"", ""MEASURED weak""]}");
        }

        private static JObject Without(string text, string key)
        {
            JObject found = JObject.Parse(text);
            found.Remove(key);
            return found;
        }

        private static readonly JObject Block = new JObject
        {
            ["note"] = "a made up block",
            [ProfileFile] = new JObject
            {
                ["levelProfiles.p1.keyRules.entry.rule"] = Without(EntryText, "entry"), ["levelProfiles.p1.keyRules.ffl.rule"] = Without(FflText, "entry"),
                ["levelProfiles.p1.keyRules.hand alone.rule"] = Without(HandText, "entry"),
                ["levelProfiles.p1.keyRules.parameter X.rule"] = JObject.Parse("{\"status\": \"CHOSEN by Bader\", \"question\": \"Q901\", \"where\": \"made up\"}"),
            },
            ["config/project.json"] = JObject.Parse("{\"floors.types.generic\": {\"status\": \"UNKNOWN\", \"why\": \"no Q\", \"nearestSource\": \"floors.note\"}, "
                + "\"made.caseRule\": {\"status\": \"CHOSEN by Code\", \"where\": \"note\"}}"),
        };

        private const string Key = "levelProfiles.parsons2024Rev6.keyRules.";

        // test_value_status.KEO, Q110: the keo_file of the DP2 typology, so the DP2 plots of the made row take a status for prototypeFile and the
        // VD2 plot, whose keo_file the made block lacks, is UNKNOWN
        private static JObject Keo()
        {
            return new JObject { ["status"] = "DOCUMENT", ["document"] = "a made up document", ["pageOrSheet"] = "UNKNOWN", ["source"] = "source" };
        }

        // test_value_status.MADE_BLOCK and MADE_CONFIG_BLOCK, the block of the made row in place of the real one
        private static JObject MadeBlock(string profileFile)
        {
            var weak = new JObject { ["status"] = "MEASURED", ["share"] = 0.5, ["strength"] = "weak", ["measuredBy"] = "made" };
            var strong = new JObject { ["status"] = "MEASURED", ["share"] = 0.97, ["strength"] = "strong", ["measuredBy"] = "made" };
            var profile = new JObject
            {
                [Key + "entry.rule"] = weak.DeepClone(), [Key + "ffl.rule"] = strong.DeepClone(), [Key + "steps.rule"] = strong.DeepClone(), [Key + "hand villa.rule"] = strong.DeepClone(),
                [Key + "hand cluster.rule"] = strong.DeepClone(), [Key + "hand alone.rule"] = strong.DeepClone(), [Key + "linkOrigin.rule"] = strong.DeepClone(),
                [Key + "linkRotation.rule"] = strong.DeepClone(), [Key + "parameter CASE.rule"] = weak.DeepClone(),
                [Key + "parameter RRE_ZZ_SP_RVT-Name.rule"] = new JObject { ["status"] = "CHOSEN by Bader", ["question"] = "Q902", ["where"] = "made up" },
                [Key + "rampSlope.rule"] = new JObject { ["status"] = "CHOSEN by Code", ["where"] = "a made up note" }, ["typologies.DP2.keo_file"] = Keo(),
            };
            var config = new JObject { ["floors.types.generic"] = new JObject { ["status"] = "UNKNOWN", ["why"] = "made up", ["nearestSource"] = "floors.note" } };
            return new JObject { ["note"] = "a made up block", [profileFile] = profile, ["config/project.json"] = config };
        }

        // test_value_status.MADE_COUNTS, since Q107 the steps fields read steps.riser_added and each heightOffset the elevation of its level, and a
        // field an override left at its value by the rule names no override
        private const string MadeCounts = "floors.generic.heightOffset:UNKNOWN=6 floors.generic.level:UNKNOWN=6 floors.generic.levelElevation:UNKNOWN=6 "
            + "floors.generic.outline:UNKNOWN=6 floors.generic.plotValue:DOCUMENT=6 floors.generic.type:UNKNOWN=6 "
            + "floors.generic.z:UNKNOWN=6 floors.ramp.corners:UNKNOWN=6 floors.ramp.heightOffset:UNKNOWN=6 floors.ramp.level:UNKNOWN=6 "
            + "floors.ramp.levelElevation:UNKNOWN=6 floors.ramp.outline:UNKNOWN=6 floors.ramp.plotValue:DOCUMENT=6 "
            + "floors.ramp.slopePercent:CHOSEN by Code=6 floors.ramp.type:CHOSEN by Code=5 floors.ramp.type:RECOMMENDED=1 "
            + "floors.yard.heightOffset:UNKNOWN=3 floors.yard.level:UNKNOWN=3 floors.yard.levelElevation:UNKNOWN=3 "
            + "floors.yard.outline:UNKNOWN=3 floors.yard.plotValue:DOCUMENT=3 floors.yard.type:UNKNOWN=3 floors.yard.z:UNKNOWN=3 "
            + "insert:MEASURED strong=3 insert:RECOMMENDED=3 levels.entry:MEASURED weak=5 levels.entry:RECOMMENDED=1 "
            + "levels.ffl:RECOMMENDED=1 levels.ffl:UNKNOWN=5 levels.height:MEASURED weak=4 levels.height:RECOMMENDED=2 "
            + "levels.ssl:UNKNOWN=6 levels.steps:RECOMMENDED=1 levels.steps:UNKNOWN=5 linkName:RECOMMENDED=2 linkName:UNKNOWN=4 "
            + "mirrored:MEASURED strong=5 mirrored:RECOMMENDED=1 parameters.CASE:MEASURED weak=4 parameters.CASE:RECOMMENDED=2 "
            + "parameters.RRE_ZZ_SP_Façade-Color:UNKNOWN=6 parameters.RRE_ZZ_SP_Façade-Design-Option:UNKNOWN=6 "
            + "parameters.RRE_ZZ_SP_Number-of-Steps:UNKNOWN=5 parameters.RRE_ZZ_SP_Plot-Number:UNKNOWN=6 "
            + "parameters.RRE_ZZ_SP_Prototype:UNKNOWN=6 parameters.RRE_ZZ_SP_Prototype-FFL:RECOMMENDED=1 "
            + "parameters.RRE_ZZ_SP_Prototype-FFL:UNKNOWN=5 parameters.RRE_ZZ_SP_Prototype-Foundation-Type:UNKNOWN=6 "
            + "parameters.RRE_ZZ_SP_Prototype-HVAC-Schedule-Type:UNKNOWN=6 parameters.RRE_ZZ_SP_RVT-Name:RECOMMENDED=2 "
            + "parameters.RRE_ZZ_SP_RVT-Name:UNKNOWN=4 prototypeFile:DOCUMENT=5 prototypeFile:UNKNOWN=1 rotationDeg:MEASURED strong=3 rotationDeg:RECOMMENDED=3 "
            + "unit:DOCUMENT=6";

        private static ValueStatusTable Table(string levelProfile = "p1")
        {
            return new ValueStatusTable(Map(), Block, ProfileFile, levelProfile);
        }

        private static string Text(Tuple<string, JObject, string> found)
        {
            return found.Item1 + " " + found.Item2.ToString(Formatting.None) + " | " + found.Item3;
        }

        [Fact]
        public void R401_a_value_takes_the_status_of_the_block_entry_its_field_and_source_name_with_the_entry_and_its_evidence()
        {
            ValueStatusTable table = Table();
            Assert.Equal("MEASURED {\"entries\":[" + EntryText + "],\"strength\":\"weak\"} | levels.entry from estimate", Text(table.Of("levels.entry", "estimate")));
            Assert.Equal("MEASURED {\"entries\":[" + FflText + "],\"strength\":\"strong\"} | levels.ffl from estimate", Text(table.Of("levels.ffl", "estimate")));
            Assert.Equal("MEASURED {\"entries\":[" + HandText + "],\"strength\":\"strong\"} | mirrored from computed hand alone", Text(table.Of("mirrored", "computed", "alone")));
            Assert.Equal("CHOSEN by Bader {\"entries\":[{\"entry\":\"rules/made.json levelProfiles.p1.keyRules.parameter X.rule\",\"status\":\"CHOSEN by Bader\",\"question\":\"Q901\",\"where\":\"made up\"}]}"
                + " | parameters.X from keyRules", Text(table.Of("parameters.X", "keyRules")));
            Assert.Equal("CHOSEN by Code {\"entries\":[{\"entry\":\"config/project.json made.caseRule\",\"status\":\"CHOSEN by Code\",\"where\":\"note\"}]} | parameters.CASE from keyRules",
                Text(table.Of("parameters.CASE", "keyRules")));
            Assert.Equal("UNKNOWN", table.Of("floors.generic.type", "computed").Item1);
        }

        [Fact]
        public void R401_a_field_of_several_block_entries_takes_the_first_assumed_one_else_the_first_and_lists_each()
        {
            ValueStatusTable table = Table();
            Assert.Equal("MEASURED {\"entries\":[" + FflText + "," + EntryText + "],\"strength\":\"weak\"} | levels.height from estimate", Text(table.Of("levels.height", "estimate")));
            Assert.Equal("MEASURED {\"entries\":[" + HandText + "," + FflText + "],\"strength\":\"strong\"} | insert from computed hand alone", Text(table.Of("insert", "computed", "alone")));
            Assert.Equal("UNKNOWN {\"entries\":[{\"entry\":\"levelProfiles.{levelProfile}.keyRules.hand {hand}.rule\",\"status\":\"UNKNOWN\",\"why\":\"the plot holds no hand (R401)\"}," + FflText
                + "]} | insert from computed hand None", Text(table.Of("insert", "computed")));
        }

        [Fact]
        public void R401_an_override_and_a_client_input_take_the_status_the_map_gives_their_source()
        {
            ValueStatusTable table = Table();
            Assert.Equal("RECOMMENDED {\"entries\":[" + Override + "]} | parameters.X from override", Text(table.Of("parameters.X", "override")));
            Assert.Equal("DOCUMENT {\"entries\":[{\"status\":\"DOCUMENT\",\"document\":\"a made up plan\"}]} | unit from locationPlan", Text(table.Of("unit", "locationPlan")));
        }

        [Fact]
        public void R401_an_override_marks_every_field_it_moves_with_the_status_of_the_override_list()
        {
            ValueStatusTable table = Table();
            Assert.Equal("RECOMMENDED {\"entries\":[" + Override + "],\"override\":\"entryM\"} | levels.entry from override by entryM",
                Text(table.Of("levels.entry", "estimate", null, new[] { "entryM", "heightM" })));
            Assert.Equal("RECOMMENDED", table.Of("levels.height", "estimate", null, new[] { "entryM" }).Item1);
            Assert.Equal("{\"entries\":[" + Override + "],\"override\":\"heightM\"}", table.Of("insert", "computed", "alone", new[] { "heightM" }).Item2.ToString(Formatting.None));
            Assert.Equal("MEASURED", table.Of("levels.ffl", "estimate", null, new[] { "entryM" }).Item1);
        }

        [Fact]
        public void R401_an_override_set_of_another_shape_is_refused_with_one_line()
        {
            const string line = "valueStatus.overrideSets maps each override of the list to the list of the fields it moves, the settings hold {0} (R401)";
            foreach (Tuple<string, string> shape in new[]
            {
                Tuple.Create("{\"entryM\": \"levels.entry\"}", "entryM: \"levels.entry\""), Tuple.Create("null", "none"),
                Tuple.Create("{\"entryM\": [\"levels.entry\", 3]}", "entryM: [\"levels.entry\", 3]"),
            })
            {
                var refused = Assert.Throws<InvalidDataException>(() => new ValueStatusTable(Map(shape.Item1), Block, ProfileFile, "p1"));
                Assert.Equal(string.Format(line, shape.Item2), refused.Message);
            }
            JObject noSets = Map();
            noSets.Remove("overrideSets");
            Assert.Throws<InvalidDataException>(() => new ValueStatusTable(noSets, Block, ProfileFile, "p1"));
        }

        [Fact]
        public void R401_a_pair_the_map_does_not_hold_or_an_entry_the_block_lacks_is_unknown_never_a_guess()
        {
            ValueStatusTable table = Table();
            Assert.Equal("UNKNOWN {\"entries\":[{\"status\":\"UNKNOWN\",\"why\":\"valueStatus holds no status for prototypeFile from computed (R401)\"}]} | prototypeFile from computed",
                Text(table.Of("prototypeFile", "computed")));
            Assert.Equal("UNKNOWN", table.Of("levels.entry", "keyRules").Item1);
            Assert.Equal("{\"entries\":[{\"entry\":\"rules/made.json levelProfiles.p1.keyRules.notInTheBlock\",\"status\":\"UNKNOWN\",\"why\":\"the block status holds no entry (R401)\"}]}",
                table.Of("floors.generic.level", "computed").Item2.ToString(Formatting.None));
            Assert.Equal("UNKNOWN {\"entries\":[{\"entry\":\"levelProfiles.{levelProfile}.keyRules.hand {hand}.rule\",\"status\":\"UNKNOWN\",\"why\":\"the plot holds no hand (R401)\"}]}"
                + " | mirrored from computed hand None", Text(table.Of("mirrored", "computed")));
            Assert.Equal("UNKNOWN", Table("p2").Of("levels.entry", "estimate").Item1);
            Assert.Equal("UNKNOWN", new ValueStatusTable(null, new JObject(), ProfileFile, "p1").Of("unit", "locationPlan").Item1);
            Assert.Equal("MEASURED weak|MEASURED|UNKNOWN", ValueStatus.Label("MEASURED", new JObject { ["strength"] = "weak" }) + "|" + ValueStatus.Label("MEASURED", new JObject())
                + "|" + ValueStatus.Label("UNKNOWN", new JObject { ["strength"] = "weak" }));
        }

        private static Dictionary<string, Dictionary<string, object>> MadeStep(OverrideList list, out OverrideParameterStepTests.Built built)
        {
            return MadeStep(list, out built, out _);
        }

        // The step status of the made row with the overrides of list, the plans of the made row with no override its values by the rule (R3 of the
        // review of PR 75)
        private static Dictionary<string, Dictionary<string, object>> MadeStep(OverrideList list, out OverrideParameterStepTests.Built built, out ValueStatusTable table)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            string profileFile = (string)config["profile"];
            built = OverrideParameterStepTests.Build(list, changeRules: rules => rules["status"] = MadeBlock(profileFile));
            OverrideParameterStepTests.Built plain = OverrideParameterStepTests.Build(new OverrideList(new Dictionary<string, JObject>()), changeRules: rules => rules["status"] = MadeBlock(profileFile));
            table = ValueStatus.Table(built.Config, built.Rules, "parsons2024Rev6");
            return ValueStatus.Step(built.Plans.Keys.ToList(), built.Plans, built.Hands, table, plain.Plans);
        }

        // The map of config/project.json on a made block of the rules profile
        private static ValueStatusTable RealTable(JObject block)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            return new ValueStatusTable((JObject)config["valueStatus"], new JObject { [(string)config["profile"]] = block }, (string)config["profile"], "parsons2024Rev6");
        }

        // test_r401_q107_review1_a_height_offset_takes_the_unknown_elevation_of_its_level_in_the_new_site_file: floors.levels names the level,
        // not its elevation in the new site file, which no block entry maps, so a heightOffset of entries MEASURED weak beside it is UNKNOWN
        [Fact]
        public void R401_Q107_review1_a_height_offset_takes_the_unknown_elevation_of_its_level_in_the_new_site_file()
        {
            var block = new JObject();
            foreach (string name in new[] { "generic.rule", "yard.rule", "ffl.rule", "rampFront.rule", "entry.rule" })
            {
                block[Key + name] = new JObject { ["status"] = "MEASURED", ["share"] = 0.5, ["strength"] = "weak" };
            }
            ValueStatusTable table = RealTable(block);
            foreach (string kind in new[] { "generic", "yard", "ramp" })
            {
                Tuple<string, JObject, string> found = table.Of("floors." + kind + ".heightOffset", "estimate");
                Assert.Equal("UNKNOWN MEASURED,MEASURED,UNKNOWN", found.Item1 + " " + string.Join(",", found.Item2["entries"].Select(entry => (string)entry["status"])));
                Assert.DoesNotContain(found.Item2["entries"], entry => ((string)entry["entry"] ?? "").Contains("floors.levels"));
                Assert.Contains("elevation", (string)found.Item2["entries"][2]["why"]);
                Assert.Equal("UNKNOWN", table.Of("floors." + kind + ".levelElevation", "computed").Item1);
            }
        }

        // test_r401_q107_review1_a_field_an_override_moved_but_the_code_still_computes_takes_the_worst_of_the_override_and_what_it_reads
        [Fact]
        public void R401_Q107_review1_a_field_an_override_moved_but_the_code_still_computes_takes_the_worst_of_the_override_and_what_it_reads()
        {
            var block = new JObject();
            foreach (string name in new[] { "steps.rule", "entry.rule", "ffl.rule" })
            {
                block[Key + name] = new JObject { ["status"] = "MEASURED", ["share"] = 0.97, ["strength"] = "strong" };
            }
            Tuple<string, JObject, string> found = RealTable(block).Of("levels.steps", "estimate", null, new[] { "heightM" });
            Assert.Equal("UNKNOWN levels.steps from override by heightM heightM RECOMMENDED,MEASURED,UNKNOWN,MEASURED", found.Item1 + " " + found.Item3 + " " + (string)found.Item2["override"] + " "
                + string.Join(",", found.Item2["entries"].Select(entry => (string)entry["status"])));
            var riser = (JObject)block.DeepClone();
            riser["steps.riser_added"] = new JObject { ["status"] = "MEASURED", ["share"] = 0.97, ["strength"] = "strong" };
            Assert.Equal("RECOMMENDED", RealTable(riser).Of("levels.steps", "estimate", null, new[] { "heightM" }).Item1);
            found = RealTable(block).Of("levels.ffl", "estimate", null, new[] { "heightM" });
            Assert.Equal("RECOMMENDED 1", found.Item1 + " " + found.Item2["entries"].Count());
            Assert.Equal("RECOMMENDED {\"entries\":[" + Override + "," + FflText + "],\"override\":\"entryM\"} | levels.height from override by entryM",
                Text(Table().Of("levels.height", "estimate", null, new[] { "entryM" })));
        }

        // test_r401_q107_review1_an_override_reads_list_of_another_shape_or_of_a_field_its_set_does_not_hold_is_refused_with_one_line
        [Fact]
        public void R401_Q107_review1_an_override_reads_list_of_another_shape_or_of_a_field_its_set_does_not_hold_is_refused_with_one_line()
        {
            const string line = "valueStatus.overrideReads maps an override of overrideSets to the fields of its set the code still computes from other entries, each to "
                + "the list of them, the settings hold {0} (R401)";
            foreach (Tuple<string, string> shape in new[]
            {
                Tuple.Create("{\"entryM\": {\"levels.ffl\": [{\"profile\": \"a\"}]}}", "entryM: {\"levels.ffl\": [{\"profile\": \"a\"}]}"), Tuple.Create("null", "none"),
                Tuple.Create("{\"entryM\": {\"levels.height\": {\"profile\": \"a\"}}}", "entryM: {\"levels.height\": {\"profile\": \"a\"}}"), Tuple.Create("{\"other\": {}}", "other: {}"),
            })
            {
                JObject map = Map();
                map["overrideReads"] = JToken.Parse(shape.Item1);
                Assert.Equal(string.Format(line, shape.Item2), Assert.Throws<InvalidDataException>(() => new ValueStatusTable(map, Block, ProfileFile, "p1")).Message);
            }
            JObject moved = Map();
            moved["movedBy"] = JObject.Parse("{\"levels.height\": 3}");
            Assert.Equal("valueStatus.movedBy maps a field to the field whose value tells whether an override moved it, the settings hold {\"levels.height\": 3} (R401)",
                Assert.Throws<InvalidDataException>(() => new ValueStatusTable(moved, Block, ProfileFile, "p1")).Message);
        }

        // test_r401_q107_review1_an_override_that_leaves_a_field_at_its_value_by_the_rule_did_not_move_it
        [Fact]
        public void R401_Q107_review1_an_override_that_leaves_a_field_at_its_value_by_the_rule_did_not_move_it()
        {
            Assert.Equal(Text(Table().Of("levels.height", "estimate")), Text(Table().Of("levels.height", "estimate", null, new[] { "entryM" }, true)));
        }

        // test_r401_q107_review1_the_build_names_no_override_for_a_value_it_left_at_the_rule: a heightM gap of 0 leaves the FFL of 2 at the rule
        [Fact]
        public void R401_Q107_review1_the_build_names_no_override_for_a_value_it_left_at_the_rule()
        {
            var list = new OverrideList(new Dictionary<string, JObject>
            {
                ["2"] = JObject.Parse("{\"heightM\": {\"gapToRuleM\": 0.0}}"), ["3"] = JObject.Parse("{\"heightM\": {\"gapToRuleM\": 0.1}}"),
            });
            Dictionary<string, Dictionary<string, object>> step = MadeStep(list, out OverrideParameterStepTests.Built built);
            Assert.Equal("heightM", string.Join(" ", (List<string>)built.Plans["2"].Overrides["applied"]));
            Assert.Equal("levels.ffl from estimate | levels.steps from estimate | levels.ffl from override by heightM", (string)Field(step, "2", "levels.ffl")["statusFrom"] + " | "
                + (string)Field(step, "2", "levels.steps")["statusFrom"] + " | " + (string)Field(step, "3", "levels.ffl")["statusFrom"]);
        }

        // test_r401_q107_review1_a_ramp_height_offset_is_held_by_its_lowest_corner_which_an_entry_gap_may_leave_as_it_was: the heightOffset of a
        // ramp is its lowest corner less the elevation of its level, so an entryM gap that moves the back corners above the front leaves it
        [Fact]
        public void R401_Q107_review1_a_ramp_height_offset_is_held_by_its_lowest_corner_which_an_entry_gap_may_leave_as_it_was()
        {
            Assert.Equal("floors.ramp.corners lowest", RealTable(new JObject()).MovedBy("floors.ramp.heightOffset"));
            Func<double[], PlotPlan> plan = corners => new PlotPlan { Overrides = new Dictionary<string, object> { ["rampZ"] = corners }, Parameters = new Dictionary<string, object>() };
            var decimals = new Dictionary<string, int> { ["rampZ"] = 3 };
            PlotPlan moved = plan(new[] { 10.0, 10.1, 10.4, 10.3 }), rule = plan(new[] { 10.0, 10.1, 10.35, 10.25 }), lower = plan(new[] { 9.95, 10.1, 10.4, 10.3 });
            Assert.True(ValueStatus.Unmoved("floors.ramp.corners lowest", moved, rule, decimals));
            Assert.False(ValueStatus.Unmoved("floors.ramp.corners", moved, rule, decimals));
            Assert.False(ValueStatus.Unmoved("floors.ramp.corners lowest", lower, rule, decimals));
        }

        // test_r401_q107_review1_the_yard_outline_names_the_insert_override_applied_after_the_mirror_flag: the build applies the mirror flag before
        // the insert, where the sorted list puts insert first, so the yard outline both move names insert, the last applied
        [Fact]
        public void R401_Q107_review1_the_yard_outline_names_the_insert_override_applied_after_the_mirror_flag()
        {
            var list = new OverrideList(new Dictionary<string, JObject> { ["4"] = JObject.Parse("{\"mirrored\": false, \"insert\": {\"gapToRuleM\": [0.3, -0.2]}}") });
            Dictionary<string, Dictionary<string, object>> step = MadeStep(list, out OverrideParameterStepTests.Built built);
            Assert.Equal("insert mirrored | mirrored insert", string.Join(" ", (List<string>)built.Plans["4"].Overrides["applied"]) + " | " + string.Join(" ", built.Plans["4"].AppliedOrder));
            Assert.EndsWith(" by insert", (string)Field(step, "4", "floors.yard.outline")["statusFrom"]);
            Assert.Equal("insert from override", (string)Field(step, "4", "insert")["statusFrom"]);
        }

        private static JToken Field(Dictionary<string, Dictionary<string, object>> step, string number, string field)
        {
            return ((JObject)step[number]["fields"])[field];
        }

        // test_r401_q107_a_value_of_several_entries_takes_the_worst_status_in_the_order_of_valueStatus_worst_first
        [Fact]
        public void R401_Q107_a_value_of_several_entries_takes_the_worst_status_in_the_order_of_valueStatus_worst_first()
        {
            JObject map = Map();
            map["fields"]["levels.ssl"] = JObject.Parse("{\"estimate\": [{\"profile\": \"levelProfiles.{levelProfile}.keyRules.entry.rule\"}, {\"config\": \"made.caseRule\"}]}");
            map["fields"]["levels.steps"] = JObject.Parse("{\"estimate\": [{\"profile\": \"levelProfiles.{levelProfile}.keyRules.parameter X.rule\"}, "
                + "{\"profile\": \"levelProfiles.{levelProfile}.keyRules.ffl.rule\"}]}");
            var table = new ValueStatusTable(map, Block, ProfileFile, "p1");
            Tuple<string, JObject, string> found = table.Of("levels.ssl", "estimate");
            Assert.Equal("CHOSEN by Code MEASURED,CHOSEN by Code False", found.Item1 + " " + string.Join(",", found.Item2["entries"].Select(entry => (string)entry["status"]))
                + " " + (found.Item2["strength"] != null));
            // No entry of the order: the first of the list, CHOSEN by Bader before MEASURED strong
            Assert.Equal("CHOSEN by Bader", table.Of("levels.steps", "estimate").Item1);
            Assert.Equal("{\"entries\":[" + FflText + "," + EntryText + "],\"strength\":\"weak\"}", table.Of("levels.height", "estimate").Item2.ToString(Formatting.None));
            Assert.Equal("UNKNOWN", table.Of("insert", "computed").Item1);
        }

        // test_r401_q107_a_missing_status_order_or_one_holding_an_unknown_status_is_refused_with_one_line
        [Fact]
        public void R401_Q107_a_missing_status_order_or_one_holding_an_unknown_status_is_refused_with_one_line()
        {
            const string line = "valueStatus.worstFirst lists the statuses a value of several block entries takes first, the worst first, each of UNKNOWN, CHOSEN by Code, "
                + "CHOSEN by Bader, RECOMMENDED, MEASURED weak, MEASURED strong, MEASURED or DOCUMENT, the settings hold {0} (R401)";
            foreach (Tuple<string, string> shape in new[]
            {
                Tuple.Create("null", "none"), Tuple.Create("[\"UNKNOWN\", \"WRONG\"]", "[\"UNKNOWN\", \"WRONG\"]"), Tuple.Create("\"UNKNOWN\"", "\"UNKNOWN\""), Tuple.Create("[]", "[]"),
            })
            {
                JObject map = Map();
                map["worstFirst"] = JToken.Parse(shape.Item1);
                var refused = Assert.Throws<InvalidDataException>(() => new ValueStatusTable(map, Block, ProfileFile, "p1"));
                Assert.Equal(string.Format(line, shape.Item2), refused.Message);
            }
            JObject noOrder = Map();
            noOrder.Remove("worstFirst");
            Assert.Equal(string.Format(line, "none"), Assert.Throws<InvalidDataException>(() => new ValueStatusTable(noOrder, Block, ProfileFile, "p1")).Message);
        }

        // test_r401_q107_the_detail_names_the_override_applied_last_whose_set_holds_the_field
        [Fact]
        public void R401_Q107_the_detail_names_the_override_applied_last_whose_set_holds_the_field()
        {
            var table = new ValueStatusTable(Map("{\"entryM\": [\"levels.entry\", \"levels.height\"], \"heightM\": [\"levels.ffl\", \"levels.height\", \"insert\"]}"), Block, ProfileFile, "p1");
            Assert.Equal("RECOMMENDED {\"entries\":[" + Override + "],\"override\":\"heightM\"} | levels.height from override by heightM",
                Text(table.Of("levels.height", "estimate", null, new[] { "entryM", "heightM" })));
            Assert.Equal("levels.height from override by entryM", table.Of("levels.height", "estimate", null, new[] { "heightM", "entryM" }).Item3);
            Assert.Equal("levels.entry from override by entryM", table.Of("levels.entry", "estimate", null, new[] { "entryM", "heightM" }).Item3);
        }

        // test_r401_q107_the_build_names_the_override_that_moved_the_value_by_the_order_it_applied_them
        [Fact]
        public void R401_Q107_the_build_names_the_override_that_moved_the_value_by_the_order_it_applied_them()
        {
            var list = new OverrideList(new Dictionary<string, JObject>
            {
                ["2"] = JObject.Parse("{\"entryM\": {\"gapToRuleM\": 0.05}, \"heightM\": {\"gapToRuleM\": 0.1}}"), ["5"] = JObject.Parse("{\"entryM\": {\"gapToRuleM\": 0.05}, \"steps\": \"4\"}"),
                ["6"] = JObject.Parse("{\"entryM\": {\"gapToRuleM\": 0.05}, \"rampFrontM\": {\"gapToRuleM\": -0.05}}"),
            });
            Dictionary<string, Dictionary<string, object>> step = MadeStep(list, out OverrideParameterStepTests.Built built);
            Assert.Equal("entryM heightM | entryM heightM", string.Join(" ", (List<string>)built.Plans["2"].Overrides["applied"]) + " | " + string.Join(" ", built.Plans["2"].AppliedOrder));
            Func<string, string, string> by = (number, field) => ((string)Field(step, number, field)["statusFrom"]).Split(new[] { " by " }, StringSplitOptions.None).Last();
            Assert.Equal("entryM heightM heightM", by("2", "levels.entry") + " " + by("2", "levels.height") + " " + by("2", "levels.ffl"));
            // The step count of 2 stays that of the rule under both, so neither moved it (R3 of the review of PR 75)
            Assert.Equal("levels.steps from estimate | linkName from keyRules", (string)Field(step, "2", "levels.steps")["statusFrom"] + " | " + (string)Field(step, "2", "linkName")["statusFrom"]);
            Assert.Equal("steps steps entryM", by("5", "levels.steps") + " " + by("5", "parameters.RRE_ZZ_SP_RVT-Name") + " " + by("5", "levels.height"));
            Assert.Equal("rampFrontM rampFrontM entryM", by("6", "floors.ramp.heightOffset") + " " + by("6", "floors.ramp.corners") + " " + by("6", "levels.entry"));
        }

        [Fact]
        public void R401_parity_status_each_field_of_a_plot_with_its_source_status_and_detail()
        {
            // Since Q107 each field holds the key of its detail and the step file the details once in its table statusFrom, as the plan does, where
            // the detail copied into every value of the step held the build at 2,913 MiB
            Dictionary<string, Dictionary<string, object>> step = MadeStep(OverrideParameterStepTests.Made(), out OverrideParameterStepTests.Built built, out ValueStatusTable table);
            string profileFile = (string)built.Config["profile"];
            JObject details = (JObject)ValueStatus.Tables(step, table)["statusFrom"];
            Func<string, string, JToken> detail = (number, field) => details[(string)Field(step, number, field)["statusFrom"]];
            Assert.Equal("{\"source\":\"estimate\",\"status\":\"MEASURED\",\"statusFrom\":\"levels.entry from estimate\"}", Field(step, "1", "levels.entry").ToString(Formatting.None));
            Assert.Equal("{\"entries\":[{\"entry\":\"" + profileFile + " " + Key + "entry.rule\",\"status\":\"MEASURED\",\"share\":0.5,\"strength\":\"weak\",\"measuredBy\":\"made\"}],\"strength\":\"weak\"}",
                detail("1", "levels.entry").ToString(Formatting.None));
            Assert.Equal("{\"entries\":[{\"status\":\"RECOMMENDED\",\"question\":\"Q053\",\"where\":\"steps/investigations/rule-status-audit.md section 6\"}],\"override\":\"entryM\"}",
                detail("2", "levels.entry").ToString(Formatting.None));
            Assert.Equal("levels.entry from override by entryM", (string)Field(step, "2", "levels.entry")["statusFrom"]);
            Assert.Equal(profileFile + " " + Key + "hand cluster.rule", (string)detail("3", "mirrored")["entries"][0]["entry"]);
            Assert.Equal("RECOMMENDED - RECOMMENDED mirrored RECOMMENDED mirrored", string.Join(" ", new[] { "mirrored", "insert", "rotationDeg" }.Select(field =>
                (string)Field(step, "4", field)["status"] + " " + ((string)detail("4", field)["override"] ?? "-"))));
            Assert.Equal("RECOMMENDED override heightM override", (string)Field(step, "4", "parameters.CASE")["status"] + " " + (string)Field(step, "2", "parameters.CASE")["source"] + " "
                + (string)detail("3", "insert")["override"] + " " + (string)Field(step, "3", "parameters.RRE_ZZ_SP_Prototype-FFL")["source"]);
            // The table holds every key the fields name, once, in the order of the keys
            List<string> used = step.Values.Where(plot => plot["fields"] is JObject).SelectMany(plot => ((JObject)plot["fields"]).Properties().Select(field => (string)field.Value["statusFrom"]))
                .Distinct().OrderBy(key => key, StringComparer.Ordinal).ToList();
            Assert.Equal(string.Join("|", used), string.Join("|", details.Properties().Select(item => item.Name)));
            Assert.Equal("DOCUMENT UNKNOWN", (string)Field(step, "1", "unit")["status"] + " " + (string)Field(step, "1", "prototypeFile")["status"]);
            Assert.Equal("floors.generic.heightOffset floors.generic.level floors.generic.levelElevation floors.generic.outline floors.generic.plotValue floors.generic.type "
                + "floors.generic.z floors.ramp.corners floors.ramp.heightOffset floors.ramp.level floors.ramp.levelElevation floors.ramp.outline floors.ramp.plotValue "
                + "floors.ramp.slopePercent floors.ramp.type insert levels.entry levels.ffl levels.height levels.ssl levels.steps linkName mirrored parameters.CASE "
                + "parameters.RRE_ZZ_SP_Façade-Color parameters.RRE_ZZ_SP_Façade-Design-Option parameters.RRE_ZZ_SP_Number-of-Steps parameters.RRE_ZZ_SP_Plot-Number "
                + "parameters.RRE_ZZ_SP_Prototype parameters.RRE_ZZ_SP_Prototype-FFL parameters.RRE_ZZ_SP_Prototype-Foundation-Type parameters.RRE_ZZ_SP_Prototype-HVAC-Schedule-Type "
                + "parameters.RRE_ZZ_SP_RVT-Name prototypeFile rotationDeg unit",
                string.Join(" ", ((JObject)step["1"]["fields"]).Properties().Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal)));
            Assert.Equal(7, ((JObject)step["4"]["fields"]).Properties().Count(field => field.Name.StartsWith("floors.yard.", StringComparison.Ordinal)));
            // Plot 7 of the scope has no row, so no field
            Assert.Equal("None False None", (step["7"]["unit"] ?? "None") + " " + (bool)step["7"]["written"] + " " + (step["7"]["fields"] ?? "None"));
        }

        // The values of the steps overrides and parameters of a plot by the field of the plan each one is
        private static Dictionary<string, string> Values(PlotPlan plan)
        {
            var found = new Dictionary<string, string>();
            foreach (var pair in new[] { Tuple.Create("insert", "insert"), Tuple.Create("rotationDeg", "rotationDeg"), Tuple.Create("mirrored", "mirrored"), Tuple.Create("entry", "levels.entry"),
                Tuple.Create("ffl", "levels.ffl"), Tuple.Create("ssl", "levels.ssl"), Tuple.Create("height", "levels.height"), Tuple.Create("steps", "levels.steps"),
                Tuple.Create("rampZ", "floors.ramp.corners"), Tuple.Create("rampType", "floors.ramp.type"), Tuple.Create("yardZ", "floors.yard.z"), Tuple.Create("yardOutline", "floors.yard.outline") })
            {
                found[pair.Item2] = JsonConvert.SerializeObject(plan.Overrides[pair.Item1]);
            }
            foreach (JProperty parameter in (plan.Parameters["parameters"] as JObject ?? new JObject()).Properties())
            {
                found["parameters." + parameter.Name] = parameter.Value.ToString(Formatting.None);
            }
            found["linkName"] = JsonConvert.SerializeObject(plan.Parameters["linkName"]);
            return found;
        }

        [Fact]
        public void R401_every_value_an_override_moves_carries_the_status_of_the_override_list()
        {
            Dictionary<string, Dictionary<string, object>> step = MadeStep(OverrideParameterStepTests.Made(), out OverrideParameterStepTests.Built built, out ValueStatusTable table);
            MadeStep(new OverrideList(new Dictionary<string, JObject>()), out OverrideParameterStepTests.Built plain);
            var differ = new List<string>();
            var carry = new List<string>();
            var only = new List<string>();
            foreach (string number in new[] { "1", "2", "3", "4", "5", "6" })
            {
                Dictionary<string, string> mine = Values(built.Plans[number]), other = Values(plain.Plans[number]);
                foreach (KeyValuePair<string, string> value in mine.Where(item => ((JObject)step[number]["fields"])[item.Key] != null))
                {
                    bool carries = (string)table.Used[(string)Field(step, number, value.Key)["statusFrom"]]["entries"][0]["question"] == "Q053";
                    if (!other.TryGetValue(value.Key, out string before) || before == "null")
                    {
                        // A field the build with no override does not hold, as the yard of 4 its mirror flag override alone picks
                        only.Add(number + " " + value.Key + " " + carries);
                    }
                    else if (before != value.Value)
                    {
                        differ.Add(number + " " + value.Key);
                        if (carries)
                        {
                            carry.Add(number + " " + value.Key);
                        }
                    }
                }
            }
            Assert.Equal(string.Join(", ", differ), string.Join(", ", carry));
            Assert.Equal("4 floors.yard.z False, 4 floors.yard.outline True", string.Join(", ", only));
            Assert.True(differ.Count > 15, differ.Count.ToString());
        }

        [Fact]
        public void R401_the_counts_of_the_build_name_the_plots_by_field_and_status()
        {
            Dictionary<string, Dictionary<string, object>> step = MadeStep(OverrideParameterStepTests.Made(), out _, out ValueStatusTable table);
            Assert.Equal(MadeCounts, string.Join(" ", ValueStatus.Counts(step, table).Select(item => item.Key.Item1 + ":" + item.Key.Item2 + "=" + item.Value)));
        }

        [Fact]
        public void R401_the_worker_build_writes_the_step_status_and_the_config_names_its_fields()
        {
            using (var maker = new LevelFloorStepTests())
            {
                WorkerRequest request = maker.Request("p1", ValueStatus.StepName);
                string list = Path.Combine(request.OutputFolder, "overrides.json");
                var entry = new JObject { ["plot"] = "5", ["field"] = "parameter CASE", ["value"] = "H", ["source"] = "parsons", ["model"] = "a.rvt", ["key"] = "imc" };
                File.WriteAllText(list, new JObject { ["overrides"] = new JArray(entry) }.ToString());
                request.Inputs.Add(new WorkerFile { Role = "overrides", Path = list, Sha256 = FileHash.Sha256(list) });
                int exit = WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0);
                Assert.Equal("0 done", exit + " " + WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, "p1")).Message);
                JObject step = RunStamp.Read(Path.Combine(request.OutputFolder, "steps", "step_status_csharp.json"));
                JToken five = step["plots"]["5"]["written"];
                Assert.Equal("DP2 True locationPlan DOCUMENT override RECOMMENDED", (string)five["unit"] + " " + (bool)five["written"] + " " + (string)five["fields"]["unit"]["source"] + " "
                    + (string)five["fields"]["unit"]["status"] + " " + (string)five["fields"]["parameters.CASE"]["source"] + " " + (string)five["fields"]["parameters.CASE"]["status"]);
                // Q107: the key of the detail in each field and the details once in the table statusFrom of the file
                Assert.Equal("unit from locationPlan DOCUMENT", (string)five["fields"]["unit"]["statusFrom"] + " " + (string)step["tables"]["statusFrom"]["unit from locationPlan"]["entries"][0]["status"]);
                // The worker counts the plots by field and status as the summary of step 15 of Python does
                WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, "p1"));
                Assert.Equal(6, answer.Counts["status unit DOCUMENT"]);
                // Q110: and prototypeFile by the keo_file of the typology of each plot in the real block
                Assert.Equal(6, answer.Counts["status prototypeFile DOCUMENT"]);
            }
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("unit:noGeometry written:noGeometry fields:noGeometry", string.Join(" ", config.StepFields(ValueStatus.StepName).Select(field => field.Name + ":" + field.Kind + field.Decimals)));
            Assert.Equal("status", BuildSteps.Names.Last());
        }

        // test_value_status.Q110_DETAIL, Q110 (Bader, 2026-10-28): the entries valueStatus of config/project.json lists for prototypeFile, the unit of
        // the location plan, the file of the prototype folder named with it and the keo_file of its typology, which Hands.PrototypeFiles reads for
        // every typology
        private const string Q110Detail = "{\"entries\":[{\"status\":\"DOCUMENT\",\"document\":\"the location plan, labels.csv of the data book\"},{\"status\":\"DOCUMENT\",\"document\":"
            + "\"the prototype folder of the build, pastJob.upgradedPrototypeFolder, the file named with the unit code, else with the keo_file code of its typology (R128)\"},{\"entry\":\"{0} "
            + "typologies.VD1.keo_file\",\"status\":\"DOCUMENT\",\"document\":\"a made up document\",\"pageOrSheet\":\"UNKNOWN\",\"source\":\"source\"}]}";

        [Fact]
        public void R401_Q110_prototype_file_takes_the_worst_of_its_unit_its_prototype_folder_and_the_keo_file_of_its_typology()
        {
            string profileFile = (string)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["profile"];
            ValueStatusTable table = RealTable(new JObject { ["typologies.VD1.keo_file"] = Keo() });
            Assert.Equal("DOCUMENT " + Q110Detail.Replace("{0}", profileFile) + " | prototypeFile from computed unit VD1", Text(table.Of("prototypeFile", "computed", unit: "VD1")));
            // A unit whose typology the block holds no keo_file of, and a plot with no unit, are UNKNOWN with why, never a guess
            Assert.Equal("{\"entry\":\"" + profileFile + " typologies.XX.keo_file\",\"status\":\"UNKNOWN\",\"why\":\"the block status holds no entry (R401)\"}",
                table.Of("prototypeFile", "computed", unit: "XX").Item2["entries"].Last.ToString(Formatting.None));
            Tuple<string, JObject, string> none = table.Of("prototypeFile", "computed");
            Assert.Equal("UNKNOWN prototypeFile from computed unit None", none.Item1 + " " + none.Item3);
            // The key names the unit, so each unit keeps its own detail in the table statusFrom
            Assert.Equal("prototypeFile from computed unit None|prototypeFile from computed unit VD1|prototypeFile from computed unit XX",
                string.Join("|", table.Used.Keys.OrderBy(key => key, StringComparer.Ordinal)));
        }

        [Fact]
        public void R401_Q110_each_written_plot_holds_a_status_for_prototype_file_with_its_unit()
        {
            Dictionary<string, Dictionary<string, object>> step = MadeStep(OverrideParameterStepTests.Made(), out _, out ValueStatusTable table);
            Assert.Equal("{\"source\":\"computed\",\"status\":\"UNKNOWN\",\"statusFrom\":\"prototypeFile from computed unit VD2\"}", Field(step, "1", "prototypeFile").ToString(Formatting.None));
            Assert.Equal("{\"source\":\"computed\",\"status\":\"DOCUMENT\",\"statusFrom\":\"prototypeFile from computed unit DP2\"}", Field(step, "5", "prototypeFile").ToString(Formatting.None));
            Assert.Equal("DOCUMENT DOCUMENT DOCUMENT", string.Join(" ", table.Used["prototypeFile from computed unit DP2"]["entries"].Select(entry => (string)entry["status"])));
        }

        [Fact]
        public void R401_Q110_the_counts_of_step_15_name_prototype_file_by_field_and_status()
        {
            Dictionary<string, Dictionary<string, object>> step = MadeStep(OverrideParameterStepTests.Made(), out _, out ValueStatusTable table);
            Assert.Equal("prototypeFile:DOCUMENT=5 prototypeFile:UNKNOWN=1", string.Join(" ", ValueStatus.Counts(step, table).Where(item => item.Key.Item1 == "prototypeFile")
                .Select(item => item.Key.Item1 + ":" + item.Key.Item2 + "=" + item.Value)));
            // With the real block every plot of the made row takes the keo_file of its typology
            OverrideParameterStepTests.Built real = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made());
            ValueStatusTable realTable = ValueStatus.Table(real.Config, real.Rules, "parsons2024Rev6");
            Dictionary<string, Dictionary<string, object>> realStep = ValueStatus.Step(real.Plans.Keys.ToList(), real.Plans, real.Hands, realTable);
            Assert.Equal("prototypeFile:DOCUMENT=6", string.Join(" ", ValueStatus.Counts(realStep, realTable).Where(item => item.Key.Item1 == "prototypeFile")
                .Select(item => item.Key.Item1 + ":" + item.Key.Item2 + "=" + item.Value)));
        }

        // test_value_status.DECIDED, review 1 of PR 78: a made up majority.decidedByQuestion, the open Q900 decides the hand and the file of plot 5,
        // listed under the key k1
        private static readonly JObject Decided = JObject.Parse("{\"note\": \"a made up note\", \"Q900\": {\"fields\": [\"mirrored\", \"prototypeFile\"], \"plots\": {\"k1\": [\"5\"]}, \"why\": \"made up\"}}");
        private const string DecidedEntry = "{\"status\":\"RECOMMENDED\",\"question\":\"Q900\",\"where\":\"the Q ID in the path\"}";

        private static JObject DecidedBlock()
        {
            var block = (JObject)Block.DeepClone();
            block["config/project.json"]["majority.decidedByQuestion.Q900.plots.k1"] = JObject.Parse(DecidedEntry);
            return block;
        }

        [Fact]
        public void R401_review1_PR78_a_field_an_open_question_decides_on_a_plot_takes_the_worst_of_its_entries_and_that_question()
        {
            var table = new ValueStatusTable(Map(), DecidedBlock(), ProfileFile, "p1", decided: Decided);
            Assert.Equal("RECOMMENDED {\"entries\":[" + HandText + ",{\"entry\":\"config/project.json majority.decidedByQuestion.Q900.plots.k1\",\"status\":\"RECOMMENDED\",\"question\":\"Q900\","
                + "\"where\":\"the Q ID in the path\"}]} | mirrored from computed hand alone question Q900 k1", Text(table.Of("mirrored", "computed", "alone", plot: "5")));
            // Another plot, or a field the question does not name, keeps its own list
            Tuple<string, JObject, string> other = table.Of("mirrored", "computed", "alone", plot: "6");
            Assert.Equal("MEASURED mirrored from computed hand alone", other.Item1 + " " + other.Item3);
            Tuple<string, JObject, string> entry = table.Of("levels.entry", "estimate", plot: "5");
            Assert.Equal("MEASURED levels.entry from estimate", entry.Item1 + " " + entry.Item3);
            // A decided plot whose entry the block lacks is UNKNOWN, never a guess
            Tuple<string, JObject, string> lacking = new ValueStatusTable(Map(), Block, ProfileFile, "p1", decided: Decided).Of("mirrored", "computed", "alone", plot: "5");
            Assert.Equal("UNKNOWN {\"entry\":\"config/project.json majority.decidedByQuestion.Q900.plots.k1\",\"status\":\"UNKNOWN\",\"why\":\"the block status holds no entry (R401)\"}",
                lacking.Item1 + " " + lacking.Item2["entries"].Last.ToString(Formatting.None));
        }

        [Fact]
        public void R401_review1_PR78_the_plots_q017_decides_read_recommended_for_prototype_file_and_mirrored()
        {
            string configPath = WorkerRunTests.RepoConfig();
            JObject config = JObject.Parse(File.ReadAllText(configPath));
            JObject rules = JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(configPath)), (string)config["profile"])));
            ValueStatusTable table = ValueStatus.Table(config, rules, "parsons2024Rev6");
            Tuple<string, JObject, string> file = table.Of("prototypeFile", "computed", unit: "VD4", plot: "2301");
            JToken last = file.Item2["entries"].Last;
            Assert.Equal("RECOMMENDED prototypeFile from computed unit VD4 question Q017 imc config/project.json majority.decidedByQuestion.Q017.plots.imc RECOMMENDED Q017",
                file.Item1 + " " + file.Item3 + " " + (string)last["entry"] + " " + (string)last["status"] + " " + (string)last["question"]);
            Tuple<string, JObject, string> hand = table.Of("mirrored", "computed", "villa", plot: "544");
            Assert.Equal("RECOMMENDED mirrored from computed hand villa question Q017 img", hand.Item1 + " " + hand.Item3);
            Tuple<string, JObject, string> free = table.Of("prototypeFile", "computed", unit: "VD4", plot: "2303");
            Assert.Equal("DOCUMENT prototypeFile from computed unit VD4", free.Item1 + " " + free.Item3);
        }

        // test_value_status.test_r401_review1_pr78_the_build_marks_the_plot_an_open_question_decides, review 2 of PR 78: Step passes the plot number,
        // so the decided plot of the made row reads RECOMMENDED by its question and the others keep their status
        [Fact]
        public void R401_review1_PR78_the_build_marks_the_plot_an_open_question_decides()
        {
            Dictionary<string, Dictionary<string, object>> plain = MadeStep(OverrideParameterStepTests.Made(), out OverrideParameterStepTests.Built built, out _);
            var config = (JObject)built.Config.DeepClone();
            config["majority"]["decidedByQuestion"] = Decided.DeepClone();
            var rules = (JObject)built.Rules.DeepClone();
            rules["status"]["config/project.json"]["majority.decidedByQuestion.Q900.plots.k1"] = JObject.Parse(DecidedEntry);
            ValueStatusTable table = ValueStatus.Table(config, rules, "parsons2024Rev6");
            Dictionary<string, Dictionary<string, object>> step = ValueStatus.Step(built.Plans.Keys.ToList(), built.Plans, built.Hands, table);
            Assert.Equal("RECOMMENDED " + (string)Field(plain, "5", "mirrored")["statusFrom"] + " question Q900 k1",
                (string)Field(step, "5", "mirrored")["status"] + " " + (string)Field(step, "5", "mirrored")["statusFrom"]);
            Assert.Equal((string)Field(plain, "4", "mirrored")["status"] + " " + (string)Field(plain, "4", "mirrored")["statusFrom"],
                (string)Field(step, "4", "mirrored")["status"] + " " + (string)Field(step, "4", "mirrored")["statusFrom"]);
        }
    }
}
