using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R349. The LandXML read and the road level snap of Common on made up files, the C# of landxml_reader.read_landxml,
    // surface_lookup.Surface, c3d_connect.snap_z and plan_from_book.road_level, on net48 and net8.0
    public sealed class LandXmlTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "sa_r349_" + Guid.NewGuid().ToString("N"));

        public LandXmlTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
        }

        // A square of 10 m on the plane z = 10 + easting + 2 northing in two triangles, a face tagged invisible, a face of two corners,
        // a face of an id the surface lacks and a point of two numbers. A P holds northing, easting, elevation
        private const string Square = "<P id=\"1\">0 0 10</P><P id=\"2\">0 10 20</P><P id=\"3\">10 0 30</P><P id=\"4\">10 10 40</P><P id=\"5\">5 6</P>"
            + "</Pnts><Faces><F>1 2 3</F><F>2 4 3</F><F i=\"1\">1 2 4</F><F>1 2</F><F>1 2 99</F></Faces>";

        private string Write(string name, string units, string surfaces)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, "<?xml version=\"1.0\"?><LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\" version=\"1.2\">" + units
                + "<Surfaces>" + surfaces + "</Surfaces></LandXML>");
            return path;
        }

        private static string Surface(string name, string body)
        {
            return "<Surface name=\"" + name + "\"><Definition surfType=\"TIN\" elevMin=\"10\" elevMax=\"40\"><Pnts>" + body + "</Definition></Surface>";
        }

        private const string Metre = "<Units><Metric linearUnit=\"meter\" areaUnit=\"squareMeter\"/></Units>";

        [Fact]
        public void R349_a_landxml_is_read_as_the_python_reader_reads_it_points_faces_and_counts()
        {
            LandXmlFile file = LandXmlReader.Read(Write("a.xml", Metre, Surface("Top", Square)));
            Assert.Equal("meter", file.LinearUnit);
            LandXmlSurface surface = file.Surfaces.Single();
            Assert.Equal("Top", surface.Name);
            Assert.Equal(4, surface.PointCount);
            // Easting first, the P gives the northing first
            Assert.Equal(new double[] { 0, 0, 10, 10, 0, 20, 0, 10, 30, 10, 10, 40 }, surface.Points);
            Assert.Equal(new[] { 0, 1, 2, 1, 3, 2 }, surface.Faces);
            Assert.Equal(5, surface.FaceCount);
            Assert.Equal(1, surface.InvisibleFaceCount);
            Assert.Equal(2, surface.BadFaceCount);
            Assert.Equal(1, surface.BadPointCount);
            Assert.Equal("10", surface.DeclaredZMin);
            Assert.Equal("40", surface.DeclaredZMax);
        }

        [Theory]
        [InlineData("<Units><Imperial linearUnit=\"foot\"/></Units>", "is in foot")]
        [InlineData("<Units><Imperial linearUnit=\"USSurveyFoot\"/></Units>", "is in USSurveyFoot")]
        [InlineData("<Units><Metric linearUnit=\"millimeter\"/></Units>", "is in millimeter")]
        [InlineData("", "holds no Units element")]
        [InlineData("<Units><Metric areaUnit=\"squareMeter\"/></Units>", "holds Units with no linearUnit")]
        public void R349_a_landxml_with_no_unit_or_not_in_meter_is_refused_with_the_file_and_the_unit_named(string units, string why)
        {
            string path = Write("b.xml", units, Surface("Top", Square));
            var refused = Assert.Throws<LandXmlUnitException>(() => LandXmlReader.Read(path));
            Assert.Contains("b.xml " + why, refused.Message);
            Assert.Contains("R349", refused.Message);
            // A file with no surface is refused as well once it is read
            string empty = Write("c.xml", units, "");
            Assert.Throws<LandXmlUnitException>(() => LandXmlReader.Read(empty));
        }

        [Fact]
        public void R349_a_point_id_given_twice_is_refused()
        {
            string path = Write("d.xml", Metre, Surface("Top", "<P id=\"1\">0 0 1</P><P id=\"1\">0 1 1</P></Pnts><Faces></Faces>"));
            Assert.Contains("repeated point ids", Assert.Throws<InvalidDataException>(() => LandXmlReader.Read(path)).Message);
        }

        [Fact]
        public void R349_the_level_of_a_point_comes_from_the_visible_triangle_that_holds_it()
        {
            TinSurface surface = TinSurface.Of(LandXmlReader.Read(Write("e.xml", Metre, Surface("Top", Square))).Surfaces.Single());
            Assert.Equal(2, surface.Count);
            Assert.Equal(18.0, surface.ZAt(2, 3).Value, 9);
            Assert.Equal(40.0, surface.ZAt(10, 10).Value, 9);
            // On the shared edge, the triangle of the lowest index answers, the plane is one so the level is the same
            Assert.Equal(25.0, surface.ZAt(5, 5).Value, 9);
            Assert.Null(surface.ZAt(10.003, 5));
            Assert.Null(surface.ZAt(-1, 5));
        }

        [Fact]
        public void R349_a_point_off_the_edge_within_the_snap_takes_the_level_of_the_nearest_side_and_further_off_none()
        {
            TinSurface surface = TinSurface.Of(LandXmlReader.Read(Write("f.xml", Metre, Surface("Top", Square))).Surfaces.Single());
            Assert.Equal(30.0, surface.SnapZ(10.003, 5, 0.005).Value, 9);
            Assert.Null(surface.SnapZ(10.003, 5, 0.002));
            Assert.Equal(18.0, surface.SnapZ(2, 3, 0.005).Value, 9);
            Assert.Null(surface.SnapZ(30, 30, 0.005));
        }

        [Fact]
        public void R349_the_road_level_is_the_level_of_the_first_surface_of_the_order_that_covers_the_point()
        {
            // B lies under A on the square and goes on to the east, a point A covers takes A, one only B covers takes B
            string wide = "<P id=\"1\">0 0 0</P><P id=\"2\">0 30 0</P><P id=\"3\">10 0 0</P><P id=\"4\">10 30 0</P></Pnts><Faces><F>1 2 3</F><F>2 4 3</F></Faces>";
            LandXmlFile file = LandXmlReader.Read(Write("g.xml", Metre, Surface("A", Square) + Surface("B", wide)));
            var surfaces = file.Surfaces.Select(TinSurface.Of).ToList();
            KeyValuePair<double?, string> level = TinSurface.RoadLevel(surfaces, 2, 3, 0.005);
            Assert.Equal("A", level.Value);
            Assert.Equal(18.0, level.Key.Value, 9);
            level = TinSurface.RoadLevel(surfaces, 20, 3, 0.005);
            Assert.Equal("B", level.Value);
            Assert.Equal(0.0, level.Key.Value, 9);
            level = TinSurface.RoadLevel(surfaces, 40, 3, 0.005);
            Assert.Null(level.Key);
            Assert.Null(level.Value);
            surfaces.Reverse();
            Assert.Equal("B", TinSurface.RoadLevel(surfaces, 2, 3, 0.005).Value);
        }
    }
}
