using System;
using System.IO;
using System.Reflection;

namespace SiteAdaptation.AddIn
{
    // R370, docs/design/08-install.md. Revit loads one copy of an assembly for every add-in, so the add-in serves a file of its own
    // folder only to the assemblies of the tool, never to another add-in, and never an older version than the one asked for
    public sealed class OwnFolderResolver
    {
        private readonly string folder;

        private OwnFolderResolver(string folder)
        {
            this.folder = folder;
        }

        public static void Register(string folder)
        {
            AppDomain.CurrentDomain.AssemblyResolve += new OwnFolderResolver(folder).Resolve;
        }

        public static bool IsToolAssembly(string name)
        {
            return name != null
                && (name.StartsWith("SiteAdaptation.", StringComparison.Ordinal) || name == "RevitPrototypeReader" || name == "CadReader");
        }

        // The path of the file to serve, or null. exists and versionOf stand for the file system, so a test needs no folder
        public static string PathToServe(string folder, AssemblyName requested, string requester, Func<string, bool> exists, Func<string, Version> versionOf)
        {
            if (requester != null && !IsToolAssembly(requester))
            {
                return null;
            }
            string name = requested.Name;
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return null;
            }
            string path = Path.Combine(folder, name + ".dll");
            if (!exists(path))
            {
                return null;
            }
            if (requested.Version != null && versionOf(path) < requested.Version)
            {
                return null;
            }
            return path;
        }

        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string requester = args.RequestingAssembly == null ? null : args.RequestingAssembly.GetName().Name;
            string path = PathToServe(folder, new AssemblyName(args.Name), requester, File.Exists, VersionOf);
            return path == null ? null : Assembly.LoadFrom(path);
        }

        // A file that is no assembly gets no version, and a null version is below every version asked for
        private static Version VersionOf(string file)
        {
            try
            {
                return AssemblyName.GetAssemblyName(file).Version;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
