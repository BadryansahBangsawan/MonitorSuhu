using System.Windows;
using System.Windows.Input;
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
    }

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
