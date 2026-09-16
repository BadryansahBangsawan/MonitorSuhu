using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorSuhu.App.Services;
using MonitorSuhu.App.Views;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Sensors;

namespace MonitorSuhu.App.ViewModels;

public sealed record AccentOption(string Name, string Hex);

public sealed record CatalogOption(string Id, string Label);

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly OverlayWindow _overlay;
    private readonly SensorService _sensors;

    public AppSettings Settings => _store.Settings;

    public UpdateChecker Updates { get; }

    public IReadOnlyList<AccentOption> Accents { get; } =
    [
        new("NVIDIA", AppSettings.NvidiaGreen),
        new("Cyan", "#3DDCFF"),
        new("White", "#FFFFFF"),
        new("Orange", "#FF9F0A")
    ];

    public string? SensorError => _sensors.LastError;

    public bool HasSensorError => !string.IsNullOrWhiteSpace(_sensors.LastError);

    public string ToggleHotkeyDisplay => Settings.ToggleHotkey.Display;

    public string EditHotkeyDisplay => Settings.EditHotkey.Display;

    public SettingsViewModel(SettingsStore store, OverlayWindow overlay, SensorService sensors, UpdateChecker updates)
    {
        _store = store;
        _overlay = overlay;
        _sensors = sensors;
        Updates = updates;
    }

    [RelayCommand]
    private Task CheckUpdates() => Updates.CheckAsync(userInitiated: true);

    [RelayCommand]
    private void OpenUpdate() => Updates.OpenDownloadPage();

    public bool OverlayVisible
    {
        get => Settings.OverlayVisible;
        set
        {
            if (Settings.OverlayVisible == value) return;
            Settings.OverlayVisible = value;
            OnPropertyChanged();
            if (value) _overlay.Show(); else _overlay.Hide();
            _store.SaveDebounced();
        }
    }

    public bool Locked
    {
        get => Settings.Locked;
        set
        {
            if (Settings.Locked == value) return;
            Settings.Locked = value;
            OnPropertyChanged();
            _overlay.ApplyLock();
            _store.SaveDebounced();
        }
    }

    public bool StartWithWindows
    {
        get => Settings.StartWithWindows;
        set
        {
            if (Settings.StartWithWindows == value) return;
            Settings.StartWithWindows = value;
            OnPropertyChanged();
            _store.SaveDebounced();
        }
    }

    public bool ShowCpu { get => Settings.ShowCpu; set => SetFlag(v => Settings.ShowCpu = v, Settings.ShowCpu, value); }
    public bool ShowGpu { get => Settings.ShowGpu; set => SetFlag(v => Settings.ShowGpu = v, Settings.ShowGpu, value); }
    public bool ShowSsd { get => Settings.ShowSsd; set => SetFlag(v => Settings.ShowSsd = v, Settings.ShowSsd, value); }
    public bool ShowBoard { get => Settings.ShowBoard; set => SetFlag(v => Settings.ShowBoard = v, Settings.ShowBoard, value); }
    public bool ShowRam { get => Settings.ShowRam; set => SetFlag(v => Settings.ShowRam = v, Settings.ShowRam, value); }
    public bool ShowFan { get => Settings.ShowFan; set => SetFlag(v => Settings.ShowFan = v, Settings.ShowFan, value); }


    public bool UseFahrenheit
    {
        get => Settings.UseFahrenheit;
        set
        {
            if (Settings.UseFahrenheit == value) return;
            Settings.UseFahrenheit = value;
            OnPropertyChanged();
            _overlay.ApplyTheme();
            _store.SaveDebounced();
        }
    }

    public bool CompactHud
    {
        get => Settings.CompactHud;
        set
        {
            if (Settings.CompactHud == value) return;
            Settings.CompactHud = value;
            OnPropertyChanged();
            _overlay.ApplyTheme();
            _store.SaveDebounced();
        }
    }

    public bool ShowSparkline
    {
        get => Settings.ShowSparkline;
        set
        {
            if (Settings.ShowSparkline == value) return;
            Settings.ShowSparkline = value;
            OnPropertyChanged();
            _overlay.ApplyTheme();
            _store.SaveDebounced();
        }
    }

    public double OverlayOpacity
    {
        get => Settings.OverlayOpacity;
        set
        {
            if (Math.Abs(Settings.OverlayOpacity - value) < 0.0005) return;
            Settings.OverlayOpacity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OpacityLabel));
            _overlay.ApplyTheme();
            _store.SaveDebounced();
        }
    }

    public double FontSize
    {
        get => Settings.FontSize;
        set
        {
            if (Math.Abs(Settings.FontSize - value) < 0.05) return;
            Settings.FontSize = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FontSizeLabel));
            _overlay.ApplyTheme();
            _store.SaveDebounced();
        }
    }

    public string AccentHex
    {
        get => Settings.AccentHex;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || Settings.AccentHex == value) return;
            Settings.AccentHex = value;
            OnPropertyChanged();
            _overlay.ApplyTheme();
            _store.SaveDebounced();
        }
    }

    public int PollIntervalMs
    {
        get => Settings.PollIntervalMs;
        set
        {
            var clamped = Math.Clamp(value, 400, 3000);
            if (Settings.PollIntervalMs == clamped) return;
            Settings.PollIntervalMs = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PollIntervalLabel));
            _sensors.Start(clamped);
            _store.SaveDebounced();
        }
    }

    public string OpacityLabel => $"{(int)Math.Round(Settings.OverlayOpacity * 100)}%";

    public string FontSizeLabel => $"{(int)Math.Round(Settings.FontSize)} pt";

    public string PollIntervalLabel => $"{Settings.PollIntervalMs} ms";

    public double CpuWarn { get => Warn(SensorKind.Cpu); set => SetWarn(SensorKind.Cpu, value); }
    public double CpuCrit { get => Crit(SensorKind.Cpu); set => SetCrit(SensorKind.Cpu, value); }
    public double GpuWarn { get => Warn(SensorKind.Gpu); set => SetWarn(SensorKind.Gpu, value); }
    public double GpuCrit { get => Crit(SensorKind.Gpu); set => SetCrit(SensorKind.Gpu, value); }
    public double SsdWarn { get => Warn(SensorKind.Ssd); set => SetWarn(SensorKind.Ssd, value); }
    public double SsdCrit { get => Crit(SensorKind.Ssd); set => SetCrit(SensorKind.Ssd, value); }
    public double BoardWarn { get => Warn(SensorKind.Board); set => SetWarn(SensorKind.Board, value); }
    public double BoardCrit { get => Crit(SensorKind.Board); set => SetCrit(SensorKind.Board, value); }
    public double RamWarn { get => Warn(SensorKind.Ram); set => SetWarn(SensorKind.Ram, value); }
    public double RamCrit { get => Crit(SensorKind.Ram); set => SetCrit(SensorKind.Ram, value); }
    public double FanWarn { get => Warn(SensorKind.Fan); set => SetWarn(SensorKind.Fan, value); }
    public double FanCrit { get => Crit(SensorKind.Fan); set => SetCrit(SensorKind.Fan, value); }

    public IReadOnlyList<CatalogOption> ThermalOptions { get; private set; } = [new("", "Auto")];
    public IReadOnlyList<CatalogOption> FanOptions { get; private set; } = [new("", "Auto")];

    public string CpuBinding { get => GetBinding("Cpu"); set => SetBinding("Cpu", value); }
    public string GpuBinding { get => GetBinding("Gpu"); set => SetBinding("Gpu", value); }
    public string SsdBinding { get => GetBinding("Ssd"); set => SetBinding("Ssd", value); }
    public string BoardBinding { get => GetBinding("Board"); set => SetBinding("Board", value); }
    public string RamBinding { get => GetBinding("Ram"); set => SetBinding("Ram", value); }
    public string FanBinding { get => GetBinding("Fan"); set => SetBinding("Fan", value); }

    public void RefreshCatalog()
    {
        var catalog = _sensors.LastCatalog ?? [];
        CatalogOption auto = new("", "Auto");
        ThermalOptions =
        [
            auto,
            .. catalog.Where(e => !e.IsFan).Select(e => new CatalogOption(e.Id, e.Name))
        ];
        FanOptions =
        [
            auto,
            .. catalog.Where(e => e.IsFan).Select(e => new CatalogOption(e.Id, e.Name))
        ];
        OnPropertyChanged(nameof(ThermalOptions));
        OnPropertyChanged(nameof(FanOptions));
        OnPropertyChanged(nameof(CpuBinding));
        OnPropertyChanged(nameof(GpuBinding));
        OnPropertyChanged(nameof(SsdBinding));
        OnPropertyChanged(nameof(BoardBinding));
        OnPropertyChanged(nameof(RamBinding));
        OnPropertyChanged(nameof(FanBinding));
    }

    private string GetBinding(string key)
    {
        var bindings = Settings.SensorBindings ??= new();
        return bindings.TryGetValue(key, out var id) ? id ?? "" : "";
    }

    private void SetBinding(string key, string? value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (value is null) return;
        var bindings = Settings.SensorBindings ??= new();
        var current = bindings.TryGetValue(key, out var existing) ? existing ?? "" : "";
        if (current == value) return;
        if (string.IsNullOrEmpty(value)) bindings.Remove(key);
        else bindings[key] = value;
        OnPropertyChanged(propertyName);
        _store.SaveDebounced();
    }


    [RelayCommand]
    private void Save()
    {
        _store.Save();
        _sensors.Start(Settings.PollIntervalMs);
        _overlay.ApplyLock();
        _overlay.ApplyTheme();
        if (Settings.OverlayVisible) _overlay.Show(); else _overlay.Hide();
        var exe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        AutostartService.Apply(Settings.StartWithWindows, exe);
    }

    [RelayCommand]
    private void TopLeft() => _overlay.ApplyPreset(CornerPreset.TopLeft);

    [RelayCommand]
    private void TopRight() => _overlay.ApplyPreset(CornerPreset.TopRight);

    [RelayCommand]
    private void BottomLeft() => _overlay.ApplyPreset(CornerPreset.BottomLeft);

    [RelayCommand]
    private void BottomRight() => _overlay.ApplyPreset(CornerPreset.BottomRight);

    [RelayCommand]
    private void EditOverlay()
    {
        Settings.Locked = false;
        OnPropertyChanged(nameof(Locked));
        _overlay.ApplyLock();
        _overlay.Show();
        _store.SaveDebounced();
    }

    private void SetFlag(Action<bool> assign, bool current, bool value)
    {
        if (current == value) return;
        assign(value);
        OnPropertyChanged();
        _overlay.ApplyTheme();
        _store.SaveDebounced();
    }

    private double Warn(SensorKind kind) => Settings.ThresholdsFor(kind).Warn;

    private double Crit(SensorKind kind) => Settings.ThresholdsFor(kind).Critical;

    private void SetWarn(SensorKind kind, double value)
    {
        var t = Settings.ThresholdsFor(kind);
        if (Math.Abs(t.Warn - value) < 0.05) return;
        Settings.SetThresholds(kind, value, t.Critical);
        _overlay.ApplyTheme();
        _store.SaveDebounced();
    }

    private void SetCrit(SensorKind kind, double value)
    {
        var t = Settings.ThresholdsFor(kind);
        if (Math.Abs(t.Critical - value) < 0.05) return;
        Settings.SetThresholds(kind, t.Warn, value);
        _overlay.ApplyTheme();
        _store.SaveDebounced();
    }
}
