using System.Windows;
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
}
