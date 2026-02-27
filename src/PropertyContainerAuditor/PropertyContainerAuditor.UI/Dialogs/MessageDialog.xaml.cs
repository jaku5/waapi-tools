using System.Windows;

namespace PropertyContainerAuditor.UI.Dialogs
{
    public partial class MessageDialog : Window
    {
        public MessageDialog(string title, string message, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            Title = title;
            MessageTextBlock.Text = message;
            CancelButton.Visibility = Visibility.Collapsed;
        }

        public MessageDialog(string title, string message, Window owner, bool showCancel)
        {
            InitializeComponent();
            Owner = owner;
            Title = title;
            MessageTextBlock.Text = message;
            if (!showCancel)
            {
                CancelButton.Visibility = Visibility.Collapsed;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
