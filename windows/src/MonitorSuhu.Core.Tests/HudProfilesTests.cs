using MonitorSuhu.Core.Models;
using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class HudProfilesTests
{
    [Fact]
    public void ApplyGameThenMutatingOpacityMarksCustom()
    {
        var settings = new AppSettings { OverlayVisible = true, Locked = true, PollIntervalMs = 1000 };
        var toggle = settings.ToggleHotkey;
        HudProfiles.Apply(settings, HudProfiles.Game);

        Assert.Equal(HudProfiles.Game, settings.ActiveProfile);
        Assert.True(settings.CompactHud);
        Assert.True(settings.ShowCpuLoad);
        Assert.True(settings.ShowGpuLoad);
        Assert.True(settings.ShowPower);
        Assert.False(settings.ShowSsd);
        Assert.False(settings.ShowBoard);
        Assert.True(settings.ShowFan);
        Assert.Equal(0.70, settings.OverlayOpacity, 3);
        Assert.Equal(12, settings.FontSize);
        Assert.Equal(80, settings.ThresholdsFor(SensorKind.Cpu).Warn);
        Assert.Equal(95, settings.ThresholdsFor(SensorKind.Cpu).Critical);
        Assert.True(settings.OverlayVisible);
        Assert.True(settings.Locked);
        Assert.Equal(1000, settings.PollIntervalMs);
        Assert.True(toggle.Equals(settings.ToggleHotkey));

        settings.OverlayOpacity = 0.90;
        HudProfiles.MarkCustom(settings);
        Assert.Equal(HudProfiles.Custom, settings.ActiveProfile);
    }

    [Fact]
    public void ApplyDesktopLeavesBindingsAlone()
    {
        var settings = new AppSettings { SensorBindings = new() { ["Cpu"] = "chip/temp1" } };
        HudProfiles.Apply(settings, HudProfiles.Desktop);
        Assert.Equal("chip/temp1", settings.SensorBindings["Cpu"]);
        Assert.False(settings.CompactHud);
        Assert.False(settings.ShowCpuLoad);
        Assert.True(settings.ShowBoard);
    }

    [Fact]
    public void ApplySilentShowsCpuGpuSsdOnly()
    {
        var settings = new AppSettings();
        HudProfiles.Apply(settings, HudProfiles.Silent);
        Assert.True(settings.ShowCpu);
        Assert.True(settings.ShowGpu);
        Assert.True(settings.ShowSsd);
        Assert.False(settings.ShowBoard);
        Assert.False(settings.ShowRam);
        Assert.False(settings.ShowFan);
        Assert.False(settings.ShowCpuLoad);
    }
}
