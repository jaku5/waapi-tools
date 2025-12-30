using ActormixerSanitizer.UI.ViewModels;
using System.Windows;

namespace ActormixerSanitizer.UI.Dialogs
{
    public partial class ProgressDialog : Window
    {
        public ProgressDialog(string title, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            DataContext = new ProgressDialogViewModel { Title = title };
        }

        public ProgressDialogViewModel ViewModel => (ProgressDialogViewModel)DataContext;
    }
}
