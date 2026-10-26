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
    }
}
