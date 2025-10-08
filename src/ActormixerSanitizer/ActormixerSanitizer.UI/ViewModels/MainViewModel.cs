using ActormixerSanitizer.UI.Messages;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;


namespace ActormixerSanitizer.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ActormixerSanitizerService _service;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IMessenger _messenger;
        private CancellationTokenSource _dialogCts = new CancellationTokenSource();

        
        private ObservableCollection<ActorMixerInfo> _actorMixers;
  
        private string _logText = "";

        public ICommand ConnectCommand { get; }
        public ICommand ScanCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand ToggleSelectedCommand { get; }
        public ICommand ConvertCommand { get; }
        public ICommand CopyNameCommand { get; }
        public ICommand CopyIdCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand SelectInWwiseCommand { get; }
        public ICommand ShowSelectedListCommand { get; }
        public ICommand ThemeChangeCommand { get; }
        public ICommand ToggleLogViewerCommand { get; }

        public IMessenger Messenger => _messenger;

        public ObservableCollection<ActorMixerInfo> ActorMixers
        {
            get => _actorMixers;
            set
            {
                _actorMixers = value;
                OnPropertyChanged();
            }
        }

        public string LogText
        {
            get => _logText;
            private set
            {
                _logText = value;
                OnPropertyChanged();
            }
        }




        private bool _isNotConnected;
        public bool IsNotConnected
        {
            get => _isNotConnected;
            set
            {
                _isNotConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsScanEnabled));
                OnPropertyChanged(nameof(IsConvertEnabled));
                OnPropertyChanged(nameof(IsShowSelectedListEnabled));
                OnPropertyChanged(nameof(ConnectIcon));
                OnPropertyChanged(nameof(ShowSelectedListIcon));
                OnPropertyChanged(nameof(ConvertIcon));
                OnPropertyChanged(nameof(IsConnectIconFilled));
                OnPropertyChanged(nameof(IsSelectionEnabled));
            }
        }

        public bool IsRescanRequired => IsDirty || IsSaved || IsConverted;

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                _isDirty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSelectionEnabled));
                OnPropertyChanged(nameof(IsRescanRequired));
            }
        }

        private bool _isSaved;
        public bool IsSaved
        {
            get => _isSaved;
            set
            {
                _isSaved = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSelectionEnabled));
                OnPropertyChanged(nameof(IsRescanRequired));
            }
        }

        private bool _isConverted;
        public bool IsConverted
        {
            get => _isConverted;
            set
            {
                _isConverted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSelectionEnabled));
                OnPropertyChanged(nameof(IsRescanRequired));
            }
        }

        private bool _isConnectionLost = false;
        public bool IsConnectionLost
        {
            get => _isConnectionLost;
            set
            {
                _isConnectionLost = value;
                OnPropertyChanged();
            }
        }

        private bool _isScanned;
        public bool IsScanned
        {
            get => _isScanned;
            set
            {
                _isScanned = value;
                OnPropertyChanged();
            }
        }

        private bool _isDarkTheme;
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (_isDarkTheme != value)
                {
                    _isDarkTheme = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActorIcon));
                    OnPropertyChanged(nameof(ThemeIcon));
                }
            }
        }

        public string ActorIcon => _isDarkTheme ? "pack://application:,,,/ActormixerSanitizer.UI;component/Resources/ObjectIcons_ActorMixer_nor_light.png" : "pack://application:,,,/ActormixerSanitizer.UI;component/Resources/ObjectIcons_ActorMixer_nor.png";
        public string ThemeIcon => _isDarkTheme ? "\uE706" : "\uEC46";
        public string ConnectIcon => IsNotConnected ? "\ueb55" : "\uec64";
        public bool IsConnectIconFilled => IsNotConnected;
        public string ShowSelectedListIcon => IsShowSelectedListEnabled ? "\ue7ac" : "\ue7ba";
        public string ConvertIcon => IsConvertEnabled ? "\ue19c" : "\ue8f6";

        public bool IsScanEnabled => !IsNotConnected && !IsDialogOpen;
        public bool IsSelectionEnabled => IsScanEnabled && !IsDirty && !IsSaved && !IsConverted;
        public bool IsConvertEnabled => !IsNotConnected && !IsDialogOpen;
        public bool IsShowSelectedListEnabled => !IsNotConnected && !IsDialogOpen;

        private IEnumerable<ActorMixerInfo> SelectedActors => ActorMixers.Where(a => a.IsSelected);

        public MainViewModel(ActormixerSanitizerService service, ILogger<MainViewModel> logger, IMessenger messenger)
        {
            IsNotConnected = true;

            _service = service;
            _logger = logger;
            _messenger = messenger;
            _service.StatusUpdated += OnStatusUpdated;
            _service.NotificationRequested += OnNotificationRequested;
            _service.Disconnected += OnDisconnected;
            _service.ProjectStateChanged += OnProjectStateChanged;

            ActorMixers = new ObservableCollection<ActorMixerInfo>();

            ConnectCommand = new AsyncRelayCommand(ConnectAsync);
            ScanCommand = new AsyncRelayCommand(ScanAsync);
            SelectAllCommand = new RelayCommand(SelectAll);
            SelectNoneCommand = new RelayCommand(SelectNone);
            ToggleSelectedCommand = new RelayCommand<IList>(ToggleSelected);
            ConvertCommand = new AsyncRelayCommand(ConvertAsync);
            CopyNameCommand = new RelayCommand<ActorMixerInfo>(actor =>
            {
                if (!string.IsNullOrEmpty(actor?.Name))
                    CopyToClipboard(actor.Name);
            });
            CopyIdCommand = new RelayCommand<ActorMixerInfo>(actor => CopyToClipboard(actor?.Id));
            CopyPathCommand = new RelayCommand<ActorMixerInfo>(actor => CopyToClipboard(actor?.Path));
            SelectInWwiseCommand = new RelayCommand<ActorMixerInfo>(actor => SelectInWwise(actor?.Id));
            ShowSelectedListCommand = new AsyncRelayCommand(ShowSelectedList);
            ThemeChangeCommand = new RelayCommand(ThemeChange);
            ToggleLogViewerCommand = new RelayCommand(ToggleLogViewer);

            IsDarkTheme = App.IsDarkModeEnabled();
            App.SetTheme(IsDarkTheme);

            _ = ConnectAsync();
        }

        private bool _isLogViewerVisible = false;

        private void ToggleLogViewer()
        {
            _isLogViewerVisible = !_isLogViewerVisible;
            _messenger.Send(new ToggleLogViewerMessage(_isLogViewerVisible));
        }



        private bool _isDialogOpen;
        public bool IsDialogOpen
        {
            get => _isDialogOpen;
            set
            {
                if (_isDialogOpen != value)
                {
                    _isDialogOpen = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsScanEnabled));
                    OnPropertyChanged(nameof(IsConvertEnabled));
                    OnPropertyChanged(nameof(IsShowSelectedListEnabled));
                    OnPropertyChanged(nameof(IsSelectionEnabled));
                }
            }
        }

        private async void OnNotificationRequested(object sender, string message)
        {
            // Cancel any existing dialog
            _dialogCts.Cancel();
            _dialogCts = new CancellationTokenSource();
            var token = _dialogCts.Token;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new Dialogs.MessageDialog(
                    "Notification",
                    message,
                    Application.Current.MainWindow);

                token.Register(() => Application.Current.Dispatcher.Invoke(dialog.Close));

                IsDialogOpen = true;
                dialog.Closed += (s, e) =>
                {
                    IsDialogOpen = false;
                    Application.Current.MainWindow.Activate();
                };

                dialog.Show();
            });
        }

        private async void OnStatusUpdated(object sender, string message)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => AddLog(message));
        }

        private void AddLog(string message)
        {
            LogText = $"{DateTime.Now:HH:mm:ss}: {message}\n{LogText}";
            _logger.LogInformation(message);
        }



        private bool IsReadyForConvert()
        {
            if (IsSaved)
            {
                OnNotificationRequested(this, "Project has been saved. Please scan again before converting.");
                return false;
            }
            if (IsConverted)
            {
                OnNotificationRequested(this, "Some objects have been converted. Please scan again to refresh the list.");
                return false;
            }
            if (!IsScanned)
            {
                OnNotificationRequested(this, "Please scan the project before converting.");
                return false;
            }
            return true;
        }

        private void ThemeChange()
        {
            IsDarkTheme = !IsDarkTheme;
            App.SetTheme(IsDarkTheme);
        }

        private async Task ConnectAsync()
        {
            _service.Disconnect();
            try
            {
                await _service.ConnectAsync();
                AddLog("Connected to Wwise");
                IsNotConnected = false;
            }
            catch (Exception ex)
            {
                AddLog($"Connection failed: {ex.Message}");
                IsNotConnected = true;
            }
        }

        private async Task ScanAsync()
        {
            if (await _service.CheckProjectStateAsync())
            {
                OnNotificationRequested(this, "Project has unsaved changes. Please save the project in Wwise before scanning.");
                return;
            }

            try
            {
                var selectedIds = SelectedActors.Select(a => a.Id).ToHashSet();
                var actors = await _service.GetSanitizableMixersAsync();

                ActorMixers.Clear();

                foreach (var actor in actors)
                {
                    if (selectedIds.Contains(actor.Id))
                    {
                        actor.IsSelected = true;
                    }
                    ActorMixers.Add(actor);
                }
                AddLog($"Found {actors.Count} actor mixers that can be converted");
            }
            catch (Exception ex)
            {
                AddLog($"Scan failed: {ex.Message}");
            }
        }

        private void SelectAll()
        {
            if (ActorMixers.Any())
            {
                foreach (var actor in ActorMixers)
                {
                    actor.IsSelected = true;
                }
            }

            else
                AddLog("Nothing to select");
        }

        private void SelectNone()
        {
            if (ActorMixers.Any())
            {
                foreach (var actor in ActorMixers)
                {
                    actor.IsSelected = false;
                }
            }

            else
                AddLog("Nothing to deselect");
        }

        private void ToggleSelected(IList selectedItems)
        {
            if (selectedItems != null && selectedItems.Count > 0)
            {
                foreach (ActorMixerInfo actor in selectedItems.OfType<ActorMixerInfo>().ToList())
                {
                    actor.IsSelected = !actor.IsSelected;
                }
            }

            else
                AddLog("Nothing to toggle");
        }


        private async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();

            // Cancel any existing dialog
            _dialogCts.Cancel();
            _dialogCts = new CancellationTokenSource();
            var token = _dialogCts.Token;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new Dialogs.MessageDialog(title, message, Application.Current.MainWindow, true);
                token.Register(() =>
                {
                    Application.Current.Dispatcher.Invoke(dialog.Close);
                    tcs.TrySetResult(false); // Treat cancellation as a "No"
                });

                dialog.Closed += (s, e) =>
                {
                    IsDialogOpen = false;
                    tcs.TrySetResult(dialog.Result ?? false);
                    Application.Current.MainWindow.Activate();
                };

                IsDialogOpen = true;
                dialog.Show();
            });

            return await tcs.Task;
        }

        private async Task ConvertAsync()
        {
            if (await _service.CheckProjectStateAsync())
            {
                OnNotificationRequested(this, "Project has unsaved changes. Please save the project in Wwise before converting.");
                return;
            }

            if (!IsReadyForConvert())
                return;

            var selectedActors = SelectedActors.ToList();

            if (selectedActors.Count == 0)
            {
                AddLog("No actors selected");
                return;
            }

            var confirmed = await ShowConfirmationDialog(
                "Confirm Conversion",
                $"Are you sure you want to convert {selectedActors.Count} actor-mixers?");

            if (!confirmed)
                return;

            try
            {
                await _service.ConvertToFoldersAsync(selectedActors);

                foreach (var actor in selectedActors)
                {
                    ActorMixers.Remove(actor);
                }

                OnNotificationRequested(this, $"Successfully converted {selectedActors.Count} actor-mixers.");
            }
            catch (Exception ex)
            {
                AddLog($"Conversion failed: {ex.Message}");
            }
        }

        private void CopyToClipboard(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
                AddLog($"Copied to clipboard: {text}");
            }
        }

        private async void OnDisconnected(object sender, EventArgs e)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AddLog("Disconnected from Wwise");
                IsNotConnected = true;
            });
        }

        private async void OnProjectStateChanged(object sender, EventArgs e)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsDirty = _service.IsDirty;
                IsSaved = _service.IsSaved;
                IsConverted = _service.IsConverted;
                IsConnectionLost = _service.IsConnectionLost;
                IsScanned = _service.IsScanned;
            });
        }

        private async Task SelectInWwise(string actorId)
        {
            try
            {
                await _service.SelectInProjectExplorer(actorId);
            }
            catch (Exception ex)
            {
                AddLog($"Select in Wwise failed: {ex.Message}");
            }
        }
        private async Task ShowSelectedList()
        {
            var selectedActors = SelectedActors.ToList();

            if (!selectedActors.Any())
            {
                AddLog("No selected actors to show");
                return;
            }

            try
            {
                await _service.ShowInListView(selectedActors);
            }
            catch (Exception ex)
            {
                AddLog($"Show list failed: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public async Task Cleanup()
        {
            await _service.UnsubscribeFromChangesAsync();
        }
    }
}