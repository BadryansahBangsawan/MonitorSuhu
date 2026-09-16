using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Linux.Services;

public sealed class X11HotkeyService : IDisposable
{
    private const int GrabModeAsync = 1;
    private const int KeyPress = 2;
    private const int BadAccess = 10;
    private const uint ShiftMask = 1 << 0;
    private const uint LockMask = 1 << 1;
    private const uint ControlMask = 1 << 2;
    private const uint Mod1Mask = 1 << 3;
    private const uint Mod2Mask = 1 << 4;
    private const uint Mod4Mask = 1 << 6;

    private static readonly uint[] LockCombos = [0, LockMask, Mod2Mask, LockMask | Mod2Mask];

    private IntPtr _display;
    private IntPtr _window;
    private int _toggleCode;
    private int _editCode;
    private uint _toggleMods;
    private uint _editMods;
    private Action? _onToggle;
    private Action? _onEdit;
    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private Native.XErrorHandler? _handler;

    public string? FailedMessage { get; private set; }

    public void Start(Window overlay, KeyChord toggle, KeyChord edit, Action onToggle, Action onEdit)
    {
        Stop();
        _onToggle = onToggle;
        _onEdit = onEdit;
        FailedMessage = null;

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            return;

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
            return;

        try
        {
            Native.XInitThreads();
        }
        catch (DllNotFoundException)
        {
            FailedMessage = "Global shortcuts need X11 — use the tray menu.";
            return;
        }
        catch (EntryPointNotFoundException)
        {
            // Optional; continue.
        }

        var platform = overlay.TryGetPlatformHandle();
        if (platform is null || platform.Handle == IntPtr.Zero)
        {
            FailedMessage = "Failed to register global shortcuts — use the tray menu.";
            return;
        }

        var desc = platform.HandleDescriptor ?? "";
        if (!desc.Equals("X11", StringComparison.OrdinalIgnoreCase))
        {
            FailedMessage = "Global shortcuts need X11 — use the tray menu.";
            return;
        }

        _display = Native.XOpenDisplay(null);
        if (_display == IntPtr.Zero)
        {
            FailedMessage = "Failed to register global shortcuts — use the tray menu.";
            return;
        }

        _window = platform.Handle;
        _toggleMods = ToXModifiers(toggle);
        _editMods = ToXModifiers(edit);
        _toggleCode = ToKeycode(_display, toggle.VirtualKey);
        _editCode = ToKeycode(_display, edit.VirtualKey);

        var toggleOk = _toggleCode != 0 && Grab(_toggleCode, _toggleMods);
        var editOk = _editCode != 0 && Grab(_editCode, _editMods);

        if (!toggleOk && !editOk)
            FailedMessage = "Ctrl+Shift+T and Ctrl+Shift+E are already in use.";
        else if (!toggleOk)
            FailedMessage = "Ctrl+Shift+T is already in use — overlay toggle was not registered.";
        else if (!editOk)
            FailedMessage = "Ctrl+Shift+E is already in use — edit-layout was not registered.";

        if (!toggleOk && !editOk)
        {
            Native.XCloseDisplay(_display);
            _display = IntPtr.Zero;
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _thread = new Thread(() => Pump(token))
        {
            IsBackground = true,
            Name = "MonitorSuhu.X11Hotkeys"
        };
        _thread.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
        if (_thread is { } thread)
        {
            try { thread.Join(200); } catch { /* ignore */ }
        }
        _thread = null;
        _cts?.Dispose();
        _cts = null;

        if (_display != IntPtr.Zero)
        {
            try
            {
                if (_toggleCode != 0) Ungrab(_toggleCode, _toggleMods);
                if (_editCode != 0) Ungrab(_editCode, _editMods);
                Native.XCloseDisplay(_display);
            }
            catch
            {
                // Best-effort teardown.
            }
            _display = IntPtr.Zero;
        }

        _toggleCode = 0;
        _editCode = 0;
        _onToggle = null;
        _onEdit = null;
        _handler = null;
    }

    public void Dispose() => Stop();

    private bool Grab(int keycode, uint modifiers)
    {
        var ok = true;
        foreach (var extra in LockCombos)
        {
            if (!GrabOnce(keycode, modifiers | extra))
                ok = false;
        }
        return ok;
    }

    private bool GrabOnce(int keycode, uint modifiers)
    {
        var bad = false;
        _handler = (_, error) =>
        {
            var code = Marshal.ReadByte(error, Native.ErrorCodeOffset);
            if (code == BadAccess) bad = true;
            return 0;
        };
        var previous = Native.XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(_handler));
        Native.XGrabKey(_display, keycode, modifiers, _window, 1, GrabModeAsync, GrabModeAsync);
        Native.XSync(_display, 0);
        Native.XSetErrorHandler(previous);
        return !bad;
    }

    private void Ungrab(int keycode, uint modifiers)
    {
        foreach (var extra in LockCombos)
            Native.XUngrabKey(_display, keycode, modifiers | extra, _window);
        Native.XSync(_display, 0);
    }

    private void Pump(CancellationToken token)
    {
        var display = _display;
        if (display == IntPtr.Zero) return;
        try
        {
            while (!token.IsCancellationRequested)
            {
                while (Native.XPending(display) > 0)
                {
                    Native.XNextEvent(display, out var ev);
                    if (ev.type != KeyPress) continue;
                    var code = (int)ev.keycode;
                    if (code == _toggleCode)
                        Dispatcher.UIThread.Post(() => _onToggle?.Invoke());
                    else if (code == _editCode)
                        Dispatcher.UIThread.Post(() => _onEdit?.Invoke());
                }
                Thread.Sleep(40);
            }
        }
        catch (Exception)
        {
            // Display closed from Stop.
        }
    }

    private static uint ToXModifiers(KeyChord chord)
    {
        uint m = 0;
        if (chord.Shift) m |= ShiftMask;
        if (chord.Control) m |= ControlMask;
        if (chord.Alt) m |= Mod1Mask;
        if (chord.Win) m |= Mod4Mask;
        return m;
    }

    private static int ToKeycode(IntPtr display, uint virtualKey)
    {
        var code = Native.XKeysymToKeycode(display, virtualKey);
        if (code == 0 && virtualKey is >= 0x41 and <= 0x5A)
            code = Native.XKeysymToKeycode(display, virtualKey + 0x20);
        return code;
    }

    private static class Native
    {
        // XErrorEvent.error_code after type(int)+pad, Display*, XID, unsigned long serial.
        public const int ErrorCodeOffset = 32;

        public delegate int XErrorHandler(IntPtr display, IntPtr errorEvent);

        [DllImport("libX11.so.6")]
        public static extern int XInitThreads();

        [DllImport("libX11.so.6")]
        public static extern IntPtr XOpenDisplay(string? display);

        [DllImport("libX11.so.6")]
        public static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        public static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow, int ownerEvents, int pointerMode, int keyboardMode);

        [DllImport("libX11.so.6")]
        public static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow);

        [DllImport("libX11.so.6")]
        public static extern byte XKeysymToKeycode(IntPtr display, nuint keysym);

        [DllImport("libX11.so.6")]
        public static extern int XPending(IntPtr display);

        [DllImport("libX11.so.6")]
        public static extern int XNextEvent(IntPtr display, out XEvent evt);

        [DllImport("libX11.so.6")]
        public static extern int XSync(IntPtr display, int discard);

        [DllImport("libX11.so.6")]
        public static extern IntPtr XSetErrorHandler(IntPtr handler);
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XEvent
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(84)] public uint keycode;
    }
}
