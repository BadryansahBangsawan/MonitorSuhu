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
    private readonly object _saveGate = new();
    private CancellationTokenSource? _debounce;

    public AppSettings Settings { get; private set; }

    public SettingsStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MonitorSuhu");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "settings.json");
        Settings = Load() ?? new AppSettings();
        Settings.Sanitize();
    }

    public void Save()
    {
        _debounce?.Cancel();
        WriteNow();
    }

    public void SaveDebounced(int milliseconds = 250)
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        var cts = new CancellationTokenSource();
        _debounce = cts;
        _ = WriteAfter(milliseconds, cts.Token);
    }

    public void Replace(AppSettings settings)
    {
        Settings = settings;
        Save();
    }

    private async Task WriteAfter(int milliseconds, CancellationToken token)
    {
        try
        {
            await Task.Delay(milliseconds, token).ConfigureAwait(false);
            WriteNow();
        }
        catch (OperationCanceledException)
        {
            // replaced by a newer debounce or an immediate Save()
        }
    }

    private void WriteNow()
    {
        lock (_saveGate)
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Settings, JsonOptions));
        }
    }

    private AppSettings? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions);
            if (settings is not null)
            {
                settings.Sanitize();
            }
            return settings;
        }
        catch
        {
            return null;
        }
    }
}
