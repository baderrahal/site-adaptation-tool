using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.ConfirmProfile
{
    // R636. A column the window offers: where the check found it, shown in plain words
    public sealed class ColumnChoice
    {
        public string Workbook { get; set; }
        public string Sheet { get; set; }
        public string Column { get; set; }
        public int Plots { get; set; }
        public bool Proposed { get; set; }
        public bool Confirmed { get; set; }

        public string Label
        {
            get { return Column + " in " + Sheet + " of " + Workbook + (Plots > 0 ? ", " + Plots + " plots" : "") + (Confirmed ? " (confirmed)" : Proposed ? " (proposed)" : ""); }
        }
    }

    // R636. One prototype and its hand: the scanner's guess as the check wrote it, and the hand the person picks
    public sealed class HandRow
    {
        public string Code { get; set; }
        public string Guess { get; set; }

        // "", left or right. Empty means the person gave none and no hand is written for the code
        public string Chosen { get; set; } = "";

        public string GuessText
        {
            get { return Guess == null ? "no guess" : "guess: " + Guess; }
        }
    }

    // R636. One group level and the word the person gives it
    public sealed class LevelRow
    {
        public int Level { get; set; }
        public string Word { get; set; } = "";
        public string Found { get; set; }
    }

    // R636 to R638. What the window of 2 Read data lets a person confirm, read from project_profile.json of the read folder, and the
    // file it writes, confirmed_profile.json of the work folder, which the next read takes (R632). No window type, so a test drives it.
    // CHOSEN by Lead: the smallest version, no sketch covers this window
    public sealed class ConfirmProfileModel
    {
        public const string NoColumn = "none";
        public const string Arrow = "->";

        public IReadOnlyList<ColumnChoice> TypologyColumns { get; private set; } = new ColumnChoice[0];
        public IReadOnlyList<ColumnChoice> AreaColumns { get; private set; } = new ColumnChoice[0];

        // Null for none, the person's pick of the offered columns
        public ColumnChoice Typology { get; set; }
        public ColumnChoice Area { get; set; }

        // One "old -> new" pair per line, as the proposal gave them or as the person types them
        public string CodeMapText { get; set; } = "";
        public string CodeMapFoundIn { get; private set; }

        // True when the text is what a person confirmed on an earlier read
        public bool CodeMapConfirmed { get; private set; }
        public IReadOnlyList<HandRow> Hands { get; private set; } = new HandRow[0];
        public IReadOnlyList<LevelRow> Levels { get; private set; } = new LevelRow[0];

        // The tie or the reason a column was not proposed, in the words of the check, null for none
        public string TypologyNote { get; private set; }
        public string AreaNote { get; private set; }

        public static ConfirmProfileModel Read(string profileJson)
        {
            JObject root = JObject.Parse(profileJson);
            var model = new ConfirmProfileModel();
            model.TypologyColumns = Columns(root["typologyColumn"], out ColumnChoice typology);
            model.Typology = typology;
            model.AreaColumns = Columns(root["areaColumn"], out ColumnChoice area);
            model.Area = area;
            model.TypologyNote = (string)root["typologyColumn"]?["tie"];
            model.AreaNote = (string)root["areaColumn"]?["tie"];
            JToken map = root["codeMap"];
            if (map?["proposed"] is JArray pairs)
            {
                model.CodeMapText = string.Join(Environment.NewLine, pairs.Select(pair => (string)pair["old"] + " " + Arrow + " " + (string)pair["new"]));
                model.CodeMapFoundIn = (string)map["foundIn"];
            }
            if (map?["confirmed"] is JArray given && given.Count > 0)
            {
                model.CodeMapText = string.Join(Environment.NewLine, given.Select(pair => (string)pair["old"] + " " + Arrow + " " + (string)pair["new"]));
                model.CodeMapFoundIn = null;
                model.CodeMapConfirmed = true;
            }
            var hands = new List<HandRow>();
            if (root["hands"]?["proposed"] is JObject proposed)
            {
                foreach (JProperty guess in proposed.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    string value = guess.Value.Type == JTokenType.String ? (string)guess.Value : null;
                    hands.Add(new HandRow { Code = guess.Name, Guess = value, Chosen = value ?? "" });
                }
            }
            if (root["hands"]?["confirmed"] is JObject confirmedHands)
            {
                foreach (JProperty hand in confirmedHands.Properties())
                {
                    HandRow row = hands.FirstOrDefault(item => item.Code == hand.Name);
                    if (row == null)
                    {
                        row = new HandRow { Code = hand.Name };
                        hands.Add(row);
                    }
                    row.Chosen = (string)hand.Value;
                }
            }
            model.Hands = hands;
            model.Levels = (root["levels"] as JArray ?? new JArray()).OfType<JObject>().Select(row => new LevelRow
            {
                Level = (int)row["level"],
                Found = row["word"]?.Type == JTokenType.String ? (string)row["word"] : null,
                Word = row["word"]?.Type == JTokenType.String ? (string)row["word"] : "",
            }).ToList();
            return model;
        }

        private static IReadOnlyList<ColumnChoice> Columns(JToken block, out ColumnChoice proposed)
        {
            proposed = null;
            var list = new List<ColumnChoice>();
            if (block == null)
            {
                return list;
            }
            JToken first = block["proposed"];
            foreach (JToken row in (block["candidates"] as JArray ?? new JArray()).Concat(first != null && first.Type == JTokenType.Object ? new[] { first } : new JToken[0]))
            {
                var choice = new ColumnChoice
                {
                    Workbook = (string)row["workbook"], Sheet = (string)row["sheet"], Column = (string)row["column"], Plots = (int?)row["plots"] ?? 0,
                };
                if (list.Any(item => item.Workbook == choice.Workbook && item.Sheet == choice.Sheet && item.Column == choice.Column))
                {
                    continue;
                }
                list.Add(choice);
            }
            if (first != null && first.Type == JTokenType.Object)
            {
                ColumnChoice found = list.First(item => item.Workbook == (string)first["workbook"] && item.Sheet == (string)first["sheet"] && item.Column == (string)first["column"]);
                found.Proposed = true;
                proposed = found;
            }
            // What a person confirmed on an earlier read is what the window starts on, over the proposal, so a Confirm never swaps it back
            JToken given = block["confirmed"];
            if (given != null && given.Type == JTokenType.Object && given["column"] != null)
            {
                ColumnChoice match = list.FirstOrDefault(item => item.Workbook == (string)given["workbook"] && item.Sheet == (string)given["sheet"] && item.Column == (string)given["column"]);
                if (match == null)
                {
                    match = new ColumnChoice { Workbook = (string)given["workbook"], Sheet = (string)given["sheet"], Column = (string)given["column"] };
                    list.Add(match);
                }
                match.Confirmed = true;
                proposed = match;
            }
            return list;
        }

        // The pairs of the typed text. A line with other than two words is refused with its number, so nothing typed is dropped
        public IReadOnlyList<KeyValuePair<string, string>> CodeMap()
        {
            var pairs = new List<KeyValuePair<string, string>>();
            int number = 0;
            foreach (string line in (CodeMapText ?? "").Replace("\r\n", "\n").Split('\r', '\n'))
            {
                number++;
                if (line.Trim().Length == 0)
                {
                    continue;
                }
                // A code may hold spaces, as a code with a note in brackets does, so the arrow parts the pair. A line with no arrow and two words
                // is the short form
                string[] words = line.Contains(Arrow)
                    ? line.Split(new[] { Arrow }, StringSplitOptions.None).Select(word => word.Trim()).ToArray()
                    : line.Split(new[] { ' ', '	', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length != 2 || words.Any(word => word.Length == 0))
                {
                    throw new InvalidDataException("Line " + number + " of the code map needs the old code, " + Arrow + " and the new code, like A1 " + Arrow + " B1.");
                }
                pairs.Add(new KeyValuePair<string, string>(words[0], words[1]));
            }
            return pairs;
        }

        // The file R632 reads: only what the person gave, so a block left empty is not written
        public JObject ToFile()
        {
            var file = new JObject();
            IReadOnlyList<KeyValuePair<string, string>> pairs = CodeMap();
            if (pairs.Count > 0)
            {
                file["codeMap"] = new JArray(pairs.Select(pair => new JObject { ["old"] = pair.Key, ["new"] = pair.Value }));
            }
            JObject hands = new JObject();
            foreach (HandRow hand in Hands.Where(item => item.Chosen == ConfirmedProfile.Left || item.Chosen == ConfirmedProfile.Right))
            {
                hands[hand.Code] = hand.Chosen;
            }
            if (hands.Count > 0)
            {
                file["hands"] = hands;
            }
            JArray levels = new JArray(Levels.Where(item => !string.IsNullOrWhiteSpace(item.Word)).Select(item => new JObject { ["level"] = item.Level, ["word"] = item.Word.Trim() }));
            if (levels.Count > 0)
            {
                file["levels"] = levels;
            }
            if (Typology != null)
            {
                file["typologyColumn"] = Column(Typology);
            }
            if (Area != null)
            {
                file["areaColumn"] = Column(Area);
            }
            return file;
        }

        private static JObject Column(ColumnChoice choice)
        {
            return new JObject { ["workbook"] = choice.Workbook, ["sheet"] = choice.Sheet, ["column"] = choice.Column };
        }

        // True when the person gave nothing, so there is no file to write and no read to start again
        public bool GivesNothing
        {
            get { return ToFile().Count == 0; }
        }

        // Writes confirmed_profile.json in the work folder after checking it as the read will. Returns the path, throws the reason when the
        // file would be refused, so a pick the window says is saved is saved
        public string Save(string workFolder, string runId, int levelCount)
        {
            JObject file = ToFile();
            ConfirmedProfile.From(file, Math.Max(levelCount, Levels.Count));
            Directory.CreateDirectory(workFolder);
            string path = Path.Combine(workFolder, ConfirmedProfile.FileName);
            WorkFiles.WriteText(path, runId, file.ToString(Newtonsoft.Json.Formatting.Indented));
            return path;
        }
    }
}
