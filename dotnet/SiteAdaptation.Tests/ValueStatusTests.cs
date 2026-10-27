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
            ""assumed"": [""RECOMMENDED"", ""CHOSEN by Code"", ""UNKNOWN"", ""MEASURED weak""]}");
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
                [Key + "rampSlope.rule"] = new JObject { ["status"] = "CHOSEN by Code", ["where"] = "a made up note" },
            };
            var config = new JObject { ["floors.types.generic"] = new JObject { ["status"] = "UNKNOWN", ["why"] = "made up", ["nearestSource"] = "floors.note" } };
            return new JObject { ["note"] = "a made up block", [profileFile] = profile, ["config/project.json"] = config };
        }

        // test_value_status.MADE_COUNTS
        private const string MadeCounts = "floors.generic.heightOffset:RECOMMENDED=1 floors.generic.heightOffset:UNKNOWN=5 floors.generic.level:UNKNOWN=6 "
            + "floors.generic.levelElevation:UNKNOWN=6 floors.generic.outline:UNKNOWN=6 floors.generic.plotValue:DOCUMENT=6 floors.generic.type:UNKNOWN=6 "
            + "floors.generic.z:RECOMMENDED=1 floors.generic.z:UNKNOWN=5 floors.ramp.corners:RECOMMENDED=2 floors.ramp.corners:UNKNOWN=4 "
            + "floors.ramp.heightOffset:RECOMMENDED=2 floors.ramp.heightOffset:UNKNOWN=4 floors.ramp.level:UNKNOWN=6 floors.ramp.levelElevation:UNKNOWN=6 "
            + "floors.ramp.outline:UNKNOWN=6 floors.ramp.plotValue:DOCUMENT=6 floors.ramp.slopePercent:CHOSEN by Code=6 floors.ramp.type:CHOSEN by Code=5 "
            + "floors.ramp.type:RECOMMENDED=1 floors.yard.heightOffset:RECOMMENDED=1 floors.yard.heightOffset:UNKNOWN=2 floors.yard.level:RECOMMENDED=1 "
            + "floors.yard.level:UNKNOWN=2 floors.yard.levelElevation:RECOMMENDED=1 floors.yard.levelElevation:UNKNOWN=2 floors.yard.outline:RECOMMENDED=3 "
            + "floors.yard.plotValue:DOCUMENT=2 floors.yard.plotValue:RECOMMENDED=1 floors.yard.type:RECOMMENDED=1 floors.yard.type:UNKNOWN=2 "
            + "floors.yard.z:RECOMMENDED=1 floors.yard.z:UNKNOWN=2 insert:MEASURED strong=3 insert:RECOMMENDED=3 levels.entry:MEASURED weak=5 "
            + "levels.entry:RECOMMENDED=1 levels.ffl:RECOMMENDED=1 levels.ffl:UNKNOWN=5 levels.height:MEASURED weak=4 levels.height:RECOMMENDED=2 "
            + "levels.ssl:RECOMMENDED=1 levels.ssl:UNKNOWN=5 levels.steps:MEASURED weak=3 levels.steps:RECOMMENDED=3 linkName:MEASURED weak=2 "
            + "linkName:RECOMMENDED=4 mirrored:MEASURED strong=5 mirrored:RECOMMENDED=1 parameters.CASE:MEASURED weak=4 parameters.CASE:RECOMMENDED=2 "
            + "parameters.RRE_ZZ_SP_Façade-Color:UNKNOWN=6 parameters.RRE_ZZ_SP_Façade-Design-Option:UNKNOWN=6 "
            + "parameters.RRE_ZZ_SP_Number-of-Steps:RECOMMENDED=2 parameters.RRE_ZZ_SP_Number-of-Steps:UNKNOWN=3 parameters.RRE_ZZ_SP_Plot-Number:UNKNOWN=6 "
            + "parameters.RRE_ZZ_SP_Prototype:UNKNOWN=6 parameters.RRE_ZZ_SP_Prototype-FFL:RECOMMENDED=1 parameters.RRE_ZZ_SP_Prototype-FFL:UNKNOWN=5 "
            + "parameters.RRE_ZZ_SP_Prototype-Foundation-Type:RECOMMENDED=1 parameters.RRE_ZZ_SP_Prototype-Foundation-Type:UNKNOWN=5 "
            + "parameters.RRE_ZZ_SP_Prototype-HVAC-Schedule-Type:UNKNOWN=6 parameters.RRE_ZZ_SP_RVT-Name:MEASURED weak=2 "
            + "parameters.RRE_ZZ_SP_RVT-Name:RECOMMENDED=4 prototypeFile:UNKNOWN=6 rotationDeg:MEASURED strong=3 rotationDeg:RECOMMENDED=3 unit:DOCUMENT=6";

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
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            string profileFile = (string)config["profile"];
            built = OverrideParameterStepTests.Build(list, changeRules: rules => rules["status"] = MadeBlock(profileFile));
            ValueStatusTable table = ValueStatus.Table(built.Config, built.Rules, "parsons2024Rev6");
            return ValueStatus.Step(built.Plans.Keys.ToList(), built.Plans, built.Hands, table);
        }

        private static JToken Field(Dictionary<string, Dictionary<string, object>> step, string number, string field)
        {
            return ((JObject)step[number]["fields"])[field];
        }

        [Fact]
        public void R401_parity_status_each_field_of_a_plot_with_its_source_status_and_detail()
        {
            Dictionary<string, Dictionary<string, object>> step = MadeStep(OverrideParameterStepTests.Made(), out OverrideParameterStepTests.Built built);
            string profileFile = (string)built.Config["profile"];
            Assert.Equal("{\"source\":\"estimate\",\"status\":\"MEASURED\",\"statusFrom\":{\"entries\":[{\"entry\":\"" + profileFile + " " + Key
                + "entry.rule\",\"status\":\"MEASURED\",\"share\":0.5,\"strength\":\"weak\",\"measuredBy\":\"made\"}],\"strength\":\"weak\"}}", Field(step, "1", "levels.entry").ToString(Formatting.None));
            Assert.Equal("{\"source\":\"estimate\",\"status\":\"RECOMMENDED\",\"statusFrom\":{\"entries\":[{\"status\":\"RECOMMENDED\",\"question\":\"Q053\",\"where\":\"steps/investigations/rule-status-audit.md section 6\"}],"
                + "\"override\":\"entryM\"}}", Field(step, "2", "levels.entry").ToString(Formatting.None));
            Assert.Equal(profileFile + " " + Key + "hand cluster.rule", (string)Field(step, "3", "mirrored")["statusFrom"]["entries"][0]["entry"]);
            Assert.Equal("RECOMMENDED - RECOMMENDED mirrored RECOMMENDED mirrored", string.Join(" ", new[] { "mirrored", "insert", "rotationDeg" }.Select(field =>
                (string)Field(step, "4", field)["status"] + " " + ((string)Field(step, "4", field)["statusFrom"]["override"] ?? "-"))));
            Assert.Equal("RECOMMENDED override heightM override", (string)Field(step, "4", "parameters.CASE")["status"] + " " + (string)Field(step, "2", "parameters.CASE")["source"] + " "
                + (string)Field(step, "3", "insert")["statusFrom"]["override"] + " " + (string)Field(step, "3", "parameters.RRE_ZZ_SP_Prototype-FFL")["source"]);
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
            Dictionary<string, Dictionary<string, object>> step = MadeStep(OverrideParameterStepTests.Made(), out OverrideParameterStepTests.Built built);
            MadeStep(new OverrideList(new Dictionary<string, JObject>()), out OverrideParameterStepTests.Built plain);
            var differ = new List<string>();
            var carry = new List<string>();
            foreach (string number in new[] { "1", "2", "3", "4", "5", "6" })
            {
                Dictionary<string, string> mine = Values(built.Plans[number]), other = Values(plain.Plans[number]);
                foreach (KeyValuePair<string, string> value in mine.Where(item => ((JObject)step[number]["fields"])[item.Key] != null))
                {
                    if (!other.TryGetValue(value.Key, out string before) || before != value.Value)
                    {
                        differ.Add(number + " " + value.Key);
                        if ((string)Field(step, number, value.Key)["statusFrom"]["entries"][0]["question"] == "Q053")
                        {
                            carry.Add(number + " " + value.Key);
                        }
                    }
                }
            }
            Assert.Equal(string.Join(", ", differ), string.Join(", ", carry));
            Assert.True(differ.Count > 15, differ.Count.ToString());
        }

        [Fact]
        public void R401_the_counts_of_the_build_name_the_plots_by_field_and_status()
        {
            Dictionary<string, Dictionary<string, object>> step = MadeStep(OverrideParameterStepTests.Made(), out _);
            Assert.Equal(MadeCounts, string.Join(" ", ValueStatus.Counts(step).Select(item => item.Key.Item1 + ":" + item.Key.Item2 + "=" + item.Value)));
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
                // The worker counts the plots by field and status as the summary of step 15 of Python does
                WorkerAnswer answer = WorkerAnswer.Read(WorkerFiles.AnswerPath(request.OutputFolder, "p1"));
                Assert.Equal(6, answer.Counts["status unit DOCUMENT"]);
            }
            AddinConfig config = AddinConfig.Load(WorkerRunTests.RepoConfig());
            Assert.Equal("unit:noGeometry written:noGeometry fields:noGeometry", string.Join(" ", config.StepFields(ValueStatus.StepName).Select(field => field.Name + ":" + field.Kind + field.Decimals)));
            Assert.Equal("status", BuildSteps.Names.Last());
        }
    }
}
