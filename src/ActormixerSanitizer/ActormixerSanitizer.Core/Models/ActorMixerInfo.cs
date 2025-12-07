using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core.Models
{
    public class ActorMixerInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string ParentId { get; set; }
        public string Notes { get; set; }
        public string AncestorId { get; set; }
        public string AncestorName { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
