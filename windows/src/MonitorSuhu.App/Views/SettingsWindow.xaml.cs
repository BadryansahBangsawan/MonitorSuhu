using System.Windows;
using MonitorSuhu.App.ViewModels;

namespace MonitorSuhu.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
