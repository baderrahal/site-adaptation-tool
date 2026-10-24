using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Round Z4 jobs of the test host on fakes: the site upgrade, the read, the new site file with its coordinates and the image
    public class SiteJobTests
    {
        private const string Token = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
        private const string Work = @"C:\Docs\round\work";
        private const string Source = Work + @"\site-000004.rvt";
        private const string Upgraded = Work + @"\site-000004_TEST-K24.rvt";
        private const string NewFile = Work + @"\site-Z4-TEST-NEW.rvt";
        private const string Template = @"C:\Templates\DefaultMetric.rte";
        private const string SiteFile = @"C:\Docs\round\site.json";
        private const string Images = @"C:\Docs\round\images";
        private static readonly DateTime Now = new DateTime(2026, 10, 24, 6, 0, 0, DateTimeKind.Utc);

        private static string Job(string kind, string code, Action<JObject> change = null)
        {
            var job = new JObject { ["kind"] = kind, ["token"] = Token, ["code"] = code, ["createdUtc"] = Now.AddMinutes(-2).ToString("o") };
            change?.Invoke(job);
            return job.ToString();
        }

        private static GuardResult Check(string text, Func<string, bool> exists, Func<string, bool> folder = null)
        {
            return TestHostGuard.Check(text, Token, Now, exists, folder ?? (path => true));
        }

        private static string Upgrade(Action<JObject> change = null)
        {
            return Job("siteUpgrade", "K24", job =>
            {
                job["source"] = Source;
                job["target"] = Upgraded;
                change?.Invoke(job);
            });
        }

        [Fact]
        public void R204_a_site_upgrade_saves_the_copy_as_its_own_test_copy_next_to_it_and_is_accepted()
        {
            GuardResult result = Check(Upgrade(), path => path == Source);

            Assert.True(result.Act, result.Reason);
            Assert.True(TestHostGuard.IsUpgrade(result.Job));
        }

        [Theory]
        [InlineData(Work + @"\site-000004_TEST-K25.rvt")]
        [InlineData(Work + @"\other_TEST-K24.rvt")]
        [InlineData(@"C:\Docs\round\site-000004_TEST-K24.rvt")]
        [InlineData(Source)]
        public void R204_a_target_that_is_not_the_test_copy_of_the_source_and_code_is_refused(string target)
        {
            Assert.False(Check(Upgrade(job => job["target"] = target), path => path == Source).Act);
        }

        [Fact]
        public void R204_a_target_that_exists_a_missing_source_or_a_test_copy_as_source_is_refused()
        {
            Assert.Contains("never overwritten", Check(Upgrade(), path => true).Reason);
            Assert.False(Check(Upgrade(), path => false).Act);
            Assert.False(Check(Upgrade(job => { job["source"] = Upgraded; job["target"] = Work + @"\site-000004_TEST-K24_TEST-K24.rvt"; }), path => true).Act);
            Assert.False(Check(Upgrade(job => { job["source"] = NewFile; job["target"] = Work + @"\site-Z4-TEST-NEW_TEST-K24.rvt"; }), path => path == NewFile).Act);
            Assert.False(Check(Upgrade(job => job["model"] = Upgraded), path => path == Source).Act);
            Assert.False(Check(Upgrade(job => job["prototypeFile"] = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt"), path => path == Source).Act);
        }

        [Fact]
        public void R204_the_site_upgrade_job_file_is_an_upgrade_file()
        {
            GuardResult result = Check(Upgrade(), path => path == Source);
            string documents = @"C:\Users\x\Documents";

            Assert.Null(TestHostGuard.CheckJobPath(TestHostGuard.JobPath(documents, "K24", true), result.Job, documents));
            Assert.NotNull(TestHostGuard.CheckJobPath(TestHostGuard.JobPath(documents, "K24", false), result.Job, documents));
        }

        [Fact]
        public void R204_the_site_upgrade_discards_worksets_and_saves_no_central()
        {
            string steps = SourceText("SiteAdaptation.TestHost", "JobSteps.cs");

            Assert.Contains("site ? DetachFromCentralOption.DetachAndDiscardWorksets : DetachFromCentralOption.DetachAndPreserveWorksets", steps);
            Assert.Contains("job.Kind == TestHostGuard.UpgradeKind && Path.GetFileName(job.Target) != Path.GetFileName(job.Source)", steps);
        }

        [Fact]
        public void R205_a_read_opens_a_test_copy_that_exists_and_names_no_plan_or_prototype()
        {
            Assert.True(Check(Job("read", "K24", job => job["model"] = Upgraded), path => path == Upgraded).Act);
            Assert.False(Check(Job("read", "K24", job => job["model"] = Source), path => true).Act);
            Assert.False(Check(Job("read", "K24", job => job["model"] = NewFile), path => true).Act);
            Assert.False(Check(Job("read", "K25", job => job["model"] = Upgraded), path => true).Act);
            Assert.False(Check(Job("read", "K24", job => { job["model"] = Upgraded; job["plan"] = @"C:\Docs\plan.json"; }), path => true).Act);
            Assert.False(Check(Job("read", "K24", job => { job["model"] = Upgraded; job["prototypeFile"] = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt"; }), path => true).Act);
            Assert.False(Check(Job("read", "K24", job => job["model"] = Upgraded), path => false).Act);
        }

        [Fact]
        public void R205_a_read_only_opens_reads_and_closes()
        {
            string session = SourceText("SiteAdaptation.TestHost", "JobSession.cs");
            int start = session.IndexOf("private IEnumerable<JobStep> SiteJobSteps", StringComparison.Ordinal);
            string body = session.Substring(start, session.IndexOf("// R117. The master job", start, StringComparison.Ordinal) - start);

            Assert.DoesNotContain("Save", body.Replace("saveNew", "").Replace("site.SaveNew", "").Replace("None of them saves", "").Replace("saves only the file it made", ""));
        }

        [Fact]
        public void R205_an_instance_whose_rotation_revit_cannot_give_takes_the_rotation_of_its_transform()
        {
            string reader = SourceText("RevitPrototypeReader", "RevitExtractor.cs");

            Assert.Contains("catch (Autodesk.Revit.Exceptions.InternalException)", reader);
            Assert.Contains("rotation = PointRotation(location);", reader);
            Assert.Contains("Math.Atan2(transform.BasisX.Y, transform.BasisX.X)", reader);
            Assert.DoesNotContain("UnitMath.RadiansToDegrees(location.Rotation)", reader);
        }

        [Fact]
        public void R206_an_answer_key_plan_is_never_placed_and_a_plain_plan_passes_the_same_check()
        {
            string plan = "{\"format\": \"site-adaptation-plan\", \"formatVersion\": 1, \"surveyPoint\": {\"position\": [0, 0, 0], \"sharedPosition\": [1, 2, 0]},"
                + " \"angleToTrueNorthDeg\": 0, \"prototypeFolder\": \"C:\\\\Prototypes\"";

            Assert.Contains("never placed", Placer.CheckFormat(PlanFile.Parse(plan + ", \"answerKeyOnly\": true}")));
            Assert.Null(Placer.CheckFormat(PlanFile.Parse(plan + ", \"answerKeyOnly\": false}")));
            Assert.Null(Placer.CheckFormat(PlanFile.Parse(plan + "}")));
        }

        private static string NewSite(Action<JObject> change = null)
        {
            return Job("newSite", "Z4N", job =>
            {
                job["model"] = NewFile;
                job["template"] = Template;
                job["site"] = SiteFile;
                change?.Invoke(job);
            });
        }

        [Fact]
        public void R212_a_new_site_job_with_a_template_a_site_file_and_no_file_of_its_name_is_accepted()
        {
            GuardResult result = Check(NewSite(), path => path == Template || path == SiteFile);

            Assert.True(result.Act, result.Reason);
        }

        [Fact]
        public void R212_a_new_site_job_never_overwrites_and_needs_its_template_and_site_file()
        {
            Assert.Contains("never overwritten", Check(NewSite(), path => true).Reason);
            Assert.False(Check(NewSite(), path => path == Template).Act);
            Assert.False(Check(NewSite(), path => path == SiteFile).Act);
            Assert.False(Check(NewSite(job => job["template"] = @"C:\Templates\DefaultMetric.rvt"), path => path != NewFile).Act);
            Assert.False(Check(NewSite(job => job["site"] = "site.json"), path => path != NewFile).Act);
            Assert.False(Check(NewSite(job => job["plan"] = @"C:\Docs\plan.json"), path => path != NewFile).Act);
            Assert.False(Check(NewSite(), path => path != NewFile, folder => false).Act);
        }

        [Theory]
        [InlineData(Work + @"\site_TEST-Z4N.rvt")]
        [InlineData(Work + @"\site_TEST-X-TEST-NEW.rvt")]
        [InlineData(Work + @"\-TEST-NEW.rvt")]
        [InlineData(Work + @"\site-TEST-NEW-TEST-NEW.rvt")]
        [InlineData(@"site-Z4-TEST-NEW.rvt")]
        public void R213_only_a_plain_name_ending_in_the_new_tag_is_a_new_site_file(string model)
        {
            Assert.False(Check(NewSite(job => job["model"] = model), path => path == Template || path == SiteFile).Act);
            Assert.False(TestHostGuard.IsNewSite(model));
        }

        [Fact]
        public void R213_the_floor_steps_and_the_image_may_open_the_new_site_file_and_no_other_job_may()
        {
            Func<string, bool> all = path => true;
            string plan = @"C:\Docs\plan_z4.json";
            const string prototype = "RRE-WP-NSP-KEO-ARC-DP4-ZZ-MOD-2400.rvt";

            Assert.True(Check(Job("linksAndFloors", "S40", job => { job["model"] = NewFile; job["plan"] = plan; job["prototypeFile"] = prototype; }), all).Act);
            Assert.True(Check(Job("floors", "S41", job => { job["model"] = NewFile; job["plan"] = plan; }), all).Act);
            Assert.False(Check(Job("place", "DP4", job => { job["model"] = NewFile; job["plan"] = plan; job["prototypeFile"] = prototype; }), all).Act);
            Assert.False(Check(Job("master", "MST", job => { job["model"] = NewFile; job["plan"] = plan; job["deleteFloors"] = "all"; }), all).Act);
            Assert.False(Check(Job("read", "Z4N", job => job["model"] = NewFile), all).Act);
            Assert.True(TestHostFiles.IsSavable(NewFile));
            Assert.True(TestHostFiles.IsSavable(Upgraded));
            Assert.False(TestHostFiles.IsSavable(Source));
            Assert.Null(TestHostGuard.OriginalOf(NewFile));
        }

        [Fact]
        public void R213_backups_of_the_new_site_file_are_found_by_its_own_name_only()
        {
            List<string> found = TestHostFiles.BackupFiles(NewFile, new[] { "site-Z4-TEST-NEW.0001.rvt", "site-Z4-TEST-NEW.rvt", "site-000004.0001.rvt", "site-Z4-TEST-NEW.01.rvt" });

            Assert.Equal(new[] { Work + @"\site-Z4-TEST-NEW.0001.rvt" }, found);
            Assert.Equal(Work + @"\site-Z4-TEST-NEW_backup", TestHostFiles.BackupFolder(NewFile));
        }

        private static string Image(Action<JObject> change = null)
        {
            return Job("image", "K24", job =>
            {
                job["model"] = Upgraded;
                job["imageFolder"] = Images;
                job["imageLevel"] = "Level 1";
                job["area"] = new JArray(1000.0, 2000.0, 1300.0, 2250.0);
                change?.Invoke(job);
            });
        }

        [Fact]
        public void R214_an_image_job_of_a_test_copy_or_the_new_site_file_with_an_area_is_accepted()
        {
            Assert.True(Check(Image(), path => true).Act);
            Assert.True(Check(Image(job => { job["model"] = NewFile; job["code"] = "Z4N"; }), path => true).Act);
        }

        [Theory]
        [InlineData(1000.0, 2000.0, 1000.0, 2250.0)]
        [InlineData(1300.0, 2000.0, 1000.0, 2250.0)]
        [InlineData(0.0, 0.0, 6000.0, 10.0)]
        [InlineData(double.NaN, 0.0, 10.0, 10.0)]
        public void R214_an_area_with_no_size_the_wrong_order_or_too_large_is_refused(double minE, double minN, double maxE, double maxN)
        {
            Assert.False(Check(Image(job => job["area"] = new JArray(minE, minN, maxE, maxN)), path => true).Act);
        }

        [Fact]
        public void R214_an_image_needs_its_folder_and_level_and_its_fields_belong_to_it_alone()
        {
            Assert.False(Check(Image(), path => true, folder => false).Act);
            Assert.False(Check(Image(job => job["imageLevel"] = " "), path => true).Act);
            Assert.False(Check(Image(job => job.Remove("area")), path => true).Act);
            Assert.False(Check(Image(job => job["area"] = new JArray(1.0, 2.0, 3.0)), path => true).Act);
            Assert.Contains("image job only", Check(Job("read", "K24", job => { job["model"] = Upgraded; job["imageLevel"] = "Level 1"; }), path => true).Reason);
            Assert.Contains("newSite job only", Check(Image(job => job["template"] = Template), path => true).Reason);
        }

        // R211 and R212 site spec

        private static SiteSpec Spec(Action<JObject> change = null)
        {
            var spec = new JObject
            {
                ["format"] = SiteSpec.FormatText,
                ["projectPosition"] = new JObject { ["eastM"] = 1200.0, ["northM"] = 3400.0, ["elevationM"] = 0.0, ["angleDeg"] = 0.0 },
                ["levels"] = new JArray("Level 1", "Level 2"),
                ["floorTypes"] = new JObject { ["generic"] = new JArray("Generic 150mm"), ["yard"] = new JArray("yard"), ["ramp"] = new JArray("Ramps 0.5%", "Ramps 2%") },
                ["floorParameter"] = "Plot",
                ["linkParameters"] = new JArray("P-Number", "CASE"),
                ["sharedParameterFile"] = @"C:\Docs\round\work\parameters.txt",
                ["parameterGroup"] = "SiteAdaptation",
                ["checkPoints"] = new JArray(new JObject { ["name"] = "c1", ["shared"] = new JArray(1210.0, 3390.0, 5.0) }),
            };
            change?.Invoke(spec);
            return SiteSpec.Parse(spec.ToString());
        }

        [Fact]
        public void R212_a_full_spec_passes_and_each_missing_or_doubled_part_is_refused()
        {
            Assert.Null(SiteCoordinates.Check(Spec()));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["format"] = "other")));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => ((JObject)spec["projectPosition"]).Remove("angleDeg"))));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["projectPosition"]["eastM"] = 1e9)));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["levels"] = new JArray("Level 1", "Level 1"))));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => ((JObject)spec["floorTypes"]).Remove("yard"))));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["floorTypes"]["other"] = new JArray("x"))));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["floorTypes"]["yard"] = new JArray("Generic 150mm"))));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["linkParameters"] = new JArray("Plot"))));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["sharedParameterFile"] = "parameters.txt")));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["parameterGroup"] = "")));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["checkPoints"] = new JArray(new JObject { ["name"] = "c1", ["shared"] = new JArray(1.0, 2.0) }))));
            Assert.NotNull(SiteCoordinates.Check(Spec(spec => spec["levels"] = new JArray(" Level 1"))));
        }

        [Fact]
        public void R211_with_angle_0_the_internal_point_is_the_shared_point_less_the_position()
        {
            SitePosition at = Spec().ProjectPosition;

            Assert.Equal(new[] { 10.0, -10.0, 5.0 }, SiteCoordinates.ToInternal(at, new[] { 1210.0, 3390.0, 5.0 }));
            Assert.Equal(new[] { -1200.0, -3400.0, 0.0 }, SiteCoordinates.SurveyPointInternal(at));
            Assert.Equal(new[] { 1200.0 / 0.3048, 3400.0 / 0.3048, 0.0, 0.0 }, SiteCoordinates.ProjectPositionFeet(at));
        }

        [Theory]
        [InlineData(30.0)]
        [InlineData(-12.5)]
        [InlineData(90.0)]
        public void R211_with_an_angle_internal_and_shared_turn_back_into_each_other_and_keep_distances(double angle)
        {
            SitePosition at = Spec(spec => spec["projectPosition"]["angleDeg"] = angle).ProjectPosition;
            double[] shared = { 1250.0, 3333.0, 7.0 };

            double[] back = SiteCoordinates.ToShared(at, SiteCoordinates.ToInternal(at, shared));
            double[] origin = SiteCoordinates.ToShared(at, new[] { 0.0, 0.0, 0.0 });
            double[] east = SiteCoordinates.ToShared(at, new[] { 1.0, 0.0, 0.0 });

            Assert.True(SiteCoordinates.Gap(shared, back) < 1e-9);
            Assert.Equal(new[] { 1200.0, 3400.0, 0.0 }, origin);
            Assert.Equal(angle, Math.Atan2(east[1] - origin[1], east[0] - origin[0]) * 180.0 / Math.PI, 9);
        }

        [Fact]
        public void R211_the_check_points_report_the_gap_to_what_revit_gives()
        {
            List<CheckPointResult> exact = SiteCoordinates.CompareCheckPoints(Spec(), shared => new[] { shared[0] - 1200.0, shared[1] - 3400.0, shared[2] });
            List<CheckPointResult> off = SiteCoordinates.CompareCheckPoints(Spec(), shared => new[] { shared[0] - 1200.0 + 0.003, shared[1] - 3400.0 - 0.004, shared[2] });

            Assert.Equal(0.0, Assert.Single(exact).GapMm, 9);
            Assert.Equal(5.0, Assert.Single(off).GapMm, 6);
            Assert.True(off[0].GapMm > SiteCoordinates.ToleranceMm);
        }

        [Fact]
        public void R212_the_new_site_steps_run_in_order_and_put_the_shared_parameter_setting_back()
        {
            string session = SourceText("SiteAdaptation.TestHost", "JobSession.cs");
            string site = SourceText("SiteAdaptation.TestHost", "SiteSteps.cs");
            string[] names = { "create", "coordinates", "levels", "floorTypes", "parameters", "saveNew", "close", "jobFile" };
            int start = session.IndexOf("private IEnumerable<JobStep> SiteJobSteps", StringComparison.Ordinal);
            int[] at = names.Select(name => session.IndexOf("Name = \"" + name + "\"", start, StringComparison.Ordinal)).ToArray();

            Assert.All(at, index => Assert.True(index > 0));
            Assert.Equal(at.OrderBy(index => index), at);
            Assert.Contains("app.SharedParametersFilename = before;", site);
            Assert.Contains("new SaveAsOptions { OverwriteExistingFile = false, MaximumBackups = 1 }", site);
            Assert.Contains("SpecTypeId.String.Text", site);
            Assert.DoesNotContain("OverwriteExistingFile = true", site);
        }

        private static string SourceText(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return File.ReadAllText(Path.Combine(new[] { folder, "dotnet" }.Concat(parts).ToArray()));
        }
    }
}
