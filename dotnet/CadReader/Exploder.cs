using System;
using System.Collections.Generic;
using System.Linq;
using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.Objects;
using ACadSharp.Tables;
using CSMath;

namespace CadReader
{
    // Walks model space and explodes every block reference, nested ones too, down to leaf geometry.
    // Each leaf is counted by the layer it really sits on, where layer 0 inside a block takes the layer of its insert.
    // Pieces on dropped layers are counted and not written. Children of an insert on a dropped layer are dropped with it.
    public sealed class Exploder
    {
        public const int DepthLimit = 16;
        private const int XdataLimit = 50;
        private const double ArcStep = Math.PI / 18;

        private readonly LayerConfig config;
        private readonly Action<Piece> sink;
        private readonly Action<Piece> plotGeometry;

        private readonly List<double> coreX = new List<double>();
        private readonly List<double> coreY = new List<double>();
        private readonly double[] box = { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue };
        private readonly Dictionary<string, List<XYZ>> hulls = new Dictionary<string, List<XYZ>>();

        public readonly SortedDictionary<string, SortedDictionary<string, int>> After = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> XrefInserts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<TextEntry> AllTexts = new List<TextEntry>();
        public readonly List<TextEntry> KeptTexts = new List<TextEntry>();
        public readonly List<BlockEntry> Blocks = new List<BlockEntry>();
        public int Inserts;
        public int MaxDepth;
        public int Written;
        public int Dropped;
        public int DepthLimitHits;
        public int Cycles;
        public int ArrayInserts;
        public int NoBlock;
        public int Points;

        public Exploder(LayerConfig config, Action<Piece> sink, Action<Piece> plotGeometry)
        {
            this.config = config;
            this.sink = sink;
            this.plotGeometry = plotGeometry;
        }

        public void Run(IEnumerable<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                Visit(entity, new List<Transform>(), new List<ulong>(), new List<string>(), null, false);
            }
        }

        public ExtentsEntry Extents()
        {
            if (Points == 0)
            {
                return new ExtentsEntry { Points = 0 };
            }
            return new ExtentsEntry
            {
                Points = Points,
                Kept = (double[])box.Clone(),
                Core = new[] { Quantile(coreX, 0.005), Quantile(coreY, 0.005), Quantile(coreX, 0.995), Quantile(coreY, 0.995) },
            };
        }

        private static double Quantile(List<double> values, double fraction)
        {
            var sorted = values.OrderBy(value => value).ToList();
            return sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(fraction * sorted.Count))];
        }

        private void Visit(Entity entity, List<Transform> chain, List<ulong> path, List<string> blocks, string inherited, bool suppressed)
        {
            var insert = entity as Insert;
            if (insert != null)
            {
                VisitInsert(insert, chain, path, blocks, inherited, suppressed);
                return;
            }
            string layer = EffectiveLayer(entity, inherited);
            Count(layer, entity.ObjectName);
            Leaf(entity, layer, chain, path, suppressed || config.Status(layer) == LayerConfig.Drop);
        }

        private void VisitInsert(Insert insert, List<Transform> chain, List<ulong> path, List<string> blocks, string inherited, bool suppressed)
        {
            string layer = EffectiveLayer(insert, inherited);
            bool drop = suppressed || config.Status(layer) == LayerConfig.Drop;
            Inserts++;
            var block = insert.Block;
            var piece = new Piece
            {
                Kind = "insert",
                Layer = layer,
                Handle = Hex(insert.Handle),
                Path = PathText(path),
                Name = block == null ? null : block.Name,
                Pts = new List<double[]> { Pt(World(chain, ToWcs(insert.InsertPoint, insert.Normal))) },
                Attrs = Attributes(insert),
                Xdata = Xdata(insert),
            };
            var notes = new List<string>();
            if (insert.IsMultiple)
            {
                ArrayInserts++;
                notes.Add("array insert, first cell only");
            }
            if (block == null)
            {
                NoBlock++;
                notes.Add("no block");
            }
            else if ((block.Flags & BlockTypeFlags.XRef) != 0)
            {
                int seen;
                XrefInserts.TryGetValue(block.Name, out seen);
                XrefInserts[block.Name] = seen + 1;
                notes.Add("xref");
            }
            else if (block.IsAnonymous)
            {
                notes.Add("anonymous");
            }
            if (notes.Count > 0)
            {
                piece.Note = string.Join(", ", notes);
            }
            if (!drop)
            {
                Emit(piece);
                AddAttributeTexts(insert, layer, chain, path);
                Blocks.Add(BlockOf(insert, layer, chain, path, piece.Note));
            }
            else
            {
                Dropped++;
            }

            if (block == null || (block.Flags & BlockTypeFlags.XRef) != 0)
            {
                return;
            }
            if (chain.Count >= DepthLimit)
            {
                DepthLimitHits++;
                return;
            }
            if (blocks.Contains(block.Name))
            {
                Cycles++;
                return;
            }
            chain.Add(insert.GetTransform());
            path.Add(insert.Handle);
            blocks.Add(block.Name);
            MaxDepth = Math.Max(MaxDepth, chain.Count);
            foreach (Entity child in block.Entities)
            {
                Visit(child, chain, path, blocks, layer, drop);
            }
            blocks.RemoveAt(blocks.Count - 1);
            path.RemoveAt(path.Count - 1);
            chain.RemoveAt(chain.Count - 1);
        }

        private BlockEntry BlockOf(Insert insert, string layer, List<Transform> chain, List<ulong> path, string note)
        {
            var block = insert.Block;
            var entry = new BlockEntry
            {
                Name = block == null ? null : block.Name,
                Layer = layer,
                Handle = Hex(insert.Handle),
                Path = PathText(path),
                InsertPoint = Pt(World(chain, ToWcs(insert.InsertPoint, insert.Normal))),
                Scale = new[] { insert.XScale, insert.YScale, insert.ZScale },
                RotationDeg = insert.Rotation * 180.0 / Math.PI,
                Mirrored = insert.XScale * insert.YScale < 0,
                ChainMirrored = Mirrored(chain) ? (bool?)true : null,
                Note = note,
            };
            if (block != null && (block.Flags & BlockTypeFlags.XRef) == 0)
            {
                var hull = LocalHull(block);
                if (hull.Count >= 3)
                {
                    var full = new List<Transform>(chain) { insert.GetTransform() };
                    var ring = hull.Select(point => { XYZ world = World(full, point); return new[] { world.X, world.Y }; }).ToList();
                    // A flip turns the ring the other way round, so it is turned back to counter clockwise
                    if (Mirrored(full))
                    {
                        ring.Reverse();
                    }
                    entry.Outline = ring;
                }
            }
            return entry;
        }

        // The outline of a block is the convex hull of its drawn content in the block's own coordinates.
        // One hull per block name, since an insert only moves, turns and scales it.
        private List<XYZ> LocalHull(BlockRecord block)
        {
            List<XYZ> hull;
            if (hulls.TryGetValue(block.Name, out hull))
            {
                return hull;
            }
            var points = new List<XYZ>();
            CollectPoints(block, new List<Transform>(), new List<string> { block.Name }, points);
            hull = ConvexHull(points);
            hulls[block.Name] = hull;
            return hull;
        }

        private void CollectPoints(BlockRecord block, List<Transform> local, List<string> names, List<XYZ> points)
        {
            foreach (Entity child in block.Entities)
            {
                if (config.Status(EffectiveLayer(child, null)) == LayerConfig.Drop)
                {
                    continue;
                }
                var nested = child as Insert;
                if (nested != null)
                {
                    var inner = nested.Block;
                    if (inner == null || (inner.Flags & BlockTypeFlags.XRef) != 0 || local.Count >= DepthLimit || names.Contains(inner.Name))
                    {
                        continue;
                    }
                    local.Add(nested.GetTransform());
                    names.Add(inner.Name);
                    CollectPoints(inner, local, names, points);
                    names.RemoveAt(names.Count - 1);
                    local.RemoveAt(local.Count - 1);
                    continue;
                }
                Piece piece = Geometry(child, local);
                if (piece != null && piece.Kind != "point")
                {
                    AddOutlinePoints(piece, points);
                }
            }
        }

        // Only x and y count. Arcs add their end points and circles their four extremes, which is enough for an outline.
        private static void AddOutlinePoints(Piece piece, List<XYZ> points)
        {
            Action<double[]> add = point =>
            {
                if (point != null && point.Length >= 2 && !double.IsNaN(point[0]) && !double.IsNaN(point[1]) && !double.IsInfinity(point[0]) && !double.IsInfinity(point[1]))
                {
                    points.Add(new XYZ(point[0], point[1], 0));
                }
            };
            if (piece.Pts != null)
            {
                piece.Pts.ForEach(add);
            }
            if (piece.Loops != null)
            {
                piece.Loops.ForEach(loop => loop.ForEach(add));
            }
            if (piece.Kind == "circle" && piece.Center != null && piece.Radius.HasValue)
            {
                double x = piece.Center[0], y = piece.Center[1], r = piece.Radius.Value;
                add(new[] { x + r, y });
                add(new[] { x - r, y });
                add(new[] { x, y + r });
                add(new[] { x, y - r });
            }
            if (piece.Kind == "arc")
            {
                add(piece.Start);
                add(piece.End);
            }
            if (piece.Kind == "ellipse" && piece.Center != null && piece.Start != null)
            {
                add(piece.Start);
                add(new[] { 2 * piece.Center[0] - piece.Start[0], 2 * piece.Center[1] - piece.Start[1] });
            }
        }

        // Counter clockwise, no repeated first point. Fewer than three corners gives an empty list.
        private static List<XYZ> ConvexHull(List<XYZ> points)
        {
            var sorted = points.OrderBy(point => point.X).ThenBy(point => point.Y).ToList();
            var unique = new List<XYZ>();
            foreach (XYZ point in sorted)
            {
                if (unique.Count == 0 || point.X != unique[unique.Count - 1].X || point.Y != unique[unique.Count - 1].Y)
                {
                    unique.Add(point);
                }
            }
            if (unique.Count < 3)
            {
                return new List<XYZ>();
            }
            var hull = new List<XYZ>();
            foreach (XYZ point in unique)
            {
                while (hull.Count >= 2 && Turn(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(point);
            }
            int lower = hull.Count + 1;
            for (int index = unique.Count - 2; index >= 0; index--)
            {
                while (hull.Count >= lower && Turn(hull[hull.Count - 2], hull[hull.Count - 1], unique[index]) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(unique[index]);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull.Count >= 3 ? hull : new List<XYZ>();
        }

        private static double Turn(XYZ origin, XYZ a, XYZ b)
        {
            return (a.X - origin.X) * (b.Y - origin.Y) - (a.Y - origin.Y) * (b.X - origin.X);
        }

        private void AddAttributeTexts(Insert insert, string layer, List<Transform> chain, List<ulong> path)
        {
            if (insert.Attributes == null)
            {
                return;
            }
            foreach (var attribute in insert.Attributes)
            {
                if (string.IsNullOrEmpty(attribute.Value))
                {
                    continue;
                }
                XYZ at = World(chain, ToWcs(attribute.InsertPoint, attribute.Normal));
                AddText(new TextEntry
                {
                    Kind = "attrib",
                    Layer = layer,
                    Text = attribute.Value,
                    X = at.X,
                    Y = at.Y,
                    Z = at.Z,
                    Handle = Hex(attribute.Handle),
                    Path = PathText(path),
                    Tag = attribute.Tag,
                }, false);
            }
        }

        private void Leaf(Entity entity, string layer, List<Transform> chain, List<ulong> path, bool drop)
        {
            if (entity is AttributeDefinition)
            {
                return;
            }
            bool isPlotLine = config.IsPlotLine(layer) && (entity is Line || entity is Arc);
            bool isText = entity is MText || entity is MultiLeader || (entity is TextEntity && !(entity is AttributeBase));
            if (drop && !isPlotLine && !(isText && config.IsPlotLabel(layer)))
            {
                Dropped++;
                return;
            }

            string handle = Hex(entity.Handle);
            string chainPath = PathText(path);
            if (isText)
            {
                TextEntry text = Text(entity, layer, chain, handle, chainPath);
                if (text != null)
                {
                    AddText(text, drop);
                }
                return;
            }

            Piece piece = Geometry(entity, chain);
            if (piece == null)
            {
                if (drop)
                {
                    Dropped++;
                }
                return;
            }
            piece.Layer = layer;
            piece.Handle = handle;
            piece.Path = chainPath;
            piece.Xdata = drop ? null : Xdata(entity);
            if (isPlotLine)
            {
                plotGeometry(piece);
            }
            if (drop)
            {
                Dropped++;
                return;
            }
            Emit(piece);
        }

        private void AddText(TextEntry text, bool drop)
        {
            AllTexts.Add(text);
            if (!drop)
            {
                KeptTexts.Add(text);
            }
        }

        private TextEntry Text(Entity entity, string layer, List<Transform> chain, string handle, string path)
        {
            var mtext = entity as MText;
            if (mtext != null)
            {
                return Located("mtext", mtext.InsertPoint, mtext.Normal, MTextCleaner.Plain(mtext.Value), layer, chain, handle, path, mtext.Height);
            }
            var leader = entity as MultiLeader;
            if (leader != null)
            {
                var context = leader.ContextData;
                if (context == null || string.IsNullOrWhiteSpace(context.TextLabel))
                {
                    return null;
                }
                return Located("mleader", context.TextLocation, context.TextNormal, MTextCleaner.Plain(context.TextLabel), layer, chain, handle, path, context.TextHeight);
            }
            var plain = (TextEntity)entity;
            return Located("text", plain.InsertPoint, plain.Normal, plain.Value, layer, chain, handle, path, plain.Height);
        }

        private static TextEntry Located(string kind, XYZ point, XYZ normal, string value, string layer, List<Transform> chain, string handle, string path, double height)
        {
            XYZ at = World(chain, ToWcs(point, normal));
            return new TextEntry
            {
                Kind = kind,
                Layer = layer,
                Text = value,
                X = at.X,
                Y = at.Y,
                Z = at.Z,
                Handle = handle,
                Path = path,
                Height = height > 0 ? (double?)height : null,
            };
        }

        private Piece Geometry(Entity entity, List<Transform> chain)
        {
            bool flipped = Mirrored(chain);
            var arc = entity as Arc;
            if (arc != null)
            {
                return ArcPiece(arc, chain, flipped);
            }
            var circle = entity as Circle;
            if (circle != null)
            {
                XYZ center = World(chain, ToWcs(circle.Center, circle.Normal));
                XYZ edge = World(chain, ToWcs(circle.Center + new XYZ(circle.Radius, 0, 0), circle.Normal));
                return new Piece { Kind = "circle", Center = Pt(center), Radius = Distance(center, edge) };
            }
            var line = entity as Line;
            if (line != null)
            {
                return new Piece { Kind = "line", Pts = new List<double[]> { Pt(World(chain, line.StartPoint)), Pt(World(chain, line.EndPoint)) } };
            }
            var lw = entity as LwPolyline;
            if (lw != null)
            {
                bool mirror = flipped ^ (lw.Normal.Z < 0);
                var vertices = lw.Vertices.Select(v => Vertex(chain, lw.Normal, new XYZ(v.Location.X, v.Location.Y, lw.Elevation), v.Bulge, mirror)).ToList();
                return Polyline(vertices, lw.IsClosed, chain, lw.Normal, lw.Elevation);
            }
            var flat = entity as Polyline2D;
            if (flat != null)
            {
                bool mirror = flipped ^ (flat.Normal.Z < 0);
                var vertices = flat.Vertices.Select(v => Vertex(chain, flat.Normal, new XYZ(v.Location.X, v.Location.Y, flat.Elevation), v.Bulge, mirror)).ToList();
                return Polyline(vertices, flat.IsClosed, chain, flat.Normal, flat.Elevation);
            }
            var solid = entity as Polyline3D;
            if (solid != null)
            {
                return new Piece { Kind = "polyline", Closed = solid.IsClosed, Pts = solid.Vertices.Select(v => Pt(World(chain, v.Location))).ToList(), Note = "3d" };
            }
            var hatch = entity as Hatch;
            if (hatch != null)
            {
                return HatchPiece(hatch, chain);
            }
            var point = entity as Point;
            if (point != null)
            {
                return new Piece { Kind = "point", Pts = new List<double[]> { Pt(World(chain, point.Location)) } };
            }
            var ellipse = entity as Ellipse;
            if (ellipse != null)
            {
                XYZ center = World(chain, ToWcs(ellipse.Center, ellipse.Normal));
                XYZ major = World(chain, ToWcs(ellipse.Center + ellipse.MajorAxisEndPoint, ellipse.Normal));
                return new Piece { Kind = "ellipse", Center = Pt(center), Start = Pt(major), Ratio = ellipse.RadiusRatio };
            }
            var spline = entity as Spline;
            if (spline != null)
            {
                var source = spline.ControlPoints.Count > 0 ? spline.ControlPoints : spline.FitPoints;
                return new Piece { Kind = "spline", Pts = source.Select(p => Pt(World(chain, p))).ToList(), Note = spline.ControlPoints.Count > 0 ? "control points" : "fit points" };
            }
            return null;
        }

        private static Piece ArcPiece(Arc arc, List<Transform> chain, bool flipped)
        {
            XYZ center = ToWcs(arc.Center, arc.Normal);
            XYZ start = ToWcs(arc.Center + new XYZ(arc.Radius * Math.Cos(arc.StartAngle), arc.Radius * Math.Sin(arc.StartAngle), 0), arc.Normal);
            XYZ end = ToWcs(arc.Center + new XYZ(arc.Radius * Math.Cos(arc.EndAngle), arc.Radius * Math.Sin(arc.EndAngle), 0), arc.Normal);
            XYZ worldCenter = World(chain, center);
            XYZ worldStart = World(chain, start);
            return new Piece
            {
                Kind = "arc",
                Center = Pt(worldCenter),
                Radius = Distance(worldCenter, worldStart),
                Start = Pt(worldStart),
                End = Pt(World(chain, end)),
                Ccw = !(flipped ^ (arc.Normal.Z < 0)),
            };
        }

        private static double[] Vertex(List<Transform> chain, XYZ normal, XYZ local, double bulge, bool mirror)
        {
            XYZ world = World(chain, ToWcs(local, normal));
            return new[] { world.X, world.Y, mirror ? -bulge : bulge };
        }

        private static Piece Polyline(List<double[]> vertices, bool closed, List<Transform> chain, XYZ normal, double elevation)
        {
            double z = vertices.Count == 0 ? 0 : World(chain, ToWcs(new XYZ(0, 0, elevation), normal)).Z;
            return new Piece { Kind = "polyline", Closed = closed, Pts = vertices, Elev = Math.Abs(z) > 1e-9 ? (double?)z : null };
        }

        private static Piece HatchPiece(Hatch hatch, List<Transform> chain)
        {
            var loops = new List<List<double[]>>();
            bool sampled = false;
            Func<double, double, double[]> place = (x, y) =>
            {
                XYZ world = World(chain, ToWcs(new XYZ(x, y, hatch.Elevation), hatch.Normal));
                return new[] { world.X, world.Y };
            };
            foreach (var path in hatch.Paths)
            {
                var loop = new List<double[]>();
                foreach (var edge in path.Edges)
                {
                    var polyline = edge as Hatch.BoundaryPath.Polyline;
                    if (polyline != null)
                    {
                        var bulges = polyline.HasBulge ? polyline.Bulges.ToList() : null;
                        for (int index = 0; index < polyline.Vertices.Count; index++)
                        {
                            double[] at = place(polyline.Vertices[index].X, polyline.Vertices[index].Y);
                            double bulge = bulges != null && index < bulges.Count ? bulges[index] : 0;
                            loop.Add(new[] { at[0], at[1], bulge });
                        }
                        continue;
                    }
                    var segment = edge as Hatch.BoundaryPath.Line;
                    if (segment != null)
                    {
                        loop.Add(place(segment.Start.X, segment.Start.Y));
                        loop.Add(place(segment.End.X, segment.End.Y));
                        continue;
                    }
                    var circular = edge as Hatch.BoundaryPath.Arc;
                    if (circular != null)
                    {
                        sampled = true;
                        double from = circular.StartAngle;
                        double to = circular.EndAngle;
                        if (circular.CounterClockWise)
                        {
                            while (to <= from) { to += 2 * Math.PI; }
                        }
                        else
                        {
                            while (to >= from) { to -= 2 * Math.PI; }
                        }
                        int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(to - from) / ArcStep));
                        for (int step = 0; step <= steps; step++)
                        {
                            double angle = from + (to - from) * step / steps;
                            loop.Add(place(circular.Center.X + circular.Radius * Math.Cos(angle), circular.Center.Y + circular.Radius * Math.Sin(angle)));
                        }
                        continue;
                    }
                    var elliptic = edge as Hatch.BoundaryPath.Ellipse;
                    if (elliptic != null)
                    {
                        sampled = true;
                        double ratio = elliptic.MajorAxis > 0 ? elliptic.MinorAxis / elliptic.MajorAxis : 1;
                        double tilt = Math.Atan2(elliptic.MajorAxisEndPoint.Y, elliptic.MajorAxisEndPoint.X);
                        double from = elliptic.StartAngle;
                        double to = elliptic.EndAngle;
                        if (to <= from) { to += 2 * Math.PI; }
                        int steps = Math.Max(8, (int)Math.Ceiling(Math.Abs(to - from) / ArcStep));
                        for (int step = 0; step <= steps; step++)
                        {
                            double angle = from + (to - from) * step / steps;
                            double ex = elliptic.MajorAxis * Math.Cos(angle);
                            double ey = elliptic.MinorAxis * Math.Sin(angle);
                            loop.Add(place(elliptic.Center.X + ex * Math.Cos(tilt) - ey * Math.Sin(tilt), elliptic.Center.Y + ex * Math.Sin(tilt) + ey * Math.Cos(tilt)));
                        }
                        continue;
                    }
                    var curve = edge as Hatch.BoundaryPath.Spline;
                    if (curve != null)
                    {
                        sampled = true;
                        foreach (XYZ control in curve.ControlPoints)
                        {
                            loop.Add(place(control.X, control.Y));
                        }
                    }
                }
                if (loop.Count > 0)
                {
                    loops.Add(loop);
                }
            }
            return new Piece
            {
                Kind = "hatch",
                Loops = loops,
                Name = hatch.Pattern == null ? null : hatch.Pattern.Name,
                Solid = hatch.IsSolid,
                Note = sampled ? "curved edges sampled" : null,
            };
        }

        private void Emit(Piece piece)
        {
            Written++;
            Track(piece);
            sink(piece);
        }

        private void Track(Piece piece)
        {
            double[] first = null;
            Action<double[]> grow = point =>
            {
                box[0] = Math.Min(box[0], point[0]);
                box[1] = Math.Min(box[1], point[1]);
                box[2] = Math.Max(box[2], point[0]);
                box[3] = Math.Max(box[3], point[1]);
                if (first == null)
                {
                    first = point;
                }
            };
            if (piece.Pts != null)
            {
                piece.Pts.ForEach(grow);
            }
            if (piece.Loops != null)
            {
                piece.Loops.ForEach(loop => loop.ForEach(grow));
            }
            if (piece.Center != null)
            {
                grow(piece.Center);
            }
            if (piece.Start != null)
            {
                grow(piece.Start);
            }
            if (piece.End != null)
            {
                grow(piece.End);
            }
            if (first != null)
            {
                coreX.Add(first[0]);
                coreY.Add(first[1]);
                Points++;
            }
        }

        private void Count(string layer, string type)
        {
            SortedDictionary<string, int> byType;
            if (!After.TryGetValue(layer, out byType))
            {
                byType = new SortedDictionary<string, int>();
                After[layer] = byType;
            }
            int seen;
            byType.TryGetValue(type, out seen);
            byType[type] = seen + 1;
        }

        private static string EffectiveLayer(Entity entity, string inherited)
        {
            string name = entity.Layer == null ? "0" : entity.Layer.Name;
            return name == "0" && inherited != null ? inherited : name;
        }

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

        private static List<XDataEntry> Xdata(Entity entity)
        {
            if (entity.ExtendedData == null)
            {
                return null;
            }
            var list = new List<XDataEntry>();
            foreach (var pair in entity.ExtendedData)
            {
                var records = pair.Value.Records;
                list.Add(new XDataEntry
                {
                    App = pair.Key.Name,
                    Count = records.Count > XdataLimit ? (int?)records.Count : null,
                    Values = records.Take(XdataLimit).Select(record => record.RawValue == null ? "" : record.RawValue.ToString()).ToList(),
                });
            }
            return list.Count == 0 ? null : list;
        }

        private static string Hex(ulong handle)
        {
            return handle.ToString("X");
        }

        private static string PathText(List<ulong> path)
        {
            return path.Count == 0 ? null : string.Join("/", path.Select(Hex));
        }

        private static double[] Pt(XYZ point)
        {
            return Math.Abs(point.Z) > 1e-9 ? new[] { point.X, point.Y, point.Z } : new[] { point.X, point.Y };
        }

        private static double Distance(XYZ a, XYZ b)
        {
            return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }

        // Inner transform first, so the chain is read from the last insert back to the first
        private static XYZ World(List<Transform> chain, XYZ point)
        {
            for (int index = chain.Count - 1; index >= 0; index--)
            {
                point = chain[index].ApplyTransform(point);
            }
            return point;
        }

        private static bool Mirrored(List<Transform> chain)
        {
            if (chain.Count == 0)
            {
                return false;
            }
            XYZ origin = World(chain, XYZ.Zero);
            XYZ x = World(chain, XYZ.AxisX) - origin;
            XYZ y = World(chain, XYZ.AxisY) - origin;
            return x.X * y.Y - x.Y * y.X < 0;
        }

        private static XYZ Cross(XYZ a, XYZ b)
        {
            return new XYZ(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        private static XYZ Unit(XYZ v)
        {
            double length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            return new XYZ(v.X / length, v.Y / length, v.Z / length);
        }

        // Object coordinate system to world, by the arbitrary axis rule
        public static XYZ ToWcs(XYZ point, XYZ normal)
        {
            bool noTilt = Math.Abs(normal.X) < 1e-12 && Math.Abs(normal.Y) < 1e-12;
            if (noTilt && normal.Z >= 0)
            {
                return point;
            }
            XYZ n = Unit(normal);
            XYZ across = Math.Abs(n.X) < 1.0 / 64 && Math.Abs(n.Y) < 1.0 / 64 ? XYZ.AxisY : XYZ.AxisZ;
            XYZ ax = Unit(Cross(across, n));
            XYZ ay = Unit(Cross(n, ax));
            return new XYZ(
                ax.X * point.X + ay.X * point.Y + n.X * point.Z,
                ax.Y * point.X + ay.Y * point.Y + n.Y * point.Z,
                ax.Z * point.X + ay.Z * point.Y + n.Z * point.Z);
        }
    }
}
