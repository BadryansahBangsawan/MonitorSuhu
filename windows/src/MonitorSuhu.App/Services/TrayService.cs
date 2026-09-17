using System.Windows;
using Visibility = System.Windows.Visibility;
using Hardcodet.Wpf.TaskbarNotification;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;

    private System.Windows.Controls.MenuItem? _customProfile;
    private readonly Func<string>? _activeProfile;

    public TrayService(
        Action toggleOverlay,
        Action editLayout,
        Action muteAlerts,
        Action openSettings,
        Action checkUpdates,
        Action exit,
        Action<string>? applyProfile = null,
        Func<string>? activeProfile = null)
    {
        _activeProfile = activeProfile;
        var menu = new System.Windows.Controls.ContextMenu();
        menu.Items.Add(Item("Show / Hide Overlay", toggleOverlay));
        menu.Items.Add(Item("Edit Layout", editLayout));
        menu.Items.Add(Item("Mute alerts 15 min", muteAlerts));
        menu.Items.Add(ProfileMenu(applyProfile));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(Item("Settings…", openSettings));
        menu.Items.Add(Item("Check for Updates…", checkUpdates));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(Item("Exit", exit));
        menu.Opened += (_, _) => RefreshProfileChecks();

        _icon = new TaskbarIcon
        {
            ToolTipText = "MonitorSuhu",
            Icon = LoadAppIcon(),
            ContextMenu = menu,
            Visibility = Visibility.Visible
        };
        _icon.TrayMouseDoubleClick += (_, _) => openSettings();
    }

    public void SetStatus(string title, bool critical)
    {
        const string suffix = " — CPU critical";
        _icon.ToolTipText = !critical || title.Contains(suffix)
            ? title
            : $"{title}{suffix}";
    }

    public void ShowWarning(string message)
    {
        _icon.ShowBalloonTip("MonitorSuhu", message, BalloonIcon.Warning);
    }

    public void ShowInfo(string message)
    {
        _icon.ShowBalloonTip("MonitorSuhu", message, BalloonIcon.Info);
    }

    public void Dispose() => _icon.Dispose();

    private System.Windows.Controls.MenuItem ProfileMenu(Action<string>? apply)
    {
        var menu = new System.Windows.Controls.MenuItem { Header = "Profile" };
        menu.Items.Add(ProfileItem("Desktop", HudProfiles.Desktop, apply));
        menu.Items.Add(ProfileItem("Game", HudProfiles.Game, apply));
        menu.Items.Add(ProfileItem("Silent", HudProfiles.Silent, apply));
        _customProfile = new System.Windows.Controls.MenuItem { Header = "Custom", IsEnabled = false };
        menu.Items.Add(_customProfile);
        return menu;
    }

    private System.Windows.Controls.MenuItem ProfileItem(string header, string name, Action<string>? apply)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header, Tag = name, IsCheckable = true };
        item.Click += (_, _) => apply?.Invoke(name);
        return item;
    }

    private void RefreshProfileChecks()
    {
        var active = _activeProfile?.Invoke() ?? HudProfiles.Custom;
        if (_icon.ContextMenu is not { } menu) return;
        foreach (var obj in menu.Items)
        {
            if (obj is not System.Windows.Controls.MenuItem { Header: "Profile", Items: { } items }) continue;
            foreach (var child in items)
            {
                if (child is System.Windows.Controls.MenuItem item)
                    item.IsChecked = item.Tag as string == active;
            }
        }
        if (_customProfile is { } custom)
            custom.IsChecked = active == HudProfiles.Custom;
    }

    private static System.Windows.Controls.MenuItem Item(string header, Action action)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path)
                && System.Drawing.Icon.ExtractAssociatedIcon(path) is { } extracted)
            {
                return extracted;
            }
        }
        catch
        {
            // Fall back to the generic application icon.
        }
        return System.Drawing.SystemIcons.Application;
    }
}
