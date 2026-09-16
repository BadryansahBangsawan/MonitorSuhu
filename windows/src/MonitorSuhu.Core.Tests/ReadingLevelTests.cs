using MonitorSuhu.Core.Models;
using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class ReadingLevelTests
{
    [Fact]
    public void Worst_CpuCriticalFanSpinning_UsesCpuNotRpm()
    {
        var settings = new AppSettings();
        settings.Sanitize();
        var readings = new[]
        {
            new SensorReading("cpu", SensorKind.Cpu, "CPU", 90),
            new SensorReading("fan", SensorKind.Fan, "FAN", 2000)
        };

        Assert.Equal(ReadingLevel.Critical, ReadingLevelUtil.Worst(readings, settings));
        Assert.Equal(ReadingLevel.Ok, ReadingLevelUtil.Of(readings[1], settings));
    }

    [Fact]
    public void Of_WarnBand()
    {
        var t = new Thresholds { Warn = 75, Critical = 90 };
        Assert.Equal(ReadingLevel.Ok, ReadingLevelUtil.Of(74, t));
        Assert.Equal(ReadingLevel.Warn, ReadingLevelUtil.Of(75, t));
        Assert.Equal(ReadingLevel.Critical, ReadingLevelUtil.Of(90, t));
    }
}
