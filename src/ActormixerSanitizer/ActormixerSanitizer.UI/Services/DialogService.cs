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

        public IProgressDialog ShowProgressDialog(string title)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new Dialogs.ProgressDialog(title, Application.Current.MainWindow);
                dialog.Show();
                return new ProgressDialogHandle(dialog);
            });
        }

        private class ProgressDialogHandle : IProgressDialog
        {
            private readonly Dialogs.ProgressDialog _dialog;

            public ProgressDialogHandle(Dialogs.ProgressDialog dialog)
            {
                _dialog = dialog;
            }

            public void Update(double value, string text, string status)
            {
                _dialog.Dispatcher.Invoke(() =>
                {
                    _dialog.ViewModel.ProgressValue = value;
                    _dialog.ViewModel.ProgressText = text;
                    _dialog.ViewModel.CurrentStatus = status;
                });
            }

            public void Dispose()
            {
                _dialog.Dispatcher.Invoke(() => _dialog.Close());
            }
        }
    }
}
