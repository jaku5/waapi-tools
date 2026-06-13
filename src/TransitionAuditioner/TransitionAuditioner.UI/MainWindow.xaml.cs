using System.ComponentModel;
using System.Windows;
using TransitionAuditioner.UI.ViewModels;

namespace TransitionAuditioner.UI
{
    public partial class MainWindow : Window
    {
        private bool _shuttingDown;

        // Remembered window height while the Activity panel is open, so toggling it off and on
        // restores the user's chosen size. 0 until the panel is first shown.
        private double _expandedHeight;

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
                vm.PropertyChanged += OnViewModelPropertyChanged;
                await vm.InitializeAsync();
            }
        }

        // The Activity panel fills a star row. When it's hidden the window auto-sizes to its
        // (compact) content; when it's shown we switch to a fixed height so the panel has room and
        // the window edge becomes a resize handle for it.
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.IsActivityVisible) || DataContext is not MainViewModel vm)
                return;

            if (vm.IsActivityVisible)
            {
                var compactHeight = ActualHeight;
                SizeToContent = SizeToContent.Manual;
                // Keep at least ~120px of panel so it can't be dragged away entirely.
                MinHeight = compactHeight + 120;
                Height = _expandedHeight > 0 ? _expandedHeight : compactHeight + 260;
            }
            else
            {
                _expandedHeight = ActualHeight;
                MinHeight = 0;
                SizeToContent = SizeToContent.Height;
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

            // Re-invoke Close() on a fresh dispatcher turn. Calling it directly here can run
            // while WPF is still unwinding this (cancelled) close, which throws
            // "Cannot ... Close ... while a Window is closing." from VerifyNotClosing().
            await Dispatcher.InvokeAsync(Close);
        }
    }
}
