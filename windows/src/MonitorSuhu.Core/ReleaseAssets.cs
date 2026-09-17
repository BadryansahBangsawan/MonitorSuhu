namespace MonitorSuhu.Core;

public sealed record ReleaseAsset(string Name, string Url, long Size);

public sealed record GitHubRelease(
    string Tag,
    string? HtmlUrl,
    IReadOnlyList<ReleaseAsset> Assets,
    bool Draft = false,
    bool Prerelease = false);

public static class ReleaseAssets
{
    public const long MaxBytes = 200L * 1024 * 1024;

    public static string CurrentPlatform =>
        OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "macos"
        : OperatingSystem.IsLinux() ? "linux"
        : "unknown";

    public static ReleaseAsset? Pick(IEnumerable<ReleaseAsset> assets, string? platform = null)
    {
        var os = (platform ?? CurrentPlatform).Trim().ToLowerInvariant();
        foreach (var asset in assets)
        {
            if (Matches(asset.Name, os) && IsTrusted(asset.Url, asset.Name, os))
                return asset;
        }
        return null;
    }

    public static bool Matches(string name, string platform)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.ToLowerInvariant();
        if (!n.Contains("monitorsuhu")) return false;
        return platform switch
        {
            "macos" => n.Contains("macos") && n.EndsWith(".dmg"),
            "windows" => n.Contains("windows") && n.EndsWith(".exe"),
            "linux" => n.Contains("linux") && (n.EndsWith(".tar.gz") || n.EndsWith(".zip")),
            _ => false
        };
    }

    public static bool IsTrusted(string url, string name, string platform)
    {
        if (!Matches(name, platform)) return false;
        if (name.IndexOfAny(['/', '\\']) >= 0) return false;
        if (name.Contains("..", StringComparison.Ordinal)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        var host = uri.Host.ToLowerInvariant();
        return host is "github.com"
            or "objects.githubusercontent.com"
            or "release-assets.githubusercontent.com";
    }

    /// Newest non-draft release that ships a file for this OS.
    /// A Windows-only tag is not "latest" on macOS or Linux.
    public static (string Tag, string? HtmlUrl, ReleaseAsset Asset)? LatestFor(
        IEnumerable<GitHubRelease> releases,
        string? platform = null)
    {
        var os = (platform ?? CurrentPlatform).Trim().ToLowerInvariant();
        foreach (var release in releases)
        {
            if (release.Draft || release.Prerelease) continue;
            var asset = Pick(release.Assets, os);
            if (asset is null) continue;
            var tag = release.Tag.StartsWith('v') ? release.Tag[1..] : release.Tag;
            return (Versioning.Trim(tag), release.HtmlUrl, asset);
        }
        return null;
    }
}
