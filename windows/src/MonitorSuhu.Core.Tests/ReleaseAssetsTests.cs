using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class ReleaseAssetsTests
{
    private static readonly ReleaseAsset Mac = new(
        "MonitorSuhu-1.0.4-macos.dmg",
        "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.4/MonitorSuhu-1.0.4-macos.dmg",
        2_000_000);

    private static readonly ReleaseAsset Win = new(
        "MonitorSuhu-1.0.4-windows-x64.exe",
        "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.4/MonitorSuhu-1.0.4-windows-x64.exe",
        50_000_000);

    private static readonly ReleaseAsset Linux = new(
        "MonitorSuhu-1.0.4-linux-x64.tar.gz",
        "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.4/MonitorSuhu-1.0.4-linux-x64.tar.gz",
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
        Assert.False(ReleaseAssets.IsTrusted("http://github.com/a/b/MonitorSuhu-1.0.4-macos.dmg", Mac.Name, "macos"));
        Assert.False(ReleaseAssets.IsTrusted(Mac.Url, "../MonitorSuhu-1.0.4-macos.dmg", "macos"));
        Assert.False(ReleaseAssets.IsTrusted("https://evil.example/MonitorSuhu-1.0.4-macos.dmg", Mac.Name, "macos"));
        Assert.True(ReleaseAssets.IsTrusted(Mac.Url, Mac.Name, "macos"));
    }
}
