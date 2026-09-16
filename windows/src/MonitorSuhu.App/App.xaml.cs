using System.Windows;
using Application = System.Windows.Application;
using MonitorSuhu.App.Services;
using MonitorSuhu.App.ViewModels;
using MonitorSuhu.App.Views;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Sensors;

namespace MonitorSuhu.App;

public partial class App : Application
{
    private SettingsStore? _store;
    private SensorService? _sensors;
    private HotkeyService? _hotkeys;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private TrayService? _tray;
    private OverlayViewModel? _overlayVm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _store = new SettingsStore();
        _sensors = new SensorService();
        _hotkeys = new HotkeyService();
        _overlayVm = new OverlayViewModel(_store, _sensors);

        _overlay = new OverlayWindow(_overlayVm, _store);
        _overlay.Show();
        if (!_store.Settings.OverlayVisible)
        {
            _overlay.Hide();
        }

        _tray = new TrayService(
            toggleOverlay: ToggleOverlay,
            editLayout: EditLayout,
            openSettings: OpenSettings,
            exit: Shutdown);

        _sensors.Start(_store.Settings.PollIntervalMs);
        _hotkeys.Start(_overlay, _store.Settings.ToggleHotkey, _store.Settings.EditHotkey, ToggleOverlay, EditLayout);
    }

    public void ToggleOverlay()
    {
        if (_overlay is null || _store is null) return;
        if (_overlay.IsVisible)
        {
            _overlay.Hide();
            _store.Settings.OverlayVisible = false;
        }
        else
        {
            _overlay.Show();
            _store.Settings.OverlayVisible = true;
        }
        _store.Save();
    }

    public void EditLayout()
    {
        if (_overlay is null || _store is null) return;
        _store.Settings.Locked = false;
        _store.Save();
        _overlay.ApplyLock();
        _overlay.Show();
    }

    public void OpenSettings()
    {
        if (_store is null || _sensors is null || _overlay is null) return;
        if (_settingsWindow is null)
        {
            var vm = new SettingsViewModel(_store, _overlay, _sensors);
            _settingsWindow = new SettingsWindow(vm);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _store?.Save();
        _hotkeys?.Dispose();
        _sensors?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
