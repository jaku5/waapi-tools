using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using JPAudio.WaapiTools.Tool.TransitionAuditioner.Core;
using JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Models;

namespace TransitionAuditioner.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ITransitionAuditionerService _service;
        private MusicObjectInfo? _target;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ThemeIcon))]
        private bool _isDarkTheme;

        /// <summary>Segoe Fluent glyph for the theme toggle: sun when dark (switch to light), moon when light.</summary>
        public string ThemeIcon => IsDarkTheme ? "" : "";

        /// <summary>Whether the Activity log panel is shown. Hidden by default to keep the window compact.</summary>
        [ObservableProperty]
        private bool _isActivityVisible;

        [ObservableProperty]
        private string _header = "Transition Auditioner";

        [ObservableProperty]
        private string _subHeader = "Connecting to Wwise...";

        [ObservableProperty]
        private string _targetName = "—";

        /// <summary>Live description of the current Wwise selection (independent of the chosen target).</summary>
        [ObservableProperty]
        private string _currentSelection = "—";

        /// <summary>Cue offset from the end of each segment, in seconds (default 1 s).</summary>
        [ObservableProperty]
        private double _offsetSeconds = 1.0;

        /// <summary>
        /// Selected length basis, by index: 0 Exit cue, 1 Segment end, 2 Audio length —
        /// matching the <see cref="SegmentLengthSource"/> enum order.
        /// </summary>
        [ObservableProperty]
        private int _lengthSourceIndex;

        /// <summary>Whether to open the Music Playlist Editor on setup (playlist targets only).</summary>
        [ObservableProperty]
        private bool _openPlaylistEditor = true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
        private bool _hasTarget;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ShowInExplorerCommand))]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _isReady;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
        [NotifyCanExecuteChangedFor(nameof(ShowInExplorerCommand))]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        [NotifyCanExecuteChangedFor(nameof(PullSelectionCommand))]
        private bool _isBusy;

        /// <summary>Activity log as a single string, newest entry on top, each line timestamped.</summary>
        [ObservableProperty]
        private string _logText = string.Empty;

        public MainViewModel(ITransitionAuditionerService service)
        {
            _service = service;
            _service.StatusUpdated += (_, msg) => Append(msg);
            _service.NotificationRequested += (_, msg) => Append("⚠ " + msg);
            _service.Disconnected += (_, _) => Append("Disconnected from Wwise.");
            _service.SelectionChanged += (_, text) => OnUiThread(() => CurrentSelection = text);

            IsDarkTheme = App.IsDarkModeEnabled();
            App.SetTheme(IsDarkTheme);

            // Follow the OS light/dark setting while running, keeping the toggle icon in sync.
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        [RelayCommand]
        private void ThemeChange()
        {
            IsDarkTheme = !IsDarkTheme;
            App.SetTheme(IsDarkTheme);
        }

        [RelayCommand]
        private void ToggleActivity() => IsActivityVisible = !IsActivityVisible;

        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General)
                return;

            // SystemEvents fires on a background thread — marshal to the UI thread.
            OnUiThread(() =>
            {
                IsDarkTheme = App.IsDarkModeEnabled();
                App.SetTheme(IsDarkTheme);
            });
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

                if (await RefreshTargetAsync())
                    Append("Ready. Set the cue offset, then click Set Up & Audition.");
                else
                    SubHeader = "Select a music object in Wwise and click Pull Selection.";
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

        /// <summary>Re-reads the current Wwise selection into the target. Leaves the existing target
        /// untouched (and lets the service explain why) if the selection isn't a valid target.</summary>
        private async Task<bool> RefreshTargetAsync()
        {
            var target = await _service.GetSelectedTargetAsync();
            if (target == null)
                return false;

            _target = target;
            TargetName = $"{target.Name}  ({target.Type})";
            HasTarget = true;
            return true;
        }

        private bool CanPull => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanPull))]
        private async Task PullSelectionAsync()
        {
            IsBusy = true;
            try
            {
                if (await RefreshTargetAsync())
                    Append($"Target: {TargetName}");
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
                _service.LengthSource = (SegmentLengthSource)LengthSourceIndex;
                _service.OpenPlaylistEditor = OpenPlaylistEditor;
                await _service.SetUpAuditionAsync(_target);
                IsReady = true;

                // Reveal the copied structure once setup (and its undo group) has finished, so the
                // selection actually takes and the editor follows it.
                await _service.ShowInProjectExplorerAsync();
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
        private async Task ShowInExplorerAsync()
        {
            _service.OpenPlaylistEditor = OpenPlaylistEditor;
            await _service.ShowInProjectExplorerAsync();
        }

        [RelayCommand(CanExecute = nameof(CanInteract))]
        private async Task PlayAsync() => await _service.PlayAsync();

        [RelayCommand(CanExecute = nameof(CanInteract))]
        private async Task StopAsync() => await _service.StopAsync();

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

        private void Append(string message) =>
            OnUiThread(() => LogText = $"{DateTime.Now:HH:mm:ss}: {message}\n{LogText}");

        /// <summary>Runs an action on the UI thread (WAAPI events arrive on background threads).</summary>
        private static void OnUiThread(Action action)
        {
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                dispatcher.Invoke(action);
            else
                action();
        }
    }
}

