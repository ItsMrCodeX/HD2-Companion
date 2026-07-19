using System.Text.Json;
using StratagemDeck.Mobile.Models;

namespace StratagemDeck.Mobile.Services;

public class PreferencesService
{
    private const string LoadoutKey = "saved_loadout";

    private readonly object _lock = new();
    private List<LoadoutSlot>? _pendingSlots;
    private CancellationTokenSource? _debounceCts;

    public void SaveLoadout(List<LoadoutSlot> slots)
    {
        lock (_lock) { _pendingSlots = slots; }

        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token);
                if (token.IsCancellationRequested) return;

                List<LoadoutSlot> toSave;
                lock (_lock)
                {
                    toSave = _pendingSlots!;
                    _pendingSlots = null;
                }

                var data = new LoadoutConfig
                {
                    Slots = toSave.Select(s => new LoadoutSlotData
                    {
                        SlotIndex = s.SlotIndex,
                        StratagemName = s.SelectedStratagem?.Name,
                        Category = s.SelectedStratagem?.Category,
                        MissionStratagemNames = s.MissionStrats.Select(m => m.Name).ToList(),
                        MissionStratagemCategories = s.MissionStrats.Select(m => m.Category).ToList()
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(data);
                Preferences.Default.Set(LoadoutKey, json);
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    public List<LoadoutSlot> LoadLoadout(IEnumerable<Stratagem> allStrats)
    {
        var all = allStrats.ToList();
        var json = Preferences.Default.Get(LoadoutKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return CreateDefaultSlots();

        try
        {
            var config = JsonSerializer.Deserialize<LoadoutConfig>(json);
            if (config == null || config.Slots.Count == 0)
                return CreateDefaultSlots();

            var slots = new List<LoadoutSlot>();
            foreach (var s in config.Slots)
            {
                var slot = new LoadoutSlot
                {
                    SlotIndex = s.SlotIndex,
                    Label = GetSlotLabel(s.SlotIndex)
                };

                if (s.StratagemName != null)
                {
                    slot.SelectedStratagem = all.FirstOrDefault(
                        x => x.Name == s.StratagemName && x.Category == s.Category);
                }

                if (s.MissionStratagemNames.Count > 0)
                {
                    for (int i = 0; i < s.MissionStratagemNames.Count; i++)
                    {
                        var cat = i < s.MissionStratagemCategories.Count
                            ? s.MissionStratagemCategories[i] : null;
                        var found = all.FirstOrDefault(
                            x => x.Name == s.MissionStratagemNames[i] && x.Category == cat);
                        if (found != null)
                            slot.MissionStrats.Add(found);
                    }
                }

                slots.Add(slot);
            }
            return slots;
        }
        catch
        {
            return CreateDefaultSlots();
        }
    }

    public string? GetLastPairedPin()
    {
        return Preferences.Default.Get("paired_pin", (string?)null);
    }

    public void SavePairedPin(string pin)
    {
        Preferences.Default.Set("paired_pin", pin);
    }

    public string? GetLastServerIp()
    {
        return Preferences.Default.Get("server_ip", (string?)null);
    }

    public void SaveServerIp(string ip)
    {
        Preferences.Default.Set("server_ip", ip);
    }

    private static List<LoadoutSlot> CreateDefaultSlots()
    {
        return new List<LoadoutSlot>
        {
            new() { SlotIndex = 0, Label = "Slot 1" },
            new() { SlotIndex = 1, Label = "Slot 2" },
            new() { SlotIndex = 2, Label = "Slot 3" },
            new() { SlotIndex = 3, Label = "Slot 4" },
            new() { SlotIndex = 4, Label = "Mission" },
        };
    }

    private static string GetSlotLabel(int index) => index switch
    {
        0 => "Slot 1",
        1 => "Slot 2",
        2 => "Slot 3",
        3 => "Slot 4",
        4 => "Mission",
        _ => $"Slot {index}"
    };
}
