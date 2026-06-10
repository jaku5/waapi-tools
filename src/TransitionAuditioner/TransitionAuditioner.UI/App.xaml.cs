using System.Windows;
using JPAudio.WaapiTools.ClientJson;
using JPAudio.WaapiTools.Tool.TransitionAuditioner.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TransitionAuditioner.UI.ViewModels;

namespace TransitionAuditioner.UI
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
            mainWindow.Show();

            SetTheme(IsDarkModeEnabled());
            SystemEvents.UserPreferenceChanged += (_, _) => SetTheme(IsDarkModeEnabled());
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddDebug());
            services.AddSingleton<IJsonClient, JsonClient>();
            services.AddSingleton<ITransitionAuditionerService, TransitionAuditionerService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            _serviceProvider?.Dispose();
        }

        public static bool IsDarkModeEnabled()
        {
            const string registryKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string registryValue = "AppsUseLightTheme";

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(registryKey);
            if (key?.GetValue(registryValue) is int value)
            {
                return value == 0; // 0 means Dark, 1 means Light
            }

            return false;
        }

        public static void SetTheme(bool isDark)
        {
            try
            {
#pragma warning disable WPF0001
                if (Application.Current != null)
                {
                    Application.Current.ThemeMode = isDark ? ThemeMode.Dark : ThemeMode.Light;
                }
#pragma warning restore WPF0001
            }
            catch
            {
                // Silently ignore (e.g. headless/test scenarios).
            }
        }
    }
}
