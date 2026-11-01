using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R443 Door level source counts, SCAN-UI of Lane C. Made up plots and a civil file of the test folder only
    public class R443_DoorLevelPickTests
    {
        private static JObject Config(string source, string civilPath)
        {
            return new JObject
            {
                ["designBasis"] = new JObject
                {
                    ["mode"] = "rev6",
                    ["doorLevel"] = new JObject
                    {
                        ["source"] = source,
                        ["sources"] = new JArray(DesignBasis.CivilFile, DesignBasis.RoadPlusSlope),
                        ["civilFile"] = new JObject { ["path"] = civilPath == null ? JValue.CreateNull() : (JToken)civilPath, ["plotColumn"] = "Plot No.", ["levelColumn"] = "El @ Entry" },
                    },
                },
            };
        }

        // The step levels as the worker writes it (R345), each plot with its doorSource on the raw and the written side
        private static JObject Levels(params string[] sources)
        {
            var plots = new Dictionary<string, Dictionary<string, object>>();
            for (int at = 0; at < sources.Length; at++)
            {
                plots[(at + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object> { ["unit"] = "VL4", ["doorSource"] = sources[at] };
            }
            return StepFile.Build(KeyLevels.StepName, "csharp", "nbh=012", plots, new ParityField[0]);
        }

        private static string CivilFolder()
        {
            string root = Path.Combine(Path.GetTempPath(), "r443_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            Directory.CreateDirectory(Path.Combine(root, "civil"));
            File.WriteAllText(Path.Combine(root, "civil", "entry.csv"), "Plot No.,El @ Entry\n1,1.5\n");
            return root;
        }

        [Fact]
        public void R443_each_choice_counts_the_plots_of_the_step_levels_whose_doorSource_is_that_source()
        {
            string root = CivilFolder();
            JObject config = Config(DesignBasis.RoadPlusSlope, "civil/entry.csv");
            DoorLevelPick pick = DoorLevelPick.Of(config, Path.Combine(root, "config", "project.json"), Levels("civilFile", "roadPlusSlope", "roadPlusSlope", null), null);

            Assert.Equal(new[] { DesignBasis.CivilFile, DesignBasis.RoadPlusSlope }, pick.Choices.Select(choice => choice.Source));
            Assert.Equal(new[] { 1, 2 }, pick.Choices.Select(choice => choice.Plots));
            Assert.Equal(new[] { "Civil entry file (1 plots)", "Road level plus slope (2 plots)" }, pick.Choices.Select(choice => choice.Label));
            Assert.All(pick.Choices, choice => Assert.True(choice.Enabled));
            Assert.False(pick.CivilGrey);
            Assert.Null(pick.Help);
        }

        [Fact]
        public void R443_a_run_with_no_step_levels_or_the_past_job_counts_0_plots_of_each_source()
        {
            DoorLevelPick none = DoorLevelPick.Of(Config(DesignBasis.RoadPlusSlope, null), @"C:\work\config\project.json", null, null);
            DoorLevelPick past = DoorLevelPick.Of(Config(DesignBasis.RoadPlusSlope, null), @"C:\work\config\project.json", Levels(null, null), null);

            Assert.Equal(new[] { 0, 0 }, none.Choices.Select(choice => choice.Plots));
            Assert.Equal(new[] { 0, 0 }, past.Choices.Select(choice => choice.Plots));
            Assert.Equal("Road level plus slope (1,234 plots)", DoorLevelPick.Label(DesignBasis.RoadPlusSlope, 1234));
        }

        [Fact]
        public void R443_the_selected_choice_is_the_settings_pick_else_the_source_of_config()
        {
            string root = CivilFolder();
            string configPath = Path.Combine(root, "config", "project.json");

            Assert.Equal(DesignBasis.CivilFile, DoorLevelPick.Of(Config(DesignBasis.CivilFile, "civil/entry.csv"), configPath, null, null).Selected);
            Assert.Equal(DesignBasis.RoadPlusSlope, DoorLevelPick.Of(Config(DesignBasis.CivilFile, "civil/entry.csv"), configPath, null, DoorLevelSources.RoadPlusSlope).Selected);
            Assert.Equal(DesignBasis.CivilFile, DoorLevelPick.Of(Config(DesignBasis.RoadPlusSlope, "civil/entry.csv"), configPath, null, DoorLevelSources.CivilFile).Selected);
            Assert.Equal(DesignBasis.RoadPlusSlope, DoorLevelPick.Of(Config(DesignBasis.RoadPlusSlope, "civil/entry.csv"), configPath, null, null).Selected);
            Assert.Equal(DesignBasis.CivilFile, DoorLevelPick.Of(Config(DesignBasis.CivilFile, Path.Combine(root, "civil", "entry.csv")), @"C:\elsewhere\config\project.json", null, null).Selected);
        }

        [Fact]
        public void R443_the_civil_choice_is_grey_with_the_help_line_when_config_names_no_civil_file_or_the_file_is_not_on_disk()
        {
            string root = CivilFolder();
            string configPath = Path.Combine(root, "config", "project.json");
            DoorLevelPick noPath = DoorLevelPick.Of(Config(DesignBasis.RoadPlusSlope, null), configPath, Levels("roadPlusSlope"), null);
            DoorLevelPick empty = DoorLevelPick.Of(Config(DesignBasis.RoadPlusSlope, ""), configPath, null, null);
            DoorLevelPick missing = DoorLevelPick.Of(Config(DesignBasis.CivilFile, "civil/gone.csv"), configPath, null, DoorLevelSources.CivilFile);

            foreach (DoorLevelPick pick in new[] { noPath, empty, missing })
            {
                Assert.True(pick.CivilGrey);
                Assert.False(pick.Choices[0].Enabled);
                Assert.True(pick.Choices[1].Enabled);
                Assert.Equal(DoorLevelPick.HelpLine, pick.Help);
                // A grey choice cannot be the selected one of a dropdown, so road level plus slope is taken
                Assert.Equal(DesignBasis.RoadPlusSlope, pick.Selected);
            }
            Assert.Equal("No civil entry file in the setup, so the door level comes from road level plus slope.", DoorLevelPick.HelpLine);
            Assert.Equal(1, noPath.Choices[1].Plots);
        }

        [Fact]
        public void R443_a_config_with_no_door_level_block_gives_no_picker()
        {
            Assert.Null(DoorLevelPick.Of(new JObject(), @"C:\work\config\project.json", Levels("roadPlusSlope"), null));
            Assert.Null(DoorLevelPick.Of(new JObject { ["designBasis"] = new JObject { ["mode"] = "roshnPastJob" } }, @"C:\work\config\project.json", null, null));
        }

        [Fact]
        public void R443_the_settings_keep_doorLevelSource_round_trip_leave_it_out_while_null_and_refuse_another_value()
        {
            Assert.Equal(DesignBasis.CivilFile, DoorLevelSources.CivilFile);
            Assert.Equal(DesignBasis.RoadPlusSlope, DoorLevelSources.RoadPlusSlope);

            var settings = new ProjectSettings { DoorLevelSource = DoorLevelSources.CivilFile };
            string json = SettingsJson.Write(settings);
            Assert.Contains("\"doorLevelSource\": \"civilFile\"", json);
            Assert.Equal(DoorLevelSources.CivilFile, SettingsJson.Read(json).DoorLevelSource);

            ProjectSettings read = SettingsJson.Read("{\"formatVersion\":1,\"inputs\":{},\"scope\":{\"kind\":\"project\"},\"dryRunFirst\":true,\"lastRuns\":{}}");
            Assert.Null(read.DoorLevelSource);
            Assert.DoesNotContain("doorLevelSource", SettingsJson.Write(read));
            Assert.Equal(1, read.FormatVersion);

            settings.DoorLevelSource = "survey";
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => SettingsJson.Read(SettingsJson.Write(settings)));
            Assert.Contains("doorLevelSource survey", refused.Message);
        }
    }
}
