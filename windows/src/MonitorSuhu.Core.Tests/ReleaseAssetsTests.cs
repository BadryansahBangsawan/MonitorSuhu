using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class ReleaseAssetsTests
{
    private static readonly ReleaseAsset Mac = new(
        "MonitorSuhu-1.0.5-macos.dmg",
        "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.5/MonitorSuhu-1.0.5-macos.dmg",
        2_000_000);

    private static readonly ReleaseAsset Win = new(
        "MonitorSuhu-1.0.5-windows-x64.exe",
        "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.5/MonitorSuhu-1.0.5-windows-x64.exe",
        50_000_000);

    private static readonly ReleaseAsset Linux = new(
        "MonitorSuhu-1.0.5-linux-x64.tar.gz",
        "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.5/MonitorSuhu-1.0.5-linux-x64.tar.gz",
        40_000_000);

    [Fact]
    public void Pick_Macos_TakesDmgNotExe()
    {
        var picked = ReleaseAssets.Pick([Win, Mac, Linux], "macos");
        Assert.Equal(Mac.Name, picked?.Name);
    }

    [Fact]
    public void Pick_Windows_TakesExeNotDmg()
    {
        var picked = ReleaseAssets.Pick([Mac, Win], "windows");
        Assert.Equal(Win.Name, picked?.Name);
    }

    [Fact]
    public void Pick_UnknownPlatform_ReturnsNull()
    {
        Assert.Null(ReleaseAssets.Pick([Mac, Win], "amiga"));
    }

    [Fact]
    public void IsTrusted_RejectsHttpAndPath()
    {
        Assert.False(ReleaseAssets.IsTrusted("http://github.com/a/b/MonitorSuhu-1.0.5-macos.dmg", Mac.Name, "macos"));
        Assert.False(ReleaseAssets.IsTrusted(Mac.Url, "../MonitorSuhu-1.0.5-macos.dmg", "macos"));
        Assert.False(ReleaseAssets.IsTrusted("https://evil.example/MonitorSuhu-1.0.5-macos.dmg", Mac.Name, "macos"));
        Assert.True(ReleaseAssets.IsTrusted(Mac.Url, Mac.Name, "macos"));
    }

    [Fact]
    public void LatestFor_SkipsOtherPlatforms()
    {
        var winOnly = new GitHubRelease("v1.0.8", "https://github.com/x", [Win]);
        var both = new GitHubRelease("v1.0.7", "https://github.com/y", [Mac, Win]);
        Assert.Equal("1.0.7", ReleaseAssets.LatestFor([winOnly, both], "macos")?.Tag);
        Assert.Equal("1.0.8", ReleaseAssets.LatestFor([winOnly, both], "windows")?.Tag);
        Assert.Null(ReleaseAssets.LatestFor([winOnly, both], "linux"));
    }

    [Fact]
    public void LatestFor_Linux_TakesTarball()
    {
        var winOnly = new GitHubRelease("v1.0.8", "https://github.com/x", [Win]);
        var withLinux = new GitHubRelease("v1.0.10", "https://github.com/y", [Mac, Win, Linux]);
        Assert.Equal("1.0.10", ReleaseAssets.LatestFor([winOnly, withLinux], "linux")?.Tag);
    }
}
