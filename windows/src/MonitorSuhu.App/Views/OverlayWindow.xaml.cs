using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MonitorSuhu.App.ViewModels;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;

namespace MonitorSuhu.App.Views;

public enum CornerPreset { TopLeft, TopRight, BottomLeft, BottomRight }

public partial class OverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExNoactivate = 0x08000000;
    private const int WsExTopmost = 0x00000008;
    private const double SnapPx = 24;
    private const double MarginPx = 16;

    private readonly OverlayViewModel _vm;
    private readonly SettingsStore _store;

    public OverlayWindow(OverlayViewModel vm, SettingsStore store)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
        _store = store;
        Loaded += (_, _) =>
        {
            ApplyExtendedStyle();
            ApplyLock();
            RestorePosition();
        };
        LocationChanged += (_, _) =>
        {
            if (!_store.Settings.Locked) SnapIfNeeded();
            PersistPosition();
        };
    }

    public void ApplyTheme()
    {
        _vm.RefreshTheme();
        SizeToContent = SizeToContent.WidthAndHeight;
    }

    public void ApplyLock()
    {
        ApplyExtendedStyle();
        Cursor = _store.Settings.Locked ? Cursors.Arrow : Cursors.SizeAll;
        _vm.RefreshTheme();
    }

    public void ApplyPreset(CornerPreset preset)
    {
        var wa = CurrentWorkArea();
        SizeToContent = SizeToContent.WidthAndHeight;
        UpdateLayout();
        var w = ActualWidth;
        var h = ActualHeight;
        var x = preset switch
        {
            CornerPreset.TopLeft or CornerPreset.BottomLeft => wa.Left + MarginPx,
            _ => wa.Right - w - MarginPx
        };
        var y = preset switch
        {
            CornerPreset.TopLeft or CornerPreset.TopRight => wa.Top + MarginPx,
            _ => wa.Bottom - h - MarginPx
        };
        Left = x;
        Top = y;
        PersistPosition();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (_store.Settings.Locked) return;
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ApplyExtendedStyle()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var style = Native.GetWindowLong(hwnd, GwlExstyle);
        style |= WsExToolwindow | WsExNoactivate | WsExTopmost;
        if (_store.Settings.Locked)
        {
            style |= WsExTransparent;
        }
        else
        {
            style &= ~WsExTransparent;
        }
        Native.SetWindowLong(hwnd, GwlExstyle, style);
    }

    private void SnapIfNeeded()
    {
        var wa = CurrentWorkArea();
        var x = Left;
        var y = Top;
        if (Math.Abs(x - wa.Left) < SnapPx) x = wa.Left + MarginPx;
        if (Math.Abs((x + ActualWidth) - wa.Right) < SnapPx) x = wa.Right - ActualWidth - MarginPx;
        if (Math.Abs(y - wa.Top) < SnapPx) y = wa.Top + MarginPx;
        if (Math.Abs((y + ActualHeight) - wa.Bottom) < SnapPx) y = wa.Bottom - ActualHeight - MarginPx;
        if (Math.Abs(x - Left) > 0.5 || Math.Abs(y - Top) > 0.5)
        {
            Left = x;
            Top = y;
        }
    }

    private void PersistPosition()
    {
        var wa = CurrentWorkArea();
        _store.Settings.Position = new OverlayPosition
        {
            MonitorDeviceName = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).EnsureHandle()).DeviceName,
            RelativeX = wa.Width <= 0 ? 0 : (Left - wa.Left) / wa.Width,
            RelativeY = wa.Height <= 0 ? 0 : (Top - wa.Top) / wa.Height
        };
        _store.Save();
    }

    private void RestorePosition()
    {
        var saved = _store.Settings.Position;
        if (saved is null)
        {
            ApplyPreset(CornerPreset.TopRight);
            return;
        }

        var screen = System.Windows.Forms.Screen.AllScreens
            .FirstOrDefault(s => s.DeviceName == saved.MonitorDeviceName)
            ?? System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is null)
        {
            ApplyPreset(CornerPreset.TopRight);
            return;
        }

        var wa = screen.WorkingArea;
        Left = wa.Left + saved.RelativeX * wa.Width;
        Top = wa.Top + saved.RelativeY * wa.Height;
    }

    private Rect CurrentWorkArea()
    {
        var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).EnsureHandle());
        var wa = screen.WorkingArea;
        return new Rect(wa.Left, wa.Top, wa.Width, wa.Height);
    }

    private static class Native
    {
        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
