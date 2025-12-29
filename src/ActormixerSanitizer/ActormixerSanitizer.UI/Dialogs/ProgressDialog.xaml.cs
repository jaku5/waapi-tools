using System.Windows;

namespace ActormixerSanitizer.UI.Dialogs
{
    public partial class ProgressDialog : Window
    {
        public ProgressDialog(string title, Window owner)
        {
            InitializeComponent();
            TitleTextBlock.Text = title;
            Owner = owner;
        }

        public void UpdateProgress(double value, string message, string status = "")
        {
            ProgressBar.Value = value;
            MessageTextBlock.Text = message;
            StatusTextBlock.Text = status;
        }
    }
}
