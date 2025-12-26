using ActormixerSanitizer.UI.Messages;
using ActormixerSanitizer.UI.Services;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core.Models;
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
        private readonly IActormixerSanitizerService _service;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IMessenger _messenger;
        private readonly IDialogService _dialogService;
        private CancellationTokenSource _dialogCts = new CancellationTokenSource();


        private ObservableCollection<ActorMixerInfo> _actorMixers;

        private string _logText = "";
        private string _currentStatus = "";

        public ICommand ConnectCommand { get; }
        public ICommand ScanCommand { get; }
        public ICommand MarkAllCommand { get; }
        public ICommand UnmarkAllCommand { get; }
        public ICommand ToggleMarkedCommand { get; }
        public ICommand ConvertCommand { get; }
        public ICommand CopyNameCommand { get; }
        public ICommand CopyIdCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand SelectInWwiseCommand { get; }
        public ICommand ShowMarkedListCommand { get; }
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

        public string CurrentStatus
        {
            get => _currentStatus;
            private set
            {
                _currentStatus = value;
                OnPropertyChanged();
            }
        }




        public bool IsRescanRequired => IsDirty || IsSaved || IsConverted || (!IsScanned && ActorMixers.Any());

        private bool _isNotConnected;
        public bool IsNotConnected
        {
            get => _isNotConnected;
            set
            {
                _isNotConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectIcon));
                OnPropertyChanged(nameof(IsConnectIconFilled));
                NotifyStateChanged();
            }
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                _isDirty = value;
                OnPropertyChanged();
                NotifyStateChanged();
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
                NotifyStateChanged();
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
                NotifyStateChanged();
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
                NotifyStateChanged();
            }
        }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                _isConnecting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WindowTitle));
                NotifyStateChanged();
            }
        }

        public string ProjectName => _service.ProjectName;
        public string WwiseVersion => _service.WwiseVersion;

        public bool CanConnect => IsNotConnected && !IsConnecting;
        public bool ShowConnectingProgress => IsConnecting;
        public bool ShowConnectionIcon => !IsConnecting;

        public string WindowTitle
        {
            get
            {
                string baseTitle = "Actormixer Sanitizer";
                if (IsConnecting) return $"{baseTitle} - [Connecting...]";
                if (IsNotConnected) return $"{baseTitle} - [Disconnected]";
                
                string projectPart = !string.IsNullOrEmpty(ProjectName) ? $" - {ProjectName}" : "";
                string versionPart = !string.IsNullOrEmpty(WwiseVersion) ? $" (Wwise {WwiseVersion})" : "";
                
                return $"{baseTitle}{projectPart}{versionPart}";
            }
        }

        public bool IsScanning
        {
            get => _service.IsScanning;
            private set
            {
                OnPropertyChanged();
                NotifyStateChanged();
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
                    OnPropertyChanged(nameof(FolderIcon));
                    OnPropertyChanged(nameof(ThemeIcon));
                }
            }
        }

        public string ActorIcon => _isDarkTheme ? "pack://application:,,,/ActormixerSanitizer.UI;component/Resources/ObjectIcons_ActorMixer_nor_light.png" : "pack://application:,,,/ActormixerSanitizer.UI;component/Resources/ObjectIcons_ActorMixer_nor.png";
        public string FolderIcon => _isDarkTheme ? "pack://application:,,,/ActormixerSanitizer.UI;component/Resources/ObjectIcons_Folder_nor_light.png" : "pack://application:,,,/ActormixerSanitizer.UI;component/Resources/ObjectIcons_Folder_nor.png";
        public string ThemeIcon => _isDarkTheme ? "\uE706" : "\uEC46";
        public string ConnectIcon => IsNotConnected ? "\ueb55" : "\uec64";
        public bool IsConnectIconFilled => IsNotConnected;
        public string ShowMarkedListIcon => IsShowMarkedListEnabled ? "\ue7ac" : "\ue7ba";
        public string ConvertIcon => IsMarkingEnabled ? "\uf5b0" : "\ue7ba";

        public bool IsScanEnabled => !IsNotConnected && !IsDialogOpen && !IsScanning;
        public bool IsMarkingEnabled => IsScanEnabled && !IsDirty && !IsSaved && !IsConverted && ActorMixers != null && ActorMixers.Any() && !IsScanning;
        public bool IsSelectionEnabled => IsMarkingEnabled;
        public bool IsConvertEnabled => !IsNotConnected && !IsDialogOpen && !IsScanning;
        public bool IsShowMarkedListEnabled => !IsNotConnected && !IsDialogOpen && ActorMixers != null && ActorMixers.Any() && !IsScanning;

        private IEnumerable<ActorMixerInfo> MarkedActors => ActorMixers.Where(a => a.IsMarked);

        public MainViewModel(IActormixerSanitizerService service, ILogger<MainViewModel> logger, IMessenger messenger, IDialogService dialogService)
        {
            IsNotConnected = true;

            _service = service;
            _logger = logger;
            _messenger = messenger;
            _dialogService = dialogService;
            _service.StatusUpdated += OnStatusUpdated;
            _service.NotificationRequested += OnNotificationRequested;
            _service.Disconnected += OnDisconnected;
            _service.ProjectStateChanged += OnProjectStateChanged;

            ActorMixers = new ObservableCollection<ActorMixerInfo>();

            ConnectCommand = new AsyncRelayCommand(ConnectAsync);
            ScanCommand = new AsyncRelayCommand(ScanAsync);
            MarkAllCommand = new RelayCommand(MarkAll);
            UnmarkAllCommand = new RelayCommand(UnmarkAll);
            ToggleMarkedCommand = new RelayCommand<IList>(ToggleMarked);
            ConvertCommand = new AsyncRelayCommand(ConvertAsync);
            CopyNameCommand = new RelayCommand<ActorMixerInfo>(actor =>
            {
                if (!string.IsNullOrEmpty(actor?.Name))
                    CopyToClipboard(actor.Name);
            });
            CopyIdCommand = new RelayCommand<ActorMixerInfo>(actor => CopyToClipboard(actor?.Id));
            CopyPathCommand = new RelayCommand<ActorMixerInfo>(actor => CopyToClipboard(actor?.Path));
            SelectInWwiseCommand = new AsyncRelayCommand<ActorMixerInfo>(async actor => await SelectInWwise(actor?.Id));
            ShowMarkedListCommand = new AsyncRelayCommand(ShowMarkedList);
            ThemeChangeCommand = new RelayCommand(ThemeChange);
            ToggleLogViewerCommand = new RelayCommand(ToggleLogViewer);

            IsDarkTheme = App.IsDarkModeEnabled();
            App.SetTheme(IsDarkTheme);

            _ = ConnectAsync();
            _ = StartAutoConnectLoop();
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
                    NotifyStateChanged();
                }
            }
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsScanEnabled));
            OnPropertyChanged(nameof(IsMarkingEnabled));
            OnPropertyChanged(nameof(IsConvertEnabled));
            OnPropertyChanged(nameof(IsSelectionEnabled));
            OnPropertyChanged(nameof(IsRescanRequired));
            OnPropertyChanged(nameof(IsShowMarkedListEnabled));
            OnPropertyChanged(nameof(ConvertIcon));
            OnPropertyChanged(nameof(ShowMarkedListIcon));
            OnPropertyChanged(nameof(IsScanning));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(ShowConnectingProgress));
            OnPropertyChanged(nameof(ShowConnectionIcon));
        }

        private async void OnNotificationRequested(object sender, string message)
        {
            IsDialogOpen = true;
            await _dialogService.ShowNotification("Notification", message);
            IsDialogOpen = false;
        }

        private async void OnStatusUpdated(object sender, string message)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => AddLog(message));
        }

        private void AddLog(string message)
        {
            CurrentStatus = message;
            LogText = $"{DateTime.Now:HH:mm:ss}: {message}\n{LogText}";
            _logger.LogInformation(message);
        }



        private async Task<bool> IsReadyForConvert()
        {
            if (IsSaved)
            {
                await _dialogService.ShowNotification("Notification", "Project has been saved. Please scan again before converting.");
                return false;
            }
            if (IsConverted)
            {
                await _dialogService.ShowNotification("Notification", "Some objects have been converted. Please scan again to refresh the list.");
                return false;
            }
            if (!IsScanned)
            {
                await _dialogService.ShowNotification("Notification", "Please scan the project before converting.");
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
            if (IsConnecting) return;
            
            try
            {
                IsConnecting = true;
                _service.Disconnect();
                await _service.ConnectAsync();
                AddLog("Connected to Wwise");
                IsNotConnected = false;
            }
            catch (Exception ex)
            {
                AddLog($"Connection failed: {ex.Message}");
                IsNotConnected = true;
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private async Task StartAutoConnectLoop()
        {
            while (true)
            {
                if (IsNotConnected && !IsConnecting)
                {
                    try
                    {
                        await ConnectAsync();
                    }
                    catch
                    {
                        // Ignore connection attempts in loop
                    }
                }
                await Task.Delay(5000); // Check every 5 seconds
            }
        }

        private async Task ScanAsync()
        {
            if (await _service.CheckProjectStateAsync())
            {
                await _dialogService.ShowNotification("Notification", "Project has unsaved changes. Please save the project in Wwise before scanning.");
                return;
            }

            try
            {
                var markedIds = MarkedActors.Select(a => a.Id).ToHashSet();
                var actors = await _service.GetSanitizableMixersAsync();

                ActorMixers.Clear();

                if (actors != null)
                {
                    foreach (var actor in actors)
                    {
                        if (markedIds.Contains(actor.Id))
                        {
                            actor.IsMarked = true;
                        }
                        ActorMixers.Add(actor);
                    }
                    AddLog($"Found {actors.Count} actor mixer{(actors.Count == 1 ? "" : "s")} that can be converted");
                }
                else
                {
                    AddLog("Scan returned no mixers.");
                }
                IsScanned = true;
            }
            catch (Exception ex)
            {
                AddLog($"Scan failed: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
                OnPropertyChanged(nameof(IsScanning));
                NotifyStateChanged();
            }
        }

        private void MarkAll()
        {
            if (ActorMixers.Any())
            {
                foreach (var actor in ActorMixers)
                {
                    actor.IsMarked = true;
                }
            }

            else
                AddLog("Nothing to mark");
        }

        private void UnmarkAll()
        {
            if (ActorMixers.Any())
            {
                foreach (var actor in ActorMixers)
                {
                    actor.IsMarked = false;
                }
            }

            else
                AddLog("Nothing to unmark");
        }

        private void ToggleMarked(IList selectedItems)
        {
            if (selectedItems != null && selectedItems.Count > 0)
            {
                foreach (ActorMixerInfo actor in selectedItems.OfType<ActorMixerInfo>().ToList())
                {
                    actor.IsMarked = !actor.IsMarked;
                }
            }

            else
                AddLog("Nothing selected in the list to toggle");
        }

        private async Task ConvertAsync()
        {
            if (await _service.CheckProjectStateAsync())
            {
                await _dialogService.ShowNotification("Notification", "Project has unsaved changes. Please save the project in Wwise before converting.");
                return;
            }

            if (!await IsReadyForConvert())
                return;

            var markedActors = MarkedActors.ToList();

            if (markedActors.Count == 0)
            {
                AddLog("No actors marked");
                return;
            }

            var confirmed = await _dialogService.ShowConfirmationDialog(
                "Confirm Conversion",
                $"Are you sure you want to convert {markedActors.Count} actor-mixer{(markedActors.Count == 1 ? "" : "s")}?");

            if (!confirmed)
                return;

            try
            {
                await _service.ConvertToFoldersAsync(markedActors);

                foreach (var actor in markedActors)
                {
                    ActorMixers.Remove(actor);
                }

                string message = $"Successfully converted {markedActors.Count} Actor-mixer{(markedActors.Count == 1 ? "" : "s")} to Virtual Folder{(markedActors.Count == 1 ? "" : "s")}.";
                await _dialogService.ShowNotification("Conversion Successful", message);
                OnPropertyChanged(nameof(IsMarkingEnabled));
                OnPropertyChanged(nameof(IsShowMarkedListEnabled));
            }
            catch (Exception ex)
            {
                string errorMessage = $"An error occurred during conversion:\n\n{ex.Message}\n\n" +
                                     "Some changes may have been partially applied. Please check the log for details.";
                AddLog($"Conversion failed: {ex.Message}");
                await _dialogService.ShowNotification("Conversion Failed", errorMessage);
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
                OnPropertyChanged(nameof(IsScanning));
                NotifyStateChanged();
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
        private async Task ShowMarkedList()
        {
            var markedActors = MarkedActors.ToList();

            if (!markedActors.Any())
            {
                AddLog("No marked actors to show");
                return;
            }

            try
            {
                await _service.ShowInListView(markedActors);
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