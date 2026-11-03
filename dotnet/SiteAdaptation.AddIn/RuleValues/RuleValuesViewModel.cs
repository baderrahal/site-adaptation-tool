using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;

namespace SiteAdaptation.AddIn.RuleValues
{
    // R541. One row of the table. Typing changes the project value only when its row takes it
    public sealed class RuleValueRow : Bindable
    {
        private readonly Action changed;
        private string text;
        private string problem;

        public RuleValueRow(RuleValue value, Action changed)
        {
            Value = value;
            this.changed = changed;
            text = RuleValues_Text(value.Project);
        }

        public RuleValue Value { get; }
        public string Id => Value.Id;
        public string Name => Value.Name;
        public string Manual => RuleValues_Text(Value.Manual);
        public string Unit => Value.Unit;
        public string Page => Value.Page;
        public bool Changed => Value.Changed;
        public string Problem => problem;

        public string Project
        {
            get { return text; }
            set
            {
                if (Set(ref text, value))
                {
                    double? number = Common.Settings.RuleValues.Parse(Value, value, out problem);
                    if (number != null)
                    {
                        Value.Project = number.Value;
                    }
                    Changed(nameof(Changed));
                    Changed(nameof(Problem));
                    changed();
                }
            }
        }

        public void Take(double value)
        {
            Project = RuleValues_Text(value);
        }

        private static string RuleValues_Text(double value)
        {
            return Common.Settings.RuleValues.Text(value);
        }
    }

    // R541. The window on the rows of config/rule_values.json over the rules file of the project. Saved holds the file the settings name after a Save
    public sealed class RuleValuesViewModel : Bindable
    {
        private readonly string rulesText;
        private readonly string copyPath;
        private readonly string inputRoot;
        private readonly IPathPicker picker;
        private string line = string.Empty;
        private bool lineIsProblem;

        public RuleValuesViewModel(IReadOnlyList<RuleValue> values, string rulesText, string copyPath, string inputRoot, IPathPicker picker)
        {
            this.rulesText = rulesText;
            this.copyPath = copyPath;
            this.inputRoot = inputRoot;
            this.picker = picker;
            Rows = values.Select(value => new RuleValueRow(value, Refresh)).ToList();
        }

        public IReadOnlyList<RuleValueRow> Rows { get; }
        public string Saved { get; private set; }
        public string Line { get { return line; } private set { Set(ref line, value); } }
        public bool LineIsProblem { get { return lineIsProblem; } private set { Set(ref lineIsProblem, value); } }
        public bool HasLine => line.Length > 0;
        public bool CanSave => copyPath != null && Rows.All(row => row.Problem == null);

        public void Save()
        {
            try
            {
                IReadOnlyList<RuleValue> values = Rows.Select(row => row.Value).ToList();
                RuleValues_WriteCopy(values);
                Saved = copyPath;
                Say(RuleValuesText.Saved, false);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Say(TextTemplate.Fill(RuleValuesText.SaveFailed, error.Message), true);
            }
        }

        public void Reset()
        {
            foreach (RuleValueRow row in Rows)
            {
                row.Take(row.Value.Manual);
            }
            Say(string.Empty, false);
        }

        public void Export()
        {
            string path = picker.Save(RuleValuesText.Export);
            if (path == null)
            {
                return;
            }
            if (WorkFolder.Inside(Path.GetDirectoryName(path), inputRoot))
            {
                Say(TextTemplate.Fill(RuleValuesText.SaveFailed, RuleValuesText.InInputs), true);
                return;
            }
            try
            {
                File.WriteAllText(path, Common.Settings.RuleValues.ExportText(Rows.Select(row => row.Value)));
                Say(TextTemplate.Fill(RuleValuesText.Exported, Path.GetFileName(path)), false);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Say(TextTemplate.Fill(RuleValuesText.SaveFailed, error.Message), true);
            }
        }

        public void Import()
        {
            string path = picker.File(RuleValuesText.Import);
            if (path == null)
            {
                return;
            }
            try
            {
                IReadOnlyDictionary<string, double> values = Common.Settings.RuleValues.Import(File.ReadAllText(path), Rows.Select(row => row.Value).ToList(), out string refusal);
                if (values == null)
                {
                    Say(TextTemplate.Fill(RuleValuesText.ImportFailed, refusal), true);
                    return;
                }
                foreach (RuleValueRow row in Rows.Where(item => values.ContainsKey(item.Value.Key)))
                {
                    row.Take(values[row.Value.Key]);
                }
                Say(string.Empty, false);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Say(TextTemplate.Fill(RuleValuesText.ImportFailed, error.Message), true);
            }
        }

        private void RuleValues_WriteCopy(IReadOnlyList<RuleValue> values)
        {
            Common.Settings.RuleValues.WriteCopy(copyPath, Common.Settings.RuleValues.Apply(rulesText, values), values);
        }

        private void Refresh()
        {
            Changed(nameof(CanSave));
            Say(Rows.Select(row => row.Problem).FirstOrDefault(problem => problem != null) ?? string.Empty, true);
        }

        private void Say(string text, bool problem)
        {
            Line = text;
            LineIsProblem = problem;
            Changed(nameof(HasLine));
        }
    }
}
