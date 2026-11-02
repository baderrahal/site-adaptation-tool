using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Autodesk.Revit.UI;

namespace SiteAdaptation.TestHost
{
    // R103 and R110. Revit loads this at every start. It does nothing, not even an event subscription, unless the run script
    // started this Revit with a token and the path of its own job file, and that file is there
    public class TestHostApp : IExternalApplication
    {
        // Kept as text here, a type of SiteAdaptation.Common must not load before the resolver is in place
        private const string TokenVariable = "SITEADAPTATION_TESTHOST_TOKEN";
        private const string JobVariable = "SITEADAPT_JOB";
        private const string ListenerVariable = "SITEADAPT_LISTENER_ROOT";
        private const string ListenerSlotVariable = "SITEADAPT_LISTENER_SLOT";

        private static string addinsFolder;

        public Result OnStartup(UIControlledApplication application)
        {
            string token = Environment.GetEnvironmentVariable(TokenVariable);
            if (string.IsNullOrEmpty(token))
            {
                return Result.Succeeded;
            }
            string jobPath = Environment.GetEnvironmentVariable(JobVariable);
            // R448. Only a Revit the start script of the listener started has this setting, a job file names one job of its own
            string listenerRoot = Environment.GetEnvironmentVariable(ListenerVariable);
            if (string.IsNullOrEmpty(jobPath) && !string.IsNullOrEmpty(listenerRoot))
            {
                StartListener(application, token, listenerRoot);
                return Result.Succeeded;
            }
            if (string.IsNullOrEmpty(jobPath) || !File.Exists(jobPath))
            {
                return Result.Succeeded;
            }
            addinsFolder = Path.GetDirectoryName(Path.GetDirectoryName(typeof(TestHostApp).Assembly.Location));
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try
            {
                Wake(application, token, jobPath);
            }
            catch (Exception failure)
            {
                // Revit never sees a failure of the test host. Common may be what failed to load, so the line is written by hand
                try
                {
                    string line = "{\"utc\": \"" + DateTime.UtcNow.ToString("o") + "\", \"reason\": \"the test host failed to start: "
                        + failure.GetType().Name + " " + failure.Message.Replace("\\", "/").Replace("\"", "'") + "\"}";
                    File.WriteAllText(Path.ChangeExtension(jobPath, ".refused.json"), line);
                }
                catch (Exception)
                {
                    // Nothing more can be done, Revit goes on as a normal session
                }
            }
            return Result.Succeeded;
        }

        // R448. As the job above: Revit never sees a failure, and the start script reads the line it waits for
        private static void StartListener(UIControlledApplication application, string token, string listenerRoot)
        {
            addinsFolder = Path.GetDirectoryName(Path.GetDirectoryName(typeof(TestHostApp).Assembly.Location));
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try
            {
                WakeListener(application, token);
            }
            catch (Exception failure)
            {
                try
                {
                    string line = "{\"utc\": \"" + DateTime.UtcNow.ToString("o") + "\", \"reason\": \"the listener failed to start: "
                        + failure.GetType().Name + " " + failure.Message.Replace("\\", "/").Replace("\"", "'") + "\"}";
                    File.WriteAllText(Path.Combine(listenerRoot, "listener-" + Environment.GetEnvironmentVariable(ListenerSlotVariable) + ".refused.json"), line);
                }
                catch (Exception)
                {
                    // Nothing more can be done, Revit goes on as a normal session and the start script stops it at its limit
                }
            }
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WakeListener(UIControlledApplication application, string token)
        {
            RevitListener.Start(application, token);
        }

        // The placer, the reader and Common load from the folders the buttons run from, so the test host runs their code and no copy of it
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            string asking = args.RequestingAssembly == null ? null : args.RequestingAssembly.GetName().Name;
            string folder;
            switch (name)
            {
                case "SiteAdaptation.Placer":
                case "SiteAdaptation.Common":
                    folder = JobSession.PlacerFolderName;
                    break;
                case "Newtonsoft.Json":
                    // Only for the assemblies of this tool, another add-in keeps its own resolution
                    if (asking != "SiteAdaptation.Common" && asking != "SiteAdaptation.Placer" && asking != "RevitPrototypeReader" && asking != "SiteAdaptation.TestHost")
                    {
                        return null;
                    }
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
