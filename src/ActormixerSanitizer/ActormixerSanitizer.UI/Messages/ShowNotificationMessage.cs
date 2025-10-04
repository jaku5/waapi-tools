using CommunityToolkit.Mvvm.Messaging.Messages;

namespace ActormixerSanitizer.UI.Messages
{
    public class ShowNotificationMessage : ValueChangedMessage<string>
    {
        public ShowNotificationMessage(string value) : base(value)
        {
        }
    }
}
