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
    }
}
