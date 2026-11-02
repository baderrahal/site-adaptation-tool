using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.Flow
{
    // R459. One element a warning names, as the Revit side read it at commit: its id, the plot value it holds and its type name
    public sealed class WarningElement
    {
        public string Id { get; set; }
        public string Plot { get; set; }
        public string TypeName { get; set; }
    }

    // R459. One entry of a list: the words of the warning, the role pairs it may hold, and whether both elements must hold one plot value
    public sealed class KnownWarning
    {
        public string Text { get; set; }
        public bool SamePlot { get; set; }
        public IReadOnlyList<string[]> RolePairs { get; set; } = new string[0][];
    }

    // R459. A warning a step took without showing it, the two elements in the order of the role pair that allowed it
    public sealed class AcceptedWarning
    {
        public string Text { get; set; }
        public string Plot { get; set; }
        public string FirstType { get; set; }
        public string SecondType { get; set; }
        public string FirstId { get; set; }
        public string SecondId { get; set; }
    }

    // R459. The optional side of a transaction that can take known warnings, so a fake of IModelTransaction needs no change and a step
    // on one without it simply accepts nothing
    public interface IKnownWarningTransaction
    {
        void AcceptKnownWarnings(KnownWarningList list);

        IReadOnlyList<AcceptedWarning> AcceptedWarnings { get; }
    }

    // R459. What a step accepts in its transaction, with no Revit type. A step gives its own entries, the parameter that holds the plot
    // value and the type names of each role, so another step reuses it with its own list
    public sealed class KnownWarningList
    {
        private readonly List<KnownWarning> entries;
        private readonly Dictionary<string, string> roleOfType;

        public KnownWarningList(IEnumerable<KnownWarning> entries, string plotParameter, IReadOnlyDictionary<string, IReadOnlyList<string>> typesOfRole)
        {
            this.entries = entries.ToList();
            PlotParameter = plotParameter;
            roleOfType = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyList<string>> role in typesOfRole)
            {
                foreach (string type in role.Value)
                {
                    roleOfType[type] = role.Key;
                }
            }
        }

        public string PlotParameter { get; }

        public IReadOnlyList<KnownWarning> Entries => entries;

        // The block floors.knownWarnings of config, null when config holds none, so every warning shows
        public static KnownWarningList OfFloors(JObject config)
        {
            JObject floors = config?["floors"] as JObject;
            return Parse(floors?["knownWarnings"], (string)floors?["plotParameter"], floors?["types"] as JObject, "floors.knownWarnings");
        }

        // A block of config by its own name: entries, each with text, samePlot and rolePairs, the types of the roles a pair may name. Null
        // for no block. A block of the wrong form stops the run by name before the model changes, it never shows or hides a warning unasked
        public static KnownWarningList Parse(JToken block, string plotParameter, JObject types, string name)
        {
            if (block == null)
            {
                return null;
            }
            InvalidDataException Bad(string why) => new InvalidDataException("config " + name + ": " + why + " (R459)");
            if (string.IsNullOrEmpty(plotParameter))
            {
                throw Bad("no plot parameter");
            }
            if (types == null)
            {
                throw Bad("no types of its roles");
            }
            if (!(block is JObject) || !(block["entries"] is JArray list))
            {
                throw Bad("entries is no list");
            }
            var entries = new List<KnownWarning>();
            foreach (JToken entry in list)
            {
                if (!(entry is JObject) || entry["text"]?.Type != JTokenType.String || ((string)entry["text"]).Length == 0)
                {
                    throw Bad("an entry has no text");
                }
                if (entry["samePlot"]?.Type != JTokenType.Boolean)
                {
                    throw Bad("the entry " + (string)entry["text"] + " has no samePlot true or false");
                }
                if (!(entry["rolePairs"] is JArray pairs) || pairs.Any(pair => !(pair is JArray two) || two.Count != 2 || two.Any(role => role.Type != JTokenType.String)))
                {
                    throw Bad("the rolePairs of " + (string)entry["text"] + " are not pairs of two roles");
                }
                foreach (string role in pairs.SelectMany(pair => pair).Select(item => (string)item).Where(role => !(types[role] is JArray)))
                {
                    throw Bad("the role " + role + " names no types");
                }
                entries.Add(new KnownWarning { Text = (string)entry["text"], SamePlot = (bool)entry["samePlot"], RolePairs = pairs.Select(pair => pair.Select(role => (string)role).ToArray()).ToList() });
            }
            return new KnownWarningList(entries, plotParameter, types.Properties().Where(role => role.Value is JArray)
                .ToDictionary(role => role.Name, role => (IReadOnlyList<string>)role.Value.Select(type => (string)type).ToList()));
        }

        // True when an entry names these words, so the Revit side reads the elements of that warning only
        public bool Knows(string text)
        {
            return entries.Any(entry => string.Equals(entry.Text, text, StringComparison.Ordinal));
        }

        // The warning as accepted, or null when it shows. Only the exact words, exactly two elements, each with a plot value and the type of
        // a role, in a pair the entry names, and one plot value when the entry asks it. Everything else shows, as Revit would show it
        public AcceptedWarning Decide(string text, IReadOnlyList<WarningElement> elements)
        {
            if (elements == null || elements.Count != 2)
            {
                return null;
            }
            string one = PlotOf(elements[0]), two = PlotOf(elements[1]), roleOne = RoleOf(elements[0]), roleTwo = RoleOf(elements[1]);
            if (one == null || two == null || roleOne == null || roleTwo == null)
            {
                return null;
            }
            foreach (KnownWarning entry in entries.Where(item => string.Equals(item.Text, text, StringComparison.Ordinal) && (!item.SamePlot || one == two)))
            {
                foreach (string[] pair in entry.RolePairs)
                {
                    if (pair[0] == roleOne && pair[1] == roleTwo)
                    {
                        return Accepted(text, elements[0], elements[1], one, two);
                    }
                    if (pair[0] == roleTwo && pair[1] == roleOne)
                    {
                        return Accepted(text, elements[1], elements[0], two, one);
                    }
                }
            }
            return null;
        }

        private static string PlotOf(WarningElement element)
        {
            string plot = element?.Plot?.Trim();
            return string.IsNullOrEmpty(plot) ? null : plot;
        }

        private string RoleOf(WarningElement element)
        {
            return element?.TypeName != null && roleOfType.TryGetValue(element.TypeName, out string role) ? role : null;
        }

        private static AcceptedWarning Accepted(string text, WarningElement first, WarningElement second, string plotFirst, string plotSecond)
        {
            return new AcceptedWarning
            {
                Text = text, Plot = plotFirst == plotSecond ? plotFirst : plotFirst + ", " + plotSecond,
                FirstType = first.TypeName, SecondType = second.TypeName, FirstId = first.Id, SecondId = second.Id,
            };
        }
    }

    // R459. The file of a step with what it accepted so far on one NBH
    public static class AcceptedWarningFile
    {
        public const string Format = "site-adaptation-accepted-warnings";

        private const string List = "accepted";
        private static readonly string[] Fields = { "text", "plot", "firstType", "secondType", "firstId", "secondId" };

        public static string Text(string runId, string nbh, IEnumerable<AcceptedWarning> accepted)
        {
            List<AcceptedWarning> items = accepted.ToList();
            return new JObject
            {
                ["format"] = Format, ["runId"] = runId, ["neighbourhood"] = nbh, ["count"] = items.Count,
                [List] = new JArray(items.Select(item => new JObject
                {
                    ["text"] = item.Text, ["plot"] = item.Plot, ["firstType"] = item.FirstType, ["secondType"] = item.SecondType, ["firstId"] = item.FirstId, ["secondId"] = item.SecondId,
                })),
            }.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        // Null when the file is not there or does not read. 4 Check and the next run of the step lose only this count by it, never their run
        public static List<AcceptedWarning> Load(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                JObject file = JObject.Parse(WorkFiles.ReadText(path));
                return Refusal(file) == null ? Read(file) : null;
            }
            catch (Exception failure) when (failure is Newtonsoft.Json.JsonException || failure is IOException || failure is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static List<AcceptedWarning> Read(JObject file)
        {
            return ((JArray)file[List]).OfType<JObject>().Select(item => new AcceptedWarning
            {
                Text = (string)item["text"], Plot = (string)item["plot"], FirstType = (string)item["firstType"], SecondType = (string)item["secondType"],
                FirstId = (string)item["firstId"], SecondId = (string)item["secondId"],
            }).ToList();
        }

        // What the earlier runs accepted on floors that still stand and what this run accepted, each pair once, the newer one kept as a run
        // may give a floor another type. A warning a run deleted is not raised again by a run that leaves its floors alone, so a run that
        // accepted nothing must not empty the list
        public static List<AcceptedWarning> Merge(IEnumerable<AcceptedWarning> earlier, IEnumerable<AcceptedWarning> now, ICollection<string> standing)
        {
            var pairs = new Dictionary<string, AcceptedWarning>(StringComparer.Ordinal);
            foreach (AcceptedWarning item in earlier.Where(item => standing.Contains(item.FirstId) && standing.Contains(item.SecondId)).Concat(now))
            {
                pairs[Key(item)] = item;
            }
            return pairs.Values.OrderBy(item => item.Plot, StringComparer.Ordinal).ThenBy(item => item.FirstType, StringComparer.Ordinal).ThenBy(item => item.SecondType, StringComparer.Ordinal)
                .ThenBy(item => item.FirstId, StringComparer.Ordinal).ThenBy(item => item.SecondId, StringComparer.Ordinal).ToList();
        }

        private static string Key(AcceptedWarning item)
        {
            bool inOrder = string.CompareOrdinal(item.FirstId, item.SecondId) <= 0;
            return item.Text + "|" + (inOrder ? item.FirstId : item.SecondId) + "|" + (inOrder ? item.SecondId : item.FirstId);
        }

        // Null when the file is of this form, else why not
        public static string Refusal(JObject file)
        {
            const string not = "the accepted warnings file is not of the form of R459";
            if ((string)file?["format"] != Format || file["neighbourhood"]?.Type != JTokenType.String || !(file[List] is JArray items) || file["count"]?.Type != JTokenType.Integer || (int)file["count"] != items.Count)
            {
                return not;
            }
            return items.All(item => item is JObject && Fields.All(field => item[field]?.Type == JTokenType.String)) ? null : not;
        }
    }
}
