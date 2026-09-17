namespace MonitorSuhu.Core.Models;

public enum SensorKind
{
    Cpu,
    Gpu,
    Ssd,
    Board,
    Ram,
    Fan,
    CpuLoad,
    GpuLoad,
    Power
}

public enum CatalogHint
{
    Temp,
    Fan,
    Load,
    Power
}

public static class SensorKindMeta
{
    public static string HudLabel(this SensorKind kind) => kind switch
    {
        SensorKind.Cpu => "CPU",
        SensorKind.Gpu => "GPU",
        SensorKind.Ssd => "SSD",
        SensorKind.Board => "BOARD",
        SensorKind.Ram => "RAM",
        SensorKind.Fan => "FAN",
        SensorKind.CpuLoad => "CPU%",
        SensorKind.GpuLoad => "GPU%",
        SensorKind.Power => "PWR",
        _ => kind.ToString()
    };

    public static CatalogHint Hint(this SensorKind kind) => kind switch
    {
        SensorKind.Fan => CatalogHint.Fan,
        SensorKind.CpuLoad or SensorKind.GpuLoad => CatalogHint.Load,
        SensorKind.Power => CatalogHint.Power,
        _ => CatalogHint.Temp
    };
}

public sealed record SensorReading(string Id, SensorKind Kind, string Label, double Value);

public sealed class HardwareSnapshot
{
    public IReadOnlyList<SensorReading> Readings { get; init; } = [];
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
