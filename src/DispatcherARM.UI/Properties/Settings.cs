using System.IO;
using System.Text.Json;

namespace DispatcherARM.UI.Properties;

/// <summary>
/// Простое key-value хранилище пользовательских настроек (JSON-файл рядом с exe)
/// </summary>
public sealed class Settings
{
    private static Settings? _instance;
    public static Settings Default => _instance ??= Load();

    private static readonly string FilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "user_settings.json");

    private Dictionary<string, string> _data = new();

    public string OneCExchangeFolder
    {
        get => _data.TryGetValue(nameof(OneCExchangeFolder), out var v) ? v : "";
        set => _data[nameof(OneCExchangeFolder)] = value;
    }

    private static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var d    = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return new Settings { _data = d ?? new() };
            }
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
