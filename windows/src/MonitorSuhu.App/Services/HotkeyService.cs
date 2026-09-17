using System.Runtime.InteropServices;
using System.Windows.Interop;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.App.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ToggleId = 1;
    private const int EditId = 2;
    private const uint ModNorepeat = 0x4000;
    private static readonly IntPtr HwndMessage = new(-3);

    private HwndSource? _source;
    private Action? _onToggle;
    private Action? _onEdit;

    public string? FailedMessage { get; private set; }

    public void Start(KeyChord toggle, KeyChord edit, Action onToggle, Action onEdit)
    {
        Stop();
        _onToggle = onToggle;
        _onEdit = onEdit;
        FailedMessage = null;

        var parameters = new HwndSourceParameters("MonitorSuhuHotkeys")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0
        };
        parameters.ParentWindow = HwndMessage;
        _source = new HwndSource(parameters);
        _source.AddHook(Hook);

        var hwnd = _source.Handle;
        var toggleOk = Native.RegisterHotKey(hwnd, ToggleId, toggle.Modifiers | ModNorepeat, toggle.VirtualKey);
        var editOk = Native.RegisterHotKey(hwnd, EditId, edit.Modifiers | ModNorepeat, edit.VirtualKey);

        if (!toggleOk && !editOk)
        {
            FailedMessage = $"{toggle.Display} and {edit.Display} are already in use.";
        }
        else if (!toggleOk)
        {
            FailedMessage = $"{toggle.Display} is already in use — overlay toggle was not registered.";
        }
        else if (!editOk)
        {
            FailedMessage = $"{edit.Display} is already in use — edit-layout was not registered.";
        }
    }

    public void Stop()
    {
        if (_source is null) return;
        var hwnd = _source.Handle;
        Native.UnregisterHotKey(hwnd, ToggleId);
        Native.UnregisterHotKey(hwnd, EditId);
        _source.RemoveHook(Hook);
        _source.Dispose();
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
