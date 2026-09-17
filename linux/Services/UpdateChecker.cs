using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MonitorSuhu.Core;

namespace MonitorSuhu.Linux.Services;

public sealed partial class UpdateChecker : ObservableObject
{
    public const string ReleasesPage = "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest";
    private const string Api = "https://api.github.com/repos/BadryansahBangsawan/MonitorSuhu/releases/latest";

    public string CurrentVersion { get; }

    [ObservableProperty] private string? latestVersion;
    [ObservableProperty] private string? latestUrl;
    [ObservableProperty] private ReleaseAsset? latestAsset;
    [ObservableProperty] private bool hasUpdate;
    [ObservableProperty] private bool checking;
    [ObservableProperty] private bool installing;
    [ObservableProperty] private string? lastError;

    public string UpdateMessage =>
        HasUpdate && !string.IsNullOrEmpty(LatestVersion)
            ? $"MonitorSuhu {LatestVersion} is ready."
            : $"MonitorSuhu {CurrentVersion}";

    public string UpdateHint =>
        HasUpdate
            ? "Download the release, replace this app, then open MonitorSuhu again."
            : "";

    partial void OnHasUpdateChanged(bool value)
    {
        OnPropertyChanged(nameof(UpdateMessage));
        OnPropertyChanged(nameof(UpdateHint));
    }

    public UpdateChecker(string? currentVersion = null)
    {
        var v = currentVersion
            ?? typeof(UpdateChecker).Assembly.GetName().Version?.ToString()
            ?? "0";
        CurrentVersion = Versioning.Trim(v);
    }

    public async Task CheckAsync(bool userInitiated = false)
    {
        if (Checking || Installing) return;
        Checking = true;
        LastError = null;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"MonitorSuhu/{CurrentVersion}");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await client.GetAsync(Api);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"GitHub HTTP {(int)response.StatusCode}";
                if (userInitiated)
                    await ShowAsync("Could not check for updates.", LastError);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            if (tag.StartsWith('v')) tag = tag[1..];
            var page = ReleasesPage;
            if (doc.RootElement.TryGetProperty("html_url", out var html) && html.GetString() is { } href)
                page = href;

            LatestVersion = tag;
            LatestUrl = page;
            LatestAsset = ReleaseAssets.Pick(ParseAssets(doc.RootElement), "linux");
            HasUpdate = Versioning.IsNewer(tag, CurrentVersion);
            OnPropertyChanged(nameof(UpdateMessage));
            OnPropertyChanged(nameof(UpdateHint));

            if (userInitiated)
            {
                if (HasUpdate)
                {
                    await ShowAsync(
                        $"MonitorSuhu {tag} is available",
                        "Download the release, replace this app, then open MonitorSuhu again.",
                        open: page);
                }
                else
                {
                    await ShowAsync("You're up to date.", $"MonitorSuhu {CurrentVersion} is the latest release.");
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            if (userInitiated)
                await ShowAsync("Could not check for updates.", ex.Message);
        }
        finally
        {
            Checking = false;
        }
    }

    public Task ApplyAsync()
    {
        OpenDownloadPage();
        return Task.CompletedTask;
    }

    public void OpenDownloadPage()
    {
        var url = string.IsNullOrWhiteSpace(LatestUrl) ? ReleasesPage : LatestUrl;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static List<ReleaseAsset> ParseAssets(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return [];
        var list = new List<ReleaseAsset>();
        foreach (var item in assets.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var url = item.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            var size = item.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : 0;
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url))
                list.Add(new ReleaseAsset(name, url, size));
        }
        return list;
    }

    public static bool IsNewer(string latest, string current) =>
        Versioning.IsNewer(latest, current);

    private static async Task ShowAsync(string title, string message, string? open = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var text = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8
            };
            var panel = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12
            };
            panel.Children.Add(text);
            panel.Children.Add(buttons);

            var window = new Window
            {
                Title = title,
                Width = 440,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = panel
            };

            if (open is not null)
            {
                var yes = new Button { Content = "Open download page", IsDefault = true };
                yes.Click += (_, _) =>
                {
                    Process.Start(new ProcessStartInfo(open) { UseShellExecute = true });
                    window.Close();
                };
                var no = new Button { Content = "Not now" };
                no.Click += (_, _) => window.Close();
                buttons.Children.Add(no);
                buttons.Children.Add(yes);
            }
            else
            {
                var ok = new Button { Content = "OK", IsDefault = true };
                ok.Click += (_, _) => window.Close();
                buttons.Children.Add(ok);
            }

            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var owner = lifetime?.Windows.FirstOrDefault(w => w.IsActive) ?? lifetime?.MainWindow;
            if (owner is not null)
                window.Show(owner);
            else
                window.Show();
        });
    }
}
