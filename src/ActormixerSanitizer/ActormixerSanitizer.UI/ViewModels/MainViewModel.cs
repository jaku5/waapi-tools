using CommunityToolkit.Mvvm.Input;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
        }
    }

    private IEnumerable<ActorMixerInfo> SelectedActors => ActorMixers.Where(a => a.IsSelected);

    public MainViewModel()
    {
        _service = new ActormixerSanitizerService();
        _service.LogMessage += OnLogMessage;
        _service.Disconnected += OnDisconnected;

        ActorMixers = new ObservableCollection<ActorMixerInfo>();

        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        ScanCommand = new AsyncRelayCommand(ScanAsync);
        SelectAllCommand = new RelayCommand(SelectAll);
        SelectNoneCommand = new RelayCommand(SelectNone);
        ToggleSelectedCommand = new RelayCommand<IList>(ToggleSelected);
        ConvertCommand = new AsyncRelayCommand(ConvertAsync);
        CopyNameCommand = new RelayCommand<ActorMixerInfo>(actor => CopyToClipboard(actor?.Name));
        CopyIdCommand = new RelayCommand<ActorMixerInfo>(actor => CopyToClipboard(actor?.Id));
        CopyPathCommand = new RelayCommand<ActorMixerInfo>(actor => CopyToClipboard(actor?.Path));
        SelectInWwiseCommand = new RelayCommand<ActorMixerInfo>(actor => SelectInWwise(actor?.Id));
        ShowSelectedListCommand = new AsyncRelayCommand(ShowSelectedList);

        _ = ConnectAsync();
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
        try
        {
            var actors = await _service.GetSanitizableMixersAsync();
            ActorMixers.Clear();
            foreach (var actor in actors)
            {
                actor.IsSelected = true;
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
        var selectedActors = SelectedActors.ToList();

        if (selectedActors.Count == 0)
        {
            AddLog("No actors selected");
            return;
        }

        try
        {
            await _service.ConvertToFoldersAsync(selectedActors);
            await ScanAsync(); // Refresh the list
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

    private void OnLogMessage(object sender, string message)
    {
        AddLog(message);
    }

    private void OnDisconnected(object sender, EventArgs e)
    {
        AddLog("Disconnected from Wwise");
        IsNotConnected = true;
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
            AddLog("No selected actors tho show");
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
}