using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Update
{
    // R402. The logic of the button Update from report, D4 of steps/decisions-2026-10-28.md: the level Excel and the report Excel of one NBH
    // read back after the team edits them, refused when of another build, each change of an editable column listed against the plan of the
    // model by Plot No, checked against the level rules of the profile and the manual rules of the block manualRules, and the ticked
    // changes saved as entries of the override list. It writes nothing to the model, the buttons update the plots it lists
    public static class UpdateFromReport
    {
        // The level rule checks by name, the names an entry of manualRules lists in updateChecks (CHOSEN in 5f)
        public const string FflOverEntry = "fflOverEntry";
        public const string StepsRule = "stepsRule";
        public const string StepsAtMost = "stepsAtMost";
        public const string StepHeights = "stepHeights";
        public const string Riser = "riser";
        public const string OneStepUnits = "oneStepUnits";
        public const string OneFflPerPiece = "oneFflPerPiece";
        public const string VillaTypeCheck = "villaType";
        public static readonly string[] Checks = { FflOverEntry, StepsRule, StepsAtMost, StepHeights, Riser, OneStepUnits, OneFflPerPiece, VillaTypeCheck };

        // The field of the override list each editable column writes, as R360 names them. No build applies unit yet, the unit of a plot
        // comes from typologyRev9 of plots.csv (proposed question of 5f)
        private static readonly Dictionary<string, string> Fields = new Dictionary<string, string>
        {
            [UpdateText.Ffl] = "heightM", [UpdateText.Entry] = "entryM", [UpdateText.Steps] = "steps", [UpdateText.VillaType] = "unit", [UpdateText.Mirror] = "mirrored",
        };

        // The decimals of the columns of the level Excel (R364): El @ Entry and Height 0.0000, FFL and SSL 0.000, Steps 0
        private const int EntryDecimals = 4;
        private const int FflDecimals = 3;

        private const string NoNumber = "the cell holds no number";
        private const string NoWhole = "the cell holds no whole number";
        private const string Empty = "the cell is empty";
        private const string NoType = "the cell holds no villa type";
        private const string NoFlag = "the cell holds no TRUE or FALSE";

        public static string OverrideField(string field)
        {
            return field != null && Fields.TryGetValue(field, out string found) ? found : null;
        }

        private static string Text(JToken token)
        {
            return token?.Type == JTokenType.String ? (string)token : null;
        }

        private static bool IsNumber(JToken token)
        {
            return token != null && (token.Type == JTokenType.Float || token.Type == JTokenType.Integer);
        }

        private static object Cell(object[] row, int column)
        {
            return row != null && column >= 0 && column < row.Length ? row[column] : null;
        }

        // A key cell as text, a number the team typed as its digits
        private static string Key(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string text:
                    return text.Length == 0 ? null : text;
                case double number:
                    return number.ToString("R", CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        // The build id the writers put in the Settings sheet of a plan, the run id of its run stamp or the words of R364 for none
        public static string BuildIdOf(JObject plan)
        {
            return Text((plan?["run"] as JObject)?["runId"]) ?? LevelExcelText.NoBuildId;
        }

        private static Dictionary<string, object> Settings(UpdateBook book)
        {
            UpdateSheet sheet = book.Sheet(LevelExcelText.SettingsSheet);
            if (sheet == null)
            {
                return null;
            }
            var found = new Dictionary<string, object>();
            foreach (object[] row in sheet.Rows)
            {
                if (Cell(row, 0) is string name && !found.ContainsKey(name))
                {
                    found[name] = Cell(row, 1);
                }
            }
            return found;
        }

        // Null when the Settings sheet of the Excel names the build id and the SHA-256 of the plan of the model, else the one line of why
        public static string Refusal(UpdateBook book, JObject plan, string planSha256)
        {
            Dictionary<string, object> settings = Settings(book);
            if (settings == null || !(settings.TryGetValue(LevelExcelText.PlanBuildId, out object id) && id is string) || !(settings.TryGetValue(LevelExcelText.PlanSha, out object sha) && sha is string))
            {
                return book.FileName + " holds no sheet Settings with the Plan build id and the Plan SHA-256, so its build is not known (R402)";
            }
            string built = BuildIdOf(plan);
            if ((string)id != built)
            {
                return book.FileName + " is of another build, its Plan build id is " + id + ", the plan of the model " + built + " (R402)";
            }
            if (!string.Equals((string)sha, planSha256, StringComparison.OrdinalIgnoreCase))
            {
                return book.FileName + " is of another build, its Plan SHA-256 is " + sha + ", the plan of the model " + planSha256 + " (R402)";
            }
            return null;
        }

        // The edited levels of one written plot as the Levels sheet holds them, each null where the cell is not a valid change
        private sealed class LevelEdit
        {
            public string Plot;
            public string Cluster;
            public double? Entry;
            public double? Ffl;
            public long? Steps;
            public readonly List<UpdateChange> Changes = new List<UpdateChange>();
        }

        private sealed class Context
        {
            public JObject Plan;
            public Dictionary<string, JObject> Written;
            public HashSet<string> NotWritten;
            public UpdateList List;
            public readonly Dictionary<string, LevelEdit> Levels = new Dictionary<string, LevelEdit>();
            public readonly List<string> LevelOrder = new List<string>();

            public string WhyNot(string plot)
            {
                return NotWritten.Contains(plot) ? "the plan does not write the plot" : "no plot of the plan";
            }
        }

        public static UpdateList Compare(IList<UpdateBook> books, JObject plan, string planSha256, JObject rules, string profile, JObject overrides = null)
        {
            var list = new UpdateList();
            foreach (UpdateBook book in books)
            {
                list.Refusal = Refusal(book, plan, planSha256);
                if (list.Refusal == null && book.Sheet(LevelExcelText.LevelsSheet) == null && book.Sheet(ReportExcelText.PlacedSheet) == null)
                {
                    list.Refusal = book.FileName + " holds no sheet Levels or Placed, so it is no level Excel or report Excel (R402)";
                }
                if (list.Refusal != null)
                {
                    return list;
                }
            }
            JObject levelProfile = rules?["levelProfiles"]?[profile] as JObject;
            if (levelProfile == null)
            {
                list.Refusal = "the rules profile holds no level profile " + profile + " (R402)";
                return list;
            }
            var made = new Context
            {
                Plan = plan, List = list,
                Written = (plan["plots"] as JArray ?? new JArray()).OfType<JObject>().ToDictionary(item => (string)item["plot"]),
                NotWritten = new HashSet<string>((plan["missing"] as JArray ?? new JArray()).OfType<JObject>().Where(item => item["written"]?.Type == JTokenType.Boolean && !(bool)item["written"]).Select(item => (string)item["plot"])),
            };
            foreach (UpdateBook book in books)
            {
                string refusal = ReadLevels(book, made) ?? ReadPlaced(book, made);
                if (refusal != null)
                {
                    list.Refusal = refusal;
                    list.Changes.Clear();
                    list.Ignored.Clear();
                    return list;
                }
            }
            CheckLevels(made, rules, levelProfile);
            CheckTypes(made, rules);
            ManualRules(list, rules);
            NotSavable(list, overrides, Text(levelProfile["job"]));
            foreach (UpdateChange change in list.Changes)
            {
                change.Ticked = change.Invalid == null && change.ManualRules.Count == 0;
            }
            return list;
        }

        private static int Header(UpdateSheet sheet, string[] header)
        {
            for (int index = 0; index < sheet.Rows.Count; index++)
            {
                object[] row = sheet.Rows[index];
                if (header.Select((name, column) => Cell(row, column) as string == name).All(same => same))
                {
                    return index;
                }
            }
            return -1;
        }

        private static void Ignore(Context made, UpdateBook book, UpdateSheet sheet, int index, string plot, string column, object model, object excel, string why)
        {
            made.List.Ignored.Add(new IgnoredCell { Plot = plot, FileName = book.FileName, Sheet = sheet.Name, Row = index + 1, Column = column, InModel = model, InExcel = excel, Why = why });
        }

        // A number cell against the value of the plan at the decimals of its column, 0 for a whole number: false when they agree, else true
        // with the value read, rounded by R385, or why the cell can not be taken
        private static bool Differs(object cell, JToken model, int decimals, out object inModel, out object inExcel, out string invalid)
        {
            inModel = IsNumber(model) ? (decimals == 0 ? (object)(long)Rounding.Fixed((double)model, 0) : Rounding.Fixed((double)model, decimals)) : null;
            inExcel = cell;
            invalid = null;
            if (cell == null)
            {
                invalid = inModel == null ? null : Empty;
                return invalid != null;
            }
            if (!(cell is double || cell is long))
            {
                invalid = NoNumber;
                return true;
            }
            double read = Convert.ToDouble(cell, CultureInfo.InvariantCulture);
            if (decimals == 0)
            {
                if (Math.Floor(read) != read)
                {
                    invalid = NoWhole;
                    return true;
                }
                inExcel = (long)read;
            }
            else
            {
                inExcel = Rounding.Fixed(read, decimals);
            }
            return inModel == null || !inExcel.Equals(inModel);
        }

        private static UpdateChange Change(Context made, UpdateBook book, UpdateSheet sheet, int index, string plot, string field, object model, object excel, string invalid)
        {
            var change = new UpdateChange { Plot = plot, Field = field, FileName = book.FileName, Sheet = sheet.Name, Row = index + 1, InModel = model, InExcel = excel, Invalid = invalid };
            made.List.Changes.Add(change);
            return change;
        }

        // The sheet Levels of the level Excel (R364): Plot No the key, El @ Entry, FFL and Steps editable, Unit, Road at front, SSL and Height
        // held to the plan as not editable. Cluster and Source are texts the writer makes from the step files, which the plan does not hold
        private static string ReadLevels(UpdateBook book, Context made)
        {
            UpdateSheet sheet = book.Sheet(LevelExcelText.LevelsSheet);
            if (sheet == null)
            {
                return null;
            }
            string[] columns = LevelExcelText.LevelColumns;
            int header = Header(sheet, columns);
            if (header < 0)
            {
                return book.FileName + " holds no header " + string.Join(", ", columns) + " in its sheet Levels (R402)";
            }
            Func<string, int> at = name => Array.IndexOf(columns, name);
            var seen = new HashSet<string>();
            for (int index = header + 1; index < sheet.Rows.Count; index++)
            {
                object[] row = sheet.Rows[index];
                string plot = Key(Cell(row, 0));
                if (plot == null)
                {
                    break;
                }
                if (!seen.Add(plot) || !made.Written.TryGetValue(plot, out JObject planned))
                {
                    string why = seen.Count > 0 && made.Levels.ContainsKey(plot) ? "a second row of the plot" : made.WhyNot(plot);
                    foreach (string name in columns.Where(name => name != LevelExcelText.PlotNo && name != LevelExcelText.Cluster && name != LevelExcelText.Source))
                    {
                        if (Cell(row, at(name)) != null)
                        {
                            Ignore(made, book, sheet, index, plot, name, null, Cell(row, at(name)), why);
                        }
                    }
                    continue;
                }
                JObject levels = planned["levels"] as JObject ?? new JObject();
                var edit = new LevelEdit { Plot = plot, Cluster = Cell(row, at(LevelExcelText.Cluster)) as string };
                made.Levels[plot] = edit;
                made.LevelOrder.Add(plot);
                string unit = Text(planned["unit"]?["value"]);
                object typed = Cell(row, at(LevelExcelText.Unit));
                if (!Equals(typed, unit))
                {
                    Ignore(made, book, sheet, index, plot, LevelExcelText.Unit, unit, typed, "not editable");
                }
                foreach (string name in columns)
                {
                    object cell = Cell(row, at(name));
                    object model, excel;
                    string invalid;
                    switch (name)
                    {
                        case LevelExcelText.RoadAtFront:
                        case LevelExcelText.Ssl:
                        case LevelExcelText.Height:
                            string key = name == LevelExcelText.RoadAtFront ? "roadAtFrontM" : name == LevelExcelText.Ssl ? "ssl" : "height";
                            if (Differs(cell, levels[key]?["value"], name == LevelExcelText.Ssl ? FflDecimals : EntryDecimals, out model, out excel, out invalid))
                            {
                                Ignore(made, book, sheet, index, plot, name, model, cell, "not editable");
                            }
                            break;
                        case LevelExcelText.Entry:
                            if (Differs(cell, levels["entry"]?["value"], EntryDecimals, out model, out excel, out invalid))
                            {
                                edit.Changes.Add(Change(made, book, sheet, index, plot, UpdateText.Entry, model, excel, invalid));
                                edit.Entry = invalid == null ? (double?)excel : null;
                            }
                            break;
                        case LevelExcelText.Ffl:
                            if (Differs(cell, levels["ffl"]?["value"], FflDecimals, out model, out excel, out invalid))
                            {
                                edit.Changes.Add(Change(made, book, sheet, index, plot, UpdateText.Ffl, model, excel, invalid));
                                edit.Ffl = invalid == null ? (double?)excel : null;
                            }
                            break;
                        case LevelExcelText.Steps:
                            if (Differs(cell, levels["steps"]?["value"], 0, out model, out excel, out invalid))
                            {
                                edit.Changes.Add(Change(made, book, sheet, index, plot, UpdateText.Steps, model, excel, invalid));
                                edit.Steps = invalid == null ? (long?)excel : null;
                            }
                            break;
                    }
                }
            }
            return null;
        }

        // The sheet Placed of the report Excel (R365): plot and kind the key, on a row of kind link the villa type and the mirror flag
        // editable, the type of a floor, the mirror of a floor and the element id not editable
        private static string ReadPlaced(UpdateBook book, Context made)
        {
            UpdateSheet sheet = book.Sheet(ReportExcelText.PlacedSheet);
            if (sheet == null)
            {
                return null;
            }
            string[] columns = ReportExcelText.PlacedColumns;
            int header = Header(sheet, columns);
            if (header < 0)
            {
                return book.FileName + " holds no header " + string.Join(", ", columns) + " in its sheet Placed (R402)";
            }
            int type = Array.IndexOf(columns, ReportExcelText.Type), mirror = Array.IndexOf(columns, ReportExcelText.Mirror), element = Array.IndexOf(columns, ReportExcelText.ElementId);
            var seen = new HashSet<string>();
            for (int index = header + 1; index < sheet.Rows.Count; index++)
            {
                object[] row = sheet.Rows[index];
                string plot = Key(Cell(row, 0));
                if (plot == null)
                {
                    break;
                }
                string kind = Cell(row, 1) as string;
                bool first = seen.Add(plot + "\n" + kind);
                JObject planned = null;
                JObject floor = null;
                bool known = first && made.Written.TryGetValue(plot, out planned) && (kind == ReportExcelText.LinkKind || (floor = planned["floors"]?[kind ?? ""] as JObject) != null);
                if (!known)
                {
                    string why = !first ? "a second row of the plot and kind" : planned == null ? made.WhyNot(plot) : "no kind of the plot in the plan";
                    Ignore(made, book, sheet, index, plot, ReportExcelText.Kind, null, kind, why);
                    continue;
                }
                if (Cell(row, element) != null)
                {
                    Ignore(made, book, sheet, index, plot, ReportExcelText.ElementId, null, Cell(row, element), "not editable");
                }
                if (floor != null)
                {
                    string model = Text(floor["type"]?["value"]);
                    if (!Equals(Cell(row, type), model))
                    {
                        Ignore(made, book, sheet, index, plot, ReportExcelText.Type, model, Cell(row, type), "the type of a floor is not editable");
                    }
                    if (Cell(row, mirror) != null)
                    {
                        Ignore(made, book, sheet, index, plot, ReportExcelText.Mirror, null, Cell(row, mirror), "a floor holds no mirror flag");
                    }
                    continue;
                }
                string unit = Text(planned["unit"]?["value"]);
                object typed = Cell(row, type);
                if (!Equals(typed, unit))
                {
                    Change(made, book, sheet, index, plot, UpdateText.VillaType, unit, typed, typed is string text && text.Trim().Length > 0 ? null : NoType);
                }
                JToken flag = planned["mirrored"]?["value"];
                object model2 = flag?.Type == JTokenType.Boolean ? (object)(bool)flag : null;
                object read = Cell(row, mirror);
                if (!Equals(read, model2))
                {
                    Change(made, book, sheet, index, plot, UpdateText.Mirror, model2, read, read is bool ? null : read == null ? Empty : NoFlag);
                }
            }
            return null;
        }

        // The rules of the steps block the level checks read, by key, each lacking key named once in Lacks with the check it stops
        private sealed class StepRules
        {
            public JObject Rules;
            public JObject Steps;
            public UpdateList List;

            public double? Number(string key, string check)
            {
                JToken value = Steps?[key];
                if (IsNumber(value))
                {
                    return (double)value;
                }
                Lack("steps." + key, check);
                return null;
            }

            public JArray Array(string key, string check)
            {
                if (Steps?[key] is JArray found)
                {
                    return found;
                }
                Lack("steps." + key, check);
                return null;
            }

            public void Lack(string key, string check)
            {
                string line = "the rules profile holds no " + key + ", so the check " + check + " is not run (R402)";
                if (!List.Lacks.Contains(line))
                {
                    List.Lacks.Add(line);
                }
            }
        }

        // The level rule checks of each plot with a level change, on its row before the edit, the plan, and after it. A check the row held
        // before and breaks after is broken by the change and named on each level change of the plot. Steps not edited are the count the
        // steps rule gives the height after, as the build gives them again
        private static void CheckLevels(Context made, JObject rules, JObject levelProfile)
        {
            var steps = new StepRules { Rules = rules, Steps = rules["steps"] as JObject, List = made.List };
            JObject keyRules = levelProfile["keyRules"] as JObject ?? new JObject();
            string stepsRule = Text(keyRules["steps"]?["rule"]);
            JObject baseRule = keyRules["baseHeight"] as JObject;
            var after = new Dictionary<string, double>();
            foreach (string plot in made.LevelOrder)
            {
                LevelEdit edit = made.Levels[plot];
                JObject planned = made.Written[plot];
                JObject levels = planned["levels"] as JObject ?? new JObject();
                JToken entryToken = levels["entry"]?["value"], fflToken = levels["ffl"]?["value"], stepsToken = levels["steps"]?["value"];
                if (!IsNumber(fflToken))
                {
                    continue;
                }
                after[plot] = edit.Ffl ?? (double)fflToken;
                List<UpdateChange> valid = edit.Changes.Where(change => change.Invalid == null).ToList();
                if (valid.Count == 0 || !IsNumber(entryToken) || !IsNumber(stepsToken))
                {
                    continue;
                }
                string unit = Text(planned["unit"]?["value"]);
                double entry0 = (double)entryToken, ffl0 = (double)fflToken;
                long steps0 = (long)stepsToken;
                double entry1 = edit.Entry ?? entry0, ffl1 = edit.Ffl ?? ffl0;
                double height0 = Rounding.Fixed(ffl0 - entry0, EntryDecimals), height1 = Rounding.Fixed(ffl1 - entry1, EntryDecimals);
                double? baseHeight = steps.Steps?["base_height"] is JObject ? KeyLevels.BaseByRule(rules, unit, baseRule) : (double?)null;
                Func<double, long?> byRule = height =>
                {
                    if (stepsRule == null)
                    {
                        steps.Lack("keyRules.steps.rule", StepsRule);
                        return null;
                    }
                    JToken found = KeyLevels.StepsByRule(stepsRule, height, unit, rules, levelProfile, baseRule);
                    return found == null ? (long?)null : (long)found;
                };
                // A build applies a steps override after the steps rule, so a plot the plan gave its override count keeps that count
                bool stepsOverride = (planned["overrides"]?["applied"] as JArray)?.Any(item => Text(item) == Fields[UpdateText.Steps]) == true;
                long? steps1 = edit.Steps ?? (!stepsOverride && (edit.Entry.HasValue || edit.Ffl.HasValue) ? byRule(height1) : steps0);
                if (!steps1.HasValue)
                {
                    continue;
                }
                var broken = new List<string>();
                Action<string, bool?, bool?> hold = (name, before, now) =>
                {
                    if (before == true && now == false)
                    {
                        broken.Add(name);
                    }
                };
                if (baseHeight.HasValue)
                {
                    // The FFL is written to the mm, so a height half a mm under the base still stands on it
                    double slack = 0.5 * Math.Pow(10, -FflDecimals);
                    hold(FflOverEntry, height0 >= baseHeight.Value - slack, height1 >= baseHeight.Value - slack);
                }
                else
                {
                    steps.Lack("steps.base_height", FflOverEntry);
                }
                if (edit.Steps.HasValue)
                {
                    long? rule0 = byRule(height0), rule1 = byRule(height1);
                    hold(StepsRule, rule0.HasValue ? rule0 == steps0 : (bool?)null, rule1.HasValue ? rule1 == steps1 : (bool?)null);
                }
                double? low = steps.Number("front_min", StepsAtMost), high = steps.Number("front_max", StepsAtMost);
                if (low.HasValue && high.HasValue)
                {
                    hold(StepsAtMost, steps0 >= low && steps0 <= high, steps1 >= low && steps1 <= high);
                }
                JArray legend = steps.Array("legend", StepHeights);
                if (legend != null)
                {
                    hold(StepHeights, InLegend(legend, height0, steps0), InLegend(legend, height1, steps1.Value));
                }
                double? riserMin = steps.Number("riser_min", Riser), riserMax = steps.Number("riser_max", Riser);
                if (riserMin.HasValue && riserMax.HasValue && baseHeight.HasValue)
                {
                    Func<double, long, bool> riser = (height, count) =>
                    {
                        if (count < 2)
                        {
                            return true;
                        }
                        double millimetres = Rounding.Fixed((height - baseHeight.Value) * 1000 / (count - 1), 6);
                        return millimetres >= riserMin.Value && millimetres <= riserMax.Value;
                    };
                    hold(Riser, riser(height0, steps0), riser(height1, steps1.Value));
                }
                JArray single = steps.Array("one_step_only", OneStepUnits);
                if (single != null && single.Any(item => Text(item) == unit))
                {
                    hold(OneStepUnits, steps0 == 1, steps1 == 1);
                }
                foreach (UpdateChange change in valid)
                {
                    change.Breaks.AddRange(broken);
                }
            }
            // One FFL per piece: the written plots of one cluster of the sheet that share the FFL of the rule (levelRule.ffl) are one piece
            var pieces = made.LevelOrder.Where(plot => after.ContainsKey(plot) && made.Levels[plot].Cluster != null && made.Levels[plot].Cluster != LevelExcelText.NoCluster
                    && IsNumber(made.Written[plot]["levelRule"]?["ffl"]))
                .GroupBy(plot => made.Levels[plot].Cluster + "\n" + ((double)made.Written[plot]["levelRule"]["ffl"]).ToString("R", CultureInfo.InvariantCulture));
            foreach (IGrouping<string, string> piece in pieces)
            {
                List<string> members = piece.ToList();
                List<UpdateChange> moved = members.SelectMany(plot => made.Levels[plot].Changes).Where(change => change.Field == UpdateText.Ffl && change.Invalid == null).ToList();
                if (members.Count < 2 || moved.Count == 0)
                {
                    continue;
                }
                bool before = members.Select(plot => (double)made.Written[plot]["levels"]["ffl"]["value"]).Distinct().Count() == 1;
                bool now = members.Select(plot => after[plot]).Distinct().Count() == 1;
                if (before && !now)
                {
                    foreach (UpdateChange change in moved)
                    {
                        change.Breaks.Add(OneFflPerPiece);
                    }
                }
            }
        }

        // The step heights of steps.legend for a count, held when the height lies between the least and the most it lists for that count
        private static bool InLegend(JArray legend, double height, long count)
        {
            List<double> heights = legend.OfType<JArray>().Where(pair => pair.Count == 2 && IsNumber(pair[0]) && IsNumber(pair[1]) && (long)Rounding.Fixed((double)pair[1], 0) == count).Select(pair => (double)pair[0]).ToList();
            return heights.Count > 0 && height >= heights.Min() && height <= heights.Max();
        }

        // A villa type a typology of the rules profile names
        private static void CheckTypes(Context made, JObject rules)
        {
            JObject typologies = rules["typologies"] as JObject;
            List<UpdateChange> types = made.List.Changes.Where(change => change.Field == UpdateText.VillaType && change.Invalid == null).ToList();
            if (types.Count == 0)
            {
                return;
            }
            if (typologies == null)
            {
                made.List.Lacks.Add("the rules profile holds no typologies, so the check " + VillaTypeCheck + " is not run (R402)");
                return;
            }
            foreach (UpdateChange change in types.Where(change => typologies[(string)change.InExcel] == null))
            {
                change.Breaks.Add(VillaTypeCheck);
            }
        }

        // Apply takes the key and model of an entry of the job on the plot (Q169), so a change of a plot with none can not be saved and is
        // marked here, before the window offers it. An override list not given marks none
        private static void NotSavable(UpdateList list, JObject overrides, string job)
        {
            if (overrides == null || job == null)
            {
                return;
            }
            var held = new HashSet<string>((overrides["overrides"] as JArray ?? new JArray()).OfType<JObject>().Where(item => Text(item["source"]) == job).Select(item => Text(item["plot"])).Where(plot => plot != null));
            string why = UpdateText.Fill(UpdateText.NotSavable, job);
            foreach (UpdateChange change in list.Changes.Where(change => change.Invalid == null && !held.Contains(change.Plot)))
            {
                change.Invalid = why;
            }
        }

        // The block manualRules of the rules profile, a list of entries or an object by id, each entry naming the checks it holds by
        // updateChecks. Lane B 5c writes the block from docs/design/15-manual-rules.md, a block of no updateChecks holds no change
        private static void ManualRules(UpdateList list, JObject rules)
        {
            JToken block = rules["manualRules"];
            IEnumerable<Tuple<string, JObject>> entries;
            if (block is JArray array)
            {
                entries = array.OfType<JObject>().Select(item => Tuple.Create(Text(item["id"]), item));
            }
            else if (block is JObject named)
            {
                entries = named.Properties().Where(item => item.Value is JObject).Select(item => Tuple.Create(Text(item.Value["id"]) ?? item.Name, (JObject)item.Value));
            }
            else
            {
                list.Lacks.Add("the rules profile holds no block manualRules, so no change breaks a manual rule (R402)");
                return;
            }
            var byCheck = new Dictionary<string, List<string>>();
            var bare = new List<string>();
            foreach (Tuple<string, JObject> entry in entries.Where(item => item.Item1 != null))
            {
                JArray checks = entry.Item2["updateChecks"] as JArray;
                if (checks == null || checks.Count == 0)
                {
                    bare.Add(entry.Item1);
                    continue;
                }
                foreach (string check in checks.Select(Text).Where(item => item != null))
                {
                    if (!byCheck.TryGetValue(check, out List<string> ids))
                    {
                        byCheck[check] = ids = new List<string>();
                    }
                    ids.Add(entry.Item1);
                }
            }
            if (bare.Count > 0)
            {
                list.Lacks.Add(bare.Count + " manual rules of the block name no updateChecks, so no change is held against them: " + string.Join(", ", bare) + " (R402)");
            }
            foreach (UpdateChange change in list.Changes)
            {
                foreach (string id in change.Breaks.Where(byCheck.ContainsKey).SelectMany(check => byCheck[check]).Distinct())
                {
                    change.ManualRules.Add(id);
                }
            }
        }

        // Each ticked change saved as one entry of the override list of R330 in the form of R360: El @ Entry and FFL as the gap to the rule of
        // the plan (levelRule) at 4 decimals, Steps a whole number, the villa type a text and Mirror true or false, with the job of the level
        // profile as source, the key and model of an entry of that source of the plot, and byHand true so a majority run keeps it. An entry
        // of the same plot, field and source is replaced in place, else the new one goes after the last entry of its plot
        public static UpdateApplied Apply(IEnumerable<UpdateChange> ticked, JObject plan, JObject overrides, JObject rules, string profile, string runId)
        {
            string job = Text(rules?["levelProfiles"]?[profile]?["job"]);
            if (job == null)
            {
                throw new InvalidDataException("the level profile " + profile + " names no job, so an override has no source (R402)");
            }
            var applied = new UpdateApplied { List = (JObject)overrides.DeepClone() };
            JArray entries = applied.List["overrides"] as JArray ?? throw new InvalidDataException("the override list holds no overrides (R402)");
            Dictionary<string, JObject> plots = (plan["plots"] as JArray ?? new JArray()).OfType<JObject>().ToDictionary(item => (string)item["plot"]);
            string reason = UpdateText.Reason.Replace("<run id>", runId);
            foreach (UpdateChange change in ticked)
            {
                string where = "plot " + change.Plot + " " + change.Field + ": ";
                string field = OverrideField(change.Field);
                if (change.Invalid != null || field == null)
                {
                    applied.NotApplied.Add(where + (change.Invalid ?? "no field of the override list") + " (R402)");
                    continue;
                }
                JToken value;
                if (change.Field == UpdateText.Entry || change.Field == UpdateText.Ffl)
                {
                    JToken rule = plots.TryGetValue(change.Plot, out JObject planned) ? planned["levelRule"]?[change.Field == UpdateText.Entry ? "entry" : "ffl"] : null;
                    if (!IsNumber(rule))
                    {
                        applied.NotApplied.Add(where + "the plan holds no levelRule of the plot, so no gap to the rule (R402)");
                        continue;
                    }
                    value = new JObject { ["gapToRuleM"] = Rounding.Fixed((double)change.InExcel - (double)rule, EntryDecimals) };
                }
                else
                {
                    value = new JValue(change.InExcel);
                }
                List<JObject> mine = entries.OfType<JObject>().Where(item => Text(item["plot"]) == change.Plot).ToList();
                List<JObject> same = mine.Where(item => Text(item["source"]) == job && Text(item["field"]) == field).ToList();
                JObject origin = same.FirstOrDefault() ?? mine.FirstOrDefault(item => Text(item["source"]) == job);
                if (origin == null)
                {
                    applied.NotApplied.Add(where + "no entry of source " + job + " of the plot names its key and model (R402)");
                    continue;
                }
                var entry = new JObject
                {
                    ["plot"] = change.Plot, ["field"] = field, ["value"] = value, ["rule"] = null, ["source"] = job, ["model"] = origin["model"]?.DeepClone(), ["key"] = origin["key"]?.DeepClone(),
                    ["decision"] = UpdateText.Decision, ["reason"] = reason, ["byHand"] = true,
                };
                int place;
                if (same.Count > 0)
                {
                    place = entries.IndexOf(same[0]);
                    foreach (JObject old in same)
                    {
                        if (!applied.Entries.Contains(old))
                        {
                            applied.Replaced.Add(old);
                        }
                        applied.Entries.Remove(old);
                        old.Remove();
                    }
                }
                else
                {
                    place = entries.IndexOf(mine[mine.Count - 1]) + 1;
                }
                entries.Insert(place, entry);
                applied.Entries.Add(entry);
                if (!applied.Plots.Contains(change.Plot))
                {
                    applied.Plots.Add(change.Plot);
                }
            }
            return applied;
        }

        private static JToken Value(object value)
        {
            return value == null ? JValue.CreateNull() : new JValue(value);
        }

        // The result as JSON for the worker and the second route python/update_check.py: the refusal, each change with its cells and checks,
        // the ignored cells, the tiles, chips and lacks, and when applied the entries, the plots, the changes not applied and the list text
        public static JObject ResultJson(UpdateList list, UpdateApplied applied)
        {
            var found = new JObject
            {
                ["refusal"] = Value(list.Refusal),
                ["changes"] = new JArray(list.Changes.Select(change => new JObject
                {
                    ["file"] = change.FileName, ["sheet"] = change.Sheet, ["row"] = change.Row, ["plot"] = change.Plot, ["field"] = change.Field, ["inModel"] = Value(change.InModel), ["inExcel"] = Value(change.InExcel),
                    ["invalid"] = Value(change.Invalid), ["breaks"] = new JArray(change.Breaks), ["manualRules"] = new JArray(change.ManualRules), ["ruleCheck"] = change.RuleCheck, ["ticked"] = change.Ticked,
                })),
                ["ignored"] = new JArray(list.Ignored.Select(cell => new JObject
                {
                    ["file"] = cell.FileName, ["sheet"] = cell.Sheet, ["row"] = cell.Row, ["plot"] = cell.Plot, ["column"] = cell.Column, ["inModel"] = Value(cell.InModel), ["inExcel"] = Value(cell.InExcel), ["why"] = cell.Why,
                })),
                ["tiles"] = JObject.FromObject(list.Tiles()),
                ["chips"] = JObject.FromObject(list.Chips()),
                ["lacks"] = new JArray(list.Lacks),
            };
            if (applied != null)
            {
                found["entries"] = new JArray(applied.Entries.Select(item => item.DeepClone()));
                found["replaced"] = new JArray(applied.Replaced.Select(item => item.DeepClone()));
                found["plots"] = new JArray(applied.Plots);
                found["notApplied"] = new JArray(applied.NotApplied);
                found["listText"] = ListText(applied.List);
            }
            return found;
        }

        // The list as majority.overrides_text writes it, one entry per line, so Bader finds and edits a plot by hand
        public static string ListText(JObject list)
        {
            string[] keys = { "note", "overrides", "cannot", "skip" };
            string other = list.Properties().Select(item => item.Name).FirstOrDefault(name => !keys.Contains(name));
            if (other != null)
            {
                throw new InvalidDataException("the override list holds " + other + ", which the form of majority.overrides_text does not write (R402)");
            }
            Func<JToken, string> block = items =>
            {
                List<string> lines = (items as JArray ?? new JArray()).Select(item => "    " + EntryText(item)).ToList();
                return string.Join(",\n", lines) + (lines.Count > 0 ? "\n" : "");
            };
            var text = new StringBuilder("{\n  \"note\": " + EntryText(list["note"]) + ",\n  \"overrides\": [\n" + block(list["overrides"]) + "  ]");
            foreach (string key in new[] { "cannot", "skip" })
            {
                if (list[key] != null)
                {
                    text.Append(",\n  \"" + key + "\": [\n" + block(list[key]) + "  ]");
                }
            }
            return text.Append("\n}\n").ToString();
        }

        // A value as json.dumps of Python with ensure_ascii off writes it: ", " and ": " between items, a text with only the quote, the
        // backslash and the controls escaped, a number as PyJson writes it
        public static string EntryText(JToken value)
        {
            switch (value?.Type ?? JTokenType.Null)
            {
                case JTokenType.Object:
                    return "{" + string.Join(", ", ((JObject)value).Properties().Select(item => Quote(item.Name) + ": " + EntryText(item.Value))) + "}";
                case JTokenType.Array:
                    return "[" + string.Join(", ", ((JArray)value).Select(EntryText)) + "]";
                case JTokenType.String:
                    return Quote((string)value);
                default:
                    return PyJson.Dumps(value ?? JValue.CreateNull());
            }
        }

        private static string Quote(string text)
        {
            var written = new StringBuilder("\"");
            foreach (char letter in text)
            {
                switch (letter)
                {
                    case '"':
                        written.Append("\\\"");
                        break;
                    case '\\':
                        written.Append("\\\\");
                        break;
                    case '\n':
                        written.Append("\\n");
                        break;
                    case '\r':
                        written.Append("\\r");
                        break;
                    case '\t':
                        written.Append("\\t");
                        break;
                    case '\b':
                        written.Append("\\b");
                        break;
                    case '\f':
                        written.Append("\\f");
                        break;
                    default:
                        if (letter < 0x20)
                        {
                            written.Append("\\u").Append(((int)letter).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            written.Append(letter);
                        }
                        break;
                }
            }
            return written.Append('"').ToString();
        }
    }
}
