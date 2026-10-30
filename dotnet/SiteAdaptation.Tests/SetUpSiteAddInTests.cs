#if NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SiteAdaptation.AddIn;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.SetUp;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R373, R382. The wiring of button 1 in the add-in on net48, the add-in loads no Revit type here. The window is the other helper's
    // part of 5b, so the body takes it from a factory the main session sets at the merge, and with none set button 1 stays not built
    public sealed class SetUpSiteAddInTests
    {
        [Fact]
        public void R373_button_1_takes_its_window_from_a_public_static_factory_of_its_body_and_stays_not_built_while_it_is_unset()
        {
            Type body = typeof(Bodies).Assembly.GetType("SiteAdaptation.AddIn.SetUpSiteBody");
            Assert.NotNull(body);
            PropertyInfo factory = body.GetProperty("Dialog", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(factory);
            Assert.Equal(typeof(Func<IntPtr, ISetUpSiteDialog>), factory.PropertyType);
            Assert.True(factory.CanWrite);
            Assert.Null(factory.GetValue(null));

            var made = new ButtonBody();
            Assert.Equal("1 Set up site is not built yet.", Bodies.Of(1).NotBuilt);
            Assert.Equal("1 Set up site is not built yet.", Bodies.Of(RibbonLayout.Button(1), () => made).NotBuilt);
            factory.SetValue(null, (Func<IntPtr, ISetUpSiteDialog>)(owner => null));
            try
            {
                Assert.Same(made, Bodies.Of(RibbonLayout.Button(1), () => made));
                Assert.Equal("1 Set up site is not built yet.", Bodies.Of(1).NotBuilt);
                Assert.Equal("2 Read data is not built yet.", Bodies.Of(RibbonLayout.Button(2), () => made).NotBuilt);
                // R375. Button 2 is built once App sets its worker, and only for its own body
                Assert.Equal("2 Read data is not built yet.", Bodies.Of(RibbonLayout.Button(2), () => made, () => made).NotBuilt);
                ReadDataBody.Worker = new LaunchedWorker("CadReader.exe", answer => { });
                try
                {
                    Assert.Same(made, Bodies.Of(RibbonLayout.Button(2), null, () => made));
                    Assert.Equal("3 Review data is not built yet.", Bodies.Of(RibbonLayout.Button(3), () => made, () => made).NotBuilt);
                }
                finally
                {
                    ReadDataBody.Worker = null;
                }
            }
            finally
            {
                factory.SetValue(null, null);
            }
        }

        // The one entry of a button hands the application to the body, so button 1 reads the active document through the flow
        [Fact]
        public void R382_the_entry_of_a_button_hands_the_application_to_its_body_and_button_1_runs_by_set_up_run_on_the_revit_document()
        {
            string addin = Path.Combine(ThemeGateTests.RepoRoot(), "dotnet", "SiteAdaptation.AddIn");
            string buttons = File.ReadAllText(Path.Combine(addin, "Commands", "Buttons.cs"));
            string setUp = File.ReadAllText(Path.Combine(addin, "Commands", "SetUpSiteBody.cs"));

            Assert.Contains("Bodies.Of(button, () => SetUpSiteBody.For(application))", buttons);
            Assert.Contains("new SetUpRun(new RevitSiteDocument(", setUp);
            Assert.Contains("new NoTags()", setUp);
            Assert.Contains("internal sealed class RevitSiteDocument : ISiteDocument", File.ReadAllText(Path.Combine(addin, "Revit", "RevitSiteDocument.cs")));
        }
    }
}
#endif
