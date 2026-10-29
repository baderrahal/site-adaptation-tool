using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // Lane B 5b, open items 3 to 8 of docs/design/09-risks.md. The setUp mode of the addinRun job runs button 1 on the four copies of
    // 3.1 as the ribbon runs it, the key copy taken by a file copy and hashed before and after (D19). Files only, both legs
    public class SetUpRunHostTests
    {
        private static string Text(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules", "index.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return File.ReadAllText(Path.Combine(new[] { folder }.Concat(parts).ToArray())).Replace("\r\n", "\n");
        }

        [Fact]
        public void R374_a_job_carries_the_setup_mode_and_a_job_of_5a_has_none()
        {
            Assert.True(JsonConvert.DeserializeObject<TestHostJob>("{\"kind\": \"addinRun\", \"setUp\": true}").SetUp);
            Assert.Null(JsonConvert.DeserializeObject<TestHostJob>("{\"kind\": \"addinRun\"}").SetUp);
        }

        [Fact]
        public void R374_the_run_script_copies_the_key_hashes_its_source_before_and_after_and_writes_the_settings_of_the_run()
        {
            string script = Text("dotnet", "SiteAdaptation.TestHost", "Run-TestHost.ps1");
            int branch = script.IndexOf("        'addinRun' {", StringComparison.Ordinal);
            int setUp = script.IndexOf("            if ($round.setUp) {", branch, StringComparison.Ordinal);
            Assert.True(branch > 0 && setUp > branch);
            Assert.Contains("$setUpHash = (Get-FileHash -LiteralPath $setUpSource).Hash", script);
            Assert.Contains("Copy-Item -LiteralPath $setUpSource -Destination (Join-Path $runFolder ($Code + '-SETUP-IMD.rvt'))", script);
            Assert.Contains("$settingsCopy.addin.position.file = $setUpPosition", script);
            Assert.Contains("-not $setUpPosition.StartsWith($docs + '\\')", script);
            Assert.Contains("$roundJob.setUp = $true", script);
            // The source is held after the run by its hash, write time and size, and a change fails the run
            Assert.Contains("$setUpSame = ((Get-FileHash -LiteralPath $setUpSource).Hash -eq $setUpHash)", script);
            Assert.Contains("$stopReason = 'the key copy of the setUp run changed (D19)'", script);
            Assert.True(script.IndexOf("$stopReason = 'the key copy of the setUp run changed (D19)'", StringComparison.Ordinal)
                < script.IndexOf("if (-not $DryStart -and ($stopReason -or $logResult -ne 'ok')) {", StringComparison.Ordinal));
        }

        [Fact]
        public void R374_the_session_runs_button_1_by_the_ribbon_entry_with_its_own_window_and_puts_both_back()
        {
            string session = Text("dotnet", "SiteAdaptation.TestHost", "AddinRunSession.cs");
            string run = Text("dotnet", "SiteAdaptation.TestHost", "SetUpSiteRun.cs");

            Assert.Contains("setUp = job.SetUp == true ? new SetUpSiteRun(job, runner) : null;", session);
            Assert.Contains("lastOk = runner.Run(setUp.Steps(app));", session);
            Assert.Contains("buttons.GetMethod(AddinRunNames.Run, new[] { typeof(int), typeof(UIApplication) })", run);
            Assert.Contains("stateProperty.SetValue(null, stateBefore);", run);
            Assert.Contains("dialogProperty.SetValue(null, dialogBefore);", run);
            // A dry run first, then Apply unless a view holds a stop, then a second run on the same copy
            Assert.Contains("get { return true; }", run);
            Assert.Contains("SetUpSiteView dry = callbacks.DryRun(Slope);", run);
            Assert.Contains("return new SetUpSiteAnswer { Apply = Stops == 0, RampSlope = Slope };", run);
            Assert.Contains("entry[\"secondResult\"] = Button(again, second);", run);
            // Revit refuses to close the active document from the API (run 1 of 5b), so a copy is closed once the next one is active
            int open = run.IndexOf("Document document = ui.OpenAndActivateDocument(path).Document;", StringComparison.Ordinal);
            int close = run.IndexOf("previous.Close(false);", StringComparison.Ordinal);
            Assert.True(open > 0 && close > open);
            Assert.DoesNotContain("document.Close(false);", run.Substring(open));
            // The four copies of 3.1 and the files that hold coordinates stay in the run folder under the work root
            foreach (string label in new[] { "\"template\"", "\"noTemplate\"", "\"imd\"", "\"r17b\"" })
            {
                Assert.Contains(label, run);
            }
            Assert.Contains("JsonOut.Write(System.IO.Path.Combine(folder, ResultFile), result);", run);
        }
    }
}
