using System;
using System.Collections.Generic;
using System.Globalization;
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
            get
            {
                string where = ConfirmProfileText.ColumnLabel.Replace("<column>", Column).Replace("<sheet>", Sheet).Replace("<workbook>", Workbook);
                string plots = Plots > 0 ? ConfirmProfileText.ColumnPlots.Replace("<n>", Plots.ToString(CultureInfo.InvariantCulture)) : "";
                return where + plots + (Confirmed ? ConfirmProfileText.ColumnConfirmed : Proposed ? ConfirmProfileText.ColumnProposed : "");
            }
        }
    }

    // R636, R633. One prototype and its hand. CHOSEN by Lead: a guess is shown beside the box and does not start selected, as a guess changes
    // no mirror until it is confirmed and one press on the whole form is not that. Only a hand confirmed on an earlier read starts selected
    public sealed class HandRow
    {
        public string Code { get; set; }
        public string Guess { get; set; }

        // "", left or right. Empty means no hand is written for the code
        public string Chosen { get; set; } = "";

        public string GuessText
        {
            get { return Guess == null ? ConfirmProfileText.NoGuess : ConfirmProfileText.GuessLine.Replace("<hand>", Guess); }
        }
    }

    // R636. One group level. CHOSEN by Lead: a word is written only when the person typed or changed it, or confirmed it on an earlier read. A
    // computed word left alone stays computed, and so does not go over the header of the area column the person just picked (DataCheckFind.cs)
    public sealed class LevelRow
    {
        public int Level { get; set; }
        public string Word { get; set; } = "";

        // The word as the window opened on it
        public string First { get; set; } = "";

        // True when the check holds this word as what a person typed on an earlier read
        public bool Given { get; set; }

        // Where the word came from, beside the box, so a word found by the tool and one confirmed before do not look alike
        public string SourceText { get; set; } = "";

        public string Name { get { return ConfirmProfileText.LevelRow.Replace("<n>", Level.ToString(CultureInfo.InvariantCulture)); } }
        public string BoxName { get { return ConfirmProfileText.LevelBox.Replace("<n>", Level.ToString(CultureInfo.InvariantCulture)); } }

        public bool Written
        {
            get { return !string.IsNullOrWhiteSpace(Word) && (Given || Word.Trim() != First); }
        }
    }

    // R636. What the window of 2 Read data lets a person confirm, read from project_profile.json of the read folder, and the file it writes,
    // confirmed_profile.json of the work folder, which the next read takes (R632). No window type, so a test drives it.
    // CHOSEN by Lead: the smallest version, no sketch covers this window
    public sealed class ConfirmProfileModel
    {
        public const string Arrow = "->";

        // The word the check gives direction when only reading left to right set it, DataCheckCodes.Guess, a private text of Common
        public const string DirectionGuessWord = "a guess";

        public IReadOnlyList<ColumnChoice> TypologyColumns { get; private set; } = new ColumnChoice[0];
        public IReadOnlyList<ColumnChoice> AreaColumns { get; private set; } = new ColumnChoice[0];

        // Null for none, else the column the window shows, which starts on the column confirmed before or proposed
        public ColumnChoice Typology { get; set; }
        public ColumnChoice Area { get; set; }

        // One "old -> new" pair per line, as the check gave them or as the person types them
        public string CodeMapText { get; set; } = "";

        // What the window says of the map: where it came from, and any doubt on it before anyone confirms it
        public string CodeMapLine { get; private set; } = ConfirmProfileText.CodeMapNone;
        public string CodeMapDoubt { get; private set; } = "";

        public IReadOnlyList<HandRow> Hands { get; private set; } = new HandRow[0];
        public IReadOnlyList<LevelRow> Levels { get; private set; } = new LevelRow[0];

        // The tie or the reason a column was not proposed, in the words of the check, null for none
        public string TypologyNote { get; private set; }
        public string AreaNote { get; private set; }

        public bool AnyGuess
        {
            get { return Hands.Any(hand => hand.Guess != null); }
        }

        // The first line of the window, which says proposes only when something is there
        public string Intro
        {
            get
            {
                bool shown = Typology != null || Area != null || !string.IsNullOrWhiteSpace(CodeMapText) || AnyGuess || Hands.Any(hand => hand.Chosen.Length > 0) || Levels.Any(level => level.First.Length > 0);
                return shown ? ConfirmProfileText.IntroFound : ConfirmProfileText.IntroNothing;
            }
        }

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
            model.ReadCodeMap(root["codeMap"]);
            model.Hands = HandsOf(root["hands"]);
            model.Levels = LevelsOf(root["levels"] as JArray, root["areaColumn"]?["confirmed"]);
            return model;
        }

        private void ReadCodeMap(JToken map)
        {
            if (map?["confirmed"] is JArray given && given.Count > 0)
            {
                CodeMapText = PairLines(given);
                CodeMapLine = ConfirmProfileText.CodeMapConfirmed;
                return;
            }
            if (map?["proposed"] is JArray pairs && pairs.Count > 0)
            {
                CodeMapText = PairLines(pairs);
                CodeMapLine = ConfirmProfileText.CodeMapFound.Replace("<source>", (string)map["foundIn"] ?? "");
                var doubts = new List<string>();
                if (((string)map["direction"] ?? "").StartsWith(DirectionGuessWord, StringComparison.Ordinal))
                {
                    doubts.Add(ConfirmProfileText.CodeMapGuess);
                }
                if (map["unsure"]?.Type == JTokenType.Boolean && (bool)map["unsure"])
                {
                    doubts.Add(ConfirmProfileText.CodeMapUnsure);
                }
                CodeMapDoubt = string.Join(" ", doubts);
            }
        }

        private static string PairLines(JArray pairs)
        {
            return string.Join(Environment.NewLine, pairs.Select(pair => (string)pair["old"] + " " + Arrow + " " + (string)pair["new"]));
        }

        private static IReadOnlyList<HandRow> HandsOf(JToken block)
        {
            var hands = new List<HandRow>();
            if (block?["proposed"] is JObject proposed)
            {
                foreach (JProperty guess in proposed.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    hands.Add(new HandRow { Code = guess.Name, Guess = guess.Value.Type == JTokenType.String ? (string)guess.Value : null });
                }
            }
            if (block?["confirmed"] is JObject confirmed)
            {
                foreach (JProperty hand in confirmed.Properties())
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
            return hands;
        }

        private static IReadOnlyList<LevelRow> LevelsOf(JArray levels, JToken areaConfirmed)
        {
            string header = areaConfirmed != null && areaConfirmed.Type == JTokenType.Object ? (string)areaConfirmed["column"] : null;
            return (levels ?? new JArray()).OfType<JObject>().Select(row =>
            {
                string word = row["word"]?.Type == JTokenType.String ? (string)row["word"] : "";
                bool entered = (string)row["source"] == ConfirmedProfile.PersonEntered;
                // The check tags the header of a confirmed area column as person entered too. It is no typed word, so a change of the column takes its new header
                bool fromArea = entered && (int)row["level"] == 1 && word == header;
                return new LevelRow
                {
                    Level = (int)row["level"], Word = word, First = word, Given = entered && !fromArea,
                    SourceText = fromArea ? ConfirmProfileText.LevelFromArea : entered ? ConfirmProfileText.LevelConfirmed : word.Length > 0 ? ConfirmProfileText.LevelFound : "",
                };
            }).ToList();
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

        // The pairs of the text. A line that is no pair, or a code that holds what is left of a mistyped arrow, is refused with its line number,
        // so nothing typed is dropped and no junk code reaches the read
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
                string lineNumber = number.ToString(CultureInfo.InvariantCulture);
                if (words.Length != 2 || words.Any(word => word.Length == 0))
                {
                    throw new InvalidDataException(ConfirmProfileText.CodeLine.Replace("<n>", lineNumber));
                }
                if (words.Any(word => Arrow.IndexOf(word[0]) >= 0 || Arrow.IndexOf(word[word.Length - 1]) >= 0))
                {
                    throw new InvalidDataException(ConfirmProfileText.CodeEdge.Replace("<n>", lineNumber));
                }
                pairs.Add(new KeyValuePair<string, string>(words[0], words[1]));
            }
            return pairs;
        }

        // The file R632 reads. CHOSEN by Lead: a press on Confirm is a person confirming what the window shows, so both columns and the code map go
        // in as they stand at the press, also when the tool proposed them and they were not touched, and the read tags them person entered. A
        // hand goes in only when one is selected and a level word only as LevelRow.Written says, so a guess or a computed word left alone stays
        // what it was. A block that is empty is not written
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
            JArray levels = new JArray(Levels.Where(item => item.Written).Select(item => new JObject { ["level"] = item.Level, ["word"] = item.Word.Trim() }));
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

        // True when every block is empty, so there is nothing to write
        public bool GivesNothing
        {
            get { return ToFile().Count == 0; }
        }

        // Writes confirmed_profile.json in the work folder after checking it as the read will, so a pick the window says is saved is saved.
        // Throws the reason when the file would be refused
        public void Save(string workFolder, string runId)
        {
            JObject file = ToFile();
            ConfirmedProfile.From(file, Levels.Count);
            Directory.CreateDirectory(workFolder);
            WorkFiles.WriteText(Path.Combine(workFolder, ConfirmedProfile.FileName), runId, file.ToString(Newtonsoft.Json.Formatting.Indented));
        }
    }
}
