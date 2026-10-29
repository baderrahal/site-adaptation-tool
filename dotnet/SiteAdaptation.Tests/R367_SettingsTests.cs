using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R367, docs/design/06-settings.md. The settings JSON kept in the project, its schema and the rule that only a real run writes it
    public class R367_SettingsTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private sealed class MemoryEntity : ISettingsEntity
        {
            public string Text { get; set; }
            public int Writes { get; private set; }

            public string Read()
            {
                return Text;
            }

            public void Write(string json)
            {
                Writes++;
                Text = json;
            }
        }

        private sealed class OpenTransaction : IModelTransaction
        {
            public bool Commit()
            {
                return true;
            }

            public void RollBack()
            {
            }

            public IReadOnlyList<string> Warnings
            {
                get { return new string[0]; }
            }

            public IReadOnlyList<string> Errors
            {
                get { return new string[0]; }
            }

            public void Dispose()
            {
            }
        }

        private static ProjectSettings Sample()
        {
            var settings = new ProjectSettings
            {
                ConfigFile = @"D:\job\config\project.json",
                RulesFile = @"D:\job\rules\profile.json",
                LevelProfile = "parsons2024Rev6",
                Scope = new SettingsScope { Kind = ScopeKinds.Nbh, Nbh = "013" },
                DryRunFirst = false,
            };
            settings.Inputs[InputNames.Curbs] = new SettingsInput { Path = @"D:\job\in\curbs.dwg", Size = 1234, Sha256 = Hash };
            settings.Inputs[InputNames.Prototypes] = new SettingsInput { Path = @"D:\job\in\prototypes" };
            return settings;
        }

        private static ButtonRun Run(string time)
        {
            var run = new ButtonRun { Time = time, Mode = RunModes.AfterDryRun, Scope = new SettingsScope { Kind = ScopeKinds.Plots, Plots = "2290-2299" } };
            run.InputHashes[InputNames.Curbs] = Hash;
            run.Counts["placed"] = 10;
            run.Counts["skipped"] = 2;
            run.Issues.Add("plot 2291: no front edge");
            return run;
        }

        [Fact]
        public void R367_the_settings_json_holds_the_fields_of_section_6_and_reads_back_as_written()
        {
            ProjectSettings settings = Sample();
            settings.LastRuns["7"] = Run("2026-10-29T10:00:00Z");
            string json = SettingsJson.Write(settings);
            foreach (string name in new[] { "formatVersion", "configFile", "rulesFile", "levelProfile", "inputs", "path", "size", "sha256", "scope", "kind", "nbh", "dryRunFirst", "lastRuns", "time", "mode", "inputHashes", "counts", "issues" })
            {
                Assert.Contains("\"" + name + "\"", json);
            }
            ProjectSettings read = SettingsJson.Read(json);
            Assert.Equal(SettingsJson.Version, read.FormatVersion);
            Assert.Equal("parsons2024Rev6", read.LevelProfile);
            Assert.Equal(1234, read.Inputs[InputNames.Curbs].Size);
            Assert.Null(read.Inputs[InputNames.Prototypes].Sha256);
            Assert.Equal("013", read.Scope.Nbh);
            Assert.False(read.DryRunFirst);
            Assert.Equal(10, read.LastRuns["7"].Counts["placed"]);
            Assert.Equal("2290-2299", read.LastRuns["7"].Scope.Plots);
            Assert.Equal(json, SettingsJson.Write(read));
        }

        [Fact]
        public void R367_a_new_project_starts_with_dry_run_first_and_the_whole_project()
        {
            ProjectSettings settings = SettingsJson.Read("{\"formatVersion\": 1}");
            Assert.True(settings.DryRunFirst);
            Assert.Equal(ScopeKinds.Project, settings.Scope.Kind);
            Assert.Empty(settings.Inputs);
            Assert.Empty(settings.LastRuns);
        }

        [Fact]
        public void R367_a_settings_json_of_a_later_format_version_is_refused_with_both_versions_named()
        {
            var error = Assert.Throws<InvalidDataException>(() => SettingsJson.Read("{\"formatVersion\": 2, \"later\": {\"field\": 1}}"));
            Assert.Contains("formatVersion 2", error.Message);
            Assert.Contains("formatVersion " + SettingsJson.Version, error.Message);
        }

        [Theory]
        [InlineData("[1, 2]")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("{\"formatVersion\": \"1\"}")]
        [InlineData("{\"formatVersion\": 0}")]
        [InlineData("{\"formatVersion\": 1.5}")]
        public void R367_a_settings_json_with_no_whole_format_version_from_1_is_refused(string json)
        {
            Assert.Throws<InvalidDataException>(() => SettingsJson.Read(json));
        }

        [Theory]
        [InlineData("{\"formatVersion\": 1, \"origin\": [1.5, 2.5]}")]
        [InlineData("{\"formatVersion\": 1, \"scope\": {\"kind\": \"project\", \"level\": 12.5}}")]
        [InlineData("{\"formatVersion\": 1, \"lastRuns\": {\"6\": {\"counts\": {\"top\": 12.5}}}}")]
        [InlineData("{\"formatVersion\": 1, \"inputs\": {\"curbs\": {\"path\": \"a.dwg\", \"x\": 3}}}")]
        public void R367_a_field_the_settings_json_does_not_hold_or_a_number_that_is_no_count_is_refused_so_no_coordinate_or_level_gets_in(string json)
        {
            Assert.Throws<InvalidDataException>(() => SettingsJson.Read(json));
        }

        [Fact]
        public void R367_a_number_with_decimals_in_a_field_that_holds_a_count_is_refused_by_its_value_and_path()
        {
            // Review 1 of 4h-1: counts is a known field, so only the decimals check refuses this one
            var refused = Assert.Throws<InvalidDataException>(() => SettingsJson.Read("{\"formatVersion\": 1, \"lastRuns\": {\"6\": {\"counts\": {\"top\": 12.5}}}}"));
            Assert.Contains("the number with decimals 12.5 at lastRuns.6.counts.top", refused.Message);
        }

#if NETFRAMEWORK
        // The add-in is built for net48 only, so the pane lists are held on that runtime
        [Fact]
        public void R367_the_lists_of_common_and_of_the_pane_name_the_same_values_until_5c_joins_them()
        {
            // Review 1 of 4h-1: join J1 of 5c moves the pane onto SettingsJson, till then a value one side names the other refuses
            Assert.Equal(SiteAdaptation.AddIn.Pane.ScopeKinds.All, ScopeKinds.All);
            Assert.Equal(SiteAdaptation.AddIn.Pane.VillaModes.All, VillaModes.All);
            Assert.Equal(SiteAdaptation.AddIn.Pane.InputKeys.All, InputNames.All);
        }
#endif

        [Fact]
        public void R367_no_field_of_the_settings_json_holds_a_number_with_decimals()
        {
            Type[] types = { typeof(ProjectSettings), typeof(SettingsInput), typeof(SettingsScope), typeof(ButtonRun) };
            Type[] fractions = { typeof(double), typeof(float), typeof(decimal), typeof(double?), typeof(float?), typeof(decimal?) };
            var found = new List<string>();
            foreach (Type type in types)
            {
                foreach (PropertyInfo property in type.GetProperties())
                {
                    Type[] parts = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments() : new[] { property.PropertyType };
                    if (fractions.Contains(property.PropertyType) || parts.Any(part => fractions.Contains(part)))
                    {
                        found.Add(type.Name + "." + property.Name);
                    }
                }
            }
            Assert.Empty(found);
        }

        [Theory]
        [InlineData("{\"formatVersion\": 1, \"inputs\": {\"drawing\": {\"path\": \"a.dwg\"}}}")]
        [InlineData("{\"formatVersion\": 1, \"scope\": {\"kind\": \"zone\"}}")]
        [InlineData("{\"formatVersion\": 1, \"inputs\": {\"curbs\": {\"path\": \"a.dwg\", \"sha256\": \"ABC\"}}}")]
        [InlineData("{\"formatVersion\": 1, \"inputs\": {\"curbs\": {\"path\": \"a.dwg\", \"size\": -1}}}")]
        [InlineData("{\"formatVersion\": 1, \"villaMode\": \"boxes\"}")]
        public void R367_an_input_scope_hash_size_or_villa_mode_the_add_in_does_not_know_is_refused_by_name(string json)
        {
            var error = Assert.Throws<InvalidDataException>(() => SettingsJson.Read(json));
            Assert.Matches(new Regex("drawing|zone|ABC|-1|boxes"), error.Message);
        }

        [Fact]
        public void R367_the_inputs_are_the_eight_of_section_6_and_the_parameter_sheet_of_the_pane()
        {
            Assert.Equal(new[] { "baseplan", "villaBlocks", "curbs", "roadSurface", "dlus", "sales", "plotNumbers", "prototypes", "parameterSheet" }, InputNames.All);
        }

        [Fact]
        public void R367_the_schema_is_SiteAdaptation_Settings_with_a_fixed_guid_one_string_field_and_the_vendor_of_the_manifest()
        {
            Assert.Equal("SiteAdaptation_Settings", SettingsSchema.Name);
            Assert.Equal(new Guid("8faba002-4d7b-46ad-8f25-8d4c81ec65eb"), SettingsSchema.Guid);
            Assert.Matches(new Regex("^[A-Za-z][A-Za-z0-9_]*$"), SettingsSchema.Name);
            Assert.Matches(new Regex("^[A-Za-z][A-Za-z0-9_]*$"), SettingsSchema.Field);
            string manifest = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "SiteAdaptationAddIn_Revit2024.addin");
            Assert.Equal(SettingsSchema.VendorId, (string)XDocument.Load(manifest).Root.Element("AddIn").Element("VendorId"));
        }

        [Fact]
        public void R367_a_pick_changes_the_settings_in_memory_only()
        {
            var entity = new MemoryEntity { Text = SettingsJson.Write(Sample()) };
            var store = new SettingsStore(entity);
            ProjectSettings settings = store.Load();
            settings.LevelProfile = "native2022";
            settings.Inputs[InputNames.Sales] = new SettingsInput { Path = @"D:\job\in\sales.xlsx" };
            Assert.Equal(0, entity.Writes);
            Assert.Equal("parsons2024Rev6", store.Load().LevelProfile);
        }

        [Fact]
        public void R367_a_dry_run_and_cancel_write_nothing_and_leave_the_settings_as_they_were()
        {
            var entity = new MemoryEntity();
            var store = new SettingsStore(entity);
            ProjectSettings settings = Sample();
            using (var transaction = new OpenTransaction())
            {
                Assert.False(store.Record(settings, 7, Run("2026-10-29T10:00:00Z"), RunKind.Dry, transaction));
            }
            Assert.False(store.Record(settings, 7, Run("2026-10-29T10:00:00Z"), RunKind.Dry, null));
            Assert.Equal(0, entity.Writes);
            Assert.Null(entity.Text);
            Assert.Empty(settings.LastRuns);
            Assert.Null(store.Load());
        }

        [Fact]
        public void R367_a_real_run_writes_the_settings_with_its_record_inside_its_transaction()
        {
            var entity = new MemoryEntity();
            var store = new SettingsStore(entity);
            ProjectSettings settings = Sample();
            using (var transaction = new OpenTransaction())
            {
                Assert.True(store.Record(settings, 6, Run("2026-10-29T10:00:00Z"), RunKind.Real, transaction));
            }
            Assert.Equal(1, entity.Writes);
            // Review 1 of 4h-1: the record goes on a copy, the settings of the pane are left as they were
            Assert.Empty(settings.LastRuns);
            ProjectSettings read = store.Load();
            Assert.Equal("2026-10-29T10:00:00Z", read.LastRuns["6"].Time);
            Assert.Equal(new[] { "plot 2291: no front edge" }, read.LastRuns["6"].Issues);
            using (var transaction = new OpenTransaction())
            {
                store.Record(settings, 6, Run("2026-10-29T11:00:00Z"), RunKind.Real, transaction);
            }
            Assert.Equal("2026-10-29T11:00:00Z", store.Load().LastRuns["6"].Time);
            Assert.Single(store.Load().LastRuns);
        }

        [Fact]
        public void R367_a_real_run_with_no_open_transaction_is_refused()
        {
            var entity = new MemoryEntity();
            var store = new SettingsStore(entity);
            var error = Assert.Throws<InvalidOperationException>(() => store.Record(Sample(), 6, Run("2026-10-29T10:00:00Z"), RunKind.Real, null));
            Assert.Contains("transaction", error.Message);
            Assert.Equal(0, entity.Writes);
        }

        [Fact]
        public void R367_the_revit_side_takes_the_schema_from_common_and_writes_only_while_the_document_is_modifiable()
        {
            string source = File.ReadAllText(Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn", "Revit", "SettingsEntity.cs"));
            Assert.Contains("SettingsSchema.Guid", source);
            Assert.Contains("SettingsSchema.Name", source);
            Assert.Contains("SettingsSchema.VendorId", source);
            Assert.Contains("ProjectInformation", source);
            Assert.Contains("IsModifiable", source);
            Assert.Contains("AccessLevel.Vendor", source);
            Assert.DoesNotMatch(new Regex(@"[0-9a-f]{8}-[0-9a-f]{4}-"), source);
            Assert.DoesNotMatch(new Regex(@"Registry"), source);
        }
    }
}
