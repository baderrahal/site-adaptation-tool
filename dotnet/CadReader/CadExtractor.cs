using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using SiteAdaptation.Common;

namespace CadReader
{
    public static class CadExtractor
    {
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
            var entities = document.ModelSpace.Entities.ToList();
            return new CadExtract
            {
                Layers = BuildLayers(document, entities, sourceName),
                Plots = new PlotsFile { Source = sourceName, Plots = BuildPlots(entities) },
                Texts = new TextsFile { Source = sourceName, Texts = BuildTexts(entities) },
            };
        }

        public static void WriteFiles(CadExtract extract, string outDir)
        {
            JsonOut.Write(Path.Combine(outDir, "layers.json"), extract.Layers);
            JsonOut.Write(Path.Combine(outDir, "plots.json"), extract.Plots);
            JsonOut.Write(Path.Combine(outDir, "texts.json"), extract.Texts);
        }

        private static LayersFile BuildLayers(CadDocument document, List<Entity> entities, string sourceName)
        {
            var byLayer = entities.GroupBy(LayerName).ToDictionary(group => group.Key, group => group.ToList());
            var layers = new List<LayerEntry>();
            foreach (var layer in document.Layers)
            {
                List<Entity> own;
                if (!byLayer.TryGetValue(layer.Name, out own))
                {
                    own = new List<Entity>();
                }
                layers.Add(new LayerEntry
                {
                    Name = layer.Name,
                    Entities = own.Count,
                    ByType = own.GroupBy(entity => entity.ObjectName).OrderBy(group => group.Key).ToDictionary(group => group.Key, group => group.Count()),
                });
            }
            return new LayersFile { Source = sourceName, Units = document.Header.InsUnits.ToString(), Layers = layers };
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

        private static List<TextEntry> BuildTexts(List<Entity> entities)
        {
            var texts = new List<TextEntry>();
            foreach (var entity in entities)
            {
                var mtext = entity as MText;
                if (mtext != null)
                {
                    texts.Add(Text("mtext", entity, MTextCleaner.Plain(mtext.Value), mtext.InsertPoint));
                    continue;
                }
                var text = entity as TextEntity;
                if (text != null && !(entity is AttributeBase))
                {
                    texts.Add(Text("text", entity, text.Value, text.InsertPoint));
                }
            }
            return texts;
        }

        private static TextEntry Text(string kind, Entity entity, string value, CSMath.XYZ point)
        {
            return new TextEntry { Kind = kind, Layer = LayerName(entity), Text = value, X = point.X, Y = point.Y, Z = point.Z };
        }

        private static string LayerName(Entity entity)
        {
            return entity.Layer == null ? "0" : entity.Layer.Name;
        }
    }
}
