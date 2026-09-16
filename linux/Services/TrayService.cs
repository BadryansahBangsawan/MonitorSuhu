using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace MonitorSuhu.Linux.Services;

public sealed class TrayService : IDisposable
{
    private readonly TrayIcon _icon;
    private readonly TrayIcons _icons;

    public TrayService(Action toggleOverlay, Action editLayout, Action openSettings, Action checkUpdates, Action exit)
    {
        var menu = new NativeMenu();
        menu.Items.Add(Item("Show / Hide Overlay", toggleOverlay));
        menu.Items.Add(Item("Edit Layout", editLayout));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item("Settings…", openSettings));
        menu.Items.Add(Item("Check for Updates…", checkUpdates));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item("Exit", exit));

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
