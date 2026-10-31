using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ACadSharp;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace CadReader
{
    // R420, R421. The layer inventory of any CAD file the user gives: every insert exploded by the Exploder of R19 with no keep or drop
    // list, so no layer is dropped, then per layer its closed outlines, texts, inserts and line and arc segments, and the candidate faces of
    // each layer alone and of each pair of layers that both hold scan.pairMinSegments segments, written as scan_cad_<n>.json in the shape
    // of docs/design/17-scanner.md at the decimals of R2, so the C# and Python sides score the same numbers
    public static class CadScan
    {
        public const string CadRole = "scanCad:";
        public const string ExcelRole = "scanExcel:";
        public const string PdfRole = "scanPdf:";
        public const string ResultFile = "scan.json";

        public static string FileName(int n)
        {
            return "scan_cad_" + n.ToString(CultureInfo.InvariantCulture) + ".json";
        }

        private sealed class LayerParts
        {
            public readonly List<PlotShapes.Seg> Segments = new List<PlotShapes.Seg>();
            public readonly JArray Lines = new JArray();
            public readonly JArray Arcs = new JArray();
            public int Texts;
            public int Blocks;
        }

        private static double R(double value)
        {
            return JsonOut.Round3(value);
        }

        private static JArray Point(double x, double y)
        {
            return new JArray(R(x), R(y));
        }

        public static JObject Inventory(CadDocument document, string name, string path, long size, string sha256, int pairMinSegments)
        {
            // No plot line layer and no keep or drop list, so every piece reaches the sink and none is dropped (R420)
            var config = new LayerConfig { PlotLineLayers = new List<string>(), PlotLabelLayers = new List<string>() };
            var layers = new SortedDictionary<string, LayerParts>(StringComparer.Ordinal);
            Func<string, LayerParts> partsOf = layer =>
            {
                layer = layer ?? "";
                if (!layers.TryGetValue(layer, out LayerParts parts))
                {
                    layers[layer] = parts = new LayerParts();
                }
                return parts;
            };
            var exploder = new Exploder(config, piece => AddPiece(piece, partsOf), piece => { });
            exploder.Run(document.ModelSpace.Entities.ToList());
            foreach (ACadSharp.Tables.Layer layer in document.Layers)
            {
                partsOf(layer.Name);
            }
            var texts = new JArray();
            foreach (TextEntry text in exploder.AllTexts)
            {
                partsOf(text.Layer).Texts++;
                texts.Add(new JObject { ["layer"] = text.Layer ?? "", ["text"] = text.Text ?? "", ["x"] = R(text.X), ["y"] = R(text.Y) });
            }
            var inserts = new JArray();
            foreach (BlockEntry block in exploder.Blocks)
            {
                partsOf(block.Layer).Blocks++;
                inserts.Add(new JObject { ["layer"] = block.Layer ?? "", ["block"] = block.Name ?? "", ["x"] = R(block.InsertPoint[0]), ["y"] = R(block.InsertPoint[1]) });
            }
            var rows = new JArray();
            var candidates = new List<KeyValuePair<string, JObject>>();
            foreach (KeyValuePair<string, LayerParts> layer in layers)
            {
                JArray faces = Faces(layer.Value.Segments, layer.Key);
                rows.Add(new JObject
                {
                    ["layer"] = layer.Key, ["closedOutlines"] = faces.Count, ["texts"] = layer.Value.Texts, ["blocks"] = layer.Value.Blocks, ["segments"] = layer.Value.Segments.Count,
                });
                if (faces.Count > 0)
                {
                    candidates.Add(Candidate(new[] { layer.Key }, faces));
                }
            }
            List<string> large = layers.Where(layer => layer.Value.Segments.Count >= pairMinSegments).Select(layer => layer.Key).ToList();
            for (int first = 0; first < large.Count; first++)
            {
                for (int second = first + 1; second < large.Count; second++)
                {
                    List<PlotShapes.Seg> both = layers[large[first]].Segments.Concat(layers[large[second]].Segments).ToList();
                    candidates.Add(Candidate(new[] { large[first], large[second] }, Faces(both, large[first] + ScanLogic.LayerJoin + large[second])));
                }
            }
            var lines = new JObject();
            var arcs = new JObject();
            foreach (KeyValuePair<string, LayerParts> layer in layers.Where(layer => layer.Value.Segments.Count > 0))
            {
                lines[layer.Key] = layer.Value.Lines;
                arcs[layer.Key] = layer.Value.Arcs;
            }
            return new JObject
            {
                ["source"] = name,
                ["path"] = path,
                ["size"] = size,
                ["sha256"] = sha256,
                ["units"] = document.Header.InsUnits.ToString(),
                ["layers"] = rows,
                ["candidates"] = new JArray(candidates.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Value)),
                ["texts"] = texts,
                ["inserts"] = inserts,
                ["lines"] = lines,
                ["arcs"] = arcs,
            };
        }

        private static KeyValuePair<string, JObject> Candidate(string[] layers, JArray faces)
        {
            return new KeyValuePair<string, JObject>(string.Join(ScanLogic.LayerJoin, layers), new JObject { ["layers"] = new JArray(layers.Cast<object>().ToArray()), ["faces"] = faces });
        }

        // R421. The faces PlotShapes traces on the segments, each its ring with the arcs sampled as PlotLabels.Ring samples them, the centroid
        // of that ring and the area
        private static JArray Faces(List<PlotShapes.Seg> segments, string layer)
        {
            var faces = new JArray();
            if (segments.Count == 0)
            {
                return faces;
            }
            foreach (PlotEntry face in PlotShapes.Build(segments, layer).Where(shape => shape.Kind == "plot_shape"))
            {
                List<double[]> ring = PlotLabels.Ring(face);
                double[] centroid = Centroid(ring);
                faces.Add(new JObject
                {
                    ["ring"] = new JArray(ring.Select(point => (object)Point(point[0], point[1])).ToArray()),
                    ["centroid"] = Point(centroid[0], centroid[1]),
                    ["area"] = R(face.Area ?? 0),
                });
            }
            return faces;
        }

        // R424. The centroid of a ring taken from its first point. PlotLabels.Centroid multiplies site coordinates near 2.4e6 m, and on the
        // ROSHN baseplan its centroid of a 300 m2 face lay 0.27 m from the corner mean on net8.0 and 0.06 m on net48 (round 4j-1, Q251)
        public static double[] Centroid(List<double[]> ring)
        {
            double ox = ring[0][0];
            double oy = ring[0][1];
            var local = ring.Select(point => new[] { point[0] - ox, point[1] - oy }).ToList();
            double[] centre = PlotLabels.Centroid(local);
            return new[] { centre[0] + ox, centre[1] + oy };
        }

        // A line, an arc and each side of a polyline as a segment of its layer, a side of bulge as the arc it bends to. Circles, hatches,
        // ellipses and splines are no line or arc and trace no face here (R420)
        private static void AddPiece(Piece piece, Func<string, LayerParts> partsOf)
        {
            if (piece.Kind == "line" && piece.Pts != null && piece.Pts.Count == 2)
            {
                AddLine(partsOf(piece.Layer), piece.Pts[0][0], piece.Pts[0][1], piece.Pts[1][0], piece.Pts[1][1]);
            }
            else if (piece.Kind == "arc" && piece.Center != null && piece.Radius.HasValue)
            {
                AddArc(partsOf(piece.Layer), PlotShapes.Seg.FromPiece(piece));
            }
            else if (piece.Kind == "polyline" && piece.Pts != null && piece.Pts.Count >= 2)
            {
                LayerParts parts = partsOf(piece.Layer);
                // A 3d polyline holds x, y and z, the others x, y and the bulge of the side that starts at the vertex
                bool bulged = piece.Note != "3d";
                int sides = piece.Closed == true ? piece.Pts.Count : piece.Pts.Count - 1;
                for (int index = 0; index < sides; index++)
                {
                    double[] from = piece.Pts[index], to = piece.Pts[(index + 1) % piece.Pts.Count];
                    double bulge = bulged && from.Length > 2 ? from[2] : 0;
                    if (Math.Abs(bulge) < 1e-12)
                    {
                        AddLine(parts, from[0], from[1], to[0], to[1]);
                    }
                    else
                    {
                        PlotShapes.Seg arc = BulgeArc(from[0], from[1], to[0], to[1], bulge);
                        if (arc != null)
                        {
                            AddArc(parts, arc);
                        }
                    }
                }
            }
        }

        private static void AddLine(LayerParts parts, double x1, double y1, double x2, double y2)
        {
            if (x1 == x2 && y1 == y2)
            {
                return;
            }
            parts.Segments.Add(new PlotShapes.Seg { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 });
            parts.Lines.Add(new JArray(R(x1), R(y1), R(x2), R(y2)));
        }

        // The arc counter clockwise from A0 to A1, as PlotShapes takes it, written in degrees for the second method
        private static void AddArc(LayerParts parts, PlotShapes.Seg arc)
        {
            parts.Segments.Add(arc);
            parts.Arcs.Add(new JArray(R(arc.Cx), R(arc.Cy), R(arc.Radius), R(arc.A0 * 180 / Math.PI), R(arc.A1 * 180 / Math.PI)));
        }

        // The arc of a polyline side of bulge b, the tangent of a quarter of its sweep, counter clockwise for b above 0, as one counter
        // clockwise arc of PlotShapes from the start to the end, or from the end to the start for b under 0
        private static PlotShapes.Seg BulgeArc(double x1, double y1, double x2, double y2, double bulge)
        {
            double dx = x2 - x1, dy = y2 - y1;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            if (chord == 0)
            {
                return null;
            }
            double sweep = 4 * Math.Atan(Math.Abs(bulge));
            double radius = chord / (2 * Math.Sin(sweep / 2));
            double offset = Math.Sign(bulge) * (chord / 2) / Math.Tan(sweep / 2);
            double cx = (x1 + x2) / 2 - dy / chord * offset, cy = (y1 + y2) / 2 + dx / chord * offset;
            double start = Math.Atan2(y1 - cy, x1 - cx), end = Math.Atan2(y2 - cy, x2 - cx);
            return new PlotShapes.Seg { IsArc = true, Cx = cx, Cy = cy, Radius = radius, A0 = bulge > 0 ? start : end, A1 = bulge > 0 ? end : start };
        }

        private sealed class Read<T>
        {
            public T Value;
            public InputMark Mark;
        }

        // D19. Not an InvalidDataException, so no refusal of a file takes it and the run fails
        private sealed class InputChanged : Exception
        {
            public InputChanged(string message) : base(message)
            {
            }
        }

        // D19. A file read in place, marked before and held after, a change fails the run
        private static Read<T> Guarded<T>(string path, Func<InputMark, T> read)
        {
            InputMark before = InputGuard.Mark(path);
            T value = read(before);
            string changed = InputGuard.ChangedWhileRead(path, before);
            if (changed != null)
            {
                throw new InputChanged(changed);
            }
            return new Read<T> { Value = value, Mark = before };
        }

        private static JObject InputRow(string role, InputMark mark)
        {
            return new JObject { ["role"] = role, ["name"] = mark.Name, ["size"] = mark.Size, ["sha256"] = mark.Sha256 };
        }

        // The inputs of a role and a number from 1, by the number, a number that is not a whole number above 0 or is given twice refused
        private static List<KeyValuePair<int, WorkerFile>> Numbered(WorkerContext run, string prefix)
        {
            var found = new List<KeyValuePair<int, WorkerFile>>();
            foreach (WorkerFile input in run.Request.Inputs.Where(input => input.Role != null && input.Role.StartsWith(prefix, StringComparison.Ordinal)))
            {
                string text = input.Role.Substring(prefix.Length);
                if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < 1 || found.Any(item => item.Key == n))
                {
                    throw run.Refusal("the input " + input.Role + " is not " + prefix + "<n>, n a whole number from 1 given once (R427)");
                }
                found.Add(new KeyValuePair<int, WorkerFile>(n, input));
            }
            return found.OrderBy(item => item.Key).ToList();
        }

        // R420 to R427. The worker command scan: the config and the rules profile it names, each CAD file read to scan_cad_<n>.json, every
        // sheet of each Excel file, every page of each PDF file and the names of the prototype files, then scan.json, all into the output
        // folder of the request, each written by a temporary name the runner moves onto its name once every check passed
        public static void Command(WorkerContext run)
        {
            WorkerRequest request = run.Request;
            if (string.IsNullOrEmpty(request.OutputFolder))
            {
                throw run.Refusal("a scan names no output folder for scan.json (R427)");
            }
            string configPath = run.Input("config");
            Read<JObject> config = Guarded(configPath, _ => JObject.Parse(WorkFiles.ReadText(configPath)));
            string profileName = config.Value["profile"]?.Type == JTokenType.String ? (string)config.Value["profile"] : throw run.Refusal("config names no rules profile, profile (R277)");
            // The profile path of config is of the repo, the folder above the config folder, as python/project_profile.py reads it
            string profilePath = Path.IsPathRooted(profileName) ? profileName : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(configPath))), profileName);
            if (!File.Exists(profilePath))
            {
                throw run.Refusal("the rules profile config names is not on disk: " + profilePath);
            }
            Read<JObject> profile = Guarded(profilePath, _ => JObject.Parse(WorkFiles.ReadText(profilePath)));
            ScanSettings settings;
            HashSet<string> codes, pairs;
            List<WorkerFile> prototypes = request.Inputs.Where(input => input.Role != null && input.Role.StartsWith(BuildSteps.PrototypeRole, StringComparison.Ordinal))
                .OrderBy(input => input.Role, StringComparer.Ordinal).ToList();
            foreach (WorkerFile input in prototypes.Where(input => input.Role.Substring(BuildSteps.PrototypeRole.Length) != Path.GetFileName(input.Path)))
            {
                throw run.Refusal("the input " + input.Role + " is the file " + Path.GetFileName(input.Path) + ", a prototype file is named by its own file name (R425)");
            }
            try
            {
                settings = ScanSettings.From(profile.Value);
                codes = ScanLogic.KnownCodes(profile.Value, config.Value["prototypeNames"] as JObject, prototypes.Select(input => Path.GetFileName(input.Path)));
                pairs = ScanLogic.OldNewPairs(profile.Value);
            }
            catch (InvalidDataException refused)
            {
                throw run.Refusal(refused.Message);
            }
            var inputs = new JArray { InputRow("config", config.Mark) };
            var cads = new List<JObject>();
            foreach (KeyValuePair<int, WorkerFile> cad in Numbered(run, CadRole))
            {
                string path = run.Input(cad.Value.Role);
                var problems = new List<string>();
                Read<JObject> read = Guarded(path, mark => Inventory(CadExtractor.Open(path, problems), Path.GetFileName(path), path, mark.Size, mark.Sha256, settings.PairMinSegments));
                run.WriteJson(request.OutputFolder, FileName(cad.Key), read.Value);
                cads.Add(read.Value);
                inputs.Add(InputRow(cad.Value.Role, read.Mark));
                run.Counts["scan drawing " + cad.Key + " layers"] = ((JArray)read.Value["layers"]).Count;
                run.Counts["scan drawing " + cad.Key + " reader notes"] = problems.Count;
                WorkerMemory.Release();
            }
            var excels = new List<ScanWorkbook>();
            foreach (KeyValuePair<int, WorkerFile> excel in Numbered(run, ExcelRole))
            {
                string path = run.Input(excel.Value.Role);
                Read<ScanWorkbook> read;
                try
                {
                    read = Guarded(path, _ => ScanSources.Workbook(path));
                }
                catch (InvalidDataException refused)
                {
                    throw run.Refusal(Path.GetFileName(path) + ": " + refused.Message);
                }
                excels.Add(read.Value);
                inputs.Add(InputRow(excel.Value.Role, read.Mark));
            }
            var pdfs = new List<ScanPdf>();
            foreach (KeyValuePair<int, WorkerFile> pdf in Numbered(run, PdfRole))
            {
                string path = run.Input(pdf.Value.Role);
                Read<ScanPdf> read;
                try
                {
                    read = Guarded(path, _ => ScanSources.Pdf(path, settings.PdfJoinGapPt, settings.PdfLineTolerancePt));
                }
                catch (InvalidDataException refused)
                {
                    throw run.Refusal(Path.GetFileName(path) + ": " + refused.Message);
                }
                pdfs.Add(read.Value);
                inputs.Add(InputRow(pdf.Value.Role, read.Mark));
            }
            foreach (WorkerFile prototype in prototypes.OrderBy(input => Path.GetFileName(input.Path), StringComparer.Ordinal))
            {
                inputs.Add(InputRow(prototype.Role, InputGuard.Mark(prototype.Path)));
            }
            JObject result = ScanLogic.Result(settings, codes, pairs, cads, excels, pdfs, inputs);
            run.WriteJson(request.OutputFolder, ResultFile, result);
            run.Counts["scan rows"] = ((JArray)result["rows"]).Count;
            run.Counts["scan conflicts"] = ((JArray)result["conflicts"]).Count;
            run.Counts["scan checks"] = ((JArray)result["checks"]).Count;
            run.Counts["scan plots"] = ((JArray)result["plots"]).Count;
        }
    }
}
