using System.Threading.Tasks;

namespace ActormixerSanitizer.UI.Services
{
    public interface IDialogService
    {
        Task ShowNotification(string title, string message);
        Task<bool> ShowConfirmationDialog(string title, string message);
        IProgressDialog ShowProgressDialog(string title);
    }

    public interface IProgressDialog : System.IDisposable
    {
        void Update(double value, string text, string status);
    }
}
