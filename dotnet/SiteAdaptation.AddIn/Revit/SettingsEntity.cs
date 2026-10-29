using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.Revit
{
    // R367. The Settings entity on ProjectInformation, saved with the .rvt. SettingsStore of Common decides when it is written
    internal sealed class SettingsEntity : ISettingsEntity
    {
        private readonly Document document;

        public SettingsEntity(Document document)
        {
            this.document = document;
        }

        public string Read()
        {
            Schema schema = Schema.Lookup(SettingsSchema.Guid);
            if (schema == null)
            {
                return null;
            }
            Entity entity = document.ProjectInformation.GetEntity(schema);
            return entity.IsValid() ? entity.Get<string>(SettingsSchema.Field) : null;
        }

        public void Write(string json)
        {
            // Outside a transaction Revit would throw its own message, this one names the rule
            if (!document.IsModifiable)
            {
                throw new InvalidOperationException("the settings are written only inside the transaction of a real run (R367)");
            }
            var entity = new Entity(Schema.Lookup(SettingsSchema.Guid) ?? Build());
            entity.Set(SettingsSchema.Field, json);
            document.ProjectInformation.SetEntity(entity);
        }

        private static Schema Build()
        {
            var builder = new SchemaBuilder(SettingsSchema.Guid);
            builder.SetSchemaName(SettingsSchema.Name);
            builder.SetVendorId(SettingsSchema.VendorId);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Vendor);
            builder.AddSimpleField(SettingsSchema.Field, typeof(string));
            return builder.Finish();
        }
    }
}
