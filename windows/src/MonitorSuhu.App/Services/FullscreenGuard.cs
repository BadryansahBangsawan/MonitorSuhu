using System.Runtime.InteropServices;
using System.Windows.Threading;
using MonitorSuhu.App.Views;
using MonitorSuhu.Core.Services;

namespace MonitorSuhu.App.Services;

public sealed class FullscreenGuard : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0;

    private readonly SettingsStore _store;
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _timer;
    private readonly Native.WinEventDelegate _hookProc;
    private IntPtr _hook;

    public FullscreenGuard(SettingsStore store, OverlayWindow overlay)
    {
        _store = store;
        _overlay = overlay;
        _hookProc = OnForegroundChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        _hook = Native.SetWinEventHook(
            EventSystemForeground, EventSystemForeground,
            IntPtr.Zero, _hookProc, 0, 0, WinEventOutOfContext);
        _timer.Start();
        Tick();
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        _overlay.Dispatcher.BeginInvoke(Tick);
    }

    private void Tick()
    {
        var settings = _store.Settings;
        // Games report QUNS_RUNNING_D3D_FULL_SCREEN — do not Hide() the HUD.
        // Only slideshow / presentation mode honors Hide in fullscreen.
        var fullscreen = settings.HideInFullscreen && IsPresentationMode();
        var capturing = settings.HideDuringCapture && IsScreensaverRunning();
        _overlay.ApplySuppressed(FullscreenPolicy.ShouldHide(settings, fullscreen, capturing));
    }

    private static bool IsPresentationMode()
    {
        if (Native.SHQueryUserNotificationState(out var state) != 0)
            return false;
        return state == Native.QunsPresentationMode;
    }

    private static bool IsScreensaverRunning()
    {
        if (!Native.SystemParametersInfo(Native.SpiGetScreensaverRunning, 0, out var running, 0))
            return false;
        return running != 0;
    }

    private static class Native
    {
        public const int QunsPresentationMode = 4;
        public const uint SpiGetScreensaverRunning = 0x0072;

        public delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(out int pquns);

        [DllImport("user32.dll")]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out int pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    }
}
