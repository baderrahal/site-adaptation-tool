using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R600 to R605. The marks and plain lines of the data check and the three files it writes. A mark never rises as the data gets worse, so
    // each need takes the worst of the marks its facts give
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

        private static readonly string[] Order = { "missing", "weak", "usable", "strong" };

        // The worse of two marks
        private static string Worst(string one, string other)
        {
            return Array.IndexOf(Order, one) <= Array.IndexOf(Order, other) ? one : other;
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
                if (outside < settings.RoadOutsideShare * segments.Count)
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

        // The ties the scan named and could not break, in words, for the lines and for the ties field
        private static List<string> TiesOf(Findings f)
        {
            var found = new List<string>(f.Input.Ties);
            foreach (JObject tie in f.Checks.Where(item => Str(item["what"]) == "tie"))
            {
                string text = Str(tie["source"]) + ": " + Str(tie["value"]);
                if (!found.Contains(text))
                {
                    found.Add(text);
                }
            }
            return found;
        }

        private static DataCheckOutput Build(Findings f)
        {
            DataCheckSettings settings = f.Settings;
            DataCheckInput input = f.Input;
            List<Plot> plots = f.Plots;
            int total = plots.Count;
            int unreadDrawings = input.Notes.Count(note => note.Kind == "drawing" && !note.Read);
            string unread = unreadDrawings == 0 ? "" : " " + Count(unreadDrawings, "drawing could not be read.", "drawings could not be read.");
            bool tied = f.Checks.Any(item => Str(item["what"]) == "tie" && Str(item["source"]) == "plotOutline" || Str(item["what"]) == "tie" && Str(item["source"]) == "plotDrawing");
            var needs = new List<JObject>();

            // Plot outlines. A face with no number, with two numbers or outside is said, so the share can never hide a plot left out
            if (input.CadReads.Count == 0)
            {
                needs.Add(Need("plotOutlines", "missing", "No drawing was read from the folder, so there are no plot outlines." + unread));
            }
            else if (f.Outline == null)
            {
                needs.Add(Need("plotOutlines", "missing", tied
                    ? "Candidates for the plot outlines tie and none is taken, they are named under ties." + unread
                    : "No closed outline in the drawings holds a plot number." + unread));
            }
            else
            {
                int outside = ChecksOf(f.Checks, "labelOutside");
                int faces = Math.Max(f.OutlineFaces, f.LabelledFaces);
                int valid = total - f.NumberFlags;
                int noNumber = Math.Max(0, faces - valid);
                double share = (double)Math.Min(valid, faces) / (faces + outside);
                string said = Count(faces, "outline", "outlines") + " found on " + Quote(LayerOf(Str(f.Outline["source"]))) + ", " + valid + " with a number of their own";
                if (noNumber > 0)
                {
                    said += ", " + noNumber + " without one";
                }
                if (outside > 0)
                {
                    said += ", " + Count(outside, "number", "numbers") + " outside every outline";
                }
                string mark = Mark(share, settings);
                if (f.Input.Ties.Count > 0)
                {
                    said += ". A tie of candidates was broken";
                }
                // R730. The plots no villa goes on leave the plot table and are listed apart under otherPlots with their reason
                int other = plots.Any(VillaGoesOn) ? plots.Count(plot => !VillaGoesOn(plot)) : 0;
                if (other > 0)
                {
                    said += ". " + Count(other, "plot", "plots") + " with no villa sign listed apart with the reason";
                }
                needs.Add(Need("plotOutlines", mark, said + "." + unread));
            }

            // Plot numbers. Distinct numbers over outlines that hold one, so a share can never pass 100 %
            JObject numberRow = f.NumberRow;
            if (total == 0 || numberRow == null)
            {
                bool numberTie = f.Checks.Any(item => Str(item["what"]) == "tie" && Str(item["source"]) == "plotNumber");
                needs.Add(Need("plotNumbers", "missing", numberTie ? "Text layers tie for the plot numbers and none is taken, they are named under ties." : "No text inside the plot outlines gives a plot number."));
            }
            else
            {
                int labelled = Math.Max((int)numberRow["score"], total);
                double share = Math.Min(1.0, (double)total / labelled);
                string mark = Mark(share, settings);
                string said = "Plot numbers are text on " + Quote(LayerOf(Str(numberRow["source"]))) + ", " + labelled + " outlines hold one and " + total + " numbers are distinct";
                if (f.NumbersTwice > 0)
                {
                    said += ", " + Count(f.NumbersTwice, "number appears", "numbers appear") + " more than once";
                    mark = Worst(mark, "weak");
                }
                if (f.NumberFlags > 0)
                {
                    said += ", " + Count(f.NumberFlags, "label holds", "labels hold") + " more than one number";
                    mark = Worst(mark, "usable");
                }
                needs.Add(Need("plotNumbers", mark, said + "."));
            }

            // Group levels. An assumed unit or a unit the table lacks lowers the mark, and a level with no word says why
            int levels = settings.GroupGapsM.Count;
            if (total == 0)
            {
                needs.Add(Need("groupLevels", "missing", "No plots were read, so no groups of plots can be found."));
            }
            else if (f.GroupWhy != null)
            {
                needs.Add(Need("groupLevels", "weak", "No groups were found by distance: " + f.GroupWhy));
            }
            else
            {
                int named = f.Words.Count(word => word != null);
                List<int> counts = Enumerable.Range(0, levels).Select(level => plots.Select(plot => plot.Group[level]).Distinct().Count()).ToList();
                string smaller = string.Join(" and ", counts.Skip(1).Select(count => count.ToString(CultureInfo.InvariantCulture)));
                bool areaUsed = input.Confirmed?.AreaColumn != null && f.AreaGone == null;
                int columnAreas = plots.Where(plot => plot.AreaName != null).Select(plot => plot.AreaName).Distinct(StringComparer.Ordinal).Count();
                // R635. Every later button works per area, so where the areas came from leads the line and a person is told to pick the column
                string areaLine = areaUsed
                    ? "Areas from the confirmed column: " + columnAreas + "."
                    : f.AreaGone != null
                        ? "The confirmed area column was not used, " + f.AreaGone + ". Areas by distance only: " + counts[0] + "."
                        : "Areas found by distance only: " + counts[0] + ", pick the area column.";
                string wordText = named == 0 ? "no word for the levels, name each one once" : "words " + string.Join(", ", f.Words.Where(word => word != null).Select(Plain));
                string notes = string.Join(" ", Enumerable.Range(0, levels).Where(level => f.WordNotes[level] != null).Select(level => "Level " + (level + 1) + " " + f.WordNotes[level] + "."));
                string mark = named == levels ? "strong" : named > 0 ? "usable" : "weak";
                string assumed = "";
                if (f.UnitAssumed)
                {
                    mark = Worst(mark, "weak");
                    assumed = "The drawing states no unit and metres are assumed. ";
                }
                if (f.AreaGone != null)
                {
                    mark = Worst(mark, "weak");
                }
                needs.Add(Need("groupLevels", mark, assumed + areaLine + (smaller.Length > 0 ? " Smaller groups: " + smaller + "," : "") + " " + wordText + ". " + notes));
            }

            // Villa type. The worst of the coverage, the disagreement between kinds of source and the join of a sheet
            int typed = plots.Count(plot => plot.Villa != null);
            List<string> both = BothOldAndNew(input.Confirmed != null && input.Confirmed.CodeMap.Count > 0 ? input.Confirmed.CodeMap : f.Tables != null && f.Tables.Count > 0 ? f.Tables[0].Pairs : new List<KeyValuePair<string, string>>());
            string codeNote = both.Count > 0 ? " Old and new code: " + string.Join(", ", both.Select(Plain)) + "."
                : input.Confirmed != null && input.Confirmed.CodeMap.Count > 0 ? " Code map confirmed."
                : f.Tables != null && f.Tables.Count > 0 ? " Code table proposed."
                : f.TablesTooLarge != null && f.TablesTooLarge.Count > 0 ? " Code table too large."
                : " No code table found.";
            string columnGone = f.ColumnGone == null ? "" : "The confirmed column was not used, " + f.ColumnGone + ". ";
            if (total == 0)
            {
                needs.Add(Need("villaType", "missing", "No plots were read, so there is no villa type to give them."));
            }
            else if (typed == 0)
            {
                string why = plots[0].VillaWhy ?? "No block, text or sheet column gives a villa type.";
                needs.Add(Need("villaType", "missing", columnGone + (f.SheetPoor != null ? "Villa type is missing, " + f.SheetPoor + "." : why) + codeNote));
            }
            else
            {
                int disputed = plots.Count(plot => plot.VillaDispute != null);
                int doubted = plots.Count(plot => plot.MapDoubt != null);
                int noFile = input.Confirmed == null ? 0 : plots.Count(plot => plot.Villa != null && !f.Prototypes.Contains(plot.Villa));
                string mark = Mark((double)typed / total, settings);
                if (disputed > (1 - settings.StrongShare) * typed)
                {
                    mark = Worst(mark, "weak");
                }
                string said = "Villa type is read from " + (f.ColumnApplied ? "the confirmed column" : f.Lead) + " for " + typed + " of " + total + " plots";
                if (disputed > 0)
                {
                    said += ", another kind of source gives another type on " + disputed;
                }
                // A person confirmed the column, so a sheet the drawing joins poorly no longer lowers what it gives
                if (f.SheetPoor != null && !f.ColumnApplied)
                {
                    mark = Worst(mark, "weak");
                    said += ", " + f.SheetPoor;
                }
                // R631. A person settled the column and that lifts the mark, the count of what the drawing still says stays in the line
                int disagree = plots.Count(plot => plot.ColumnDisagrees != null);
                if (disagree > 0)
                {
                    said += ", the drawing gives another code on " + disagree;
                }
                if (doubted > 0 || both.Count > 0)
                {
                    mark = Worst(mark, "weak");
                    said += ", " + doubted + " the code map leaves as read";
                }
                // R632. A plot with no prototype file can link nothing, however it was confirmed, as a zone column taken for a villa type
                if (noFile > 0)
                {
                    mark = Worst(mark, "weak");
                    said += ", " + noFile + " with no prototype file";
                }
                if (f.ColumnGone != null)
                {
                    mark = Worst(mark, "weak");
                }
                needs.Add(Need("villaType", mark, columnGone + said + "." + codeNote));
            }

            // Villa blocks
            if (total == 0 || f.BlockSources.Count == 0)
            {
                needs.Add(Need("villaBlocks", "missing", "No block is inserted inside the plot outlines."));
            }
            else
            {
                int holding = plots.Count(plot => plot.HasBlock);
                string where = f.BlockSources.Count == 1 ? Quote(LayerOf(f.BlockSources.First())) : Count(f.BlockSources.Count, "layer", "layers");
                needs.Add(Need("villaBlocks", Mark((double)holding / total, settings), "Villa blocks are inserts on " + where + ", " + holding + " of " + total + " plots hold one."));
            }

            // Road edges. A layer that the plots all lie near proves no road, so the mark is at most usable until the lines are scored
            string roadLayer;
            double roadShare;
            RoadEdges(settings, f.Drawing, f.Outline, plots, f.MetersPerUnit ?? 1.0, out roadLayer, out roadShare);
            if (roadLayer == null)
            {
                needs.Add(Need("roadEdges", "missing", total == 0 ? "No plots were read, so no road edges can be found beside them." : "No layer of lines beside the plots marks a road edge."));
            }
            else
            {
                needs.Add(Need("roadEdges", Worst(Mark(roadShare, settings), "usable"),
                    "Road edges are lines on " + Quote(roadLayer) + ", within " + DataCheckSettings.Text(settings.RoadReachM) + " m of " + Math.Round(roadShare * total) + " of " + total + " plots, not scored."));
            }

            // Road levels
            needs.Add(input.LandXmlFiles > 0
                ? Need("roadLevels", "usable", Count(input.LandXmlFiles, "surface file", "surface files") + " of Land XML found, how far they reach over the plots is checked when the data is read.")
                : Need("roadLevels", "missing", "No Land XML surface file is in the folder, so there are no road levels."));

            // Coordinates
            string units = f.Drawing == null ? "" : Str(f.Drawing["units"]);
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
            needs.Add(SharedParametersNeed(input));

            // The mirror is a value of each plot, not a need. It is the sign of the scale of the villa block, which the drawing states. What the
            // sign means for the link rests on the hand of the prototype, which only a person can confirm, so the value is weak until then
            int hands = plots.Count(plot => plot.Mirror != null);
            int handed = plots.Count(plot => plot.HandKnown && !plot.BlockOfOtherType);
            var values = new JArray
            {
                hands == 0
                    ? Need("mirror", "missing", total == 0 ? "No plots were read, so there is no mirror to give them." : "No villa block inside a plot gives a mirror.")
                    : handed > 0
                        ? Need("mirror", Mark((double)handed / total, settings), "Mirror comes from the block scale and a confirmed hand for " + handed + " of " + total + " plots.")
                        : Need("mirror", "weak", "Mirror is the sign of the block scale for " + hands + " of " + total + " plots, the folder does not say which hand a prototype has."),
            };
            foreach (JObject row in ProposalRows(f))
            {
                values.Add(WithNameTwice(row, f));
            }

            foreach (string tie in TiesOf(f).Where(text => !input.Ties.Contains(text)))
            {
                input.Ties.Add(tie);
            }
            var scanChecks = new JArray(f.Checks.Select(item => (string)item["what"]).GroupBy(what => what).Select(group => new JObject { ["what"] = group.Key, ["count"] = group.Count() }));
            var profile = new JObject { ["levels"] = ProfileOf(f) };
            AddProposals(profile, f);
            var plotRows = new JArray();
            var otherRows = new JArray();
            // R730. The plot table holds the plots a villa goes on, every other plot is listed apart with its reason. A read that finds no
            // villa sign on any plot cannot tell them apart, so then every plot stays in the table
            bool parted = plots.Any(VillaGoesOn);
            foreach (Plot plot in plots)
            {
                if (!parted || VillaGoesOn(plot))
                {
                    plotRows.Add(PlotRow(f, plot));
                }
                else
                {
                    otherRows.Add(OtherRow(f, plot));
                }
            }
            JObject check = Wrap(new JArray(needs), input, scanChecks, values);
            check["proposalSources"] = ProposalSources(f);
            return new DataCheckOutput
            {
                Check = check,
                Profile = profile,
                Plots = new JObject { ["areaFrom"] = input.Confirmed?.AreaColumn != null && f.AreaGone == null ? AreaFromColumn : AreaFromDistance, ["plots"] = plotRows, ["otherPlots"] = otherRows },
            };
        }

        private static JArray ProfileOf(Findings f)
        {
            var rows = new JArray();
            for (int level = 0; level < f.Settings.GroupGapsM.Count; level++)
            {
                JObject row = Level(level + 1, f.Words[level], f.WordSources[level]);
                if (f.Words[level] == null && (f.WordCandidates[level].Count > 0 || f.WordNotes[level] != null))
                {
                    row["candidates"] = new JArray(f.WordCandidates[level]);
                    row["why"] = f.WordNotes[level];
                }
                rows.Add(row);
            }
            return rows;
        }

        // R730. A villa goes on a plot that has a villa type, a villa code from any source, also two that disagree, or a villa block on it
        private static bool VillaGoesOn(Plot plot)
        {
            return plot.Villa != null || plot.CodeSeen || plot.HasBlock || plot.BlockCodes.Count > 0;
        }

        // R730. A plot no villa goes on: its number, its area and why, so nothing read is dropped in silence
        private static JObject OtherRow(Findings f, Plot plot)
        {
            return new JObject
            {
                ["number"] = plot.NumberFlag != null ? null : plot.Number,
                ["areaM2"] = f.MetersPerUnit == null ? (double?)null : Rounding.Fixed(plot.Area * f.MetersPerUnit.Value * f.MetersPerUnit.Value, 3),
                ["why"] = "no villa goes on it: " + (plot.VillaWhy ?? "no villa type, villa code or villa block was found in the plot"),
                ["source"] = new JObject { ["number"] = plot.NumberFlag != null ? Missing : Computed, ["why"] = Computed },
            };
        }

        private static JObject PlotRow(Findings f, Plot plot)
        {
            int levels = f.Settings.GroupGapsM.Count;
            var gone = new JObject();
            var disputes = new JObject();
            var weak = new JObject();
            if (plot.Villa == null)
            {
                gone["villaType"] = plot.VillaWhy;
            }
            if (plot.Mirror == null)
            {
                gone["mirror"] = plot.MirrorWhy;
            }
            else if (!plot.HandKnown)
            {
                weak["mirror"] = "the sign of the block scale, which hand a prototype file has is not known";
            }
            else if (plot.BlockOfOtherType)
            {
                weak["mirror"] = "the block in this plot is not of the plot's own type, so its scale says little about the prototype";
            }
            if (plot.Mirror != null && plot.BlockSpan > 1)
            {
                // R731. The block stands on several plots and the hand of each is taken to turn at each house along it
                string row = "one of the " + plot.BlockSpan + " houses of one block, the hand taken to turn at each house along the block";
                weak["mirror"] = weak["mirror"] == null ? row : (string)weak["mirror"] + ", " + row;
            }
            var typeDoubts = new List<string>();
            if (plot.MapDoubt != null)
            {
                typeDoubts.Add(plot.MapDoubt);
            }
            if (plot.ListedTwoWays != null)
            {
                typeDoubts.Add(plot.ListedTwoWays);
            }
            if (f.Input.Confirmed != null && plot.Villa != null && !f.Prototypes.Contains(plot.Villa))
            {
                typeDoubts.Add("no prototype file has the code " + plot.Villa);
            }
            if (typeDoubts.Count > 0)
            {
                weak["villaType"] = string.Join(", ", typeDoubts);
            }
            if (plot.ColumnDisagrees != null)
            {
                disputes["villaType"] = plot.ColumnDisagrees;
            }
            if (plot.AreaWhy != null)
            {
                gone["area"] = plot.AreaWhy;
            }
            if (plot.VillaDispute != null)
            {
                disputes["villaType"] = plot.VillaDispute;
            }
            if (plot.NumberFlag != null)
            {
                gone["number"] = plot.NumberFlag;
            }
            if (plot.Hole)
            {
                weak["outline"] = HoleWhy;
            }
            if (plot.Group == null)
            {
                gone["groups"] = f.GroupWhy ?? "no groups were found";
            }
            else if (f.UnitAssumed)
            {
                weak["groups"] = "the drawing states no unit and metres are assumed";
            }
            return new JObject
            {
                ["number"] = plot.NumberFlag != null ? null : plot.Number,
                ["villaType"] = plot.Villa,
                ["codeRead"] = plot.CodeRead != null && plot.CodeRead != plot.Villa ? plot.CodeRead : null,
                ["mirror"] = plot.Mirror,
                ["area"] = plot.AreaName,
                ["areaM2"] = f.MetersPerUnit == null ? (double?)null : Rounding.Fixed(plot.Area * f.MetersPerUnit.Value * f.MetersPerUnit.Value, 3),
                ["groups"] = plot.Group == null ? new JArray()
                    : new JArray(Enumerable.Range(0, levels).Select(level => (object)(f.GroupNames[level].TryGetValue(plot.Group[level], out string name) ? name : "L" + (level + 1) + "-" + (plot.Group[level] + 1))).ToArray()),
                ["source"] = new JObject
                {
                    ["number"] = plot.NumberFlag != null ? Missing : Computed, ["villaType"] = plot.Villa == null ? Missing : Computed, ["mirror"] = plot.Mirror == null ? Missing : Computed,
                    ["groups"] = plot.Group == null ? Missing : Computed, ["area"] = plot.AreaName == null ? Missing : ConfirmedProfile.PersonEntered,
                },
                ["missing"] = gone,
                ["disputed"] = disputes,
                ["weak"] = weak,
            };
        }
    }
}
