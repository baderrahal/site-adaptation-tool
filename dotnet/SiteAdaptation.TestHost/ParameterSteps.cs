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
                ForgeTypeId spec = definition.GetDataType();
                ForgeTypeId group = definition.GetGroupTypeId();
                bindings.Add(new SharedParameterBindingItem
                {
                    Name = definition.Name, ElementId = definition.Id.Value, Binding = binding is InstanceBinding ? "instance" : binding is TypeBinding ? "type" : null,
                    Categories = binding.Categories.Cast<Category>().Select(category => category.Name).ToList(),
                    // R460. The spec and the group of the definition, as the parameter sheet of button 1 names them
                    DataType = Id(spec), DataTypeLabel = Label(spec, LabelUtils.GetLabelForSpec), Group = Id(group), GroupLabel = Label(group, LabelUtils.GetLabelForGroup),
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
                            seen.Add(new SharedParameterSeenItem
                            {
                                Category = element.Category == null ? category.ToString() : element.Category.Name, Name = parameter.Definition.Name, Guid = parameter.GUID,
                                Value = parameter.HasValue ? parameter.AsValueString() ?? parameter.AsString() : null,
                            });
                        }
                    }
                }
            }
            SharedParameterFile file = SharedParameterFile.Build(System.IO.Path.GetFileName(job.Model), elements, bindings, seen);
            JsonOut.Write(TestHostGuard.OutputPath(documents, job.Code, "parameters"), file);
            return "shared parameters " + file.Parameters.Count + ", with a group " + file.Parameters.Count(row => row.Group != null) + ", bound " + file.Parameters.Count(row => row.Bound) + ", other bindings " + file.OtherBindings.Count + ", seen on elements " + seen.Count;
        }

        private static string Id(ForgeTypeId id)
        {
            return id == null || string.IsNullOrEmpty(id.TypeId) ? null : id.TypeId;
        }

        // A spec or group of no label, such as the empty group, gives null and never stops the read
        private static string Label(ForgeTypeId id, System.Func<ForgeTypeId, string> label)
        {
            if (Id(id) == null)
            {
                return null;
            }
            try
            {
                return label(id);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                return null;
            }
        }
    }
}
