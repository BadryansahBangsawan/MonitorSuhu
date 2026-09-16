using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using MonitorSuhu.Core;

namespace MonitorSuhu.App.Services;

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
    [ObservableProperty] private double progress;
    [ObservableProperty] private string? lastError;

    public string UpdateMessage =>
        Installing
            ? $"Installing MonitorSuhu {LatestVersion}…"
            : HasUpdate && !string.IsNullOrEmpty(LatestVersion)
                ? $"Version {LatestVersion} is available. You have {CurrentVersion}."
                : $"MonitorSuhu {CurrentVersion}";

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
                    Show("Could not check for updates.", LastError);
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
            LatestAsset = ReleaseAssets.Pick(ParseAssets(doc.RootElement), "windows");
            HasUpdate = Versioning.IsNewer(tag, CurrentVersion);
            OnPropertyChanged(nameof(UpdateMessage));

            if (userInitiated)
            {
                if (HasUpdate)
                    PromptInstall(tag);
                else
                    Show("You're up to date.", $"MonitorSuhu {CurrentVersion} is the latest release.");
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            if (userInitiated)
                Show("Could not check for updates.", ex.Message);
        }
        finally
        {
            Checking = false;
        }
    }

    public async Task ApplyAsync()
    {
        if (Installing) return;
        if (LatestAsset is null)
        {
            OpenDownloadPage();
            return;
        }

        Installing = true;
        Progress = 0;
        LastError = null;
        OnPropertyChanged(nameof(UpdateMessage));
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "MonitorSuhu-update");
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, LatestAsset.Name);
            var progress = new Progress<double>(p =>
            {
                Progress = p;
                OnPropertyChanged(nameof(UpdateMessage));
            });
            await UpdateDownload.ToFileAsync(
                LatestAsset.Url,
                dest,
                $"MonitorSuhu/{CurrentVersion}",
                LatestAsset.Size,
                "windows",
                progress);
            UpdateInstaller.LaunchWindows(dest);
            System.Windows.Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Show("Could not install update.", ex.Message);
        }
        finally
        {
            Installing = false;
            OnPropertyChanged(nameof(UpdateMessage));
        }
    }

    public void OpenDownloadPage()
    {
        var url = string.IsNullOrWhiteSpace(LatestUrl) ? ReleasesPage : LatestUrl;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static bool IsNewer(string latest, string current) =>
        Versioning.IsNewer(latest, current);

    private void PromptInstall(string tag)
    {
        var canInstall = LatestAsset is not null;
        var message = canInstall
            ? "Download and install it now. Your settings stay in AppData."
            : "Download it from GitHub Releases, then run the installer.";
        if (!canInstall)
        {
            Show($"MonitorSuhu {tag} is available", message, open: LatestUrl ?? ReleasesPage);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            message + "\n\nInstall now?",
            $"MonitorSuhu {tag} is available",
            System.Windows.MessageBoxButton.YesNo);
        if (result == System.Windows.MessageBoxResult.Yes)
            _ = ApplyAsync();
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
            Process.Start(new ProcessStartInfo(open) { UseShellExecute = true });
    }
}
