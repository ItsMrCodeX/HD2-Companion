using System.Text.Json;
using StratagemDeck.Mobile.Models;
using SkiaSharp;
using Svg.Skia;

namespace StratagemDeck.Mobile.Services;

public class StratagemDataService
{
    private Dictionary<string, List<Stratagem>> _byCategory = new();
    public List<string> Categories { get; private set; } = new();

    private class StratagemEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("keys")]
        public string Keys { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("shortName")]
        public string? ShortName { get; set; }
    }

    private static string CacheDir => Path.Combine(FileSystem.AppDataDirectory, "icon_cache");

    public async Task LoadAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("stratagems.json");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();

        var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, StratagemEntry>>>(json);
        if (raw == null) return;

        _byCategory.Clear();
        Categories.Clear();
        Directory.CreateDirectory(CacheDir);

        foreach (var (category, strats) in raw)
        {
            Categories.Add(category);
            var list = new List<Stratagem>();
            foreach (var (name, entry) in strats)
            {
                var strat = new Stratagem
                {
                    Name = name,
                    Category = category,
                    ShortName = entry.ShortName,
                    Keys = entry.Keys.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
                };

                strat.IconSource = await LoadIconAsync(strat);

                list.Add(strat);
            }
            _byCategory[category] = list;
        }
    }

    private async Task<ImageSource?> LoadIconAsync(Stratagem strat)
    {
        var iconName = strat.GetNormalizedFileName();
        var cacheKey = Path.GetFileNameWithoutExtension(iconName).Replace("/", "_") + ".png";
        var cachePath = Path.Combine(CacheDir, cacheKey);

        if (!File.Exists(cachePath))
        {
            try
            {
                using var iconStream = await FileSystem.OpenAppPackageFileAsync(iconName);
                var pngBytes = DecodeSvgToPng(iconStream);
                if (pngBytes != null)
                    await File.WriteAllBytesAsync(cachePath, pngBytes);
            }
            catch
            {
                return null;
            }
        }

        return File.Exists(cachePath)
            ? ImageSource.FromFile(cachePath)
            : null;
    }

    private static byte[]? DecodeSvgToPng(Stream svgStream)
    {
        using var svg = new SKSvg();
        svg.Load(svgStream);
        if (svg.Picture == null)
            return null;

        var size = svg.Picture.CullRect;
        float maxDim = Math.Max(size.Width, size.Height);
        float scale = maxDim > 0 ? 96f / maxDim : 1f;

        var bitmap = new SKBitmap(
            (int)(size.Width * scale),
            (int)(size.Height * scale));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        canvas.DrawPicture(svg.Picture);

        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    public List<Stratagem> GetByCategory(string category)
    {
        return _byCategory.TryGetValue(category, out var list) ? list : new List<Stratagem>();
    }

    public List<Stratagem> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Stratagem>();

        var lower = query.ToLowerInvariant();
        return _byCategory.Values
            .SelectMany(x => x)
            .Where(s => s.Name.Contains(lower, StringComparison.OrdinalIgnoreCase)
                     || s.DisplayName.Contains(lower, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public Stratagem? FindByName(string name, string? category = null)
    {
        if (category != null && _byCategory.TryGetValue(category, out var list))
            return list.FirstOrDefault(s => s.Name == name);
        return _byCategory.Values.SelectMany(x => x).FirstOrDefault(s => s.Name == name);
    }
}
