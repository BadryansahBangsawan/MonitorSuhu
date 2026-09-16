using System.IO;
using System.Text.Json;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Core.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    public AppSettings Settings { get; private set; }

    public SettingsStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MonitorSuhu");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "settings.json");
        Settings = Load() ?? new AppSettings();
    }

    public void Save()
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(Settings, JsonOptions));
    }

    public void Replace(AppSettings settings)
    {
        Settings = settings;
        Save();
    }

    private AppSettings? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
