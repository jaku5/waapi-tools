using System.Windows;

namespace ActormixerSanitizer.UI.Dialogs
{
    public partial class MessageDialog : Window
    { 
        public bool? Result { get; private set; }

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
            Result = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }
    }
}
