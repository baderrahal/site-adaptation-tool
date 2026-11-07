using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.Common.ReadData
{
    // R740. The input rows of the old read found in the project folder by the names config gives, so the Build reads what the data check read and
    // no person browses a row. A file is taken by its name only, the folder of road surfaces holds the most surfaces named, the folder of prototypes
    // the most prototype files. A name two files carry is left out with its reason, as the tool cannot tell which one is meant
    public static class FolderRoles
    {
        public sealed class Found
        {
            public Dictionary<string, string> Paths { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
            public List<string> Left { get; } = new List<string>();
        }

        public static Found Of(JObject config, string folder, IReadOnlyList<string> picked)
        {
            var found = new Found();
            if (config == null || string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                found.Left.Add("the project folder is not on disk: " + (folder ?? "none named"));
                return found;
            }
            HashSet<string> skip;
            try
            {
                skip = DataCheckSettings.SkipFoldersOf(config);
            }
            catch (InvalidDataException refused)
            {
                found.Left.Add(refused.Message);
                return found;
            }
            List<string> files = Walk(Path.GetFullPath(folder), skip);
            JObject drawings = config["drawings"] as JObject;
            string curbDrawing = Text(config.SelectToken("dataBook.join.curb"), "drawing");
            Take(found, files, InputNames.Baseplan, Text(drawings, "baseplan"), "drawings.baseplan");
            Take(found, files, InputNames.VillaBlocks, Text(drawings, "villaBlocks"), "drawings.villaBlocks");
            Take(found, files, InputNames.Curbs, curbDrawing == null ? null : Text(drawings, curbDrawing), "drawings of dataBook.join.curb.drawing");
            JObject workbookRoles = config.SelectToken("addin.workbookRoles") as JObject;
            JObject workbooks = config.SelectToken("dataBook.rev9.workbooks") as JObject;
            foreach (string role in new[] { InputNames.Dlus, InputNames.Sales })
            {
                string key = workbookRoles?.Properties().Where(pair => pair.Value.Type == JTokenType.String && (string)pair.Value == role).Select(pair => pair.Name).FirstOrDefault();
                Take(found, files, role, key == null ? null : Text(workbooks, key), "dataBook.rev9.workbooks by addin.workbookRoles");
            }
            // As the old read names them: the surfaces named or picked under rev6, those of siteBuild.roadSurfaces under the past job
            IEnumerable<string> named = RoadSurfaceChoices.IsRev6(config) ? RoadSurfaceChoices.Named(config, picked)
                : ((config.SelectToken("siteBuild.roadSurfaces") as JArray) ?? new JArray()).Where(name => name.Type == JTokenType.String).Select(name => (string)name);
            var surfaces = new HashSet<string>(named.Select(name => name + ".xml"), StringComparer.OrdinalIgnoreCase);
            Most(found, files, InputNames.RoadSurface, file => surfaces.Contains(Path.GetFileName(file)), "no folder holds a road surface named in config or picked");
            Most(found, files, InputNames.Prototypes, file => string.Equals(Path.GetExtension(file), ".rvt", StringComparison.OrdinalIgnoreCase), "no folder holds a prototype file");
            return found;
        }

        private static void Take(Found found, List<string> files, string role, string named, string where)
        {
            string name = string.IsNullOrWhiteSpace(named) ? null : Path.GetFileName(named.Replace('/', Path.DirectorySeparatorChar));
            if (name == null)
            {
                found.Left.Add(role + ": config names no file in " + where);
                return;
            }
            List<string> same = files.Where(file => string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (same.Count == 1)
            {
                found.Paths[role] = same[0];
            }
            else
            {
                found.Left.Add(role + ": " + (same.Count == 0 ? name + " is not in the folder" : same.Count + " files are named " + name));
            }
        }

        private static void Most(Found found, List<string> files, string role, Func<string, bool> counts, string none)
        {
            List<IGrouping<string, string>> folders = files.Where(counts).GroupBy(file => Path.GetDirectoryName(file), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).ToList();
            if (folders.Count == 0)
            {
                found.Left.Add(role + ": " + none);
                return;
            }
            if (folders.Count > 1 && folders[1].Count() == folders[0].Count())
            {
                found.Left.Add(role + ": two folders hold " + folders[0].Count() + " each, " + folders[0].Key + " and " + folders[1].Key);
                return;
            }
            found.Paths[role] = folders[0].Key;
        }

        // As the data check walks the folder, a skipped folder at any depth and a link or junction are never followed
        private static List<string> Walk(string folder, HashSet<string> skip)
        {
            var files = new List<string>();
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                string here = pending.Pop();
                files.AddRange(Directory.GetFiles(here));
                foreach (string child in Directory.GetDirectories(here).Where(child => !skip.Contains(Path.GetFileName(child)) && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0))
                {
                    pending.Push(child);
                }
            }
            files.Sort(StringComparer.Ordinal);
            return files;
        }

        private static string Text(JToken parent, string name)
        {
            JToken value = (parent as JObject)?[name];
            return value != null && value.Type == JTokenType.String && ((string)value).Length > 0 ? (string)value : null;
        }
    }
}
