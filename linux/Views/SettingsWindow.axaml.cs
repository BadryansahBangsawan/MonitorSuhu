using Avalonia.Controls;
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
    }
}
