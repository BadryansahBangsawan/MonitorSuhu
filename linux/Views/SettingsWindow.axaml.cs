using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MonitorSuhu.Linux.ViewModels;

namespace MonitorSuhu.Linux.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.RefreshCatalog();
        Opened += (_, _) => vm.RefreshCatalog();
        Activated += (_, _) => vm.RefreshCatalog();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || !vm.IsRecording)
            return;

        e.Handled = vm.HandleRecordKey(
            VirtualKey(e.Key),
            control: e.KeyModifiers.HasFlag(KeyModifiers.Control),
            shift: e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            alt: e.KeyModifiers.HasFlag(KeyModifiers.Alt),
            win: e.KeyModifiers.HasFlag(KeyModifiers.Meta),
            escape: e.Key == Key.Escape);
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
