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

namespace MonitorSuhu.Linux.Services;

public sealed partial class UpdateChecker : ObservableObject
{
    public const string ReleasesPage = "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest";
    private const string Api = "https://api.github.com/repos/BadryansahBangsawan/MonitorSuhu/releases/latest";

    public string CurrentVersion { get; }

    [ObservableProperty] private string? latestVersion;
    [ObservableProperty] private string? latestUrl;
    [ObservableProperty] private bool hasUpdate;
    [ObservableProperty] private bool checking;
    [ObservableProperty] private string? lastError;

    public string UpdateMessage =>
        HasUpdate && !string.IsNullOrEmpty(LatestVersion)
            ? $"Version {LatestVersion} is available. You have {CurrentVersion}."
            : $"MonitorSuhu {CurrentVersion}";

    public UpdateChecker(string? currentVersion = null)
    {
        var v = currentVersion
            ?? typeof(UpdateChecker).Assembly.GetName().Version?.ToString()
            ?? "0";
        CurrentVersion = TrimVersion(v);
    }

    public async Task CheckAsync(bool userInitiated = false)
    {
        if (Checking) return;
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
            HasUpdate = IsNewer(tag, CurrentVersion);
            OnPropertyChanged(nameof(UpdateMessage));

            if (userInitiated)
            {
                if (HasUpdate)
                {
                    await ShowAsync(
                        $"MonitorSuhu {tag} is available",
                        "Download it from GitHub Releases, then run the installer.",
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

    public void OpenDownloadPage()
    {
        var url = string.IsNullOrWhiteSpace(LatestUrl) ? ReleasesPage : LatestUrl;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static bool IsNewer(string latest, string current)
    {
        var a = Parse(latest);
        var b = Parse(current);
        var n = Math.Max(a.Count, b.Count);
        for (var i = 0; i < n; i++)
        {
            var x = i < a.Count ? a[i] : 0;
            var y = i < b.Count ? b[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    private static List<int> Parse(string version) =>
        version.Split('.')
            .Select(part => int.TryParse(new string(part.Where(char.IsDigit).ToArray()), out var n) ? n : 0)
            .ToList();

    private static string TrimVersion(string version)
    {
        var parts = Parse(version);
        while (parts.Count > 3) parts.RemoveAt(parts.Count - 1);
        while (parts.Count > 1 && parts[^1] == 0 && parts.Count > 3) parts.RemoveAt(parts.Count - 1);
        if (parts.Count == 4 && parts[3] == 0) parts.RemoveAt(3);
        return string.Join('.', parts);
    }

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
