using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorSuhu.App.Views;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Sensors;

namespace MonitorSuhu.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly OverlayWindow _overlay;
    private readonly SensorService _sensors;

    public AppSettings Settings => _store.Settings;

    public SettingsViewModel(SettingsStore store, OverlayWindow overlay, SensorService sensors)
    {
        _store = store;
        _overlay = overlay;
        _sensors = sensors;
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
        _store.Save();
        _overlay.ApplyLock();
        _overlay.Show();
    }
}
