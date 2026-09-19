using System.Runtime.InteropServices;
using Avalonia.Threading;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Linux.Views;

namespace MonitorSuhu.Linux.Services;

public sealed class FullscreenGuard : IDisposable
{
    private readonly SettingsStore _store;
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _timer;
    private readonly bool _wayland =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    private IntPtr _display;

    public FullscreenGuard(SettingsStore store, OverlayWindow overlay)
    {
        _store = store;
        _overlay = overlay;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        if (!_wayland && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
        {
            try
            {
                _display = Native.XOpenDisplay(null);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        _timer.Start();
        Tick();
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_display != IntPtr.Zero)
        {
            Native.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }

    private void Tick()
    {
        var settings = _store.Settings;
        var fullscreen = settings.HideInFullscreen && IsX11Fullscreen();
        _overlay.ApplySuppressed(FullscreenPolicy.ShouldHide(settings, fullscreen, capturing: false));
    }

    private bool IsX11Fullscreen()
    {
        if (_wayland || _display == IntPtr.Zero) return false;

        try
        {
            var overlayHandle = OverlayXid();
            var root = Native.XDefaultRootWindow(_display);
            var activeAtom = Native.XInternAtom(_display, "_NET_ACTIVE_WINDOW", false);
            if (activeAtom == IntPtr.Zero) return false;

            if (!ReadProperty(_display, root, activeAtom, (IntPtr)Native.XaWindow, 1, out var activeRaw, out var nitems)
                || nitems == 0)
            {
                return false;
            }

            var active = Marshal.ReadIntPtr(activeRaw);
            Native.XFree(activeRaw);
            if (active == IntPtr.Zero || active == overlayHandle) return false;

            var stateAtom = Native.XInternAtom(_display, "_NET_WM_STATE", false);
            var fullscreenAtom = Native.XInternAtom(_display, "_NET_WM_STATE_FULLSCREEN", false);
            if (stateAtom == IntPtr.Zero || fullscreenAtom == IntPtr.Zero) return false;

            if (!ReadProperty(_display, active, stateAtom, (IntPtr)Native.XaAtom, 64, out var stateRaw, out var count))
                return false;

            try
            {
                for (nuint i = 0; i < count; i++)
                {
                    var atom = Marshal.ReadIntPtr(stateRaw, (int)i * IntPtr.Size);
                    if (atom == fullscreenAtom) return true;
                }
            }
            finally
            {
                Native.XFree(stateRaw);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private IntPtr OverlayXid()
    {
        try
        {
            var handle = _overlay.TryGetPlatformHandle();
            if (handle is null) return IntPtr.Zero;
            var desc = handle.HandleDescriptor ?? "";
            if (!desc.Equals("X11", StringComparison.OrdinalIgnoreCase)
                && !desc.Equals("XID", StringComparison.OrdinalIgnoreCase))
            {
                return IntPtr.Zero;
            }
            return handle.Handle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static bool ReadProperty(
        IntPtr display,
        IntPtr window,
        IntPtr property,
        IntPtr reqType,
        nint length,
        out IntPtr data,
        out nuint nitems)
    {
        data = IntPtr.Zero;
        nitems = 0;
        var status = Native.XGetWindowProperty(
            display,
            window,
            property,
            0,
            length,
            0,
            reqType,
            out _,
            out _,
            out nitems,
            out _,
            out data);
        if (status != 0 || data == IntPtr.Zero)
        {
            if (data != IntPtr.Zero) Native.XFree(data);
            data = IntPtr.Zero;
            nitems = 0;
            return false;
        }
        return true;
    }

    private static class Native
    {
        public const int XaAtom = 4;
        public const int XaWindow = 33;

        [DllImport("libX11.so.6")]
        public static extern IntPtr XOpenDisplay(string? display);

        [DllImport("libX11.so.6")]
        public static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        public static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport("libX11.so.6")]
        public static extern IntPtr XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

        [DllImport("libX11.so.6")]
        public static extern int XGetWindowProperty(
            IntPtr display,
            IntPtr window,
            IntPtr property,
            nint long_offset,
            nint long_length,
            int delete,
            IntPtr req_type,
            out IntPtr actual_type,
            out int actual_format,
            out nuint nitems,
            out nuint bytes_after,
            out IntPtr prop);

        [DllImport("libX11.so.6")]
        public static extern int XFree(IntPtr data);
    }
}
