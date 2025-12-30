using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ActormixerSanitizer.UI.ViewModels
{
    public partial class ProgressDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private string _progressText = "";

        [ObservableProperty]
        private string _currentStatus = "";

        [ObservableProperty]
        private string _title = "Processing";

        public ProgressDialogViewModel()
        {
        }
    }
}
