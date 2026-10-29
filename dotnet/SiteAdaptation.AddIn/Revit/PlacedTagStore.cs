using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using SiteAdaptation.Common.Tags;

namespace SiteAdaptation.AddIn.Revit
{
    // R368. The entities of SiteAdaptation_Placed on the elements of a document. SecondRun of Common decides what a run does with them
    internal sealed class PlacedTagStore
    {
        private readonly Document document;

        public PlacedTagStore(Document document)
        {
            this.document = document;
        }

        // Every element tagged by any listed version, read by the field names of its version. A version no element of this document
        // was ever tagged with has no schema in memory, so Lookup gives null and it is passed over
        public IReadOnlyList<KeyValuePair<Element, PlacedTag>> Collect()
        {
            var found = new List<KeyValuePair<Element, PlacedTag>>();
            foreach (SchemaVersion version in PlacedSchema.Versions)
            {
                Schema schema = Schema.Lookup(version.Guid);
                if (schema == null)
                {
                    continue;
                }
                foreach (Element element in new FilteredElementCollector(document).WherePasses(new ExtensibleStorageFilter(version.Guid)))
                {
                    Entity entity = element.GetEntity(schema);
                    var fields = version.Fields.ToDictionary(name => name, name => entity.Get<string>(name), StringComparer.Ordinal);
                    found.Add(new KeyValuePair<Element, PlacedTag>(element, PlacedTag.FromFields(version, fields)));
                }
            }
            return found;
        }

        // Writes the entity of the latest version and deletes those of older ones, in the one transaction of the run
        public void Write(Element element, PlacedTag tag)
        {
            if (!document.IsModifiable)
            {
                throw new InvalidOperationException("a tag is written only inside the transaction of a real run (R368)");
            }
            SchemaVersion latest = PlacedSchema.Latest;
            var entity = new Entity(Schema.Lookup(latest.Guid) ?? Build(latest));
            foreach (KeyValuePair<string, string> field in tag.ToFields())
            {
                entity.Set(field.Key, field.Value);
            }
            element.SetEntity(entity);
            foreach (SchemaVersion older in PlacedSchema.Versions.Where(version => version.Version != latest.Version))
            {
                Schema schema = Schema.Lookup(older.Guid);
                if (schema != null && element.GetEntity(schema).IsValid())
                {
                    element.DeleteEntity(schema);
                }
            }
        }

        private static Schema Build(SchemaVersion version)
        {
            var builder = new SchemaBuilder(version.Guid);
            builder.SetSchemaName(version.Name);
            builder.SetVendorId(PlacedSchema.VendorId);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Vendor);
            foreach (string field in version.Fields)
            {
                builder.AddSimpleField(field, typeof(string));
            }
            return builder.Finish();
        }
    }
}
