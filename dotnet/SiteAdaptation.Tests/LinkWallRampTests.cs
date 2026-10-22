using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // The Revit API cannot be built outside Revit, so these tests drive the reader with a hand built IModelSource.
    // They prove the logic and the JSON shape. They do not prove that RevitModelSource reads a real model correctly
    public class LinkWallRampTests
    {
        private sealed class FakeSource : IModelSource
        {
            public IList<string> Errors { get; } = new List<string>();
            public List<SourceLink> LinkList { get; } = new List<SourceLink>();
            public List<PrototypeRow> InstanceList { get; } = new List<PrototypeRow>();
            public List<WallRow> WallList { get; } = new List<WallRow>();
            public List<RampRow> RampList { get; } = new List<RampRow>();
            public Func<IEnumerable<SourceLink>> LinksFailure { get; set; }

            public IEnumerable<SourceLink> Links()
            {
                if (LinksFailure != null)
                {
                    return LinksFailure();
                }
                return LinkList;
            }

            public IEnumerable<PrototypeRow> Instances()
            {
                return InstanceList;
            }

            public IEnumerable<WallRow> Walls()
            {
                return WallList;
            }

            public IEnumerable<RampRow> Ramps()
            {
                return RampList;
            }
        }

        private static readonly double[] North = { 0, 0, 1 };

        private static SourceLink Link(string file, double[] origin, double angleDeg, bool mirrored, IModelSource inside)
        {
            double radians = angleDeg * Math.PI / 180;
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            double[] axisX = { c, s, 0 };
            // A mirror image flips the Y axis. The three axes then form a left handed set
            double[] axisY = mirrored ? new[] { s, -c, 0 } : new[] { -s, c, 0 };
            return new SourceLink
            {
                File = file,
                Transform = LinkMath.Describe(origin, axisX, axisY, North),
                Open = () => inside,
            };
        }

        private static PrototypeRow Villa(string mark, bool mirrored)
        {
            // Built the way RevitModelSource builds it, instance parameters merged over type parameters
            var instance = ParameterBag.Collect(new Dictionary<string, object>
            {
                { "Mark", mark },
                { "Plot Number", 17 },
                { "Comments", "north facing" },
            });
            var type = ParameterBag.Collect(new Dictionary<string, object>
            {
                { "Width", 12.5 },
                { "Depth", 20.0 },
                { "Type Mark", "T1" },
                { "Mark", "type mark loses to the instance" },
            });
            return new PrototypeRow
            {
                FamilyName = "Villa A",
                TypeName = "Type 1",
                Placement = "placed",
                ElementId = 4711,
                Parameters = ParameterBag.Merge(instance, type),
                BoundingBox = new BoxInfo { Min = new[] { 4.0, -5.0, 0 }, Max = new[] { 16.5, 15.0, 9 } },
                InsertionPoint = new[] { 10.0, 5, 0 },
                RotationDeg = 90,
                Level = "Level 0",
                Mirrored = mirrored,
            };
        }

        private static JObject Run(IModelSource host)
        {
            var entry = new DocumentEntry { File = "site.rvt", Kind = "project", Prototypes = new List<PrototypeRow>() };
            ModelReader.ReadSections(host, entry);
            var file = new PrototypesFile { Tool = "RevitPrototypeReader", Documents = new List<DocumentEntry> { entry } };
            return (JObject)JObject.Parse(JsonOut.ToJson(file))["documents"][0];
        }

        private static double[] Numbers(JToken token)
        {
            return token.Select(item => (double)item).ToArray();
        }

        [Fact]
        public void R16_link_with_translation_and_rotation_is_read_with_its_instances()
        {
            var villas = new FakeSource();
            villas.InstanceList.Add(Villa("V-01", false));
            var host = new FakeSource();
            host.LinkList.Add(Link("Villa Prototypes.rvt", new[] { 100.5, 200.25, 3 }, 30, false, villas));

            JObject document = Run(host);
            JToken link = document["links"].Single();

            Assert.Equal("Villa Prototypes.rvt", (string)link["file"]);
            Assert.Equal(new[] { 100.5, 200.25, 3 }, Numbers(link["transform"]["origin"]));
            Assert.Equal(30, (double)link["transform"]["rotationDeg"], 3);
            Assert.False((bool)link["transform"]["mirrored"]);
            Assert.Equal(new[] { 0.866, 0.5, 0 }, Numbers(link["transform"]["axisX"]));
            Assert.Equal(new[] { -0.5, 0.866, 0 }, Numbers(link["transform"]["axisY"]));

            JToken instance = link["instances"].Single();
            Assert.Equal("Villa A", (string)instance["familyName"]);
            Assert.Equal("Type 1", (string)instance["typeName"]);
            Assert.Equal(new[] { 10.0, 5, 0 }, Numbers(instance["insertionPoint"]));
            Assert.Equal(90, (double)instance["rotationDeg"], 3);
            Assert.Equal("Level 0", (string)instance["level"]);
            Assert.False((bool)instance["mirrored"]);
        }

        [Fact]
        public void R16_link_instances_carry_instance_and_type_parameters_with_the_instance_winning()
        {
            var villas = new FakeSource();
            villas.InstanceList.Add(Villa("V-01", false));
            var host = new FakeSource();
            host.LinkList.Add(Link("Villa Prototypes.rvt", new[] { 0.0, 0, 0 }, 0, false, villas));

            JToken parameters = Run(host)["links"][0]["instances"][0]["parameters"];

            Assert.Equal("V-01", (string)parameters["Mark"]);
            Assert.Equal(17, (int)parameters["Plot Number"]);
            Assert.Equal("north facing", (string)parameters["Comments"]);
            Assert.Equal(12.5, (double)parameters["Width"], 3);
            Assert.Equal(20, (double)parameters["Depth"], 3);
            Assert.Equal("T1", (string)parameters["Type Mark"]);
            Assert.Equal(6, parameters.Count());
        }

        [Fact]
        public void R16_mirrored_link_is_flagged_and_keeps_its_axes()
        {
            var villas = new FakeSource();
            villas.InstanceList.Add(Villa("V-02", true));
            var host = new FakeSource();
            host.LinkList.Add(Link("Mirrored Villas.rvt", new[] { -40.0, 12, 0 }, 30, true, villas));

            JToken link = Run(host)["links"].Single();

            Assert.True((bool)link["transform"]["mirrored"]);
            Assert.Equal(30, (double)link["transform"]["rotationDeg"], 3);
            Assert.Equal(new[] { 0.5, -0.866, 0 }, Numbers(link["transform"]["axisY"]));
            Assert.True((bool)link["instances"][0]["mirrored"]);
        }

        [Fact]
        public void R16_flipping_the_x_axis_alone_is_a_mirror_turned_half_a_circle()
        {
            LinkTransform transform = LinkMath.Describe(new[] { 0.0, 0, 0 }, new[] { -1.0, 0, 0 }, new[] { 0.0, 1, 0 }, North);

            Assert.True(transform.Mirrored);
            Assert.Equal(180, transform.RotationDeg, 6);
        }

        [Fact]
        public void R16_rotation_is_counter_clockwise_and_wraps_into_0_to_360()
        {
            double[] y = { 0, 1, 0 };
            Assert.Equal(270, LinkMath.PlanAngleDeg(new[] { 0.0, -1, 0 }), 6);
            Assert.Equal(0, LinkMath.PlanAngleDeg(new[] { 1.0, 0, 0 }), 6);
            Assert.Equal(0, LinkMath.PlanAngleDeg(new[] { Math.Cos(-1e-12), Math.Sin(-1e-12), 0 }), 6);
            Assert.False(LinkMath.Describe(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, y, North).Mirrored);
        }

        [Fact]
        public void R16_links_inside_a_link_are_read_one_level_deep_and_deeper_ones_are_counted()
        {
            var third = new FakeSource();
            third.InstanceList.Add(Villa("never read", false));
            var second = new FakeSource();
            second.InstanceList.Add(Villa("V-03", false));
            second.LinkList.Add(Link("Third.rvt", new[] { 0.0, 0, 0 }, 0, false, third));
            second.LinkList.Add(Link("Third Again.rvt", new[] { 0.0, 0, 0 }, 0, false, third));
            var first = new FakeSource();
            first.LinkList.Add(Link("Second.rvt", new[] { 5.0, 0, 0 }, 90, false, second));
            var host = new FakeSource();
            host.LinkList.Add(Link("First.rvt", new[] { 0.0, 0, 0 }, 0, false, first));

            JToken top = Run(host)["links"].Single();
            JToken nested = top["links"].Single();

            Assert.Equal("First.rvt", (string)top["file"]);
            Assert.Equal("Second.rvt", (string)nested["file"]);
            Assert.Equal(90, (double)nested["transform"]["rotationDeg"], 3);
            Assert.Equal("V-03", (string)nested["instances"][0]["parameters"]["Mark"]);
            Assert.Null(nested["links"]);
            Assert.Equal(2, (int)nested["deeperLinks"]);
            Assert.DoesNotContain("Third.rvt", nested.ToString());
        }

        [Fact]
        public void R16_a_link_that_is_not_loaded_or_fails_is_recorded_and_the_others_still_read()
        {
            var good = new FakeSource();
            good.InstanceList.Add(Villa("V-04", false));
            good.Errors.Add("instance 99: ArgumentException: bad element");
            var host = new FakeSource();
            host.LinkList.Add(Link("A Unloaded.rvt", new[] { 0.0, 0, 0 }, 0, false, null));
            host.LinkList.Add(new SourceLink
            {
                File = "B Broken.rvt",
                Transform = LinkMath.Describe(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, North),
                Open = () => { throw new InvalidOperationException("boom"); },
            });
            host.LinkList.Add(Link("C Good.rvt", new[] { 0.0, 0, 0 }, 0, false, good));

            JToken links = Run(host)["links"];

            Assert.Equal(3, links.Count());
            Assert.Equal("Link is not loaded", (string)links[0]["errors"][0]);
            Assert.Empty(links[0]["instances"]);
            Assert.Equal("InvalidOperationException: boom", (string)links[1]["errors"][0]);
            Assert.Single(links[2]["instances"]);
            Assert.Equal("instance 99: ArgumentException: bad element", (string)links[2]["errors"][0]);
        }

        [Fact]
        public void R16_a_failure_reading_links_does_not_lose_walls_and_ramps()
        {
            var host = new FakeSource();
            host.LinksFailure = () => { throw new InvalidOperationException("no links"); };
            host.WallList.Add(new WallRow { TypeName = "Generic - 200mm", ElementId = 1 });
            host.RampList.Add(new RampRow { TypeName = "Ramp 1", ElementId = 2 });
            host.Errors.Add("wall 7: NullReferenceException: x");

            JObject document = Run(host);

            Assert.Empty(document["links"]);
            Assert.Single(document["walls"]);
            Assert.Single(document["ramps"]);
            Assert.Equal(new[] { "links: InvalidOperationException: no links", "wall 7: NullReferenceException: x" }, document["errors"].Select(item => (string)item).ToArray());
        }

        [Fact]
        public void R16_existing_document_keys_stay_and_new_sections_come_after_them()
        {
            var family = new DocumentEntry { File = "villa.rfa", Kind = "family", Prototypes = new List<PrototypeRow> { Villa("F", false) } };
            family.Prototypes[0].Mirrored = null;
            string familyJson = JsonOut.ToJson(family);
            foreach (string absent in new[] { "\"links\"", "\"walls\"", "\"ramps\"", "\"errors\"", "\"mirrored\"" })
            {
                Assert.DoesNotContain(absent, familyJson);
            }

            var project = new DocumentEntry { File = "site.rvt", Kind = "project", AngleToTrueNorthDeg = 1, Prototypes = new List<PrototypeRow>() };
            ModelReader.ReadSections(new FakeSource(), project);
            string projectJson = JsonOut.ToJson(project);
            Assert.Contains("\"links\": []", projectJson);
            Assert.Contains("\"walls\": []", projectJson);
            Assert.Contains("\"ramps\": []", projectJson);
            Assert.DoesNotContain("\"errors\"", projectJson);
            int[] order = new[] { "\"file\"", "\"kind\"", "\"angleToTrueNorthDeg\"", "\"prototypes\"", "\"links\"", "\"walls\"", "\"ramps\"" }
                .Select(key => projectJson.IndexOf(key, StringComparison.Ordinal)).ToArray();
            Assert.Equal(order.OrderBy(position => position).ToArray(), order);
            Assert.DoesNotContain(-1, order);
        }

        [Fact]
        public void R17_wall_row_carries_type_location_length_height_and_every_instance_parameter()
        {
            var host = new FakeSource();
            host.WallList.Add(new WallRow
            {
                TypeName = "Generic - 200mm",
                ElementId = 3001,
                Location = new LocationInfo { Kind = "line", Start = new[] { 0.0, 0, 0 }, End = new[] { 12.5, 0, 0 } },
                Length = 12.5,
                Height = 3.5,
                Parameters = ParameterBag.Collect(new Dictionary<string, object>
                {
                    { "Base Constraint", "Level 0" },
                    { "Base Offset", 0.0 },
                    { "Top Offset", 0.0 },
                    { "Unconnected Height", 3.5 },
                    { "Mark", "W-01" },
                    { "Comments", "south boundary" },
                    { "Room Bounding", 1 },
                    { "Plot Boundary Flag", 1 },
                }),
            });
            host.WallList.Add(new WallRow
            {
                TypeName = "Curved - 100mm",
                ElementId = 3002,
                Location = new LocationInfo
                {
                    Kind = "arc",
                    Start = new[] { 0.0, 0, 0 },
                    End = new[] { 10.0, 10, 0 },
                    Center = new[] { 0.0, 10, 0 },
                    Radius = 10,
                },
                Length = 15.708,
                Parameters = new Dictionary<string, object> { { "Mark", "W-02" } },
            });

            JToken walls = Run(host)["walls"];
            JToken straight = walls[0];

            Assert.Equal(2, walls.Count());
            Assert.Equal("Generic - 200mm", (string)straight["typeName"]);
            Assert.Equal(3001, (long)straight["elementId"]);
            Assert.Equal("line", (string)straight["location"]["kind"]);
            Assert.Equal(new[] { 0.0, 0, 0 }, Numbers(straight["location"]["start"]));
            Assert.Equal(new[] { 12.5, 0, 0 }, Numbers(straight["location"]["end"]));
            Assert.Equal(12.5, (double)straight["length"], 3);
            Assert.Equal(3.5, (double)straight["height"], 3);
            JToken parameters = straight["parameters"];
            Assert.Equal(8, parameters.Count());
            Assert.Equal("Level 0", (string)parameters["Base Constraint"]);
            Assert.Equal(0, (double)parameters["Base Offset"], 3);
            Assert.Equal(0, (double)parameters["Top Offset"], 3);
            Assert.Equal(3.5, (double)parameters["Unconnected Height"], 3);
            Assert.Equal("W-01", (string)parameters["Mark"]);
            Assert.Equal("south boundary", (string)parameters["Comments"]);
            Assert.Equal(1, (int)parameters["Room Bounding"]);
            Assert.Equal(1, (int)parameters["Plot Boundary Flag"]);

            JToken curved = walls[1];
            Assert.Equal("arc", (string)curved["location"]["kind"]);
            Assert.Equal(new[] { 0.0, 10, 0 }, Numbers(curved["location"]["center"]));
            Assert.Equal(10, (double)curved["location"]["radius"], 3);
            Assert.Null(curved["height"]);
        }

        [Fact]
        public void R17_ramp_row_carries_type_location_box_and_every_instance_parameter()
        {
            var host = new FakeSource();
            host.RampList.Add(new RampRow
            {
                TypeName = "Ramp 1",
                ElementId = 5001,
                BoundingBox = new BoxInfo { Min = new[] { 20.0, 30, 0 }, Max = new[] { 32.0, 33, 1 } },
                Parameters = ParameterBag.Collect(new Dictionary<string, object>
                {
                    { "Base Level", "Level 0" },
                    { "Top Level", "Level 1" },
                    { "Width", 3.0 },
                    { "Thickness", 0.2 },
                    { "Maximum Incline Length", 12.0 },
                    { "Mark", "R-01" },
                    { "Comments", "car park access" },
                }),
            });
            host.RampList.Add(new RampRow
            {
                TypeName = "Ramp 2",
                ElementId = 5002,
                Location = new LocationInfo { Kind = "line", Start = new[] { 1.0, 1, 0 }, End = new[] { 9.0, 1, 0.5 } },
                Parameters = new Dictionary<string, object> { { "Mark", "R-02" } },
            });

            JToken ramps = Run(host)["ramps"];
            JToken first = ramps[0];

            Assert.Equal(2, ramps.Count());
            Assert.Equal("Ramp 1", (string)first["typeName"]);
            Assert.Equal(5001, (long)first["elementId"]);
            Assert.Null(first["location"]);
            Assert.Equal(new[] { 20.0, 30, 0 }, Numbers(first["boundingBox"]["min"]));
            Assert.Equal(new[] { 32.0, 33, 1 }, Numbers(first["boundingBox"]["max"]));
            JToken parameters = first["parameters"];
            Assert.Equal(7, parameters.Count());
            Assert.Equal("Level 0", (string)parameters["Base Level"]);
            Assert.Equal("Level 1", (string)parameters["Top Level"]);
            Assert.Equal(3, (double)parameters["Width"], 3);
            Assert.Equal(0.2, (double)parameters["Thickness"], 3);
            Assert.Equal(12, (double)parameters["Maximum Incline Length"], 3);
            Assert.Equal("R-01", (string)parameters["Mark"]);
            Assert.Equal("car park access", (string)parameters["Comments"]);

            JToken placed = ramps[1];
            Assert.Equal("line", (string)placed["location"]["kind"]);
            Assert.Equal(new[] { 9.0, 1, 0.5 }, Numbers(placed["location"]["end"]));
        }

        [Fact]
        public void R17_parameter_bag_sorts_by_name_keeps_same_name_twice_and_lets_the_instance_win()
        {
            var collected = ParameterBag.Collect(new[]
            {
                new KeyValuePair<string, object>("Zone", "02"),
                new KeyValuePair<string, object>("Comments", "first"),
                new KeyValuePair<string, object>("Comments", "second"),
                new KeyValuePair<string, object>("Empty", null),
            });

            Assert.Equal(new[] { "Comments", "Comments (2)", "Zone" }, collected.Keys.ToArray());
            Assert.Equal("first", collected["Comments"]);
            Assert.Equal("second", collected["Comments (2)"]);

            var merged = ParameterBag.Merge(
                new Dictionary<string, object> { { "Mark", "instance" } },
                new Dictionary<string, object> { { "Mark", "type" }, { "Width", 1.5 } });

            Assert.Equal("instance", merged["Mark"]);
            Assert.Equal(1.5, merged["Width"]);
            Assert.Equal(2, merged.Count);
        }
    }
}
