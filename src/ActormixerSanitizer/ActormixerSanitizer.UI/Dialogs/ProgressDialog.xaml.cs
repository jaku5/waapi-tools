using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ActormixerSanitizer.UI.Dialogs
{
    public partial class ProgressDialog : Window, INotifyPropertyChanged
    {
        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        private string _progressText = "";
        public string ProgressText
        {
            get => _progressText;
            set { _progressText = value; OnPropertyChanged(); }
        }

        private string _currentStatus = "";
        public string CurrentStatus
        {
            get => _currentStatus;
            set { _currentStatus = value; OnPropertyChanged(); }
        }

        private string _titleText = "Processing";
        public string TitleText
        {
            get => _titleText;
            set { _titleText = value; OnPropertyChanged(); }
        }

        public ProgressDialog(string title, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            TitleText = title;
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
