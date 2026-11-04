using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R600 to R605. The marks and plain lines of the data check and the three files it writes
    public static partial class DataCheckLogic
    {
        private static string Plain(string word)
        {
            return string.IsNullOrWhiteSpace(word) || Regex.IsMatch(word, @"[a-z][A-Z]|[{}\[\]]") || word.Length > 30 ? "a word" : word;
        }

        private static string Count(int count, string one, string many)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? one : many);
        }

        private static int ChecksOf(List<JObject> checks, string what)
        {
            return checks.Count(item => Str(item["what"]) == what);
        }

        private static string LayerOf(string source)
        {
            int cut = source.IndexOf('|');
            return cut < 0 ? source : source.Substring(cut + 1);
        }

        // R604. The layer of lines that best marks the edges of the roads: not a layer of the plot outlines, its lines mostly outside the plots,
        // and the most plots with a line within roadReach of them. Lines are sampled along their length, so a long edge reaches the plots
        // beside it as well as the plots at its ends
        private static void RoadEdges(DataCheckSettings settings, JObject drawing, JObject outline, List<Plot> plots, double metersPerUnit, out string layer, out double share)
        {
            layer = null;
            share = 0;
            if (drawing == null || outline == null || plots.Count == 0 || !(drawing["lines"] is JObject lines))
            {
                return;
            }
            var plotLayers = new HashSet<string>(LayerOf(Str(outline["source"])).Split(new[] { ScanLogic.LayerJoin }, StringSplitOptions.None), StringComparer.Ordinal);
            double reach = settings.RoadReachM / metersPerUnit;
            var inside = new BoxGrid(plots.Select(plot => plot.Box).ToList(), 0);
            var near = new BoxGrid(plots.Select(plot => plot.Box).ToList(), reach);
            int best = 0;
            foreach (JProperty property in lines.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                var segments = property.Value as JArray;
                if (plotLayers.Contains(property.Name) || segments == null || segments.Count < settings.RoadMinSegments)
                {
                    continue;
                }
                int outside = 0;
                var reached = new HashSet<int>();
                foreach (JArray segment in segments.OfType<JArray>())
                {
                    double x1 = (double)segment[0], y1 = (double)segment[1], x2 = (double)segment[2], y2 = (double)segment[3];
                    double mx = (x1 + x2) / 2, my = (y1 + y2) / 2;
                    if (!inside.Holding(mx, my).Any(at => ScanLogic.Contains(plots[at].Ring, mx, my)))
                    {
                        outside++;
                    }
                    double length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                    int samples = (int)Math.Min(400, Math.Max(1, Math.Ceiling(length / (reach / 2))));
                    for (int step = 0; step <= samples; step++)
                    {
                        double t = (double)step / samples;
                        foreach (int at in near.Holding(x1 + t * (x2 - x1), y1 + t * (y2 - y1)))
                        {
                            reached.Add(at);
                        }
                    }
                }
                if (outside < 0.5 * segments.Count)
                {
                    continue;
                }
                if (reached.Count > best)
                {
                    best = reached.Count;
                    layer = property.Name;
                }
            }
            share = (double)best / plots.Count;
        }

        private static DataCheckOutput Build(DataCheckSettings settings, DataCheckInput input, JObject scan, List<JObject> rows, List<JObject> checks, JObject outline, List<Plot> plots, string lead,
            HashSet<string> blockSources, JObject drawing, double? metersPerUnit, string[] words, List<Dictionary<int, string>> groupNames, string groupWhy)
        {
            int total = plots.Count;
            int unreadDrawings = input.Notes.Count(note => note.Kind == "drawing" && !note.Read);
            string unread = unreadDrawings == 0 ? "" : " " + Count(unreadDrawings, "drawing could not be read.", "drawings could not be read.");
            var needs = new List<JObject>();

            // Plot outlines
            if (input.CadReads.Count == 0)
            {
                needs.Add(Need("plotOutlines", "missing", "No drawing was read from the folder, so there are no plot outlines." + unread));
            }
            else if (outline == null)
            {
                needs.Add(Need("plotOutlines", "missing", "No closed outline in the drawings holds a plot number." + unread));
            }
            else
            {
                int lost = ChecksOf(checks, "labelOutside") + ChecksOf(checks, "manyLabels") + ChecksOf(checks, "numberTwice");
                double share = (double)total / (total + lost);
                string tail = lost == 0 ? "each holds one number." : Count(lost, "number is outside an outline or shared.", "numbers are outside an outline or shared.");
                needs.Add(Need("plotOutlines", Mark(share, settings), Count(total, "plot outline", "plot outlines") + " found on " + Quote(LayerOf(Str(outline["source"]))) + ", " + tail + unread));
            }

            // Plot numbers
            JObject numberRow = rows.FirstOrDefault(row => Str(row["kind"]) == "plotNumber" && Str(row["proposedUse"]) == "plot numbers");
            if (total == 0 || numberRow == null)
            {
                needs.Add(Need("plotNumbers", "missing", "No text inside the plot outlines gives a plot number."));
            }
            else
            {
                double share = (double)(int)numberRow["score"] / total;
                needs.Add(Need("plotNumbers", Mark(share, settings), "Plot numbers are text on " + Quote(LayerOf(Str(numberRow["source"]))) + ", " + (int)numberRow["score"] + " of " + total + " plots hold one."));
            }

            // Group levels
            int levels = settings.GroupGapsM.Count;
            if (total == 0)
            {
                needs.Add(Need("groupLevels", "missing", "No plots were read, so no groups of plots can be found."));
            }
            else if (groupWhy != null)
            {
                needs.Add(Need("groupLevels", "weak", "Groups could not be found by distance: " + groupWhy));
            }
            else
            {
                int named = words.Count(word => word != null);
                string sizes = string.Join(", ", Enumerable.Range(0, levels).Select(level => "level " + (level + 1) + " has " + Count(plots.Select(plot => plot.Group[level]).Distinct().Count(), "group", "groups")));
                string wordText = named == 0 ? "No word for them in the drawings or sheets." : "Words: " + string.Join(", ", words.Where(word => word != null).Select(Plain)) + ".";
                needs.Add(Need("groupLevels", named == levels ? "strong" : named > 0 ? "usable" : "weak", "By distance: " + sizes + ". " + wordText));
            }

            // Villa type
            int typed = plots.Count(plot => plot.Villa != null);
            if (total == 0)
            {
                needs.Add(Need("villaType", "missing", "No plots were read, so there is no villa type to give them."));
            }
            else if (typed == 0)
            {
                needs.Add(Need("villaType", "missing", plots[0].VillaWhy ?? "No block, text or sheet column gives a villa type."));
            }
            else
            {
                // R602. A type the sources do not agree on is no strong read: past (1 - strongShare) of the typed plots in dispute the need is weak
                int disputed = plots.Count(plot => plot.VillaDispute != null);
                string mark = Mark((double)typed / total, settings);
                if (disputed > (1 - settings.StrongShare) * typed && mark != "weak")
                {
                    mark = "weak";
                }
                string said = disputed == 0 ? "." : ", another kind of source gives another type on " + disputed + " of them.";
                needs.Add(Need("villaType", mark, "Villa type is read from " + lead + " for " + typed + " of " + total + " plots" + said));
            }

            // Villa blocks
            if (total == 0 || blockSources.Count == 0)
            {
                needs.Add(Need("villaBlocks", "missing", "No block is inserted inside the plot outlines."));
            }
            else
            {
                int holding = plots.Count(plot => plot.HasBlock);
                string where = blockSources.Count == 1 ? Quote(LayerOf(blockSources.First())) : Count(blockSources.Count, "layer", "layers");
                needs.Add(Need("villaBlocks", Mark((double)holding / total, settings), "Villa blocks are inserts on " + where + ", " + holding + " of " + total + " plots hold one."));
            }

            // Road edges
            string roadLayer;
            double roadShare;
            RoadEdges(settings, drawing, outline, plots, metersPerUnit ?? 1.0, out roadLayer, out roadShare);
            if (roadLayer == null)
            {
                needs.Add(Need("roadEdges", "missing", total == 0 ? "No plots were read, so no road edges can be found beside them." : "No layer of lines beside the plots marks a road edge."));
            }
            else
            {
                needs.Add(Need("roadEdges", Mark(roadShare, settings), "Road edges are lines on " + Quote(roadLayer) + ", within " + DataCheckSettings.Text(settings.RoadReachM) + " m of " + Math.Round(roadShare * total) + " of " + total + " plots."));
            }

            // Road levels
            needs.Add(input.LandXmlFiles > 0
                ? Need("roadLevels", "usable", Count(input.LandXmlFiles, "surface file", "surface files") + " of Land XML found, how far they reach over the plots is checked when the data is read.")
                : Need("roadLevels", "missing", "No Land XML surface file is in the folder, so there are no road levels."));

            // Coordinates
            string units = drawing == null ? "" : Str(drawing["units"]);
            if (input.LandXmlWithCoordinateSystem > 0)
            {
                needs.Add(Need("coordinates", "usable", Count(input.LandXmlWithCoordinateSystem, "surface file states", "surface files state") + " a coordinate system."));
            }
            else if (total > 0 && units.Length > 0 && units != "Unitless")
            {
                needs.Add(Need("coordinates", "weak", "The drawing is in " + Plain(units).ToLowerInvariant() + " and names no coordinate system, the plots sit in drawing space."));
            }
            else
            {
                needs.Add(Need("coordinates", "missing", "No drawing or surface file names a coordinate system."));
            }

            // Prototypes
            int named2 = input.PrototypeNames.Count;
            int coded = input.PrototypeNames.Count(name => settings.PrototypeCode(name) != null);
            if (named2 == 0)
            {
                needs.Add(Need("prototypes", "missing", "No Revit prototype file is in the folder."));
            }
            else
            {
                needs.Add(Need("prototypes", coded == named2 ? "strong" : coded > 0 ? "usable" : "weak",
                    Count(named2, "prototype file", "prototype files") + " found, " + coded + " with a villa type code in the file name. The files are opened at Read data."));
            }

            // Rules and shared parameters
            needs.Add(input.RuleFiles > 0
                ? Need("rules", "usable", Count(input.RuleFiles, "rule set file", "rule set files") + " found in the folder.")
                : Need("rules", "missing", "No rule set file is in the folder, the rules are chosen in the add-in."));
            needs.Add(input.SharedParameterFiles > 0
                ? Need("sharedParameters", "usable", Count(input.SharedParameterFiles, "shared parameter file", "shared parameter files") + " found in the folder.")
                : Need("sharedParameters", "missing", "No shared parameter file is in the folder."));

            // R602. The mirror is a value of each plot, not a need. It is the sign of the scale of the villa block, which the drawing states, and
            // the folder does not say which hand a prototype file has, so what the sign means for the link is not known and the value is weak
            int hands = plots.Count(plot => plot.Mirror != null);
            var values = new JArray
            {
                hands == 0
                    ? Need("mirror", "missing", total == 0 ? "No plots were read, so there is no mirror to give them." : "No villa block inside a plot gives a mirror.")
                    : Need("mirror", "weak", "Mirror is the sign of the block scale for " + hands + " of " + total + " plots, the folder does not say which hand a prototype has."),
            };
            var scanChecks = new JArray(checks.Select(item => (JToken)new JObject { ["what"] = item["what"], ["count"] = 1 })
                .GroupBy(item => (string)item["what"]).Select(group => new JObject { ["what"] = group.Key, ["count"] = group.Count() }));
            var profile = new JArray(Enumerable.Range(0, levels).Select(level => Level(level + 1, words[level], words[level] == null ? Missing : Computed)));
            var plotRows = new JArray();
            foreach (Plot plot in plots)
            {
                var gone = new JObject();
                if (plot.Villa == null)
                {
                    gone["villaType"] = plot.VillaWhy;
                }
                if (plot.Mirror == null)
                {
                    gone["mirror"] = plot.MirrorWhy;
                }
                var disputes = new JObject();
                if (plot.VillaDispute != null)
                {
                    disputes["villaType"] = plot.VillaDispute;
                }
                plotRows.Add(new JObject
                {
                    ["number"] = plot.Number,
                    ["villaType"] = plot.Villa,
                    ["mirror"] = plot.Mirror,
                    ["groups"] = new JArray(Enumerable.Range(0, levels).Select(level => (object)(groupNames[level].TryGetValue(plot.Group[level], out string name) ? name
                        : "L" + (level + 1) + "-" + (plot.Group[level] + 1))).ToArray()),
                    ["source"] = new JObject
                    {
                        ["number"] = Computed, ["villaType"] = plot.Villa == null ? Missing : Computed, ["mirror"] = plot.Mirror == null ? Missing : Computed, ["groups"] = Computed,
                    },
                    ["missing"] = gone,
                    ["disputed"] = disputes,
                });
            }
            return new DataCheckOutput
            {
                Check = Wrap(new JArray(needs), input, scanChecks, values),
                Profile = new JObject { ["levels"] = profile },
                Plots = new JObject { ["plots"] = plotRows },
            };
        }
    }
}
