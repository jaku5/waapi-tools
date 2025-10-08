using ActormixerSanitizer.UI.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;


//using Wpf.Ui.Contracts;
//using Wpf.Ui.Services;

namespace ActormixerSanitizer.UI
{
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            _serviceProvider = serviceCollection.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();

            mainWindow.Show();

            SetTheme(IsDarkModeEnabled());
            SystemEvents.UserPreferenceChanged += (s, e) => SetTheme(IsDarkModeEnabled());
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure =>
            {
                configure.AddDebug();
            });

            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            services.AddSingleton<ActormixerSanitizerService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            _serviceProvider.Dispose();
        }

        public static bool IsDarkModeEnabled()
        {
            const string registryKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string registryValue = "AppsUseLightTheme";

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryKey))
            {
                if (key != null)
                {
                    var value = key.GetValue(registryValue);
                    if (value != null && value is int)
                    {
                        return (int)value == 0;  // 0 means Dark, 1 means Light
                    }
                }
            }

            return false; // Default to Light if the key or value doesn't exist
        }

        public static void SetTheme(bool isDark)
        {
#pragma warning disable WPF0001
            Application.Current.ThemeMode = isDark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001
        }
    }
}
