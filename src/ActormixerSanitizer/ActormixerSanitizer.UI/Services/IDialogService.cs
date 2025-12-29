using System.Threading.Tasks;

namespace ActormixerSanitizer.UI.Services
{
    public interface IDialogService
    {
        Task ShowNotification(string title, string message);
        Task<bool> ShowConfirmationDialog(string title, string message);
    }
}
