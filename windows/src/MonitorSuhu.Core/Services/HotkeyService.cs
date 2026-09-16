using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Window = System.Windows.Window;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Core.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ToggleId = 1;
    private const int EditId = 2;

    private HwndSource? _source;
    private Action? _onToggle;
    private Action? _onEdit;

    public void Start(Window window, KeyChord toggle, KeyChord edit, Action onToggle, Action onEdit)
    {
        Stop();
        _onToggle = onToggle;
        _onEdit = onEdit;

        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(Hook);

        Native.RegisterHotKey(helper.Handle, ToggleId, toggle.Modifiers, toggle.VirtualKey);
        Native.RegisterHotKey(helper.Handle, EditId, edit.Modifiers, edit.VirtualKey);
    }

    public void Stop()
    {
        if (_source is null) return;
        var hwnd = _source.Handle;
        Native.UnregisterHotKey(hwnd, ToggleId);
        Native.UnregisterHotKey(hwnd, EditId);
        _source.RemoveHook(Hook);
        _source = null;
    }

    public void Dispose() => Stop();

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        var id = wParam.ToInt32();
        if (id == ToggleId) _onToggle?.Invoke();
        if (id == EditId) _onEdit?.Invoke();
        handled = true;
        return IntPtr.Zero;
    }

    private static class Native
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
