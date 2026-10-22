using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;
using Xunit.Abstractions;

namespace SiteAdaptation.Tests
{
    // Same approach as LinkWallRampTests. Test doubles prove the logic and the JSON shape and nothing about a real model
    public class LinkNameFloorTests
    {
        private readonly ITestOutputHelper output;

        public LinkNameFloorTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static LinkWallRampTests.FakeSource Villas(string mark)
        {
            var source = new LinkWallRampTests.FakeSource();
            source.InstanceList.Add(LinkWallRampTests.Villa(mark, false));
            return source;
        }

        private static SourceLink Instance(string file, string name, Dictionary<string, object> parameters, IModelSource inside)
        {
            return new SourceLink
            {
                File = file,
                Name = name,
                Parameters = parameters,
                Transform = LinkMath.Describe(new[] { 1.0, 2, 3 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, LinkWallRampTests.North),
                Open = () => inside,
            };
        }

        private static string[] Errors(JToken entry)
        {
            return entry["errors"] == null ? new string[0] : entry["errors"].Select(line => (string)line).ToArray();
        }

        [Fact]
        public void R16_link_not_loaded_still_returns_its_name_and_every_parameter()
        {
            var parameters = LinkWallRampTests.FullParameters("9001", "VX1");
            parameters["Comments"] = "long text kept whole " + new string('x', 400);
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(Instance("Type A.rvt", "9001 | VX1 | 1 | XX | XX", parameters, null));

            JObject document = LinkWallRampTests.Run(host);
            JToken link = document["links"].Single();

            Assert.Equal("Type A.rvt", (string)link["file"]);
            Assert.Equal("9001 | VX1 | 1 | XX | XX", (string)link["name"]);
            Assert.Equal("9001", (string)link["plotNumber"]);
            Assert.Equal("VX1", (string)link["prototype"]);
            Assert.Equal("10.500", (string)link["ffl"]);
            Assert.Equal("2", (string)link["steps"]);
            Assert.Equal("C1", (string)link["case"]);
            Assert.Equal(6, link["parameters"].Count());
            Assert.Equal(421, ((string)link["parameters"]["Comments"]).Length);
            Assert.Equal(new[] { "Link is not loaded" }, Errors(link));
            Assert.Null(link["contentsRef"]);
            Assert.Equal(new[] { 1.0, 2, 3 }, LinkWallRampTests.Numbers(link["transform"]["origin"]));
            Assert.Empty(document["linkedFiles"]);
        }

        [Fact]
        public void R16_name_with_five_parts_is_split_into_plot_prototype_steps_color_and_option()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(Instance("Type A.rvt", "9001 | VX1 | 1 | XX | XX", LinkWallRampTests.FullParameters(), null));
            host.LinkList.Add(Instance("Type A.rvt", "9002 | VX2 | 3 | RED | B", LinkWallRampTests.FullParameters(), null));

            JToken first = LinkWallRampTests.Run(host)["links"][0]["nameParts"];

            Assert.Equal("9001", (string)first["plot"]);
            Assert.Equal("VX1", (string)first["prototype"]);
            Assert.Equal("1", (string)first["steps"]);
            Assert.Equal("XX", (string)first["facadeColor"]);
            Assert.Equal("XX", (string)first["designOption"]);
            JToken second = LinkWallRampTests.Run(host)["links"][1]["nameParts"];
            Assert.Equal("RED", (string)second["facadeColor"]);
            Assert.Equal("B", (string)second["designOption"]);
        }

        [Fact]
        public void R16_name_with_fewer_parts_writes_null_for_the_rest_and_adds_an_error_line()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(Instance("Type A.rvt", "9001 | VX1 | 1", LinkWallRampTests.FullParameters(), null));
            host.LinkList.Add(Instance("Type A.rvt", "9001 | VX1 | 1 | XX | XX | extra", LinkWallRampTests.FullParameters(), null));
            host.LinkList.Add(Instance("Type A.rvt", null, LinkWallRampTests.FullParameters(), null));

            JToken links = LinkWallRampTests.Run(host)["links"];
            JToken parts = links[0]["nameParts"];

            Assert.Equal("1", (string)parts["steps"]);
            Assert.Equal(JTokenType.Null, parts["facadeColor"].Type);
            Assert.Equal(JTokenType.Null, parts["designOption"].Type);
            Assert.NotNull(((JObject)parts).Property("designOption"));
            Assert.Equal(new[] { "Link name has 3 of 5 parts", "Link is not loaded" }, Errors(links[0]));
            Assert.Contains("Link name has 6 parts and only the first 5 are read", Errors(links[1]));
            Assert.Equal("XX", (string)links[1]["nameParts"]["designOption"]);
            Assert.Contains("Link name is empty", Errors(links[2]));
            Assert.Equal(JTokenType.Null, links[2]["nameParts"]["plot"].Type);
        }

        [Fact]
        public void R16_missing_parameter_is_written_as_null_and_listed_in_errors()
        {
            var parameters = LinkWallRampTests.FullParameters();
            parameters.Remove(LinkFields.Ffl);
            parameters[LinkFields.Case] = "   ";
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(Instance("Type A.rvt", "17 | VA | 2 | XX | XX", parameters, null));

            JToken link = LinkWallRampTests.Run(host)["links"].Single();

            Assert.NotNull(((JObject)link).Property("ffl"));
            Assert.Equal(JTokenType.Null, link["ffl"].Type);
            Assert.Equal(JTokenType.Null, link["case"].Type);
            Assert.Equal("17", (string)link["plotNumber"]);
            string[] errors = Errors(link);
            Assert.Contains("Parameter RRE_ZZ_SP_Prototype-FFL is missing", errors);
            Assert.Contains("Parameter CASE is empty", errors);
            Assert.Equal(3, errors.Length);
        }

        [Fact]
        public void R16_prototype_comes_from_the_parameter_and_not_from_the_file_name()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(Instance("Type A.rvt", "9001 | VX1 | 1 | XX | XX", LinkWallRampTests.FullParameters("9001", "VX1"), null));

            JToken link = LinkWallRampTests.Run(host)["links"].Single();

            Assert.Equal("Type A.rvt", (string)link["file"]);
            Assert.Equal("VX1", (string)link["prototype"]);
        }

        [Fact]
        public void R16_a_link_with_reading_problems_keeps_them_and_stays_in_the_list()
        {
            var host = new LinkWallRampTests.FakeSource();
            SourceLink link = Instance("Type A.rvt", "17 | VA | 2 | XX | XX", LinkWallRampTests.FullParameters(), null);
            link.Problems = new List<string> { "parameter Foo: InvalidOperationException: boom" };
            link.Transform = null;
            host.LinkList.Add(link);

            JToken entry = LinkWallRampTests.Run(host)["links"].Single();

            Assert.Equal("parameter Foo: InvalidOperationException: boom", Errors(entry)[0]);
            Assert.Null(entry["transform"]);
            Assert.Equal("17", (string)entry["plotNumber"]);
        }

        [Fact]
        public void R16_two_links_to_one_file_read_its_contents_once_from_the_first_loaded_link()
        {
            var unloaded = (IModelSource)null;
            var firstLoaded = Villas("from the first loaded link");
            var secondLoaded = Villas("from the second loaded link");
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(Instance("Type A.rvt", "1 | VX1 | 1 | XX | XX", LinkWallRampTests.FullParameters("1"), unloaded));
            host.LinkList.Add(Instance("Type A.rvt", "2 | VX1 | 1 | XX | XX", LinkWallRampTests.FullParameters("2"), firstLoaded));
            host.LinkList.Add(Instance("type a.rvt", "3 | VX1 | 1 | XX | XX", LinkWallRampTests.FullParameters("3"), secondLoaded));

            JObject document = LinkWallRampTests.Run(host);

            JToken file = document["linkedFiles"].Single();
            Assert.Equal("Type A.rvt", (string)file["file"]);
            Assert.Equal("from the first loaded link", (string)file["instances"][0]["parameters"]["Mark"]);
            Assert.Equal(1, firstLoaded.InstanceCalls);
            Assert.Equal(0, secondLoaded.InstanceCalls);
            Assert.All(document["links"], link => Assert.Null(link["instances"]));
            Assert.Equal(3, document["links"].Count(link => (string)link["contentsRef"] == "Type A.rvt"));
        }

        [Fact]
        public void R16_link_contents_none_reads_no_contents_but_keeps_name_and_parameters()
        {
            var loaded = Villas("never read");
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(Instance("Type A.rvt", "1 | VX1 | 1 | XX | XX", LinkWallRampTests.FullParameters("1"), loaded));
            host.LinkList.Add(Instance("Type B.rvt", "2 | VX2 | 1 | XX | XX", LinkWallRampTests.FullParameters("2"), null));

            JObject document = LinkWallRampTests.Run(host, new ReaderOptions { LinkContents = LinkContents.None });

            Assert.Null(document["linkedFiles"]);
            Assert.Equal(0, loaded.InstanceCalls);
            Assert.Equal(2, document["links"].Count());
            Assert.All(document["links"], link => Assert.Null(link["contentsRef"]));
            Assert.Equal("1", (string)document["links"][0]["plotNumber"]);
            Assert.Empty(Errors(document["links"][0]));
            Assert.Equal("2", (string)document["links"][1]["plotNumber"]);
            Assert.Equal(new[] { "Link is not loaded" }, Errors(document["links"][1]));
        }

        [Fact]
        public void R16_link_contents_flag_is_parsed_from_arguments_and_from_the_options_file()
        {
            Assert.Equal(LinkContents.Once, new ReaderOptions().LinkContents);
            Assert.Equal(LinkContents.Once, ReaderOptions.Parse(new string[0]).LinkContents);
            Assert.Equal(LinkContents.None, ReaderOptions.Parse(new[] { "--link-contents", "none" }).LinkContents);
            Assert.Equal(LinkContents.Once, ReaderOptions.Parse(new[] { "--link-contents=ONCE" }).LinkContents);
            Assert.Throws<ArgumentException>(() => ReaderOptions.Parse(new[] { "--link-contents", "all" }));
            Assert.Throws<ArgumentException>(() => ReaderOptions.Parse(new[] { "--link-contents" }));
            Assert.Throws<ArgumentException>(() => ReaderOptions.Parse(new[] { "--other", "x" }));

            string folder = Path.Combine(Path.GetTempPath(), "options-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                Assert.Equal(LinkContents.Once, ReaderOptions.Load(folder).LinkContents);
                File.WriteAllText(Path.Combine(folder, ReaderOptions.FileName), "# comment\n--link-contents none\n");
                Assert.Equal(LinkContents.None, ReaderOptions.Load(folder).LinkContents);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void R16_once_keeps_the_file_small_when_many_links_share_few_files()
        {
            var sources = new[] { Villas("a"), Villas("b"), Villas("c") };
            foreach (var source in sources)
            {
                for (int index = 0; index < 60; index++)
                {
                    source.InstanceList.Add(LinkWallRampTests.Villa("V-" + index, index % 2 == 0));
                }
            }
            var host = new LinkWallRampTests.FakeSource();
            for (int index = 0; index < 90; index++)
            {
                string plot = (2000 + index).ToString();
                host.LinkList.Add(Instance("File" + (index % 3) + ".rvt", plot + " | VX1 | 1 | XX | XX", LinkWallRampTests.FullParameters(plot), sources[index % 3]));
            }

            JObject once = LinkWallRampTests.Run(host);
            int onceSize = once.ToString().Length;

            // The old shape: the same contents copied into every link entry
            JObject copied = (JObject)once.DeepClone();
            foreach (JToken link in copied["links"])
            {
                JToken file = copied["linkedFiles"].Single(item => (string)item["file"] == (string)link["contentsRef"]);
                ((JObject)link)["instances"] = file["instances"].DeepClone();
                ((JObject)link).Remove("contentsRef");
            }
            copied.Remove("linkedFiles");
            int copiedSize = copied.ToString().Length;

            output.WriteLine("dummy case: 90 links, 3 files, 61 instances each. once " + onceSize + " characters, every link copied " + copiedSize + " characters");
            Assert.Equal(3, once["linkedFiles"].Count());
            Assert.All(sources, source => Assert.Equal(1, source.InstanceCalls));
            Assert.True(onceSize * 5 < copiedSize);
        }

        [Fact]
        public void R17_floors_are_read_from_the_host_and_never_from_inside_a_link()
        {
            var linked = Villas("v");
            linked.FloorList.Add(new FloorRow { TypeName = "Inside the link", ElementId = 9 });
            var host = new LinkWallRampTests.FakeSource();
            host.LinkList.Add(Instance("Type A.rvt", "1 | VX1 | 1 | XX | XX", LinkWallRampTests.FullParameters(), linked));
            host.FloorList.Add(new FloorRow { TypeName = "Generic 150", ElementId = 1 });

            JObject document = LinkWallRampTests.Run(host);

            Assert.Equal("Generic 150", (string)document["floors"].Single()["typeName"]);
            Assert.Equal(0, linked.FloorCalls);
        }

        [Fact]
        public void R17_floor_with_an_arc_in_its_boundary_keeps_centre_and_radius()
        {
            var host = new LinkWallRampTests.FakeSource();
            var loops = new List<IList<BoundaryEdge>>
            {
                new List<BoundaryEdge>
                {
                    Straight(0, 0, 10, 0),
                    Straight(10, 0, 10, 10),
                    new BoundaryEdge { Start = new[] { 10.0, 10, 0 }, End = new[] { 0.0, 10, 0 }, Center = new[] { 5.0, 10, 0 }, Radius = 5, Ccw = true },
                    Straight(0, 10, 0, 0),
                },
            };
            List<double[]> boundary;
            List<FloorArc> arcs;
            Assert.True(FloorMath.TryOuter(loops, out boundary, out arcs));
            host.FloorList.Add(new FloorRow
            {
                TypeName = "Generic 150",
                ElementId = 4001,
                Level = "Level 0",
                HeightOffset = 0.15,
                TopElevation = 3.15,
                Thickness = 0.15,
                Area = 139.27,
                Boundary = boundary,
                Arcs = arcs,
                Parameters = ParameterBag.Collect(new Dictionary<string, object> { { "Mark", "F-01" }, { "Comments", "arc side" }, { "Room Bounding", 1 } }),
            });

            JToken floor = LinkWallRampTests.Run(host)["floors"].Single();

            Assert.Equal("Generic 150", (string)floor["typeName"]);
            Assert.Equal(4001, (long)floor["elementId"]);
            Assert.Equal("Level 0", (string)floor["level"]);
            Assert.Equal(0.15, (double)floor["heightOffset"], 3);
            Assert.Equal(3.15, (double)floor["topElevation"], 3);
            Assert.Equal(0.15, (double)floor["thickness"], 3);
            Assert.Equal(139.27, (double)floor["area"], 3);
            Assert.Equal(4, floor["boundary"].Count());
            Assert.Equal(new[] { 10.0, 10, 0 }, LinkWallRampTests.Numbers(floor["boundary"][2]));
            JToken arc = floor["arcs"].Single();
            Assert.Equal(2, (int)arc["edge"]);
            Assert.Equal(new[] { 5.0, 10, 0 }, LinkWallRampTests.Numbers(arc["center"]));
            Assert.Equal(5, (double)arc["radius"], 3);
            Assert.True((bool)arc["ccw"]);
            Assert.Equal(3, floor["parameters"].Count());
            Assert.Equal("arc side", (string)floor["parameters"]["Comments"]);
        }

        [Fact]
        public void R17_outer_boundary_is_the_loop_with_the_most_area_and_curves_are_cut_into_straight_pieces()
        {
            var hole = new List<BoundaryEdge> { Straight(2, 2, 4, 2), Straight(4, 2, 4, 4), Straight(4, 4, 2, 4), Straight(2, 4, 2, 2) };
            var outer = new List<BoundaryEdge>
            {
                Straight(0, 0, 10, 0),
                Straight(10, 0, 10, 10),
                new BoundaryEdge
                {
                    Start = new[] { 10.0, 10, 0 },
                    End = new[] { 0.0, 10, 0 },
                    Via = new List<double[]> { new[] { 7.0, 11, 0 }, new[] { 3.0, 11, 0 } },
                },
                Straight(0, 10, 0, 0),
            };
            List<double[]> vertices;
            List<FloorArc> arcs;

            Assert.True(FloorMath.TryOuter(new IList<BoundaryEdge>[] { hole, outer }, out vertices, out arcs));

            Assert.Equal(6, vertices.Count);
            Assert.Equal(new[] { 7.0, 11, 0 }, vertices[3]);
            Assert.Empty(arcs);
            Assert.False(FloorMath.TryOuter(new IList<BoundaryEdge>[] { new List<BoundaryEdge> { Straight(0, 0, 1, 0) } }, out vertices, out arcs));
            Assert.Null(vertices);
        }

        [Fact]
        public void R17_a_failure_reading_floors_does_not_lose_walls_and_ramps()
        {
            var host = new LinkWallRampTests.FakeSource();
            host.FloorsFailure = () => { throw new InvalidOperationException("no floors"); };
            host.WallList.Add(new WallRow { TypeName = "Generic - 200mm", ElementId = 1 });
            host.RampList.Add(new RampRow { TypeName = "Ramp 1", ElementId = 2 });

            JObject document = LinkWallRampTests.Run(host);

            Assert.Empty(document["floors"]);
            Assert.Single(document["walls"]);
            Assert.Single(document["ramps"]);
            Assert.Equal("floors: InvalidOperationException: no floors", (string)document["errors"][0]);
        }

        private static BoundaryEdge Straight(double x1, double y1, double x2, double y2)
        {
            return new BoundaryEdge { Start = new[] { x1, y1, 0 }, End = new[] { x2, y2, 0 } };
        }
    }
}
