using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R353. The shared parameters a read job of the test host finds in an answer key, made up in code (R10): the parameter elements with
    // their GUID, the binding of each and the categories it is bound to, and the GUIDs the parameters of the elements give by name
    public sealed class SharedParameterFileTests
    {
        private static readonly Guid First = new Guid("11111111-2222-3333-4444-555555555555");
        private static readonly Guid Second = new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        [Fact]
        public void R353_the_shared_parameters_of_a_key_are_listed_by_name_with_their_guid_binding_categories_and_the_guids_of_the_elements()
        {
            var elements = new List<SharedParameterElementItem>
            {
                new SharedParameterElementItem { Name = "RRE_ZZ_SP_Prototype", Guid = Second, ElementId = 20 },
                new SharedParameterElementItem { Name = "CASE", Guid = First, ElementId = 10 },
                new SharedParameterElementItem { Name = "Unbound", Guid = Guid.Empty, ElementId = 30 },
            };
            var bindings = new List<SharedParameterBindingItem>
            {
                new SharedParameterBindingItem { ElementId = 10, Binding = "instance", Categories = new List<string> { "RVT Links", "Floors" } },
                new SharedParameterBindingItem { ElementId = 20, Binding = "instance", Categories = new List<string> { "RVT Links" } },
                new SharedParameterBindingItem { Name = "Plot", ElementId = 99, Binding = "type", Categories = new List<string> { "Walls" } },
            };
            var seen = new List<SharedParameterSeenItem>
            {
                new SharedParameterSeenItem { Category = "RVT Links", Name = "CASE", Guid = First },
                new SharedParameterSeenItem { Category = "RVT Links", Name = "CASE", Guid = First },
                new SharedParameterSeenItem { Category = "Floors", Name = "CASE", Guid = First },
                new SharedParameterSeenItem { Category = "RVT Links", Name = "RRE_ZZ_SP_Prototype", Guid = Second },
            };
            SharedParameterFile file = SharedParameterFile.Build("key_TEST-GC.rvt", elements, bindings, seen);
            Assert.Equal(SharedParameterFile.FormatText, file.Format);
            Assert.Equal(new[] { "CASE", "RRE_ZZ_SP_Prototype", "Unbound" }, file.Parameters.Select(row => row.Name));
            SharedParameterRow caseRow = file.Parameters[0];
            Assert.Equal("11111111-2222-3333-4444-555555555555", caseRow.Guid);
            Assert.True(caseRow.Bound);
            Assert.Equal("instance", caseRow.Binding);
            Assert.Equal(new[] { "Floors", "RVT Links" }, caseRow.Categories);
            Assert.False(file.Parameters[2].Bound);
            Assert.Null(file.Parameters[2].Binding);
            Assert.Equal(new[] { "Floors|CASE|11111111-2222-3333-4444-555555555555|1", "RVT Links|CASE|11111111-2222-3333-4444-555555555555|2", "RVT Links|RRE_ZZ_SP_Prototype|aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee|1" },
                file.OnElements.Select(item => item.Category + "|" + item.Name + "|" + string.Join(",", item.Guids) + "|" + item.Count));
            JObject written = JObject.Parse(JsonOut.ToJson(file));
            Assert.Equal("key_TEST-GC.rvt", (string)written["model"]);
            Assert.Equal(new[] { "Plot" }, written["otherBindings"].Select(item => (string)item));
        }

        [Fact]
        public void R460_a_read_gives_the_spec_and_group_of_each_bound_definition_and_the_first_value_met_on_the_elements()
        {
            var elements = new List<SharedParameterElementItem> { new SharedParameterElementItem { Name = "CASE", Guid = First, ElementId = 10 } };
            var bindings = new List<SharedParameterBindingItem>
            {
                new SharedParameterBindingItem
                {
                    ElementId = 10, Binding = "instance", Categories = new List<string> { "RVT Links" }, DataType = "autodesk.spec:spec.string-2.0.0", DataTypeLabel = "Text",
                    Group = "autodesk.parameter.group:data-1.0.0", GroupLabel = "Data",
                },
            };
            var seen = new List<SharedParameterSeenItem>
            {
                new SharedParameterSeenItem { Category = "RVT Links", Name = "CASE", Guid = First, Value = null },
                new SharedParameterSeenItem { Category = "RVT Links", Name = "CASE", Guid = First, Value = "" },
                new SharedParameterSeenItem { Category = "RVT Links", Name = "CASE", Guid = First, Value = "B" },
                new SharedParameterSeenItem { Category = "RVT Links", Name = "CASE", Guid = First, Value = "A" },
                new SharedParameterSeenItem { Category = "Floors", Name = "CASE", Guid = First },
            };
            SharedParameterFile file = SharedParameterFile.Build("key_TEST-KC.rvt", elements, bindings, seen);
            SharedParameterRow row = file.Parameters.Single();
            Assert.Equal(new[] { "autodesk.spec:spec.string-2.0.0", "Text", "autodesk.parameter.group:data-1.0.0", "Data" }, new[] { row.DataType, row.DataTypeLabel, row.Group, row.GroupLabel });
            Assert.Equal(new[] { "Floors|0|", "RVT Links|2|B" }, file.OnElements.Select(item => item.Category + "|" + item.Filled + "|" + item.Sample));
            JObject written = JObject.Parse(JsonOut.ToJson(file));
            Assert.Equal("Data", (string)written["parameters"][0]["groupLabel"]);
            Assert.Equal("B", (string)written["onElements"][1]["sample"]);
        }

        // R353. The key points of the step keyPoints keep every double so it reads back as the same double, where the reader writes 3
        // decimals: the project position, the survey point, the project base point and the corners of each floor in internal and shared
        // coordinates, the floors by element id, the plot by the number of its Plot parameter
        [Fact]
        public void R353_the_key_points_of_a_read_keep_every_double_as_it_reads_back_and_the_plot_of_each_floor()
        {
            double east = 571418.99150000005, north = 2364074.2405000001, half = 0.15000000000000002;
            var floors = new List<KeyPointFloor>
            {
                new KeyPointFloor { ElementId = 9, TypeName = "yard", PlotText = "TH1-2586", Corners = new List<double[]> { new[] { -1.0, 2.0, half } }, SharedCorners = new List<double[]> { new[] { east - 1.0, north + 2.0, half } } },
                new KeyPointFloor { ElementId = 4, TypeName = "Generic 150mm", PlotText = "DT1 - 2585", Corners = new List<double[]> { new[] { -519.87450000000001, -309.52249999999998, 0.0 } },
                    SharedCorners = new List<double[]> { new[] { east - 519.87450000000001, north - 309.52249999999998, 0.0 } } },
                new KeyPointFloor { ElementId = 7, TypeName = "Generic 150mm", PlotText = null, Corners = new List<double[]>(), SharedCorners = new List<double[]>() },
            };
            string text = KeyPointsFile.Text(KeyPointsFile.Build("key_TEST-KC.rvt", new[] { east, north, 0.0 }, 0.0, new[] { 0.0, 0.0, 179.51 }, new[] { east, north, 179.51 },
                new[] { 0.0, 0.0, 179.51 }, new[] { east, north, 179.51 }, floors));
            JObject read = KeyPointsFile.PointsOf(text);
            Assert.Equal(KeyPointsFile.FormatText, (string)read["format"]);
            Assert.Equal("m", (string)read["lengthUnit"]);
            Assert.Equal(east, ExactDouble.Parse(ValueText(text, "eastWest")));
            Assert.Equal(new long[] { 4, 7, 9 }, read["floors"].Select(item => (long)item["elementId"]));
            Assert.Equal(new[] { "2585", null, "2586" }, read["floors"].Select(item => (string)item["plot"]));
            Assert.Equal(-519.87450000000001, (double)read["floors"][0]["corners"][0][0]);
            Assert.Contains("2364074.2405000003", text);
            Assert.Throws<System.IO.InvalidDataException>(() => KeyPointsFile.PointsOf("{\"format\": \"another\"}"));
        }

        // R353. A floor of no plot holds a JSON null both in the file built in memory and in the file read back, so the count of the floors
        // with a plot the step gives is the same on both: here 2 of 4, the floor with no Plot parameter and the one whose text is no plot
        // left out, where a null text taken as a value counted all 4
        [Fact]
        public void R353_a_floor_of_no_plot_is_a_null_and_is_never_counted_as_a_floor_with_a_plot()
        {
            var floors = new List<KeyPointFloor>
            {
                new KeyPointFloor { ElementId = 1, TypeName = "Generic 150mm", PlotText = "DT1 - 2585", Corners = new List<double[]>(), SharedCorners = new List<double[]>() },
                new KeyPointFloor { ElementId = 2, TypeName = "Generic 150mm", PlotText = null, Corners = new List<double[]>(), SharedCorners = new List<double[]>() },
                new KeyPointFloor { ElementId = 3, TypeName = "Generic 150mm", PlotText = "road", Corners = new List<double[]>(), SharedCorners = new List<double[]>() },
                new KeyPointFloor { ElementId = 4, TypeName = "yard", PlotText = "TH1-2586", Corners = new List<double[]>(), SharedCorners = new List<double[]>() },
            };
            JObject built = KeyPointsFile.Build("key_TEST-KC.rvt", new[] { 1.0, 2.0, 0.0 }, 0.0, new[] { 0.0, 0.0, 0.0 }, new[] { 1.0, 2.0, 0.0 }, new[] { 0.0, 0.0, 0.0 }, new[] { 1.0, 2.0, 0.0 }, floors);
            Assert.Equal(new[] { JTokenType.String, JTokenType.Null, JTokenType.Null, JTokenType.String }, built["floors"].Select(item => item["plot"].Type));
            Assert.Equal(2, KeyPointsFile.WithPlot(built));
            Assert.Equal(2, KeyPointsFile.WithPlot(KeyPointsFile.PointsOf(KeyPointsFile.Text(built))));
            Assert.Equal("floors 4, with a plot 2", KeyPointsFile.Summary(built));
        }

        private static string ValueText(string text, string name)
        {
            int at = text.IndexOf("\"" + name + "\": ", StringComparison.Ordinal) + name.Length + 4;
            int end = text.IndexOfAny(new[] { ',', '\r', '\n', '}' }, at);
            return text.Substring(at, end - at);
        }
    }
}
