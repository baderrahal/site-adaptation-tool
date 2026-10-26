using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common
{
    // R353. A shared parameter element of a model, as the read job gives it from SharedParameterElement
    public sealed class SharedParameterElementItem
    {
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public long ElementId { get; set; }
    }

    // R353. A binding of the model, the element id of its definition, instance or type, and the categories it is bound to
    public sealed class SharedParameterBindingItem
    {
        public string Name { get; set; }
        public long ElementId { get; set; }
        public string Binding { get; set; }
        public List<string> Categories { get; set; }
    }

    // R353. A shared parameter met on an element of a category, by its name and the GUID the parameter gives, the second route
    public sealed class SharedParameterSeenItem
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public Guid Guid { get; set; }
    }

    public sealed class SharedParameterRow
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public long ElementId { get; set; }
        public bool Bound { get; set; }
        public string Binding { get; set; }
        public List<string> Categories { get; set; }
    }

    public sealed class SharedParameterOnElements
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public List<string> Guids { get; set; }
        public int Count { get; set; }
    }

    // R353. testhost_<code>_parameters.json of a read job: every shared parameter element of the model by name with its GUID, whether
    // and how it is bound and to which categories, and per category and name the GUIDs the parameters of the elements give, so the
    // GUIDs of config come from the answer keys and are held by a second route of the same window. The Revit side only collects
    public sealed class SharedParameterFile
    {
        public const string FormatText = "site-adaptation-shared-parameters";

        public string Format { get; set; } = FormatText;
        public string Model { get; set; }
        public List<SharedParameterRow> Parameters { get; set; }
        public List<SharedParameterOnElements> OnElements { get; set; }
        // The names of the bindings whose definition is no shared parameter element, the project parameters, which hold no GUID
        public List<string> OtherBindings { get; set; }

        public static SharedParameterFile Build(string model, IEnumerable<SharedParameterElementItem> elements, IEnumerable<SharedParameterBindingItem> bindings, IEnumerable<SharedParameterSeenItem> seen)
        {
            Dictionary<long, SharedParameterBindingItem> byId = bindings.GroupBy(item => item.ElementId).ToDictionary(group => group.Key, group => group.First());
            List<SharedParameterElementItem> items = elements.ToList();
            var known = new HashSet<long>(items.Select(item => item.ElementId));
            return new SharedParameterFile
            {
                Model = model,
                Parameters = items.Select(item =>
                {
                    SharedParameterBindingItem binding;
                    bool bound = byId.TryGetValue(item.ElementId, out binding);
                    return new SharedParameterRow
                    {
                        Name = item.Name, Guid = item.Guid.ToString("D"), ElementId = item.ElementId, Bound = bound, Binding = bound ? binding.Binding : null,
                        Categories = bound ? (binding.Categories ?? new List<string>()).OrderBy(name => name, StringComparer.Ordinal).ToList() : new List<string>(),
                    };
                }).OrderBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.Guid, StringComparer.Ordinal).ToList(),
                OnElements = seen.GroupBy(item => new { item.Category, item.Name }).Select(group => new SharedParameterOnElements
                {
                    Category = group.Key.Category, Name = group.Key.Name, Guids = group.Select(item => item.Guid.ToString("D")).Distinct().OrderBy(text => text, StringComparer.Ordinal).ToList(),
                    Count = group.Count(),
                }).OrderBy(item => item.Category, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal).ToList(),
                OtherBindings = byId.Where(item => !known.Contains(item.Key)).Select(item => item.Value.Name).OrderBy(name => name, StringComparer.Ordinal).ToList(),
            };
        }
    }
}
