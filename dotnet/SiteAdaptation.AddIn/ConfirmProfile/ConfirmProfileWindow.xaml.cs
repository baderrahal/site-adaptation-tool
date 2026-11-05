using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SiteAdaptation.AddIn.ConfirmProfile
{
    // R636. The window Confirm what was found. It holds no logic: the clicks go to the session, which a test drives with no click
    public partial class ConfirmProfileWindow : Window
    {
        public static readonly string[] HandChoices = { "", "left", "right" };

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
            CodeMapSource.Text = model.CodeMapFoundIn == null ? ConfirmProfileText.CodeMapNone : string.Format(System.Globalization.CultureInfo.InvariantCulture, ConfirmProfileText.CodeMapFound, model.CodeMapFoundIn);
            HandsNone.Visibility = model.Hands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LevelsNone.Visibility = model.Levels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Entry 0 is none, then the columns in the order the check gave them
        private static void Fill(ComboBox box, System.Collections.Generic.IReadOnlyList<ColumnChoice> columns, ColumnChoice selected)
        {
            box.Items.Add(ConfirmProfileText.NoneChoice);
            foreach (ColumnChoice column in columns)
            {
                box.Items.Add(column.Label);
            }
            box.SelectedIndex = selected == null ? 0 : columns.ToList().IndexOf(selected) + 1;
        }

        private static ColumnChoice Picked(ComboBox box, System.Collections.Generic.IReadOnlyList<ColumnChoice> columns)
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
