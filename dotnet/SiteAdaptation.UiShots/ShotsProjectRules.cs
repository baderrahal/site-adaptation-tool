using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.RulesWindow;
using SiteAdaptation.Common;

namespace SiteAdaptation.UiShots
{
    // R682. The rules table of a project over a made up template of 4 rules in a folder of the temp folder, set up and one rule confirmed by the
    // commands of Common as the window runs them, no real value. A draft for Bader
    public static partial class Shots
    {
        private const string SampleTemplate = "{\"template\":\"manual\",\"manual\":\"a made up manual\",\"rules\":["
            + "{\"id\":\"A1\",\"name\":\"Made up slope\",\"value\":\"0.5% to 2%\",\"source\":\"rule\"},"
            + "{\"id\":\"A2\",\"name\":\"Made up wall top\",\"value\":\"Level + 1.70 m\",\"source\":\"rule\"},"
            + "{\"id\":\"A3\",\"name\":\"Made up step count\",\"value\":3,\"source\":\"rule\"},"
            + "{\"id\":\"A4\",\"name\":\"Made up setback\",\"value\":null,\"source\":\"missing\"}]}";

        // Laid out over a project set up in temp, so a test reads its parts. The folder goes once the window is laid out
        public static ProjectRulesWindow LaidOutProjectRules()
        {
            string root = Path.Combine(Path.GetTempPath(), "SiteAdaptation.UiShots." + Guid.NewGuid().ToString("N"));
            try
            {
                string project = Path.Combine(root, "work", Common.Settings.ShippedSet.ProjectsFolder, "Sample");
                string templates = Path.Combine(root, "templates");
                Directory.CreateDirectory(Path.Combine(project, "config"));
                Directory.CreateDirectory(Path.Combine(project, "rules"));
                Directory.CreateDirectory(templates);
                File.WriteAllText(Path.Combine(templates, ProjectRulesTable.ManualTemplate), SampleTemplate);
                string config = Path.Combine(project, "config", "project.json");
                File.WriteAllText(config, new JObject
                {
                    ["inputRoot"] = Path.Combine(root, "input"),
                    ["siteBuild"] = new JObject { ["minFreeGB"] = 0.1 },
                    ["addin"] = new JObject { ["workerTimeLimitMinS"] = 0.5, ["workerPeakMiB"] = new JObject { ["roles"] = new JArray("curbs"), ["files"] = new JArray(new JObject { ["name"] = "a.dwg", ["bytes"] = 1, ["peakMiB"] = 100 }) } },
                    ["designBasis"] = new JObject { ["mode"] = "new", ["modes"] = new JArray("old", "new") },
                    ["projectRules"] = new JObject { ["manualName"] = "a made up manual", ["pastJobMode"] = "old", ["ruleIds"] = new JArray("A1", "A2", "A3", "A4") },
                }.ToString());
                string work = Path.Combine(root, "work", "model");
                var place = new ProjectRulesPlace
                {
                    ConfigFile = config, RulesProfile = Path.Combine(project, "rules", "profile.json"), WorkRoot = Path.Combine(root, "work"), WorkFolder = work,
                    TemplateFolder = templates, Who = "Sample person", Document = new WorkerDocument { Path = Path.Combine(work, "Sample.rvt"), SessionId = "shot" },
                };
                var model = new ProjectRulesViewModel(place);
                model.UseManualCommand.Execute(null);
                // A shot of a refused setup would show a fault as the window, so it stops with the reason
                if (!model.HasRules)
                {
                    throw new InvalidOperationException(model.Line);
                }
                model.Save(model.Rows.First(row => row.Id == "A1"));
                var window = new ProjectRulesWindow(model);
                LayOut(window);
                return window;
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static BitmapSource ProjectRulesPage()
        {
            ProjectRulesWindow window = LaidOutProjectRules();
            return Draw(Body(window));
        }
    }
}
