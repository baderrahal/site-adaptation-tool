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
    // R363. The check rows of button 3 and the chip Assumed of Q108, CheckRows of Common, the C# side of python/tests/test_check_rows.py on the
    // same made up plots, entries and statuses, so the two sides give one check table, on net48 and net8.0
    public sealed class CheckRowTests
    {
        private const string Gap = "a build places the outline of its rule and leaves these (LEFT of plan_from_book.py)";

        private static JObject Block()
        {
            return (JObject)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["checkRows"];
        }

        private static CheckPlot Plot(string unit, string nbh, string skip, string[] applied, string notApplied, params string[] lacks)
        {
            return new CheckPlot { Unit = unit, Nbh = nbh, Skip = skip, Applied = applied.ToList(), NotApplied = JObject.Parse(notApplied), Lacks = lacks.ToList() };
        }

        // test_check_rows.PLOTS: 1 follows every rule, 2 is written with overrides, a question and a yard it lacks, 3 is not written, 4 has no unit,
        // 5 is on the skip list
        private static readonly Dictionary<string, CheckPlot> Plots = new Dictionary<string, CheckPlot>
        {
            ["1"] = Plot("VD2", "N1", null, new string[0], "{}"),
            ["2"] = Plot("DP2", "N1", null, new[] { "entryM", "parameter CASE" }, "{\"rampFrontFallM\": \"a ramp front fall is not applied (Q057)\"}", "floors.yard"),
            ["3"] = Plot("DP2", "N1", null, new string[0], "{\"mirrored\": \"the plot is not written, it lacks levels.ffl\"}", "levels.ffl", "parameters.RRE_ZZ_SP_Prototype-FFL"),
            ["4"] = Plot(null, null, null, new string[0], "{}", "unit"),
            ["5"] = Plot("VD4", "N2", "a made up reason", new string[0], "{\"heightM\": \"the plot is not written, it lacks skip\"}", "skip"),
        };

        private const string List = @"{""overrides"": [
            {""plot"": ""2"", ""field"": ""entryM"", ""value"": {""gapToRuleM"": 0.0077}, ""rule"": null, ""decision"": ""parsons entry"", ""source"": ""parsons"", ""key"": ""imc"", ""model"": ""a.rvt""},
            {""plot"": ""2"", ""field"": ""entryM"", ""value"": {""gapToRuleM"": 0.5}, ""rule"": null, ""decision"": ""native entry"", ""source"": ""native"", ""key"": ""u1"", ""model"": ""b.rvt""},
            {""plot"": ""2"", ""field"": ""parameter CASE"", ""value"": ""F"", ""rule"": ""D"", ""decision"": ""parsons parameter CASE"", ""source"": ""parsons"", ""key"": ""imc"", ""model"": ""a.rvt""},
            {""plot"": ""2"", ""field"": ""rampFrontFallM"", ""value"": {""gapToRuleM"": 0.1}, ""rule"": null, ""decision"": ""parsons rampFrontFall"", ""source"": ""parsons"", ""key"": ""imc"", ""model"": ""a.rvt""},
            {""plot"": ""3"", ""field"": ""mirrored"", ""value"": true, ""rule"": false, ""decision"": ""parsons hand villa"", ""source"": ""parsons"", ""key"": ""imc"", ""model"": ""a.rvt""},
            {""plot"": ""5"", ""field"": ""heightM"", ""value"": {""gapToRuleM"": 0.001}, ""rule"": null, ""decision"": ""parsons ffl"", ""source"": ""parsons"", ""key"": ""imc"", ""model"": ""a.rvt""}],
          ""cannot"": [
            {""plot"": ""3"", ""field"": ""rampFrontM"", ""decision"": ""parsons rampFront"", ""reason"": ""a made up reason of the key"", ""source"": ""parsons"", ""key"": ""imc"", ""model"": ""a.rvt""},
            {""plot"": ""9"", ""field"": ""entryM"", ""decision"": ""parsons entry"", ""reason"": ""a plot outside the scope"", ""source"": ""parsons"", ""key"": ""imc"", ""model"": ""a.rvt""},
            {""plot"": ""2"", ""field"": ""heightM"", ""decision"": ""native ffl"", ""reason"": ""another job"", ""source"": ""native"", ""key"": ""u1"", ""model"": ""b.rvt""}]}";

        private const string Decided = @"{""note"": ""a made up note"", ""Q017"": {""fields"": [""prototypeFile"", ""mirrored""], ""plots"": {""imc"": [""2"", ""9""]}, ""why"": ""a made up why""}}";

        private static readonly string[] Numbers = { "1", "2", "3", "4", "5" };

        private static JObject Row(string check, string field, string came, string use, JToken rule = null, JToken tool = null, string writes = null, JToken value = null,
            string notOffered = null, string why = null)
        {
            return new JObject
            {
                ["check"] = check, ["field"] = field, ["from"] = came, ["use"] = use, ["rule"] = rule ?? JValue.CreateNull(), ["toolValue"] = tool ?? JValue.CreateNull(),
                ["past"] = new JObject { ["writes"] = writes, ["value"] = value ?? JValue.CreateNull(), ["why"] = notOffered }, ["why"] = why,
            };
        }

        private static Dictionary<string, List<JObject>> MadeRows()
        {
            Tuple<List<JObject>, List<JObject>> entries = CheckRows.ListEntries(JObject.Parse(List), new[] { "parsons" });
            return CheckRows.Rows(Numbers, Plots, entries.Item1, entries.Item2, JObject.Parse(Decided), Block());
        }

        private static void Same(IEnumerable<JObject> expected, IEnumerable<JObject> found)
        {
            Assert.Equal(new JArray(expected).ToString(Formatting.None), new JArray(found).ToString(Formatting.None));
        }

        [Fact]
        public void R363_parity_check_rows_one_row_per_plot_and_check_with_its_type_field_and_use()
        {
            Dictionary<string, List<JObject>> found = MadeRows();
            JObject block = Block();
            Assert.Empty(found["1"]);
            Same(new[]
            {
                Row("Floor", "floors.yard", "missing", "Rule", notOffered: Gap),
                Row("Hand", "mirrored", "question", "Rule", rule: "Q017", writes: "trueOrFalse", why: "a made up why"),
                Row("Unit code", "prototypeFile", "question", "Rule", rule: "Q017", why: "a made up why",
                    notOffered: "no field of the list gives a prototype file, the code is Typology_Rev9 (Q017) and never a key's (Q056)"),
                Row("Level", "entryM", "override", "Past", rule: "parsons entry", writes: "gapToRuleM", value: JObject.Parse("{\"gapToRuleM\": 0.0077}")),
                Row("Level", "rampFrontFallM", "override", "Rule", rule: "parsons rampFrontFall", value: JObject.Parse("{\"gapToRuleM\": 0.1}"),
                    notOffered: "a build never applies a fall (Q057)", why: "a ramp front fall is not applied (Q057)"),
                Row("Parameter", "parameter CASE", "override", "Past", rule: "parsons parameter CASE", tool: "D", writes: "text", value: "F"),
            }, found["2"]);
            Same(new[]
            {
                Row("Hand", "mirrored", "override", "Rule", rule: "parsons hand villa", tool: false, writes: "trueOrFalse", value: true, why: "the plot is not written, it lacks levels.ffl"),
                Row("Level", "levels.ffl", "missing", "Rule", notOffered: "the rule gives no level to hold a gap to"),
                Row("Level", "rampFrontM", "cannot", "Rule", rule: "parsons rampFront", notOffered: (string)block["cannotWhy"], why: "a made up reason of the key"),
                Row("Parameter", "parameters.RRE_ZZ_SP_Prototype-FFL", "missing", "Rule", notOffered: "the rule gives no value to hold a gap to"),
            }, found["3"]);
            Same(new[] { Row("Unit code", "unit", "missing", "Rule", notOffered: (string)block["missing"]["unit"]["why"]) }, found["4"]);
            // A plot on the skip list opens every row on Skip
            Same(new[]
            {
                Row("Level", "heightM", "override", "Skip", rule: "parsons ffl", writes: "gapToRuleM", value: JObject.Parse("{\"gapToRuleM\": 0.001}"), why: "the plot is not written, it lacks skip"),
                Row("Skip", "skip", "missing", "Skip", notOffered: (string)block["missing"]["skip"]["why"]),
            }, found["5"]);
        }

        [Fact]
        public void R363_past_writes_in_the_form_of_the_list_by_check_and_field_as_the_design_gives_it()
        {
            JObject block = Block();
            Assert.Equal("entryM Level gapToRuleM|heightM Level gapToRuleM|rampFrontM Level gapToRuleM|steps Level wholeNumber|rampFrontFallM Level |insert Position gapToRuleM|"
                + "rotationDeg Position gapToRuleDeg|mirrored Hand trueOrFalse|rampType Floor slopeClass|genericFloor Floor |yardOutline Floor |rampOutline Floor |"
                + "parameter RRE_ZZ_SP_Prototype-FFL Parameter gapToRuleM|parameter * Parameter text|prototypeFile Unit code ",
                string.Join("|", ((JObject)block["fields"]).Properties().Select(item => item.Name + " " + (string)item.Value["check"] + " " + (string)item.Value["past"])));
            Assert.Equal("Plot cut|Floor|Hand|Unit code|Position|Level", string.Join("|", block["types"].Select(item => (string)item)));
            Assert.Equal("text", (string)CheckRows.EntryOf(block, "fields", "parameter RRE_ZZ_SP_RVT-Name")["past"]);
            Assert.Equal("Level", (string)CheckRows.EntryOf(block, "missing", "levels.entry")["check"]);
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => CheckRows.EntryOf(block, "fields", "foo"));
            Assert.Equal("checkRows.fields names no check for foo (R363)", refused.Message);
        }

        [Fact]
        public void R363_the_chips_count_the_rows_by_type_in_the_order_of_the_mock_and_the_plots_that_follow_every_rule()
        {
            JObject chips = CheckRows.Chips(Numbers, MadeRows(), 1, Block());
            Assert.Equal("All 13|Plot cut 0|Floor 1|Hand 2|Unit code 2|Position 0|Level 5|Parameter 2|Skip 1|Assumed 1|followEveryRule 1",
                string.Join("|", chips.Properties().Select(item => item.Name + " " + (int)item.Value)));
            // Review 1 of PR 81: a plot with no row follows every rule only when no override set or moved a value of it, its walls included
            Assert.Equal(0, (int)CheckRows.Chips(Numbers, MadeRows(), 0, Block(), new HashSet<string> { "1" })["followEveryRule"]);
        }

        // test_check_rows.DETAILS, the details of the statuses of the plan as the table statusFrom holds them (R401)
        private const string Ffl = "rules/r.json levelProfiles.p.keyRules.ffl.rule";
        private const string Elevation = "the elevation of the level of the floor in the new site file";
        private const string Split = "rules/r.json levelProfiles.p.keyRules.levelSplit.rule";

        private static string Unmapped(string field)
        {
            return "{\"entries\": [{\"status\": \"UNKNOWN\", \"why\": \"" + string.Format(ValueStatus.NoStatus, field, "computed") + "\"}]}";
        }

        private static JObject Details()
        {
            string ffl = "{\"entry\": \"" + Ffl + "\", \"status\": \"MEASURED\", \"share\": 0.7465, \"strength\": \"weak\"}";
            string generic = "{\"entry\": \"rules/r.json levelProfiles.p.keyRules.generic.rule\", \"status\": \"MEASURED\", \"share\": 0.99, \"strength\": \"strong\"}";
            return JObject.Parse("{"
                + "\"levels.ffl from estimate\": {\"entries\": [" + ffl + "], \"strength\": \"weak\"},"
                + "\"levels.ffl from override by heightM\": {\"entries\": [{\"status\": \"RECOMMENDED\", \"question\": \"Q053\", \"where\": \"a made up place\"}], \"overrides\": [\"heightM\"]},"
                + "\"floors.generic.heightOffset from estimate\": {\"entries\": [" + generic + ", " + ffl + ", {\"status\": \"UNKNOWN\", \"why\": \"" + Elevation + "\"}]},"
                + "\"prototypeFile from computed question Q017 imc\": {\"entries\": [{\"status\": \"DOCUMENT\", \"document\": \"the prototype folder\"}, "
                + "{\"entry\": \"config/project.json majority.decidedByQuestion.Q017.plots.imc\", \"status\": \"RECOMMENDED\", \"question\": \"Q017\"}]},"
                + "\"unit from locationPlan\": {\"entries\": [{\"status\": \"DOCUMENT\", \"document\": \"the location plan\"}]},"
                + "\"floors.generic.outline from computed\": {\"entries\": [" + generic + "], \"strength\": \"strong\"},"
                + "\"walls.top from keyRules\": {\"entries\": [{\"entry\": \"rules/r.json levelProfiles.p.keyRules.wallTop.rule\", \"status\": \"MEASURED\", \"share\": 0.95, "
                + "\"strength\": \"strong\"}, " + ffl + "], \"strength\": \"weak\"},"
                + "\"parameters.CASE from keyRules\": {\"entries\": [{\"entry\": \"rules/r.json levelProfiles.p.keyRules.parameters.CASE.rule\", \"status\": \"CHOSEN by Code\"}]},"
                + "\"mirrored from override\": {\"entries\": [{\"status\": \"RECOMMENDED\", \"question\": \"Q053\", \"where\": \"a made up place\"}]},"
                + "\"prototypeFile from computed question Q017 img\": {\"entries\": [{\"status\": \"DOCUMENT\", \"document\": \"the prototype folder\"}, "
                + "{\"entry\": \"config/project.json majority.decidedByQuestion.Q017.plots.img\", \"status\": \"RECOMMENDED\", \"question\": \"Q017\"}]},"
                // Two weak entries tie for the worst status, so the value counts under both rules (breaker F2 of PR 81)
                + "\"levels.steps from estimate\": {\"entries\": [{\"entry\": \"" + Split + "\", \"status\": \"MEASURED\", \"share\": 0.8, \"strength\": \"weak\"}, " + ffl
                + "], \"strength\": \"weak\"},"
                + "\"floors.generic.levelElevation from computed\": " + Unmapped("floors.generic.levelElevation") + ","
                + "\"floors.ramp.levelElevation from computed\": " + Unmapped("floors.ramp.levelElevation") + "}");
        }

        private static StatusValue Value(string field, string status, string from, string source, params string[] plots)
        {
            return new StatusValue { Field = field, Plots = plots.ToList(), Status = status, StatusFrom = from, Source = source };
        }

        private static readonly StatusValue[] Values =
        {
            Value("levels.ffl", "MEASURED", "levels.ffl from estimate", "estimate", "1"), Value("levels.ffl", "MEASURED", "levels.ffl from estimate", "estimate", "2"),
            Value("levels.ffl", "RECOMMENDED", "levels.ffl from override by heightM", "estimate", "3"),
            Value("floors.generic.heightOffset", "UNKNOWN", "floors.generic.heightOffset from estimate", "estimate", "1"),
            Value("prototypeFile", "RECOMMENDED", "prototypeFile from computed question Q017 imc", "computed", "2"),
            Value("unit", "DOCUMENT", "unit from locationPlan", "locationPlan", "1"), Value("floors.generic.outline", "MEASURED", "floors.generic.outline from computed", "computed", "1"),
            Value("walls.top", "MEASURED", "walls.top from keyRules", "keyRules", "2", "1"),
            Value("parameters.CASE", "CHOSEN by Code", "parameters.CASE from keyRules", "keyRules", "10"),
            Value("parameters.CASE", "CHOSEN by Code", "parameters.CASE from keyRules", "keyRules", "9"),
            Value("mirrored", "RECOMMENDED", "mirrored from override", "override", "4"),
            Value("prototypeFile", "RECOMMENDED", "prototypeFile from computed question Q017 img", "computed", "6"),
            Value("levels.steps", "MEASURED", "levels.steps from estimate", "estimate", "5"),
            Value("floors.generic.levelElevation", "UNKNOWN", "floors.generic.levelElevation from computed", "computed", "1"),
            Value("floors.ramp.levelElevation", "UNKNOWN", "floors.ramp.levelElevation from computed", "computed", "1"),
        };

        private static JObject AssumedRow(string rule, string status, string[] plots, int values, params string[] fields)
        {
            return new JObject { ["rule"] = rule, ["status"] = status, ["plotCount"] = plots.Length, ["plots"] = new JArray(plots), ["values"] = values, ["fields"] = new JArray(fields) };
        }

        private static readonly string[] Assumed = { "RECOMMENDED", "CHOSEN by Code", "UNKNOWN", "MEASURED weak" };
        private static readonly string[] Worst = { "UNKNOWN", "CHOSEN by Code", "RECOMMENDED", "MEASURED weak" };

        [Fact]
        public void R363_Q108_the_assumed_chip_groups_by_rule_one_row_per_rule_and_status_with_its_plots()
        {
            JObject found = CheckRows.AssumedRows(Values, Details(), Assumed, Worst);
            // Bader's Q108 word for word: DOCUMENT and strong MEASURED values are not shown, nothing else is left out, so a value an override sets or
            // moved counts under its override rule, Q053, and a value of a tie under each rule of the tie (review 1 of PR 81)
            var expected = new JObject
            {
                ["Q017 | RECOMMENDED"] = AssumedRow("Q017", "RECOMMENDED", new[] { "2", "6" }, 2, "prototypeFile"),
                ["Q053 | RECOMMENDED"] = AssumedRow("Q053", "RECOMMENDED", new[] { "3", "4" }, 2, "levels.ffl", "mirrored"),
                [Ffl + " | MEASURED weak"] = AssumedRow(Ffl, "MEASURED weak", new[] { "1", "2", "5" }, 4, "levels.ffl", "levels.steps", "walls.top"),
                [Split + " | MEASURED weak"] = AssumedRow(Split, "MEASURED weak", new[] { "5" }, 1, "levels.steps"),
                ["rules/r.json levelProfiles.p.keyRules.parameters.CASE.rule | CHOSEN by Code"] = AssumedRow("rules/r.json levelProfiles.p.keyRules.parameters.CASE.rule",
                    "CHOSEN by Code", new[] { "9", "10" }, 2, "parameters.CASE"),
                [Elevation + " | UNKNOWN"] = AssumedRow(Elevation, "UNKNOWN", new[] { "1" }, 1, "floors.generic.heightOffset"),
                [CheckRows.NoStatusRule + " | UNKNOWN"] = AssumedRow(CheckRows.NoStatusRule, "UNKNOWN", new[] { "1" }, 2, "floors.generic.levelElevation", "floors.ramp.levelElevation"),
            };
            Assert.Equal(expected.ToString(Formatting.None), found.ToString(Formatting.None));
            // Only the DOCUMENT and the strong MEASURED value are left out, the tie counts twice
            Assert.Equal(Values.Length - 2 + 1, found.Properties().Sum(item => (int)item.Value["values"]));
        }

        [Fact]
        public void R363_the_plots_an_override_set_or_moved_a_value_of_follow_no_rule_whole()
        {
            // Plot 3 holds a value an override moved and 4 one of the source override, a wall on 2 and 7 with a moved base puts both in
            List<StatusValue> values = Values.Concat(new[] { Value("walls.base", "RECOMMENDED", "levels.ffl from override by heightM", "keyRules", "2", "7") }).ToList();
            Assert.Equal("2 3 4 7", string.Join(" ", CheckRows.MovedPlots(values, Details()).OrderBy(plot => plot, StringComparer.Ordinal)));
        }

        [Fact]
        public void R363_Q108_the_values_of_a_plan_each_value_of_a_written_plot_and_of_a_wall_on_its_written_plots()
        {
            Func<string, string, string, JObject> sourced = (source, status, key) => new JObject { ["source"] = source, ["status"] = status, ["statusFrom"] = key };
            var status = new Dictionary<string, Dictionary<string, object>>
            {
                ["1"] = new Dictionary<string, object> { ["unit"] = "DP2", ["written"] = true, ["fields"] = new JObject { ["unit"] = sourced("locationPlan", "DOCUMENT", "u"),
                    ["levels.ffl"] = sourced("estimate", "MEASURED", "f") } },
                ["3"] = new Dictionary<string, object> { ["unit"] = "DP2", ["written"] = false, ["fields"] = null },
                ["9"] = new Dictionary<string, object> { ["unit"] = "DP2", ["written"] = true, ["fields"] = new JObject { ["unit"] = sourced("locationPlan", "DOCUMENT", "u") } },
            };
            Func<string, List<string>, PlanWall> wall = (plot, plots) =>
            {
                var made = new PlanWall { Plot = plot, Plots = plots };
                made.Status["plots"] = new[] { "locationPlan", "DOCUMENT", "p" };
                made.Status["ends"] = new[] { "keyRules", "UNKNOWN", "e" };
                made.Status["base"] = new[] { "keyRules", "MEASURED", "b" };
                made.Status["top"] = new[] { "keyRules", "MEASURED", "t" };
                return made;
            };
            // Plot 9 is out of the scope and 3 is in it with no value, so the first wall stands on plot 1 alone and the second on no written plot
            List<StatusValue> found = CheckRows.PlanValues(new[] { "1", "3" }, status, new[] { wall("1", new List<string> { "1", "3" }), wall("3", new List<string> { "3" }) });
            Assert.Equal("unit 1 DOCUMENT u locationPlan|levels.ffl 1 MEASURED f estimate|walls.plots 1 DOCUMENT p locationPlan|walls.ends 1 UNKNOWN e keyRules|"
                + "walls.base 1 MEASURED b keyRules|walls.top 1 MEASURED t keyRules",
                string.Join("|", found.Select(item => item.Field + " " + string.Join(" ", item.Plots) + " " + item.Status + " " + item.StatusFrom + " " + item.Source)));
        }

        [Fact]
        public void R363_Q108_the_rules_of_a_value_are_its_worst_tied_entries_named_by_rule()
        {
            JObject details = Details();
            Func<JObject, string> rules = detail => string.Join("|", CheckRows.RulesOf(detail, Worst));
            Assert.Equal(Elevation, rules((JObject)details["floors.generic.heightOffset from estimate"]));
            Assert.Equal(Ffl, rules((JObject)details["walls.top from keyRules"]));
            Assert.Equal(Split + "|" + Ffl, rules((JObject)details["levels.steps from estimate"]));
            Assert.Equal("a document", rules(JObject.Parse("{\"entries\": [{\"status\": \"DOCUMENT\", \"document\": \"a document\"}, {\"status\": \"DOCUMENT\", \"document\": \"another\"}]}")));
            Assert.Equal("Q053", rules(JObject.Parse("{\"entries\": [{\"status\": \"RECOMMENDED\", \"question\": \"Q053\"}]}")));
            // A decidedByQuestion entry by its question, and every field valueStatus maps no status for by one cause (breaker F5 of PR 81)
            Assert.Equal("Q017", rules((JObject)details["prototypeFile from computed question Q017 img"]));
            Assert.Equal(CheckRows.NoStatusRule, rules((JObject)details["floors.ramp.levelElevation from computed"]));
        }

        [Fact]
        public void R401_R363_the_worst_tied_entries_of_a_value_the_status_takes_the_first()
        {
            JObject details = Details();
            List<JObject> tie = ((JArray)details["levels.steps from estimate"]["entries"]).OfType<JObject>().ToList();
            Assert.Equal(tie, ValueStatus.WorstEntries(tie, Worst));
            List<JObject> mixed = ((JArray)details["floors.generic.heightOffset from estimate"]["entries"]).OfType<JObject>().ToList();
            Assert.Equal(new[] { mixed[2] }, ValueStatus.WorstEntries(mixed, Worst));
            // No entry of a status worstFirst names gives the first entry alone, as Of takes it
            List<JObject> none = new[] { JObject.Parse("{\"status\": \"DOCUMENT\", \"document\": \"a\"}"), JObject.Parse("{\"status\": \"DOCUMENT\", \"document\": \"b\"}") }.ToList();
            Assert.Equal(new[] { none[0] }, ValueStatus.WorstEntries(none, Worst));
            Assert.Equal("MEASURED weak", ValueStatus.Label("MEASURED", JObject.Parse("{\"strength\": \"weak\"}")));
            Assert.Equal("UNKNOWN", ValueStatus.Label("UNKNOWN", JObject.Parse("{\"strength\": \"weak\"}")));
            // Of keeps the first of the tie, the status and strength it gave before
            var map = (JObject)JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()))["valueStatus"];
            map["fields"] = JObject.Parse("{\"levels.steps\": {\"estimate\": [{\"profile\": \"a.rule\"}, {\"profile\": \"b.rule\"}]}}");
            var block = JObject.Parse("{\"rules/r.json\": {\"a.rule\": {\"status\": \"MEASURED\", \"share\": 0.8, \"strength\": \"weak\"}, "
                + "\"b.rule\": {\"status\": \"MEASURED\", \"share\": 0.7, \"strength\": \"weak\"}}}");
            Tuple<string, JObject, string> status = new ValueStatusTable(map, block, "rules/r.json", "p").Of("levels.steps", "estimate");
            Assert.Equal("MEASURED weak 0.8 0.7", status.Item1 + " " + (string)status.Item2["strength"] + " "
                + string.Join(" ", ValueStatus.WorstEntries(((JArray)status.Item2["entries"]).OfType<JObject>().ToList(), Worst).Select(entry => ((double)entry["share"]).ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }

        // test_check_rows.ROW_OVERRIDES on the made up row of OverrideParameterStepTests.Build, plot 6 with no road level at a front corner and 8 a
        // DP2 with no face
        [Fact]
        public void R363_parity_check_rows_of_a_made_up_build_the_lacks_of_written_and_unwritten_plots()
        {
            var list = new OverrideList(new Dictionary<string, JObject>
            {
                ["2"] = JObject.Parse("{\"heightM\": {\"gapToRuleM\": 0.1}, \"rampOutline\": {\"gapToRuleM\": 0.1}}"),
                ["6"] = JObject.Parse("{\"entryM\": {\"gapToRuleM\": 0.1}, \"mirrored\": false}"),
                ["8"] = JObject.Parse("{\"mirrored\": true}"),
            });
            OverrideParameterStepTests.Built built = OverrideParameterStepTests.Build(list, null, new[] { "8" }, new[] { "6" });
            string[] numbers = { "1", "2", "3", "4", "5", "6", "8" };
            var rows = numbers.ToDictionary(number => number, number => new Dictionary<string, string> { ["typologyRev9"] = number == "1" ? "VD2" : "DP2" });
            Dictionary<string, CheckPlot> plots = CheckRows.PlotsOf(numbers, built.Plans, rows);
            string noFace = "transform levels.ffl parameters.CASE parameters.RRE_ZZ_SP_Number-of-Steps parameters.RRE_ZZ_SP_Prototype-FFL parameters.RRE_ZZ_SP_Prototype-Foundation-Type";
            Assert.Equal("1 floors.yard|2 |3 floors.yard|4 floors.yard|5 |6 levels.ffl parameters.RRE_ZZ_SP_Number-of-Steps parameters.RRE_ZZ_SP_Prototype-FFL|8 " + noFace,
                string.Join("|", numbers.Select(number => number + " " + string.Join(" ", plots[number].Lacks))));
            Dictionary<string, List<JObject>> found = CheckRows.Rows(numbers, plots, new List<JObject>(), new List<JObject>(), null, Block());
            Func<string, string> text = number => string.Join(", ", found[number].Select(item => (string)item["check"] + " " + (string)item["field"] + " " + (string)item["from"] + " " + (string)item["use"]));
            Assert.Equal("Floor floors.yard missing Rule", text("1"));
            Assert.Equal("Floor rampOutline override Rule, Level heightM override Past", text("2"));
            Assert.Equal("Floor floors.yard missing Rule", text("3"));
            Assert.Equal("Floor floors.yard missing Rule", text("4"));
            Assert.Equal("", text("5"));
            Assert.Equal("Hand mirrored override Rule, Level entryM override Rule, Level levels.ffl missing Rule, Parameter parameters.RRE_ZZ_SP_Number-of-Steps missing Rule, "
                + "Parameter parameters.RRE_ZZ_SP_Prototype-FFL missing Rule", text("6"));
            Assert.Equal("Hand mirrored override Rule, Position transform missing Rule, Level levels.ffl missing Rule, Parameter parameters.CASE missing Rule, "
                + "Parameter parameters.RRE_ZZ_SP_Number-of-Steps missing Rule, Parameter parameters.RRE_ZZ_SP_Prototype-FFL missing Rule, "
                + "Parameter parameters.RRE_ZZ_SP_Prototype-Foundation-Type missing Rule", text("8"));
        }

        [Fact]
        public void R363_the_worker_build_writes_the_step_check_rows_with_the_tables_assumed_and_chips()
        {
            using (var maker = new LevelFloorStepTests())
            {
                WorkerRequest request = maker.Request("c1", CheckRows.StepName);
                string list = Path.Combine(request.OutputFolder, "overrides.json");
                File.WriteAllText(list, new JObject { ["overrides"] = new JArray(new JObject { ["plot"] = "3", ["field"] = "heightM", ["value"] = JObject.Parse("{\"gapToRuleM\": 0.1}"),
                    ["rule"] = null, ["decision"] = "parsons ffl", ["source"] = "parsons", ["model"] = "a.rvt", ["key"] = "imc" }) }.ToString());
                request.Inputs.Add(new WorkerFile { Role = "overrides", Path = list, Sha256 = FileHash.Sha256(list) });
                int code = WorkerRunner.Run(request.Write(request.OutputFolder), WorkerCommands.Default(), () => 9000.0);
                Assert.True(code == 0, code + " " + WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, "c1")).Message);
                JObject step = RunStamp.Read(Path.Combine(request.OutputFolder, "steps", "step_checkRows_csharp.json"));
                JToken three = step["plots"]["3"]["written"];
                // Plot 3 lacks a yard outline as in the made up build of test_level_floor_steps.py
                Assert.Equal("Floor floors.yard missing Rule , Level heightM override Past parsons ffl", string.Join(", ", three["rows"].Select(item => (string)item["check"] + " " + (string)item["field"] + " "
                    + (string)item["from"] + " " + (string)item["use"] + " " + (string)item["rule"])));
                Assert.Equal(0.1, (double)three["rows"][1]["past"]["value"]["gapToRuleM"]);
                Assert.Equal(6, ((JObject)step["plots"]).Count);
                Assert.Equal((int)step["tables"]["chips"]["All"], ((JObject)step["plots"]).Properties().Sum(plot => ((JArray)plot.Value["written"]["rows"]).Count));
                Assert.True(((JObject)step["tables"]["assumed"]).Count > 0);
                Assert.True(((JObject)step["tables"]["assumed"]).Properties().All(item => (string)item.Value["status"] != "DOCUMENT" && (string)item.Value["status"] != "MEASURED strong"));
            }
        }

        [Fact]
        public void R363_the_config_names_the_fields_of_the_step_check_rows_and_the_build_steps_hold_it()
        {
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("unit:noGeometry nbh:noGeometry rows:noGeometry", string.Join(" ", config.StepFields(CheckRows.StepName).Select(field => field.Name + ":" + field.Kind + field.Decimals)));
            Assert.Equal(CheckRows.StepName, BuildSteps.Names.Last());
            Assert.Equal(BuildSteps.OverrideRoles, WorkerRunner.Steps[CheckRows.StepName]);
        }
    }
}
