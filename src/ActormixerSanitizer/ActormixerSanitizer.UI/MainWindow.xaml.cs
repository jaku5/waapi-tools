using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ActormixerSanitizer.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Application.Current.ThemeMode = ThemeMode.System;
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