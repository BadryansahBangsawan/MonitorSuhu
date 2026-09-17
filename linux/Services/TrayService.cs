using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Linux.Services;

public sealed class TrayService : IDisposable
{
    private readonly TrayIcon _icon;
    private readonly TrayIcons _icons;

    private NativeMenuItem? _customProfile;
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
        var menu = new NativeMenu();
        menu.Items.Add(Item("Show / Hide Overlay", toggleOverlay));
        menu.Items.Add(Item("Edit Layout", editLayout));
        menu.Items.Add(Item("Mute alerts 15 min", muteAlerts));
        menu.Items.Add(ProfileMenu(applyProfile));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item("Settings…", openSettings));
        menu.Items.Add(Item("Check for Updates…", checkUpdates));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item("Exit", exit));
        menu.Opening += (_, _) => RefreshProfileChecks();

        _icon = new TrayIcon
        {
            ToolTipText = "MonitorSuhu",
            Menu = menu,
            IsVisible = true,
            Icon = LoadAppIcon()
        };

        _icons = new TrayIcons();
        _icons.Add(_icon);
        if (Application.Current is { } app)
            TrayIcon.SetIcons(app, _icons);
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
        if (string.IsNullOrEmpty(message)) return;
        _icon.ToolTipText = message;
    }

    public void ShowInfo(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        _icon.ToolTipText = message;
    }

    public void Dispose()
    {
        _icon.IsVisible = false;
        if (Application.Current is { } app)
            TrayIcon.SetIcons(app, new TrayIcons());
        _icon.Dispose();
    }

    private NativeMenuItem ProfileMenu(Action<string>? apply)
    {
        var submenu = new NativeMenu();
        submenu.Items.Add(ProfileItem("Desktop", HudProfiles.Desktop, apply));
        submenu.Items.Add(ProfileItem("Game", HudProfiles.Game, apply));
        submenu.Items.Add(ProfileItem("Silent", HudProfiles.Silent, apply));
        _customProfile = new NativeMenuItem("Custom") { IsEnabled = false };
        submenu.Items.Add(_customProfile);
        return new NativeMenuItem("Profile") { Menu = submenu };
    }

    private NativeMenuItem ProfileItem(string header, string name, Action<string>? apply)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => apply?.Invoke(name);
        item.ToggleType = NativeMenuItemToggleType.Radio;
        return item;
    }

    private void RefreshProfileChecks()
    {
        var active = _activeProfile?.Invoke() ?? HudProfiles.Custom;
        if (_icon.Menu is not { } menu) return;
        foreach (var obj in menu.Items)
        {
            if (obj is not NativeMenuItem { Header: "Profile", Menu: { } submenu }) continue;
            var names = new[] { HudProfiles.Desktop, HudProfiles.Game, HudProfiles.Silent };
            var i = 0;
            foreach (var child in submenu.Items)
            {
                if (child is NativeMenuItem item && i < names.Length)
                {
                    item.IsChecked = names[i] == active;
                    i++;
                }
            }
        }
        if (_customProfile is { } custom)
            custom.IsChecked = active == HudProfiles.Custom;
    }

    private static NativeMenuItem Item(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private static WindowIcon? LoadAppIcon()
    {
        try
        {
            foreach (var path in new[]
                     {
                         Path.Combine(AppContext.BaseDirectory, "MonitorSuhu.ico"),
                         Path.Combine(AppContext.BaseDirectory, "Assets", "MonitorSuhu.ico")
                     })
            {
                if (File.Exists(path))
                    return new WindowIcon(path);
            }
        }
        catch
        {
            // Fall through to the Avalonia asset, then no icon.
        }

        foreach (var uri in new[]
                 {
                     new Uri("avares://MonitorSuhu/Assets/MonitorSuhu.ico"),
                     new Uri("avares://MonitorSuhu.Linux/Assets/MonitorSuhu.ico")
                 })
        {
            try
            {
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    return new WindowIcon(stream);
                }
            }
            catch
            {
                // Tray still works without a custom icon.
            }
        }

        return null;
    }
}
