using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Linux.Services;
using MonitorSuhu.Linux.Views;

namespace MonitorSuhu.Linux.ViewModels;

public sealed record AccentOption(string Name, string Hex);

public sealed record CatalogOption(string Id, string Label);

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly OverlayWindow _overlay;
    private readonly HwmonSensorService _sensors;
    private readonly Func<string?>? _restartHotkeys;
    private HotkeySlot _recording;

    public AppSettings Settings => _store.Settings;

    public UpdateChecker Updates { get; }

    public IReadOnlyList<AccentOption> Accents { get; } =
    [
        new("NVIDIA", AppSettings.NvidiaGreen),
        new("Cyan", "#3DDCFF"),
        new("White", "#FFFFFF"),
        new("Orange", "#FF9F0A")
    ];

    public bool IsWayland { get; } =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    public string ShortcutsCaption
    {
        get
        {
            if (IsRecording) return "Press a shortcut with a modifier. Esc cancels.";
            if (!string.IsNullOrEmpty(HotkeyConflict)) return HotkeyConflict!;
            if (!string.IsNullOrEmpty(HotkeyFailedMessage)) return HotkeyFailedMessage!;
            if (IsWayland)
                return "Global shortcuts are unavailable on Wayland — use the tray menu. The shortcut is saved for X11.";
            return "Click a shortcut to rebind. At least one modifier plus a letter or number.";
        }
    }

    public string LockCaption =>
        IsWayland
            ? "Lock on Wayland keeps the HUD from moving; clicks may still hit the overlay."
            : "When locked, clicks pass through the HUD.";

    public string? SensorError => _sensors.LastError;

    public bool HasSensorError => !string.IsNullOrWhiteSpace(_sensors.LastError);

    public string ToggleHotkeyDisplay =>
        _recording == HotkeySlot.Toggle ? "Press a shortcut…" : Settings.ToggleHotkey.Display;

    public string EditHotkeyDisplay =>
        _recording == HotkeySlot.Edit ? "Press a shortcut…" : Settings.EditHotkey.Display;

    public bool IsRecording => _recording != HotkeySlot.None;

    public string? HotkeyConflict { get; private set; }

    public string? HotkeyFailedMessage { get; private set; }

    public SettingsViewModel(
        SettingsStore store,
        OverlayWindow overlay,
        HwmonSensorService sensors,
        UpdateChecker updates,
        Func<string?>? restartHotkeys = null,
        string? hotkeyFailedMessage = null)
    {
        _store = store;
        _overlay = overlay;
        _sensors = sensors;
        Updates = updates;
        _restartHotkeys = restartHotkeys;
        HotkeyFailedMessage = hotkeyFailedMessage;
    }

    [RelayCommand]
    private Task CheckUpdates() => Updates.CheckAsync(userInitiated: true);

    [RelayCommand]
    private void OpenUpdate()
    {
        if (Updates.LatestAsset is not null)
        {
            _ = Updates.ApplyAsync();
            return;
        }
        Updates.OpenDownloadPage();
    }

    [RelayCommand]
    private Task ApplyUpdate() => Updates.ApplyAsync();

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
            var exe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            AutostartService.Apply(value, exe);
            _store.SaveDebounced();
        }
    }

    public bool ShowCpu { get => Settings.ShowCpu; set => SetFlag(v => Settings.ShowCpu = v, Settings.ShowCpu, value); }
    public bool ShowGpu { get => Settings.ShowGpu; set => SetFlag(v => Settings.ShowGpu = v, Settings.ShowGpu, value); }
    public bool ShowSsd { get => Settings.ShowSsd; set => SetFlag(v => Settings.ShowSsd = v, Settings.ShowSsd, value); }
    public bool ShowBoard { get => Settings.ShowBoard; set => SetFlag(v => Settings.ShowBoard = v, Settings.ShowBoard, value); }
    public bool ShowRam { get => Settings.ShowRam; set => SetFlag(v => Settings.ShowRam = v, Settings.ShowRam, value); }
    public bool ShowFan { get => Settings.ShowFan; set => SetFlag(v => Settings.ShowFan = v, Settings.ShowFan, value); }
    public bool ShowCpuLoad { get => Settings.ShowCpuLoad; set => SetFlag(v => Settings.ShowCpuLoad = v, Settings.ShowCpuLoad, value); }
    public bool ShowGpuLoad { get => Settings.ShowGpuLoad; set => SetFlag(v => Settings.ShowGpuLoad = v, Settings.ShowGpuLoad, value); }
    public bool ShowPower { get => Settings.ShowPower; set => SetFlag(v => Settings.ShowPower = v, Settings.ShowPower, value); }

    public bool AlertsEnabled
    {
        get => Settings.AlertsEnabled;
        set
        {
            if (Settings.AlertsEnabled == value) return;
            Settings.AlertsEnabled = value;
            OnPropertyChanged();
            _store.SaveDebounced();
        }
    }

    public string MuteCaption => Settings.MuteCaption() ?? "Critical alerts change the tray tooltip and send a desktop notification if notify-send is installed. Mute lasts 15 minutes.";

    [RelayCommand]
    private void MuteAlerts()
    {
        Settings.MuteAlerts();
        OnPropertyChanged(nameof(MuteCaption));
        _store.SaveDebounced();
    }

    [RelayCommand]
    private void RecordToggle() => ToggleRecording(HotkeySlot.Toggle);

    [RelayCommand]
    private void RecordEdit() => ToggleRecording(HotkeySlot.Edit);

    public bool HandleRecordKey(uint virtualKey, bool control, bool shift, bool alt, bool win, bool escape)
    {
        if (!IsRecording) return false;
        if (escape)
        {
            _recording = HotkeySlot.None;
            NotifyHotkeys();
            return true;
        }

        var chord = KeyChord.TryCreate(virtualKey, control, shift, alt, win);
        if (chord is null) return true;

        var target = _recording;
        _recording = HotkeySlot.None;
        var other = target == HotkeySlot.Toggle ? Settings.EditHotkey : Settings.ToggleHotkey;
        if (chord.Equals(other))
        {
            HotkeyConflict = "That shortcut is already used by the other action.";
            NotifyHotkeys();
            return true;
        }

        HotkeyConflict = null;
        if (target == HotkeySlot.Toggle) Settings.ToggleHotkey = chord;
        else Settings.EditHotkey = chord;
        _store.Save();
        HotkeyFailedMessage = _restartHotkeys?.Invoke();
        NotifyHotkeys();
        return true;
    }

    private void ToggleRecording(HotkeySlot slot)
    {
        if (_recording == slot)
        {
            _recording = HotkeySlot.None;
        }
        else
        {
            HotkeyConflict = null;
            _recording = slot;
        }
        NotifyHotkeys();
    }

    private void NotifyHotkeys()
    {
        OnPropertyChanged(nameof(ToggleHotkeyDisplay));
        OnPropertyChanged(nameof(EditHotkeyDisplay));
        OnPropertyChanged(nameof(ShortcutsCaption));
        OnPropertyChanged(nameof(IsRecording));
    }

    private enum HotkeySlot { None, Toggle, Edit }

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
    public double CpuLoadWarn { get => Warn(SensorKind.CpuLoad); set => SetWarn(SensorKind.CpuLoad, value); }
    public double CpuLoadCrit { get => Crit(SensorKind.CpuLoad); set => SetCrit(SensorKind.CpuLoad, value); }
    public double GpuLoadWarn { get => Warn(SensorKind.GpuLoad); set => SetWarn(SensorKind.GpuLoad, value); }
    public double GpuLoadCrit { get => Crit(SensorKind.GpuLoad); set => SetCrit(SensorKind.GpuLoad, value); }
    public double PowerWarn { get => Warn(SensorKind.Power); set => SetWarn(SensorKind.Power, value); }
    public double PowerCrit { get => Crit(SensorKind.Power); set => SetCrit(SensorKind.Power, value); }

    public IReadOnlyList<CatalogOption> ThermalOptions { get; private set; } = [new("", "Auto")];
    public IReadOnlyList<CatalogOption> FanOptions { get; private set; } = [new("", "Auto")];
    public IReadOnlyList<CatalogOption> LoadOptions { get; private set; } = [new("", "Auto")];
    public IReadOnlyList<CatalogOption> PowerOptions { get; private set; } = [new("", "Auto")];

    public string CpuBinding { get => GetBinding("Cpu"); set => SetBinding("Cpu", value); }
    public string GpuBinding { get => GetBinding("Gpu"); set => SetBinding("Gpu", value); }
    public string SsdBinding { get => GetBinding("Ssd"); set => SetBinding("Ssd", value); }
    public string BoardBinding { get => GetBinding("Board"); set => SetBinding("Board", value); }
    public string RamBinding { get => GetBinding("Ram"); set => SetBinding("Ram", value); }
    public string FanBinding { get => GetBinding("Fan"); set => SetBinding("Fan", value); }
    public string CpuLoadBinding { get => GetBinding("CpuLoad"); set => SetBinding("CpuLoad", value); }
    public string GpuLoadBinding { get => GetBinding("GpuLoad"); set => SetBinding("GpuLoad", value); }
    public string PowerBinding { get => GetBinding("Power"); set => SetBinding("Power", value); }

    public void RefreshCatalog()
    {
        var catalog = _sensors.LastCatalog ?? [];
        CatalogOption auto = new("", "Auto");
        ThermalOptions =
        [
            auto,
            .. catalog.Where(e => e.Hint == CatalogHint.Temp).Select(e => new CatalogOption(e.Id, e.Name))
        ];
        FanOptions =
        [
            auto,
            .. catalog.Where(e => e.Hint == CatalogHint.Fan).Select(e => new CatalogOption(e.Id, e.Name))
        ];
        LoadOptions =
        [
            auto,
            .. catalog.Where(e => e.Hint == CatalogHint.Load).Select(e => new CatalogOption(e.Id, e.Name))
        ];
        PowerOptions =
        [
            auto,
            .. catalog.Where(e => e.Hint == CatalogHint.Power).Select(e => new CatalogOption(e.Id, e.Name))
        ];
        OnPropertyChanged(nameof(ThermalOptions));
        OnPropertyChanged(nameof(FanOptions));
        OnPropertyChanged(nameof(LoadOptions));
        OnPropertyChanged(nameof(PowerOptions));
        OnPropertyChanged(nameof(CpuBinding));
        OnPropertyChanged(nameof(GpuBinding));
        OnPropertyChanged(nameof(SsdBinding));
        OnPropertyChanged(nameof(BoardBinding));
        OnPropertyChanged(nameof(RamBinding));
        OnPropertyChanged(nameof(FanBinding));
        OnPropertyChanged(nameof(CpuLoadBinding));
        OnPropertyChanged(nameof(GpuLoadBinding));
        OnPropertyChanged(nameof(PowerBinding));
        OnPropertyChanged(nameof(SensorError));
        OnPropertyChanged(nameof(HasSensorError));
    }

    private string GetBinding(string key)
    {
        var bindings = Settings.SensorBindings ??= new();
        return bindings.TryGetValue(key, out var id) ? id ?? "" : "";
    }

    private void SetBinding(string key, string? value, [CallerMemberName] string? propertyName = null)
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

    private void SetFlag(Action<bool> assign, bool current, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (current == value) return;
        assign(value);
        OnPropertyChanged(propertyName);
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
