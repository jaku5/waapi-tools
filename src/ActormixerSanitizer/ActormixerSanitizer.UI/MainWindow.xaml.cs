using ActormixerSanitizer.UI.Messages;
using CommunityToolkit.Mvvm.Messaging;
using ActormixerSanitizer.UI.ViewModels;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core;
using System.Windows.Controls;
using System.Windows.Input;
//using ActormixerSanitizer.Core.Models;
using ActormixerSanitizer.UI.ViewModels;
using Wpf.Ui.Controls;
using System.Windows;

namespace ActormixerSanitizer.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow, IRecipient<ToggleLogViewerMessage>
    {
        private GridLength _lastLogViewerHeight = new GridLength(1, GridUnitType.Star);

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                Wpf.Ui.Appearance.SystemThemeWatcher.Watch(
                    this,                                    // Window class
                    Wpf.Ui.Controls.WindowBackdropType.Mica, // Background type
                    true                                     // Whether to change accents automatically
                );

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
                actor.IsSelected = !actor.IsSelected;
                e.Handled = true;
            }
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.Cleanup();
            }
        }
    }
}