using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using CivilAssembly = Autodesk.Civil.DatabaseServices.Assembly;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Exception = System.Exception;

[assembly: CommandClass(typeof(SiteAdaptation.C3DProbe.Civil.DataCommands))]

namespace SiteAdaptation.C3DProbe.Civil
{
    // R162. Writes the tables of one drawing for the data book, only what lies inside the Phase 1 border. Read only: every
    // object opens for read, nested block entities are read in place and moved with the block transform, nothing is copied
    // into the drawing, the transaction is aborted and no save is called
    public class DataCommands
    {
        public const string BorderVariable = "C3DPROBE_BORDER";
        public const string LandXmlVariable = "C3DPROBE_LANDXML";
        public const string KnownVariable = "C3DPROBE_KNOWN";
        private const int MaxDepth = 16;
        private const double ArcStepDeg = 5;

        private Border border;
        private Transaction tr;
        private CsvTable entities;
        private CsvTable texts;
        private readonly List<Dictionary<string, object>> blockRows = new List<Dictionary<string, object>>();
        private readonly SortedSet<string> attributeTags = new SortedSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, int>> layerCounts = new Dictionary<string, Dictionary<string, int>>();
        private readonly Dictionary<string, Dictionary<string, int>> counts = new Dictionary<string, Dictionary<string, int>>();

        private static readonly string[] EntityHeader =
        {
            "handle", "type", "layer", "path", "parentBlock", "part", "x1", "y1", "z1", "x2", "y2", "z2", "cx", "cy", "radius",
            "length", "area", "closed", "zMin", "zMax", "cut",
        };

        private static readonly string[] TextHeader = { "handle", "type", "layer", "path", "parentBlock", "text", "x", "y", "z", "height", "rotationDeg" };

        [CommandMethod("C3DDATA")]
        public void Data()
        {
            var document = AcApp.DocumentManager.MdiActiveDocument;
            Database db = document.Database;
            string refusal = WorkGuard.Refusal(Environment.GetEnvironmentVariable(ProbeEnv.Work), db.Filename);
            string outFolder = Environment.GetEnvironmentVariable(ProbeEnv.Out);
            if (refusal != null || string.IsNullOrWhiteSpace(outFolder))
            {
                document.Editor.WriteMessage("\nC3DPROBE data refused: " + (refusal ?? ProbeEnv.Out + " is not set") + "\n");
                return;
            }
            var started = DateTime.Now;
            var summary = new Dictionary<string, object> { ["drawing"] = Path.GetFileName(db.Filename), ["reader"] = "C3DProbe" };
            try
            {
                border = Border.FromCsv(File.ReadAllLines(Environment.GetEnvironmentVariable(BorderVariable)));
                Directory.CreateDirectory(outFolder);
                using (tr = db.TransactionManager.StartOpenCloseTransaction())
                using (entities = new CsvTable(Path.Combine(outFolder, "entities.csv"), EntityHeader))
                using (texts = new CsvTable(Path.Combine(outFolder, "texts.csv"), TextHeader))
                {
                    var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId id in model)
                    {
                        Walk(id, Matrix3d.Identity, "", "", 0);
                    }
                    WriteBlocks(Path.Combine(outFolder, "blocks.csv"));
                    WriteLayers(db, Path.Combine(outFolder, "layers.csv"));
                    var civil = CivilDocument.GetCivilDocument(db);
                    WriteSurfaces(civil, Path.Combine(outFolder, "surfaces.csv"), Path.GetFileNameWithoutExtension(db.Filename));
                    WriteCivil(db, civil, model, Path.Combine(outFolder, "civil.csv"));
                    tr.Abort();
                }
                summary["counts"] = counts;
                summary["seconds"] = (int)(DateTime.Now - started).TotalSeconds;
                summary["done"] = true;
                Utf8.Write(Path.Combine(outFolder, "data.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
                document.Editor.WriteMessage("\nC3DPROBE data ok in " + summary["seconds"] + " s\n");
            }
            catch (Exception failure)
            {
                Utf8.Write(Path.Combine(outFolder, "data.error.txt"), failure.ToString());
                document.Editor.WriteMessage("\nC3DPROBE data failed: " + failure.GetType().Name + ": " + failure.Message + "\n");
            }
        }

        // Closes every drawing without saving and ends Civil 3D, the last line of a data book script (R160)
        [CommandMethod("C3DQUIT", CommandFlags.Session)]
        public void Quit()
        {
            foreach (var document in AcApp.DocumentManager.Cast<Autodesk.AutoCAD.ApplicationServices.Document>().ToList())
            {
                document.CloseAndDiscard();
            }
            AcApp.Quit();
        }

        private void Count(string table, string what)
        {
            if (!counts.TryGetValue(table, out var byWhat))
            {
                byWhat = new Dictionary<string, int>();
                counts[table] = byWhat;
            }
            byWhat.TryGetValue(what, out int seen);
            byWhat[what] = seen + 1;
        }

        private void CountLayer(string layer, string type)
        {
            if (!layerCounts.TryGetValue(layer, out var byType))
            {
                byType = new Dictionary<string, int>();
                layerCounts[layer] = byType;
            }
            byType.TryGetValue(type, out int seen);
            byType[type] = seen + 1;
        }

        private void Walk(ObjectId id, Matrix3d transform, string path, string parent, int depth)
        {
            Entity entity;
            try
            {
                entity = tr.GetObject(id, OpenMode.ForRead, false, true) as Entity;
            }
            catch (Exception)
            {
                Count("entities", "unreadable");
                return;
            }
            if (entity == null || entity is AttributeDefinition)
            {
                return;
            }
            string type = id.ObjectClass.DxfName;
            string handle = entity.Handle.ToString();
            if (entity is BlockReference reference)
            {
                Block(reference, transform, path, parent, depth, type, handle);
                return;
            }
            if (entity is Curve curve && !type.StartsWith("AECC", StringComparison.Ordinal))
            {
                CurveRows(curve, transform, path, parent, type, handle);
                return;
            }
            if (TextRow(entity, transform, path, parent, type, handle))
            {
                return;
            }
            PointRow(entity, transform, path, parent, type, handle);
        }

        private void Block(BlockReference reference, Matrix3d transform, string path, string parent, int depth, string type, string handle)
        {
            Matrix3d combined = transform * reference.BlockTransform;
            Point3d at = reference.Position.TransformBy(transform);
            var record = (BlockTableRecord)tr.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
            string name = record.Name;
            try
            {
                if (reference.IsDynamicBlock || record.IsAnonymous)
                {
                    name = ((BlockTableRecord)tr.GetObject(reference.DynamicBlockTableRecord, OpenMode.ForRead)).Name;
                }
            }
            catch (Exception)
            {
            }
            bool xref = record.IsFromExternalReference || record.IsFromOverlayReference;
            Vector3d xAxis = Vector3d.XAxis.TransformBy(combined);
            Vector3d yAxis = Vector3d.YAxis.TransformBy(combined);
            Vector3d zAxis = Vector3d.ZAxis.TransformBy(combined);
            var attributes = new Dictionary<string, string>();
            foreach (ObjectId attributeId in reference.AttributeCollection)
            {
                if (tr.GetObject(attributeId, OpenMode.ForRead, false, true) is AttributeReference attribute && !attribute.Invisible)
                {
                    attributes[attribute.Tag] = attribute.TextString;
                    attributeTags.Add(attribute.Tag);
                    TextRow(attribute, transform, path + "/" + handle, name, "ATTRIB", attribute.Handle.ToString());
                }
            }
            // The extents centre places a unit block on its plot, its insert point sits on a corner
            double? cx = null;
            double? cy = null;
            try
            {
                Extents3d box = reference.GeometricExtents;
                Point3d centre = new Point3d((box.MinPoint.X + box.MaxPoint.X) / 2, (box.MinPoint.Y + box.MaxPoint.Y) / 2, 0).TransformBy(transform);
                cx = centre.X;
                cy = centre.Y;
            }
            catch (Exception)
            {
            }
            if (border.Contains(at.X, at.Y))
            {
                blockRows.Add(new Dictionary<string, object>
                {
                    ["cx"] = cx, ["cy"] = cy,
                    ["handle"] = handle, ["name"] = name, ["definition"] = record.Name, ["layer"] = reference.Layer, ["path"] = path, ["parentBlock"] = parent,
                    ["x"] = at.X, ["y"] = at.Y, ["z"] = at.Z,
                    ["rotationDeg"] = (Math.Atan2(xAxis.Y, xAxis.X) * 180 / Math.PI + 360) % 360,
                    ["scaleX"] = xAxis.Length, ["scaleY"] = yAxis.Length, ["scaleZ"] = zAxis.Length,
                    ["mirrored"] = xAxis.X * yAxis.Y - xAxis.Y * yAxis.X < 0, ["xref"] = xref, ["attributes"] = attributes,
                });
                Count("blocks", "kept");
                CountLayer(reference.Layer, type);
            }
            else
            {
                Count("blocks", "outside");
            }
            if (xref || depth >= MaxDepth)
            {
                Count("blocks", xref ? "xrefNotOpened" : "depthLimit");
                return;
            }
            foreach (ObjectId child in record)
            {
                Walk(child, combined, path + "/" + handle, name, depth + 1);
            }
        }

        private void CurveRows(Curve curve, Matrix3d transform, string path, string parent, string type, string handle)
        {
            List<double[]> points;
            double radius = double.NaN;
            Point3d? centre = null;
            try
            {
                points = Sample(curve).Select(p => Xyz(p.TransformBy(transform))).ToList();
                if (curve is Arc arc)
                {
                    centre = arc.Center.TransformBy(transform);
                    radius = arc.Radius * Vector3d.XAxis.TransformBy(transform).Length;
                }
                else if (curve is Circle circle)
                {
                    centre = circle.Center.TransformBy(transform);
                    radius = circle.Radius * Vector3d.XAxis.TransformBy(transform).Length;
                }
            }
            catch (Exception)
            {
                PointRow(curve, transform, path, parent, type, handle);
                return;
            }
            bool closed = SafeClosed(curve);
            if (closed && points.Count > 2)
            {
                points.Add(points[0]);
            }
            ClipResult clip = border.Clip(points);
            if (clip.Parts.Count == 0)
            {
                Count("entities", "outside");
                return;
            }
            double area = closed && !clip.Cut ? Shoelace(points) : double.NaN;
            for (int part = 0; part < clip.Parts.Count; part++)
            {
                var piece = clip.Parts[part];
                var first = piece[0];
                var last = piece[piece.Count - 1];
                entities.Add(new object[]
                {
                    handle, type, curve.Layer, path, parent, part, first[0], first[1], first[2], last[0], last[1], last[2],
                    centre?.X, centre?.Y, radius, Border.Length(piece), area, closed, piece.Min(p => p[2]), piece.Max(p => p[2]), clip.Cut,
                });
                Count("entities", clip.Cut ? "keptCut" : "kept");
            }
            CountLayer(curve.Layer, type);
        }

        private static bool SafeClosed(Curve curve)
        {
            try
            {
                return curve.Closed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Points along a curve in its own space: every vertex, arcs every few degrees, other curves by length
        private static List<Point3d> Sample(Curve curve)
        {
            var points = new List<Point3d>();
            if (curve is Line line)
            {
                points.Add(line.StartPoint);
                points.Add(line.EndPoint);
                return points;
            }
            if (curve is Polyline poly)
            {
                for (int i = 0; i < poly.NumberOfVertices; i++)
                {
                    bool hasNext = i + 1 < poly.NumberOfVertices || poly.Closed;
                    if (hasNext && poly.GetSegmentType(i) == SegmentType.Arc)
                    {
                        CircularArc3d arc = poly.GetArcSegmentAt(i);
                        double sweep = Math.Abs(arc.EndAngle - arc.StartAngle) * 180 / Math.PI;
                        int n = Math.Max(2, (int)Math.Ceiling(sweep / ArcStepDeg) + 1);
                        Point3d[] arcPoints = arc.GetSamplePoints(n).Select(sample => sample.Point).ToArray();
                        points.AddRange(i == 0 ? arcPoints : arcPoints.Skip(points.Count > 0 ? 1 : 0));
                    }
                    else if (points.Count == 0 || !points[points.Count - 1].IsEqualTo(poly.GetPoint3dAt(i)))
                    {
                        points.Add(poly.GetPoint3dAt(i));
                    }
                }
                if (poly.Closed && points.Count > 1 && points[points.Count - 1].IsEqualTo(points[0]))
                {
                    points.RemoveAt(points.Count - 1);
                }
                return points;
            }
            if (curve is Polyline2d || curve is Polyline3d)
            {
                int last = (int)Math.Round(curve.EndParam);
                for (int k = 0; k <= last; k++)
                {
                    points.Add(curve.GetPointAtParameter(Math.Min(k, curve.EndParam)));
                }
                return points;
            }
            double length = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
            int count;
            if (curve is Arc arcCurve)
            {
                count = Math.Max(2, (int)Math.Ceiling(arcCurve.TotalAngle * 180 / Math.PI / ArcStepDeg) + 1);
            }
            else if (curve is Circle)
            {
                count = (int)(360 / ArcStepDeg);
            }
            else
            {
                count = Math.Min(512, Math.Max(8, (int)Math.Ceiling(length)));
            }
            bool closed = SafeClosed(curve);
            int steps = closed ? count : count - 1;
            for (int k = 0; k < count; k++)
            {
                points.Add(curve.GetPointAtDist(length * k / steps));
            }
            return points;
        }

        private bool TextRow(Entity entity, Matrix3d transform, string path, string parent, string type, string handle)
        {
            string text;
            Point3d at;
            double height;
            double rotation;
            if (entity is DBText dbText)
            {
                text = dbText.TextString;
                at = dbText.Position;
                height = dbText.Height;
                rotation = dbText.Rotation;
            }
            else if (entity is MText mText)
            {
                text = mText.Text;
                at = mText.Location;
                height = mText.TextHeight;
                rotation = mText.Rotation;
            }
            else if (entity is MLeader leader && leader.ContentType == ContentType.MTextContent && leader.MText != null)
            {
                text = leader.MText.Text;
                at = leader.MText.Location;
                height = leader.MText.TextHeight;
                rotation = leader.MText.Rotation;
            }
            else
            {
                return false;
            }
            Point3d world = at.TransformBy(transform);
            Vector3d xAxis = Vector3d.XAxis.TransformBy(transform);
            if (!border.Contains(world.X, world.Y))
            {
                Count("texts", "outside");
                return true;
            }
            texts.Add(new object[]
            {
                handle, type, entity.Layer, path, parent, (text ?? "").Replace("\r", " ").Replace("\n", " "), world.X, world.Y, world.Z,
                height * xAxis.Length, ((rotation + Math.Atan2(xAxis.Y, xAxis.X)) * 180 / Math.PI + 360) % 360,
            });
            Count("texts", "kept");
            entities.Add(new object[] { handle, type, entity.Layer, path, parent, 0, world.X, world.Y, world.Z, null, null, null, null, null, null, null, null, null, world.Z, world.Z, false });
            Count("entities", "kept");
            CountLayer(entity.Layer, type);
            return true;
        }

        // Any other entity is one point: its own position where it has one, else the middle of its extents
        private void PointRow(Entity entity, Matrix3d transform, string path, string parent, string type, string handle)
        {
            Point3d at;
            double zMin;
            double zMax;
            double area = double.NaN;
            try
            {
                if (entity is DBPoint point)
                {
                    at = point.Position;
                    zMin = zMax = at.Z;
                }
                else
                {
                    Extents3d box = entity.GeometricExtents;
                    at = new Point3d((box.MinPoint.X + box.MaxPoint.X) / 2, (box.MinPoint.Y + box.MaxPoint.Y) / 2, (box.MinPoint.Z + box.MaxPoint.Z) / 2);
                    zMin = box.MinPoint.Z;
                    zMax = box.MaxPoint.Z;
                }
                if (entity is Hatch hatch)
                {
                    Vector3d x = Vector3d.XAxis.TransformBy(transform);
                    Vector3d y = Vector3d.YAxis.TransformBy(transform);
                    area = hatch.Area * Math.Abs(x.X * y.Y - x.Y * y.X);
                }
            }
            catch (Exception)
            {
                Count("entities", "noPosition");
                return;
            }
            Point3d world = at.TransformBy(transform);
            if (!border.Contains(world.X, world.Y))
            {
                Count("entities", "outside");
                return;
            }
            double shift = world.Z - at.Z;
            entities.Add(new object[] { handle, type, entity.Layer, path, parent, 0, world.X, world.Y, world.Z, null, null, null, null, null, null, null, area, null, zMin + shift, zMax + shift, false });
            Count("entities", "kept");
            CountLayer(entity.Layer, type);
        }

        private void WriteBlocks(string path)
        {
            var header = new List<string> { "handle", "name", "definition", "layer", "path", "parentBlock", "x", "y", "z", "rotationDeg", "scaleX", "scaleY", "scaleZ", "mirrored", "xref", "cx", "cy", "reader" };
            int fixedColumns = header.Count - 1;
            header.AddRange(attributeTags.Select(tag => "attr_" + tag));
            using (var table = new CsvTable(path, header))
            {
                foreach (var row in blockRows)
                {
                    var attributes = (Dictionary<string, string>)row["attributes"];
                    var cells = header.Take(fixedColumns).Select(key => row[key]).ToList();
                    cells.Add("C3DProbe");
                    cells.AddRange(attributeTags.Select(tag => attributes.TryGetValue(tag, out string value) ? (object)value : null));
                    table.Add(cells);
                }
            }
        }

        private void WriteLayers(Database db, string path)
        {
            using (var table = new CsvTable(path, new[] { "layer", "colour", "on", "frozen", "locked", "linetype", "entitiesInside", "countsByType" }))
            {
                var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in layers)
                {
                    var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    layerCounts.TryGetValue(layer.Name, out var byType);
                    string linetype = null;
                    try
                    {
                        linetype = ((LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId, OpenMode.ForRead)).Name;
                    }
                    catch (Exception)
                    {
                    }
                    table.Add(new object[]
                    {
                        layer.Name, layer.Color.ToString(), !layer.IsOff, layer.IsFrozen, layer.IsLocked, linetype,
                        byType?.Values.Sum() ?? 0, byType == null ? "" : string.Join(" ", byType.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)),
                    });
                }
            }
        }

        private void WriteSurfaces(CivilDocument civil, string path, string drawing)
        {
            string landxml = Environment.GetEnvironmentVariable(LandXmlVariable);
            var known = new HashSet<string>((Environment.GetEnvironmentVariable(KnownVariable) ?? "").Split('|').Where(n => n.Length > 0), StringComparer.OrdinalIgnoreCase);
            var header = new[]
            {
                "name", "description", "type", "style", "layer", "points", "triangles", "pointsInside", "trianglesInside", "zMinInside", "zMaxInside",
                "boundaries", "definitions", "operations", "isReference", "inExistingLandxml", "exportedLandxml",
            };
            using (var table = new CsvTable(path, header))
            {
                foreach (ObjectId id in civil.GetSurfaceIds())
                {
                    var surface = (CivilSurface)tr.GetObject(id, OpenMode.ForRead);
                    var general = surface.GetGeneralProperties();
                    string style = null;
                    try
                    {
                        style = Reflect.Get(tr.GetObject(surface.StyleId, OpenMode.ForRead), "Name") as string;
                    }
                    catch (Exception)
                    {
                    }
                    int triangles = 0;
                    int trianglesInside = 0;
                    double zMin = double.NaN;
                    double zMax = double.NaN;
                    var vertexIndex = new Dictionary<(long, long), int>();
                    var vertices = new List<Point3d>();
                    var faces = new List<int[]>();
                    bool export = !known.Contains(surface.Name) && !string.IsNullOrWhiteSpace(landxml);
                    string boundaries = null;
                    if (surface is TinSurface tin)
                    {
                        triangles = tin.GetTinProperties().NumberOfTriangles;
                        boundaries = string.Join(" | ", Reflect.Items(Reflect.Get(tin, "BoundariesDefinition"))
                            .Select(b => Reflect.Get(b, "Name") + ":" + Reflect.Get(b, "BoundaryType")));
                        foreach (TinSurfaceTriangle triangle in tin.GetTriangles(false))
                        {
                            Point3d a = triangle.Vertex1.Location;
                            Point3d b = triangle.Vertex2.Location;
                            Point3d c = triangle.Vertex3.Location;
                            if (!border.Contains((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3))
                            {
                                continue;
                            }
                            trianglesInside++;
                            foreach (Point3d p in new[] { a, b, c })
                            {
                                zMin = double.IsNaN(zMin) ? p.Z : Math.Min(zMin, p.Z);
                                zMax = double.IsNaN(zMax) ? p.Z : Math.Max(zMax, p.Z);
                            }
                            if (export)
                            {
                                faces.Add(new[] { Index(vertexIndex, vertices, a), Index(vertexIndex, vertices, b), Index(vertexIndex, vertices, c) });
                            }
                        }
                    }
                    string exported = null;
                    if (export && faces.Count > 0)
                    {
                        exported = Path.Combine(landxml, Safe(drawing) + "__" + Safe(surface.Name) + ".xml");
                        WriteLandXml(exported, surface.Name, vertices, faces);
                    }
                    var definitions = Reflect.Counts(surface, "Definition").Where(p => p.Value.HasValue && p.Value > 0).Select(p => p.Key + "=" + p.Value);
                    var operations = Reflect.Items(Reflect.Get(surface, "Operations")).GroupBy(o => o.GetType().Name).Select(g => g.Key + "=" + g.Count());
                    table.Add(new object[]
                    {
                        surface.Name, surface.Description, surface.GetType().Name, style, surface.Layer, general.NumberOfPoints, triangles,
                        export ? vertices.Count : (int?)null, trianglesInside, zMin, zMax, boundaries, string.Join(" ", definitions), string.Join(" ", operations),
                        surface.IsReferenceObject, known.Contains(surface.Name), exported == null ? null : Path.GetFileName(exported),
                    });
                    Count("surfaces", trianglesInside > 0 ? "kept" : "outside");
                }
            }
        }

        private static int Index(Dictionary<(long, long), int> index, List<Point3d> vertices, Point3d p)
        {
            var key = ((long)Math.Round(p.X * 10000), (long)Math.Round(p.Y * 10000));
            if (!index.TryGetValue(key, out int at))
            {
                at = vertices.Count;
                index[key] = at;
                vertices.Add(p);
            }
            return at;
        }

        private static string Safe(string name)
        {
            return string.Concat((name ?? "").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        }

        // LandXML 1.2 as the existing export writes it: P holds northing, easting and elevation, F three point ids
        private static void WriteLandXml(string path, string name, List<Point3d> vertices, List<int[]> faces)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var culture = CultureInfo.InvariantCulture;
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.Write("<?xml version=\"1.0\"?>\n<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\" version=\"1.2\">");
                writer.Write("<Units><Metric linearUnit=\"meter\" areaUnit=\"squareMeter\" volumeUnit=\"cubicMeter\"/></Units>");
                writer.Write("<Surfaces><Surface name=\"" + System.Security.SecurityElement.Escape(name) + "\"><Definition surfType=\"TIN\"><Pnts>\n");
                for (int k = 0; k < vertices.Count; k++)
                {
                    Point3d p = vertices[k];
                    writer.Write("<P id=\"" + (k + 1) + "\">" + p.Y.ToString("0.####", culture) + " " + p.X.ToString("0.####", culture) + " " + p.Z.ToString("0.####", culture) + "</P>\n");
                }
                writer.Write("</Pnts><Faces>\n");
                foreach (int[] face in faces)
                {
                    writer.Write("<F>" + (face[0] + 1) + " " + (face[1] + 1) + " " + (face[2] + 1) + "</F>\n");
                }
                writer.Write("</Faces></Definition></Surface></Surfaces></LandXML>\n");
            }
        }

        private void WriteCivil(Database db, CivilDocument civil, BlockTableRecord model, string path)
        {
            using (var table = new CsvTable(path, new[] { "kind", "name", "layer", "parent", "property", "value" }))
            {
                void Rows(string kind, string name, string layer, string parent, Dictionary<string, object> values)
                {
                    foreach (var pair in values.OrderBy(p => p.Key))
                    {
                        table.Add(new object[] { kind, name, layer, parent, pair.Key, pair.Value });
                    }
                    Count("civil", kind);
                }
                var drawing = new Dictionary<string, object> { ["insunits"] = db.Insunits.ToString() };
                try
                {
                    drawing["CGEOCS"] = AcApp.GetSystemVariable("CGEOCS");
                }
                catch (Exception)
                {
                }
                try
                {
                    foreach (var pair in Reflect.Plain(civil.Settings.DrawingSettings.UnitZoneSettings))
                    {
                        drawing["unitZone." + pair.Key] = pair.Value;
                    }
                }
                catch (Exception)
                {
                }
                Rows("drawing", Path.GetFileName(db.Filename), null, null, drawing);
                foreach (ObjectId id in civil.GetAlignmentIds())
                {
                    var alignment = (Alignment)tr.GetObject(id, OpenMode.ForRead);
                    if (!AlignmentInside(alignment))
                    {
                        Count("civil", "alignmentOutside");
                        continue;
                    }
                    Rows("alignment", alignment.Name, alignment.Layer, null, Reflect.Plain(alignment));
                    foreach (ObjectId profileId in alignment.GetProfileIds())
                    {
                        var profile = tr.GetObject(profileId, OpenMode.ForRead) as Entity;
                        Rows("profile", Reflect.Get(profile, "Name") as string, profile?.Layer, alignment.Name, Reflect.Plain(profile));
                    }
                }
                foreach (ObjectId id in civil.AssemblyCollection)
                {
                    var assembly = (CivilAssembly)tr.GetObject(id, OpenMode.ForRead);
                    if (!Inside(assembly))
                    {
                        Count("civil", "assemblyOutside");
                        continue;
                    }
                    Rows("assembly", assembly.Name, assembly.Layer, null, Reflect.Plain(assembly));
                    foreach (var group in Reflect.Items(Reflect.Get(assembly, "Groups")))
                    {
                        foreach (var subId in Reflect.Items(Reflect.Get(group, "GetSubassemblyIds")))
                        {
                            if (!(subId is ObjectId objectId))
                            {
                                continue;
                            }
                            DBObject sub = tr.GetObject(objectId, OpenMode.ForRead);
                            var values = Reflect.Plain(sub);
                            foreach (var property in sub.GetType().GetProperties().Where(p => p.Name.StartsWith("Params", StringComparison.Ordinal)))
                            {
                                try
                                {
                                    foreach (var item in Reflect.Items(property.GetValue(sub)))
                                    {
                                        values["param." + Reflect.Get(item, "Key")] = Reflect.Clean(Reflect.Get(item, "Value"));
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }
                            foreach (string part in new[] { "Points", "Links", "Shapes" })
                            {
                                values["codes." + part] = string.Join(" ", Reflect.Items(Reflect.Get(sub, part)).SelectMany(item => Reflect.Strings(Reflect.Get(item, "Codes"))).Distinct());
                            }
                            Rows("subassembly", Reflect.Get(sub, "Name") as string, null, assembly.Name, values);
                        }
                    }
                }
                foreach (ObjectId id in civil.PointGroups)
                {
                    var group = tr.GetObject(id, OpenMode.ForRead);
                    var values = Reflect.Plain(group);
                    int inside = 0;
                    try
                    {
                        foreach (uint number in (uint[])Reflect.Get(group, "GetPointNumbers"))
                        {
                            var point = (CogoPoint)tr.GetObject(civil.CogoPoints.GetPointByPointNumber(number), OpenMode.ForRead);
                            inside += border.Contains(point.Easting, point.Northing) ? 1 : 0;
                        }
                    }
                    catch (Exception)
                    {
                    }
                    values["pointsInside"] = inside;
                    Rows("pointGroup", Reflect.Get(group, "Name") as string, null, null, values);
                }
                foreach (ObjectId siteId in civil.GetSiteIds())
                {
                    var site = tr.GetObject(siteId, OpenMode.ForRead);
                    var values = Reflect.Plain(site);
                    foreach (var pair in Reflect.IdCounts(site))
                    {
                        values["count." + pair.Key] = pair.Value;
                    }
                    Rows("site", Reflect.Get(site, "Name") as string, null, null, values);
                    foreach (var parcelId in Reflect.Items(Reflect.Get(site, "GetParcelIds")))
                    {
                        if (parcelId is ObjectId objectId && tr.GetObject(objectId, OpenMode.ForRead) is Entity parcel)
                        {
                            if (Inside(parcel))
                            {
                                var parcelValues = Reflect.Plain(parcel);
                                parcelValues["outline"] = ParcelOutline(parcel);
                                Rows("parcel", Reflect.Get(parcel, "Name") as string, parcel.Layer, Reflect.Get(site, "Name") as string, parcelValues);
                            }
                            else
                            {
                                Count("civil", "parcelOutside");
                            }
                        }
                    }
                }
                foreach (ObjectId id in model)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead, false, true) is FeatureLine feature))
                    {
                        continue;
                    }
                    var points = new List<double[]>();
                    foreach (Point3d p in feature.GetPoints(Autodesk.Civil.FeatureLinePointType.AllPoints))
                    {
                        points.Add(Xyz(p));
                    }
                    ClipResult clip = border.Clip(points);
                    if (clip.Parts.Count == 0)
                    {
                        Count("civil", "featureLineOutside");
                        continue;
                    }
                    var values = Reflect.Plain(feature);
                    values["insideLength"] = Round.To(clip.Parts.Sum(Border.Length));
                    values["cut"] = clip.Cut;
                    values["zMinInside"] = clip.Parts.SelectMany(p => p).Min(p => p[2]);
                    values["zMaxInside"] = clip.Parts.SelectMany(p => p).Max(p => p[2]);
                    Rows("featureLine", feature.Name, feature.Layer, null, values);
                }
                foreach (ObjectId id in civil.CorridorCollection)
                {
                    var corridor = (Corridor)tr.GetObject(id, OpenMode.ForRead);
                    var values = Reflect.Plain(corridor);
                    values["pointCodes"] = string.Join(" ", Reflect.Strings(Reflect.Get(corridor, "GetPointCodes")));
                    values["linkCodes"] = string.Join(" ", Reflect.Strings(Reflect.Get(corridor, "GetLinkCodes")));
                    Rows("corridor", corridor.Name, corridor.Layer, null, values);
                }
                foreach (ObjectId id in civil.GetPipeNetworkIds())
                {
                    var network = tr.GetObject(id, OpenMode.ForRead);
                    var values = Reflect.Plain(network);
                    foreach (var pair in Reflect.IdCounts(network))
                    {
                        values["count." + pair.Key] = pair.Value;
                    }
                    Rows("pipeNetwork", Reflect.Get(network, "Name") as string, null, null, values);
                }
                foreach (ObjectId id in civil.GetSurfaceIds())
                {
                    var surface = (CivilSurface)tr.GetObject(id, OpenMode.ForRead);
                    if (surface.IsReferenceObject)
                    {
                        Rows("dataShortcut", surface.Name, surface.Layer, null, new Dictionary<string, object> { ["object"] = "surface" });
                    }
                }
            }
        }

        private bool Inside(Entity entity)
        {
            try
            {
                Extents3d box = entity.GeometricExtents;
                return border.Contains((box.MinPoint.X + box.MaxPoint.X) / 2, (box.MinPoint.Y + box.MaxPoint.Y) / 2);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool AlignmentInside(Alignment alignment)
        {
            double step = Math.Max(1, (alignment.EndingStation - alignment.StartingStation) / 200);
            for (double station = alignment.StartingStation; station <= alignment.EndingStation; station += step)
            {
                double east = 0;
                double north = 0;
                try
                {
                    alignment.PointLocation(station, 0, ref east, ref north);
                }
                catch (Exception)
                {
                    continue;
                }
                if (border.Contains(east, north))
                {
                    return true;
                }
            }
            return false;
        }

        // R189. The boundary of a parcel as x y pairs joined by semicolons, from its base curve, so plot faces drawn as Civil 3D
        // parcels can be compared with the baseplan. The exploded pieces stand in when the base curve cannot be read
        private static string ParcelOutline(Entity parcel)
        {
            var points = new List<Point3d>();
            try
            {
                if (Reflect.Get(parcel, "BaseCurve") is Curve curve)
                {
                    points = Sample(curve);
                }
            }
            catch (Exception)
            {
            }
            if (points.Count < 3)
            {
                try
                {
                    var pieces = new DBObjectCollection();
                    parcel.Explode(pieces);
                    points = pieces.OfType<Curve>().SelectMany(Sample).ToList();
                    foreach (DBObject piece in pieces)
                    {
                        piece.Dispose();
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }
            var culture = CultureInfo.InvariantCulture;
            return points.Count < 3 ? null : string.Join(";", points.Select(p => p.X.ToString("0.###", culture) + " " + p.Y.ToString("0.###", culture)));
        }

        private static double[] Xyz(Point3d point)
        {
            return new[] { point.X, point.Y, point.Z };
        }

        private static double Shoelace(List<double[]> points)
        {
            double sum = 0;
            for (int k = 0; k + 1 < points.Count; k++)
            {
                sum += points[k][0] * points[k + 1][1] - points[k + 1][0] * points[k][1];
            }
            return Math.Abs(sum) / 2;
        }
    }
}
