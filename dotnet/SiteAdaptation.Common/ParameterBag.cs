using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteAdaptation.Common
{
    public static class ParameterBag
    {
        // Sorted by name so two runs diff cleanly. Two parameters with one name on the same element both stay, the later one as "Name (2)"
        public static Dictionary<string, object> Collect(IEnumerable<KeyValuePair<string, object>> pairs)
        {
            var bag = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in pairs.Where(item => item.Value != null).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                string name = pair.Key;
                int copy = 2;
                while (bag.ContainsKey(name))
                {
                    name = pair.Key + " (" + copy + ")";
                    copy++;
                }
                bag[name] = pair.Value;
            }
            return bag;
        }

        // The instance value wins when the type holds a parameter of the same name
        public static Dictionary<string, object> Merge(Dictionary<string, object> instance, Dictionary<string, object> type)
        {
            var merged = new Dictionary<string, object>(instance);
            foreach (KeyValuePair<string, object> pair in type)
            {
                if (!merged.ContainsKey(pair.Key))
                {
                    merged[pair.Key] = pair.Value;
                }
            }
            return merged;
        }
    }
}
