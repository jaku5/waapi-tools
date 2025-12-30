using System.Threading.Tasks;

namespace ActormixerSanitizer.UI.Services
{
    public interface IDialogService
    {
        Task ShowNotification(string title, string message);
        Task<bool> ShowConfirmationDialog(string title, string message);
        Task RunTaskWithProgress(string title, System.Func<IProgressDialog, System.Threading.CancellationToken, System.Threading.Tasks.Task> work);
    }

    public interface IProgressDialog : System.IDisposable
    {
        void Update(double value, string text, string status);
        System.Windows.Input.ICommand CancelCommand { get; }
    }
}
