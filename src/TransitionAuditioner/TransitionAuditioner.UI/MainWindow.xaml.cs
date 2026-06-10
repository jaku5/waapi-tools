using System.ComponentModel;
using System.Windows;
using TransitionAuditioner.UI.ViewModels;

namespace TransitionAuditioner.UI
{
    public partial class MainWindow : Window
    {
        private bool _shuttingDown;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.InitializeAsync();
            }
        }

        private async void OnClosing(object? sender, CancelEventArgs e)
        {
            // Run teardown before the window actually closes so the temp Work Unit is
            // always removed, even if the user just clicks the window's X.
            if (_shuttingDown || DataContext is not MainViewModel vm)
                return;

            e.Cancel = true;
            _shuttingDown = true;
            await vm.ShutdownAsync();
            Close();
        }
    }
}
