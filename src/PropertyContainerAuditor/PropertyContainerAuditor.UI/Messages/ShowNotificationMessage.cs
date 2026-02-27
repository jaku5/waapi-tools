using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PropertyContainerAuditor.UI.Messages
{
    public class ShowNotificationMessage : ValueChangedMessage<string>
    {
        public ShowNotificationMessage(string value) : base(value)
        {
        }
    }
}
