using MonitorSuhu.Core.Models;
using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class SanitizeTests
{
    [Fact]
    public void Sanitize_ClampsOpacityIntervalAndHex()
    {
        var settings = new AppSettings
        {
            OverlayOpacity = 2,
            FontSize = 3,
            PollIntervalMs = 50,
            AccentHex = "not-a-color"
        };
        settings.Sanitize();

        Assert.InRange(settings.OverlayOpacity, 0.4, 0.95);
        Assert.InRange(settings.FontSize, 11, 18);
        Assert.InRange(settings.PollIntervalMs, 400, 3000);
        Assert.Equal(AppSettings.NvidiaGreen, settings.AccentHex);
    }

    [Fact]
    public void SetThresholds_WarnAtOrAboveCrit_BumpsCrit()
    {
        var settings = new AppSettings();
        settings.SetThresholds(SensorKind.Cpu, 100, 90);
        var t = settings.ThresholdsFor(SensorKind.Cpu);
        Assert.True(t.Warn < t.Critical);
        Assert.InRange(t.Warn, 1, 120);
        Assert.InRange(t.Critical, 2, 130);
    }

    [Fact]
    public void SetThresholds_FanUsesRpmRange()
    {
        var settings = new AppSettings();
        settings.SetThresholds(SensorKind.Fan, 10, 20);
        var t = settings.ThresholdsFor(SensorKind.Fan);
        Assert.InRange(t.Warn, 500, 8000);
        Assert.InRange(t.Critical, 600, 10000);
        Assert.True(t.Warn < t.Critical);
    }
}
