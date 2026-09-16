namespace MonitorSuhu.Core;

public sealed record ReleaseAsset(string Name, string Url, long Size);

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
}
