using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class AlertGateTests
{
    [Fact]
    public void FirstCriticalFires_StayCriticalDoesNot_DropThenRiseFires()
    {
        var now = 1_000_000L;
        var gate = new AlertGate(() => now);
        var settings = new AppSettings();
        settings.Sanitize();
        var cpu = new SensorReading("cpu", SensorKind.Cpu, "CPU", 90);

        var first = gate.Evaluate([cpu], settings);
        Assert.Equal("CPU 90°C", Assert.Single(first).Message);

        var second = gate.Evaluate([cpu], settings);
        Assert.Empty(second);

        var stillHot = gate.Evaluate([cpu], settings);
        Assert.Empty(stillHot);

        var cool = new SensorReading("cpu", SensorKind.Cpu, "CPU", 80);
        Assert.Empty(gate.Evaluate([cool], settings));

        var rise = new SensorReading("cpu", SensorKind.Cpu, "CPU", 90);
        Assert.Equal("CPU 90°C", Assert.Single(gate.Evaluate([rise], settings)).Message);
    }

    [Fact]
    public void MutedDoesNotFire()
    {
        var now = 1_000_000L;
        var gate = new AlertGate(() => now);
        var settings = new AppSettings { AlertMuteUntil = now + AlertGate.MuteMs };
        settings.Sanitize();
        var cpu = new SensorReading("cpu", SensorKind.Cpu, "CPU", 94);

        Assert.Empty(gate.Evaluate([cpu], settings));
    }

    [Fact]
    public void DisabledDoesNotFire()
    {
        var gate = new AlertGate(() => 1);
        var settings = new AppSettings { AlertsEnabled = false };
        settings.Sanitize();
        var cpu = new SensorReading("cpu", SensorKind.Cpu, "CPU", 94);

        Assert.Empty(gate.Evaluate([cpu], settings));
    }

    [Fact]
    public void HiddenKindDoesNotFire()
    {
        var gate = new AlertGate(() => 1);
        var settings = new AppSettings { ShowCpu = false };
        settings.Sanitize();
        var cpu = new SensorReading("cpu", SensorKind.Cpu, "CPU", 94);

        Assert.Empty(gate.Evaluate([cpu], settings));
    }

    [Fact]
    public void LoadAndPowerUseFormatValue()
    {
        var gate = new AlertGate(() => 1);
        var settings = new AppSettings { ShowCpuLoad = true, ShowPower = true };
        settings.Sanitize();
        var readings = new[]
        {
            new SensorReading("cpu-load", SensorKind.CpuLoad, "CPU%", 99),
            new SensorReading("power", SensorKind.Power, "PWR", 312)
        };

        var events = gate.Evaluate(readings, settings);
        Assert.Equal(new[] { "CPU% 99%", "PWR 312 W" }, events.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void ReenableWhileStillCriticalDoesNotFire()
    {
        var gate = new AlertGate(() => 1);
        var settings = new AppSettings();
        settings.Sanitize();
        var cpu = new SensorReading("cpu", SensorKind.Cpu, "CPU", 90);

        Assert.Equal("CPU 90°C", Assert.Single(gate.Evaluate([cpu], settings)).Message);

        settings.AlertsEnabled = false;
        Assert.Empty(gate.Evaluate([cpu], settings));

        settings.AlertsEnabled = true;
        Assert.Empty(gate.Evaluate([cpu], settings));
    }
}
