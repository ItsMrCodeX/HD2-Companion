using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace StratagemDeck.Mobile.Models;

public class Stratagem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("keys")]
    public List<string> Keys { get; set; } = new();

    [JsonPropertyName("shortName")]
    public string? ShortName { get; set; }

    [JsonIgnore]
    public string DisplayName => ShortName ?? Name;

    [JsonIgnore]
    public ImageSource? IconSource { get; set; }

    public string GetNormalizedFileName()
    {
        var n = Name
            .Replace(" ", "_")
            .Replace("/", "_")
            .Replace("\"", "")
            .Replace("-", "_")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(".", "_") + "_Icon";

        while (n.Contains("__"))
            n = n.Replace("__", "_");

        return "icons/" + n.Trim('_').ToLowerInvariant() + ".svg";
    }
}

public class DiscoveryInfo
{
    public string PcName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
}

public class LoadoutSlot : System.ComponentModel.INotifyPropertyChanged
{
    private int _slotIndex;
    private string _label = string.Empty;
    private Stratagem? _selectedStratagem;
    private ObservableCollection<Stratagem> _missionStrats = new();

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public int SlotIndex
    {
        get => _slotIndex;
        set { _slotIndex = value; Notify(nameof(SlotIndex)); }
    }

    public string Label
    {
        get => _label;
        set { _label = value; Notify(nameof(Label)); }
    }

    public Stratagem? SelectedStratagem
    {
        get => _selectedStratagem;
        set
        {
            _selectedStratagem = value;
            Notify(nameof(SelectedStratagem));
            Notify(nameof(SlotIcon));
            Notify(nameof(SlotName));
        }
    }

    [JsonIgnore]
    public ImageSource? SlotIcon => _selectedStratagem?.IconSource;

    [JsonIgnore]
    public string SlotName => _selectedStratagem?.DisplayName ?? string.Empty;

    public ObservableCollection<Stratagem> MissionStrats
    {
        get => _missionStrats;
        set { _missionStrats = value; Notify(nameof(MissionStrats)); }
    }

    private void Notify(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

public class LoadoutConfig
{
    public List<LoadoutSlotData> Slots { get; set; } = new();
}

public class LoadoutSlotData
{
    public int SlotIndex { get; set; }
    public string? StratagemName { get; set; }
    public string? Category { get; set; }
    public List<string> MissionStratagemNames { get; set; } = new();
    public List<string> MissionStratagemCategories { get; set; } = new();
}
