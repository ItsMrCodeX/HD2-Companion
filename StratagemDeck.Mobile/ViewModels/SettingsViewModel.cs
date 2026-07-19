using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StratagemDeck.Mobile.Models;
using StratagemDeck.Mobile.Services;

namespace StratagemDeck.Mobile.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SessionService _session;
    private readonly UdpDiscoveryService _discovery;

    private string _status = string.Empty;
    private string _pinEntry = string.Empty;
    private string _manualIp = string.Empty;
    private bool _isScanning;
    private bool _showManualEntry;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DiscoveryInfo> DiscoveredServers { get; } = new();

    public ICommand ConnectToServerCommand { get; }
    public ICommand ConnectManualCommand { get; }
    public ICommand StartScanCommand { get; }
    public ICommand ToggleManualEntryCommand { get; }
    public ICommand OpenQrScannerCommand { get; }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string PinEntry
    {
        get => _pinEntry;
        set { _pinEntry = value; OnPropertyChanged(); }
    }

    public string ManualIp
    {
        get => _manualIp;
        set { _manualIp = value; OnPropertyChanged(); }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); }
    }

    public bool ShowManualEntry
    {
        get => _showManualEntry;
        set { _showManualEntry = value; OnPropertyChanged(); }
    }

    public bool IsConnected => _session.IsConnected;
    public string ConnectedServer => _session.IsConnected ? _session.ServerIp : "Not connected";
    public string CurrentPin => _session.Pin;

    public SettingsViewModel(SessionService session, UdpDiscoveryService discovery)
    {
        _session = session;
        _discovery = discovery;

        _discovery.OnServerDiscovered += OnServerDiscovered;
        _discovery.OnLog += msg => MainThread.BeginInvokeOnMainThread(() => Status = msg);

        ConnectToServerCommand = new Command<DiscoveryInfo>(async (d) => await ConnectToServer(d));
        ConnectManualCommand = new Command(async () => await ConnectManual());
        StartScanCommand = new Command(StartScan);
        ToggleManualEntryCommand = new Command(() =>
        {
            ShowManualEntry = !ShowManualEntry;
            if (ShowManualEntry)
            {
                _discovery.StopScanning();
                IsScanning = false;
            }
        });
        OpenQrScannerCommand = new Command(async () => await OpenQrScanner());
    }

    public Task InitializeAsync()
    {
        StartScan();
        return Task.CompletedTask;
    }

    public void StartScan()
    {
        IsScanning = true;
        ShowManualEntry = false;
        Status = "Scanning...";
        DiscoveredServers.Clear();
        _discovery.StartScanning();
    }

    private async Task ConnectToServer(DiscoveryInfo server)
    {
        _discovery.StopScanning();
        IsScanning = false;
        Status = "Connecting...";

        var ok = await _session.ConnectAsync(server.IpAddress, server.Pin);
        Status = ok
            ? "Connected"
            : "Failed";
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(ConnectedServer));
        OnPropertyChanged(nameof(CurrentPin));
    }

    private async Task OpenQrScanner()
    {
        await Shell.Current.GoToAsync("qrscan");
    }

    public async Task HandleQrScanResult(string ip, string pin)
    {
        if (Shell.Current.CurrentPage is Pages.QrScanPage)
            await Shell.Current.GoToAsync("..");

        Status = "Connecting...";
        var ok = await _session.ConnectAsync(ip, pin);
        Status = ok ? "Connected" : "Failed";
        ManualIp = ip;
        PinEntry = pin;
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(ConnectedServer));
        OnPropertyChanged(nameof(CurrentPin));
    }
    private async Task ConnectManual()
    {
        if (string.IsNullOrWhiteSpace(ManualIp) || string.IsNullOrWhiteSpace(PinEntry))
        {
            Status = "Enter IP and PIN";
            return;
        }

        Status = "Connecting...";
        var ok = await _session.ConnectAsync(ManualIp, PinEntry);
        Status = ok
            ? "Connected"
            : "Failed - check IP/PIN";
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(ConnectedServer));
        OnPropertyChanged(nameof(CurrentPin));
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
