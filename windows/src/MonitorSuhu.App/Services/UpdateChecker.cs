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
    private const string Api = "https://api.github.com/repos/BadryansahBangsawan/MonitorSuhu/releases?per_page=30";

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
            ? Progress < 1
                ? $"Downloading MonitorSuhu {LatestVersion}…"
                : $"Installing MonitorSuhu {LatestVersion}…"
            : HasUpdate && !string.IsNullOrEmpty(LatestVersion)
                ? $"MonitorSuhu {LatestVersion} is ready."
                : $"MonitorSuhu {CurrentVersion}";

    public string UpdateHint =>
        Installing
            ? "The app will close to finish. Open MonitorSuhu again from the Start menu if it does not restart."
            : HasUpdate
                ? "Install from here. Settings stay in AppData. The app will close — reopen it if it does not come back."
                : "";

    partial void OnInstallingChanged(bool value) => NotifyCopy();
    partial void OnProgressChanged(double value) => NotifyCopy();
    partial void OnHasUpdateChanged(bool value) => NotifyCopy();

    private void NotifyCopy()
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
                    Show("Could not check for updates.", LastError);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            var hit = ReleaseAssets.LatestFor(ParseReleases(doc.RootElement), "windows");
            if (hit is null)
            {
                LatestVersion = null;
                LatestUrl = ReleasesPage;
                LatestAsset = null;
                HasUpdate = false;
                LastError = "No Windows build on GitHub Releases.";
                OnPropertyChanged(nameof(UpdateMessage));
                OnPropertyChanged(nameof(UpdateHint));
                if (userInitiated)
                    Show("No Windows update.", "GitHub latest has no Windows installer. macOS or Linux-only releases are ignored here.");
                return;
            }

            LatestVersion = hit.Value.Tag;
            LatestUrl = string.IsNullOrWhiteSpace(hit.Value.HtmlUrl) ? ReleasesPage : hit.Value.HtmlUrl;
            LatestAsset = hit.Value.Asset;
            HasUpdate = Versioning.IsNewer(hit.Value.Tag, CurrentVersion);
            OnPropertyChanged(nameof(UpdateMessage));
            OnPropertyChanged(nameof(UpdateHint));

            if (userInitiated)
            {
                if (HasUpdate)
                    PromptInstall(hit.Value.Tag);
                else
                    Show("You're up to date.", $"MonitorSuhu {CurrentVersion} is the latest Windows release.");
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
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MonitorSuhu-update");
            System.IO.Directory.CreateDirectory(dir);
            var dest = System.IO.Path.Combine(dir, LatestAsset.Name);
            var progress = new Progress<double>(p => Progress = p);
            await UpdateDownload.ToFileAsync(
                LatestAsset.Url,
                dest,
                $"MonitorSuhu/{CurrentVersion}",
                LatestAsset.Size,
                "windows",
                progress);
            Progress = 1;
            await Task.Delay(1800);
            UpdateInstaller.LaunchWindows(dest);
            System.Windows.Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Installing = false;
            Show("Could not install update.", ex.Message);
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
            ? "Download and install it now. Settings stay in AppData. The app will close — open MonitorSuhu again from the Start menu if it does not restart."
            : "Download it from GitHub Releases, run the installer, then open MonitorSuhu again.";
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

    private static List<GitHubRelease> ParseReleases(JsonElement root)
    {
        var list = new List<GitHubRelease>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (ParseRelease(item) is { } release)
                    list.Add(release);
            }
        }
        else if (ParseRelease(root) is { } one)
        {
            list.Add(one);
        }
        return list;
    }

    private static GitHubRelease? ParseRelease(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("tag_name", out var tagEl)) return null;
        var tag = tagEl.GetString() ?? "";
        if (string.IsNullOrEmpty(tag)) return null;
        string? page = null;
        if (root.TryGetProperty("html_url", out var html))
            page = html.GetString();
        var draft = root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True;
        var pre = root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True;
        return new GitHubRelease(tag, page, ParseAssets(root), draft, pre);
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
