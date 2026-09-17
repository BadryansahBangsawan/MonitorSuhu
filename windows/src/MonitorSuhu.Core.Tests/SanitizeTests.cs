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

    [Fact]
    public void SetThresholds_LoadUsesPercentRange()
    {
        var settings = new AppSettings();
        settings.SetThresholds(SensorKind.CpuLoad, 0, 200);
        var t = settings.ThresholdsFor(SensorKind.CpuLoad);
        Assert.InRange(t.Warn, 1, 100);
        Assert.InRange(t.Critical, 2, 100);
        Assert.True(t.Warn < t.Critical);
    }

    [Fact]
    public void SetThresholds_PowerUsesWattRange()
    {
        var settings = new AppSettings();
        settings.SetThresholds(SensorKind.Power, 1, 2);
        var t = settings.ThresholdsFor(SensorKind.Power);
        Assert.InRange(t.Warn, 5, 800);
        Assert.InRange(t.Critical, 10, 1000);
        Assert.True(t.Warn < t.Critical);
    }

    [Fact]
    public void FormatValue_UsesKindUnits()
    {
        var settings = new AppSettings();
        Assert.Equal("72°C", settings.FormatValue(new SensorReading("cpu", SensorKind.Cpu, "CPU", 72)));
        Assert.Equal("2100 RPM", settings.FormatValue(new SensorReading("fan", SensorKind.Fan, "FAN", 2100)));
        Assert.Equal("42%", settings.FormatValue(new SensorReading("cpu-load", SensorKind.CpuLoad, "CPU%", 42)));
        Assert.Equal("99%", settings.FormatValue(new SensorReading("gpu-load", SensorKind.GpuLoad, "GPU%", 99)));
        Assert.Equal("125 W", settings.FormatValue(new SensorReading("power", SensorKind.Power, "PWR", 125)));
    }

    [Fact]
    public void ExtraHudRows_DefaultOff()
    {
        var settings = new AppSettings();
        Assert.False(settings.ShowCpuLoad);
        Assert.False(settings.ShowGpuLoad);
        Assert.False(settings.ShowPower);
        Assert.False(settings.IsKindVisible(SensorKind.CpuLoad));
        Assert.True(settings.IsKindVisible(SensorKind.Cpu));
    }
}
