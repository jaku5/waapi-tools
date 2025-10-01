using CommunityToolkit.Mvvm.Messaging.Messages;

namespace ActormixerSanitizer.UI.Messages
{
    public class ToggleLogViewerMessage : ValueChangedMessage<bool>
    {
        public ToggleLogViewerMessage(bool value) : base(value)
        {
        }
    }
}
