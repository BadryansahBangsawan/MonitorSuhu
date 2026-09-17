using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Window = System.Windows.Window;
using Rect = System.Windows.Rect;
using Point = System.Windows.Point;
using Cursors = System.Windows.Input.Cursors;
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
    private bool _isFinalizing;
    private bool _needsRestore;

    public OverlayWindow(OverlayViewModel vm, SettingsStore store)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
        _store = store;
        _vm.Rows.CollectionChanged += (_, _) => TryFinishRestore();
        Loaded += (_, _) =>
        {
            ApplyExtendedStyle();
            ApplyLock();
            _needsRestore = true;
            RestorePosition();
            TryFinishRestore();
        };
        SizeChanged += OnHudSizeChanged;
        LocationChanged += (_, _) =>
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
        ApplyExtendedStyle();
        Cursor = _store.Settings.Locked ? Cursors.Arrow : Cursors.SizeAll;
        _vm.RefreshTheme();
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
        _isFinalizing = true;
        Left = x;
        Top = y;
        FinalizePosition(forceSnap: true);
        _isFinalizing = false;
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

    private void OnHudSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isFinalizing) return;

        var wa = CurrentWorkArea();
        var prevW = e.PreviousSize.Width;
        var prevH = e.PreviousSize.Height;
        if (prevW > 1 && prevH > 1 && (e.WidthChanged || e.HeightChanged))
        {
            var oldMidX = Left + prevW / 2;
            var oldMidY = Top + prevH / 2;
            _isFinalizing = true;
            if (oldMidX >= wa.Left + wa.Width / 2)
            {
                Left = Left + prevW - e.NewSize.Width;
            }
            if (oldMidY >= wa.Top + wa.Height / 2)
            {
                Top = Top + prevH - e.NewSize.Height;
            }
            ApplyOrigin(ClampOrigin(Left, Top, e.NewSize.Width, e.NewSize.Height, wa));
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

    private void ApplyExtendedStyle()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var style = Native.GetWindowLongPtr(hwnd, GwlExstyle).ToInt64();
        style |= WsExToolwindow | WsExNoactivate | WsExTopmost;
        if (_store.Settings.Locked)
        {
            style |= WsExTransparent;
        }
        else
        {
            style &= ~WsExTransparent;
        }
        Native.SetWindowLongPtr(hwnd, GwlExstyle, (IntPtr)style);
    }

    private void FinalizePosition(bool forceSnap = false)
    {
        var wa = CurrentWorkArea();
        var w = ActualWidth;
        var h = ActualHeight;
        var origin = ClampOrigin(Left, Top, w, h, wa);

        if (forceSnap || !_store.Settings.Locked)
        {
            var x = origin.X;
            var y = origin.Y;
            if (Math.Abs(x - wa.Left) < SnapPx) x = wa.Left + MarginPx;
            if (Math.Abs((x + w) - wa.Right) < SnapPx) x = wa.Right - w - MarginPx;
            if (Math.Abs(y - wa.Top) < SnapPx) y = wa.Top + MarginPx;
            if (Math.Abs((y + h) - wa.Bottom) < SnapPx) y = wa.Bottom - h - MarginPx;
            origin = ClampOrigin(x, y, w, h, wa);
        }

        if (Math.Abs(origin.X - Left) > 0.5 || Math.Abs(origin.Y - Top) > 0.5)
        {
            ApplyOrigin(origin);
        }
    }

    private void PersistPosition()
    {
        if (_needsRestore) return;
        var wa = CurrentWorkArea();
        if (wa.Width <= 0 || wa.Height <= 0) return;

        var w = ActualWidth;
        var h = ActualHeight;
        var next = new OverlayPosition
        {
            MonitorDeviceName = CurrentScreen()?.DeviceName ?? "",
            RelativeX = Round4((Left - wa.Left) / wa.Width),
            RelativeY = Round4((Top - wa.Top) / wa.Height),
            RelativeMaxX = Round4((Left + w - wa.Left) / wa.Width),
            RelativeMaxY = Round4((Top + h - wa.Top) / wa.Height)
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

        var screen = System.Windows.Forms.Screen.AllScreens
            .FirstOrDefault(s => s.DeviceName == saved.MonitorDeviceName)
            ?? System.Windows.Forms.Screen.PrimaryScreen;
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

        var wa = ToDip(screen.WorkingArea);
        var w = ActualWidth;
        var h = ActualHeight;
        var pinRight = saved.RelativeMaxX is { } maxX && Math.Abs(maxX - 1) < Math.Abs(saved.RelativeX);
        var pinBottom = saved.RelativeMaxY is { } maxY && Math.Abs(maxY - 1) < Math.Abs(saved.RelativeY);

        double x;
        double y;
        if (pinRight && saved.RelativeMaxX is { } right)
        {
            x = wa.Left + right * wa.Width - w;
        }
        else
        {
            x = wa.Left + saved.RelativeX * wa.Width;
        }
        if (pinBottom && saved.RelativeMaxY is { } bottom)
        {
            y = wa.Top + bottom * wa.Height - h;
        }
        else
        {
            y = wa.Top + saved.RelativeY * wa.Height;
        }

        _isFinalizing = true;
        ApplyOrigin(ClampOrigin(x, y, w, h, wa));
        _isFinalizing = false;
    }

    private void ApplyOrigin(Point origin)
    {
        Left = origin.X;
        Top = origin.Y;
    }

    private static Point ClampOrigin(double x, double y, double w, double h, Rect wa)
    {
        var inset = new Rect(
            wa.X + MarginPx,
            wa.Y + MarginPx,
            Math.Max(0, wa.Width - 2 * MarginPx),
            Math.Max(0, wa.Height - 2 * MarginPx));
        if (inset.Width <= 0 || inset.Height <= 0) return new Point(x, y);
        w = Math.Min(w, inset.Width);
        h = Math.Min(h, inset.Height);
        if (x < inset.Left) x = inset.Left;
        if (x + w > inset.Right) x = inset.Right - w;
        if (y < inset.Top) y = inset.Top;
        if (y + h > inset.Bottom) y = inset.Bottom - h;
        return new Point(x, y);
    }

    private Rect CurrentWorkArea()
    {
        var screen = CurrentScreen();
        if (screen is null)
        {
            return new Rect(0, 0, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
        }
        return ToDip(screen.WorkingArea);
    }

    private System.Windows.Forms.Screen? CurrentScreen()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        return System.Windows.Forms.Screen.FromHandle(hwnd);
    }

    private Rect ToDip(System.Drawing.Rectangle pixels)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is { } ct)
        {
            var origin = ct.TransformFromDevice.Transform(new Point(pixels.Left, pixels.Top));
            var corner = ct.TransformFromDevice.Transform(new Point(pixels.Right, pixels.Bottom));
            return new Rect(origin, corner);
        }
        var scale = VisualTreeHelper.GetDpi(this);
        var sx = scale.DpiScaleX <= 0 ? 1 : scale.DpiScaleX;
        var sy = scale.DpiScaleY <= 0 ? 1 : scale.DpiScaleY;
        return new Rect(pixels.Left / sx, pixels.Top / sy, pixels.Width / sx, pixels.Height / sy);
    }

    private static double Round4(double value) => Math.Round(value, 4);

    private static bool Nearly(double? a, double? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return Math.Abs(a.Value - b.Value) < 0.00015;
    }

    private static class Native
    {
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
