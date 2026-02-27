using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JPAudio.WaapiTools.Tool.PropertyContainerAuditor.Core.Models
{
    public class ActorMixerInfo : INotifyPropertyChanged
    {
        private bool _isMarked;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string ParentId { get; set; }
        public string Notes { get; set; }
        public string AncestorId { get; set; }
        public string AncestorName { get; set; }

        public bool IsMarked
        {
            get => _isMarked;
            set
            {
                if (_isMarked != value)
                {
                    _isMarked = value;
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
