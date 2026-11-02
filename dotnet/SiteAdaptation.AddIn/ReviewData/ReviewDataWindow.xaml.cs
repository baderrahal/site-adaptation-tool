using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SiteAdaptation.Common.Review;

namespace SiteAdaptation.AddIn.ReviewData
{
    // R406. The window of button 3. It holds no logic: each button hands its click to the session, which a test drives with no click
    public partial class ReviewDataWindow : Window
    {
        private readonly ReviewDataSession session;

        public ReviewDataWindow(ReviewDataSession session)
        {
            this.session = session;
            InitializeComponent();
            DataContext = session.Model;
            Closing += OnClosing;
        }

        // R446. The X and Close take one path. A window never shown has no one to ask, as in the tests and the shots
        private void OnClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = IsVisible && !session.CanClose(() => ReviewCloseWindow.Ask(this));
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnZoom(object sender, RoutedEventArgs e)
        {
            session.Zoom();
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            session.Export();
        }

        private void OnConfirmAll(object sender, RoutedEventArgs e)
        {
            session.ConfirmAll();
        }

        private void OnConfirmRows(object sender, RoutedEventArgs e)
        {
            session.ConfirmRows();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            session.Save();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            session.Mark(ReviewCheckValue.Ok);
        }

        private void OnWrong(object sender, RoutedEventArgs e)
        {
            session.Mark(ReviewCheckValue.Wrong);
        }

        private void OnChip(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is ChipViewModel chip)
            {
                session.Model.SetChip(chip.Chip);
            }
        }
    }
}
