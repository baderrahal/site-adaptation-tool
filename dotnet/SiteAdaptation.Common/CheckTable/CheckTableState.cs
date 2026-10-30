using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common.CheckTable
{
    // R376. What the check table window decides apart from the window: the chip and the find box, the Use of each row, what changed
    // since the table opened or was saved, and what a Use would give. No WPF type, so the tests run on net48 and net8.0
    public sealed class CheckTableState
    {
        public const string AllChip = "All";
        public const string AssumedChip = "Assumed";

        private readonly CheckTableView view;
        private readonly Dictionary<int, string> now = new Dictionary<int, string>();
        private Dictionary<int, string> saved = new Dictionary<int, string>();

        public CheckTableState(CheckTableView view)
        {
            this.view = view;
            foreach (CheckRow row in view.Rows)
            {
                now[row.Index] = row.OpenUse;
                saved[row.Index] = row.OpenUse;
            }
        }

        public IReadOnlyList<CheckRow> Rows => view.Rows;

        // The chip Assumed groups rules and lists no row, an assumed status is no issue (item 8 of 7.7)
        public IReadOnlyList<CheckRow> Filter(string chip, string find)
        {
            string text = (find ?? "").Trim();
            return view.Rows.Where(row => chip == AllChip || (chip != AssumedChip && row.Check == chip))
                .Where(row => text.Length == 0 || row.Plot.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        public int CountOf(string chip)
        {
            return view.Counts.Where(item => item.Key == chip).Select(item => item.Value).FirstOrDefault();
        }

        public string UseOf(int index)
        {
            return now[index];
        }

        // Past on a row that offers none is refused, the dropdown shows it grey so only a caller that skips the window gets here
        public void Set(int index, string use)
        {
            if (!CheckUse.IsValid(use))
            {
                throw new ArgumentException("no Use " + use, nameof(use));
            }
            CheckRow row = view.Rows.Single(item => item.Index == index);
            if (use == CheckUse.Past && !row.PastOffered)
            {
                throw new ArgumentException("Past is not offered for the row " + index + ": " + row.PastWhy, nameof(use));
            }
            now[index] = use;
        }

        // The reason Past is grey on a row, null where it is offered
        public string PastTip(CheckRow row)
        {
            return row.PastOffered ? null : row.PastWhy;
        }

        // True while a Use differs from the one at the last save, or from the opening one before a first save
        public bool Changed => view.Rows.Any(row => now[row.Index] != saved[row.Index]);

        // Each row whose Use differs from the opening value. A row set back to it is no change (03b 4 Check)
        public IReadOnlyList<CheckChoice> Choices()
        {
            return view.Rows.Where(row => now[row.Index] != row.OpenUse).Select(row => new CheckChoice(row.Index, now[row.Index])).ToList();
        }

        public void MarkSaved()
        {
            saved = new Dictionary<int, string>(now);
        }

        // The text a Use would give the row, for the side box. A callback gives its own, else Rule gives the tool value, Past the value
        // it writes in words and Skip nothing
        public static string Gives(CheckRow row, string use, Func<CheckRow, string, string> given)
        {
            string own = given?.Invoke(row, use);
            if (own != null)
            {
                return own;
            }
            return use == CheckUse.Past ? CheckShown.Past(row.PastValue) : Text(use == CheckUse.Rule ? row.ToolValue : null);
        }

        // A value as one line: a text as it is, an object or array compact
        public static string Text(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return "";
            }
            return token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
        }
    }

    // R376, docs/design/07-mock.md 7.4. The line under the title: the level profile, the override file and its entries
    public sealed class CheckTableInfo
    {
        public string LevelProfile { get; set; }

        public string OverrideFile { get; set; }

        public int Overrides { get; set; }

        // The entries on a plot the table of the NBH keeps, one with no nbhId standing in the NBH of the scope, all of them for the whole project
        public int OnNbh { get; set; }

        public static CheckTableInfo Of(string levelProfile, string overrideFile, JObject overrideList, JObject step, string nbh)
        {
            JObject plots = step?["plots"] as JObject ?? new JObject();
            List<string> entries = (overrideList?["overrides"] as JArray ?? new JArray()).OfType<JObject>().Select(item => item["plot"]?.ToString()).ToList();
            return new CheckTableInfo
            {
                LevelProfile = levelProfile, OverrideFile = overrideFile, Overrides = entries.Count,
                OnNbh = nbh == null ? entries.Count : entries.Count(plot => plot != null && plots[plot] is JObject found && ((string)found["nbh"] == nbh || CheckTableBuild.NoNbh(found["nbh"]))),
            };
        }
    }
}
