using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SiteAdaptation.C3DProbe.Civil
{
    // The Civil 3D API differs between releases and many getters throw on objects that do not use them, so the dump
    // reads plain values by reflection and a getter that fails only leaves its field out
    internal static class Reflect
    {
        public static Dictionary<string, object> Plain(object target)
        {
            var values = new Dictionary<string, object>();
            if (target == null)
            {
                return values;
            }
            foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0 || !property.CanRead)
                {
                    continue;
                }
                Type type = property.PropertyType;
                if (!(type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(long)
                      || type == typeof(double) || type == typeof(uint) || type.IsEnum))
                {
                    continue;
                }
                try
                {
                    object value = property.GetValue(target);
                    values[property.Name] = Clean(value);
                }
                catch (Exception)
                {
                }
            }
            return values;
        }

        public static object Clean(object value)
        {
            if (value is double number)
            {
                return double.IsNaN(number) || double.IsInfinity(number) ? null : (object)Round.To(number, 4);
            }
            if (value != null && value.GetType().IsEnum)
            {
                return value.ToString();
            }
            return value;
        }

        public static object Get(object target, string name)
        {
            if (target == null)
            {
                return null;
            }
            try
            {
                var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(target);
                }
                var method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                return method?.Invoke(target, null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static List<object> Items(object collection)
        {
            var items = new List<object>();
            if (collection is IEnumerable enumerable && !(collection is string))
            {
                try
                {
                    foreach (object item in enumerable)
                    {
                        items.Add(item);
                    }
                }
                catch (Exception)
                {
                }
            }
            return items;
        }

        public static List<string> Strings(object value)
        {
            return Items(value).Where(item => item != null).Select(item => item.ToString()).ToList();
        }

        public static int? Count(object collection)
        {
            object count = Get(collection, "Count");
            if (count is int number)
            {
                return number;
            }
            return collection is IEnumerable && !(collection is string) ? Items(collection).Count : (int?)null;
        }

        // Every property whose name ends with the suffix and whose value has a count, for example the definition
        // parts of a surface
        public static Dictionary<string, int?> Counts(object target, string suffix)
        {
            var counts = new Dictionary<string, int?>();
            foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.Name.EndsWith(suffix, StringComparison.Ordinal) || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }
                try
                {
                    counts[property.Name] = Count(property.GetValue(target));
                }
                catch (Exception)
                {
                    counts[property.Name] = null;
                }
            }
            return counts;
        }

        // Calls each public method with no parameters that starts with Get, ends with Ids and returns a collection
        public static Dictionary<string, int?> IdCounts(object target)
        {
            var counts = new Dictionary<string, int?>();
            foreach (var method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!method.Name.StartsWith("Get", StringComparison.Ordinal) || !method.Name.EndsWith("Ids", StringComparison.Ordinal)
                    || method.GetParameters().Length > 0)
                {
                    continue;
                }
                try
                {
                    counts[method.Name] = Count(method.Invoke(target, null));
                }
                catch (Exception)
                {
                    counts[method.Name] = null;
                }
            }
            return counts;
        }
    }
}
