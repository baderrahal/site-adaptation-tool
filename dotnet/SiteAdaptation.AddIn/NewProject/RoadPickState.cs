using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.NewProject
{
    public enum RoadPickWhy
    {
        Ready,
        PastJob,
        NoSetup,
    }

    // R492. What the button Pick surfaces of New project and of the pane needs from the settings: whether a build reads the pick
    // (the rev6 design basis only, else the saved project takes its surfaces from its past job), the road surface folder and the
    // effective order, the pick else the list of the saved project. No Revit type, so a test drives it with a text reader
    public sealed class RoadPickState
    {
        private readonly JObject config;

        private RoadPickState(RoadPickWhy why, string folder, IReadOnlyList<string> effective, JObject config = null)
        {
            this.config = config;
            Why = why;
            Folder = folder;
            Effective = effective;
        }

        public RoadPickWhy Why { get; }
        public string Folder { get; }
        public IReadOnlyList<string> Effective { get; }
        public bool Enabled { get { return Why == RoadPickWhy.Ready; } }

        public static RoadPickState Of(ProjectSettings settings, Func<string, string> readText)
        {
            string text = settings == null || string.IsNullOrEmpty(settings.ConfigFile) ? null : readText(settings.ConfigFile);
            JObject config = null;
            if (text != null)
            {
                try
                {
                    // R484. The mode of the settings and through them of the draft, the window shows any config and refuses nothing
                    config = ProjectModes.Apply(JObject.Parse(text), settings.ProjectMode);
                }
                catch (Exception failure) when (failure is JsonException || failure is InvalidDataException)
                {
                    config = null;
                }
            }
            if (config == null)
            {
                return new RoadPickState(RoadPickWhy.NoSetup, null, new string[0]);
            }
            settings.Inputs.TryGetValue(InputNames.RoadSurface, out SettingsInput input);
            IReadOnlyList<string> effective = RoadSurfaceChoices.Named(config, settings.RoadSurfaces).Select(Stem).ToList();
            return new RoadPickState(RoadSurfaceChoices.IsRev6(config) ? RoadPickWhy.Ready : RoadPickWhy.PastJob, input?.Path, effective, config);
        }

        // The files of the folder a road surface may be, as stems, a name siteBuild.roadFolder.notTaken refuses is not offered
        public IReadOnlyList<string> InFolder()
        {
            return string.IsNullOrEmpty(Folder) ? new string[0] : RoadSurfaceChoices.InFolder(Folder, config).Select(Stem).ToList();
        }

        // The name a build gives the surface, the part after the last double underscore of the file name
        public static string Short(string stem)
        {
            return ReadSettings.RoadName(Stem(stem));
        }

        public static string Stem(string name)
        {
            string file = name ?? string.Empty;
            return file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(file) : file;
        }
    }
}
