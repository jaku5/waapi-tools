using CommunityToolkit.Mvvm.Input;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Linq;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ActormixerSanitizerService _service;
    private ObservableCollection<ActorMixerInfo> _actorMixers;
    private string _logText = "";

    public ICommand ConnectCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand ConvertCommand { get; }

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

    public MainViewModel()
    {
        _service = new ActormixerSanitizerService();
        _service.LogMessage += OnLogMessage;
        _service.Disconnected += OnDisconnected;

        ActorMixers = new ObservableCollection<ActorMixerInfo>();

        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        ScanCommand = new AsyncRelayCommand(ScanAsync);
        ConvertCommand = new AsyncRelayCommand(ConvertAsync);
    }

    private async Task ConnectAsync()
    {
        try
        {
            await _service.ConnectAsync();
            AddLog("Connected to Wwise");
        }
        catch (Exception ex)
        {
            AddLog($"Connection failed: {ex.Message}");
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
                actor.IsSelected = true; // Select all by default
                ActorMixers.Add(actor);
            }
            AddLog($"Found {actors.Count} actor mixers that can be converted");
        }
        catch (Exception ex)
        {
            AddLog($"Scan failed: {ex.Message}");
        }
    }

    private async Task ConvertAsync()
    {
        var selectedActors = ActorMixers.Where(a => a.IsSelected).ToList();

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

    private void OnLogMessage(object sender, string message)
    {
        AddLog(message);
    }

    private void OnDisconnected(object sender, EventArgs e)
    {
        AddLog("Disconnected from Wwise");
    }

    private void AddLog(string message)
    {
        LogText = $"{DateTime.Now:HH:mm:ss}: {message}\n{LogText}";
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}