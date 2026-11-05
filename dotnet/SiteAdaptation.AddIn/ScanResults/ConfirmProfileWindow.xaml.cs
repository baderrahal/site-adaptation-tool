using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.ConfirmProfile
{
    // R636. The window Confirm what was found. It holds no logic: the clicks go to the session, which a test drives with no click
    public partial class ConfirmProfileWindow : Window
    {
        public static readonly string[] HandChoices = { "", ConfirmedProfile.Left, ConfirmedProfile.Right };

        private readonly ConfirmProfileSession session;

        public ConfirmProfileWindow(ConfirmProfileSession session)
        {
            this.session = session;
            InitializeComponent();
            ConfirmProfileModel model = session.Model;
            DataContext = model;
            Fill(TypologyBox, model.TypologyColumns, model.Typology);
            Fill(AreaBox, model.AreaColumns, model.Area);
            TypologyBox.SelectionChanged += (sender, e) => model.Typology = Picked(TypologyBox, model.TypologyColumns);
            AreaBox.SelectionChanged += (sender, e) => model.Area = Picked(AreaBox, model.AreaColumns);
            TypologyNote.Text = model.TypologyNote ?? "";
            AreaNote.Text = model.AreaNote ?? "";
            CodeMapDoubt.Visibility = Shown(model.CodeMapDoubt.Length > 0);
            HandsGuessHelp.Visibility = Shown(model.AnyGuess);
            HandsNone.Visibility = Shown(model.Hands.Count == 0);
            LevelsNone.Visibility = Shown(model.Levels.Count == 0);
        }

        private static Visibility Shown(bool shown)
        {
            return shown ? Visibility.Visible : Visibility.Collapsed;
        }

        // Entry 0 is none, then the columns in the order the check gave them
        private static void Fill(ComboBox box, IReadOnlyList<ColumnChoice> columns, ColumnChoice selected)
        {
            box.Items.Add(ConfirmProfileText.NoneChoice);
            foreach (ColumnChoice column in columns)
            {
                box.Items.Add(column.Label);
            }
            box.SelectedIndex = selected == null ? 0 : columns.ToList().IndexOf(selected) + 1;
        }

        private static ColumnChoice Picked(ComboBox box, IReadOnlyList<ColumnChoice> columns)
        {
            return box.SelectedIndex <= 0 ? null : columns[box.SelectedIndex - 1];
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            session.Confirm();
            RefusalText.Text = session.Refusal ?? "";
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            session.Cancel();
        }
    }
}
