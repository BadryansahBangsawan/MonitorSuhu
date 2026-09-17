using System.Runtime.InteropServices;
using System.Windows.Threading;
using MonitorSuhu.App.Views;
using MonitorSuhu.Core.Services;

namespace MonitorSuhu.App.Services;

public sealed class FullscreenGuard : IDisposable
{
    private readonly SettingsStore _store;
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _timer;

    public FullscreenGuard(SettingsStore store, OverlayWindow overlay)
    {
        _store = store;
        _overlay = overlay;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        _timer.Start();
        Tick();
    }

    public void Dispose()
    {
        _timer.Stop();
    }

    private void Tick()
    {
        var settings = _store.Settings;
        var fullscreen = settings.HideInFullscreen && IsExclusiveFullscreen();
        var capturing = settings.HideDuringCapture && IsScreensaverRunning();
        _overlay.ApplySuppressed(FullscreenPolicy.ShouldHide(settings, fullscreen, capturing));
    }

    private static bool IsExclusiveFullscreen()
    {
        // Borderless games cover the monitor but still allow Topmost overlays.
        // Only exclusive D3D fullscreen (QUNS_RUNNING_D3D_FULL_SCREEN) should hide.
        if (Native.SHQueryUserNotificationState(out var state) == 0)
            return state == Native.QunsRunningD3dFullScreen;
        return false;
    }

    private static bool IsScreensaverRunning()
    {
        if (!Native.SystemParametersInfo(Native.SpiGetScreensaverRunning, 0, out var running, 0))
            return false;
        return running != 0;
    }

    private static class Native
    {
        public const int QunsRunningD3dFullScreen = 3;
        public const uint SpiGetScreensaverRunning = 0x0072;

        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(out int pquns);

        [DllImport("user32.dll")]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out int pvParam, uint fWinIni);
    }
}
