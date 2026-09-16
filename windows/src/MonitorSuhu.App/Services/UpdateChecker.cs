using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MonitorSuhu.App.Services;

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
                {
                    Show("Could not check for updates.", LastError);
                }
                return;
            }

            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            if (tag.StartsWith('v')) tag = tag[1..];
            var page = ReleasesPage;
            if (doc.RootElement.TryGetProperty("html_url", out var html) && html.GetString() is { } href)
            {
                page = href;
            }

            LatestVersion = tag;
            LatestUrl = page;
            HasUpdate = IsNewer(tag, CurrentVersion);
            OnPropertyChanged(nameof(UpdateMessage));

            if (userInitiated)
            {
                if (HasUpdate)
                {
                    Show(
                        $"MonitorSuhu {tag} is available",
                        "Download it from GitHub Releases, then run the installer.",
                        open: page);
                }
                else
                {
                    Show("You're up to date.", $"MonitorSuhu {CurrentVersion} is the latest release.");
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            if (userInitiated)
            {
                Show("Could not check for updates.", ex.Message);
            }
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

    private static void Show(string title, string message, string? open = null)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => Show(title, message, open));
            return;
        }

        if (open is null)
        {
            System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            message + "\n\nOpen the download page?",
            title,
            System.Windows.MessageBoxButton.YesNo);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo(open) { UseShellExecute = true });
        }
    }
}
