using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StratagemDeck.Mobile.Models;
using StratagemDeck.Mobile.Services;

namespace StratagemDeck.Mobile.ViewModels;

public class GameViewModel : INotifyPropertyChanged
{
    private readonly SessionService _session;
    private readonly StratagemSender _sender;
    private bool _isSending;
    private string _status = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand TapSlotCommand { get; }
    public ICommand SendMissionStratagemCommand { get; }

    public ObservableCollection<LoadoutSlot> Slots => _session.Slots;

    public ImageSource? Slot0Icon => Slots.Count > 0 ? Slots[0].SlotIcon : null;
    public string Slot0Name => Slots.Count > 0 ? Slots[0].SlotName : "—";
    public ImageSource? Slot1Icon => Slots.Count > 1 ? Slots[1].SlotIcon : null;
    public string Slot1Name => Slots.Count > 1 ? Slots[1].SlotName : "—";
    public ImageSource? Slot2Icon => Slots.Count > 2 ? Slots[2].SlotIcon : null;
    public string Slot2Name => Slots.Count > 2 ? Slots[2].SlotName : "—";
    public ImageSource? Slot3Icon => Slots.Count > 3 ? Slots[3].SlotIcon : null;
    public string Slot3Name => Slots.Count > 3 ? Slots[3].SlotName : "—";
    public ObservableCollection<Stratagem> MissionStrats => Slots.Count > 4 ? Slots[4].MissionStrats : new ObservableCollection<Stratagem>();

    private void NotifySlotsChanged()
    {
        OnPropertyChanged(nameof(Slot0Icon));
        OnPropertyChanged(nameof(Slot0Name));
        OnPropertyChanged(nameof(Slot1Icon));
        OnPropertyChanged(nameof(Slot1Name));
        OnPropertyChanged(nameof(Slot2Icon));
        OnPropertyChanged(nameof(Slot2Name));
        OnPropertyChanged(nameof(Slot3Icon));
        OnPropertyChanged(nameof(Slot3Name));
        OnPropertyChanged(nameof(MissionStrats));
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public bool IsConnected => _session.IsConnected;

    public GameViewModel(SessionService session, StratagemSender sender)
    {
        _session = session;
        _sender = sender;

        TapSlotCommand = new Command<string>(async (idx) => await OnTapSlot(idx));
        SendMissionStratagemCommand = new Command<Stratagem>(async (s) => await SendStratagem(s));
        _session.OnConnectedChanged += () =>
        {
            OnPropertyChanged(nameof(IsConnected));
        };
        _session.OnLoadoutChanged += () =>
        {
            NotifySlotsChanged();
        };
    }

    public Task InitializeAsync()
    {
        _session.InitializeAsync();
        Status = _session.IsConnected ? "Connected" : "Not connected";
        return Task.CompletedTask;
    }

    private async Task OnTapSlot(string? indexStr)
    {
        if (!int.TryParse(indexStr, out var idx)) return;
        if (_isSending) return;

        var slot = Slots.FirstOrDefault(s => s.SlotIndex == idx);
        if (slot?.SelectedStratagem == null) return;

        if (!_session.IsConnected || string.IsNullOrEmpty(_session.ServerIp))
        {
            Status = "Not connected";
            return;
        }

        _isSending = true;

        try
        {
            var strat = slot.SelectedStratagem;
            await SendStratagem(strat);
        }
        finally
        {
            _isSending = false;
        }
    }

    private async Task SendStratagem(Stratagem strat)
    {
        Status = $"{strat.DisplayName}";
        await _sender.SendAsync(_session.ServerIp, _session.Pin, strat);
        Status = "Sent";
        await Task.Delay(1000);
        Status = "Connected";
    }

    public void UpdateConnectionStatus()
    {
        OnPropertyChanged(nameof(IsConnected));
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
