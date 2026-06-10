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
        private MusicObjectInfo? _target;

        [ObservableProperty]
        private string _header = "Transition Auditioner";

        [ObservableProperty]
        private string _subHeader = "Connecting to Wwise...";

        [ObservableProperty]
        private string _targetName = "—";

        /// <summary>Cue offset from the end of each segment, in seconds (default 1 s).</summary>
        [ObservableProperty]
        private double _offsetSeconds = 1.0;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
        private bool _hasTarget;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ShowInExplorerCommand))]
        private bool _isReady;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
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

        /// <summary>Connects and identifies the selected target, but does not build anything yet.</summary>
        public async Task InitializeAsync()
        {
            IsBusy = true;
            try
            {
                await _service.ConnectAsync();
                SubHeader = _service.ProjectName is { Length: > 0 } p
                    ? $"{p}  ·  Wwise {_service.WwiseVersion}"
                    : "Connected.";

                _target = await _service.GetSelectedTargetAsync();
                if (_target == null)
                {
                    SubHeader = "Select a music container in Wwise, then reopen the tool.";
                    return;
                }

                TargetName = $"{_target.Name}  ({_target.Type})";
                HasTarget = true;
                Append("Ready. Set the cue offset, then click Set Up & Audition.");
            }
            catch (Exception ex)
            {
                Append("✖ " + ex.Message);
                SubHeader = "Connection failed — see log.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSetUp => HasTarget && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanSetUp))]
        private async Task SetUpAsync()
        {
            if (_target == null)
                return;

            if (OffsetSeconds <= 0)
            {
                Append("⚠ Cue offset must be greater than 0 seconds.");
                return;
            }

            IsBusy = true;
            try
            {
                // Re-running with a new offset: tear down the previous harness first.
                if (_service.IsSetUp)
                {
                    await _service.TeardownAsync();
                    IsReady = false;
                }

                _service.AuditionCueOffsetFromEndMs = (int)Math.Round(OffsetSeconds * 1000.0);
                await _service.SetUpAuditionAsync(_target);
                IsReady = true;
            }
            catch (Exception ex)
            {
                Append("✖ " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanInteract => IsReady && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanInteract))]
        private async Task ShowInExplorerAsync() => await _service.ShowInProjectExplorerAsync();

        [RelayCommand]
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
