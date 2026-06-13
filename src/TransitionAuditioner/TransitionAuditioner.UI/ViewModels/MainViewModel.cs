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
        [NotifyPropertyChangedFor(nameof(TargetIcon))]
        private bool _isDarkTheme;

        /// <summary>Segoe Fluent glyph for the theme toggle: sun when dark (switch to light), moon when light.</summary>
        public string ThemeIcon => IsDarkTheme ? "" : "";

        /// <summary>Whether the Activity log panel is shown. Hidden by default to keep the window compact.</summary>
        [ObservableProperty]
        private bool _isActivityVisible;

        /// <summary>True while no Wwise connection is established. Starts disconnected.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        [NotifyPropertyChangedFor(nameof(ConnectIcon))]
        [NotifyPropertyChangedFor(nameof(CanConnect))]
        [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
        private bool _isNotConnected = true;

        /// <summary>True while a connection attempt is in flight (button shows a spinner).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        [NotifyPropertyChangedFor(nameof(ShowConnectingProgress))]
        [NotifyPropertyChangedFor(nameof(ShowConnectionIcon))]
        [NotifyPropertyChangedFor(nameof(CanConnect))]
        [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
        private bool _isConnecting;

        // Enabled only when disconnected (and not mid-attempt): while connected the button is purely
        // informational, so there's nothing to click. Both inputs must raise PropertyChanged *and*
        // NotifyCanExecuteChanged — the button binds IsEnabled to this property but, because it also
        // has a Command, WPF coerces IsEnabled down to the command's CanExecute, so the two must stay
        // in sync or a stale CanExecute wins and the button stays disabled.
        public bool CanConnect => IsNotConnected && !IsConnecting;
        public bool ShowConnectingProgress => IsConnecting;
        public bool ShowConnectionIcon => !IsConnecting;

        /// <summary>Connect-button glyph: broken link when disconnected, linked when connected.</summary>
        public string ConnectIcon => IsNotConnected ? "" : "";

        /// <summary>Window title carrying connection state and the open project / Wwise version.</summary>
        public string WindowTitle
        {
            get
            {
                const string baseTitle = "Transition Auditioner";
                if (IsConnecting) return $"{baseTitle} - [Connecting...]";
                if (IsNotConnected) return $"{baseTitle} - [Disconnected]";

                string projectPart = !string.IsNullOrEmpty(_service.ProjectName) ? $" - {_service.ProjectName}" : "";
                string versionPart = !string.IsNullOrEmpty(_service.WwiseVersion) ? $" (Wwise {_service.WwiseVersion})" : "";
                return $"{baseTitle}{projectPart}{versionPart}";
            }
        }

        /// <summary>Playlist-editor button tooltip. On Wwise 2024+ the tool opens the editor; on 2023
        /// (no getOrCreateView) it can only inspect the copied playlist, so the wording reflects that
        /// the user must open the editor themselves first.</summary>
        public string OpenPlaylistEditorToolTip => _service.CanCreatePlaylistEditorView
            ? "Open the Music Playlist Editor in Wwise"
            : "Show the copied playlist in Wwise (open a Music Playlist Editor there first)";

        [ObservableProperty]
        private string _targetName = "—";

        /// <summary>Theme-aware Wwise object icon for the current target's type, or null when there is
        /// no target (the bound Image is hidden via HasTarget in that case).</summary>
        public string? TargetIcon
        {
            get
            {
                var baseName = _target?.Type switch
                {
                    not null when Is(_target.Type, "MusicSwitchContainer") => "ObjectIcons_MusicSwitchContainer",
                    not null when Is(_target.Type, "MusicPlaylistContainer") => "ObjectIcons_MusicRandomSequenceContainer",
                    not null when Is(_target.Type, "MusicSegment") => "ObjectIcons_MusicSegment",
                    _ => null,
                };
                if (baseName is null)
                    return null;

                var suffix = IsDarkTheme ? "_nor_light" : "_nor";
                return $"pack://application:,,,/TransitionAuditioner;component/Resources/{baseName}{suffix}.png";
            }
        }

        private static bool Is(string type, string expected) =>
            string.Equals(type, expected, StringComparison.OrdinalIgnoreCase);

        /// <summary>Live description of the current Wwise selection (independent of the chosen target).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PullSelectionToolTip))]
        private string _currentSelection = "—";

        /// <summary>Id of the first currently-selected Wwise object, used to compare against the target.</summary>
        private string _currentSelectionId = string.Empty;

        /// <summary>Whether the live selection is a valid, non-harness target that can be pulled.</summary>
        private bool _selectionIsAuditionable;

        /// <summary>
        /// Pull-selection glyph: outline (EA63) when the live selection already matches the current
        /// target, filled (EA64) when it differs — signalling there's a new object to pull.
        /// </summary>
        public string PullSelectionIcon =>
            _currentSelectionId.Length > 0 && _currentSelectionId == _target?.Id
                ? ""
                : "";

        public string PullSelectionToolTip =>
            $"Use the object currently selected in Wwise\nCurrent selection: {CurrentSelection}";

        /// <summary>Cue offset from the end of each segment, in seconds (default 1 s).</summary>
        [ObservableProperty]
        private double _offsetSeconds = 1.0;

        /// <summary>
        /// Selected length basis, by index: 0 Exit cue, 1 Segment end, 2 Audio length —
        /// matching the <see cref="SegmentLengthSource"/> enum order.
        /// </summary>
        [ObservableProperty]
        private int _lengthSourceIndex;


        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
        private bool _hasTarget;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ShowInExplorerCommand))]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenPlaylistEditorCommand))]
        private bool _isReady;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
        [NotifyCanExecuteChangedFor(nameof(ShowInExplorerCommand))]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        [NotifyCanExecuteChangedFor(nameof(PullSelectionCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenPlaylistEditorCommand))]
        private bool _isBusy;

        /// <summary>Activity log as a single string, newest entry on top, each line timestamped.</summary>
        [ObservableProperty]
        private string _logText = string.Empty;

        public MainViewModel(ITransitionAuditionerService service)
        {
            _service = service;
            _service.StatusUpdated += (_, msg) => Append(msg);
            _service.NotificationRequested += (_, msg) => Append("⚠ " + msg);
            _service.Disconnected += (_, _) => OnUiThread(() =>
            {
                Append("Disconnected from Wwise.");
                IsNotConnected = true;
            });
            _service.SelectionChanged += (_, info) => OnUiThread(() =>
            {
                _currentSelectionId = info.Id;
                _selectionIsAuditionable = info.IsAuditionable;
                CurrentSelection = info.Text;
                OnPropertyChanged(nameof(PullSelectionIcon));
                PullSelectionCommand.NotifyCanExecuteChanged();
            });

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

        /// <summary>Connects on window load. Reconnect later via the Connect button.</summary>
        public Task InitializeAsync() => ConnectAsync();

        /// <summary>Connects to Wwise (dropping any existing connection first) and pulls the
        /// initial target. Drives the Connect button and the window title.</summary>
        [RelayCommand(CanExecute = nameof(CanConnect))]
        private async Task ConnectAsync()
        {
            if (IsConnecting) return;

            IsConnecting = true;
            try
            {
                _service.Disconnect();
                await _service.ConnectAsync();
                IsNotConnected = false;
                Append("Connected to Wwise.");
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(OpenPlaylistEditorToolTip));

                if (await RefreshTargetAsync())
                    Append("Ready. Set the cue offset, then click Set Up & Audition.");
                else
                    Append("Select a music object in Wwise and click Pull Selection.");
            }
            catch (Exception ex)
            {
                IsNotConnected = true;
                Append("✖ " + ex.Message);
            }
            finally
            {
                IsConnecting = false;
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
            // Just the name in the box — the type is conveyed by the icon (and stays in the Pull
            // button tooltip via the live selection text).
            TargetName = target.Name;
            HasTarget = true;
            OnPropertyChanged(nameof(PullSelectionIcon));
            OnPropertyChanged(nameof(TargetIcon));
            PullSelectionCommand.NotifyCanExecuteChanged();
            return true;
        }

        // Pullable only when the live selection is a valid, non-harness target that isn't already
        // the current target.
        private bool CanPull =>
            !IsBusy && _selectionIsAuditionable && _currentSelectionId != _target?.Id;

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
        private async Task ShowInExplorerAsync() => await _service.ShowInProjectExplorerAsync();

        [RelayCommand(CanExecute = nameof(CanInteract))]
        private async Task OpenPlaylistEditorAsync()
        {
            try
            {
                await _service.OpenPlaylistEditorAsync();
            }
            catch (Exception ex)
            {
                Append("✖ " + ex.Message);
            }
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




