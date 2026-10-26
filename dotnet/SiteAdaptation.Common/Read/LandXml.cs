using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace SiteAdaptation.Common
{
    // R349. A LandXML in another unit than meter, or with no unit, refused with the file and the unit named and never converted
    public sealed class LandXmlUnitException : Exception
    {
        public LandXmlUnitException(string message) : base(message)
        {
        }
    }

    // R349. One surface of a LandXML as python/landxml_reader.py keeps it: the points in file order as easting, northing and
    // elevation, the visible faces as indexes into them, and the counts of its summary
    public sealed class LandXmlSurface
    {
        public string Name { get; set; }
        public double[] Points { get; set; }
        public int[] Faces { get; set; }
        public int FaceCount { get; set; }
        public int InvisibleFaceCount { get; set; }
        public int BadFaceCount { get; set; }
        public int BadPointCount { get; set; }
        public string DeclaredZMin { get; set; }
        public string DeclaredZMax { get; set; }
        public bool DeclaredSeen { get; set; }

        public int PointCount => Points.Length / 3;

        public int VisibleFaceCount => Faces.Length / 3;
    }

    public sealed class LandXmlFile
    {
        public string File { get; set; }
        public string EpsgCode { get; set; }
        public string LinearUnit { get; set; }
        public List<LandXmlSurface> Surfaces { get; set; }
    }

    // R349. The C# of python/landxml_reader.read_landxml: a streaming read of P and F inside each Surface, the order of a P being
    // northing, easting, elevation, a face tagged i 1 counted as invisible and left out (R41), a P or F that is not three numbers counted
    // as bad, a face naming a point id the surface lacks counted as bad, a point id given twice refused. The unit is held as R388 holds
    // it in Python: a Units element, then the linearUnit of its Metric or Imperial element, meter only, checked when each surface ends,
    // before the surface is kept, and once more at the end of the file
    public static class LandXmlReader
    {
        public const string Metre = "meter";
        private const string Invisible = "1";

        public static LandXmlFile Read(string path)
        {
            string name = Path.GetFileName(path);
            var file = new LandXmlFile { File = name, Surfaces = new List<LandXmlSurface>() };
            bool unitsSeen = false;
            string unit = null;
            Builder surface = null;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, IgnoreComments = true, IgnoreProcessingInstructions = true };
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        string tag = reader.Name;
                        bool empty = reader.IsEmptyElement;
                        if (tag == "P" || tag == "F")
                        {
                            string id = reader.GetAttribute("id");
                            string visible = reader.GetAttribute("i");
                            string text = empty ? "" : Text(reader);
                            if (surface != null)
                            {
                                if (tag == "P")
                                {
                                    surface.Point(id, text);
                                }
                                else
                                {
                                    surface.Face(visible, text);
                                }
                            }
                            continue;
                        }
                        if (tag == "Surface")
                        {
                            surface = new Builder(reader.GetAttribute("name") ?? "");
                        }
                        else if (tag == "Definition" && surface != null)
                        {
                            surface.Declared(reader.GetAttribute("elevMin"), reader.GetAttribute("elevMax"));
                        }
                        else if (tag == "CoordinateSystem")
                        {
                            file.EpsgCode = reader.GetAttribute("epsgCode");
                        }
                        else if (tag == "Units")
                        {
                            unitsSeen = true;
                        }
                        else if ((tag == "Metric" || tag == "Imperial") && unitsSeen)
                        {
                            unit = reader.GetAttribute("linearUnit");
                        }
                        if (empty && tag == "Surface" && surface != null)
                        {
                            CheckUnit(name, unitsSeen, unit);
                            file.Surfaces.Add(surface.Finish());
                            surface = null;
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "Surface" && surface != null)
                    {
                        // Refused before the surface is kept, so no reader takes a surface of another unit
                        CheckUnit(name, unitsSeen, unit);
                        file.Surfaces.Add(surface.Finish());
                        surface = null;
                    }
                }
            }
            CheckUnit(name, unitsSeen, unit);
            file.LinearUnit = unit;
            return file;
        }

        public static void CheckUnit(string name, bool unitsSeen, string unit)
        {
            if (!unitsSeen)
            {
                throw new LandXmlUnitException(name + " holds no Units element, a LandXML is read only in " + Metre + " (R349)");
            }
            if (unit == null)
            {
                throw new LandXmlUnitException(name + " holds Units with no linearUnit, a LandXML is read only in " + Metre + " (R349)");
            }
            if (unit != Metre)
            {
                throw new LandXmlUnitException(name + " is in " + unit + ", a LandXML is read only in " + Metre + " and never converted (R349)");
            }
        }

        // The text of a P or F up to its end, the reader left on its end tag
        private static string Text(XmlReader reader)
        {
            var text = new StringBuilder();
            int depth = reader.Depth;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA || reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.SignificantWhitespace)
                {
                    text.Append(reader.Value);
                }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                {
                    break;
                }
            }
            return text.ToString();
        }

        // Python str.split() with no argument parts on runs of whitespace and drops the empty ends
        private static string[] Parts(string text)
        {
            return text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool Number(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool Whole(string text, out long value)
        {
            return long.TryParse((text ?? "").Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        private sealed class Builder
        {
            private readonly LandXmlSurface surface;
            private readonly List<long> ids = new List<long>();
            private readonly List<double> xyz = new List<double>();
            private readonly List<long> faces = new List<long>();

            public Builder(string name)
            {
                surface = new LandXmlSurface { Name = name };
            }

            public void Declared(string low, string high)
            {
                surface.DeclaredSeen = true;
                surface.DeclaredZMin = low;
                surface.DeclaredZMax = high;
            }

            public void Point(string id, string text)
            {
                string[] parts = Parts(text);
                double northing, easting, z;
                long pointId;
                if (parts.Length != 3 || !Number(parts[0], out northing) || !Number(parts[1], out easting) || !Number(parts[2], out z) || !Whole(id, out pointId))
                {
                    surface.BadPointCount++;
                    return;
                }
                ids.Add(pointId);
                xyz.Add(easting);
                xyz.Add(northing);
                xyz.Add(z);
            }

            public void Face(string visible, string text)
            {
                surface.FaceCount++;
                if (visible == Invisible)
                {
                    surface.InvisibleFaceCount++;
                    return;
                }
                string[] parts = Parts(text);
                var corners = new long[3];
                bool good = parts.Length == 3;
                for (int index = 0; good && index < 3; index++)
                {
                    good = Whole(parts[index], out corners[index]);
                }
                if (!good)
                {
                    surface.BadFaceCount++;
                    return;
                }
                faces.AddRange(corners);
            }

            // The faces by the index of each point id, a face naming a missing id dropped and counted as bad (build_arrays)
            public LandXmlSurface Finish()
            {
                var index = new Dictionary<long, int>(ids.Count);
                for (int position = 0; position < ids.Count; position++)
                {
                    if (index.ContainsKey(ids[position]))
                    {
                        throw new InvalidDataException("surface " + surface.Name + " has repeated point ids");
                    }
                    index[ids[position]] = position;
                }
                var kept = new List<int>(faces.Count);
                for (int face = 0; face < faces.Count; face += 3)
                {
                    int a, b, c;
                    if (index.TryGetValue(faces[face], out a) && index.TryGetValue(faces[face + 1], out b) && index.TryGetValue(faces[face + 2], out c))
                    {
                        kept.Add(a);
                        kept.Add(b);
                        kept.Add(c);
                    }
                    else
                    {
                        surface.BadFaceCount++;
                    }
                }
                surface.Points = xyz.ToArray();
                surface.Faces = kept.ToArray();
                return surface;
            }
        }
    }
}
