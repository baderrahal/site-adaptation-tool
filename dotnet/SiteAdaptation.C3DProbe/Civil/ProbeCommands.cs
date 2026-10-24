using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using CivilAssembly = Autodesk.Civil.DatabaseServices.Assembly;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Exception = System.Exception;

[assembly: CommandClass(typeof(SiteAdaptation.C3DProbe.Civil.ProbeCommands))]

namespace SiteAdaptation.C3DProbe.Civil
{
    // Read only (R1, R150). Every object opens for read, no transaction commits and nothing calls a save. The runner
    // hashes the working copy before and after a run to prove it (R151)
    public class ProbeCommands
    {
        private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions { WriteIndented = true };
        private static readonly JsonSerializerOptions OneLine = new JsonSerializerOptions { WriteIndented = false };

        [CommandMethod("C3DDUMP")]
        public void Dump()
        {
            Run("dump", (db, tr, prefix) =>
            {
                var civil = CivilDocument.GetCivilDocument(db);
                var scan = ScanModelSpace(db, tr);
                var payload = new Dictionary<string, object>
                {
                    ["drawing"] = Path.GetFileName(db.Filename),
                    ["units"] = Units(db, civil),
                    ["xrefs"] = Xrefs(db, tr),
                    ["civilIdCounts"] = Reflect.IdCounts(civil),
                    ["surfaces"] = Surfaces(civil, tr),
                    ["corridors"] = Corridors(civil, tr),
                    ["assemblies"] = Assemblies(civil, tr),
                    ["alignments"] = Alignments(civil, tr),
                    ["sites"] = Sites(civil, tr),
                    ["cogo"] = Cogo(civil, tr),
                    ["pipeNetworks"] = PipeNetworks(civil, tr),
                    ["classes"] = scan.Classes,
                    ["layers"] = scan.Layers,
                };
                Utf8.Write(Path.Combine(OutFolder(), prefix + ".dump.json"), JsonSerializer.Serialize(payload, Indented));
                return "classes " + scan.Classes.Count + ", layers " + scan.Layers.Count;
            });
        }

        [CommandMethod("C3DLEVELS")]
        public void Levels()
        {
            Run("levels", (db, tr, prefix) =>
            {
                string pointsPath = Environment.GetEnvironmentVariable(ProbeEnv.Points);
                var points = PointTable.Read(File.ReadAllLines(pointsPath));
                var civil = CivilDocument.GetCivilDocument(db);
                var rows = new StringBuilder("id,surface,z\n");
                var hits = new Dictionary<string, int>();
                foreach (var surface in SelectedSurfaces(civil, tr))
                {
                    int hit = 0;
                    foreach (var point in points)
                    {
                        double z;
                        try
                        {
                            z = surface.FindElevationAtXY(point.X, point.Y);
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        rows.Append(PointTable.Row(point.Id, surface.Name, z)).Append('\n');
                        hit++;
                    }
                    hits[surface.Name] = hit;
                }
                Utf8.Write(Path.Combine(OutFolder(), prefix + "." + Path.GetFileNameWithoutExtension(pointsPath) + ".levels.csv"), rows.ToString());
                return "points " + points.Count + ", " + string.Join(", ", hits.Select(pair => pair.Key + " " + pair.Value));
            });
        }

        [CommandMethod("C3DBORDER")]
        public void Border()
        {
            Run("border", (db, tr, prefix) =>
            {
                var civil = CivilDocument.GetCivilDocument(db);
                var result = new List<object>();
                foreach (var surface in SelectedSurfaces(civil, tr))
                {
                    if (!(surface is TinSurface tin))
                    {
                        result.Add(new Dictionary<string, object> { ["surface"] = surface.Name, ["skipped"] = "not a TIN" });
                        continue;
                    }
                    var border = new TinBorder();
                    int read = 0;
                    foreach (TinSurfaceTriangle triangle in tin.GetTriangles(false))
                    {
                        var a = triangle.Vertex1.Location;
                        var b = triangle.Vertex2.Location;
                        var c = triangle.Vertex3.Location;
                        border.Add(a.X, a.Y, b.X, b.Y, c.X, c.Y);
                        read++;
                    }
                    var rings = border.Rings();
                    result.Add(new Dictionary<string, object>
                    {
                        ["surface"] = surface.Name,
                        ["visibleTriangles"] = read,
                        ["vertices"] = border.Vertices,
                        ["borderEdges"] = border.BorderEdges().Count,
                        ["rings"] = rings.Select(ring => ring.Select(p => new[] { Round.To(p[0]), Round.To(p[1]) }).ToList()).ToList(),
                    });
                }
                Utf8.Write(Path.Combine(OutFolder(), prefix + ".borders.json"), JsonSerializer.Serialize(result, OneLine));
                return "surfaces " + result.Count;
            });
        }

        // The 3D lines a level can come from: 3D polylines, feature lines and the corridor feature lines by code
        [CommandMethod("C3DLINES")]
        public void Lines()
        {
            Run("lines", (db, tr, prefix) =>
            {
                var civil = CivilDocument.GetCivilDocument(db);
                var text = new StringBuilder();
                var counts = new Dictionary<string, int>();
                void Emit(Dictionary<string, object> line)
                {
                    text.Append(JsonSerializer.Serialize(line, OneLine)).Append('\n');
                    string kind = (string)line["kind"];
                    counts.TryGetValue(kind, out int seen);
                    counts[kind] = seen + 1;
                }
                var modelSpace = ModelSpace(db, tr);
                foreach (ObjectId id in modelSpace)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false, true);
                    if (entity is Polyline3d poly)
                    {
                        var points = new List<double[]>();
                        foreach (ObjectId vertexId in poly)
                        {
                            var vertex = (PolylineVertex3d)tr.GetObject(vertexId, OpenMode.ForRead);
                            points.Add(Xyz(vertex.Position));
                        }
                        Emit(Line("polyline3d", poly.Layer, null, null, points));
                    }
                    else if (entity is FeatureLine feature)
                    {
                        var points = new List<double[]>();
                        foreach (Autodesk.AutoCAD.Geometry.Point3d point in feature.GetPoints(Autodesk.Civil.FeatureLinePointType.AllPoints))
                        {
                            points.Add(Xyz(point));
                        }
                        Emit(Line("featureLine", feature.Layer, feature.Name, null, points));
                    }
                }
                foreach (ObjectId corridorId in civil.CorridorCollection)
                {
                    var corridor = (Corridor)tr.GetObject(corridorId, OpenMode.ForRead);
                    foreach (Baseline baseline in corridor.Baselines)
                    {
                        foreach (var group in Reflect.Items(Reflect.Get(baseline.MainBaselineFeatureLines, "FeatureLinesCol")))
                        {
                            foreach (var item in Reflect.Items(group))
                            {
                                if (!(item is CorridorFeatureLine line))
                                {
                                    continue;
                                }
                                var points = new List<double[]>();
                                foreach (FeatureLinePoint point in line.FeatureLinePoints)
                                {
                                    points.Add(Xyz(point.XYZ));
                                }
                                var emitted = Line("corridorLine", corridor.Layer, corridor.Name + " / " + baseline.Name, line.CodeName, points);
                                Emit(emitted);
                            }
                        }
                    }
                }
                Utf8.Write(Path.Combine(OutFolder(), prefix + ".lines.jsonl"), text.ToString());
                return string.Join(", ", counts.Select(pair => pair.Key + " " + pair.Value));
            });
        }

        private static Dictionary<string, object> Line(string kind, string layer, string name, string code, List<double[]> points)
        {
            return new Dictionary<string, object>
            {
                ["kind"] = kind,
                ["layer"] = layer,
                ["name"] = name,
                ["code"] = code,
                ["points"] = points,
            };
        }

        private static double[] Xyz(Autodesk.AutoCAD.Geometry.Point3d point)
        {
            return new[] { Round.To(point.X), Round.To(point.Y), Round.To(point.Z, 4) };
        }

        private delegate string ProbeStep(Database db, Transaction tr, string prefix);

        private static void Run(string name, ProbeStep step)
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            Editor editor = document.Editor;
            Database db = document.Database;
            string refusal = WorkGuard.Refusal(Environment.GetEnvironmentVariable(ProbeEnv.Work), db.Filename);
            if (refusal != null)
            {
                editor.WriteMessage("\nC3DPROBE " + name + " refused: " + refusal + "\n");
                return;
            }
            string prefix = Path.GetFileNameWithoutExtension(db.Filename);
            var started = DateTime.Now;
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    string summary = step(db, tr, prefix);
                    tr.Abort();
                    editor.WriteMessage("\nC3DPROBE " + name + " ok in " + (int)(DateTime.Now - started).TotalSeconds + " s: " + summary + "\n");
                }
            }
            catch (Exception failure)
            {
                Utf8.Write(Path.Combine(OutFolder(), prefix + "." + name + ".error.txt"), failure.ToString());
                editor.WriteMessage("\nC3DPROBE " + name + " failed: " + failure.GetType().Name + ": " + failure.Message + "\n");
            }
        }

        private static string OutFolder()
        {
            string folder = Environment.GetEnvironmentVariable(ProbeEnv.Out);
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new InvalidOperationException(ProbeEnv.Out + " is not set");
            }
            return folder;
        }

        private static BlockTableRecord ModelSpace(Database db, Transaction tr)
        {
            var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        }

        private static List<CivilSurface> SelectedSurfaces(CivilDocument civil, Transaction tr)
        {
            string filter = Environment.GetEnvironmentVariable(ProbeEnv.Surfaces);
            var names = string.IsNullOrWhiteSpace(filter) ? null : new HashSet<string>(filter.Split('|'), StringComparer.OrdinalIgnoreCase);
            var surfaces = new List<CivilSurface>();
            foreach (ObjectId id in civil.GetSurfaceIds())
            {
                var surface = (CivilSurface)tr.GetObject(id, OpenMode.ForRead);
                if (names == null || names.Contains(surface.Name))
                {
                    surfaces.Add(surface);
                }
            }
            return surfaces;
        }

        private static string NameOf(Transaction tr, ObjectId id)
        {
            if (id.IsNull || !id.IsValid)
            {
                return null;
            }
            try
            {
                return Reflect.Get(tr.GetObject(id, OpenMode.ForRead), "Name") as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Dictionary<string, object> Units(Database db, CivilDocument civil)
        {
            var units = new Dictionary<string, object>
            {
                ["insunits"] = db.Insunits.ToString(),
                ["lunits"] = db.Lunits,
                ["measurement"] = db.Measurement.ToString(),
            };
            try
            {
                units["CGEOCS"] = Application.GetSystemVariable("CGEOCS");
            }
            catch (Exception failure)
            {
                units["CGEOCS"] = "unreadable: " + failure.Message;
            }
            try
            {
                units["unitZone"] = Reflect.Plain(civil.Settings.DrawingSettings.UnitZoneSettings);
            }
            catch (Exception failure)
            {
                units["unitZone"] = "unreadable: " + failure.Message;
            }
            return units;
        }

        private static List<object> Xrefs(Database db, Transaction tr)
        {
            var xrefs = new List<object>();
            var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId id in table)
            {
                var record = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (record.IsFromExternalReference || record.IsFromOverlayReference)
                {
                    xrefs.Add(new Dictionary<string, object>
                    {
                        ["name"] = record.Name,
                        ["path"] = record.PathName,
                        ["status"] = record.XrefStatus.ToString(),
                        ["overlay"] = record.IsFromOverlayReference,
                    });
                }
            }
            return xrefs;
        }

        private static List<object> Surfaces(CivilDocument civil, Transaction tr)
        {
            var list = new List<object>();
            foreach (ObjectId id in civil.GetSurfaceIds())
            {
                var surface = (CivilSurface)tr.GetObject(id, OpenMode.ForRead);
                var entry = new Dictionary<string, object>
                {
                    ["name"] = surface.Name,
                    ["type"] = surface.GetType().Name,
                    ["layer"] = surface.Layer,
                    ["plain"] = Reflect.Plain(surface),
                };
                try
                {
                    var general = surface.GetGeneralProperties();
                    entry["points"] = general.NumberOfPoints;
                    entry["minZ"] = Round.To(general.MinimumElevation, 4);
                    entry["maxZ"] = Round.To(general.MaximumElevation, 4);
                    entry["meanZ"] = Round.To(general.MeanElevation, 4);
                    entry["extents"] = new[]
                    {
                        Round.To(general.MinimumCoordinateX), Round.To(general.MinimumCoordinateY),
                        Round.To(general.MaximumCoordinateX), Round.To(general.MaximumCoordinateY),
                    };
                }
                catch (Exception failure)
                {
                    entry["generalError"] = failure.Message;
                }
                if (surface is TinSurface tin)
                {
                    try
                    {
                        var tinProperties = tin.GetTinProperties();
                        entry["triangles"] = tinProperties.NumberOfTriangles;
                        entry["tin"] = Reflect.Plain(tinProperties);
                    }
                    catch (Exception failure)
                    {
                        entry["tinError"] = failure.Message;
                    }
                    entry["boundaries"] = Reflect.Items(Reflect.Get(tin, "BoundariesDefinition")).Select(boundary =>
                    {
                        var plain = Reflect.Plain(boundary);
                        plain["polylines"] = Reflect.Count(Reflect.Get(boundary, "PolyLines"));
                        return (object)plain;
                    }).ToList();
                    entry["breaklineGroups"] = Reflect.Items(Reflect.Get(tin, "BreaklinesDefinition")).Select(group =>
                    {
                        var plain = Reflect.Plain(group);
                        plain["entities"] = Reflect.Count(Reflect.Get(group, "BreaklineEntities"));
                        return (object)plain;
                    }).ToList();
                }
                entry["definitions"] = Reflect.Counts(surface, "Definition");
                entry["operations"] = Reflect.Items(Reflect.Get(surface, "Operations"))
                    .GroupBy(operation => operation.GetType().Name)
                    .ToDictionary(group => group.Key, group => group.Count());
                list.Add(entry);
            }
            return list;
        }

        private static List<object> Corridors(CivilDocument civil, Transaction tr)
        {
            var list = new List<object>();
            foreach (ObjectId id in civil.CorridorCollection)
            {
                var corridor = (Corridor)tr.GetObject(id, OpenMode.ForRead);
                var baselines = new List<object>();
                foreach (Baseline baseline in corridor.Baselines)
                {
                    var regions = new List<object>();
                    foreach (BaselineRegion region in baseline.BaselineRegions)
                    {
                        regions.Add(new Dictionary<string, object>
                        {
                            ["name"] = region.Name,
                            ["start"] = Round.To(region.StartStation),
                            ["end"] = Round.To(region.EndStation),
                            ["assembly"] = NameOf(tr, region.AssemblyId),
                        });
                    }
                    var codes = new Dictionary<string, int>();
                    foreach (var group in Reflect.Items(Reflect.Get(baseline.MainBaselineFeatureLines, "FeatureLinesCol")))
                    {
                        foreach (var item in Reflect.Items(group))
                        {
                            string code = (item as CorridorFeatureLine)?.CodeName ?? "?";
                            codes.TryGetValue(code, out int seen);
                            codes[code] = seen + 1;
                        }
                    }
                    baselines.Add(new Dictionary<string, object>
                    {
                        ["name"] = baseline.Name,
                        ["alignment"] = NameOf(tr, baseline.AlignmentId),
                        ["profile"] = NameOf(tr, baseline.ProfileId),
                        ["start"] = Round.To(baseline.StartStation),
                        ["end"] = Round.To(baseline.EndStation),
                        ["regions"] = regions,
                        ["featureLineCodes"] = codes,
                    });
                }
                var surfaces = Reflect.Items(Reflect.Get(corridor, "CorridorSurfaces")).Select(item => (object)new Dictionary<string, object>
                {
                    ["plain"] = Reflect.Plain(item),
                    ["surface"] = Reflect.Get(item, "SurfaceId") is ObjectId surfaceId ? NameOf(tr, surfaceId) : null,
                    ["linkCodes"] = Reflect.Strings(Reflect.Get(item, "LinkCodes")),
                    ["featureLineCodes"] = Reflect.Strings(Reflect.Get(item, "FeatureLineCodes")),
                }).ToList();
                list.Add(new Dictionary<string, object>
                {
                    ["name"] = corridor.Name,
                    ["layer"] = corridor.Layer,
                    ["plain"] = Reflect.Plain(corridor),
                    ["pointCodes"] = Reflect.Strings(Reflect.Get(corridor, "GetPointCodes")),
                    ["linkCodes"] = Reflect.Strings(Reflect.Get(corridor, "GetLinkCodes")),
                    ["shapeCodes"] = Reflect.Strings(Reflect.Get(corridor, "GetShapeCodes")),
                    ["baselines"] = baselines,
                    ["corridorSurfaces"] = surfaces,
                });
            }
            return list;
        }

        private static List<object> Assemblies(CivilDocument civil, Transaction tr)
        {
            var list = new List<object>();
            foreach (ObjectId id in civil.AssemblyCollection)
            {
                var assembly = (CivilAssembly)tr.GetObject(id, OpenMode.ForRead);
                var groups = new List<object>();
                foreach (var group in Reflect.Items(Reflect.Get(assembly, "Groups")))
                {
                    var subassemblies = new List<object>();
                    foreach (var subId in Reflect.Items(Reflect.Get(group, "GetSubassemblyIds")))
                    {
                        if (subId is ObjectId objectId)
                        {
                            subassemblies.Add(Subassembly(tr.GetObject(objectId, OpenMode.ForRead)));
                        }
                    }
                    groups.Add(new Dictionary<string, object>
                    {
                        ["name"] = Reflect.Get(group, "Name"),
                        ["subassemblies"] = subassemblies,
                    });
                }
                list.Add(new Dictionary<string, object> { ["name"] = assembly.Name, ["groups"] = groups });
            }
            return list;
        }

        private static Dictionary<string, object> Subassembly(DBObject subassembly)
        {
            var entry = Reflect.Plain(subassembly);
            entry["class"] = subassembly.GetType().Name;
            foreach (var property in subassembly.GetType().GetProperties())
            {
                if (!property.Name.StartsWith("Params", StringComparison.Ordinal))
                {
                    continue;
                }
                object value;
                try
                {
                    value = property.GetValue(subassembly);
                }
                catch (Exception)
                {
                    continue;
                }
                entry[property.Name] = Reflect.Items(value).Select(item => (object)Reflect.Plain(item)).ToList();
            }
            foreach (string part in new[] { "Points", "Links", "Shapes" })
            {
                entry[part + "Codes"] = Reflect.Items(Reflect.Get(subassembly, part))
                    .Select(item => (object)Reflect.Strings(Reflect.Get(item, "Codes")))
                    .ToList();
            }
            return entry;
        }

        private static List<object> Alignments(CivilDocument civil, Transaction tr)
        {
            var list = new List<object>();
            foreach (ObjectId id in civil.GetAlignmentIds())
            {
                var alignment = (Alignment)tr.GetObject(id, OpenMode.ForRead);
                var profiles = new List<object>();
                foreach (ObjectId profileId in alignment.GetProfileIds())
                {
                    profiles.Add(Reflect.Plain(tr.GetObject(profileId, OpenMode.ForRead)));
                }
                var entry = Reflect.Plain(alignment);
                entry["profiles"] = profiles;
                list.Add(entry);
            }
            return list;
        }

        private static List<object> Sites(CivilDocument civil, Transaction tr)
        {
            var list = new List<object>();
            foreach (ObjectId id in civil.GetSiteIds())
            {
                var site = tr.GetObject(id, OpenMode.ForRead);
                var entry = Reflect.Plain(site);
                entry["idCounts"] = Reflect.IdCounts(site);
                list.Add(entry);
            }
            return list;
        }

        private static Dictionary<string, object> Cogo(CivilDocument civil, Transaction tr)
        {
            var groups = new List<object>();
            foreach (ObjectId id in civil.PointGroups)
            {
                groups.Add(Reflect.Plain(tr.GetObject(id, OpenMode.ForRead)));
            }
            return new Dictionary<string, object>
            {
                ["points"] = civil.CogoPoints.Count,
                ["pointGroups"] = groups,
            };
        }

        private static List<object> PipeNetworks(CivilDocument civil, Transaction tr)
        {
            var list = new List<object>();
            foreach (ObjectId id in civil.GetPipeNetworkIds())
            {
                var network = tr.GetObject(id, OpenMode.ForRead);
                var entry = Reflect.Plain(network);
                entry["idCounts"] = Reflect.IdCounts(network);
                list.Add(entry);
            }
            return list;
        }

        private sealed class Scan
        {
            public Dictionary<string, int> Classes = new Dictionary<string, int>();
            public Dictionary<string, Dictionary<string, object>> Layers = new Dictionary<string, Dictionary<string, object>>();
        }

        // Counts the model space by layer and class, with the Z range of each, so a layer of 3D lines shows
        private static Scan ScanModelSpace(Database db, Transaction tr)
        {
            var scan = new Scan();
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in layerTable)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                scan.Layers[layer.Name] = new Dictionary<string, object>
                {
                    ["off"] = layer.IsOff,
                    ["frozen"] = layer.IsFrozen,
                    ["byClass"] = new Dictionary<string, int>(),
                    ["zRange"] = new Dictionary<string, double[]>(),
                };
            }
            foreach (ObjectId id in ModelSpace(db, tr))
            {
                string className = id.ObjectClass.Name;
                scan.Classes.TryGetValue(className, out int seen);
                scan.Classes[className] = seen + 1;
                if (!(tr.GetObject(id, OpenMode.ForRead, false, true) is Entity entity))
                {
                    continue;
                }
                if (!scan.Layers.TryGetValue(entity.Layer, out var layer))
                {
                    continue;
                }
                var byClass = (Dictionary<string, int>)layer["byClass"];
                byClass.TryGetValue(className, out int count);
                byClass[className] = count + 1;
                // Surfaces and corridors are skipped here, their extents are slow and they report their own levels
                if (className.StartsWith("AeccDbSurface", StringComparison.Ordinal) || className.StartsWith("AeccDbCorridor", StringComparison.Ordinal))
                {
                    continue;
                }
                try
                {
                    var extents = entity.GeometricExtents;
                    var zRange = (Dictionary<string, double[]>)layer["zRange"];
                    if (zRange.TryGetValue(className, out var range))
                    {
                        range[0] = Math.Min(range[0], Round.To(extents.MinPoint.Z, 4));
                        range[1] = Math.Max(range[1], Round.To(extents.MaxPoint.Z, 4));
                    }
                    else
                    {
                        zRange[className] = new[] { Round.To(extents.MinPoint.Z, 4), Round.To(extents.MaxPoint.Z, 4) };
                    }
                }
                catch (Exception)
                {
                }
            }
            foreach (string name in scan.Layers.Keys.ToList())
            {
                if (((Dictionary<string, int>)scan.Layers[name]["byClass"]).Count == 0)
                {
                    scan.Layers[name].Remove("zRange");
                }
            }
            return scan;
        }
    }
}
