using System.Threading.Tasks;
using System.Windows;

namespace ActormixerSanitizer.UI.Services
{
    public class DialogService : IDialogService
    {
        public async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            bool? result = false;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (Application.Current.MainWindow.WindowState == WindowState.Minimized)
                {
                    Application.Current.MainWindow.WindowState = WindowState.Normal;
                }
                var dialog = new Dialogs.MessageDialog(title, message, Application.Current.MainWindow, true);

                // IsDialogOpen handling should be done in the ViewModel
                result = dialog.ShowDialog();
                Application.Current.MainWindow.Activate();
            });

            return result ?? false;
        }

        public async Task ShowNotification(string title, string message)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (Application.Current.MainWindow.WindowState == WindowState.Minimized)
                {
                    Application.Current.MainWindow.WindowState = WindowState.Normal;
                }
                var dialog = new Dialogs.MessageDialog(
                    title,
                    message,
                    Application.Current.MainWindow);

                // IsDialogOpen handling should be done in the ViewModel
                dialog.ShowDialog();
                Application.Current.MainWindow.Activate();
            });
        }

        private Dialogs.ProgressDialog _progressDialog;

        public void ShowProgress(string title)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_progressDialog != null) return;

                _progressDialog = new Dialogs.ProgressDialog(title, Application.Current.MainWindow);
                // Simulate blocking by disabling main window
                Application.Current.MainWindow.IsEnabled = false;
                _progressDialog.Show();
            });
        }

        public void UpdateProgress(double value, string message, string status = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _progressDialog?.UpdateProgress(value, message, status);
            });
        }

        public void HideProgress()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_progressDialog != null)
                {
                    _progressDialog.Close();
                    _progressDialog = null;
                    Application.Current.MainWindow.IsEnabled = true;
                    Application.Current.MainWindow.Activate();
                }
            });
        }
    }
}
