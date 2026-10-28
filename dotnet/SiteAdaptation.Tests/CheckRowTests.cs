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
        }

        // test_check_rows.DETAILS, the details of the statuses of the plan as the table statusFrom holds them (R401)
        private const string Ffl = "rules/r.json levelProfiles.p.keyRules.ffl.rule";
        private const string Elevation = "the elevation of the level of the floor in the new site file";

        private static JObject Details()
        {
            string ffl = "{\"entry\": \"" + Ffl + "\", \"status\": \"MEASURED\", \"share\": 0.7465, \"strength\": \"weak\"}";
            string generic = "{\"entry\": \"rules/r.json levelProfiles.p.keyRules.generic.rule\", \"status\": \"MEASURED\", \"share\": 0.99, \"strength\": \"strong\"}";
            return JObject.Parse("{"
                + "\"levels.ffl from estimate\": {\"entries\": [" + ffl + "], \"strength\": \"weak\"},"
                + "\"levels.ffl from override by heightM\": {\"entries\": [{\"status\": \"RECOMMENDED\", \"question\": \"Q053\", \"where\": \"a made up place\"}], \"override\": \"heightM\"},"
                + "\"floors.generic.heightOffset from estimate\": {\"entries\": [" + generic + ", " + ffl + ", {\"status\": \"UNKNOWN\", \"why\": \"" + Elevation + "\"}]},"
                + "\"prototypeFile from computed question Q017 imc\": {\"entries\": [{\"status\": \"DOCUMENT\", \"document\": \"the prototype folder\"}, "
                + "{\"entry\": \"config/project.json majority.decidedByQuestion.Q017.plots.imc\", \"status\": \"RECOMMENDED\", \"question\": \"Q017\"}]},"
                + "\"unit from locationPlan\": {\"entries\": [{\"status\": \"DOCUMENT\", \"document\": \"the location plan\"}]},"
                + "\"floors.generic.outline from computed\": {\"entries\": [" + generic + "], \"strength\": \"strong\"},"
                + "\"walls.top from keyRules\": {\"entries\": [{\"entry\": \"rules/r.json levelProfiles.p.keyRules.wallTop.rule\", \"status\": \"MEASURED\", \"share\": 0.95, "
                + "\"strength\": \"strong\"}, " + ffl + "], \"strength\": \"weak\"},"
                + "\"parameters.CASE from keyRules\": {\"entries\": [{\"entry\": \"rules/r.json levelProfiles.p.keyRules.parameters.CASE.rule\", \"status\": \"CHOSEN by Code\"}]},"
                + "\"mirrored from override\": {\"entries\": [{\"status\": \"RECOMMENDED\", \"question\": \"Q053\", \"where\": \"a made up place\"}]}}");
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
            // A value of the source override, whose detail names no override as no override moved it, has its own row under its check type
            Value("mirrored", "RECOMMENDED", "mirrored from override", "override", "4"),
        };

        private static readonly string[] Assumed = { "RECOMMENDED", "CHOSEN by Code", "UNKNOWN", "MEASURED weak" };
        private static readonly string[] Worst = { "UNKNOWN", "CHOSEN by Code", "RECOMMENDED", "MEASURED weak" };

        [Fact]
        public void R363_Q108_the_assumed_chip_groups_by_rule_one_row_per_rule_and_status_with_its_plots()
        {
            JObject found = CheckRows.AssumedRows(Values, Details(), Assumed, Worst);
            JObject expected = new JObject
            {
                ["config/project.json majority.decidedByQuestion.Q017.plots.imc | RECOMMENDED"] = JObject.Parse("{\"rule\": \"config/project.json majority.decidedByQuestion.Q017.plots.imc\", "
                    + "\"status\": \"RECOMMENDED\", \"plotCount\": 1, \"plots\": [\"2\"], \"values\": 1, \"fields\": [\"prototypeFile\"]}"),
                ["rules/r.json levelProfiles.p.keyRules.ffl.rule | MEASURED weak"] = JObject.Parse("{\"rule\": \"" + Ffl + "\", \"status\": \"MEASURED weak\", \"plotCount\": 2, "
                    + "\"plots\": [\"1\", \"2\"], \"values\": 3, \"fields\": [\"levels.ffl\", \"walls.top\"]}"),
                ["rules/r.json levelProfiles.p.keyRules.parameters.CASE.rule | CHOSEN by Code"] = JObject.Parse("{\"rule\": \"rules/r.json levelProfiles.p.keyRules.parameters.CASE.rule\", "
                    + "\"status\": \"CHOSEN by Code\", \"plotCount\": 2, \"plots\": [\"9\", \"10\"], \"values\": 2, \"fields\": [\"parameters.CASE\"]}"),
                [Elevation + " | UNKNOWN"] = JObject.Parse("{\"rule\": \"" + Elevation + "\", \"status\": \"UNKNOWN\", \"plotCount\": 1, \"plots\": [\"1\"], \"values\": 1, "
                    + "\"fields\": [\"floors.generic.heightOffset\"]}"),
            };
            Assert.Equal(expected.ToString(Formatting.None), found.ToString(Formatting.None));
            // DOCUMENT, strong MEASURED and a value an override sets are not shown, the last has its own row under its check type
            Assert.Equal(Values.Length - 4, found.Properties().Sum(item => (int)item.Value["values"]));
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
        public void R363_Q108_the_rule_of_a_value_is_its_worst_entry_by_worst_first_else_its_first()
        {
            JObject details = Details();
            Assert.Equal(Elevation, CheckRows.RuleOf((JObject)details["floors.generic.heightOffset from estimate"], Worst));
            Assert.Equal(Ffl, CheckRows.RuleOf((JObject)details["walls.top from keyRules"], Worst));
            Assert.Equal("a document", CheckRows.RuleOf(JObject.Parse("{\"entries\": [{\"status\": \"DOCUMENT\", \"document\": \"a document\"}, {\"status\": \"DOCUMENT\", \"document\": \"another\"}]}"), Worst));
            Assert.Equal("Q053", CheckRows.RuleOf(JObject.Parse("{\"entries\": [{\"status\": \"RECOMMENDED\", \"question\": \"Q053\"}]}"), Worst));
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
