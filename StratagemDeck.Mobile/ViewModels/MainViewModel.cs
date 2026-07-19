using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StratagemDeck.Mobile.Models;
using StratagemDeck.Mobile.Services;

namespace StratagemDeck.Mobile.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly StratagemDataService _dataService;
    private readonly PreferencesService _prefs;
    private readonly UdpDiscoveryService _discovery;
    private readonly StratagemSender _sender;

    private string _status = "Scanning...";
    private string _serverIp = string.Empty;
    private string _serverName = string.Empty;
    private bool _isConnected;
    private bool _isScanning;
    private string _searchQuery = string.Empty;
    private LoadoutSlot? _selectedSlot;
    private bool _isSending;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LoadoutSlot> Slots { get; } = new();
    public ObservableCollection<Stratagem> AvailableStrats { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<DiscoveryInfo> DiscoveredServers { get; } = new();

    public ICommand SelectSlotCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand TapStratagemCommand { get; }
    public ICommand DiscoveryTappedCommand { get; }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string ServerName
    {
        get => _serverName;
        set { _serverName = value; OnPropertyChanged(); }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            OnPropertyChanged();
        }
    }

    public LoadoutSlot? SelectedSlot
    {
        get => _selectedSlot;
        set
        {
            _selectedSlot = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSlotIndex));
        }
    }

    public int SelectedSlotIndex => SelectedSlot?.SlotIndex ?? -1;

    public bool IsSending
    {
        get => _isSending;
        set { _isSending = value; OnPropertyChanged(); }
    }

    public string Pin { get; set; } = string.Empty;

    public MainViewModel(
        StratagemDataService dataService,
        PreferencesService prefs,
        UdpDiscoveryService discovery,
        StratagemSender sender)
    {
        _dataService = dataService;
        _prefs = prefs;
        _discovery = discovery;
        _sender = sender;

        _discovery.OnServerDiscovered += OnServerDiscovered;

        SelectSlotCommand = new Command<string>(OnSelectSlot);
        SelectCategoryCommand = new Command<string>(OnSelectCategory);
        TapStratagemCommand = new Command<Stratagem>(async (s) => await OnTapStratagem(s));
        DiscoveryTappedCommand = new Command<DiscoveryInfo>(async (d) => await ConnectToServer(d));
    }

    public async Task InitializeAsync()
    {
        await _dataService.LoadAsync();

        Categories.Clear();
        foreach (var cat in _dataService.Categories)
            Categories.Add(cat);

        var allStrats = _dataService.GetByCategory(_dataService.Categories.FirstOrDefault() ?? "");
        Slots.Clear();
        var saved = _prefs.LoadLoadout(allStrats);
        foreach (var slot in saved)
            Slots.Add(slot);

        var savedIp = _prefs.GetLastServerIp();
        var savedPin = _prefs.GetLastPairedPin();

        if (savedIp != null && savedPin != null)
        {
            Pin = savedPin;
            Status = "Connecting...";
            var ok = await _discovery.PingServer(savedIp, savedPin);
            if (ok)
            {
                _serverIp = savedIp;
                ServerName = savedIp;
                IsConnected = true;
                Status = "Connected";
            }
            else
            {
                Status = "Scanning...";
                StartScanning();
            }
        }
        else
        {
            StartScanning();
        }
    }

    public void StartScanning()
    {
        IsScanning = true;
        Status = "Scanning...";
        _discovery.StartScanning();
    }

    public async Task ConnectToServer(DiscoveryInfo server)
    {
        _discovery.StopScanning();
        IsScanning = false;
        Status = "Connecting...";

        var ok = await _discovery.PingServer(server.IpAddress, server.Pin);
        if (ok)
        {
            _serverIp = server.IpAddress;
            _serverName = server.PcName;
            Pin = server.Pin;
            IsConnected = true;
            Status = "Connected";
            _prefs.SaveServerIp(server.IpAddress);
            _prefs.SavePairedPin(server.Pin);
        }
        else
        {
            Status = "Failed, retrying...";
            StartScanning();
        }
    }

    public async Task ConnectWithPin(string ip, string pin)
    {
        Status = "Connecting...";
        var ok = await _discovery.PingServer(ip, pin);
        if (ok)
        {
            _serverIp = ip;
            _serverName = ip;
            Pin = pin;
            IsConnected = true;
            Status = "Connected";
            _prefs.SaveServerIp(ip);
            _prefs.SavePairedPin(pin);
        }
        else
        {
            Status = "Invalid PIN";
        }
    }

    private void OnSelectSlot(string? indexStr)
    {
        if (!int.TryParse(indexStr, out var idx)) return;
        var slot = Slots.FirstOrDefault(s => s.SlotIndex == idx);

        if (SelectedSlot?.SlotIndex == idx)
        {
            SelectedSlot = null;
        }
        else
        {
            SelectedSlot = slot;
            Status = slot?.SelectedStratagem != null
                ? $"Slot {idx + 1}: {slot.SelectedStratagem.DisplayName} (tap stratagem to replace)"
                : $"Slot {idx + 1} selected - choose a stratagem";
        }
    }

    private void OnSelectCategory(string? category)
    {
        if (string.IsNullOrEmpty(category)) return;

        AvailableStrats.Clear();
        foreach (var s in _dataService.GetByCategory(category))
            AvailableStrats.Add(s);

        SearchQuery = string.Empty;
    }

    private async Task OnTapStratagem(Stratagem stratagem)
    {
        if (SelectedSlot != null)
        {
        var slot = Slots.FirstOrDefault(s => s.SlotIndex == SelectedSlot.SlotIndex);
        if (slot == null) return;

        slot.SelectedStratagem = stratagem;

        SaveLoadout();
        Status = $"Assigned {stratagem.DisplayName} to Slot {SelectedSlot.SlotIndex + 1}";
        SelectedSlot = null;
            return;
        }

        if (!IsConnected || string.IsNullOrEmpty(_serverIp))
        {
            Status = "Not connected";
            return;
        }

        if (_isSending) return;
        _isSending = true;

        try
        {
            Status = $"Sending: {stratagem.DisplayName}";
            await _sender.SendAsync(_serverIp, Pin, stratagem);
            Status = "Sent";

            await Task.Delay(1200);
            Status = IsConnected ? "Connected" : "Ready";
        }
        finally
        {
            _isSending = false;
        }
    }

    public void SaveLoadout()
    {
        _prefs.SaveLoadout(Slots.ToList());
    }

    private void OnServerDiscovered(DiscoveryInfo info)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = DiscoveredServers.FirstOrDefault(s => s.IpAddress == info.IpAddress);
            if (existing != null)
            {
                var idx = DiscoveredServers.IndexOf(existing);
                DiscoveredServers[idx] = info;
            }
            else
            {
                DiscoveredServers.Add(info);
            }
        });
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
