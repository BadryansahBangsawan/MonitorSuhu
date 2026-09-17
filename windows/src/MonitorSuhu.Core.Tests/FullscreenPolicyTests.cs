using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class FullscreenPolicyTests
{
    [Fact]
    public void FullscreenHidesWhenEnabled()
    {
        var settings = new AppSettings { HideInFullscreen = true, HideDuringCapture = false };
        Assert.True(FullscreenPolicy.ShouldHide(settings, fullscreen: true, capturing: false));
        Assert.False(FullscreenPolicy.ShouldHide(settings, fullscreen: false, capturing: false));
    }

    [Fact]
    public void CaptureHidesWhenEnabled()
    {
        var settings = new AppSettings { HideInFullscreen = false, HideDuringCapture = true };
        Assert.True(FullscreenPolicy.ShouldHide(settings, fullscreen: false, capturing: true));
        Assert.False(FullscreenPolicy.ShouldHide(settings, fullscreen: false, capturing: false));
    }

    [Fact]
    public void DisabledFlagsNeverHide()
    {
        var settings = new AppSettings { HideInFullscreen = false, HideDuringCapture = false };
        Assert.False(FullscreenPolicy.ShouldHide(settings, fullscreen: true, capturing: true));
    }

    [Fact]
    public void RectCoversMonitorAllowsTwoPixelSlack()
    {
        Assert.True(FullscreenPolicy.RectCoversMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1080));
        Assert.True(FullscreenPolicy.RectCoversMonitor(0, 0, 1918, 1080, 0, 0, 1920, 1080));
        Assert.False(FullscreenPolicy.RectCoversMonitor(16, 16, 1904, 1064, 0, 0, 1920, 1080));
    }

    [Fact]
    public void CoversScreenMatchesPointsOrBackingPixels()
    {
        Assert.True(FullscreenPolicy.CoversScreen(1440, 900, 1440, 900, 2));
        Assert.True(FullscreenPolicy.CoversScreen(2880, 1800, 1440, 900, 2));
        Assert.False(FullscreenPolicy.CoversScreen(400, 200, 1440, 900, 2));
    }

    [Fact]
    public void CaptureOwnerDetectsScreencaptureUi()
    {
        Assert.True(FullscreenPolicy.IsCaptureOwner("screencaptureui"));
        Assert.True(FullscreenPolicy.IsCaptureOwner("screencapture"));
        Assert.False(FullscreenPolicy.IsCaptureOwner("Safari"));
        Assert.False(FullscreenPolicy.IsCaptureOwner(null));
    }
}
