using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.IO;
using SiteAdaptation.Common;

namespace CadReader
{
    public static class CadExtractor
    {
        private const int NoteGroups = 40;
        private const int NoteKeyLength = 60;
        private const int NoteTextLength = 200;

        public static CadDocument Open(string path, List<string> problems)
        {
            NotificationEventHandler note = (sender, args) =>
            {
                if (args.NotificationType == NotificationType.Error || args.NotificationType == NotificationType.NotImplemented)
                {
                    problems.Add(args.NotificationType + ": " + args.Message);
                }
            };

            string extension = Path.GetExtension(path).ToLowerInvariant();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (extension == ".dwg")
                {
                    return DwgReader.Read(stream, note);
                }
                if (extension == ".dxf")
                {
                    return DxfReader.Read(stream, note);
                }
            }
            throw new ArgumentException("Only .dwg and .dxf files are supported, got " + extension);
        }

        public static CadExtract Extract(CadDocument document, string sourceName)
        {
            return Extract(document, sourceName, null, null, null, null);
        }

        // With a sink, pieces are handed over one by one and extract.Pieces stays empty. Without one they are kept in extract.Pieces.
        public static CadExtract Extract(CadDocument document, string sourceName, LayerConfig config, Action<Piece> sink, string sourcePath, List<string> problems)
        {
            config = config ?? new LayerConfig();
            var entities = document.ModelSpace.Entities.ToList();
            var kept = sink == null ? new List<Piece>() : null;
            var segments = new List<PlotShapes.Seg>();
            var exploder = new Exploder(config, sink ?? (piece => kept.Add(piece)), piece => segments.Add(PlotShapes.Seg.FromPiece(piece)));
            exploder.Run(entities);

            var geometry = PlotShapes.Build(segments, config.PlotLineName);
            var faces = geometry.Where(shape => shape.Kind == "plot_shape").ToList();
            var join = PlotLabels.Join(faces, exploder.AllTexts, config);

            var plots = BuildPlots(entities);
            plots.AddRange(join.Plots);
            plots.AddRange(geometry.Where(shape => shape.Kind != "plot_shape"));

            return new CadExtract
            {
                Layers = BuildLayers(document, entities, sourceName, config, exploder, sourcePath, problems),
                Plots = new PlotsFile { Source = sourceName, Summary = join.Summary, Plots = plots },
                OtherFaces = new OtherFacesFile { Source = sourceName, Faces = join.Other },
                Texts = new TextsFile { Source = sourceName, Texts = exploder.KeptTexts },
                Blocks = new BlocksFile { Source = sourceName, Blocks = exploder.Blocks },
                Pieces = kept == null ? null : new PiecesFile { Source = sourceName, Pieces = kept },
            };
        }

        public static void WriteFiles(CadExtract extract, string outDir)
        {
            JsonOut.Write(Path.Combine(outDir, "layers.json"), extract.Layers);
            JsonOut.Write(Path.Combine(outDir, "plots.json"), extract.Plots);
            JsonOut.Write(Path.Combine(outDir, "other_faces.json"), extract.OtherFaces);
            JsonOut.Write(Path.Combine(outDir, "texts.json"), extract.Texts);
            JsonOut.Write(Path.Combine(outDir, "blocks.json"), extract.Blocks);
            if (extract.Pieces != null)
            {
                JsonOut.Write(Path.Combine(outDir, "pieces.json"), extract.Pieces);
            }
        }

        private static LayersFile BuildLayers(CadDocument document, List<Entity> entities, string sourceName, LayerConfig config, Exploder exploder, string sourcePath, List<string> problems)
        {
            var byLayer = entities.GroupBy(LayerName).ToDictionary(group => group.Key, group => group.ToList());
            var names = document.Layers.Select(layer => layer.Name).ToList();
            names.AddRange(exploder.After.Keys.Where(name => !names.Contains(name, StringComparer.OrdinalIgnoreCase)));
            var layers = new List<LayerEntry>();
            foreach (string name in names)
            {
                List<Entity> own;
                if (!byLayer.TryGetValue(name, out own))
                {
                    own = new List<Entity>();
                }
                SortedDictionary<string, int> after;
                if (!exploder.After.TryGetValue(name, out after))
                {
                    after = new SortedDictionary<string, int>();
                }
                layers.Add(new LayerEntry
                {
                    Name = name,
                    Status = config.Status(name),
                    Entities = own.Count,
                    ByType = own.GroupBy(entity => entity.ObjectName).OrderBy(group => group.Key).ToDictionary(group => group.Key, group => group.Count()),
                    After = after.Values.Sum(),
                    AfterByType = after.ToDictionary(pair => pair.Key, pair => pair.Value),
                });
            }
            return new LayersFile
            {
                Source = sourceName,
                Units = document.Header.InsUnits.ToString(),
                Totals = new TotalsEntry
                {
                    Before = entities.Count,
                    After = layers.Sum(layer => layer.After),
                    Inserts = exploder.Inserts,
                    MaxDepth = exploder.MaxDepth,
                    Written = exploder.Written,
                    Dropped = exploder.Dropped,
                    DepthLimitHits = exploder.DepthLimitHits,
                    Cycles = exploder.Cycles,
                    ArrayInserts = exploder.ArrayInserts,
                    NoBlock = exploder.NoBlock,
                },
                Extents = exploder.Extents(),
                Xrefs = BuildXrefs(document, exploder, sourcePath),
                Notes = BuildNotes(problems),
                Classes = BuildClasses(document),
                Layers = layers,
            };
        }

        private static List<ClassEntry> BuildClasses(CadDocument document)
        {
            return document.Classes.Select(entry => new ClassEntry
            {
                DxfName = entry.DxfName,
                CppClassName = entry.CppClassName,
                ApplicationName = entry.ApplicationName,
                InstanceCount = entry.InstanceCount,
                IsAnEntity = entry.IsAnEntity,
            }).OrderBy(entry => entry.DxfName, StringComparer.Ordinal).ToList();
        }

        private static List<XrefEntry> BuildXrefs(CadDocument document, Exploder exploder, string sourcePath)
        {
            string folder = string.IsNullOrEmpty(sourcePath) ? null : Path.GetDirectoryName(Path.GetFullPath(sourcePath));
            var xrefs = new List<XrefEntry>();
            foreach (var block in document.BlockRecords.Where(record => (record.Flags & BlockTypeFlags.XRef) != 0))
            {
                string path = block.BlockEntity == null ? null : block.BlockEntity.XRefPath;
                int inserts;
                exploder.XrefInserts.TryGetValue(block.Name, out inserts);
                xrefs.Add(new XrefEntry { Name = block.Name, Path = path, Found = XrefFound(folder, block.Name, path), Inserts = inserts });
            }
            return xrefs;
        }

        // Found means the saved path exists or a drawing of the same name sits next to the file that was read
        private static bool XrefFound(string folder, string name, string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Path.IsPathRooted(path) && File.Exists(path))
                {
                    return true;
                }
                if (folder == null)
                {
                    return false;
                }
                string file = string.IsNullOrEmpty(path) ? name : Path.GetFileName(path);
                string stem = Path.GetFileNameWithoutExtension(file);
                return File.Exists(Path.Combine(folder, file)) || File.Exists(Path.Combine(folder, stem + ".dwg"));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static List<NoteEntry> BuildNotes(List<string> problems)
        {
            if (problems == null)
            {
                return new List<NoteEntry>();
            }
            return problems
                .GroupBy(text => text.Length > NoteKeyLength ? text.Substring(0, NoteKeyLength) : text)
                .Select(group => new NoteEntry { Text = Shorten(group.First()), Count = group.Count() })
                .OrderByDescending(note => note.Count)
                .Take(NoteGroups)
                .ToList();
        }

        private static string Shorten(string text)
        {
            return text.Length > NoteTextLength ? text.Substring(0, NoteTextLength) : text;
        }

        private static List<PlotEntry> BuildPlots(List<Entity> entities)
        {
            var plots = new List<PlotEntry>();
            foreach (var entity in entities)
            {
                var lw = entity as LwPolyline;
                if (lw != null)
                {
                    plots.Add(Polyline(entity, lw.IsClosed, lw.Vertices.Select(v => new[] { v.Location.X, v.Location.Y }).ToList()));
                    continue;
                }
                var flat = entity as Polyline2D;
                if (flat != null)
                {
                    plots.Add(Polyline(entity, flat.IsClosed, flat.Vertices.Select(v => new[] { v.Location.X, v.Location.Y }).ToList()));
                    continue;
                }
                var solid = entity as Polyline3D;
                if (solid != null)
                {
                    plots.Add(Polyline(entity, solid.IsClosed, solid.Vertices.Select(v => new[] { v.Location.X, v.Location.Y, v.Location.Z }).ToList()));
                    continue;
                }
                var insert = entity as Insert;
                if (insert != null)
                {
                    plots.Add(new PlotEntry
                    {
                        Kind = "block",
                        Layer = LayerName(entity),
                        Block = insert.Block == null ? null : insert.Block.Name,
                        InsertPoint = new[] { insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z },
                        RotationDeg = insert.Rotation * 180.0 / Math.PI,
                        Attributes = Attributes(insert),
                    });
                }
            }
            return plots;
        }

        private static PlotEntry Polyline(Entity entity, bool closed, List<double[]> vertices)
        {
            return new PlotEntry { Kind = "polyline", Layer = LayerName(entity), Closed = closed, Vertices = vertices };
        }

        // The same tag can repeat on one block, so the last value wins and nothing throws
        private static Dictionary<string, string> Attributes(Insert insert)
        {
            if (insert.Attributes == null || insert.Attributes.Count == 0)
            {
                return null;
            }
            var attributes = new Dictionary<string, string>();
            foreach (var attribute in insert.Attributes)
            {
                attributes[attribute.Tag ?? ""] = attribute.Value;
            }
            return attributes;
        }

        private static string LayerName(Entity entity)
        {
            return entity.Layer == null ? "0" : entity.Layer.Name;
        }
    }
}
