using CommunityToolkit.Mvvm.Input;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ActormixerSanitizerService _service;
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
            }
        }
    }

    public string ActorIcon => _isDarkTheme ? "ObjectIcons_ActorMixer_nor_light.png" : "ObjectIcons_ActorMixer_nor.png";

    public bool IsScanEnabled => !IsNotConnected;
    public bool IsConvertEnabled => !IsNotConnected;
    public bool IsShowSelectedListEnabled => !IsNotConnected;

    private IEnumerable<ActorMixerInfo> SelectedActors => ActorMixers.Where(a => a.IsSelected);

    public MainViewModel()
    {
        _service = new ActormixerSanitizerService();
        _service.LogMessage += OnLogMessage;
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

        IsDarkTheme = Wpf.Ui.Appearance.ApplicationThemeManager.GetSystemTheme() == Wpf.Ui.Appearance.SystemTheme.Dark;
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(IsDarkTheme ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light);

        _ = ConnectAsync();
    }

    private void ThemeChange()
    {
        IsDarkTheme = !IsDarkTheme;
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(IsDarkTheme ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light);
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
        await _service.CheckProjectStateAsync();

        // Consolidated state validation logic
        if (!IsScanned && IsDirty)
        {
            AddLog("Project has unsaved changes. Please save before first scan.");
            return;
        }
        if (IsSaved && IsDirty)
        {
            AddLog("Project has been saved since the last scan. Please scan again before converting.");
            return;
        }

        if (IsConverted && IsDirty)
        {
            AddLog("Object have been converted since the last scan. Please scan again before converting.");
            return;
        }
        if (IsDirty)
        {
            AddLog("Project has unsaved changes. Please save before scan.");
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


    private async Task ConvertAsync()
    {
        if (IsSaved)
        {
            AddLog("Project has been saved since the last scan. Please scan again before converting.");
            return;
        }

        if (IsConverted)
        {
            AddLog("Conversion has been executed since the last scan. Please scan again before converting.");
            return;
        }

        if (!IsScanned)
        {
            AddLog("Scan was not yet run in this session. Please scan and try again.");
            return;
        }

        if (!IsDirty)
        {
            try
            {
                await _service.CheckProjectStateAsync();
            }
            catch (Exception ex)
            {
                AddLog($"Error checking project state: {ex.Message}");
                return;
            }
        }

        else
        {
            AddLog("Project has been changed since the last scan. Please scan again before converting.");
            return;
        }

        var selectedActors = SelectedActors.ToList();

        if (selectedActors.Count == 0)
        {
            AddLog("No actors selected");
            return;
        }

        try
        {
            await _service.ConvertToFoldersAsync(selectedActors);
        }
        catch (Exception ex)
        {
            AddLog($"Conversion failed: {ex.Message}");
        }

        foreach (var actor in selectedActors)
        {
            ActorMixers.Remove(actor);
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

    private void OnLogMessage(object sender, string message)
    {
        AddLog(message);
    }

    private void OnDisconnected(object sender, EventArgs e)
    {
        AddLog("Disconnected from Wwise");
        IsNotConnected = true;
    }

    private void OnProjectStateChanged(object sender, EventArgs e)
    {
        IsDirty = _service.IsDirty;
        IsSaved = _service.IsSaved;
        IsConverted = _service.IsConverted;
        IsConnectionLost = _service.IsConnectionLost;
        IsScanned = _service.IsScanned;
    }

    private void AddLog(string message)
    {
        LogText = $"{DateTime.Now:HH:mm:ss}: {message}\n{LogText}";
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