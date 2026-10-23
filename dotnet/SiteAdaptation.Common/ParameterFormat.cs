using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common
{
    // One parameter with everything the relation study needs. Text is the value as Revit shows it.
    // Number is the value as a number, in the unit named by Unit, and is there for numeric parameters only
    public class ParameterDetail
    {
        public string Name { get; set; }
        public string BuiltIn { get; set; }
        public string Storage { get; set; }
        public string Text { get; set; }
        public double? Number { get; set; }
        public string Unit { get; set; }
    }

    public static class ParameterFormat
    {
        // Revit names the built in id of a parameter that is not built in INVALID
        public const string NotBuiltIn = "INVALID";

        public static ParameterDetail Make(string name, string builtIn, string storage, string text, double? internalNumber, ValueUnit unit)
        {
            var detail = new ParameterDetail
            {
                Name = name,
                BuiltIn = string.IsNullOrEmpty(builtIn) || builtIn == NotBuiltIn ? null : builtIn,
                Storage = storage,
                Text = text,
            };
            if (internalNumber.HasValue)
            {
                detail.Number = UnitMath.FromInternal(internalNumber.Value, unit);
                detail.Unit = UnitMath.UnitName(unit);
            }
            return detail;
        }

        // Sorted by name so two runs diff cleanly. A name held twice keeps both, the later one as "Name (2)", as ParameterBag does
        public static List<ParameterDetail> Collect(IEnumerable<ParameterDetail> details)
        {
            var result = new List<ParameterDetail>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (ParameterDetail detail in details.Where(item => item != null && item.Name != null).OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                string name = detail.Name;
                int copy = 2;
                while (!names.Add(name))
                {
                    name = detail.Name + " (" + copy + ")";
                    copy++;
                }
                detail.Name = name;
                result.Add(detail);
            }
            return result;
        }
    }
}
