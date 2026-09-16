using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MonitorSuhu.App.Services;
using MonitorSuhu.App.ViewModels;
using MonitorSuhu.App.Views;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Sensors;

namespace MonitorSuhu.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private SettingsStore? _store;
    private SensorService? _sensors;
    private HotkeyService? _hotkeys;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private TrayService? _tray;
    private OverlayViewModel? _overlayVm;
    private UpdateChecker? _updates;
    private AlertGate? _alertGate;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = CreateInstanceMutex(out var created);
        if (!created)
        {
            try
            {
                if (!_mutex.WaitOne(TimeSpan.Zero))
                {
                    _mutex.Dispose();
                    _mutex = null;
                    Shutdown();
                    return;
                }
            }
            catch (AbandonedMutexException)
            {
                // previous instance crashed; we now own the mutex
            }
        }

        _store = new SettingsStore();
        _sensors = new SensorService(() => _store!.Settings.SensorBindings);
        _alertGate = new AlertGate();
        _sensors.Updated += snapshot =>
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher is not { } dispatcher) return;
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            dispatcher.BeginInvoke(() => ApplyTrayStatus(snapshot));
        };
        _hotkeys = new HotkeyService();
        _overlayVm = new OverlayViewModel(_store, _sensors);
        _updates = new UpdateChecker();

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
            checkUpdates: () => _ = CheckForUpdatesAsync(),
            exit: Shutdown);
        ApplyTrayStatus(_sensors.Snapshot);

        _sensors.Start(_store.Settings.PollIntervalMs);
        _hotkeys.Start(_store.Settings.ToggleHotkey, _store.Settings.EditHotkey, ToggleOverlay, EditLayout);
        if (_hotkeys.FailedMessage is { } message)
        {
            _tray.ShowWarning(message);
        }

        _updates.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UpdateChecker.HasUpdate) && _updates.HasUpdate)
            {
                _tray?.ShowInfo($"MonitorSuhu {_updates.LatestVersion} is available.");
            }
        };
        _ = _updates.CheckAsync();
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
        if (_store is null || _sensors is null || _overlay is null || _updates is null) return;
        if (_settingsWindow is null)
        {
            var vm = new SettingsViewModel(_store, _overlay, _sensors, _updates);
            _settingsWindow = new SettingsWindow(vm);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updates is null) return;
        if (_updates.HasUpdate)
        {
            _updates.OpenDownloadPage();
            return;
        }
        await _updates.CheckAsync(userInitiated: true);
    }

    private void ApplyTrayStatus(HardwareSnapshot snapshot)
    {
        var settings = _store?.Settings;
        if (settings is null) return;
        double? cpu = snapshot.Readings.FirstOrDefault(r => r.Kind == SensorKind.Cpu) is { } reading
            && settings.IsKindVisible(SensorKind.Cpu)
                ? reading.Value
                : null;
        var title = settings.MenuBarTitle(cpu);
        var crit = cpu is { } c && c >= settings.ThresholdsFor(SensorKind.Cpu).Critical;
        _tray?.SetStatus(title, crit);
        _alertGate?.Evaluate(snapshot.Readings, settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _store?.Save();
        _hotkeys?.Dispose();
        _sensors?.Dispose();
        _tray?.Dispose();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { /* not owned */ }
            _mutex.Dispose();
        }
        base.OnExit(e);
    }

    private static Mutex CreateInstanceMutex(out bool created)
    {
        try
        {
            return new Mutex(true, @"Global\MonitorSuhu", out created);
        }
        catch (UnauthorizedAccessException)
        {
            return new Mutex(true, @"Local\MonitorSuhu", out created);
        }
    }
}
