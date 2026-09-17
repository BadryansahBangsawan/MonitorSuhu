using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Linux.Services;
using MonitorSuhu.Linux.ViewModels;
using MonitorSuhu.Linux.Views;

namespace MonitorSuhu.Linux;

public partial class App : Application
{
    private SettingsStore? _store;
    private HwmonSensorService? _sensors;
    private X11HotkeyService? _hotkeys;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private TrayService? _tray;
    private OverlayViewModel? _overlayVm;
    private UpdateChecker? _updates;
    private AlertPresenter? _alerts;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => OnDesktopExit();
            StartDesktop(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartDesktop(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _store = new SettingsStore();
        _sensors = new HwmonSensorService(() => _store.Settings.SensorBindings);
        _alerts = new AlertPresenter(message => _tray?.ShowWarning(message));
        _overlayVm = new OverlayViewModel(_store, _sensors);
        _overlay = new OverlayWindow(_overlayVm, _store);
        _updates = new UpdateChecker();
        _hotkeys = new X11HotkeyService();

        _sensors.Updated += snapshot =>
        {
            Dispatcher.UIThread.Post(() => ApplyTrayStatus(snapshot));
        };

        desktop.MainWindow = _overlay;
        _overlay.Show();
        if (!_store.Settings.OverlayVisible)
            _overlay.Hide();

        _tray = new TrayService(
            toggleOverlay: ToggleOverlay,
            editLayout: EditLayout,
            muteAlerts: MuteAlerts,
            openSettings: OpenSettings,
            checkUpdates: () => _ = CheckForUpdatesAsync(),
            exit: () => desktop.Shutdown());
        ApplyTrayStatus(_sensors.Snapshot);

        _sensors.Start(_store.Settings.PollIntervalMs);

        _hotkeys.Start(
            _overlay,
            _store.Settings.ToggleHotkey,
            _store.Settings.EditHotkey,
            ToggleOverlay,
            EditLayout);
        if (_hotkeys.FailedMessage is { } message)
            _tray.ShowWarning(message);

        _updates.PropertyChanged += OnUpdateCheckerPropertyChanged;
        _ = _updates.CheckAsync();
    }

    private void OnUpdateCheckerPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UpdateChecker.HasUpdate) && _updates is { HasUpdate: true })
            _tray?.ShowInfo($"MonitorSuhu {_updates.LatestVersion} is available.");
    }

    public void ToggleOverlay()
    {
        if (_overlay is null || _store is null)
            return;
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
        if (_overlay is null || _store is null)
            return;
        _store.Settings.Locked = false;
        _store.Save();
        _overlay.ApplyLock();
        _overlay.Show();
    }

    public void MuteAlerts()
    {
        if (_store is null)
            return;
        _store.Settings.MuteAlerts();
        _store.Save();
    }

    public string? RestartHotkeys()
    {
        if (_hotkeys is null || _store is null || _overlay is null) return null;
        _hotkeys.Start(
            _overlay,
            _store.Settings.ToggleHotkey,
            _store.Settings.EditHotkey,
            ToggleOverlay,
            EditLayout);
        return _hotkeys.FailedMessage;
    }

    public void OpenSettings()
    {
        if (_store is null || _sensors is null || _overlay is null || _updates is null)
            return;
        if (_settingsWindow is null)
        {
            var vm = new SettingsViewModel(
                _store,
                _overlay,
                _sensors,
                _updates,
                restartHotkeys: RestartHotkeys,
                hotkeyFailedMessage: _hotkeys?.FailedMessage);
            _settingsWindow = new SettingsWindow(vm);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updates is null)
            return;
        if (_updates.HasUpdate)
        {
            if (_updates.LatestAsset is not null)
                await _updates.ApplyAsync();
            else
                _updates.OpenDownloadPage();
            return;
        }
        await _updates.CheckAsync(userInitiated: true);
    }

    private void ApplyTrayStatus(HardwareSnapshot snapshot)
    {
        var settings = _store?.Settings;
        if (settings is null)
            return;
        double? cpu = snapshot.Readings.FirstOrDefault(r => r.Kind == SensorKind.Cpu) is { } reading
            && settings.IsKindVisible(SensorKind.Cpu)
                ? reading.Value
                : null;
        var title = settings.MenuBarTitle(cpu);
        var crit = cpu is { } c && c >= settings.ThresholdsFor(SensorKind.Cpu).Critical;
        _tray?.SetStatus(title, crit);
        _alerts?.Evaluate(snapshot.Readings, settings);
    }

    private void OnDesktopExit()
    {
        _store?.Save();
        _hotkeys?.Dispose();
        _sensors?.Dispose();
        _tray?.Dispose();
    }
}
