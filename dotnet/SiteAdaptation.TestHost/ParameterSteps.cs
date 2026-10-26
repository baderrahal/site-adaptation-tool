using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace SiteAdaptation.TestHost
{
    // R353. After a read job has read its model, the shared parameters of it: every SharedParameterElement with its GUID, the binding of
    // its definition and the categories it is bound to, and the GUID the shared parameters of the Revit links, floors and walls give by
    // name, the second route. SharedParameterFile of Common makes the file, nothing is changed or saved
    internal sealed class ParameterSteps
    {
        private static readonly BuiltInCategory[] Categories = { BuiltInCategory.OST_RvtLinks, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Walls };

        private readonly TestHostJob job;
        private readonly string documents;

        public ParameterSteps(TestHostJob job, string documents)
        {
            this.job = job;
            this.documents = documents;
        }

        public string SharedParameters(Document document)
        {
            List<SharedParameterElementItem> elements = new FilteredElementCollector(document).OfClass(typeof(SharedParameterElement)).Cast<SharedParameterElement>()
                .Select(element => new SharedParameterElementItem { Name = element.Name, Guid = element.GuidValue, ElementId = element.Id.Value }).ToList();
            var bindings = new List<SharedParameterBindingItem>();
            DefinitionBindingMapIterator iterator = document.ParameterBindings.ForwardIterator();
            while (iterator.MoveNext())
            {
                var definition = iterator.Key as InternalDefinition;
                var binding = iterator.Current as ElementBinding;
                if (definition == null || binding == null)
                {
                    continue;
                }
                bindings.Add(new SharedParameterBindingItem
                {
                    Name = definition.Name, ElementId = definition.Id.Value, Binding = binding is InstanceBinding ? "instance" : binding is TypeBinding ? "type" : null,
                    Categories = binding.Categories.Cast<Category>().Select(category => category.Name).ToList(),
                });
            }
            var seen = new List<SharedParameterSeenItem>();
            foreach (BuiltInCategory category in Categories)
            {
                foreach (Element element in new FilteredElementCollector(document).OfCategory(category).WhereElementIsNotElementType())
                {
                    foreach (Parameter parameter in element.Parameters)
                    {
                        if (parameter.IsShared)
                        {
                            seen.Add(new SharedParameterSeenItem { Category = element.Category == null ? category.ToString() : element.Category.Name, Name = parameter.Definition.Name, Guid = parameter.GUID });
                        }
                    }
                }
            }
            SharedParameterFile file = SharedParameterFile.Build(System.IO.Path.GetFileName(job.Model), elements, bindings, seen);
            JsonOut.Write(TestHostGuard.OutputPath(documents, job.Code, "parameters"), file);
            return "shared parameters " + file.Parameters.Count + ", bound " + file.Parameters.Count(row => row.Bound) + ", other bindings " + file.OtherBindings.Count + ", seen on elements " + seen.Count;
        }
    }
}
