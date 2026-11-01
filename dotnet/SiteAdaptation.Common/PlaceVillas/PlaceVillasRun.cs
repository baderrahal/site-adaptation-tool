using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.Common.PlaceVillas
{
    // R377, R404. Button 7 Place villas as a body of the flow of R382. Every decision is made here and IVillaDocument only carries it out.
    // A villa is placed by insert, rotationDeg and mirrored with its tag, its parameters and its name, origin to origin with its Z at the
    // FFL of the plan, RRE_ZZ_SP_Prototype-FFL written with the other parameters (W6, Bader's changes of 28 Oct 2026)
    public sealed class PlaceVillasRun
    {
        // R404. The part of the tag of a villa solid, so a later button finds it by kind link as it finds the link
        public const string MassPart = "mass";
        public const string RecordFormat = "site-adaptation-place-villas";

        private readonly IVillaDocument document;
        private readonly Func<RunContext, VillaInputs> load;
        private readonly Func<RunContext, ShapeCache> shapes;
        private readonly Func<string, string> hash;
        private readonly Func<IReadOnlyList<string>, bool> askLoad;
        private readonly Func<RunContext, MassFileSet> massFiles;

        // askLoad shows the lines of the types to load or reload with Load and Cancel and is true for Load (03b 7, Link types to load)
        public PlaceVillasRun(IVillaDocument document, Func<RunContext, VillaInputs> load, Func<RunContext, ShapeCache> shapes, Func<string, string> hash, Func<IReadOnlyList<string>, bool> askLoad)
            : this(document, load, shapes, hash, askLoad, null)
        {
        }

        // R450, R452. massFiles gives the mass files of a run whose linkFolder is not solids, null where none is wired
        public PlaceVillasRun(IVillaDocument document, Func<RunContext, VillaInputs> load, Func<RunContext, ShapeCache> shapes, Func<string, string> hash, Func<IReadOnlyList<string>, bool> askLoad,
            Func<RunContext, MassFileSet> massFiles)
        {
            this.document = document;
            this.load = load;
            this.shapes = shapes;
            this.hash = hash;
            this.askLoad = askLoad;
            this.massFiles = massFiles;
        }

        public ButtonBody Body()
        {
            return new ButtonBody { Preview = Preview, Outside = Outside, Apply = Apply };
        }

        private enum Act
        {
            Place,
            Swap,
            Move,
            Keep,
            Skip,
        }

        private sealed class Villa
        {
            public PlanPlot Plot;
            public string Number;

            // The prototype file of the plan, and the file the villa links, another one in Masses with linkFolder not solids (R452)
            public string Prototype;
            public string File;
            public double[] Internal;
            public PlacementSteps Steps;
            public PlanItem Item;
            public BaseShape Shape;
        }

        private sealed class Step
        {
            public Villa Villa;
            public Act Act;
            public MatchOutcome Outcome;
            public string Solid;
            public bool SolidIsLink;
        }

        private sealed class TypeUse
        {
            public VillaLinkType Existing;
            public bool Load;
            public bool Reload;
            public string Unusable;
        }

        private sealed class Decision
        {
            public VillaInputs Inputs;
            public bool Masses;

            // R452. Masses placed as links of the mass files, or of the files linkFolder names, rather than as solids in the site
            public bool MassLinks;
            public bool Solids => Masses && !MassLinks;

            // R450. The prototypes whose mass file is missing or stale, made outside the transaction, and the base of each mass by prototype
            public readonly List<string> Makes = new List<string>();
            public readonly SortedDictionary<string, MassBase> MassBases = new SortedDictionary<string, MassBase>(StringComparer.OrdinalIgnoreCase);
            public readonly List<Villa> Villas = new List<Villa>();
            public readonly List<Step> Steps = new List<Step>();
            public readonly List<string> Listed = new List<string>();

            // R404. The plots of the scope a masses run leaves to the link that stands on them, standing as placed
            public readonly List<string> LinkStands = new List<string>();
            public readonly Dictionary<string, TypeUse> Types = new Dictionary<string, TypeUse>(StringComparer.OrdinalIgnoreCase);
            public readonly SortedDictionary<string, string> Hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public readonly List<FootprintRow> Footprint = new List<FootprintRow>();

            public int Count(Act act) => Steps.Count(step => step.Act == act);
        }

        private RunSummary Preview(RunContext context)
        {
            Decision decision = Decide(context);
            string counts = (decision.Masses ? "Masses" : "Links") + ": place " + decision.Count(Act.Place) + ", swap " + decision.Count(Act.Swap) + ", move " + decision.Count(Act.Move)
                + ", already placed " + decision.Count(Act.Keep) + ", skip " + decision.Count(Act.Skip) + ", listed " + decision.Listed.Count
                + ", link types to load " + decision.Types.Values.Count(use => use.Load) + ", to reload " + decision.Types.Values.Count(use => use.Reload)
                + ", over the footprint limit " + decision.Footprint.Count(row => row.Flagged) + (decision.MassLinks ? ", mass files to make " + decision.Makes.Count : "");
            var lines = decision.Makes.Select(file => "make the mass file of " + Path.GetFileName(file)).Concat(decision.Steps.Select(step => "plot " + step.Villa.Number + ": " + What(step)))
                .Concat(decision.Listed).Concat(FootprintLines(decision)).ToList();
            return new RunSummary(counts, lines);
        }

        // 03b 7, Link types to load: a step of its own before the transaction, after Load on the dialog that names each type
        private IDisposable Outside(RunContext context)
        {
            Decision decision = Decide(context);
            if (decision.Makes.Count > 0)
            {
                // R450. The mass files are new documents of their own, made before any transaction of the site and before the link
                // types load, so a mass made again is reloaded by the step below
                MassFileSet set = massFiles(context);
                foreach (string prototype in decision.Makes)
                {
                    set.Make(prototype, decision.Inputs.Config);
                }
                decision = Decide(context);
                if (decision.Makes.Count > 0)
                {
                    throw new InvalidOperationException("the mass file of " + Path.GetFileName(decision.Makes[0]) + " is still stale after it was made, nothing was placed (R450)");
                }
            }
            List<KeyValuePair<string, TypeUse>> loads = decision.Types.Where(use => use.Value.Load || use.Value.Reload).OrderBy(use => use.Key, StringComparer.Ordinal).ToList();
            if (loads.Count == 0)
            {
                return null;
            }
            var lines = loads.Select(use => (use.Value.Load ? "load " : "reload ") + use.Key + (use.Value.Reload ? ", its file changed since the last real run of Place villas" : "")).ToList();
            lines.Add("Load clears the Undo list of Revit.");
            if (!askLoad(lines))
            {
                throw new OperationCanceledException("Cancel on the link types to load, nothing was loaded or placed");
            }
            foreach (KeyValuePair<string, TypeUse> use in loads)
            {
                string result = use.Value.Load ? document.LoadType(use.Value.Existing.Key) : document.ReloadType(use.Value.Existing.Key);
                if (result != null)
                {
                    throw new InvalidOperationException("the link " + use.Key + " did not load, " + result + ", nothing was placed");
                }
            }
            return null;
        }

        private IReadOnlyList<string> Apply(RunContext context)
        {
            Decision decision = Decide(context);
            string waiting = decision.Types.Where(use => use.Value.Load).Select(use => use.Key).FirstOrDefault();
            if (waiting != null)
            {
                throw new InvalidOperationException("the link type of " + waiting + " is not loaded, the step before the transaction did not load it");
            }
            if (decision.Makes.Count > 0)
            {
                throw new InvalidOperationException("the mass file of " + Path.GetFileName(decision.Makes[0]) + " is not made, the step before the transaction did not make it (R450)");
            }
            VillaInputs inputs = decision.Inputs;
            var issues = new List<string>(decision.Listed);
            var created = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var standing = new List<string>(decision.LinkStands);
            foreach (Step step in decision.Steps)
            {
                Villa villa = step.Villa;
                if (step.Act == Act.Skip)
                {
                    continue;
                }
                if (step.Act == Act.Keep)
                {
                    standing.Add(villa.Number);
                    continue;
                }
                string id;
                if (step.Act == Act.Move)
                {
                    id = step.Outcome.Element;
                    if (decision.Solids)
                    {
                        document.ReshapeSolid(id, villa.Shape.Points, inputs.Config.SolidHeightM, villa.Steps);
                    }
                    else
                    {
                        document.MoveLink(id, villa.Steps);
                    }
                }
                else
                {
                    if (step.Act == Act.Swap)
                    {
                        document.Delete(step.Solid);
                    }
                    id = decision.Solids ? document.PlaceSolid(villa.Shape.Points, inputs.Config.SolidHeightM, villa.Steps) : document.PlaceLink(TypeKey(decision, villa, created), villa.Steps);
                }
                ReadBack(decision, villa, id);
                issues.AddRange(WriteParameters(decision.Solids, villa, id, inputs.ParameterGuids));
                document.Tag(id, step.Act == Act.Move ? step.Outcome.NextTag(context.RunId, inputs.Profile) : PlacedTag.ForPlacement(villa.Item, context.RunId, inputs.Profile, id));
                standing.Add(villa.Number);
            }
            if (standing.Count == 0)
            {
                throw new InvalidOperationException("nothing of the scope is placed or already placed, so Place villas is not done: " + string.Join(". ", issues.DefaultIfEmpty("the plan gives no villa")));
            }
            issues.AddRange(FootprintLines(decision));
            string nbh = inputs.Plan.Neighbourhood ?? "all";
            if (decision.Masses)
            {
                issues.AddRange(FootprintCheck.SummaryLines(decision.Footprint));
                context.WriteText("footprint_" + nbh + ".json", FootprintCheck.Json(decision.Footprint, decision.MassBases).ToString(Newtonsoft.Json.Formatting.Indented));
            }
            var record = new JObject
            {
                ["format"] = RecordFormat, ["runId"] = context.RunId, ["mode"] = decision.Masses ? VillaModes.Masses : VillaModes.Links, ["plan"] = inputs.PlanPath,
                ["linkFolder"] = decision.Masses ? inputs.Config.MassFiles?.LinkFolder ?? LinkFolders.Solids : null,
                ["prototypes"] = new JObject(decision.Hashes.Select(item => new JProperty(item.Key, item.Value))),
                ["placed"] = new JArray(standing.OrderBy(plot => plot, StringComparer.Ordinal)),
                ["counts"] = new JObject(new[] { Act.Place, Act.Swap, Act.Move, Act.Keep, Act.Skip }.Select(act => new JProperty(act.ToString().ToLowerInvariant(), decision.Count(act)))),
            };
            context.WriteText("place_" + nbh + ".json", record.ToString(Newtonsoft.Json.Formatting.Indented));
            return issues;
        }

        // The hash of each prototype file the last real run listed, for the reload of a type whose file changed
        public static Dictionary<string, string> Hashes(JObject record)
        {
            return ((record?["prototypes"] as JObject) ?? new JObject()).Properties().ToDictionary(item => item.Name, item => (string)item.Value, StringComparer.OrdinalIgnoreCase);
        }

        public static HashSet<string> Placed(JObject record)
        {
            return new HashSet<string>(((record?["placed"] as JArray) ?? new JArray()).Select(token => (string)token), StringComparer.Ordinal);
        }

        private Decision Decide(RunContext context)
        {
            VillaInputs inputs = load(context) ?? throw new InvalidOperationException("Place villas has no plan to read");
            PlanFile plan = inputs.Plan;
            var decision = new Decision { Inputs = inputs, Masses = VillaModeChoice.Resolve(inputs.Mode, inputs.Config) == VillaModes.Masses };
            decision.MassLinks = decision.Masses && inputs.Config.MassFiles != null && inputs.Config.MassFiles.AsLinks;
            string format = Placer.CheckFormat(plan);
            if (format != null && !(plan != null && plan.PrototypeFolder != null && format.Contains("prototypeFolder") && UnixFull(plan.PrototypeFolder)))
            {
                throw new InvalidDataException(format);
            }
            SurveyReading survey = document.Survey();
            CoordinateCheck coordinates = Placer.CheckCoordinates(plan, survey);
            if (!coordinates.Match)
            {
                throw new InvalidDataException(coordinates.Message ?? "the plan or the document gives no survey point, nothing was placed (R83)");
            }
            string filter = Placer.CheckFilter(plan, inputs.PrototypeFilter);
            if (filter != null)
            {
                throw new InvalidDataException(filter);
            }
            double[] offset = SharedCoordinates.Offset(survey.Position, survey.SharedPosition);
            inputs.ParameterGuids.TryGetValue(Placer.PlotParameter, out string plotGuid);
            IReadOnlyList<VillaElement> elements = document.Villas(plotGuid);
            // R452. A mass link is a link of the masses, never one that keeps Masses off its plot
            ILookup<string, VillaElement> linkOn = elements.Where(element => !element.IsSolid && !IsMass(element)).Select(element => new { element, plot = element.Tag != null && element.Tag.Key.Kind == PlacedKinds.Link ? element.Tag.Key.Plot : element.PlotNumber?.Trim() })
                .Where(item => !string.IsNullOrEmpty(item.plot)).ToLookup(item => item.plot, item => item.element, StringComparer.Ordinal);
            ShapeCache cache = decision.Solids ? shapes(context) : null;
            MassFileSet set = decision.MassLinks ? massFiles?.Invoke(context) ?? throw new InvalidOperationException("addin.lightMode.linkFolder is " + inputs.Config.MassFiles.LinkFolder + " and no mass files are wired (R450)") : null;
            string linkFolder = set?.LinkFolder(inputs.Config.MassFiles, plan.PrototypeFolder);
            var states = new Dictionary<string, MassState>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<PlanItem>();
            foreach (PlanPlot plot in plan.Plots ?? new List<PlanPlot>())
            {
                string number = plot?.Plot?.Trim();
                bool first = string.IsNullOrEmpty(number) || seen.Add(number);
                if (!Placer.InFilter(plot, inputs.PrototypeFilter))
                {
                    continue;
                }
                string reason = !first ? "the plan lists this plot number more than once, only its first entry is tried" : Lacking(plot, number);
                if (reason != null)
                {
                    decision.Listed.Add("plot " + (number ?? "") + ": skipped, " + reason);
                    continue;
                }
                string name = plot.PrototypeFile.Value.Trim();
                if (Path.GetFileName(name) != name)
                {
                    decision.Listed.Add("plot " + number + ": skipped, prototypeFile must be a file name in the prototype folder, not a path");
                    continue;
                }
                string file = Path.Combine(plan.PrototypeFolder, name);
                if (!document.FileExists(file))
                {
                    throw new InvalidDataException("the prototype file " + file + " of plot " + number + " is not in the folder, nothing was placed (03b 7 Stop)");
                }
                if (decision.Masses && linkOn[number].Any())
                {
                    decision.Listed.Add("plot " + number + ": a link stands on the plot, element " + string.Join(", ", linkOn[number].Select(element => element.UniqueId)) + ", Masses places no solid beside it");
                    if (inputs.InScope == null || inputs.InScope(number))
                    {
                        decision.LinkStands.Add(number);
                    }
                    continue;
                }
                double ffl = plot.Levels.Ffl.Value.Value;
                double zGap = plot.Insert.Value[2] - ffl;
                if (Math.Abs(zGap) > Placer.ZTolerance)
                {
                    decision.Listed.Add("plot " + number + ": skipped, its insert Z and FFL differ by " + (zGap * 1000).ToString("0.###", CultureInfo.InvariantCulture) + " mm");
                    continue;
                }
                // W6. The FFL is a shared elevation, so the survey offset moves it the way button 5 moves the top of a floor
                double[] point = PlacementMath.InternalPoint(new[] { plot.Insert.Value[0], plot.Insert.Value[1], ffl }, offset);
                bool mirrored = plot.Mirrored.Value.Value;
                double rotation = plot.RotationDeg.Value.Value;
                string linked = decision.MassLinks ? MassFiles.PathFor(linkFolder, file) : file;
                BaseShape shape = cache?.Get(file, inputs.Config);
                if (decision.MassLinks && inputs.Config.MassFiles.LinkFolder == LinkFolders.Masses)
                {
                    if (!states.TryGetValue(file, out MassState state))
                    {
                        states[file] = state = set.State(file, inputs.Config);
                        decision.MassBases[file] = state.Base;
                        if (state.Stale)
                        {
                            decision.Makes.Add(file);
                        }
                    }
                    shape = state.Base == null ? null : new BaseShape(state.Base.Source, state.Base.Ring);
                }
                else if (decision.MassLinks && !document.FileExists(linked))
                {
                    throw new InvalidDataException("the link file " + linked + " of plot " + number + " is not in the folder linkFolder names, nothing was placed (R452)");
                }
                var villa = new Villa
                {
                    Plot = plot, Number = number, Prototype = file, File = linked, Internal = point, Steps = PlacementMath.Steps(point, rotation, mirrored),
                    Item = new PlanItem(new TagKey(number, PlacedKinds.Link, decision.Masses ? MassPart : ""), Values(point[0], point[1], point[2], rotation, mirrored),
                        inputs.InScope == null || inputs.InScope(number), inputs.LastPlaced.Contains(number)),
                    Shape = shape,
                };
                decision.Villas.Add(villa);
                items.Add(villa.Item);
                // A mass file still to make has no hash yet, the run after its making hashes it
                if (!decision.Hashes.ContainsKey(linked) && (!decision.MassLinks || document.FileExists(linked)))
                {
                    decision.Hashes[linked] = hash(linked);
                }
            }

            Dictionary<string, Villa> byPlot = decision.Villas.ToDictionary(villa => villa.Number, StringComparer.Ordinal);
            var found = new List<FoundElement>();
            foreach (VillaElement element in elements)
            {
                if (decision.Masses && IsMass(element) && element.IsSolid != decision.Solids)
                {
                    // R452. A mass of the other form, a solid where masses are links or a mass link where they are solids, is swapped
                    // below, never matched as the villa
                    continue;
                }
                if (element.Tag != null && element.Tag.Key.Kind == PlacedKinds.Link)
                {
                    found.Add(FoundElement.Tagged(element.UniqueId, element.Tag, Current(decision, element, byPlot)));
                }
                else if (element.Tag == null && !element.IsSolid && !string.IsNullOrWhiteSpace(element.PlotNumber))
                {
                    found.Add(FoundElement.Untagged(element.UniqueId, element.PlotNumber.Trim(), PlacedKinds.Link));
                }
            }
            MatchResult match = SecondRun.Match(items, found, inputs.SetToPlan);
            // R404, R452. What a run swaps: in Links every mass, a solid or a mass link, in Masses each mass of the other form
            Dictionary<string, VillaElement> solids = elements.Where(element => IsMass(element) && (!decision.Masses || element.IsSolid != decision.Solids))
                .GroupBy(element => element.Tag.Key.Plot, StringComparer.Ordinal).Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
            var swapped = new HashSet<string>(StringComparer.Ordinal);
            foreach (MatchOutcome outcome in match.Outcomes)
            {
                Villa villa = byPlot[outcome.Item.Key.Plot];
                var step = new Step { Villa = villa, Outcome = outcome };
                if (outcome.Action == MatchAction.Place)
                {
                    if (solids.TryGetValue(villa.Number, out VillaElement solid))
                    {
                        step.Act = Act.Swap;
                        step.Solid = solid.UniqueId;
                        step.SolidIsLink = !solid.IsSolid;
                        swapped.Add(solid.UniqueId);
                    }
                    else
                    {
                        step.Act = Act.Place;
                    }
                }
                else if (outcome.Action == MatchAction.Update)
                {
                    step.Act = Act.Move;
                }
                else
                {
                    step.Act = outcome.Case == MatchCase.AlreadyPlaced ? Act.Keep : Act.Skip;
                    if (outcome.Case == MatchCase.ChangedByHand)
                    {
                        decision.Listed.Add("plot " + villa.Number + ": changed by hand in " + string.Join(", ", outcome.Differences) + ", left as it is");
                    }
                }
                decision.Listed.AddRange(outcome.Issues);
                decision.Steps.Add(step);
            }
            decision.Listed.AddRange(match.Unplanned.Where(id => !swapped.Contains(id)).Select(id => "element " + id + " is tagged for no villa of this run, left as it is"));
            if (!decision.Solids)
            {
                Types(decision);
            }
            if (decision.Masses)
            {
                // A mass still to make, or a link of linkFolder prototypes or a folder, has no base to measure, the run after its making does
                foreach (Step step in decision.Steps.Where(step => step.Act != Act.Skip && step.Villa.Shape != null))
                {
                    List<double[]> placed = ShapeMath.Place(step.Villa.Shape.Points, step.Villa.Plot.Insert.Value, step.Villa.Plot.RotationDeg.Value.Value, step.Villa.Plot.Mirrored.Value.Value);
                    decision.Footprint.Add(FootprintCheck.Row(step.Villa.Number, placed, inputs.Footprint ?? new List<double[][]>(), inputs.Config, Path.GetFileName(step.Villa.Prototype)));
                }
                foreach (KeyValuePair<string, MassBase> mass in decision.MassBases.Where(item => item.Value != null && item.Value.Flagged))
                {
                    decision.Listed.Add("prototype " + Path.GetFileName(mass.Key) + ": the mass base is the largest ground slab, " + mass.Value.Reason + ", flagged (R451, Q279)");
                }
            }
            return decision;
        }

        // 03b 7 Link types: matched by the full path after Path.GetFullPath without case, never by the file name alone. A type of the
        // same name at another path is not reused, both are listed and a new type is made for the path of the plan
        private void Types(Decision decision)
        {
            IReadOnlyList<VillaLinkType> types = document.LinkTypes();
            foreach (string file in decision.Villas.Select(villa => villa.File).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                List<VillaLinkType> same = types.Where(type => type.FullPath != null && SamePath(type.FullPath, file)).ToList();
                foreach (VillaLinkType other in types.Where(type => type.FullPath != null && !SamePath(type.FullPath, file) && string.Equals(Path.GetFileName(type.FullPath), Path.GetFileName(file), StringComparison.OrdinalIgnoreCase)))
                {
                    decision.Listed.Add("the link type of " + other.FullPath + " has the name of " + file + " at another path and is not reused" + (same.Count == 0 ? ", a new type is made for " + file : ""));
                }
                var use = new TypeUse { Existing = same.FirstOrDefault(type => type.Usable && type.Loaded) };
                if (use.Existing != null)
                {
                    use.Reload = decision.Inputs.LastHashes.TryGetValue(file, out string last) && last != null && decision.Hashes.TryGetValue(file, out string now) && !string.Equals(last, now, StringComparison.OrdinalIgnoreCase);
                }
                else if ((use.Existing = same.FirstOrDefault(type => type.Usable)) != null)
                {
                    use.Load = true;
                }
                else if (same.Count > 0)
                {
                    use.Unusable = same[0].Why ?? "it cannot be used";
                }
                decision.Types[file] = use;
            }
            foreach (Step step in decision.Steps.Where(step => (step.Act == Act.Place || step.Act == Act.Swap) && decision.Types[step.Villa.File].Unusable != null))
            {
                decision.Listed.Add("plot " + step.Villa.Number + ": skipped, the link type of " + step.Villa.File + " cannot be used, " + decision.Types[step.Villa.File].Unusable + ". Reload or remove it first");
                step.Act = Act.Skip;
            }
        }

        private string TypeKey(Decision decision, Villa villa, Dictionary<string, string> created)
        {
            TypeUse use = decision.Types[villa.File];
            if (use.Existing != null)
            {
                return use.Existing.Key;
            }
            if (!created.TryGetValue(villa.File, out string key))
            {
                key = document.CreateType(villa.File) ?? throw new InvalidOperationException("plot " + villa.Number + ": the link " + villa.File + " did not load");
                created[villa.File] = key;
            }
            return key;
        }

        // R84, R404. Each element read back after its steps: a link by its transform within 1 mm, 0.01 degree and its mirror flag, a solid
        // by the outline of its base within 1 mm of the shape the plan gives, both ways
        private void ReadBack(Decision decision, Villa villa, string id)
        {
            bool mirrored = villa.Plot.Mirrored.Value.Value;
            double rotation = villa.Plot.RotationDeg.Value.Value;
            string mismatch;
            if (decision.Solids)
            {
                IReadOnlyList<double[]> read = document.ReadBackBase(id);
                double gap = read == null || read.Count < 3 ? double.PositiveInfinity : OutlineDistance.HausdorffMm(read, ShapeMath.Place(villa.Shape.Points, villa.Internal, rotation, mirrored), decision.Inputs.Config.FootprintSampleStepMm);
                double zGap = ZGapMm(read, villa.Internal[2]);
                mismatch = gap > Placer.ReadBackOrigin * 1000 ? "its base lies " + (double.IsInfinity(gap) ? "nowhere" : gap.ToString("0.###", CultureInfo.InvariantCulture) + " mm") + " from the plan"
                    : zGap > Placer.ReadBackOrigin * 1000 ? "its base is " + (double.IsInfinity(zGap) ? "at no Z" : zGap.ToString("0.###", CultureInfo.InvariantCulture) + " mm") + " off the FFL" : null;
            }
            else
            {
                mismatch = PlacementMath.Mismatch(document.ReadBackLink(id), villa.Internal, rotation, mirrored);
            }
            if (mismatch != null)
            {
                throw new InvalidOperationException("plot " + villa.Number + ": the placed villa does not match the plan, " + mismatch);
            }
        }

        // The largest gap of the corners of a base read back to the Z the plan gives, infinite for a base that gives no Z
        private static double ZGapMm(IReadOnlyList<double[]> read, double z)
        {
            if (read == null || read.Count == 0 || read.Any(point => point.Length < 3))
            {
                return double.PositiveInfinity;
            }
            return read.Max(point => Math.Abs(point[2] - z)) * 1000;
        }

        // R85, R107. The plot number written or the run fails, except a solid whose category button 1 did not bind it on, whose tag holds
        // the plot so no later run places it twice (CHOSEN, an open question of Lane B 5c). RRE_ZZ_SP_Prototype-FFL, Number-of-Steps and
        // RVT-Name are written here with the others, as Levels writes no link parameter (W6)
        private IEnumerable<string> WriteParameters(bool masses, Villa villa, string id, IReadOnlyDictionary<string, string> guids)
        {
            var issues = new List<string>();
            foreach (KeyValuePair<string, Sourced<string>> parameter in villa.Plot.Parameters ?? new Dictionary<string, Sourced<string>>())
            {
                if (parameter.Value?.Value == null)
                {
                    issues.Add("plot " + villa.Number + ": " + parameter.Key + " has no value in the plan");
                    continue;
                }
                ParameterWrite result = document.Write(id, guids.TryGetValue(parameter.Key, out string guid) ? guid : null, parameter.Value.Value);
                if (result == ParameterWrite.Written)
                {
                    continue;
                }
                string line = "plot " + villa.Number + ": " + parameter.Key + " was not written on the " + (masses ? "solid" : "link") + ", " + result;
                if (parameter.Key == Placer.PlotParameter && !(masses && result == ParameterWrite.Missing))
                {
                    throw new InvalidOperationException(line + ", a villa without its plot number would be placed again by the next run (R85)");
                }
                issues.Add(masses && parameter.Key == Placer.PlotParameter ? line + ", its category holds no binding of button 1, the tag holds the plot" : line);
            }
            if (villa.Plot.LinkName == null || string.IsNullOrWhiteSpace(villa.Plot.LinkName.Value))
            {
                issues.Add("plot " + villa.Number + ": the plan has no linkName, the villa keeps the name Revit gives it");
            }
            else
            {
                ParameterWrite named = document.WriteName(id, villa.Plot.LinkName.Value);
                if (named != ParameterWrite.Written)
                {
                    issues.Add("plot " + villa.Number + ": the name was not written, " + named);
                }
            }
            return issues;
        }

        // R368. A link holds what its transform reads now. A solid holds its placed values while its base lies within 1 mm of the base
        // they give, else the gap is a value of its own, so a solid moved by hand shows as changed by hand. A tag of 5c holds no z, its
        // villa stood at Z 0, so the Z is left out of what it holds now and the plan moves it to the FFL in place (W6)
        private static PlacedValues Current(Decision decision, VillaElement element, Dictionary<string, Villa> byPlot)
        {
            if (!element.IsSolid)
            {
                LinkTransform found = LinkMath.Describe(element.Transform.Origin, element.Transform.AxisX, element.Transform.AxisY, element.Transform.AxisZ);
                PlacedValues now = Values(found.Origin[0], found.Origin[1], found.Origin.Length > 2 ? found.Origin[2] : 0, found.RotationDeg, found.Mirrored);
                return element.Tag.Placed.Values.ContainsKey(ZValue) ? now : Without(now, ZValue);
            }
            PlacedValues placed = element.Tag.Placed;
            if (!decision.Solids || !byPlot.TryGetValue(element.Tag.Key.Plot, out Villa villa) || villa.Shape == null || element.Base == null
                || !placed.Values.TryGetValue("origin", out IReadOnlyList<double> origin) || !placed.Values.TryGetValue("rotationDeg", out IReadOnlyList<double> rotation) || !placed.Values.TryGetValue("mirrored", out IReadOnlyList<double> mirrored))
            {
                return placed;
            }
            double gap = element.Base.Count < 3 ? double.MaxValue : OutlineDistance.HausdorffMm(element.Base, ShapeMath.Place(villa.Shape.Points, origin.ToArray(), rotation[0], mirrored[0] != 0), decision.Inputs.Config.FootprintSampleStepMm);
            double zGap = placed.Values.TryGetValue(ZValue, out IReadOnlyList<double> z) ? ZGapMm(element.Base, z[0]) : 0;
            if (gap <= Placer.ReadBackOrigin * 1000 && zGap <= Placer.ReadBackOrigin * 1000)
            {
                return placed;
            }
            var values = placed.Values.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.Ordinal);
            values["baseOffMm"] = new[] { Math.Min(Math.Max(gap, zGap), 1e9) };
            return new PlacedValues(values);
        }

        // W6. The Z of the internal origin, a value of its own so a tag of 5c, which holds origin [x, y] alone, still reads
        private const string ZValue = "z";

        private static PlacedValues Values(double x, double y, double z, double rotationDeg, bool mirrored)
        {
            return new PlacedValues(new Dictionary<string, double[]>
            {
                ["origin"] = new[] { x, y }, [ZValue] = new[] { z }, ["rotationDeg"] = new[] { rotationDeg }, ["mirrored"] = new[] { mirrored ? 1.0 : 0.0 },
            });
        }

        private static PlacedValues Without(PlacedValues values, string name)
        {
            return new PlacedValues(values.Values.Where(item => item.Key != name).ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.Ordinal));
        }

        private static string Lacking(PlanPlot plot, string number)
        {
            var lacking = new List<string>();
            if (string.IsNullOrEmpty(number))
            {
                lacking.Add("plot");
            }
            if (plot.PrototypeFile == null || string.IsNullOrWhiteSpace(plot.PrototypeFile.Value))
            {
                lacking.Add("prototypeFile");
            }
            if (plot.Insert?.Value == null || plot.Insert.Value.Length != 3)
            {
                lacking.Add("insert");
            }
            if (plot.RotationDeg?.Value == null)
            {
                lacking.Add("rotationDeg");
            }
            if (plot.Mirrored?.Value == null)
            {
                lacking.Add("mirrored");
            }
            if (plot.Levels?.Ffl?.Value == null)
            {
                lacking.Add("levels.ffl");
            }
            if (lacking.Count > 0)
            {
                return "the plan lacks " + string.Join(", ", lacking);
            }
            if (plot.Parameters == null || !plot.Parameters.TryGetValue(Placer.PlotParameter, out Sourced<string> held) || held?.Value?.Trim() != number)
            {
                return "the plan has no " + Placer.PlotParameter + " equal to the plot";
            }
            return null;
        }

        private static string What(Step step)
        {
            switch (step.Act)
            {
                case Act.Place:
                    return "place";
                case Act.Swap:
                    return step.SolidIsLink ? "swap the mass link " + step.Solid : "swap the solid " + step.Solid + " for its link";
                case Act.Move:
                    return "move in place, " + string.Join(", ", step.Outcome.Differences);
                case Act.Keep:
                    return "already placed, skipped";
                default:
                    return "skip, " + step.Outcome.Case;
            }
        }

        private static IEnumerable<string> FootprintLines(Decision decision)
        {
            return decision.Footprint.Where(row => row.Flagged).Select(row => "plot " + row.Plot + ": " + row.Why + ", flagged (R404)");
        }

        // R404, R452. A solid or a mass link a masses run placed, by the part of its tag and its own UniqueId
        private static bool IsMass(VillaElement element)
        {
            return element.Tag != null && element.Tag.Key.Part == MassPart && string.Equals(element.UniqueId, element.Tag.PlacedId, StringComparison.Ordinal);
        }

        private static bool SamePath(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }

        // The tests of the Linux runner give a rooted folder with no drive, which Placer.IsFullPath, written for Windows, does not take
        private static bool UnixFull(string folder)
        {
            return Path.DirectorySeparatorChar == '/' && folder.StartsWith("/", StringComparison.Ordinal);
        }
    }
}
