using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Autodesk.Revit.UI;

namespace SiteAdaptation.TestHost
{
    // R101. Revit loads this at every start. It does nothing, not even an event subscription, unless the run script
    // started this Revit with a token and a job file waits in Documents\SiteAdaptation
    public class TestHostApp : IExternalApplication
    {
        // Kept as text here, a type of SiteAdaptation.Common must not load before the resolver is in place
        private const string TokenVariable = "SITEADAPTATION_TESTHOST_TOKEN";
        private const string JobFileName = "testhost_job.json";

        private static string addinsFolder;

        public Result OnStartup(UIControlledApplication application)
        {
            string token = Environment.GetEnvironmentVariable(TokenVariable);
            if (string.IsNullOrEmpty(token))
            {
                return Result.Succeeded;
            }
            string jobPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SiteAdaptation", JobFileName);
            if (!File.Exists(jobPath))
            {
                return Result.Succeeded;
            }
            addinsFolder = Path.GetDirectoryName(Path.GetDirectoryName(typeof(TestHostApp).Assembly.Location));
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            Wake(application, token, jobPath);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Wake(UIControlledApplication application, string token, string jobPath)
        {
            JobSession.Start(application, token, jobPath, addinsFolder);
        }

        // The placer, the reader and Common load from the folders the buttons run from, so the test host runs their code and no copy of it
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            string folder;
            switch (name)
            {
                case "SiteAdaptation.Placer":
                case "SiteAdaptation.Common":
                case "Newtonsoft.Json":
                    folder = JobSession.PlacerFolderName;
                    break;
                case "RevitPrototypeReader":
                    folder = JobSession.ReaderFolderName;
                    break;
                default:
                    return null;
            }
            string path = Path.Combine(addinsFolder, folder, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }
    }
}
