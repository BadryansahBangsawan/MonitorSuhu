using System.Windows;
using Visibility = System.Windows.Visibility;
using Hardcodet.Wpf.TaskbarNotification;

namespace MonitorSuhu.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayService(Action toggleOverlay, Action editLayout, Action openSettings, Action exit)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        menu.Items.Add(Item("Show / Hide Overlay", toggleOverlay));
        menu.Items.Add(Item("Edit Layout", editLayout));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(Item("Settings…", openSettings));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(Item("Exit", exit));

        _icon = new TaskbarIcon
        {
            ToolTipText = "MonitorSuhu",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenu = menu,
            Visibility = Visibility.Visible
        };
        _icon.TrayMouseDoubleClick += (_, _) => openSettings();
    }

    public void ShowWarning(string message)
    {
        _icon.ShowBalloonTip("MonitorSuhu", message, BalloonIcon.Warning);
    }

    public void Dispose() => _icon.Dispose();

    private static System.Windows.Controls.MenuItem Item(string header, Action action)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
