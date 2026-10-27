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

        // test_value_status.MAP and BLOCK
        private static readonly JObject Map = JObject.Parse(@"{
            ""sources"": {""override"": {""status"": ""RECOMMENDED"", ""question"": ""Q900"", ""where"": ""a made up list""},
                          ""locationPlan"": {""status"": ""DOCUMENT"", ""document"": ""a made up plan""}},
            ""fields"": {""levels.entry"": {""estimate"": {""profile"": ""levelProfiles.{levelProfile}.keyRules.entry.rule""}},
                         ""levels.ffl"": {""estimate"": {""profile"": ""levelProfiles.{levelProfile}.keyRules.ffl.rule""}},
                         ""mirrored"": {""computed"": {""profile"": ""levelProfiles.{levelProfile}.keyRules.hand {hand}.rule""}},
                         ""parameters.*"": {""keyRules"": {""profile"": ""levelProfiles.{levelProfile}.keyRules.parameter {parameter}.rule""}},
                         ""parameters.CASE"": {""keyRules"": {""config"": ""made.caseRule""}},
                         ""floors.generic.type"": {""computed"": {""config"": ""floors.types.generic""}},
                         ""floors.generic.level"": {""computed"": {""profile"": ""levelProfiles.{levelProfile}.keyRules.notInTheBlock""}}},
            ""overrideSets"": {""entryM"": ""levels.entry""},
            ""assumed"": [""RECOMMENDED"", ""CHOSEN by Code"", ""UNKNOWN"", ""MEASURED weak""]}");

        private static readonly JObject Block = JObject.Parse(@"{""note"": ""a made up block"",
            ""rules/made.json"": {
                ""levelProfiles.p1.keyRules.entry.rule"": {""status"": ""MEASURED"", ""share"": 0.5, ""strength"": ""weak"", ""measuredBy"": ""levelProfiles.p1.keyRules.entry""},
                ""levelProfiles.p1.keyRules.ffl.rule"": {""status"": ""MEASURED"", ""share"": 0.95, ""strength"": ""strong"", ""measuredBy"": ""levelProfiles.p1.keyRules.ffl""},
                ""levelProfiles.p1.keyRules.hand alone.rule"": {""status"": ""MEASURED"", ""share"": 1.0, ""strength"": ""strong"", ""measuredBy"": ""levelProfiles.p1.keyRules.hand alone""},
                ""levelProfiles.p1.keyRules.parameter X.rule"": {""status"": ""CHOSEN by Bader"", ""question"": ""Q901"", ""where"": ""made up""}},
            ""config/project.json"": {
                ""floors.types.generic"": {""status"": ""UNKNOWN"", ""why"": ""no Q"", ""nearestSource"": ""floors.note""},
                ""made.caseRule"": {""status"": ""CHOSEN by Code"", ""where"": ""note""}}}");

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
                [Key + "parameter CASE.rule"] = weak.DeepClone(),
                [Key + "parameter RRE_ZZ_SP_RVT-Name.rule"] = new JObject { ["status"] = "CHOSEN by Bader", ["question"] = "Q902", ["where"] = "made up" },
                [Key + "rampSlope.rule"] = new JObject { ["status"] = "CHOSEN by Code", ["where"] = "a made up note" },
            };
            var config = new JObject { ["floors.types.generic"] = new JObject { ["status"] = "UNKNOWN", ["why"] = "made up", ["nearestSource"] = "floors.note" } };
            return new JObject { ["note"] = "a made up block", [profileFile] = profile, ["config/project.json"] = config };
        }

        // test_value_status.MADE_COUNTS
        private const string MadeCounts = "floors.generic.heightOffset:MEASURED strong=6 floors.generic.level:UNKNOWN=6 floors.generic.levelElevation:UNKNOWN=6 "
            + "floors.generic.outline:UNKNOWN=6 floors.generic.plotValue:DOCUMENT=6 floors.generic.type:UNKNOWN=6 floors.generic.z:MEASURED strong=6 "
            + "floors.ramp.corners:RECOMMENDED=1 floors.ramp.corners:UNKNOWN=5 floors.ramp.heightOffset:UNKNOWN=6 floors.ramp.level:UNKNOWN=6 "
            + "floors.ramp.levelElevation:UNKNOWN=6 floors.ramp.outline:UNKNOWN=6 floors.ramp.plotValue:DOCUMENT=6 floors.ramp.slopePercent:CHOSEN by Code=6 "
            + "floors.ramp.type:CHOSEN by Code=5 floors.ramp.type:RECOMMENDED=1 floors.yard.heightOffset:UNKNOWN=3 floors.yard.level:UNKNOWN=3 "
            + "floors.yard.levelElevation:UNKNOWN=3 floors.yard.outline:UNKNOWN=3 floors.yard.plotValue:DOCUMENT=3 floors.yard.type:UNKNOWN=3 "
            + "floors.yard.z:UNKNOWN=3 insert:MEASURED strong=5 insert:RECOMMENDED=1 levels.entry:MEASURED weak=5 levels.entry:RECOMMENDED=1 "
            + "levels.ffl:MEASURED strong=5 levels.ffl:RECOMMENDED=1 levels.height:MEASURED strong=6 levels.ssl:UNKNOWN=6 levels.steps:MEASURED strong=5 "
            + "levels.steps:RECOMMENDED=1 linkName:CHOSEN by Bader=5 linkName:RECOMMENDED=1 mirrored:MEASURED strong=5 mirrored:RECOMMENDED=1 "
            + "parameters.CASE:MEASURED weak=5 parameters.CASE:RECOMMENDED=1 parameters.RRE_ZZ_SP_Façade-Color:UNKNOWN=6 "
            + "parameters.RRE_ZZ_SP_Façade-Design-Option:UNKNOWN=6 parameters.RRE_ZZ_SP_Number-of-Steps:UNKNOWN=5 parameters.RRE_ZZ_SP_Plot-Number:UNKNOWN=6 "
            + "parameters.RRE_ZZ_SP_Prototype:UNKNOWN=6 parameters.RRE_ZZ_SP_Prototype-FFL:RECOMMENDED=1 parameters.RRE_ZZ_SP_Prototype-FFL:UNKNOWN=5 "
            + "parameters.RRE_ZZ_SP_Prototype-Foundation-Type:UNKNOWN=6 parameters.RRE_ZZ_SP_Prototype-HVAC-Schedule-Type:UNKNOWN=6 "
            + "parameters.RRE_ZZ_SP_RVT-Name:CHOSEN by Bader=5 parameters.RRE_ZZ_SP_RVT-Name:RECOMMENDED=1 prototypeFile:UNKNOWN=6 "
            + "rotationDeg:RECOMMENDED=2 rotationDeg:UNKNOWN=4 unit:DOCUMENT=6";

        private static ValueStatusTable Table(string levelProfile = "p1")
        {
            return new ValueStatusTable(Map, Block, ProfileFile, levelProfile);
        }

        private static string Text(Tuple<string, JObject> found)
        {
            return found.Item1 + " " + found.Item2.ToString(Formatting.None);
        }

        [Fact]
        public void R401_a_value_takes_the_status_of_the_block_entry_its_field_and_source_name_with_the_entry_and_its_evidence()
        {
            ValueStatusTable table = Table();
            Assert.Equal("MEASURED {\"entry\":\"rules/made.json levelProfiles.p1.keyRules.entry.rule\",\"share\":0.5,\"strength\":\"weak\",\"measuredBy\":\"levelProfiles.p1.keyRules.entry\"}",
                Text(table.Of("levels.entry", "estimate")));
            Assert.Equal("rules/made.json levelProfiles.p1.keyRules.hand alone.rule", (string)table.Of("mirrored", "computed", "alone").Item2["entry"]);
            Assert.Equal("CHOSEN by Bader {\"entry\":\"rules/made.json levelProfiles.p1.keyRules.parameter X.rule\",\"question\":\"Q901\",\"where\":\"made up\"}",
                Text(table.Of("parameters.X", "keyRules")));
            Assert.Equal("CHOSEN by Code {\"entry\":\"config/project.json made.caseRule\",\"where\":\"note\"}", Text(table.Of("parameters.CASE", "keyRules")));
            Assert.Equal("UNKNOWN {\"entry\":\"config/project.json floors.types.generic\",\"why\":\"no Q\",\"nearestSource\":\"floors.note\"}", Text(table.Of("floors.generic.type", "computed")));
        }

        [Fact]
        public void R401_an_override_and_a_client_input_take_the_status_the_map_gives_their_source()
        {
            ValueStatusTable table = Table();
            Assert.Equal("RECOMMENDED {\"question\":\"Q900\",\"where\":\"a made up list\"}", Text(table.Of("parameters.X", "override")));
            Assert.Equal("RECOMMENDED {\"question\":\"Q900\",\"where\":\"a made up list\"}", Text(table.Of("insert", "override")));
            Assert.Equal("DOCUMENT {\"document\":\"a made up plan\"}", Text(table.Of("unit", "locationPlan")));
        }

        [Fact]
        public void R401_an_applied_override_whose_value_keeps_its_source_gives_the_value_the_status_of_the_override_list()
        {
            ValueStatusTable table = Table();
            Assert.Equal("RECOMMENDED {\"question\":\"Q900\",\"where\":\"a made up list\",\"override\":\"entryM\"}", Text(table.Of("levels.entry", "estimate", null, new[] { "entryM", "heightM" })));
            Assert.Equal("MEASURED", table.Of("levels.ffl", "estimate", null, new[] { "entryM" }).Item1);
        }

        [Fact]
        public void R401_a_pair_the_map_does_not_hold_or_an_entry_the_block_lacks_is_unknown_never_a_guess()
        {
            ValueStatusTable table = Table();
            Assert.Equal("UNKNOWN {\"why\":\"valueStatus holds no status for prototypeFile from computed (R401)\"}", Text(table.Of("prototypeFile", "computed")));
            Assert.Equal("UNKNOWN {\"why\":\"valueStatus holds no status for levels.entry from keyRules (R401)\"}", Text(table.Of("levels.entry", "keyRules")));
            Assert.Equal("UNKNOWN {\"why\":\"the block status holds no entry rules/made.json levelProfiles.p1.keyRules.notInTheBlock (R401)\"}", Text(table.Of("floors.generic.level", "computed")));
            Assert.Equal("UNKNOWN {\"why\":\"the plot holds no hand for levelProfiles.{levelProfile}.keyRules.hand {hand}.rule (R401)\"}", Text(table.Of("mirrored", "computed")));
            Assert.Equal("UNKNOWN", Table("p2").Of("levels.entry", "estimate").Item1);
            Assert.Equal("UNKNOWN", new ValueStatusTable(new JObject(), new JObject(), ProfileFile, "p1").Of("unit", "locationPlan").Item1);
            Assert.Equal("MEASURED weak|MEASURED|UNKNOWN", ValueStatus.Label("MEASURED", new JObject { ["strength"] = "weak" }) + "|" + ValueStatus.Label("MEASURED", new JObject())
                + "|" + ValueStatus.Label("UNKNOWN", new JObject { ["strength"] = "weak" }));
        }

        private static Dictionary<string, Dictionary<string, object>> MadeStep(out OverrideParameterStepTests.Built built)
        {
            JObject config = JObject.Parse(File.ReadAllText(WorkerRunTests.RepoConfig()));
            string profileFile = (string)config["profile"];
            built = OverrideParameterStepTests.Build(OverrideParameterStepTests.Made(), changeRules: rules => rules["status"] = MadeBlock(profileFile));
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
            Dictionary<string, Dictionary<string, object>> step = MadeStep(out OverrideParameterStepTests.Built built);
            string profileFile = (string)built.Config["profile"];
            Assert.Equal("{\"source\":\"estimate\",\"status\":\"MEASURED\",\"statusFrom\":{\"entry\":\"" + profileFile + " " + Key + "entry.rule\",\"share\":0.5,\"strength\":\"weak\",\"measuredBy\":\"made\"}}",
                Field(step, "1", "levels.entry").ToString(Formatting.None));
            Assert.Equal("{\"source\":\"estimate\",\"status\":\"RECOMMENDED\",\"statusFrom\":{\"question\":\"Q053\",\"where\":\"steps/investigations/rule-status-audit.md section 6\",\"override\":\"entryM\"}}",
                Field(step, "2", "levels.entry").ToString(Formatting.None));
            Assert.Equal(profileFile + " " + Key + "hand cluster.rule", (string)Field(step, "3", "mirrored")["statusFrom"]["entry"]);
            Assert.Equal("override RECOMMENDED|override RECOMMENDED|DOCUMENT|UNKNOWN", (string)Field(step, "4", "mirrored")["source"] + " " + (string)Field(step, "4", "mirrored")["status"] + "|"
                + (string)Field(step, "2", "parameters.CASE")["source"] + " " + (string)Field(step, "2", "parameters.CASE")["status"] + "|" + (string)Field(step, "1", "unit")["status"]
                + "|" + (string)Field(step, "1", "prototypeFile")["status"]);
            // The fields of a plot with a yard and of one with none, as test_value_status finds them in the plan
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

        [Fact]
        public void R401_the_counts_of_the_build_name_the_plots_by_field_and_status()
        {
            Dictionary<string, Dictionary<string, object>> step = MadeStep(out _);
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
