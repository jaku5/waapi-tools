using PropertyContainerAuditor.UI.Messages;
using CommunityToolkit.Mvvm.Messaging;
using PropertyContainerAuditor.UI.ViewModels;
using JPAudio.WaapiTools.Tool.PropertyContainerAuditor.Core;
using System.Windows.Controls;
using System.Windows.Input;
//using PropertyContainerAuditor.Core.Models;
using PropertyContainerAuditor.UI.ViewModels;
using System.Windows;
using JPAudio.WaapiTools.Tool.PropertyContainerAuditor.Core.Models;


namespace PropertyContainerAuditor.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IRecipient<ToggleLogViewerMessage>
    {
        private GridLength _lastLogViewerHeight = new GridLength(1, GridUnitType.Star);

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.Messenger.Register<ToggleLogViewerMessage>(this);
                }
            };
        }
        public void Receive(ToggleLogViewerMessage message)
        {
            if (message.Value)
            {
                LogViewerRow.Height = _lastLogViewerHeight;
            }
            else
            {
                _lastLogViewerHeight = LogViewerRow.Height;
                LogViewerRow.Height = new GridLength(0);
            }
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is ActorMixerInfo actor)
            {
                actor.IsMarked = !actor.IsMarked;
                e.Handled = true;
            }
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (DataContext is MainViewModel vm)
                {
                    await vm.Cleanup();
                }
            }
            catch
            {
                // Best-effort cleanup during shutdown — nothing to do if it fails
            }
        }
    }
}
