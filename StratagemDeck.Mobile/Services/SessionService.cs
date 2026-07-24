using System.Collections.ObjectModel;
using StratagemDeck.Mobile.Models;

namespace StratagemDeck.Mobile.Services;

public class SessionService
{
    private readonly StratagemDataService _dataService;
    private readonly PreferencesService _prefs;
    private readonly UdpDiscoveryService _discovery;

    public ObservableCollection<LoadoutSlot> Slots { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    public string ServerIp { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsDataLoaded { get; private set; }

    public event Action? OnDataLoaded;
    public event Action? OnConnectedChanged;
    public event Action? OnLoadoutChanged;

    public SessionService(
        StratagemDataService dataService,
        PreferencesService prefs,
        UdpDiscoveryService discovery)
    {
        _dataService = dataService;
        _prefs = prefs;
        _discovery = discovery;
    }

    public async Task InitializeAsync()
    {
        if (IsDataLoaded) return;

        await _dataService.LoadAsync();

        Categories.Clear();
        foreach (var cat in _dataService.Categories)
            Categories.Add(cat);

        var allStrats = _dataService.Categories.SelectMany(c => _dataService.GetByCategory(c));
        Slots.Clear();
        var saved = _prefs.LoadLoadout(allStrats);
        foreach (var slot in saved)
            Slots.Add(slot);

        var savedIp = _prefs.GetLastServerIp();
        var savedPin = _prefs.GetLastPairedPin();
        if (savedIp != null && savedPin != null)
        {
            ServerIp = savedIp;
            Pin = savedPin;
            var ok = await _discovery.PingServer(savedIp, savedPin);
            IsConnected = ok;
            ServerName = ok ? savedIp : string.Empty;
            if (ok)
                _discovery.StopScanning();
        }

        IsDataLoaded = true;
        OnDataLoaded?.Invoke();
    }

    public void SaveLoadout()
    {
        _prefs.SaveLoadout(Slots.ToList());
        OnLoadoutChanged?.Invoke();
    }

    public async Task<bool> ConnectAsync(string ip, string pin)
    {
        var ok = await _discovery.PingServer(ip, pin);
        if (ok)
        {
            _discovery.StopScanning();
            ServerIp = ip;
            Pin = pin;
            ServerName = ip;
            IsConnected = true;
            _prefs.SaveServerIp(ip);
            _prefs.SavePairedPin(pin);
            OnConnectedChanged?.Invoke();
        }
        return ok;
    }

    public List<Stratagem> GetByCategory(string category)
        => _dataService.GetByCategory(category);

    public List<Stratagem> Search(string query)
        => _dataService.Search(query);
}
