using MonitorSuhu.Sensors;
using Xunit;

namespace MonitorSuhu.Sensors.Tests;

public sealed class PickBoardTests
{
    [Fact]
    public void PrefersSystemOverCpuNamed()
    {
        var value = SensorMapper.PickBoard(
        [
            ("CPU", 88),
            ("System", 42)
        ]);
        Assert.Equal(42, value);
    }

    [Fact]
    public void CpuNamedIsLastResort()
    {
        var value = SensorMapper.PickBoard([("CPU Package", 88)]);
        Assert.Equal(88, value);
    }

    [Fact]
    public void SkipsCoreNamedWhenAmbientExists()
    {
        var value = SensorMapper.PickBoard(
        [
            ("Core 0", 91),
            ("Ambient", 37)
        ]);
        Assert.Equal(37, value);
    }
}
