using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Linux.ViewModels;

namespace MonitorSuhu.Linux.Views;

public enum CornerPreset { TopLeft, TopRight, BottomLeft, BottomRight }

public partial class OverlayWindow : Window
{
    private const double SnapPx = 24;
    private const double MarginPx = 16;

    private readonly OverlayViewModel _vm;
    private readonly SettingsStore _store;
    private bool _isFinalizing;
    private bool _needsRestore;

    public OverlayWindow(OverlayViewModel vm, SettingsStore store)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
        _store = store;
        _vm.Rows.CollectionChanged += (_, _) => TryFinishRestore();
        Opened += (_, _) =>
        {
            ApplyLock();
            SizeToContent = SizeToContent.WidthAndHeight;
            _needsRestore = true;
            RestorePosition();
            TryFinishRestore();
        };
        SizeChanged += OnHudSizeChanged;
        PositionChanged += (_, _) =>
        {
            if (_isFinalizing) return;
            _isFinalizing = true;
            try
            {
                FinalizePosition();
                PersistPosition();
            }
            finally
            {
                _isFinalizing = false;
            }
        };
    }

    public void ApplyTheme()
    {
        _vm.RefreshTheme();
        SizeToContent = SizeToContent.WidthAndHeight;
    }

    public void ApplyLock()
    {
        Cursor = _store.Settings.Locked
            ? new Cursor(StandardCursorType.Arrow)
            : new Cursor(StandardCursorType.SizeAll);
        _vm.RefreshTheme();
        TryApplyX11WindowType(_store.Settings.Locked);
    }

    public void ApplySuppressed(bool suppressed)
    {
        if (suppressed)
        {
            if (IsVisible) Hide();
            return;
        }
        if (_store.Settings.OverlayVisible && !IsVisible)
            Show();
    }

    public void ApplyPreset(CornerPreset preset)
    {
        var wa = CurrentWorkArea();
        SizeToContent = SizeToContent.WidthAndHeight;
        UpdateLayout();
        GetHudPixelSize(out var w, out var h);
        var x = preset switch
        {
            CornerPreset.TopLeft or CornerPreset.BottomLeft => wa.X + MarginPx,
            _ => wa.Right - w - MarginPx
        };
        var y = preset switch
        {
            CornerPreset.TopLeft or CornerPreset.TopRight => wa.Y + MarginPx,
            _ => wa.Bottom - h - MarginPx
        };
        _isFinalizing = true;
        ApplyOrigin(new PixelPoint((int)Math.Round(x), (int)Math.Round(y)));
        FinalizePosition(forceSnap: true);
        _isFinalizing = false;
        PersistPosition();
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (_store.Settings.Locked) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnHudSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isFinalizing) return;

        var wa = CurrentWorkArea();
        var scale = Scale;
        var prevW = e.PreviousSize.Width * scale;
        var prevH = e.PreviousSize.Height * scale;
        var newW = e.NewSize.Width * scale;
        var newH = e.NewSize.Height * scale;
        if (prevW > 1 && prevH > 1 && (e.WidthChanged || e.HeightChanged))
        {
            var left = (double)Position.X;
            var top = (double)Position.Y;
            var oldMidX = left + prevW / 2;
            var oldMidY = top + prevH / 2;
            _isFinalizing = true;
            if (oldMidX >= wa.X + wa.Width / 2.0)
            {
                left = left + prevW - newW;
            }
            if (oldMidY >= wa.Y + wa.Height / 2.0)
            {
                top = top + prevH - newH;
            }
            ApplyOrigin(ClampOrigin(left, top, newW, newH, wa));
            _isFinalizing = false;
        }

        TryFinishRestore();
    }

    private void TryFinishRestore()
    {
        if (!_needsRestore || !IsVisible) return;
        RestorePosition();
        if (_vm.Rows.Count == 0) return;
        _isFinalizing = true;
        try
        {
            FinalizePosition(forceSnap: true);
            _needsRestore = false;
            PersistPosition();
        }
        finally
        {
            _isFinalizing = false;
        }
    }

    private void FinalizePosition(bool forceSnap = false)
    {
        var wa = CurrentWorkArea();
        GetHudPixelSize(out var w, out var h);
        var origin = ClampOrigin(Position.X, Position.Y, w, h, wa);

        if (forceSnap || !_store.Settings.Locked)
        {
            var x = (double)origin.X;
            var y = (double)origin.Y;
            if (Math.Abs(x - wa.X) < SnapPx) x = wa.X + MarginPx;
            if (Math.Abs((x + w) - wa.Right) < SnapPx) x = wa.Right - w - MarginPx;
            if (Math.Abs(y - wa.Y) < SnapPx) y = wa.Y + MarginPx;
            if (Math.Abs((y + h) - wa.Bottom) < SnapPx) y = wa.Bottom - h - MarginPx;
            origin = ClampOrigin(x, y, w, h, wa);
        }

        if (Math.Abs(origin.X - Position.X) > 0.5 || Math.Abs(origin.Y - Position.Y) > 0.5)
        {
            ApplyOrigin(origin);
        }
    }

    private void PersistPosition()
    {
        if (_needsRestore) return;
        var wa = CurrentWorkArea();
        if (wa.Width <= 0 || wa.Height <= 0) return;

        GetHudPixelSize(out var w, out var h);
        var next = new OverlayPosition
        {
            MonitorDeviceName = ScreenDeviceName(CurrentScreen()),
            RelativeX = Round4((Position.X - wa.X) / (double)wa.Width),
            RelativeY = Round4((Position.Y - wa.Y) / (double)wa.Height),
            RelativeMaxX = Round4((Position.X + w - wa.X) / wa.Width),
            RelativeMaxY = Round4((Position.Y + h - wa.Y) / wa.Height)
        };

        var cur = _store.Settings.Position;
        if (cur is not null
            && cur.MonitorDeviceName == next.MonitorDeviceName
            && Nearly(cur.RelativeX, next.RelativeX)
            && Nearly(cur.RelativeY, next.RelativeY)
            && Nearly(cur.RelativeMaxX, next.RelativeMaxX)
            && Nearly(cur.RelativeMaxY, next.RelativeMaxY))
        {
            return;
        }

        _store.Settings.Position = next;
        _store.SaveDebounced();
    }

    private void RestorePosition()
    {
        var saved = _store.Settings.Position;
        if (saved is null)
        {
            ApplyPreset(CornerPreset.TopRight);
            return;
        }

        var screen = FindScreen(saved.MonitorDeviceName);
        if (screen is null)
        {
            ApplyPreset(CornerPreset.TopRight);
            return;
        }

        if (saved.RelativeMaxX is null && saved.RelativeMaxY is null)
        {
            var nearLeft = saved.RelativeX < 0.08;
            var nearRight = saved.RelativeX > 0.75;
            var nearTop = saved.RelativeY < 0.08;
            var nearBottom = saved.RelativeY > 0.75;
            if (nearLeft && nearTop) { ApplyPreset(CornerPreset.TopLeft); return; }
            if (nearRight && nearTop) { ApplyPreset(CornerPreset.TopRight); return; }
            if (nearLeft && nearBottom) { ApplyPreset(CornerPreset.BottomLeft); return; }
            if (nearRight && nearBottom) { ApplyPreset(CornerPreset.BottomRight); return; }
        }

        var wa = screen.WorkingArea;
        GetHudPixelSize(out var w, out var h);
        var pinRight = saved.RelativeMaxX is { } maxX && Math.Abs(maxX - 1) < Math.Abs(saved.RelativeX);
        var pinBottom = saved.RelativeMaxY is { } maxY && Math.Abs(maxY - 1) < Math.Abs(saved.RelativeY);

        double x;
        double y;
        if (pinRight && saved.RelativeMaxX is { } right)
        {
            x = wa.X + right * wa.Width - w;
        }
        else
        {
            x = wa.X + saved.RelativeX * wa.Width;
        }
        if (pinBottom && saved.RelativeMaxY is { } bottom)
        {
            y = wa.Y + bottom * wa.Height - h;
        }
        else
        {
            y = wa.Y + saved.RelativeY * wa.Height;
        }

        _isFinalizing = true;
        ApplyOrigin(ClampOrigin(x, y, w, h, wa));
        _isFinalizing = false;
    }

    private void ApplyOrigin(PixelPoint origin) => Position = origin;

    private static PixelPoint ClampOrigin(double x, double y, double w, double h, PixelRect wa)
    {
        var insetX = wa.X + MarginPx;
        var insetY = wa.Y + MarginPx;
        var insetW = Math.Max(0, wa.Width - 2 * MarginPx);
        var insetH = Math.Max(0, wa.Height - 2 * MarginPx);
        if (insetW <= 0 || insetH <= 0)
        {
            return new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
        }
        w = Math.Min(w, insetW);
        h = Math.Min(h, insetH);
        if (x < insetX) x = insetX;
        if (x + w > insetX + insetW) x = insetX + insetW - w;
        if (y < insetY) y = insetY;
        if (y + h > insetY + insetH) y = insetY + insetH - h;
        return new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
    }

    private PixelRect CurrentWorkArea()
    {
        try
        {
            var screen = CurrentScreen();
            if (screen is not null && screen.WorkingArea.Width > 0)
            {
                return screen.WorkingArea;
            }
            if (Screens.Primary is { } primary && primary.WorkingArea.Width > 0)
            {
                return primary.WorkingArea;
            }
        }
        catch
        {
            // platform handle / screens may not exist yet
        }
        return new PixelRect(0, 0, 1280, 720);
    }

    private Screen? CurrentScreen()
    {
        try
        {
            if (PlatformImpl is not null)
            {
                return Screens.ScreenFromWindow(this)
                    ?? Screens.ScreenFromPoint(Position)
                    ?? Screens.Primary;
            }
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            return Screens.ScreenFromPoint(Position) ?? Screens.Primary;
        }
        catch
        {
            return Screens.Primary;
        }
    }

    private Screen? FindScreen(string deviceName)
    {
        IReadOnlyList<Screen> all;
        try
        {
            all = Screens.All;
        }
        catch
        {
            return null;
        }

        if (!string.IsNullOrEmpty(deviceName))
        {
            foreach (var screen in all)
            {
                if (ScreenDeviceName(screen) == deviceName)
                {
                    return screen;
                }
            }
        }

        return Screens.Primary ?? all.FirstOrDefault();
    }

    private void GetHudPixelSize(out double w, out double h)
    {
        var scale = Scale;
        var dipW = ClientSize.Width;
        var dipH = ClientSize.Height;
        if (dipW <= 0) dipW = Bounds.Width;
        if (dipH <= 0) dipH = Bounds.Height;
        w = dipW * scale;
        h = dipH * scale;
    }

    private double Scale => RenderScaling <= 0 ? 1 : RenderScaling;

    private static string ScreenDeviceName(Screen? screen)
    {
        if (screen is null) return "";
        if (!string.IsNullOrWhiteSpace(screen.DisplayName)) return screen.DisplayName;
        var b = screen.Bounds;
        return $"{b.X},{b.Y},{b.Width}x{b.Height}";
    }

    private void TryApplyX11WindowType(bool locked)
    {
        try
        {
            var handle = TryGetPlatformHandle();
            if (handle is null) return;
            var desc = handle.HandleDescriptor;
            if (!string.Equals(desc, "XID", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(desc, "X11", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            NativeX11.SetNetWmWindowType(handle.Handle, locked);
        }
        catch
        {
            // best-effort; Wayland and missing libX11 stay as drag-lock only
        }
    }

    private static double Round4(double value) => Math.Round(value, 4);

    private static bool Nearly(double? a, double? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return Math.Abs(a.Value - b.Value) < 0.00015;
    }

    private static class NativeX11
    {
        private const int PropModeReplace = 0;
        private const int XaAtom = 4;

        public static void SetNetWmWindowType(IntPtr window, bool notification)
        {
            var display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero) return;
            try
            {
                var property = XInternAtom(display, "_NET_WM_WINDOW_TYPE", false);
                var value = XInternAtom(
                    display,
                    notification ? "_NET_WM_WINDOW_TYPE_NOTIFICATION" : "_NET_WM_WINDOW_TYPE_NORMAL",
                    false);
                if (property == IntPtr.Zero || value == IntPtr.Zero) return;
                XChangeProperty(display, window, property, (IntPtr)XaAtom, 32, PropModeReplace, ref value, 1);
                XFlush(display);
            }
            finally
            {
                XCloseDisplay(display);
            }
        }

        [DllImport("libX11.so.6")]
        private static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

        [DllImport("libX11.so.6")]
        private static extern int XChangeProperty(
            IntPtr display,
            IntPtr window,
            IntPtr property,
            IntPtr type,
            int format,
            int mode,
            ref IntPtr data,
            int nelements);

        [DllImport("libX11.so.6")]
        private static extern int XFlush(IntPtr display);
    }
}
