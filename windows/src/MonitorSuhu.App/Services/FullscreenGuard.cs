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
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        if (hwnd == Native.GetShellWindow() || hwnd == Native.GetDesktopWindow()) return false;

        Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == (uint)Environment.ProcessId) return false;

        if (!Native.GetWindowRect(hwnd, out var rect)) return false;

        var monitor = Native.MonitorFromWindow(hwnd, Native.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return false;

        var info = new Native.MonitorInfo { CbSize = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(monitor, ref info)) return false;

        return FullscreenPolicy.RectCoversMonitor(
            rect.Left, rect.Top, rect.Right, rect.Bottom,
            info.RcMonitor.Left, info.RcMonitor.Top, info.RcMonitor.Right, info.RcMonitor.Bottom);
    }

    private static bool IsScreensaverRunning()
    {
        if (!Native.SystemParametersInfo(Native.SpiGetScreensaverRunning, 0, out var running, 0))
            return false;
        return running != 0;
    }

    private static class Native
    {
        public const uint MonitorDefaultToNearest = 2;
        public const uint SpiGetScreensaverRunning = 0x0072;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MonitorInfo
        {
            public int CbSize;
            public Rect RcMonitor;
            public Rect RcWork;
            public uint DwFlags;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out int pvParam, uint fWinIni);
    }
}
