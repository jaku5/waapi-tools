using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JPAudio.WaapiTools.Tool.TransitionAuditioner.Core;
using JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Models;

namespace TransitionAuditioner.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ITransitionAuditionerService _service;

        [ObservableProperty]
        private string _header = "Transition Auditioner";

        [ObservableProperty]
        private string _subHeader = "Connecting to Wwise...";

        [ObservableProperty]
        private string _targetName = "—";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ShowInExplorerCommand))]
        [NotifyCanExecuteChangedFor(nameof(FinishCommand))]
        private bool _isReady;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ShowInExplorerCommand))]
        private bool _isBusy;

        public ObservableCollection<string> Log { get; } = new();

        public MainViewModel(ITransitionAuditionerService service)
        {
            _service = service;
            _service.StatusUpdated += (_, msg) => Append(msg);
            _service.NotificationRequested += (_, msg) => Append("⚠ " + msg);
            _service.Disconnected += (_, _) => Append("Disconnected from Wwise.");
        }

        public async Task InitializeAsync()
        {
            IsBusy = true;
            try
            {
                await _service.ConnectAsync();
                SubHeader = _service.ProjectName is { Length: > 0 } p
                    ? $"{p}  ·  Wwise {_service.WwiseVersion}"
                    : "Connected.";

                var target = await _service.GetSelectedTargetAsync();
                if (target == null)
                {
                    SubHeader = "Select a music container in Wwise, then reopen the tool.";
                    return;
                }

                TargetName = $"{target.Name}  ({target.Type})";
                await _service.SetUpAuditionAsync(target);
                IsReady = true;
            }
            catch (Exception ex)
            {
                Append("✖ " + ex.Message);
                SubHeader = "Setup failed — see log.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanInteract => IsReady && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanInteract))]
        private async Task ShowInExplorerAsync() => await _service.ShowInProjectExplorerAsync();

        [RelayCommand(CanExecute = nameof(IsReady))]
        private void Finish() => Application.Current?.MainWindow?.Close();

        /// <summary>Called when the window closes: guarantees the temp Work Unit is removed.</summary>
        public async Task ShutdownAsync()
        {
            try
            {
                await _service.TeardownAsync();
            }
            catch
            {
                // Teardown is best-effort and already swallows its own errors.
            }
            finally
            {
                _service.Disconnect();
            }
        }

        private void Append(string message)
        {
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => Log.Add(message));
            }
            else
            {
                Log.Add(message);
            }
        }
    }
}
