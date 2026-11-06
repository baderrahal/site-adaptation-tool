using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.Pane
{
    // R686, R687. The press of Build on card 2: the plan, the one ask for the manual template when no rules are chosen, the stop on a blocking line,
    // the preview when ticked. The windows are given by the Revit side, so a test runs the flow with none
    public sealed class SiteBuildFlow
    {
        // Rules are per project, so the one ask is once per config of a project in this session of Revit
        private readonly HashSet<string> askedRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The place of the plan of the active project, null with none
        public Func<SiteBuildPlace> Place { get; set; }

        // Asks once whether to use the manual template, true for yes
        public Func<string, bool> AskRules { get; set; }

        // Runs rulesSetup with the manual template, null when done, else why not
        public Func<string> UseManual { get; set; }

        // Shows the preview, true to go on
        public Func<SiteBuildResult, bool> ShowPreview { get; set; }

        // The lines card 2 shows after a press, and false when the build stops there
        public bool Run(bool preview, out string line)
        {
            SiteBuildPlace place = Place?.Invoke();
            if (place == null)
            {
                line = SiteBuildText.NoRead;
                return false;
            }
            SiteBuildResult plan = SiteBuildPlan.Make(place);
            // Point 3 of R2-C3: the first Build with no rules chosen asks once, a yes sets them up and the plan is made again on them
            if (plan.RulesChoice != null && AskRules != null && askedRules.Add(place.ConfigFile ?? string.Empty))
            {
                if (AskRules(TextTemplate.Fill(SiteBuildText.AskRules, plan.RulesChoice)))
                {
                    string refused = UseManual?.Invoke();
                    if (refused == null)
                    {
                        place = Place?.Invoke() ?? place;
                        plan = SiteBuildPlan.Make(place);
                    }
                    else
                    {
                        plan.Issues.Insert(0, refused);
                    }
                }
            }
            if (plan.StopLine != null)
            {
                line = plan.StopLine;
                return false;
            }
            if (preview && ShowPreview != null && !ShowPreview(plan))
            {
                line = string.Join(" ", new[] { plan.Progress, plan.Coordinates }.Where(part => !string.IsNullOrEmpty(part)));
                return false;
            }
            line = string.Join(" ", new[] { plan.Progress, plan.Coordinates, plan.Issues.Count > 0 ? TextTemplate.Fill(SiteBuildText.Issues, plan.Issues.Count) : null, SiteBuildText.PlanReady }
                .Where(part => !string.IsNullOrEmpty(part)));
            return true;
        }
    }

    // R687. The plot outlines of preview.json fitted into the preview, north up, made up of no Revit type
    public static class SiteBuildPreview
    {
        public const double Width = 640;
        public const double Height = 400;
        public const double Margin = 12;

        public static List<PointCollection> Outlines(string previewFile)
        {
            List<List<double[]>> rings;
            try
            {
                rings = ((JObject.Parse(WorkFiles.ReadText(previewFile))["plots"] as JArray) ?? new JArray()).OfType<JObject>()
                    .Select(plot => ((plot["outline"] as JArray) ?? new JArray()).OfType<JArray>().Where(point => point.Count >= 2).Select(point => new[] { (double)point[0], (double)point[1] }).ToList())
                    .Where(ring => ring.Count >= 3).ToList();
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is JsonException || failure is FormatException || failure is ArgumentException)
            {
                return new List<PointCollection>();
            }
            return Fit(rings, Width, Height, Margin);
        }

        // One scale for both axes, so a plot keeps its shape, and the y of the drawing turned so north is up
        public static List<PointCollection> Fit(List<List<double[]>> rings, double width, double height, double margin)
        {
            List<double[]> all = rings.SelectMany(ring => ring).ToList();
            if (all.Count == 0)
            {
                return new List<PointCollection>();
            }
            double minX = all.Min(point => point[0]), maxX = all.Max(point => point[0]);
            double minY = all.Min(point => point[1]), maxY = all.Max(point => point[1]);
            double spanX = Math.Max(maxX - minX, 1e-9), spanY = Math.Max(maxY - minY, 1e-9);
            double scale = Math.Min((width - 2 * margin) / spanX, (height - 2 * margin) / spanY);
            return rings.Select(ring => new PointCollection(ring.Select(point => new Point(margin + (point[0] - minX) * scale, height - margin - (point[1] - minY) * scale)))).ToList();
        }

        public static string Line(SiteBuildResult plan)
        {
            return TextTemplate.Fill(SiteBuildText.PreviewLine, plan.PlotsWithOutline.ToString(CultureInfo.InvariantCulture), plan.Issues.Count.ToString(CultureInfo.InvariantCulture));
        }
    }
}
