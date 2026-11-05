using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R635. The column of a handed over sheet that parts the plots into areas, the key every later button works by. It is found as the typology
    // column is, by what it holds and never by a name, and a proposal changes no plot. With none confirmed the groups by distance stand and the
    // data check says so
    public static partial class DataCheckLogic
    {
        private sealed class AreaCandidate
        {
            public string Workbook;
            public string Sheet;
            public string Column;
            public int Plots;
            public int Values;
            public double Agreement;
            public int Order;
            public Dictionary<Plot, string> Cells;
        }

        // The plots nearest to each plot by the centre of its outline, at most nearPlots and a quarter of the plots, so a small project is not
        // measured against itself
        private static Dictionary<Plot, List<Plot>> NearestPlots(List<Plot> plots, int nearPlots)
        {
            int take = Math.Min(nearPlots, Math.Max(1, plots.Count / 4));
            var centres = plots.Select(plot => new[] { (plot.Box[0] + plot.Box[2]) / 2, (plot.Box[1] + plot.Box[3]) / 2 }).ToList();
            var nearest = new Dictionary<Plot, List<Plot>>();
            for (int at = 0; at < plots.Count; at++)
            {
                double x = centres[at][0], y = centres[at][1];
                var order = new List<KeyValuePair<double, int>>(plots.Count);
                for (int other = 0; other < plots.Count; other++)
                {
                    if (other != at)
                    {
                        double dx = centres[other][0] - x, dy = centres[other][1] - y;
                        order.Add(new KeyValuePair<double, int>(dx * dx + dy * dy, other));
                    }
                }
                nearest[plots[at]] = order.OrderBy(item => item.Key).ThenBy(item => item.Value).Take(take).Select(item => plots[item.Value]).ToList();
            }
            return nearest;
        }

        // How far a plot's value agrees with the values of the plots around it, beyond the chance that two plots share a value: 1 when every near
        // plot agrees, 0 when they agree no more than plots drawn at random
        private static double NearAgreement(Dictionary<Plot, string> cells, Dictionary<Plot, List<Plot>> nearest)
        {
            double total = cells.Count;
            double chance = cells.Values.GroupBy(value => value, StringComparer.Ordinal).Sum(group => (group.Count() / total) * (group.Count() / total));
            if (chance >= 1)
            {
                return 0;
            }
            int same = 0, seen = 0;
            foreach (KeyValuePair<Plot, string> cell in cells)
            {
                foreach (Plot near in nearest[cell.Key])
                {
                    if (cells.TryGetValue(near, out string value))
                    {
                        seen++;
                        same += value == cell.Value ? 1 : 0;
                    }
                }
            }
            return seen == 0 ? 0 : ((double)same / seen - chance) / (1 - chance);
        }

        // R635. A column parts the plots into areas when it gives a value to usableShare of the plots, holds two values or more and fewer than the
        // blocks of the finest level, so it is coarser than a block name, at least one value runs across blocks, and the plots around a plot
        // agree with it by areaAgreement beyond chance, as a type, a height or an aspect does not. Best first by the most areas, then the most
        // plots. Columns of one rank that part the plots otherwise are a tie the person settles
        // R639. A column that would rank but whose name two columns carry goes on nameTwice, for the line, and never on the list
        private static List<AreaCandidate> AreaColumns(DataCheckSettings settings, DataCheckInput input, List<Plot> plots, List<KeyValuePair<string, string>> nameTwice)
        {
            var found = new List<AreaCandidate>();
            if (plots.Count == 0 || plots.Any(plot => plot.Group == null))
            {
                return found;
            }
            var byNumber = new Dictionary<string, Plot>(StringComparer.Ordinal);
            foreach (Plot plot in plots.Where(plot => plot.NumberFlag == null))
            {
                byNumber[plot.Number] = plot;
            }
            int finest = settings.GroupGapsM.Count - 1;
            int blocks = plots.Select(plot => plot.Group[finest]).Distinct().Count();
            Dictionary<Plot, List<Plot>> nearest = null;
            int order = 0;
            foreach (ScanWorkbook book in input.Excels)
            {
                foreach (ScanSheet sheet in book.Sheets)
                {
                    int key = KeyColumn(settings, sheet, byNumber, plots.Count);
                    if (key < 0)
                    {
                        continue;
                    }
                    for (int column = 0; column < sheet.Headers.Count; column++)
                    {
                        order++;
                        if (column == key)
                        {
                            continue;
                        }
                        Dictionary<Plot, string> cells = ColumnCells(sheet, key, column, byNumber).Where(item => item.Value.Length > 0).ToDictionary(item => item.Key, item => item.Value);
                        List<IGrouping<string, KeyValuePair<Plot, string>>> values = cells.GroupBy(item => item.Value, StringComparer.Ordinal).ToList();
                        if (cells.Count < settings.UsableShare * plots.Count || values.Count < 2 || values.Count >= blocks
                            || !values.Any(value => value.Select(item => item.Key.Group[finest]).Distinct().Count() >= 2))
                        {
                            continue;
                        }
                        nearest = nearest ?? NearestPlots(plots, settings.AreaNearPlots);
                        double agreement = NearAgreement(cells, nearest);
                        if (agreement >= settings.AreaAgreement && NameTwice(sheet, column))
                        {
                            AddNameTwice(nameTwice, sheet, column);
                        }
                        else if (agreement >= settings.AreaAgreement)
                        {
                            found.Add(new AreaCandidate { Workbook = book.Name, Sheet = sheet.Name, Column = sheet.Headers[column], Plots = cells.Count, Values = values.Count, Agreement = agreement, Order = order, Cells = cells });
                        }
                    }
                }
            }
            return found.OrderByDescending(item => item.Values).ThenByDescending(item => item.Plots).ThenBy(item => item.Order).ToList();
        }

        // R635. The first area column, unless another of the same rank parts some plot otherwise, then the person picks. Columns of one rank that
        // hold the same value on every plot are one answer under two names, the first in reading order stands for them
        private static AreaCandidate AreaProposal(List<AreaCandidate> areas, out string tie)
        {
            tie = null;
            if (areas.Count == 0)
            {
                return null;
            }
            AreaCandidate first = areas[0];
            List<AreaCandidate> level = areas.Where(item => item.Values == first.Values && item.Plots == first.Plots).ToList();
            if (level.Count == 1)
            {
                return first;
            }
            if (level.Any(item => !SameCells(item.Cells, first.Cells)))
            {
                tie = Count(level.Count, "column", "columns") + " tie as area columns and part some plots otherwise, none is proposed, pick one";
                return null;
            }
            return first;
        }

        // R635. A confirmed area column gives each plot its area, a plot it does not list has none and says why. Gives the reason when it cannot be
        // used, then no plot changes
        private static string ApplyArea(DataCheckSettings settings, DataCheckInput input, List<Plot> plots, ConfirmedColumn column)
        {
            ColumnFound found = FindColumn(settings, input, plots, column);
            if (found.Why != null)
            {
                return found.Why;
            }
            if (found.Cells.Values.Where(cell => cell.Length > 0).Distinct(StringComparer.Ordinal).Count() < 2)
            {
                return "it holds one value on every row, so it parts nothing";
            }
            foreach (Plot plot in plots.Where(plot => plot.NumberFlag == null))
            {
                if (found.Cells.TryGetValue(plot, out string cell) && cell.Length > 0)
                {
                    plot.AreaName = cell;
                }
                else
                {
                    plot.AreaWhy = "the confirmed area column has no value for this plot";
                }
            }
            return null;
        }

        private static void AddNameTwice(List<KeyValuePair<string, string>> nameTwice, ScanSheet sheet, int column)
        {
            var named = new KeyValuePair<string, string>(sheet.Name, sheet.Headers[column]);
            if (!nameTwice.Contains(named))
            {
                nameTwice.Add(named);
            }
        }

        // R639. A column passed over for its name, said on the line of the need it could have filled, on every branch of that line. First, so the
        // cut of Line drops the tail of the old words and never this
        private static JObject WithNameTwice(JObject row, Findings f)
        {
            string need = Str(row["need"]);
            List<KeyValuePair<string, string>> twice = need == "areaColumn" ? f.AreaTwice : need == "typologyColumn" ? f.TypologyTwice : null;
            if (twice == null || twice.Count == 0)
            {
                return row;
            }
            string more = twice.Count == 1 ? "" : " and " + Count(twice.Count - 1, "more name", "more names");
            row["line"] = Line("Two columns of sheet " + twice[0].Key + " carry the name " + twice[0].Value + more + ", so neither was taken. " + Str(row["line"]));
            return row;
        }

        private static JObject AreaRow(AreaCandidate item)
        {
            return new JObject { ["workbook"] = item.Workbook, ["sheet"] = item.Sheet, ["column"] = item.Column, ["plots"] = item.Plots, ["areas"] = item.Values, ["source"] = Computed };
        }

        private static JObject AreaBlock(Findings f)
        {
            List<AreaCandidate> areas = f.Areas ?? new List<AreaCandidate>();
            ConfirmedColumn column = f.Input.Confirmed?.AreaColumn;
            return new JObject
            {
                ["proposed"] = f.AreaProposed == null ? null : AreaRow(f.AreaProposed),
                ["confirmed"] = column == null ? null : new JObject { ["workbook"] = column.Workbook, ["sheet"] = column.Sheet, ["column"] = column.Column, ["source"] = ConfirmedProfile.PersonEntered, ["found"] = f.AreaGone == null, ["why"] = f.AreaGone },
                ["source"] = column != null ? ConfirmedProfile.PersonEntered : f.AreaProposed != null ? Computed : Missing,
                ["tie"] = f.AreaTie,
                ["candidates"] = new JArray(areas.Select(AreaRow)),
            };
        }
    }
}
