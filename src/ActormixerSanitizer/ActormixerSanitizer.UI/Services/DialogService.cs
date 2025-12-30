using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;

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

        public async Task RunTaskWithProgress(string title, System.Func<IProgressDialog, CancellationToken, Task> work)
        {
            var cts = new CancellationTokenSource();
            var dialog = new Dialogs.ProgressDialog(title, Application.Current.MainWindow);
            var handle = new ProgressDialogHandle(dialog, cts);

            var workTask = work(handle, cts.Token);

            _ = workTask.ContinueWith(t =>
            {
                dialog.Dispatcher.Invoke(() => dialog.Close());
            });

            dialog.ShowDialog();
            await workTask;
        }

        private class ProgressDialogHandle : IProgressDialog
        {
            private readonly Dialogs.ProgressDialog _dialog;
            private readonly CancellationTokenSource _cts;

            public ProgressDialogHandle(Dialogs.ProgressDialog dialog, CancellationTokenSource cts)
            {
                _dialog = dialog;
                _cts = cts;
                _dialog.CancelCommand = CancelCommand;
            }

            public System.Windows.Input.ICommand CancelCommand => new RelayCommand(() =>
            {
                _cts.Cancel();
                _dialog.Close();
            });

            public void Update(double value, string text, string status)
            {
                _dialog.Dispatcher.Invoke(() =>
                {
                    _dialog.ProgressValue = value;
                    _dialog.ProgressText = text;
                    _dialog.CurrentStatus = status;
                });
            }

            public void Dispose()
            {
                _cts.Dispose();
            }
        }
    }
}
