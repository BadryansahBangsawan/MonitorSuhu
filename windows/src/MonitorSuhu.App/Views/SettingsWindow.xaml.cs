using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Window = System.Windows.Window;
using MonitorSuhu.App.ViewModels;

namespace MonitorSuhu.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.RefreshCatalog();
        Activated += (_, _) => vm.RefreshCatalog();
        SourceInitialized += (_, _) => UseDarkTitleBar();
    }

    // The window is dark; without this Windows draws a white caption bar on top of it.
    private void UseDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var on = 1;
        try
        {
            // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10 2004+ and 11); 19 on older Windows 10 builds.
            if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || !vm.IsRecording)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = vm.HandleRecordKey(
            VirtualKey(key),
            control: Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
            shift: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
            alt: Keyboard.Modifiers.HasFlag(ModifierKeys.Alt),
            win: Keyboard.Modifiers.HasFlag(ModifierKeys.Windows),
            escape: key == Key.Escape);
    }

    private static uint VirtualKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
            return (uint)('A' + (key - Key.A));
        if (key is >= Key.D0 and <= Key.D9)
            return (uint)('0' + (key - Key.D0));
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return (uint)('0' + (key - Key.NumPad0));
        return 0;
    }
}
